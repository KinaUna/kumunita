using Kumunita.Core.Documents;
using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
    IDocumentStore store,
    // The per-request translation read seam (the flash-message resolution —
    // the LocaleController.FlashAsync / AdminPortabilityController.T /
    // BookmarksController.T idiom). Optional (default null) so a
    // test-construction site that builds this controller without the provider
    // keeps compiling and resolves the flash to the raw key floor; DI always
    // supplies the live ITranslationProvider + ILocalizationService in the app.
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null) : Controller
{
    /// <summary>
    /// Resolves a <c>documents.*</c> kw-l key to the operator's effective
    /// language (the house <see cref="EffectiveLanguageCode.ResolveAsync"/> +
    /// <see cref="ITranslationProvider.GetAsync"/> seam — the same chain the
    /// view's <c>&lt;kw-l&gt;</c> TagHelper uses, so the upload flash toast
    /// renders in the operator's language). Falls back to the raw key when
    /// the translation seam is absent (the test-construction floor — the
    /// <c>DocumentControllerTests</c> pin the raw key, the
    /// <see cref="BookmarksController"/>'s <c>T()</c> idiom).
    /// </summary>
    private async Task<string> T(string key)
    {
        if (translationProvider is null || localization is null)
            return key;
        var lang = await EffectiveLanguageCode.ResolveAsync(
            HttpContext?.Request, localization, translationProvider);
        return await translationProvider.GetAsync(key, lang);
    }

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

        // ADR 0125 D1 — the owner (the uploader) may edit; a non-owner sees no
        // affordance (the ADR 0122 D7 posture — the form's existence is not
        // leaked; the edit route returns 404 for a non-owner, not a 403).
        var isOwner = string.Equals(result.Document.OwnerId, actorId, StringComparison.Ordinal);

        var vm = new DocumentDetailViewModel(
            Document: result.Document,
            // The GetAsync decision allowed — the download re-runs the same Read
            // (C-M21·5). The view (U04) renders the download link only here.
            CanDownload: true,
            DownloadUrl: $"/documents/{id}/download",
            // ADR 0125 — the owner-only edit affordance (U04).
            CanEdit: isOwner,
            EditUrl: $"/documents/{id}/edit");
        return View("Detail", vm);
    }

    // ── GET /documents/{id}/edit — the owner-only edit form (ADR 0125 D1) ───
    //
    // ADR 0125 (U03) — the owner (the uploader — Document.OwnerId) re-chooses
    // who can access it (the audience — the sole access boundary, C-M21·1) and
    // replaces the file (the ADR 0011 content-addressed blob — ADR 0122 D3,
    // unchanged lane). Title/summary editable alongside (D2). File optional
    // (D3): no file = keep the stored blob; a present file = the new reference.
    // Ownership is immutable, Created is preserved, Modified is stamped (D4).
    // The owner gate is owner-ONLY (a non-owner GlobalAdmin is denied — D1); a
    // non-owner (including a non-owner GlobalAdmin) gets a 404 (not a 403 — the
    // ADR 0122 D7 posture: the form's existence is not leaked). One write, no
    // AccessAudit row (D6 — the read lane already audited the visibility).
    [HttpGet("/documents/{id}/edit")]
    [Authorize]
    public async Task<IActionResult> Edit(string id)
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();
        if (!IsValidDocId(id))
            return BadRequest("A document id is required.");

        // The Read path (audience) — the document must be visible to the actor;
        // the owner branch always passes for the owner. A missing doc is null.
        var result = await documents.GetAsync(id, subject).ConfigureAwait(false);
        if (result.Document is null)
            return NotFound();

        // ADR 0125 D1 — owner gate: only the owner may open the form. A
        // non-owner (including a non-owner GlobalAdmin) is a 404, not a 403 (the
        // ADR 0122 D7 posture — the form's existence is not leaked).
        if (!string.Equals(result.Document.OwnerId, subject, StringComparison.Ordinal))
            return NotFound();

        // ADR 0036 — the audience editor pre-fills from the stored audience (the
        // single source of the editor shape); the grant pickers seed the
        // audience members from the same IUserInfoService the M2 editor uses.
        var doc = result.Document;
        var vm = new DocumentEditViewModel
        {
            DocumentId = id,
            Title = doc.Title,
            Summary = doc.Summary,
            Audience = AudienceEditorModel.FromAudience(doc.Audience)
        };

        return View("Edit", vm);
    }

    // ── POST /documents/{id}/edit — the owner-only edit write lane (ADR 0125) ──
    [HttpPost("/documents/{id}/edit")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, [FromForm] DocumentEditViewModel form)
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();
        if (!IsValidDocId(id))
            return BadRequest("A document id is required.");

        // ADR 0125 D1 — owner gate (re-checked on the write; the owner branch of
        // the Read path passes for the owner). A non-owner is a 404 (not a 403
        // — the ADR 0122 D7 posture).
        var result = await documents.GetAsync(id, subject).ConfigureAwait(false);
        if (result.Document is null)
            return NotFound();
        if (!string.Equals(result.Document.OwnerId, subject, StringComparison.Ordinal))
            return NotFound();

        // A malformed audience is a form error, not a 404 (the M2 mode-required
        // pin — the PostComposeViewModel / DocumentUpload precedents).
        if (form.Audience is null || !form.Audience.IsValid)
        {
            ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            form.DocumentId = id; // the form action needs the route id on re-render
            return View("Edit", form);
        }

        if (string.IsNullOrWhiteSpace(form.Title))
            return BadRequest("A title is required.");

        // ADR 0125 D3 — the file is OPTIONAL on the edit lane: a present,
        // non-empty file is the new reference; a null / zero-byte file keeps the
        // stored blob. Store-first (C-M21·6) — the media write precedes the
        // document write so the document's MediaId never dangles.
        var doc = result.Document;
        var mediaId = doc.MediaId;
        var filename = doc.Filename;
        var contentType = doc.ContentType;
        var sizeBytes = doc.SizeBytes;
        var fileReplaced = false;

        if (form.File is not null && form.File.Length > 0)
        {
            // M25 (U8) — the edit lane calls media.PutAsync (a genuine upload), so it
            // adopts the same Web-only IUploadGate as the Upload lane (C-UP·3: a
            // single 413 producer; a second inline guard here would be the second).
            // Runs BEFORE PutAsync → a reject writes no byte (C-UP·2). The
            // optional-file wrapper (above) and the allowlist (below) are untouched.
            var requestServices = HttpContext.RequestServices;
            var settingsSvc = requestServices.GetRequiredService<Kumunita.Core.Usage.IStorageSettingsService>();
            var uploadGate  = requestServices.GetRequiredService<IUploadGate>();
            var settings = await settingsSvc.GetOrCreateAsync(CancellationToken.None);
            var reject = await uploadGate.CheckUpload(form.File.Length, subject, settings, mediaOpts.Value.MaxBytes, mediaOpts.Value.MaxPlatformBytes);
            if (reject is not null) return reject;

            if (!mediaOpts.Value.IsDocumentAllowed(form.File.ContentType))
                return StatusCode(StatusCodes.Status415UnsupportedMediaType);

            using var stream = new MemoryStream();
            await form.File.CopyToAsync(stream).ConfigureAwait(false);
            var stored = await media.PutAsync(stream.ToArray(), form.File.FileName, form.File.ContentType, subject)
                .ConfigureAwait(false);

            mediaId = stored.Id;
            filename = SanitizeFilename(form.File.FileName, stored.Id);
            contentType = stored.ContentType;
            sizeBytes = stored.SizeBytes;
            fileReplaced = true;
        }

        var edit = new DocumentEdit(
            Title: form.Title.Trim(),
            Summary: string.IsNullOrWhiteSpace(form.Summary) ? null : form.Summary.Trim(),
            MediaId: mediaId,
            Filename: filename,
            ContentType: contentType,
            SizeBytes: sizeBytes,
            Audience: form.Audience.BuildAudience(),
            FileReplaced: fileReplaced);

        await using var session = store.LightweightSession();
        try
        {
            await documents.UpdateAsync(id, edit, subject, session).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            // Concurrent delete (the owner deleted the doc between GET and POST).
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Defense in depth (the Core gate is authoritative; the owner gate
            // above should have caught this first) — a 404, not a 403.
            return NotFound();
        }

        TempData["info"] = await T("documents.flash_edited");
        return Redirect($"/documents/{id}");
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
        if (file is null || file.Length == 0) return BadRequest("Choose a file.");   // empty → 400 (untouched)
        // M25 (U8) — the size/over-quota guard is now the Web-only
        // IUploadGate (C-UP·3): the single 413 producer, running the pure Core
        // StorageLimits.Decide over the admin doc + the subject's usage. Runs
        // BEFORE PutAsync, so a reject writes no byte (C-UP·2). Allowlist +
        // empty-file checks untouched, same positions.
        var requestServices = HttpContext.RequestServices;
        var settingsSvc = requestServices.GetRequiredService<Kumunita.Core.Usage.IStorageSettingsService>();
        var uploadGate  = requestServices.GetRequiredService<IUploadGate>();
        var settings = await settingsSvc.GetOrCreateAsync(CancellationToken.None);
        var reject = await uploadGate.CheckUpload(file.Length, subject, settings, mediaOpts.Value.MaxBytes, mediaOpts.Value.MaxPlatformBytes);
        if (reject is not null) return reject;                              // oversize/over-quota → 413 (the gate)
        if (!mediaOpts.Value.IsDocumentAllowed(file.ContentType))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);   // disallowed type → 415 (untouched)

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

        // Flash the closed kw-l key, localized to the operator's language
        // (the house T() idiom — BookmarksController / AdminQuietController) +
        // redirect to the detail.
        TempData["info"] = await T("documents.flash_uploaded");
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
