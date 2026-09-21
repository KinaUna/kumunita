using Kumunita.Core.Events;
using Kumunita.Core.Identity;
using Marten;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <c>EventReminders</c> job (ARCHITECTURE.md §6.4 — the second of
/// the three §6.4 scheduled jobs, alongside <c>AuditPurge</c> (shipped M1) and
/// <c>VerifyDigest</c> (deferred); ADR 0054 §3.6).
/// <para>
/// Modeled as a <b>self-rescheduling <see cref="Wolverine.TimeoutMessage"/></b> —
/// each run re-publishes <see cref="EventReminderTick"/> for the next day. The
/// <see cref="AuditPurgeHandler"/> precedent verbatim: the sanctioned recurring-job
/// shape for this project ("a scheduled/recurring job IS a message that gets
/// published with a delay, handled by a normal message handler," and
/// "<c>TimeoutMessage</c> bakes a delay into the message type itself, so every
/// re-publish of the same message type carries the same schedule").
/// </para>
/// <para>
/// The reminder's <em>business logic</em> (the 24-hour "remind the day before"
/// window, the Going-RSVP + author-always recipient set, the
/// <c>remind:{eventId}:{userId}</c> idempotency keys, the no-<c>AccessAudit</c>
/// posture) is the Wolverine-free
/// <see cref="Kumunita.Core.Events.EventReminderService"/> in <c>Kumunita.Core</c>
/// (U07) — the harness tests prove the window/recipient/staging shape without a
/// message host; this handler is just a thin adapter that injects the live
/// <see cref="IDocumentStore"/> + the <b>frozen</b> <see cref="IMailerStage"/>
/// stager into that service, then re-schedules the next tick.
/// </para>
/// </summary>
public static class EventReminderHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 day ahead. Because the message
    /// type bakes in the delay (<see cref="Wolverine.TimeoutMessage"/>'s schedule
    /// constructor), re-yielding a fresh <see cref="EventReminderTick"/> carries the
    /// same schedule every run — no per-callsite <c>DelayedFor</c> needed.
    /// <para>
    /// Durability: <c>IntegrateWithWolverine()</c> (Program.cs) backs the
    /// scheduled message with Postgres storage, so a Coolify redeploy mid-day does
    /// not silently drop a pending run (treat the §6.4 jobs as needing durable
    /// scheduling, not an in-memory timer — the <see cref="AuditPurgeHandler"/>
    /// precedent).
    /// </para>
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        EventReminderTick tick,
        IDocumentStore store,
        IOptions<EventReminderOptions> options,
        IMailerStage mailer)
    {
        // `now` is passed explicitly to the service so the window boundary is
        // deterministic under Wolverine's test-time control; using UtcNow here
        // keeps production correct while the harness pins its own (the
        // AuditPurgeHandler shape verbatim). The mailer is the frozen IMailerStage
        // seam (the host resolves the real OutboxEmailStager in production) — the
        // U07 signature requires it (drift (f) in m4-handoff-notes.md; the design
        // doc §4 C# block omits it and is stale).
        await EventReminderService.SendRemindersAsync(
            store,
            options.Value,
            DateTimeOffset.UtcNow,
            mailer);

        // Self-reschedule: return a fresh EventReminderTick so the recurring
        // schedule carries forward (the TimeoutMessage's 1-day delay is baked into
        // the type, so this re-publish picks up the same 1-day cadence). Without
        // this line the tick would fire exactly once and the daily reminders would
        // silently stop after first boot (the AuditPurgeHandler precedent verbatim).
        //
        // `Task<IEnumerable<object>>` is the async-eligible cascade shape in
        // Wolverine; we return the array rather than `yield return` (which is
        // invalid inside an async method).
        return new[] { new EventReminderTick() };
    }
}

/// <summary>
/// The recurring message shape for the EventReminders job (see
/// <see cref="EventReminderHandler"/>). One class, one baked-in schedule (1 day),
/// re-yielded by the handler after each run.
/// </summary>
/// <remarks>
/// <c>TimeoutMessage</c> is a <em>record</em> whose base constructor takes a
/// <see cref="TimeSpan"/> schedule. The <see cref="AuditPurgeTick"/> precedent
/// verbatim: the <see cref="TimeSpan"/> is the delay before each run, and the
/// handler re-yields a fresh <see cref="EventReminderTick"/> after each run to
/// keep the schedule going (§6.4 self-rescheduling pattern).
/// </remarks>
public sealed record EventReminderTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
