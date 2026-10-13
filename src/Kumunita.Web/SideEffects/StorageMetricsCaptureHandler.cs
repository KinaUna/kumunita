using Kumunita.Core.Usage;
using Marten;

namespace Kumunita.Web.SideEffects;

/// <summary>
/// The recurring <see cref="StorageMetricsCaptureTick"/> job (ADR 0156,
/// M33·3 — the <see cref="UsagePurgeHandler"/> thin-adapter shape verbatim).
/// Each run re-publishes <see cref="StorageMetricsCaptureTick"/> for the
/// next day (the <see cref="Wolverine.TimeoutMessage"/>'s 1-day delay is
/// baked into the type, so this re-publish picks up the same cadence). The
/// business logic is the Wolverine-free
/// <see cref="StorageMetricsCaptureService"/> in <c>Kumunita.Core</c> — this
/// handler is a thin adapter that injects a live <see cref="IDocumentStore"/>
/// + the live <see cref="IStorageMetricsService"/> into that service, then
/// re-schedules the next tick. Postgres-backed durability (the
/// <see cref="UsagePurgeHandler"/> precedent).
/// </summary>
public static class StorageMetricsCaptureHandler
{
    /// <summary>
    /// Durable recurring tick: self-schedules 1 day ahead.
    /// <c>Task&lt;IEnumerable&lt;object&gt;&gt;</c> is the async-eligible
    /// cascade shape in Wolverine (an <c>IEnumerable&lt;object&gt;</c>
    /// iterator can't <c>await</c>); we return the array rather than
    /// <c>yield return</c> (which is invalid inside an async method) — the
    /// <see cref="UsagePurgeHandler"/> note.
    /// </summary>
    public static async Task<IEnumerable<object>> Handle(
        StorageMetricsCaptureTick tick,
        IDocumentStore store,
        IStorageMetricsService metrics)
    {
        await StorageMetricsCaptureService.CaptureAndPurgeAsync(
            store, metrics, DateTimeOffset.UtcNow);

        return new[] { new StorageMetricsCaptureTick() };
    }
}
