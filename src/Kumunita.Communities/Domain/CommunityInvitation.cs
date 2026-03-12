using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Communities;

namespace Kumunita.Communities.Domain;

public enum InvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Expired,
    Revoked
}

/// <summary>
/// An invitation for a user (by email) to join a community.
/// Invitations expire after a configurable TTL (default 7 days).
/// </summary>
public class CommunityInvitation
{
    public CommunityInvitationId Id { get; set; }
    public CommunityId CommunityId { get; set; }

    /// <summary>
    /// Must be a Manager or Moderator in the community.
    /// Moderators may only invite at Member level.
    /// </summary>
    public UserId InvitedByUserId { get; set; }

    public string InvitedEmail { get; set; } = default!;

    /// <summary>
    /// Secure random token included in the invitation link.
    /// Single-use — consumed on acceptance.
    /// </summary>
    public string Token { get; set; } = default!;

    /// <summary>
    /// The role to assign when the invitation is accepted.
    /// Moderators can only set this to Member.
    /// </summary>
    public CommunityRole AssignedRole { get; set; } = CommunityRole.Member;

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>
    /// Set when the invitation is accepted, linking it to the membership created.
    /// </summary>
    public CommunityMembershipId? ResultingMembershipId { get; set; }

    public bool IsExpired => Status == InvitationStatus.Pending && DateTimeOffset.UtcNow > ExpiresAt;
}
