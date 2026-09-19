using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Marten;
using Marten.Services;

namespace Kumunita.Core.Pages;

/// <summary>
/// The <c>/pages</c> bounded-context's store-composing service (ADR 0039;
/// the Pages lane <c>PG</c>). Mirrors the <see cref="Announcements
/// .AnnouncementService"/> shape: a single implementation of
/// <see cref="IPageService"/> that composes the host-registered
/// <see cref="IDocumentStore"/> (the write lanes' C3
/// <see cref="Authorization.IAuthorizationService"/> /
/// <see cref="IUserInfoService"/> seams land in U03), kept behind the
/// interface so the Web-side consumer (the U04 <c>PageController</c>) can be
/// tested with an NSubstitute double instead of a live Postgres (the
/// <see cref="IAnnouncementService"/> convention).
/// <para>
/// **U02 (this unit):** the read lanes (<c>GetByPathAsync</c> /
/// <c>GetTreeAsync</c> / <c>GetByMountPointAsync</c> /
/// <c>GetTranslationsAsync</c> / <c>GetBySlugUnderParentAsync</c>), the
/// <c>PageToAuditableResource</c> adapter (a separate file), and the
/// standing-matrix gate helpers (<c>CheckCreateStanding</c> /
/// <c>CheckEditStanding</c> / <c>CheckTranslateStanding</c>) + the
/// cycle-guard / depth-cap helpers the U03 write lanes will call. The read
/// lanes load documents; the authorization decision (the frozen
/// <see cref="Authorization.IAuthorizationService.CanAsync"/> /
/// <c>CanSeeAsync</c>) is made by the Web layer through the adapter —
/// <c>PG</c> adds an *adapter*, not a *branch* (ADR 0006 §A, ADR 0039 §3.4).
/// </para>
/// </summary>
public sealed class PageService : IPageService
{
    /// <summary>
    /// The hierarchy depth cap (ADR 0039 §3.3): a root page is depth 1, and
    /// no page may sit deeper than <c>MaxDepth</c> (8) — a sane bound the
    /// write lanes enforce (U03's <c>CreateAsync</c>/<c>MoveAsync</c>) via
    /// <see cref="EnsureDepthWithinLimitAsync"/>.
    /// </summary>
    public const int MaxDepth = 8;

    private readonly IDocumentStore _store;

    public PageService(IDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    // ─── Read lanes (ADR 0039 §3.3 / §3.4 / §3.5) ─────────────────────────
    //
    // The read lanes load and filter documents only. The per-page
    // authorization decision (the frozen CanAsync(Read) / CanSeeAsync(Read))
    // is made by the Web layer through the PageToAuditableResource adapter
    // (ADR 0039 §3.4) — PG adds an adapter, not a branch (ADR 0006 §A). The
    // IsDeleted soft-delete filter (ADR 0024) lives **here**, with the read,
    // because the browse view is the single place a deleted page could leak
    // (U03's DeleteAsync sets the flag; the read is where it is hidden).

    /// <inheritdoc />
    public async Task<Page> GetByPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new KeyNotFoundException("A page path is required.");

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            throw new KeyNotFoundException("A page path is required.");

        // Walk root-to-leaf following the (ParentId, Slug) chain — the derived
        // path (ADR 0039 §3.3) resolves to one page per segment.
        string? parentId = null;
        Page? page = null;
        foreach (var segment in segments)
        {
            page = await LoadByParentAndSlugAsync(parentId, segment).ConfigureAwait(false);
            if (page is null)
                throw new KeyNotFoundException($"No page at path '{path}'.");
            parentId = page.Id;
        }
        return page!;
    }

    /// <inheritdoc />
    public async Task<Page> GetBySlugUnderParentAsync(string? parentId, string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new KeyNotFoundException("A page slug is required.");
        var page = await LoadByParentAndSlugAsync(parentId, slug).ConfigureAwait(false);
        if (page is null)
            throw new KeyNotFoundException($"No page with slug '{slug}' under the requested parent.");
        return page;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Page>> GetTreeAsync()
    {
        await using var session = _store.QuerySession();
        return await session.Query<Page>()
            .Where(p => p.IsDeleted == false)   // ADR 0024 soft-delete filter (U02, not U03)
            .OrderBy(p => p.Created)
            .ThenBy(p => p.Slug)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PageTranslation>> GetTranslationsAsync(string pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId))
            throw new KeyNotFoundException("A page id is required.");

        await using var session = _store.QuerySession();
        var page = await session.LoadAsync<Page>(pageId).ConfigureAwait(false);
        if (page is null)
            throw new KeyNotFoundException($"Page '{pageId}' was not found.");

        return await session.Query<PageTranslation>()
            .Where(t => t.PageId == pageId)
            .OrderBy(t => t.LanguageCode)   // the ADR 0022 read (ordered by LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(Page, IReadOnlyList<PageTranslation>)> ResolvePageAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new KeyNotFoundException("A page path is required.");

        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            throw new KeyNotFoundException("A page path is required.");

        // One session: the page walk and the translation read share it, so the
        // (page, translations) pair is a single consistent read (the ADR 0043
        // D7 read seam — one read, not two that could drift). The walk is the
        // GetByPathAsync contract (missing path / deleted page ⇒ the
        // KeyNotFoundException the Web layer maps to 404); the translation
        // read is the ADR 0022 shape (ordered by LanguageCode, empty when the
        // page is un-translated — the authored-in body is the default).
        await using var session = _store.QuerySession();

        string? parentId = null;
        Page? page = null;
        foreach (var segment in segments)
        {
            page = await LoadByParentAndSlugAsync(session, parentId, segment).ConfigureAwait(false);
            if (page is null)
                throw new KeyNotFoundException($"No page at path '{path}'.");
            parentId = page.Id;
        }

        var resolved = page!;   // non-null: the loop throws KeyNotFoundException otherwise
        var translations = await session
            .Query<PageTranslation>()
            .Where(t => t.PageId == resolved.Id)
            .OrderBy(t => t.LanguageCode)   // the ADR 0022 read (ordered by LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);

        return (resolved, translations);
    }

