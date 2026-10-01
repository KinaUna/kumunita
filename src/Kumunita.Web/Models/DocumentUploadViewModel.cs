namespace Kumunita.Web.Models;

/// <summary>
/// M21 (ADR 0122, U03) — the upload compose form-bound model
/// (<c>GET /documents/new</c> + <c>POST /documents</c>). The M2 reusable
/// <see cref="AudienceEditorModel"/> (the single-source pin, the
/// <see cref="PostComposeViewModel"/> precedent) is the **sole** access
/// boundary on the form: it is written verbatim into the
/// <see cref="Kumunita.Core.Documents.Document.Audience"/> row at
/// <c>POST</c> (C-M21·1 — the uploader's choice is absolute; no auto-add of
/// grants). <see cref="File"/> is the <c>IFormFile</c> (Web-only, C-ATT·4 —
/// the bytes never cross into Core; the <see cref="Kumunita.Core.Media
/// .IMediaStore"/> lane is untouched, D3).
/// </summary>
public sealed class DocumentUploadViewModel
{
    /// <summary>The human-facing document name (a label, never a gate — D1).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional one-line description (display-only; null on write when blank).</summary>
    public string? Summary { get; set; }

    /// <summary>The uploaded file (the <c>IFormFile</c> Web boundary).</summary>
    public IFormFile? File { get; set; }

    /// <summary>
    /// The M2 reusable audience editor (the sole access boundary, C-M21·1).
    /// A malformed shape (<see cref="AudienceEditorModel.IsValid"/> false)
    /// fails validation (a form error, not a silent default — the M2 mode-
    /// required pin, the <see cref="PostComposeViewModel.Audience"/>
    /// precedent: non-nullable, <c>= new()</c>-initialized so the binder has
    /// a landing shape). Written verbatim via <see
    /// cref="AudienceEditorModel.BuildAudience"/> at <c>POST</c>.
    /// </summary>
    public AudienceEditorModel Audience { get; set; } = new();
}
