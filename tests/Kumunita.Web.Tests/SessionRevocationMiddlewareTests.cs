using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Sessions;
using Kumunita.Web.Security;
using Marten;
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
/// Regression test for <see cref="SessionRevocationMiddleware"/> (the per-session
/// revocation gate, registered immediately after <see cref="RevocationStampMiddleware"/>).
/// <para>
/// <b>Root cause under test (fixed 2026-11-01):</b>
/// <see cref="SessionRevocationMiddleware.InvokeAsync"/>, on the first request
/// that carries a <c>kumunita.sid</c> cookie item, resolved the
/// <see cref="IDocumentStore"/> and applied <c>await using</c> to it:
/// <c>await using var store = requestServices.GetRequiredService&lt;IDocumentStore&gt;();</c>.
/// Because <see cref="IDocumentStore"/> is registered as a
/// <b>singleton</b> by <c>AddMarten</c>, that <c>await using</c> called
/// <c>DisposeAsync()</c> on the process-wide singleton, which also disposed the
/// shared <c>Npgsql.PoolingDataSource</c> the store owns. That single dispose
/// killed the connection pool for the <em>entire</em> process — every
/// subsequent Marten read, EF Core <see cref="AppDbContext"/> operation, and
/// Wolverine background agent (heartbeat, durability, control listener, metrics
/// sweeper) then failed with
/// <c>System.ObjectDisposedException: 'Npgsql.PoolingDataSource'</c> until the
/// container was redeployed. The trigger is the first authenticated request
/// carrying a <c>kumunita.sid</c> (i.e. the first page load after an MFA
/// sign-in), which is exactly the reported repro.
/// </para>
/// <para>
/// The fix resolves the store <b>without</b> disposing it (the host's
/// <c>app.StopAsync()</c> is the only correct disposal point for the singleton)
/// and disposes <b>only the query session</b> — the per-request scoped resource.
/// </para>
/// <para>
/// Each test pins the negative property: after
/// <see cref="SessionRevocationMiddleware.InvokeAsync"/> completes, the
/// <see cref="IDocumentStore"/> double has <b>not</b> been disposed. The pre-fix
/// code disposed it, so every one of these assertions fails against the old
/// implementation.
/// </para>
/// </summary>
public sealed class SessionRevocationMiddlewareTests
{
    private const string SessionId    = "sid-synthetic-0001";
    private const string SubjectId    = "u-synthetic-subject";
    private const string SidCookieKey = "kumunita.sid";

    // ── Active session → pass-through; the store must NOT be disposed ─────

