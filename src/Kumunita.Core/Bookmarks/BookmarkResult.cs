namespace Kumunita.Core.Bookmarks;

/// <summary>
/// The bookmark toggle outcome (M17 / ADR 0118 D3/D4, design doc §2.2 — locked
/// verbatim). A bookmark is a personal pin with no audience decision (D2):
/// the toggle is idempotent (F1 — the <c>M17DocTypes</c> unique index on
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
/// </summary>
public sealed record BookmarkRow(
    string TargetKind,
    string TargetId,
    string? Title,
    string? Link,
    bool Degraded,
    DateTimeOffset Created);

/// <summary>
/// One group in the bookmark list, keyed by the closed <c>TargetKind</c>
/// set {<c>post</c>, <c>event</c>, <c>todo</c>, <c>announcement</c>,
/// <c>page</c>} (design doc §2.2, D1).
/// </summary>
public sealed record BookmarkGroup(
    string Kind,
    IReadOnlyList<BookmarkRow> Items);

/// <summary>
/// The full result of <see cref="IBookmarkService.ListAsync"/> (design doc
/// §2.2). The groups are ordered by the closed <c>TargetKind</c> set (D1);
/// within a group, rows are ordered by <see cref="Bookmark.Created"/>
/// descending (newest first — the post-feed shape). **No**
/// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row is committed
/// (C-M17·2 — the ADR 0105 participant-by-id / M6-inbox personal-read shape).
/// </summary>
public sealed record BookmarkListResult(
    IReadOnlyList<BookmarkGroup> Groups);
