using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Usage;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The 4 D3 aggregation pins for <see cref="UsageAnalyticsService"/>
/// (M13, ADR 0114; design doc §aggregation + §pinned tests, verbatim).
/// <see cref="PostgresFixture"/> harness (Testcontainers <c>postgres:18</c>)
/// — the same <c>BootStoreAsync</c> shape as <see cref="EventReminderServiceTests"/>:
/// a fresh scratch database per test, the M1/M3/M4 doc surfaces registered
/// alongside <see cref="UsageDocTypes"/>, rows planted via a write session.
/// </summary>
public class UsageAnalyticsServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static int _plantCounter;

    // ── Pin 1 ─────────────────────────────────────────────────────────────
    // A row at now − 91d is outside the 90-day window; a row at now − 1d
    // is inside. The 90-day window returns Total == 1 (the D3 cutoff pin —
    // the cutoff is computed at the seam call, not at the query).

    [Fact]
    public async Task Aggregation_Window_Excludes_Older_Rows()
    {
        var store = await BootStoreAsync();
        var service = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;

        await PlantEvent(store, now.AddDays(-91), "a1", "GET /posts/{id}");
        await PlantEvent(store, now.AddDays(-1),  "a1", "GET /posts/{id}");

        var result = await service.GetWindowAsync(90);

        Assert.Equal(1, result.Total);
        // The row that IS in the window is the recent one — the surface
        // ranking carries exactly the one row that counts.
        var row = Assert.Single(result.SurfaceRanking);
        Assert.Equal("posts", row.Surface);
        Assert.Equal(1, row.Total);
        await store.DisposeAsync();
    }

    // ── Pin 2 ─────────────────────────────────────────────────────────────
    // Plant 5 posts, 3 events, 3 groups: the ranking is posts, events,
    // groups — events before groups on the tie (the D3 ordering pin:
    // descending by Total, ties ascending by Surface).

    [Fact]
    public async Task Aggregation_SurfaceRanking_Descending_Then_Alphabetical()
    {
        var store = await BootStoreAsync();
        var service = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 5; i++) await PlantEvent(store, now, "a1", "GET /posts/{id}");
        for (var i = 0; i < 3; i++) await PlantEvent(store, now, "a1", "GET /events/{id}");
        for (var i = 0; i < 3; i++) await PlantEvent(store, now, "a1", "GET /groups/{id}");

        var result = await service.GetWindowAsync(30);

        Assert.Equal(11, result.Total);
        var ranking = result.SurfaceRanking
            .Select(r => (Surface: r.Surface, Total: r.Total))
            .ToList();
        // posts (5) first; then the 3/3 tie broken alphabetically: events
        // before groups.
        Assert.Equal(
            new[] { ("posts", 5), ("events", 3), ("groups", 3) },
            ranking);
        await store.DisposeAsync();
    }

    // ── Pin 3 ─────────────────────────────────────────────────────────────
    // 3 rows with ActorId "a1" + 2 rows with ActorId "":
    // AuthenticatedTotal == 3, AnonymousTotal == 2 (the D3 field pin).

    [Fact]
    public async Task Aggregation_AuthenticatedVsAnonymous_Counts()
    {
        var store = await BootStoreAsync();
        var service = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;

        for (var i = 0; i < 3; i++) await PlantEvent(store, now, "a1", "GET /posts/{id}");
        for (var i = 0; i < 2; i++) await PlantEvent(store, now, "",   "GET /posts/{id}");

        var result = await service.GetWindowAsync(30);

        Assert.Equal(5, result.Total);
        Assert.Equal(3, result.AuthenticatedTotal);
        Assert.Equal(2, result.AnonymousTotal);
        await store.DisposeAsync();
    }

    // ── Pin 4 ─────────────────────────────────────────────────────────────
    // ActorId "a1", "a1", "a2" + 1 row ActorId "": DistinctActors == 2
    // (the D3 field pin — the count is over non-empty ActorIds only; the
    // empty row neither counts as an actor nor leaks one).

    [Fact]
    public async Task Aggregation_DistinctActors_Counts_Unique_NonEmpty()
    {
        var store = await BootStoreAsync();
        var service = new UsageAnalyticsService(store);
        var now = DateTimeOffset.UtcNow;

        await PlantEvent(store, now, "a1", "GET /posts/{id}");
        await PlantEvent(store, now, "a1", "GET /posts/{id}");
        await PlantEvent(store, now, "a2", "GET /events/{id}");
        await PlantEvent(store, now, "",   "GET /groups/{id}");

        var result = await service.GetWindowAsync(30);

        Assert.Equal(4, result.Total);
        Assert.Equal(3, result.AuthenticatedTotal);
        Assert.Equal(1, result.AnonymousTotal);
        Assert.Equal(2, result.DistinctActors);
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot the scratch store with the M1/M3/M4 surfaces (the
    /// <see cref="EventReminderServiceTests"/> shape) plus the M13
    /// <see cref="UsageDocTypes"/> surface so the <c>UsageEvent</c> table
    /// exists.</summary>
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
            UsageDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static async Task PlantEvent(IDocumentStore store, DateTimeOffset at,
        string actorId, string routeTemplate)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new UsageEvent
        {
            Id = $"u04-{Interlocked.Increment(ref _plantCounter)}-{Guid.NewGuid():N}"[..20],
            At = at,
            ActorId = actorId,
            RouteTemplate = routeTemplate
        });
        await w.SaveChangesAsync(ct);
    }
}
