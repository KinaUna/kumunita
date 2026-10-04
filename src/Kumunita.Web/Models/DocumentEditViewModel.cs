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

    // ── Organization (the "documents organization" lane) ─────────────────────
    /// <summary>
    /// The folder this document is being moved to (the ADR 0039 Pages
    /// <c>ParentId</c> forest carried to Documents). An empty value =
    /// "Unfiled" (the root). The controller resolves it to a validated
    /// folder id (a shape-violating folder is a form error — the M3 "a form
    /// is a shape" precedent) before the Core write lane sees it.
    /// </summary>
    public string? FolderId { get; set; }

    /// <summary>
    /// The TG-lane tag labels the owner typed (the client
    /// <c>client/lib/tag-suggest.ts</c> posts a JSON array of label strings,
    /// e.g. <c>["bylaws", "budget"]</c>, into this one field; the server
    /// parses + normalizes via <see cref="TagSlugs.Parse"/>). <c>null</c>
    /// (the form did not post the field — the U8b "leave existing" shape)
    /// leaves the document's existing tags; a present value (even an empty
    /// <c>[]</c> when the owner removed every chip) is authoritative ⇒
    /// empty detaches all, non-empty attaches. The Core write lane resolves
    /// them to <c>Tag</c> ids through
    /// <see cref="Kumunita.Core.Tags.ITagService.AttachToDocumentAsync"/>
    /// (the <c>AttachToPostAsync</c> / <c>AttachToPageAsync</c> precedent).
    /// </summary>
    public string? TagIds { get; set; }

    /// <summary>
    /// The tag **slugs** the tag-suggest input's starting chips (the
    /// edit-form pre-seed, the <c>PostsController.SeedExistingTagSlugsAsync</c>
    /// idiom). <c>[BindNever]</c> — never bound from the form; the controller
    /// populates it on the <c>GET</c> edit form (and re-populates it on a
    /// re-render so a failed POST does not silently drop the author's tags
    /// under the U8b empty-set-detach semantics).
    /// </summary>
    [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
    public System.Collections.Generic.IReadOnlyList<string> ExistingTagSlugs { get; set; } = [];
}
