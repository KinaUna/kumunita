using Kumunita.Core.ErrorReports;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Kumunita.Web.Services;
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
/// M32 U07 (ADR 0155) — the <c>/admin/error-reports/{id}</c> GlobalAdmin
/// detail + resolve + escalate surface pins (M32·7 / M32·8 / M32·10 — the
/// design doc §2.4 test 12–18):
/// <list type="number">
/// <item><b>M32_10_Admin_Detail_SignedIn_GlobalAdmin_Sees_Report</b> — a
/// signed-in <c>GlobalAdmin</c> visiting <c>GET /admin/error-reports/{id}</c>
/// sees the full report (the <see cref="AdminErrorReportDetailViewModel"/>
/// carrying the <c>ErrorReport</c> — the 15-member M32·3 ceiling) via the
/// <see cref="IErrorReportService.ListAsync"/> read (a read — no audit, the
/// M31·4 pin) (FACES M32-4, M32·10).</item>
/// <item><b>M32_10_Admin_Detail_NonGlobalAdmin_Denied</b> — a non-
/// <c>GlobalAdmin</c> gets a 403: the <c>[Authorize(Roles = GlobalAdmin)]</c>
/// gate is the standard admin gate (the thin-token rule ADR 0001-B, the
/// M32·10 pin — asserted via reflection, the house "string pin, no
/// TestServer" idiom) (FACES M32-10).</item>
/// <item><b>M32_8_Admin_Resolve_GlobalAdmin_Updates_Row</b> — a
/// <c>GlobalAdmin</c> marking a report resolved calls the one
/// <see cref="IErrorReportService.MarkResolvedAsync"/> lane (the M32·8
/// single-write-lane), sets the <c>errorreport.resolve.flash</c> flash, and
/// redirects to the detail view. The audit-row assertion (exactly one
/// <c>AccessAudit</c> row, <c>Via = Admin</c>, action
/// <c>errorreport.resolve</c>) lives in the
/// <see cref="Kumunita.Core.Tests.ErrorReportResolveTests"/> (the service
/// owns it) (FACES M32-5).</item>
/// <item><b>M32_8_Admin_Resolve_AlreadyResolved_NoOp</b> — marking an
/// already-<c>resolved</c> report is a no-op: the lane returns
/// <c>null</c>, the controller sets NO flash, redirects to the detail view
/// (the M32·8 idempotency pin — the "no second audit row" half lives in the
/// Core <see cref="Kumunita.Core.Tests.ErrorReportResolveTests"/>) (FACES
/// M32-6).</item>
/// <item><b>M32_7_Admin_Escalate_Configured_ForwardSucceeds_Resolves_Row</b>
/// — a <c>GlobalAdmin</c> escalating a report with a <c>Success == true</c>
/// forward (the <see cref="IEscalationForwarder"/> stubbed) stamps the
/// report <c>resolved</c> via the one
/// <see cref="IErrorReportService.MarkResolvedAsync"/> lane (the
/// <c>"Escalated to operator endpoint"</c> marker — the M32·8 pin; the Core
/// service writes the <c>errorreport.resolve</c> audit row — the
/// <c>errorreport.escalate</c> action is the *Web-layer* marker the register
/// names) + sets the <c>errorreport.escalate.flash_success</c> flash, and
/// redirects to the detail view (FACES M32-7, M32·7).</item>
/// <item><b>M32_7_Admin_Escalate_ForwardFails_NoStateChange</b> — escalating
/// with a <c>Success == false</c> forward (the forwarder stubbed) is a
/// no-op: the controller sets the
/// <c>errorreport.escalate.flash_failure</c> flash, does NOT call
/// <see cref="IErrorReportService.MarkResolvedAsync"/> (no state change,
/// the admin can retry), and redirects to the detail view (FACES M32-9,
/// M32·7).</item>
/// <item><b>M32_6_Admin_Escalate_NotConfigured_NoStateChange</b> — escalating
/// with a <c>Configured == false</c> forward (the
/// <c>KUMUNITA_ESCALATION_ENDPOINT</c> env var absent, the forwarder
/// stubbed) is a no-op: the controller sets the
/// <c>errorreport.escalate.not_configured</c> flash, does NOT call
/// <see cref="IErrorReportService.MarkResolvedAsync"/> (no state change),
/// and redirects to the detail view (FACES M32-8, M32·6).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Test model (the house idiom, verbatim from the M31
/// <see cref="AdminErrorReportTests"/>):</b> direct-construction
/// (<see cref="ErrorReportAdminController"/> with an NSubstitute
/// <see cref="IErrorReportService"/> seam + an NSubstitute
/// <see cref="IEscalationForwarder"/> stub, a <see cref="DefaultHttpContext"/>,
/// no TestServer). The <c>[Authorize(Roles = GlobalAdmin)]</c> gate is
/// asserted via reflection (the
/// <see cref="AdminErrorReportTests.M31_4_Admin_List_NonGlobalAdmin_Denied"/>
/// idiom). The <see cref="IEscalationForwarder"/> is **stubbed** (the M32·5
/// pin — the <see cref="EscalationForwarder"/> impl is **not** called; Core
/// stays HTTP-free, ADR 0006-D). The audit-row assertions live in the Core
/// <see cref="Kumunita.Core.Tests.ErrorReportResolveTests"/> (the service
/// owns them) — the controller is the thin seam.
/// </para>
public class AdminErrorReportDetailTests
{
    private const string Admin = "admin-m32-u07";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (ErrorReportAdminController controller, IErrorReportService svc, IEscalationForwarder forwarder) Build(
        ClaimsPrincipal user,
        IReadOnlyList<ErrorReport> reports,
        ErrorReport? resolveResult,
        IEscalationForwarder? forwarder = null)
    {
        var svc = Substitute.For<IErrorReportService>();
        svc.ListAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ErrorReport>>(reports));
        svc.MarkResolvedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(resolveResult));

        // The escalation forwarder is stubbed (the M32·5 pin — the
        // EscalationForwarder impl is NOT called; Core stays HTTP-free).
        var fwd = forwarder ?? Substitute.For<IEscalationForwarder>();

        var controller = new ErrorReportAdminController(svc, escalationForwarder: fwd);

        var httpContext = new DefaultHttpContext();
        httpContext.User = user;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, svc, fwd);
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

    // ── 12 — a signed-in GlobalAdmin sees the report detail (M32·10) ──────
    // FACES M32-4: GET /admin/error-reports/{id} returns the report (the
    // AdminErrorReportDetailViewModel carrying the 15-member ErrorReport)
    // via the ListAsync read (a read — no audit, the M31·4 pin). A missing
    // report is a NotFound() (the controller's 404 branch — not pinned here,
    // the found-report path is the M32-4 FACE).

    [Fact(DisplayName = "M32_10_Admin_Detail_SignedIn_GlobalAdmin_Sees_Report")]
    public async Task M32_10_Admin_Detail_SignedIn_GlobalAdmin_Sees_Report()
    {
        var report = new ErrorReport
        {
            Id = "er-detail-12",
            SubjectId = "sub-resident-12",
            Description = "The group calendar is down",
            RequestId = "req-12",
            ExceptionType = null,
            Created = DateTimeOffset.UtcNow,
            TriageStatus = "new",
            Origin = "general",
        };
        var reports = new[] { report };

        var (controller, svc, _) = Build(GlobalAdminPrincipal(), reports, resolveResult: null);
        var view = Assert.IsType<ViewResult>(await controller.Detail("er-detail-12"));
        var vm = Assert.IsType<AdminErrorReportDetailViewModel>(view.ViewData.Model);

        // The detail view model carries the full report (the 15-member M32
        // ceiling — the M32·3 pin); the read seam was called exactly once.
        Assert.Same(report, vm.Report);
        Assert.Equal("er-detail-12", vm.Report.Id);
        Assert.Equal("general", vm.Report.Origin);
        Assert.Equal("new", vm.Report.TriageStatus);
        await svc.Received(1).ListAsync(100, Arg.Any<CancellationToken>());
        // No write lane was touched (a read — not an access decision, M31·4).
        await svc.DidNotReceive().MarkResolvedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    // ── 13 — a non-GlobalAdmin is denied (M32·10) ─────────────────────────
    // FACES M32-10: the [Authorize(Roles = GlobalAdmin)] gate is the standard
    // admin gate (the thin-token rule ADR 0001-B, M32·10 — no new authz
    // surface). In a direct-construction harness (no TestServer), the
    // attribute is asserted via reflection (the AdminErrorReportTests idiom).
    // The class-level gate covers the Detail + Resolve + Escalate actions.

    [Fact(DisplayName = "M32_10_Admin_Detail_NonGlobalAdmin_Denied")]
    public void M32_10_Admin_Detail_NonGlobalAdmin_Denied()
    {
        var attr = typeof(ErrorReportAdminController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.NotNull(attr.Roles);
        Assert.Contains(Kumunita.Core.Identity.Roles.GlobalAdmin, attr.Roles.Split(','));

        // The Detail action carries no weaker gate of its own (no
        // [AllowAnonymous] that would widen the class-level gate).
        var detail = typeof(ErrorReportAdminController).GetMethod(nameof(ErrorReportAdminController.Detail));
        Assert.NotNull(detail);
        Assert.Empty(detail!.GetCustomAttributes<AllowAnonymousAttribute>());

        // The Resolve + Escalate actions carry no weaker gate of their own
        // either — a non-GlobalAdmin never reaches MarkResolvedAsync or the
        // escalation forwarder (M32·10).
        var resolve = typeof(ErrorReportAdminController).GetMethod(nameof(ErrorReportAdminController.Resolve));
        Assert.NotNull(resolve);
        Assert.Empty(resolve!.GetCustomAttributes<AllowAnonymousAttribute>());
        var escalate = typeof(ErrorReportAdminController).GetMethod(nameof(ErrorReportAdminController.Escalate));
        Assert.NotNull(escalate);
        Assert.Empty(escalate!.GetCustomAttributes<AllowAnonymousAttribute>());
    }

    // ── 14 — a GlobalAdmin resolves a report (M32·8) ──────────────────────
    // FACES M32-5: the effective resolve calls the one MarkResolvedAsync
    // lane (the audit-row assertion — exactly one AccessAudit row, Via =
    // Admin, action errorreport.resolve — lives in the Core
    // ErrorReportResolveTests), sets the errorreport.resolve.flash flash
    // (the M32·9 closed set), and redirects to the detail view.

    [Fact(DisplayName = "M32_8_Admin_Resolve_GlobalAdmin_Updates_Row")]
    public async Task M32_8_Admin_Resolve_GlobalAdmin_Updates_Row()
    {
        const string reportId = "er-m32-14";
        var resolveResult = new ErrorReport { Id = reportId, TriageStatus = "resolved" };
        var (controller, svc, _) = Build(GlobalAdminPrincipal(), reports: [], resolveResult);

        var action = await controller.Resolve(reportId, resolutionNote: "Restarted the worker");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(ErrorReportAdminController.Detail), redirect.ActionName);
        Assert.Equal(reportId, redirect.RouteValues["id"]);

        // The one write lane was called exactly once for the signed-in
        // GlobalAdmin (M32·8 — the single audited write-lane shape). The
        // audit-row assertion lives in the Core ErrorReportResolveTests.
        await svc.Received(1).MarkResolvedAsync(
            reportId, Admin, "Restarted the worker", Arg.Any<CancellationToken>());

        // The flash is the errorreport.resolve.flash key (the M32·9 closed
        // set) — resolved to the KnownTranslationKeys.EnValues floor in this
        // test-construction site (the translation seams are null).
        Assert.Equal(
            KnownTranslationKeys.EnValues["errorreport.resolve.flash"],
            controller.TempData["info"]);
    }

    // ── 15 — resolving an already-resolved report is a no-op (M32·8) ──────
    // FACES M32-6: the lane returns null (an already-resolved report, the
    // M32·8 idempotency pin) → the controller sets NO flash (no second audit
    // row — the "no second audit row" half lives in the Core
    // ErrorReportResolveTests) and redirects to the detail view (the same
    // redirect either way).

    [Fact(DisplayName = "M32_8_Admin_Resolve_AlreadyResolved_NoOp")]
    public async Task M32_8_Admin_Resolve_AlreadyResolved_NoOp()
    {
        const string reportId = "er-m32-15";
        var (controller, svc, _) = Build(GlobalAdminPrincipal(), reports: [], resolveResult: null);

        var action = await controller.Resolve(reportId, resolutionNote: "Second note");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(ErrorReportAdminController.Detail), redirect.ActionName);
        Assert.Equal(reportId, redirect.RouteValues["id"]);

        // The write lane was called exactly once (the controller does not
        // double-call on a no-op) — but it returned null (the already-
        // resolved report, the M32·8 idempotency pin).
        await svc.Received(1).MarkResolvedAsync(
            reportId, Admin, "Second note", Arg.Any<CancellationToken>());

        // A no-op → NO flash (no second audit row — M32·8). The redirect is
        // the same either way, but the flash is the discriminator.
        Assert.False(controller.TempData.ContainsKey("info"));
    }

    // ── 16 — escalate with a successful forward resolves the row (M32·7) ──
    // FACES M32-7: a Success == true forward (the forwarder stubbed — the
    // EscalationForwarder impl is NOT called, M32·5) → the controller calls
    // MarkResolvedAsync(id, actor, "Escalated to operator endpoint") (the
    // M32·8 pin; the Core service writes the errorreport.resolve audit row —
    // errorreport.escalate is the Web-layer marker the register names),
    // sets the errorreport.escalate.flash_success flash, and redirects to
    // the detail view.

    [Fact(DisplayName = "M32_7_Admin_Escalate_Configured_ForwardSucceeds_Resolves_Row")]
    public async Task M32_7_Admin_Escalate_Configured_ForwardSucceeds_Resolves_Row()
    {
        const string reportId = "er-m32-16";
        var resolveResult = new ErrorReport { Id = reportId, TriageStatus = "resolved" };

        var forwarder = Substitute.For<IEscalationForwarder>();
        forwarder.ForwardAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new EscalationResult(
                Configured: true, Success: true, StatusCode: 202, Error: null)));

        var (controller, svc, _) = Build(GlobalAdminPrincipal(), reports: [], resolveResult, forwarder);

        var action = await controller.Escalate(reportId);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(ErrorReportAdminController.Detail), redirect.ActionName);
        Assert.Equal(reportId, redirect.RouteValues["id"]);

        // The successful forward stamps resolved via the one MarkResolvedAsync
        // lane (the "Escalated to operator endpoint" marker — the M32·8 pin).
        await svc.Received(1).MarkResolvedAsync(
            reportId, Admin, "Escalated to operator endpoint", Arg.Any<CancellationToken>());

        // The flash is the errorreport.escalate.flash_success key (the M32·9
        // closed set) — the KnownTranslationKeys.EnValues floor.
        Assert.Equal(
            KnownTranslationKeys.EnValues["errorreport.escalate.flash_success"],
            controller.TempData["info"]);
    }

    // ── 17 — escalate with a failed forward is a no-op (M32·7) ────────────
    // FACES M32-9: a Success == false forward (the forwarder stubbed) → the
    // controller sets the errorreport.escalate.flash_failure flash, does
    // NOT call MarkResolvedAsync (no state change, the admin can retry),
    // and redirects to the detail view.

    [Fact(DisplayName = "M32_7_Admin_Escalate_ForwardFails_NoStateChange")]
    public async Task M32_7_Admin_Escalate_ForwardFails_NoStateChange()
    {
        const string reportId = "er-m32-17";

        var forwarder = Substitute.For<IEscalationForwarder>();
        forwarder.ForwardAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new EscalationResult(
                Configured: true, Success: false, StatusCode: 503, Error: "Service Unavailable")));

        var (controller, svc, _) = Build(GlobalAdminPrincipal(), reports: [], resolveResult: null, forwarder);

        var action = await controller.Escalate(reportId);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(ErrorReportAdminController.Detail), redirect.ActionName);

        // A failed forward → NO MarkResolvedAsync call (no state change —
        // the M32·7 pin; the report stays new/triaged).
        await svc.DidNotReceive().MarkResolvedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        // The flash is the errorreport.escalate.flash_failure key (the M32·9
        // closed set) — the KnownTranslationKeys.EnValues floor.
        Assert.Equal(
            KnownTranslationKeys.EnValues["errorreport.escalate.flash_failure"],
            controller.TempData["error"]);
        Assert.False(controller.TempData.ContainsKey("info"));
    }

    // ── 18 — escalate with no endpoint configured is a no-op (M32·6) ──────
    // FACES M32-8: a Configured == false forward (the
    // KUMUNITA_ESCALATION_ENDPOINT env var absent, the forwarder stubbed) →
    // the controller sets the errorreport.escalate.not_configured flash,
    // does NOT call MarkResolvedAsync (no state change), and redirects to
    // the detail view.

    [Fact(DisplayName = "M32_6_Admin_Escalate_NotConfigured_NoStateChange")]
    public async Task M32_6_Admin_Escalate_NotConfigured_NoStateChange()
    {
        const string reportId = "er-m32-18";

        var forwarder = Substitute.For<IEscalationForwarder>();
        forwarder.ForwardAsync(reportId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new EscalationResult(
                Configured: false, Success: false, StatusCode: null, Error: null)));

        var (controller, svc, _) = Build(GlobalAdminPrincipal(), reports: [], resolveResult: null, forwarder);

        var action = await controller.Escalate(reportId);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(ErrorReportAdminController.Detail), redirect.ActionName);

        // An unconfigured forward → NO MarkResolvedAsync call (no state
        // change — the M32·6 pin).
        await svc.DidNotReceive().MarkResolvedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());

        // The flash is the errorreport.escalate.not_configured key (the
        // M32·9 closed set) — the KnownTranslationKeys.EnValues floor.
        Assert.Equal(
            KnownTranslationKeys.EnValues["errorreport.escalate.not_configured"],
            controller.TempData["error"]);
        Assert.False(controller.TempData.ContainsKey("info"));
    }
}
