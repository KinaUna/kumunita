using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Localization
{
    public static class LocalizationModule
    {
        public static IServiceCollection AddLocalizationModule(
            this IServiceCollection services)
        {
            // Register module-specific services
            // services.AddScoped<ITranslationService, TranslationService>();

            return services;
        }

        public static StoreOptions AddLocalizationSchema(this StoreOptions opts)
        {
            // Register Marten documents owned by this module
            // opts.Schema.For<Language>().DatabaseSchemaName("localization");
            // opts.Schema.For<TranslationKey>().DatabaseSchemaName("localization");

            return opts;
        }
    }
}
