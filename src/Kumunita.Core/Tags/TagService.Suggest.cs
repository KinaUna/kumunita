using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Query;
using Kumunita.Core.Posts;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Tags;

/// <summary>
/// The tag suggestion + standing-check surface — the
/// <c>SuggestAsync</c> prefix-suggestion read (with its
/// <c>BuildTagItemsAsync</c> / <c>MatchesPrefix</c> helpers) and the
/// <c>CanAttachTo*</c> / <c>CanTranslateTag</c> standing checks (ADR 0006
/// module-boundary, the tag attach/translate standing matrix). A
/// partial class of <see cref="TagService"/> extracted so the main
/// file stays under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class TagService
{
    public async Task<IReadOnlyList<TagItem>> SuggestAsync(
        string prefix, string actorId, string? preferredLanguageCode = null)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));

        await using var session = _store.QuerySession();
        var (readablePosts, readablePages) = await LoadActorReadableContentAsync(actorId, session);

        var tagIds = CollectReadableTagIds(readablePosts, readablePages);
        if (tagIds.Count == 0) return Array.Empty<TagItem>();

        var pfx = (prefix ?? string.Empty).Trim().ToLowerInvariant();
        var items = await BuildTagItemsAsync(session, tagIds, readablePosts, readablePages, preferredLanguageCode);
        return items
            .Where(i => MatchesPrefix(i, pfx))
            .OrderBy(i => i.DisplayedName, StringComparer.Ordinal)
            .ThenBy(i => i.Tag.Slug, StringComparer.Ordinal)
            .Take(10)   // C-TG·2 / F10 — the ≤ 10 cap
            .ToList();
    }

    // ── Read-lane helpers (private — not part of the §2.1 11-member surface) ──

    /// <summary>
    /// The C-TG·2 base query's candidate load + content-Read scoping: the posts
    /// (community + group) and <c>PageKind.User</c> blog pages carrying ≥ 1 tag,
    /// each passed through its **own** <c>Read</c> decision (the ADR 0035
    /// <c>PostReadDecision</c> routing applied in Core: group posts →
    /// <see cref="IAuthorizationService.CanSeeGroupAsync"/> (the ADR 0013
    /// membership lane, C-TG·3); community posts / blog pages →
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// (the ADR 0001 audience lane)). The content's own Read-decision rows are
    /// the content's (D7); the tag lane adds none (C-TG·8).
    /// </summary>
    private async Task<(IReadOnlyList<Post> posts, IReadOnlyList<Page> pages)>
        LoadActorReadableContentAsync(string actorId, IQuerySession session)
    {
        var allPosts = await session.Query<Post>().ToListAsync();
        var communityPosts = allPosts
            .Where(p => string.IsNullOrEmpty(p.GroupId) && p.TagIds is { Count: > 0 })
            .ToList();
        var groupPosts = allPosts
            .Where(p => !string.IsNullOrEmpty(p.GroupId) && p.TagIds is { Count: > 0 })
            .ToList();

        var readablePosts = new List<Post>();
        foreach (var post in communityPosts)
        {
            var decision = await _authz.CanAsync(actorId, AccessAction.Read, new PostToAuditableResource(post));
            if (decision.Allowed) readablePosts.Add(post);
        }
        foreach (var post in groupPosts)
        {
            var decision = await _authz.CanSeeGroupAsync(actorId, post.GroupId, post.Id);
            if (decision.Allowed) readablePosts.Add(post);
        }

        var allPages = await session.Query<Page>().ToListAsync();
        var blogPages = allPages
            .Where(p => p.Kind == PageKind.User && p.TagIds is { Count: > 0 })
            .ToList();

        var readablePages = new List<Page>();
        foreach (var page in blogPages)
        {
            var decision = await _authz.CanAsync(actorId, AccessAction.Read, new PageToAuditableResource(page));
            if (decision.Allowed) readablePages.Add(page);
        }

        return (readablePosts, readablePages);
    }

    /// <summary>The distinct tag ids used on the readable content (the base query).</summary>
    private static HashSet<string> CollectReadableTagIds(IReadOnlyList<Post> posts, IReadOnlyList<Page> pages)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in posts) foreach (var id in p.TagIds) ids.Add(id);
        foreach (var p in pages) foreach (var id in p.TagIds) ids.Add(id);
        return ids;
    }

    /// <summary>
    /// Resolves the tags + their translations + the use-count + the display name
    /// (the ADR 0005 preference order: the viewer's effective language → the
    /// <see cref="TagTranslation"/> for it → the base <see cref="Tag.Name"/>)
    /// into <see cref="TagItem"/> rows.
    /// <para>
    /// <paramref name="preferredLanguageCode"/> is the actor's **per-request
    /// effective** language (the Web's <c>EffectiveLanguageCode</c> chain) — the
    /// top of the ADR 0005 preference order. When it resolves to a concrete
    /// enabled code (the usual Web call path), that code is the effective
    /// language for the tag-name lookup. When it is null or blank (a Core-only
    /// call with no per-request signal), the provider falls through to the
    /// instance default → <c>en</c> floor — the exact behavior the seam's own
    /// tests pin (the "Core collapses to the instance default" idiom).
    /// </para>
    /// </summary>
    private async Task<List<TagItem>> BuildTagItemsAsync(
        IQuerySession session, HashSet<string> tagIds,
        IReadOnlyList<Post> readablePosts, IReadOnlyList<Page> readablePages,
        string? preferredLanguageCode = null)
    {
        var tags = await session.Query<Tag>().Where(t => tagIds.Contains(t.Id)).ToListAsync();
        var translations = await session.Query<TagTranslation>().Where(t => tagIds.Contains(t.TagId)).ToListAsync();

        // The viewer's effective language (the ADR 0005 preference order —
        // the per-request preference when the Web threaded one in, else the
        // instance default → en floor).
        var effective = await _translations.ResolveEffectiveLanguageAsync(preferredLanguageCode);

        var nameByTag = translations
            .Where(t => string.Equals(t.LanguageCode, effective, StringComparison.Ordinal))
            .ToDictionary(t => t.TagId, t => t.Name, StringComparer.Ordinal);

        var useCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in tagIds) useCount[id] = 0;
        foreach (var p in readablePosts) foreach (var id in p.TagIds) if (useCount.ContainsKey(id)) useCount[id]++;
        foreach (var p in readablePages) foreach (var id in p.TagIds) if (useCount.ContainsKey(id)) useCount[id]++;

        return tags
            .Select(t => new TagItem(t, useCount[t.Id], nameByTag.TryGetValue(t.Id, out var n) ? n : t.Name))
            .ToList();
    }

    /// <summary>
    /// The F9/F10 autocomplete filter — <c>starts_with(displayName, prefix) OR
    /// starts_with(slug, prefix)</c>, where <c>displayName</c> is the name
    /// resolved in the viewer's language (D5); a blank prefix matches all (the
    /// top-of-list autocomplete).
    /// </summary>
    private static bool MatchesPrefix(TagItem item, string prefix)
    {
        if (prefix.Length == 0) return true;
        return item.DisplayedName.StartsWith(prefix, StringComparison.Ordinal)
               || item.Tag.Slug.StartsWith(prefix, StringComparison.Ordinal);
    }

    // ── Standing probes (C-TG·5; a display pin, not a gate — the real deny
    //    is the write-lane re-check, the PostService.CanAddTranslation /
    //    PageStandingPolicy.CanTranslatePage idiom) ──

    /// <inheritdoc />
    public bool CanAttachToPost(Post post, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(roles);
        return string.Equals(post.AuthorId, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

    /// <inheritdoc />
    public bool CanAttachToPage(Page page, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(roles);
        if (page.Kind == PageKind.System)
            return false;
        return string.Equals(page.AuthorId, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

    /// <inheritdoc />
    public bool CanAttachToDocument(
        Document document, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(roles);
        return string.Equals(document.OwnerId, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

    /// <inheritdoc />
    public bool CanAttachToTodo(
        Projects.TodoItem todo, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(todo);
        ArgumentNullException.ThrowIfNull(roles);
        // The to-do's own edit lane (creator ∪ assignee ∪ GlobalAdmin — the
        // <c>ProjectService.CheckTodoStanding</c> shape, ADR 0147):
        return string.Equals(todo.AuthorId, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin)
               || (!string.IsNullOrEmpty(todo.AssigneeId)
                   && string.Equals(todo.AssigneeId, actorId, StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public bool CanAttachToBoard(
        Projects.KanbanBoard board, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(roles);
        // The board's own edit lane (creator ∪ GlobalAdmin — the
        // <c>ProjectService.CheckBoardStanding</c> shape, ADR 0147):
        return string.Equals(board.AuthorId, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

    /// <inheritdoc />
    public bool CanTranslateTag(Tag tag, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(roles);
        return string.Equals(tag.CreatedBy, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
    }

}
