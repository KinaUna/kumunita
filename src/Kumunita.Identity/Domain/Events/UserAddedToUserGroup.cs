using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Identity.Domain.Events;

public record UserAddedToUserGroup(
    UserId UserId,
    GroupId GroupId,
    GroupRole Role,
    UserId AddedBy) : DomainEvent;