using Kumunita.Announcements.Infrastructure;
using Kumunita.Announcements.Infrastructure.SeedData;
using Marten;
using Marten.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace Kumunita.Announcements;

public static class AnnouncementsModule
{
    public static IServiceCollection AddAnnouncementsModule(
        this IServiceCollection services)
    {
        services.AddSingleton<IInitialData, AnnouncementSettingsSeed>();
        return services;
    }

    public static StoreOptions AddAnnouncementsSchema(this StoreOptions opts)
        => opts.AddAnnouncementsMartenSchema();
}