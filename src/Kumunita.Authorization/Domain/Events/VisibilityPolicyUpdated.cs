using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Authorization.Domain.Events;

public record VisibilityPolicyUpdated(
    UserId OwnerId,
    string ResourceTypeName,
    VisibilityLevel NewVisibility,
    IReadOnlyList<GroupId> AllowedGroupIds,
    IReadOnlyList<UserId> AllowedUserIds) : DomainEvent;