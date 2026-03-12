using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Communities;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record InvitationCreated(
    CommunityId CommunityId,
    CommunityInvitationId InvitationId,
    string InvitedEmail,
    CommunityRole AssignedRole,
    UserId InvitedBy,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredAt) : IDomainEvent;