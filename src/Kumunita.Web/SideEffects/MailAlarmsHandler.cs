using Kumunita.Core.Identity;
using Kumunita.Core.Notifications;
using Marten;

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
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        MailAlarmsTick tick,
        IDocumentStore store,
        IAlertSink alertSink)
    {
        await MailAlarmsService.EvaluateAsync(store, alertSink, DateTimeOffset.UtcNow);

        return new[] { new MailAlarmsTick() };
    }
}
