using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.Tags;
using Kumunita.Core.Translations;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Projects;

/// <summary>
/// C3-03 (ADR 0293) — the **to-do comment** lane (ADR 0100 / C-M3·1),
/// extracted out of <see cref="TodoService"/> as a real part-vs-whole surface
/// (the C3-00 <c>EventRsvpGuardianService</c> pattern; the
/// <see cref="ProjectsStore"/> base supplies the shared <c>_store</c> /
/// <c>_authorization</c> / <c>_mentions</c> / <c>_notifications</c> + the
/// <c>ResolveLanguageCodeAsync</c> ADR 0018 helper). The two comment methods
/// are a **named domain concept** with their own standing read (the to-do's
/// single <c>Read</c> decision, C-M3·1), their own reply-parent validation
/// (ADR 0100 / C-M5·7), their own mention-emitter lane (M36 / ADR 0165), and
/// their own author-only soft-delete (ADR 0016 / ADR 0024) — a C13 cold read of
/// this type passes on its own. The over-ceiling <c>CreateTodoCommentAsync</c>
/// (68/cc18) lives here, so its ratchet entry **re-points** to this named seam
/// (the C3-00 <c>RsvpAsync</c> re-point) rather than stay on the god-service.
/// </summary>
public sealed class TodoCommentService : ProjectsStore, ITodoCommentService
{
    public TodoCommentService(
        IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        INotificationEmitter? notifications = null, ITranslationProvider? translator = null,
        ILocalizationService? localization = null, ITagService? tags = null,
        Kumunita.Core.Mentions.IMentionService? mentions = null)
        : base(store, authorization, userInfo, notifications, translator, localization, tags, mentions)
    {
    }

    /// <inheritdoc />
    public async Task<TodoComment> CreateTodoCommentAsync(
        string todoId, string actorId, IReadOnlySet<string> actorRoles,
        string body, string? languageCode, string? parentId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("A comment body is required.", nameof(body));
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to comment on a to-do.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        // C3 — the write session owns both the domain write and the audit row
        // (the M5 self-composed-session convention, ADR 0067 §4).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var todo = await session.LoadAsync<TodoItem>(todoId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoId}' was not found in the session; nothing to comment on.");
        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoId}' was not found in the session; nothing to comment on.");

