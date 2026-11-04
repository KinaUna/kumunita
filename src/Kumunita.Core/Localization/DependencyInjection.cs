using Kumunita.Core.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Core;

/// <summary>
/// C10 (IMPROVE-CODE) — the <c>Localization</c> bounded context's DI
/// registration, extracted from the Core composition root along the <b>module
/// self-registration</b> seam (FACES: <i>Adaptive</i>). Composed by
/// <see cref="ServiceCollectionExtensions.AddKumunitaCore"/>. The two Core seams
/// the multilingual lane (ML, ADR 0005) shipped — the per-request read path
/// (ITranslationProvider, U2) and the admin management seam
/// (ILocalizationService, U3). Core stays HTTP-free (M·8): neither touches
/// an HttpRequest or a cookie. Languages are data, not config (M·4).
/// </summary>
public static class LocalizationModule
{
    /// <summary>Registers the <c>Localization</c> bounded context's seams.</summary>
    public static IServiceCollection AddLocalization(this IServiceCollection services)
    {
        services.AddTransient<ITranslationProvider, TranslationProvider>();
        services.AddTransient<ILocalizationService, LocalizationService>();

        return services;
    }
}
