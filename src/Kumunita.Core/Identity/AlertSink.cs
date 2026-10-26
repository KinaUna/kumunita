namespace Kumunita.Core.Identity;

/// <summary>
/// One-shot, alert-worthy operational event (a domain payload, NOT a persisted
/// doc — it is never stored, it is the alert's input). Core-owned (ADR 0006 —
/// the domain "something alert-worthy happened" is a domain fact, not a
/// transport concern); the channel that delivers it (Sentry / SMTP / log) is
/// a Web + owner-ops concern (the <see cref="ISmtpSender"/> Core-seam /
/// Web-channel split).
/// <para>
/// The payload is a <em>closed</em> list of fields (the F9.U1 / F10 §3c
/// closed-list precedent). The fields are operator-readable and carry no
/// persisted-doc concerns: no <c>EmailDeadLetter.Id</c> (a persisted-doc
/// concern, stays in the /health / re-queue path), no timestamp (the alert's
/// *time* is stamped by the <em>channel</em> — Sentry's <c>Received</c>, the
/// log line, the human channel's receipt — not the domain; the domain
/// <see cref="AlertEvent"/> is a <em>value</em> payload).
/// </para>
/// </summary>
/// <param name="Kind">
/// A stable operator-readable discriminator. F11's one value is
/// <c>"email.dead-letter"</c>. <c>Kind</c> is a <c>string</c>, NOT an
/// <c>enum</c> — a new kind (e.g. F18's <c>"mail.bounce-alarm"</c>) is open
/// without a type change (a new enum would be a frozen-seam re-shape, the
/// "do not re-derive" discipline).
/// </param>
/// <param name="Subject">
/// The operator-readable one-liner — the "which address failed" the operator
/// re-queues (OPS §7). For F11 the <see cref="EmailDeadLetter.Recipient"/>
/// (falling back to the <c>IdempotencyKey</c>).
/// </param>
/// <param name="Detail">
/// The last delivery failure / threshold context (nullable). For F11 the
/// <see cref="EmailDeadLetter.LastError"/>.
/// </param>
/// <param name="Attempts">
/// The retry-count context (nullable so a non-retry alert, e.g. F18's volume
/// alarm, can carry no attempt count). For F11 the
/// <see cref="EmailDeadLetter.Attempts"/>.
/// </param>
public sealed record AlertEvent(
    string Kind,
    string Subject,
    string? Detail,
    int? Attempts);

/// <summary>
/// The alert seam (JOB-02). Core-owned (ADR 0006 — Core stays HTTP-free: the
/// seam never names Sentry/SMTP types, it only carries an <see cref="AlertEvent"/>).
/// The channel binding (the Sentry-backed <c>SentryAlertSink</c>, the F12
/// human-channel sink, the off-by-default <see cref="NoopAlertSink"/>) is a
/// Web + owner-ops concern.
/// <para>
/// <b>Contract: <see cref="ReportAsync"/> is best-effort at the boundary</b> —
/// a sink/channel failure must <em>NEVER</em> propagate into the caller's
/// domain write path (the <see cref="EmailDeadLetter"/> row is the
/// load-bearing operator signal, OPS §8; the alert is a <em>copy</em>, not
/// the <em>signal</em>). The off-by-default <see cref="NoopAlertSink"/> honors
/// this by construction (it does nothing); a real channel binding wraps its
/// call in <c>try/catch</c> (fail-open at the boundary).
/// </para>
/// </summary>
public interface IAlertSink
{
    /// <summary>
    /// Report one <see cref="AlertEvent"/> to the bound channel. Best-effort
    /// at the boundary: a channel failure must not propagate (see the type
    /// doc). The <paramref name="ct"/> is the standard one-payload + ct shape
    /// (the <see cref="ISmtpSender.SendAsync(OutboxEmail, CancellationToken)"/>
    /// precedent).
    /// </summary>
    Task ReportAsync(AlertEvent alert, CancellationToken ct = default);
}

/// <summary>
/// The <b>default</b> <see cref="IAlertSink"/> binding (off-by-default, the
/// F10 DSN-gated no-op idiom). <see cref="ReportAsync"/> does nothing: no
/// network, no throw, no state. The "no external telemetry by default"
/// standing deferral is honored by construction — an unconfigured instance
/// does not send any alert anywhere. The <c>/health</c> degraded gate is
/// the <em>always-on</em> signal (OPS §8); the alert is the <em>additive</em>
/// push (pin §3.0).
/// <para>
/// A structured <c>ILogger</c> line would also be in-scope (the pin fixes the
/// <em>behavior</em> — no network, no throw, no state — not the <em>form</em>);
/// this form is the simplest correct implementation (a pure no-op).
/// </para>
/// </summary>
public sealed class NoopAlertSink : IAlertSink
{
    public Task ReportAsync(AlertEvent alert, CancellationToken ct = default)
        => Task.CompletedTask;
}
