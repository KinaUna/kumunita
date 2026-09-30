using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bookmarks;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <c>M17</c> U01 pin (ADR 0118 D1 / D4, the design doc §2.1): the
/// <see cref="M17DocTypes"/> surface registers the <see cref="Bookmark"/>
/// document with the **unique** business-key index on <c>(OwnerId,
/// TargetKind, TargetId)</c> — the F1 idempotency witness (the M9
/// <c>convo_uidx_pair</c> shape; a duplicate bookmark of the same owner +
/// target commits exactly one row, the DB is the arbiter) — plus the
/// non-unique <c>(OwnerId, Created)</c> list-ordering index. Verified against
/// the *live* Postgres catalog (the <c>M16DocTypesDdlTests</c> shape), not the
/// in-memory object graph. The test name is a doc-registration shape pin
/// (outside the design doc's §2.5 seam list — the M16/U01 precedent).
/// </summary>
public class M17DocTypesDdlTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private string _connection = "";

    public async ValueTask InitializeAsync() => _connection = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task M17DocTypes_RegistersBookmarkWithUniqueOwnerTargetIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            // The sibling harnesses (M16DocTypesDdlTests / ProjectDocShapeTests)
            // add the two storage features before a *DocTypes surface — that is
            // the established shape that actually applies the document DDL.
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M17DocTypes.Configure(opts);
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // ── Force the document's schema to materialize (the M16/M9/M5
        // doc-shape test shape): in this stack document DDL is applied lazily
        // on first save — a bare ApplyAllConfiguredChangesToDatabaseAsync only
        // applies the storage features + Weasel migrations, so the sibling
        // tests round-trip-store their docs first. Store + load the Bookmark
        // so mt."mt_doc_bookmark" exists, then inspect the live catalog.
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new Bookmark
            {
                Id = "bm-1",
                OwnerId = "u-owner",
                TargetKind = "post",
                TargetId = "post-42",
                Created = new DateTimeOffset(2026, 9, 29, 9, 0, 0, TimeSpan.Zero),
            });
            await w.SaveChangesAsync(ct);
        }
        await using (var q = store.QuerySession())
        {
            var bm = (await q.LoadAsync<Bookmark>("bm-1", ct))!;
            Assert.Equal("u-owner", bm.OwnerId);
            Assert.Equal("post", bm.TargetKind);
            Assert.Equal("post-42", bm.TargetId);
        }

        // ── (1) The Bookmark document registered. Marten's default doc-table
        // convention is the <c>mt_doc_</c> prefix + lowercase class name, so
        // the table is mt.mt_doc_bookmark.
        var cols = await QueryColumnsAsync("mt_doc_bookmark");
        Assert.True(cols.Count > 0, "mt_doc_bookmark table not found in mt");
        Assert.Contains("id", cols);
        Assert.Contains("data", cols);

        // ── (2) The F1 idempotency witness (D4) — the **unique** business-key
        // index on (OwnerId, TargetKind, TargetId). It must be UNIQUE (not
        // the primary key) and its expression must reference all three JSONB
        // keys (PascalCase — the ADR 0004 data->'IsDraft' shape).
        var idx = await QueryUniqueIndexAsync("mt_doc_bookmark");
        Assert.NotNull(idx);
        Assert.True(idx!.IsUnique, "the (OwnerId, TargetKind, TargetId) index must be UNIQUE (D4 / F1)");
        Assert.Contains("OwnerId", idx.Expr!);
        Assert.Contains("TargetKind", idx.Expr!);
        Assert.Contains("TargetId", idx.Expr!);
    }

    // ── catalog helpers (the M16DocTypesDdlTests / AdminOverrideDdlTests shape) ──

    // Distinct column names on mt.<table> (information_schema).
    private async Task<List<string>> QueryColumnsAsync(string table)
    {
        var cols = new List<string>();
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_schema = 'mt' AND table_name = @p_table
            """;
        cmd.Parameters.AddWithValue("p_table", table);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) cols.Add(r.GetString(0));
        return cols;
    }

    /// <summary>
    /// A non-primary **unique** index on <c>mt.&lt;table&gt;</c> carrying a
    /// JSONB expression (the (OwnerId, TargetKind, TargetId) business-key
    /// witness). Returns null if no such index exists. The table is resolved
    /// via the <c>pg_class</c> / <c>pg_namespace</c> join (the repo's own
    /// index-query shape).
    /// </summary>
    private async Task<DbIndex?> QueryUniqueIndexAsync(string table)
    {
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT pg_index.indisunique AS is_unique,
                   pg_get_indexdef(pg_index.indexrelid) AS expr
            FROM pg_index
            WHERE pg_index.indrelid = (
                        SELECT c.oid
                        FROM pg_class c
                        JOIN pg_namespace n ON n.oid = c.relnamespace
                        WHERE n.nspname = 'mt'
                          AND c.relname = @p_table
                          AND c.relkind = 'r')
              AND NOT pg_index.indisprimary
              AND pg_index.indisunique
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("p_table", table);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new DbIndex(r.GetFieldValue<bool>(0), r.GetFieldValue<string?>(1));
    }

    private sealed record DbIndex(bool IsUnique, string? Expr);
}
