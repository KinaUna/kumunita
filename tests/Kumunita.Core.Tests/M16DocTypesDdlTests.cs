using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Inventory;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <c>M16</c> U01 pin (ADR 0117 D1 / D2 / D4, the design doc §2 + §9.2):
/// the <see cref="M16DocTypes"/> surface registers **both** documents
/// (<see cref="InventoryItem"/> + <see cref="InventoryCheckout"/>) and the
/// **open-checkout unique partial index** (the F1 idempotency witness — at most
/// one open checkout per item; a concurrent double-check-out's second commit
/// fails at the DB layer, the M9 <c>convo_uidx_pair</c> shape); the
/// <see cref="InventoryItemToAuditableResource"/> adapter presents the frozen
/// 6-member <see cref="IAuditableResource"/> surface with the **exact**
/// <c>TargetKind</c> ("inventory" — the <c>AccessAudit.TargetKind</c>
/// discriminator, C3). Verified against the *live* Postgres catalog (the
/// <c>KumunitaFeatureDdlTests</c> / <c>AdminOverrideDdlTests</c> shape), not the
/// in-memory object graph.
/// </summary>
public class M16DocTypesDdlTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private string _connection = "";

    public async ValueTask InitializeAsync() => _connection = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Apply_Registers_BothDocs_And_OpenCheckout_UniquePartialIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            // The sibling harnesses (ProjectDocShapeTests / MessagingServiceTests)
            // add the two storage features before a *DocTypes surface — that is
            // the established shape that actually applies the document DDL.
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M16DocTypes.Configure(opts);
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // ── Force the documents' schema to materialize (the M9/M5 doc-shape
        // test shape): in this stack document DDL is applied lazily on first
        // save — a bare ApplyAllConfiguredChangesToDatabaseAsync only applies
        // the storage features + Weasel migrations, so the sibling tests
        // (ProjectDocShapeTests) round-trip-store their docs first. Store +
        // load both M16 docs so mt."InventoryItem" / mt."InventoryCheckout"
        // exist, then inspect the live catalog.
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new InventoryItem
            {
                Id = "inv-1",
                Name = "Shared drill",
                OwnerKind = "shared",
                ComponentId = "comp-a",
                AuthorId = "u-author",
                Audience = null, // public
                Created = new DateTimeOffset(2026, 9, 29, 9, 0, 0, TimeSpan.Zero),
            });
            w.Store(new InventoryCheckout
            {
                Id = "invco-1",
                ItemId = "inv-1",
                BorrowerId = "u-borrower",
                CheckedOutAt = new DateTimeOffset(2026, 9, 29, 9, 0, 0, TimeSpan.Zero),
                CheckedInAt = null, // open
                Note = "on loan",
            });
            await w.SaveChangesAsync(ct);
        }
        await using (var q = store.QuerySession())
        {
            var item = (await q.LoadAsync<InventoryItem>("inv-1", ct))!;
            Assert.Equal("Shared drill", item.Name);
            var co = (await q.LoadAsync<InventoryCheckout>("invco-1", ct))!;
            Assert.Equal("inv-1", co.ItemId);
        }

        // ── (1) Both documents registered. Marten's default doc-table
        // convention is the <c>mt_doc_</c> prefix + lowercase class name
        // (distinct from the hand-rolled Weasel mt."AdminOverride" table), so
        // the two tables are mt.mt_doc_inventoryitem + mt.mt_doc_inventorycheckout.
        var itemCols = await QueryColumnsAsync("mt_doc_inventoryitem");
        Assert.True(itemCols.Count > 0, "mt_doc_inventoryitem table not found in mt");
        Assert.Contains("id", itemCols);
        Assert.Contains("data", itemCols);

        var checkoutCols = await QueryColumnsAsync("mt_doc_inventorycheckout");
        Assert.True(checkoutCols.Count > 0, "mt_doc_inventorycheckout table not found in mt");
        Assert.Contains("id", checkoutCols);
        Assert.Contains("data", checkoutCols);
        var checkoutTable = "mt_doc_inventorycheckout";

        // ── (2) The F1 idempotency witness (D4) — the open-checkout **unique
        // partial index** on (ItemId) where CheckedInAt IS NULL. It must be
        // UNIQUE, carry a non-null **partial** predicate, and that predicate
        // must reference the `CheckedInAt` key (PascalCase JSONB — the ADR 0004
        // data->'IsDraft' shape) with an `IS NULL` clause (open == null).
        var idx = await QueryOpenCheckoutUniquePartialIndexAsync(checkoutTable);
        Assert.NotNull(idx);
        Assert.True(idx!.IsUnique, "the open-checkout index must be UNIQUE (D4 / F1)");
        Assert.False(string.IsNullOrWhiteSpace(idx.Pred),
            "the open-checkout index must be PARTIAL (carry a WHERE predicate — the F1 witness)");
        Assert.Contains("CheckedInAt", idx.Pred!);
        Assert.Contains("IS NULL", idx.Pred!);
    }

    [Fact]
    public void Adapter_Presents_Frozen6MemberSurface_With_InventoryTargetKind()
    {
        var doc = new InventoryItem
        {
            Id = "inv-1",
            Name = "Shared drill",
            OwnerKind = "shared",
            ComponentId = "comp-a",
            AuthorId = "u-author",
            Audience = null, // public
        };

        // The **exact** M5 ProjectToAuditableResource 6-member projection —
        // the only difference is the TargetKind string ("inventory", C3).
        IAuditableResource adapter = new InventoryItemToAuditableResource(doc);
        Assert.Equal("inv-1", adapter.Id);
        Assert.Equal("Shared drill", adapter.Name);
        Assert.Equal("u-author", adapter.OwnerId);
        Assert.Null(adapter.Audience);
        Assert.Equal("comp-a", adapter.ComponentId);
        Assert.Equal("inventory", adapter.TargetKind); // the EXACT string (C3)

        // OwnerKind is a string label — present on the doc, NEVER consulted by
        // the adapter (D3 / C-M16·1 / C-M16·5 — a grouping + write-standing
        // breadth, not a read/access gate).
        Assert.Equal("shared", doc.OwnerKind);
        Assert.DoesNotContain("OwnerKind", typeof(IAuditableResource).GetProperties().Select(p => p.Name));
    }

    // ── catalog helpers (the AdminOverrideDdlTests shape) ──────────────────

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
    /// The unique index on <c>mt.&lt;table&gt;</c> that carries a non-null
    /// **partial** predicate (the open-checkout witness). Returns null if no
    /// such index exists. The table is resolved via the <c>pg_class</c> /
    /// <c>pg_namespace</c> join (the repo's own index-query shape — the
    /// <c>to_regclass</c> two-arg form does not exist in Postgres).
    /// </summary>
    private async Task<DbIndex?> QueryOpenCheckoutUniquePartialIndexAsync(string table)
    {
        await using var conn = new NpgsqlConnection(_connection);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT pg_index.indisunique AS is_unique,
                   pg_get_expr(pg_index.indpred, pg_index.indrelid) AS pred
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
              AND pg_index.indpred IS NOT NULL
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("p_table", table);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new DbIndex(r.GetFieldValue<bool>(0), r.GetFieldValue<string?>(1));
    }

    private sealed record DbIndex(bool IsUnique, string? Pred);
}
