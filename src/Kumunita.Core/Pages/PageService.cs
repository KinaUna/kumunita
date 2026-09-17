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
    // existing role claims (Roles.GlobalAdmin / Roles.Translator /
    // Roles.ModeratorComponent) are reused — no new AccessAction, no new
    // AccessVia, no branch in the frozen IAuthorizationService (ADR 0039).

    /// <summary>
    /// The **create** standing (ADR 0039 §3.7): a GlobalAdmin may create any
    /// page; a community Moderator may create a page scoped to their
    /// community (<c>page.ComponentId</c>); a plain Member may not. A
    /// flat/public page (<c>ComponentId</c> null) requires a GlobalAdmin.
    /// </summary>
    public static void CheckCreateStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the create standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;
        if (page.ComponentId is not null && actorRoles.Contains(Roles.ModeratorComponent(page.ComponentId)))
            return;

        throw new UnauthorizedAccessException(
            $"Only a GlobalAdmin or a moderator of community '{page.ComponentId}' may create a page.");
    }

    /// <summary>
    /// The **edit** standing (ADR 0039 §3.7): the page's <c>AuthorId</c> may
    /// edit; a GlobalAdmin may edit any page; a community Moderator may edit a
    /// page scoped to their community (<c>page.ComponentId</c>). A plain
    /// Member who did not author it is denied.
    /// </summary>
    public static void CheckEditStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the edit standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
            return;
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;
        if (page.ComponentId is not null && actorRoles.Contains(Roles.ModeratorComponent(page.ComponentId)))
            return;

        throw new UnauthorizedAccessException(
            $"Only the author, a GlobalAdmin, or a moderator of community '{page.ComponentId}' may edit this page.");
    }

    /// <summary>
    /// The **translation** standing (ADR 0039 §3.7 / ADR 0029 carried over):
    /// a GlobalAdmin or a Translator may add a translation of any page; a
    /// community Moderator may add a translation of a page scoped to their
    /// community (<c>page.ComponentId</c>). A flat/public page has no community
    /// to moderate, so the component-moderator standing does not qualify —
    /// only GlobalAdmin / Translator may translate it.
    /// </summary>
    public static void CheckTranslateStanding(string actorId, IReadOnlySet<string> actorRoles, Page? page)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (page is null)
            throw new KeyNotFoundException("A page is required for the translation standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required.");

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;
        if (actorRoles.Contains(Roles.Translator))
            return;
        if (page.ComponentId is not null && actorRoles.Contains(Roles.ModeratorComponent(page.ComponentId)))
            return;

        throw new UnauthorizedAccessException(
            $"Only a GlobalAdmin, a Translator, or a moderator of community '{page.ComponentId}' may add a translation.");
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

    // ─── Write lanes (ADR 0039 §3.7 — U03) ─────────────────────────────────
    //
    // Each write lane (a) re-checks standing **server-side** (the C3
    // single-source pin — a Web [Authorize(Roles=…)] is a convenience pre-gate,
    // not the source of truth), (b) persists the mutation, and (c) stores an
    // AccessAudit row **in the caller's in-flight IDocumentSession**
    // (invariant C3, ADR 0006 — synchronous, in-transaction, not a Wolverine
    // side effect) with TargetKind = "page" and the §3.7 action name. The
    // standing helpers (U02) are the pure gate; the **move/delete** standing
    // (admin/mod only, *not* a plain author) is a distinct resolver below,
    // because CheckEditStanding (U02) deliberately allows the author and
    // move/delete must not (design doc §3.7: a page is platform content, not a
    // personal note — AccessVia is Admin/Moderator, never Owner, for these two).
    // The ImageIds / AttachmentIds fields are populated by the caller (the
    // Web layer's ContentImageIds.ExtractContentImageIds /
    // AttachmentIds.ExtractAttachmentIds idiom, ADR 0025 / ADR 0034 — the
    // RC U04/U05 + ATT U5 shape: Core never parses the body, it normalizes
    // the POCO's fields via ?? []). The client never sends them.

    /// <summary>
    /// Creates a <see cref="Page"/> in the **caller's** in-flight session
    /// (C3). Standing (§3.7): a GlobalAdmin or a community Moderator scoped
    /// to <see cref="Page.ComponentId"/>; a plain Member is denied (403).
    /// A root-slug collision (two roots sharing a slug) is a
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

        // Standing re-check (server-side, C3 single-source pin).
        // The helper requires a non-null page — pass the (still-in-memory)
        // incoming doc, not a DB load. CheckCreateStanding is a pure role-claim
        // check: GlobalAdmin or a ModeratorComponent(page.ComponentId).
        CheckCreateStanding(actorId, actorRoles, page);

        // Root-slug guard (the index does not cover it — Postgres treats NULLs
        // as distinct, the U01 drift note): if ParentId is null, no *other*
        // root page may already carry this slug.
        if (page.ParentId is null)
        {
            var existingRoot = await session.Query<Page>()
                .Where(p => p.ParentId == null && p.Slug == page.Slug && p.IsDeleted == false)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            if (existingRoot is not null && !string.Equals(existingRoot.Id, page.Id, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"A root page with slug '{page.Slug}' already exists (id '{existingRoot.Id}'); " +
                    "choose a different slug or nest the new page under a parent.");
        }

        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrEmpty(page.Id))
            page.Id = Guid.NewGuid().ToString("N");
        page.AuthorId = actorId;
        page.Created = now;
        page.ImageIds = page.ImageIds ?? [];       // RC ADR 0025 — caller-parsed, never spoofed
        page.AttachmentIds = page.AttachmentIds ?? []; // ATT ADR 0034 — caller-parsed, never spoofed

        // Resolve the audit Via tag (Admin for GlobalAdmin, Moderator for a
        // component-moderator) before storing. A create has no stored author,
        // so the standing is the narrowest of {GlobalAdmin, Moderator}.
        var via = ResolveWriteStandingVia(actorRoles, page.ComponentId);

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
    /// session (C3). Standing (§3.7): the author, a GlobalAdmin, or a
    /// community Moderator scoped to <see cref="Page.ComponentId"/>.
    /// A missing page is a <see cref="KeyNotFoundException"/> (404); a denied
    /// actor is a <see cref="UnauthorizedAccessException"/> (403).
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

        // The audit Via tag (narrowest standing that applied): Owner if the
        // actor is the author, else Moderator (component-scoped), else Admin.
        var via = ResolveWriteStandingVia(
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
    /// **caller's** in-flight session (C3). Standing (§3.7): a GlobalAdmin or
    /// a community Moderator scoped to <see cref="Page.ComponentId"/> —
    /// **not** a plain author. Applies the cycle-guard + depth-cap; a
    /// <paramref name="newSlug"/> change is authoritative for root-level
    /// uniqueness. The derived path is rewritten by the single
    /// <see cref="Page.ParentId"/> / <see cref="Page.Slug"/> column write.
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

        // Standing re-check (server-side): admin/mod only, NOT author.
        var via = ResolveMoveDeleteStanding(page.ComponentId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                $"Only a GlobalAdmin or a moderator of community '{page.ComponentId}' may move a page.");

        // Cycle guard + depth cap (the U02 hierarchy guards).
        await EnsureNoCycleAsync(pageId, newParentId).ConfigureAwait(false);
        await EnsureDepthWithinLimitAsync(newParentId).ConfigureAwait(false);

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
    /// stamps <see cref="Page.Modified"/>. Standing (§3.7): a GlobalAdmin or
    /// a community Moderator scoped to <see cref="Page.ComponentId"/> —
    /// **not** a plain author.
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

        // Standing re-check (server-side): admin/mod only, NOT author.
        var via = ResolveMoveDeleteStanding(page.ComponentId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                $"Only a GlobalAdmin or a moderator of community '{page.ComponentId}' may delete a page.");

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
    /// §3.7). Standing: a GlobalAdmin, a Translator, or a community Moderator
    /// scoped to <see cref="Page.ComponentId"/>; a flat/public page has no
    /// community to moderate, so the component-moderator standing does not
    /// qualify. The <c>(PageId, LanguageCode)</c> unique index (U01) is the
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

        // Standing re-check (server-side): GlobalAdmin / Translator /
        // ModeratorComponent(page.ComponentId). CheckTranslateStanding is
        // the U02 pure helper — it throws UnauthorizedAccessException if the
        // actor has no qualifying standing.
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
            page.ComponentId, actorId, actorRoles);

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

    // ─── Write-lane standing resolvers (U03 — distinct from U02's helpers) ─

    /// <summary>
    /// The **move/delete** standing resolver (§3.7: a GlobalAdmin or a
    /// community Moderator scoped to <paramref name="componentId"/> — **not**
    /// a plain author). Returns the <see cref="AccessVia"/> the actor
    /// qualifies under, or <c>null</c> to deny. Precedence: Moderator (most
    /// specific) before Admin. A flat/public page (componentId null) has no
    /// community to moderate, so only a GlobalAdmin qualifies.
    /// </summary>
    private static AccessVia? ResolveMoveDeleteStanding(
        string? componentId, string actorId, IReadOnlySet<string> actorRoles)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (componentId is not null
            && actorRoles.Contains(Roles.ModeratorComponent(componentId)))
            return AccessVia.Moderator;
        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;
        return null;
    }

    /// <summary>
    /// The **translation** standing resolver (design doc §3.7 / ADR 0029
    /// carried over): a Translator or a GlobalAdmin (<see cref="AccessVia
    /// .Admin"/>), or a community Moderator scoped to
    /// <paramref name="componentId"/> (<see cref="AccessVia.Moderator"/>).
    /// A flat/public page (componentId null) has no community to moderate,
    /// so the component-moderator branch is excluded for it. Precedence:
    /// Moderator (most specific) before Admin.
    /// </summary>
    private static AccessVia ResolveTranslationStandingVia(
        string? componentId, string actorId, IReadOnlySet<string> actorRoles)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (componentId is not null
            && actorRoles.Contains(Roles.ModeratorComponent(componentId)))
            return AccessVia.Moderator;
        return AccessVia.Admin;   // GlobalAdmin or Translator — both map to Admin
    }

    /// <summary>
    /// Resolves the <see cref="AccessVia"/> tag for the **create** and
    /// **edit** write lanes' audit rows. Precedence (most specific / narrowest
    /// right first, so the audit row records the narrowest standing that
    /// applied): <c>Owner</c> (the author, edit-lane only) → <c>Moderator</c>
    /// (a community-moderator scoped to <paramref name="moderatorComponentId"/>)
    /// → <c>Admin</c> (GlobalAdmin). The caller has already verified the
    /// standing before calling this — it is a pure tag-resolution, not a gate.
    /// </summary>
    private static AccessVia ResolveWriteStandingVia(
        IReadOnlySet<string> actorRoles,
        string? moderatorComponentId,
        bool isAuthor = false)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (isAuthor)
            return AccessVia.Owner;
        if (moderatorComponentId is not null
            && actorRoles.Contains(Roles.ModeratorComponent(moderatorComponentId)))
            return AccessVia.Moderator;
        return AccessVia.Admin;
    }

    // ─── Private helpers ───────────────────────────────────────────────────

    private async Task<Page?> LoadByParentAndSlugAsync(string? parentId, string slug)
    {
        await using var session = _store.QuerySession();
        var q = session.Query<Page>()
            .Where(p => p.Slug == slug && p.IsDeleted == false);   // ADR 0024 filter
        if (parentId is null)
            q = q.Where(p => p.ParentId == null);
        else
            q = q.Where(p => p.ParentId == parentId);
        return await q.FirstOrDefaultAsync().ConfigureAwait(false);
    }
}
