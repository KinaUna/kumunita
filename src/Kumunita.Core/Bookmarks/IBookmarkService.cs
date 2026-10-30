using Marten;

namespace Kumunita.Core.Bookmarks;

/// <summary>
/// The <c>M17</c> (Bookmarks) service seam (ADR 0118 / design doc
/// <c>m17-bookmarks-design.md</c> §2.2 — locked verbatim). The public
/// surface of <see cref="BookmarkService"/>.
/// <para>
/// **M17 composes only frozen seams** (C-M17·1): it adds **no** new
/// <c>AccessAction</c>, **no** new <c>AccessVia</c>, **no** branch in
/// <c>Decide()</c>, **no** adapter, **no** method on
/// <c>IAuthorizationService</c>, and **no** <c>INotificationService</c>
/// (D7). The <b>only</b> authorization call in the entire milestone is the
/// D3 write-lane visibility check, which is the <b>target's own</b> frozen
/// <c>CanAsync(Read)</c> — M17 adds no decision of its own.
/// </para>
/// <para>
/// **D2 — a bookmark is a personal-by-id record, never an audience
/// decision** (the ADR 0105 / M9-messaging participant-by-id shape). A
/// non-owner — a <c>GlobalAdmin</c> included — gets a non-leaky 404 (the Web
/// layer's <c>ownerId == User id</c> check), and the owner's own read is a
/// **personal read** that **never audits**: zero <c>AccessAudit</c> rows,
/// zero <c>CanSeeAsync</c> passes over the bookmark rows (C-M17·2).
/// </para>
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// The owner's own rows, grouped by <see cref="Bookmark.TargetKind"/>
    /// (the closed set, D1), each row resolved or degraded per **D5**
    /// (design doc §2.4): a row whose target resolves to a doc visible to
    /// the owner carries a <c>Title</c> + <c>Link</c> (<c>Degraded =
    /// false</c>); an absent / soft-deleted / no-longer-visible target
    /// renders <c>Degraded = true</c> with <c>Title</c> / <c>Link</c>
    /// <c>null</c> (the closed <c>bm.list.degraded</c> label — exactly one
    /// static string, C-M17·5).
    /// <para>
    /// **No** <c>AccessAudit</c> row is committed (C-M17·2); **no**
    /// <c>CanSeeAsync</c> pass over the bookmark rows (the ADR 0105
    /// "reads never audit" + M6-inbox personal-read shape). Rows are loaded
    /// via the identity predicate <c>OwnerId == ownerId</c>, ordered by
    /// <see cref="Bookmark.Created"/> descending.
    /// </para>
    /// </summary>
    Task<BookmarkListResult> ListAsync(string ownerId);

    /// <summary>
    /// Toggles the owner's pin on a target (design doc §2.3, the D3
    /// write-lane table): **first** re-runs the target's own frozen
    /// <c>CanAsync(Read)</c> decision (the *same call the target's detail
    /// page uses*):
    /// <list type="bullet">
    /// <item>Deny / absent / soft-deleted ⇒ <see
    /// cref="BookmarkToggleStatus.Refused"/> — the Web layer maps to a
    /// **404** (never 403), **no row is created**, and **no** M17 Deny row
    /// survives (the M16 create-gate posture).</item>
    /// <item>visible + an existing row ⇒ <see
    /// cref="BookmarkToggleStatus.AlreadyBookmarked"/> (the F1 no-op — one
    /// row, one <c>Created</c>; the unique index is the witness).</item>
    /// <item>visible, no row yet ⇒ <see cref="BookmarkToggleStatus.Bookmarked"/>
    /// (a fresh <c>Bookmark</c> row with a new <c>Id</c> and a fresh
    /// <c>Created</c>; re-bookmark after removal is a fresh
    /// <c>Created</c>, D4).</item>
    /// </list>
    /// <para>
    /// The row commits in the **caller's** in-flight <paramref name="session"/>
    /// (invariant C3); the D3 decision's own <c>AccessAudit</c> row is the
    /// **target's own** frozen row (M17 commits none of its own, C-M17·2 /
    /// C-M17·3).
    /// </para>
    /// </summary>
    Task<BookmarkToggleResult> ToggleAsync(
        string ownerId, string targetKind, string targetId, IDocumentSession session);

    /// <summary>
    /// Removes the owner's row by the unique key (design doc §2.3, the D4
    /// physical-removal row): the owner's row present ⇒ <see
    /// cref="BookmarkToggleStatus.Removed"/> (the row is deleted
    /// **physically** — no <c>IsDeleted</c>, D4); absent ⇒ <see
    /// cref="BookmarkToggleStatus.NotBookmarked"/> (a no-op).
    /// <para>
    /// **No target read at all** — a dangling row is removable (D5 / F4):
    /// removing a pin is keyed on the row's own id and needs no visibility
    /// decision on the (possibly dead) target. No <c>AccessAudit</c> row is
    /// committed (C-M17·2).
    /// </para>
    /// </summary>
    Task<BookmarkToggleResult> RemoveAsync(
        string ownerId, string targetKind, string targetId, IDocumentSession session);

    /// <summary>
    /// Answers the detail-page button-state question (F1 — "the button
    /// reflects 'bookmarked' on reload"): does the owner have a pin for
    /// this (targetKind, targetId)? Returns <c>true</c> when a
    /// <see cref="Bookmark"/> row exists for the owner; <c>false</c>
    /// otherwise (no row, or no <c>ownerId</c>).
    /// <para>
    /// **No target read at all** (the same D5 / F4 principle as
    /// <see cref="RemoveAsync"/>): the answer is keyed on the owner's own
    /// row alone, so a row whose target is absent or deleted still answers
    /// <c>true</c> (the row is the owner's personal record, and the
    /// button's remove affordance must work even for a dangling row).
    /// No <c>AccessAudit</c> row, no <c>CanAsync</c> call
    /// (C-M17·2 — the personal-read shape, the <see cref="ListAsync"/>
    /// precedent).
    /// </para>
    /// </summary>
    Task<bool> IsBookmarkedAsync(string ownerId, string targetKind, string targetId);

    /// <summary>
    /// ADR 0227 — the batched form of <see cref="IsBookmarkedAsync"/> for card
    /// lists (the feed card footer): of <paramref name="targetIds"/> (all of
    /// <paramref name="targetKind"/>), the ids the owner has bookmarked. One
    /// query per page. Same personal-read shape as the single read: no target
    /// read, no <c>AccessAudit</c> row, no <c>CanAsync</c> pass; a blank owner,
    /// a kind outside the closed set or an empty id list answers an empty set
    /// with no query.
    /// </summary>
    Task<IReadOnlySet<string>> ListBookmarkedIdsAsync(
        string ownerId, string targetKind, IReadOnlyCollection<string> targetIds);

    // ── Folder-tree methods (ADR 0168 — the M37 folder-tree shape re-applied
    //    to the bookmark surface; the §deferred "bookmark folders" lane
    //    from m17-bookmarks-design.md, now settled). The depth ≤ 2 pin, the
    //    (OwnerId, ParentId, Name) unique index, the owner-only pin, and the
    //    "deletion moves bookmarks to the root" invariant are the M37
    //    C-M37·6 pin, re-applied (D2). ──────────────────────────────────

    /// <summary>
    /// Creates a folder in the owner's bookmark tree (ADR 0168 D2 — the M37
    /// D6 shape re-applied). The depth ≤ 2 pin: if a parent is given it
    /// must itself be a root. The <c>(OwnerId, ParentId, Name)</c> unique
    /// index is the witness (a sibling already holding the name under this
    /// parent is the no-op — returns the existing row). A foreign / absent
    /// parent throws <see cref="KeyNotFoundException"/> (non-leaky 404).
    /// A depth violation throws <see cref="InvalidOperationException"/>
    /// (the Web layer maps it to 400).
    /// </summary>
    Task<FolderRow> CreateFolderAsync(
        string ownerId, string name, string? parentId, IDocumentSession session);

    /// <summary>
    /// Renames a folder within the owner's tree (ADR 0168 D2). The
    /// <c>(OwnerId, ParentId, Name)</c> unique index is re-checked (a
    /// sibling already holding the new name under the same parent throws
    /// <see cref="InvalidOperationException"/>). A non-owner or missing
    /// folder throws <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<FolderRow> RenameFolderAsync(
        string ownerId, string folderId, string newName, IDocumentSession session);

    /// <summary>
    /// Deletes a folder and moves its bookmarks to the root
    /// (<c>FolderId = null</c>) — ADR 0168 D2; the bookmark rows are never
    /// lost (they are re-parented, not deleted). Child folders are
    /// recursively deleted (their bookmarks move to the root too). A
    /// non-owner or missing folder throws <see cref="KeyNotFoundException"/>.
    /// Returns <see cref="BookmarkFolderDeleteResult.BookmarksMoved"/> on a
    /// leaf folder, <see cref="BookmarkFolderDeleteResult.FoldersDeleted"/>
    /// when child folders were also removed.
    /// </summary>
    Task<BookmarkFolderDeleteResult> DeleteFolderAsync(
        string ownerId, string folderId, IDocumentSession session);

    /// <summary>
    /// Moves a bookmark to (or from) a folder in the owner's tree
    /// (ADR 0168 D2 — the M37 MoveNoteToFolderAsync shape re-applied). The
    /// bookmark and the folder are in the same owner's tree; a foreign or
    /// absent folder throws <see cref="KeyNotFoundException"/> (non-leaky
    /// 404). A non-owner or missing bookmark throws
    /// <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<BookmarkRow> MoveBookmarkToFolderAsync(
        string ownerId, string targetKind, string targetId, string? folderId,
        IDocumentSession session);
}
