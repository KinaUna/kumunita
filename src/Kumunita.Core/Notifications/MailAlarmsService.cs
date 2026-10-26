using Kumunita.Core.Identity;
using Marten;

namespace Kumunita.Core.Notifications;

/// <summary>
/// The F18 (MAIL-06) in-repo bounce/complaint ingest-rate alarm — the **T4
/// code leg** (pin <c>docs/design/prod-hardening-f18-mailalarms.md</c> §6; the
/// provider-side alarms T1–T3 are the owner-ops leg, F18.U3). A
/// <b>read-only, Wolverine-free, HTTP-free</b> static service (ADR 0006-D: no
/// <c>IHttpContextAccessor</c>, no Web types) — the exact
/// <see cref="Kumunita.Core.Media.MediaLifecycleService"/> /
/// <see cref="Kumunita.Core.Usage.UsagePurgeService"/> shape (static,
/// store-injected, a <c>now</c> injection param for test determinism), the
/// difference being this one only <em>reads</em> (no deletes, no writes — the
/// read-only cross-check, pin §5.3/§5.8).
/// <para>
/// The T4 gate (pin §2): count <see cref="SuppressedAddress"/> rows with
/// <c>Created</c> in the last <see cref="WindowHours"/> hours and
/// <c>Reason</c> in the <c>"bounce"</c>/<c>"complaint"</c> closed set; when
/// the total is <see cref="AlertThreshold"/> or higher (≥ 5), fire <b>one</b>
/// <see cref="AlertEvent"/> with the pin §3 payload mapping through the
/// existing Core <see cref="IAlertSink"/> seam (F12's binding — the
/// <c>SupportEmailAlertSink</c> in production, the <c>NoopAlertSink</c> floor
/// when unconfigured). Best-effort at the boundary: the
/// <see cref="IAlertSink.ReportAsync"/> call is wrapped in <c>try/catch</c>
/// swallow (a channel failure never propagates; the <see cref="SuppressedAddress"/>
/// rows are the load-bearing signal, the alert is a copy — F11 pin §2a). No
/// PII in the alert body (pin §5.8): counts + the <c>Reason</c> breakdown
/// only — the suppressed e-mails stay in the DB, never in the alert.
/// </para>
/// </summary>
public static class MailAlarmsService
{
    /// <summary>
    /// The T4 in-repo alarm threshold (pin §2 T4, a platform constant): the
    /// number of new <see cref="SuppressedAddress"/> rows with
    /// <c>Reason</c> ∈ <c>"bounce"</c>/<c>"complaint"</c> in the last
    /// <see cref="WindowHours"/> hours that trips the alert. A platform
    /// constant, not a config knob (the
    /// <see cref="Kumunita.Core.Usage.UsagePurgeService.RetentionDays"/>
    /// precedent: the operator who wants a different value edits the constant
    /// and redeploys; the per-instance knob is a named deferral).
    /// </summary>
    public const int AlertThreshold = 5;

    /// <summary>
    /// The T4 rolling window (pin §2 T4 — "last 24 h", a platform constant):
    /// the <see cref="SuppressedAddress.Created"/> look-back the gate counts
    /// over.
    /// </summary>
    public const int WindowHours = 24;

    /// <summary>
    /// The pin §3 payload mapping's <c>Kind</c> — the F11 pin's
    /// pre-reserved <c>"mail.bounce-alarm"</c> (a <c>string</c>, NOT an
    /// <c>enum</c> — F11's enabling design so F18 adds a kind without a type
    /// change). Rides F12's kind-agnostic <see cref="IAlertSink"/> binding.
    /// </summary>
    public const string AlertKind = "mail.bounce-alarm";

