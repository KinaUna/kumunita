namespace Kumunita.Shared.Kernel.Events;

// Marker interface — enables Wolverine routing policies
// e.g. "all domain events go to durable local queues"
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
