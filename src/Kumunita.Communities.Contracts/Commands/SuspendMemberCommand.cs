using Kumunita.Shared.Kernel;

namespace Kumunita.Communities.Contracts.Commands;

/// <summary>Managers only.</summary>
public record SuspendMemberCommand(
    string Slug,
    UserId TargetUserId,
    string Reason);