    [Fact]
    public async Task ActiveSession_PassesThrough_AndStoreIsNotDisposed()
    {
        var (mw, ctx, store, _, nextCalled) =
            Build(revoked: false, lastActive: DateTime.UtcNow);

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled[0]);   // the downstream handler ran
        store.DidNotReceive().Dispose();
        await store.DidNotReceive().DisposeAsync();
    }

    // ── Revoked session → sign-out + redirect; the store must NOT be disposed ─

    [Fact]
    public async Task RevokedSession_SignsOut_AndStoreIsNotDisposed()
    {
        var (mw, ctx, store, signIn, nextCalled) =
            Build(revoked: true, lastActive: DateTime.UtcNow.AddDays(-30));
        ctx.Response.Body = new MemoryStream();   // Redirect + CompleteAsync need a writable body

        await mw.InvokeAsync(ctx);

        await signIn.Received(1).SignOutAsync();
        Assert.Equal("/Account/Login?error=session-revoked",
            ctx.Response.Headers["Location"].ToString());
        Assert.False(nextCalled[0]);        // short-circuited — handler did not run
        store.DidNotReceive().Dispose();
        await store.DidNotReceive().DisposeAsync();
    }

    // ── No sid in the cookie → pass-through; the store must NOT be disposed ─

    [Fact]
    public async Task NoSid_PassesThrough_AndStoreIsNotDisposed()
    {
        var (mw, ctx, store, _, nextCalled) =
            Build(revoked: false, lastActive: DateTime.UtcNow, sid: null);

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled[0]);
        store.DidNotReceive().Dispose();
        await store.DidNotReceive().DisposeAsync();
    }

    // ── Unauthenticated → pass-through; the store must NOT be disposed ─────

    [Fact]
    public async Task Unauthenticated_PassesThrough_AndStoreIsNotDisposed()
    {
        var (mw, ctx, store, _, nextCalled) =
            Build(revoked: false, lastActive: DateTime.UtcNow, authenticated: false);

        await mw.InvokeAsync(ctx);

        Assert.True(nextCalled[0]);
        store.DidNotReceive().Dispose();
        await store.DidNotReceive().DisposeAsync();
    }

    // ── Harness ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the middleware over a <see cref="DefaultHttpContext"/>. The
    /// <see cref="IDocumentStore"/> is the shared singleton under test —
    /// NSubstitute records its <c>Dispose</c> / <c>DisposeAsync</c> calls so the
    /// assertions can pin "not disposed" (the property that was broken). The
    /// <see cref="IQuerySession"/> it opens is a substitute that returns the
    /// pinned <see cref="SessionDoc"/>.
    /// </summary>
    private static (SessionRevocationMiddleware mw, HttpContext ctx,
        IDocumentStore store, SignInManager<User> signIn, bool[] nextCalled)
        Build(bool revoked, DateTime lastActive, string? sid = SessionId, bool authenticated = true)
    {
        var doc = new SessionDoc
        {
            Id         = SessionId,
            UserId     = SubjectId,
            LastActive = lastActive,
            RevokedAt  = revoked ? DateTime.UtcNow : null,
        };

        var store = Substitute.For<IDocumentStore>();
        var querySession = Substitute.For<IQuerySession>();
        querySession.LoadAsync<SessionDoc>(SessionId)
            .Returns(Task.FromResult<SessionDoc?>(doc));
        store.QuerySession().Returns(querySession);

        var config = new ConfigurationBuilder().Build();
        var um     = BuildUserManager();
        var signIn = BuildSignInManager(um);

        var props = new AuthenticationProperties();
        if (sid is not null) props.Items[SidCookieKey] = sid;
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, SubjectId) }, "test")),
            props,
            Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
        var authService = new StubAuthService(ticket);

        var services = new ServiceCollection()
            .AddSingleton<IDocumentStore>(store)
            .AddSingleton<IAuthenticationService>(authService)
            .AddSingleton<UserManager<User>>(um)
            .AddSingleton<SignInManager<User>>(signIn)
            .BuildServiceProvider();

        var ctx = new DefaultHttpContext { RequestServices = services };
        ctx.User = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity(
                  new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, SubjectId) }, "test"))
            : new ClaimsPrincipal(new ClaimsIdentity("anon"));

        var nextCalled = new bool[1];
        var mw = new SessionRevocationMiddleware(
            next: c =>
            {
                nextCalled[0] = true;
                c.Response.StatusCode = 200;
                return Task.CompletedTask;
            },
            configuration: config);

        return (mw, ctx, store, signIn, nextCalled);
    }

    private static UserManager<User> BuildUserManager()
    {
        var user = new User { Id = SubjectId, Email = "s@example.test", UserName = "s@example.test" };
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
        um.FindByIdAsync(SubjectId).Returns(Task.FromResult<User?>(user));
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
    /// <c>AuthenticateAsync(HttpContext, string)</c> returns a pre-built ticket
    /// (the session id lives in <c>AuthenticationProperties.Items["kumunita.sid"]</c>).
    /// A manual fake (not NSubstitute) to sidestep the .NET 10 overload-resolution
    /// quirks — the same idiom <see cref="RevocationStampFailClosedTests"/> uses.
    /// </summary>
    private sealed class StubAuthService(AuthenticationTicket ticket) : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.Success(ticket));
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => throw new NotSupportedException();
        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => throw new NotSupportedException();
    }
}
