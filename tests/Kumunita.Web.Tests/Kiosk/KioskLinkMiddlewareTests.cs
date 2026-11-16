using System.IO;
using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Core.Kiosk;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

using KClaim = System.Security.Claims.Claim;
using KIdentity = System.Security.Claims.ClaimsIdentity;
using KPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0302 (IMPROVE-UX R16) — the pinned tests for
/// <see cref="KioskLinkMiddleware"/>. The unit card's "Done when" (verbatim):
/// "tests: token on <c>/posts</c> → refused; on <c>/kiosk</c> → playlist."
/// <list type="bullet">
/// <item>(a) a <b>valid token on <c>/kiosk</c></b> → the middleware signs the
/// owner in (the owner's playlist then renders via the controller — the
/// <see cref="KioskController"/> pin) and passes through (NOT a login
/// redirect): <c>SignInAsync</c> is called with the owner principal.</item>
/// <item>(b) the <b>same valid token on <c>/posts</c></b> (any non-kiosk
/// path) → refused: no principal is minted (<c>SignInAsync</c> is NOT
/// called, <c>context.User</c> stays anonymous), so the surface's own
/// <c>[Authorize]</c> denies it.</item>
/// <item>(c) a <b>revoked / unknown token on <c>/kiosk</c></b> → refused:
/// the Core seam returns <c>null</c>, no principal is minted, the request
/// passes through to the surface's own shape (not a crash).</item>
/// <item>(d) a <b>token on <c>/kiosk/slide</c></b> validates the same way as
/// on <c>/kiosk</c> (both are the ADR 0302 D4 surfaces).</item>
/// <item>(e) the minted principal carries the <b>owner's
/// <c>Kumunita.Sub</c></b> + the <b><c>kiosk-link</c> role</b> and <b>no</b>
/// <c>Member</c>/<c>Moderator</c>/<c>GlobalAdmin</c> role (the read-only pin)
/// and <b>only</b> <see cref="ClaimTypes.All"/> claim types (trap B —
/// <see cref="ClaimShapingInvariantBTests"/> stays green).</item>
/// <item>(f) the <b>rate-limit policy</b> "kiosk-link" is present on the
/// token-presenting surface (<c>GET /kiosk</c>) (the "rate-limited" line).</item>
/// </list>
/// <para>
/// Harness: the house <b>direct-construction</b> middleware idiom (the
/// <see cref="RevocationStampFailClosedTests"/> /
/// <see cref="AdminTwoFactorMiddlewareTests"/> shape) — an
/// <see cref="IKioskLinkService"/> substitute (the R15 Core seam), an
/// <see cref="IAuthenticationService"/> stub that records
/// <c>SignInAsync</c> (the middleware's sign-in seam), a
/// <see cref="ServiceCollection"/> as <c>RequestServices</c>, and a
/// <see cref="DefaultHttpContext"/> with an in-memory response body. No
/// Postgres (the Core seam is substituted; the hash-at-rest behaviour is
/// pinned by the <c>KioskLinkServiceTests</c> R16 overload test).
/// </para>
/// </summary>
public sealed class KioskLinkMiddlewareTests
{
    private const string Owner = "kiosk-link-owner-001";
    private const string Token = "kskl_test-token-0001";

    // ── (a) valid token on /kiosk → the owner signs in ────────────────────

    [Fact]
    public async Task ADR0302_R16_ValidToken_OnKiosk_SignsInOwner()
    {
        var service = StubService(valid: true);
        var r = await Run(path: "/kiosk", link: Token, service: service);

        Assert.True(r.NextCalled, "a valid token must pass through to the surface (the owner's playlist renders)");
        Assert.True(r.SignInCalled, "a valid token on /kiosk must sign the owner in");
        Assert.True(
            CookieAuthenticationDefaults.AuthenticationScheme == r.SignedInScheme,
            "the sign-in must be on the host auth scheme (the kumunita.auth cookie the slide-refresh loop carries)");
        Assert.NotNull(r.SignedInPrincipal);
        Assert.True(Owner == KumunitaPrincipal.SubjectId(r.SignedInPrincipal!), "the signed-in principal must carry the owner's subject");
        Assert.NotNull(r.FinalUser);
        Assert.True(Owner == KumunitaPrincipal.SubjectId(r.FinalUser), "context.User must carry the owner's subject");
    }

    // ── (b) the same valid token on /posts → refused (inert) ──────────────

    [Fact]
    public async Task ADR0302_R16_ValidToken_OnNonKioskPath_IsInert()
    {
        var service = StubService(valid: true);
        var r = await Run(path: "/posts", link: Token, service: service);

        Assert.True(r.NextCalled, "the request still passes through (to the surface's own gate)");
        Assert.False(r.SignInCalled, "a token on a non-kiosk path must mint NO principal (inert)");
        Assert.False(r.FinalUser?.Identity?.IsAuthenticated ?? false,
            "the surface's own [Authorize] sees an anonymous user and denies");
        // The Core seam was not even consulted (the fast path short-circuits).
        await service.DidNotReceiveWithAnyArgs().ValidatePresentedAsync(default!);
    }

