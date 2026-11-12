using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
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
/// The gate (D6, C-M19·5): the controller does **not** own the write session
/// — it delegates the write to <see cref="IIdentityService.SetGuestAccessAsync"/>
/// (U01's single audited lane). The GlobalAdmin standing is enforced by
/// <c>[Authorize(Roles = Roles.GlobalAdmin)]</c> at the boundary (the Web owns
/// the standing check; the Core seam does not re-check <c>User</c> — the
/// ADR 0050 split).
/// </para>
/// </summary>
[Route("admin/guests")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminGuestsController(
    IIdentityService identity,
    // IS36 (D9) — the ADR 0015 D1 "kw-l floor" optional-ctor-param idiom:
    // optional (default null) so the existing test-construction site
    // (AdminGuestsControllerTests, `new AdminGuestsController(identity)`)
    // keeps compiling without a store; DI always supplies the live
    // IDocumentStore in the app. When null (a test harness that does not
    // supply it), the audit write in Index is skipped (a read is a no-op
    // without a store — the "no disclosure" shape); when supplied (the app,
    // and the new IS36 tests), exactly one aggregate AccessAudit row is
    // written (TargetKind "guests", Action "guests.view"). No new AccessVia,
    // no new IAuthorizationService method (the AccessAuditFactory "no new
    // seam" shape).
    IDocumentStore? store = null,
    // IMPROVE-UX A13 — the account-listing read for the guest form's picker.
    // The same profile-list seam the /admin add-picker uses
    // (AdminController.Communities' community panels seed their candidates
    // from GetProfilesAsync(false) — the ADR 0062 add-picker shape): the guest
    // form now offers the accounts the admin already sees on /admin/accounts
    // as a <select> instead of a free-text subject id. Optional (default null)
    // so the two pinned test-construction sites (AdminGuestsControllerTests
    // `new AdminGuestsController(identity)`, AdminReadAuditTests
    // `new AdminGuestsController(identity, store: store)`) keep compiling
    // without a profile seam; DI always supplies the live IProfileSettingsService
    // in the app. When null (a harness that does not supply it), the picker is
    // left empty (the C-M19·4 floor) — the write seam is unaffected.
    IProfileSettingsService? profiles = null) : Controller
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

        // IMPROVE-UX A13 — seed the guest account picker from the SAME
        // profile-list read the /admin add-picker uses (the ADR 0062 shape).
        // The picker offers the accounts an admin can already see on
        // /admin/accounts, so they *pick* the guest account instead of typing
        // an opaque subject id (the E19 "re-key" handoff). Reuses the
        // AdminIndexViewModel.AccountRow row type (no parallel projection).
        // Order the options by display name (then email) so the picker is
        // scannable, mirroring the /admin/accounts row sort. When the profile
        // seam is absent (a test harness) the picker stays empty (the C-M19·4
        // floor) — the Save contract is byte-for-byte unchanged.
        if (profiles is not null)
        {
            var candidates = await profiles.GetProfilesAsync(verifiedOnly: false);
            model.AccountOptions = candidates
                .OrderBy(p => p.DisplayName, StringComparer.Ordinal)
                .ThenBy(p => p.Email ?? string.Empty, StringComparer.Ordinal)
                .Select(p => new AdminIndexViewModel.AccountRow
                {
                    SubjectId   = p.SubjectId,
                    Email       = p.Email,
                    DisplayName = p.DisplayName,
                    Verified    = p.Verified,
                    Blocked     = p.Blocked
                })
                .ToList();
        }

        // IS36 (D9) — the guest standing list is a disclosure surface (class
        // (c) — the PII-bearing GuestAccess rows). A GlobalAdmin reading it
        // leaves exactly one aggregate AccessAudit row (the ADR 0108
        // "portability.export" precedent, adapted to a read view): TargetKind
        // "guests", Action "guests.view", VisibleCount = 1 when a subject was
        // supplied (one standing rendered), 0 for the create shape (no
        // subject), TargetId null, Via Admin, Outcome Allow. Guarded: when the
        // optional store is null the write is skipped (the "no disclosure"
        // shape). Counts only — no PII (the IS08 gate).
        if (store is not null)
        {
            await using (var auditSession = store.LightweightSession())
            {
                auditSession.Store(new AccessAudit
                {
                    Id           = Guid.NewGuid().ToString("N"),
                    At           = DateTimeOffset.UtcNow,
                    ActorId      = ActorId(User) ?? string.Empty,
                    Action       = "guests.view",
                    TargetKind   = "guests",
                    VisibleCount = subjectId is not null ? 1 : 0,
                    Via          = AccessVia.Admin,
                    Outcome      = AccessOutcome.Allow
                });
                await auditSession.SaveChangesAsync();
            }
        }

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
        // controller does not own the document write itself.
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

        TempData["info"] = await this.FlashAsync("admin.guests_saved", "Guest standing saved.");   // the U03 admin.guests_saved key's en value
        return RedirectToAction(nameof(Index), new { subjectId });
    }
}
