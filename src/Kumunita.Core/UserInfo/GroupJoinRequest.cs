namespace Kumunita.Core.UserInfo;

/// <summary>
/// The lifecycle state of a <see cref="GroupJoinRequest"/> row (ADR 0094 — a
/// resident's <b>self-initiated</b> request to join a <b>public</b> group;
/// the reverse direction of the m2b owner-invited lane).
/// <para>
/// <c>Pending → { Approved, Declined, Withdrawn }</c>; a resident
/// re-requesting after any resolution resets the row back to <c>Pending</c>.
/// Any other transition is invalid and the service throws
/// <see cref="InvalidOperationException"/> (the Web maps that to an error
/// message, never a 500). A <b>withdrawal</b> (the resident pulling a Pending
/// request back) is the self-lane terminal — the direct analogue of the m2b
/// owner's <c>Cancelled</c>: it drops the row off both pending lists and it is
/// re-requestable afterward.
/// </para>
/// </summary>
public enum JoinRequestStatus
{
    Pending,
    Approved,
    Declined,
    Withdrawn
}

/// <summary>
/// One row per (group, user) <b>join request</b> pair (ADR 0094).
/// <see cref="JoinRequestStatus"/> is the single state field; the business key
/// (<c>GroupId</c>, <c>UserId</c>) is the same pair shape as
/// <see cref="GroupMembership"/> and <see cref="GroupInvitation"/>, enforced
/// by the unique index in <see cref="M1DocTypes"/>'s <c>Configure</c> —
/// <see cref="Id"/> is the surrogate PK for clean Marten identity.
/// <para>
/// The <b>membership fact</b> itself never lands on this document — approving
/// upserts the live <see cref="GroupMembership"/> row in the same session
/// (invariant C4: the new membership is live on the very next
/// <c>GetGroupIdsAsync</c> / <c>GetGroupsForUserAsync</c> call); this row is
/// the request's history (requested when, when resolved, by whom). The
/// "who did what, via what standing" fact is on the
/// <see cref="Authorization.AccessAudit"/> row
/// (<c>group.join.request</c> / <c>group.join.approve</c> /
/// <c>group.join.decline</c> / <c>group.join.withdraw</c>,
/// <c>TargetKind</c> "group"), the same lane as the m2b invitation lane
/// (invariant C3, same transaction as the state write).
/// </para>
/// </summary>
public sealed class GroupJoinRequest
{
    /// <summary>Surrogate PK; (GroupId, UserId) remains the business key (unique index).</summary>
    public string Id { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;

    /// <summary>The requesting resident (the row's <c>UserId</c>). The
    /// withdraw self-lane verifies the actor equals exactly this field.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The single state field (the ADR 0094 state machine).</summary>
    public JoinRequestStatus Status { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Set when the row leaves <see cref="JoinRequestStatus.Pending"/>
    /// (approve, decline, or withdraw); null while pending. A re-request after
    /// any resolution clears both stamps back to the fresh <c>Pending</c>
    /// shape.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The account that moved the row out of <c>Pending</c> (the
    /// resident for a withdrawal; the group's owner or a GlobalAdmin for
    /// approve/decline); null while pending.</summary>
    public string? ResolvedBy { get; set; }
}
