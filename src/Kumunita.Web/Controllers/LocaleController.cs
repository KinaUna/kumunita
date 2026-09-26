using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident's settings surface (ADR 0005 B; M·5, M7 FACES) — three
/// linkable section pages (ADR 0080): **Language** (<c>/settings/language</c>,
/// which now also carries the **Email &amp; notification language** section,
/// ADR 0061, folded in 2026-09-30), **Time zone** (<c>/settings/timezone</c>,
/// ADR 0019's resident surface, folded in 2026-09-13 and split back out
/// 2026-09-25) and **Date &amp; time format** (<c>/settings/dateformat</c>,
/// ADR 0020). Each page is its own GET action rendering its section of the
/// same <see cref="LocaleSettingsViewModel"/>; the four POST save lanes are
/// unchanged — the email-language save now redirects back to the **Language**
/// page that owns the section it writes.
/// The <see cref="LocaleCookie"/> read/write/clear trio is consumed **here** —
/// this is the settings-page save (M7: the cookie is written → the **next**
/// request renders in the new language). The cookie is **never** a claim
/// (thin-token rule, ADR 0001-B); it is passed to
/// <see cref="ITranslationProvider"/> as a plain BCP-47 string on every
/// subsequent request (M·8 — Core stays HTTP-free).
/// <para>
/// The **time zone** section is the user half of ADR 0019: the platform
/// default (<c>LocaleSettings.DefaultTimezone</c>, admin-written, <c>UTC</c>
/// floor, read through
/// <see cref="ILocalizationService.GetDefaultTimezoneAsync"/>) is the fallback;
/// this page sets/clears the resident's **personal override** — a persisted
/// <see cref="Profile.TimeZone"/> field written through the
/// <see cref="IUserInfoService.SetProfileTimezoneAsync"/> lane (the
/// <c>SetProfileAvatarAsync</c> single-write-lane shape — owner-scope, no
/// audit row). The <c>SaveTimezone</c> action is the former
/// <c>TimezoneController.Save</c> verbatim (same routes, same fail-closed
/// <c>KeyNotFoundException</c> catch); only the home moved.
/// </para>
/// </summary>
[Authorize]
public sealed class LocaleController(
    ILocalizationService localization,
    IUserInfoService userInfo,
    IDocumentStore store,
    // The per-request translation read seam — used to render the
    // language-change flash message in the resident's current language
    // (the locale.flash_set / locale.flash_reset keys). Optional (default
    // null) so any test-construction site that builds this controller
    // without the seam keeps compiling and renders the provider floor's
    // English; DI always supplies the live ITranslationProvider in the app.
    ITranslationProvider? translationProvider = null) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// Resolve a settings flash-message template (locale.flash_*,
    /// settings.timezone_flash_*, settings.dateformat_flash_*,
    /// settings.email_flash_*) in the resident's current effective language
    /// and apply its {0} placeholder(s). Falls back to the English source text
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
    /// code (the language the resident just selected — not the request's
    /// current one) and apply the {0}/{1} placeholders. Same floor discipline
    /// as <see cref="FlashAsync"/>.
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
    /// <c>GET /settings/language</c> — the resident's settings page: the
    /// language section (the enabled catalog picker + the instance default
    /// marker, echoing the current cookie value — M·1's "preference if
    /// present" state) **and** the time-zone section (ADR 0019 — the actor's
    /// <see cref="Profile.TimeZone"/> override, pre-selected against the OS
    /// zones, with the platform default as marker/fallback).
    /// </summary>
    [HttpGet("/settings/language")]
    public async Task<IActionResult> Index()
    {
        return View(await BuildModel());
    }

    /// <summary>
    /// <c>GET /settings/timezone</c> — the resident's time-zone settings
    /// section (ADR 0019) on its own linkable page (ADR 0080). The model is
    /// the full <see cref="LocaleSettingsViewModel"/>; the view renders only
    /// the time-zone section.
    /// </summary>
    [HttpGet("/settings/timezone")]
    public async Task<IActionResult> SettingsTimezone()
    {
        return View("Timezone", await BuildModel());
    }

    /// <summary>
    /// <c>GET /settings/dateformat</c> — the resident's date-time format
    /// settings section (ADR 0020) on its own linkable page (ADR 0080). The
    /// model is the full <see cref="LocaleSettingsViewModel"/>; the view
    /// renders only the date-format section.
    /// </summary>
    [HttpGet("/settings/dateformat")]
    public async Task<IActionResult> SettingsDateFormat()
    {
        return View("DateFormat", await BuildModel());
    }

    /// <summary>
    /// <c>GET /settings/email-language</c> — retired as its own page: the
    /// email &amp; notification language section (ADR 0061) was folded into
    /// the **Language** tab (ADR 0080, 2026-09-30). The route is kept as a
    /// redirect so saved links and deep references still land the resident on
    /// the settings tab that now owns the section.
    /// </summary>
    [HttpGet("/settings/email-language")]
    public IActionResult SettingsEmailLanguage()
    {
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Builds the full <see cref="LocaleSettingsViewModel"/> (all four
    /// sections populated) — shared by the four settings GET actions (Index
    /// + the three section actions). Extracted so each section view renders
    /// its slice without re-reading the catalog / profile / platform
    /// defaults.
    /// </summary>
    private async Task<LocaleSettingsViewModel> BuildModel()
    {
        var catalog = await localization.ListLanguagesAsync();

        // The time-zone section's actor + current override (null-safe: an
        // unauthenticated request has no subject and no profile read).
        string? subject = SubjectId(User);
        Profile? profile = subject is null ? null : await userInfo.GetProfileAsync(subject);
        string? currentTz = profile?.TimeZone;
        string? currentDf = profile?.DateFormat;
        string? currentEl = profile?.EmailLanguage;

        // The instance default (M·1: preference → default → "en").
        // ILocalizationService does not expose a GetDefaultLanguageAsync;
        // the U5 handoff note confirms U6 sources this from its own read path.
        string defaultCode = "en";
        await using var session = store.QuerySession();
        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, System.Threading.CancellationToken.None);
        if (settings is not null)
            defaultCode = settings.DefaultLanguageCode;

        // ADR 0046: the pre-selection = the saved preference (the cookie) or,
        // failing that, the browser's Accept-Language match against the
        // enabled catalog — a suggestion only, never persisted (the cookie
        // stays the only write path).
        var enabledRows = catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .ToList();
        var saved = LocaleCookie.Read(Request);
        var browserCode = string.IsNullOrWhiteSpace(saved)
            ? RequestLanguage.Browser(Request, enabledRows)
            : null;
        var languageOptions = enabledRows
            .Select(l => new LocaleOption(l.Id, l.NativeName))
            .ToList();

        return new LocaleSettingsViewModel
        {
            Languages = languageOptions,
            CurrentCode = saved,
            BrowserCode = browserCode,
            DefaultCode = defaultCode,

            // The time-zone section (ADR 0019): the OS zones (the shared
            // EffectiveTimezoneResolver.Zones source), the actor's saved
            // override (or null), the pre-selected id (override if set, else
            // the platform default), and the platform default for the marker.
            Timezone = BuildTimezoneSection(subject, currentTz,
                defaultTz: await localization.GetDefaultTimezoneAsync()),
            DateFormat = BuildDateFormatSection(subject, currentDf,
                defaultFmt: await localization.GetDefaultDateFormatAsync()),

            // ADR 0061 — the email & notification language section: the
            // actor's saved outbound-language override (or null = instance
            // default), pre-selected against the enabled catalog; the
            // instance default is the "default" marker. Reuses the enabled
            // catalog list (Languages) for its picker.
            EmailLanguage = subject is null
                ? null
                : new LocaleSettingsViewModel.EmailLanguageSettings
                {
                    Languages = languageOptions,
                    CurrentCode = currentEl,
                    SelectedCode = string.IsNullOrWhiteSpace(currentEl) ? defaultCode : currentEl,
                    DefaultCode = defaultCode,
                },
        };
    }

    /// <summary>Seeds the time-zone section of <see cref="LocaleSettingsViewModel"/>.
    /// Extracted (static) so the tests can target the shaping directly, without
    /// an HTTP action round-trip.</summary>
    private static LocaleSettingsViewModel.TimezoneSettings? BuildTimezoneSection(
        string? subject, string? currentTz, string defaultTz)
    {
        if (subject is null) return null;

        // The "current" marker is always a concrete, selectable id: the
        // override if set, else the platform default (the language section's
        // same "default marker" shape).
        string selected = string.IsNullOrWhiteSpace(currentTz) ? defaultTz : currentTz;

        return new LocaleSettingsViewModel.TimezoneSettings
        {
            Zones = EffectiveTimezoneResolver.Zones(),
            CurrentId = currentTz,
            SelectedId = selected,
            DefaultId = defaultTz,
        };
    }

    /// <summary>Seeds the date-time format section of
    /// <see cref="LocaleSettingsViewModel"/>. Extracted (static) so the tests
    /// can target the shaping directly, without an HTTP action round-trip —
    /// the same shape as <see cref="BuildTimezoneSection"/></summary>
    private static LocaleSettingsViewModel.DateFormatSettings? BuildDateFormatSection(
        string? subject, string? currentFmt, string defaultFmt)
    {
        if (subject is null) return null;

        // The "current" marker is always a concrete format string: the
        // override if set, else the platform default (the timezone section's
        // same "default marker" shape).
        string selected = string.IsNullOrWhiteSpace(currentFmt) ? defaultFmt : currentFmt;

        return new LocaleSettingsViewModel.DateFormatSettings
        {
            Presets = Kumunita.Core.Localization.DateFormat.Presets
                .Select(p => (p.Label, p.Format))
                .ToList(),
            CurrentFormat = currentFmt,
            SelectedFormat = selected,
            DefaultFormat = defaultFmt,
            // A preset (Match returns the preset) → no custom box; a custom
            // format string (Match returns null) → the view reveals the box.
            CurrentIsPreset = Kumunita.Core.Localization.DateFormat.Match(selected) is not null,
        };
    }

    /// <summary>
    /// <c>POST /settings/language</c> — the M7 FACES save (the language
    /// section of the settings page).
    /// With a <c>code</c> form value: <see cref="LocaleCookie.Write"/>
    /// (365-day cookie, <c>HttpOnly</c>, <c>SameSite=Lax</c>).
    /// With <c>clear=1</c>: <see cref="LocaleCookie.Clear"/>
    /// (the "reset to default" action — the next request resolves to the
    /// instance default, M·1). The change takes effect on the **next** request
    /// (M·4 — data, not config; no rebuild, no restart).
    /// </summary>
    [HttpPost("/settings/language")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? code, string? clear)
    {
        // Resolve the flash text in the language the resident **selects** —
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

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /settings/timezone</c> — the time-zone section save (ADR 0019).
    /// With a <c>timezone</c> form value: <see cref="IUserInfoService.SetProfileTimezoneAsync"/>
    /// (sets the override; the self-scope check is this page's <c>[Authorize]</c>
    /// gate + the actor being the subject). With <c>clear=1</c>: the same lane
    /// with a <c>null</c> value (the "reset to platform default" action — the
    /// next request resolves to the instance default). The change is live on
    /// the very next request (M·4: data, not config). A missing profile (an
    /// edge-case pre-bootstrap account) fails closed: the lane throws
    /// <c>KeyNotFoundException</> and we surface the error + redirect (the
    /// <c>SetProfileAvatarAsync</c> pin). (Former
    /// <c>TimezoneController.Save</c>; same routes, same semantics.)
    /// </summary>
    [HttpPost("/settings/timezone")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTimezone(string? timezone, string? clear)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
            return RedirectToAction(nameof(SettingsTimezone));

        try
        {
            if (clear == "1")
            {
                // null ⇒ clear (the lane stores + saves, so the clear persists
                // — the next request resolves to the instance default).
                await userInfo.SetProfileTimezoneAsync(subject, null, subject);
                TempData["info"] = await FlashAsync("settings.timezone_flash_reset");
            }
            else if (!string.IsNullOrWhiteSpace(timezone))
            {
                await userInfo.SetProfileTimezoneAsync(subject, timezone, subject);
                TempData["info"] = await FlashAsync("settings.timezone_flash_set", timezone);
            }
        }
        catch (KeyNotFoundException)
        {
            // A pre-bootstrap edge (no profile row). Fail closed + surface the
            // error; the lane never load-or-creates (the SetProfileAvatarAsync
            // pin), so nothing is half-written.
            TempData["error"] = "Your profile is not available — sign out and back in.";
        }

        return RedirectToAction(nameof(SettingsTimezone));
    }

    /// <summary>
    /// <c>POST /settings/dateformat</c> — the date-time format section save
    /// (ADR 0020). With a <c>format</c> (a preset) or <c>customFormat</c> (a
    /// free-text .NET custom datetime format string) form value:
    /// <see cref="IUserInfoService.SetProfileDateFormatAsync"/> (sets the
    /// override; the self-scope check is this page's <c>[Authorize]</c> gate +
    /// the actor being the subject). With <c>clear=1</c>: the same lane with a
    /// <c>null</c> value (the "reset to platform default" action — the next
    /// request resolves to the instance default). A missing profile (an
    /// edge-case pre-bootstrap account) fails closed: the lane throws
    /// <c>KeyNotFoundException</c> and we surface the error + redirect (the
    /// <c>SetProfileTimezoneAsync</c> pin).
    /// </summary>
    [HttpPost("/settings/dateformat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDateFormat(string? format, string? customFormat, string? clear)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
            return RedirectToAction(nameof(SettingsDateFormat));

        // The select posts the preset's format string as `format`; when "Custom…"
        // is chosen it posts `customFormat` = the free-text value (and `format`
        // = "custom"). A blank/`"custom"` `customFormat` means "use the preset".
        // The user lane stores whatever it's given (no validation — the resolver
        // degrades a bad value to the default / floor, the ADR 0019 posture);
        // the admin lane is the one that fail-closes with a 409.
        bool hasCustom = !string.IsNullOrWhiteSpace(customFormat) && customFormat != "custom";
        string? chosen = hasCustom ? customFormat : format;

        try
        {
            if (clear == "1")
            {
                // null ⇒ clear (the lane stores + saves, so the clear persists
                // — the next request resolves to the instance default).
                await userInfo.SetProfileDateFormatAsync(subject, null, subject);
                TempData["info"] = await FlashAsync("settings.dateformat_flash_reset");
            }
            else if (!string.IsNullOrWhiteSpace(chosen))
            {
                await userInfo.SetProfileDateFormatAsync(subject, chosen, subject);
                TempData["info"] = await FlashAsync("settings.dateformat_flash_set");
            }
        }
        catch (KeyNotFoundException)
        {
            // A pre-bootstrap edge (no profile row). Fail closed + surface the
            // error; the lane never load-or-creates (the SetProfileTimezoneAsync
            // pin), so nothing is half-written.
            TempData["error"] = "Your profile is not available — sign out and back in.";
        }

        return RedirectToAction(nameof(SettingsDateFormat));
    }

    /// <summary>
    /// <c>POST /settings/email-language</c> — the email &amp; notification
    /// language section save (ADR 0061). With a <c>code</c> form value:
    /// <see cref="IUserInfoService.SetProfileEmailLanguageAsync"/> (sets the
    /// override; the self-scope check is this page's <c>[Authorize]</c> gate +
    /// the actor being the subject). With <c>clear=1</c>: the same lane with a
    /// <c>null</c> value (the "reset to instance default" action — the next
    /// outbound email resolves to the instance default). A missing profile
    /// (an edge-case pre-bootstrap account) fails closed: the lane throws
    /// <c>KeyNotFoundException</c> and we surface the error + redirect (the
    /// <c>SetProfileDateFormatAsync</c> pin).
    /// </summary>
    [HttpPost("/settings/email-language")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmailLanguage(string? code, string? clear)
    {
        var subject = SubjectId(User);
        if (string.IsNullOrEmpty(subject))
            return RedirectToAction(nameof(Index));

        try
        {
            if (clear == "1")
            {
                // null ⇒ clear (the lane stores + saves, so the clear persists
                // — the next outbound email resolves to the instance default).
                await userInfo.SetProfileEmailLanguageAsync(subject, null, subject);
                TempData["info"] = await FlashAsync("settings.email_flash_reset");
            }
            else if (!string.IsNullOrWhiteSpace(code))
            {
                await userInfo.SetProfileEmailLanguageAsync(subject, code, subject);
                TempData["info"] = await FlashAsync("settings.email_flash_set");
            }
        }
        catch (KeyNotFoundException)
        {
            // A pre-bootstrap edge (no profile row). Fail closed + surface the
            // error; the lane never load-or-creates (the
            // SetProfileDateFormatAsync pin), so nothing is half-written.
            TempData["error"] = "Your profile is not available — sign out and back in.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class LocaleSettingsViewModel
    {
        public List<LocaleOption> Languages { get; init; } = new();
        public string? CurrentCode { get; init; }

        /// <summary>ADR 0046 — the browser's <c>Accept-Language</c> matched
        /// against the enabled catalog, pre-selected when
        /// <see cref="CurrentCode"/> is <c>null</c> (no saved preference).
        /// A suggestion only: it is never persisted, and an explicit save
        /// (the cookie write) always wins over it.</summary>
        public string? BrowserCode { get; init; }

        public string DefaultCode { get; init; } = "en";

        /// <summary>The time-zone section (ADR 0019), or <c>null</c> when the
        /// caller has no subject (a signed-out visitor — the <c>/language</c>
        /// quick picker renders the language section only).</summary>
        public TimezoneSettings? Timezone { get; init; }

        /// <summary>The date-time format section (ADR 0020), or <c>null</c>
        /// when the caller has no subject (the public quick picker renders the
        /// language section only).</summary>
        public DateFormatSettings? DateFormat { get; init; }

        /// <summary>The email &amp; notification language section (ADR 0061),
        /// or <c>null</c> when the caller has no subject (the public quick
        /// picker renders the language section only). Reuses the enabled
        /// catalog list (<see cref="Languages"/>) as its picker.</summary>
        public EmailLanguageSettings? EmailLanguage { get; init; }

        /// <summary>The email &amp; notification language section of the
        /// settings page (ADR 0061) — the resident's
        /// <see cref="Kumunita.Core.UserInfo.Profile.EmailLanguage"/>
        /// override for the language the platform writes to them in (outbound
        /// account emails + event reminders). The same picker shape as the
        /// other sections (the enabled catalog), but <c>null</c> is the
        /// "use the instance default" state, not a concrete code.</summary>
        public sealed class EmailLanguageSettings
        {
            /// <summary>The enabled catalog (code → native name) the picker
            /// offers — the same set as <see cref="Languages"/>.</summary>
            public List<LocaleOption> Languages { get; init; } = new();

            /// <summary>The resident's saved outbound-language code, or
            /// <c>null</c> (no override — the instance default applies).</summary>
            public string? CurrentCode { get; init; }

            /// <summary>The code pre-selected in the picker (the override if
            /// set, else the instance default).</summary>
            public string? SelectedCode { get; init; }

            /// <summary>The instance default (the "default" marker in the
            /// picker).</summary>
            public string? DefaultCode { get; init; }
        }

        /// <summary>The time-zone section of the settings page (ADR 0019) —
        /// the former <c>TimezoneController.TimezoneSettingsViewModel</c>,
        /// now a section of <see cref="LocaleSettingsViewModel"/> (one
        /// settings page, two sections). The public quick picker leaves it
        /// <c>null</c>.</summary>
        public sealed class TimezoneSettings
        {
            public List<(string Id, string DisplayName)> Zones { get; init; } = new();

            /// <summary>The resident's saved override, or <c>null</c> (no
            /// override — the platform default applies).</summary>
            public string? CurrentId { get; init; }

            /// <summary>The id pre-selected in the picker (the override if
            /// set, else the platform default).</summary>
            public string SelectedId { get; init; } = "UTC";

            /// <summary>The platform default (the "default" marker in the
            /// picker).</summary>
            public string DefaultId { get; init; } = "UTC";
        }

        /// <summary>The date-time format section of the settings page (ADR
        /// 0020) — the same shape as <see cref="TimezoneSettings"/>, but the
        /// picker offers the curated
        /// <see cref="Kumunita.Core.Localization.DateFormat.Presets"/> (a
        /// (label, format-string) list) plus a "Custom…" text box (the
        /// resident's power-user path). The public quick picker leaves it
        /// <c>null</c>.</summary>
        public sealed class DateFormatSettings
        {
            public List<(string Label, string Format)> Presets { get; init; } = new();

            /// <summary>The resident's saved override (a format string), or
            /// <c>null</c> (no override — the platform default applies).</summary>
            public string? CurrentFormat { get; init; }

            /// <summary>The format string pre-selected in the picker (the
            /// override if set, else the platform default).</summary>
            public string SelectedFormat { get; init; } = Kumunita.Core.Localization.DateFormat.FloorFormat;

            /// <summary>The platform default (the "default" marker in the
            /// picker).</summary>
            public string DefaultFormat { get; init; } = Kumunita.Core.Localization.DateFormat.FloorFormat;

            /// <summary>Whether <see cref="SelectedFormat"/> is one of the
            /// presets (true) or a custom format string (false) — the view uses
            /// this to reveal the "Custom…" text box instead of a lambda in the
            /// Razor markup.</summary>
            public bool CurrentIsPreset { get; init; } = true;
        }
    }

    public sealed record LocaleOption(string Code, string NativeName);
}
