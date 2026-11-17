using Kumunita.Core.Identity;
using Kumunita.Core.Kiosk;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// ADR 0302 (IMPROVE-UX R16) — the shared kiosk link's <b>authentication
/// seam</b>: the Web surface that makes a <see cref="KioskLink"/> token
/// (R15's Core seam) reachable, turning it into a usable wall-screen
/// credential. This is R16 — the missing auth R19's SEC review filed as
/// finding F1 (a resident could mint, list and revoke a token that
/// authenticated <b>nowhere</b> until this landed).
/// <para>
/// **The surface (ADR 0302 D4, the invariants this makes reachable):** a
/// presented token authenticates **only** on <c>/kiosk</c> + <c>/kiosk/slide</c>
/// — a token on any other path is <b>inert</b> (the middleware mints no
/// principal, so the surface's own <c>[Authorize]</c>/read decision denies
/// it). On the kiosk surfaces it builds a <b>restricted owner principal</b>
/// (trap B — the <see cref="ClaimTypes.All"/> admissible set only:
/// <c>Kumunita.Sub</c> = the <b>owner's</b> subject id, so every read the
/// link authenticates audits under the owner (the ADR 0302 D4 invariant);
/// <c>Kumunita.Verified</c> = <c>false</c>; and the single role value
/// <see cref="Roles.KioskLink"/> (the M19 <c>Roles.Guest</c> precedent — a new
/// role <b>value</b>, NOT a new claim <b>type</b>, so
/// <c>ClaimShapingInvariantBTests</c> stays green). It carries <b>no</b>
/// <c>Member</c> / <c>Moderator</c> / <c>GlobalAdmin</c> role (read-only — no
/// write standing). R17's community-visible filter switches on exactly that
/// role value, so the restricted principal is what makes the filter engage.
/// </para>
/// <para>
/// **The carrier (R18, already built):** the <c>/settings/kiosk</c> surface
/// hands the owner a bare token and the <c>kiosk-settings.ts</c> copy
/// affordance assembles <c>/kiosk?link=&lt;token&gt;</c> client-side — so this
/// middleware parses the <b><c>?link=</c> query param</b>. (The site sets
/// <c>Referrer-Policy: no-referrer</c> (ADR 0285 D7), so the query token does
/// not leak via <c>Referer</c>.)
/// </para>
/// <para>
/// **The cookie (why sign-in, not just <c>context.User</c>):** the kiosk's
/// slide-refresh loop (<c>client/lib/kiosk.ts</c>) re-fetches
/// <c>/kiosk/slide?slide=N</c> on a <b>same-origin, cookie-carrying</b> fetch
/// that carries <b>no</b> <c>?link=</c> token. So the principal must be
/// established as a <b>cookie</b> (the host auth scheme — the <c>kumunita.auth</c>
/// cookie real residents are minted on, the
/// <see cref="KumunitaClaimsPrincipalFactory"/> seam) on the <c>/kiosk</c>
/// page load; the subsequent refresh fetches then authenticate on that
/// cookie. The <c>context.User</c> assignment is belt-and-braces for the
/// current request (a direct-construction test stubs
/// <c>IAuthenticationService</c> and does not auto-populate it).
/// </para>
/// <para>
/// **Registered** in <c>Program.cs</c> immediately after
/// <c>UseAuthentication</c> and before <c>BlockedAccountMiddleware</c>
/// (the principal is a signed-in owner, so the downstream block /
/// time-limit gates correctly apply to the minted owner — a blocked owner's
/// link stops working — while the session/stamp revocation gates pass
/// through on the token principal's absence of a <c>kumunita.stamp</c> /
/// <c>kumunita.sid</c> cookie item, the backward-compat shape). Scoped
/// services (<see cref="IKioskLinkService"/>) are resolved per request from
/// <see cref="HttpContext.RequestServices"/> — the
/// <see cref="PrivilegedStampMiddleware"/> scoping idiom (no captive
/// dependency).
/// </para>
/// </summary>
public sealed class KioskLinkMiddleware
{
    /// <summary>The <c>?link=</c> query param the R18 surface assembles into
    /// the wall-screen URL (<c>/kiosk?link=&lt;token&gt;</c>) — the carrier
    /// this middleware parses.</summary>
    public const string LinkQueryParam = "link";

    private readonly RequestDelegate _next;

    /// <summary>Conforms to the documented middleware-class shape (a public
    /// constructor with a <see cref="RequestDelegate"/>). Scoped services
    /// (<see cref="IKioskLinkService"/>) are resolved per request from
    /// <see cref="HttpContext.RequestServices"/> — the
    /// <see cref="PrivilegedStampMiddleware"/> scoping idiom (a
    /// root-lifetime middleware must not hold scoped services across
    /// requests, the "captive dependency" pitfall).</summary>
    public KioskLinkMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    /// The two kiosk surfaces the token may reach (ADR 0302 D4:
    /// <c>/kiosk</c> + its slides). An exact <see cref="PathString"/> match,
    /// so <c>/kiosk/anything-else</c>, <c>/kioskX</c> and <c>/settings/kiosk</c>
    /// (the signed-in editor) are <b>not</b> the surface (the token is inert
    /// there — the surface's own gate handles them).
    /// </summary>
    private static bool IsKioskSurface(PathString path) =>
        path == "/kiosk" || path == "/kiosk/slide";

