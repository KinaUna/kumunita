using Kumunita.Core.Identity;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/signup</c> surface (ADR 0050) — the GlobalAdmin's thin control
/// plane over whether self-service sign-up is <b>open</b> or <b>invitation-only</b>
/// (closed) on this instance. Mirrors <see cref="AdminTimezoneController"/>
/// (ADR 0019) exactly: <see cref="Roles.GlobalAdmin"/>-gated (the <c>AdminController</c>
/// precedent), a thin wrapper over the <b>one</b> matching
/// <see cref="IIdentityService"/> seam
/// (<see cref="IIdentityService.IsSignupOpenAsync"/> / <see
/// cref="IIdentityService.SetSignupOpenAsync"/>), and the audit row is the
/// **service's** (exactly one <c>AccessAudit</c>, <c>Via = Admin</c>, action
/// <c>signup.set-open</c>, <c>TargetKind</c> "signup" — the singleton-toggle
/// shape, the same as <c>timezone.set-default</c> / <c>dateformat.set-default</c>).
/// <para>
/// <b>Why a dedicated controller.</b> The <c>AdminController</c>'s constructor is
/// pinned by two Web-layer test harnesses
/// (<c>AdminControllerBlockTests</c> / <c>AdminControllerMandatoryTests</c>), so a
/// new dependency there would break them. A separate <c>/admin/signup</c> surface
/// (mirroring <c>/admin/timezone</c> / <c>/admin/dateformat</c>) is the
/// convention-consistent shape — the codebase already puts the platform-default
/// <i>language</i> / <i>timezone</i> / <i>date format</i> on their own controllers,
/// and the sign-up gate is their exact analog (one admin-settled instance value,
/// one audited write lane).
/// </para>
/// <para>
/// <b>The gate.</b> The README's deferred "Invitation-only sign-up" item is the
/// long-term default this surfaces: while the platform widens beyond the
/// development circle, sign-up is <c>true</c> (open) by default; an admin
/// tightens it to <c>false</c> (invitation-only) when the community is ready.
/// Existing residents are never touched by the flip — only new self-service
/// accounts are gated (SECURITY.md §6, adversary A2).
/// </para>
/// </summary>
[Route("admin/signup")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminSignupController(
    IIdentityService identity) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/signup</c> — the open / invitation-only toggle. Seeds the
    /// form with the current gate (<see cref="IIdentityService.IsSignupOpenAsync"/>,
    /// the <c>true</c> floor).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new SignupAdminViewModel
        {
            IsOpen = await identity.IsSignupOpenAsync(),
            NotifyAdmins = await identity.IsNotifyAdminsOnSignupAsync()
        };

        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/signup</c> — sets the gate. Delegates to
    /// <see cref="IIdentityService.SetSignupOpenAsync"/> (the single audited
    /// write lane — exactly one <c>AccessAudit</c> row, <c>Via = Admin</c>).
    /// Success → a surfaced <c>info</c> + redirect (the change is live on the
    /// very next <c>IsSignupOpenAsync</c> / render — data, not config).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(bool isOpen)
    {
        var actor = ActorId(User) ?? string.Empty;
        await identity.SetSignupOpenAsync(isOpen, actor);
        TempData["info"] = isOpen
            ? "Sign-up is now open — residents can create an account."
            : "Sign-up is now invitation-only — new self-service accounts are gated.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// <c>POST /admin/signup/notify</c> (ADR 0077) — sets the notify-admins gate
    /// (whether the account lane notifies GlobalAdmins on sign-up and
    /// verification). An independent toggle from the open / invitation-only gate
    /// (the two are distinct admin-settled instance values; each has its own
    /// audited write lane — the ADR 0019 / ADR 0020 singleton-toggle shape).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotify(bool notifyAdmins)
    {
        var actor = ActorId(User) ?? string.Empty;
        await identity.SetNotifyAdminsOnSignupAsync(notifyAdmins, actor);
        TempData["info"] = notifyAdmins
            ? "Admins will now be notified when a resident signs up or verifies their account."
            : "Admins will no longer be notified when a resident signs up or verifies their account.";
        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class SignupAdminViewModel
    {
        public bool IsOpen { get; init; } = true;

        /// <summary>ADR 0077 — the notify-admins gate (the <c>true</c> floor).</summary>
        public bool NotifyAdmins { get; init; } = true;
    }
}
