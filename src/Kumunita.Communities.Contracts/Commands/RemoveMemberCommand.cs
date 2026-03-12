using Kumunita.Shared.Kernel;

namespace Kumunita.Communities.Contracts.Commands;

/// <summary>Managers only, or the member themselves (leaving).</summary>
public record RemoveMemberCommand(
    string Slug,
    UserId TargetUserId);