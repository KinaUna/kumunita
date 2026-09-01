using Kumunita.Core;
using Marten;
using Npgsql;

namespace Kumunita.Core.Tests;

/// <summary>
/// Pins M0's versioned DDL (ADR 0004 Decision B): applying <see cref="KumunitaFeature"/>
/// must create <c>mt.community(id varchar PK, name varchar NOT NULL)</c>, and a second
/// apply must be a no-op (the idempotency claim the boot block and ADR rely on).
/// Verified against the *live* Postgres catalog, not the in-memory object graph.
/// </summary>
public class KumunitaFeatureDdlTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private string _connection = "";

    public async ValueTask InitializeAsync() => _connection = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Apply_Creates_CommunityTable_With_IdPk_And_NotNullName()
    {
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, TestContext.Current.CancellationToken);

        var columns = await QueryColumnsAsync();
        var byName = columns.ToDictionary(c => c[0]);

        Assert.True(byName.ContainsKey("id"));
        Assert.True(byName.ContainsKey("name"));
        Assert.Equal("character varying", byName["id"][2]);
        Assert.Equal("character varying", byName["name"][2]);
        Assert.Equal("NO", byName["name"][1]); // name NOT NULL

        var pkColumns = await QueryPrimaryKeyColumnsAsync();
        Assert.Equal(["id"], pkColumns);
    }

    [Fact]
    public async Task ApplyIsIdempotent_SecondRunIsANoOp()
    {
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, TestContext.Current.CancellationToken);
        var first = await QueryColumnsAsync();

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, TestContext.Current.CancellationToken);
        var second = await QueryColumnsAsync();

        Assert.Equal(
            first.Select(c => string.Join("|", c)),
            second.Select(c => string.Join("|", c)));

        // Row count untouched — the idempotent re-apply did not insert/duplicate anything.
        Assert.Equal(0L, await QueryRowCountAsync());
    }

    // Each row: [0]=column name, [1]=is_nullable, [2]=data_type.
    private async Task<List<string[]>> QueryColumnsAsync()
    {
        var rows = new List<string[]>();
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT column_name, is_nullable, data_type
            FROM information_schema.columns
            WHERE table_schema = 'mt' AND table_name = 'community'
            ORDER BY ordinal_position
            """;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new string[3];
            for (var i = 0; i < 3; i++) row[i] = r.GetString(i);
            rows.Add(row);
        }
        return rows;
    }

    private async Task<List<string>> QueryPrimaryKeyColumnsAsync()
    {
        var cols = new List<string>();
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.attname
            FROM pg_index i
            JOIN pg_attribute a ON a.attrelid = i.indrelid AND a.attnum = ANY (i.indkey)
            WHERE i.indisprimary
              AND i.indrelid = 'mt.community'::regclass
            ORDER BY a.attnum
            """;
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            cols.Add(r.GetString(0));
        }
        return cols;
    }

    private async Task<long> QueryRowCountAsync()
    {
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM mt.community";
        return (long)(await cmd.ExecuteScalarAsync()!)!;
    }
}
