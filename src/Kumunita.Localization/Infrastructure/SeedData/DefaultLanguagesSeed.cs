using Kumunita.Localization.Domain;
using Marten;
using Marten.Schema;

namespace Kumunita.Localization.Infrastructure.SeedData;

public class DefaultLanguagesSeed : IInitialData
{
    public async Task Populate(IDocumentStore store, CancellationToken ct)
    {
        await using var session = store.LightweightSession();

        var existing = await session.Query<Language>().AnyAsync(ct);
        if (existing) return; // already seeded

        session.StoreObjects(new[]
        {
            new Language("en", "English", "English", IsDefault: true),
            // Add your three official languages here
            new Language("fr", "French", "Français"),
            new Language("de", "German", "Deutsch"),
            new Language("it", "Italian", "Italiano"),
        });

        await session.SaveChangesAsync(ct);
    }
}
