namespace Kumunita.Core.Usage;

/// <summary>
/// The M33 history read result (the M13 <c>UsageAnalyticsResult</c> wrapper
/// precedent). <see cref="WindowDays"/> echoes the pinned window the caller
/// passed (so the view can label the section without re-deriving it);
/// <see cref="Points"/> is the sample rows **ascending** by
/// <c>SampleDate</c> — **only the days present** (no fabricated zero rows,
/// the M33-9 FACE).
/// </summary>
public sealed record StorageHistoryResult(int WindowDays,
    IReadOnlyList<StorageMetricsSample> Points);
