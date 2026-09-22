using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Web.Security;

/// <summary>
/// Privilege-revocation enforcement for elevated-role sessions (M4 / OPS §10).
/// <para>
/// The <see cref="KumunitaClaimsPrincipalFactory"/> mints the claim set ONCE at
/// sign-in; the <c>SecurityStampValidator</c> is intentionally disabled within
/// live sessions (its <c>ValidationInterval</c> equals the 14-day cookie lifetime,
/// <c>Program.cs</c>). That is correct for ordinary residents (their standing is
/// just <c>Member</c> — no privilege to revoke), but it leaves a gap for
/// <em>privileged</em> roles: a demoted GlobalAdmin or reassigned Moderator keeps
/// their cookie-carried role claims until sign-out or cookie expiry.
/// </para>
/// <para>
/// This middleware closes that gap for <em>privileged</em> users only. On every
/// request from a principal carrying an elevated role claim
/// (<c>GlobalAdmin</c>, <c>Moderator</c>, <c>Translator</c>, or a
/// <c>moderator:*</c> component scope), it re-reads the user's current role set
/// from the database and compares it to the cookie's role claims. If they differ
/// (a promotion, demotion, or scope change happened while the session was live),
/// the middleware signs the user out and redirects to the login page — the
/// resident must re-authenticate to pick up the new claim set.
/// </para>
/// <para>
/// <b>Cost:</b> one EF read (roles) + one <c>mt</c> read (moderator assignments,
/// only when the <c>Moderator</c> role is present) per request <em>for privileged
/// users only</em>. In a single-neighborhood deployment that is a handful of
/// GlobalAdmins / Moderators / Translators — negligible. Ordinary residents
/// (the vast majority of traffic) pass through with zero additional DB hits.
/// </para>
/// <para>
/// Registered in <c>Program.cs</c> between <c>UseAuthentication</c> and
/// <c>UseAuthorization</c>, after <see cref="BlockedAccountMiddleware"/>
/// (block enforcement is the higher-priority gate: a blocked user is signed out
/// unconditionally, regardless of roles).
/// </para>
/// </summary>
public sealed class PrivilegedStampMiddleware
{
    private readonly RequestDelegate _next;

    public PrivilegedStampMiddleware(RequestDelegate next)
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

        // Fast path: only privileged users need the stamp re-read. An ordinary
        // resident (Member only, no elevated roles) has no privilege to revoke —
        // their standing is the implicit verified-resident floor, which is
        // re-checked by BlockedAccountMiddleware (Profile.Blocked) and the
        // AuthorizationService per-request anyway.
        var hasElevatedRole = context.User.Claims.Any(c =>
            c.Type == ClaimTypes.Role &&
            (c.Value == Roles.GlobalAdmin
             || c.Value == Roles.Moderator
             || c.Value == Roles.Translator
             || c.Value.StartsWith(Roles.ModeratorComponent(string.Empty), StringComparison.Ordinal)));

        if (!hasElevatedRole)
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

        // Resolved from the CURRENT request's scope (same pattern as
        // BlockedAccountMiddleware — scoped services must not be constructor-
        // injected into a root-lifetime middleware).
        var requestServices = context.RequestServices;
        var userManager = requestServices.GetRequiredService<UserManager<User>>();

        var user = await userManager.FindByIdAsync(subject);
        if (user is null)
        {
            // The user was deleted while signed in — sign out.
            await requestServices.GetRequiredService<SignInManager<User>>().SignOutAsync();
            context.Response.Redirect("/Account/Login?error=account-removed");
            await context.Response.CompleteAsync();
            return;
        }

        // Re-read the current role set from the database.
        var currentRoles = (await userManager.GetRolesAsync(user)).ToHashSet();

        // Moderator component scopes (only present when the Moderator role is held).
        var currentModeratorScopes = new HashSet<string>();
        if (currentRoles.Contains(Roles.Moderator))
        {
            var userInfo = requestServices.GetRequiredService<IUserInfoService>();
            var assignments = await userInfo.GetAssignmentsAsync(subject);
            foreach (var a in assignments)
                currentModeratorScopes.Add(Roles.ModeratorComponent(a.ComponentId));
        }

        // The expected role claim set (Identity roles + component scopes).
        // Note: Member is the implicit verified-resident standing — it is not in
        // the Identity role table, so it is not part of this comparison. A
        // de-verified user would have their Member role stripped at the factory
        // level on re-sign-in; this middleware only tracks the *elevated* roles
        // that come from the Identity role table.
        var expectedRoles = new HashSet<string>(currentRoles);
        expectedRoles.UnionWith(currentModeratorScopes);

        // The cookie's current role claim set.
        var cookieRoles = context.User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet();

        // Strip the implicit Member claim (it's not in the Identity role table
        // and its presence is driven by Profile.Verified, which
        // BlockedAccountMiddleware already handles). Compare the rest.
        cookieRoles.Remove(Roles.Member);
        expectedRoles.Remove(Roles.Member);

        if (!expectedRoles.SetEquals(cookieRoles))
        {
            // Role set changed while the session was live (promotion, demotion,
            // or component-scope reassignment). Sign out; the resident must
            // re-authenticate to pick up the new claim set.
            await requestServices.GetRequiredService<SignInManager<User>>().SignOutAsync();
            context.Response.Redirect("/Account/Login?error=role-changed");
            await context.Response.CompleteAsync();
            return;
        }

        await _next(context);
    }
}
