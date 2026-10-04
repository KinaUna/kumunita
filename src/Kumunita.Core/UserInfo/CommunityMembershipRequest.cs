namespace Kumunita.Core.UserInfo;

/// <summary>
/// The lifecycle state of a <see cref="CommunityMembershipRequest"/> row (the
/// GU community-approval lane — the reverse of the ADR 0094 self-initiated
/// join-request and the sibling of the m2b owner-invited group lane, but for
/// <b>communities</b> and with the <b>guardian</b> as the resolver).
/// <para>
/// <c>Pending → { Approved, Declined }</c>; a re-request after any resolution
/// resets the row back to <c>Pending</c>. Any other transition is invalid and
/// the service throws <see cref="InvalidOperationException"/> (the Web maps
/// that to an error message, never a 500). A request has no self-withdrawal
/// — the child never sees it (the child's own surface reads their
/// <b>effective</b> membership, which already includes the mandatory
/// implicit set); only a guardian of the child resolves it.
/// </para>
/// </summary>
public enum CommunityMembershipRequestStatus
{
    Pending,
    Approved,
    Declined
}

/// <summary>
/// One row per (component, child) <b>community-membership request</b> pair —
/// the pending-approval state for a <b>supervised child's</b> community
/// membership (the GU community-approval lane; the sibling of the m2b
/// <see cref="GroupInvitation"/> lane, but for <b>communities</b> and with
/// the <b>guardian</b> as the resolver).
/// <see cref="CommunityMembershipRequestStatus"/> is the single state field;
/// the business key (<c>ComponentId</c>, <c>UserId</c>) is the same pair
/// shape as <see cref="ComponentMembership"/> and <see cref="GroupInvitation"/>,
/// enforced by the unique index in <see cref="M1DocTypes"/>'s <c>Configure</c>
/// — <see cref="Id"/> is the surrogate PK for clean Marten identity.
/// <para>
/// The <b>membership fact</b> itself never lands on this document — approving
/// upserts the live <see cref="ComponentMembership"/> row in the same session
/// (the <c>GetCommunityIdsAsync</c> union read includes it on the very next
/// call — invariant C4). This row is the request's history (requested when,
/// by whom, when resolved, by whom). The "who did what, via what standing"
/// fact is on the <see cref="Authorization.AccessAudit"/> row
/// (<c>community.membership.request</c> /
/// <c>community.membership.approve</c> /
/// <c>community.membership.decline</c>, <c>TargetKind</c> "component"),
/// the same lane as <see cref="SetCommunityMembershipAsync"/> /
/// <see cref="ClearCommunityMembershipAsync"/> (invariant C3, same
/// transaction as the state write).
/// </para>
/// <para>
/// The <b>child's</b> standing is the <b>recipient</b> of the request
/// (the row's <c>UserId</c> is the supervised child's
/// <c>Profile.SubjectId</c>); the <b>resolver</b> is the <b>guardian</b>
/// (a row must have an <b>active</b> <see cref="GuardianLink"/> for the
/// exact (guardian, child) pair, the ADR 0028 G·2/G·3 deny-by-default
/// shape). The request's <b>auditor</b> is the <b>admin</b> or
/// <b>community moderator</b> who initiated the add — their
/// <c>AccessAudit</c> row on the same commit records the original
/// <c>community.add-member</c> decision, and this row carries their
/// <c>RequestedBy</c>.
/// </para>
/// </summary>
public sealed class CommunityMembershipRequest
{
    /// <summary>Surrogate PK; (ComponentId, UserId) is the business key
    /// (the same pair shape as <see cref="ComponentMembership"/>).</summary>
    public string Id { get; set; } = string.Empty;

    public string ComponentId { get; set; } = string.Empty;

    /// <summary>The supervised child (the row's <c>UserId</c>); the resolver's
    /// <see cref="GuardianLink"/> must hold for (guardian, child).</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The account that created (or re-created) the request
    /// (the admin / community moderator — the
    /// <see cref="SetCommunityMembershipAsync"/> /
    /// <see cref="AddCommunityMemberAsync"/> actor).</summary>
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>The single state field (the
    /// <see cref="CommunityMembershipRequestStatus"/> machine).</summary>
    public CommunityMembershipRequestStatus Status { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Set when the row leaves <see cref="CommunityMembershipRequestStatus.Pending"/>
    /// (approve or decline); null while pending. A re-request clears both
    /// stamps back to the fresh <c>Pending</c> shape.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The account that moved the row out of
    /// <see cref="CommunityMembershipRequestStatus.Pending"/> (the child's
    /// guardian — a row with no active link for (guardian, child) is refused
    /// by the ADR 0028 G·2/G·3 deny-by-default shape); null while pending.</summary>
    public string? ResolvedBy { get; set; }
}
