namespace Kumunita.Core.PersonalNotes;

/// <summary>
/// One personal note (M37 / ADR 0167 D1, the design doc §2.1 shape — locked
/// verbatim): a *body of the resident's own writing* attached to a target —
/// a note, not a pointer; the body survives the target's death (D4 / D7).
/// **No** <c>Audience</c>, **no** <c>IsDeleted</c> (a note is *content*,
/// not a pointer — D4; the body survives the target's death), **no**
/// <c>ComponentId</c>, **no** <c>LanguageCode</c> (D1/D2/D4).
/// <para>
/// <c>TargetKind</c> is a **string** — the M5 <c>KanbanStatuses</c>
/// string-not-enum shape — restricted to the closed set
/// {<c>post</c>, <c>grouppost</c>, <c>event</c>, <c>page</c>, <c>project</c>,
/// <c>todo</c>, <c>board</c>, <c>document</c>, <c>directory</c>}: the exact
/// vocabulary the existing <c>*ToAuditableResource</c> adapters and the
/// <c>AccessAudit</c> rows already emit (D1).
/// </para>
/// <para>
/// <c>Body</c> is the resident's own writing, stored as raw text and
/// Markdown-rendered on read via the **frozen** <c>MarkdownRenderer</c>
/// (the M31 / ADR 0031 one-renderer pin; D4). The body is **never**
/// mutated in place — an edit is a new revision and
/// <see cref="Modified"/> is the body's revision witness (the M14/M16
/// author-only edit-lane shape, ADR 0014 / ADR 0016).
/// </para>
/// <para>
/// <c>Tags</c> are author-set, free-form, per-note (D5): normalized on
/// write (lowercase, trimmed, deduped, sorted — the M26 <c>Tag</c> doc's
/// <c>Name</c> normalization shape, ADR 0044). A tag is **not** a
/// platform-wide <c>Tag</c> doc (C-M37·5).
/// </para>
/// <para>
/// <c>FolderId</c> is the **owner's** folder id (D6): a note cannot live
/// in another resident's folder (C-M37·6, the
/// <c>NoteCannotLiveInAnotherResidentFolder</c> pin).
/// </para>
/// </summary>
public sealed class PersonalNote
{
    /// <summary>Surrogate document identity (Marten's default <c>string</c>
    /// Id, the M3/Media/Page/M17 convention).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The authoring resident (the note's owner; author-only by
    /// construction — a non-owner, a <c>GlobalAdmin</c> included, gets a
    /// non-leaky 404 and no audit row; the ADR 0105 participant-by-id shape,
    /// D2).</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// The target's surface kind — a **string** (the M5
    /// <c>KanbanStatuses</c> string-not-enum shape) restricted to the closed
    /// set {<c>post</c>, <c>grouppost</c>, <c>event</c>, <c>page</c>,
    /// <c>project</c>, <c>todo</c>, <c>board</c>, <c>document</c>,
    /// <c>directory</c>}: the exact vocabulary the existing
    /// <c>*ToAuditableResource</c> adapters and the <c>AccessAudit</c> rows
    /// already emit (D1).
    /// </summary>
    public string TargetKind { get; set; } = string.Empty;

    /// <summary>The target's doc id.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>
    /// The resident's own writing — a note, not a pointer: the body survives
    /// the target's death (D4 / D7; C-M37·4). Stored as raw text;
    /// Markdown-rendered on read via the **frozen** <c>MarkdownRenderer</c>
    /// (the M31 / ADR 0031 one-renderer pin). The body is **never** mutated
    /// in place — an edit is a new revision (the M14/M16 author-only
    /// edit-lane shape, ADR 0014 / ADR 0016).
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// The **owner's** folder id (D6): a note cannot live in another
    /// resident's folder (C-M37·6). <c>null</c> = the note sits at the root
    /// of the owner's tree (also where a note lands when its folder is
    /// deleted — the note body is never lost; the
    /// <c>C_M37_6_FolderDeletion_MovesNotesToRoot_BodiesSurvive</c> pin).
    /// </summary>
    public string? FolderId { get; set; }

    /// <summary>
    /// Author-set, free-form, per-note tags (D5): normalized on write
    /// (lowercase, trimmed, deduped, sorted — the M26 <c>Tag</c> doc's
    /// <c>Name</c> normalization shape, ADR 0044). A tag is **not** a
    /// platform-wide <c>Tag</c> doc (C-M37·5). <c>null</c> = no tags.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; set; }

    /// <summary>When the note was created — never mutated (D4 / F1; the
    /// <c>Created</c> on the <c>(OwnerId, Created)</c> list-ordering
    /// witness index).</summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>The body's revision witness — <c>null</c> until the first
    /// edit (D4; the M14/M16 author-only edit-lane shape, ADR 0014 /
    /// ADR 0016: the body is never mutated in place, an edit is a new
    /// revision and this timestamp is the witness).</summary>
    public DateTimeOffset? Modified { get; set; }
}
