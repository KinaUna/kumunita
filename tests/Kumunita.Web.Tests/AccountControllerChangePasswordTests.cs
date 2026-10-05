using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Marten;
using System.Security.Claims;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies the <see cref="AccountController.ChangePassword"/> surface (ADR
/// 0138) — the resident's self-serve change-password form. The pin:
/// <list type="bullet">
/// <item><b>GET, unlocked</b> — the change-password <c>View</c> model is
/// returned.</item>
/// <item><b>GET, locked</b> — the <c>ChangePasswordLocked</c> notice is
/// returned (a locked, non-admin sample account on an opted-in demo instance);
/// no identity write lane is even reached.</item>
/// <item><b>POST, locked</b> — the guard is authoritative on the write path
/// too: the <c>ChangePasswordLocked</c> notice is returned;
/// <c>ChangePasswordAsync</c> is NOT invoked (a crafted POST is refused).</item>
/// <item><b>POST, unlocked, wrong current password</b> — a form error is
/// re-rendered; <c>ChangePasswordAsync</c> is NOT invoked.</item>
/// <item><b>POST, unlocked, correct current password</b> —
/// <c>ChangePasswordAsync(subject, new, byAdmin: false)</c> is invoked (the
/// single audited write lane), the resident is signed out, and the action
/// redirects to the login surface.</item>
/// </list>
/// Mirrors <see cref="AccountControllerSignupGateTests"/> (the gate is
/// authoritative on both the hidden form and the write path) — the audit-row
/// assertion lives in the Core tests (the service owns it), so this is a thin-
/// seam test.
/// </summary>
public class AccountControllerChangePasswordTests
{
    private const string Subject = "subj-change-password-001";

    private static (AccountController controller, IIdentityService identity, UserManager<User> userManager) Build(
        bool locked = false)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.IsChangePasswordLockedForAsync(Subject).Returns(locked);

        var userManager = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        var controller = new AccountController(
            signInManager: Substitute.For<SignInManager<User>>(
                userManager,
                Substitute.For<IHttpContextAccessor>(),
                Substitute.For<IUserClaimsPrincipalFactory<User>>(),
                Options.Create(new IdentityOptions()),
                NullLogger<SignInManager<User>>.Instance,
                Substitute.For<IAuthenticationSchemeProvider>(),
                Substitute.For<IUserConfirmation<User>>()),
            userManager:   userManager,
            identity:      identity,
            userInfo:      Substitute.For<IUserInfoService>(),
            store:         Substitute.For<IDocumentStore>());

        // The change-password actions read the signed-in principal (never a
        // path param) — a principal carrying the Kumunita.Sub subject claim.
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Subject) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new NoOpTempDataProvider());

        return (controller, identity, userManager);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    // ── GET ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_GET_Unlocked_ReturnsForm()
    {
        var (controller, _, _) = Build(locked: false);

        var action = await controller.ChangePassword();
        var view = Assert.IsType<ViewResult>(action);
        Assert.IsType<ChangePasswordViewModel>(view.ViewData.Model);
    }

    [Fact]
    public async Task ChangePassword_GET_Locked_ReturnsLockedNotice()
    {
        var (controller, _, _) = Build(locked: true);

        var action = await controller.ChangePassword();
        var view = Assert.IsType<ViewResult>(action);
        Assert.Equal("ChangePasswordLocked", view.ViewName);
        Assert.IsType<ChangePasswordLockedViewModel>(view.ViewData.Model);
    }

    // ── POST ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_POST_Locked_ReturnsLockedNotice_AndDoesNotWrite()
    {
        // The guard is authoritative on the write path too: a locked,
        // non-admin sample account is denied with the static notice — no
        // current-password check, no write (a crafted POST is refused).
        var (controller, identity, userManager) = Build(locked: true);

        var model = ValidModel();
        var action = await controller.ChangePassword(model);
        var view = Assert.IsType<ViewResult>(action);
        Assert.Equal("ChangePasswordLocked", view.ViewName);

        await identity.DidNotReceiveWithAnyArgs()
            .ChangePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        await userManager.DidNotReceiveWithAnyArgs()
            .CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ChangePassword_POST_Unlocked_WrongCurrentPassword_ReRendersForm_AndDoesNotWrite()
    {
        var (controller, identity, userManager) = Build(locked: false);
        var user = new User { Id = Subject, Email = Subject + "@ex.net", UserName = Subject };
        userManager.FindByIdAsync(Subject).Returns(user);
        userManager.CheckPasswordAsync(user, "current-pw").Returns(false);

        var action = await controller.ChangePassword(ValidModel(current: "wrong-current-pw"));
        var view = Assert.IsType<ViewResult>(action);
        Assert.IsType<ChangePasswordViewModel>(view.ViewData.Model);

        await identity.DidNotReceiveWithAnyArgs()
            .ChangePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task ChangePassword_POST_Unlocked_CorrectCurrentPassword_Writes_AndRedirects()
    {
        var (controller, identity, userManager) = Build(locked: false);
        var user = new User { Id = Subject, Email = Subject + "@ex.net", UserName = Subject };
        userManager.FindByIdAsync(Subject).Returns(user);
        userManager.CheckPasswordAsync(user, "current-pw").Returns(true);

        var action = await controller.ChangePassword(ValidModel(current: "current-pw"));
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AccountController.Login), redirect.ActionName);

        // The single audited write lane (the self-serve lane, byAdmin: false)
        // is invoked with the subject minted from the principal (never a path
        // param).
        await identity.Received(1).ChangePasswordAsync(Subject, "new-pw-1", byAdmin: false);
    }

    private static ChangePasswordViewModel ValidModel(string current = "current-pw")
        => new()
        {
            CurrentPassword = current,
            NewPassword = "new-pw-1",
            ConfirmNewPassword = "new-pw-1",
        };
}
