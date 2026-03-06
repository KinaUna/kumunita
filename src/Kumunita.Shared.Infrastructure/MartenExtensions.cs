using Kumunita.Identity;
using Kumunita.Localization;
using Kumunita.Shared.Kernel;
using Kumunita.Shared.Kernel.ValueObjects;
using Marten;

namespace Kumunita.Shared.Infrastructure;

public static class MartenExtensions
{
    public static StoreOptions ConfigureModuleSchemas(this StoreOptions opts)
    {
        // Each module will add its own configuration here as it's built
        // For example, the Announcements module will add:
        // opts.Schema.For<Announcement>().DatabaseSchemaName("announcements");

        // Wolverine schema is handled by IntegrateWithWolverine above
        // Identity schema is handled by EF Core separately

        opts.AddLocalizationSchema();
        opts.AddIdentitySchema();

        // Register all strongly typed IDs so Marten understands them in LINQ
        opts.RegisterValueType(typeof(UserId));
        opts.RegisterValueType(typeof(GroupId));
        opts.RegisterValueType(typeof(RoleId));
        opts.RegisterValueType(typeof(PermissionId));
        opts.RegisterValueType(typeof(TranslationKeyId));
        opts.RegisterValueType(typeof(TranslationId));
        opts.RegisterValueType(typeof(AnnouncementId));

        // Register LanguageCode as a value type for LINQ queries
        opts.RegisterValueType(typeof(LanguageCode));

        return opts;
    }
}