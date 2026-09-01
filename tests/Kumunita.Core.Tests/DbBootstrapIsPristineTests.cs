using Kumunita.Core.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kumunita.Core.Tests;

/// <summary>
/// Pinned behavior of <see cref="DbBootstrap.IsPristineAsync"/> (M0 first-boot gate, OPS.md §2):
/// it must report a brand-new database as pristine, and stop doing so the moment *either*
/// domain schema (<c>mt</c>) or the Identity schema (<c>identity</c>) exists.
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
    public async Task Is_Not_Pristine_When_Mt_Schema_Exists()
    {
        await CreateSchemaAsync("mt");
        var db = CreateContext();

        Assert.False(await DbBootstrap.IsPristineAsync(db, TestContext.Current.CancellationToken));
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
