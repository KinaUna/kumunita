using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Events;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The ADR 0060 **warm-boot backfill** lane for the sample events' de / fr / da
/// <see cref="EventTranslation"/> rows (<see cref="SampleDataSeeder.BackfillEventTranslationsAsync"/>).
/// Mirrors the <see cref="EventServiceTests"/> boot shape verbatim — same
/// <see cref="PostgresFixture"/>, same <c>BootStoreAsync</c> (M4 schema, fresh
/// scratch Postgres per test) — and pins the three invariants that make the lane
/// safe to run on every warm deploy:
/// </summary>
/// <list type="number">
/// <item><b>Create-if-missing</b> — a sample event (authored in <c>en</c>) with no
///       translation rows gains exactly the three baseline rows (de / fr / da)
///       for the one sample event in the registry.</item>
/// <item><b>Never clobbers</b> — an existing <c>(EventId, LanguageCode)</c> row
///       (a Translator's in-app edit) is left untouched; the lane adds the other
///       two languages but keeps the human-edited one (the ADR 0042 D1 invariant).</item>
/// <item><b>Idempotent</b> — a second run over the same database adds nothing; the
///       row count is unchanged and the human-edited body is still intact.</item>
/// </list>
/// <para>
/// Only one sample-event title (<c>"Community Cleanup Day"</c>) is planted, so the
/// lane's "no such event" skip path (the other three registry keys) is also
/// exercised for free.
/// </para>
public class SampleDataEventTranslationBackfillTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string SampleTitle = "Community Cleanup Day";

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>Plant a document row directly (test fixture seeding — the backfill
    /// lane under test reads the live documents, it does not take a service seam).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    private static async Task RunBackfill(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.OpenSession(new Marten.Services.SessionOptions());
        await SampleDataSeeder.BackfillEventTranslationsAsync(s, ct);
    }

    private static async Task<List<EventTranslation>> TranslationsFor(
        IDocumentStore store, string eventId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var r = store.OpenSession(new Marten.Services.SessionOptions());
        return (await r.Query<EventTranslation>()
                .Where(t => t.EventId == eventId)
                .ToListAsync(ct)).ToList();
    }

    private static Event CleanupEvent(string id) => new()
    {
        Id = id,
        Title = SampleTitle,
        Body = "Gloves and bags provided.",
        AuthorId = "author-0",
        Start = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
        IsDraft = false,
        IsDeleted = false,
        LanguageCode = "en",
    };

    private static readonly string[] BaselineCodes =
        ["de", "fr", "da"];

    // ── 1 — creates the missing de/fr/da rows for a sample event ──────────────
    [Fact]
    public async Task Backfill_CreatesMissingTranslationsForSampleEvent()
    {
        var store = await BootStoreAsync();
        await Plant(store, CleanupEvent("ev-1"));

        await RunBackfill(store);

        var rows = await TranslationsFor(store, "ev-1");
        // Exactly the three baseline languages — no en row is written (the
        // authored-in base is the event's own title/body, never read or written).
        Assert.Equal(3, rows.Count);
        Assert.Equal(BaselineCodes.OrderBy(c => c), rows.Select(r => r.LanguageCode).OrderBy(c => c));
        // Every baseline row carries the registry's text and a real body.
        foreach (var row in rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Body));
            var baseline = SampleDataSeeder.EventTranslationBaselines[SampleTitle]
                .Single(b => b.Code == row.LanguageCode);
            Assert.Equal(baseline.Body, row.Body);
            Assert.Equal(baseline.Title, row.Title);
        }
    }

    // ── 2 — never clobbers a Translator's in-app edit ─────────────────────────
    [Fact]
    public async Task Backfill_NeverClobbersExistingTranslation()
    {
        var store = await BootStoreAsync();
        await Plant(store, CleanupEvent("ev-2"));
        // A Translator has already edited the German row (the ADR 0059 lane) — a
        // distinct, human-authored body.
        const string humanBody = "Übersetzt von der Übersetzerin.";
        await Plant(store, new EventTranslation
        {
            Id = "tr-2-de", EventId = "ev-2", LanguageCode = "de",
            Title = "Eigenübersetzung", Body = humanBody,
            AuthorId = "translator-0", Created = DateTimeOffset.UtcNow,
        });

        await RunBackfill(store);

        var rows = await TranslationsFor(store, "ev-2");
        // Still exactly three rows: the human de + the backfilled fr / da.
        Assert.Equal(3, rows.Count);
        var de = rows.Single(r => r.LanguageCode == "de");
        // The human edit is preserved verbatim — not overwritten with the baseline.
        Assert.Equal(humanBody, de.Body);
        Assert.Equal("Eigenübersetzung", de.Title);
        Assert.Equal("translator-0", de.AuthorId);
        // The other two baseline languages were added by the lane.
        Assert.Contains(rows, r => r.LanguageCode == "fr");
        Assert.Contains(rows, r => r.LanguageCode == "da");
    }

    // ── 3 — idempotent: a second run adds nothing and keeps the human edit ────
    [Fact]
    public async Task Backfill_IsIdempotent()
    {
        var store = await BootStoreAsync();
        await Plant(store, CleanupEvent("ev-3"));
        const string humanBody = "Zweite Boot-Übersetzung.";
        await Plant(store, new EventTranslation
        {
            Id = "tr-3-da", EventId = "ev-3", LanguageCode = "da",
            Title = "Egen oversættelse", Body = humanBody,
            AuthorId = "translator-0", Created = DateTimeOffset.UtcNow,
        });

        await RunBackfill(store);
        var afterFirst = await TranslationsFor(store, "ev-3");
        Assert.Equal(3, afterFirst.Count);

        // A second warm boot over the same database.
        await RunBackfill(store);
        var afterSecond = await TranslationsFor(store, "ev-3");

        Assert.Equal(3, afterSecond.Count);
        Assert.Equal(
            afterFirst.Select(r => r.Id).OrderBy(x => x),
            afterSecond.Select(r => r.Id).OrderBy(x => x));
        // The human Danish edit is still intact after two runs.
        Assert.Equal(humanBody, afterSecond.Single(r => r.LanguageCode == "da").Body);
    }
}
