using System.Security.Claims;

namespace Kumunita.Core.Identity;

/// <summary>
/// The thin principal (§B, ADR 0001-B): the *entire* identity that crosses the
/// Identity ↔ cookie seam.
/// <para>
/// Only the IdentityModule issues <see cref="ThinPrincipal"/> and only it knows the
/// identity source (cookie claims now; the later OIDC <c>sub</c> swap is mechanical and
/// confined to this module).
/// </para>
/// <para>
/// **No audience, group, delegation, or content data appears in the principal or the
/// cookie.** "Can X see Y" is always a per-request query to the AuthorizationModule
/// (fat authorization). A test asserts the claim set carries no relational data (D5).
/// </para>
/// </summary>
public sealed record ThinPrincipal(
    /// <summary>Stable across the instance; later: the OIDC <c>sub</c> (ADR 0001).</summary>
    string SubjectId,
    /// <summary>Reserved for federation (ADR 0001); null until OpenIddict lands.</summary>
    string? ExternalId,
    bool IsVerifiedResident,
    IReadOnlyList<string> Roles)
{
    public static readonly IReadOnlyList<string> NoRoles = [];
}

/// <summary>
/// The three roles (ADR 0003). Roles are simple claim strings on the thin principal.
/// <para>
/// **Member** — verified resident; participates within audiences that grant access.
/// **Moderator** — scoped to one or more functional components.
/// **GlobalAdmin** — full control; the only role that can manage roles, set moderator
/// scope, toggle scope-level <c>moderatorAccess</c>, and read the audit log.
/// </para>
/// </summary>
public static class Roles
{
    public const string Member = "Member";
    public const string Moderator = "Moderator";
    public const string GlobalAdmin = "GlobalAdmin";

    /// <summary>A component-scope claim: a Moderator governs <paramref name="componentId"/>.</summary>
    public static string ModeratorComponent(string componentId) => $"moderator:{componentId}";
}

/// <summary>
/// The claim names that make up the thin principal (the claim set is the whole
/// principal — the no-relational-data seam test asserts *only* these names appear).
/// </summary>
public static class ClaimTypes
{
    public const string Subject = "Kumunita.Sub";
    public const string ExternalId = "Kumunita.ExternalId";
    public const string Verified = "Kumunita.Verified";
    public const string Role = "Kumunita.Role";

    /// <summary>The admissible claim set — used by the claim-shape pin test (D5).</summary>
    public static readonly IReadOnlySet<string> All =
        new HashSet<string> { Subject, ExternalId, Verified, Role };
}
