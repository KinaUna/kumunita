using Kumunita.Localization.Domain;
using Marten;

namespace Kumunita.Localization.Infrastructure;

public static class LocalizationMartenConfiguration
{
    public static StoreOptions AddLocalizationMartenSchema(this StoreOptions opts)
    {
        opts.Schema.For<Language>()
            .DatabaseSchemaName("localization")
            .Identity(x => x.Code); // LanguageCode is the natural identity

        opts.Schema.For<TranslationKey>()
            .DatabaseSchemaName("localization")
            .Index(x => x.Module)
            .Index(x => x.Feature);

        opts.Schema.For<Translation>()
            .DatabaseSchemaName("localization")
            .Index(x => x.TranslationKeyId)
            .Index(x => x.LanguageCode);

        opts.Schema.For<LocalizationSettings>()
            .DatabaseSchemaName("localization")
            .SingleTenanted(); // only one settings document ever exists

        return opts;
    }
}