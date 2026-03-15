using Kumunita.Announcements.Domain;
using Marten;
using Marten.Schema;

namespace Kumunita.Announcements.Infrastructure.SeedData;

public class AnnouncementSettingsSeed : IInitialData
{
    public async Task Populate(IDocumentStore store, CancellationToken ct)
    {
        await using IDocumentSession session = store.LightweightSession();

        AnnouncementSettings? existing = await session
            .LoadAsync<AnnouncementSettings>(
                AnnouncementSettings.SingletonId, ct);

        if (existing is not null) return;

        session.Store(AnnouncementSettings.CreateDefaults());
        await session.SaveChangesAsync(ct);
    }
}