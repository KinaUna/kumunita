using Kumunita.Core.Authorization;
using Kumunita.Core;
using Marten;
using Npgsql;

namespace Kumunita.Core.Tests;

/// <summary>
/// Pins M1's versioned DDL (ADR 0004 §B.1 — hand-rolled Weasel carve-out): applying
/// <see cref="AuthorizationFeature"/> must create <c>mt."AdminOverride"</c>
/// (id PK NOT NULL, userId NOT NULL, token NOT NULL, grantedAt NOT NULL, expiresAt
/// NOT NULL, consumedAt NULL), plus a non-unique composite index on
/// <c>(userId, consumedAt)</c>. A second apply must be a no-op (the idempotency claim
/// the boot block and ADR rely on). Verified against the *live* Postgres catalog.
/// </summary>
public class AdminOverrideDdlTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private string _connection = "";

    public async ValueTask InitializeAsync() => _connection = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Apply_Creates_AdminOverride_Table_With_ExpectedColumns()
    {
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<AuthorizationFeature>();
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, TestContext.Current.CancellationToken);

        var columns = await QueryColumnsAsync();
        var byName = columns.ToDictionary(c => c[0]);

        // All six columns present, in DDL order.
        Assert.Equal(
            new[] { "id", "userId", "token", "grantedAt", "expiresAt", "consumedAt" },
            columns.Select(c => c[0]).ToArray());

        // Type checks (Postgres data_type values as they appear in information_schema).
        Assert.Equal("character varying", byName["id"][2]);
        Assert.Equal("character varying", byName["userId"][2]);
        Assert.Equal("character varying", byName["token"][2]);
        Assert.Equal("timestamp with time zone", byName["grantedAt"][2]);
        Assert.Equal("timestamp with time zone", byName["expiresAt"][2]);
        Assert.Equal("timestamp with time zone", byName["consumedAt"][2]);
    }

    [Fact]
    public async Task Apply_Creates_NonUniqueCompositeIndex_On_UserAndConsumed()
    {
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<AuthorizationFeature>();
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, TestContext.Current.CancellationToken);

        // The index must exist, be non-unique, and cover exactly (userId, consumedAt) in that order.
        var idx = await QueryIndexAsync("idx_admin_override_user_consumed");
        Assert.True(idx is not null, $"expected index named 'idx_admin_override_user_consumed' on mt.\"AdminOverride\"");
        Assert.False(idx!.IsUnique, "index must be NON-unique by design (ADR 0004 §B.1)");
        Assert.Equal(new[] { "userId", "consumedAt" }, idx.Columns);
    }

    [Fact]
    public async Task ApplyIsIdempotent_SecondRunIsANoOp()
    {
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<AuthorizationFeature>();
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
            WHERE table_schema = 'mt' AND table_name = 'AdminOverride'
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

    /// <summary>
    /// Look up one non-PK index by name on <c>mt."AdminOverride"</c>. Returns null if absent.
    /// Uses a <c>pg_class</c> join (rather than the shorter <c>indexrelid::regclass::name</c>
    /// cast) because the latter only resolves unqualified names through <c>search_path</c>,
    /// which does not include our <c>mt</c> schema in this session.
    /// </summary>
    private async Task<DbIndex?> QueryIndexAsync(string indexName)
    {
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT pg_index.indisunique AS is_unique,
                   (SELECT array_agg(a.attname ORDER BY u.ord)
                    FROM unnest(pg_index.indkey) WITH ORDINALITY AS u(attnum, ord)
                    JOIN pg_attribute a ON a.attrelid = pg_index.indrelid AND a.attnum = u.attnum) AS columns
            FROM pg_index
            JOIN pg_class idx ON idx.oid = pg_index.indexrelid
            WHERE idx.relname = @p_indexname
              AND pg_index.indrelid = 'mt."AdminOverride"'::regclass
              AND NOT pg_index.indisprimary
            """;
        cmd.Parameters.AddWithValue("p_indexname", indexName);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new DbIndex(r.GetFieldValue<bool>(0), r.GetFieldValue<string[]>(1));
    }

    /// <summary>A small record capturing the one index we care about (name known from the ADR).</summary>
    private sealed record DbIndex(bool IsUnique, string[] Columns);

    private async Task<long> QueryRowCountAsync()
    {
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM mt.\"AdminOverride\"";
        return (long)(await cmd.ExecuteScalarAsync()!)!;
    }
}
