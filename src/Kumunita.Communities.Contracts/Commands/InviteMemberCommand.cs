using Kumunita.Shared.Kernel.Communities;

namespace Kumunita.Communities.Contracts.Commands;

/// <summary>
/// Managers can invite at any role level.
/// Moderators can only invite at Member level.
/// </summary>
public record InviteMemberCommand(
    string Slug,
    string InvitedEmail,
    CommunityRole AssignedRole);