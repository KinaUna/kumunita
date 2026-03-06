using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Identity.Domain.Events;

public record UserGroupCreated(
    GroupId GroupId,
    UserId OwnerId,
    GroupId? ParentGroupId) : DomainEvent;