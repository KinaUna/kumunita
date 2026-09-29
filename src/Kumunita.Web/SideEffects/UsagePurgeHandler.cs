using Kumunita.Core.Usage;
using Marten;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <see cref="UsagePurgeTick"/> job (ADR 0114 D5, the
/// <see cref="AuditPurgeHandler"/> shape verbatim). Each run re-publishes
/// <see cref="UsagePurgeTick"/> for the next day (the
/// <see cref="Wolverine.TimeoutMessage"/>'s 1-day delay is baked into the type,
/// so this re-publish picks up the same cadence). The business logic is the
/// Wolverine-free <see cref="UsagePurgeService"/> in <c>Kumunita.Core</c> —
/// this handler is a thin adapter that injects a live
/// <see cref="IDocumentStore"/> into that service, then re-schedules the next
/// tick. Postgres-backed durability: a Coolify redeploy mid-day does not
/// silently drop a pending run (the <c>AuditPurgeHandler</c> precedent).
/// </summary>
public static class UsagePurgeHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 day ahead. Because the message
    /// type bakes in the delay (<see cref="Wolverine.TimeoutMessage"/>'s
    /// schedule constructor), re-yielding a fresh <see cref="UsagePurgeTick"/>
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
        UsagePurgeTick tick,
        IDocumentStore store)
    {
        await UsagePurgeService.PurgeAsync(store, DateTimeOffset.UtcNow);

        return new[] { new UsagePurgeTick() };
    }
}
