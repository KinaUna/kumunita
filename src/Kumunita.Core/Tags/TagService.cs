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
/// The <c>TG</c> lane's service (U5: the attach / create / translate lane +
/// the three standing probes + the <see cref="DeriveSlug"/> helper; U6: the
/// four read-lane methods). <see cref="ListForActorAsync"/> /
/// <see cref="ListPostsByTagPagedAsync"/> / <see cref="ListPagesByTagPagedAsync"/> /
/// <see cref="SuggestAsync"/> are all four callers of the one C-TG·2 base
/// query over the actor's readable content (the content's own <c>Read</c>
/// decision is the gate, C-TG·1/C-TG·3; the tag lane writes no tag-family
/// <c>AccessAudit</c> row of its own, C-TG·8/D7).
/// <para>
/// **C3 idiom:** every write takes the **caller's**
/// <see cref="IDocumentSession"/> (the write and the in-session
/// <c>AccessAudit</c> row commit or roll back atomically — the
/// <see cref="Posts.PostService.PostTranslationLane"/> shape). One
/// <c>SaveChangesAsync</c> per write.
/// </para>
/// <para>
/// **Standing (C-TG·5, D4):** attach = the object's existing edit standing
/// (author ∪ GlobalAdmin — ADR 0014 / 0016 for posts, ADR 0040 for pages;
/// the ADR 0013 group lane has no component-moderator standing, ADR 0007);
/// translate = the tag's <c>CreatedBy</c> ∪ GlobalAdmin only. A denied actor
/// throws <see cref="UnauthorizedAccessException"/> **before** anything is
/// stored (C-TG·9).
/// </para>
/// <para>
/// **Via (ADR 0006-D lane pin):** only <see cref="AccessVia.Owner"/> (the
/// actor is the author / creator) or <see cref="AccessVia.Admin"/> (the
/// actor is a GlobalAdmin). Never <c>Moderator</c> / <c>BreakGlass</c> /
/// <c>Group</c> / any other value — the tag lane composes only the frozen
/// seams (the ADR 0006-D pin).
/// </para>
/// </summary>
public partial class TagService : ITagService
{
    // ADR 0090 D4 — the M7 paged by-tag feeds' page size (the PostService /
    // EventService / ProjectService `PageSize = 30` precedent).
    private const int PageSize = 30;

    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authz;
    private readonly ITranslationProvider _translations;

