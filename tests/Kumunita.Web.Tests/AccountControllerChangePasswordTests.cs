using Kumunita.Core.Identity;
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
using System.Security.Claims;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies the <see cref="SecuritySettingsController.ChangePassword"/>
/// surface (ADR 0138, now co-located with MFA on the Security tab — moved
/// from <see cref="AccountController"/> 2026-10-31). The pin:
/// <list type="bullet">
/// <item><b>GET (Mfa, unlocked)</b> — the <c>Mfa</c> surface carries
/// <c>PasswordChangeLocked == false</c> (the form will render).</item>
/// <item><b>GET (Mfa, locked)</b> — the <c>Mfa</c> surface carries
/// <c>PasswordChangeLocked == true</c> (a locked, non-admin sample account on
/// an opted-in demo instance; the _PasswordChange partial renders the
/// "password changes are locked" notice instead of the form).</item>
/// <item><b>POST, locked</b> — the guard is authoritative on the write path
/// too: a redirect to <c>Mfa</c> is returned; <c>ChangePasswordAsync</c> and
/// <c>CheckPasswordAsync</c> are NOT invoked (a crafted POST is refused).</item>
/// <item><b>POST, unlocked, wrong current password</b> — a redirect to
/// <c>Mfa</c> is returned (the flash carries the error);
/// <c>ChangePasswordAsync</c> is NOT invoked.</item>
/// <item><b>POST, unlocked, correct current password</b> —
/// <c>ChangePasswordAsync(subject, new, byAdmin: false)</c> is invoked (the
/// single audited write lane), the resident is signed out, and the action
/// redirects to the login surface.</item>
/// </list>
/// Mirrors the <see cref="SecuritySettingsMfaLockTests"/> harness shape (the
/// SecuritySettingsController constructor + principal from raw claims) — the
/// audit-row assertion lives in the Core tests (the service owns it), so this
/// is a thin-seam test.
/// </summary>
public class AccountControllerChangePasswordTests
{
    private const string Subject = "subj-change-password-001";

    private static (SecuritySettingsController controller, IIdentityService identity, UserManager<User> userManager, SignInManager<User> signInManager) Build(
        bool passwordLocked = false, bool mfaLocked = false)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.IsChangePasswordLockedForAsync(Subject).Returns(passwordLocked);
        identity.IsMfaLockedForAsync(Subject).Returns(mfaLocked);

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

