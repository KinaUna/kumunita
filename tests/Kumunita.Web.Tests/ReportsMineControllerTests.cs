using System.Security.Claims;
using Kumunita.Core.ErrorReports;
using Kumunita.Core.Moderation;
using Kumunita.Core.Posts;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The <see cref="ReportsMineController"/> Web-boundary seam tests
/// (IMPROVE-UX R06 — the D1 "My reports" shape's Web side: the signed-in
/// resident's own content reports + platform issues, the two R05 read
/// seams, one action, one route). Mirrors the
/// <see cref="BookmarksControllerTests"/> /
/// <see cref="AdminErrorReportDetailTests"/> harness shape: the **frozen**
/// R05 seams (<see cref="IModerationService"/> /
/// <see cref="IErrorReportService"/>) are substituted with NSubstitute (the
/// controller never re-derives access — the ADR 0105 / M17 "operator has no
/// read standing" gate: a caller with no <c>SubjectId</c> gets a non-leaky
/// <see cref="NotFoundResult"/> and neither seam is called).
/// <para>
/// The R06 pins (the R06 card's three tests):
/// <list type="number">
/// <item><b>R06_01_SignedInSubject_BothSeams_Called_With_SubjectId</b> —
/// a signed-in subject gets the view with both lists, the
/// <see cref="IModerationService.ListMyReportsAsync"/> called exactly once
/// with the subject id (the <c>reporterId</c>) and the
/// <see cref="IErrorReportService.ListMineAsync"/> called exactly once with
/// the subject id (the <c>subjectId</c>). The view-model's
/// <c>HasAny</c> is true (at least one list non-empty).</item>
/// <item><b>R06_02_BlankSubjectId_404_NeitherSeamCalled</b> — a caller
/// with no <c>SubjectId</c> gets a non-leaky <see cref="NotFoundResult"/>
/// and **neither** seam is called (the "operator has no read standing"
/// gate — the <see cref="BookmarksControllerTests.F3_NonOwner_Gets_404_And_NoAuditRow_Commits"/>
/// shape). That 404 commits no <c>AccessAudit</c> row (the R05 seams
/// already pin that; the controller's gate is the Web-layer half).</item>
/// <item><b>R06_03_BothListsEmpty_HasAnyFalse</b> — a signed-in subject
/// with zero reports **and** zero issues gets the view with
/// <c>HasAny == false</c> (the view's friendly empty-state branch — the
/// E16 "one next action" shape). Both seams are still called (the
/// personal read; the empty lists are the subject's own absence of
/// rows, not a permission error).</item>
/// </list>
/// </para>
/// <para>
/// **No database, no Testcontainers** — a pure NSubstitute seam test
/// (the seam's read decisions are the seam's; this layer pins the
/// controller's mapping + the view-model shape).
/// </para>
/// </summary>
public sealed class ReportsMineControllerTests
{
    private const string Resident = "subj-r06-resident";

    // ── 1 — R06_01_SignedInSubject_BothSeams_Called_With_SubjectId ────────

