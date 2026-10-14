namespace Kumunita.Core.PersonalNotes;

/// <summary>
/// One personal folder node (M37 / ADR 0167 D1, the design doc §2.1 shape —
/// locked verbatim): a personal folder — the depth ≤ 2 pin (D6); a folder's
/// deletion moves its notes to the root (<c>FolderId = null</c>) — the
/// note body is never lost (D6; C-M37·6). **No** <c>Audience</c>, **no**
/// <c>IsDeleted</c>, **no** <c>ComponentId</c>, **no**
/// <c>LanguageCode</c> (D1/D2).
/// <para>
/// A **personal** tree, bounded in depth (≤ 2: root → child → grandchild),
/// unique in name within the owner's tree (D6; the
/// <c>(OwnerId, ParentId, Name)</c> **unique** index on the
/// <c>PersonalNotesDocTypes</c> surface is the witness — the
/// <c>C_M37_6_FolderTree_DepthBounded_UniqueName</c> pin). A
/// <c>ParentId</c> of <c>null</c> is the root of the owner's tree.
/// </para>
/// </summary>
public sealed class NoteFolder
{
    /// <summary>Surrogate document identity (Marten's default <c>string</c>
    /// Id, the M3/Media/Page/M17 convention).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The owning resident (author-only by construction — the
    /// ADR 0105 participant-by-id shape, D2; a non-owner, a
    /// <c>GlobalAdmin</c> included, gets a non-leaky 404 and no audit row).</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// The **owner's** parent folder id — depth ≤ 2 (D6): <c>null</c> is
    /// the root of the owner's tree; a root folder's child is a child; a
    /// child's child is a grandchild (the tree's bounded depth). A folder
    /// cannot be another resident's (the
    /// <c>C_M37_6_FolderTree_DepthBounded_UniqueName</c> pin).
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// The folder's name — unique within the owner's tree (D6; C-M37·6).
    /// Uniqueness is per <c>(OwnerId, ParentId, Name)</c> — the
    /// <c>(OwnerId, ParentId, Name)</c> **unique** index on the
    /// <c>PersonalNotesDocTypes</c> surface is the witness — so two
    /// siblings may not share a name, but the same name is allowed under
    /// different parents.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the folder was created — never mutated (D6).</summary>
    public DateTimeOffset Created { get; set; }
}
