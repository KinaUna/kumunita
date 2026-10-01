using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="BlockedAccountMiddleware"/> (IP · U05 — D5 /
/// C-IP·3 / GATE-2). This is the block-enforcement per-request gate: a signed-in
/// account whose <see cref="Profile.Blocked"/> flag gets set (a GlobalAdmin's
/// suspension) while it holds a live cookie is signed out and pushed to
/// <c>/Account/Login?error=blocked</c> <em>before any handler runs</em>.
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute seams + <see cref="DefaultHttpContext"/> — the shape
/// <see cref="UsageCaptureMiddlewareTests"/> and <see cref="GuestClaimMintTests"/>
/// use), <b>not</b> a full host boot. The seam under test is the middleware's
/// <see cref="HttpContext.RequestServices"/> resolution of
/// <see cref="IUserInfoService"/> + <see cref="SignInManager{User}"/> — scoped
/// services resolved per request (the reason the middleware does not
/// constructor-inject them, per the class-level scoping note). A stubbed scoped
/// provider means no Postgres. The production middleware is <b>correct and
/// untouched</b>; this only pins its sign-out + redirect shape for the blocked
/// path and the pass-through for the three non-trigger paths (D5).
/// </para>
/// </summary>
public class BlockedAccountMiddlewareTests
{
    private const string Subject = "s1";

    // ── Pass-through (non-trigger) paths ─────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_PassesThrough()
    {
        var (middleware, signIn, context, nextCalled) = Build(profile: null, authenticated: false);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled[0]);                                        // the downstream handler ran
        Assert.False(context.Response.Headers.ContainsKey("Location"));   // no redirect
        await signIn.DidNotReceive().SignOutAsync();                      // no sign-out
    }

    [Fact]
    public async Task ProfileNull_PassesThrough()
    {
        // Authenticated, but the profile read comes back empty → a normal
        // "no profile" result, not a suspension → pass through.
        var (middleware, signIn, context, nextCalled) = Build(profile: null, authenticated: true);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled[0]);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        await signIn.DidNotReceive().SignOutAsync();
    }

    [Fact]
    public async Task NotBlocked_PassesThrough()
    {
        var profile = new Profile { SubjectId = Subject, Blocked = false };
        var (middleware, signIn, context, nextCalled) = Build(profile, authenticated: true);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled[0]);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        await signIn.DidNotReceive().SignOutAsync();
    }

    // ── The security path (blocked → sign out + redirect) ───────────────────

    [Fact]
    public async Task Blocked_SignsOutAndRedirects()
    {
        var profile = new Profile { SubjectId = Subject, Blocked = true };
        var (middleware, signIn, context, nextCalled) = Build(profile, authenticated: true);

        await middleware.InvokeAsync(context);

        await signIn.Received(1).SignOutAsync();                          // signed out
        Assert.Equal("/Account/Login?error=blocked",                      // the blocked-specific message
            context.Response.Headers["Location"].ToString());
        Assert.False(nextCalled[0]);                                      // short-circuited — no handler ran
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the middleware over a <see cref="DefaultHttpContext"/>. When
    /// <paramref name="authenticated"/> the principal carries the
    /// <see cref="Kumunita.Core.Identity.ClaimTypes.Subject"/> claim; otherwise
    /// it is an unauthenticated identity. <paramref name="profile"/> is what the
    /// stubbed <see cref="IUserInfoService.GetProfileAsync(string)"/> returns
    /// (null = the "no profile" shape).
    /// </summary>
    private static (BlockedAccountMiddleware middleware, SignInManager<User> signIn,
        HttpContext context, bool[] nextCalled) Build(Profile? profile, bool authenticated)
    {
        var context = new DefaultHttpContext();
        context.User = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity(
                  new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Subject) }, "test"))
            : new ClaimsPrincipal(new ClaimsIdentity("anon"));   // IsAuthenticated == false

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(Task.FromResult(profile));

        var signIn = BuildSignInManager();

        var services = new ServiceCollection()
            .AddSingleton<IUserInfoService>(userInfo)
            .AddSingleton<SignInManager<User>>(signIn)
            .BuildServiceProvider();
        context.RequestServices = services;

        // A reference-type holder (not a captured local) so the assertion reads
        // the value AFTER InvokeAsync ran, not the value captured at Build time.
        var nextCalled = new bool[1];
        var middleware = new BlockedAccountMiddleware(ctx =>
        {
            nextCalled[0] = true;    // the downstream "handler"
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        return (middleware, signIn, context, nextCalled);
    }

    /// <summary>
    /// A proxy of the concrete <see cref="SignInManager{TUser}"/> — its
    /// <see cref="SignInManager{TUser}.SignOutAsync()"/> is <c>virtual</c>, so
    /// NSubstitute intercepts it and the internals never run (no real cookie
    /// clearing). The non-user-manager constructor dependencies are inert stubs;
    /// they exist only to satisfy the constructor. The key assertion is the
    /// redirect URL + the sign-out call count.
    /// </summary>
    /// <remarks>
    /// .NET 10 <see cref="SignInManager{TUser}"/> constructor (7 params —
    /// <c>ISecurityTokenValidator</c> was removed in .NET 10, replaced by
    /// <c>IUserClaimsPrincipalFactory{TUser}</c> at position 3 and
    /// <c>IAuthenticationSchemeProvider</c> at position 6).
    /// </remarks>
    private static SignInManager<User> BuildSignInManager()
        => Substitute.For<SignInManager<User>>(
            new UserManager<User>(
                Substitute.For<IUserRoleStore<User>>(),
                Options.Create(new IdentityOptions()),
                new PasswordHasher<User>(),
                new[] { new UserValidator<User>() },
                new[] { new PasswordValidator<User>() },
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                EmptyServiceProvider.Instance,
                NullLogger<UserManager<User>>.Instance),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<User>>.Instance,
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>());

    /// <summary>The no-op <see cref="IServiceProvider"/> the
    /// <see cref="UserManager{TUser}"/> constructor demands (the middleware
    /// never resolves from it — the only seams exercised are the substituted
    /// ones). Mirrors the private helper of the same name in
    /// <see cref="GuestClaimMintTests"/>.</summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
