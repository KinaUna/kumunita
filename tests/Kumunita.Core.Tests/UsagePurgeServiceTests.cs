using Kumunita.Core;
using Kumunita.Core.Usage;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C12 seam test for <see cref="UsagePurgeService"/> (M13 usage-row retention,
/// ADR 0114 D5). The platform constant is <see cref="UsagePurgeService.RetentionDays"/>
/// (365 days). Two legs: rows older than the cutoff are deleted (and the count
/// returned); not-expired rows are kept; a second run is a no-op (0).
/// Mirrors <see cref="MediaLifecycleTests"/> — the same store-injected static
/// shape with a <c>now</c> injection point for a deterministic boundary.
/// </summary>
public sealed class UsagePurgeServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Purge_DeletesExpired_KeepsLive_NoOpOnSecondRun()
    {
        var store = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var now = DateTimeOffset.UtcNow;

        var expiredId = Guid.NewGuid().ToString("n");
        var liveId = Guid.NewGuid().ToString("n");

        await using (var session = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            session.Store(new UsageEvent
            {
                Id = expiredId,
                At = now.AddDays(-(UsagePurgeService.RetentionDays + 1)),
                ActorId = "resident-expired",
                RouteTemplate = "GET /posts/{id}"
            });
            session.Store(new UsageEvent
            {
                Id = liveId,
                At = now,
                ActorId = "resident-live",
                RouteTemplate = "GET /posts/{id}"
            });
            await session.SaveChangesAsync(ct);
        }

        // Precondition: both rows exist before the purge.
        Assert.Equal(2, await CountAsync(store));

        // First purge: exactly the expired row is deleted.
        var deleted = await UsagePurgeService.PurgeAsync(store, now, ct);
        Assert.Equal(1, deleted);

        // (1) the expired row is gone.
        await using (var q = store.QuerySession())
            Assert.Null(await q.LoadAsync<UsageEvent>(expiredId, ct));

        // (2) the live row is still present.
        await using (var q2 = store.QuerySession())
            Assert.NotNull(await q2.LoadAsync<UsageEvent>(liveId, ct));

        // (3) second run is a no-op (idempotent — the M13 "natural no-op" shape).
        var second = await UsagePurgeService.PurgeAsync(store, now, ct);
        Assert.Equal(0, second);
    }

    private async Task<IDocumentStore> BootAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            UsageDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    private async Task<int> CountAsync(IDocumentStore store)
    {
        await using var session = store.QuerySession();
        return await session.Query<UsageEvent>().CountAsync(TestContext.Current.CancellationToken);
    }
}
