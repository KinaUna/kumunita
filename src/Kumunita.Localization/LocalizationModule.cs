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
        // services.AddScoped<ITranslationService, TranslationService>();
        services.AddScoped<TranslationResolver>();
        services.AddSingleton<IInitialData, DefaultLanguagesSeed>();

        return services;
    }

    public static StoreOptions AddLocalizationSchema(this StoreOptions opts)
    {
        // Register Marten documents owned by this module
        // opts.Schema.For<Language>().DatabaseSchemaName("localization");
        // opts.Schema.For<TranslationKey>().DatabaseSchemaName("localization");

        return opts.AddLocalizationMartenSchema(); // calls LocalizationMartenConfiguration
    }
}