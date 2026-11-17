using Kumunita.Core.Identity;
using Kumunita.Core.Notifications;
using Marten;

namespace Kumunita.Core.InstanceStatus;

/// <summary>
/// The F18 T4 mail-alarm state read (IMPROVE-UX O01, the
/// <see cref="InstanceStatus"/> POCO's <see cref="InstanceStatus.MailAlarm"/>
/// section). A small **read-only** seam (the
/// <see cref="IEmailDeadLetterCounter"/> / <see cref="Kumunita.Core.Usage.IStorageMetricsService"/>
/// precedent — one DB read behind an interface, so the
/// <see cref="InstanceStatusService"/> composer can be tested with the seam
/// substituted rather than a live <c>PostgresFixture</c>).
/// <para>
/// Reuses, not re-derives:
/// <list type="bullet">
/// <item>the <see cref="IEmailDeadLetterCounter"/> dead-letter count (a
/// reused Identity seam, unchanged);</item>
/// <item>the <see cref="MailAlarmThresholdPolicy.Build"/> T4 firing decision
/// (a reused pure Notifications seam, unchanged — the
/// <see cref="MailAlarmsService.AlertThreshold"/> /
/// <see cref="MailAlarmsService.WindowHours"/> platform constants are the
/// single source).</item>
/// </list>
/// The one DB query this seam owns is the window's
/// <see cref="SuppressedAddress"/> bounce / complaint count — the exact query
/// shape the <see cref="MailAlarmsService"/> T4 gate uses (the
/// <see cref="MailAlarmsService.WindowHours"/> rolling window, the
/// <c>"bounce"</c> / <c>"complaint"</c> closed set). The alarm *firing*
/// itself is unchanged — the <see cref="MailAlarmsService"/> /
/// <see cref="IAlertSink"/> firing path is NOT touched (O04 adds the inbox
/// leg, not O01).
/// </para>
/// <para>
/// <b>Read-only:</b> a <c>QuerySession</c> (read-only by construction), zero
/// writes, no new doc, no doc-surface change.
/// </para>
/// </summary>
public interface IMailAlarmStateReader
{
    /// <summary>
    /// The T4 mail-alarm state: the window's <see cref="SuppressedAddress"/>
    /// bounce / complaint counts, the <see cref="IEmailDeadLetterCounter"/>
    /// dead-letter count, and whether the alarm is currently firing (the
    /// <see cref="MailAlarmThresholdPolicy.Build"/> decision).
    /// </summary>
    /// <param name="now">
    /// The "now" instant (the <see cref="MailAlarmsService"/>
    /// <c>now</c>-injection precedent — the window start is
    /// <c>now - WindowHours</c>, so the boundary is deterministic under test).
    /// </param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The <see cref="MailAlarmCounts"/> snapshot (counts + the firing boolean).</returns>
    Task<MailAlarmCounts> ReadAsync(DateTimeOffset now, CancellationToken ct);
}

/// <summary>
/// The F18 T4 mail-alarm state (the
/// <see cref="IMailAlarmStateReader.ReadAsync"/> result — the
/// <see cref="InstanceStatus.MailAlarm"/> section's source). Counts + a
/// firing boolean — <b>never</b> the suppressed e-mail addresses (the
/// <see cref="MailAlarmsService"/> no-PII invariant, pin §5.8).
/// </summary>
public sealed record MailAlarmCounts(
    int Bounces,
    int Complaints,
    int DeadLetters,
    bool Firing);

/// <summary>
/// The production <see cref="IMailAlarmStateReader"/> (the
/// <see cref="Kumunita.Core.Identity.EmailDeadLetterCounter"/> shape — a
/// store-injected read seam). The window's bounce / complaint read is the
/// exact <see cref="MailAlarmsService"/> T4 query shape (one
/// <c>QuerySession</c>, the <see cref="MailAlarmsService.WindowHours"/>
/// rolling window, the <c>"bounce"</c> / <c>"complaint"</c> closed set); the
/// dead-letter count reuses the <see cref="IEmailDeadLetterCounter"/> seam;
/// the firing decision reuses the pure <see cref="MailAlarmThresholdPolicy.Build"/>
/// (the <see cref="MailAlarmsService.AlertThreshold"/> platform constant is
/// the single source).
/// </summary>
public sealed class MailAlarmStateReader : IMailAlarmStateReader
{
    private readonly IDocumentStore _store;
    private readonly IEmailDeadLetterCounter _deadLetters;

    public MailAlarmStateReader(IDocumentStore store, IEmailDeadLetterCounter deadLetters)
    {
        _store = store;
        _deadLetters = deadLetters;
    }

    /// <inheritdoc />
    public async Task<MailAlarmCounts> ReadAsync(DateTimeOffset now, CancellationToken ct)
    {
        var windowStart = now.AddHours(-MailAlarmsService.WindowHours);

        int bounces, complaints;
        // One read-only QuerySession (read-only by construction — the "no
        // write" invariant is enforced by the type), the exact
        // MailAlarmsService T4 query shape (the WindowHours rolling window,
        // the "bounce" / "complaint" closed set — "manual" rows do not trip
        // the alarm).
        await using (var session = _store.QuerySession())
        {
            bounces = await session.Query<SuppressedAddress>()
                .Where(a => a.Created >= windowStart && a.Reason == "bounce")
                .CountAsync(ct)
                .ConfigureAwait(false);
            complaints = await session.Query<SuppressedAddress>()
                .Where(a => a.Created >= windowStart && a.Reason == "complaint")
                .CountAsync(ct)
                .ConfigureAwait(false);
        }

        // Reuse the IEmailDeadLetterCounter seam (not a second inline count).
        var deadLetters = await _deadLetters.GetCountAsync(ct).ConfigureAwait(false);

        // Reuse the pure MailAlarmThresholdPolicy decision (the firing
        // boolean) — the MailAlarmsService.AlertThreshold platform constant
        // is the single source.
        var firing = MailAlarmThresholdPolicy.Build(bounces, complaints, deadLetters) is not null;

        return new MailAlarmCounts(bounces, complaints, deadLetters, firing);
    }
}
