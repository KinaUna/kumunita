using Kumunita.Core.Privacy;

namespace Kumunita.Core.Identity;

/// <summary>
/// An outbound email through the durable outbox (ARCHITECTURE.md §5/§6.1 — all email is a
/// side effect; a handler publishes this within its command's transaction, and the
/// Wolverine transactional outbox dispatches only on a successful commit).
/// </summary>
[DataClass(DataClassKind.Pii, Erasure.Delete, ExportScope.Never,
    reason: "F1 §2C decision (now decided); D6: body nulled 7 days after delivery, row deleted 90 days",
    retentionKey: "OutboxBodyDays")]
public sealed class OutboxEmail
{
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The per-email idempotency key (§6.2): an outbox row keyed on this so retries are
    /// bounded and a re-verify (a new attempt) is distinct from a replay.
    /// <c>verify:{userId}:{attempt}</c>; <c>setup:{userId}</c>; later kinds ride the
    /// same key scheme.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>Rendered Markdown (the durable state the handler needs when SMTP is down).</summary>
    public string Body { get; set; } = string.Empty;

    public DateTimeOffset QueuedAt { get; set; }
}

/// <summary>
/// A dead-letter row (ARCHITECTURE.md §5/§6.2): written by the durable email handler when all
/// retries over ~24 hours are exhausted (§6.2). A non-empty count drives <c>/health</c>'s
/// **degraded** state (OPS §8); the operator re-queues or discards rows (OPS §7).
/// </summary>
[DataClass(DataClassKind.Operational, Erasure.Delete, ExportScope.Never,
    reason: "D6: 30 days; the recipient is the resident's address (IS34a)",
    retentionKey: "DeadLetterDays")]
public sealed class EmailDeadLetter
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The idempotency key the dead-lettered attempt was keyed on.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>The last delivery failure (for the operator's re-queue decision, OPS §7).</summary>
    public string? LastError { get; set; }

    /// <summary>Attempts made (6 over ~24 h before dead-letter, §6.2).</summary>
    public int Attempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset DeadAt { get; set; }
}
