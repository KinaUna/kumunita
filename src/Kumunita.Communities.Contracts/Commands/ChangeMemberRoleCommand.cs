using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Communities;

namespace Kumunita.Communities.Contracts.Commands;

/// <summary>
/// Managers only. Cannot assign a role higher than Manager.
/// Moderators cannot use this command.
/// </summary>
public record ChangeMemberRoleCommand(
    string Slug,
    UserId TargetUserId,
    CommunityRole NewRole);