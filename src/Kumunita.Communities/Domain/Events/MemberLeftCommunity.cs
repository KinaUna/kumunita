using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record MemberLeftCommunity(
    CommunityId CommunityId,
    string Slug,
    UserId UserId,
    DateTimeOffset OccurredAt) : IDomainEvent;