namespace Kumunita.Core.Documents;

/// <summary>
/// M21-adjacent (the "documents organization" lane) — a **folder** in the
/// community's shared document repository. A folder is one node in a
/// **forest** (the ADR 0039 Pages idiom, carried verbatim to the Documents
/// context): nested by <see cref="ParentId"/> (<c>null</c> = a root node —
/// the forest has no single mandatory root, matching the Pages
/// <c>ParentId</c> shape). The full path is **derived** (the chain of
/// ancestor names) — never stored, so a subtree moves in one column write
/// (the <see cref="DocumentFolderService.MoveAsync"/> lane).
/// <para>
/// A folder is **not** a gate — it is a pure organization unit (the ADR 0039
/// "folder" reading: "folder" is not a type, it is "has ≥ 1 child document or
/// child folder"). Access to a document is governed solely by its
/// <see cref="Document.Audience"/> + <see cref="Kumunita.Core.Authorization
/// .IAuthorizationService"/> Read decision; a folder's <see cref="OwnerId"/>
/// is the creator (an audit identity + the move/rename standing owner), never
/// an access boundary.
/// </para>
/// <para>
/// **Standing (the ADR 0125 owner-reading carried to folders):** the
/// <see cref="OwnerId"/> (the actor who created the folder) ∪ GlobalAdmin
/// may rename, move, or delete it. The <c>GlobalAdmin</c> branch exists
/// because folders are a community-level organizational aid — the same
/// elevated-standing pattern as the ADR 0044 tag translate lane (creator ∪
/// GlobalAdmin).
/// </para>
/// </summary>
public sealed class DocumentFolder
{
    /// <summary>Surrogate document identity (Marten's default <c>string</c> Id,
    /// the M3/Media/Page/Document convention).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The parent folder this folder is nested under. <c>null</c> = a root
    /// node (the forest — any number of roots: "Contracts", "Meetings",
    /// "Bylaws", …; there is no single mandatory root — the ADR 0039 Pages
    /// <c>ParentId</c> shape).
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// The folder's display name (a label, never a gate). Non-blank. The
    /// uniqueness of a name is scoped to its parent (the
    /// <c>(ParentId, Name)</c> write-lane guard — a root and a child may
    /// share a name; two children of the same parent may not, the
    /// ADR 0039 <c>(ParentId, Slug)</c> idiom applied to display names).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The subject id of the actor who created the folder — the standing
    /// owner (the rename / move / delete right; the ADR 0125 owner-reading
    /// carried to the folder lane). Non-blank.
    /// </summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>When the folder was created.</summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// When the folder was last renamed, moved, or had a document's folder
    /// changed to point at it. <c>null</c> = never modified after creation.
    /// </summary>
    public DateTimeOffset? Modified { get; set; }
}
