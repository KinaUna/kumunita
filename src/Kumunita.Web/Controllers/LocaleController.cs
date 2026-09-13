using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident's settings page (ADR 0005 B; M·5, M7 FACES) — one page, two
/// sections: **Language** and **Time zone** (ADR 0019's resident surface,
/// folded into this page 2026-09-13; the standalone <c>/settings/timezone</c>
/// page + nav link were removed in the same change).
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
    IDocumentStore store) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

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
        var catalog = await localization.ListLanguagesAsync();

        // The time-zone section's actor + current override (null-safe: an
        // unauthenticated request has no subject and no profile read).
        string? subject = SubjectId(User);
        string? currentTz = subject is null
            ? null
            : (await userInfo.GetProfileAsync(subject))?.TimeZone;

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

            // The time-zone section (ADR 0019): the OS zones (the shared
            // EffectiveTimezoneResolver.Zones source), the actor's saved
            // override (or null), the pre-selected id (override if set, else
            // the platform default), and the platform default for the marker.
            Timezone = BuildTimezoneSection(subject, currentTz,
                defaultTz: await localization.GetDefaultTimezoneAsync()),
        };

        return View(model);
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
            return RedirectToAction(nameof(Index));

        try
        {
            if (clear == "1")
            {
                // null ⇒ clear (the lane stores + saves, so the clear persists
                // — the next request resolves to the instance default).
                await userInfo.SetProfileTimezoneAsync(subject, null, subject);
                TempData["info"] = "Time zone reset — the platform default will be used.";
            }
            else if (!string.IsNullOrWhiteSpace(timezone))
            {
                await userInfo.SetProfileTimezoneAsync(subject, timezone, subject);
                TempData["info"] = $"Time zone set to \"{timezone}\" — it takes effect on the next request.";
            }
        }
        catch (KeyNotFoundException)
        {
            // A pre-bootstrap edge (no profile row). Fail closed + surface the
            // error; the lane never load-or-creates (the SetProfileAvatarAsync
            // pin), so nothing is half-written.
            TempData["error"] = "Your profile is not available — sign out and back in.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class LocaleSettingsViewModel
    {
        public List<LocaleOption> Languages { get; init; } = new();
        public string? CurrentCode { get; init; }
        public string DefaultCode { get; init; } = "en";

        /// <summary>The time-zone section (ADR 0019), or <c>null</c> when the
        /// caller has no subject (a signed-out visitor — the <c>/language</c>
        /// quick picker renders the language section only).</summary>
        public TimezoneSettings? Timezone { get; init; }

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
    }

    public sealed record LocaleOption(string Code, string NativeName);
}
