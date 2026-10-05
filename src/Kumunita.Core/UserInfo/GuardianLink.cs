namespace Kumunita.Core.UserInfo;

/// <summary>
/// One row per (guardian, child) pair — the GU standing's relationship document
/// (ADR 0028 §B / ADR 0038 §F). A child may have one or two guardians (each an
/// <b>active</b> row); the child has guardian standing iff <b>any</b> active
/// row exists.
/// <see cref="GuardianId"/> is the standing holder: G·4 (formation is
/// creation-based) is anchored here for the <see cref="GuardianLinkStatus
/// .Active"/> rows that originate from the GU formation lane
/// (<c>CreateGuardianLinkAsync</c>); for the GA lane (ADR 0038 §F), the
/// standing holder is the <b>assigned</b> guardian, and their
/// <see cref="GuardianLinkStatus.Active"/> row is written only <b>after</b>
/// they accept (with consent) the request the assigning guardian created.
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

    /// <summary>The guardian account — the standing holder. For the GU formation
    /// lane the creator; for the GA lane (ADR 0038 §F) the assigned guardian,
    /// whose row only becomes <see cref="GuardianLinkStatus.Active"/> after
    /// they accept the request with consent.</summary>
    public string GuardianId { get; set; } = string.Empty;

    /// <summary>The supervised child account (the target of every GU action).</summary>
    public string ChildId { get; set; } = string.Empty;

    /// <summary>The four-state machine (ADR 0028 §B / ADR 0038 §F):
    /// <see cref="GuardianLinkStatus.Pending"/> →
    /// <see cref="GuardianLinkStatus.Active"/> →
    /// <see cref="GuardianLinkStatus.Dissolved"/>; and
    /// <see cref="GuardianLinkStatus.Pending"/> →
    /// <see cref="GuardianLinkStatus.Declined"/> (the ADR 0038 §F acceptance
    /// lane — an assigned guardian may refuse the request). Only
    /// <see cref="GuardianLinkStatus.Active"/> rows confer standing; the
    /// standing gates (<c>GuardActiveLinkAsync</c>,
    /// <c>ActiveLinkAsync</c>, <c>ActiveGuardiansAsync</c>) all query
    /// <c>Status == Active</c> exclusively.</summary>
    public GuardianLinkStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the row moves Active → Dissolved (the independence lane);
    /// null while Pending / Active / Declined.</summary>
    public DateTimeOffset? DissolvedAt { get; set; }

    /// <summary>The account that dissolved the row (the guardian, or a GlobalAdmin
    /// on the G·5 safety valve); null while Pending / Active / Declined. The
    /// <c>DelegationGrant.RevokedBy</c> / <c>GroupInvitation.ResolvedBy</c>
    /// precedent.</summary>
    public string? DissolvedBy { get; set; }

    /// <summary>The timestamp of the ADR 0038 §F state transition that resolved
    /// this row: <c>Accept</c> (Pending → Active), <c>Decline</c> (Pending →
    /// Declined), or re-refresh (an assign-again over a previously-declined or
    /// re-pending row). Null while the row is in its initial state and never
    /// transitioned.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The subject id of the account that resolved this row in the
    /// ADR 0038 §F acceptance lane (the <b>assigned</b> guardian, on Accept /
    /// Decline). Null while the row is still Pending, or for GU-formation
    /// lane rows (which are written directly Active). For re-assignment over
    /// a previously-resolved row, this carries the most recent resolver.</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>GA (ADR 0038 §F) — the conferrer: the subject id of the
    /// <b>assigning</b> guardian who created this row via
    /// <c>AssignGuardianLinkAsync</c>. Non-null on every GA-lane row
    /// (Pending / Active / Declined), null on GU-formation-lane rows (which
    /// are written directly Active by the creating guardian). Persists the
    /// "who conferred the standing" legibility that ADR 0038 §D named as a
    /// limitation (recoverable only from the <c>guardian.assign</c> audit
    /// row) — the §F amendment supersedes the byte-identical-POCO invariant
    /// (S·3) in order to surface the conferrer on the row itself (the
    /// assignee's Index card, the Detail "other guardians" list, and any
    /// future audit projection all read this field directly).</summary>
    public string? AssignedById { get; set; }
}

/// <summary>
/// The <see cref="GuardianLink"/> state machine (ADR 0028 §B, extended by
/// ADR 0038 §F for the acceptance lane):
/// <see cref="Pending"/> → <see cref="Active"/> (the assigned guardian
/// accepted with consent) → <see cref="Dissolved"/>; and <see cref
/// "Pending"/> → <see cref="Declined"/> (the assigned guardian refused).
/// <see cref="Active"/> → <see cref="Dissolved"/> is the GU formation lane's
/// one-way independence transition (re-attaching is a fresh
/// <see cref="Pending"/> row — a deliberate new act, not an undo).
/// <see cref="Declined"/> rows are terminal for the lane (a re-assignment by
/// the conferrer overwrites the row back to <see cref="Pending"/> — a
/// deliberate new act, not an undo).
/// </summary>
public enum GuardianLinkStatus
{
    /// <summary>The row was assigned by an existing guardian (ADR 0038 §F);
    /// the assigned guardian has not yet accepted or declined. This is the
    /// only state that has no standing: the standing gates all query
    /// <see cref="Active"/> exclusively.</summary>
    Pending,

    /// <summary>The row confers standing. Written directly Active on the GU
    /// formation lane (creation-based, G·4); written Pending → Active on the
    /// GA lane when the assigned guardian accepts with consent (ADR 0038 §F).</summary>
    Active,

    /// <summary>The row was dissolved (the independence lane, GU G·4 / G·5).
    /// Terminal: a re-assignment writes a fresh row, not a revival.</summary>
    Dissolved,

    /// <summary>The row was declined by the assigned guardian (ADR 0038 §F).
    /// Terminal for the lane: a re-assignment by the conferrer overwrites the
    /// row back to <see cref="Pending"/>.</summary>
    Declined
}
