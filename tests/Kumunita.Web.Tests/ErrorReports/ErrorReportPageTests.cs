using Kumunita.Core;
using Kumunita.Core.ErrorReports;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.IO;
using System.Security.Claims;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M31 U06 (ADR 0154) — the <c>/Home/Error</c> 500-page report-an-issue
/// pins (M31·2 / M31·5 / M31·7 / M31·8):
/// <list type="number">
/// <item><b>M31_2_Error_Page_Shows_Report_Form</b> — the 500 error page
/// renders the report form (the <c>errorreport.*</c> kw-l keys 1–8 are
/// resolved in the view + the form POSTs to <c>/Home/Error/Report</c> with
/// the <c>Description</c> textarea, the optional <c>ContactEmail</c> email
/// input, and the anti-forgery token — the public, anonymous-safe,
/// non-blocking affordance, M31·2) (FACES M31-1 / M31-7).</item>
/// <item><b>M31_5_Error_Report_Post_Creates_ErrorReport</b> — a valid
/// <c>POST /Home/Error/Report</c> stores one <c>ErrorReport</c> row through
/// the one <see cref="IErrorReportService.CreateAsync"/> lane + re-renders
/// the error view with <c>FormSubmitted = true</c> (the
/// <c>errorreport.thanks</c> confirmation, no redirect — M31·2 / M31·5).
/// The audit-row assertion (exactly one <c>AccessAudit</c> row, the
/// <c>Via</c> pin) lives in the
/// <see cref="Kumunita.Core.Tests.ErrorReportServiceTests"/> (the service
/// owns it) (FACES M31-1 / M31-2).</item>
/// <item><b>M31_5_Error_Report_Post_Validation_BlankDescription_Renders_Error</b>
/// — a blank <c>Description</c> is a form-level validation error (a 400
/// re-render, never a 500): the action re-renders the form pre-filled with
/// the resident's typed values + <c>FormSubmitted = false</c>, and does NOT
/// call the write lane (M31·5 "never a 500 back to the resident").</item>
/// <item><b>M31_2_Error_Report_Post_Confirmation_Visible</b> — after a
/// successful submit the <c>errorreport.thanks</c> confirmation is visible
/// (the view renders key 8 in place of the form when
/// <c>Model.FormSubmitted</c> is true — no redirect, no modal, M31·2).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Test model (the house idiom):</b> direct-construction
/// (<see cref="HomeController"/> with an NSubstitute
/// <see cref="IErrorReportService"/> seam, a <see cref="DefaultHttpContext"/>,
/// no TestServer — the <see cref="AdminOnboardingControllerTests"/> /
/// <see cref="Kumunita.Web.Tests.AdminOnboardingBannerTests"/>
/// "string pin, no TestServer" house idiom). The view is a Razor file
/// (<c>Views/Shared/Error.cshtml</c>) — the Web.Tests suite has no
/// <c>RazorPage</c> / <c>RenderViewToString</c> harness, so the form +
/// confirmation pins are <b>structural string pins</b> on the view source
/// (the <see cref="Kumunita.Web.Tests.AdminOnboardingBannerTests"/>
/// <c>LoadBannerSource</c> idiom, walking up to the repo root), and the
/// <c>Error</c> / <c>ErrorReport</c> action pins are
/// <b>controller-behaviour pins</b> (the <see cref="ErrorViewModel"/> the
/// action returns + the <see cref="IErrorReportService"/> call log).
/// </para>
public class ErrorReportPageTests
{
    private static string LoadErrorViewSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "src", "Kumunita.Web", "Views", "Shared", "Error.cshtml");
        Assert.True(File.Exists(path), $"Error.cshtml not found at {path}");
        return File.ReadAllText(path);
    }

    private static HomeController Build(IErrorReportService? errorReports = null, ClaimsPrincipal? user = null)
    {
        var controller = new HomeController(
            logger: NullLogger<HomeController>.Instance,
            community: Options.Create(new CommunityOptions { Name = "Kumunita" }),
            errorReports: errorReports);
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-m31-u06";
        // The User is set on the DefaultHttpContext BEFORE the ControllerContext
        // is attached (ControllerBase.User is read-only — the
        // AdminOnboardingControllerTests idiom: set httpContext.User, then
        // controller.ControllerContext).
        httpContext.User = user ?? new ClaimsPrincipal(new ClaimsIdentity(new Claim[0], authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static ClaimsPrincipal AnonymousPrincipal() =>
        new(new ClaimsIdentity(new Claim[0], authenticationType: "test"));

    private static ClaimsPrincipal ResidentPrincipal(string subject) =>
        new(new ClaimsIdentity(
            new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subject) },
            authenticationType: "test"));

    // ── 9 — the 500 error page renders the report form (M31·2) ─────────────
    // FACES M31-1 / M31-7: the Error.cshtml resolves the errorreport.* kw-l
    // keys 1–8, renders the form (the Description textarea + the optional
    // ContactEmail email input + the submit button) POSTing to
    // /Home/Error/Report with the anti-forgery token, and renders the
    // errorreport.thanks confirmation in place of the form when
    // Model.FormSubmitted is true. The Error action returns a ViewResult
    // (the form is public — no [Authorize] gate, M31·2 / M31·9).

    [Fact(DisplayName = "M31_2_Error_Page_Shows_Report_Form")]
    public void M31_2_Error_Page_Shows_Report_Form()
    {
        // (a) The Error action renders the report form (a ViewResult, the
        // public 500 page — M31·2). The form is always available: the
        // ErrorViewModel defaults FormSubmitted to false (the form branch),
        // and a directly-visited /Home/Error (no IExceptionHandlerFeature)
        // still renders.
        var controller = Build(user: AnonymousPrincipal());
        var action = controller.Error();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<ErrorViewModel>(view.ViewData.Model);
        Assert.False(vm.FormSubmitted);
        Assert.True(vm.ShowRequestId);

        // (b) The view source (the structural string pin — the house
        // "string pin, no TestServer" idiom) resolves the closed
        // errorreport.* kw-l key set 1–8 (M31·7 / M31·8), renders the form
        // (the two bind-model fields + the submit), POSTs to the M31
        // submission route with the anti-forgery token, and renders the
        // thanks confirmation in the FormSubmitted branch.
        var src = LoadErrorViewSource();

        // The kw-l keys 1–8 (resolved server-side via the house seam).
        Assert.Contains("errorreport.title", src);
        Assert.Contains("errorreport.intro", src);
        Assert.Contains("errorreport.description.label", src);
        Assert.Contains("errorreport.description.placeholder", src);
        Assert.Contains("errorreport.email.label", src);
        Assert.Contains("errorreport.email.placeholder", src);
        Assert.Contains("errorreport.submit", src);
        Assert.Contains("errorreport.thanks", src);

        // The form binds to the two resident-supplied fields (explicit name
        // attributes — the view's @model is ErrorViewModel, so asp-for is not
        // available for the bind-model fields).
        Assert.Contains("name=\"Description\"", src);
        Assert.Contains("name=\"ContactEmail\"", src);
        Assert.Contains("type=\"email\"", src);

        // The form POSTs to the M31 submission route with the anti-forgery
        // token (the house [ValidateAntiForgeryToken] idiom).
        Assert.Contains("asp-action=\"ErrorReport\"", src);
        Assert.Contains("@Html.AntiForgeryToken()", src);

        // The confirmation branch (key 8 in place of the form when
        // Model.FormSubmitted — no redirect, no modal, M31·2).
        Assert.Contains("@if (Model.FormSubmitted)", src);
        Assert.Contains("alert alert-success", src);

        // The form is public — NOT role-gated (M31·2 / M31·9): no role-claim
        // read (IsGlobalAdmin / IsInRole / KumunitaPrincipal.Is*) guards the
        // form on this 500 page (the page is the one place an anonymous
        // visitor sees a product surface). (Asserting the role-gate absence
        // rather than the literal "[Authorize]" string — the view's own
        // documentation comment mentions "[Authorize]", so the gate's
        // absence is the faithful pin.)
        Assert.DoesNotContain("IsGlobalAdmin", src);
        Assert.DoesNotContain("IsInRole", src);
        Assert.DoesNotContain("KumunitaPrincipal.Is", src);
    }

    // ── 10 — a valid POST /Home/Error/Report stores the row (M31·5) ────────
    // FACES M31-1 / M31-2: on a valid model the action calls the one
    // CreateAsync write lane with the draft projected from the form + the
    // request context, then re-renders the error view with FormSubmitted =
    // true (the thanks confirmation — no redirect). The audit-row assertion
    // (exactly one AccessAudit row, the Via pin) lives in the
    // ErrorReportServiceTests (the service owns it).

    [Fact(DisplayName = "M31_5_Error_Report_Post_Creates_ErrorReport")]
    public async Task M31_5_Error_Report_Post_Creates_ErrorReport()
    {
        var svc = Substitute.For<IErrorReportService>();
        var stored = new ErrorReport { Id = "er-m31-10", TriageStatus = "new" };
        svc.CreateAsync(Arg.Any<ErrorReportDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stored));

        var controller = Build(svc, user: AnonymousPrincipal()); // SubjectId = "" (anonymous)
        controller.ModelState.Clear();

        var form = new ErrorReportFormModel
        {
            Description = "I was trying to RSVP for the Saturday event and the page went blank",
            ContactEmail = "resident@example.com",
        };

        var action = await controller.ErrorReport(form);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<ErrorViewModel>(view.ViewData.Model);
        Assert.True(vm.FormSubmitted); // the thanks confirmation (M31·2)

        // The one write lane was called exactly once with the draft projected
        // from the form + the request context (the anonymous SubjectId "").
        // In a DefaultHttpContext the UserAgent header is empty → Truncate
        // yields "" (an empty string, not null); the IExceptionHandlerFeature
        // is absent → ExceptionType is null.
        await svc.Received(1).CreateAsync(Arg.Is<ErrorReportDraft>(d =>
            d.SubjectId == string.Empty &&
            d.Description == "I was trying to RSVP for the Saturday event and the page went blank" &&
            d.ContactEmail == "resident@example.com" &&
            d.RequestId == "trace-m31-u06" &&
            d.ExceptionType == null &&
            string.IsNullOrEmpty(d.UserAgent)), Arg.Any<CancellationToken>());
    }

    // ── 11 — a blank description is a 400 re-render, never a 500 (M31·5) ───
    // FACES M31-7 (the validation path): on !ModelState.IsValid the action
    // re-renders the form pre-filled (the resident's typed values preserved)
    // with FormSubmitted = false, and does NOT call the write lane — a
    // form-level validation error is a 400 re-render, not a crash (M31·5).

    [Fact(DisplayName = "M31_5_Error_Report_Post_Validation_BlankDescription_Renders_Error")]
    public async Task M31_5_Error_Report_Post_Validation_BlankDescription_Renders_Error()
    {
        var svc = Substitute.For<IErrorReportService>();
        var controller = Build(svc, user: AnonymousPrincipal());

        // A blank description → the [Required] validation fails (the house
        // [Required] + ModelState idiom). The resident typed an email but no
        // description (the echo-back must preserve it).
        controller.ModelState.AddModelError("Description", "Description is required.");

        var form = new ErrorReportFormModel
        {
            Description = "",
            ContactEmail = "resident@example.com",
        };

        var action = await controller.ErrorReport(form);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<ErrorViewModel>(view.ViewData.Model);

        // A 400 re-render, never a 500: the form is re-shown (FormSubmitted
        // false) with the resident's already-typed values preserved.
        Assert.False(vm.FormSubmitted);
        Assert.Equal("", vm.FormDescription);
        Assert.Equal("resident@example.com", vm.FormContactEmail);

        // The write lane was NOT called (the blank description never reaches
        // CreateAsync — the M31·5 "never a 500 back to the resident" pin).
        await svc.DidNotReceive().CreateAsync(
            Arg.Any<ErrorReportDraft>(), Arg.Any<CancellationToken>());
    }

    // ── 12 — the post-submission confirmation is visible (M31·2) ───────────
    // FACES M31-1: after a successful submit (a non-null draft), the action
    // re-renders the error view with FormSubmitted = true — the
    // errorreport.thanks confirmation is visible (the view renders key 8 in
    // place of the form), no redirect, no modal.

    [Fact(DisplayName = "M31_2_Error_Report_Post_Confirmation_Visible")]
    public async Task M31_2_Error_Report_Post_Confirmation_Visible()
    {
        var svc = Substitute.For<IErrorReportService>();
        var stored = new ErrorReport { Id = "er-m31-12", TriageStatus = "new" };
        svc.CreateAsync(Arg.Any<ErrorReportDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stored));

        var controller = Build(svc, user: ResidentPrincipal("sub-resident-m31-12")); // signed-in
        controller.ModelState.Clear();

        var form = new ErrorReportFormModel
        {
            Description = "The community page timed out",
            ContactEmail = null,
        };

        var action = await controller.ErrorReport(form);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<ErrorViewModel>(view.ViewData.Model);

        // The confirmation is visible (FormSubmitted true → the view renders
        // the errorreport.thanks key in place of the form — no redirect).
        Assert.True(vm.FormSubmitted);

        // The write lane was called with the signed-in SubjectId (the
        // resident's claim — FACES M31-2) + a null ContactEmail (the null
        // form field maps to null in the draft, M31·5).
        await svc.Received(1).CreateAsync(Arg.Is<ErrorReportDraft>(d =>
            d.SubjectId == "sub-resident-m31-12" &&
            d.Description == "The community page timed out" &&
            d.ContactEmail == null), Arg.Any<CancellationToken>());
    }
}