    /// <inheritdoc />
    public async Task<Page?> GetByMountPointAsync(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return null;   // a mount point is a display concern — unmounted ⇒ null (no 404)

        await using var session = _store.QuerySession();
        return await session.Query<Page>()
            .Where(p => p.MountPoint == slot && p.IsDeleted == false)
            .OrderBy(p => p.Created)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    // ─── Standing-matrix gate helpers (ADR 0039 §3.7 — pure, no store) ────
    //
    // The C3 server-side re-check pattern (the AnnouncementService
    // EnsureCreatePermissionAsync / EnsureEditPermissionAsync shape): a Web
    // [Authorize(Roles=…)] is a convenience pre-gate, not the source of
    // truth. Each helper is a **pure** role-claim check (no DB access), so
    // it is directly testable and callable from any of U03's write lanes.
    // A null page is a KeyNotFoundException (the Web layer's 404); a denied
    // actor is an UnauthorizedAccessException (the Web layer's 403). The
    // existing role claims (Roles.GlobalAdmin / Roles.Translator) are
    // consulted — no new AccessAction, no new AccessVia, no branch in the
    // frozen IAuthorizationService (ADR 0039). ADR 0040 removes the community
    // Moderator lane from pages (a system page has no community to moderate; a
    // blog page is personal content), so Roles.ModeratorComponent grants no
    // page standing.

    /// <summary>
    /// The **create** standing (ADR 0040, amending ADR 0039 §3.7): a
    /// <see cref="PageKind.System"/> page is <b>GlobalAdmin only</b> (a
    /// community Moderator has <i>no</i> standing on a system page — the ADR
    /// 0040 amendment, user sign-off 2026-09-17); a <see cref
    /// "PageKind.User"/> (blog) page may be created by <b>any signed-in
    /// actor</b> (they become the author). The "only the user themselves"
    /// constraint is enforced by the <b>ownership</b> guard in
    /// <see cref="EnsureBlogOwnershipAsync"/> (a blog page must nest under the
    /// actor's own <c>blog/{uid}</c> root), <b>not</b> here.
    /// </summary>
    public static void CheckCreateStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the create standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (page.Kind == PageKind.System)
        {
            if (actorRoles.Contains(Roles.GlobalAdmin))
                return;
            throw new UnauthorizedAccessException("Only a GlobalAdmin may create a system page.");
        }

        // PageKind.User (a blog page): any signed-in actor — they become the
        // author. The ownership guard ensures it is under their own blog root.
    }

    /// <summary>
    /// The **edit** standing (ADR 0040, amending ADR 0039 §3.7): a
    /// <see cref="PageKind.System"/> page is <b>GlobalAdmin only</b> (the
    /// author branch is disabled for a system page — a community Moderator
    /// has no standing on it); a <see cref="PageKind.User"/> (blog) page is
    /// the <b>author ∪ GlobalAdmin</b> (the ADR 0040 amendment: a community
    /// Moderator has no standing on a resident's personal blog — it is
    /// personal content, not community content).
    /// </summary>
    public static void CheckEditStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the edit standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (page.Kind == PageKind.System)
        {
            if (actorRoles.Contains(Roles.GlobalAdmin))
                return;
            throw new UnauthorizedAccessException("Only a GlobalAdmin may edit a system page.");
        }

        // PageKind.User (a blog page): the author (the resident) ∪ GlobalAdmin
        // (platform override). A community Moderator has no standing on a
        // resident's personal blog (ADR 0040 — a blog page is personal
        // content, not community content).
        if (string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return;
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;

        throw new UnauthorizedAccessException(
            "Only the author or a GlobalAdmin may edit this blog page.");
    }

