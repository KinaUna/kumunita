using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;


/// <summary>
/// SEC H2 — the pinned tests for <see cref="AdminTwoFactorMiddleware"/>
/// (design doc §2.3, U7; the §2.4 gate (b) "admin without 2FA" is composed
/// from this class).
/// <para>
/// The gate: an authenticated <b>GlobalAdmin</b> whose 2FA is not enrolled
/// (and who is not a sample-data account) may only reach the enrolment
/// routes (<c>/settings/security/mfa[/recovery|/disable]</c>), logout
/// (<c>/account/logout</c>) and static assets; every other route gets a
/// 302 to <c>/settings/security/mfa</c>. Non-GlobalAdmins, enrolled admins
/// and sample-data accounts pass through untouched.
/// </para>
/// <para>
/// Harness: a real <see cref="UserManager{User}"/> over the shared
/// <see cref="PostgresFixture"/> database (the <c>F5U4_KillAllTests</c>
/// idiom — the repo's Web tests have no EF InMemory provider, so the
/// Postgres fixture is the house pattern), a substitute
/// <see cref="IIdentityService"/> (the <c>IsSampleDataAccountAsync</c>
/// exemption seam), and a <see cref="DefaultHttpContext"/> with a synthetic
/// principal carrying the <c>Kumunita.Role</c> claim (synthetic id/email
/// only — no PII).
/// </para>
/// </summary>
public sealed class AdminTwoFactorMiddlewareTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Subject = "user-a";
    private const string Email = "a@example.test";

    // ── The seven pinned names (design doc §2.3, verbatim) ─────────────

    /// <summary>
    /// Un-enrolled GlobalAdmin at <c>/admin</c> (a non-allow-listed route) →
    /// 302 to the enrolment page, <c>next</c> not called.
    /// </summary>
    [Fact]
    public async Task UnenrolledAdmin_Admin_RedirectsToEnrolment()
    {
        var b = await BootAsync(
            identity: Substitute.For<IIdentityService>());

        var (status, location, nextCalled) = await RunAsync(
            b, path: "/admin", role: Roles.GlobalAdmin);

        Assert.False(nextCalled, "a gated request must not reach the pipeline");
        Assert.Equal(StatusCodes.Status302Found, status);
        Assert.Equal("/settings/security/mfa", location);
    }

    /// <summary>
    /// Un-enrolled GlobalAdmin at an arbitrary other non-allow-listed route
    /// (<c>/directory</c>) → the same 302 (the allow-list is closed, not a
    /// single-route check).
    /// </summary>
    [Fact]
    public async Task UnenrolledAdmin_AnyOtherRoute_Redirects()
    {
        var b = await BootAsync(
            identity: Substitute.For<IIdentityService>());

        var (status, location, nextCalled) = await RunAsync(
            b, path: "/directory", role: Roles.GlobalAdmin);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, status);
        Assert.Equal("/settings/security/mfa", location);
    }

    /// <summary>
    /// Un-enrolled GlobalAdmin at every allow-listed route (the enrolment
    /// GET/POST, the recovery GET/POST, the disable POST, logout, and a
    /// static asset) → passes through, no redirect.
    /// </summary>
    [Fact]
    public async Task UnenrolledAdmin_EnrolmentAndLogout_Allowed()
    {
        var b = await BootAsync(
            identity: Substitute.For<IIdentityService>());

        string[] allowedPaths =
        {
            "/settings/security/mfa",
            "/settings/security/mfa/recovery",
            "/settings/security/mfa/disable",
            "/account/logout",
            "/lib/bootstrap/bootstrap.bundle.min.js",
        };

        foreach (var path in allowedPaths)
        {
            var (status, location, nextCalled) = await RunAsync(
                b, path: path, role: Roles.GlobalAdmin);

            Assert.True(nextCalled, $"the allow-listed route {path} must pass through");
            Assert.True(string.IsNullOrEmpty(location), $"the allow-listed route {path} must not redirect");
            Assert.Equal(StatusCodes.Status200OK, status);
        }
    }

    /// <summary>
    /// A GlobalAdmin with 2FA enrolled passes through a non-allow-listed
    /// route untouched (the gate exists only for the not-enrolled case).
    /// </summary>
    [Fact]
    public async Task EnrolledAdmin_PassesThrough()
    {
        var b = await BootAsync(
            identity: Substitute.For<IIdentityService>());
        await b.UserManager.SetTwoFactorEnabledAsync(b.User, true);

        var (status, location, nextCalled) = await RunAsync(
            b, path: "/admin", role: Roles.GlobalAdmin);

        Assert.True(nextCalled, "an enrolled admin must pass through the gate");
        Assert.True(string.IsNullOrEmpty(location), "an enrolled admin must not be redirected");
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    /// <summary>
    /// A non-GlobalAdmin (a Member) is out of scope for the gate: even with
    /// 2FA not enrolled the request passes through untouched (S-2 is a
    /// GlobalAdmin-only invariant).
    /// </summary>
    [Fact]
    public async Task NonAdmin_PassesThrough()
    {
        var b = await BootAsync(
            identity: Substitute.For<IIdentityService>());

        var (status, location, nextCalled) = await RunAsync(
            b, path: "/admin", role: Roles.Member);

        Assert.True(nextCalled, "non-admins must pass through untouched");
        Assert.True(string.IsNullOrEmpty(location), "non-admins must not be redirected");
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    /// <summary>
    /// A sample-data GlobalAdmin without enrolled 2FA keeps its exemption
    /// (veto 2; the same seam the login leg-1.5b branch uses): the request
    /// passes through.
    /// </summary>
    [Fact]
    public async Task SampleDataAdmin_Exempt()
    {
        var identity = Substitute.For<IIdentityService>();
        var b = await BootAsync(identity: identity);
        // BootAsync pins IsSampleDataAccountAsync(Arg.Any) → false as the
        // default; NSubstitute uses the most-recent matching setup, so the
        // per-subject override must be set AFTER that default (this line) for
        // the "user-a" call to resolve to true.
        identity.IsSampleDataAccountAsync(Subject).Returns(true);

        var (status, location, nextCalled) = await RunAsync(
            b, path: "/admin", role: Roles.GlobalAdmin);

        Assert.True(nextCalled, "a sample-data admin must keep the exemption");
        Assert.True(string.IsNullOrEmpty(location), "a sample-data admin must not be redirected");
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    /// <summary>
    /// An admin whose 2FA was enrolled and then disabled is re-gated on the
    /// very next non-allow-listed request — the gate re-reads the enrolment
    /// state per request (no cached session decision).
    /// </summary>
    [Fact]
    public async Task AdminWhoDisables2fa_IsGatedOnNextRequest()
    {
        var b = await BootAsync(
            identity: Substitute.For<IIdentityService>());
        await b.UserManager.SetTwoFactorEnabledAsync(b.User, true);

        // The account is enrolled now: a non-allow-listed route passes.
        var (okStatus, okLocation, okCalled) = await RunAsync(
            b, path: "/admin", role: Roles.GlobalAdmin);
        Assert.True(okCalled, "an enrolled admin must pass through");
        Assert.True(string.IsNullOrEmpty(okLocation), "an enrolled admin must not be redirected");

        // The admin disables 2FA (legal on the allow-listed POST
        // /settings/security/mfa/disable route).
        await b.UserManager.SetTwoFactorEnabledAsync(b.User, false);

        // The next non-allow-listed request is gated again.
        var (status, location, nextCalled) = await RunAsync(
            b, path: "/admin", role: Roles.GlobalAdmin);
        Assert.False(nextCalled, "a disabled 2FA must re-gate the account on the next request");
        Assert.Equal(StatusCodes.Status302Found, status);
        Assert.Equal("/settings/security/mfa", location);
    }

    // ── Harness ─────────────────────────────────────────────────────────

    private sealed record Boot
    {
        public required User              User        { get; init; }
        public required UserManager<User> UserManager { get; init; }
        public required IIdentityService  Identity    { get; init; }
    }

    private async Task<Boot> BootAsync(IIdentityService identity)
    {
        var ct = TestContext.Current.CancellationToken;

        // Real UserManager over the shared Postgres fixture (the
        // F5U4_KillAllTests.BootAsync idiom) — a fresh schema per test so
        // the two-factor state is isolated.
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

        // Synthetic resident (rule 7: no PII — fixed synthetic id/email).
        var user = new User { Id = Subject, Email = Email, UserName = Email };
        await userManager.CreateAsync(user, "Passw0rd!-synthetic");

        // NSubstitute returns null for unconfigured Task<bool> (unbox NRE) —
        // pin the exemption seam to "not a sample account" by default;
        /// the SampleDataAdmin_Exempt test re-pins it to true.
        identity.IsSampleDataAccountAsync(Arg.Any<string>()).Returns(false);

        return new Boot { User = user, UserManager = userManager, Identity = identity };
    }

    /// <summary>
    /// Drives one request through the middleware with a synthetic principal
    /// carrying the given <c>Kumunita.Role</c> claim (and the
    /// <c>Kumunita.Sub</c> subject claim) and returns
    /// (status, Location header, whether <c>next</c> ran).
    /// </summary>
    private static async Task<(int status, string? location, bool nextCalled)> RunAsync(
        Boot b, string path, string role)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        // The DefaultHttpContext's response Body is an empty, non-writable
        // stream — writing a status/redirect (Response.Redirect, or the next
        // delegate's StatusCode set) throws. Back it with a real in-memory
        // stream (the house middleware-test idiom).
        var body = new MemoryStream();
        ctx.Response.Body = new System.IO.BufferedStream(body);

        var claims = new List<Claim>
        {
            new(Kumunita.Core.Identity.ClaimTypes.Subject, Subject),
            new(Kumunita.Core.Identity.ClaimTypes.Role, role),
        };
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims.ToArray(), "test"));

        ctx.RequestServices = new DictionaryProvider(
            new Dictionary<Type, object>
            {
                [typeof(IIdentityService)]       = b.Identity,
                [typeof(UserManager<User>)]      = b.UserManager,
            });

        var nextCalled = false;
        RequestDelegate next = ctx2 =>
        {
            nextCalled = true;
            ctx2.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new AdminTwoFactorMiddleware(next);
        await middleware.InvokeAsync(ctx);

        return (ctx.Response.StatusCode,
                ctx.Response.Headers.Location.ToString(),
                nextCalled);
    }

    // ── Minimal service-provider stubs (the F5U4_KillAllTests idiom) ─────

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
}
