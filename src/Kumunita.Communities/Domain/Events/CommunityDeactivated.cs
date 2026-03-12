using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record CommunityDeactivated(
    CommunityId CommunityId,
    string Slug,
    string DeactivatedByPlatformAdmin,
    DateTimeOffset OccurredAt) : IDomainEvent;