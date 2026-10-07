using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// M28 (ADR 0151, D5) — <em>guardian time-limit</em> enforcement: a child whose
/// guardian-set <see cref="GuardianTimeLimitSchedule"/> window says "not now"
/// (evaluated in the child's ADR 0019 effective zone) is signed out and pushed
/// to the login page with a time-limit-specific message <em>before any handler
/// runs</em>. This is the **inverse of the M20 notification-quiet lane**
/// (ADR 0121): M20 defers a resident's own *notification emails*; M28 gates a
/// child's *whole-platform access* as a hard sign-out.
/// <para>
/// The exact <see cref="BlockedAccountMiddleware"/> shape — anonymous pass,
/// the subject claim read, the single-row <c>mt</c> load from
/// <see cref="IUserInfoService.GetActiveTimeLimitAsync(string)"/>, the
/// ADR 0019 effective-zone resolution via <see cref="EffectiveTimezoneResolver"/>
/// (the <c>Profile.TimeZone</c> override → platform default → <c>UTC</c> floor,
/// the <c>EventReminderService.TryResolveZone</c> shape), the
/// <see cref="GuardianTimeLimitEvaluator.IsAllowedNow"/> call, the
/// <see cref="SignInManager{User}.SignOutAsync()"/> + the
/// <c>?error=time-limit</c> redirect.
/// </para>
/// <para>
/// **Invariants held** (design doc §2.4, ADR 0151):
/// <list type="bullet">
///   <item><c>C-M28·1</c> — the effect is a **whole-platform sign-out**, not a
///     route-by-route <c>403</c>/<c>404</c>; a restricted child is out.</item>
///   <item><c>C-M28·2</c> — the verdict is a **pure function**: this middleware
///     calls the pure <see cref="GuardianTimeLimitEvaluator.IsAllowedNow"/>
///     (no <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>, no
///     <c>CanAsync</c>/<c>CanSeeAsync</c>, no <c>Decide()</c> branch) and reads
///     only the schedule row — never the child's content (G·1-adjacent).</item>
///   <item><c>C-M28·3</c> — the floor: a <c>null</c> / <c>Enabled == false</c>
///     schedule is never restricted (allowed).</item>
///   <item><c>C-M28·5</c> — **zero new authorization surface**: no
///     <c>AccessAction</c>, no <c>AccessVia</c>, no <c>IAuthorizationService</c>
///     call. The frozen <c>IUserInfoService</c> GU read seam is the only seam
///     touched.</item>
///   <item><c>C-M28·6</c> — the verdict runs in the **child's** effective zone
///     (ADR 0019, wall-clock-first), never <c>UTC</c> unless the child has no
///     override and the platform has no default (the resolver's <c>UTC</c>
///     floor).</item>
/// </list>
/// The <c>?error=time-limit</c> code is **distinct** from <c>?error=blocked</c>
/// — the child sees a different message ("outside your allowed hours" vs
/// "account suspended"). Registered in <c>Program.cs</c> **after**
/// <see cref="BlockedAccountMiddleware"/> (a fully-blocked account hits the
/// <c>blocked</c> landing first; M28 is the time-window layer, the second gate,
/// not the total-block layer).
/// </para>
/// <para>
/// <b>Dependency scoping</b> (the <see cref="BlockedAccountMiddleware"/>
/// captive-dependency note): <see cref="IUserInfoService"/>,
/// <see cref="EffectiveTimezoneResolver"/> and
/// <see cref="SignInManager{User}"/> all resolve to <em>scoped</em> services,
/// and <c>UseMiddleware&lt;T&gt;()</c> constructs the middleware <em>once at app
/// startup</em> from the root provider. Injecting scoped services into that
/// root-lifetime instance would hold them across requests, so
/// <see cref="InvokeAsync"/> resolves them from
/// <see cref="HttpContext.RequestServices"/> (the per-request scoped provider)
/// instead; the only thing constructor-injected is the
/// <see cref="RequestDelegate"/> — exactly the shape the ASP.NET Core
/// middleware-class contract requires.
/// </para>
/// </summary>
public sealed class TimeLimitMiddleware
{
    private readonly RequestDelegate _next;

    // Conforms to the documented middleware-class shape (learn.microsoft.com/aspnet/core/fundamentals/
    // middleware/write#middleware-class): "a public constructor with a parameter of type RequestDelegate."
    // Without this the app fails to start with
    // "A suitable constructor for type 'TimeLimitMiddleware' could not be located."
    public TimeLimitMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // (1) Anonymous → pass. A signed-out child is already out.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // (2) Resolve the subject (the signed-in principal). Fully qualified:
        // Kumunita.Core.Identity.ClaimTypes collides in name with
        // System.Security.Claims.ClaimTypes, so the local alias is unresolvable here.
        var subject = context.User.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            await _next(context);
            return;
        }

        // Resolved from the CURRENT request's scope (see the scoping note on this
        // class), not held across requests — see the class-level remarks for why
        // constructor injection would be wrong here.
        var requestServices = context.RequestServices;

        // (3) The enforcement read (the U03 seam, child-keyed, no guardian gate,
        // no audit row). The floor (C-M28·3): null / disabled → never restricted.
        var schedule = await requestServices
            .GetRequiredService<IUserInfoService>()
            .GetActiveTimeLimitAsync(subject);
        if (schedule is null || !schedule.Enabled)
        {
            await _next(context);
            return;
        }

        // (4) The child's ADR 0019 effective zone (wall-clock-first, C-M28·6):
        // Profile.TimeZone override → platform default → the UTC floor. The
        // resolver reads THIS request's principal (the child) — never the
        // guardian's zone. One scoped instance per request; the read is the
        // same cost profile as the BlockedAccountMiddleware's single-row load.
        var effectiveZone = await requestServices
            .GetRequiredService<EffectiveTimezoneResolver>()
            .GetAsync();

        // (5) The pure verdict (C-M28·2/C-M28·5 — a function of (schedule, now,
        // zone); no IAuthorizationService, no Decide() branch).
        if (GuardianTimeLimitEvaluator.IsAllowedNow(schedule, DateTimeOffset.Now, effectiveZone))
        {
            await _next(context);
            return;
        }

        // Restricted right now: sign out (clears BOTH of the app's cookies — the
        // kumunita.auth cookie and the auxiliary .AspNetCore.Identity.Application
        // cookie — exactly as BlockedAccountMiddleware) and land the child on the
        // login page with a time-limit-specific message (the ?error=time-limit
        // binding on the Login view — the ?error=blocked precedent, a distinct
        // code). Deliberately NOT a 403 or an AccessDenied redirect: the
        // restriction is whole-platform and should force a full sign-out, not
        // just deny the one route (C-M28·1).
        await requestServices.GetRequiredService<SignInManager<User>>().SignOutAsync();
        context.Response.Redirect("/Account/Login?error=time-limit");
        await context.Response.CompleteAsync();
    }
}
