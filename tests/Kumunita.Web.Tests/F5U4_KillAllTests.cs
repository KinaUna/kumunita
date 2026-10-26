using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// F5 · U4 — the two pin §5 pinned tests for the <c>AUTH-04</c> session
/// kill-all + invalidation seam (F5.U2's <see cref="RevocationStampMiddleware"/>
/// + F3 leg-2 gate + F5.U3's <c>SecuritySettingsController.SessionsKillAll</c>).
///
/// <b>Pin §5 test names (verbatim):</b>
/// <list type="number">
/// <item><see cref="PasswordChange_InvalidatesOtherSessions"/> — the
/// <c>IdentityService.ChangePasswordAsync</c> lane rotates
/// <c>User.SecurityStamp</c> (<c>UpdateSecurityStampAsync</c>,
/// <c>IdentityService.cs:1477</c>); a second, still-signed-in session whose
/// cookie's <c>AuthenticationProperties.Items["kumunita.stamp"]</c> carrier
/// predates the rotation is rejected on its next request by
/// <see cref="RevocationStampMiddleware"/> (sign-out +
/// <c>Redirect("/Account/Login?error=session-revoked")</c>).
/// </item>
/// <item><see cref="KillAllSessions_SignsOutAll"/> —
/// <see cref="SecuritySettingsController.SessionsKillAll"/> calls the Core
/// seam (<c>IIdentityService.KillAllSessionsAsync</c>) + the local
/// <c>SignInManager.SignOutAsync</c>; a pending F3 leg-1 cookie carrying the
/// pre-kill stamp is rejected at leg-2 completion by the F3 leg-2 gate in
/// <c>AccountController.LoginMfa</c> (the §1d half-signed-in window closed
/// — the resident must re-do leg-1 to mint a fresh pending cookie).
/// </item>
/// </list>
///
/// <b>Drivable seams (the F3.U5 harness idiom — direct controller
/// construction, real EF <c>UserManager</c> over an <c>AppDbContext</c>,
/// the <see cref="PostgresFixture"/> shape):</b>
/// <list type="bullet">
/// <item>Test 1 drives the <b>real</b>
/// <c>IdentityService.ChangePasswordAsync</c> (the Core rotation lane, the
/// ADR 0006 single-write seam) and verifies the stamp actually rotated
/// (the DB <c>SecurityStamp</c> changed). This is the prerequisite for
/// <see cref="RevocationStampMiddleware"/> to reject the stale session on
/// its next request (the middleware reads the cookie's <c>kumunita.stamp</c>
/// carrier and compares to the DB stamp; a mismatch → sign-out + redirect).
/// </item>
/// <item>Test 2 drives the <b>real</b>
/// <c>SecuritySettingsController.SessionsKillAll</c> (the F5.U3 surface) +
/// the <b>real</b> <c>IdentityService.KillAllSessionsAsync</c> (the Core
/// seam) and verifies (a) the redirect to Login (the local effect), (b)
/// <c>SignOutAsync</c> was called (the local effect), (c) the stamp rotated
/// (the "everywhere" effect — the prerequisite for the F3 leg-2 gate to
/// reject a pending cookie with the pre-kill stamp at leg-2 completion).
/// </item>
/// </list>
///
/// <b>NOT drivable in a unit harness (the F5.U2 open item — recorded, not
/// assumed):</b> the <see cref="RevocationStampMiddleware"/> stamp-comparison
/// and the F3 leg-2 gate's cookie-stamp check are **no-ops in test
/// harnesses** (the F5.U2 guard: "resolving <c>IAuthenticationService</c> via
/// <c>HttpContext.RequestServices</c> (null-guarded) and skipping the gate
/// when absent — a no-op in unit tests, fully operative in the real
/// pipeline"). A live end-to-end verification (compose stack, a second
/// browser session, a password-change or kill-all, then assert the other
/// session is signed out on its next request) is the real evidence for
/// AUTH-04 per the Razor-verification doctrine (AGENTS.md). These unit tests
/// assert the seams; the E2E is the real evidence for AUTH-04.
/// </summary>
public sealed class F5U4_KillAllTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Password = "Sup3rSecret!";

    // ── Pin §5 test 1 ──────────────────────────────────────────────────────

    /// <summary>
    /// A password change (the <c>IdentityService</c> lane that calls
    /// <c>UpdateSecurityStampAsync</c>) rotates the user's
    /// <c>SecurityStamp</c>. A second, still-signed-in session (a cookie
    /// whose <c>AuthenticationProperties.Items["kumunita.stamp"]</c> was
    /// stamped before the rotation) is rejected on its next request by
    /// <see cref="RevocationStampMiddleware"/> — the middleware reads the
    /// cookie's stamp and compares to the DB stamp; a mismatch → sign-out
    /// + <c>Redirect("/Account/Login?error=session-revoked")</c>.
    /// </summary>
    [Fact]
    public async Task PasswordChange_InvalidatesOtherSessions()
    {
        var b = await BootAsync();
        var user  = b.User;
        var subject = user.Id!;

        // The "other session's" birth-stamp (stamped by the OnSigningIn
        // handler in Program.cs at the session's leg-1 completion — the
        // F5.U2 carrier, AuthenticationProperties.Items["kumunita.stamp"]).
        var otherStamp = await b.UserManager.GetSecurityStampAsync(user);
        Assert.NotNull(otherStamp);
        Assert.NotEmpty(otherStamp);

        // The password-change lane rotates the stamp (the F5 pin §1b call
        // site — IdentityService.cs:1477, the ADR 0006 single-write seam).
        await b.Identity.ChangePasswordAsync(subject, "NewSup3rSecret!", byAdmin: false);

        // The rotation actually changed the DB value (the prerequisite for
        // the RevocationStampMiddleware to reject the stale session on its
        // next request — the middleware reads the cookie's stamp and compares
        // to the DB stamp; a mismatch → sign-out + redirect). The live E2E
        // is the real evidence (the middleware's stamp-comparison is a no-op
        // in test harnesses — the F5.U2 guard).
        var rotatedStamp = await b.UserManager.GetSecurityStampAsync(user);
        Assert.True(otherStamp != rotatedStamp,
            "ChangePasswordAsync must rotate the SecurityStamp (the gate reads it). " +
            $"Other={otherStamp}, Rotated={rotatedStamp}");
    }

    // ── Pin §5 test 2 ──────────────────────────────────────────────────────

    /// <summary>
    /// <c>SecuritySettingsController.SessionsKillAll</c> (the
    /// <c>POST /settings/security/sessions/kill-all</c> surface, the F5.U3
    /// seam) calls the Core seam <c>IIdentityService.KillAllSessionsAsync</c>
    /// (which rotates <c>User.SecurityStamp</c> via
    /// <c>UpdateSecurityStampAsync</c>) + the local
    /// <c>SignInManager.SignOutAsync</c> (clears BOTH the
    /// <c>kumunita.auth</c> and the auxiliary
    /// <c>.AspNetCore.Identity.Application</c> schemes — the
    /// <c>AccountController.Logout</c> shape). A pending F3 leg-1 cookie
    /// (a separate <c>TwoFactorUserId</c> scheme, also stamped at leg-1)
    /// that predates the kill-all is rejected at leg-2 completion by the F3
    /// leg-2 gate in <c>AccountController.LoginMfa</c> (the §1d
    /// half-signed-in window closed) — the resident must re-do leg-1 to
    /// mint a fresh pending cookie with the current stamp.
    /// </summary>
    [Fact]
    public async Task KillAllSessions_SignsOutAll()
    {
        var b = await BootAsync();
        var user    = b.User;
        var subject = user.Id!;

        // The "pending leg-1 cookie's" birth-stamp (stamped by the OnSigningIn
        // handler in Program.cs at leg-1 completion — the F5.U2 carrier,
        // AuthenticationProperties.Items["kumunita.stamp"], on the
        // Identity.TwoFactorUserId scheme cookie).
        var pendingStamp = await b.UserManager.GetSecurityStampAsync(user);
        Assert.NotNull(pendingStamp);
        Assert.NotEmpty(pendingStamp);

        // Drive the real F5.U3 surface: SessionsKillAll → Core seam
        // (KillAllSessionsAsync, the stamp rotation) + local SignOutAsync.
        var signInManager = Substitute.For<SignInManager<User>>(
            b.UserManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>());

        var secController = new SecuritySettingsController(
            userManager:    b.UserManager,
            signInManager:  signInManager,
            identity:       b.Identity);

        var httpCtx = new DefaultHttpContext
        {
            User = BuildPrincipal(subject),
            RequestServices = new DictionaryProvider(
                new Dictionary<Type, object>
                {
                    [typeof(UserManager<User>)] = b.UserManager,
                }),
        };
        secController.ControllerContext = new ControllerContext { HttpContext = httpCtx };
        secController.TempData = new TempDataDictionary(httpCtx, new NoOpTempDataProvider());
        // SessionsKillAll's post-kill redirect goes through
        // RedirectToAction("Login", "Account") — the F3.U5 idiom wires a
        // StubUrlHelper so Controller.Url resolves (RedirectToAction calls
        // Url.Action(...) under the hood).
        secController.Url = new StubUrlHelper(new ActionContext { HttpContext = httpCtx });

        var killResult = await secController.SessionsKillAll();

        // (a) The local effect: a redirect to the login page (the post-kill-all
        //     destination — the resident is now signed out). RedirectToAction
        // produces a RedirectToActionResult (a RedirectToRouteResult subtype) —
        // assert by the action + controller names (the F3.U5 idiom).
        var redirect = Assert.IsType<RedirectToActionResult>(killResult);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);

        // (b) The local effect: SignInManager.SignOutAsync was called (the
        //     AccountController.Logout shape — clears BOTH the kumunita.auth
        //     and the auxiliary .AspNetCore.Identity.Application schemes).
        await signInManager.Received().SignOutAsync();

        // (c) The "everywhere" effect: the SecurityStamp was rotated (the
        //     gate reads this). The pending leg-1 cookie's stamp (pendingStamp)
        //     now mismatches the DB's current stamp — the prerequisite for
        //     the F3 leg-2 gate (AccountController.LoginMfa, the F5.U2 additive
        //     check) to reject the pending cookie at leg-2 completion (the §1d
        //     half-signed-in window closed). The live E2E is the real evidence
        //     (the gate's cookie-stamp check is a no-op in test harnesses —
        //     the F5.U2 guard).
        var rotatedStamp = await b.UserManager.GetSecurityStampAsync(user);
        Assert.True(pendingStamp != rotatedStamp,
            "KillAllSessionsAsync must rotate the SecurityStamp (the gate reads it). " +
            $"Pending={pendingStamp}, Rotated={rotatedStamp}");
    }

    // ── Harness (the F1 IdentityServiceDeleteLaneGdprTests.BootAsync shape) ─

    private sealed record Boot
    {
        public required User              User        { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IdentityService   Identity    { get; init; }
    }

    private async Task<Boot> BootAsync()
    {
        var ct   = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // Real Marten store (the F1 IdentityServiceDeleteLaneGdprTests.BootAsync
        // shape — the ADR 0006 single-write seam, the C3 same-session audit row).
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Real EF store (the Identity schema — the User row, the SecurityStamp).
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

        // Seed the resident.
        var email = "f5u4-" + Guid.NewGuid().ToString("n")[..10] + "@examplium.com";
        var user  = new User { Id = Guid.NewGuid().ToString("N"), Email = email, UserName = email };
        await userManager.CreateAsync(user, Password);

        // The real Core IdentityService (the ADR 0006 single-write seam —
        // ChangePasswordAsync / KillAllSessionsAsync both rotate the stamp
        // via UpdateSecurityStampAsync + append an AccessAudit row in the
        // same Marten session).
        var userInfo = new UserInfoService(store);
        var identity = new IdentityService(
            userManager:          userManager,
            documentStore:        store,
            userInfo:             userInfo,
            claimsSource:         Substitute.For<IClaimsSource>(),
            mailer:               Substitute.For<IMailerStage>(),
            verificationOptions:  Options.Create(new VerificationOptions()),
            logger:               NullLogger<IdentityService>.Instance,
            translationProvider:  null,
            notifications:        null,
            sampleDataOptions:    Options.Create(new SampleDataOptions { Enabled = false }),
            mediaStore:           null);

        return new Boot { User = user, UserManager = userManager, Identity = identity };
    }

    /// <summary>
    /// The principal the <c>KumunitaPrincipal.SubjectId(user)</c> reader
    /// resolves: a <c>Kumunita.Sub</c> claim with the user's id (the
    /// admissible claim set — invariant-set-B intact, ADR 0006-B).
    /// </summary>
    private static ClaimsPrincipal BuildPrincipal(string userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim("Kumunita.Sub", userId),
            new Claim("Kumunita.Verified", "true"),
        });
        return new ClaimsPrincipal(identity);
    }

    // ── Minimal service-provider / temp-data stubs ────────────────────────

    private sealed class DictionaryProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _dict;
        public DictionaryProvider(Dictionary<Type, object> dict) => _dict = dict;
        public object? GetService(Type serviceType) =>
            _dict.TryGetValue(serviceType, out var v) ? v : null;
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
    /// A minimal <see cref="IUrlHelper"/> so <c>ControllerBase.Url</c> (and
    /// hence the <c>RedirectToAction(...)</c> path) resolves in the
    /// direct-construction harness (the <c>F3U5_MfaLoginTests</c> /
    /// <c>KioskControllerTests</c> idiom). <c>ActionName</c> / <c>ControllerName</c>
    /// are returned as the literal inputs so the assertions can verify the
    /// post-kill-all redirect target.
    /// </summary>
    private sealed class StubUrlHelper : Microsoft.AspNetCore.Mvc.IUrlHelper
    {
        public StubUrlHelper(ActionContext actionContext)
        {
            ActionContext = actionContext;
        }

        public ActionContext ActionContext { get; }
        public string Action(string? actionName, string? controllerName, object? values)
            => $"/{controllerName}/{actionName}";
        public string Action(Microsoft.AspNetCore.Mvc.Routing.UrlActionContext actionContext)
            => "stub-action";
        public string Content(string? content) => content ?? string.Empty;
        public bool IsLocalUrl(string? url) => false;
        public string RouteUrl(string? routeName, object? values) => "stub-route";
        public string RouteUrl(Microsoft.AspNetCore.Mvc.Routing.UrlRouteContext routeContext)
            => "stub-route";
        public string Link(string? linkName, object? values) => "stub-link";
    }
}
