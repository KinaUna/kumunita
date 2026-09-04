using Kumunita.Core.Authorization;
using Marten;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <c>AuditPurge</c> job (ARCHITECTURE.md §6.4 — one of the three
/// §6.4 scheduled jobs, alongside <c>EventReminders</c> and <c>VerifyDigest</c>).
/// <para>
/// Modeled as a <b>self-rescheduling <see cref="Wolverine.TimeoutMessage"/></b> —
/// each run re-publishes <see cref="AuditPurgeTick"/> for the next day. Per the
/// CQRS-lite conventions (<c>docs/wolverine/scheduled-and-delayed-messages.md</c>)
/// this is the sanctioned recurring-job shape for this project: "Wolverine doesn't
/// have a separate job-scheduler API — a scheduled/recurring job IS a message that
/// gets published with a delay, handled by a normal message handler," and
/// "<c>TimeoutMessage</c> bakes a delay into the message type itself, so every
/// re-publish of the same message type carries the same schedule."
/// </para>
/// <para>
/// The purge's <em>business logic</em> (tiering on <see cref="AccessVia"/>, the
/// <see cref="AuditPurgeSummary"/> audit-of-audit) is the Wolverine-free
/// <see cref="AuditPurgeService"/> in <c>Kumunita.Core</c> — the harness tests
/// prove the tiering and the summary shape without a message host; this handler
/// is just a thin adapter that injects a live <see cref="IDocumentSession"/> into
/// that service, then re-schedules the next tick.
/// </para>
/// </summary>
public static class AuditPurgeHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 day ahead. Because the message
    /// type bakes in the delay (<see cref="Wolverine.TimeoutMessage"/>'s schedule
    /// constructor), re-yielding a fresh <see cref="AuditPurgeTick"/> carries the
    /// same schedule every run — no per-callsite <c>DelayedFor</c> needed.
    /// <para>
    /// Durability: <c>IntegrateWithWolverine()</c> (Program.cs) backs the
    /// scheduled message with Postgres storage, so a Coolify redeploy mid-day does
    /// not silently drop a pending run (the reference doc's note that "a scheduled
    /// message only survives a process restart if it's durable" — treat the §6.4
    /// jobs as needing durable scheduling, not an in-memory timer).
    /// </para>
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        AuditPurgeTick tick,
        IDocumentStore store,
        IOptions<AuditPurgeOptions> options)
    {
        // `now` is passed explicitly to the service so the ~90-day tier boundary is
        // deterministic under Wolverine's test-time control; using UtcNow here keeps
        // production correct while the harness pins its own.
        await AuditPurgeService.PurgeAsync(
            store,
            options.Value,
            DateTimeOffset.UtcNow);

        // Self-reschedule: return a fresh AuditPurgeTick so the recurring schedule
        // carries forward (the TimeoutMessage's 1-day delay is baked into the type,
        // so this re-publish picks up the same 1-day cadence — the pattern from
        // scheduled-and-delayed-messages.md's Recurring-jobs section). Without this
        // line the tick would fire exactly once and the daily purge would silently
        // stop after first boot.
        //
        // `Task<IEnumerable<object>>` is the async-eligible cascade shape in
        // Wolverine (an `IEnumerable<object>` iterator can't `await`); we return
        // the array rather than `yield return` (which is invalid inside an async
        // method).
        return new[] { new AuditPurgeTick() };
    }
}

/// <summary>
/// The recurring message shape for the purge job (see <see cref="AuditPurgeHandler"/>).
/// One class, one baked-in schedule (1 day), re-yielded by the handler after each run.
/// </summary>
/// <remarks>
/// <c>TimeoutMessage</c> is a <em>record</em> whose base constructor takes a
/// <see cref="TimeSpan"/> schedule (in the <c>Wolverine</c> namespace, the
/// <c>WolverineFx</c> package's assembly). The reference template
/// (<c>docs/wolverine/scheduled-and-delayed-messages.md</c>) shows exactly this
/// shape: <c>record AuditPurgeTick() : TimeoutMessage(1.Days())</c> — the
/// <see cref="TimeSpan"/> is the delay before each run, and the handler
/// re-yields a fresh <see cref="AuditPurgeTick"/> after each run to keep the
/// schedule going (§6.4 self-rescheduling pattern).
/// </remarks>
public sealed record AuditPurgeTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1));
