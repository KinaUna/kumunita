namespace Kumunita.Core.UserInfo;

/// <summary>
/// One row per (guardian, child) pair — the GU standing's relationship document
/// (ADR 0028 §B). A child may have one or two guardians (each an active row);
/// the child has guardian standing iff <b>any</b> active row exists.
/// <see cref="GuardianId"/> is the creator: G·4 (formation is creation-based)
/// is anchored here — the standing basis is that this guardian <i>created</i>
/// the child's account, so there is no self-serve "claim guardianship" lane.
/// <para>
/// G·2 — the service is the resolver, not this document. Whether a row is
/// "active" is decided by <c>IUserInfoService</c> off <see cref="Status"/> at
/// decision time, exactly like <c>DelegationGrant.IsActiveAt</c> (the "service
/// is the resolver" rule): this POCO carries the state, it does not carry an
/// <c>IsActive</c> boolean. A dissolve is live on the very next lane read (C4).
/// </para>
/// <para>
/// G·5 — the safety valve is anchored by <see cref="DissolvedBy"/> (the
/// GlobalAdmin who dissolved, on the <c>viaAdmin</c> branch) and the
/// <c>DissolveGuardianLinkAsync(viaAdmin: true)</c> audit row — a GlobalAdmin
/// may always dissolve an active link or un-suspend an account, audited
/// <c>Via: Admin</c>.
/// </para>
/// </summary>
public sealed class GuardianLink
{
    /// <summary>Surrogate PK; (GuardianId, ChildId) remains the business key.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The guardian account (the creator — G·4).</summary>
    public string GuardianId { get; set; } = string.Empty;

    /// <summary>The supervised child account (the target of every GU action).</summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>The two-state machine (ADR 0028 §B): Active → Dissolved.</summary>
    public GuardianLinkStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the row moves Active → Dissolved (the independence lane);
    /// null while Active.</summary>
    public DateTimeOffset? DissolvedAt { get; set; }

    /// <summary>The account that dissolved the row (the guardian, or a GlobalAdmin
    /// on the G·5 safety valve); null while Active. The <c>DelegationGrant.
    /// RevokedBy</c> / <c>GroupInvitation.ResolvedBy</c> precedent.</summary>
    public string? DissolvedBy { get; set; }
}

/// <summary>
/// The <see cref="GuardianLink"/> state machine (ADR 0028 §B).
/// <c>Active → Dissolved</c>; dissolve is effectively one-way in practice
/// (re-attaching is a fresh <c>Active</c> row — a deliberate new act, not an
/// undo).
/// </summary>
public enum GuardianLinkStatus
{
    Active,
    Dissolved
}
