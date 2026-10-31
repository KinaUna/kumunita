using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// SEC M5 · U18 — the <c>RevocationStampMiddleware</c> fail-closed cut-off.
/// <para>
/// Before U18 the middleware's backward-compat rule let an empty-stamp cookie
/// pass through for the session's whole life. U18 time-boxes that: when the
/// config key <c>Security:RevocationStamp:RequiredAfter</c> (an ISO UTC date)
/// is set and <c>DateTime.UtcNow</c> is at or past it, an empty stamp is
/// treated as revoked — the SAME response as a stale stamp (sign-out +
/// <c>Redirect("/Account/Login?error=session-revoked")</c>). Before the
/// cut-off (or with the key unset) the pass-through is unchanged.
/// </para>
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom (the shape
/// <see cref="BlockedAccountMiddlewareTests"/> uses) — a
/// <see cref="DefaultHttpContext"/> with a stubbed
/// <see cref="IAuthenticationService"/> (NSubstitute) that returns a ticket
/// whose <c>Properties.Items["kumunita.stamp"]</c> is the cookie's
/// birth-stamp, and NSubstitute seams for the scoped
/// <see cref="UserManager{User}"/> / <see cref="SignInManager{User}"/> the
/// middleware resolves from <see cref="HttpContext.RequestServices"/>. The
/// key assertion is the redirect URL + the pass-through/no-redirect shape.
/// </para>
/// </summary>
public sealed class RevocationStampFailClosedTests
{
    private const string SubjectId = "u18-synthetic-subject-id";
    private const string StampKey  = "kumunita.stamp";
    private const string ConfigKey = "Security:RevocationStamp:RequiredAfter";

    // ── Pin §2.3 test 1 — before the cut-off, an empty stamp passes ──────────

    [Fact]
    public async Task NoStamp_BeforeCutoff_PassesThrough()
    {
        var cutOff = DateTime.UtcNow.AddDays(+30);   // future — gate not yet active

        var (mw, ctx, nextCalled) = Build(
            cookieStamp:  null,
            cutOffUtc:    cutOff,
            userPresent:  true,
            currentStamp: "any-stamp-value");

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled[0]);
        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.False(ctx.Response.Headers.ContainsKey("Location"));
    }

    // ── Pin §2.3 test 2 — after the cut-off, an empty stamp is rejected ─────

    [Fact]
    public async Task NoStamp_AfterCutoff_IsRejected()
    {
        var cutOff = DateTime.UtcNow.AddDays(-30);  // past — gate active

        var (mw, ctx, nextCalled) = Build(
            cookieStamp:  null,
            cutOffUtc:    cutOff,
            userPresent:  true,
            currentStamp: "any-stamp-value");

        await mw.InvokeAsync(ctx);

        Assert.False(nextCalled[0]);
        Assert.Equal("/Account/Login?error=session-revoked",
            ctx.Response.Headers["Location"].ToString());
    }

    // ── Pin §2.3 test 3 — after the cut-off, a fresh stamp still passes ─────

    [Fact]
    public async Task FreshStamp_AfterCutoff_Passes()
    {
        var cutOff = DateTime.UtcNow.AddDays(-30);  // gate active, but the stamp is fresh
        var freshStamp = "u18-fresh-stamp";

        var (mw, ctx, nextCalled) = Build(
            cookieStamp:  freshStamp,
            cutOffUtc:    cutOff,
            userPresent:  true,
            currentStamp: freshStamp);   // cookie == DB — no rotation since sign-in

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled[0]);
        Assert.Equal(200, ctx.Response.StatusCode);
        Assert.False(ctx.Response.Headers.ContainsKey("Location"));
    }

    // ── Pin §2.3 test 4 — a stale stamp is rejected (the same response) ─────

    [Fact]
    public async Task Verify_ThenChangePassword_OldSessionRejected()
    {
        // The "old session's" birth-stamp was minted at the verify/sign-in
        // leg; the password-change lane then rotates the SecurityStamp in
        // the DB. The next request from the old session: the cookie's stamp
        // is now STALE (mismatches the DB's current stamp) — the middleware
        // rejects with the SAME response as the empty-stamp fail-closed
        // path (sign-out + redirect to the revoked-message login URL).
        //
        // The "cut-off" is deliberately in the PAST, so this test also
        // proves the stale-stamp rejection path does NOT depend on the
        // cut-off (it always fires on a mismatch, independent of U18).
        var cutOff = DateTime.UtcNow.AddDays(-30);
        var oldSessionStamp = "u18-old-session-stamp";

        var (mw, ctx, nextCalled) = Build(
            cookieStamp:  oldSessionStamp,
            cutOffUtc:    cutOff,
            userPresent:  true,
            currentStamp: "u18-rotated-stamp-after-change-password");  // DB value after the rotation

        await mw.InvokeAsync(ctx);

        Assert.False(nextCalled[0]);
        Assert.Equal("/Account/Login?error=session-revoked",
            ctx.Response.Headers["Location"].ToString());
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    private static (RevocationStampMiddleware mw, HttpContext ctx, bool[] nextCalled) Build(
        string?    cookieStamp,
        DateTime   cutOffUtc,
        bool       userPresent,
        string     currentStamp)
    {
        // IConfiguration is not IDisposable in .NET 10 — no `using`.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ConfigKey] = cutOffUtc.ToString("O") })
            .Build();

        var userManager = BuildUserManager(userPresent, currentStamp);
        var signIn      = BuildSignInManager(userManager);

        // Stub the authentication pipeline: the middleware calls
        // context.AuthenticateAsync(IdentityConstants.ApplicationScheme),
        // which resolves IAuthenticationService from RequestServices.
        // Build the ticket here — stamp null → key omitted (empty-stamp shape).
        var props = new AuthenticationProperties();
        if (cookieStamp is not null) props.Items[StampKey] = cookieStamp;
        var identity = new ClaimsIdentity(
            new[] { new Claim("Kumunita.Sub", SubjectId) }, "test");
        // .NET 10 ctor: (principal, properties, scheme) — the scheme name
        // must match the one the middleware queries (IdentityConstants.ApplicationScheme).
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            props,
            Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
        // Manual fake (not NSubstitute) to avoid overload-resolution
        // ambiguity with .NET 10's IAuthenticationService signatures.
        var authService = new StubAuthService(ticket);

        var services = new ServiceCollection()
            .AddSingleton<UserManager<User>>(userManager)
            .AddSingleton<SignInManager<User>>(signIn)
            .AddSingleton<IAuthenticationService>(authService)
            .BuildServiceProvider();

        var ctx = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("Kumunita.Sub", SubjectId) }, "test")),
        };

        var nextCalled = new bool[1];
        var mw = new RevocationStampMiddleware(
            next: c =>
            {
                nextCalled[0] = true;
                c.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            configuration: config);

        return (mw, ctx, nextCalled);
    }

    /// <summary>
    /// A <see cref="UserManager{User}"/> whose <c>FindByIdAsync</c> and
    /// <c>GetSecurityStampAsync</c> return the pinned synthetic values —
    /// both methods are virtual, so NSubstitute intercepts them and the
    /// real logic (which would hit the inert <c>IUserStore</c>) never runs.
    /// </summary>
    private static UserManager<User> BuildUserManager(bool userPresent, string currentStamp)
    {
        var user = new User { Id = SubjectId, Email = "u18@example.test", UserName = "u18@example.test" };

        var um = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new EmptyServiceProvider(),
            NullLogger<UserManager<User>>.Instance);

        um.FindByIdAsync(SubjectId)
            .Returns(userPresent ? Task.FromResult(user) : Task.FromResult<User?>(null));
        um.GetSecurityStampAsync(user).Returns(Task.FromResult(currentStamp));

        return um;
    }

    private static SignInManager<User> BuildSignInManager(UserManager<User> userManager)
        => Substitute.For<SignInManager<User>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>());

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    /// <summary>
    /// A minimal <see cref="IAuthenticationService"/> whose
    /// <c>AuthenticateAsync(HttpContext, string)</c> returns a pre-built
    /// ticket (the cookie's birth-stamp lives in
    /// <c>AuthenticationProperties.Items["kumunita.stamp"]</c>). The other
    /// members throw — the middleware under test only ever calls
    /// <c>AuthenticateAsync</c>. A manual fake is used rather than
    /// NSubstitute to sidestep the .NET 10 overload-resolution quirks.
    /// </summary>
    private sealed class StubAuthService(AuthenticationTicket ticket) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string scheme)
            => Task.FromResult(AuthenticateResult.Success(ticket));

        public Task ChallengeAsync(HttpContext context, string scheme, AuthenticationProperties properties)
            => throw new NotSupportedException();

        public Task ForbidAsync(HttpContext context, string scheme, AuthenticationProperties properties)
            => throw new NotSupportedException();

        public Task SignInAsync(HttpContext context, string scheme, ClaimsPrincipal principal, AuthenticationProperties properties)
            => throw new NotSupportedException();

        public Task SignOutAsync(HttpContext context, string scheme, AuthenticationProperties properties)
            => throw new NotSupportedException();
    }
}
