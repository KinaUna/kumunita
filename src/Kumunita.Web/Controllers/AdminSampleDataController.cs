using Kumunita.Core.Identity;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/sample</c> surface (ADR 0138) — the GlobalAdmin's thin control
/// plane over whether this <b>demo (sample-data) instance</b> locks its sample
/// accounts out of changing their own password (except the sample
/// <c>GlobalAdmin</c>). Mirrors <see cref="AdminSignupController"/> (ADR 0050)
/// exactly: <see cref="Roles.GlobalAdmin"/>-gated (the <c>AdminController</c>
/// precedent), a thin wrapper over the <b>two</b> matching
/// <see cref="IIdentityService"/> seams (<see
/// cref="IIdentityService.IsSamplePasswordChangeLockedAsync"/> / <see
/// cref="IIdentityService.SetSamplePasswordChangeLockedAsync"/>), and the audit
/// row is the **service's** (exactly one <c>AccessAudit</c>, <c>Via = Admin</c>,
/// action <c>sample.set-password-lock</c>, <c>TargetKind</c> "sample" — the
/// singleton-toggle shape, the same as <c>signup.set-open</c> /
/// <c>timezone.set-default</c>).
/// <para>
/// <b>Shown only when sample data is enabled.</b> The surface is gated on
/// <see cref="IIdentityService.IsSampleDataEnabledAsync"/> (the
/// <c>SampleData__Enabled</c> flag): a real deployment never carries the flag,
/// so both the <c>GET</c> and the <c>POST</c> return <b>404</b> there — the
/// surface is unreachable by construction (the ADR 0056 "unreachable by
/// construction" shape). The link to it on <c>/admin/platform</c> is likewise
/// only rendered when the flag is set.
/// </para>
/// <para>
/// <b>Why a dedicated controller.</b> The <c>AdminController</c>'s constructor
/// is pinned by two Web-layer test harnesses
/// (<c>AdminControllerBlockTests</c> / <c>AdminControllerMandatoryTests</c>), so
/// a new dependency there would break them. A separate <c>/admin/sample</c>
/// surface (mirroring <c>/admin/signup</c> / <c>/admin/timezone</c>) is the
/// convention-consistent shape — the codebase already puts the sign-up gate on
/// its own controller, and the sample-data lock is its exact analog (one
/// admin-settled instance value, one audited write lane, gated on a
/// config-flag existence).
/// </para>
/// </summary>
[Route("admin/sample")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminSampleDataController(
    IIdentityService identity) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/sample</c> — the sample-account change-password lock
    /// toggle. Returns <b>404</b> when the <c>SampleData__Enabled</c> flag is
    /// not set (a real instance — the surface is unreachable by construction,
    /// the ADR 0056 shape). Otherwise seeds the form with the current lock
    /// (<see cref="IIdentityService.IsSamplePasswordChangeLockedAsync"/>, the
    /// <c>false</c> floor).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // ADR 0138 — the existence gate: a real deployment never carries the
        // SampleData__Enabled flag, so the surface 404s there (unreachable by
        // construction, the ADR 0056 shape) rather than rendering a control
        // that would be a no-op (there are no sample accounts to lock).
        if (!await identity.IsSampleDataEnabledAsync())
            return NotFound();

        return View(new SampleAdminViewModel
        {
            PasswordChangeLocked = await identity.IsSamplePasswordChangeLockedAsync()
        });
    }

    /// <summary>
    /// <c>POST /admin/sample</c> — sets the lock. Returns <b>404</b> when the
    /// <c>SampleData__Enabled</c> flag is not set (the gate is authoritative
    /// on the write path too, not just the hidden link — a crafted POST to a
    /// real instance is refused, the ADR 0050 gate shape). Otherwise delegates
    /// to <see cref="IIdentityService.SetSamplePasswordChangeLockedAsync"/>
    /// (the single audited write lane — exactly one <c>AccessAudit</c> row,
    /// <c>Via = Admin</c>). Success → a surfaced <c>info</c> + redirect (the
    /// change is live on the very next <c>IsChangePasswordLockedForAsync</c> /
    /// render — data, not config).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(bool passwordChangeLocked)
    {
        // ADR 0138 — the existence gate, authoritative on the write path too.
        if (!await identity.IsSampleDataEnabledAsync())
            return NotFound();

        var actor = ActorId(User) ?? string.Empty;
        await identity.SetSamplePasswordChangeLockedAsync(passwordChangeLocked, actor);
        TempData["info"] = passwordChangeLocked
            ? "Sample accounts are now locked out of changing their own password (the demo admin can still change theirs). Visitors can test features without breaking the shared credentials."
            : "Sample accounts can change their own password again.";
        return RedirectToAction(nameof(Index));
    }
}
