using Kumunita.Core.Localization;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident's locale-preference settings page (ADR 0005 B; M·5, M7 FACES).
/// The <see cref="LocaleCookie"/> read/write/clear trio is consumed **here** —
/// this is the settings-page save (M7: the cookie is written → the **next**
/// request renders in the new language). The cookie is **never** a claim
/// (thin-token rule, ADR 0001-B); it is passed to
/// <see cref="ITranslationProvider"/> as a plain BCP-47 string on every
/// subsequent request (M·8 — Core stays HTTP-free).
/// </summary>
[Authorize]
public sealed class LocaleController(
    ILocalizationService localization,
    IDocumentStore store) : Controller
{
    /// <summary>
    /// <c>GET /settings/language</c> — the resident's language-preference page.
    /// Reads the enabled catalog (the picker) + the instance default
    /// (<c>LocaleSettings</c> singleton) for the "default" marker, and echoes
    /// the current cookie value (M·1's "preference if present" state).
    /// </summary>
    [HttpGet("/settings/language")]
    public async Task<IActionResult> Index()
    {
        var catalog = await localization.ListLanguagesAsync();

        // The instance default (M·1: preference → default → "en").
        // ILocalizationService does not expose a GetDefaultLanguageAsync;
        // the U5 handoff note confirms U6 sources this from its own read path.
        string defaultCode = "en";
        await using var session = store.QuerySession();
        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, System.Threading.CancellationToken.None);
        if (settings is not null)
            defaultCode = settings.DefaultLanguageCode;

        var model = new LocaleSettingsViewModel
        {
            Languages = catalog
                .Where(l => l.Enabled)
                .OrderBy(l => l.SortOrder)
                .Select(l => new LocaleOption(l.Id, l.NativeName))
                .ToList(),
            CurrentCode = LocaleCookie.Read(Request),
            DefaultCode = defaultCode,
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /settings/language</c> — the M7 FACES save.
    /// With a <c>code</c> form value: <see cref="LocaleCookie.Write"/>
    /// (365-day cookie, <c>HttpOnly</c>, <c>SameSite=Lax</c>).
    /// With <c>clear=1</c>: <see cref="LocaleCookie.Clear"/>
    /// (the "reset to default" action — the next request resolves to the
    /// instance default, M·1). The change takes effect on the **next** request
    /// (M·4 — data, not config; no rebuild, no restart).
    /// </summary>
    [HttpPost("/settings/language")]
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

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class LocaleSettingsViewModel
    {
        public List<LocaleOption> Languages { get; init; } = new();
        public string? CurrentCode { get; init; }
        public string DefaultCode { get; init; } = "en";
    }

    public sealed record LocaleOption(string Code, string NativeName);
}
