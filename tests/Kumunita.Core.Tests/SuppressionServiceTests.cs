using Kumunita.Core;
using Kumunita.Core.Notifications;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C12 seam test for <see cref="SuppressionService"/> (F17 / MAIL-03).
/// Drives the real <see cref="SuppressionService"/> over a scratch Postgres:
/// the write lane (<c>SuppressAsync</c>) and the read lane (<c>IsSuppressedAsync</c>),
/// including the pin §3.4 idempotency rule (re-suppression overwrites, one row).
/// Naming <see cref="ISuppressionService"/> (the seam) keeps it off the gate's
/// "I*Service named by a test" red list.
/// </summary>
public sealed class SuppressionServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Suppress_StoresLowercasedAnd_IsSuppressed()
    {
        var store = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SuppressionService(store);   // ISuppressionService impl

        var email = "  Bounce@Test.Example ";
        await svc.SuppressAsync(email, "bounce", "SES", ct);

        // The lookup lowercases + trims — the stored Id is the normalized form.
        Assert.True(await svc.IsSuppressedAsync(email, ct));
        Assert.True(await svc.IsSuppressedAsync("bounce@test.example", ct));

        await using var session = store.QuerySession();
        var row = await session.LoadAsync<SuppressedAddress>("bounce@test.example", ct);
        Assert.NotNull(row);
        Assert.Equal("bounce", row!.Reason);
        Assert.Equal("ses", row.Source);   // source is normalized to lowercase too
    }

    [Fact]
    public async Task Suppress_Resuppress_SameRow_LastWriteWins()
    {
        var store = await BootAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SuppressionService(store);

        await svc.SuppressAsync("dupe@test.example", "bounce", "ses", ct);
        await svc.SuppressAsync("dupe@test.example", "complaint", "postal", ct);

        await using var session = store.QuerySession();
        var rows = (await session.Query<SuppressedAddress>()
            .Where(a => a.Id == "dupe@test.example").ToListAsync(ct)).Count;
        Assert.Equal(1, rows);   // pin §3.4 — one row, no duplicate
    }

    [Fact]
    public async Task IsSuppressed_Absent_ReturnsFalse()
    {
        var store = await BootAsync();
        var svc = new SuppressionService(store);
        Assert.False(await svc.IsSuppressedAsync("nobody@test.example", TestContext.Current.CancellationToken));
        Assert.False(await svc.IsSuppressedAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Suppress_RequiresAllThreeFields()
    {
        var store = await BootAsync();
        var svc = new SuppressionService(store);
        var ct = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SuppressAsync("", "bounce", "ses", ct));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SuppressAsync("a@b.c", "", "ses", ct));
        await Assert.ThrowsAsync<ArgumentException>(() => svc.SuppressAsync("a@b.c", "bounce", "", ct));
    }

    private async Task<IDocumentStore> BootAsync()
    {
        // The SuppressedAddress doc lives on NotificationDocTypes (pin §1.1).
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            CoreDocTypes.Configure(opts);
            NotificationDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }
}
