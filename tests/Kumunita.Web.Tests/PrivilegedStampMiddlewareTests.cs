using System.Security.Claims;
using Kumunita.Core.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="Kumunita.Web.Security.PrivilegedStampMiddleware"/>
/// (IP · U05 — D5 / C-IP·3 / GATE-2). This is the privilege-revocation
/// per-request gate: a signed-in principal carrying an <em>elevated</em> role
/// claim (GlobalAdmin / Moderator / Translator / <c>moderator:*</c>) is
/// re-checked against the database's current role set on every request; a
/// mismatch (promotion, demotion, or scope reassignment while the session was
/// live) signs the user out and lands them on the login page so they must
/// re-authenticate to pick up the new claim set.
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute seams + <see cref="DefaultHttpContext"/> — the shape
/// <see cref="UsageCaptureMiddlewareTests"/> and <see cref="GuestClaimMintTests"/>
/// use), <b>not</b> a full host boot. The seams under test are the
/// middleware's <see cref="HttpContext.RequestServices"/> resolutions of
/// <see cref="UserManager{User}"/>, <see cref="SignInManager{User}"/>, and
/// (for the Moderator branch) <c>IUserInfoService</c>. The
/// <see cref="UserManager{TUser}"/> is a real instance backed by a substituted
/// <see cref="IUserRoleStore{TUser}"/> (its constructor has no parameterless
/// overload, so it cannot be proxied directly — the
/// <see cref="GuestClaimMintTests"/> idiom), which drives
/// <c>FindByIdAsync</c> (user present vs. deleted) and <c>GetRolesAsync</c>
/// (role-set match vs. mismatch). No Postgres. The production middleware is
/// <b>correct and untouched</b>; this only pins the sign-out + redirect shape
/// for each security path and the pass-through for the non-trigger paths.
/// </para>
/// </summary>
public class PrivilegedStampMiddlewareTests
{
    private const string Subject = "s1";

    // ── Pass-through (non-trigger) paths ─────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_PassesThrough()
    {
        var (middleware, signIn, context, nextCalled) = Build(
            principal: new ClaimsPrincipal(new ClaimsIdentity("anon")),   // IsAuthenticated == false
            user: null, dbRoles: null);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled[0]);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        await signIn.DidNotReceive().SignOutAsync();
    }

    [Fact]
    public async Task NoElevatedRole_PassesThrough()
    {
        // Authenticated but Member-only (the ordinary resident) → the fast path
        // short-circuits before any DB read.
        var (middleware, signIn, context, nextCalled) = Build(
            principal: Principal([Roles.Member]),
            user: null, dbRoles: null);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled[0]);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        await signIn.DidNotReceive().SignOutAsync();
    }

    [Fact]
    public async Task RoleSetMatch_PassesThrough()
    {
        // The cookie carries GlobalAdmin and the DB's current role set also
        // carries GlobalAdmin → the standing is unchanged → pass through.
        var (middleware, signIn, context, nextCalled) = Build(
            principal: Principal([Roles.GlobalAdmin]),
            user: new User { Id = Subject },
            dbRoles: [Roles.GlobalAdmin]);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled[0]);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        await signIn.DidNotReceive().SignOutAsync();
    }

    // ── The security paths (sign out + redirect) ─────────────────────────────

    [Fact]
    public async Task UserDeleted_SignsOutAndRedirectsAccountRemoved()
    {
        // The cookie carries GlobalAdmin, but the account was deleted while the
        // session was live → the user read comes back null → sign out.
        var (middleware, signIn, context, nextCalled) = Build(
            principal: Principal([Roles.GlobalAdmin]),
            user: null,            // FindByIdAsync → null
            dbRoles: null);

        await middleware.InvokeAsync(context);

        await signIn.Received(1).SignOutAsync();
        Assert.Equal("/Account/Login?error=account-removed",
            context.Response.Headers["Location"].ToString());
        Assert.False(nextCalled[0]);
    }

    [Fact]
    public async Task RoleSetMismatch_SignsOutAndRedirectsRoleChanged()
    {
        // The cookie carries GlobalAdmin but the DB's current role set has been
        // demoted to Member → the standing changed while the session was live →
        // sign out so the resident re-authenticates to pick up the new claims.
        var (middleware, signIn, context, nextCalled) = Build(
            principal: Principal([Roles.GlobalAdmin]),   // cookie: GlobalAdmin
            user: new User { Id = Subject },
            dbRoles: [Roles.Member]);                    // DB: Member (a demotion)

        await middleware.InvokeAsync(context);

        await signIn.Received(1).SignOutAsync();
        Assert.Equal("/Account/Login?error=role-changed",
            context.Response.Headers["Location"].ToString());
        Assert.False(nextCalled[0]);
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the middleware over a <see cref="DefaultHttpContext"/>.
    /// <paramref name="principal"/> is set on <c>context.User</c>;
    /// <paramref name="user"/> is what the substituted
    /// <see cref="IUserRoleStore{TUser}"/>.<c>FindByIdAsync</c> returns
    /// (null = the account was deleted); <paramref name="dbRoles"/> is what
    /// <c>GetRolesAsync</c> returns (the database's current role set).
    /// </summary>
    private static (Kumunita.Web.Security.PrivilegedStampMiddleware middleware,
        SignInManager<User> signIn, HttpContext context, bool[] nextCalled) Build(
        ClaimsPrincipal principal, User? user, string[]? dbRoles)
    {
        var context = new DefaultHttpContext();
        context.User = principal;

        var userStore = Substitute.For<IUserRoleStore<User>>();
        userStore.FindByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(user));
        if (dbRoles is not null)
            userStore.GetRolesAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult((IList<string>)dbRoles));

        var userManager = new UserManager<User>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            new[] { new UserValidator<User>() },
            new[] { new PasswordValidator<User>() },
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            EmptyServiceProvider.Instance,
            NullLogger<UserManager<User>>.Instance);

        var signIn = BuildSignInManager(userManager);

        var services = new ServiceCollection()
            .AddSingleton<UserManager<User>>(userManager)
            .AddSingleton<SignInManager<User>>(signIn)
            .BuildServiceProvider();
        context.RequestServices = services;

        var nextCalled = new bool[1];
        var middleware = new Kumunita.Web.Security.PrivilegedStampMiddleware(ctx =>
        {
            nextCalled[0] = true;    // the downstream "handler"
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        });

        return (middleware, signIn, context, nextCalled);
    }

    /// <summary>An authenticated principal with the given role claims.</summary>
    private static ClaimsPrincipal Principal(params string[] roles)
        => new(new ClaimsIdentity(
            new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Subject) }
                .Concat(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r))),
            "test"));

    /// <summary>
    /// A proxy of the concrete <see cref="SignInManager{TUser}"/> — its
    /// <see cref="SignInManager{TUser}.SignOutAsync()"/> is <c>virtual</c>, so
    /// NSubstitute intercepts it and the internals never run. The non-user-manager
    /// constructor dependencies are inert stubs. The key assertion is the redirect
    /// URL + the sign-out call count.
    /// </summary>
    /// <remarks>
    /// .NET 10 <see cref="SignInManager{TUser}"/> constructor (7 params —
    /// <c>ISecurityTokenValidator</c> was removed in .NET 10, replaced by
    /// <c>IUserClaimsPrincipalFactory{TUser}</c> at position 3 and
    /// <c>IAuthenticationSchemeProvider</c> at position 6).
    /// </remarks>
    private static SignInManager<User> BuildSignInManager(UserManager<User> userManager)
        => Substitute.For<SignInManager<User>>(
            userManager,
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
