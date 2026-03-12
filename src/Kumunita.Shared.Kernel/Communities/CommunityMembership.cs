namespace Kumunita.Shared.Kernel.Communities;

public enum MembershipStatus
{
    Active,
    Suspended,  // suspended within this community by a Manager
    Left        // user left or was removed
}

/// <summary>
/// Records a user's membership in a specific community.
/// Stored in the platform-level 'communities' schema alongside Community —
/// membership is a platform concept, not a per-community one.
/// </summary>
public class CommunityMembership
{
    public CommunityMembershipId Id { get; set; }
    public CommunityId CommunityId { get; set; }
    public UserId UserId { get; set; }
    public CommunityRole Role { get; set; }
    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>
    /// The user who sent the invitation that led to this membership.
    /// </summary>
    public UserId InvitedBy { get; set; }

    public DateTimeOffset? SuspendedAt { get; set; }
    public UserId? SuspendedBy { get; set; }
    public string? SuspensionReason { get; set; }

    public DateTimeOffset? LeftAt { get; set; }
}
