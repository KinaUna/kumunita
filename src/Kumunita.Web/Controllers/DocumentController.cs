using Kumunita.Core.Documents;
using Kumunita.Core.Media;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M21 (ADR 0122, U03) — the documents routes: the feed (<c>GET /documents</c>),
/// the detail (<c>GET /documents/{id}</c>), the upload form
/// (<c>GET /documents/new</c>), the upload write lane (<c>POST /documents</c>),
/// and the download serve (<c>GET /documents/{id}/download</c>).
/// <para>
/// The authorization is the per-<see cref="Document"/> <c>Read</c> decision —
/// one <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> call,
/// one audit row, emitted **by** the frozen seam (<c>DocumentService
/// .GetAsync</c>), never this controller (C-M21·4). The upload standing is
/// gated at **this** boundary (D5 — <c>GlobalAdmin ∪ Moderator</c>; a
/// non-eligible actor is refused with **404, not 403** — the form's existence
/// is not leaked); the Core service is standing-agnostic (C-M21·7). The bytes
/// ride the frozen ADR 0011 <see cref="IMediaStore"/> (D3 — the media lane is
/// untouched, a new consumer); the document allowlist is
/// <see cref="MediaOptions.IsDocumentAllowed"/> (D3). The download is
/// <c>Content-Disposition: attachment</c> (D6 — a download, never an inline
/// render) behind the single <c>Read</c> decision, **Deny → 404** (D7 — not
/// 403; feed and detail agree, C-M21·5).
/// </para>
/// </summary>
public sealed class DocumentController(
    DocumentService documents,
    IMediaStore media,
    IOptions<MediaOptions> mediaOpts,
    IDocumentStore store) : Controller
{
    // ── GET /documents — the repository feed (D4, C-M21·3) ────────────────
    [HttpGet("/documents")]
    [Authorize]
    public async Task<IActionResult> Index(int page = 1)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null) return Unauthorized();

        // One CanSeeAsync (aggregate row) over the candidate set — the service's
        // C-M21·3 shape. The view model never carries the hidden count (F1:
        // the feed does not leak "how many you cannot see").
        var result = await documents.ListAsync(actorId, page);
        var vm = new DocumentIndexViewModel(
            Visible: result.Visible,
            HasMore: result.HasMore,
            Page: result.Page,
            // D5 — the upload standing, resolved once at the boundary (the
            // view (U04) shows/hides the "Upload" link without re-resolving).
            CanUpload: KumunitaPrincipal.IsGlobalAdmin(User) || KumunitaPrincipal.IsModerator(User));
        return View("Index", vm);
    }

    // ── GET /documents/{id} — the detail (D7, C-M21·4) ─────────────────────
    [HttpGet("/documents/{id}")]
    [Authorize]
    public async Task<IActionResult> Detail(string id)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null) return Unauthorized();

        // One CanAsync (the decision row) inside GetAsync (C-M21·4). A missing
        // document and a Deny both return Document = null → the **same** 404
        // (D7: feed and detail agree; no existence leak).
        var result = await documents.GetAsync(id, actorId);
        if (result.Document is null) return NotFound();

        var vm = new DocumentDetailViewModel(
            Document: result.Document,
            // The GetAsync decision allowed — the download re-runs the same Read
            // (C-M21·5). The view (U04) renders the download link only here.
            CanDownload: true,
            DownloadUrl: $"/documents/{id}/download");
        return View("Detail", vm);
    }

    // ── GET /documents/new — the compose form (D5) ─────────────────────────
    [HttpGet("/documents/new")]
    [Authorize]
    public IActionResult New()
    {
        // D5 — the upload standing gate (Web boundary). A non-eligible actor
        // gets 404 (not 403 — the form's existence is not leaked to a
        // non-privileged actor; the ADR 0017 compose-standing shape).
        if (!KumunitaPrincipal.IsGlobalAdmin(User) && !KumunitaPrincipal.IsModerator(User))
            return NotFound();
        return View("New");
    }

    // ── POST /documents — the upload write lane (D5, A2, D3) ───────────────
    [HttpPost("/documents")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromForm] DocumentUploadViewModel form)
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();

        // D5 — the standing gate (Web boundary, same as New): 404 not 403.
        if (!KumunitaPrincipal.IsGlobalAdmin(User) && !KumunitaPrincipal.IsModerator(User))
            return NotFound();

        // A form is a shape: a malformed audience is a form error, not a 404
        // (the M2 mode-required pin, the PostComposeViewModel precedent).
        if (form.Audience is null || !form.Audience.IsValid)
        {
            ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            return View(form);
        }

        // D3 — guards-before-write (the ADR 0011 / AttachmentController.Upload
        // ordering): empty → 400, oversize → 413, disallowed type → 415 —
        // **no file written on any guard**.
        var file = form.File;
        if (string.IsNullOrWhiteSpace(form.Title)) return BadRequest("A title is required.");
        if (file is null || file.Length == 0) return BadRequest("Choose a file.");
        if (mediaOpts.Value.MaxBytes > 0 && file.Length > mediaOpts.Value.MaxBytes)
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge);
        if (!mediaOpts.Value.IsDocumentAllowed(file.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);

        // D3 — store-first (orphan-safe, C-MED·7): the bytes land on the frozen
        // ADR 0011 volume before the catalog row references them.
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var stored = await media.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject);

        // C-M21·1 — the uploader's audience is written verbatim (the M2 single-
        // deserialization site, the PostComposeViewModel.Audience.BuildAudience
        // precedent; Core receives the frozen shape, bit-identical to the form).
        // C-M21·4 — one SaveChangesAsync: the domain write commits atomically in
        // the caller's session (the C3 same-transaction lane, the
        // PostService.CreatePostAsync LightweightSession precedent). No
        // AccessAudit row (A2 — the write is authenticated, not a read).
        var draft = new DocumentUpload(
            Title: form.Title.Trim(),
            Summary: string.IsNullOrWhiteSpace(form.Summary) ? null : form.Summary.Trim(),
            MediaId: stored.Id,
            Filename: file.FileName,
            ContentType: file.ContentType,
            SizeBytes: file.Length,
            Audience: form.Audience.BuildAudience());

        await using var session = store.LightweightSession();
        var doc = await documents.UploadAsync(draft, subject, session);

        // Flash the closed kw-l key (U04 renders it) + redirect to the detail.
        TempData["info"] = "documents.flash_uploaded";
        return Redirect($"/documents/{doc.Id}");
    }

    // ── GET /documents/{id}/download — the byte serve (D6, D7) ─────────────
    [HttpGet("/documents/{id}/download")]
    [Authorize]
    public async Task<IActionResult> Serve([FromRoute] string id)
    {
        // Step 1: validate the id (1–128 lowercase hex) → 400. The document
        // identity is a "N"-formatted Guid (16 hex) or a content-hash id (64
        // hex) — the same character class the image/attachment lanes accept.
        if (!IsValidDocId(id)) return BadRequest();

        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null) return Unauthorized();

        // Step 2 + 4 (collapsed by GetAsync, the design doc §7 shape): one
        // CanAsync (the single decision row, emitted by the frozen seam) — a
        // missing document and a Deny both return Document = null → the
        // **same** 404 (D7 — no existence leak; C-M21·5: feed and detail
        // agree, the same Read decision governs all three read surfaces).
        var result = await documents.GetAsync(id, actorId);
        if (result.Document is null) return NotFound();

        // Step 3: blob (IMediaStore) miss → 404 (orphan — the document is the
        // authority for access, not the blob; C-M21·6). Zero audit rows here
        // (the decision already ran in GetAsync).
        var stored = await media.GetAsync(result.Document.MediaId);
        if (stored is null) return NotFound();

        // Step 5: serve (D6 — Content-Disposition: attachment; the stored
        // Content-Type, already validated at write — no second allowlist check).
        var stream = await media.OpenReadAsync(result.Document.MediaId);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var filename = SanitizeFilename(result.Document.Filename ?? stored.Filename, id);
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"" + filename + "\"; filename*=UTF-8''" + Uri.EscapeDataString(filename);
        return File(stream, stored.ContentType);
    }

    // ── helpers (mirror AttachmentController) ──────────────────────────────

    /// <summary>
    /// RFC 6266 sanitization of the <see cref="Document.Filename"/> (the source
    /// for <c>Content-Disposition: attachment; filename=…</c>): null/whitespace
    /// → the id fallback <c>{id}.bin</c>; otherwise strip the prohibited set
    /// (path separators <c>/</c> and <c>\</c>, the header terminators
    /// <c>";</c> / <c>"</c>, and other control chars) so a stored name can't
    /// inject a header or a path. Verbatim from
    /// <see cref="AttachmentController"/> (D6 — the download difference from
    /// the image lane, which omits <c>Content-Disposition</c>).
    /// </summary>
    private static string SanitizeFilename(string? original, string id)
    {
        if (string.IsNullOrWhiteSpace(original))
            return id + ".bin";
        var cleaned = new System.Text.StringBuilder(original.Length);
        foreach (var c in original)
        {
            if (c < ' ' || c == '"' || c == '\\' || c == '/' || c == ';' || c == ':' || c == '<' || c == '>' || c == '?' || c == '|')
                continue;
            cleaned.Append(c);
        }
        return cleaned.Length == 0 ? id + ".bin" : cleaned.ToString();
    }

    /// <summary>
    /// Validates the route id: 1–128 lowercase hex chars (<c>[0-9a-f]</c>) —
    /// the <c>Guid.ToString("N")</c> (16 hex) and content-hash (64 hex) shapes.
    /// The same character test <see cref="AttachmentController"/> accepts for
    /// its route id (C-ATT·7 step 1 — duplicated per-lane, not shared).
    /// </summary>
    private static bool IsValidDocId(string id)
    {
        if (id.Length is < 1 or > 128) return false;
        foreach (var c in id)
        {
            if (c is >= '0' and <= '9' or >= 'a' and <= 'f') continue;
            return false;
        }
        return true;
    }
}
