using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The resident's time-zone-preference settings page (ADR 0019) — the user
/// half of the timezone feature. Mirrors <see cref="LocaleController"/>
/// exactly (the ADR 0005 B "instance default + user override" shape): the
/// <b>platform default</b> is the <c>LocaleSettings.DefaultTimezone</c>
/// singleton (an admin writes it, the <c>UTC</c> floor, read through
/// <see cref="ILocalizationService.GetDefaultTimezoneAsync"/>), and this
/// page is where the resident sets / clears their <b>personal override</b> —
/// a persisted <see cref="Profile.TimeZone"/> field written through the
/// <see cref="IUserInfoService.SetProfileTimezoneAsync"/> lane (the
/// <c>SetProfileAvatarAsync</c> single-write-lane shape — owner-scope, no
/// audit row; the self-scope check is the Web boundary, the <c>[Authorize]</c>
/// gate plus the actor being the subject).
/// <para>
/// <b>Resolution order (the feature's contract):</b> signed-in actor's
/// <c>Profile.TimeZone</c> → the instance default → <c>UTC</c> (the
/// <c>EffectiveTimezoneResolver</c> enforces this for the <c>kw-dt</c>
/// TagHelper's render path; this page is the *write* half — it only sets the
/// override, it never changes the default). The change is live on the very
/// next request (M·4: data, not config — no rebuild, no restart).
/// </para>
/// <para>
/// <b>Thin token.</b> The actor is read per action from the
/// <c>Kumunita.Sub</c> claim (<see cref="KumunitaPrincipal.SubjectId"/>,
/// **not** the BCL <c>Identity.Name</c>) and passed to the lane as
/// <c>actorBy</c> — the same shape as the locale settings page's save.
/// </para>
/// </summary>
[Authorize]
public sealed class TimezoneController(
    IUserInfoService userInfo,
    ILocalizationService localization) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /settings/timezone</c> — the resident's time-zone preference
    /// page. Seeds the picker with the OS zones (the shared
    /// <see cref="EffectiveTimezoneResolver.Zones"/> source), the current
    /// override (the saved <c>Profile.TimeZone</c>, or the instance default
    /// when none is set — the "preference if present" shape), and the instance
    /// default for the "default" marker.
    /// </summary>
    [HttpGet("/settings/timezone")]
    public async Task<IActionResult> Index()
    {
        var subject = SubjectId(User);
        string? current = subject is null
            ? null
            : (await userInfo.GetProfileAsync(subject))?.TimeZone;

        string defaultTz = await localization.GetDefaultTimezoneAsync();

        // Seed the picker: the actor's override if set, else the platform
        // default (so the "current" marker is always a concrete, selectable id
        // — the locale settings page's same "default marker" shape).
        string selected = string.IsNullOrWhiteSpace(current) ? defaultTz : current!;

        var model = new TimezoneSettingsViewModel
        {
            Zones = EffectiveTimezoneResolver.Zones(),
            CurrentId = current,
            SelectedId = selected,
            DefaultId = defaultTz,
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /settings/timezone</c> — the save. With a <c>timezone</c> form
    /// value: <see cref="IUserInfoService.SetProfileTimezoneAsync"/> (sets the
    /// override; the self-scope check is this page's <c>[Authorize]</c> gate +
    /// the actor being the subject). With <c>clear=1</c>: the same lane with a
    /// <c>null</c> value (the "reset to platform default" action — the next
    /// request resolves to the instance default). The change is live on the
    /// very next request (M·4: data, not config). A missing profile (an
    /// edge-case pre-bootstrap account) fails closed: the lane throws
    /// <c>KeyNotFoundException</c> and we surface the error + redirect (the
    /// <c>SetProfileAvatarAsync</c> pin).
    /// </summary>
    [HttpPost("/settings/timezone")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? timezone, string? clear)
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

    public sealed class TimezoneSettingsViewModel
    {
        public List<(string Id, string DisplayName)> Zones { get; init; } = new();

        /// <summary>The resident's saved override, or <c>null</c> (no override
        /// — the platform default applies).</summary>
        public string? CurrentId { get; init; }

        /// <summary>The id pre-selected in the picker (the override if set,
        /// else the platform default).</summary>
        public string SelectedId { get; init; } = "UTC";

        /// <summary>The platform default (the "default" marker in the
        /// picker).</summary>
        public string DefaultId { get; init; } = "UTC";
    }
}
