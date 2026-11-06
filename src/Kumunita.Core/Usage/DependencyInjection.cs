using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Usage</c> bounded context's DI registration,
/// extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The operator
/// plane's read/write seams (C-M13·6 / C-SM·2/3/4 / C-UP·1/3/4 — zero new
 /// authorization surface, C-SM·6): the D3 aggregation read
/// (<see cref="IUsageAnalyticsService"/>), the storage-metrics read
/// (<see cref="IStorageMetricsService"/>), and the storage-settings admin write
/// lane + pure decision (<see cref="IStorageSettingsService"/>). Each composes
/// the host-registered Marten <c>IDocumentStore</c> + the frozen seams it
/// reuses (the C-SM·7 per-user-usage read seam the settings service reuses —
/// the design §2.6 drift-guard: not a second copy of the query). Core stays
 /// HTTP-free (C-UP·3 / ADR 0006-D): the Web gate (U8) is the only place a 413
 /// is produced.
/// </summary>
public static class UsageModule
{
    /// <summary>Registers the <c>Usage</c> bounded context's operator-plane read/write seams.</summary>
    public static IServiceCollection AddUsage(this IServiceCollection services)
    {
        // M13 (ADR 0114, plan U04): the D3 aggregation seam — it touches only
        // UsageEvent; the AspNetUsers table is never read (the "NewSignups
        // dropped" deferral). Zero new authorization surface (C-M13·6).
        services.AddTransient<IUsageAnalyticsService>(sp => new UsageAnalyticsService(
            sp.GetRequiredService<Marten.IDocumentStore>()));

        // M24 (ADR 0134, plan U4): the storage-metrics read seam (C-SM·2/3/4).
        // It composes the host-registered Marten IDocumentStore (the
        // MediaObject catalog read) + the IMediaFileStore volume seam (the two
        // volume-stat reads, U3). Read-only, zero writes (C-SM·2); no new
        // authorization surface (C-SM·6).
        services.AddTransient<IStorageMetricsService>(sp => new StorageMetricsService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IMediaFileStore>()));

        // M25 (design §2.1, U4): the storage-settings seam — the admin write
        // lane + pure decision (C-UP·1/3/4). Composes the host-registered
        // Marten IDocumentStore (the CommunityStorageSettings read / single
        // admin write) + the M24 IStorageMetricsService (the C-SM·7 seam
        // reuse). Core stays HTTP-free (C-UP·3 / ADR 0006-D).
        services.AddTransient<IStorageSettingsService>(sp => new StorageSettingsService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<IStorageMetricsService>()));   // C-SM·7 seam reuse

        return services;
    }
}
