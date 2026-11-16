using Kumunita.Core.AdminOnboarding;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Reflection;
using System.Security.Claims;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M30 U06 (ADR 0153) — the <c>/admin/onboarding</c> surface pins
/// (M30·1 / M30·3 / M30·4 / M30·7): the GET seeds the view model with the
/// current <c>CompletedAt</c> state + the closed ten-step list (the original
/// seven M30 steps, extended additively per ADR 0153 D1 by the time-zone /
/// date-format / sign-up steps); the
/// POST <c>/admin/onboarding/complete</c> stamps completion through the
/// <b>one</b> <see cref="IAdminOnboardingService.CompleteAsync"/> lane
/// (exactly one <c>AccessAudit</c> row is the service's pin — the
/// <see cref="AdminOnboardingServiceTests"/> owns it, the controller is
/// the thin seam); and the surface is <c>GlobalAdmin</c>-gated
/// (the <c>[Authorize(Roles = GlobalAdmin)]</c> gate, M30·3, the M29·7
/// pin).
/// </summary>
/// <para>
/// <b>Test model:</b> direct-construction (NSubstitute
/// <c>IAdminOnboardingService</c> seam — the
/// <see cref="AdminSiteControllerTests"/> shape, admin-scope). The
/// audit-row assertion lives in
/// <see cref="Kumunita.Core.Tests.AdminOnboardingServiceTests"/> (the
/// service owns it); the controller is tested as the thin seam — the
/// call log is the assertion target.
/// </para>
public class AdminOnboardingControllerTests
{
    private const string Admin = "admin-onboarding-u06";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (AdminOnboardingController controller, IAdminOnboardingService svc) Build(
        DateTimeOffset? completedAt)
    {
        var svc = Substitute.For<IAdminOnboardingService>();
        svc.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(completedAt));

