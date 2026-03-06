using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Announcements;

public static class AnnouncementsModule
{
    public static IServiceCollection AddAnnouncementsModule(
        this IServiceCollection services)
    {
        // Register module-specific services
        // services.AddScoped<IAnnouncementService, AnnouncementService>();

        return services;
    }

    public static StoreOptions AddLocalizationSchema(this StoreOptions opts)
    {
        // Register Marten documents owned by this module
        // opts.Schema.For<Announcement>().DatabaseSchemaName("announcements");
            

        return opts;
    }
}