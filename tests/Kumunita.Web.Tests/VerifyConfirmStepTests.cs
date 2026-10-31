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
/// SEC L3 (U24) — the self-serve confirm-step regression tests
/// (design doc §2.3, U24 row).
/// <para>
/// The invariant: a GET to the verify link must be side-effect-free —
/// it renders a confirm page but must NOT sign in (no
/// <c>HttpContext.SignInAsync</c>) and must NOT consume the one-time
/// token (<c>ConsumedAt</c> stays <c>null</c>). Only the POST (the
/// same link, the token row id round-tripped in the hidden input)
/// validates the token, signs the resident in via
/// <see cref="AccountController"/>'s <c>ConfirmAndSignInAsync</c>
/// path, and consumes the token.
/// </para>
/// <para>
/// Harness: mirrors <see cref="VerifySignInStampTests"/> — a real
/// <see cref="UserManager{User}"/> over the shared
/// <see cref="PostgresFixture"/>, a real Marten store (the
/// <c>LoadUsableVerifyTokenAsync</c> seam), a real
/// <see cref="KumunitaClaimsPrincipalFactory"/>, and a substitute
/// <see cref="IAuthenticationService"/> to capture
/// <c>SignInAsync</c>.
/// </para>
/// </summary>
public sealed class VerifyConfirmStepTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Subject = "user-a";
    private const string Email = "a@example.test";
    private const string TokenId = "tok-1";
    private const string TokenSecret = "secret-abc";

    // ── Pin §2.3 test 1 — the GET never signs in ───────────────────────

    [Fact]
    public async Task VerifyGet_DoesNotSignIn()
    {
        var (controller, store) = await BuildControllerAsync();

        // The account has a password → the self-serve lane.
        var result = await controller.Verify(id: TokenId);

        // The GET renders a ViewResult (the confirm page), not a
        // RedirectResult (sign-in).
        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<VerifyViewModel>(view.ViewData.Model);
        Assert.Equal(TokenId, vm.Id);
        Assert.False(vm.RequiresPassword);

        // SignInAsync must NOT have been called.
        Assert.Equal(0, CapturedSignInCalls);
    }

    // ── Pin §2.3 test 2 — the POST signs in ────────────────────────────

    [Fact]
    public async Task VerifyPost_SignsIn()
    {
        var (controller, store) = await BuildControllerAsync();

        // Drive the self-serve Verify POST lane. The account has a
        // password (created above), so the action takes the
        // ConfirmAndSignInAsync path → SignInAfterVerifyAsync →
        // HttpContext.SignInAsync.
        var result = await controller.Verify(new VerifyViewModel { Id = TokenId });

        // The POST returns a RedirectResult (sign-in → /profile/edit).
        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/profile/edit", redirect.Url);

        // SignInAsync was called exactly once.
        Assert.Equal(1, CapturedSignInCalls);
    }

    // ── Pin §2.3 test 3 — the GET leaves the token unconsumed ──────────

    [Fact]
    public async Task VerifyGet_LeavesTokenUnconsumed()
    {
        var (controller, store) = await BuildControllerAsync();

        // The GET renders the confirm page (no sign-in, no consume).
        var result = await controller.Verify(id: TokenId);
        Assert.IsType<ViewResult>(result);

        // Re-load the token from the store: ConsumedAt must still be null
        // (the GET did NOT consume the one-time token — only the POST does).
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        var token = await session.LoadAsync<IdentityToken>(TokenId);
        Assert.NotNull(token);
        // The GET must not consume the one-time token — only the POST (the
        // ConfirmAndSignInAsync / VerifyAndSetPasswordAsync lane) does.
        Assert.True(token!.ConsumedAt is null,
            "the GET must not consume the one-time token — only the POST (the " +
            "ConfirmAndSignInAsync / VerifyAndSetPasswordAsync lane) does");
    }

    // ── Harness (mirrors VerifySignInStampTests) ───────────────────────

    private static int CapturedSignInCalls { get; set; }

    private async Task<(AccountController controller, IDocumentStore store)> BuildControllerAsync()
    {
        var ct   = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        // Real Marten store (the LoadUsableVerifyTokenAsync seam).
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
                Id        = TokenId,
                Kind      = IdentityToken.KindVerify,
                UserId    = Subject,
                Token     = TokenSecret,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });
            await martenSession.SaveChangesAsync(ct);
        }

        // Real EF store (the Identity schema — the User row).
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

        // ── Real principal factory (the admissible claim set is minted
        //    here; CreateAsync runs for real) ────────────────────────────

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

        // ── IAuthenticationService — count the SignInAsync calls ────────

        CapturedSignInCalls = 0;
        var authService = Substitute.For<IAuthenticationService>();
        authService.SignInAsync(
                Arg.Any<HttpContext>(),
                Arg.Any<string>(),
                Arg.Any<ClaimsPrincipal>(),
                Arg.Any<AuthenticationProperties>())
            .Returns(callInfo =>
            {
                CapturedSignInCalls++;
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

        return (controller, store);
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
