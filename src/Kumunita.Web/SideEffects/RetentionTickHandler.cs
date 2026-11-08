using Kumunita.Core.Privacy;
using Marten;
using Microsoft.Extensions.Options;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <see cref="RetentionTick"/> job (IMPROVE-SEC D6 / E5 — the
/// Web-side leg of the retention sweep IS33/IS34a made pure + I/O in Core).
/// <para>
/// The <b>work</b> lives in the Wolverine-free, HTTP-free
/// <see cref="RetentionService"/> in <c>Kumunita.Core.Privacy</c> (IS34a,
/// frozen) — this handler is a thin adapter that injects a live
/// <see cref="IDocumentStore"/> + the config-bound
/// <see cref="RetentionOptions"/> (IS33) into that static service, and
/// re-yields a fresh <see cref="RetentionTick"/> so the recurring schedule
/// carries forward. The tick's 1-day delay is baked into the
/// <see cref="Wolverine.TimeoutMessage"/> base type, so every re-publish
/// carries the same cadence — no per-callsite <c>DelayedFor</c> needed
/// (the <c>MediaLifecycleHandler</c> / <c>AuditPurgeHandler</c> shape
/// verbatim). Postgres-backed durability: a Coolify redeploy mid-day does
/// not silently drop a pending run (the <c>AuditPurgeHandler</c> precedent).
/// </para>
/// <para>
/// There is **no DI registration** of <see cref="RetentionService"/> — it is
/// a <c>static</c> class (the <c>UsagePurgeService</c> /
/// <c>MediaLifecycleService</c> shape), and <see cref="RetentionOptions"/>
/// is bound in <c>Program.cs</c> (IS34b) the way
/// <c>AuditPurgeOptions</c> / <c>MediaOptions</c> are.
/// </para>
/// </summary>
public static class RetentionTickHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 day ahead. Because the message
    /// type bakes in the delay (<see cref="Wolverine.TimeoutMessage"/>'s
    /// schedule constructor), re-yielding a fresh <see cref="RetentionTick"/>
    /// carries the same schedule every run — no per-callsite
    /// <c>DelayedFor</c> needed.
    /// <para>
    /// <c>Task&lt;IEnumerable&lt;object&gt;&gt;</c> is the async-eligible
    /// cascade shape in Wolverine (an <c>IEnumerable&lt;object&gt;</c>
    /// iterator can't <c>await</c>); we return the array rather than
    /// <c>yield return</c> (which is invalid inside an async method).
    /// </para>
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        RetentionTick tick,
        IDocumentStore store,
        IOptions<RetentionOptions> options)
    {
        // `now` is passed explicitly to the service (UtcNow here keeps
        // production correct; the harness pins its own — the
        // AuditPurgeHandler precedent). RetentionService logs counts (no PII);
        // nothing logged here.
        await RetentionService.RunAsync(
            store, options.Value, DateTimeOffset.UtcNow);

        // Self-reschedule: return a fresh RetentionTick so the recurring
        // schedule carries forward (the TimeoutMessage's 1-day delay is baked
        // into the type, so this re-publish picks up the same 1-day cadence —
        // the pattern from scheduled-and-delayed-messages.md's Recurring-jobs
        // section). Without this line the tick would fire exactly once and the
        // daily sweep would silently stop after first boot.
        return new[] { new RetentionTick() };
    }
}
