using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/languages</c> surface (ADR 0005 D; ML lane U5) — the
/// GlobalAdmin's thin control plane over the multilingual lane, <see
/// cref="Roles.GlobalAdmin"/>-gated (the <c>AdminController</c> precedent).
///
/// Every action delegates to the **one** matching
/// <see cref="ILocalizationService"/> method — the **only** Core seam this
/// controller consumes. The audit rows are the **service's** (M·6: exactly one
/// <c>AccessAudit</c> row, <c>Via = Admin</c>, per mutation); this controller
/// never touches an <c>AccessAudit</c> row and never reads
/// <c>IDocumentStore</c> directly (ADR 0006-D — Core stays HTTP-free, the Web is
/// a thin wrapper).
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
/// surface (<see cref="SaveTranslation"/> / <see cref="SavePage"/>), which it
/// reused rather than reshaped.
/// </summary>
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
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
        // no new seam): for each language, which UI keys and static pages are
        // present vs. missing — the gap a resident would hit via M·2's per-string
        // fallback, surfaced *before* they hit it.
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
                PresentPageSlugs = completeness.PresentPageSlugs,
                MissingPageSlugs = completeness.MissingPageSlugs,
            });
        }

        return View(rows);
    }

    // ── /admin/languages — catalog mutations (all delegate to ILocalizationService) ──

    [HttpPost]
    [ValidateAntiForgeryToken]
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(string code)
    {
        var actor = ActorId(User) ?? string.Empty;
        await localization.SetLanguageEnabledAsync(code, enabled: true, actor);
        TempData["info"] = $"Language “{code}” enabled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(string code)
    {
        var actor = ActorId(User) ?? string.Empty;
        await localization.SetLanguageEnabledAsync(code, enabled: false, actor);
        TempData["info"] = $"Language “{code}” disabled.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
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
    [HttpPost]
    [ValidateAntiForgeryToken]
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

    [HttpPost]
    [ValidateAntiForgeryToken]
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
    [HttpGet]
    public async Task<IActionResult> Translations(string code)
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
        });
    }

    [HttpPost]
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

    // ── /admin/languages/{code}/pages/{slug} — static-page editor/preview (M5/M13) ──

    [HttpGet]
    public async Task<IActionResult> PreviewPage(string code, string slug)
    {
        var page = await localization.GetPageAsync(slug, code);
        return View(new PageEditorViewModel
        {
            Code  = code,
            Slug  = slug,
            Title = page?.Title ?? string.Empty,
            Body  = page?.Body ?? string.Empty,
            HasSaved = page is not null,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePage(string code, string slug, string title, string body)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(slug))
            return RedirectToAction(nameof(Index));

        var actor = ActorId(User) ?? string.Empty;
        await localization.UpsertPageAsync(slug, code, title ?? string.Empty, body ?? string.Empty, actor);
        TempData["info"] = $"Page “{slug}” in “{code}” saved (visible on the next request).";
        return RedirectToAction(nameof(PreviewPage), new { code, slug });
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
        public IReadOnlyList<string> PresentPageSlugs { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> MissingPageSlugs { get; init; } = Array.Empty<string>();
    }

    public sealed class PageEditorViewModel
    {
        public string Code { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public bool HasSaved { get; init; }
    }

    // U6 (ML-UI): the key-managed editor (FACES L5). Same D6-5 pattern as the
    // two view models above — nested **public** types so the separately-
    // compiled view class can bind to them.
    public sealed class TranslationEditorViewModel
    {
        public string Code { get; init; } = string.Empty;
        public IReadOnlyList<TranslationRow> Rows { get; init; } = Array.Empty<TranslationRow>();
    }

    public sealed class TranslationRow
    {
        public string Key { get; init; } = string.Empty;
        public string EnReference { get; init; } = string.Empty;
        public string CurrentValue { get; init; } = string.Empty;
    }
}
