using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Communities;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Communities.Domain.Events;

public record MemberRoleChanged(
    CommunityId CommunityId,
    string Slug,
    UserId UserId,
    CommunityRole OldRole,
    CommunityRole NewRole,
    UserId ChangedBy,
    DateTimeOffset OccurredAt) : IDomainEvent;