    /// <summary>
    /// The **translation** standing (ADR 0040, amending ADR 0039 §3.7 /
    /// ADR 0029 carried over): a <see cref="PageKind.System"/> page may be
    /// translated by a <b>GlobalAdmin or a Translator only</b> (the ADR 0040
    /// amendment; a platform page has no community to moderate); a
    /// <see cref="PageKind.User"/> (blog) page is also GlobalAdmin ∪ Translator
    /// — a community Moderator has <b>no</b> translation standing on either
    /// kind (a blog is personal content, not community content).
    /// </summary>
    public static void CheckTranslateStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the translation standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (!CanTranslatePage(actorId, actorRoles, page))
            throw new UnauthorizedAccessException(
                "Only a GlobalAdmin or a Translator may add a translation.");
    }

    /// <summary>
    /// The public ADR 0029 standing probe the Web layer calls to decide
    /// whether to render the "add a translation" affordance on a page (a
    /// <b>display</b> pin, not a gate — the real deny is the
    /// <see cref="AddTranslationAsync"/> standing re-check, which re-runs this
    /// same rule server-side via <see cref="CheckTranslateStanding"/>). It
    /// delegates to the single shared decision body
    /// (<see cref="CanTranslatePageCore"/>), so the display and the gate can
    /// never drift apart (the
    /// <see cref="Kumunita.Core.Posts.PostService.CanAddTranslation"/> /
    /// <see cref="Kumunita.Core.Announcements.AnnouncementService
    /// .CanTranslateAnnouncement"/> shape).
    /// </summary>
    public static bool CanTranslatePage(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            return false;
        if (string.IsNullOrEmpty(actorId))
            return false;
        return CanTranslatePageCore(actorRoles, page);
    }

    /// <summary>
    /// The single shared translation-standing decision (ADR 0040, amending
    /// ADR 0039 §3.7 / ADR 0029 carried over): a GlobalAdmin or a Translator
    /// qualifies on **any** page (system or user). A community Moderator has
    /// **no** translation standing on either kind (the ADR 0040 amendment:
    /// a system page has no community to moderate; a blog page is personal
    /// content, not community content). Called by both <see
    /// cref="CanTranslatePage"/> (the display probe) and <see
    /// cref="CheckTranslateStanding"/> (the write-lane gate), so the two can
    /// never disagree.
    /// </summary>
    private static bool CanTranslatePageCore(IReadOnlySet<string> actorRoles, Page page)
    {
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return true;
        if (actorRoles.Contains(Roles.Translator))
            return true;
        return false;
    }

    // ─── Hierarchy guards (ADR 0039 §3.3 — cycle-guard + depth-cap) ───────
    //
    // The U03 write lanes (CreateAsync / MoveAsync) call these before
    // committing. They are DB-backed (Marten has no FK, so the guard is the
    // service's, not a schema constraint) and are testable now (U02) so the
    // guard is exercised before the write lanes land.

    /// <summary>
    /// The depth of a page — the number of path segments from the forest
    /// root to this page (a root page has depth 1). Walks the
    /// <see cref="Page.ParentId"/> chain up to the root. A guard against an
    /// existing data cycle caps the walk (it terminates even on a corrupted
    /// chain).
    /// </summary>
    public async Task<int> GetDepthAsync(string pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId))
            return 0;

        int depth = 0;
        string? currentId = pageId;
        int guard = 0;
        while (currentId is not null && ++guard <= MaxDepth * 2)
        {
            depth++;
            await using var session = _store.QuerySession();
            var page = await session.LoadAsync<Page>(currentId).ConfigureAwait(false);
            currentId = page?.ParentId;
        }
        return depth;
    }

    /// <summary>
    /// Cycle guard (ADR 0039 §3.3): making <paramref name="newParentId"/> the
    /// new parent of <paramref name="nodeId"/> would create a cycle if
    /// <paramref name="newParentId"/> is the node itself or a descendant of
    /// the node. Walks the <see cref="Page.ParentId"/> chain from
    /// <paramref name="newParentId"/> up to the root; if any ancestor (or the
    /// node itself) equals <paramref name="nodeId"/>, the move is a cycle.
    /// Becoming a root (<paramref name="newParentId"/> null) always passes.
    /// </summary>
    /// <exception cref="InvalidOperationException">The move would create a cycle.</exception>
    public async Task EnsureNoCycleAsync(string nodeId, string? newParentId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("A node id is required.", nameof(nodeId));

        if (newParentId is null)
            return; // becoming a root — no cycle possible

        string? currentId = newParentId;
        int guard = 0;
        while (currentId is not null && ++guard <= MaxDepth * 2)
        {
            if (string.Equals(currentId, nodeId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Moving page '{nodeId}' under page '{newParentId}' would create a cycle.");

            await using var session = _store.QuerySession();
            var page = await session.LoadAsync<Page>(currentId).ConfigureAwait(false);
            currentId = page?.ParentId;
        }
    }

    /// <summary>
    /// Depth cap (ADR 0039 §3.3): the new parent's depth + 1 (the node being
    /// placed) must not exceed <see cref="MaxDepth"/> (8). Becoming a root
    /// (<paramref name="newParentId"/> null) always passes (depth 1).
    /// </summary>
    /// <exception cref="InvalidOperationException">The move would exceed the max depth.</exception>
    public async Task EnsureDepthWithinLimitAsync(string? newParentId)
    {
        if (newParentId is null)
            return; // becoming a root — depth 1, always within the cap

        int parentDepth = await GetDepthAsync(newParentId).ConfigureAwait(false);
        if (parentDepth + 1 > MaxDepth)
            throw new InvalidOperationException(
                $"Moving a page under '{newParentId}' (depth {parentDepth}) would exceed the max depth of {MaxDepth}.");
    }

    // ─── ADR 0040 — blog lanes (per-user feed + ownership guard) ─────────

    public async Task<Page?> GetBlogRootAsync(string actorId)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An actor id is required.", nameof(actorId));

        await using var session = _store.QuerySession();
        return await session.Query<Page>()
            .Where(p => p.Kind == PageKind.User
                && p.ParentId == null
                && p.AuthorId == actorId
                && p.IsDeleted == false)
            .OrderByDescending(p => p.Modified)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Page>> GetBlogPagesAsync(string authorId)
    {
        if (string.IsNullOrEmpty(authorId))
            throw new ArgumentException("An author id is required.", nameof(authorId));

        await using var session = _store.QuerySession();
        return await session.Query<Page>()
            .Where(p => p.Kind == PageKind.User
                && p.AuthorId == authorId
                && p.IsDeleted == false)
            .OrderByDescending(p => p.Modified)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<bool> IsUnderBlogAsync(string pageId)
    {
        if (string.IsNullOrEmpty(pageId))
            return false;

        await using var session = _store.QuerySession();
        var page = await session.LoadAsync<Page>(pageId).ConfigureAwait(false);
        if (page is null)
            return false;

        if (page.Kind == PageKind.User)
            return true;

        // Walk up to the root and check its kind.
        var cursor = page;
        while (cursor.ParentId is not null)
        {
            cursor = await session.LoadAsync<Page>(cursor.ParentId).ConfigureAwait(false);
            if (cursor is null)
                return false;
        }
        return cursor.Kind == PageKind.User;
    }

    // ─── Write lanes (ADR 0039 §3.7 — U03) ─────────────────────────────────
    //
    // Each write lane (a) re-checks standing **server-side** (the C3
    // single-source pin — a Web [Authorize(Roles=…)] is a convenience pre-gate,
    // not the source of truth), (b) persists the mutation, and (c) stores an
    // AccessAudit row **in the caller's in-flight IDocumentSession**
    // (invariant C3, ADR 0006 — synchronous, in-transaction, not a Wolverine
    // side effect) with TargetKind = "page" and the ADR 0040 action name. The
    // standing helpers (U02) are the pure gate; the **move/delete** standing
    // (ADR 0040 — a system page is GlobalAdmin-only; a blog page is author ∪
    // GlobalAdmin; a community Moderator has no standing on either) is a
    // distinct resolver below, because CheckEditStanding (U02) is the edit gate
    // and move/delete resolve a narrower / different set per kind.
    // The ImageIds / AttachmentIds fields are populated by the caller (the
    // Web layer's ContentImageIds.ExtractContentImageIds /
    // AttachmentIds.ExtractAttachmentIds idiom, ADR 0025 / ADR 0034 — the
    // RC U04/U05 + ATT U5 shape: Core never parses the body, it normalizes
    // the POCO's fields via ?? []). The client never sends them.

    /// <summary>
    /// Creates a <see cref="Page"/> in the **caller's** in-flight session
    /// (C3). Standing (ADR 0040, amending §3.7): a <see cref="PageKind
    /// .System"/> page is a GlobalAdmin only; a <see cref="PageKind.User"/>
    /// (blog) page is any signed-in actor (they become the author — the
    /// ownership guard in <see cref="EnsureBlogOwnershipAsync"/> keeps it
    /// under their own <c>blog/{uid}</c> root); a community Moderator has no
    /// standing on either kind. A root-slug collision (two roots sharing a
    /// slug <b>in the same namespace</b> — scoped by Kind + AuthorId) is a
    /// <see cref="InvalidOperationException"/> — the
    /// <c>(ParentId, Slug)</c> unique index does **not** prevent it (Postgres
    /// treats NULLs as distinct), so this lane is the authoritative root-slug
    /// guard. A denied actor throws **before** anything is stored.
    /// </summary>
    public async Task<Page> CreateAsync(
        Page page, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An authoring actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        if (string.IsNullOrWhiteSpace(page.Slug))
            throw new ArgumentException("A page slug is required.", nameof(page.Slug));

        // Standing re-check (server-side, C3 single-source pin, ADR 0040
        // kind-aware): a system page is GlobalAdmin-only; a blog page is
        // any signed-in actor (they become the author).
        CheckCreateStanding(actorId, actorRoles, page);

        // A blog page is authored by the actor (they own it) — the web
        // composer pre-gate is a convenience, the write lane is the source of
        // truth (C3). A system page may have an empty AuthorId (a platform
        // page has no resident author).
        if (page.Kind == PageKind.User && string.IsNullOrEmpty(page.AuthorId))
            page.AuthorId = actorId;

        // ADR 0040 namespace + ownership guards (server-side, C3):
        //   1. a page may only nest under a parent of the *same* kind
        //      (system ⇔ system, blog ⇔ blog);
        //   2. a blog page may only nest under the actor's own blog root.
        Page? parent = null;
        if (page.ParentId is not null)
            parent = await session.LoadAsync<Page>(page.ParentId).ConfigureAwait(false)
                ?? throw new KeyNotFoundException(
                    $"Parent page '{page.ParentId}' not found.");
        await EnsureCreateNamespaceAsync(page.Kind, parent, session).ConfigureAwait(false);
        await EnsureBlogOwnershipAsync(page.Kind, parent, actorId, session).ConfigureAwait(false);

        // ADR 0040: MountPoint is a *system*-page concept (a UI slot such as
        // footer/community or help/account). A resident's blog page only
        // surfaces in its own /blog feed, so it never mounts to a UI slot —
        // clear it server-side (C3) so a client cannot set it on a blog page.
        ClearMountPointForBlog(page);

        // Root-slug guard (the index does not cover it — Postgres treats NULLs
        // as distinct, the U01 drift note): if ParentId is null, no *other*
        // root page may already carry this slug **in the same namespace**.
        // ADR 0040 scopes the collision by (Kind, AuthorId):
        //   • a <b>system</b> root (Kind=System) collides only with another
        //     same-slug system root — the <c>system/</c> namespace is singular;
        //   • a <b>blog</b> root (Kind=User) collides only with another
        //     same-slug root owned by the <b>same</b> author — two residents'
        //     blogs are disjoint by ownership, so two residents may both have
        //     a root page slugged <c>"recipes"</c> (their <c>blog/{uid}</c>
        //     namespaces never overlap), while a single resident is still
        //     capped at one blog root (a second top-level page of theirs
        //     nests under the first).
        if (page.ParentId is null)
        {
            var existingRoot = await session.Query<Page>()
                .Where(p => p.ParentId == null
                    && p.Slug == page.Slug
                    && p.Kind == page.Kind
                    && p.AuthorId == page.AuthorId
                    && p.IsDeleted == false)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (existingRoot is not null && !string.Equals(existingRoot.Id, page.Id, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"A root page with slug '{page.Slug}' already exists in this namespace (id '{existingRoot.Id}'); " +
                    "choose a different slug or nest the new page under a parent.");
        }

        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrEmpty(page.Id))
            page.Id = Guid.NewGuid().ToString("N");
        page.AuthorId = actorId;
        page.Created = now;
        page.ImageIds = page.ImageIds ?? [];       // RC ADR 0025 — caller-parsed, never spoofed
        page.AttachmentIds = page.AttachmentIds ?? []; // ATT ADR 0034 — caller-parsed, never spoofed

        // Resolve the audit Via tag before storing. A create's actor IS the
        // author for a blog page (they own it — AccessVia.Owner, the narrowest
        // standing), and for a system page a GlobalAdmin (AccessVia.Admin). A
        // system page has no resident author, so isAuthor is false there.
        var via = ResolveWriteStandingVia(
            page.Kind, actorRoles, page.ComponentId,
            isAuthor: page.Kind == PageKind.User);

        var audit = new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.create",
            TargetKind = "page",
            TargetId = page.Id,
            Via = via,
            Outcome = AccessOutcome.Allow
        };

        session.Store(page);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return page;
    }

    /// <summary>
    /// Edits an existing <see cref="Page"/> in the **caller's** in-flight
    /// session (C3). Standing (ADR 0040, amending §3.7): a <see cref
    /// "PageKind.System"/> page is a GlobalAdmin only; a <see cref
    /// "PageKind.User"/> (blog) page is the author ∪ GlobalAdmin; a community
    /// Moderator has no standing on either kind. A missing page is a <see
    /// cref="KeyNotFoundException"/> (404); a denied actor is a <see
    /// cref="UnauthorizedAccessException"/> (403).
    /// </summary>
    public async Task<Page> UpdateAsync(
        Page updated, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        ArgumentNullException.ThrowIfNull(updated);
        if (string.IsNullOrEmpty(updated.Id)) throw new ArgumentException("A page id is required.", nameof(updated.Id));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var existing = await session.LoadAsync<Page>(updated.Id).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Page '{updated.Id}' was not found in the session; nothing to edit.");

        // Standing re-check (server-side, C3 single-source pin).
        CheckEditStanding(actorId, actorRoles, existing);

        // Capture the **stored** page's author + component scope *before* the
        // field-copy below overwrites them: the standing decision (and the
        // audit Via tag) is based on the resource the actor has standing over
        // (the stored page), not the incoming `updated` (which may carry a
        // null ComponentId on a content-only edit).
        var storedAuthorId = existing.AuthorId;
        var storedComponentId = existing.ComponentId;

        var now = DateTimeOffset.UtcNow;
        existing.Title = updated.Title;
        existing.Body = updated.Body;
        existing.Audience = updated.Audience;
        existing.ComponentId = updated.ComponentId;
        existing.LanguageCode = updated.LanguageCode;
        existing.MountPoint = updated.MountPoint;
        existing.ImageIds = updated.ImageIds ?? [];          // RC ADR 0025 — caller-parsed
        existing.AttachmentIds = updated.AttachmentIds ?? []; // ATT ADR 0034 — caller-parsed

        // ADR 0040: MountPoint is a *system*-page concept (a UI slot) — a blog
        // page only surfaces in its own /blog feed, so an edit may not (re)mount
        // it; clear it server-side (C3).
        ClearMountPointForBlog(existing);

        // The audit Via tag (narrowest standing that applied): Owner if the
        // actor is the author (a blog page), else Admin (a GlobalAdmin). ADR
        // 0040: a community Moderator has no write standing on either kind.
        var via = ResolveWriteStandingVia(
            existing.Kind,
            actorRoles,
            storedComponentId,
            isAuthor: string.Equals(storedAuthorId, actorId, StringComparison.Ordinal));

        var audit = new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.update",
            TargetKind = "page",
            TargetId = existing.Id,
            Via = via,
            Outcome = AccessOutcome.Allow
        };

        existing.Modified = now;
        session.Store(existing);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return existing;
    }

    /// <summary>
    /// Publishes a draft <see cref="Page"/> — **author-only** (ADR 0037 pin):
    /// the sole decision is <c>AuthorId == actorId</c> (ordinal); a
    /// non-author is denied (403) **even at GlobalAdmin**. Idempotent.
    /// </summary>
    public async Task<Page> PublishAsync(string pageId, string actorId, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(pageId)) throw new ArgumentException("A page id is required.", nameof(pageId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting author is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(session);

        var page = await session.LoadAsync<Page>(pageId).ConfigureAwait(false);
        if (page is null)
            throw new KeyNotFoundException($"Page '{pageId}' was not found in the session; nothing to publish.");

        // Author-only gate (ADR 0037): only the author may publish. A
        // non-author (including a GlobalAdmin) is denied — the ADR 0037 pin.
        if (!string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author of a page may publish it.");

        var now = DateTimeOffset.UtcNow;
        bool wasDraft = page.IsDraft;
        if (wasDraft)
        {
            page.IsDraft = false;
            page.Modified = now;
        }

        var audit = new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.publish",
            TargetKind = "page",
            TargetId = page.Id,
            Via = AccessVia.Owner,   // ADR 0037 — the publish standing is Owner-only
            Outcome = AccessOutcome.Allow
        };

        session.Store(page);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return page;
    }

    /// <summary>
    /// Moves a <see cref="Page"/> under a new parent (reparent) in the
    /// **caller's** in-flight session (C3). Standing (ADR 0040, amending §3.7):
    /// a <see cref="PageKind.System"/> page is a GlobalAdmin only; a <see cref
    /// "PageKind.User"/> (blog) page is the author ∪ GlobalAdmin; a community
    /// Moderator has no standing on either kind. Applies the cycle-guard +
    /// depth-cap + the ADR 0040 namespace guard; a <paramref name="newSlug"/>
    /// change is authoritative for root-level uniqueness. The derived path is
    /// rewritten by the single <see cref="Page.ParentId"/> / <see
    /// cref="Page.Slug"/> column write.
    /// </summary>
    public async Task<Page> MoveAsync(
        string pageId, string? newParentId, string? newSlug,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(pageId)) throw new ArgumentException("A page id is required.", nameof(pageId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var page = await session.LoadAsync<Page>(pageId).ConfigureAwait(false);
        if (page is null)
            throw new KeyNotFoundException($"Page '{pageId}' was not found in the session; nothing to move.");

        // Standing re-check (ADR 0040): a system page is GlobalAdmin-only;
        // a blog page is author ∪ GlobalAdmin.
        var via = ResolveMoveDeleteStanding(page.Kind, page.AuthorId, page.ComponentId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                page.Kind == PageKind.System
                    ? "Only a GlobalAdmin may move a system page."
                    : "Only the author or a GlobalAdmin may move a blog page.");

        // Cycle guard + depth cap (the U02 hierarchy guards).
        await EnsureNoCycleAsync(pageId, newParentId).ConfigureAwait(false);
        await EnsureDepthWithinLimitAsync(newParentId).ConfigureAwait(false);

        // ADR 0040 namespace guard (server-side, C3): a page may only be
        // reparented under a parent of the *same* kind (a system page under
        // a system page, a blog page under a blog page). A root move
        // (newParentId null) is a no-op for the guard.
        if (newParentId is not null)
        {
            Page? newParent = await session.LoadAsync<Page>(newParentId).ConfigureAwait(false)
                ?? throw new KeyNotFoundException(
                    $"Parent page '{newParentId}' not found.");
            await EnsureCreateNamespaceAsync(page.Kind, newParent, session).ConfigureAwait(false);
        }

        // Root-slug guard: if the move makes the page a root (newParentId null)
        // and newSlug is specified, no *other* root page may already carry it.
        if (newParentId is null && newSlug is not null && !string.IsNullOrWhiteSpace(newSlug)
            && (newSlug != page.Slug || page.ParentId is not null))
        {
            var existingRoot = await session.Query<Page>()
                .Where(p => p.ParentId == null && p.Slug == newSlug && p.IsDeleted == false)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (existingRoot is not null && !string.Equals(existingRoot.Id, pageId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"A root page with slug '{newSlug}' already exists (id '{existingRoot.Id}'); " +
                    "choose a different slug or nest the page under a parent.");
        }

        var now = DateTimeOffset.UtcNow;
        page.ParentId = newParentId;
        if (newSlug is not null && !string.IsNullOrWhiteSpace(newSlug))
            page.Slug = newSlug;
        page.Modified = now;

        var audit = new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.move",
            TargetKind = "page",
            TargetId = page.Id,
            Via = via.Value,
            Outcome = AccessOutcome.Allow
        };

        session.Store(page);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return page;
    }

    /// <summary>
    /// **Soft-deletes** a <see cref="Page"/> in the **caller's** in-flight
    /// session (C3): sets <see cref="Page.IsDeleted"/> to <c>true</c> and
    /// stamps <see cref="Page.Modified"/> (ADR 0024 idiom). Standing (ADR 0040,
    /// amending §3.7): a <see cref="PageKind.System"/> page is a GlobalAdmin
    /// only; a <see cref="PageKind.User"/> (blog) page is the author ∪
    /// GlobalAdmin; a community Moderator has no standing on either kind.
    /// </summary>
    public async Task DeleteAsync(
        string pageId, string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(pageId)) throw new ArgumentException("A page id is required.", nameof(pageId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var page = await session.LoadAsync<Page>(pageId).ConfigureAwait(false);
        if (page is null)
            throw new KeyNotFoundException($"Page '{pageId}' was not found in the session; nothing to delete.");

        // Standing re-check (ADR 0040): a system page is GlobalAdmin-only;
        // a blog page is author ∪ GlobalAdmin.
        var via = ResolveMoveDeleteStanding(page.Kind, page.AuthorId, page.ComponentId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                page.Kind == PageKind.System
                    ? "Only a GlobalAdmin may delete a system page."
                    : "Only the author or a GlobalAdmin may delete a blog page.");

        var now = DateTimeOffset.UtcNow;
        page.IsDeleted = true;
        page.Modified = now;

        var audit = new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.delete",
            TargetKind = "page",
            TargetId = page.Id,
            Via = via.Value,
            Outcome = AccessOutcome.Allow
        };

        session.Store(page);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a **user-added translation** of a page in the **caller's**
    /// in-flight session (C3; the ADR 0029 standing carried over, design doc
    /// §3.7). Standing (ADR 0040): a GlobalAdmin or a Translator on **either**
    /// kind — a community Moderator has no translation standing on either kind
    /// (a system page has no community to moderate; a blog page is personal
    /// content). The <c>(PageId, LanguageCode)</c> unique index (U01) is the
    /// add-only duplicate guard.
    /// </summary>
    public async Task<PageTranslation> AddTranslationAsync(
        string pageId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(pageId))
            throw new ArgumentException("A page id is required.", nameof(pageId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var page = await session.LoadAsync<Page>(pageId).ConfigureAwait(false);
        if (page is null)
            throw new KeyNotFoundException($"Page '{pageId}' was not found in the session; nothing to translate.");

        // Standing re-check (server-side, ADR 0040): GlobalAdmin / Translator
        // on either kind — a community Moderator has no translation standing.
        // CheckTranslateStanding is the U02 pure helper — it throws
        // UnauthorizedAccessException if the actor has no qualifying standing.
        CheckTranslateStanding(actorId, actorRoles, page);

        var now = DateTimeOffset.UtcNow;
        var translation = new PageTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            PageId = pageId,
            LanguageCode = languageCode,
            Title = title,
            Body = body,
            AuthorId = actorId,
            Created = now
        };

        var via = ResolveTranslationStandingVia(
            page.Kind, page.ComponentId, actorId, actorRoles);

        var audit = new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.translation.add",
            TargetKind = "page",
            TargetId = pageId,
            Via = via,
            Outcome = AccessOutcome.Allow
        };

        session.Store(translation);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return translation;
    }

    // ─── ADR 0048 — edit + delete lane for page translations ───────────────
    // ADR 0029 (carried over to ADR 0040) was add-only. ADR 0048 lifts the
    // "add-only" pin: a GlobalAdmin or a Translator (the same standing as
    // the add lane) may now **update** the existing (page, languageCode)
    // row in place, or **remove** it. A hard <c>session.Delete</c>; the
    // trail is preserved by the <c>AccessAudit</c> row.

    /// <summary>
    /// **Updates** the existing <see cref="PageTranslation"/> row for
    /// (<paramref name="pageId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing (ADR 0048, same as
    /// the add lane): a <see cref="Roles.GlobalAdmin"/> or a
    /// <see cref="Roles.Translator"/> — a community Moderator has no
    /// translation standing. A missing page or row is a
    /// <see cref="KeyNotFoundException"/>; a denied actor throws
    /// <see cref="UnauthorizedAccessException"/>. One <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task<PageTranslation> UpdateTranslationAsync(
        string pageId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(pageId))
            throw new ArgumentException("A page id is required.", nameof(pageId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var page = await session.LoadAsync<Page>(pageId).ConfigureAwait(false);
        if (page is null)
            throw new KeyNotFoundException($"Page '{pageId}' was not found in the session; nothing to update.");

        var row = await session.Query<PageTranslation>()
            .Where(t => t.PageId == pageId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"Page '{pageId}' has no translation in '{languageCode}'; nothing to update.");

        CheckTranslateStanding(actorId, actorRoles, page);

        row.Title = title;
        row.Body = body;

        var now = DateTimeOffset.UtcNow;
        var via = ResolveTranslationStandingVia(page.Kind, page.ComponentId, actorId, actorRoles);
        var audit = new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.translation.update",
            TargetKind = "page",
            TargetId = pageId,
            Via = via,
            Outcome = AccessOutcome.Allow
        };

        session.Store(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
        return row;
    }

    /// <summary>
    /// **Removes** the existing <see cref="PageTranslation"/> row for
    /// (<paramref name="pageId"/>, <paramref name="languageCode"/>) in the
    /// <b>caller's</b> in-flight session (C3). Standing is the same as the
    /// add lane. A hard <c>session.Delete</c>; the trail is preserved by the
    /// <see cref="AccessAudit"/> row (action <c>page.translation.remove</c>).
    /// A missing page or row is a <see cref="KeyNotFoundException"/>. One
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task RemoveTranslationAsync(
        string pageId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, IDocumentSession session)
    {
        if (string.IsNullOrEmpty(pageId))
            throw new ArgumentException("A page id is required.", nameof(pageId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new ArgumentException("An acting actor is required.", nameof(actorId));
        ArgumentNullException.ThrowIfNull(actorRoles);
        ArgumentNullException.ThrowIfNull(session);

        var page = await session.LoadAsync<Page>(pageId).ConfigureAwait(false);
        if (page is null)
            throw new KeyNotFoundException($"Page '{pageId}' was not found in the session; nothing to remove.");

        var row = await session.Query<PageTranslation>()
            .Where(t => t.PageId == pageId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException(
                $"Page '{pageId}' has no translation in '{languageCode}'; nothing to remove.");

        CheckTranslateStanding(actorId, actorRoles, page);

        var now = DateTimeOffset.UtcNow;
        var via = ResolveTranslationStandingVia(page.Kind, page.ComponentId, actorId, actorRoles);
        var audit = new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = "page.translation.remove",
            TargetKind = "page",
            TargetId = pageId,
            Via = via,
            Outcome = AccessOutcome.Allow
        };

        session.Delete(row);
        session.Store(audit);
        await session.SaveChangesAsync().ConfigureAwait(false);
    }

    // ─── Write-lane standing resolvers (U03 — distinct from U02's helpers) ─

    /// <summary>
    /// The **move/delete** standing resolver (ADR 0040, amending ADR 0039
    /// §3.7): a <see cref="PageKind.System"/> page is <b>GlobalAdmin only</b>
    /// (a community Moderator has no standing on a system page, and the ADR
    /// 0037 author-only publish pin is the *only* place an author's lane
    /// applies — it does not extend to move/delete of a system page); a
    /// <see cref="PageKind.User"/> (blog) page is <b>author ∪ GlobalAdmin</b>
    /// — the **author lane is enabled** for this kind (the ADR 0040 behavioral
    /// change: a resident may reparent / soft-delete their own blog pages),
    /// while a community Moderator still has no standing on it (a blog is
    /// personal content, not community content). Returns the
    /// <see cref="AccessVia"/> the actor qualifies under, or <c>null</c> to
    /// deny. Precedence (most specific first): Owner → Admin.
    /// </summary>
    private static AccessVia? ResolveMoveDeleteStanding(
        PageKind kind, string authorId, string? componentId,
        string actorId, IReadOnlySet<string> actorRoles)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);

        if (kind == PageKind.System)
        {
            // ADR 0040: a system page is GlobalAdmin-only for move/delete —
            // neither the author (there is no resident author) nor a
            // community Moderator qualifies.
            if (actorRoles.Contains(Roles.GlobalAdmin))
                return AccessVia.Admin;
            return null;
        }

        // PageKind.User (a blog page): the author (the resident) ∪ GlobalAdmin
        // (platform override). A community Moderator has no standing on a
        // resident's personal blog (ADR 0040 — a blog page is personal
        // content, not community content).
        if (!string.IsNullOrEmpty(authorId)
            && string.Equals(authorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;
        return null;
    }

    /// <summary>
    /// The **translation** standing resolver (ADR 0040, amending ADR 0039
    /// §3.7 / ADR 0029 carried over): a GlobalAdmin or a Translator
    /// (<see cref="AccessVia.Admin"/>) on <b>either</b> kind. A community
    /// Moderator has <b>no</b> translation standing on either kind (the ADR
    /// 0040 amendment — a system page has no community to moderate; a blog
    /// page is personal content, not community content), so the
    /// component-moderator branch (<see cref="AccessVia.Moderator"/>) is
    /// retired for both. Both qualifying roles map to
    /// <see cref="AccessVia.Admin"/>.
    /// </summary>
    private static AccessVia ResolveTranslationStandingVia(
        PageKind kind, string? componentId, string actorId, IReadOnlySet<string> actorRoles)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        // ADR 0040: a community Moderator has no translation standing on
        // either kind (a system page has no community to moderate; a blog
        // page is personal content). Only GlobalAdmin / Translator qualify,
        // both of which map to AccessVia.Admin.
        return AccessVia.Admin;
    }

    /// <summary>
    /// Resolves the <see cref="AccessVia"/> tag for the **create** and
    /// **edit** write lanes' audit rows. Precedence (narrowest right first,
    /// so the audit row records the narrowest standing that applied):
    /// <c>Owner</c> (the author, a blog page) → <c>Admin</c> (GlobalAdmin).
    /// ADR 0040: a community Moderator has no write standing on either kind,
    /// so <see cref="AccessVia.Moderator"/> is never returned (the
    /// <paramref name="moderatorComponentId"/> parameter is retained for
    /// seam-shape stability, not consulted). The caller has already verified
    /// the standing before calling this — it is a pure tag-resolution, not a
    /// gate.
    /// </summary>
    private static AccessVia ResolveWriteStandingVia(
        PageKind kind, IReadOnlySet<string> actorRoles,
        string? moderatorComponentId, bool isAuthor = false)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (isAuthor)
            return AccessVia.Owner;
        // ADR 0040: a community Moderator has no write standing on either
        // kind (a system page is GlobalAdmin-only; a blog page is author ∪
        // GlobalAdmin). Only GlobalAdmin qualifies here → AccessVia.Admin.
        return AccessVia.Admin;
    }

    // ─── ADR 0040 — namespace guards + blog lanes (the standing matrix's
    //     "parent page option differentiates between system and user pages"
    //     and the "only the user themselves" ownership constraint) ─────────

    /// <summary>
    /// The <b>namespace guard</b> for a <b>create</b> (ADR 0040): a
    /// <see cref="PageKind.System"/> page may nest only under another system
    /// page (or be a root), and a <see cref="PageKind.User"/> (blog) page may
    /// nest only under another user page owned by the actor's blog root (or be
    /// a root — which for a blog is the <c>blog/{uid}</c> root itself). The
    /// guard is <b>parent-driven</b>: it walks up from <paramref name="parent"/>
    /// to the root and checks the root's kind matches the page's kind. A
    /// mismatch (<c>system/…</c> under <c>blog/{uid}</c>, or the reverse) is a
    /// structural error — the two namespaces are disjoint by design.
    /// </summary>
    private async Task EnsureCreateNamespaceAsync(
        PageKind kind, Page? parent, IDocumentSession session)
    {
        if (parent is null)
            return;   // a root — no parent to mismatch against

        // Walk up to the root of the parent's subtree; the root's kind must
        // match the page's kind (the two namespaces are disjoint).
        var cursor = parent;
        while (cursor.ParentId is not null)
        {
            cursor = await session.LoadAsync<Page>(cursor.ParentId).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Orphaned page chain: parent '{cursor.ParentId}' not found.");
        }
        if (cursor.Kind != kind)
            throw new InvalidOperationException(
                kind == PageKind.System
                    ? "A system page may not be nested under a user (blog) page."
                    : "A user (blog) page may not be nested under a system page.");
    }

    /// <summary>
    /// The <b>ownership guard</b> for a <b>blog</b> create/move (ADR 0040,
    /// user sign-off 1): a <see cref="PageKind.User"/> page must nest under the
    /// actor's <b>own</b> <c>blog/{uid}</c> root — a resident cannot nest under
    /// another resident's blog. A <see cref="PageKind.System"/> page is
    /// admin-only (no ownership constraint). Called by the Web composer
    /// pre-gate and <see cref="CreateAsync"/> / <see cref="MoveAsync"/>
    /// (server-side, C3).
    /// </summary>
    private async Task EnsureBlogOwnershipAsync(
        PageKind kind, Page? parent, string actorId, IDocumentSession session)
    {
        if (kind != PageKind.User)
            return;   // a system page — the admin-only path; no ownership guard

        // A root-level blog page <b>is</b> the actor's own blog root — the
        // actor becomes its author, so there is nothing to guard against.
        // (A resident cannot forge another resident's root because the author
        // is always set to the actor by the write lane.)
        if (parent is null)
            return;

        // Walk up to the root of the parent's subtree.
        Page root = parent;
        while (root.ParentId is not null)
        {
            root = await session.LoadAsync<Page>(root.ParentId).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    $"Orphaned page chain: parent '{root.ParentId}' not found.");
        }

        // The blog root must be owned by the actor (or be the global admin's
        // own blog root — the admin may manage their own blog).
        if (!string.Equals(root.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                $"A blog page may only be nested under the actor's own blog root " +
                $"(found root author '{root.AuthorId}', actor '{actorId}').");
    }

    /// <summary>
    /// The <b>mount-point guard</b> (ADR 0040): a <see cref="Page.MountPoint"/>
    /// is a <b>system-page</b> concept — a UI slot such as
    /// <c>footer/community</c> or <c>help/account</c>. A resident's
    /// <see cref="PageKind.User"/> (blog) page only surfaces in its own
    /// <c>/blog</c> feed, so it never mounts to a UI slot. The write lane
    /// clears it (server-side, C3) so a client cannot (re)mount a blog page.
    /// A <see cref="PageKind.System"/> page keeps its slot untouched.
    /// </summary>
    private static void ClearMountPointForBlog(Page page)
    {
        if (page.Kind == PageKind.User)
            page.MountPoint = null;
    }

    // ─── Private helpers ───────────────────────────────────────────────────

    private async Task<Page?> LoadByParentAndSlugAsync(string? parentId, string slug)
    {
        await using var session = _store.QuerySession();
        return await LoadByParentAndSlugAsync(session, parentId, slug).ConfigureAwait(false);
    }

    // The ResolvePageAsync read seam (ADR 0043 D7) needs the path walk and the
    // translation read in a **single** session, so the walk runs against a
    // caller-owned session instead of opening one per segment.
    private static async Task<Page?> LoadByParentAndSlugAsync(
        IQuerySession session, string? parentId, string slug)
    {
        var q = session.Query<Page>()
            .Where(p => p.Slug == slug && p.IsDeleted == false);   // ADR 0024 filter
        if (parentId is null)
            q = q.Where(p => p.ParentId == null);
        else
            q = q.Where(p => p.ParentId == parentId);
        return await q.FirstOrDefaultAsync().ConfigureAwait(false);
    }
}
