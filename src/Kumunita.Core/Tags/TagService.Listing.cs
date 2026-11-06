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
/// The tag listing surface — the paged tag-actor listing + the
/// per-tag post/page paged reads (ADR 0006 module-boundary). A
/// partial class of <see cref="TagService"/> extracted so the main
/// file stays under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class TagService
{
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
    public async Task<IReadOnlyList<TagItem>> ListForActorAsync(
        string actorId, string? preferredLanguageCode = null)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));

        await using var session = _store.QuerySession();
        var (readablePosts, readablePages) = await LoadActorReadableContentAsync(actorId, session);

        var tagIds = CollectReadableTagIds(readablePosts, readablePages);
        if (tagIds.Count == 0) return Array.Empty<TagItem>();

        var items = await BuildTagItemsAsync(session, tagIds, readablePosts, readablePages, preferredLanguageCode);
        return items
            .OrderBy(i => i.DisplayedName, StringComparer.Ordinal)
            .ThenBy(i => i.Tag.Slug, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The by-tag post results (F3 / F4) — the actor-readable posts whose
    /// <c>TagIds</c> contains the tag resolved from <paramref name="slug"/> —
    /// **paged** (ADR 0090 D6, M7 U01 — the design doc §7.6 lock): the
    /// <see cref="LoadActorReadableContentAsync"/> C-TG·3 gate (the
    /// content's own <c>Read</c> decision, never a tag-lane one), the same
    /// <c>TagIds</c> contains + <c>Created</c> ascending order, then a
    /// <c>Skip((page - 1) * PageSize).Take(PageSize)</c> window over the
    /// ordered candidate list (<see cref="PageSize"/> = 30 — the D4 shape).
    /// <see cref="TagPostPage.HasMore"/> is the sole paging signal (D1 —
    /// <c>pageCount == PageSize</c>); an out-of-range page returns an empty
    /// page with <c>HasMore: false</c>. **No** tag-lane <c>AccessAudit</c>
    /// row (C-TG·8 — the content's own decision rows are the content's, D7).
    /// A tag slug that resolves to no <see cref="Tag"/> doc returns an empty
    /// page with <c>HasMore: false</c> (the not-found shape — the Web's
    /// 404-floor reads it as an empty page).
    /// </summary>
    public async Task<TagPostPage> ListPostsByTagPagedAsync(
        string slug, string actorId, int page,
        SortSpec? sort = null, CancellationToken ct = default, int? pageSize = null)
    {
        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("A tag slug is required.", nameof(slug));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        var tag = await session.Query<Tag>().Where(t => t.Slug == slug).FirstOrDefaultAsync();
        if (tag is null)
            return new TagPostPage(Items: Array.Empty<Post>(), HasMore: false);

        var (readablePosts, _) = await LoadActorReadableContentAsync(actorId, session);
        // M26 U8 (design doc §2.2 row 14, closed allowlist created/modified/
        // title; correction C-1 — the pinned default is <b>ascending</b>):
        // null keeps the pinned .OrderBy(p => p.Created) byte-for-byte
        // (C-SORT·2); non-null applies the allowlist + the ThenBy(Id)
        // tie-breaker (C-SORT·5) via the shared TagPeopleSortSupport helper
        // (in-memory LINQ-to-objects — the §2.2 `?? MinValue` modified
        // sentinel works as pinned).
        IEnumerable<Post> ordered = readablePosts.Where(p => p.TagIds.Contains(tag.Id));
        if (sort is null)
            ordered = ordered.OrderBy(p => p.Created); // ← the pinned line, verbatim
        else
            ordered = TagPeopleSortSupport.OrderByTagFeedSort(ordered, sort,
                new HashSet<string> { "created", "modified", "title" },
                defaultDir: false, // the pinned default (correction C-1): created, asc
                p => p.Created, p => p.Modified, p => p.Title, p => p.Id);
        var candidates = ordered.ToList();

        var items = candidates
            .Skip((page - 1) * ps)
            .Take(ps)
            .ToList();

        // ADR 0090 D1 / D6 — the sole paging signal: the page's candidate
        // set filled the page (design doc §7.6).
        return new TagPostPage(Items: items, HasMore: items.Count == ps);
    }

    /// <summary>
    /// The by-tag blog-page results — the actor-readable <c>PageKind.User</c>
    /// pages whose <c>TagIds</c> contains the tag resolved from
    /// <paramref name="slug"/> — **paged** (ADR 0090 D6, M7 U01 — the design
    /// doc §7.6 lock): the same readable-content filter
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
    /// page with <c>HasMore: false</c> (the not-found shape — the Web's
    /// 404-floor reads it as an empty page).
    /// </summary>
    public async Task<TagPagePage> ListPagesByTagPagedAsync(
        string slug, string actorId, int page,
        SortSpec? sort = null, CancellationToken ct = default, int? pageSize = null)
    {
        if (string.IsNullOrEmpty(slug))
            throw new ArgumentException("A tag slug is required.", nameof(slug));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        var tag = await session.Query<Tag>().Where(t => t.Slug == slug).FirstOrDefaultAsync();
        if (tag is null)
            return new TagPagePage(Items: Array.Empty<Page>(), HasMore: false);

        var (_, readablePages) = await LoadActorReadableContentAsync(actorId, session);
        // M26 U8 (design doc §2.2 row 15, closed allowlist created/modified/
        // title; the <b>created asc</b> default is the current order):
        // null keeps the pinned .OrderBy(p => p.Created) byte-for-byte
        // (C-SORT·2); non-null applies the allowlist + the ThenBy(Id)
        // tie-breaker (C-SORT·5) via the shared TagPeopleSortSupport helper
        // (in-memory LINQ-to-objects — the §2.2 `?? MinValue` modified
        // sentinel works as pinned).
        IEnumerable<Page> ordered = readablePages.Where(p => p.TagIds.Contains(tag.Id));
        if (sort is null)
            ordered = ordered.OrderBy(p => p.Created); // ← the pinned line, verbatim
        else
            ordered = TagPeopleSortSupport.OrderByTagFeedSort(ordered, sort,
                new HashSet<string> { "created", "modified", "title" },
                defaultDir: false, // the pinned default: created, asc
                p => p.Created, p => p.Modified, p => p.Title, p => p.Id);
        var candidates = ordered.ToList();

        var items = candidates
            .Skip((page - 1) * ps)
            .Take(ps)
            .ToList();

        // ADR 0090 D1 / D6 — the sole paging signal: the page's candidate
        // set filled the page (design doc §7.6).
        return new TagPagePage(Items: items, HasMore: items.Count == ps);
    }

    /// <inheritdoc />

}
