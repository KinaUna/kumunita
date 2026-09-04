using Microsoft.AspNetCore.Identity;

namespace Kumunita.Core.Identity;

/// <summary>
/// Kumunita's identity user (the `identity` schema, ADR 0004 B/C). Subclasses the stock
/// <see cref="IdentityUser"/> — the *only* delta is <see cref="ExternalId"/> (ADR 0001:
/// reserved for federation, OIDC's `sub`, later). The thin principal's
/// <see cref="ThinPrincipal.ExternalId"/> is minted from this column at sign-in.
/// The `User` identity (its `Id` is the `ThinPrincipal.SubjectId`) — a stable string —
/// is the seam between the Identity layer (the `identity` schema) and the UserInfo /
/// Authorization layers (which key `Profile.SubjectId` / group / delegation / assignments
/// and `AccessAudit.ActorId` by the same string).
/// </summary>
public sealed class User : IdentityUser
{
    /// <summary>Reserved for federation (ADR 0001); null until OIDC lands. Minted onto
    /// <see cref="ThinPrincipal.ExternalId"/> by the Web claim factory on sign-in.</summary>
    public string? ExternalId { get; set; }
}
