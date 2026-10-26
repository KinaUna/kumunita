using Marten;

namespace Kumunita.Core.PersonalNotes;

/// <summary>
/// The <c>M37</c> (Personal notes) service seam (ADR 0167 / design doc
/// <c>m37-personal-notes-design.md</c> §2.2 — locked verbatim). The public
/// surface of <see cref="PersonalNoteService"/>.
/// <para>
/// **M37 composes only frozen seams** (C-M37·1): it adds **no** new
/// <c>AccessAction</c>, **no** new <c>AccessVia</c>, **no** branch in
/// <c>Decide()</c>, **no** adapter, **no** method on
/// <c>IAuthorizationService</c>, and **no** <c>INotificationService</c>.
/// The <b>only</b> authorization call in the entire milestone is the D3
/// write-lane visibility check, which is the <b>target's own</b> frozen
/// <c>CanAsync(Read)</c> — M37 adds no decision of its own.
/// </para>
/// <para>
/// **D2 — a personal note is a personal-by-id record, never an audience
/// decision** (the ADR 0105 / M9-messaging participant-by-id shape). A
/// non-owner — a <c>GlobalAdmin</c> included — gets a non-leaky 404 (the
/// Web layer's <c>ownerId == User id</c> check), and the owner's own read
/// is a **personal read** that **never audits**: zero <c>AccessAudit</c>
/// rows, zero <c>CanSeeAsync</c> passes over the note rows (C-M37·2).
/// </para>
/// <para>
/// **U02 implements the 8 note methods; the 4 folder-tree methods are
/// U03.** The interface is declared once and frozen for U03–U05 (the M36/
/// U03/U04 precedent of the interface declared once and extended
/// additively). U03 replaces the 4 stub bodies with the D6 implementations.
/// </para>
/// </summary>
public interface IPersonalNoteService
{
    // ── Note methods (U02) ──────────────────────────────────────────────────

    /// <summary>
    /// Note attach — the D3 write-lane visibility check (the target's own
    /// frozen <c>CanAsync(Read)</c>) re-run BEFORE the row is created.
    /// <list type="bullet">
    /// <item>visible + dup ⇒ returns the existing <see cref="NoteRow"/>
    /// (the F1 no-op — one row, one <c>Created</c>; the unique index is the
    /// witness).</item>
    /// <item>visible, no row yet ⇒ creates the row and returns the new
    /// <see cref="NoteRow"/>.</item>
    /// <item>Deny / absent / soft-deleted ⇒ throws
    /// <see cref="KeyNotFoundException"/> (the Web layer maps to **404**,
    /// never 403; no row; no surviving Deny row — the M16 create-gate
    /// posture).</item>
    /// </list>
    /// </summary>
    Task<NoteRow> CreateNoteAsync(
        string ownerId, string targetKind, string targetId, string body,
        IDocumentSession session);

    /// <summary>
    /// The owner's own note by id — zero audit (C-M37·2). A non-owner gets a
    /// non-leaky 404 (the Web layer's <c>[Authorize]</c> +
    /// <c>ownerId == User id</c> check). A missing note throws
    /// <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<NoteRow> GetNoteAsync(string ownerId, string noteId);

    /// <summary>
    /// The owner's own notes, grouped by <see cref="NoteRow.TargetKind"/>
    /// and nested under the owner's folder tree. The D7 degraded rows
    /// (target absent / soft-deleted / no longer visible) carry
    /// <see cref="NoteRow.Degraded"/> = true, <see cref="NoteRow.Title"/> /
    /// <see cref="NoteRow.Link"/> = null (the closed <c>pn.list.degraded</c>
    /// label), and **the body remains visible** to the owner (D4).
    /// <para>
    /// **No** <c>AccessAudit</c> row is committed (C-M37·2); **no**
    /// <c>CanSeeAsync</c> pass over the note rows (the ADR 0105
    /// "reads never audit" + M6-inbox personal-read shape).
    /// </para>
    /// </summary>
    Task<NoteListResult> ListNotesAsync(string ownerId);

