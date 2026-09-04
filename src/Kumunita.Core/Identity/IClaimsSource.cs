using System.Security.Claims;

namespace Kumunita.Core.Identity;

/// <summary>
/// The Identity ↔ cookie seam (design doc "Seams & contracts"): the claim set is the
/// whole principal. Declared in Core because the thin principal is built *from* claims;
/// implemented in Kumunita.Web (cookie authentication). <see cref="ClaimsPrincipal"/> is
/// BCL (System.Security.Claims), not an ASP.NET HTTP type (ADR 0006-D holds).
/// </summary>
public interface IClaimsSource
{
    /// <summary>The authenticated claim set for the current request, or null when unauthenticated.</summary>
    ClaimsPrincipal? Current { get; }
}
