using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Communities;

namespace Kumunita.Communities.Contracts.Queries;

public record PendingInvitationResult(
    CommunityInvitationId Id,
    string InvitedEmail,
    CommunityRole AssignedRole,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);