namespace Kumunita.Core.Usage;

/// <summary>
/// The M33 storage-sample document (ADR 0156, M33·2) — the M24
/// <see cref="StorageMetricsSnapshot"/> frozen at a UTC day boundary. One
/// sample per UTC day; the <see cref="Id"/> is **deterministic**
/// (<c>"smh-" + yyyy-MM-dd</c> from <see cref="SampleDate"/>'s UTC date),
/// so a same-day re-capture **overwrites** the row
/// (idempotent-by-construction — the M13 "no-double-send guard" / the M32·8
/// idempotency precedent; **no** dedup query needed, M33·2).
/// <para>
/// The 8-member field set below is the **M33·2 ceiling** — no field outside
/// the set may appear in the doc (drift-guard §2.6). The
/// <see cref="StorageMetricsSnapshot.AsOf"/> member is **not** carried
/// (replaced by <see cref="SampleDate"/> — the snapshot's capture instant is
/// the day).
/// </para>
/// <para>
/// This doc is **not** an <c>IAuditableResource</c> — it is platform
/// telemetry (the M13 C-M13·6 "the <c>UsageEvent</c> row is not an auditable
/// resource" + the M24 C-SM·6 "read = no row" precedent); a capture emits
/// **zero** <c>AccessAudit</c> rows (M33·4). The sample **never leaves the
/// instance** (M33·6).
/// </para>
/// </summary>
public sealed class StorageMetricsSample
{
    /// <summary>The deterministic <c>"smh-" + yyyy-MM-dd</c> id (M33·2).</summary>
    public string Id { get; set; } = null!;

    /// <summary>The UTC day captured (M33·2 — the snapshot's capture instant).</summary>
    public DateTimeOffset SampleDate { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.TotalUsedBytes"/>.</summary>
    public long TotalUsedBytes { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.TotalVolumeBytes"/>.</summary>
    public long TotalVolumeBytes { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.FreeVolumeBytes"/>.</summary>
    public long FreeVolumeBytes { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.UserContentUsedBytes"/>
    /// (<c>== TotalUsedBytes</c> by M24 design).</summary>
    public long UserContentUsedBytes { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.TotalUniqueFiles"/>.</summary>
    public int TotalUniqueFiles { get; set; }

    /// <summary>Mirrors <see cref="StorageMetricsSnapshot.TotalDistinctUsers"/>.</summary>
    public int TotalDistinctUsers { get; set; }
}
