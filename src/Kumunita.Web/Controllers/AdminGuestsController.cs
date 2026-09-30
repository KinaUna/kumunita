using Kumunita.Core.Identity;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/admin/guests</c> surface (ADR 0120, D6) — the GlobalAdmin's
/// control plane over the guest standing: settle the bounded window (D3) +
/// the closed surface set (D4) for a guest account. Mirrors
/// <see cref="AdminSignupController"/> (ADR 0050) exactly:
/// <see cref="Roles.GlobalAdmin"/>-gated, a thin wrapper over the <b>one</b>
/// matching <see cref="IIdentityService"/> seam (the
/// <see cref="IIdentityService.SetGuestAccessAsync"/> /
/// <see cref="IIdentityService.GetGuestAccessAsync"/> pair), and the audit
/// row is the **service's** (exactly one <c>AccessAudit</c>, <c>Via = Admin</c>,
/// action <c>guest.set-standing</c> — the C-M19·5 single + audited write
/// lane). A dedicated controller: the <c>AdminController</c> constructor is
/// pinned by two Web-layer test harnesses, so a new dependency there would
/// break them (the ADR 0050 dedicated-controller rationale).
/// <para>
/// The gate (D6, C-M19·5): the controller does **not** <c>IDocumentSession</c>
/// — it delegates the write to <see cref="IIdentityService.SetGuestAccessAsync"/>
/// (U01's single audited lane). The GlobalAdmin standing is enforced by
/// <c>[Authorize(Roles = Roles.GlobalAdmin)]</c> at the boundary (the Web owns
/// the standing check; the Core seam does not re-check <c>User</c> — the
/// ADR 0050 split).
/// </para>
/// </summary>
[Route("admin/guests")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminGuestsController(IIdentityService identity) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /admin/guests</c> — the guest standing control plane. Seeds the
    /// form with the current standing (<see cref="IIdentityService.GetGuestAccessAsync"/>
    /// — the C-M19·4 empty floor: a guest with no settled standing renders the
    /// empty set, not an error). With no <c>subjectId</c>, an empty form model
    /// is returned (the create shape).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? subjectId)
    {
        var model = new GuestAdminViewModel { SubjectId = subjectId ?? string.Empty };
        if (subjectId is not null)
            model.Seed(await identity.GetGuestAccessAsync(subjectId));
        return View(model);
    }

    /// <summary>
    /// <c>POST /admin/guests</c> — settles the standing. Delegates to
    /// <see cref="IIdentityService.SetGuestAccessAsync"/> (the single audited
    /// write lane — exactly one <c>AccessAudit</c> row, <c>Via = Admin</c>,
    /// C-M19·5). Success → a surfaced <c>TempData["info"]</c>
    /// (<c>admin.guests_saved</c>, the U03 key) + redirect (the change is
    /// live on the very next <c>GetGuestAccessAsync</c> / render — data, not
    /// config, D3).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        string subjectId,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        bool announcements,
        bool events,
        bool directory)
    {
        // Validate the form (C-M19·3 forbids an unbounded standing — the
        // window must be non-empty and ordered): an inverted or empty window
        // is rejected at the boundary. The design doc §7 (the authoritative
        // controller spec) pins `return BadRequest()` for this case.
        if (validUntil <= validFrom)
        {
            return BadRequest();
        }

        // The closed surface set (D4) — compose the [Flags] value from the
        // checkboxes; unchecked = absent (the empty floor is the default,
        // C-M19·4 — a guest with no surfaces is a signed-in shell, not an error).
        var surfaces = GuestSurface.None;
        if (announcements) surfaces |= GuestSurface.Announcements;
        if (events)        surfaces |= GuestSurface.Events;
        if (directory)     surfaces |= GuestSurface.Directory;

        // Delegate to the SINGLE audited write lane (C-M19·5) — the
        // controller does NOT IDocumentSession the document itself.
        var actor = ActorId(User) ?? string.Empty;
        await identity.SetGuestAccessAsync(
            new GuestAccess
            {
                SubjectId = subjectId,
                ValidFrom = validFrom,
                ValidUntil = validUntil,
                AllowedSurfaces = surfaces,
            },
            actor);

        TempData["info"] = "Guest standing saved.";   // the U03 admin.guests_saved key's en value
        return RedirectToAction(nameof(Index), new { subjectId });
    }
}