        var signInManager = Substitute.For<SignInManager<User>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>());

        var controller = new SecuritySettingsController(
            userManager:   userManager,
            signInManager: signInManager,
            identity:      identity);

        // The change-password actions read the signed-in principal (never a
        // path param) — a principal carrying the Kumunita.Sub subject claim
        // (the KumunitaPrincipal.SubjectId reader; == User.Id).
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Subject) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(
            new DefaultHttpContext(), new NoOpTempDataProvider());

        return (controller, identity, userManager, signInManager);
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

    // ── GET (the Mfa surface carries the PasswordChangeLocked flag) ─────────

    [Fact]
    public async Task Mfa_GET_PasswordUnlocked_PasswordChangeNotLocked()
    {
        var (controller, _, _, _) = Build(passwordLocked: false);

        var action = await controller.Mfa();
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<MfaSettingsViewModel>(view.ViewData.Model);
        Assert.False(model.PasswordChangeLocked);
    }

    [Fact]
    public async Task Mfa_GET_PasswordLocked_PasswordChangeLocked()
    {
        // Password-locked but MFA-unlocked: the Mfa() GET returns the
        // MfaSettingsViewModel with PasswordChangeLocked == true (the
        // _PasswordChange partial renders the "password changes are locked"
        // notice in place of the form — the ADR 0138 surface).
        var (controller, _, userManager, _) = Build(passwordLocked: true, mfaLocked: false);
        // ResolveUserAsync (FindByIdAsync) runs before the lock read — stub a
        // resolvable account so the lock seam is actually consulted.
        userManager.FindByIdAsync(Subject).Returns(new User { Id = Subject, Email = Subject + "@ex.net", UserName = Subject });

        var action = await controller.Mfa();
        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<MfaSettingsViewModel>(view.ViewData.Model);
        Assert.True(model.PasswordChangeLocked);
    }

    [Fact]
    public async Task Mfa_GET_MfaLockedAndPasswordLocked_MfaLockedCarriesFlag()
    {
        // MFA-locked (renders the MfaLocked notice) AND password-locked: the
        // MfaLockedViewModel carries PasswordChangeLocked == true (so the
        // co-located change-password section on the locked-MFA page shows the
        // "password changes are locked" notice too).
        var (controller, _, userManager, _) = Build(passwordLocked: true, mfaLocked: true);
        // ResolveUserAsync (FindByIdAsync) runs before the MFA-lock read — stub
        // a resolvable account so the lock seam is actually consulted.
        userManager.FindByIdAsync(Subject).Returns(new User { Id = Subject, Email = Subject + "@ex.net", UserName = Subject });

        var action = await controller.Mfa();
        var view = Assert.IsType<ViewResult>(action);
        Assert.Equal("MfaLocked", view.ViewName);
        var model = Assert.IsType<MfaLockedViewModel>(view.ViewData.Model);
        Assert.True(model.PasswordChangeLocked);
    }

    // ── POST ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ChangePassword_POST_Locked_RedirectsToMfa_AndDoesNotWrite()
    {
        // The guard is authoritative on the write path too: a locked,
        // non-admin sample account is denied with a redirect — no
        // current-password check, no write (a crafted POST is refused).
        var (controller, identity, userManager, _) = Build(passwordLocked: true);
        // ResolveUserAsync (FindByIdAsync) runs before the lock check — stub a
        // resolvable account so the lock branch is actually reached.
        var user = new User { Id = Subject, Email = Subject + "@ex.net", UserName = Subject };
        userManager.FindByIdAsync(Subject).Returns(user);

        var model = ValidModel();
        var action = await controller.ChangePassword(model);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(SecuritySettingsController.Mfa), redirect.ActionName);

        await identity.DidNotReceiveWithAnyArgs()
            .ChangePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
        await userManager.DidNotReceiveWithAnyArgs()
            .CheckPasswordAsync(Arg.Any<User>(), Arg.Any<string>());
    }

    [Fact]
    public async Task ChangePassword_POST_Unlocked_WrongCurrentPassword_RedirectsToMfa_AndDoesNotWrite()
    {
        var (controller, identity, userManager, _) = Build(passwordLocked: false);
        var user = new User { Id = Subject, Email = Subject + "@ex.net", UserName = Subject };
        userManager.FindByIdAsync(Subject).Returns(user);
        userManager.CheckPasswordAsync(user, "wrong-current-pw").Returns(false);

        var action = await controller.ChangePassword(ValidModel(current: "wrong-current-pw"));
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(SecuritySettingsController.Mfa), redirect.ActionName);

        await identity.DidNotReceiveWithAnyArgs()
            .ChangePasswordAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task ChangePassword_POST_Unlocked_CorrectCurrentPassword_Writes_SignsOut_AndRedirectsToLogin()
    {
        var (controller, identity, userManager, signInManager) = Build(passwordLocked: false);
        var user = new User { Id = Subject, Email = Subject + "@ex.net", UserName = Subject };
        userManager.FindByIdAsync(Subject).Returns(user);
        userManager.CheckPasswordAsync(user, "current-pw").Returns(true);

        var action = await controller.ChangePassword(ValidModel(current: "current-pw"));
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);

        // The single audited write lane (the self-serve lane, byAdmin: false)
        // is invoked with the subject minted from the principal (never a path
        // param — User.Id == the Kumunita.Sub claim value).
        await identity.Received(1).ChangePasswordAsync(Subject, "new-pw-1", byAdmin: false);
        await signInManager.Received(1).SignOutAsync();
    }

    private static ChangePasswordViewModel ValidModel(string current = "current-pw")
        => new()
        {
            CurrentPassword = current,
            NewPassword = "new-pw-1",
            ConfirmNewPassword = "new-pw-1",
        };
}