    // ── (c) revoked / unknown token on /kiosk → refused ───────────────────

    [Fact]
    public async Task ADR0302_R16_RevokedOrUnknownToken_OnKiosk_IsRefused()
    {
        // The Core seam returns null (revoked / unknown / blank).
        var service = StubService(valid: false);
        var r = await Run(path: "/kiosk", link: Token, service: service);

        Assert.True(r.NextCalled, "a refused token still passes through to the surface's own shape (not a crash)");
        Assert.False(r.SignInCalled, "a refused token must mint NO principal");
        Assert.False(r.FinalUser?.Identity?.IsAuthenticated ?? false,
            "the surface's own [Authorize] sees an anonymous user (the existing sign-in / denied shape)");
        await service.Received(1).ValidatePresentedAsync(Token);   // the seam WAS consulted (it refused)
    }

    [Fact]
    public async Task ADR0302_R16_NoToken_OnKiosk_IsInert()
    {
        // A signed-in-only kiosk (no ?link=) — the token is absent: inert,
        // the surface's own gate handles it. The seam is not even consulted.
        var service = StubService(valid: true);
        var r = await Run(path: "/kiosk", link: null, service: service);

        Assert.True(r.NextCalled);
        Assert.False(r.SignInCalled, "no ?link= token → no principal minted");
        await service.DidNotReceiveWithAnyArgs().ValidatePresentedAsync(default!);
    }

    [Fact]
    public async Task ADR0302_R16_SignedInOwnerOnKiosk_PersonalKioskUntouched()
    {
        // ADR 0302 D5 — the personal kiosk (a signed-in owner) is untouched:
        // the token is not needed and must not override the session.
        var service = StubService(valid: true);
        var r = await Run(path: "/kiosk", link: Token, service: service,
            alreadyAuthed: new KPrincipal(new KIdentity(
                [new KClaim(Kumunita.Core.Identity.ClaimTypes.Subject, Owner),
                 new KClaim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.Member)],
                "existing-session")));

