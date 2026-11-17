using Kumunita.Core.ErrorReports;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.IO;
using System.Security.Claims;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M32 U07 (ADR 0155) — the public <c>GET /issues/new</c> +
/// <c>POST /issues/new</c> general issue-submission surface pins (M32·4 —
/// public, anonymous-safe, non-blocking; the design doc §2.4 test 7–11):
/// <list type="number">
/// <item><b>M32_4_Issue_Page_Shows_Issue_Form</b> — the
/// <c>GET /issues/new</c> action returns a <see cref="ViewResult"/> with a
/// fresh <see cref="IssueFormModel"/> (<c>FormSubmitted = false</c>, the
/// form branch); the view source resolves the <c>issue.*</c> kw-l keys
/// 1–8, renders the form (the <c>Description</c> textarea + the optional
/// <c>ContactEmail</c> email input + the submit button) POSTing to
/// <c>/issues/new</c> with the anti-forgery token, and renders the
/// <c>issue.thanks</c> confirmation in place of the form when
/// <c>Model.FormSubmitted</c> is true — the public, anonymous-safe,
/// non-blocking affordance (M32·4) (FACES M32-1 / M32-2).</item>
/// <item><b>M32_4_Issue_Post_SignedIn_Creates_ErrorReport_Origin_General</b>
/// — a signed-in resident's valid <c>POST /issues/new</c> calls the one
/// <see cref="IErrorReportService.CreateAsync"/> lane with a draft whose
/// <c>SubjectId</c> is set + <c>Origin = "general"</c> +
/// <c>ExceptionType = null</c> (the M32·4 pin — the general issue is not
/// tied to an error page), then re-renders with <c>FormSubmitted = true</c>
/// (FACES M32-1).</item>
/// <item><b>M32_4_Issue_Post_Anonymous_Creates_ErrorReport_Origin_General</b>
/// — an anonymous visitor's valid <c>POST /issues/new</c> calls the write
/// lane with a draft whose <c>SubjectId</c> is the empty string +
/// <c>Origin = "general"</c> + <c>ExceptionType = null</c>, then re-renders
/// with <c>FormSubmitted = true</c> (FACES M32-2).</item>
/// <item><b>M32_4_Issue_Post_Validation_BlankDescription_Renders_Error</b>
/// — a blank <c>Description</c> is a form-level 400 re-render (never a 500):
/// the action re-renders the form pre-filled with the resident's typed
/// values + <c>FormSubmitted = false</c>, and does NOT call the write lane
/// (the M32·4 "never a 500 back to the resident" pin, the M31·5 precedent).</item>
/// <item><b>M32_4_Issue_Post_Confirmation_Visible</b> — after a successful
/// submit the <c>issue.thanks</c> confirmation is visible (the view renders
/// the <c>issue.thanks</c> key in place of the form when
/// <c>Model.FormSubmitted</c> is true — no redirect, no modal, M32·4;
/// the audit-row assertion lives in the
/// <see cref="Kumunita.Core.Tests.ErrorReportResolveTests"/>
/// <c>M32_4_CreateAsync_Origin_General_Stores_ErrorReport</c> — the service
/// owns it).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Test model (the house idiom, verbatim from the M31
/// <see cref="ErrorReportPageTests"/>):</b> direct-construction
/// (<see cref="IssueController"/> with an NSubstitute
/// <see cref="IErrorReportService"/> seam, a <see cref="DefaultHttpContext"/>,
/// no TestServer). The view is a Razor file
/// (<c>Views/Issues/New.cshtml</c>) — the Web.Tests suite has no
/// <c>RazorPage</c> / <c>RenderViewToString</c> harness, so the form +
/// confirmation pins are <b>structural string pins</b> on the view source
/// (the <see cref="ErrorReportPageTests"/> <c>LoadErrorViewSource</c>
/// idiom, walking up to the repo root), and the
/// <c>GetNew</c> / <c>PostNew</c> action pins are <b>controller-behaviour
/// pins</b> (the <see cref="IssueFormModel"/> the action returns + the
/// <see cref="IErrorReportService"/> call log).
/// </para>
public class IssuePageTests
{
    private static string LoadIssueViewSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        // M33-post: the production view lives at Views/Issue/New.cshtml (singular)
        // — matching the singular IssueController class name (MVC's default
        // Views/{Controller}/{Action}.cshtml resolution). The live app serves
        // GET /issues/new with a 200 from exactly this path, so the test's
        // earlier plural path (Views/Issues/) pointed at a file that never
        // existed. Pin the real production view, not a dead plural duplicate.
        var path = Path.Combine(dir!.FullName, "src", "Kumunita.Web", "Views", "Issue", "New.cshtml");
        Assert.True(File.Exists(path), $"Views/Issue/New.cshtml not found at {path}");
        return File.ReadAllText(path);
    }

    private static IssueController Build(IErrorReportService? errorReports = null, ClaimsPrincipal? user = null)
    {
        var controller = new IssueController(errorReports ?? Substitute.For<IErrorReportService>());
        var httpContext = new DefaultHttpContext();
        httpContext.TraceIdentifier = "trace-m32-u07";
        // The User is set on the DefaultHttpContext BEFORE the ControllerContext
        // is attached (the AdminOnboardingControllerTests idiom).
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

    // ── 7 — the /issues/new page renders the issue form (M32·4) ───────────
    // FACES M32-1 / M32-2: the GET action returns a fresh form view (the
    // public, anonymous-safe surface — no [Authorize] gate, M32·4); the view
    // source resolves the issue.* kw-l keys 1–8, renders the form (the
    // Description textarea + the optional ContactEmail email input + the
    // submit button) POSTing to /issues/new with the anti-forgery token,
    // and renders the issue.thanks confirmation in place of the form when
    // Model.FormSubmitted is true.

    [Fact(DisplayName = "M32_4_Issue_Page_Shows_Issue_Form")]
    public void M32_4_Issue_Page_Shows_Issue_Form()
    {
        // (a) The GET action renders the issue form (a ViewResult with a
        // fresh IssueFormModel, FormSubmitted false — the form branch; the
        // page is public — no [Authorize] gate, M32·4).
        var controller = Build(user: AnonymousPrincipal());
        var action = controller.GetNew();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<IssueFormModel>(view.ViewData.Model);
        Assert.False(vm.FormSubmitted);
        Assert.Equal("", vm.Description);

        // (b) The view source (the structural string pin — the house
        // "string pin, no TestServer" idiom) resolves the closed issue.*
        // kw-l key set 1–8 (M32·9), renders the form (the two bind-model
        // fields + the submit), POSTs to the M32 submission route with the
        // anti-forgery token, and renders the issue.thanks confirmation in
        // the FormSubmitted branch.
        var src = LoadIssueViewSource();

        // The kw-l keys 1–8 (resolved server-side via the house seam).
        Assert.Contains("issue.title", src);
        Assert.Contains("issue.intro", src);
        Assert.Contains("issue.description.label", src);
        Assert.Contains("issue.description.placeholder", src);
        Assert.Contains("issue.email.label", src);
        Assert.Contains("issue.email.placeholder", src);
        Assert.Contains("issue.submit", src);
        Assert.Contains("issue.thanks", src);

        // The form binds to the two resident-supplied fields (explicit name
        // attributes — the view's @model is IssueFormModel).
        Assert.Contains("name=\"Description\"", src);
        Assert.Contains("name=\"ContactEmail\"", src);
        Assert.Contains("type=\"email\"", src);

        // The form POSTs to the M32 submission route with the anti-forgery
        // token (the house [ValidateAntiForgeryToken] idiom).
        Assert.Contains("asp-action=\"PostNew\"", src);
        Assert.Contains("@Html.AntiForgeryToken()", src);

        // The confirmation branch (issue.thanks in place of the form when
        // Model.FormSubmitted — no redirect, no modal, M32·4).
        Assert.Contains("@if (Model.FormSubmitted)", src);
        Assert.Contains("alert alert-success", src);

        // The form is public — NOT role-gated (M32·4 — the M31·2 precedent):
        // no role-claim read (IsGlobalAdmin / IsInRole /
        // KumunitaPrincipal.Is*) guards the form on this page (the
        // anonymous-safe pin). Asserting the role-gate absence rather than
        // the literal "[Authorize]" string (the view's own documentation
        // comment mentions "[Authorize]").
        Assert.DoesNotContain("IsGlobalAdmin", src);
        Assert.DoesNotContain("IsInRole", src);
        Assert.DoesNotContain("KumunitaPrincipal.Is", src);
    }

    // ── 8 — a signed-in resident's POST creates the row Origin=general
    //      (M32·4) ─────────────────────────────────────────────────────────
    // FACES M32-1: on a valid model the action calls the one CreateAsync
    // write lane with the draft (SubjectId set, Origin "general",
    // ExceptionType null), then re-renders with FormSubmitted = true (the
    // issue.thanks confirmation — no redirect). The audit-row assertion
    // (exactly one AccessAudit row, Via = Resident) lives in the Core
    // ErrorReportResolveTests (the service owns it).

    [Fact(DisplayName = "M32_4_Issue_Post_SignedIn_Creates_ErrorReport_Origin_General")]
    public async Task M32_4_Issue_Post_SignedIn_Creates_ErrorReport_Origin_General()
    {
        var svc = Substitute.For<IErrorReportService>();
        var stored = new ErrorReport { Id = "er-m32-8", TriageStatus = "new", Origin = "general" };
        svc.CreateAsync(Arg.Any<ErrorReportDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stored));

        var controller = Build(svc, user: ResidentPrincipal("sub-resident-m32-8"));
        controller.ModelState.Clear();

        var form = new IssueFormModel
        {
            Description = "The group calendar is down",
            ContactEmail = "resident@example.com",
        };

        var action = await controller.PostNew(form);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<IssueFormModel>(view.ViewData.Model);
        Assert.True(vm.FormSubmitted); // the issue.thanks confirmation (M32·4)

        // The one write lane was called exactly once with the draft
        // projected from the form + the request context: the signed-in
        // SubjectId (FACES M32-1), Origin "general" (the M32·4 pin),
        // ExceptionType null (a general issue is not tied to an error page).
        await svc.Received(1).CreateAsync(Arg.Is<ErrorReportDraft>(d =>
            d.SubjectId == "sub-resident-m32-8" &&
            d.Description == "The group calendar is down" &&
            d.ContactEmail == "resident@example.com" &&
            d.Origin == "general" &&
            d.ExceptionType == null), Arg.Any<CancellationToken>());
    }

    // ── 9 — an anonymous visitor's POST creates the row Origin=general
    //      (M32·4) ─────────────────────────────────────────────────────────
    // FACES M32-2: the anonymous visitor's SubjectId is the empty string;
    // the audit-row assertion (exactly one AccessAudit row, Via = Anonymous)
    // lives in the Core ErrorReportResolveTests (the service owns it).

    [Fact(DisplayName = "M32_4_Issue_Post_Anonymous_Creates_ErrorReport_Origin_General")]
    public async Task M32_4_Issue_Post_Anonymous_Creates_ErrorReport_Origin_General()
    {
        var svc = Substitute.For<IErrorReportService>();
        var stored = new ErrorReport { Id = "er-m32-9", TriageStatus = "new", Origin = "general" };
        svc.CreateAsync(Arg.Any<ErrorReportDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stored));

        var controller = Build(svc, user: AnonymousPrincipal()); // SubjectId = "" (anonymous)
        controller.ModelState.Clear();

        var form = new IssueFormModel
        {
            Description = "I was trying to RSVP for the Saturday event and the page went blank",
            ContactEmail = "anonymous@example.com",
        };

        var action = await controller.PostNew(form);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<IssueFormModel>(view.ViewData.Model);
        Assert.True(vm.FormSubmitted);

        // The one write lane was called exactly once with the draft: the
        // anonymous SubjectId "" (FACES M32-2) + Origin "general" (M32·4) +
        // ExceptionType null.
        await svc.Received(1).CreateAsync(Arg.Is<ErrorReportDraft>(d =>
            d.SubjectId == string.Empty &&
            d.Description == "I was trying to RSVP for the Saturday event and the page went blank" &&
            d.ContactEmail == "anonymous@example.com" &&
            d.Origin == "general" &&
            d.ExceptionType == null), Arg.Any<CancellationToken>());
    }

    // ── 10 — a blank description is a 400 re-render, never a 500 (M32·4) ─
    // FACES M32-1 (the validation path): on !ModelState.IsValid the action
    // re-renders the form pre-filled (the resident's typed values preserved)
    // with FormSubmitted = false, and does NOT call the write lane — a
    // form-level validation error is a 400 re-render, not a crash (M32·4).

    [Fact(DisplayName = "M32_4_Issue_Post_Validation_BlankDescription_Renders_Error")]
    public async Task M32_4_Issue_Post_Validation_BlankDescription_Renders_Error()
    {
        var svc = Substitute.For<IErrorReportService>();
        var controller = Build(svc, user: AnonymousPrincipal());

        // A blank description → the [Required] validation fails (the house
        // [Required] + ModelState idiom). The resident typed an email but no
        // description (the echo-back must preserve it).
        controller.ModelState.AddModelError("Description", "Description is required.");

        var form = new IssueFormModel
        {
            Description = "",
            ContactEmail = "resident@example.com",
        };

        var action = await controller.PostNew(form);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<IssueFormModel>(view.ViewData.Model);

        // A 400 re-render, never a 500: the form is re-shown (FormSubmitted
        // false) with the resident's already-typed values preserved.
        Assert.False(vm.FormSubmitted);
        Assert.Equal("", vm.Description);
        Assert.Equal("resident@example.com", vm.ContactEmail);

        // The write lane was NOT called (the blank description never reaches
        // CreateAsync — the M32·4 "never a 500 back to the resident" pin).
        await svc.DidNotReceive().CreateAsync(
            Arg.Any<ErrorReportDraft>(), Arg.Any<CancellationToken>());
    }

    // ── 11 — the post-submission confirmation is visible (M32·4) ──────────
    // FACES M32-1: after a successful submit (a non-null stored row), the
    // action re-renders the view with FormSubmitted = true — the
    // issue.thanks confirmation is visible (the view renders the key in
    // place of the form), no redirect, no modal. The audit-row assertion
    // lives in the Core ErrorReportResolveTests (the service owns it).

    [Fact(DisplayName = "M32_4_Issue_Post_Confirmation_Visible")]
    public async Task M32_4_Issue_Post_Confirmation_Visible()
    {
        var svc = Substitute.For<IErrorReportService>();
        var stored = new ErrorReport { Id = "er-m32-11", TriageStatus = "new", Origin = "general" };
        svc.CreateAsync(Arg.Any<ErrorReportDraft>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(stored));

        var controller = Build(svc, user: ResidentPrincipal("sub-resident-m32-11")); // signed-in
        controller.ModelState.Clear();

        var form = new IssueFormModel
        {
            Description = "The community page timed out",
            ContactEmail = null,
        };

        var action = await controller.PostNew(form);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<IssueFormModel>(view.ViewData.Model);

        // The confirmation is visible (FormSubmitted true → the view renders
        // the issue.thanks key in place of the form — no redirect).
        Assert.True(vm.FormSubmitted);

        // The write lane was called with the signed-in SubjectId + a null
        // ContactEmail (the null form field maps to null in the draft) +
        // Origin "general" (the M32·4 pin).
        await svc.Received(1).CreateAsync(Arg.Is<ErrorReportDraft>(d =>
            d.SubjectId == "sub-resident-m32-11" &&
            d.Description == "The community page timed out" &&
            d.ContactEmail == null &&
            d.Origin == "general"), Arg.Any<CancellationToken>());
    }
}
