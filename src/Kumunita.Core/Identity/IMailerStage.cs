namespace Kumunita.Core.Identity;

/// <summary>
/// The email-staging seam (ARCHITECTURE.md §5/§6.1): the Identity lifecycle, the admin
/// manual-verify valve, and the first-boot seeder all *stage* an <see cref="OutboxEmail"/>
/// row **inside their own Marten session** (one <c>SaveChangesAsync</c> = the domain write
/// and the outbox row commit atomically — invariant C3, no silent missing email). The
/// durable <c>Wolverine</c> handler (M1 step 7) reads the staged rows and dispatches them
/// on a successful commit — a failed send never rolls back the domain write (the design
/// doc's "Handoff" test).
/// <para>
/// <b>This seam is deliberately abstract</b>: Core has no Wolverine dependency (ADR
/// 0006-D; the repo convention is "Wolverine is a *Web* package") and the seam's
/// contract is the <em>row's shape</em>, not the dispatch. A step-6
/// <see cref="OutboxEmailStager"/> implements it by storing the <c>OutboxEmail</c>
/// document on the session the caller supplies; step-7's durable handler (in
/// <c>Kumunita.Web/SideEffects/</c>, see <see cref="SmtpSender"/> for the
/// per-attempt seam it calls) <em>consumes</em> those rows after commit —
/// the two are complementary, not substitutes (the stager owns the write-path
/// contract; the handler owns the read-path side effect and the retry/dead-letter
/// policy from ARCHITECTURE.md §6.2).
/// </para>
/// </summary>
public interface IMailerStage
{
    /// <summary>
    /// Stage an outbound email row into <paramref name="session"/>. The row is stored
    /// (not dispatched) — the durable handler (M1 step 7) picks it up after the session's
    /// <c>SaveChangesAsync</c> commits. Idempotency: the caller supplies the
    /// <see cref="OutboxEmail.IdempotencyKey"/> (the §6.2 per-email key —
    /// <c>verify:{userId}:{attempt}</c>, <c>setup:{userId}</c>, ...).
    /// </summary>
    Task StageAsync(
        Marten.IDocumentSession session,
        string idempotencyKey,
        string recipient,
        string subject,
        string body,
        CancellationToken ct = default);
}

/// <summary>
/// The concrete <see cref="IMailerStage"/> implementation: stores the
/// <see cref="OutboxEmail"/> row on the caller's session (one
/// <c>SaveChangesAsync</c> = the domain write + the outbox row commit atomically).
/// This stays in production alongside the step-7 durable handler — the handler
/// (in <c>Kumunita.Web/SideEffects/</c>) *consumes* the row after commit and
/// dispatches over SMTP via <see cref="ISmtpSender"/>; the stager does not
/// change, it just writes the row. The seam's contract (row shape + idempotency
/// key) is unchanged across the two steps — only the <em>delivery</em>
/// mechanism is added.
/// </summary>
public sealed class OutboxEmailStager : IMailerStage
{
    /// <inheritdoc />
    public Task StageAsync(
        Marten.IDocumentSession session,
        string idempotencyKey,
        string recipient,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        session.Store(new OutboxEmail
        {
            Id = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            QueuedAt = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }
}
