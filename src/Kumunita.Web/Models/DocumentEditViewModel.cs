namespace Kumunita.Web.Models;

/// <summary>
/// ADR 0125 (U03) — the document edit form-bound model
/// (<c>GET /documents/{id}/edit</c> + <c>POST /documents/{id}/edit</c>).
/// The <b>owner</b> (the uploader — <c>Document.OwnerId</c>) re-chooses
/// <b>who can access it</b> (the <see cref="AudienceEditorModel"/>, the M2
/// reusable audience editor — the **sole** access boundary, C-M21·1; written
/// verbatim via <see cref="AudienceEditorModel.BuildAudience"/> at
/// <c>POST</c>) and <b>replaces the file</b> (the optional
/// <see cref="File"/> — ADR 0125 D3: <c>null</c> / empty keeps the stored
/// blob, a present file is the new reference). The title/summary are the
/// owner's labels, editable alongside (ADR 0125 D2).
/// <para>
/// Unlike the <see cref="DocumentUploadViewModel"/> (whose <c>File</c> is
/// required — a new document needs bytes), the edit lane's <c>File</c> is
/// **optional**: an owner may edit only the audience, or only the file, or
/// both. The <c>IFormFile</c> is Web-only (C-ATT·4 — the bytes never cross
/// into Core); the <c>IMediaStore</c> lane is untouched (ADR 0122 D3).
/// </para>
/// </summary>
public sealed class DocumentEditViewModel
{
    /// <summary>
    /// The document id (the route value the form posts to
    /// <c>/documents/{id}/edit</c> — not bound from the form, set by the
    /// controller from the validated <c>id</c> on the <c>GET</c> edit form).
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>The human-facing document name (a label, never a gate — ADR 0122 D1).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional one-line description (display-only; null on write when blank).</summary>
    public string? Summary { get; set; }

    /// <summary>
    /// The replacement file (the <c>IFormFile</c> Web boundary). **Optional on
    /// the edit lane** (ADR 0125 D3): <c>null</c> / zero-byte keeps the stored
    /// blob; a present, non-empty file is the new reference (the Core
    /// <see cref="Kumunita.Core.Documents.DocumentEdit.FileReplaced"/> marker is
    /// derived from it in the controller).
    /// </summary>
    public IFormFile? File { get; set; }

    /// <summary>
    /// The M2 reusable audience editor (the sole access boundary, C-M21·1).
    /// Pre-filled from the stored <c>Document.Audience</c> via
    /// <see cref="AudienceEditorModel.FromAudience"/> (the ADR 0036 single
    /// source) on the <c>GET</c> edit form; a malformed shape (
    /// <see cref="AudienceEditorModel.IsValid"/> false) fails validation (a form
    /// error, not a silent default — the M2 mode-required pin). Written verbatim
    /// via <see cref="AudienceEditorModel.BuildAudience"/> at <c>POST</c>.
    /// </summary>
    public AudienceEditorModel Audience { get; set; } = new();
}
