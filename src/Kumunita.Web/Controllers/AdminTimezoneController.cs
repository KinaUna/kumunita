using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Web.Localization;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/timezone</c> surface (ADR 0019) — the GlobalAdmin's thin
/// control plane over the <b>platform default</b> time zone. Mirrors
/// <see cref="LanguagesController"/> / its <c>SetDefault</c> action exactly:
/// <see cref="Roles.GlobalAdmin"/>-gated (the <c>AdminController</c> precedent),
/// a thin wrapper over the <b>one</b> matching
/// <see cref="ILocalizationService"/> method (<see
/// cref="ILocalizationService.SetDefaultTimezoneAsync"/>), and the audit row
/// is the **service's** (exactly one <c>AccessAudit</c>, <c>Via = Admin</c>,
/// action <c>timezone.set-default</c>, <c>TargetKind</c> "timezone",
/// <c>TargetId</c> = the id — the <c>language.set-default</c> pin).
/// <para>
/// <b>Why a dedicated controller.</b> The <c>AdminController</c>'s constructor
/// is pinned by two Web-layer test harnesses
/// (<c>AdminControllerBlockTests</c> / <c>AdminControllerMandatoryTests</c>), so
/// a new dependency there would break them. A separate
/// <c>/admin/timezone</c> surface (mirroring <c>/admin/languages</c>) is the
/// convention-consistent shape — the codebase already puts the platform
/// default *language* on its own <c>/admin/languages</c> controller, and the
/// ADR 0019 "platform default" surface is the timezone's exact analog.
/// </para>
/// <para>
/// <b>Fail-closed.</b> A blank or unknown IANA id throws
/// <c>InvalidOperationException</c> in the service <b>before</b> any write
/// (no audit row for the blocked attempt — the <c>RemoveLanguageAsync</c> /
/// M·7 pin); the controller maps it to <b>409</b> (the optimistic-concurrency
/// story, the <c>Remove</c> precedent) + a surfaced message.
/// </para>
/// </summary>
[Route("admin/timezone")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminTimezoneController(
    ILocalizationService localization) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/timezone</c> — the platform-default picker. Seeds the
    /// picker with the OS zones (the shared
    /// <see cref="EffectiveTimezoneResolver.Zones"/> source), the current
    /// default (<see cref="ILocalizationService.GetDefaultTimezoneAsync"/>,
    /// the <c>UTC</c> floor), and the current default as the pre-selected id.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        string current = await localization.GetDefaultTimezoneAsync();

        var model = new TimezoneAdminViewModel
        {
            Zones = EffectiveTimezoneResolver.Zones(),
            CurrentId = current,
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/timezone</c> — sets the platform default. Delegates to
    /// <see cref="ILocalizationService.SetDefaultTimezoneAsync"/> (the single
    /// audited write lane). A blank/unknown id → the service's
    /// <c>InvalidOperationException</c> (fail-closed, **no audit row**) →
    /// mapped to **409** + a surfaced error (the <c>Remove</c> precedent).
    /// Success → a surfaced <c>info</c> + redirect (the change is live on the
    /// very next <c>GetDefaultTimezoneAsync</c> / render, M·4: data, not
    /// config).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string timezone)
    {
        var actor = ActorId(User) ?? string.Empty;
        try
        {
            await localization.SetDefaultTimezoneAsync(timezone, actor);
            TempData["info"] = $"Platform default time zone set to “{timezone}”.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            // M·7 / fail-closed: the service rejected the id (blank or unknown)
            // before any write — no audit row was committed for the blocked
            // attempt. Surface it as 409 (the optimistic-concurrency story) + a
            // message the admin can act on.
            TempData["error"] = ex.Message;
            return StatusCode(409);
        }
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class TimezoneAdminViewModel
    {
        public List<(string Id, string DisplayName)> Zones { get; init; } = new();
        public string CurrentId { get; init; } = "UTC";
    }
}
