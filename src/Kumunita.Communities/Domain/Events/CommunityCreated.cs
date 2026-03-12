using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record CommunityCreated(
    CommunityId CommunityId,
    string Slug,
    string CreatedByPlatformAdmin,
    DateTimeOffset OccurredAt) : IDomainEvent;