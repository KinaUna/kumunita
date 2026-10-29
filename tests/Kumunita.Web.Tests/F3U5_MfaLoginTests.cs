using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Marten;

namespace Kumunita.Web.Tests;

/// <summary>
/// F3 · U5 — the three pin §7 pinned tests for the <c>GlobalAdmin</c> MFA
/// enforcement seam (F3.U4's <see cref="AccountController.Login"/> /
/// <see cref="AccountController.LoginMfa"/> actions).
///
/// Drives the **public** controller actions (no private helpers made public).
/// The harness mirrors <see cref="AccountControllerChangePasswordTests"/>
/// (substituted <c>SignInManager&lt;User&gt;</c> — the sign-in-leg returns are
/// scripted to the <c>Microsoft.AspNetCore.Identity.SignInResult</c> states the
/// pin §7 names) + the F1 <c>IdentityServiceDeleteLaneGdprTests</c> (real
/// <c>UserManager&lt;User&gt;</c> over an EF <c>AppDbContext</c>, the
/// <see cref="PostgresFixture"/> shape) for the role seeding + the
/// recovery-code count assertion.
///
/// <b>Pin §7 test names (verbatim):</b>
/// <list type="number">
/// <item><see cref="GlobalAdminLogin_RequiresMfaChallenge"/> — proves §5 Leg 1.5.</item>
/// <item><see cref="GlobalAdminRecoveryCode_WorksOnce"/> — proves §6.</item>
/// <item><see cref="NonAdmin_MfaOptional"/> — proves §5 Leg 1.5c.</item>
/// </list>
/// </summary>
public sealed class F3U5_MfaLoginTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password  = "Sup3rSecret!";
    private const string AdminEmail  = "mfa-admin@examplium.com";
    private const string MemberEmail = "mfa-member@examplium.com";

    // ── Pin §7 test 1 (§5 Leg 1.5) ─────────────────────────────────────────

    /// <summary>
    /// A <c>GlobalAdmin</c> with <c>TwoFactorEnabled == true</c> posts a correct
    /// password to <see cref="AccountController.Login"/>. Asserts the response
    /// is the **challenge** (a <see cref="ViewResult"/> with
    /// <c>MfaPending == true</c>, NOT a redirect); then a correct TOTP code
    /// completes the sign-in. Proves pin §5 Leg 1.5.
    /// </summary>
    [Fact]
    public async Task GlobalAdminLogin_RequiresMfaChallenge()
    {
        var h = await BuildAsync(globalAdmin: true, mfaEnabled: true);

        // Leg 1 — MFA-on: the password leg returns TwoFactorRequired
        // (Succeeded=false, RequiresTwoFactor=true — the pin §7 state).
        h.SignInManager
            .PasswordSignInAsync(h.User!, Password, isPersistent: false, lockoutOnFailure: true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.TwoFactorRequired);

        var model = new LoginViewModel { Email = AdminEmail, Password = Password };
        var action = await h.Controller.Login(model);

        // The response is the **challenge** (a View with MfaPending == true),
        // NOT a redirect to ReturnUrl (proves §5 Leg 1.5).
        var view = Assert.IsType<ViewResult>(action);
        var loginModel = Assert.IsType<LoginViewModel>(view.ViewData.Model);
        Assert.True(loginModel.MfaPending);
        Assert.Equal(AdminEmail, loginModel.Email);

        // A correct TOTP code completes the sign-in.
        h.SignInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var mfaModel = new LoginViewModel { MfaCode = "123456" };
        var redirect = Assert.IsType<RedirectResult>(await h.Controller.LoginMfa(mfaModel));
        Assert.Equal("/", redirect.Url);   // ADR 0234 — Home
    }

    // ── Pin §7 test 2 (§6) ─────────────────────────────────────────────────

    /// <summary>
    /// A <c>GlobalAdmin</c> at the challenge leg presents a **recovery code**.
    /// Asserts (a) the first presentation **succeeds** and completes sign-in;
    /// (b) the **same** code presented again is **rejected** (count drops by 1
    /// via <c>CountRecoveryCodesAsync</c>). Proves pin §6.
    /// </summary>
    [Fact]
    public async Task GlobalAdminRecoveryCode_WorksOnce()
    {
        var h = await BuildAsync(globalAdmin: true, mfaEnabled: true);
        Assert.False(string.IsNullOrEmpty(h.RecoveryCode));
        var firstCode = h.RecoveryCode!;

        // (a) First presentation succeeds.
        h.SignInManager
            .TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(
                Microsoft.AspNetCore.Identity.SignInResult.Success,
                Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var firstAction = await h.Controller.LoginMfa(new LoginViewModel { MfaRecoveryCode = firstCode });
        var firstRedirect = Assert.IsType<RedirectResult>(firstAction);
        Assert.Equal("/settings/security/mfa", firstRedirect.Url);   // ADR 0234 — recovery → MFA settings

        // The "redeem under the hood" (the real <c>SignInManager</c> calls
        // <c>UserManager.RedeemTwoFactorRecoveryCodeAsync</c> internally, pin
        // §6.2) — simulate it here so the count assertion is against real DB
        // state (the substituted <c>SignInManager</c> does not perform the
        // redeem; the test asserts the *effect*, not the *mechanism*).
        await h.UserManager.RedeemTwoFactorRecoveryCodeAsync(h.User!, firstCode);
        Assert.Equal(9, await h.UserManager.CountRecoveryCodesAsync(h.User!));

        // (b) The same code presented again is rejected.
        var secondAction = await h.Controller.LoginMfa(new LoginViewModel { MfaRecoveryCode = firstCode });
        var secondView = Assert.IsType<ViewResult>(secondAction);
        var secondModel = Assert.IsType<LoginViewModel>(secondView.ViewData.Model);
        Assert.True(secondModel.MfaPending);

        // The count is unchanged (the code was already consumed).
        Assert.Equal(9, await h.UserManager.CountRecoveryCodesAsync(h.User!));
    }

    // ── ADR 0214 — a wrong code keeps the pending sign-in ──────────────────

    /// <summary>
    /// A wrong authenticator code re-renders the challenge **without** signing out:
    /// the pending <c>TwoFactorUserId</c> cookie must survive so a retry can succeed
    /// (the old <c>SignOutAsync</c> made every retry fail). The typed code is cleared.
    /// </summary>
    [Fact]
    public async Task WrongTotpCode_KeepsPendingSignIn_AndClearsTheField()
    {
        var h = await BuildAsync(globalAdmin: true, mfaEnabled: true);
        h.SignInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var action = await h.Controller.LoginMfa(new LoginViewModel { MfaCode = "000000" });

        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<LoginViewModel>(view.ViewData.Model);
        Assert.True(model.MfaPending);
        Assert.Null(model.MfaCode);
        await h.SignInManager.DidNotReceive().SignOutAsync();
    }

    /// <summary>
    /// A lockout ends the challenge: the pending cookie is cleared and the password
    /// form (not the code form) is shown with a lockout message.
    /// </summary>
    [Fact]
    public async Task LockedOutTotpCode_SignsOut_AndReturnsToThePasswordForm()
    {
        var h = await BuildAsync(globalAdmin: true, mfaEnabled: true);
        h.SignInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var action = await h.Controller.LoginMfa(new LoginViewModel { MfaCode = "000000" });

        var view = Assert.IsType<ViewResult>(action);
        var model = Assert.IsType<LoginViewModel>(view.ViewData.Model);
        Assert.False(model.MfaPending);
        await h.SignInManager.Received(1).SignOutAsync();
    }

    // ── Pin §7 test 3 (§5 Leg 1.5c) ────────────────────────────────────────

    /// <summary>
    /// A non-<c>GlobalAdmin</c> (e.g. <c>Member</c>) with
    /// <c>TwoFactorEnabled == false</c> posts a correct password. Asserts
    /// sign-in completes **directly** (a redirect, no challenge) — MFA is
    /// **optional** for them. Proves pin §5 Leg 1.5c.
    /// </summary>
    [Fact]
    public async Task NonAdmin_MfaOptional()
    {
        var h = await BuildAsync(globalAdmin: false, mfaEnabled: false);

        // Leg 1 — MFA-off: the password leg returns Success
        // (Succeeded=true, RequiresTwoFactor=false — the pin §7 state).
        h.SignInManager
            .PasswordSignInAsync(h.User!, Password, isPersistent: false, lockoutOnFailure: true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Leg 1.5c — the persistent sign-in also returns Success.
        h.SignInManager
            .PasswordSignInAsync(h.User!, Password, isPersistent: true, lockoutOnFailure: true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var model = new LoginViewModel { Email = MemberEmail, Password = Password };
        var action = await h.Controller.Login(model);

        // Sign-in completes **directly** (a redirect, no challenge) —
        // MFA is optional for a non-GlobalAdmin (proves §5 Leg 1.5c).
        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal("/", redirect.Url);   // ADR 0234 — Home
    }

    // ── ADR 0194 — the sample-admin login-enrollment exemption ─────────────

    /// <summary>
    /// ADR 0194 — the sample-admin login-exemption (leg 1.5b). A
    /// <c>GlobalAdmin</c> whose account is a sample account (a member of the
    /// closed <c>SampleAccountEmails</c> set, on a <c>SampleData__Enabled</c>
    /// instance) with <c>TwoFactorEnabled == false</c> posts a correct
    /// password to <see cref="AccountController.Login"/>. Asserts the
    /// response is a <b>direct sign-in</b> (a <see cref="RedirectResult"/>
    /// to <c>/</c> (Home), the leg 1.5c path) — NOT the enrollment
    /// redirect to <c>/settings/security/mfa</c> (leg 1.5b). The exemption
    /// reads the <c>IsSampleDataAccountAsync</c> seam (conjuncts 1+3 only —
    /// the admin-toggle gate is NOT consulted, so a locked instance still
    /// lets its admin sign in). Proves the ADR 0194 login-exemption.
    /// </summary>
    [Fact]
    public async Task SampleAdminLogin_SkipsForcedMfaEnrollment()
    {
        var h = await BuildAsync(globalAdmin: true, mfaEnabled: false, sampleDataAccount: true);

        // Leg 1 — MFA-off: the password leg returns Success
        // (Succeeded=true, RequiresTwoFactor=false — the pin §7 state).
        h.SignInManager
            .PasswordSignInAsync(h.User!, Password, isPersistent: false, lockoutOnFailure: true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Leg 1.5c — the persistent sign-in also returns Success.
        h.SignInManager
            .PasswordSignInAsync(h.User!, Password, isPersistent: true, lockoutOnFailure: true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var model = new LoginViewModel { Email = AdminEmail, Password = Password };
        var action = await h.Controller.Login(model);

        // Sign-in completes **directly** (a redirect to Home, the
        // leg 1.5c path) — NOT the enrollment redirect to /settings/security/mfa
        // (leg 1.5b is skipped by the ADR 0194 exemption).
        var redirect = Assert.IsType<RedirectResult>(action);
        Assert.Equal("/", redirect.Url);   // ADR 0234 — Home

        // The IsSampleDataAccountAsync seam was consulted (the exemption read).
        await h.Identity
            .Received(1)
            .IsSampleDataAccountAsync(Arg.Any<string>());
    }

    /// <summary>
    /// ADR 0194 — the control: a <b>non-sample</b> <c>GlobalAdmin</c> with
    /// <c>TwoFactorEnabled == false</c> posts a correct password. Asserts
    /// the response is the <b>enrollment redirect</b> (a
    /// <see cref="RedirectToActionResult"/> to the <c>Mfa</c> action of
    /// <c>SecuritySettings</c>, the leg 1.5b path) — the ADR 0194 exemption
    /// does NOT fire for a non-sample account. Proves the exemption is
    /// scoped to sample accounts only (a real GlobalAdmin still must enroll).
    /// </summary>
    [Fact]
    public async Task NonSampleGlobalAdminLogin_StillForcedToMfaEnrollment()
    {
        var h = await BuildAsync(globalAdmin: true, mfaEnabled: false, sampleDataAccount: false);

        // Leg 1 — MFA-off: the password leg returns Success.
        h.SignInManager
            .PasswordSignInAsync(h.User!, Password, isPersistent: false, lockoutOnFailure: true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var model = new LoginViewModel { Email = AdminEmail, Password = Password };
        var action = await h.Controller.Login(model);

        // The enrollment redirect (leg 1.5b fires — the exemption is false).
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal("Mfa", redirect.ActionName);
        Assert.Equal("SecuritySettings", redirect.ControllerName);

        // The IsSampleDataAccountAsync seam was consulted (and returned false).
        await h.Identity
            .Received(1)
            .IsSampleDataAccountAsync(Arg.Any<string>());
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private sealed record Harness
    {
        public required AccountController   Controller  { get; init; }
        public required UserManager<User>   UserManager { get; init; }
        public required SignInManager<User> SignInManager { get; init; }
        public required IIdentityService    Identity    { get; init; }
        public required DbContextOptions<AppDbContext> DbOptions { get; init; }
        public required User?               User        { get; init; }
        public string?                      RecoveryCode { get; init; }
    }

    private async Task<Harness> BuildAsync(bool globalAdmin, bool mfaEnabled, bool sampleDataAccount = false)
    {
        var ct   = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.MigrateAsync(ct);
        }

        // Real UserManager over the EF store (the F1 precedent — the
        // IdentityServiceDeleteLaneGdprTests.BootAsync shape).
        var userStore = new UserStore<User, IdentityRole, AppDbContext, string,
            IdentityUserClaim<string>, IdentityUserRole<string>,
            IdentityUserLogin<string>, IdentityUserToken<string>,
            IdentityRoleClaim<string>>(new AppDbContext(dbOptions));

        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        // Seed the user.
        var email = globalAdmin ? AdminEmail : MemberEmail;
        var user  = new User { Id = Guid.NewGuid().ToString("N"), Email = email, UserName = email };
        await userManager.CreateAsync(user, Password);

        if (globalAdmin)
        {
            await using var db2 = new AppDbContext(dbOptions);
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                    .AnyAsync(db2.Roles.Where(r => r.Name == Roles.GlobalAdmin), ct))
            {
                db2.Roles.Add(new IdentityRole
                {
                    Name           = Roles.GlobalAdmin,
                    NormalizedName = Roles.GlobalAdmin.ToUpperInvariant()
                });
                await db2.SaveChangesAsync(ct);
            }
            await userManager.AddToRoleAsync(user, Roles.GlobalAdmin);
        }

        string? recoveryCode = null;
        if (mfaEnabled)
        {
            await userManager.SetTwoFactorEnabledAsync(user, true);
            userManager.GenerateNewAuthenticatorKey();
            var codes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10))?.ToArray();
            recoveryCode = codes?.FirstOrDefault();
        }

        // Substituted SignInManager (the AccountControllerChangePasswordTests
        // convention — the sign-in-leg returns are scripted to the pin §7
        // SignInResult states).
        var signInManager = Substitute.For<SignInManager<User>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>());

        // Controller (the 5-arg ctor — the F3.U4 seam).
        // ADR 0194 — the IsSampleDataAccountAsync seam is scripted per the
        // sampleDataAccount parameter (the login-exemption read). The
        // default NSubstitute return (false) is the non-sample control; the
        // sample-admin test flips it to true.
        var identity = Substitute.For<IIdentityService>();
        identity.IsSampleDataAccountAsync(Arg.Any<string>()).Returns(sampleDataAccount);

        var controller = new AccountController(
            signInManager: signInManager,
            userManager:   userManager,
            identity:      identity,
            userInfo:      Substitute.For<IUserInfoService>(),
            store:         Substitute.For<IDocumentStore>());

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        // The Login / LoginMfa success path calls Url.IsLocalUrl (the
        // ReturnUrl guard) — wire a stub so the Url property resolves
        // (the KioskControllerTests.StubUrlHelper idiom).
        controller.Url = new StubUrlHelper(new ActionContext { HttpContext = httpContext });

        return new Harness
        {
            Controller   = controller,
            UserManager  = userManager,
            SignInManager = signInManager,
            Identity     = identity,
            DbOptions    = dbOptions,
            User         = user,
            RecoveryCode = recoveryCode,
        };
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context)
            => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
        }
    }

    /// <summary>
    /// A minimal <see cref="IUrlHelper"/> so <c>ControllerBase.Url</c> (and hence
    /// the success-path <c>Url.IsLocalUrl(...)</c>) resolves in the
    /// direct-construction harness (the <c>KioskControllerTests</c> idiom).
    /// <c>IsLocalUrl</c> returns <c>false</c> so the controller takes the
    /// <c>Redirect("/")</c> branch (the assertions' target).
    /// </summary>
    private sealed class StubUrlHelper : IUrlHelper
    {
        public StubUrlHelper(ActionContext actionContext)
        {
            ActionContext = actionContext;
        }

        public ActionContext ActionContext { get; }
        public string Action(UrlActionContext actionContext) => "stub-action";
        public string Content(string? content) => content ?? string.Empty;
        public bool IsLocalUrl(string? url) => false;
        public string RouteUrl(UrlRouteContext routeContext) => "stub-route";
        public string Link(string? linkName, object? values) => "stub-link";
    }
}
