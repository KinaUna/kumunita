using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Tags;

/// <summary>
/// The <c>TG</c> lane's service (U5: the attach / create / translate lane +
/// the three standing probes + the <see cref="DeriveSlug"/> helper; U6: the
/// four read-lane methods). <see cref="ListForActorAsync"/> /
/// <see cref="ListPostsByTagAsync"/> / <see cref="ListPagesByTagAsync"/> /
/// <see cref="SuggestAsync"/> are all four callers of the one C-TG·2 base
/// query over the actor's readable content (the content's own <c>Read</c>
/// decision is the gate, C-TG·1/C-TG·3; the tag lane writes no tag-family
/// <c>AccessAudit</c> row of its own, C-TG·8/D7).
/// <para>
/// **C3 idiom:** every write takes the **caller's**
/// <see cref="IDocumentSession"/> (the write and the in-session
/// <c>AccessAudit</c> row commit or roll back atomically — the
/// <see cref="PostService.AddPostTranslationAsync"/> shape). One
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
public sealed class TagService : ITagService
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

    // ── Read lane (U6 — the C-TG·2 base query over the actor's readable content) ──
    //
    // The one base query (C-TG·2, D5): "the set of Tag rows used on ≥ 1 Post /
    // PageKind.User Page the actor may already read." All four read-lane methods
    // derive from it. Scoping to readable content applies the **content's own
    // Read decision** (the ADR 0035 PostReadDecision routing applied in Core:
    // group posts → the ADR 0013 membership lane via CanSeeGroupAsync; community
    // posts / blog pages → the ADR 0001 audience lane via CanAsync(Read)) — the
    // tag grants nothing and adds no decision of its own (C-TG·1 / C-TG·3).
    //
    // **No tag-family audit row (C-TG·8, D7):** a read emits no `tag.*` row and
    // no TargetKind "tag" row — the tag lane never writes an AccessAudit row of
    // its own; the content's own Read-decision rows (post / page / grouppost)
    // are the content's, "rendered on a surface that already required the
    // referenced content's reach" (D7: "the access decision is the content's
    // existing Read, not the tag's").
    //
    // Each read opens its own QuerySession (the
    // PostService.GetPostTranslationsAsync read idiom — a read, not a
    // write-lane transaction).

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagItem>> ListForActorAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));

        await using var session = _store.QuerySession();
        var (readablePosts, readablePages) = await LoadActorReadableContentAsync(actorId, session);

        var tagIds = CollectReadableTagIds(readablePosts, readablePages);
        if (tagIds.Count == 0) return Array.Empty<TagItem>();

        var items = await BuildTagItemsAsync(session, tagIds, readablePosts, readablePages);
        return items
            .OrderBy(i => i.DisplayedName, StringComparer.Ordinal)
            .ThenBy(i => i.Tag.Slug, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Post>> ListPostsByTagAsync(string slug, string actorId)
    {
        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("A tag slug is required.", nameof(slug));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));

        await using var session = _store.QuerySession();
        var tag = await session.Query<Tag>().Where(t => t.Slug == slug).FirstOrDefaultAsync();
        if (tag is null) return Array.Empty<Post>();

        // C-TG·3: the post's own Read decision is applied (in
        // LoadActorReadableContentAsync) **before** the post is returned.
        var (readablePosts, _) = await LoadActorReadableContentAsync(actorId, session);
        return readablePosts
            .Where(p => p.TagIds.Contains(tag.Id))
            .OrderBy(p => p.Created)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Page>> ListPagesByTagAsync(string slug, string actorId)
    {
        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("A tag slug is required.", nameof(slug));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));

        await using var session = _store.QuerySession();
        var tag = await session.Query<Tag>().Where(t => t.Slug == slug).FirstOrDefaultAsync();
        if (tag is null) return Array.Empty<Page>();

        var (_, readablePages) = await LoadActorReadableContentAsync(actorId, session);
        return readablePages
            .Where(p => p.TagIds.Contains(tag.Id))
            .OrderBy(p => p.Created)
            .ToList();
    }

    /// <summary>
    /// The <see cref="ListPostsByTagAsync"/> shape, **paged** (ADR 0090 D6,
    /// M7 U01 — the design doc §7.6 lock): the same readable-content filter
    /// (the <see cref="LoadActorReadableContentAsync"/> C-TG·3 gate — the
    /// content's own <c>Read</c> decision, never a tag-lane one), the same
    /// <c>TagIds</c> contains + <c>Created</c> ascending order, then a
    /// <c>Skip((page - 1) * PageSize).Take(PageSize)</c> window over the
    /// ordered candidate list (<see cref="PageSize"/> = 30 — the D4 shape).
    /// <see cref="TagPostPage.HasMore"/> is the sole paging signal (D1 —
    /// <c>pageCount == PageSize</c>); an out-of-range page returns an empty
    /// page with <c>HasMore: false</c>. **No** tag-lane <c>AccessAudit</c>
    /// row (C-TG·8 — the content's own decision rows are the content's, D7).
    /// A tag slug that resolves to no <see cref="Tag"/> doc returns an empty
    /// page with <c>HasMore: false</c> (the <see cref="ListPostsByTagAsync"/>
    /// empty shape). The non-paged <see cref="ListPostsByTagAsync"/> is
    /// unmodified (its call sites keep the whole-list read).
    /// </summary>
    public async Task<TagPostPage> ListPostsByTagPagedAsync(string slug, string actorId, int page, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("A tag slug is required.", nameof(slug));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        var tag = await session.Query<Tag>().Where(t => t.Slug == slug).FirstOrDefaultAsync();
        if (tag is null)
            return new TagPostPage(Items: Array.Empty<Post>(), HasMore: false);

        var (readablePosts, _) = await LoadActorReadableContentAsync(actorId, session);
        var candidates = readablePosts
            .Where(p => p.TagIds.Contains(tag.Id))
            .OrderBy(p => p.Created)
            .ToList();

        var items = candidates
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        // ADR 0090 D1 / D6 — the sole paging signal: the page's candidate
        // set filled the page (design doc §7.6).
        return new TagPostPage(Items: items, HasMore: items.Count == PageSize);
    }

    /// <summary>
    /// The <see cref="ListPagesByTagAsync"/> shape, **paged** (ADR 0090 D6,
    /// M7 U01 — the design doc §7.6 lock): the same readable-content filter
    /// (the <see cref="LoadActorReadableContentAsync"/> C-TG·3 gate — the
    /// content's own <c>Read</c> decision, never a tag-lane one), the same
    /// <c>TagIds</c> contains + <c>Created</c> ascending order, then a
    /// <c>Skip((page - 1) * PageSize).Take(PageSize)</c> window over the
    /// ordered candidate list (<see cref="PageSize"/> = 30 — the D4 shape).
    /// <see cref="TagPagePage.HasMore"/> is the sole paging signal (D1 —
    /// <c>pageCount == PageSize</c>); an out-of-range page returns an empty
    /// page with <c>HasMore: false</c>. **No** tag-lane <c>AccessAudit</c>
    /// row (C-TG·8 — the content's own decision rows are the content's, D7).
    /// A tag slug that resolves to no <see cref="Tag"/> doc returns an empty
    /// page with <c>HasMore: false</c> (the <see cref="ListPagesByTagAsync"/>
    /// empty shape). The non-paged <see cref="ListPagesByTagAsync"/> is
    /// unmodified (its call sites keep the whole-list read).
    /// </summary>
    public async Task<TagPagePage> ListPagesByTagPagedAsync(string slug, string actorId, int page, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("A tag slug is required.", nameof(slug));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        var tag = await session.Query<Tag>().Where(t => t.Slug == slug).FirstOrDefaultAsync();
        if (tag is null)
            return new TagPagePage(Items: Array.Empty<Page>(), HasMore: false);

        var (_, readablePages) = await LoadActorReadableContentAsync(actorId, session);
        var candidates = readablePages
            .Where(p => p.TagIds.Contains(tag.Id))
            .OrderBy(p => p.Created)
            .ToList();

        var items = candidates
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        // ADR 0090 D1 / D6 — the sole paging signal: the page's candidate
        // set filled the page (design doc §7.6).
        return new TagPagePage(Items: items, HasMore: items.Count == PageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagItem>> SuggestAsync(string prefix, string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));

        await using var session = _store.QuerySession();
        var (readablePosts, readablePages) = await LoadActorReadableContentAsync(actorId, session);

        var tagIds = CollectReadableTagIds(readablePosts, readablePages);
        if (tagIds.Count == 0) return Array.Empty<TagItem>();

        var pfx = (prefix ?? string.Empty).Trim().ToLowerInvariant();
        var items = await BuildTagItemsAsync(session, tagIds, readablePosts, readablePages);
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
    /// </summary>
    private async Task<List<TagItem>> BuildTagItemsAsync(
        IQuerySession session, HashSet<string> tagIds,
        IReadOnlyList<Post> readablePosts, IReadOnlyList<Page> readablePages)
    {
        var tags = await session.Query<Tag>().Where(t => tagIds.Contains(t.Id)).ToListAsync();
        var translations = await session.Query<TagTranslation>().Where(t => tagIds.Contains(t.TagId)).ToListAsync();
        var effective = await _translations.ResolveEffectiveLanguageAsync((string?)null);

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
    //    PageService.CanTranslatePage idiom) ──

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
    public bool CanTranslateTag(Tag tag, string actorId, IReadOnlySet<string> roles)
    {
        ArgumentNullException.ThrowIfNull(tag);
        ArgumentNullException.ThrowIfNull(roles);
        return string.Equals(tag.CreatedBy, actorId, StringComparison.Ordinal)
               || roles.Contains(Roles.GlobalAdmin);
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
