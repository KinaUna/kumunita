using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Identity.Domain.Events;

public record UserRemovedFromUserGroup(
    UserId UserId,
    GroupId GroupId,
    UserId RemovedBy) : DomainEvent;