using Kumunita.Core.Media;
using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The <see cref="IStorageMetricsService"/> impl (U4). Read-only, zero writes
/// (C-SM·2): every method opens at most one <c>QuerySession</c> and reads the
/// <see cref="MediaObject"/> catalog; <see cref="GetSnapshotAsync"/> also calls
/// the two <c>IMediaFileStore</c> volume-stat reads (a filesystem stat, not a
/// Postgres query — so it is **not** issued through the session, C-SM·4 / F7).
/// It emits zero <c>AccessAudit</c> rows and zero new documents (C-SM·2 / C-SM·6).
/// <para>
/// <b>Marten parser fallback (recorded per the <c>UsageAnalyticsService</c>
/// precedent in this context):</b> the per-user <c>GroupBy</c>/<c>Sum</c>
/// shapes whose SQL translation is non-trivial for the provider are evaluated
/// <b>Linq-to-objects over the <c>MediaObject</c> row set</b> (one
/// server-side <c>ToListAsync</c>, then client-side group/sum). The
/// deterministic results are identical to the design doc §2.3 shapes, and the
/// pins assert the <em>results</em>, not the SQL emitted — so the fallback is
/// invisible to the contract. The two volume reads are always plain BCL calls
/// (<see cref="IMediaFileStore"/>), never a session query (F7).
/// </para>
/// </summary>
public sealed class StorageMetricsService : IStorageMetricsService
{
    private readonly IDocumentStore _store;
    private readonly IMediaFileStore _volume;

