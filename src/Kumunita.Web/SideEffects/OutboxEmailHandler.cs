using Kumunita.Core.Identity;
using Marten;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The single durable <see cref="OutboxEmail"/> handler (ARCHITECTURE.md §6.2 —
/// "a single Wolverine durable handler" for all email kinds). All email kinds
/// travel as this one message type; the per-kind behavior (verification, report
/// notification, audience notification, event reminder) is encoded in the
/// <see cref="OutboxEmail.IdempotencyKey"/> and body, not in extra handlers —
/// matches the Critter-Stack reference template's "differentiate by
/// OutboxEmail, not by adding more handlers" shape (per
/// <c>snippets/wolverine-durable-email-handler.cs</c>).
/// <para>
/// <b>Two methods, one handler:</b>
/// <list type="bullet">
/// <item><see cref="Handle"/> — the per-attempt send. Called once per delivery
/// attempt. If the transport throws, the host's retry policy
/// (<c>RetryWithCooldown</c>, configured in <c>Program.cs</c> — 6 attempts over
/// ~24h with cooldowns, §6.2) schedules the next attempt. No dead-letter here:
/// the terminal failure is reported through the framework's
/// <c>Fault&lt;T&gt;</c> envelope after the retry schedule is exhausted, and
/// <see cref="HandleFault"/> is the hook that writes the domain
/// <see cref="EmailDeadLetter"/> row.</item>
/// <item><see cref="HandleFault"/> — the terminal-failure hook. Wired up by
/// <c>opts.PublishFaultEvents()</c> in the host, this receives a
/// <c>Fault&lt;OutboxEmail&gt;</c> envelope after the configured retry schedule
/// is exhausted (see the Critter-Stack reference
/// <c>error-handling-and-retries.md</c>: "Auto-publishes a typed
/// <c>Fault&lt;T&gt;</c> envelope (original message + exception + attempt
/// count + correlation ids) on terminal failure. This is likely the cleanest
/// hook for kumunita's <c>EmailDeadLetter</c> write"). It writes the
/// <em>domain</em> row (the operator-visible record that drives /health's
/// degraded gate) via the Wolverine-free <see cref="EmailDeadLetterWriter"/>,
/// so the row shape and the <c>SessionOptions</c> / <c>SaveChangesAsync</c>
/// contract stay testable (see <c>SideEffectHarnessTests</c>).</item>
/// </list>
/// </para>
/// <para>
/// <b>Why a static handler?</b> Per the CQRS-lite conventions
/// (<c>docs/wolverine/message-handlers.md</c>) the default preference is "static
/// methods with method injection" — the dependencies (<see cref="ISmtpSender"/>,
/// <see cref="IDocumentSession"/>) are visible right at the call site.
/// </para>
/// </summary>
public static class OutboxEmailHandler
{
    /// <summary>
    /// Per-attempt send. The durable policy (retry, cooldown, dead-letter) is
    /// configured once at startup in <c>Program.cs</c> — not per-call here — so
    /// this method stays a pure function of its inputs.
    /// </summary>
    public static Task Handle(OutboxEmail message, ISmtpSender sender)
    {
        if (message is null)
            throw new InvalidOperationException("Wolverine routed a null OutboxEmail to the handler.");
        if (string.IsNullOrWhiteSpace(message.IdempotencyKey))
            throw new InvalidOperationException(
                "OutboxEmail arrived without an IdempotencyKey; the §6.2 per-email key " +
                "(verify:{userId}:{attempt}, setup:{userId}, report:{id}, ...) is the " +
                "contract that bounds retry count and makes re-verify distinct from replay.");

        // SmtpSender.SendAsync throws on any failure (SmtpException / TimeoutException /
        // ArgumentException for a malformed recipient). The host's retry policy
        // (Program.cs) inspects the concrete exception and applies the configured
        // RetryWithCooldown list (6 attempts / ~24h per §6.2).
        return sender.SendAsync(message);
    }

    /// <summary>
    /// Terminal-failure hook: Wolverine publishes a <c>Fault&lt;OutboxEmail&gt;</c>
    /// after the configured retry schedule is exhausted (config:
    /// <c>opts.PublishFaultEvents()</c> in <c>Program.cs</c>). Writes the domain
    /// <see cref="EmailDeadLetter"/> row via the shared Wolverine-free writer so
    /// the failure/retry/dead-letter harness can verify the same shape without a
    /// live message host.
    /// <para>
    /// Critter-Stack reference (<c>error-handling-and-retries.md</c>): "This is
    /// likely the cleanest hook for kumunita's <c>EmailDeadLetter</c> write:
    /// handle <c>Fault&lt;OutboxEmail&gt;</c> rather than trying to detect
    /// 'last attempt' logic inside the primary handler itself."
    /// </para>
    /// </summary>
    public static async Task HandleFault(
        Wolverine.Fault<OutboxEmail> fault,
        IDocumentSession session)
    {
        var origin = fault.Message
            ?? throw new InvalidOperationException(
                "Fault envelope carried no original OutboxEmail; cannot write an EmailDeadLetter without it.");

        var lastError = fault.Exception?.Message ?? "unknown";
        var attempts  = fault.Attempts;   // set by Wolverine's retry schedule; 6 per §6.2

        await EmailDeadLetterWriter.WriteAndCommitAsync(
            session, origin, lastError, attempts);

        // /health (controllers/HealthController) reads the EmailDeadLetter count and
        // reports degraded while non-zero (OPS §8, ARCHITECTURE.md §6.2).
    }
}