    /// <summary>
    /// A signed-in subject (the <c>SubjectId</c> claim) gets the view with
    /// both lists. The <see cref="IModerationService.ListMyReportsAsync"/>
    /// is called **exactly once** with the actor's <c>SubjectId</c> as the
    /// <c>reporterId</c>, and the
    /// <see cref="IErrorReportService.ListMineAsync"/> is called **exactly
    /// once** with the actor's <c>SubjectId</c> as the <c>subjectId</c>.
    /// The view-model's <c>HasAny</c> is true (at least one list
    /// non-empty). The controller composes **only** the two R05 seams (no
    /// <c>IAuthorizationService</c> is injected — the R05 seams already
    /// pin the "read, not a decision" shape: zero
    /// <c>CanSeeAsync</c> passes, zero <c>AccessAudit</c> rows).
    /// </summary>
    [Fact]
    public async Task R06_01_SignedInSubject_BothSeams_Called_With_SubjectId()
    {
        var reports = new[]
        {
            new MyReportRow
            {
                ReportId = "r-1",
                PostId = "p-1",
                Status = "filed",
                At = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero),
                TargetTitle = "A visible post",
            },
            new MyReportRow
            {
                ReportId = "r-2",
                PostId = "p-2",
                Status = "resolved",
                At = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero),
                TargetTitle = null, // the R05 "no longer visible" shape
                Outcome = ReportOutcome.Hidden,
                ResolutionNote = "The post was hidden.",
                ResolvedAt = new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
                ResolvedBy = "mod-1",
            },
        };
        var issues = new[]
        {
            new ErrorReport
            {
                Id = "e-1",
                SubjectId = Resident,
                Description = "The calendar page went blank",
                Created = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero),
                Origin = "general",
                TriageStatus = "new",
            },
        };

        var moderation = Substitute.For<IModerationService>();
        moderation.ListMyReportsAsync(Resident)
            .Returns(Task.FromResult<IReadOnlyList<MyReportRow>>(reports));
        var errorReports = Substitute.For<IErrorReportService>();
        errorReports.ListMineAsync(Resident)
            .Returns(Task.FromResult<IReadOnlyList<ErrorReport>>(issues));

        var controller = Build(moderation, errorReports, subjectId: Resident);
        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<ReportsMineController.ReportsMineViewModel>(view.ViewData.Model);

        // The view-model carries both lists verbatim (the controller
        // never reshapes the seam results — the ADR 0105 / M17
        // "personal read, zero audit" shape).
        Assert.Same(reports, vm.Reports);
        Assert.Same(issues, vm.Issues);
        Assert.Equal(2, vm.Reports.Count);
        Assert.Equal(1, vm.Issues.Count);
        Assert.True(vm.HasAny);

        // Both seams were called exactly once with the actor's SubjectId
        // (the sole-reader pin — the controller never re-derives access;
        // the two R05 seams are the only readers).
        await moderation.Received(1).ListMyReportsAsync(Resident);
        await errorReports.Received(1).ListMineAsync(Resident);
    }

    // ── 2 — R06_02_BlankSubjectId_404_NeitherSeamCalled ───────────────────

    /// <summary>
    /// A caller with no <c>SubjectId</c> (the ADR 0105 / M17 "operator has
    /// no read standing" gate, the
    /// <see cref="BookmarksControllerTests.F3_NonOwner_Gets_404_And_NoAuditRow_Commits"/>
    /// shape) gets a non-leaky <see cref="NotFoundResult"/>, and
    /// **neither** seam is called (the sole-reader pin — the controller
    /// returns the 404 before delegating any read to the seams; that 404
    /// commits no <c>AccessAudit</c> row, the R05 seams already pin
    /// that).
    /// </summary>
    [Fact]
    public async Task R06_02_BlankSubjectId_404_NeitherSeamCalled()
    {
        // The seams are **not** stubbed — the no-subject path returns
        // NotFound before reaching them. NSubstitute's auto-substitution
        // of the un-stubbed method is inert (no call is logged for a
        // method the controller never invokes), and the DidNotReceive
        // assertion below is the pin.
        var moderation = Substitute.For<IModerationService>();
        var errorReports = Substitute.For<IErrorReportService>();

        var controller = Build(moderation, errorReports, subjectId: null);
        var result = await controller.Index();

        Assert.IsType<NotFoundResult>(result);

        await moderation.DidNotReceive().ListMyReportsAsync(Arg.Any<string>());
        await errorReports.DidNotReceive().ListMineAsync(Arg.Any<string>());
    }

    // ── 3 — R06_03_BothListsEmpty_HasAnyFalse ─────────────────────────────

    /// <summary>
    /// A signed-in subject with zero reports **and** zero issues gets the
    /// view with <c>HasAny == false</c> (the view's friendly empty-state
    /// branch — the E16 "one next action" shape). Both seams are still
    /// called (the personal read; the empty lists are the subject's own
    /// absence of rows, not a permission error — the ADR 0105 / M17
    /// "personal read, zero audit" shape).
    /// </summary>
    [Fact]
    public async Task R06_03_BothListsEmpty_HasAnyFalse()
    {
        var moderation = Substitute.For<IModerationService>();
        moderation.ListMyReportsAsync(Resident)
            .Returns(Task.FromResult<IReadOnlyList<MyReportRow>>([]));
        var errorReports = Substitute.For<IErrorReportService>();
        errorReports.ListMineAsync(Resident)
            .Returns(Task.FromResult<IReadOnlyList<ErrorReport>>([]));

        var controller = Build(moderation, errorReports, subjectId: Resident);
        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<ReportsMineController.ReportsMineViewModel>(view.ViewData.Model);

        Assert.Empty(vm.Reports);
        Assert.Empty(vm.Issues);
        Assert.False(vm.HasAny);

        // Both seams were still called (the personal read; the empty
        // lists are the subject's own absence of rows).
        await moderation.Received(1).ListMyReportsAsync(Resident);
        await errorReports.Received(1).ListMineAsync(Resident);
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="ReportsMineController"/> over NSubstitute seams.
    /// The <see cref="IModerationService"/> + <see cref="IErrorReportService"/>
    /// are the **only** seams (the R05 "read, not a decision" shape — the
    /// controller composes no <c>IAuthorizationService</c>, no raw store,
    /// no <c>IAccessAuditService</c>; the ADR 0105 / M17 "operator has no
    /// read standing" gate is the controller's own <c>SubjectId</c> check,
    /// not a seam call). A no-op <see cref="ITempDataProvider"/> closes the
    /// <c>TempData</c> bag so the <c>DefaultHttpContext</c> (which leaves
    /// <c>Session</c> null) doesn't NRE.
    /// </summary>
    private static ReportsMineController Build(
        IModerationService moderation,
        IErrorReportService errorReports,
        string? subjectId = null)
    {
        var controller = new ReportsMineController(moderation, errorReports);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the view result / the call
            // log, not the bag
        }
    }
}