    /// <summary>
    /// Reads the <see cref="SuppressedAddress"/> list (the F17 data source,
    /// pin §1) + the <see cref="EmailDeadLetter"/> count (pin §1) and, when the
    /// T4 gate (pin §2) is breached, fires exactly <b>one</b>
    /// <see cref="AlertEvent"/> through <see cref="IAlertSink.ReportAsync"/>
    /// (best-effort, <c>try/catch</c> swallow). <b>Read-only</b> (pin §5.3) —
    /// no writes, no new doc, no doc-surface change; the <c>/health</c>
    /// degraded gate is UNCHANGED (pin §5.5).
    /// </summary>
    /// <param name="store">The shared document store (the host injects the same <c>IDocumentStore</c> the domain services use).</param>
    /// <param name="alertSink">
    /// The existing Core <see cref="IAlertSink"/> seam (F12's binding in
    /// production; the <c>NoopAlertSink</c> floor when unconfigured).
    /// <c>null</c> is a no-op (a harness-construction site that did not supply
    /// one — the F11 <c>HandleFault</c> idiom).
    /// </param>
    /// <param name="now">Injection point for tests (pin the window start so the 24 h boundary is deterministic).</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns><c>true</c> when the T4 gate was breached (the alert fired, or was a no-op on an unconfigured sink); <c>false</c> when under threshold (no alert).</returns>
    public static async Task<bool> EvaluateAsync(
        IDocumentStore store,
        IAlertSink? alertSink,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        var windowStart = now.AddHours(-WindowHours);

        int bounces, complaints, deadLetters;
        // Read-only cross-check (pin §1/§5.3): count the F17 list's new
        // bounce/complaint rows in the window + the EmailDeadLetter count
        // (pin §1 second signal) — all in ONE read session (a QuerySession is
        // read-only by construction — the "no write" invariant is enforced by
        // the type), zero writes, zero SaveChangesAsync.
        await using (var session = store.QuerySession())
        {
            // The Reason filter is the pin §2 T4 closed set ("bounce" |
            // "complaint"); "manual" rows do not trip the alarm (the gate is
            // the provider bounce/complaint ingest, not manual suppression).
            bounces = await session.Query<SuppressedAddress>()
                .Where(a => a.Created >= windowStart && a.Reason == "bounce")
                .CountAsync(ct);
            complaints = await session.Query<SuppressedAddress>()
                .Where(a => a.Created >= windowStart && a.Reason == "complaint")
                .CountAsync(ct);
            // The EmailDeadLetter count (pin §1 second signal, read-only — the
            // /health degraded gate is UNCHANGED, pin §5.5).
            deadLetters = await session.Query<EmailDeadLetter>()
                .CountAsync(ct);
        }

        var total = bounces + complaints;
        if (total < AlertThreshold)
            return false;   // under the T4 threshold — no alert (the pin §6 "still broken, still loud" re-fire is the intended semantics while breached)

        // The pin §3 payload mapping (fixed, U2 implements) — no PII (pin §5.8):
        // counts + the Reason breakdown only; the suppressed e-mails stay in the
        // DB. Attempts is null (the F11 pin: a non-retry alert carries no
        // attempt count).
        var alert = new AlertEvent(
            Kind:     AlertKind,
            Subject:  $"SuppressedAddress ingest: {total} new bounce/complaint rows in {WindowHours}h (threshold {AlertThreshold})",
            Detail:   $"bounce: {bounces}, complaint: {complaints}; EmailDeadLetter count: {deadLetters}",
            Attempts: null);

        // Best-effort at the boundary (the IAlertSink contract, F11 pin §2a /
        // pin §5.6): a channel failure never propagates — the
        // SuppressedAddress rows + the provider console are the load-bearing
        // signals, the alert is a copy.
        if (alertSink is null)
            return true;    // the gate breached but there is no channel (a no-op, not a failure)
        try
        {
            await alertSink.ReportAsync(alert, ct);
        }
        catch
        {
            // Swallowed (pin §5.6 contract) — a sink/channel failure is never
            // rethrown; the owner sees the SuppressedAddress rows in the DB
            // regardless.
        }
        return true;
    }
}
