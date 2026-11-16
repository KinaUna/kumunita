using Kumunita.Core;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
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
/// M31 U06 (ADR 0154) — the <c>/admin/error-reports</c> GlobalAdmin triage
/// surface pins (M31·4 / M31·6 / M31·9):
/// <list type="number">
/// <item><b>M31_4_Admin_List_SignedIn_GlobalAdmin_Sees_Reports</b> — a
/// signed-in <c>GlobalAdmin</c> visiting <c>GET /admin/error-reports</c> sees
/// all <c>ErrorReport</c> rows, newest first (the
/// <see cref="IErrorReportService.ListAsync"/> read, M31·4 — a read, not an
/// access decision; no per-row <c>IAuthorizationService</c> call, no
/// <c>AccessAudit</c> row) (FACES M31-3).</item>
/// <item><b>M31_4_Admin_List_NonGlobalAdmin_Denied</b> — a non-
/// <c>GlobalAdmin</c> (including a signed-in resident) gets a 403: the
/// <c>[Authorize(Roles = GlobalAdmin)]</c> gate is the standard admin gate
/// (the thin-token rule ADR 0001-B, M31·9 — asserted via reflection, the
/// house "string pin, no TestServer" idiom) (FACES M31-6).</item>
/// <item><b>M31_6_Admin_MarkTriaged_GlobalAdmin_Updates_Row</b> — a
/// <c>GlobalAdmin</c> marking a <c>new</c> report triaged calls the one
/// <see cref="IErrorReportService.MarkTriagedAsync"/> lane, sets the
/// <c>errorreport.list.flash_triaged</c> flash, and redirects to the list
/// (M31·6). The audit-row assertion (exactly one <c>AccessAudit</c> row,
/// <c>Via = Admin</c>) lives in the
/// <see cref="Kumunita.Core.Tests.ErrorReportServiceTests"/> (the service
/// owns it) (FACES M31-4).</item>
/// <item><b>M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp</b> — marking an
/// already-<c>triaged</c> report is a no-op: the lane returns
/// <c>null</c>, the controller sets NO flash, redirects to the list, and
/// does not write a second audit row (the M31·6 idempotency pin — the
/// "no second audit row" half lives in the
/// <see cref="Kumunita.Core.Tests.ErrorReportServiceTests"/>) (FACES M31-5).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Test model (the house idiom):</b> direct-construction
/// (<see cref="ErrorReportAdminController"/> with an NSubstitute
/// <see cref="IErrorReportService"/> seam, a <see cref="DefaultHttpContext"/>,
/// no TestServer — the <see cref="AdminOnboardingControllerTests"/> shape,
/// admin-scope). The <c>[Authorize(Roles = GlobalAdmin)]</c> gate is the
/// standard admin surface (the <see cref="AdminOnboardingController"/> shape,
/// the thin-token rule ADR 0001-B); in a direct-construction harness the
/// attribute is asserted via reflection (the
/// <see cref="AdminOnboardingControllerTests.GET_NonGlobalAdmin_IsDenied"/>
/// idiom). The audit-row assertions live in the Core
/// <see cref="Kumunita.Core.Tests.ErrorReportServiceTests"/> (the service
/// owns them) — the controller is the thin seam.
/// </para>
public class AdminErrorReportTests
{
    private const string Admin = "admin-m31-u06";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (ErrorReportAdminController controller, IErrorReportService svc) Build(
        ClaimsPrincipal user, IReadOnlyList<ErrorReport> reports, ErrorReport? triageResult)
    {
        var svc = Substitute.For<IErrorReportService>();
        svc.ListAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ErrorReport>>(reports));
        svc.MarkTriagedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(triageResult));

        var controller = new ErrorReportAdminController(svc);

        var httpContext = new DefaultHttpContext();
        httpContext.User = user;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, svc);
    }

    private static ClaimsPrincipal GlobalAdminPrincipal() =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Kumunita.Core.Identity.Roles.GlobalAdmin),
            },
            authenticationType: "test"));

    private static ClaimsPrincipal ResidentPrincipal(string subject) =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subject),
                new Claim(Kumunita.Core.Identity.ClaimTypes.Role, "Resident"),
            },
            authenticationType: "test"));

    // ── 13 — a signed-in GlobalAdmin sees the report list (M31·4) ──────────
    // FACES M31-3: GET /admin/error-reports returns the ListAsync read
    // (newest first, the view model's Reports collection) — a read, not an
    // access decision (no per-row IAuthorizationService call, no audit row,
    // M31·4).

    [Fact(DisplayName = "M31_4_Admin_List_SignedIn_GlobalAdmin_Sees_Reports")]
    public async Task M31_4_Admin_List_SignedIn_GlobalAdmin_Sees_Reports()
    {
        var newest = new ErrorReport { Id = "er-newest", Description = "newest", Created = DateTimeOffset.UtcNow, TriageStatus = "new" };
        var oldest = new ErrorReport { Id = "er-oldest", Description = "oldest", Created = DateTimeOffset.UtcNow.AddHours(-1), TriageStatus = "triaged" };
        var reports = new[] { newest, oldest }; // newest first (the ListAsync ordering)

        var (controller, svc) = Build(GlobalAdminPrincipal(), reports, triageResult: null);
        var view = Assert.IsType<ViewResult>(await controller.Index());
        var vm = Assert.IsType<AdminErrorReportViewModel>(view.ViewData.Model);

        // The list is the ListAsync read, newest first (M31·4 — a read).
        Assert.Equal(2, vm.Reports.Count);
        Assert.Equal("er-newest", vm.Reports[0].Id);
        Assert.Equal("er-oldest", vm.Reports[1].Id);

        // The read seam was called exactly once (the single ListAsync read);
        // no write lane was touched (a read, not an access decision — M31·4).
        await svc.Received(1).ListAsync(100, Arg.Any<CancellationToken>());
        await svc.DidNotReceive().MarkTriagedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── 14 — a non-GlobalAdmin is denied (M31·4 / M31·9) ───────────────────
    // FACES M31-6: the [Authorize(Roles = GlobalAdmin)] gate is the standard
    // admin gate (the thin-token rule ADR 0001-B). In a direct-construction
    // harness (no TestServer), the attribute is asserted via reflection —
    // the AdminOnboardingControllerTests idiom. The class-level gate covers
    // both the GET and the POST (no per-action [AllowAnonymous] that would
    // widen it).

    [Fact(DisplayName = "M31_4_Admin_List_NonGlobalAdmin_Denied")]
    public void M31_4_Admin_List_NonGlobalAdmin_Denied()
    {
        var attr = typeof(ErrorReportAdminController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.NotNull(attr.Roles);
        Assert.Contains(Kumunita.Core.Identity.Roles.GlobalAdmin, attr.Roles.Split(','));

        // The GET action carries no weaker gate of its own (no
        // [AllowAnonymous] that would widen the class-level gate).
        var index = typeof(ErrorReportAdminController).GetMethod(nameof(ErrorReportAdminController.Index));
        Assert.NotNull(index);
        Assert.Empty(index!.GetCustomAttributes<AllowAnonymousAttribute>());

        // The POST action carries no weaker gate of its own either — a
        // non-GlobalAdmin never reaches MarkTriagedAsync (M31·9).
        var mark = typeof(ErrorReportAdminController).GetMethod(nameof(ErrorReportAdminController.MarkTriaged));
        Assert.NotNull(mark);
        Assert.Empty(mark!.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    // ── 15 — a GlobalAdmin marks a new report triaged (M31·6) ──────────────
    // FACES M31-4: the effective triage calls the one MarkTriagedAsync lane
    // (the audit-row assertion — exactly one AccessAudit row, Via = Admin,
    // action errorreport.triage — lives in the ErrorReportServiceTests),
    // sets the errorreport.list.flash_triaged flash (the U05 closed set,
    // M31·7), and redirects to the list (the AdminOnboardingController
    // .Complete shape — the [ValidateAntiForgeryToken] POST + the
    // TempData["info"] flash + the RedirectToAction(nameof(Index)) redirect).

    [Fact(DisplayName = "M31_6_Admin_MarkTriaged_GlobalAdmin_Updates_Row")]
    public async Task M31_6_Admin_MarkTriaged_GlobalAdmin_Updates_Row()
    {
        const string reportId = "er-m31-15";
        var triageResult = new ErrorReport { Id = reportId, TriageStatus = "triaged" };
        var (controller, svc) = Build(GlobalAdminPrincipal(), reports: [], triageResult);

        var action = await controller.MarkTriaged(reportId);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(ErrorReportAdminController.Index), redirect.ActionName);

        // The one write lane was called exactly once for the signed-in
        // GlobalAdmin (M31·6 — the single audited write-lane shape). The
        // audit-row assertion lives in the ErrorReportServiceTests (the
        // service owns it).
        await svc.Received(1).MarkTriagedAsync(reportId, Admin, Arg.Any<CancellationToken>());

        // The flash is the errorreport.list.flash_triaged key (the U05
        // closed set, M31·7) — resolved to the KnownTranslationKeys.EnValues
        // floor in this test-construction site (the translation seams are
        // null — the AdminOnboardingControllerTests idiom).
        Assert.Equal(
            KnownTranslationKeys.EnValues["errorreport.list.flash_triaged"],
            controller.TempData["info"]);
    }

    // ── 16 — marking an already-triaged report is a no-op (M31·6) ──────────
    // FACES M31-5: the lane returns null (an already-triaged report, the
    // M31·6 idempotency pin) → the controller sets NO flash (no second audit
    // row — the "no second audit row" half lives in the
    // ErrorReportServiceTests) and redirects to the list (the same redirect
    // either way).

    [Fact(DisplayName = "M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp")]
    public async Task M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp()
    {
        const string reportId = "er-m31-16";
        var (controller, svc) = Build(GlobalAdminPrincipal(), reports: [], triageResult: null);

        var action = await controller.MarkTriaged(reportId);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(ErrorReportAdminController.Index), redirect.ActionName);

        // The write lane was called exactly once (the controller does not
        // double-call on a no-op) — but it returned null (the already-
        // triaged report, the M31·6 idempotency pin).
        await svc.Received(1).MarkTriagedAsync(reportId, Admin, Arg.Any<CancellationToken>());

        // A no-op → NO flash (no second audit row — M31·6). The redirect is
        // the same either way, but the flash is the discriminator.
        Assert.False(controller.TempData.ContainsKey("info"));
    }
}
