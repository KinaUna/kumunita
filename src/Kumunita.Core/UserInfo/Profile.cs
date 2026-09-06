using Kumunita.Core.Authorization;

namespace Kumunita.Core.UserInfo;

/// <summary>
/// A resident's profile (ARCHITECTURE.md §5 — UserInfoModule documents, <c>mt</c> schema).
/// Stored from M1 so M2 never patches visibility onto a legacy document (the M1 design's
/// "emergent impact"). Document identity is <see cref="SubjectId"/> (the thin principal's
/// subject).
/// <para>
/// <see cref="Visibility"/> defaults per ADR 0001-B — the author's choice, absolute by
/// default: a bootstrapped profile starts visible to the author alone (an empty audience
/// denies everyone, including the author, per invariant C1). <see cref="ContactVisibility"/>
/// gates the *opt-in* contact block (email/phone) and is evaluated only after
/// <see cref="Visibility"/> allows the profile — never on a hidden profile (§9 testing).
/// </para>
/// <para>
/// <see cref="ExternalId"/> is reserved for federation (ADR 0001): IdentityModule is the only
/// component that knows the identity source, so the later OIDC <c>sub</c> swap is additive.
/// <see cref="HouseholdId"/> is display/metadata ONLY — the authorization path never reads
/// it; household-based visibility is expressed as a household *group* the owner grants
/// (ADR 0001-B).
/// </para>
/// </summary>
public sealed class Profile
{
    /// <summary>Document identity — one profile per account. Mapped in
    /// <c>M1DocTypes.Configure</c> via <see cref="Marten.StoreOptions.Schema"/>.Identity
    /// because Marten's default identity convention (a property named <c>Id</c>) won't
    /// pick this up. ADR 0004 §B.1.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>Reserved for federation (later OIDC <c>sub</c>); null until then.</summary>
    public string? ExternalId { get; set; }

    /// <summary>Display/metadata only — never consulted by the authorization path.</summary>
    public string? HouseholdId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Verified resident (the claim's <see cref="Identity.ThinPrincipal.IsVerifiedResident"/>
    /// is minted from here at sign-in). Unverified accounts cannot sign in.</summary>
    public bool Verified { get; set; }

    /// <summary>Blocked resident (a GlobalAdmin's suspension; ADR-style admin lane). A blocked
    /// account loses all role standing (no <c>Member</c>/<c>Moderator</c>/<c>GlobalAdmin</c>,
    /// so it cannot act or be granted standing) until unblocked — a reversible suspension that
    /// preserves the account and its documents. Evaluated at the Identity↔cookie seam
    /// (<see cref="Identity.ClaimShaping"/>'s Web factory) exactly as <see cref="Verified"/> is;
    /// no claim type is minted for it (the no-relational-data invariant holds: the effect is
    /// "no roles", read from <c>mt</c> at sign-in).</summary>
    public bool Blocked { get; set; }

    /// <summary>Who may see this profile. The author-controlled audience (ADR 0003: the author
    /// always controls their own content's audience). Bootstrap default: self-only.</summary>
    public Audience Visibility { get; set; } = new();

    /// <summary>Gates the contact block only; evaluated after <see cref="Visibility"/> allows.</summary>
    public Audience? ContactVisibility { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }
}

/// <summary>A profile contact-surface update (the M1 bootstrap surface — the author's own
/// profile; the profile *editing* UI and directory visibility rules are M2). Null fields
/// leave the current value untouched.</summary>
public sealed record ProfileUpdate(
    string? DisplayName,
    string? Email,
    string? Phone,
    Audience? Visibility,
    Audience? ContactVisibility);
