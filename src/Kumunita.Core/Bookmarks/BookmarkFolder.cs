namespace Kumunita.Core.Bookmarks;

/// <summary>
/// One bookmark folder node (ADR 0168 D2, the M37 <c>NoteFolder</c> shape
/// re-applied to the bookmark surface — the §deferred "bookmark folders"
/// lane from the <c>m17-bookmarks-design.md</c> scope, now settled): a
/// personal folder — the depth ≤ 2 pin (D2); a folder's deletion moves its
/// bookmarks to the root (<c>FolderId = null</c>) — the pinned bookmark is
/// never lost (D2; the M37 C-M37·6 pin). **No** <c>Audience</c>, **no**
/// <c>IsDeleted</c>, **no** <c>ComponentId</c>, **no**
/// <c>LanguageCode</c> (D1/D2 — a bookmark folder is a *personal-by-id*
/// record, the ADR 0105 shape, never an audience decision).
/// <para>
/// A **personal** tree, bounded in depth (≤ 2: root → child → grandchild),
/// unique in name within the owner's tree (D2; the
/// <c>(OwnerId, ParentId, Name)</c> **unique** index on the
/// <c>BookmarkDocTypes</c> surface is the witness — the M37
/// <c>C_M37_6_FolderTree_DepthBounded_UniqueName</c> pin re-applied). A
/// <c>ParentId</c> of <c>null</c> is the root of the owner's tree.
/// </para>
/// </summary>
public sealed class BookmarkFolder
{
    /// <summary>Surrogate document identity (Marten's default <c>string</c>
    /// Id, the M3/Media/Page/M17/M37 convention).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The owning resident (author-only by construction — the
    /// ADR 0105 participant-by-id shape, D2; a non-owner, a
    /// <c>GlobalAdmin</c> included, gets a non-leaky 404 and no audit row).</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// The **owner's** parent folder id — depth ≤ 2 (D2): <c>null</c> is
    /// the root of the owner's tree; a root folder's child is a child; a
    /// child's child is a grandchild (the tree's bounded depth). A folder
    /// cannot be another resident's (the M37
    /// <c>C_M37_6_FolderTree_DepthBounded_UniqueName</c> pin re-applied).
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// The folder's name — unique within the owner's tree (D2; C-M17·6).
    /// Uniqueness is per <c>(OwnerId, ParentId, Name)</c> — the
    /// <c>(OwnerId, ParentId, Name)</c> **unique** index on the
    /// <c>BookmarkDocTypes</c> surface is the witness — so two
    /// siblings may not share a name, but the same name is allowed under
    /// different parents.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the folder was created — never mutated (D2).</summary>
    public DateTimeOffset Created { get; set; }
}
