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
    IDocumentStore store) : Controller
{
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
            CurrentCode = LocaleCookie.Read(Request),
            DefaultCode = defaultCode,
        };

        // "Index" is resolved under this controller's folder → Views/PublicLocale/Index.cshtml.
        return View("Index", model);
    }

    /// <summary>
    /// <c>POST /language</c> — the exact semantics of <see cref="LocaleController.Save"/>:
    /// a <c>code</c> form value → <see cref="LocaleCookie.Write"/>; <c>clear=1</c> →
    /// <see cref="LocaleCookie.Clear"/> (reset to the instance default). The change
    /// takes effect on the <strong>next</strong> request (M·4 — data, not config).
    /// </summary>
    [HttpPost("/language")]
    [ValidateAntiForgeryToken]
    public IActionResult Save(string? code, string? clear)
    {
        if (clear == "1")
        {
            LocaleCookie.Clear(Response);
            TempData["info"] = "Language preference reset — the instance default will be used.";
        }
        else if (!string.IsNullOrWhiteSpace(code))
        {
            LocaleCookie.Write(Response, code);
            TempData["info"] = $"Language preference set to \"{code}\" — it takes effect on the next request.";
        }

        return RedirectToAction(nameof(Index));
    }
}
