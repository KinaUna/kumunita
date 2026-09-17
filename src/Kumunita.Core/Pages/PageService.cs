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
