using System.Security.Claims;

namespace Kumunita.Web.Security;

/// <summary>
/// Razor- and controller-usable helpers for reading the admissible claim set minted
/// by <c>KumunitaClaimsPrincipalFactory</c> (step 6). Those roles live under the custom
/// <c>Kumunita.Core.Identity.ClaimTypes.Role</c> claim type, not the BCL default
/// (<see cref="System.Security.Claims.ClaimTypes.Role"/>), so ASP.NET Core's
/// <c>ClaimsPrincipal.IsInRole</c> (which checks the BCL default) does NOT reflect them —
/// use <see cref="HasRole(System.Security.Claims.ClaimsPrincipal, string)"/> instead.
/// </summary>
public static class KumunitaPrincipal
{
    public static bool HasRole(ClaimsPrincipal user, string role)
    {
        if (user is null)
            return false;

        // The admissible claim set uses Kumunita's own short-type names:
        // Kumunita.Core.Identity.ClaimTypes.Role = "Kumunita.Role" (NOT the BCL
        // "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", so
        // ClaimsPrincipal.IsInRole does not see these — this helper reads them directly).
        return user.Claims.Any(c => c.Type == "Kumunita.Role" && c.Value == role);
    }

    public static bool IsGlobalAdmin(ClaimsPrincipal user) => HasRole(user, "GlobalAdmin");

    public static bool IsModerator(ClaimsPrincipal user) => HasRole(user, "Moderator");

    public static bool IsVerifiedResolved(ClaimsPrincipal user) =>
        user?.Claims.Any(c => c.Type == "Kumunita.Verified" && c.Value == "true") == true;

    public static string? SubjectId(ClaimsPrincipal user) =>
        user?.Claims.FirstOrDefault(c => c.Type == "Kumunita.Sub")?.Value;
}
