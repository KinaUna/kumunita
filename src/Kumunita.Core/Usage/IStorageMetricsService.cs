namespace Kumunita.Core.Usage;

/// <summary>
/// The M24 storage-metrics read seam (C-SM·2/3/4). Read-only, zero writes
/// (C-SM·2): every method opens at most one <c>QuerySession</c> and calls the
/// two <c>IMediaFileStore</c> volume-stat reads; it emits zero
/// <c>AccessAudit</c> rows and zero new documents. Returns plain DTOs (C-SM·3).
/// </summary>
public interface IStorageMetricsService
{
    /// <summary>
    /// The four headline metrics (total used, available, user-content used) +
    /// the per-user aggregate counts (C-SM·2/4). **One** <c>QuerySession</c> +
    /// **two** <c>MediaObject</c> catalog queries + **two** volume reads
    /// (<c>GetTotalSpaceBytesAsync</c> / <c>GetFreeSpaceBytesAsync</c>), the
    /// latter two **not** in the session (C-SM·4).
    /// </summary>
    Task<StorageMetricsSnapshot> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// The **paged** per-user table (the M24 title's "space used per user",
    /// F3/F4). Default sort **descending by bytes used** (C-SM·4); the M7
    /// <c>HasMore</c> discipline (C-SM·4/7). <paramref name="pageSize"/>
    /// defaults to 25.
    /// </summary>
    Task<PerUserStoragePage> GetPerUserListAsync(int page, int pageSize = 25,
        CancellationToken ct = default);

    /// <summary>
    /// The **C-SM·7 handoff seam** M25 reuses (or re-points to):
    /// <c>Σ SizeBytes WHERE CreatedById == subjectId</c>. This is the exact
    /// shape M25's U4 (<c>IStorageSettingsService.GetPerUserUsageBytesAsync</c>)
    /// delegates to or duplicates — the drift-guard (design doc §2.7) names it.
    /// </summary>
    Task<long> GetPerUserUsageBytesAsync(string subjectId,
        CancellationToken ct = default);
}
