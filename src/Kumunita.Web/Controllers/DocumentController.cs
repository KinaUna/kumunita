using Kumunita.Core.Documents;
using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Core.Query;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    // F16 / UPL-02 — the serve-lane virus-scan seam (the "widen the ctor as
    // the lane needs it" shape). The download serve action runs the gate
    // (short-circuit / scan / quarantine + 404); the upload lane is untouched.
    // Optional (default null) so the existing test-construction sites keep
    // compiling; a null scanner is the fail-open floor (pin §2.1) — the serve
    // proceeds unchanged (DI always supplies the live ClamAvScanner in the app).
    IVirusScanner? scanner = null,
    // The per-request translation read seam (the flash-message resolution —
    // the LocaleController.FlashAsync / AdminPortabilityController.T /
    // BookmarksController.T idiom). Optional (default null) so a
    // test-construction site that builds this controller without the provider
    // keeps compiling and resolves the flash to the raw key floor; DI always
    // supplies the live ITranslationProvider + ILocalizationService in the app.
    ILocalizationService? localization = null,
    ITranslationProvider? translationProvider = null,
    // The items-per-page preference seam (FeedPaging). Optional (default
    // null) so test-construction sites that build this controller without it
    // keep compiling (a missing seam falls back to the platform page-size
    // default); DI always supplies it in the app.
    Kumunita.Core.UserInfo.IUserInfoService? userInfo = null) : Controller
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

    /// <summary>
    /// A failed upload guard on a browser form post (ADR 0233): re-render the
    /// form the person was on — typed fields kept — with a localized error
    /// beside the file input and the guard's HTTP status (400 / 413 / 415) on
    /// the response. Replaces the bare <see cref="StatusCodeResult"/> /
    /// <see cref="BadRequestObjectResult"/>, which a browser shows as a blank
    /// page. Still a guard: it runs before any byte is written.
    /// </summary>
    private async Task<IActionResult> RejectFormAsync(string viewName, object model, int statusCode, string field, string key)
    {
        ModelState.AddModelError(field, await T(key));
        var view = View(viewName, model);
        view.StatusCode = statusCode;
        return view;
    }

    // ── GET /documents — the repository feed (D4, C-M21·3) ────────────────
    [HttpGet("/documents")]
    [Authorize]
    public async Task<IActionResult> Index(int page = 1, string? sort = null, string? dir = null)
    {
        var actorId = KumunitaPrincipal.SubjectId(User);
        if (actorId is null) return Unauthorized();

        // M26 U13 (C-SORT·3) — the ?sort=/?dir= → SortSpec mapping is
        // Web-only: parse against the documents' closed allowlist (row 12:
        // created/modified/title + this surface's own `size` key), or null
        // when the viewer chose no sort (C-SORT·2, F1 — the seam keeps its
        // pinned Created-desc order).
        var feedSort = ParseSort(sort, dir, DocumentFeedAllowedKeys);

        // The resident's items-per-page preference (FeedPaging resolves
        // Profile.PageSize → PageSizer default/clamp); a no-actor read or a
        // missing seam uses the platform default. Passed to the seam (the
        // Skip/Take window).
        int pageSize = await FeedPaging.PageSizeAsync(userInfo, actorId);
        // One CanSeeAsync (aggregate row) over the candidate set — the service's
        // C-M21·3 shape. The view model never carries the hidden count (F1:
        // the feed does not leak "how many you cannot see").
        var result = await documents.ListAsync(actorId, page, sort: feedSort, pageSize: pageSize);
        // ADR 0239 D1 — resolve the uploader's display name per row (the
        // PostsController feed shape: a *display* lookup, never an
        // AccessAudit subject — the audience decision is already made by
        // ListAsync). One GetProfileAsync per **distinct owner** (a
        // neighborhood's documents are usually shared across a handful of
        // owners — the batch is by owner id, not by row). A missing /
        // blocked profile degrades to the raw owner id (the
        // profile?.DisplayName ?? post.AuthorId shape); a null
        // IUserInfoService (a test-construction site) leaves the dictionary
        // empty and the view falls back to the raw id for every row.
        var ownerNames = new Dictionary<string, string>();
        if (userInfo is not null)
        {
            var nameCache = new Dictionary<string, string>();
            foreach (var d in result.Visible)
            {
                if (!nameCache.TryGetValue(d.OwnerId, out var name))
                {
                    var profile = await userInfo.GetProfileAsync(d.OwnerId);
                    name = profile?.DisplayName is { Length: > 0 } dn ? dn : d.OwnerId;
                    nameCache[d.OwnerId] = name;
                }
                ownerNames[d.Id] = name;
            }
        }
        var vm = new DocumentIndexViewModel(
            Visible: result.Visible,
            HasMore: result.HasMore,
            Page: result.Page,
            // D5 — the upload standing, resolved once at the boundary (the
            // view (U04) shows/hides the "Upload" link without re-resolving).
            CanUpload: KumunitaPrincipal.IsGlobalAdmin(User) || KumunitaPrincipal.IsModerator(User),
            OwnerNames: ownerNames);

        // M26 U13 (D-SORT·5) — the one shared sort control (the U10 _Sort
        // reference, reused verbatim — C-SORT·1): the closed documents
        // allowlist (U2 §2.2 row 12 — created/modified/title/size), no dead
        // options (F9 — the `size` key is only on this surface).
        var sortVm = SortViewModel.ForRoute(
            "/documents",
            currentKey: feedSort?.Key,
            currentDir: feedSort is { } s ? (s.Descending ? "desc" : "asc") : null,
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
                ("size", "desc"),
            ]);

        return View("Index", vm with { Sort = sortVm });
    }

    // M26 U13 (C-SORT·1) — the documents feed's closed sort allowlist
    // (U2 §2.2 row 12: created/modified/title/size; surface default created
    // desc; `size` → the non-null SizeBytes long — this surface's own key,
    // F9).
    private static readonly IReadOnlySet<string> DocumentFeedAllowedKeys =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title", "size" };

    // M26 U13 (C-SORT·3) — the sort param is "carried" only when the request
    // actually specified a non-blank ?sort= key (?dir= alone is not a sort
    // choice; C-SORT·2, F1).
    private static bool HasSortParam(string? sort)
        => !string.IsNullOrWhiteSpace(sort);

    // M26 U13 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // surface's closed allowlist — or null when the viewer chose no sort.
    private static SortSpec? ParseSort(string? sort, string? dir, IReadOnlySet<string> allowedKeys)
        => HasSortParam(sort)
            ? SortKeys.Parse(sort, dir, allowedKeys, "created", defaultDir: true)
            : null;

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

        // ── Organization (the "documents organization" lane) ────────────────
        // Resolve the document's tags to (slug, display-name) rows (the
        // PostsController's tag-rows shape — the display-name is resolved to
        // the viewer's language through the ITagService read seam; a label,
        // never a gate — C-TG·1). A null ITagService (a test-construction
        // site) ⇒ empty list (no-op). The read is plain (no AccessAudit row —
        // the C-TG·8 shape: the tag lane is a plain read; the content's own
        // Read decision already ran in GetAsync above).
        var tagRows = new List<DocumentTagRow>();
        if (result.Document.TagIds.Count > 0)
        {
            var postTagIds = result.Document.TagIds.ToHashSet(StringComparer.Ordinal);
            await using var q = store.QuerySession();
            var tagDocs = await q.Query<Kumunita.Core.Tags.Tag>()
                .Where(t => postTagIds.Contains(t.Id))
                .ToListAsync();
            tagRows = tagDocs
                .Select(t => new DocumentTagRow(t.Slug, t.Name))
                .Where(t => !string.IsNullOrEmpty(t.Slug))
                .OrderBy(t => t.DisplayedName, StringComparer.Ordinal)
                .ToList();
        }

        // Resolve the document's folder name (the display label, never a gate;
        // null = "Unfiled" the root). A plain read (no AccessAudit row — the
        // folder is a label, the C-TG·8 shape).
        string? folderName = null;
        if (result.Document.FolderId is not null)
        {
            await using var q2 = store.QuerySession();
            var folder = await q2.LoadAsync<Kumunita.Core.Documents.DocumentFolder>(result.Document.FolderId);
            folderName = folder?.Name;
        }

        // ADR 0239 D1 — resolve the uploader's + last-modifier's display
        // names (the PostsController feed shape: a *display* lookup, never an
        // AccessAudit subject). The uploader is Document.OwnerId (a required
        // field); the last modifier is Document.ModifiedById — a
        // <c>null</c> ModifiedById (a pre-ADR-0239 document that has never
        // been edited through a Modified-stamping lane) degrades to the
        // uploader, the owner is the implicit last modifier (ADR 0239 D1).
        // A missing / blocked profile degrades to the raw id; a null
        // IUserInfoService (a test-construction site) degrades to the raw id
        // for both. **<c>ModifiedByName</c> is null when the modifier is the
        // same subject as the uploader** (or when there is no modifier at
        // all) — the detail view's "Last modified by" row is redundant with
        // the "Uploaded by" row in that case (the date is still shown by
        // the "Last updated" row), so the row is hidden.
        string ownerName = result.Document.OwnerId;
        string? modifiedByName = null;
        if (userInfo is not null)
        {
            var ownerProfile = await userInfo.GetProfileAsync(result.Document.OwnerId);
            ownerName = ownerProfile?.DisplayName is { Length: > 0 } oname ? oname : result.Document.OwnerId;
            // A distinct modifier (not null, not the uploader): resolve its
            // name. A null modifier or a modifier == uploader: leave
            // modifiedByName null (the detail row is redundant with the
            // "Uploaded by" row — the date is still shown by the "Last
            // updated" row).
            if (result.Document.ModifiedById is { } modifierId
                && !string.Equals(modifierId, result.Document.OwnerId, StringComparison.Ordinal))
            {
                var modifierProfile = await userInfo.GetProfileAsync(modifierId);
                modifiedByName = modifierProfile?.DisplayName is { Length: > 0 } mname ? mname : modifierId;
            }
        }

        var vm = new DocumentDetailViewModel(
            Document: result.Document,
            // The GetAsync decision allowed — the download re-runs the same Read
            // (C-M21·5). The view (U04) renders the download link only here.
            CanDownload: true,
            DownloadUrl: $"/documents/{id}/download",
            // ADR 0125 — the owner-only edit affordance (U04).
            CanEdit: isOwner,
            EditUrl: $"/documents/{id}/edit",
            // Organization (the "documents organization" lane).
            Tags: tagRows,
            FolderName: folderName,
            FolderId: result.Document.FolderId,
            // ADR 0239 — attribution + delete.
            OwnerName: ownerName,
            ModifiedByName: modifiedByName,
            CanDelete: isOwner);
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
        var subject = Kumunita.Web.Security.KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();
        if (!IsValidDocId(id))
            return BadRequest(await this.FlashAsync("validation.document_id_required", "A document id is required."));

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
            FolderId = doc.FolderId,
            // The "documents organization" lane (TG) — pre-seed the tag
            // slugs the tag-suggest input starts with (the
            // PostsController.SeedExistingTagSlugsAsync idiom). A null
            // ITagService (a test-construction site) ⇒ empty list (no-op).
            ExistingTagSlugs = await SeedExistingTagSlugsAsync(doc),
            Audience = AudienceEditorModel.FromAudience(doc.Audience),
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
            return BadRequest(await this.FlashAsync("validation.document_id_required", "A document id is required."));

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
            ModelState.AddModelError("Audience.Mode", await this.FlashAsync("validation.audience_mode", "Audience mode is required (Any or All)."));
            form.DocumentId = id; // the form action needs the route id on re-render
            return View("Edit", form);
        }

        if (string.IsNullOrWhiteSpace(form.Title))
            return await RejectFormAsync("Edit", form, StatusCodes.Status400BadRequest, "Title", "documents.err_title_required");

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
            if (reject is StatusCodeResult gateStatus)
                return await RejectFormAsync("Edit", form, gateStatus.StatusCode, "File", "documents.err_too_large");
            if (reject is not null) return reject;

            if (!mediaOpts.Value.IsDocumentAllowed(form.File.ContentType))
                return await RejectFormAsync("Edit", form, StatusCodes.Status415UnsupportedMediaType, "File", "documents.err_type_not_allowed");

            // SEC U22 (owner decision, 2026-10-31) — magic-byte sniffer wire.
            // Reject on **`Mismatch`** only (declared type whose known signature
            // is contradicted by the bytes); `Match` and `Unknown` pass through.
            // The wire reads the leading 16 bytes via `OpenReadStream` so it
            // works for every `IFormFile` implementation without materialising
            // the payload twice; the read is bounded to 16 bytes so it cannot
            // leak a full upload into memory.
            var head = new byte[Math.Min(16L, form.File.Length)];
            if (head.Length > 0)
            {
                await using var rs = form.File.OpenReadStream();
                var n = 0;
                while (n < head.Length)
                {
                    var got = await rs.ReadAsync(head.AsMemory(n));
                    if (got == 0) break;
                    n += got;
                }
                if (n < head.Length) Array.Resize(ref head, n);
            }
            if (UploadSignatureSniffer.Classify(form.File.ContentType, head) == UploadSignatureSniffer.SniffVerdict.Mismatch)
                return await RejectFormAsync("Edit", form, StatusCodes.Status415UnsupportedMediaType, "File", "documents.err_type_not_allowed");

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
            FileReplaced: fileReplaced,
            // ── Organization (the "documents organization" lane) ────────────
            FolderId: form.FolderId is { Length: > 0 } ? form.FolderId : null,
            // The TG-lane slugs (the client posts a JSON array of label strings;
            // the server parses + normalizes). <c>null</c> (the form did not
            // post the field — the U8b "leave existing" shape) leaves the
            // document's existing tags; a present value (even an empty
            // <c>[]</c>) is authoritative ⇒ empty detaches all, non-empty
            // attaches.
            TagSlugs: form.TagIds is null ? null : TagSlugs.Parse(form.TagIds));

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

    // ── POST /documents/{id}/delete — the owner-only soft-delete lane (ADR 0239) ──
    //
    // ADR 0239 (D3/D4/D5) — the owner (the uploader — Document.OwnerId) may
    // remove the document from the community's repository. The record is kept
    // (never hard-deleted): Document.DeletedAt + Document.DeletedById are
    // stamped, and every read surface (feed, detail, download, search, note
    // targets) hides it (D4). The gate is owner-only (D3) — a non-owner
    // (including a non-owner GlobalAdmin) gets a 404 (not a 403 — the ADR
    // 0122 D7 posture: the route's existence is not leaked). A second delete
    // is idempotent (D4 — the ADR 0024 pin). No AccessAudit row (the ADR 0122
    // §1.a A2 write-lane convention). On success, flash the closed
    // documents.flash_deleted key and redirect to the feed — the document is
    // gone from the detail page by construction (D4), so there is no detail
    // page to land on.
    [HttpPost("/documents/{id}/delete")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();
        if (!IsValidDocId(id))
            return BadRequest(await this.FlashAsync("validation.document_id_required", "A document id is required."));

        // ADR 0125 D1 — owner gate (re-checked on the write; the owner branch
        // of the Read path passes for the owner). A non-owner is a 404 (not a
        // 403 — the ADR 0122 D7 posture).
        var result = await documents.GetAsync(id, subject).ConfigureAwait(false);
        if (result.Document is null)
            return NotFound();
        if (!string.Equals(result.Document.OwnerId, subject, StringComparison.Ordinal))
            return NotFound();

        await using var session = store.LightweightSession();
        try
        {
            await documents.DeleteAsync(id, subject, session).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            // Concurrent state change — the doc is gone (the ADR 0122 D7 posture).
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            // Defense in depth (the Core gate is authoritative; the owner gate
            // above should have caught this first) — a 404, not a 403.
            return NotFound();
        }

        TempData["info"] = await T("documents.flash_deleted");
        // ADR 0241 — the toast offers Undo (a POST to the restore route).
        TempData["info_action_url"] = $"/documents/{id}/restore";
        TempData["info_action_label"] = await T("documents.undo");
        return Redirect("/documents");
    }

    // ── GET /documents/deleted — the restore list (ADR 0241) ─────────────────
    //
    // The viewer's own soft-deleted documents; every soft-deleted document for a
    // GlobalAdmin. Titles and names only (no download, no content), so it needs no
    // audience decision and writes no AccessAudit row.
    [HttpGet("/documents/deleted")]
    [Authorize]
    public async Task<IActionResult> Deleted()
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();
        var isAdmin = KumunitaPrincipal.IsGlobalAdmin(User);

        var docs = await documents.ListDeletedAsync(subject, isAdmin).ConfigureAwait(false);
        var nameCache = new Dictionary<string, string>();
        var rows = new List<DocumentDeletedRow>(docs.Count);
        foreach (var d in docs)
        {
            if (!nameCache.TryGetValue(d.OwnerId, out var name))
            {
                var profile = userInfo is null ? null : await userInfo.GetProfileAsync(d.OwnerId);
                name = profile?.DisplayName is { Length: > 0 } dn ? dn : d.OwnerId;
                nameCache[d.OwnerId] = name;
            }
            rows.Add(new DocumentDeletedRow(d, name));
        }
        return View("Deleted", new DocumentDeletedViewModel(rows, isAdmin));
    }

    // ── POST /documents/{id}/restore — undo a soft delete (ADR 0241) ─────────
    //
    // The owner, or a GlobalAdmin, may restore. Anyone else gets a 404 (the ADR 0122 D7
    // posture, as for delete). Idempotent. Lands on the restored document's detail page.
    [HttpPost("/documents/{id}/restore")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(string id)
    {
        var subject = KumunitaPrincipal.SubjectId(User);
        if (subject is null) return Unauthorized();
        if (!IsValidDocId(id))
            return BadRequest(await this.FlashAsync("validation.document_id_required", "A document id is required."));

        await using var session = store.LightweightSession();
        try
        {
            await documents.RestoreAsync(id, subject, KumunitaPrincipal.IsGlobalAdmin(User), session).ConfigureAwait(false);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(); // not a 403 — the route's existence is not leaked
        }

        TempData["info"] = await T("documents.flash_restored");
        return Redirect($"/documents/{id}");
    }

    // ── GET /documents/new — the compose form (D5) ─────────────────────────
    [HttpGet("/documents/new")]
    [Authorize]
    public IActionResult New()
    {
        // ADR 0233 — no model parameter: a bound `DocumentUploadViewModel?` was
        // validated on a plain GET, so the non-nullable `Audience.Mode` put a
        // "The Mode field is required." alert on a fresh form. The view reads
        // `Model?` and renders blank without one.
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
    [EnableRateLimiting("upload")]
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
            ModelState.AddModelError("Audience.Mode", await this.FlashAsync("validation.audience_mode", "Audience mode is required (Any or All)."));
            return View("New", form);
        }

        // D3 — guards-before-write (the ADR 0011 / AttachmentController.Upload
        // ordering): empty → 400, oversize → 413, disallowed type → 415 —
        // **no file written on any guard**.
        var file = form.File;
        if (string.IsNullOrWhiteSpace(form.Title))
            return await RejectFormAsync("New", form, StatusCodes.Status400BadRequest, "Title", "documents.err_title_required");
        if (file is null || file.Length == 0)
            return await RejectFormAsync("New", form, StatusCodes.Status400BadRequest, "File", "documents.err_file_required");   // empty → 400
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
        if (reject is StatusCodeResult gateStatus)                          // oversize/over-quota → 413 (the gate)
            return await RejectFormAsync("New", form, gateStatus.StatusCode, "File", "documents.err_too_large");
        if (reject is not null) return reject;
        if (!mediaOpts.Value.IsDocumentAllowed(file.ContentType))           // disallowed type → 415
            return await RejectFormAsync("New", form, StatusCodes.Status415UnsupportedMediaType, "File", "documents.err_type_not_allowed");

        // SEC U22 (owner decision, 2026-10-31) — magic-byte sniffer wire.
        // Reject on **`Mismatch`** only (declared type whose known signature
        // is contradicted by the bytes); `Match` and `Unknown` pass through.
        // The wire reads the leading 16 bytes via `OpenReadStream` so it
        // works for every `IFormFile` implementation without materialising
        // the payload twice; the read is bounded to 16 bytes so it cannot
        // leak a full upload into memory.
        var head = new byte[Math.Min(16L, file.Length)];
        if (head.Length > 0)
        {
            await using var rs = file.OpenReadStream();
            var n = 0;
            while (n < head.Length)
            {
                var got = await rs.ReadAsync(head.AsMemory(n));
                if (got == 0) break;
                n += got;
            }
            if (n < head.Length) Array.Resize(ref head, n);
        }
        if (UploadSignatureSniffer.Classify(file.ContentType, head) == UploadSignatureSniffer.SniffVerdict.Mismatch)
            return await RejectFormAsync("New", form, StatusCodes.Status415UnsupportedMediaType, "File", "documents.err_type_not_allowed");

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
            Audience: form.Audience.BuildAudience(),
            // ── Organization (the "documents organization" lane) ────────────
            FolderId: form.FolderId is { Length: > 0 } ? form.FolderId : null,
            // The TG-lane slugs (the client posts a JSON array of label strings;
            // the server parses + normalizes). A blank / absent value is the
            // "no tags" state (the M3/M7 default-empty idiom).
            TagSlugs: form.TagIds is null ? Array.Empty<string>() : TagSlugs.Parse(form.TagIds));

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

        // ── F16 / UPL-02: the quarantine gate (serve lane is the
        // enforcement point, pin §4/§5). After the blob-miss 404, before the
        // OpenReadAsync serve — a malware byte is refused, 404, zero audit
        // rows (the no-leak posture; the Read decision already ran in
        // GetAsync above, so this refusal is a store-level refusal, not an
        // audience one). ──
        if (await IsQuarantinedOrInfectedAsync(result.Document.MediaId, stored, CancellationToken.None))
            return NotFound();

        // Step 5: serve (D6 — Content-Disposition: attachment; the stored
        // Content-Type, already validated at write — no second allowlist check).
        var stream = await media.OpenReadAsync(result.Document.MediaId);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        var filename = SanitizeFilename(result.Document.Filename ?? stored.Filename, id);
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"" + filename + "\"; filename*=UTF-8''" + Uri.EscapeDataString(filename);
        return File(stream, stored.ContentType);
    }

    /// <summary>
    /// F16 / UPL-02 — the serve-lane quarantine gate (pin §4/§5). Returns
    /// <c>true</c> when the byte must be <b>refused with 404</b>: either it is
    /// already quarantined (idempotent short-circuit — no re-scan, no re-write,
    /// zero audit rows), or the scanner flags the payload <see
    /// cref="VirusScanStatus.Infected"/> (set <see cref="MediaObject.Quarantined"/>
    /// + one <c>SaveChangesAsync</c>, C3 single-write-lane). A
    /// <see cref="VirusScanStatus.Clean"/> verdict (incl. the fail-open
    /// unavailable-daemon default, pin §2.1) returns <c>false</c> — the serve
    /// proceeds unchanged. The scanner re-uses the serve lane's own
    /// <see cref="IMediaStore.OpenReadAsync"/> read seam (no new byte-read seam).
    /// </summary>
    private async Task<bool> IsQuarantinedOrInfectedAsync(string id, MediaObject stored, CancellationToken ct)
    {
        if (stored.Quarantined) return true;               // idempotent short-circuit (no re-scan, §3)

        // Fail-open floor (pin §2.1): a box / test-construction site without a
        // scanner degrades to Clean — the serve proceeds unchanged (never a
        // crash, never a false-positive quarantine).
        if (scanner is null) return false;

        VirusScanStatus verdict = VirusScanStatus.Clean;
        try
        {
            await using var scanStream = await media.OpenReadAsync(id, ct);
            verdict = await scanner!.ScanAsync(scanStream, ct);
        }
        catch { /* fail-open: a scanner fault degrades to Clean (§2.1), never a crash */ }

        if (verdict is VirusScanStatus.Infected)
        {
            stored.Quarantined = true;                     // quarantine (persist, §3)
            await using var s = store.LightweightSession();
            s.Store(stored);
            await s.SaveChangesAsync(ct);                  // one commit (C3 single-write-lane, ADR 0006)
            return true;                                   // refuse with 404 (no-leak, §4)
        }
        return false;                                       // Clean → serve proceeds unchanged
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

    /// <summary>
    /// The "documents organization" lane (TG) — the edit form's pre-seeded
    /// tag **slugs** (the tag-suggest input's starting chips, the
    /// <c>PostsController.SeedExistingTagSlugsAsync</c> idiom). A null
    /// <see cref="Kumunita.Core.Tags.ITagService"/> (a test-construction
    /// site) ⇒ empty list (no-op). The read is plain (no <c>AccessAudit</c>
    /// row — the C-TG·8 shape: the tag lane is a plain read; the content's
    /// own <c>Read</c> decision already ran in the controller's
    /// <c>GetAsync</c> above).
    /// </summary>
    private async Task<IReadOnlyList<string>> SeedExistingTagSlugsAsync(Kumunita.Core.Documents.Document? doc)
    {
        if (doc is null || doc.TagIds.Count == 0)
            return [];

        await using var session = store.QuerySession();
        var postTagIds = doc.TagIds.ToHashSet(StringComparer.Ordinal);
        var tagDocs = await session.Query<Kumunita.Core.Tags.Tag>()
            .Where(t => postTagIds.Contains(t.Id))
            .ToListAsync();

        return tagDocs
            .Select(t => t.Slug)
            .Where(s => !string.IsNullOrEmpty(s))
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
