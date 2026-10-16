namespace Kumunita.Core.Bookmarks;

/// <summary>
/// The bookmark toggle outcome (M17 / ADR 0118 D3/D4, design doc §2.2 — locked
/// verbatim). A bookmark is a personal pin with no audience decision (D2):
/// the toggle is idempotent (F1 — the <c>BookmarkDocTypes</c> unique index on
/// <c>(OwnerId, TargetKind, TargetId)</c> is the at-most-one-row witness),
/// and the write-lane visibility check (D3) re-runs the target's own frozen
/// <c>CanAsync(Read)</c> before any row is created — a Deny or an absent
/// target is <see cref="BookmarkToggleStatus.Refused"/> (the Web layer maps
/// to 404, never 403), **no row survives**.
/// </summary>
public enum BookmarkToggleStatus
{
    /// <summary>A new <see cref="Bookmark"/> row was created (D3 passed, no duplicate).</summary>
    Bookmarked,
    /// <summary>A row already existed for this (owner, kind, id) — the F1 no-op.</summary>
    AlreadyBookmarked,
    /// <summary>The D3 write-lane visibility check failed (the target is
    /// absent, soft-deleted, or not visible to the actor). No row was created.</summary>
    Refused,
    /// <summary>The owner's row was physically removed (D4 — no <c>IsDeleted</c>).</summary>
    Removed,
    /// <summary>No row existed to remove (a no-op; a dangling row is removable
    /// without a target read — D5 / F4).</summary>
    NotBookmarked,
}

/// <summary>
/// The result of a <see cref="IBookmarkService.ToggleAsync"/> or
/// <see cref="IBookmarkService.RemoveAsync"/> call (design doc §2.2 — the
/// exact LOCKED shape).
/// </summary>
public sealed record BookmarkToggleResult(BookmarkToggleStatus Status);

/// <summary>
/// One resolved row in the owner's bookmark list (design doc §2.2, the
/// <c>BookmarkRow</c> shape). <see cref="Title"/> and <see cref="Link"/> are
/// <c>null</c> when <see cref="Degraded"/> is <c>true</c> (D5 — the dangling
/// row renders as the closed <c>bm.list.degraded</c> label, exactly one
/// static string, C-M17·5).
/// <para>
/// <c>Id</c> is the bookmark row's own id (the per-row move / unbookmark
/// key); <c>FolderId</c> is the owner's folder id the bookmark sits in
/// (ADR 0168 D2 — <c>null</c> = the bookmark is unfiled at the root of the
/// owner's tree; the M37 <c>NoteRow.FolderId</c> shape re-applied). Both
/// default to <c>null</c> so the pre-folder call sites
/// (the <c>BookmarkService</c> / the controller / acceptance seam tests)
/// compile unchanged; the live <see cref="BookmarkService"/> always sets
/// them.
/// </para>
/// </summary>
public sealed record BookmarkRow(
    string TargetKind,
    string TargetId,
    string? Title,
    string? Link,
    bool Degraded,
    DateTimeOffset Created,
    string? Id = null,
    string? FolderId = null);

/// <summary>
/// One group in the bookmark list, keyed by the closed <c>TargetKind</c>
/// set {<c>post</c>, <c>event</c>, <c>todo</c>, <c>announcement</c>,
/// <c>page</c>, <c>project</c>, <c>board</c>} (design doc §2.2, D1 — the
/// project / board kinds joined by the ADR 0118 amendment, 2026-10-16).
/// <see cref="FolderId"/> is the owner's
/// folder id the group's items sit in (ADR 0168 D2 — the M37
/// <c>NoteGroup.FolderId</c> shape re-applied; <c>null</c> = the
/// unfiled bucket at the root of the owner's tree). Defaults to
/// <c>null</c> so the pre-folder call sites compile unchanged.
/// </summary>
public sealed record BookmarkGroup(
    string Kind,
    IReadOnlyList<BookmarkRow> Items,
    string? FolderId = null);

/// <summary>
/// One folder row in the owner's bookmark list (ADR 0168 D2 — the M37
/// <c>FolderRow</c> shape re-applied to the bookmark surface).
/// <see cref="BookmarkCount"/> is the count of the owner's bookmarks sitting
/// in this folder (across all kinds — the M37
/// <c>FolderRow.NoteCount</c> shape re-applied).
/// </summary>
public sealed record FolderRow(
    string Id,
    string Name,
    string? ParentId,
    int BookmarkCount);

/// <summary>
/// The full result of <see cref="IBookmarkService.ListAsync"/> (design doc
/// §2.2 + ADR 0168 D2). The groups are ordered by the closed
/// <c>TargetKind</c> set (D1) and — within a kind — nested under the
/// owner's folder tree (ADR 0168 D2 — the M37
/// <c>NoteListResult</c> shape re-applied: one group per folder, then the
/// unfiled bucket); <see cref="Folders"/> is the owner's bookmark folder
/// tree (root → child, depth ≤ 2). Within a group, rows are ordered by
/// <see cref="Bookmark.Created"/> descending (newest first — the
/// post-feed shape). **No**
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row is committed
/// (C-M17·2 — the ADR 0105 participant-by-id / M6-inbox personal-read shape).
/// </summary>
public sealed record BookmarkListResult(
    IReadOnlyList<BookmarkGroup> Groups,
    IReadOnlyList<FolderRow> Folders);

/// <summary>
/// The bookmark-folder delete outcome (ADR 0168 D2 — the M37
/// <c>FolderDeleteResult</c> shape re-applied to the bookmark surface).
/// </summary>
public enum BookmarkFolderDeleteResult
{
    /// <summary>The folder was deleted and its bookmarks moved to the root.</summary>
    BookmarksMoved,
    /// <summary>The folder was deleted and its child folders were also deleted.</summary>
    FoldersDeleted,
}
