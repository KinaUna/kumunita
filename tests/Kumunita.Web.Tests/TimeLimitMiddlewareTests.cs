using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
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
/// M28 (ADR 0151, D5 / C-M28·1 / C-M28·3) — the <see cref="TimeLimitMiddleware"/>
/// (the <see cref="BlockedAccountMiddleware"/> analog): a child whose
/// guardian-set <see cref="GuardianTimeLimitSchedule"/> window says "not now"
/// (evaluated in the child's ADR 0019 effective zone) is signed out and pushed
/// to <c>/Account/Login?error=time-limit</c> <em>before any handler runs</em>.
/// The three F6 tests (design doc §2.6 items 15–17) pin the sign-out + redirect
/// shape for the restricted path and the pass-through for the two allowed
/// paths (C-M28·3 — the floor: no schedule / allowed window = never restricted).
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute seams + <see cref="DefaultHttpContext"/> — the shape
/// <see cref="BlockedAccountMiddlewareTests"/> uses), <b>not</b> a full host
/// boot. The seams under test are the middleware's
/// <see cref="HttpContext.RequestServices"/> resolution of
/// <see cref="IUserInfoService"/> (the enforcement read
/// <c>GetActiveTimeLimitAsync</c> + the profile read the resolver uses for the
/// ADR 0019 zone), <see cref="Kumunita.Web.Localization.EffectiveTimezoneResolver"/>
/// (the real scoped resolver — its <c>GetAsync()</c> is non-virtual, so it is
/// driven with a stubbed <c>IUserInfoService</c>/<c>ILocalizationService</c> +
/// an <c>IHttpContextAccessor</c> bound to the test context), and
/// <see cref="SignInManager{User}"/>. A stubbed scoped provider means no
/// Postgres. The production middleware is <b>correct and untouched</b>; this
/// only pins its sign-out + redirect shape for the restricted path and the
/// pass-through for the two allowed paths (D5).
/// </para>
/// <para>
/// Zone determinism: the harness forces the child's ADR 0019 effective zone to
/// <c>UTC</c> (the <c>Profile.TimeZone</c> override is stubbed to <c>"UTC"</c>)
/// so the <see cref="GuardianTimeLimitEvaluator.IsAllowedNow"/> verdict is
/// deterministic regardless of the machine's local clock. The two schedules
/// used here make the window match <em>all</em> hours × <em>all</em> days
/// (empty <c>Hours</c> / <c>DaysOfWeek</c> = "all"), so the verdict is the same
/// for any <c>now</c>: a <c>Blocked</c> schedule is always restricted (the
/// "total block" reading), an <c>Allowed</c> schedule is always allowed (the
/// floor). This isolates the <em>polarity</em> the F6 tests target without
/// coupling the assertions to the wall clock (the zone-dependent verdict is
/// pinned separately by U02's F4 pure test).
/// </para>
/// </summary>
public class TimeLimitMiddlewareTests
{
    private const string Subject = "s1";

    // ── The security path (restricted → sign out + redirect) ─────────────────

    [Fact]
    public async Task F6_Restricted_SignsOutAndRedirects_TimeLimit()
    {
        // A Blocked-mode schedule with empty Hours/DaysOfWeek = "all hours ×
        // all days" window → windowMatches always true → Blocked = restricted
        // DURING the window → always restricted (the "total block" reading).
        var schedule = new GuardianTimeLimitSchedule
        {
            ChildId  = Subject,
            Enabled  = true,
            Mode     = TimeLimitMode.Blocked,
            Hours    = Array.Empty<int>(),
            DaysOfWeek = Array.Empty<int>(),
        };
        var (middleware, signIn, context, nextCalled) = Build(schedule, authenticated: true);

        await middleware.InvokeAsync(context);

        await signIn.Received(1).SignOutAsync();                                   // signed out
        Assert.Equal("/Account/Login?error=time-limit",                            // the distinct code
            context.Response.Headers["Location"].ToString());
        Assert.False(nextCalled[0]);                                              // short-circuited — no handler ran
    }

    // ── Pass-through (allowed) paths ─────────────────────────────────────────

