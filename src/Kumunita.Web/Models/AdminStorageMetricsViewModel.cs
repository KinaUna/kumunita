using Kumunita.Core.Usage;

namespace Kumunita.Web.Models;

/// <summary>
/// The view model for <c>/admin/storage</c> (M24, the storage-metrics
/// surface). The M13 <see cref="AnalyticsViewModel"/> precedent, mirrored:
/// a plain projection of the <see cref="StorageMetricsSnapshot"/> headline
/// metrics (C-SM·2) + the <see cref="PerUserStoragePage"/> paged per-user
/// table (C-SM·4/5). The controller does the projection (this POCO carries
/// nothing the two Core DTOs do not already carry — C-SM·3: no
/// <c>IFormFile</c> / <c>ActionResult</c> / <c>HttpContext</c>, Web-only
/// shape). Read-only: the surface emits zero <c>AccessAudit</c> rows
/// (C-SM·6 — M24 has no export lane in v1).
/// </summary>
public class AdminStorageMetricsViewModel
{
    // The four headline metrics (F1) — the <see cref="StorageMetricsSnapshot"/>
    // projection. <see cref="AvailableBytes"/> is the volume's free space
    // (<c>FreeVolumeBytes</c>, the "available space" the milestone title names).
    public long           TotalUsedBytes     { get; init; }
    public long           AvailableBytes     { get; init; }
    public long           UserContentUsedBytes { get; init; }
    public int            TotalUniqueFiles   { get; init; }
    public int            TotalDistinctUsers { get; init; }
    public DateTimeOffset AsOf               { get; init; }

    /// <summary>
    /// The operator's platform storage limit in bytes (env
    /// <c>Media__MaxPlatformBytes</c>), or <c>0</c> = unlimited. When non-zero,
    /// <see cref="AvailableBytes"/> is capped to <c>min(physical free,
    /// limit − used)</c>; the view uses this to label the "available" card as a
    /// platform-budget figure rather than raw physical free space.
    /// </summary>
    public long           PlatformLimitBytes { get; init; }

    // The paged per-user table (F3) — the <see cref="PerUserStoragePage"/>
    // projection (the M7 <c>HasMore</c> discipline, C-SM·4). <see cref="Items"/>
    // is sorted descending by bytes used (C-SM·4 / F4); a row with
    // <see cref="PerUserStorageRow.CreatedById"/> of <c>null</c> / <c>""</c>
    // is the single "unknown / not captured" bucket (C-SM·5).
    public IReadOnlyList<PerUserStorageRow> Items   { get; init; } =
        System.Array.Empty<PerUserStorageRow>();
    public int                              TotalUsers { get; init; }
    public int                              Page       { get; init; }
    public bool                             HasMore    { get; init; }
}
