namespace Kumunita.Shared.Kernel.Events;

// Optional base record for events that need OccurredAt populated automatically
public abstract record DomainEvent : IDomainEvent
{
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}