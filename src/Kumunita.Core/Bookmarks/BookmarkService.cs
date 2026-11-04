using Kumunita.Core.Announcements;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Kumunita.Core.Inventory;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Projects;
using Kumunita.Core.Targets;
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
    /// <para>
    /// The <c>project</c> / <c>board</c> kinds joined by the ADR 0118
    /// amendment (2026-10-16) — the M37 PersonalNotes closed-set
    /// vocabulary already carried them; the M17 surface now matches
    /// (the project / board detail surfaces gain the bookmark
    /// affordance; the D3 write-lane check re-runs the owning
    /// surface's own frozen <c>IProjectService.GetProjectAsync</c> /
    /// <c>GetBoardAsync</c> read — the same call the detail page uses,
    /// committing the target's own audit row, exactly the category
    /// the existing <c>todo</c> branch already establishes).
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> ClosedTargetKinds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "post", "event", "todo", "announcement", "page", "project", "board", "inventory", "directory",
        };

    // The closed set's canonical order (for group ordering in the list).
    private static readonly string[] ClosedTargetKindsOrdered =
        ["post", "event", "todo", "announcement", "page", "project", "board", "inventory", "directory"];

    private readonly IDocumentStore _store;
    private readonly IContentTargetResolver _resolver;

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
        IDocumentStore store,
        IContentTargetResolver resolver)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
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
            var (title, link) = await _resolver.ResolveForListAsync(row.TargetKind, row.TargetId, ownerId, ContentTargetKinds.Bookmarks).ConfigureAwait(false);
            resolved.Add((row, title, link));
        }

        // Group by (TargetKind, FolderId) in the closed set's canonical order
        // (D1), only including kinds the owner actually pinned (ADR 0168 D2 —
        // the M37 ListNotesAsync shape re-applied: one group per folder, then
        // the unfiled bucket, within each kind).
        var groups = new List<BookmarkGroup>();
        foreach (var kind in ClosedTargetKindsOrdered)
        {
            var kindRows = resolved
                .Where(r => string.Equals(r.Row.TargetKind, kind, StringComparison.Ordinal))
                .ToList();
            if (kindRows.Count == 0) continue;

            // Root bucket (FolderId = null — the unfiled rows).
            var rootItems = kindRows
                .Where(r => r.Row.FolderId is null)
                .Select(r => ToBookmarkRow(kind, r))
                .ToList();
            if (rootItems.Count > 0)
                groups.Add(new BookmarkGroup(kind, rootItems, null));

            // Folder sub-buckets (ADR 0168 D2 — the owner's tree).
            var folderIds = kindRows
                .Select(r => r.Row.FolderId)
                .Where(f => f is not null)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            foreach (var folderId in folderIds)
            {
                var items = kindRows
                    .Where(r => r.Row.FolderId == folderId)
                    .Select(r => ToBookmarkRow(kind, r))
                    .ToList();
                groups.Add(new BookmarkGroup(kind, items, folderId));
            }
        }

        // Load the owner's folders (ADR 0168 D2 — the personal tree).
        var folders = await q.Query<BookmarkFolder>()
            .Where(f => f.OwnerId == ownerId)
            .OrderBy(f => f.Created)
            .ToListAsync()
            .ConfigureAwait(false);

        var folderRows = folders
            .Select(f => new FolderRow(f.Id, f.Name, f.ParentId,
                rows.Count(b => b.FolderId == f.Id)))
            .ToList();

        return new BookmarkListResult(groups, folderRows);
    }

    /// <summary>
    /// Builds a <see cref="BookmarkRow"/> from a resolved bookmark
    /// (<c>title</c>, <c>link</c>) — carrying the row's own
    /// <see cref="BookmarkRow.Id"/> and <see cref="BookmarkRow.FolderId"/>
    /// (ADR 0168 D2) so the Web layer can render the per-row move control
    /// and the folder nesting.
    /// </summary>
    private static BookmarkRow ToBookmarkRow(
        string kind, (Bookmark Row, string? Title, string? Link) r)
    {
        bool degraded = r.Title is null && r.Link is null;
        return new BookmarkRow(
            kind,
            r.Row.TargetId,
            r.Title,
            r.Link,
            degraded,
            r.Row.Created,
            r.Row.Id,
            r.Row.FolderId);
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
        var (visible, _, _) = await _resolver.ResolveAsync(targetKind, targetId, ownerId, ContentTargetKinds.Bookmarks).ConfigureAwait(false);
        if (!visible)
            return new BookmarkToggleResult(BookmarkToggleStatus.Refused);

        // F1 — the unique (OwnerId, TargetKind, TargetId) row is at-most-one:
        // an existing row is the no-op (AlreadyBookmarked), one row, one
        // Created. The BookmarkDocTypes unique index is the DB witness.
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

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> ListBookmarkedIdsAsync(
        string ownerId, string targetKind, IReadOnlyCollection<string> targetIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentNullException.ThrowIfNull(targetIds);

        // ADR 0227 — same gates as IsBookmarkedAsync: no owner / unknown kind /
        // nothing asked ⇒ empty, no query.
        if (string.IsNullOrWhiteSpace(ownerId)
            || !ClosedTargetKinds.Contains(targetKind)
            || targetIds.Count == 0)
            return new HashSet<string>(StringComparer.Ordinal);

        await using var q = _store.QuerySession();
        var ids = await q.Query<Bookmark>()
            .Where(b => b.OwnerId == ownerId && b.TargetKind == targetKind && targetIds.Contains(b.TargetId))
            .Select(b => b.TargetId)
            .ToListAsync()
            .ConfigureAwait(false);
        return new HashSet<string>(ids, StringComparer.Ordinal);
    }

    // ── Folder-tree methods (ADR 0168 D2 — the M37 folder-tree shape
    //    re-applied to the bookmark surface) ─────────────────────────────
    //
    // D2 (ADR 0168): the folder tree is a personal tree, bounded in depth
    // (≤ 2 — a new folder's parent must itself be a root), unique in name
    // within the owner's tree (the (OwnerId, ParentId, Name) unique index
    // is the witness), a bookmark's FolderId is the owner's folder id (a
    // bookmark cannot live in another resident's folder), and a folder's
    // deletion MOVES its bookmarks to the root (FolderId = null) — the
    // bookmark is never lost. The M37 C-M37·6 pin, re-applied to the
    // bookmark surface (the §deferred "bookmark folders" lane from
    // m17-bookmarks-design.md, now settled).

    /// <inheritdoc />
    public async Task<FolderRow> CreateFolderAsync(
        string ownerId, string name, string? parentId, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(session);

        // D2 — the depth ≤ 2 pin: if a parent is given, it must itself be a
        // ROOT (ParentId == null). A folder whose parent is a non-root would
        // open the tree past the bound (the ADR 0168 "arbitrary-depth folder
        // tree" forbid). A foreign / absent parent is a non-leaky 404. The
        // depth violation throws InvalidOperationException (the Web layer maps
        // it to 400 — the depth is violated).
        if (parentId is not null)
        {
            var parent = await session.LoadAsync<BookmarkFolder>(parentId).ConfigureAwait(false);
            if (parent is null || !string.Equals(parent.OwnerId, ownerId, StringComparison.Ordinal))
                throw new KeyNotFoundException($"Parent folder '{parentId}' not found for owner '{ownerId}'.");
            if (parent.ParentId is not null)
                throw new InvalidOperationException(
                    "Folder depth exceeds the bound (≤ 2): a new folder's parent must be a root folder.");
        }

        // D2 — the (OwnerId, ParentId, Name) unique index is the witness: a
        // sibling already holding the name under this parent is the no-op
        // (return the existing row — the M17 Bookmark idempotent-create
        // shape, adapted to the frozen <see cref="FolderRow"/> return). The
        // same name under a *different* parent is allowed (the index is
        // keyed on all three).
        var existing = parentId is null
            ? await session.Query<BookmarkFolder>()
                .Where(f => f.OwnerId == ownerId && f.Name == name && f.ParentId == null)
                .FirstOrDefaultAsync().ConfigureAwait(false)
            : await session.Query<BookmarkFolder>()
                .Where(f => f.OwnerId == ownerId && f.Name == name && f.ParentId == parentId)
                .FirstOrDefaultAsync().ConfigureAwait(false);
        if (existing is not null)
            return ToFolderRow(existing, await FolderBookmarkCountAsync(session, ownerId, existing.Id).ConfigureAwait(false));

        var folder = new BookmarkFolder
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = ownerId,
            ParentId = parentId,
            Name = name,
            Created = DateTimeOffset.UtcNow,
        };
        session.Store(folder);
        return new FolderRow(folder.Id, folder.Name, folder.ParentId, 0);
    }

    /// <inheritdoc />
    public async Task<FolderRow> RenameFolderAsync(
        string ownerId, string folderId, string newName, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        ArgumentNullException.ThrowIfNull(session);

        var folder = await session.LoadAsync<BookmarkFolder>(folderId).ConfigureAwait(false);
        // Owner pin (D2): a non-owner — a GlobalAdmin included — gets a
        // non-leaky 404 (no leak that the folder exists).
        if (folder is null || !string.Equals(folder.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Folder '{folderId}' not found for owner '{ownerId}'.");

        // No-op when the name is unchanged.
        if (string.Equals(folder.Name, newName, StringComparison.Ordinal))
            return ToFolderRow(folder, await FolderBookmarkCountAsync(session, ownerId, folderId).ConfigureAwait(false));

        // D2 — the (OwnerId, ParentId, Name) unique index is the witness: a
        // sibling already holding the new name under the same parent is a
        // conflict the DB would reject on save; surfaced as
        // InvalidOperationException (the Web layer maps to 400/409).
        var conflict = folder.ParentId is null
            ? await session.Query<BookmarkFolder>()
                .Where(f => f.OwnerId == ownerId && f.Name == newName && f.ParentId == null && f.Id != folderId)
                .AnyAsync().ConfigureAwait(false)
            : await session.Query<BookmarkFolder>()
                .Where(f => f.OwnerId == ownerId && f.Name == newName && f.ParentId == folder.ParentId && f.Id != folderId)
                .AnyAsync().ConfigureAwait(false);
        if (conflict)
            throw new InvalidOperationException(
                $"A folder named '{newName}' already exists in this part of the owner's tree.");

        folder.Name = newName;
        session.Store(folder);
        return ToFolderRow(folder, await FolderBookmarkCountAsync(session, ownerId, folderId).ConfigureAwait(false));
    }

    /// <inheritdoc />
    public async Task<BookmarkFolderDeleteResult> DeleteFolderAsync(
        string ownerId, string folderId, IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentNullException.ThrowIfNull(session);

        var folder = await session.LoadAsync<BookmarkFolder>(folderId).ConfigureAwait(false);
        // Owner pin (D2): a non-owner gets a non-leaky 404 (no row, no leak).
        if (folder is null || !string.Equals(folder.OwnerId, ownerId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Folder '{folderId}' not found for owner '{ownerId}'.");

        // D2 — the folder's bookmarks are MOVED to the root (FolderId =
        // null); the bookmark rows are never lost (the folder row is
        // physically removed — the M17 Bookmark physical-removal shape — but
        // the bookmark rows are re-parented, not deleted).
        await MoveFolderBookmarksToRootAsync(session, ownerId, folderId).ConfigureAwait(false);

        // D2 — child folders are recursively deleted (their bookmarks move
        // to the root too). The tree is bounded (≤ 2), so there is at most
        // one level of children; the loop is a belt-and-suspenders over that
        // bound. Deleting a folder that has children is the FoldersDeleted
        // outcome; a leaf folder (bookmarks only) is the BookmarksMoved
        // outcome.
        var result = BookmarkFolderDeleteResult.BookmarksMoved;
        var children = await session.Query<BookmarkFolder>()
            .Where(f => f.OwnerId == ownerId && f.ParentId == folderId)
            .ToListAsync().ConfigureAwait(false);
        foreach (var child in children)
        {
            await MoveFolderBookmarksToRootAsync(session, ownerId, child.Id).ConfigureAwait(false);
            // Grandchildren — bounded, but recurse for safety (no unbounded
            // surface per D2; the guard below is defensive only).
            var grandchildren = await session.Query<BookmarkFolder>()
                .Where(f => f.OwnerId == ownerId && f.ParentId == child.Id)
                .ToListAsync().ConfigureAwait(false);
            foreach (var grandchild in grandchildren)
                await MoveFolderBookmarksToRootAsync(session, ownerId, grandchild.Id).ConfigureAwait(false);
            session.Delete(child);
            result = BookmarkFolderDeleteResult.FoldersDeleted;
        }

        session.Delete(folder);
        return result;
    }

    /// <inheritdoc />
    public async Task<BookmarkRow> MoveBookmarkToFolderAsync(
        string ownerId, string targetKind, string targetId, string? folderId,
        IDocumentSession session)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetId);
        ArgumentNullException.ThrowIfNull(session);

        var bookmark = await session.Query<Bookmark>()
            .Where(b => b.OwnerId == ownerId && b.TargetKind == targetKind && b.TargetId == targetId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);
        // Owner pin: a non-owner gets a non-leaky 404.
        if (bookmark is null)
            throw new KeyNotFoundException($"Bookmark '{targetKind}/{targetId}' not found for owner '{ownerId}'.");

        // D2 — the bookmark and the folder are in the SAME owner's tree: a
        // bookmark cannot live in another resident's folder (C-M17·6, the
        // M37 C-M37·6 pin re-applied). An absent or foreign folder is a
        // non-leaky 404 (no leak that the folder exists).
        if (folderId is not null)
        {
            var folder = await session.LoadAsync<BookmarkFolder>(folderId).ConfigureAwait(false);
            if (folder is null || !string.Equals(folder.OwnerId, ownerId, StringComparison.Ordinal))
                throw new KeyNotFoundException($"Folder '{folderId}' not found for owner '{ownerId}'.");
        }

        bookmark.FolderId = folderId; // null = move the bookmark to the root.
        session.Store(bookmark);

        // Resolve the (possibly dead) target for the row's Title / Link —
        // the D5 degrade (a dangling bookmark still renders the
        // bm.list.degraded label; the move control works regardless).
        var (title, link) =
            await _resolver.ResolveForListAsync(bookmark.TargetKind, bookmark.TargetId, ownerId, ContentTargetKinds.Bookmarks).ConfigureAwait(false);
        return new BookmarkRow(
            bookmark.TargetKind,
            bookmark.TargetId,
            title,
            link,
            title is null && link is null,
            bookmark.Created,
            bookmark.Id,
            bookmark.FolderId);
    }

    // ── Folder-tree private helpers (ADR 0168) ───────────────────────────

    /// <summary>Builds a <see cref="FolderRow"/> from a
    /// <see cref="BookmarkFolder"/> + the folder's bookmark count.</summary>
    private static FolderRow ToFolderRow(BookmarkFolder folder, int bookmarkCount)
        => new FolderRow(folder.Id, folder.Name, folder.ParentId, bookmarkCount);

    /// <summary>
    /// The count of the owner's bookmarks sitting in the given folder (the
    /// <see cref="FolderRow.BookmarkCount"/> shape; the M17 shape of
    /// counting over the identity predicate).
    /// </summary>
    private static async Task<int> FolderBookmarkCountAsync(
        IDocumentSession session, string ownerId, string folderId)
        => await session.Query<Bookmark>()
            .Where(b => b.OwnerId == ownerId && b.FolderId == folderId)
            .CountAsync()
            .ConfigureAwait(false);

    /// <summary>
    /// Moves every owner-bookmark in the given folder to the root
    /// (<c>FolderId = null</c>) — the D2 pin: the bookmarks are never lost,
    /// the folder deletion is a re-parent, not a bookmark removal.
    /// </summary>
    private static async Task MoveFolderBookmarksToRootAsync(
        IDocumentSession session, string ownerId, string folderId)
    {
        var bookmarks = await session.Query<Bookmark>()
            .Where(b => b.OwnerId == ownerId && b.FolderId == folderId)
            .ToListAsync()
            .ConfigureAwait(false);
        foreach (var bookmark in bookmarks)
        {
            bookmark.FolderId = null; // D2 — moved to the root; never lost.
            session.Store(bookmark);
        }
    }

    // ── Committed write overloads (IMPROVE-CODE C09 — the controller's
    //    caller-owns-commit write lanes moved off the controller) ─────────
    // The house idiom (the <c>PersonalNoteService</c> C09 precedent): a
    // service method with NO <see cref="IDocumentSession"/> param opens its
    // own session, does the work, and commits — the controller drops
    // <c>IDocumentStore</c> from its ctor (ADR 0006 §D "route + authz +
    // shape"). Each overload delegates to the frozen session-based method
    // above (the D3 / D4 / ADR 0168 pins are unchanged) and owns the single
    // write (the C3 "caller owns the single write" lane, now the service's).

    /// <summary>
    /// The committed <c>Toggle</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.BookmarksController"/>
    /// <c>Toggle</c> write lane moved behind this service, ADR 0006 §D).
    /// Delegates to the session-based <see cref="ToggleAsync"/> in the
    /// service's own session (the D3 write-lane check is unchanged); the
    /// controller's flash / redirect / 404 mapping is unchanged.
    /// </summary>
    public async Task<BookmarkToggleResult> ToggleAsync(
        string ownerId, string targetKind, string targetId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var result = await ToggleAsync(ownerId, targetKind, targetId, session).ConfigureAwait(false);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// The committed <c>Remove</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.BookmarksController"/>
    /// <c>Remove</c> write lane moved behind this service, ADR 0006 §D).
    /// Delegates to the session-based <see cref="RemoveAsync"/> in the
    /// service's own session (the D4 physical-removal pin is unchanged);
    /// the controller's flash / redirect mapping is unchanged.
    /// </summary>
    public async Task<BookmarkToggleResult> RemoveAsync(
        string ownerId, string targetKind, string targetId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var result = await RemoveAsync(ownerId, targetKind, targetId, session).ConfigureAwait(false);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// The committed <c>CreateFolder</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.BookmarksController"/>
    /// <c>FolderNewSave</c> write lane moved behind this service, ADR
    /// 0006 §D). Delegates to the session-based
    /// <see cref="CreateFolderAsync"/> in the service's own session (the
    /// ADR 0168 D2 pins are unchanged); the controller's flash / redirect
    /// / 404 / 400 mapping is unchanged.
    /// </summary>
    public async Task<FolderRow> CreateFolderAsync(
        string ownerId, string name, string? parentId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var result = await CreateFolderAsync(ownerId, name, parentId, session).ConfigureAwait(false);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// The committed <c>RenameFolder</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.BookmarksController"/>
    /// <c>FolderRenameSave</c> write lane moved behind this service, ADR
    /// 0006 §D). Delegates to the session-based
    /// <see cref="RenameFolderAsync"/> in the service's own session (the
    /// ADR 0168 D2 pins are unchanged); the controller's flash / redirect
    /// / 404 / 400 mapping is unchanged.
    /// </summary>
    public async Task<FolderRow> RenameFolderAsync(
        string ownerId, string folderId, string newName, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var result = await RenameFolderAsync(ownerId, folderId, newName, session).ConfigureAwait(false);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// The committed <c>DeleteFolder</c> lane (IMPROVE-CODE C09 — the
    /// <see cref="Kumunita.Web.Controllers.BookmarksController"/>
    /// <c>FolderDelete</c> write lane moved behind this service, ADR
    /// 0006 §D). Delegates to the session-based
    /// <see cref="DeleteFolderAsync"/> in the service's own session (the
    /// ADR 0168 D2 pins are unchanged); the controller's flash / redirect
    /// / 404 mapping is unchanged.
    /// </summary>
    public async Task<BookmarkFolderDeleteResult> DeleteFolderAsync(
        string ownerId, string folderId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var result = await DeleteFolderAsync(ownerId, folderId, session).ConfigureAwait(false);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// The committed <c>MoveBookmarkToFolder</c> lane (IMPROVE-CODE C09 —
    /// the <see cref="Kumunita.Web.Controllers.BookmarksController"/>
    /// <c>BookmarkMoveSave</c> write lane moved behind this service, ADR
    /// 0006 §D). Delegates to the session-based
    /// <see cref="MoveBookmarkToFolderAsync"/> in the service's own
    /// session (the ADR 0168 D2 pins are unchanged); the controller's flash
    /// / redirect / 404 mapping is unchanged.
    /// </summary>
    public async Task<BookmarkRow> MoveBookmarkToFolderAsync(
        string ownerId, string targetKind, string targetId, string? folderId, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var result = await MoveBookmarkToFolderAsync(ownerId, targetKind, targetId, folderId, session).ConfigureAwait(false);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return result;
    }
}
