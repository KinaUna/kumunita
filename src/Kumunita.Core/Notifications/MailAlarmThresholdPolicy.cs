using Kumunita.Core.Identity;

namespace Kumunita.Core.Notifications;

/// <summary>
/// The F18 (MAIL-06) T4 in-repo alarm's **decision** surface, extracted from
/// the <see cref="MailAlarmsService"/> I/O method as a pure, store-free
/// <c>*Policy</c> static (the C04 / C2-06 decision-extraction shape — the
/// <see cref="Kumunita.Core.Projects.GoalStandingPolicy"/> precedent: a
/// static class of pure methods over plain values, no
/// <c>Marten.IDocumentStore</c>, no <c>IDocumentSession</c>, no async I/O).
/// <para>
/// The <b>decision</b> (pin <c>docs/design/prod-hardening-f18-mailalarms.md</c>
/// §2/§3): given the three window counts the I/O method reads (the
/// <see cref="SuppressedAddress"/> <c>bounce</c> / <c>complaint</c> rows in
/// the last <see cref="MailAlarmsService.WindowHours"/> hours + the
/// <see cref="EmailDeadLetter"/> count), decide the T4 threshold breach
/// (<c>bounces + complaints ≥ AlertThreshold</c>) and — when breached — build
/// the exactly-one <see cref="AlertEvent"/> with the pin §3 payload mapping
/// (counts + the <c>Reason</c> breakdown only — **no PII**, pin §5.8; the
/// suppressed e-mails stay in the DB, never in the alert).
/// </para>
/// <para>
/// The I/O method (<see cref="MailAlarmsService.EvaluateAsync"/>) keeps the
/// reads (ONE read-only <c>QuerySession</c>) + the best-effort
/// <see cref="IAlertSink.ReportAsync"/> fire (the <c>null</c>-sink no-op +
/// the <c>try/catch</c> swallow, F11 pin §2a), returning the composed
/// <see cref="AlertEvent"/> (or <c>null</c> under threshold) so the
/// Web-layer handler can read <see cref="AlertEvent.Subject"/> for its
/// notification body (IMPROVE-UX O04); the behaviour is unchanged — only the
/// decision's location moves here, where a plain unit test
/// (<c>MailAlarmThresholdPolicyTests</c>) can name it without Postgres.
/// </para>
/// </summary>
public static class MailAlarmThresholdPolicy
{
    /// <summary>
    /// The T4 decision (pin §2/§3): <paramref name="bounces"/> +
    /// <paramref name="complaints"/> <see cref="MailAlarmsService.AlertThreshold"/> or
    /// higher → exactly one <see cref="AlertEvent"/> (the pin §3 mapping —
    /// <c>Kind</c> = <see cref="MailAlarmsService.AlertKind"/>, the counts +
    /// the <c>Reason</c> breakdown + the dead-letter count in <c>Detail</c>,
    /// <c>Attempts</c> null — a non-retry alert carries no attempt count);
    /// under the threshold → <c>null</c> (no alert). Pure over the three
    /// counts — no store, no sink, no I/O.
    /// </summary>
    /// <param name="bounces">The window's <c>SuppressedAddress</c> rows with <c>Reason == "bounce"</c> (pin §2 T4 closed set).</param>
    /// <param name="complaints">The window's <c>SuppressedAddress</c> rows with <c>Reason == "complaint"</c>.</param>
    /// <param name="deadLetters">The <c>EmailDeadLetter</c> count (pin §1 second signal — the <c>/health</c> degraded gate is unchanged, pin §5.5).</param>
    /// <param name="windowHours">
    /// The rolling window for the payload text — the
    /// <see cref="MailAlarmsService.WindowHours"/> constant by default (the
    /// single source).
    /// </param>
    /// <param name="alertThreshold">
    /// The breach threshold — the
    /// <see cref="MailAlarmsService.AlertThreshold"/> constant by default (the
    /// single source).
    /// </param>
    /// <returns>The breach alert when <c>bounces + complaints ≥ alertThreshold</c>; otherwise <c>null</c>.</returns>
    public static AlertEvent? Build(
        int bounces,
        int complaints,
        int deadLetters,
        int windowHours = MailAlarmsService.WindowHours,
        int alertThreshold = MailAlarmsService.AlertThreshold)
    {
        int total = bounces + complaints;
        if (total < alertThreshold)
            return null;    // under the T4 threshold — no alert (the pin §6 "still broken, still loud" re-fire is the intended semantics while breached)

        // The pin §3 payload mapping (fixed, U2 implements) — no PII (pin §5.8):
        // counts + the Reason breakdown only; the suppressed e-mails stay in the
        // DB. Attempts is null (the F11 pin: a non-retry alert carries no
        // attempt count).
        return new AlertEvent(
            Kind:     MailAlarmsService.AlertKind,
            Subject:  $"SuppressedAddress ingest: {total} new bounce/complaint rows in {windowHours}h (threshold {alertThreshold})",
            Detail:   $"bounce: {bounces}, complaint: {complaints}; EmailDeadLetter count: {deadLetters}",
            Attempts: null);
    }
}
