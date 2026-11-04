using Kumunita.Core.SurfaceLabels;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>SurfaceLabels</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The surface
/// label catalog — closed-set, per-surface label strings with closed-set
/// translation keys (ADR 0021 closed-set doctrine, the known-key gate pins).
/// Core stays HTTP-free (ADR 0006-D).
/// </summary>
public static class SurfaceLabelsModule
{
    /// <summary>Registers the <c>SurfaceLabels</c> bounded context's seam.</summary>
    public static IServiceCollection AddSurfaceLabels(this IServiceCollection services)
    {
        services.AddTransient<ISurfaceLabelsService>(sp => new SurfaceLabelsService(
            sp.GetRequiredService<Marten.IDocumentStore>(),
            sp.GetRequiredService<Core.Localization.ITranslationProvider>()));
        return services;
    }
}
