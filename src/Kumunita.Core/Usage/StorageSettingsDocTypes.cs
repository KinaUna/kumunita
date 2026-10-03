using Marten;

namespace Kumunita.Core.Usage;

/// <summary>
/// The M25 settings doc surface (ADR 0004 §B.1 — a parallel surface to
/// <see cref="UsageDocTypes"/> / <c>MediaDocTypes</c>, not additive on an
/// existing one: <see cref="CommunityStorageSettings"/> uses the conventional
/// string <c>Id</c>, so no non-default convention or business-key index is
/// pinned). Without the boot-path call the doc is invisible to Marten (the
/// M3/Media/Usage precedent).
/// </summary>
public static class StorageSettingsDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<CommunityStorageSettings>();
    }
}
