using Kumunita.Localization.Features.Resolution;
using Kumunita.Localization.Infrastructure;
using Kumunita.Localization.Infrastructure.SeedData;
using Marten;
using Marten.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Localization;

public static class LocalizationModule
{
    public static IServiceCollection AddLocalizationModule(
        this IServiceCollection services)
    {
        // Register module-specific services
        services.AddScoped<TranslationResolver>();
        services.AddSingleton<IInitialData, DefaultLanguagesSeed>();

        return services;
    }

    public static StoreOptions AddLocalizationSchema(this StoreOptions opts)
    {
        // Register Marten documents owned by this module
        
        return opts.AddLocalizationMartenSchema(); // calls LocalizationMartenConfiguration
    }
}