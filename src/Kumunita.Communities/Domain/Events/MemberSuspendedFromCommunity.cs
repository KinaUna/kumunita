using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record MemberSuspendedFromCommunity(
    CommunityId CommunityId,
    string Slug,
    UserId UserId,
    UserId SuspendedBy,
    string Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;