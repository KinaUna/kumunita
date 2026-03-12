using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Communities;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record MemberJoinedCommunity(
    CommunityId CommunityId,
    string Slug,
    UserId UserId,
    CommunityRole Role,
    UserId InvitedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;