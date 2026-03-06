using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Identity.Domain.Events;

public record RoleRevoked(
    UserId UserId,
    string RoleName,
    UserId RevokedBy) : DomainEvent;