    /// <summary>
    /// The constructor composes the host-registered <see cref="IDocumentStore"/>
    /// (the write lane's C3 audit rows + the read lane's C-TG·2 base query)
    /// with the **frozen** seams the read lane needs (the ADR 0006-D lane pin —
    /// composes only the frozen modules, never opens a new seam, never a new
    /// <c>AccessVia</c> value beyond <c>Owner</c> / <c>Admin</c>):
    /// <see cref="IAuthorizationService"/> (the content's own <c>Read</c>
    /// decision — C-TG·3 group-lane exclusion, C-TG·2 privacy-pin; the
    /// ADR 0035 <c>PostReadDecision</c> routing applied in Core) and
    /// <see cref="ITranslationProvider"/> (display-name resolution to the
    /// viewer's language, ADR 0005 / D5). The write lane (U5) uses the caller's
    /// <see cref="IDocumentSession"/> per C3 and does not call these seams.
    /// Tests construct <see cref="TagService"/> directly with a live scratch
    /// store (the <see cref="PostService"/> harness idiom — not DI).
    /// </summary>
    public TagService(IDocumentStore store, IAuthorizationService authz, ITranslationProvider translations)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _translations = translations ?? throw new ArgumentNullException(nameof(translations));
    }

    // ── Write lane ──────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> AttachToPostAsync(
        string postId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(postId))
            throw new ArgumentException("A post id is required.", nameof(postId));
        if (slugs is null) throw new ArgumentNullException(nameof(slugs));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var post = await session.LoadAsync<Post>(postId);
        if (post is null)
            throw new KeyNotFoundException(
                $"Post '{postId}' was not found in the session; nothing to attach.");

        // Standing check (C-TG·5, D4; ADR 0014/0016 community lane,
        // ADR 0013 group lane — author ∪ GlobalAdmin, no component-moderator):
        if (!CanAttachToPost(post, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the post's author or a GlobalAdmin may attach tags to it.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveAttachVia(post.AuthorId, actorId, roles);
        var resolvedTags = new List<Tag>(slugs.Count);
        var tagIds = new List<string>(slugs.Count);

        foreach (var slug in slugs)
        {
            var derivedSlug = DeriveSlug(slug);

            // C-TG·4: create-or-reuse (the Slug is the business key —
            // a second author attaching the same Slug reuses the Tag doc,
            // and does NOT become its CreatedBy, F2).
            var existing = await session.Query<Tag>()
                .Where(t => t.Slug == derivedSlug)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                resolvedTags.Add(existing);
                tagIds.Add(existing.Id);
            }
            else
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = derivedSlug,
                    Name = derivedSlug,
                    LanguageCode = string.IsNullOrEmpty(post.LanguageCode) ? "en" : post.LanguageCode,
                    CreatedBy = actorId,
                    Created = now,
                };

                // One tag.create audit row per newly created tag (C-TG·9, F1).
                var createAudit = new Authorization.AccessAudit
                {
                    Id = Guid.NewGuid().ToString("N"),
                    At = now,
                    ActorId = actorId,
                    EffectivePrincipalId = actorId,
                    Action = "tag.create",
                    TargetKind = "tag",
                    TargetId = tag.Id,
                    Via = via,
                    Outcome = Authorization.AccessOutcome.Allow,
                };

                session.Store(tag);
                session.Store(createAudit);
                resolvedTags.Add(tag);
                tagIds.Add(tag.Id);
            }
        }

        post.TagIds = tagIds;

        // One tag.attach audit row (C-TG·9, F1).
        var attachAudit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tag.attach",
            TargetKind = "post",
            TargetId = postId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(attachAudit);

        await session.SaveChangesAsync();
        return resolvedTags;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> AttachToPageAsync(
        string pageId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(pageId))
            throw new ArgumentException("A page id is required.", nameof(pageId));
        if (slugs is null) throw new ArgumentNullException(nameof(slugs));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var page = await session.LoadAsync<Page>(pageId);
        if (page is null)
            throw new KeyNotFoundException(
                $"Page '{pageId}' was not found in the session; nothing to attach.");

        // C-TG·6, D6: a System page with a non-empty slugs set is refused
        // (a shape violation, not a standing denial — consistent with the
        // U4 PageService refusal). A no-op detach (empty slugs) on a System
        // page proceeds (it simply re-saves an already-empty TagIds).
        if (page.Kind == PageKind.System && slugs.Count > 0)
            throw new ArgumentException(
                "A PageKind.System page cannot carry tags (C-TG·6, D6).", nameof(slugs));

        // Standing check (C-TG·5, D4; ADR 0040 — author ∪ GlobalAdmin):
        if (!CanAttachToPage(page, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the page's author or a GlobalAdmin may attach tags to it.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveAttachVia(page.AuthorId, actorId, roles);
        var resolvedTags = new List<Tag>(slugs.Count);
        var tagIds = new List<string>(slugs.Count);

        foreach (var slug in slugs)
        {
            var derivedSlug = DeriveSlug(slug);

            var existing = await session.Query<Tag>()
                .Where(t => t.Slug == derivedSlug)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                resolvedTags.Add(existing);
                tagIds.Add(existing.Id);
            }
            else
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = derivedSlug,
                    Name = derivedSlug,
                    LanguageCode = string.IsNullOrEmpty(page.LanguageCode) ? "en" : page.LanguageCode,
                    CreatedBy = actorId,
                    Created = now,
                };

                var createAudit = new Authorization.AccessAudit
                {
                    Id = Guid.NewGuid().ToString("N"),
                    At = now,
                    ActorId = actorId,
                    EffectivePrincipalId = actorId,
                    Action = "tag.create",
                    TargetKind = "tag",
                    TargetId = tag.Id,
                    Via = via,
                    Outcome = Authorization.AccessOutcome.Allow,
                };

                session.Store(tag);
                session.Store(createAudit);
                resolvedTags.Add(tag);
                tagIds.Add(tag.Id);
            }
        }

        page.TagIds = tagIds;

        var attachAudit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tag.attach",
            TargetKind = "page",
            TargetId = pageId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(attachAudit);

        await session.SaveChangesAsync();
        return resolvedTags;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> AttachToDocumentAsync(
        string documentId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(documentId))
            throw new ArgumentException("A document id is required.", nameof(documentId));
        if (slugs is null) throw new ArgumentNullException(nameof(slugs));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var doc = await session.LoadAsync<Document>(documentId);
        if (doc is null)
            throw new KeyNotFoundException(
                $"Document '{documentId}' was not found in the session; nothing to attach.");

        // A shape violation — a document without an owner cannot carry tags
        // (the ADR 0125 owner-reading has no anchor to attach to).
        if (string.IsNullOrEmpty(doc.OwnerId))
            throw new ArgumentException(
                "A document without an owner cannot carry tags.", nameof(documentId));

        // Standing check (owner ∪ GlobalAdmin — the ADR 0125 owner-reading ∪
        // the ADR 0044 elevated standing):
        if (!CanAttachToDocument(doc, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the document's owner or a GlobalAdmin may attach tags to it.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveAttachVia(doc.OwnerId, actorId, roles);
        var resolvedTags = new List<Tag>(slugs.Count);
        var tagIds = new List<string>(slugs.Count);

        foreach (var slug in slugs)
        {
            var derivedSlug = DeriveSlug(slug);

            var existing = await session.Query<Tag>()
                .Where(t => t.Slug == derivedSlug)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                resolvedTags.Add(existing);
                tagIds.Add(existing.Id);
            }
            else
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = derivedSlug,
                    Name = derivedSlug,
                    LanguageCode = "en", // documents carry no authored-in LanguageCode
                    CreatedBy = actorId,
                    Created = now,
                };

                var createAudit = new Authorization.AccessAudit
                {
                    Id = Guid.NewGuid().ToString("N"),
                    At = now,
                    ActorId = actorId,
                    EffectivePrincipalId = actorId,
                    Action = "tag.create",
                    TargetKind = "tag",
                    TargetId = tag.Id,
                    Via = via,
                    Outcome = Authorization.AccessOutcome.Allow,
                };

                session.Store(tag);
                session.Store(createAudit);
                resolvedTags.Add(tag);
                tagIds.Add(tag.Id);
            }
        }

        doc.TagIds = tagIds;

        var attachAudit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tag.attach",
            TargetKind = "document",
            TargetId = documentId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(attachAudit);

        await session.SaveChangesAsync();
        return resolvedTags;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> AttachToTodoAsync(
        string todoId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(todoId))
            throw new ArgumentException("A to-do id is required.", nameof(todoId));
        if (slugs is null) throw new ArgumentNullException(nameof(slugs));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var todo = await session.LoadAsync<Projects.TodoItem>(todoId);
        if (todo is null)
            throw new KeyNotFoundException(
                $"To-do '{todoId}' was not found in the session; nothing to attach.");

        // Standing (C-TG·5; ADR 0147 — the to-do's own edit lane: creator ∪
        // assignee ∪ GlobalAdmin — the <c>ProjectService.CheckTodoStanding</c>
        // shape, re-checked here so the tag lane is self-contained):
        if (!CanAttachToTodo(todo, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the to-do's creator, its assignee, or a GlobalAdmin may attach tags to it.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveAttachVia(todo.AuthorId, actorId, roles);
        var resolvedTags = new List<Tag>(slugs.Count);
        var tagIds = new List<string>(slugs.Count);

        foreach (var slug in slugs)
        {
            var derivedSlug = DeriveSlug(slug);

            var existing = await session.Query<Tag>()
                .Where(t => t.Slug == derivedSlug)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                resolvedTags.Add(existing);
                tagIds.Add(existing.Id);
            }
            else
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = derivedSlug,
                    Name = derivedSlug,
                    LanguageCode = string.IsNullOrEmpty(todo.LanguageCode) ? "en" : todo.LanguageCode,
                    CreatedBy = actorId,
                    Created = now,
                };

                var createAudit = new Authorization.AccessAudit
                {
                    Id = Guid.NewGuid().ToString("N"),
                    At = now,
                    ActorId = actorId,
                    EffectivePrincipalId = actorId,
                    Action = "tag.create",
                    TargetKind = "tag",
                    TargetId = tag.Id,
                    Via = via,
                    Outcome = Authorization.AccessOutcome.Allow,
                };

                session.Store(tag);
                session.Store(createAudit);
                resolvedTags.Add(tag);
                tagIds.Add(tag.Id);
            }
        }

        todo.TagIds = tagIds;

        var attachAudit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tag.attach",
            TargetKind = "todo",
            TargetId = todoId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(attachAudit);

        await session.SaveChangesAsync();
        return resolvedTags;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tag>> AttachToBoardAsync(
        string boardId, IReadOnlyList<string> slugs,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(boardId))
            throw new ArgumentException("A board id is required.", nameof(boardId));
        if (slugs is null) throw new ArgumentNullException(nameof(slugs));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var board = await session.LoadAsync<Projects.KanbanBoard>(boardId);
        if (board is null)
            throw new KeyNotFoundException(
                $"Board '{boardId}' was not found in the session; nothing to attach.");

        // Standing (C-TG·5; ADR 0147 — the board's own edit lane: creator ∪
        // GlobalAdmin — the <c>ProjectService.CheckBoardStanding</c> shape,
        // re-checked here so the tag lane is self-contained):
        if (!CanAttachToBoard(board, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the board's creator or a GlobalAdmin may attach tags to it.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveAttachVia(board.AuthorId, actorId, roles);
        var resolvedTags = new List<Tag>(slugs.Count);
        var tagIds = new List<string>(slugs.Count);

        foreach (var slug in slugs)
        {
            var derivedSlug = DeriveSlug(slug);

            var existing = await session.Query<Tag>()
                .Where(t => t.Slug == derivedSlug)
                .FirstOrDefaultAsync();

            if (existing is not null)
            {
                resolvedTags.Add(existing);
                tagIds.Add(existing.Id);
            }
            else
            {
                var tag = new Tag
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Slug = derivedSlug,
                    Name = derivedSlug,
                    LanguageCode = string.IsNullOrEmpty(board.LanguageCode) ? "en" : board.LanguageCode,
                    CreatedBy = actorId,
                    Created = now,
                };

                var createAudit = new Authorization.AccessAudit
                {
                    Id = Guid.NewGuid().ToString("N"),
                    At = now,
                    ActorId = actorId,
                    EffectivePrincipalId = actorId,
                    Action = "tag.create",
                    TargetKind = "tag",
                    TargetId = tag.Id,
                    Via = via,
                    Outcome = Authorization.AccessOutcome.Allow,
                };

                session.Store(tag);
                session.Store(createAudit);
                resolvedTags.Add(tag);
                tagIds.Add(tag.Id);
            }
        }

        board.TagIds = tagIds;

        var attachAudit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tag.attach",
            TargetKind = "board",
            TargetId = boardId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(attachAudit);

        await session.SaveChangesAsync();
        return resolvedTags;
    }

    /// <inheritdoc />
    public async Task<TagTranslation> AddTagTranslationAsync(
        string tagId, string languageCode, string name,
        string actorId, IReadOnlySet<string> roles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(tagId))
            throw new ArgumentException("A tag id is required.", nameof(tagId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException(
                "A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "A translation requires a non-empty display name.", nameof(name));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(session);

        var tag = await session.LoadAsync<Tag>(tagId);
        if (tag is null)
            throw new KeyNotFoundException(
                $"Tag '{tagId}' was not found in the session; nothing to translate.");

        // Standing check (C-TG·5, D4 — creator ∪ GlobalAdmin only; a
        // non-creator attacher cannot reword the tag, F7):
        if (!CanTranslateTag(tag, actorId, roles))
            throw new UnauthorizedAccessException(
                "Only the tag's creator or a GlobalAdmin may set its translations.");

        var now = DateTimeOffset.UtcNow;
        var via = ResolveTranslateVia(tag.CreatedBy, actorId, roles);

        // Create-or-overwrite the (tagId, languageCode) row (the D4 translate
        // lane; the (TagId, LanguageCode) unique index enforces one row per
        // pair — re-adding overwrites).
        var existing = await session.Query<TagTranslation>()
            .Where(t => t.TagId == tagId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync();

        TagTranslation translation;
        if (existing is not null)
        {
            existing.Name = name;
            existing.AuthorId = actorId;
            existing.Created = now;
            translation = existing;
        }
        else
        {
            translation = new TagTranslation
            {
                Id = Guid.NewGuid().ToString("N"),
                TagId = tagId,
                LanguageCode = languageCode,
                Name = name,
                AuthorId = actorId,
                Created = now,
            };
            session.Store(translation);
        }

        // One tagtranslation.add audit row (C-TG·9, F6/F8).
        var audit = new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "tagtranslation.add",
            TargetKind = "tag",
            TargetId = tagId,
            Via = via,
            Outcome = Authorization.AccessOutcome.Allow,
        };
        session.Store(audit);

        await session.SaveChangesAsync();
        return translation;
    }


    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the <see cref="AccessVia"/> for an attach standing (the
    /// ADR 0006-D lane pin — only <see cref="AccessVia.Owner"/> or
    /// <see cref="AccessVia.Admin"/>; never <c>Moderator</c> /
    /// <c>BreakGlass</c> / <c>Group</c> or any other value). Precedence:
    /// author first (the narrowest right), then GlobalAdmin. Throws
    /// <see cref="UnauthorizedAccessException"/> if the actor holds neither
    /// (defensive — the standing probe is checked before this is called).
    /// </summary>
    private static AccessVia ResolveAttachVia(
        string authorId, string actorId, IReadOnlySet<string> roles)
    {
        if (string.Equals(authorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;
        if (roles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;
        throw new UnauthorizedAccessException(
            "The actor holds neither the author nor the GlobalAdmin standing.");
    }

    /// <summary>
    /// Resolves the <see cref="AccessVia"/> for a translate standing (the
    /// C-TG·5 pin — the tag's <c>CreatedBy</c> ∪ GlobalAdmin only).
    /// Precedence: creator first, then GlobalAdmin. Throws
    /// <see cref="UnauthorizedAccessException"/> if the actor holds neither.
    /// </summary>
    private static AccessVia ResolveTranslateVia(
        string createdBy, string actorId, IReadOnlySet<string> roles)
    {
        if (string.Equals(createdBy, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;
        if (roles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;
        throw new UnauthorizedAccessException(
            "The actor holds neither the creator nor the GlobalAdmin standing.");
    }

    /// <summary>
    /// Derives the normalized <c>Slug</c> from the typed input (C-TG·4, D3):
    /// lowercase + trim; validate the charset (letters — Unicode-aware, so
    /// accented letters like <c>é</c> are valid; digits; hyphens; underscores;
    /// ≤ 64 chars). <c>sanitation</c> and <c>Hygiène</c> are **two** different
    /// slugs (no accent-folding, no cross-language merge — D3). Throws
    /// <see cref="ArgumentException"/> on a violation.
    /// <para>
    /// The derived slug is the business key (C-TG·4) — the write lane's
    /// create-or-reuse query keys on it, so the normalization here is the
    /// identity guard (the <see cref="TagDocTypes"/> surface has no slug
    /// unique index; the write lane is the guard, the ADR 0011
    /// content-hash-Id dedup analogue).
    /// </para>
    /// </summary>
    private static string DeriveSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException(
                "A non-blank slug is required.", nameof(input));

        var slug = input.Trim().ToLowerInvariant();

        if (slug.Length > 64)
            throw new ArgumentException(
                "A slug must be at most 64 characters.", nameof(input));

        for (var i = 0; i < slug.Length; i++)
        {
            var c = slug[i];
            if (!char.IsLetter(c) && !char.IsDigit(c) && c != '-' && c != '_')
                throw new ArgumentException(
                    "A slug may contain only letters, digits, hyphens, and underscores.",
                    nameof(input));
        }

        return slug;
    }
}
