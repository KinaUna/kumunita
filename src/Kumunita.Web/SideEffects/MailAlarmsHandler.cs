using Kumunita.Core.Identity;
using Kumunita.Core.Notifications;
using Marten;
using Microsoft.Extensions.Logging;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <see cref="MailAlarmsTick"/> job (the
/// <see cref="MediaLifecycleHandler"/> shape verbatim). Each run re-publishes
/// <see cref="MailAlarmsTick"/> for the next hour (the
/// <see cref="Wolverine.TimeoutMessage"/>'s 1-hour delay is baked into the
/// type, so this re-publish picks up the same cadence). The business logic is
/// the Wolverine-free, HTTP-free <see cref="MailAlarmsService"/> in
/// <c>Kumunita.Core</c> — this handler is a thin adapter that injects a live
/// <see cref="IDocumentStore"/> and the existing <see cref="IAlertSink"/>
/// (F12's binding in production, the <c>NoopAlertSink</c> floor when
/// unconfigured) into that service, then re-schedules the next tick.
/// Postgres-backed durability: a Coolify redeploy mid-hour does not silently
/// drop a pending run (the <c>AuditPurgeHandler</c> precedent).
/// <para>
/// IMPROVE-UX O04 (D8) adds the **second delivery channel** for a T4
/// mail-alarm breach: an inbox notification to every <c>GlobalAdmin</c>
/// (the <see cref="NotificationKinds.OpsAlarm"/> kind,
/// <c>LinkPath = /admin/status</c>). The E10 / D8 concern: the e-mail leg
/// (the <see cref="IAlertSink"/> → <c>SupportEmailAlertSink</c> → SMTP) is
/// unreliable precisely because the SMTP relay that failed may be what
/// tripped the alarm. The inbox leg is additive — it does NOT change the
/// threshold, the window, the read session, the
/// <see cref="MailAlarmThresholdPolicy.Build"/> decision, or the
/// <see cref="IAlertSink"/> e-mail leg; only the *delivery* gains a second
/// channel. Best-effort at the boundary (the A14 / ADR 0077 D5 posture, the
/// <see cref="Kumunita.Core.Identity.IdentityService"/>
/// <c>RequestStorageQuotaIncreaseAsync</c> precedent): a missing emitter, a
/// fresh instance with no GlobalAdmin, or an emit error is swallowed +
/// logged, never rethrown — the tick still self-schedules and the e-mail leg
/// is unaffected.
/// </para>
/// </summary>
public static class MailAlarmsHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 hour ahead. Because the message
    /// type bakes in the delay (<see cref="Wolverine.TimeoutMessage"/>'s
    /// schedule constructor), re-yielding a fresh <see cref="MailAlarmsTick"/>
    /// carries the same schedule every run — no per-callsite
    /// <c>DelayedFor</c> needed.
    /// <para>
    /// <c>Task&lt;IEnumerable&lt;object&gt;&gt;</c> is the async-eligible
    /// cascade shape in Wolverine (an <c>IEnumerable&lt;object&gt;</c> iterator
    /// can't <c>await</c>); we return the array rather than
    /// <c>yield return</c> (which is invalid inside an async method).
    /// </para>
    /// <para>
    /// When <see cref="MailAlarmsService.EvaluateAsync"/> returns a non-null
    /// <see cref="AlertEvent"/> (the T4 gate breached), this handler also
    /// emits one <see cref="NotificationKinds.OpsAlarm"/> inbox row per
    /// <c>GlobalAdmin</c> (the O04 / D8 second channel; the A14 fan-out
    /// shape). The body is the <see cref="AlertEvent.Subject"/> (counts +
    /// threshold, **no PII**); the <c>LinkPath</c> is <c>/admin/status</c>
    /// (the O02 page). Best-effort — a failure is swallowed + logged.
    /// </para>
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        MailAlarmsTick tick,
        IDocumentStore store,
        IAlertSink alertSink,
        INotificationEmitter notifications,
        IIdentityService identity,
        // Non-generic ILogger (the app registers it as a singleton, Program.cs)
        // — ILogger<T> cannot target a static type (CS0718), the
        // DeletionDueHandler precedent.
        ILogger logger)
    {
        var alert = await MailAlarmsService.EvaluateAsync(store, alertSink, DateTimeOffset.UtcNow);

        // O04 / D8 — the second delivery channel (the inbox leg over the
        // existing e-mail leg). Only when the gate breached (non-null
        // alert). Best-effort at the boundary (the A14 / ADR 0077 D5
        // posture): a missing emitter, a fresh instance with no GlobalAdmin,
        // or an emit error is swallowed + logged — never rethrown. The
        // e-mail leg (inside EvaluateAsync) and the tick self-schedule below
        // are unaffected.
        if (alert is not null)
        {
            try
            {
                var admins = (await identity.ListGlobalAdminsAsync()).ToList();
                if (admins.Count > 0)
                {
                    var fireMinute = DateTime.UtcNow.ToString("yyyyMMddHHmm");
                    await using var session = store.OpenSession(new Marten.Services.SessionOptions());
                    foreach (var adminId in admins)
                    {
                        if (string.IsNullOrEmpty(adminId))
                            continue;
                        await notifications.EmitAsync(
                            session,
                            recipientId: adminId,
                            kind: NotificationKinds.OpsAlarm,
                            idempotencyKey: $"notification:{NotificationKinds.OpsAlarm}:{fireMinute}:{adminId}",
                            body: alert.Subject,
                            linkPath: "/admin/status",
                            ct: CancellationToken.None);
                    }
                    await session.SaveChangesAsync(CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                // Best-effort (the A14 / ADR 0077 D5 posture): the inbox leg
                // must not fail the tick or re-fire the e-mail. The e-mail
                // alert (SupportEmailAlertSink) is the existing channel;
                // this is the additive inbox channel.
                logger.LogWarning(
                    ex,
                    "O04: failed to deliver the mail-alarm inbox notification to the GlobalAdmins (Subject={Subject}); swallowed per the ADR 0077 D5 best-effort contract.",
                    alert.Subject);
            }
        }

        return new[] { new MailAlarmsTick() };
    }
}