    /// <summary>
    /// The body's new revision — the <see cref="NoteRow.Modified"/>
    /// timestamp is the witness (D4). The body is never mutated in place;
    /// edit is a new revision. A non-owner or missing note throws
    /// <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<NoteRow> UpdateNoteBodyAsync(
        string ownerId, string noteId, string body, IDocumentSession session);

    /// <summary>
    /// Physical removal — the body is gone, the note row is gone (the M17
    /// Bookmark shape). A dangling row is removable (D7 / F4 — no target
    /// read). Returns <see cref="NoteDeleteStatus.Deleted"/> on success,
    /// <see cref="NoteDeleteStatus.NotNoted"/> when no row existed (a
    /// no-op). A non-owner gets <see cref="NoteDeleteStatus.NotNoted"/>
    /// (no row, no leak).
    /// </summary>
    Task<NoteDeleteStatus> DeleteNoteAsync(
        string ownerId, string noteId, IDocumentSession session);

    /// <summary>
    /// Adds a tag to the note — the tag is normalized (lowercase, trimmed,
    /// deduped, sorted — D5 / C-M37·5). A non-owner or missing note throws
    /// <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<NoteRow> AddTagAsync(
        string ownerId, string noteId, string tag, IDocumentSession session);

    /// <summary>
    /// Removes a tag from the note — the tag is normalized before removal
    /// (D5 / C-M37·5). A non-owner or missing note throws
    /// <see cref="KeyNotFoundException"/>.
    /// </summary>
    Task<NoteRow> RemoveTagAsync(
        string ownerId, string noteId, string tag, IDocumentSession session);

    /// <summary>
    /// The "noted" state on reload — the F1 witness, zero audit (C-M37·2).
    /// Returns <c>true</c> when a <see cref="PersonalNote"/> row exists for
    /// this (owner, kind, id); <c>false</c> otherwise. No target read at all
    /// (the same D7 / F4 principle as <see cref="DeleteNoteAsync"/>).
    /// </summary>
    Task<bool> HasNoteAsync(string ownerId, string targetKind, string targetId);

    /// <summary>
    /// The owner's own note for a specific target — zero audit (C-M37·2).
    /// Returns <c>null</c> when no note exists for this (owner, kind, id)
    /// (a valid state, not an error — the Web layer maps null to 404 or
    /// renders an empty modal). A non-owner gets <c>null</c> (no leak —
    /// the ADR 0105 "operator has no read standing" precedent).
    /// </summary>
    Task<NoteRow?> GetNoteByTargetAsync(
        string ownerId, string targetKind, string targetId);

    // ── Folder-tree methods (U03 — stubbed in U02, implemented in U03) ────

    /// <summary>
    /// Creates a folder in the owner's personal tree. The depth ≤ 2 pin (D6);
    /// the <c>(OwnerId, ParentId, Name)</c> unique index is the witness.
    /// </summary>
    Task<FolderRow> CreateFolderAsync(
        string ownerId, string name, string? parentId, IDocumentSession session);

    /// <summary>
    /// Renames a folder within the owner's tree. The D6 witness — the
    /// <c>(OwnerId, ParentId, Name)</c> unique index is re-checked.
    /// </summary>
    Task<FolderRow> RenameFolderAsync(
        string ownerId, string folderId, string newName, IDocumentSession session);

    /// <summary>
    /// Deletes a folder and moves its notes to the root
    /// (<c>FolderId = null</c>) — D6; the note bodies are never lost.
    /// </summary>
    Task<FolderDeleteResult> DeleteFolderAsync(
        string ownerId, string folderId, IDocumentSession session);

    /// <summary>
    /// Moves a note to (or from) a folder in the owner's tree. The D6 pin —
    /// the note and the folder are in the same owner's tree.
    /// </summary>
    Task<NoteRow> MoveNoteToFolderAsync(
        string ownerId, string noteId, string? folderId, IDocumentSession session);
}

// ── Result types (design doc §2.2 — locked verbatim) ──────────────────────

/// <summary>
/// One resolved note row in the owner's note list (design doc §2.2, the
/// <c>NoteRow</c> shape). <see cref="Title"/> and <see cref="Link"/> are
/// <c>null</c> when <see cref="Degraded"/> is <c>true</c> (D7 — the
/// dangling row renders as the closed <c>pn.list.degraded</c> label, exactly
/// one static string; **the body remains visible** to the owner — D4).
/// </summary>
public sealed record NoteRow(
    string Id,
    string TargetKind,
    string TargetId,
    string Body,
    string? FolderId,
    IReadOnlyList<string> Tags,
    DateTimeOffset Created,
    DateTimeOffset? Modified,
    bool Degraded,
    string? Title,
    string? Link);

/// <summary>
/// One folder row in the owner's note list (design doc §2.2).
/// </summary>
public sealed record FolderRow(string Id, string Name, string? ParentId, int NoteCount);

/// <summary>
/// One group in the note list, keyed by the closed <see cref="NoteRow.TargetKind"/>
/// set (design doc §2.2, D1).
/// </summary>
public sealed record NoteGroup(string Kind, string? FolderId, IReadOnlyList<NoteRow> Items);

/// <summary>
/// The full result of <see cref="IPersonalNoteService.ListNotesAsync"/>
/// (design doc §2.2). **No** <see cref="Authorization.AccessAudit"/> row is
/// committed (C-M37·2 — the ADR 0105 participant-by-id / M6-inbox
/// personal-read shape).
/// </summary>
public sealed record NoteListResult(
    IReadOnlyList<NoteGroup> Groups,
    IReadOnlyList<FolderRow> Folders);

/// <summary>
/// The note attach outcome (design doc §2.2 — locked verbatim).
/// </summary>
public enum NoteCreateStatus
{
    /// <summary>A new <see cref="PersonalNote"/> row was created (D3 passed, no duplicate).</summary>
    Created,
    /// <summary>A row already existed for this (owner, kind, id) — the F1 no-op.</summary>
    AlreadyNoted,
    /// <summary>The D3 write-lane visibility check failed (the target is
    /// absent, soft-deleted, or not visible to the actor). No row was created.</summary>
    Refused,
}

/// <summary>
/// The note delete outcome (design doc §2.2 — locked verbatim).
/// </summary>
public enum NoteDeleteStatus
{
    /// <summary>The owner's row was physically removed (D4 — no <c>IsDeleted</c>).</summary>
    Deleted,
    /// <summary>No row existed to remove (a no-op; a dangling row is removable
    /// without a target read — D7 / F4).</summary>
    NotNoted,
    /// <summary>The D3 write-lane visibility check failed. No row was removed.</summary>
    Refused,
}

/// <summary>
/// The folder delete outcome (design doc §2.2 — locked verbatim).
/// </summary>
public enum FolderDeleteResult
{
    /// <summary>The folder was deleted and its notes moved to the root.</summary>
    NotesMoved,
    /// <summary>The folder was deleted and its child folders were also deleted.</summary>
    FoldersDeleted,
}
