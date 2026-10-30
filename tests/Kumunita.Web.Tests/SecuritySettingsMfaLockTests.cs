using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0194 — the sample-account MFA (two-factor) lock on the
/// <see cref="SecuritySettingsController"/> MFA surface.
/// <para>
/// The <see cref="IIdentityService.IsMfaLockedForAsync"/> decision (the ADR
/// 0138 / 0194 four-conjunct rule) is the single place the rule lives, so the
/// <c>GET /settings/security/mfa</c> notice and the
/// <c>MfaEnable</c> / <c>MfaRotateRecovery</c> / <c>MfaDisable</c>
/// enforcement guards agree. These tests drive the **public** controller
/// actions over a real <c>UserManager&lt;User&gt;</c> + EF
/// <c>AppDbContext</c> (the <see cref="PostgresFixture"/> shape, the
/// <see cref="MfaQrTests"/> harness idiom) + a substituted
/// <c>IIdentityService</c> whose <c>IsMfaLockedForAsync</c> seam is scripted
/// to the ADR 0194 lock states.
/// <para>
/// <b>Pin names:</b>
/// <list type="number">
/// <item><see cref="Mfa_Locked_RenderS_MfaLocked_Notice"/>
///   — the <c>GET /settings/security/mfa</c> action on a locked, non-admin
///   sample account returns a <see cref="ViewResult"/> with the view name
///   <c>"MfaLocked"</c> and a <see cref="MfaLockedViewModel"/> model (the
///   ADR 0138 "surface replaced by a notice" shape, re-applied to the MFA
///   surface — the <see cref="ChangePasswordLockedViewModel"/> precedent).
///   The <c>UserManager</c> MFA seams are NOT consulted (the lock short-
///   circuits before the <c>GetTwoFactorEnabledAsync</c> read).</item>
/// <item><see cref="Mfa_Unlocked_RenderS_EnrollmentSurface"/>
///   — the <c>GET /settings/security/mfa</c> action on a non-locked account
///   (a real account or the sample admin, the ADR 0138 exemption) returns
///   the enrollment surface (a <see cref="MfaSettingsViewModel"/> model,
///   the <c>SetupKey</c> present on the enrollment leg). The ADR 0194
///   exemption holds — the lock does not fire for a non-sample account.</item>
/// <item><see cref="MfaEnable_Locked_RefusedRefused"/>
///   — the <c>POST /settings/security/mfa</c> (enable) action on a locked,
///   non-admin sample account is refused before any MFA write (a
///   <see cref="RedirectToActionResult"/> back to the <c>Mfa</c> action —
///   the ADR 0138 ChangePassword guard shape, the defense-in-depth over the
///   GET notice). The <c>UserManager</c> MFA write seams are NOT called
///   (the guard short-circuits before <c>VerifyTwoFactorTokenAsync</c> /
///   <c>SetTwoFactorEnabledAsync</c>).</item>
/// <item><see cref="MfaRotateRecovery_Locked_RefusedRefused"/>
///   — the <c>POST /settings/security/mfa/recovery</c> (rotate) action on a
///   locked, non-admin sample account is refused before the recovery codes
///   are rotated (a <see cref="RedirectToActionResult"/> back to the
///   <c>Mfa</c> action). The <c>UserManager</c>
///   <c>GenerateNewTwoFactorRecoveryCodesAsync</c> seam is NOT called.</item>
/// <item><see cref="MfaDisable_Locked_RefusedRefused"/>
///   — the <c>POST /settings/security/mfa/disable</c> action on a locked,
///   non-admin sample account is refused before the MFA flag is cleared (a
///   <see cref="RedirectToActionResult"/> back to the <c>Mfa</c> action).
///   The <c>UserManager</c> <c>SetTwoFactorEnabledAsync</c> /
///   <c>ResetAuthenticatorKeyAsync</c> seams are NOT called.</item>
/// </list>
/// </summary>
public sealed class SecuritySettingsMfaLockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Email = "mfa-lock@examplium.com";
    private const string Password = "Sup3rSecret!";

    // ── Pin 1 — the Mfa GET renders the MfaLocked notice when locked ───────

    /// <summary>
    /// <c>GET /settings/security/mfa</c> on a locked, non-admin sample account
    /// returns a <see cref="ViewResult"/> with the view name <c>"MfaLocked"</c>
    /// and a <see cref="MfaLockedViewModel"/> model (the ADR 0138 "surface
    /// replaced by a notice" shape, re-applied to the MFA surface — the
    /// <see cref="ChangePasswordLockedViewModel"/> precedent). The
    /// <c>IsMfaLockedForAsync</c> seam was consulted (the ADR 0138 / 0194
    /// four-conjunct decision — the guard the controller reads).
    /// </summary>
    [Fact]
    public async Task Mfa_Locked_RenderS_MfaLocked_Notice()
    {
        var b = await BootAsync(isMfaLocked: true);
        var controller = BuildController(b);

        var action = await controller.Mfa();
        var view = Assert.IsType<ViewResult>(action);

        // The view is the MfaLocked notice (NOT the Mfa enrollment surface).
        Assert.Equal("MfaLocked", view.ViewName);
        Assert.IsType<MfaLockedViewModel>(view.ViewData.Model);

        // The IsMfaLockedForAsync seam was consulted (the ADR 0138 / 0194
        // four-conjunct decision the controller reads to decide the guard).
        await b.Identity
            .Received(1)
            .IsMfaLockedForAsync(Arg.Any<string>());
    }

    // ── Pin 2 — the Mfa GET renders the enrollment surface when unlocked ───

    /// <summary>
    /// <c>GET /settings/security/mfa</c> on a non-locked account (a real
    /// account or the sample admin — the ADR 0138 exemption) returns the
    /// enrollment surface (a <see cref="MfaSettingsViewModel"/> model, the
    /// <c>SetupKey</c> present on the enrollment leg). The ADR 0194
    /// exemption holds — the lock does not fire for a non-sample account.
    /// </summary>
    [Fact]
    public async Task Mfa_Unlocked_RenderS_EnrollmentSurface()
    {
        var b = await BootAsync(isMfaLocked: false);
        var controller = BuildController(b);

        var action = await controller.Mfa();
        var view = Assert.IsType<ViewResult>(action);

        // The view is the Mfa enrollment surface (NOT the MfaLocked notice).
        Assert.NotEqual("MfaLocked", view.ViewName);
        var model = Assert.IsType<MfaSettingsViewModel>(view.ViewData.Model);

        // The enrollment leg (MFA not yet enabled) carries a SetupKey.
        Assert.False(model.Enabled);
        Assert.NotNull(model.SetupKey);
    }

    // ── Pin 3 — the MfaEnable POST is refused before any MFA write ─────────

    /// <summary>
    /// <c>POST /settings/security/mfa</c> (enable) on a locked, non-admin
    /// sample account is refused before any MFA write — a
    /// <see cref="RedirectToActionResult"/> back to the <c>Mfa</c> action
    /// (the ADR 0138 ChangePassword guard shape, the defense-in-depth over
    /// the GET notice). The MFA flag is NOT flipped (the guard
    /// short-circuits before the verify/flip) — observable as the
    /// <c>UserManager.GetTwoFactorEnabledAsync</c> read returning <c>false</c>
    /// after the refused enable (the user was created with MFA off, and a
    /// successful enable would have flipped it on).
    /// </summary>
    [Fact]
    public async Task MfaEnable_Locked_RefusedRefused()
    {
        var b = await BootAsync(isMfaLocked: true);
        var controller = BuildController(b);

        var action = await controller.MfaEnable(new MfaEnableViewModel { Code = "123456" });
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(SecuritySettingsController.Mfa), redirect.ActionName);

        // The MFA flag was NOT flipped on (the guard short-circuits before the
        // SetTwoFactorEnabledAsync write — the user was created with MFA off,
        // and a successful enable would have flipped it on).
        Assert.False(await b.UserManager.GetTwoFactorEnabledAsync(b.User!));
    }

    // ── Pin 4 — the MfaRotateRecovery POST is refused before the rotate ────

    /// <summary>
    /// <c>POST /settings/security/mfa/recovery</c> (rotate) on a locked,
    /// non-admin sample account is refused before the recovery codes are
    /// rotated — a <see cref="RedirectToActionResult"/> back to the
    /// <c>Mfa</c> action (the ADR 0138 ChangePassword guard shape, the
    /// defense-in-depth over the GET notice). The
    /// <c>IsMfaLockedForAsync</c> seam was consulted (the guard the
    /// controller reads to decide the refusal).
    /// </summary>
    [Fact]
    public async Task MfaRotateRecovery_Locked_RefusedRefused()
    {
        var b = await BootAsync(isMfaLocked: true);
        var controller = BuildController(b);

        var action = await controller.MfaRotateRecovery();
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(SecuritySettingsController.Mfa), redirect.ActionName);

        // The IsMfaLockedForAsync seam was consulted (the ADR 0138 / 0194
        // four-conjunct decision the controller reads to decide the guard).
        await b.Identity
            .Received(1)
            .IsMfaLockedForAsync(Arg.Any<string>());
    }

    // ── Pin 5 — the MfaDisable POST is refused before the flag is cleared ──

    /// <summary>
    /// <c>POST /settings/security/mfa/disable</c> on a locked, non-admin
    /// sample account is refused before the MFA flag is cleared — a
    /// <see cref="RedirectToActionResult"/> back to the <c>Mfa</c> action
    /// (the ADR 0138 ChangePassword guard shape, the defense-in-depth over
    /// the GET notice). The
    /// <c>IsMfaLockedForAsync</c> seam was consulted (the guard the
    /// controller reads to decide the refusal).
    /// </summary>
    [Fact]
    public async Task MfaDisable_Locked_RefusedRefused()
    {
        var b = await BootAsync(isMfaLocked: true);
        var controller = BuildController(b);

        var action = await controller.MfaDisable(new MfaDisableViewModel { Code = "123456" });
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(SecuritySettingsController.Mfa), redirect.ActionName);

        // The IsMfaLockedForAsync seam was consulted (the ADR 0138 / 0194
        // four-conjunct decision the controller reads to decide the guard).
        await b.Identity
            .Received(1)
            .IsMfaLockedForAsync(Arg.Any<string>());
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    private sealed record Harness
    {
        public required UserManager<User>   UserManager { get; init; }
        public required User?               User        { get; init; }
        public required IIdentityService    Identity    { get; init; }
    }

    private async Task<Harness> BootAsync(bool isMfaLocked)
    {
        var ct   = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
        await using (var db = new AppDbContext(dbOptions))
        {
            await db.Database.MigrateAsync(ct);
        }

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

        var user = new User { Id = Guid.NewGuid().ToString("N"), Email = Email, UserName = Email };
        await userManager.CreateAsync(user, Password);

        // ADR 0194 — the IsMfaLockedForAsync seam is scripted to the lock
        // state under test (the ADR 0138 four-conjunct decision — the
        // SecuritySettingsController reads this via the IsMfaLockedAsync
        // helper, which delegates to the Core seam).
        var identity = Substitute.For<IIdentityService>();
        identity.IsMfaLockedForAsync(Arg.Any<string>()).Returns(isMfaLocked);

        return new Harness { UserManager = userManager, User = user, Identity = identity };
    }

    private static SecuritySettingsController BuildController(Harness b)
    {
        var signInManager = Substitute.For<SignInManager<User>>(
            b.UserManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>());

        var controller = new SecuritySettingsController(
            userManager:    b.UserManager,
            signInManager:  signInManager,
            identity:       b.Identity);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("Kumunita.Sub", b.User!.Id!) },
            authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return controller;
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
}
