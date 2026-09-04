using System.Security.Claims;
using Kumunita.Core.Identity;
using Xunit;

using KTs = Kumunita.Core.Identity.ClaimTypes;

namespace Kumunita.Core.Tests;

/// <summary>
/// Invariant set B â€” the no-relational-data claim-shape pin (plan M1 step 6, item 9;
/// ADR 0006 Â§B; the <see cref="ClaimShaping"/> class doc: "the claim set is the whole
/// principal, and the only admissible claim *types* are <see cref="KT.All"/>").
/// <para>
/// A pure unit test: no DB, no HTTP, no EF, no Marten. Exercises the two pure halves of
/// the Identity â†” cookie seam:
/// <list type="number">
/// <item><see cref="ClaimShaping.Build"/> â€” mints the admissible claim set for a given
/// resident; the produced principal's claim *types* must be exactly a subset of
/// <see cref="KT.All"/> (four names: Kumunita.Sub, Kumunita.ExternalId,
/// Kumunita.Verified, Kumunita.Role). No group id, delegation, audience, or profile
/// field may appear.</item>
/// <item><see cref="ClaimShaping.FromClaims"/> â€” maps a claim set back to a
/// <see cref="ThinPrincipal"/>; a principal without a Subject claim is not a Kumunita
/// resident (returns null).</item>
/// </list>
/// This is the seam test the ADR 0006 invariant table calls out: the claim set is the
/// *entire* principal; anything outside <see cref="KT.All"/> is a violation.
/// </para>
/// </summary>
public class ClaimShapingInvariantBTests
{
    // â”€â”€ Positive: Build produces only admissible claim types â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Build_KT_AreExactlyKTAll()
    {
        var principal = ClaimShaping.Build(
            subjectId: "user-123",
            externalId: "oidc-sub-456",
            verified: true,
            roles: new[] { Roles.Member, Roles.Moderator, Roles.ModeratorComponent("safety") });

        Assert.NotNull(principal);
        var identity = (ClaimsIdentity)Assert.Single(principal.Identities);
        var producedTypes = identity.Claims.Select(c => c.Type).ToHashSet();

        // The invariant: every claim type in the produced set is in Kumunita's admissible set.
        Assert.All(producedTypes, t => Assert.Contains(t, Kumunita.Core.Identity.ClaimTypes.All));

        // And the four known admissible types are all present (for a fully-populated
        // resident with externalId set):
        Assert.Contains(KTs.Subject, producedTypes);
        Assert.Contains(KTs.ExternalId, producedTypes);
        Assert.Contains(KTs.Verified, producedTypes);
        Assert.Contains(KTs.Role, producedTypes);
    }

    [Fact]
    public void Build_WithNullExternalId_OmitsExternalIdClaim_TypeStillAdmissible()
    {
        var principal = ClaimShaping.Build(
            subjectId: "user-789",
            externalId: null,   // no federation yet â€” the claim is omitted entirely
            verified: false,
            roles: new[] { Roles.Member });

        var identity = (ClaimsIdentity)Assert.Single(principal!.Identities);
        Assert.DoesNotContain(identity.Claims, c => c.Type == KTs.ExternalId);

        // The claim is absent (not empty-string) â€” the admissible *set* still includes
        // the type, but the value is simply not minted.
        Assert.Contains(KTs.Subject, identity.Claims.Select(c => c.Type));
        Assert.Contains(KTs.Verified, identity.Claims.Select(c => c.Type));
        Assert.Contains(KTs.Role, identity.Claims.Select(c => c.Type));
    }

    [Fact]
    public void Build_VerifiedFalse_ClaimIsFalseString()
    {
        var principal = ClaimShaping.Build("u1", null, verified: false, roles: []);
        var claim = Assert.Single(
            ((ClaimsIdentity)principal!.Identities.First()).Claims,
            c => c.Type == KTs.Verified);

        Assert.Equal("false", claim.Value);
    }

    [Fact]
    public void Build_ModeratorComponent_ClaimValues_ArePerComponentStrings()
    {
        var principal = ClaimShaping.Build(
            "mod-1", null, verified: true,
            roles: new[] { Roles.Moderator, Roles.ModeratorComponent("safety"), Roles.ModeratorComponent("governance") });

        var identity = (ClaimsIdentity)principal!.Identities.First();
        var roleClaims = identity.Claims.Where(c => c.Type == KTs.Role).Select(c => c.Value).ToList();

        Assert.Contains("Moderator", roleClaims);
        Assert.Contains("moderator:safety", roleClaims);
        Assert.Contains("moderator:governance", roleClaims);
        Assert.Equal(3, roleClaims.Count);

        // The invariant is that the component-scoped values are *role* claim values,
        // not extra claim *types*. So no new claim types are introduced.
        var distinctTypes = identity.Claims.Select(c => c.Type).ToHashSet();
        Assert.All(distinctTypes, t => Assert.Contains(t, Kumunita.Core.Identity.ClaimTypes.All));
    }

