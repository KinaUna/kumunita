using Marten;

namespace Kumunita.Core.Identity;

/// <summary>
/// Terminal-failure write for the durable <see cref="OutboxEmail"/> path
/// (ARCHITECTURE.md §5/§6.2). This is the <em>single</em> writer of
/// <see cref="EmailDeadLetter"/> rows, and it is deliberately Wolverine-free:
/// the host's durable handler hands the final-failure details here (either via
/// the <c>Fault&lt;OutboxEmail&gt;</c> hook configured with
/// <c>PublishFaultEvents()</c>, or by inspecting an exhausted retry), and this
/// method stores the domain row on the caller's session so it commits in the
/// same Marten transaction as Wolverine's "message is dead" bookkeeping.
/// <para>
/// <b>Why a shared writer instead of writing the row in the handler?</b> The
/// dead-letter write is domain state (it drives /health's degraded gate and the
/// operator's re-queue), not Wolverine plumbing. Keeping the POCO construction
/// and the <c>SaveChangesAsync</c> call in one testable place means the
/// failure/retry/dead-letter harness (M1 step 7) can verify the exact row shape
/// and the same-tx-commit guarantee without spinning up a live message host.
/// </para>
/// </summary>
public static class EmailDeadLetterWriter
{
    /// <summary>The <c>EmailDeadLetter</c> attempt count on the final failure (§6.2: "6 over ~24 h").</summary>
    public const int MaxAttempts = 6;

    /// <summary>
    /// Write the domain dead-letter document into <paramref name="session"/>.
    /// </summary>
    /// <param name="session">The live Marten session; the row commits when the caller's <c>SaveChangesAsync</c> runs.</param>
    /// <param name="origin">The <see cref="OutboxEmail"/> that was ultimately undeliverable (source for recipient/key/subject).</param>
    /// <param name="lastError">The last delivery failure (the operator's re-queue decision input, OPS §7). Null for an unknown error.</param>
    /// <param name="attempts">Attempts made before the dead-letter (typically <see cref="MaxAttempts"/>.</param>
    /// <param name="ct">Cancellation.</param>
    public static EmailDeadLetter Write(
        IDocumentSession session,
        OutboxEmail origin,
        string? lastError,
        int attempts,
        CancellationToken ct = default)
    {
        if (session is null) throw new ArgumentNullException(nameof(session));
        if (origin is null) throw new ArgumentNullException(nameof(origin));

        var now = DateTimeOffset.UtcNow;
        var row = new EmailDeadLetter
        {
            Id = Guid.NewGuid().ToString("N"),
            IdempotencyKey = origin.IdempotencyKey,
            Recipient = origin.Recipient,
            Subject = origin.Subject,
            LastError = lastError,
            Attempts = attempts,
            CreatedAt = now,
            DeadAt = now
        };

        session.Store(row);
        return row;
    }

    /// <summary>
    /// Convenience overload: write <em>and</em> commit. Used when the dead-letter
    /// write is the terminal action of a handler (the row's own commit is
    /// separate from — and can never roll back — the caller's earlier domain
    /// write that staged the <see cref="OutboxEmail"/>).
    /// </summary>
    /// <returns>The stored <see cref="EmailDeadLetter"/> row.</returns>
    public static async Task<EmailDeadLetter> WriteAndCommitAsync(
        IDocumentSession session,
        OutboxEmail origin,
        string? lastError,
        int attempts,
        CancellationToken ct = default)
    {
        var row = Write(session, origin, lastError, attempts, ct);
        await session.SaveChangesAsync();
        return row;
    }
}
