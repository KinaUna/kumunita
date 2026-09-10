namespace Kumunita.Core.UserInfo;

/// <summary>
/// The lifecycle state of a <see cref="GroupInvitation"/> row (m2b — owner-invited
/// group membership; C-M2b·3 state machine).
/// <para>
/// <c>Pending → { Accepted, Declined, Cancelled }</c>; a re-invite after any
/// resolution resets the row back to <c>Pending</c>. Any other transition is
/// invalid and the service throws <see cref="InvalidOperationException"/>
/// (the Web maps that to an error message, never a 500).
/// </para>
/// </summary>
public enum InvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled
}

/// <summary>
/// One row per (group, user) invitation pair (m2b — <see cref="InvitationStatus"/>
/// is the single state field; the business key (<c>GroupId</c>, <c>UserId</c>) is the
/// same pair shape as <see cref="GroupMembership"/>, enforced by the unique index in
/// <see cref="M1DocTypes"/>'s <c>Configure</c> — <see cref="Id"/> is the surrogate
/// PK for clean Marten identity, mirroring <see cref="GroupMembership"/>'s pin).
/// <para>
/// No TTL / expiry (m2b decision): a pending invitation stays pending until the
/// owner/admin cancels it or the invitee resolves it. The *membership fact* itself
/// never lands on this document — accepting upserts the live
/// <see cref="GroupMembership"/> row in the same session (invariant C4: the
/// new membership is live on the very next <c>GetGroupIdsAsync</c> /
/// <c>GetGroupsForUserAsync</c> call); this row is the invitation's history
/// (invited by whom, when resolved, by whom). The "who did what, via what
/// standing" fact is on the <see cref="Authorization.AccessAudit"/> row
/// (<c>group.invite</c> / <c>group.invite.accept</c> / <c>group.invite.decline</c>
/// / <c>group.invite.cancel</c>, <c>TargetKind</c> "group"), the same lane as
/// add/remove (invariant C3, same transaction as the state write).
/// </para>
/// </summary>
public sealed class GroupInvitation
{
    /// <summary>Surrogate PK; (GroupId, UserId) remains the business key (unique index).</summary>
    public string Id { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;

    /// <summary>The invited resident (the row's <c>UserId</c>). The accept/decline
    /// self-lane (C-M2b·2) verifies the actor equals exactly this field.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The account that created (or re-created) the invitation
    /// (group owner or GlobalAdmin — C-M2b·1's SoD lane).</summary>
    public string InvitedBy { get; set; } = string.Empty;

    /// <summary>The single state field (C-M2b·3's machine).</summary>
    public InvitationStatus Status { get; set; }

    public DateTimeOffset InvitedAt { get; set; }

    /// <summary>Set when the row leaves <see cref="InvitationStatus.Pending"/>
    /// (accept, decline, or cancel); null while pending. A re-invite clears both
    /// stamps back to the fresh <c>Pending</c> shape.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The account that moved the row out of <c>Pending</c> (the invitee
    /// for accept/decline — the self-lane; the owner/admin for cancel); null while
    /// pending.</summary>
    public string? ResolvedBy { get; set; }
}