    [Fact]
    public async Task F6_Allowed_PassesThrough()
    {
        // An Allowed-mode schedule with empty Hours/DaysOfWeek = "all hours ×
        // all days" window → windowMatches always true → Allowed = allowed
        // INSIDE the window → always allowed (the floor).
        var schedule = new GuardianTimeLimitSchedule
        {
            ChildId  = Subject,
            Enabled  = true,
            Mode     = TimeLimitMode.Allowed,
            Hours    = Array.Empty<int>(),
            DaysOfWeek = Array.Empty<int>(),
        };
        var (middleware, signIn, context, nextCalled) = Build(schedule, authenticated: true);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled[0]);                                              // the downstream handler ran
        Assert.False(context.Response.Headers.ContainsKey("Location"));          // no redirect
        await signIn.DidNotReceive().SignOutAsync();                            // no sign-out
    }

    [Fact]
    public async Task F6_NoSchedule_PassesThrough()
    {
        // The floor (C-M28·3): a child with no schedule (null) is never
        // restricted → pass through.
        var (middleware, signIn, context, nextCalled) = Build(schedule: null, authenticated: true);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled[0]);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
        await signIn.DidNotReceive().SignOutAsync();
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the middleware over a <see cref="DefaultHttpContext"/>. When
    /// <paramref name="authenticated"/> the principal carries the
    /// <see cref="Kumunita.Core.Identity.ClaimTypes.Subject"/> claim; otherwise
    /// it is an unauthenticated identity. <paramref name="schedule"/> is what
    /// the stubbed <see cref="IUserInfoService.GetActiveTimeLimitAsync(string)"/>
    /// returns (null = the "no schedule" floor, C-M28·3). The child's ADR 0019
    /// effective zone is forced to <c>UTC</c> (the <c>Profile.TimeZone</c>
    /// override is stubbed to <c>"UTC"</c>) so the
    /// <see cref="GuardianTimeLimitEvaluator.IsAllowedNow"/> verdict is
    /// deterministic (see the class-level zone-determinism note).
    /// </summary>
    private static (TimeLimitMiddleware middleware, SignInManager<User> signIn,
        HttpContext context, bool[] nextCalled) Build(GuardianTimeLimitSchedule? schedule, bool authenticated)
    {
        var context = new DefaultHttpContext();
        context.User = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity(
                  new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Subject) }, "test"))
            : new ClaimsPrincipal(new ClaimsIdentity("anon"));   // IsAuthenticated == false

        var userInfo = Substitute.For<IUserInfoService>();
        // The enforcement read the middleware calls.
        userInfo.GetActiveTimeLimitAsync(Arg.Any<string>())
            .Returns(Task.FromResult(schedule));
        // The profile read the EffectiveTimezoneResolver uses for the ADR 0019
        // zone: the override is stubbed to "UTC" so the effective zone is the
        // UTC floor (deterministic — the wall clock is UTC regardless of the
        // machine's local time).
        userInfo.GetProfileAsync(Arg.Any<string>())
            .Returns(Task.FromResult(new Profile { SubjectId = Subject, TimeZone = "UTC" }));

        var localization = Substitute.For<ILocalizationService>();
        localization.GetDefaultTimezoneAsync()
            .Returns(Task.FromResult<string?>("UTC"));

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        // The real (sealed) scoped resolver — GetAsync is non-virtual, so it
        // is driven with the stubbed seams above (not NSubstituted).
        var resolver = new Kumunita.Web.Localization.EffectiveTimezoneResolver(
            userInfo, localization, accessor);

        var signIn = BuildSignInManager();

        var services = new ServiceCollection()
            .AddSingleton<IUserInfoService>(userInfo)
            .AddSingleton<ILocalizationService>(localization)
            .AddSingleton<IHttpContextAccessor>(accessor)
            .AddSingleton<Kumunita.Web.Localization.EffectiveTimezoneResolver>(resolver)
            .AddSingleton<SignInManager<User>>(signIn)
            .BuildServiceProvider();
        context.RequestServices = services;

        // A reference-type holder (not a captured local) so the assertion reads
        // the value AFTER InvokeAsync ran, not the value captured at Build time.
        var nextCalled = new bool[1];
        var middleware = new TimeLimitMiddleware(ctx =>
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
    /// redirect URL + the sign-out call count. (The
    /// <see cref="BlockedAccountMiddlewareTests"/> harness shape, verbatim.)
    /// </summary>
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
    /// <see cref="BlockedAccountMiddlewareTests"/>.</summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
