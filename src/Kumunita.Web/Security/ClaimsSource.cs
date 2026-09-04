using Kumunita.Core.Identity;
namespace Kumunita.Web.Security;

/// <summary>
/// Step 6 (claim wiring): the Web-side implementation of
/// <see cref="IClaimsSource"/> — the Identity ↔ cookie seam. The cookie (minted
/// at sign-in by <see cref="KumunitaClaimsPrincipalFactory"/>) is the sole
/// identity-bearing artifact in the request; <see cref="Current"/> returns
/// <c>HttpContext.User</c> (BCL <c>ClaimsPrincipal</c>, ADR 0006-D holds: the
/// claim *set* is the whole principal).
/// <para>
/// Returning <c>null</c> when unauthenticated is the invariant the
/// <see cref="IIdentityService.GetCurrentAsync"/> doc promises: the call-site
/// receives <c>null</c> for anonymous requests (the controller then
/// maps to a 401 / <c>ChallengeAsync</c>).
/// </para>
/// </summary>
public sealed class ClaimsSource(IHttpContextAccessor accessor) : IClaimsSource
{
    /// <inheritdoc />
    public System.Security.Claims.ClaimsPrincipal? Current
    {
        get
        {
            var user = accessor.HttpContext?.User;
            // An anonymous HttpContext still has a ClaimsPrincipal (IsAuthenticated=false);
            // the seam contract is "null when unauthenticated" (the IClaimsSource doc).
            var isAuthenticated = user?.Identity?.IsAuthenticated ?? false;
            if (user is null || !isAuthenticated)
                return null;
            return user;
        }
    }
}
