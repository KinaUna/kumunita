using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Bookmarks;

/// <summary>
/// The <c>M17</c> (Bookmarks) composition service (ADR 0118 / design doc
/// <c>m17-bookmarks-design.md</c> §2.2–§2.5). A concrete class pairing the
/// frozen seams with the host-registered Marten <see cref="IDocumentStore"/>
/// (the M2 <c>DirectoryService</c> / M3 <c>PostService</c> shape) — it
/// **composes** seams, it is not itself one.
/// <para>
/// **M17 composes only frozen seams** (C-M17·1): the frozen
/// <see cref="IAuthorizationService"/> (the D3 write-lane check only) + the
/// owning surfaces' existing read seams (the D5 list resolution) over the
/// <see cref="IDocumentStore"/>. **No** <c>INotificationService</c> (D7),
/// **no** new <c>AccessAction</c> / <c>AccessVia</c> / <c>Decide()</c>
/// branch, **no** adapter of its own.
/// </para>
/// <para>
/// The D3 write-lane check and the D5 list resolution both resolve a target
/// the *same way the target's own detail page does* (F2 / C-M17·3): a
/// <c>post</c> is the <see cref="PostService"/> draft-gate +
/// <c>CanAsync(Read, <see cref="PostToAuditableResource"/>)</c>; a
/// <c>page</c> is the <see cref="PageService"/> draft-gate +
/// <c>CanAsync(Read, <see cref="PageToAuditableResource"/>)</c>; an
/// <c>event</c> / <c>todo</c> / <c>announcement</c> is the owning surface's
/// own frozen read seam (<see cref="IEventService.GetAsync"/> /
/// <see cref="IProjectService.GetTodoAsync"/> /
/// <see cref="IAnnouncementService.GetAsync"/>). This service makes no
/// decision of its own.
/// </para>
/// </summary>
public sealed class BookmarkService : IBookmarkService
{
    /// <summary>
    /// The closed <c>TargetKind</c> set (D1 — the M5 <c>KanbanStatuses</c>
    /// string-not-enum shape), the exact vocabulary the existing
    /// <c>*ToAuditableResource</c> adapters and <c>AccessAudit</c> rows
    /// already emit. A kind outside this set is a drift event (§2.7).
    /// </summary>
    public static readonly IReadOnlySet<string> ClosedTargetKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "post", "event", "todo", "announcement", "page",
        };

    // The closed set's canonical order (for group ordering in the list).
    private static readonly string[] ClosedTargetKindsOrdered =
        ["post", "event", "todo", "announcement", "page"];

    private readonly IUserInfoService _userInfo;
    private readonly IAuthorizationService _authz;
    private readonly IDocumentStore _store;
    private readonly IAnnouncementService _announcements;
    private readonly IPageService _pages;
    private readonly IEventService _events;
    private readonly IProjectService _projects;
    private readonly IIdentityService _identity;

    /// <summary>
    /// Composes the eight frozen seams over the host-registered Marten
    /// <see cref="IDocumentStore"/> (design doc §2.2 composition root —
    /// **no** <c>INotificationService</c>, D7). The extra seam versus the
    /// original seven is the frozen <see cref="IIdentityService"/> (ADR 0006
    /// §A) — used **only** to resolve the owner's real standing (their
    /// thin-principal role set) when the bookmark target is an announcement,
    /// so the D3 write-lane check re-runs the *same* read the announcement
    /// detail page uses (ADR 0118 D3/F2). It never crosses the frozen
    /// <see cref="IBookmarkService"/> seam — roles are an in-Core composition
    /// detail, exactly the category of the already-composed
    /// <see cref="IUserInfoService"/>.
    /// </summary>
    public BookmarkService(
        IUserInfoService userInfo,
        IAuthorizationService authz,
        IDocumentStore store,
        IAnnouncementService announcements,
        IPageService pages,
        IEventService events,
        IProjectService projects,
        IIdentityService identity)
    {
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _authz = authz ?? throw new ArgumentNullException(nameof(authz));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _announcements = announcements ?? throw new ArgumentNullException(nameof(announcements));
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
    }

    // ── List (D5 degraded resolution; C-M17·2 zero-audit personal read) ───

    /// <inheritdoc />
    public async Task<BookmarkListResult> ListAsync(string ownerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        // C-M17·2 — the owner's own rows, loaded by the identity predicate
        // alone (no AccessAudit row, no CanSeeAsync pass over the bookmark
        // rows — the ADR 0105 / M6-inbox personal-read shape).
        await using var q = _store.QuerySession();
        var rows = await q.Query<Bookmark>()
            .Where(b => b.OwnerId == ownerId)
            .OrderByDescending(b => b.Created)
            .ToListAsync()
            .ConfigureAwait(false);

        // Resolve each row (D5) via a **pure, non-auditing doc load** — the
        // C-M17·2 / ADR 0105 "reads never audit" shape (the
        // <see cref="EventService.GetAsync"/> / <see cref="AnnouncementService"/>.
        // GetAsync precedent). Resolved rows carry Title + Link; a dead /
        // hidden target degrades to (null, null) so the Web layer renders the
        // closed bm.list.degraded label (C-M17·5). Deliberately NOT the D3
        // write-lane check (which re-runs the target's frozen CanAsync(Read)
        // and would commit the target's own audit row — forbidden on the
        // personal list read by C-M17·2 / F3_OwnerListLoadsZeroAuditRows).
        var resolved = new List<(Bookmark Row, string? Title, string? Link)>();
        foreach (var row in rows)
        {
            var (title, link) = await ResolveForListAsync(row.TargetKind, row.TargetId, ownerId).ConfigureAwait(false);
            resolved.Add((row, title, link));
        }

        // Group by TargetKind in the closed set's canonical order (D1), only
        // including kinds the owner actually pinned.
        var groups = new List<BookmarkGroup>();
        foreach (var kind in ClosedTargetKindsOrdered)
        {
            var items = resolved
                .Where(r => string.Equals(r.Row.TargetKind, kind, StringComparison.Ordinal))
                .Select(r => new BookmarkRow(
                    kind,
                    r.Row.TargetId,
                    r.Title,
                    r.Link,
                    r.Title is null && r.Link is null,
                    r.Row.Created))
                .ToList();
            if (items.Count > 0)
                groups.Add(new BookmarkGroup(kind, items));
        }

        return new BookmarkListResult(groups);
    }

    // ── Toggle (D3 write-lane visibility check; F1 idempotent) ────────────

    /// <inheritdoc />
    public async Task<BookmarkToggleResult> ToggleAsync(
        string ownerId, string targetKind, string targetId, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(session);

        // The closed set is the boundary of the pin world (D1): a kind
        // outside it has no owning surface to re-check, so there is no
        // target to bookmark (fail-closed — the Web layer 404s it).
        if (!ClosedTargetKinds.Contains(targetKind))
            return new BookmarkToggleResult(BookmarkToggleStatus.Refused);

        // D3 (design doc §2.3) — re-run the target's own frozen Read decision
        // FIRST, the *same call the target's detail page uses*: Deny / absent
        // / soft-deleted ⇒ Refused (404; no row; the target's own audit row is
        // the decision's, not M17's).
        var (visible, _, _) = await ResolveTargetAsync(targetKind, targetId, ownerId).ConfigureAwait(false);
        if (!visible)
            return new BookmarkToggleResult(BookmarkToggleStatus.Refused);

        // F1 — the unique (OwnerId, TargetKind, TargetId) row is at-most-one:
        // an existing row is the no-op (AlreadyBookmarked), one row, one
        // Created. The M17DocTypes unique index is the DB witness.
        var existing = await session.Query<Bookmark>()
            .Where(b => b.OwnerId == ownerId && b.TargetKind == targetKind && b.TargetId == targetId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (existing is not null)
            return new BookmarkToggleResult(BookmarkToggleStatus.AlreadyBookmarked);

        session.Store(new Bookmark
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = ownerId,
            TargetKind = targetKind,
            TargetId = targetId,
            Created = DateTimeOffset.UtcNow,
        });

        return new BookmarkToggleResult(BookmarkToggleStatus.Bookmarked);
    }

    // ── Remove (D4 physical removal; no target read) ──────────────────────

    /// <inheritdoc />
    public async Task<BookmarkToggleResult> RemoveAsync(
        string ownerId, string targetKind, string targetId, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(session);

        // D5 / F4 — removing a pin is keyed on the row's own id; it needs no
        // target read at all (a dangling row is removable).
        var existing = await session.Query<Bookmark>()
            .Where(b => b.OwnerId == ownerId && b.TargetKind == targetKind && b.TargetId == targetId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        if (existing is null)
            return new BookmarkToggleResult(BookmarkToggleStatus.NotBookmarked);

        // D4 — physical removal (no IsDeleted to flip).
        session.Delete(existing);
        return new BookmarkToggleResult(BookmarkToggleStatus.Removed);
    }

    // ── Button-state read (F1; the C-M17·2 personal-read shape) ───────────

    /// <inheritdoc />
    public async Task<bool> IsBookmarkedAsync(
        string ownerId, string targetKind, string targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);

        // No ownerId ⇒ no personal read standing (the C-M17·2 "operator has
        // no read standing" gate): an anonymous / no-SubjectId caller can
        // never own a row, so answer false with no query at all.
        if (string.IsNullOrWhiteSpace(ownerId))
            return false;

        // The closed set is the boundary of the pin world (D1): a kind
        // outside it has no owning surface, so it can never be bookmarked.
        if (!ClosedTargetKinds.Contains(targetKind))
            return false;

        // C-M17·2 — a pure existence check over the owner's own row, by the
        // identity predicate alone: no AccessAudit row, no CanAsync pass,
        // no target read (a dangling row still answers true — the button's
        // remove affordance must work even when the target is gone, D5 / F4).
        await using var q = _store.QuerySession();
        return await q.Query<Bookmark>()
            .Where(b => b.OwnerId == ownerId && b.TargetKind == targetKind && b.TargetId == targetId)
            .AnyAsync()
            .ConfigureAwait(false);
    }

    // ── The D5 list resolution — a pure, non-auditing doc load ────────────

    /// <summary>
    /// Resolves a list row to (<c>title</c>, <c>link</c>) by **loading the
    /// target document directly** — a pure read that commits <b>no</b>
    /// <see cref="AccessAudit"/> row and makes <b>no</b>
    /// <see cref="IAuthorizationService.CanAsync"/> call
    /// (C-M17·2 / F3_OwnerListLoadsZeroAuditRows, the ADR 0105 "reads never
    /// audit" + M6-inbox personal-read shape; the
    /// <see cref="EventService.GetAsync"/> / <see cref="AnnouncementService"/>.
    /// GetAsync "pure read" precedent). Both return <c>null</c> when the
    /// target is absent, soft-deleted, or a non-author draft (the D5 degrade
    /// — C-M17·5).
    /// <para>
    /// This is deliberately distinct from the D3 write-lane check
    /// (<see cref="ResolveTargetAsync"/>): the write lane re-runs the target's
    /// own frozen <c>CanAsync(Read)</c> (the detail page's exact decision),
    /// which commits the target's own audit row. That is correct and expected
    /// at bookmark *creation* (C-M17·3), but would violate the list's
    /// zero-audit pin if reused here — so the list resolves by document shape
    /// alone (existence + not-deleted + draft-to-own), which is exactly the
    /// information a bookmark row needs to render a <c>Title</c> and a
    /// <c>Link</c>. A bookmark is a pointer the owner already earned by
    /// bookmarking a target they could see; re-asserting the audience decision
    /// on the personal read is precisely the leak the C-M17·2 pin forbids.
    /// </para>
    /// </summary>
    private async Task<(string? Title, string? Link)> ResolveForListAsync(
        string targetKind, string targetId, string actorId)
    {
        await using var s = _store.QuerySession();
        switch (targetKind)
        {
            case "post":
            {
                var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
                if (post is null || post.DeletedAt is not null)
                    return (null, null);
                // ADR 0037 — the author-only draft gate, as a pure ordinal
                // check (no CanAsync, no audit row).
                if (post.IsDraft && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
                    return (null, null);
                return (post.Title, $"/posts/{targetId}");
            }

            case "event":
            {
                var ev = await s.LoadAsync<Event>(targetId).ConfigureAwait(false);
                if (ev is null || ev.IsDeleted)
                    return (null, null);
                if (ev.IsDraft && !string.Equals(ev.AuthorId, actorId, StringComparison.Ordinal))
                    return (null, null);
                return (ev.Title, $"/events/{targetId}");
            }

            case "todo":
            {
                var todo = await s.LoadAsync<TodoItem>(targetId).ConfigureAwait(false);
                if (todo is null || todo.IsDeleted)
                    return (null, null);
                return (todo.Title, $"/projects/todos/{targetId}");
            }

            case "announcement":
            {
                var a = await s.LoadAsync<Announcement>(targetId).ConfigureAwait(false);
                if (a is null)
                    return (null, null);
                return (a.Title, $"/announcements/{targetId}");
            }

            case "page":
            {
                var page = await s.LoadAsync<Page>(targetId).ConfigureAwait(false);
                if (page is null || page.IsDeleted)
                    return (null, null);
                if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
                    return (null, null);
                var path = await BuildPagePathAsync(page.Id).ConfigureAwait(false);
                return path is null ? (null, null) : (page.Title, path);
            }

            default:
                return (null, null);
        }
    }

    // ── The D3 write-lane resolution (the owning surface's own frozen read) ─

    /// <summary>
    /// Resolves a target to (<c>visible</c>, <c>title</c>, <c>link</c>) the
    /// way the target's own detail page reads it — the single source of truth
    /// for the D3 write-lane check (§2.3). <c>title</c> / <c>link</c> are
    /// both <c>null</c> when the target is absent, soft-deleted, a non-author
    /// draft, or denied to the actor. Re-runs the target's own frozen
    /// <c>CanAsync(Read)</c> (committing the target's own audit row —
    /// C-M17·3); NOT used by the personal list read (C-M17·2).
    /// </summary>
    private async Task<(bool Visible, string? Title, string? Link)> ResolveTargetAsync(
        string targetKind, string targetId, string actorId)
    {
        switch (targetKind)
        {
            case "post":
            {
                await using var s = _store.QuerySession();
                var post = await s.LoadAsync<Post>(targetId).ConfigureAwait(false);
                if (post is null || post.DeletedAt is not null)
                    return (false, null, null);

                // ADR 0037 — the author-only draft gate (the PostService.
                // GetPostAsync shape): a non-author never passes.
                if (post.IsDraft && !string.Equals(post.AuthorId, actorId, StringComparison.Ordinal))
                    return (false, null, null);

                var decision = await _authz.CanAsync(actorId, AccessAction.Read, new PostToAuditableResource(post))
                    .ConfigureAwait(false);
                if (!decision.Allowed)
                    return (false, null, null);

                return (true, post.Title, $"/posts/{targetId}");
            }

            case "event":
            {
                try
                {
                    var ev = await _events.GetAsync(targetId, actorId).ConfigureAwait(false);
                    return (true, ev.Title, $"/events/{targetId}");
                }
                catch (KeyNotFoundException)
                {
                    return (false, null, null);
                }
                catch (UnauthorizedAccessException)
                {
                    return (false, null, null);
                }
            }

            case "todo":
            {
                try
                {
                    var detail = await _projects.GetTodoAsync(targetId, actorId).ConfigureAwait(false);
                    return (true, detail.Todo.Title, $"/projects/todos/{targetId}");
                }
                catch (KeyNotFoundException)
                {
                    return (false, null, null);
                }
                catch (UnauthorizedAccessException)
                {
                    return (false, null, null);
                }
            }

            case "announcement":
            {
                // The announcement lane has no AccessAudit / CanAsync of its
                // own — its GetAsync is the whole visibility gate (the
                // scope-vs-role split), returning null when not visible.
                //
                // D3/F2 (ADR 0118): the write-lane check must re-run *the same
                // read the detail page uses*, so the owner's real standing is
                // in play. The Bookmarks seam hands us only an owner id (roles
                // never cross the frozen IBookmarkService seam), so we resolve
                // the owner's thin-principal role set in-Core via the frozen
                // IIdentityService (the same category of composition as the
                // already-composed IUserInfoService) and hand that set to the
                // announcement read. A GlobalAdmin / community-moderator who is
                // not a member therefore resolves a community-targeted row —
                // matching the detail page — instead of the old lapse of an
                // empty role set denying them. Fail-closed shape preserved:
                // no EF account row (or no principal) ⇒ empty set ⇒ the read
                // degrades exactly as before.
                var principal = await _identity.GetBySubjectAsync(actorId).ConfigureAwait(false);
                var roles = principal is not null
                    ? new HashSet<string>(principal.Roles, StringComparer.Ordinal)
                    : new HashSet<string>(StringComparer.Ordinal);
                var a = await _announcements.GetAsync(targetId, actorId, roles)
                    .ConfigureAwait(false);
                if (a is null)
                    return (false, null, null);
                return (true, a.Title, $"/announcements/{targetId}");
            }

            case "page":
            {
                await using var s = _store.QuerySession();
                var page = await s.LoadAsync<Page>(targetId).ConfigureAwait(false);
                if (page is null || page.IsDeleted)
                    return (false, null, null);

                // ADR 0037 — the author-only draft gate (the PageController.
                // Show shape): a non-author never passes.
                if (page.IsDraft && !string.Equals(page.AuthorId, actorId, StringComparison.Ordinal))
                    return (false, null, null);

                var decision = await _authz.CanAsync(actorId, AccessAction.Read, new PageToAuditableResource(page))
                    .ConfigureAwait(false);
                if (!decision.Allowed)
                    return (false, null, null);

                var path = await BuildPagePathAsync(page.Id).ConfigureAwait(false);
                return path is null ? (false, null, null) : (true, page.Title, path);
            }

            default:
                return (false, null, null);
        }
    }

    /// <summary>
    /// The <c>/pages/</c>-prefixed ancestor-slug chain of a page (the
    /// <see cref="PageService.GetPathAsync"/> shape, walked over the store —
    /// <c>IPageService</c> exposes no by-id path lane, so the bookmark surface
    /// derives the same derived path from the <see cref="Page.ParentId"/>
    /// chain directly). <c>null</c> on a broken chain.
    /// </summary>
    private async Task<string?> BuildPagePathAsync(string pageId)
    {
        var segments = new List<string>();
        string? currentId = pageId;
        int guard = 0;
        while (currentId is not null && ++guard <= 64)
        {
            await using var s = _store.QuerySession();
            var page = await s.LoadAsync<Page>(currentId).ConfigureAwait(false);
            if (page is null)
                return null;
            if (!string.IsNullOrWhiteSpace(page.Slug))
                segments.Add(page.Slug);
            currentId = page.ParentId;
        }

        if (segments.Count == 0)
            return null;

        segments.Reverse();
        return "/pages/" + string.Join("/", segments);
    }
}
