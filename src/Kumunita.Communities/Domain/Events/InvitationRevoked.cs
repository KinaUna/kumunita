using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record InvitationRevoked(
    CommunityId CommunityId,
    CommunityInvitationId InvitationId,
    UserId RevokedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;