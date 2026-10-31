using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
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

namespace Kumunita.Web.Tests;

/// <summary>
/// SEC M5 (U17) — the pinned test for the verify sign-in stamp
/// (design doc §2.3, U17).
/// <para>
/// The invariant: the <see cref="AccountController.SignInAfterVerifyAsync"/>
/// sign-in path (reached via the self-serve <c>Verify</c> GET lane) must
/// attach <c>AuthenticationProperties.Items["kumunita.stamp"]</c> = the
/// user's current security stamp. Without this, the
/// <see cref="Kumunita.Web.Security.RevocationStampMiddleware"/> (which
/// reads the key at <c>:121</c>) sees an empty stamp and passes the
/// session through (fail-open). U18 makes the middleware fail closed
/// after the cut-off date; U17's job is to ensure every sign-in site
/// stamps.
/// </para>
/// <para>
/// Harness: a real <see cref="UserManager{User}"/> over the shared
/// <see cref="PostgresFixture"/> (the <c>AdminTwoFactorMiddlewareTests</c>
/// idiom — the repo's Web tests have no EF InMemory provider), a
/// substitute <see cref="IIdentityService"/> (the
/// <c>VerifyWithTokenAsync</c> seam), a substitute Marten
/// <see cref="Marten.IDocumentStore"/> (the
/// <c>LoadUsableVerifyTokenAsync</c> seam), and a real
/// <see cref="KumunitaClaimsPrincipalFactory"/> (so the
/// <c>CreateAsync</c> claim-shaping path runs for real). The
/// <see cref="IAuthenticationService"/> is substituted to capture the
/// <see cref="AuthenticationProperties"/> the controller passes to
/// <c>SignInAsync</c>.
/// </para>
/// </summary>
public sealed class VerifySignInStampTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Subject = "user-a";
    private const string Email = "a@example.test";
    private const string StampKey = "kumunita.stamp";

    /// <summary>
    /// The <see cref="AccountController.SignInAfterVerifyAsync"/> sign-in
    /// path (reached via the self-serve <c>Verify</c> GET lane) must attach
    /// a non-empty <c>kumunita.stamp</c> equal to the user's current
    /// security stamp in the <see cref="AuthenticationProperties"/> passed
    /// to <c>HttpContext.SignInAsync</c>.
    /// </summary>
    [Fact]
    public async Task VerifySignIn_CarriesCurrentStamp()
    {
        var (controller, userManager) = await BuildControllerAsync();

        // Capture the stamp BEFORE the sign-in path runs (it will not
        // change during the test — no rotation happens here).
        var user = await userManager.FindByIdAsync(Subject);
        Assert.NotNull(user);
        var expectedStamp = await userManager.GetSecurityStampAsync(user!);
        Assert.False(string.IsNullOrEmpty(expectedStamp),
            "the security stamp must be non-empty (Identity mints it at CreateAsync)");

        // Drive the self-serve Verify POST lane (SEC L3 U24 — the GET now
        // renders a confirm page; the POST validates + signs in via
        // ConfirmAndSignInAsync → SignInAfterVerifyAsync →
        // HttpContext.SignInAsync). The account has a password (created
        // above), so the action takes the ConfirmAndSignInAsync path.
        var result = await controller.Verify(new VerifyViewModel { Id = "tok-1" });
        Assert.IsType<RedirectResult>(result);

        // The captured AuthenticationProperties must carry the stamp.
        var captured = CapturedSignInProperties;
        Assert.NotNull(captured);
        Assert.True(captured!.Items.ContainsKey(StampKey),
            $"the sign-in AuthenticationProperties must carry the '{StampKey}' key " +
            "(the RevocationStampMiddleware reads it at :121)");
        Assert.False(string.IsNullOrEmpty(captured.Items[StampKey]),
            "the stamp value must be non-empty (a null/empty stamp is the " +
            "fail-open case U18 closes)");
        Assert.True(captured.Items[StampKey] == expectedStamp,
            "the stamp must equal the user's current security stamp " +
            "(UserManager.GetSecurityStampAsync)");
    }

    // ── Harness (the F5U4_KillAllTests house pattern) ─────────────────

    private static AuthenticationProperties? CapturedSignInProperties { get; set; }

    private async Task<(AccountController controller, UserManager<User> userManager)> BuildControllerAsync()
    {
        var ct   = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // Real Marten store (the F5U4_KillAllTests.BootAsync idiom — the
        // Web tests have no EF InMemory provider, so the Postgres fixture
        // + real Marten is the house pattern).
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Seed a valid verify token so LoadUsableVerifyTokenAsync returns it.
        await using (var martenSession = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            martenSession.Store(new IdentityToken
            {
                Id      = "tok-1",
                Kind    = IdentityToken.KindVerify,
                UserId  = Subject,
                Token   = "secret-abc",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });
            await martenSession.SaveChangesAsync(ct);
        }

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
        var roleStore = new RoleStore<IdentityRole, AppDbContext, string,
            IdentityUserRole<string>, IdentityRoleClaim<string>>(new AppDbContext(dbOptions));

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

        // Synthetic resident (rule 7: no PII — fixed synthetic id/email).
        var user = new User { Id = Subject, Email = Email, UserName = Email };
        await userManager.CreateAsync(user, "Passw0rd!-synthetic");

        // ── Substitutes for the controller's non-store deps ────────────

        // IIdentityService — the VerifyWithTokenAsync seam (confirm-only
        // lane: flips Profile.Verified + consumes the token in Core).
        var identity = Substitute.For<IIdentityService>();
        var profile  = new Profile { SubjectId = Subject, Verified = true };
        identity.VerifyWithTokenAsync(Arg.Any<string>()).Returns(profile);

        // IUserInfoService — the principal factory's GetProfileAsync seam.
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>()).Returns((Profile?)null);

        // ── Real principal factory (the only place the admissible claim
        //    set is minted; CreateAsync runs for real) ──────────────────

        var factory = new KumunitaClaimsPrincipalFactory(
            userManager: userManager,
            roleManager: new RoleManager<IdentityRole>(
                roleStore,
                new[] { new RoleValidator<IdentityRole>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                NullLogger<RoleManager<IdentityRole>>.Instance),
            userInfo: userInfo,
            identity: identity,
            options: Options.Create(new IdentityOptions()));

        // ── IAuthenticationService — capture the AuthenticationProperties
        //    the controller passes to HttpContext.SignInAsync ───────────

        var authService = Substitute.For<IAuthenticationService>();
        authService.SignInAsync(
                Arg.Any<HttpContext>(),
                Arg.Any<string>(),
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<AuthenticationProperties>())
            .Returns(callInfo =>
            {
                CapturedSignInProperties = callInfo.ArgAt<AuthenticationProperties>(3);
                return Task.CompletedTask;
            });

        // ── HttpContext with the needed services ────────────────────────

        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new System.IO.BufferedStream(new MemoryStream());

        var services = new ServiceCollection();
        services.AddSingleton(authService);
        services.AddSingleton(factory);
        ctx.RequestServices = services.BuildServiceProvider();

        // ── Controller ──────────────────────────────────────────────────

        var controller = new AccountController(
            signInManager: Substitute.For<SignInManager<User>>(
                userManager,
                Substitute.For<IHttpContextAccessor>(),
                Substitute.For<IUserClaimsPrincipalFactory<User>>(),
                Options.Create(new IdentityOptions()),
                NullLogger<SignInManager<User>>.Instance,
                Substitute.For<IAuthenticationSchemeProvider>(),
                Substitute.For<IUserConfirmation<User>>()),
            userManager: userManager,
            identity: identity,
            userInfo: userInfo,
            store: store);

        controller.ControllerContext = new ControllerContext { HttpContext = ctx };
        controller.TempData = new TempDataDictionary(ctx, new NoOpTempDataProvider());

        return (controller, userManager);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }
}
