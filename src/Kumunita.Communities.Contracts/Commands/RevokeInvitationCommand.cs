using Kumunita.Shared.Kernel;

namespace Kumunita.Communities.Contracts.Commands;

public record RevokeInvitationCommand(
    string Slug,
    CommunityInvitationId InvitationId);