    // â”€â”€ No-relational-data pin (the invariant B assertion) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // The claim set is the *entire* principal: anything outside KT.All is a
    // violation of ADR 0006 Â§B (the no-group-id / no-audience / no-delegation / no-
    // profile-field / no-standard-schema-claims rule). Each forbidden type is a shape
    // that could plausibly be minted by a careless factory, so each is a separate pin.

    [Theory]
    [InlineData("Kumunita.Group")]                              // group id
    [InlineData("Kumunita.Audience")]                           // audience grant
    [InlineData("Kumunita.Delegation")]                         // delegation grant
    [InlineData("Kumunita.Profile")]                            // the profile doc
    [InlineData("Kumunita.Household")]                          // the household id
    [InlineData("Kumunita.Visibility")]                         // the Audience value
    [InlineData("Kumunita.ContactVisibility")]                  // the contact audience
    [InlineData("Kumunita.ExternalId")]                         // (allowed type â€” sanity check)
    [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/sid")]   // standard schema
    [InlineData("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name")]
    [InlineData("urn:oid:1.2.840.113549.1.9.1")]               // email (RFC 5642)
    public void Build_NeverProduces_ForbiddenKT(string type)
    {
        if (type == KTs.ExternalId)
        {
            // ExternalId IS in the admissible set â€” the Build with externalId set should
            // *produce* it. Skip this as the negative check (a separate test asserts
            // the positive presence above).
            return;
        }

        var principal = ClaimShaping.Build(
            "resident-1", "ext-fed", verified: true,
            roles: new[] { Roles.Member, Roles.GlobalAdmin, Roles.Moderator, Roles.ModeratorComponent("social") });

        var allClaims = principal!.Identities.SelectMany(i => i.Claims).ToList();
        Assert.DoesNotContain(allClaims, c => c.Type == type);
    }

    // â”€â”€ FromClaims round-trip â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void FromClaims_RoundTrip_PreservesSubjectExternalVerifiedRoles()
    {
        var roles = new[] { Roles.Member, Roles.Moderator, Roles.ModeratorComponent("social") };
        var original = ClaimShaping.Build("subj-1", "ext-fed", verified: true, roles: roles);

        var mapped = ClaimShaping.FromClaims(original);

        Assert.NotNull(mapped);
        Assert.Equal("subj-1", mapped!.SubjectId);
        Assert.Equal("ext-fed", mapped.ExternalId);
        Assert.True(mapped.IsVerifiedResident);
        Assert.Equal(roles, mapped.Roles);
    }

    [Fact]
    public void FromClaims_WithNullExternalId_MapsToNull()
    {
        var principal = ClaimShaping.Build("subj-2", null, verified: false, roles: []);
        var mapped = ClaimShaping.FromClaims(principal);

        Assert.NotNull(mapped);
        Assert.Equal("subj-2", mapped!.SubjectId);
        Assert.Null(mapped.ExternalId);
        Assert.False(mapped.IsVerifiedResident);
        Assert.Empty(mapped.Roles);
    }

    [Fact]
    public void FromClaims_NullPrincipal_ReturnsNull()
    {
        Assert.Null(ClaimShaping.FromClaims(null));
    }

    [Fact]
    public void FromClaims_PrincipalWithoutSubject_ReturnsNull()
    {
        // A principal with role claims but no Subject is NOT a Kumunita resident â€”
        // the seam refuses to mint a thin principal for it.
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(KTs.Role, Roles.GlobalAdmin));
        var notResident = new ClaimsPrincipal(identity);

        Assert.Null(ClaimShaping.FromClaims(notResident));
    }

    [Fact]
    public void FromClaims_IgnoresNonAdmissibleClaims()
    {
        // Simulate a "leaked" principal (e.g., a default Identity factory that added
        // standard-schema claims) â€” the thin principal mapping must ignore them and
        // return only the admissible data.
        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(KTs.Subject, "real-user"));
        identity.AddClaim(new Claim(KTs.Verified, "true"));
        identity.AddClaim(new Claim(KTs.Role, Roles.Member));
        // Leak:
        identity.AddClaim(new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", "Bob"));
        identity.AddClaim(new Claim("Kumunita.Group", "leaked-group"));

        var principal = new ClaimsPrincipal(identity);
        var mapped = ClaimShaping.FromClaims(principal);

        Assert.NotNull(mapped);
        Assert.Equal("real-user", mapped!.SubjectId);
        Assert.True(mapped.IsVerifiedResident);
        Assert.Equal(new[] { Roles.Member }, mapped.Roles);
        // The leaked claims are simply not part of the ThinPrincipal's shape.
    }
}

