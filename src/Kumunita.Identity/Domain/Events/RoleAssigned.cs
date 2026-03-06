using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Identity.Domain.Events;

public record RoleAssigned(
    UserId UserId,
    string RoleName,
    UserId AssignedBy) : DomainEvent;