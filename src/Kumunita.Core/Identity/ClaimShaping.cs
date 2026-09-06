using System.Security.Claims;

namespace Kumunita.Core.Identity;

/// <summary>
/// The pure two halves of the Identity ↔ cookie seam (ADR 0006 §B, the no-relational-data
/// invariant): <see cref="ClaimsMinter.Build"/> mints the <em>admissible</em> claim set for
/// a resident (what the Web <c>ClaimsPrincipalFactory</c> produces at sign-in), and
/// <see cref="PrincipalMapper.FromClaims"/> maps a claim set back to a <see
/// cref="ThinPrincipal"/> (what <c>IIdentityService.GetCurrentAsync</c> reads per request).
/// <para>
/// Both are **pure** and carry no HTTP, EF, or Marten types, so the no-relational-data
/// assertion (invariant set B) is exercised as a unit test on these: the claim set is the
/// whole principal, and the only admissible claim *types* are <see cref="ClaimTypes.All"/>
/// (subject, externalId, verified, role). No group id, delegation, audience, or profile
/// field may appear.
/// </para>
/// </summary>
public static class ClaimShaping
{
    /// <summary>
    /// Mint the admissible claim set for one resident. The produced principal's claim
    /// *types* are exactly <see cref="ClaimTypes.All"/> (when <paramref name="externalId"/>
    /// is set; otherwise the ExternalId claim is omitted — the *type* is still admissible);
    /// <see cref="ClaimTypes.Role"/> has one claim per role string (a <c>Moderator</c>'s
    /// per-component strings are <see cref="Roles.ModeratorComponent(string"/> values, not
    /// extra claim types).
    /// </summary>
    public static ClaimsPrincipal Build(
        string subjectId,
        string? externalId,
        bool verified,
        IReadOnlyList<string> roles)
    {
        // The admissible claim set uses Kumunita's own role claim type (ClaimTypes.Role =
        // "Kumunita.Role"), not the BCL default. Declaring it as this identity's
        // RoleClaimType (read-only, so set via the roleType constructor overload) is what
        // makes role authorization work against it: ClaimsPrincipal.IsInRole and the
        // [Authorize(Roles = "...")] requirement both resolve roles via the identity's
        // RoleClaimType, so without this every role gate would check the wrong claim type
        // and silently mis-evaluate. (The host cannot set this per-scheme — the cookie has
        // no role-claim-type option — so it belongs on the identity at mint time, which is
        // exactly the Identity↔cookie seam this class owns.)
        // name: subjectId makes IsAuthenticated=true. For ClaimsIdentity that property
        // is read-only and derives from "Name is non-null", so a name-less identity
        // reports IsAuthenticated=false — exactly the failure the first-boot setup
        // sign-in hit: SignInAsync rejects an unauthenticated principal when
        // AuthenticationOptions.RequireAuthenticatedSignIn is set (on by default for
        // the cookie scheme), throwing the InvalidOperationException seen on the live
        // server. Setting the constructor Name does NOT add a <see cref="Claim"/> to the
        // identity (only AddClaim does), so the admissible claim *type* set is unchanged
        // and the invariant-set-B tests still pass. Using SubjectId keeps the name
        // stable, non-sensitive, and unique per resident.
        // Positional — the ClaimsIdentity overload here is
        // (string name, string nameType, string roleType); named args failed to bind
        // to this specific overload on .NET 10.
        var identity = new ClaimsIdentity(subjectId, null, ClaimTypes.Role);

        identity.AddClaim(new Claim(ClaimTypes.Subject, subjectId));
        if (externalId is not null)
            identity.AddClaim(new Claim(ClaimTypes.ExternalId, externalId));
        identity.AddClaim(new Claim(ClaimTypes.Verified, verified ? "true" : "false"));
        foreach (var role in roles)
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Map a claim set (the cookie's principal) back to the thin principal, or null when
    /// unauthenticated (no claims / no subject). The subject claim is the anchor: a
    /// principal without a <see cref="ClaimTypes.Subject"/> claim is not a Kumunita
    /// resident.
    /// </summary>
    public static ThinPrincipal? FromClaims(ClaimsPrincipal? claims)
    {
        if (claims is null)
            return null;

        var subject = claims.FindFirstValue(ClaimTypes.Subject);
        if (string.IsNullOrEmpty(subject))
            return null;

        var roles = claims
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        return new ThinPrincipal(
            subject,
            claims.FindFirstValue(ClaimTypes.ExternalId),
            claims.FindFirstValue(ClaimTypes.Verified) == "true",
            roles);
    }
}
