using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.Events;

namespace Kumunita.Identity.Domain.Events;

public record AccountSuspended(
    UserId UserId,
    UserId SuspendedBy,
    string Reason) : DomainEvent;