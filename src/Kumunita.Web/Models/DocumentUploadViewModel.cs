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

    // ── Organization (the "documents organization" lane) ─────────────────────
    /// <summary>
    /// The folder the new document is filed into (the ADR 0039 Pages
    /// <c>ParentId</c> forest carried to Documents). An empty value =
    /// "Unfiled" (the root). The controller resolves it to a validated
    /// folder id (a shape-violating folder is a form error — the M3 "a form
    /// is a shape" precedent) before the Core write lane sees it.
    /// </summary>
    public string? FolderId { get; set; }

    /// <summary>
    /// The TG-lane tag labels the uploader typed (the client
    /// <c>client/lib/tag-suggest.ts</c> posts a JSON array of label strings,
    /// e.g. <c>["bylaws", "budget"]</c>, into this one field; the server
    /// parses + normalizes via <see cref="TagSlugs.Parse"/>). The Core write
    /// lane resolves them to <c>Tag</c> ids through
    /// <see cref="Kumunita.Core.Tags.ITagService.AttachToDocumentAsync"/>
    /// (the <c>AttachToPostAsync</c> / <c>AttachToPageAsync</c> precedent).
    /// A blank / absent value is the "no tags" state (the M3/M7
    /// default-empty idiom).
    /// </summary>
    public string? TagIds { get; set; }
}
