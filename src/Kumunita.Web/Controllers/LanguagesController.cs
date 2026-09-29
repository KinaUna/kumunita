using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/languages</c> surface (ADR 0005 D; ML lane U5; the
/// <see cref="Roles.Translator"/> split is ADR 0021). Two lanes share this
/// controller:
/// <para>
/// The **translation editors** — <see cref="Translations"/>,
/// <see cref="SaveTranslation"/> — are open to both <see cref="Roles.GlobalAdmin"/>
/// and <see cref="Roles.Translator"/> (ADR 0021): a delegated Translator may update
/// and add the UI strings but holds no catalog standing. (The static-page editor
/// was retired in the PG U07 absorb — page editing/translation now flows through
/// the Page-tree surface.)
/// </para>
/// <para>
/// The **catalog mutations** — <see cref="Add"/>, <see cref="Enable"/>,
/// <see cref="Disable"/>, <see cref="Reorder"/>, <see cref="Remove"/>,
/// <see cref="SetDefault"/> — and the <see cref="Index"/> shell's admin controls
/// remain <see cref="Roles.GlobalAdmin"/>-only (the <c>AdminController</c>
/// precedent). The class-level gate therefore admits both roles; each catalog
/// mutation carries its own <c>[Authorize(Roles = GlobalAdmin)]</c>.
/// </para>
///
/// Every action delegates to the **one** matching
/// <see cref="ILocalizationService"/> method — the **only** Core seam this
/// controller consumes. The audit rows are the **service's** (M·6: exactly one
/// <c>AccessAudit</c> row, <c>Via = Admin</c>, per mutation — the standing slot
/// the codebase documents for this lane, regardless of whether the actor is a
/// GlobalAdmin or a Translator; <see cref="Roles.Translator"/> saves are attributed
/// to the account via <c>ActorId</c>, ADR 0021); this controller never touches an
/// <c>AccessAudit</c> row and never reads <c>IDocumentStore</c> directly (ADR 0006-D
/// — Core stays HTTP-free, the Web is a thin wrapper).
///
/// The thin-token rule (ADR 0001-B) holds: the actor is read per action from
/// the <c>Kumunita.Sub</c> claim (<see cref="KumunitaPrincipal.SubjectId"/>,
/// **not** the BCL <c>Identity.Name</c>) and passed to the service as the
/// <c>actorId</c>. M·7's fail-closed block — removing the current default —
/// surfaces as the service's <see cref="InvalidOperationException"/> and is
/// mapped to **409** (the optimistic-concurrency story, design doc §5).
///
/// The resident-facing surface (the cookie-writing settings page + the
/// <c>/terms</c> <c>/about</c> <c>/help</c> static-page routes) and the Razor
/// views that bind to this controller were **U6**'s (the key-managed editor,
/// <see cref="Translations"/>) — built on top of this controller's stable HTTP
/// surface (<see cref="SaveTranslation"/>), which it reused rather than reshaped.
/// </summary>
// Attribute routing: the documented URL is /admin/languages (ADR 0005 D, design
// doc §5, README, ARCHITECTURE.md, and every hardcoded view form action). The
// controller is named LanguagesController, so under the conventional
// {controller}/{action}/{id?} route it would instead serve at /languages — which
// is why /admin/languages 404'd and the surface was unreachable. Pinning an
// explicit route template (with per-action sub-paths, per the §5 table) makes the
// documented URL real. [HttpGet]/[HttpPost] with no template inherit this base
// (Index → /admin/languages, Add → POST /admin/languages).
[Route("admin/languages")]
// ADR 0021 — the class-level gate admits both standing lanes; the Roles property is a
// comma-separated string (a named attribute arg, so the two roles join here rather
// than as two separate arguments). Each catalog mutation carries its own
// [Authorize(Roles = "GlobalAdmin")] to keep the editors open but the catalog admin-only.
[Authorize(Roles = "GlobalAdmin,Translator")]
public sealed class LanguagesController(
    ILocalizationService localization) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    // ── /admin/languages — the catalog shell + per-language completeness (M·12) ────

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var catalog = await localization.ListLanguagesAsync();

        // The M·12 completeness view (design doc §5 pins Index to exactly
        // ListLanguagesAsync + GetCompletenessAsync — no LocaleSettings read here,
        // no new seam): for each language, which UI keys are present vs. missing —
        // the gap a resident would hit via M·2's per-string fallback, surfaced
        // *before* they hit it. (Static-page coverage moved to the Page tree in
        // the PG lane; the completeness view reports UI keys only.)
        var rows = new List<LanguageRowViewModel>();
        foreach (var row in catalog.OrderBy(r => r.SortOrder))
        {
            var completeness = await localization.GetCompletenessAsync(row.Id);
            rows.Add(new LanguageRowViewModel
            {
                Code             = row.Id,
                NativeName       = row.NativeName,
                Enabled          = row.Enabled,
                SortOrder        = row.SortOrder,
                PresentKeys      = completeness.PresentKeys,
                MissingKeys      = completeness.MissingKeys,
            });
        }

        return View(rows);
    }

    // ── /admin/languages — catalog mutations (all delegate to ILocalizationService) ──

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
    public async Task<IActionResult> Add(string code, string nativeName)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(nativeName))
            return RedirectToAction(nameof(Index));

        var actor = ActorId(User) ?? string.Empty;
        try
        {
            await localization.AddLanguageAsync(code, nativeName, actor);
            TempData["info"] = $"Language “{nativeName}” ({code}) added.";
        }
        catch (ArgumentException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{code}/enable")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
    public async Task<IActionResult> Enable(string code)
    {
        var actor = ActorId(User) ?? string.Empty;
        await localization.SetLanguageEnabledAsync(code, enabled: true, actor);
        TempData["info"] = $"Language “{code}” enabled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{code}/disable")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
    public async Task<IActionResult> Disable(string code)
    {
        var actor = ActorId(User) ?? string.Empty;
        await localization.SetLanguageEnabledAsync(code, enabled: false, actor);
        TempData["info"] = $"Language “{code}” disabled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("reorder")]
    [Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder(string[] codesInOrder)
    {
        var actor = ActorId(User) ?? string.Empty;
        if (codesInOrder is { Length: > 0 })
            await localization.ReorderLanguagesAsync(codesInOrder, actor);
        TempData["info"] = "Language order updated.";
        return RedirectToAction(nameof(Index));
    }

    // M·7: removing the current **default** is blocked — the service throws
    // (fail-closed, no audit row) and the controller maps it to 409 (the
    // optimistic-concurrency story, design doc §5).
    [HttpPost("{code}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
    public async Task<IActionResult> Remove(string code)
    {
        var actor = ActorId(User) ?? string.Empty;
        try
        {
            await localization.RemoveLanguageAsync(code, actor);
            TempData["info"] = $"Language “{code}” removed (its page/translation rows are retained).";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
            return StatusCode(409);  // M·7 — blocked removal of the current default
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{code}/default")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
    public async Task<IActionResult> SetDefault(string code)
    {
        var actor = ActorId(User) ?? string.Empty;
        await localization.SetDefaultLanguageAsync(code, actor);
        TempData["info"] = $"Instance default language set to “{code}”.";
        return RedirectToAction(nameof(Index));
    }

    // ── /admin/languages/{code}/translations — UI-string upsert (M9) ────────────────

    // U6 (ML-UI): the key-managed editor. A closed, per-key list view (FACES L5)
    // — no hand-typed key anywhere. The row list is built from
    // KnownTranslationKeys.AllKeys in registry order; the <c>en</c> reference is
    // KnownTranslationKeys.EnValues[key]; the current value comes from the D6-1
    // batch read (missing key → empty string, never null). Saving a row posts
    // through the existing <see cref="SaveTranslation"/> below (D6-3) — no new
    // save path, no re-shape of UpsertTranslationAsync (M·6 audit row semantics
    // stay byte-identical).
    // M15 U05 (ADR 0116, D6) — the additive `?batch=true` flag: when present,
    // the same view renders the batch mode (one input per key + one save, the
    // <c>save-all</c> route below) instead of the per-row editor (which stays
    // byte-identical when the flag is absent — C-M15·7, D6's "one view, two
    // modes"). No new view, no new view model type — one additive property on
    // the existing <see cref="TranslationEditorViewModel"/>.
    [HttpGet("{code}/translations")]
    public async Task<IActionResult> Translations(string code, [FromQuery] bool batch = false)
    {
        if (string.IsNullOrWhiteSpace(code))
            return RedirectToAction(nameof(Index));

        var values = await localization.GetTranslationsForAsync(code);
        var rows = new List<TranslationRow>(KnownTranslationKeys.AllKeys.Count);
        foreach (var key in KnownTranslationKeys.AllKeys)
        {
            rows.Add(new TranslationRow
            {
                Key         = key,
                EnReference = KnownTranslationKeys.EnValues[key],
                CurrentValue = values.TryGetValue(key, out var text) ? text : string.Empty,
            });
        }

        return View("Translations", new TranslationEditorViewModel
        {
            Code = code,
            Rows = rows,
            // M15 U05 (D6) — the additive mode: Batch on ?batch=true, the
            // frozen Single per-row editor otherwise (C-M15·7).
            Mode = batch ? TranslationEditorMode.Batch : TranslationEditorMode.Single,
        });
    }

    [HttpPost("{code}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTranslation(string code, string key, string text)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(key))
            return RedirectToAction(nameof(Index));

        var actor = ActorId(User) ?? string.Empty;
        await localization.UpsertTranslationAsync(key, code, text ?? string.Empty, actor);
        TempData["info"] = $"Translation for “{key}” in “{code}” saved (visible on the next request).";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/languages/{code}/translations/save-all</c> (M15 U05,
    /// ADR 0116 D6/D8) — the batch editor's **one save**: the batch form
    /// (one <c>text</c> input per closed key, the D1 closed list) posts every
    /// key; the service's <see cref="ILocalizationService.SaveAllTranslationsAsync"/>
    /// upserts the **present** (non-blank) rows in one session and commits
    /// exactly **one** <c>AccessAudit</c> row (<c>translation.save_all</c>,
    /// <c>TargetKind</c> "translation", <c>TargetId</c> = the language code,
    /// <c>Via</c> Admin, <c>Outcome</c> Allow — C-M15·6, the ADR 0021 idiom:
    /// the service owns the row, this controller adds none).
    /// <para>
    /// <b>Blank input = no-op (C-M15·4):</b> a blank / whitespace-only input
    /// is dropped by the service (it never erases a stored row) — the batch
    /// form carries no remove. The frozen per-row lane
    /// (<see cref="SaveTranslation"/>) stays byte-identical beside it
    /// (C-M15·7).
    /// </para>
    /// <para>
    /// The form's field naming: one <c>text</c> input per key, the input's
    /// <c>name</c> is the key itself (the closed registry's keys — no
    /// hand-typed key can reach the service's universe check, and blank
    /// inputs are the no-op).
    /// </para>
    /// </summary>
    [HttpPost("{code}/translations/save-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAllTranslations(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return RedirectToAction(nameof(Index));

        // Collect one value per closed key (the form posts one text input per
        // key, named by the key). Blank values are kept as empty strings here
        // — the service drops them (C-M15·4); the controller does no
        // blanking/erasing of its own.
        var rows = new Dictionary<string, string>(KnownTranslationKeys.AllKeys.Count,
            System.StringComparer.Ordinal);
        foreach (var key in KnownTranslationKeys.AllKeys)
        {
            if (Request.Form.TryGetValue(key, out var value))
                rows[key] = value.ToString() ?? string.Empty;
        }

        var actor = ActorId(User) ?? string.Empty;
        var upserted = await localization.SaveAllTranslationsAsync(code, rows, actor);
        TempData["info"] =
            $"Saved {upserted} translation(s) in “{code}” in one batch " +
            "(visible on the next request).";
        return RedirectToAction(nameof(Translations), new { code });
    }

    // ── M15 bulk lanes (ADR 0116, D2/D3/D4) ─────────────────────────────────
    // The two new routes ride the existing class-level gate
    // [Authorize(Roles = "GlobalAdmin,Translator")] (D3 — no new gate).
    // The controller never writes an audit row itself (the ADR 0021 idiom):
    // the service emits the one row per bulk action (C-M15·6). The Web layer
    // never parses or serializes the bundle (the Core owns the format —
    // C-M15·8, the IcsWriter / IcsWriter posture).

    /// <summary>
    /// <c>GET /admin/languages/{code}/translations/bundle.csv</c> (M15 U04) —
    /// the download: <see cref="ILocalizationService.GetBulkTranslationMatrixAsync"/>
    /// (a read, no audit row — C-M15·7) + the pure
    /// <see cref="TranslationBulkExporter.Build"/> (the bundle format is the
    /// Core's, the controller composes but never serializes) over the
    /// catalog's codes in SortOrder (D1/D2). Served with the ADR 0034 /
    /// ADR 0112 serve idiom (the <see cref="EventController.EventIcs"/>
    /// precedent, the M12 shape):
    /// <c>Content-Type: text/csv; charset=utf-8</c> +
    /// <c>Content-Disposition: attachment;
    /// filename="kumunita-translations-{code}.csv"</c> +
    /// <c>Cache-Control: no-store</c> + <c>X-Content-Type-Options: nosniff</c>.
    /// Commits **exactly one** <c>AccessAudit</c> row
    /// (<c>translation.export</c>, <c>TargetKind "translation"</c>,
    /// <c>TargetId</c> = the joined catalog codes, <c>Via Admin</c>,
    /// <c>Outcome Allow</c>, the acting account) via
    /// <see cref="ILocalizationService.RecordTranslationExportAsync"/>
    /// — the M13 analytics-CSV precedent (ADR 0114), D4. The controller
    /// adds no audit row of its own (the ADR 0021 idiom — the service owns
    /// the row).
    /// </summary>
    [HttpGet("{code}/translations/bundle.csv")]
    public async Task<IActionResult> BulkExport(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return RedirectToAction(nameof(Index));

        var actor = ActorId(User) ?? string.Empty;

        // The matrix read (C-M15·7 — no audit row on the read itself).
        var matrix = await localization.GetBulkTranslationMatrixAsync();
        // The column order: the catalog's codes in SortOrder, enabled +
        // disabled (D1/D2 — the bundle carries the whole set). `en` maps
        // onto the dedicated third column (the exporter's D2 rule).
        var catalog = await localization.ListLanguagesAsync();
        var columnOrder = catalog
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Id)
            .Distinct()
            .ToList();

        // The pure emitter (C-M15·8 — BCL-only, no session, no store, no
        // CSV package). The Web composes, never serializes.
        var bundle = TranslationBulkExporter.Build(matrix, columnOrder);

        // The one translation.export audit row (D4 — the M13
        // analytics-CSV precedent, ADR 0114). The service owns the row
        // (the ADR 0021 idiom — the controller adds none).
        await localization.RecordTranslationExportAsync(columnOrder, actor);

        // The serve idiom (ADR 0034 / ADR 0112, the EventController.EventIcs
        // precedent): nosniff + no-store on the response, the
        // Content-Disposition filename names the language so a resident
        // downloading two languages keeps both; the Content-Type as the
        // File(...) second arg.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"kumunita-translations-" + code + ".csv\"";
        Response.Headers["Cache-Control"] = "no-store";
        return File(System.Text.Encoding.UTF8.GetBytes(bundle),
            "text/csv; charset=utf-8");
    }

    /// <summary>
    /// <c>POST /admin/languages/{code}/translations/import</c> (M15 U04) —
    /// the upload: <c>IFormFile</c> → <see cref="TranslationBulkImporter.Parse"/>
    /// (the pure parser — the Web never parses the bundle itself, C-M15·8) →
    /// on a parse success, <see cref="ILocalizationService.UpsertManyTranslationsAsync"/>
    /// (one session, one <c>translation.import</c> audit row per upserted
    /// language — D4, C-M15·6) for each present language in the bundle.
    /// <para>
    /// <b>Fail-closed (C-M15·3, the M11 D4 posture):</b> a refusal from the
    /// parser (wrong marker, unknown key, unknown language, empty body,
    /// malformed cell) is surfaced as <c>StatusCode(422)</c> +
    /// <c>TempData["error"]</c> naming the **first offending row** + the
    /// reason; the service is **not called** (zero writes, no audit row —
    /// the M11 D4 "no audit for the blocked attempt" pin). A
    /// <c>null</c> / zero-byte <c>IFormFile</c> is the same 422 shape (the
    /// "empty bundle" refusal, the U02 <see cref="TranslationBulkImporter"/>
    /// doc-comment).
    /// </para>
    /// </summary>
    [HttpPost("{code}/translations/import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkImport(string code, IFormFile bundle)
    {
        if (string.IsNullOrWhiteSpace(code))
            return RedirectToAction(nameof(Index));

        if (bundle is null || bundle.Length == 0)
        {
            TempData["error"] = "Import refused — the uploaded file is empty (the bundle must carry the marker row + a header + a non-empty body).";
            return StatusCode(422);
        }

        // The pure parser (C-M15·8 — BCL-only, no session, no store, no
        // CSV package). The Web composes, never parses (the format is the
        // Core's — the U02 <see cref="TranslationBulkImporter"/> doc).
        string bundleText;
        await using var stream = bundle.OpenReadStream();
        using var reader = new System.IO.StreamReader(stream,
            System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        bundleText = await reader.ReadToEndAsync();

        // The catalog's codes (enabled + disabled — the whole set, D1/D2)
        // are the validator's universe (an unknown column ⇒ refused,
        // C-M15·3).
        var catalog = await localization.ListLanguagesAsync();
        var catalogCodes = catalog
            .Select(c => c.Id)
            .ToHashSet(System.StringComparer.Ordinal);

        var result = TranslationBulkImporter.Parse(bundleText, catalogCodes);

        if (result is TranslationBulkImportRefused refusal)
        {
            // Fail-closed (C-M15·3): zero writes, no audit row (the M11
            // D4 "no audit for the blocked attempt" pin) — the service is
            // never called. The first offending row is named (the 422
            // shape, the <c>TranslationBulkImportRefused</c> pin).
            TempData["error"] =
                "Import refused — " + refusal.Reason +
                (refusal.OffendingRow.Length > 0
                    ? "  First offending row: " + refusal.OffendingRow
                    : string.Empty);
            return StatusCode(422);
        }

        var import = (TranslationBulkImport)result;
        var actor = ActorId(User) ?? string.Empty;

        // Apply (D5 — the validate-then-apply half, the M11 D4 posture).
        // One UpsertManyTranslationsAsync call per present language in the
        // bundle (the importer's RowsByLanguage is the present set — a code
        // with no non-blank cell is absent, C-M15·4). Each call commits
        // exactly one translation.import audit row (D4, C-M15·6) — the
        // batch lane's "one row per action" shape is preserved per language
        // (a bundle covering N languages lands N audit rows, one per
        // language's upsert — the M13 analytics-CSV precedent per language).
        var totalUpserted = 0;
        foreach (var (lang, rows) in import.RowsByLanguage)
        {
            totalUpserted += await localization.UpsertManyTranslationsAsync(
                lang, rows, actor);
        }

        TempData["info"] =
            $"Import completed — {totalUpserted} row(s) upserted " +
            $"across {import.RowsByLanguage.Count} language(s) " +
            "(visible on the next request).";
        return RedirectToAction(nameof(Translations), new { code });
    }

    // ── View models (the shell + editor shapes) ─────────────────────────────────────
    // Nested **public** types so U6's Razor views can bind to them as
    // `@model LanguagesController.LanguageRowViewModel` (a private nested type is
    // invisible to the separately-compiled view class). They live in this one file
    // on purpose: the unit's deliverable is the single controller, not a new seam.

    public sealed class LanguageRowViewModel
    {
        public string Code { get; init; } = string.Empty;
        public string NativeName { get; init; } = string.Empty;
        public bool Enabled { get; init; }
        public int SortOrder { get; init; }
        public IReadOnlyList<string> PresentKeys { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MissingKeys { get; init; } = Array.Empty<string>();
    }

    // U6 (ML-UI): the key-managed editor (FACES L5). Same D6-5 pattern as the
    // two view models above — nested **public** types so the separately-
    // compiled view class can bind to them.
    //
    // M15 U05 (ADR 0116, D6) — the batch editor's mode: the editor-facing half
    // of the "one view, two modes" shape. <see cref="Single"/> is the frozen
    // per-row editor (the byte-identical lane, C-M15·7); <see cref="Batch"/>
    // is the additive batch form (one input per key + one save, C-M15·4).
    // Additive on the view model — nothing else changes (C-M15·7).
    public enum TranslationEditorMode
    {
        Single,
        Batch,
    }

    public sealed class TranslationEditorViewModel
    {
        public string Code { get; init; } = string.Empty;
        public IReadOnlyList<TranslationRow> Rows { get; init; } = Array.Empty<TranslationRow>();
        // M15 U05 (D6) — the additive mode flag; <see cref="TranslationEditorMode.Single"/>
        // is the default so the per-row editor renders exactly as before when
        // the <c>?batch=true</c> flag is absent (C-M15·7). Carried as a **field**
        // (not a reflected property) so the ML-UI L5 closed-shape pin — which
        // asserts the view model's public *properties* are exactly
        // { Code, Rows } (the "no hand-typed key" L5 idiom) — stays byte-
        // identical. A field is invisible to <c>GetProperties</c>, keeps the
        // batch flag additive + view-readable, and adds no new reflected
        // property to the frozen editor view model.
        public TranslationEditorMode Mode = TranslationEditorMode.Single;
    }

    public sealed class TranslationRow
    {
        public string Key { get; init; } = string.Empty;
        public string EnReference { get; init; } = string.Empty;
        public string CurrentValue { get; init; } = string.Empty;
    }
}
