using Kumunita.Core.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// SEC H2 — the GlobalAdmin 2FA gate (design doc §2.2, veto 2: a
/// <b>server-side gate</b>, not a redirect).
/// <para>
/// The pre-fix behaviour (audit finding H2): <c>AccountController.Login</c>
/// redirected an un-enrolled GlobalAdmin to the Mfa enrolment page
/// (<c>AccountController.cs:907</c>) after already minting the app cookie —
/// a redirect is a convenience, not a control: the admin cookie exists, and
/// nothing on later requests checks <c>TwoFactorEnabled</c>. This middleware
/// makes the gate operative: for an authenticated <b>GlobalAdmin</b> whose
/// 2FA is <b>not enrolled</b> (and who is <b>not</b> a sample-data account),
/// only the enrolment routes, logout, and static assets are reachable; every
/// other route gets a 302 to the enrolment page. Non-GlobalAdmins, enrolled
/// admins, and sample-data accounts pass through untouched.
/// </para>
/// <para>
/// <b>Allow-list (pinned, design doc §2.1):</b>
/// <c>GET/POST /settings/security/mfa</c> (<c>SecuritySettingsController.cs:268/:351</c>),
/// <c>GET/POST /settings/security/mfa/recovery</c> (<c>:405/:436</c>),
/// <c>POST /settings/security/mfa/disable</c> (<c>:481</c>),
/// <c>POST /account/logout</c>, and static assets
/// (<c>/lib</c>, <c>/css</c>, <c>/js</c>, <c>/favicon.ico</c>).
/// </para>
/// <para>
/// <b>Checks (per request):</b> role from the <c>Kumunita.Role</c> claim
/// (<c>ClaimTypes.Role</c>, <c>ThinPrincipal.cs:83</c>);
/// <c>TwoFactorEnabled</c> via
/// <c>UserManager.GetTwoFactorEnabledAsync</c>; sample-data exemption via
/// <c>IIdentityService.IsSampleDataAccountAsync</c> (<c>IIdentityService.cs:392</c>,
/// ADR 0056 "unreachable by construction" — returns false for every real
/// account). Each request re-reads the enrolment state, so disabling 2FA
/// (legally possible on the enrolment routes) re-gates the account on the
/// very next non-allow-listed request.
/// </para>
/// <para>
/// <b>Dependency scoping:</b> <see cref="IIdentityService"/> and
/// <see cref="UserManager{TUser}"/> are <em>scoped</em> services, and
/// <c>UseMiddleware&lt;T&gt;()</c> constructs the middleware <em>once at app
/// startup</em> from the root provider — injecting scoped services into that
/// root-lifetime instance would hold them across requests (the "captive
/// dependency" pitfall). This middleware therefore takes only the
/// <see cref="RequestDelegate"/> in its constructor (the documented
/// middleware-class shape) and resolves the two scoped services from
/// <see cref="HttpContext.RequestServices"/> per request — the exact pattern
/// <see cref="RevocationStampMiddleware"/> uses for
/// <see cref="UserManager{TUser}"/>.
/// </para>
/// <para>
/// <b>Registration (U8):</b> in <c>Program.cs</c>, after
/// <c>UseAuthentication</c>, before <c>UseAuthorization</c>.
/// </para>
/// </summary>
public sealed class AdminTwoFactorMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// The route prefixes an un-enrolled GlobalAdmin may reach (design doc
    /// §2.1 "Enrolment routes (H2 allow-list)" + the veto-2 decision).
    /// </summary>
    private static readonly string[] AllowedPathPrefixes =
    {
        "/settings/security/mfa",   // GET + POST /settings/security/mfa[/recovery|/disable]
        "/account/logout",
        // Static assets (design doc §2.1 pins these as /lib, /css, /js,
        // /favicon.ico — NO trailing slash: StartsWithSegments("/lib")
        // matches /lib/bootstrap/… on the segment boundary, whereas
        // StartsWithSegments("/lib/") does not).
        "/lib",
        "/css",
        "/js",
        "/favicon.ico",
    };

    /// <summary>
    /// The 302 target for a gated request: the Mfa enrolment page
    /// (<c>SecuritySettingsController.Mfa</c>, <c>GET /settings/security/mfa</c>).
    /// </summary>
    public const string EnrolmentPath = "/settings/security/mfa";

    /// <summary>
    /// Conforms to the documented middleware-class shape (learn.microsoft.com/aspnet/core/
    /// fundamentals/middleware/write#middleware-class): "a public constructor with a
    /// parameter of type RequestDelegate." Without this the app fails to start with
    /// "A suitable constructor for type 'AdminTwoFactorMiddleware' could not be located."
    /// Scoped services (<see cref="IIdentityService"/>, <see cref="UserManager{TUser}"/>)
    /// are resolved per request from <see cref="HttpContext.RequestServices"/> (see
    /// class-level doc, "Dependency scoping") — the <see cref="RevocationStampMiddleware"/>
    /// pattern.
    /// </summary>
    public AdminTwoFactorMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Non-GlobalAdmins are out of scope for this gate entirely (S-2 is a
        // GlobalAdmin-only invariant) — no service reads at all.
        var isGlobalAdmin = context.User.Claims
            .Any(c => c.Type == ClaimTypes.Role && c.Value == Roles.GlobalAdmin);
        if (!isGlobalAdmin)
        {
            await _next(context);
            return;
        }

        var subject = context.User.FindFirst(ClaimTypes.Subject)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            await _next(context);
            return;
        }

        var requestServices = context.RequestServices;
        var identity = requestServices.GetRequiredService<IIdentityService>();

        // Veto 2: sample-data accounts keep their existing exemption (the same
        // seam the login leg-1.5b branch uses at AccountController.cs:896-898).
        if (await identity.IsSampleDataAccountAsync(subject))
        {
            await _next(context);
            return;
        }

        // Enrolled admins pass through (the gate exists only for the
        // not-enrolled case).
        var userManager = requestServices.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(subject);
        if (user is null || await userManager.GetTwoFactorEnabledAsync(user))
        {
            await _next(context);
            return;
        }

        // Un-enrolled GlobalAdmin: only the enrolment / logout / static routes
        // are reachable; everything else 302s to the enrolment page.
        if (AllowedPathPrefixes.Any(p => context.Request.Path.StartsWithSegments(p)))
        {
            await _next(context);
            return;
        }

        context.Response.Redirect(EnrolmentPath);
    }
}
