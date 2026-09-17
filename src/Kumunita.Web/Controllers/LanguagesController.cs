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
    [HttpGet("{code}/translations")]
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
