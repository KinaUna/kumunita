using Kumunita.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kumunita.Core.Tests;

/// <summary>
/// Pinned behavior of <see cref="DbBootstrap.IsPristineAsync"/> (M0 first-boot gate, OPS.md §2):
/// it must report a brand-new database as pristine, and stop doing so the moment the
/// Identity schema (<c>identity</c>) exists. The <c>mt</c> schema is deliberately NOT a
/// disqualifier — Wolverine/Marten create it during <c>app.StartAsync()</c> (before the
/// gate runs), so a <c>mt</c>-only DB is still a valid "first boot" and must be reported
/// as pristine (see the method docs on <see cref="DbBootstrap.IsPristineAsync"/>).
/// </summary>
public class DbBootstrapIsPristineTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private string _connection = "";

    public async ValueTask InitializeAsync() => _connection = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Fresh_Database_Is_Pristine()
    {
        var db = CreateContext();

        Assert.True(await DbBootstrap.IsPristineAsync(db, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Is_Still_Pristine_When_Only_Mt_Schema_Exists()
    {
        // Wolverine/Marten create mt during app.StartAsync(), before the first-boot gate
        // runs, so a DB that has mt but not identity is still a genuine first boot and
        // the seeder must run. mt is not part of the pristine signal.
        await CreateSchemaAsync("mt");
        var db = CreateContext();

        Assert.True(await DbBootstrap.IsPristineAsync(db, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Is_Not_Pristine_When_Identity_Schema_Exists()
    {
        await CreateSchemaAsync("identity");
        var db = CreateContext();

        Assert.False(await DbBootstrap.IsPristineAsync(db, TestContext.Current.CancellationToken));
    }

    private AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connection).Options);

    private async Task CreateSchemaAsync(string name)
    {
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE SCHEMA {name}";
        await cmd.ExecuteNonQueryAsync();
    }
}