        var controller = new AdminOnboardingController(svc);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Kumunita.Core.Identity.Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, svc);
    }

    // ── 1 — the GET seeds the view model with the current CompletedAt state
    // + the closed seven-step list (M30·3, M30·7) ──────────────────────────
    // The Completed property is the GetAsync read's inverse
    // (CompletedAt != null) — the single read-seam the banner partial + the
    // view call, so they resolve to the same value (M30·5).

    [Fact(DisplayName = "U06 AdminOnboarding GET seeds the view model with the current CompletedAt state + the seven steps")]
    public async Task GET_SeesCurrentCompletedAt()
    {
        // (a) a not-yet-guided instance (CompletedAt = null, the floor) →
        // Completed = false (the banner shows, the walk-through is available).
        var stamp = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

        var (controllerNotCompleted, svcNotCompleted) = Build(completedAt: null);
        var viewNotCompleted = Assert.IsType<ViewResult>(await controllerNotCompleted.Index());
        var vmNotCompleted = Assert.IsType<AdminOnboardingViewModel>(viewNotCompleted.ViewData.Model);
        Assert.False(vmNotCompleted.Completed);
        Assert.Equal(10, vmNotCompleted.Steps.Count);
        await svcNotCompleted.Received(1).GetAsync(Arg.Any<CancellationToken>());

        // (b) a completed instance (CompletedAt = stamp) → Completed = true
        // (the banner clears, the walk-through is still reachable).
        var (controllerCompleted, svcCompleted) = Build(stamp);
        var viewCompleted = Assert.IsType<ViewResult>(await controllerCompleted.Index());
        var vmCompleted = Assert.IsType<AdminOnboardingViewModel>(viewCompleted.ViewData.Model);
        Assert.True(vmCompleted.Completed);
        Assert.Equal(10, vmCompleted.Steps.Count);
        await svcCompleted.Received(1).GetAsync(Arg.Any<CancellationToken>());

        // The closed ten-step set (M30·7, extended additively per ADR 0153 D1)
        // — in the walk order, each linking into the existing admin surface
        // by route.
        Assert.Equal("/admin/communities", vmCompleted.Steps[0].Route);                 // community name & description (the base name/description surface; was /admin/languages, which only owns the language catalog + UI-string translations)
        Assert.Equal("/admin/languages", vmCompleted.Steps[1].Route);                  // languages
        Assert.Equal("/admin/timezone", vmCompleted.Steps[2].Route);                   // time zone (ADR 0019, additive)
        Assert.Equal("/admin/dateformat", vmCompleted.Steps[3].Route);                 // date & time format (ADR 0020, additive)
        Assert.Equal("/admin/announcements/comments", vmCompleted.Steps[4].Route);     // moderation
        Assert.Equal("/admin/quiet", vmCompleted.Steps[5].Route);                      // notifications (quiet-time cadence)
        Assert.Equal("/admin/signup", vmCompleted.Steps[6].Route);                     // sign-up (ADR 0050, additive)
        Assert.Equal("/admin/storage/settings", vmCompleted.Steps[7].Route);           // storage limits
        Assert.Equal("/admin/site", vmCompleted.Steps[8].Route);                       // site content
        Assert.Equal("/admin/escalation", vmCompleted.Steps[9].Route);                 // issue escalation (ADR 0159 ESC outbound-config surface)
    }

    // ── M32·11 — the M30 onboarding escalation step route (the §2.4 pin) ──
    // M32 "issue submission & escalation" shipped (ADR 0155); U06 re-pointed
    // the M30 escalation step route from the
    // `/admin/announcements` placeholder to the M32 `/admin/error-reports`
    // surface, then to the ADR 0159 `/admin/escalation` ESC outbound-config
    // surface (the receiving endpoint + token + enabled switch — the surface a
    // new admin actually configures; `/admin/error-reports` is the *inbound*
    // read-only triage list). The escalation step's `Key` / `LabelKey` /
    // `DescriptionKey` are **reused, not re-authored** (the
    // `adminonboarding.step_escalation` keys); the step is now index 9 of the
    // ten-step set (re-ordered after the additive time-zone / date-format /
    // sign-up steps were added per ADR 0153 D1).

    [Fact(DisplayName = "M32·11 AdminOnboarding escalation step route points to /admin/escalation (the ADR 0159 ESC surface)")]
    public async Task M32_11_AdminOnboarding_Escalation_Step_Route_Points_To_Escalation()
    {
        var (controller, _) = Build(completedAt: null);
        var view = Assert.IsType<ViewResult>(await controller.Index());
        var vm = Assert.IsType<AdminOnboardingViewModel>(view.ViewData.Model);

        // The ten-step set (M30·7 closed set, extended additively per ADR 0153 D1).
        Assert.Equal(10, vm.Steps.Count);

        // Step 10 (index 9) — issue escalation — the ADR 0159 `/admin/escalation`
        // ESC outbound-config surface (first re-pointed by M32·11 to
        // `/admin/error-reports`, then to the ADR 0159 surface that a new admin
        // actually configures).
        Assert.Equal("/admin/escalation", vm.Steps[9].Route);

        // The escalation step's `Key` / `LabelKey` / `DescriptionKey` are reused,
        // not re-authored (the `adminonboarding.step_escalation` keys are the
        // closed M30 set).
        Assert.Equal("escalation", vm.Steps[9].Key);
        Assert.Equal("adminonboarding.step_escalation", vm.Steps[9].LabelKey);
        Assert.Equal("adminonboarding.desc_escalation", vm.Steps[9].DescriptionKey);
    }

    // ── 2 — the POST /admin/onboarding/complete stamps completion through the
    // one CompleteAsync lane (M30·4) + redirects back to the walk-through ───
    // The audit-row assertion lives in the AdminOnboardingServiceTests
    // (the service owns it) — the controller is the thin seam.

    [Fact(DisplayName = "U06 AdminOnboarding POST /complete stamps completion (one CompleteAsync call) and redirects")]
    public async Task POST_Complete_StampsCompletedAt_WritesOneAccessAuditRow()
    {
        var (controller, svc) = Build(completedAt: null);
        svc.CompleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var action = await controller.Complete();
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminOnboardingController.Index), redirect.ActionName);

        // The one write lane was called exactly once for the signed-in
        // GlobalAdmin (M30·4 — the single audited write-lane shape). The
        // audit-row assertion (exactly one AccessAudit row, Via = Admin,
        // action admin_onboarding.complete, TargetKind "admin-onboarding")
        // lives in the AdminOnboardingServiceTests (the service owns it).
        await svc.Received(1).CompleteAsync(Admin, Arg.Any<CancellationToken>());

        // The flash is the adminonboarding.flash_done key (the U05 closed
        // set, M30·6) — resolved to the KnownTranslationKeys.EnValues floor
        // in this test-construction site (the translation seams are null).
        Assert.Equal(
            Kumunita.Core.Localization.KnownTranslationKeys.EnValues["adminonboarding.flash_done"],
            controller.TempData["info"]);
    }

    // ── 3 — a non-GlobalAdmin is denied (M30·3, the M29·7 pin) ─────────────
    // The [Authorize(Roles = GlobalAdmin)] attribute on the controller is
    // the gate. In a direct-construction harness (no TestServer), the
    // attribute is asserted via reflection — the attribute is present and
    // names the GlobalAdmin role, which is the ASP.NET Core authorization
    // policy that denies a non-GlobalAdmin principal at the middleware layer.

    [Fact(DisplayName = "U06 AdminOnboarding is GlobalAdmin-gated (the [Authorize(Roles = GlobalAdmin)] pin) — GET denied for a non-GlobalAdmin")]
    public void GET_NonGlobalAdmin_IsDenied()
    {
        var attr = typeof(AdminOnboardingController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.NotNull(attr.Roles);
        Assert.Contains(Kumunita.Core.Identity.Roles.GlobalAdmin, attr.Roles.Split(','));
    }

    // ── 4 — a non-GlobalAdmin is denied (M30·3, the M29·7 pin) ─────────────
    // Same gate, the POST action: the class-level [Authorize(Roles =
    // GlobalAdmin)] covers both the GET and the POST — a non-GlobalAdmin
    // never reaches CompleteAsync (the M29·7 pin, admin-scope).

    [Fact(DisplayName = "U06 AdminOnboarding is GlobalAdmin-gated (the [Authorize(Roles = GlobalAdmin)] pin) — POST denied for a non-GlobalAdmin")]
    public void POST_NonGlobalAdmin_IsDenied()
    {
        var attr = typeof(AdminOnboardingController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.NotNull(attr.Roles);
        Assert.Contains(Kumunita.Core.Identity.Roles.GlobalAdmin, attr.Roles.Split(','));

        // The POST action itself carries no weaker gate of its own (no
        // per-action [AllowAnonymous] / [Authorize] that would widen the
        // class-level gate).
        var complete = typeof(AdminOnboardingController).GetMethod(nameof(AdminOnboardingController.Complete));
        Assert.NotNull(complete);
        Assert.Empty(
            complete!.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
    }
}
