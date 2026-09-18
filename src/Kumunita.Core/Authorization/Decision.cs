namespace Kumunita.Core.Authorization;

/// <summary>Why a decision came out the way it did (recorded in the audit row's `via`).</summary>
public enum AccessVia
{
    Owner,
    Audience,
    Delegation,
    Moderator,
    // Report-driven unlock of a specific resource (granted in M3; the enum case exists now
    // so the audit contract is complete before reports arrive).
    Report,
    BreakGlass,
    /// <summary>
    /// A plain GlobalAdmin action (role promote/demote, component-scope assign,
    /// manual verify, <c>moderatorAccess</c> toggle, delegation grant/revoke): the
    /// design doc requires these append "Via-tagged audit rows" but §5's six
    /// values have no tag for the *admin* standing — the least-distortion slot
    /// is a seventh value named for it (not <see cref="Moderator"/> /
    /// <see cref="BreakGlass"/>: recording a plain-admin row as a moderator or a
    /// break-glass event would corrupt the "who did this, by what right" query
    /// the audit log exists to answer). Reconciled with ADR 0006 in the M1 close-out.
    /// </summary>
    Admin,
    /// <summary>
    /// The group-lane standing (group posts milestone, ADR 0013): membership in
    /// the target group is the only lane that allows — the M1 <see cref="Admin"/>-value
    /// precedent, the least-distortion slot: an additive enum value, the seven
    /// frozen values untouched.
    /// </summary>
    Group,
    /// <summary>
    /// The GU standing (guardian controls, ADR 0028): action-scoped to the
    /// five supervisory actions (suspend, community/group membership curation,
    /// invitation approval — G·3), exercised only on the IUserInfoService
    /// management lanes — **never** on a CanAsync / CanSeeAsync content
    /// decision (G·1). The M1 <see cref="Admin"/> / ADR 0013 <see cref="Group"/>
    /// append precedent: an additive enum value, the eight frozen values
    /// untouched.
    /// </summary>
    Guardian,
    /// <summary>
    /// The community-visible standing (ADR 0036): the resource's
    /// <see cref="Audience.Community"/> flag is <c>true</c> and the actor is a
    /// member of the target component (the live <c>communityIds</c> contain
    /// the target's <c>ComponentId</c>). The M1 <see cref="Admin"/> / ADR 0013
    /// <see cref="Group"/> / ADR 0028 <see cref="Guardian"/> append precedent:
    /// an additive enum value, the nine frozen values untouched.
    /// </summary>
    Community,
    /// <summary>
    /// The all-residents standing (ADR 0041): the resource's
    /// <see cref="Audience.AllResidents"/> flag is <c>true</c> and the actor
    /// is signed in (a non-empty <c>actorId</c>). This is the pages-lane
    /// equivalent of the announcement's flat <c>Scope = Community</c>,
    /// <c>CommunityId = null</c> (visible to any signed-in resident, no
    /// community required). The ADR 0036 <see cref="Community"/> append
    /// precedent: an additive enum value, the ten frozen values untouched.
    /// </summary>
    Resident
}

/// <summary>The outcome an audited decision produced.</summary>
public enum AccessOutcome
{
    Allow,
    Deny
}

/// <summary>
/// A single-target access decision (the output of <c>CanAsync</c>).
/// <c>EffectivePrincipalId</c> is the owner when acting under a delegation, the actor
/// otherwise; for break-glass decisions it is the elevated account.
/// </summary>
public sealed record Decision(bool Allowed, AccessVia Via, string EffectivePrincipalId);

/// <summary>
/// A bulk decision's result (the output of <c>CanSeeAsync</c>): the candidates the actor
/// may see (with the `via` the actor saw each one through) plus how many were hidden.
/// </summary>
public sealed record VisibleSet(
    IReadOnlyList<(string Id, AccessVia Via)> Visible,
    int HiddenCount);
