using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Identity.Domain.Events;

public record AccountReactivated(
    UserId UserId,
    UserId ReactivatedBy) : DomainEvent;