        Assert.True(r.NextCalled, "a signed-in owner on /kiosk must pass through (their own full-visibility kiosk)");
        Assert.False(r.SignInCalled, "the token must NOT override the owner's existing session");
        await service.DidNotReceiveWithAnyArgs().ValidatePresentedAsync(default!);
    }

    // ── (d) a token on /kiosk/slide validates the same way ────────────────

    [Fact]
    public async Task ADR0302_R16_ValidToken_OnKioskSlide_SignsInOwner()
    {
        var service = StubService(valid: true);
        var r = await Run(path: "/kiosk/slide", link: Token, service: service);

        Assert.True(r.NextCalled, "a valid token on /kiosk/slide must pass through to the surface");
        Assert.True(r.SignInCalled, "a valid token on /kiosk/slide signs the owner in (the ADR 0302 D4 surface)");
        Assert.NotNull(r.SignedInPrincipal);
        Assert.True(Owner == KumunitaPrincipal.SubjectId(r.SignedInPrincipal!), "the slide principal must carry the owner's subject");
    }

    // ── (e) the minted principal's exact claim set (trap B) ───────────────

    [Fact]
    public async Task ADR0302_R16_MintedPrincipal_IsReadOnlyOwner_KioskLinkRole_OnlyAdmissibleTypes()
    {
        var service = StubService(valid: true);
        var r = await Run(path: "/kiosk", link: Token, service: service);
        Assert.NotNull(r.SignedInPrincipal);
        var p = r.SignedInPrincipal!;

        // The owner's subject (every read audits under the owner, ADR 0302 D4).
        Assert.True(Owner == KumunitaPrincipal.SubjectId(p), "the principal must carry the owner's subject");
        // The single kiosk-link role value (R17's filter switches on it).
        Assert.True(KumunitaPrincipal.HasRole(p, Roles.KioskLink), "the kiosk-link role must be present");
        // Read-only — NO write standing (the ADR 0302 D4 read-only pin).
        Assert.False(KumunitaPrincipal.HasRole(p, Roles.Member));
        Assert.False(KumunitaPrincipal.HasRole(p, Roles.Moderator));
        Assert.False(KumunitaPrincipal.IsGlobalAdmin(p));
        // Trap B — only the admissible claim types (no new claim type;
        // ClaimShapingInvariantBTests stays green). The kiosk-link marker is a
        // role VALUE, not a claim TYPE.
        var types = p.Claims.Select(c => c.Type).ToHashSet();
        Assert.All(types, t => Assert.True(Kumunita.Core.Identity.ClaimTypes.All.Contains(t), $"claim type {t} must be admissible"));
        // Unverified (the link is a viewer credential, not a verified-resident
        // standing).
        Assert.False(KumunitaPrincipal.IsVerifiedResolved(p), "the link principal is unverified");
    }

    // ── (f) the rate-limit policy is present on the token surface ─────────

    [Fact]
    public void ADR0302_R16_RateLimitPolicy_PresentOnKioskSurface()
    {
        // The house "string pin, no TestServer" idiom (the
        // F7F8_HardeningGateTests shape): read the source, assert the contract.
        var root = RepoRoot();
        var program = File.ReadAllText(Path.Combine(root, "src", "Kumunita.Web", "Program.cs"));
        Assert.True(
            program.Contains("AddWindow(opts, \"kiosk-link\"", StringComparison.Ordinal),
            "the 'kiosk-link' fixed-window policy must be defined in the AddRateLimiter block");

        var controller = File.ReadAllText(
            Path.Combine(root, "src", "Kumunita.Web", "Controllers", "KioskController.cs"));
        Assert.True(
            controller.Contains("[EnableRateLimiting(\"kiosk-link\")]", StringComparison.Ordinal),
            "the token-presenting surface (GET /kiosk) must carry the kiosk-link rate-limit policy");
    }

    // ── Harness ────────────────────────────────────────────────────────────

    private static IKioskLinkService StubService(bool valid)
    {
        var service = Substitute.For<IKioskLinkService>();
        service.ValidatePresentedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<KioskLink?>(valid
                ? new KioskLink { Id = "link-1", OwnerSubjectId = Owner, PlaylistId = Owner, TokenHash = "hash" }
                : null));
        return service;
    }

    private sealed record RunResult(
        bool NextCalled, bool SignInCalled, string? SignedInScheme,
        KPrincipal? SignedInPrincipal, KPrincipal? FinalUser);

    private static async Task<RunResult> Run(
        string path, string? link, IKioskLinkService service, KPrincipal? alreadyAuthed = null)
    {
        var authService = new RecordingAuthService();
        var services = new ServiceCollection()
            .AddSingleton<IKioskLinkService>(service)
            .AddSingleton<IAuthenticationService>(authService)
            .BuildServiceProvider();

        var ctx = new DefaultHttpContext { RequestServices = services };
        ctx.Request.Path = path;
        if (link is not null)
        {
            ctx.Request.QueryString = new QueryString(
                $"?{KioskLinkMiddleware.LinkQueryParam}={Uri.EscapeDataString(link)}");
        }
        if (alreadyAuthed is not null)
        {
            ctx.User = alreadyAuthed;
        }
        // The DefaultHttpContext's response Body is an empty, non-writable
        // stream — back it with a real in-memory stream (the house
        // middleware-test idiom).
        ctx.Response.Body = new System.IO.BufferedStream(new MemoryStream());

        var nextCalled = false;
        RequestDelegate next = c =>
        {
            nextCalled = true;
            c.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var mw = new KioskLinkMiddleware(next);
        await mw.InvokeAsync(ctx);

        var firstSignIn = authService.SignInCalls.FirstOrDefault();
        return new RunResult(
            NextCalled:     nextCalled,
            SignInCalled:   authService.SignInCalls.Count > 0,
            SignedInScheme: firstSignIn.Scheme,
            SignedInPrincipal: firstSignIn.Principal,
            FinalUser:      ctx.User);
    }

    /// <summary>A manual <see cref="IAuthenticationService"/> (not NSubstitute —
    /// to avoid .NET 10's overload-resolution ambiguity, the
    /// <see cref="RevocationStampFailClosedTests"/> StubAuthService shape)
    /// that records <c>SignInAsync</c> and answers <c>AuthenticateAsync</c>
    /// with a null ticket (the unauthenticated shape — the test's
    /// <c>context.User</c> is the only source of an authenticated principal).
    /// The interface's five members are matched exactly (the working-stub
    /// shape in <c>RevocationStampFailClosedTests</c> /
    /// <c>SessionRevocationMiddlewareTests</c>).</summary>
    private sealed class RecordingAuthService : IAuthenticationService
    {
        public List<(string? Scheme, ClaimsPrincipal? Principal)> SignInCalls { get; } = [];

        public Task SignInAsync(HttpContext httpContext, string? scheme, ClaimsPrincipal principal,
            AuthenticationProperties? properties)
        {
            SignInCalls.Add((scheme, principal));
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext httpContext, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext httpContext, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext httpContext, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext httpContext, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }

    /// <summary>Locate the repo root (the
    /// <see cref="F7F8_HardeningGateTests"/> repo-root idiom — the nearest
    /// ancestor holding <c>Kumunita.slnx</c>).</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
            dir = dir.Parent;
        Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
        return dir!.FullName;
    }
}
