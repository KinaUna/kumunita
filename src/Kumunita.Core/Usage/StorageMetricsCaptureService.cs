using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The M33 storage-sample capture + retention (ADR 0156, M33·2 / M33·5 / M33·7).
/// A <b>Wolverine-free static class</b> (the <see cref="UsagePurgeService"/> house
/// shape): the handler injects a live <see cref="IDocumentStore"/> + the live
/// <see cref="IStorageMetricsService"/> and calls this; the business logic has
/// **no** Wolverine reference, **no** <c>HttpClient</c> (ADR 0006-D), and
/// **zero** <c>AccessAudit</c> rows (M33·4). One sample per UTC day (the
/// deterministic <c>Id</c> overwrites a same-day row — idempotent-by-construction,
/// M33·2); the purge rides the **same** run (the M13 "one durable job" shape,
/// M33·7).
/// </summary>
public static class StorageMetricsCaptureService
{
    /// <summary>The M33 retention: 365 days (the
    /// <see cref="UsagePurgeService.RetentionDays"/> platform-constant precedent,
    /// M33·7 — a per-instance knob is a **named deferral**).</summary>
    public const int RetentionDays = 365;

    /// <summary>
    /// (1) computes the M24 snapshot via <c>metrics.GetSnapshotAsync(ct)</c>
    /// (the **M24 frozen seam reuse**, the M33·1 pin — the capture reuses the
    /// exact same read the surface renders); (2) stores **one**
    /// <see cref="StorageMetricsSample"/> for <c>now</c>'s UTC day (the
    /// deterministic <c>Id</c> <c>"smh-" + yyyy-MM-dd</c> overwrites a same-day
    /// row — M33·2); (3) **purges** <see cref="StorageMetricsSample"/> rows with
    /// <c>SampleDate &lt; now − RetentionDays</c> (batched id-collection + delete
    /// in **one** session, no per-row <c>SaveChangesAsync</c>, the
    /// <see cref="UsagePurgeService.PurgeAsync"/> house shape — M33·7); and
    /// (4) returns the purge count. **Zero** <c>AccessAudit</c> rows (M33·4).
    /// </summary>
    public static async Task<int> CaptureAndPurgeAsync(
        IDocumentStore store,
        IStorageMetricsService metrics,
        DateTimeOffset now,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metrics);

        // (1) The M24 frozen seam reuse (M33·1) — the exact same read the
        // surface renders.
        var snap = await metrics.GetSnapshotAsync(ct);

        // (2) Build the sample for now's UTC day (the deterministic Id, the 7
        // data members mirroring StorageMetricsSnapshot, SampleDate replacing
        // AsOf — the M33·2 8-member ceiling).
        var day = now.ToUniversalTime().Date;
        var sample = new StorageMetricsSample
        {
            Id = "smh-" + day.ToString("yyyy-MM-dd"),
            SampleDate = now,
            TotalUsedBytes = snap.TotalUsedBytes,
            TotalVolumeBytes = snap.TotalVolumeBytes,
            FreeVolumeBytes = snap.FreeVolumeBytes,
            UserContentUsedBytes = snap.UserContentUsedBytes,
            TotalUniqueFiles = snap.TotalUniqueFiles,
            TotalDistinctUsers = snap.TotalDistinctUsers,
        };

        // (3) One write session: store the sample (the deterministic Id
        // overwrites a same-day row — the M33·2 idempotent-by-construction pin)
        // + purge the expired (the UsagePurgeService batched shape — one
        // SaveChangesAsync for the whole batch, M33·7).
        var cutoff = now.AddDays(-RetentionDays);
        await using var session = store.OpenSession(new Marten.Services.SessionOptions());
        {
            session.Store(sample);
            var expiredIds = (await session.Query<StorageMetricsSample>()
                                       .Where(s => s.SampleDate < cutoff)
                                       .Select(s => s.Id)
                                       .ToListAsync(ct))
                           .Distinct().ToList();
            foreach (var id in expiredIds) session.Delete<StorageMetricsSample>(id);
            await session.SaveChangesAsync(ct);
            return expiredIds.Count;
        }
    }
}