        // Standing (C-M3·1): the to-do's single Read decision is the access
        // boundary — a comment inherits it. There is no comment-level standing
        // matrix; the audience decision here is the gate (the GetTodoAsync
        // shape, run against the stored row).
        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new TodoItemToAuditableResource(todo))
            .ConfigureAwait(false);
        if (!decision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not comment on to-do '{todoId}'.");

        // ADR 0100 / C-M5·7 — the reply parent (when non-null) must exist, be
        // live, and be on the **same** to-do (a parent on a different to-do is
        // a 404 — non-leaky; the C3 404-vs-403 split). The parent's own
        // DeletedAt is the ADR 0024 soft-delete flag.
        string? parentIdOrNull = null;
        if (!string.IsNullOrEmpty(parentId))
        {
            var parent = await session.LoadAsync<TodoComment>(parentId, ct).ConfigureAwait(false);
            if (parent is null)
                throw new KeyNotFoundException($"Comment '{parentId}' (the would-be parent) was not found; a reply parent must exist.");
            if (parent.DeletedAt is not null)
                throw new KeyNotFoundException($"Comment '{parentId}' (the would-be parent) was not found; a reply parent must not be soft-deleted.");
            if (!string.Equals(parent.TodoId, todoId, StringComparison.Ordinal))
                throw new KeyNotFoundException($"Comment '{parentId}' (the would-be parent) is not on to-do '{todoId}'; a reply parent must be on the same to-do.");
            parentIdOrNull = parentId;
        }

        var comment = new TodoComment
        {
            Id = Guid.NewGuid().ToString("N"),
            TodoId = todoId,
            ParentId = parentIdOrNull,                  // C-M5·7 — the sole hierarchy mechanism (null = top-level).
            AuthorId = actorId,
            Body = body,
            Created = DateTimeOffset.UtcNow,
            LanguageCode = await ResolveLanguageCodeAsync(languageCode, session, ct).ConfigureAwait(false) // ADR 0018 — the authored-in tag (instance-default floor).
        };

        session.Store(comment);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.comment.create", TargetKindTodo, todoId, AccessVia.Owner));

        // M36 (ADR 0165, U05) — the to-do comment mention emitter (Part 2.3
        // lane 7, `todo.mention`). Additive, *inside* the lane (the frozen
        // method signature + audit row untouched): extract candidate
        // `@`-tokens from the comment body, resolve them against the frozen
        // directory read (the author is never a recipient — M36-5 self pin),
        // and emit one `todo.mention` notification per resolved recipient on
        // the write session. The per-recipient idempotency key (M36·4) keeps a
        // re-emission of the same (comment, recipient) pair a no-op (ADR 0076
        // D4 / F10). `EmitAsync` does not commit; the `SaveChangesAsync`
        // below is the single commit (C3). No new authorization surface, no
        // `AccessAudit` row (M36·5 — a mention is a personal read).
        if (_mentions is not null && _notifications is not null)
        {
            var mentionTokens = Kumunita.Core.Mentions.MentionTokens.ExtractTokens(body);
            if (mentionTokens.Count > 0)
            {
                var mentionRecipients = await _mentions.ResolveAsync(mentionTokens, actorId, ct)
                    .ConfigureAwait(false);
                foreach (var recipientId in mentionRecipients)
                {
                    await _notifications.EmitAsync(
                        session,
                        recipientId: recipientId,
                        kind: Notifications.NotificationKinds.TodoMention,
                        idempotencyKey: $"notification:todo.mention:{comment.Id}:{recipientId}",
                        body: Notifications.UgcSnippets.Truncate(body),
                        targetId: null,
                        linkPath: $"/projects/todos/{todoId}#comment-{comment.Id}",
                        ct: ct).ConfigureAwait(false);
                }
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return comment;
    }

    /// <inheritdoc />
    public async Task<TodoComment> DeleteTodoCommentAsync(
        string todoId, string commentId, string actorId, IReadOnlySet<string> actorRoles,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(todoId)) throw new KeyNotFoundException("A to-do id is required.");
        if (string.IsNullOrEmpty(commentId)) throw new KeyNotFoundException("A comment id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to delete a comment.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

        var todo = await session.LoadAsync<TodoItem>(todoId, ct).ConfigureAwait(false);
        if (todo is null)
            throw new KeyNotFoundException($"To-do '{todoId}' was not found in the session; nothing to delete a comment on.");
        if (todo.IsDeleted)
            throw new KeyNotFoundException($"To-do '{todoId}' was not found in the session; nothing to delete a comment on.");

        var comment = await session.LoadAsync<TodoComment>(commentId, ct).ConfigureAwait(false);
        if (comment is null)
            throw new KeyNotFoundException($"Comment '{commentId}' was not found in the session; nothing to delete.");
        if (!string.Equals(comment.TodoId, todoId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Comment '{commentId}' is not on to-do '{todoId}'; nothing to delete.");

        // Standing (ADR 0016 / ADR 0024 shape): author-only. A non-author is
        // refused before any write (the ADR 0016 reply-delete precedent — no
        // moderator / GlobalAdmin branch on a comment's own delete).
        if (!string.Equals(comment.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException($"Actor is not the author of comment '{commentId}'.");

        comment.DeletedAt = DateTimeOffset.UtcNow;       // ADR 0024 — the soft-delete stamp.
        session.Store(comment);
        session.Store(AccessAuditFactory.SingleTarget(actorId, "todo.comment.delete", TargetKindTodo, todoId, AccessVia.Owner));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return comment;
    }
}
