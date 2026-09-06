using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Kumunita.Web;

/// <summary>
/// The Identity ↔ cookie claim-shaping seam (step 6, plan item 8): the <em>only</em>
/// place in the Web host that mints the admissible claim set for a signed-in user.
/// <para>
/// Replaces the <see cref="UserClaimsPrincipalFactory{TUser, TRole}"/> default (which
/// would mint standard Identity claims like "http://schemas.xmlsoap.org/…" that violate
/// the no-relational-data invariant set B). This factory mints <em>exactly</em>
/// <see cref="ClaimTypes.All"/> and nothing else, using the same logic as
/// <see cref="IIdentityService.GetBySubjectAsync"/>: verified-flag from the <c>Profile</c>,
/// role strings from the Identity role table, and per-component scoping from
/// <see cref="ModeratorAssignment"/>.
/// <para>
/// "Cannot sign in until verified" gate: <see cref="Roles.Member"/> is added to the
/// role claim set only when <see cref="Profile.Verified"/> is true (the same conditional
/// in <c>GetBySubjectAsync</c> line 69) — so <c>[Authorize(Roles = "Member")]</c> on
/// protected surfaces denies unverified users naturally, without a separate middleware.
/// </para>
/// </summary>
public sealed class KumunitaClaimsPrincipalFactory(
    UserManager<User> userManager,
    RoleManager<IdentityRole> roleManager,
    IUserInfoService userInfo,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User, IdentityRole>(userManager, roleManager, options)
{
    /// <inheritdoc />
    /// <remarks>
    /// The factory is called once per sign-in (when
    /// <c>SignInManager.PasswordSignInAsync</c> succeeds). The cookie holds the resulting
    /// claim set; on each subsequent request the cookie deserializes to a
    /// <see cref="System.Security.Claims.ClaimsPrincipal"/> with <em>exactly</em> these
    /// same claims, so <c>ClaimsSource.Current ⇒ HttpContext.User</c> carries the full
    /// principal with no additional DB read per request.
    /// </remarks>
    public override async Task<System.Security.Claims.ClaimsPrincipal> CreateAsync(User user)
    {
        // Read the mt-side state that drives the claim shape: Profile.Verified gates the
        // Member role; Profile.Blocked strips ALL standing; ModeratorAssignment drives the
        // per-component scope claims. Both are single-row loads keyed on user.Id.
        var profile = await userInfo.GetProfileAsync(user.Id ?? string.Empty);
        var verified = profile?.Verified ?? false;
        var blocked = profile?.Blocked ?? false;

        // A blocked resident has NO standing: mint no roles (no Member / Moderator /
        // GlobalAdmin), exactly as an unverified resident is denied Member. The suspension
        // is carried inside the admissible claim set (no new claim type — the no-relational-
        // data invariant holds), and this factory is the single place the claim set is minted
        // (ADR 0006-B), so the cookie never holds standing for a blocked account.
        var roles = blocked
            ? Array.Empty<string>()
            : await BuildRoleListAsync(user, verified);

        return ClaimShaping.Build(
            subjectId: user.Id ?? string.Empty,
            externalId: user.ExternalId,
            verified: verified,
            roles: roles);
    }

    /// <summary>
    /// Mirrors <c>IdentityService.GetBySubjectAsync</c> lines 66-80 (the roles
    /// computation) so the claim set and the thin-principal API surface always
    /// agree. <see cref="Roles.Member"/> is added only for verified residents.
    /// </summary>
    private async Task<IReadOnlyList<string>> BuildRoleListAsync(User user, bool verified)
    {
        var userRoleNames = (await userManager.GetRolesAsync(user)).ToList();

        var roles = new List<string>();
        if (verified)
            roles.Add(Roles.Member);          // Member = verified-resident standing

        roles.AddRange(userRoleNames);        // explicit Identity roles (GlobalAdmin, Moderator, …)

        if (userRoleNames.Contains(Roles.Moderator))
        {
            var assignments = await userInfo.GetAssignmentsAsync(user.Id ?? string.Empty);
            foreach (var a in assignments)
                roles.Add(Roles.ModeratorComponent(a.ComponentId));
        }

        return roles;
    }
}
