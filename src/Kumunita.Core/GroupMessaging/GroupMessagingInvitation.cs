namespace Kumunita.Core.GroupMessaging;

/// <summary>
/// The lifecycle state of a <see cref="GroupMessagingInvitation"/> row
/// (the group-messaging invitation lane — the N:1 analogue of the
/// <see cref="Kumunita.Core.UserInfo.GroupInvitation"/> community-group
/// invitation, ADR 0083, reshaped for chat threads).
/// <para>
/// Unlike the community-group invitation (which has a <c>Cancelled</c>
/// state — the owner/admin can cancel a pending invite), the group-message
/// invitation has **no** cancel state in v1: the invite stays pending until
/// the recipient accepts or declines (the user's explicit design choice).
/// </para>
/// </summary>
public enum GroupMessagingInvitationStatus
{
    /// <summary>The invite has been sent; the recipient has not yet responded.</summary>
    Pending = 0,

    /// <summary>The recipient accepted — they are now a participant of the thread.</summary>
    Accepted = 1,

    /// <summary>The recipient declined — they never become a participant.</summary>
    Declined = 2,
}

/// <summary>
/// One invitation row for a group-message thread (the N:1 analogue of the
/// community-group <see cref="Kumunita.Core.UserInfo.GroupInvitation"/>).
/// <para>
/// Business key: (ConversationId, InvitedId) — one row per (thread,
/// invitee). Re-inviting after a resolution resets the existing row to
/// Pending (the <c>GroupInvitation</c> C-M2b·3 re-stamp convention).
/// </para>
/// <para>
/// The <see cref="GroupConversation.Participants"/> set is the
/// **accepted** member set (the authorization boundary for read/send).
/// A pending invitee is **not** in <c>Participants</c> until they accept
/// via <see cref="IGroupMessagingService.AcceptInviteAsync"/>.
/// </para>
/// </summary>
public sealed class GroupMessagingInvitation
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string ConversationId { get; set; } = string.Empty; // the thread this invite is for

    public string InvitedId { get; set; } = string.Empty;      // the recipient (the invitee's subject id)

    public string InvitedBy { get; set; } = string.Empty;      // who sent the invite (the actor)

    public GroupMessagingInvitationStatus Status { get; set; } = GroupMessagingInvitationStatus.Pending;

    public DateTimeOffset InvitedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }            // stamped when the invitee accepts/declines

    public string? ResolvedBy { get; set; }                    // the invitee's subject id (self-lane)
}