    public async Task InvokeAsync(HttpContext context)
    {
        // Fast path (the plan's "refuses everything else"): on any non-kiosk
        // path the token is inert — the middleware mints NO principal, so the
        // surface's own [Authorize]/read decision denies it. No service
        // reads, no sign-in.
        if (!IsKioskSurface(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Fast path (ADR 0302 D5 — the personal kiosk is untouched): a
        // signed-in owner visiting /kiosk sees their OWN full-visibility
        // kiosk; the token is not needed and must not override the session.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // The carrier (R18): ?link=<token>. Absent or blank (a signed-in-only
        // kiosk, or a tokenless wall display) → inert (the surface's own
        // [Authorize] redirects to sign-in, the existing shape — not a crash).
        if (!context.Request.Query.TryGetValue(LinkQueryParam, out var linkValues)
            || string.IsNullOrWhiteSpace(linkValues.ToString()))
        {
            await _next(context);
            return;
        }

        var presentedToken = linkValues.ToString();

        // The rate-limit gate (the plan's "rate-limited" line) runs via
        // [EnableRateLimiting("kiosk-link")] on KioskController.Index/Slide
        // (the UseRateLimiter middleware, before UseAuthentication) — a
        // leaked / brute-forced token on the kiosk surface is throttled per
        // client IP. The seam itself stays pure (no limiter coupling here).
        var linkService = context.RequestServices.GetRequiredService<IKioskLinkService>();

        // Trap A — the hashing happens IN Core (the shared internal
        // IdentityService.Sha256Hex via ValidatePresentedAsync); the Web host
        // never re-implements the digest. Owner-less: the wall screen has no
        // owner session, so the owner is a property of the link, discovered
        // by the lookup (the EscalationTokenService.ValidateAsync(plaintext)
        // precedent this ADR names as its template).
        var link = await linkService.ValidatePresentedAsync(presentedToken);
        if (link is null)
        {
            // Unknown / revoked / blank — the token is inert. NOT a crash and
            // NOT a 404: the surface's own shape is the fallback (an
            // unauthenticated /kiosk → the existing sign-in redirect; a
            // revoked link's refresh → the owner's existing denied/empty
            // shape, the M35-5 no-leak / no-404 face).
            await _next(context);
            return;
        }

        var owner = link.OwnerSubjectId;

        // Trap B — the restricted owner principal (the ClaimShaping.Build
        // admissible set; exactly ClaimTypes.All, no new claim type):
        // Kumunita.Sub = the OWNER (every read audits under the owner,
        // ADR 0302 D4), Kumunita.Verified = false, and the single role value
        // Roles.KioskLink (the M19 Roles.Guest precedent — a role VALUE, not a
        // claim TYPE, so ClaimShapingInvariantBTests stays green). No
        // Member / Moderator / GlobalAdmin — read-only, no write standing.
        // R17's community-visible filter (roles.Contains(Roles.KioskLink)) is
        // what this role value switches on.
        // TODO ❓ (for the R16 SEC review) — the wall display browser now
        // holds the owner's kumunita.auth cookie (read-only, kiosk-link role).
        // Revoking the KioskLink row does not invalidate an already-minted
        // cookie (it lapses at the 14-day cookie span or on sign-out / browser
        // clear). The standing is read-only (no write standing) so the blast
        // radius is bounded; a short cookie span or a per-request re-validate
        // is a SEC call.
        var principal = ClaimShaping.Build(
            subjectId:  owner,
            externalId: null,
            verified:   false,
            roles:      [Roles.KioskLink]);

        // The cookie (the refresh-loop carrier — see the class doc): sign in
        // on the host auth scheme (the kumunita.auth cookie real residents
        // are minted on, the SignInAfterVerifyAsync shape), so the subsequent
        // /kiosk/slide refresh fetches (cookie-carrying, no ?link=)
        // authenticate on it.
        await context.SignInAsync(
            scheme:     CookieAuthenticationDefaults.AuthenticationScheme,
            principal:  principal,
            properties: new AuthenticationProperties { IsPersistent = true });

        // Belt-and-braces for the CURRENT request's HttpContext.User (the
        // real pipeline's SignInAsync already sets it; a direct-construction
        // test stubs IAuthenticationService and does not, so the controller
        // + the downstream gates see the principal here regardless).
        context.User = principal;

        await _next(context);
    }
}
