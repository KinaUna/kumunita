using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;
/// <summary>
/// The <strong>public</strong> language picker (ADR 0005 B; M·11; ML-UI U7, D7-1).
/// A signed-out resident can choose a language without signing in: the write is
/// the same frozen <see cref="LocaleCookie"/> cookie (M·5 — a preference, never a
/// claim, never part of the authz decision), so it is harmless and public.
///
/// This is a <strong>separate route</strong> (<c>/language</c>) from the
/// <c>[Authorize]</c>d settings page (<c>/settings/language</c>,
/// <see cref="LocaleController"/>). The settings page stays the full settings
/// experience; this is the compact, layout-visible quick picker. Neither this
/// file nor its view modifies the frozen <see cref="LocaleController"/> or
/// <c>Views/Locale/Index.cshtml</c> (D7-1/D7-2).
///
/// The view model is reused from <see cref="LocaleController.LocaleSettingsViewModel"/>
/// (a public nested type) — not duplicated.
/// </summary>
public sealed class PublicLocaleController(
    ILocalizationService localization,
    IDocumentStore store,
    // The per-request translation read seam — renders the language-change
    // flash message in the visitor's current language (locale.flash_set /
    // locale.flash_reset). Optional (default null) so any test-construction
    // site that builds this controller without the seam keeps compiling and
    // renders the provider floor's English; DI always supplies the live
    // ITranslationProvider in the app.
    ITranslationProvider? translationProvider = null) : Controller
{
    /// <summary>
    /// Resolve a locale flash-message template (locale.flash_set /
    /// locale.flash_reset) in the visitor's current effective language and
    /// apply its {0} placeholder(s). Falls back to the English source text
    /// (the provider floor — code is the floor, ADR 0015 D1) when the seam is
    /// absent or the key is unregistered, so a resident never sees a raw key.
    /// </summary>
    private async Task<string> FlashAsync(string key, params object?[] args)
    {
        var template = translationProvider is null
            ? KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key
            : await translationProvider.GetAsync(key,
                await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider));
        return args.Length > 0
            ? System.String.Format(System.Globalization.CultureInfo.InvariantCulture, template, args)
            : template;
    }

    /// <summary>
    /// Resolve a flash-message template in an **explicit** effective language
    /// code (the language the visitor just selected — not the request's
    /// current one) and apply the {0} placeholder. Same floor discipline as
    /// <see cref="FlashAsync"/>.
    /// </summary>
    private async Task<string> FlashAsyncIn(string key, string lang, params object?[] args)
    {
        var template = translationProvider is null
            ? KnownTranslationKeys.EnValues.GetValueOrDefault(key) ?? key
            : await translationProvider.GetAsync(key, lang);
        return args.Length > 0
            ? System.String.Format(System.Globalization.CultureInfo.InvariantCulture, template, args)
            : template;
    }
    /// <summary>
    /// <c>GET /language</c> — the compact picker. Builds the <em>same</em> model
    /// shape as <see cref="LocaleController.Index"/> (the enabled catalog in
    /// <c>SortOrder</c>, the <c>LocaleSettings</c> instance default, and the
    /// current cookie) and renders the compact <c>PublicLocale/Index</c> view.
    /// No <c>[Authorize]</c> — reachable by signed-out visitors (M·11).
    /// </summary>
    [HttpGet("/language")]
    public async Task<IActionResult> Index()
    {
        var catalog = await localization.ListLanguagesAsync();

        // The instance default (M·1: preference → default → "en") — the same
        // frozen read idiom as LocaleController.Index (IDocumentStore singleton
        // load of LocaleSettings).
        string defaultCode = "en";
        await using var session = store.QuerySession();
        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, System.Threading.CancellationToken.None);
        if (settings is not null)
            defaultCode = settings.DefaultLanguageCode;

        var model = new LocaleController.LocaleSettingsViewModel
        {
            Languages = catalog
                .Where(l => l.Enabled)
                .OrderBy(l => l.SortOrder)
                .Select(l => new LocaleController.LocaleOption(l.Id, l.NativeName))
                .ToList(),
            DefaultCode = defaultCode,
            CurrentCode = LocaleCookie.Read(Request) ?? defaultCode,
        };
        return View("Index", model);
    }

    /// <summary>
    /// <c>POST /language</c> — the exact semantics of <see cref="LocaleController.Save"/>:
    /// a <c>code</c> form value → <see cref="LocaleCookie.Write"/>; <c>clear=1</c> →
    /// <see cref="LocaleCookie.Clear"/> (reset to the instance default). The change
    /// takes effect on the <strong>next</strong> request (M·4 — data, not config).
    /// A <c>returnUrl</c> (the nav-bar language switcher) redirects back to the
    /// page the resident was on; without it the picker page is the destination.
    /// </summary>
    [HttpPost("/language")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? code, string? clear, string? returnUrl = null)
    {
        // Resolve the flash text in the language the visitor **selects** —
        // the toast is shown on the *next* request, which already renders in
        // the new language, and that is the language the user is most likely
        // to understand (the old behavior resolved it in the previous
        // language; the message landed on a page written in the new one).
        // A "reset to instance default" has no explicit code — fall back to
        // the request's current effective language.
        string? effectiveCode;
        if (!string.IsNullOrWhiteSpace(code))
            effectiveCode = code;
        else if (translationProvider is not null)
            effectiveCode = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
        else
            effectiveCode = null;

        string? lang = translationProvider is not null
            ? await translationProvider.ResolveEffectiveLanguageAsync(effectiveCode)
            : effectiveCode;
        string? info;
        if (clear == "1")
        {
            info = lang is not null
                ? await FlashAsyncIn("locale.flash_reset", lang)
                : await FlashAsync("locale.flash_reset");
        }
        else if (!string.IsNullOrWhiteSpace(code))
        {
            info = lang is not null
                ? await FlashAsyncIn("locale.flash_set", lang, code)
                : await FlashAsync("locale.flash_set", code);
        }
        else
        {
            info = null; // no language change requested
        }

        if (clear == "1")
            LocaleCookie.Clear(Response);
        else if (!string.IsNullOrWhiteSpace(code))
            LocaleCookie.Write(Response, code);

        if (info is not null)
            TempData["info"] = info;

        // The nav switcher carries the page it came from; the compact picker does
        // not — fall back to the picker's Index view. A relative URL only (no
        // open redirect — a scheme/rooted target is rejected by IsUrlRelative).
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }
}
