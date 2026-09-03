using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// Plan M1 step 9 — closes the /health-degraded seam: the production
/// <see cref="EmailDeadLetterCounter"/> (the one DI actually wires into
/// <see cref="Kumunita.Web.Controllers.HealthController" />) must observe the
/// <see cref="EmailDeadLetter"/> rows written by <see cref="EmailDeadLetterWriter"/>
/// against a real Postgres, so the operator-visible "degraded" gate (OPS.md §8)
/// is backed by real data end-to-end, not just by a unit-test substitute.
/// </summary>
public sealed class EmailDeadLetterCounterTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task GetCountAsync_ReflectsStoredDeadLetters_AgainstRealPostgres()
    {
        var store = await BootStoreAsync();

        // 0 rows → 0 (the "ok" side of the degraded gate).
        var counter = new EmailDeadLetterCounter(store);
        Assert.Equal(0, await counter.GetCountAsync(TestContext.Current.CancellationToken));

        // Stage an OutboxEmail + commit it (the same transaction shape a real
        // domain write uses), then write the dead-letter row via the single
        // authoritative writer and commit — mirroring OutboxEmailHandler's
        // terminal-failure path without a live Wolverine host.
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            var origin = new OutboxEmail
            {
                Id = "omsg-e2e-1",
                IdempotencyKey = "verify:u-e2e-1:1",
                Recipient = "resident@kumunita",
                Subject = "Verify your account",
                Body = "link",
                QueuedAt = DateTimeOffset.UtcNow
            };
            session.Store(origin);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);

            var row = EmailDeadLetterWriter.Write(
                session, origin,
                lastError: "SMTP 550: relay unavailable",
                attempts: EmailDeadLetterWriter.MaxAttempts,
                ct: TestContext.Current.CancellationToken);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.False(string.IsNullOrEmpty(row.Id));
        }

        // The production counter (fresh session, same store) now observes the row.
        Assert.Equal(1, await counter.GetCountAsync(TestContext.Current.CancellationToken));

        // A second dead-letter (distinct row) increments the count — the "count"
        // the degraded gate's `emailDeadLetters` field reports.
        await using (var session2 = store.OpenSession(new SessionOptions()))
        {
            var origin2 = new OutboxEmail
            {
                Id = "omsg-e2e-2",
                IdempotencyKey = "setup:u-e2e-2",
                Recipient = "another@kumunita",
                Subject = "Admin setup",
                Body = "link",
                QueuedAt = DateTimeOffset.UtcNow
            };
            var row2 = EmailDeadLetterWriter.Write(
                session2, origin2,
                lastError: "SMTP 421: server closing connection",
                attempts: EmailDeadLetterWriter.MaxAttempts,
                ct: TestContext.Current.CancellationToken);
            await session2.SaveChangesAsync(TestContext.Current.CancellationToken);
            Assert.False(string.IsNullOrEmpty(row2.Id));
        }

        Assert.Equal(2, await counter.GetCountAsync(TestContext.Current.CancellationToken));
    }

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
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
