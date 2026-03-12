using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record InvitationDeclined(
    CommunityId CommunityId,
    CommunityInvitationId InvitationId,
    DateTimeOffset OccurredAt) : IDomainEvent;