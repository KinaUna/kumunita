using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// Block enforcement for accounts that are <em>already</em> signed in: if
/// <see cref="Profile.Blocked"/> gets set (a GlobalAdmin's suspension) while a resident
/// holds a live cookie, sign them out and push them to the login page with a
/// blocked-specific message <em>before any handler runs</em>.
/// <para>
/// The <see cref="KumunitaClaimsPrincipalFactory"/> already strips all standing at mint
/// time (a blocked principal carries no role claims), which already denies every
/// <c>[Authorize(Roles = "...")]</c> gate; this middleware closes the remaining gaps —
/// the bare <c>[Authorize]</c> actions (authenticated-any) and any handler that asserts
/// no role — plus it actively clears the stale standing the cookie still carries instead
/// of merely denying. Registered in <c>Program.cs</c> between
/// <c>UseAuthentication</c> and <c>UseAuthorization</c>.
/// </para>
/// <para>
/// Anonymous requests pass through untouched. The read is a single-row load keyed on
/// the subject claim (same shape the factory does at sign-in), so the cost on
/// authenticated traffic is one <c>mt</c> round-trip per request.
/// </para>
/// <para>
/// Dependency scoping: both <see cref="IUserInfoService"/> and
/// <see cref="SignInManager{User}"/> resolve to <em>scoped</em> services, but this
/// middleware is registered via <c>UseMiddleware&lt;T&gt;()</c>, which resolves its
/// constructor dependencies from the <em>root</em> provider (at application boot), not
/// per-request — holding root-scoped instances across the request pipeline risks
/// cross-request leakage (the "captive" dependency pitfall). So instead of
/// constructor-injecting them, <see cref="InvokeAsync"/> resolves them from
/// <see cref="HttpContext.RequestServices"/> (the request's scoped provider) on demand.
/// <see cref="InvokeAsync"/> receives the <see cref="HttpContext"/> directly from the
/// middleware pipeline, so no <see cref="IHttpContextAccessor"/> is involved.
/// </para>
/// </summary>
public sealed class BlockedAccountMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        // Fully qualified: Kumunita.Core.Identity.ClaimTypes collides in name with
        // System.Security.Claims.ClaimTypes, so the local alias is unresolvable here.
        var subject = context.User.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value;
        if (string.IsNullOrEmpty(subject))
        {
            await next(context);
            return;
        }

        // Resolved from the CURRENT request's scope (see the scoping note on this
        // class), not held across requests — see the class-level remarks for why
        // constructor injection would be wrong here.
        var requestServices = context.RequestServices;
        var userInfo = requestServices.GetRequiredService<IUserInfoService>();

        var profile = await userInfo.GetProfileAsync(subject);
        if (profile is null || !profile.Blocked)
        {
            await next(context);
            return;
        }

        // Blocked while signed in: sign out (clears BOTH of the app's cookies — the
        // kumunita.auth cookie and the auxiliary .AspNetCore.Identity.Application
        // cookie — exactly as /Account/Logout does) and land the resident on the
        // login page with a blocked-specific message (see the ?error= binding on
        // AccountController.Login GET). Deliberately NOT a 403 or a redirect to the
        // AccessDenied page: the suspension is account-level and should force a
        // full sign-out, not just deny the one route.
        await requestServices.GetRequiredService<SignInManager<User>>().SignOutAsync();
        context.Response.Redirect("/Account/Login?error=blocked");
        await context.Response.CompleteAsync();
    }
}
