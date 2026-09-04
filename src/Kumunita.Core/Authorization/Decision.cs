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
    Admin
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
