namespace Kumunita.Core.Usage;

/// <summary>
/// The four headline metrics (F1). <see cref="UserContentUsedBytes"/> ==
/// <see cref="TotalUsedBytes"/> **by design** (C-SM·2/4); the "orphan file"
/// caveat is a named non-decision.
/// </summary>
public sealed record StorageMetricsSnapshot(
    long TotalUsedBytes,
    long TotalVolumeBytes,
    long FreeVolumeBytes,
    long UserContentUsedBytes,
    int TotalUniqueFiles,
    int TotalDistinctUsers,
    DateTimeOffset AsOf);
