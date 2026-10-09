using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The M33 storage-history doc surface (ADR 0004 §B.1 — a parallel surface
/// to <see cref="UsageDocTypes"/> / <see cref="StorageSettingsDocTypes"/>,
/// not additive on an existing one: <see cref="StorageMetricsSample"/> uses
/// the conventional string <c>Id</c>, so no non-default convention or
/// business-key index is pinned). Without the boot-path call the doc is
/// invisible to Marten (the M3/Media/Usage precedent, M33·1).
/// </summary>
public static class StorageHistoryDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<StorageMetricsSample>();
    }
}
