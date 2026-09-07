namespace Kumunita.Core.Identity;

/// <summary>
/// The email-staging seam (ARCHITECTURE.md §5/§6.1): the Identity lifecycle, the admin
/// manual-verify valve, and the first-boot seeder all *stage* an <see cref="OutboxEmail"/>
/// row **inside their own Marten session** (one <c>SaveChangesAsync</c> = the domain write
/// and the outbox row commit atomically — invariant C3, no silent missing email). The
/// durable <c>Wolverine</c> handler (M1 step 7) consumes the enqueued
/// <c>OutboxEmail</c>/dispatches over SMTP on a successful send; a failed send never
/// rolls back the domain write that already committed (the design doc's "Handoff" test).
/// <para>
/// <b>Contract (M1 step 7 — the C3-envelope fix, see
/// <c>docs/plans-milestones/plan-m1-step-7-outbox-email-c3.md</c>, unit U1/U2):</b>
/// <see cref="StageAsync"/> now <em>both</em> stores the <c>OutboxEmail</c> row on
/// <paramref name="session"/> <em>and</em> enqueues the matching durable-message
/// envelope into Wolverine's message store in the <em>same</em> transaction as
/// <paramref name="session"/> — the envelope is held until the caller's own
/// <c>SaveChangesAsync</c> commits (Wolverine's transactional-outbox guarantee,
/// <c>docs/wolverine/marten-integration-outbox.md</c>). A caller whose
/// <c>SaveChangesAsync</c> never commits (or throws) therefore never leaves a
/// queued/dispatched email behind.
/// <para>
/// Because of that, <c>Kumunita.Core</c> now carries a direct reference to
/// <c>WolverineFx</c> (same 6.33.0 pin as <c>Kumunita.Web</c> — see
/// <c>Kumunita.Core.csproj</c>) — the prior "Wolverine is a *Web* package" convention
/// (ADR 0006-D) is deliberately broken here: the seam's contract is still the
/// <em>row's shape</em> + the caller's transaction, not the dispatch mechanics, and the
/// <see cref="OutboxEmailStager"/> implementation needs the per-scope
/// <c>Wolverine.IMessageContext</c> (the same primitive the repo's own
/// <c>snippets/wolverine-durable-email-handler.cs</c> relies on) rather than the
/// host-level <c>IMessageBus</c> (which additionally asserts the host is already
/// started — see the risk R1 discussion in the plan).
/// </para>
/// <para>
/// The durable handler (in <c>Kumunita.Web/SideEffects/</c>, see
/// <see cref="SmtpSender"/> for the per-attempt seam it calls) <em>consumes</em>
/// the staged row after commit — the two are complementary, not substitutes (the
/// stager owns the write-path contract; the handler owns the read-path side effect
/// and the retry/dead-letter policy from ARCHITECTURE.md §6.2).
/// </para>
/// </summary>
public interface IMailerStage
{
    /// <summary>
    /// Stage an outbound email row into <paramref name="session"/> <em>and</em> enqueue
    /// the matching durable-message envelope into Wolverine's Postgres-backed message
    /// store, in the same transaction as <paramref name="session"/> (the envelope is held
    /// until the caller's own <c>SaveChangesAsync</c> commits — the C3 guarantee this
    /// fix restores, see <c>docs/plans-milestones/plan-m1-step-7-outbox-email-c3.md</c>).
    /// Idempotency: the caller supplies the <see cref="OutboxEmail.IdempotencyKey"/>
    /// (the §6.2 per-email key — <c>verify:{userId}:{attempt}</c>, <c>setup:{userId}</c>,
    /// ...).
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
/// The concrete <see cref="IMailerStage"/> implementation: <c>Store</c>s the
/// <see cref="OutboxEmail"/> row on the caller's session <em>and</em> enqueues the
/// matching durable-message envelope via the constructor-injected
/// <see cref="Wolverine.IMessageContext"/> (the U1-pinned call —
/// <c>IMessageContext.PublishAsync&lt;T&gt;(T)</c>, <c>ValueTask</c>, see
/// <see cref="U1PinnedApiProbe"/> for the verified 6.33.0 surface and
/// <c>docs/plans-milestones/m1-step-7-handoff-notes.md</c>, section "U1 — pinned
/// API"). Both land in the ambient Marten transaction — one <c>SaveChangesAsync</c>
/// = the domain write + the outbox row + the envelope commit atomically (invariant
/// C3, no silent missing email). This stays in production alongside the step-7
/// durable handler — the handler (in <c>Kumunita.Web/SideEffects/</c>) consumes the
/// dispatched envelope after commit and sends over SMTP via <see cref="ISmtpSender"/>.
/// </summary>
public sealed class OutboxEmailStager : IMailerStage
{
    private readonly Wolverine.IMessageContext _messageContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxEmailStager"/> class.
    /// </summary>
    /// <param name="messageContext">The per-scope Wolverine message context — the
    /// transactional-outbox primitive that participates in the ambient
    /// <see cref="Marten.IDocumentSession"/>'s transaction without asserting the
    /// host is already started (unlike <c>Wolverine.IMessageBus</c>). Resolved from
    /// Core's own composition root (see <c>Kumunita.Core/DependencyInjection.cs</c>).
    /// </param>
    public OutboxEmailStager(Wolverine.IMessageContext messageContext)
    {
        _messageContext = messageContext ?? throw new ArgumentNullException(nameof(messageContext));
    }

    /// <inheritdoc />
    public async Task StageAsync(
        Marten.IDocumentSession session,
        string idempotencyKey,
        string recipient,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        var email = new OutboxEmail
        {
            Id = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            QueuedAt = DateTimeOffset.UtcNow
        };
        session.Store(email);
        // U1-pinned (see U1PinnedApiProbe / m1-step-7-handoff-notes.md "U1 — pinned
        // API"): IMessageContext.PublishAsync<T>(T) -> ValueTask on Wolverine 6.33.0.
        await _messageContext.PublishAsync(email);
    }
}