    public StorageMetricsService(IDocumentStore store, IMediaFileStore volume)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
    }

    /// <inheritdoc/>
    public async Task<StorageMetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        long totalUsed;
        int totalFiles;
        int distinctKnownUsers;
        long unknownBytes;

        // One QuerySession (C-SM·4): the MediaObject catalog read. The community
        // total and the per-user map are derived from the same row set (the
        // documented Linq-to-objects fallback over a ToListAsync row set). No
        // writes (C-SM·2).
        await using (var session = _store.QuerySession())
        {
            var all = await session.Query<MediaObject>().ToListAsync(ct);

            // Community total (C-SM·2/4): Σ SizeBytes over the whole catalog.
            // SizeBytes == 0 rows contribute 0, so this equals the >0-byte sum.
            totalUsed = all.Sum(o => o.SizeBytes);
            totalFiles = all.Count;

            // Per-user map (C-SM·4/5): distinct non-empty creators of >0-byte
            // rows, + the single "unknown" bucket (CreatedById null / "") when
            // it carries bytes (C-SM·5).
            var positive = all.Where(o => o.SizeBytes > 0);
            distinctKnownUsers = positive
                .Where(o => o.CreatedById != null && o.CreatedById != "")
                .Select(o => o.CreatedById)
                .Distinct()
                .Count();
            unknownBytes = positive
                .Where(o => o.CreatedById == null || o.CreatedById == "")
                .Sum(o => o.SizeBytes);
        }

        // The two volume-stat reads (C-SM·4 / F7): a filesystem stat of the
        // configured RootPath's partition — a BCL DriveInfo/statvfs call, NOT a
        // Postgres query, so it is NOT issued through the QuerySession.
        var totalVolume = await _volume.GetTotalSpaceBytesAsync(ct);
        var freeVolume = await _volume.GetFreeSpaceBytesAsync(ct);

        return new StorageMetricsSnapshot(
            TotalUsedBytes: totalUsed,
            TotalVolumeBytes: totalVolume,
            FreeVolumeBytes: freeVolume,
            UserContentUsedBytes: totalUsed,   // == TotalUsedBytes by design (C-SM·2/4)
            TotalUniqueFiles: totalFiles,
            TotalDistinctUsers: distinctKnownUsers + (unknownBytes > 0 ? 1 : 0),
            AsOf: DateTimeOffset.UtcNow);
    }

    /// <inheritdoc/>
    public async Task<PerUserStoragePage> GetPerUserListAsync(int page, int pageSize = 25,
        CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();

        // C-SM·5: SizeBytes == 0 rows are excluded (the Where clause) — a
        // catalog row with no payload contributes nothing to the per-user table.
        var all = await session.Query<MediaObject>()
            .Where(o => o.SizeBytes > 0)
            .ToListAsync(ct);

        // The "unknown / not captured" bucket (C-SM·5 / F5): CreatedById of
        // null / "" lands in a single row, never one row per null.
        var unknownRows = all.Where(o => o.CreatedById == null || o.CreatedById == "");
        var unknownBytes = unknownRows.Sum(o => o.SizeBytes);
        var unknownFiles = unknownRows.Count();

        var distinct = all
            .Where(o => o.CreatedById != null && o.CreatedById != "")
            .GroupBy(o => o.CreatedById)
            .Select(g => new PerUserStorageRow(g.Key, g.Sum(x => x.SizeBytes), g.Count()))
            .ToList();

        if (unknownBytes > 0)
            distinct.Add(new PerUserStorageRow(null, unknownBytes, unknownFiles)); // the "unknown" bucket

        // Default sort (C-SM·4 / F4): descending by bytes used.
        distinct = distinct.OrderByDescending(r => r.Bytes).ToList();

        // The M7 HasMore discipline (C-SM·4/7; F3): a paged table.
        var pageItems = distinct.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PerUserStoragePage(pageItems, distinct.Count, page,
            distinct.Count > page * pageSize);
    }

    /// <inheritdoc/>
    public async Task<long> GetPerUserUsageBytesAsync(string subjectId, CancellationToken ct = default)
    {
        // C-SM·7 handoff seam (F10): Σ SizeBytes WHERE CreatedById == subjectId.
        // This is the exact shape M25's U4
        // (IStorageSettingsService.GetPerUserUsageBytesAsync) delegates to or
        // duplicates — the drift-guard (design doc §2.7) names it.
        await using var session = _store.QuerySession();
        var rows = await session.Query<MediaObject>()
            .Where(o => o.CreatedById == subjectId)
            .ToListAsync(ct);
        return rows.Sum(o => o.SizeBytes);
    }

    /// <inheritdoc/>
    public async Task<AvailablePlatformSpace> GetPlatformSpaceAsync(long platformLimitBytes,
        CancellationToken ct = default)
    {
        // Two independent block triggers (the platform-limit lane):
        //  (a) the platform limit (Media__MaxPlatformBytes, 0 = unlimited) is
        //      reached — used space at or above the budget. This needs the
        //      Σ SizeBytes catalog total (the same read GetSnapshotAsync does).
        //  (b) the volume's physical free space is below the 100 MiB floor —
        //      the operator safety margin that stops the volume hitting its edge.
        //      This needs only the free-space volume read.
        // When the limit is unlimited (0) trigger (a) is off, so the catalog
        // read is skipped entirely — only the cheap free-space stat runs.
        var freeVolume = await _volume.GetFreeSpaceBytesAsync(ct);
        var belowFloor = freeVolume < MediaOptions.MinFreeSpaceFloor;

        if (platformLimitBytes <= 0)
            return new AvailablePlatformSpace(0, freeVolume, 0, belowFloor);

        long totalUsed;
        await using (var session = _store.QuerySession())
        {
            var all = await session.Query<MediaObject>().ToListAsync(ct);
            totalUsed = all.Sum(o => o.SizeBytes);
        }
        var limitReached = totalUsed >= platformLimitBytes;

        return new AvailablePlatformSpace(
            UsedBytes: totalUsed,
            FreeVolumeBytes: freeVolume,
            LimitBytes: platformLimitBytes,
            IsFull: limitReached || belowFloor);
    }

    /// <inheritdoc/>
    public async Task<StorageHistoryResult> GetHistoryAsync(int days,
        CancellationToken ct = default)
    {
        // The M33·8 pinned-window guard (the M13 windowDays precedent — an
        // unknown value throws, not a 0-row query).
        if (days is not (30 or 90 or 180 or 365))
            throw new ArgumentOutOfRangeException(nameof(days),
                days, $"Window must be one of 30, 90, 180, 365 days (M33·8).");

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        // One QuerySession (M33·4 — read-only, zero writes, zero AccessAudit
        // rows): the StorageMetricsSample rows in the trailing window, ascending.
        // The Linq-to-objects fallback over a ToListAsync row set (the
        // GetSnapshotAsync precedent) — one server-side ToListAsync, then
        // client-side OrderBy; the deterministic result is identical to a
        // server-side ORDER BY.
        await using var session = _store.QuerySession();
        var points = (await session.Query<StorageMetricsSample>()
                                    .Where(s => s.SampleDate >= cutoff)
                                    .ToListAsync(ct))
                     .OrderBy(s => s.SampleDate)
                     .ToList();
        return new StorageHistoryResult(days, points);
    }
}
