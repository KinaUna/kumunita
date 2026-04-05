using Kumunita.Announcements.Domain;
using Marten;

namespace Kumunita.Announcements.Infrastructure;

public static class AnnouncementsMartenConfiguration
{
    public static StoreOptions AddAnnouncementsMartenSchema(this StoreOptions opts)
    {
        opts.Schema.For<Announcement>()
            .DatabaseSchemaName("announcements")
            .Index(x => x.Status)
            .Index(x => x.OwnerId)
            .Index(x => x.PublishedAt)
            .Index(x => x.ExpiresAt)
            .SoftDeleted(); // retracted announcements soft-deleted

        opts.Schema.For<AnnouncementSettings>()
            .DatabaseSchemaName("announcements");

        return opts;
    }
}