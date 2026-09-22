using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Marten;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies the <see cref="AccountController.Signup"/> gate (ADR 0050) — the
/// admin-settled instance value (<c>IsSignupOpenAsync</c>, the <c>true</c>
/// floor) that flips the self-service sign-up surface between <b>open</b> (the
/// form + the <c>RegisterAsync</c> write) and <b>invitation-only</b> (the static
/// <c>SignupClosed</c> notice, and the write refused).
/// <list type="bullet">
/// <item><b>GET, closed</b> — the <c>SignupClosed</c> view is returned; no
/// account-creation lane is invoked.</item>
/// <item><b>GET, open</b> — the signup <c>View</c> model is returned.</item>
/// <item><b>POST, closed</b> — the <c>SignupClosed</c> view is returned;
/// <c>RegisterAsync</c> is NOT invoked (the gate is authoritative on the write
/// path too, not just the hidden form).</item>
/// <item><b>POST, open</b> — the gate is passed (the model survives as the
/// signup form, not <c>SignupClosed</c>).</item>
/// </list>
/// </summary>
public class AccountControllerSignupGateTests
{
    // ── GET gate ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Signup_GET_Closed_ReturnsSignupClosedView()
    {
        var (controller, identity) = Build(isOpen: false);

        var action = await controller.Signup();
        var view = Assert.IsType<ViewResult>(action);
        Assert.Equal("SignupClosed", view.ViewName);
        Assert.IsType<SignupClosedViewModel>(view.ViewData.Model);

        // The gate blocks the surface — no account-creation lane is even reached.
        await identity.DidNotReceiveWithAnyArgs()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Signup_GET_Open_ReturnsSignupForm()
    {
        var (controller, _) = Build(isOpen: true);

        var action = await controller.Signup();
        var view = Assert.IsType<ViewResult>(action);
        Assert.IsType<SignupViewModel>(view.ViewData.Model);
    }

    // ── POST gate ────────────────────────────────────────────────────────

    [Fact]
    public async Task Signup_POST_Closed_ReturnsSignupClosedView_AndDoesNotRegister()
    {
        var (controller, identity) = Build(isOpen: false);
        // Seed one error so that, were the gate ever passed, validation would
        // fail before RegisterAsync (the assertion is the gate, not the form).
        controller.ModelState.AddModelError("Email", "never reached");

        var model = new SignupViewModel
        {
            DisplayName = "Invited",
            Email = "invited@example.org",
            Password = "correct-horse-1",
        };

        var action = await controller.Signup(model);
        var view = Assert.IsType<ViewResult>(action);
        Assert.Equal("SignupClosed", view.ViewName);
        Assert.IsType<SignupClosedViewModel>(view.ViewData.Model);

        await identity.DidNotReceiveWithAnyArgs()
            .RegisterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Signup_POST_Open_PassesGate()
    {
        var (controller, _) = Build(isOpen: true);
        // Seed one error so the action returns at the validation step (after the
        // gate) — proving the gate let the write path through, without invoking
        // RegisterAsync.
        controller.ModelState.AddModelError("Email", "seeded");

        var model = new SignupViewModel
        {
            DisplayName = "Resident",
            Email = "resident@example.org",
            Password = "correct-horse-1",
        };

        var action = await controller.Signup(model);
        var view = Assert.IsType<ViewResult>(action);
        // The gate is open: the model is still the signup form (not SignupClosed).
        Assert.IsType<SignupViewModel>(view.ViewData.Model);
    }

    // ── Harness ──────────────────────────────────────────────────────────

    private static (AccountController controller, IIdentityService identity) Build(bool isOpen)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.IsSignupOpenAsync().Returns(isOpen);

        // The gate's lane reads only IsSignupOpenAsync and returns a view — it
        // never touches the Identity seams (userManager / signInManager are used
        // only on the Verify handoff lane, which these tests don't drive). Both are
        // concrete classes (NSubstitute's Castle proxy needs a parameterless ctor
        // they lack), so pass null — the null reference is never dereferenced on
        // this path.
        var controller = new AccountController(
            signInManager: null!,
            userManager:   null!,
            identity:      identity,
            userInfo:      Substitute.For<IUserInfoService>(),
            store:         Substitute.For<IDocumentStore>());

        // The Signup GET path reads `User.Identity?.IsAuthenticated` (the
        // already-signed-in redirect) before the gate, so a controller context
        // with an anonymous principal is required; the POST path hits the gate
        // first and never reaches `User` on the closed branch.
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() },
        };

        return (controller, identity);
    }
}
