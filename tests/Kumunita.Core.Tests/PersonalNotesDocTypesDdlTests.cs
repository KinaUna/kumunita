using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.PersonalNotes;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <c>M37</c> U01 pin (ADR 0167 D1 / D3 / D6, the design doc §2.1): the
/// <see cref="PersonalNotesDocTypes"/> surface registers the
/// <see cref="PersonalNote"/> + <see cref="NoteFolder"/> documents with
/// their exact index set — the **unique** business-key index on
/// <c>(OwnerId, TargetKind, TargetId)</c> on <see cref="PersonalNote"/> (the
/// D3/F1 idempotency witness, the M17 <c>Bookmark</c> shape, the M9
/// <c>convo_uidx_pair</c> shape re-applied; a duplicate note of the same
/// owner + target commits exactly one row, the DB is the arbiter) and the
/// **unique** index on <c>(OwnerId, ParentId, Name)</c> on
/// <see cref="NoteFolder"/> (the D6 witness, C-M37·6) — plus the non-unique
/// <c>(OwnerId, Created)</c> + <c>(OwnerId, FolderId)</c> list indexes on
/// <see cref="PersonalNote"/> and the non-unique <c>(OwnerId, ParentId)</c>
/// index on <see cref="NoteFolder"/>. Verified against the *live* Postgres
/// catalog (the <c>BookmarkDocTypesDdlTests</c> /
/// <c>InventoryDocTypesDdlTests</c> shape), not the in-memory object graph.
/// The test names are doc-registration shape pins (outside the design doc's
/// §2.5 seam list — the M17/U01 precedent; the U01 handoff records it).
/// </summary>
public class PersonalNotesDocTypesDdlTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private string _connection = "";

    public async ValueTask InitializeAsync() => _connection = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task PersonalNotesDocTypes_RegistersPersonalNoteWithUniqueOwnerTargetIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            // The sibling harnesses (BookmarkDocTypesDdlTests /
            // InventoryDocTypesDdlTests) add the two storage features before
            // a *DocTypes surface — that is the established shape that
            // actually applies the document DDL.
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            PersonalNotesDocTypes.Configure(opts);
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // ── Force the document's schema to materialize (the M17/M16/M9/M5
        // doc-shape test shape): in this stack document DDL is applied lazily
        // on first save — a bare ApplyAllConfiguredChangesToDatabaseAsync
        // only applies the storage features + Weasel migrations, so the
        // sibling tests round-trip-store their docs first. Store + load the
        // PersonalNote so mt."mt_doc_personalnote" exists, then inspect the
        // live catalog.
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new PersonalNote
            {
                Id = "pn-1",
                OwnerId = "u-owner",
                TargetKind = "post",
                TargetId = "post-42",
                Body = "my note body",
                Created = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero),
            });
            await w.SaveChangesAsync(ct);
        }
        await using (var q = store.QuerySession())
        {
            var note = (await q.LoadAsync<PersonalNote>("pn-1", ct))!;
            Assert.Equal("u-owner", note.OwnerId);
            Assert.Equal("post", note.TargetKind);
            Assert.Equal("post-42", note.TargetId);
            Assert.Equal("my note body", note.Body);
        }

        // ── (1) The PersonalNote document registered. Marten's default
        // doc-table convention is the <c>mt_doc_</c> prefix + lowercase class
        // name, so the table is mt.mt_doc_personalnote.
        var cols = await QueryColumnsAsync("mt_doc_personalnote");
        Assert.True(cols.Count > 0, "mt_doc_personalnote table not found in mt");
        Assert.Contains("id", cols);
        Assert.Contains("data", cols);

        // ── (2) The D3/F1 idempotency witness — the **unique** business-key
        // index on (OwnerId, TargetKind, TargetId). It must be UNIQUE (not
        // the primary key) and its expression must reference all three JSONB
        // keys (PascalCase — the ADR 0004 data->'IsDraft' shape).
        var idx = await QueryUniqueIndexAsync("mt_doc_personalnote");
        Assert.NotNull(idx);
        Assert.True(idx!.IsUnique, "the (OwnerId, TargetKind, TargetId) index must be UNIQUE (D3 / F1)");
        Assert.Contains("OwnerId", idx.Expr!);
        Assert.Contains("TargetKind", idx.Expr!);
        Assert.Contains("TargetId", idx.Expr!);
    }

    [Fact]
    public async Task PersonalNotesDocTypes_RegistersNoteFolderWithUniqueOwnerParentNameIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            // The sibling harnesses (BookmarkDocTypesDdlTests /
            // InventoryDocTypesDdlTests) add the two storage features before
            // a *DocTypes surface — that is the established shape that
            // actually applies the document DDL.
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            PersonalNotesDocTypes.Configure(opts);
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // ── Force the document's schema to materialize (the M17/M16/M9/M5
        // doc-shape test shape): in this stack document DDL is applied lazily
        // on first save — a bare ApplyAllConfiguredChangesToDatabaseAsync
        // only applies the storage features + Weasel migrations, so the
        // sibling tests round-trip-store their docs first. Store + load the
        // NoteFolder so mt."mt_doc_notefolder" exists, then inspect the live
        // catalog.
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new NoteFolder
            {
                Id = "nf-1",
                OwnerId = "u-owner",
                Name = "my root folder",
                Created = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero),
            });
            await w.SaveChangesAsync(ct);
        }
        await using (var q = store.QuerySession())
        {
            var folder = (await q.LoadAsync<NoteFolder>("nf-1", ct))!;
            Assert.Equal("u-owner", folder.OwnerId);
            Assert.Equal("my root folder", folder.Name);
        }

        // ── (1) The NoteFolder document registered. Marten's default
        // doc-table convention is the <c>mt_doc_</c> prefix + lowercase class
        // name, so the table is mt.mt_doc_notefolder.
        var cols = await QueryColumnsAsync("mt_doc_notefolder");
        Assert.True(cols.Count > 0, "mt_doc_notefolder table not found in mt");
        Assert.Contains("id", cols);
        Assert.Contains("data", cols);

        // ── (2) The D6 witness (C-M37·6) — the **unique** business-key index
        // on (OwnerId, ParentId, Name). It must be UNIQUE (not the primary
        // key) and its expression must reference all three JSONB keys
        // (PascalCase — the ADR 0004 data->'IsDraft' shape).
        var idx = await QueryUniqueIndexAsync("mt_doc_notefolder");
        Assert.NotNull(idx);
        Assert.True(idx!.IsUnique, "the (OwnerId, ParentId, Name) index must be UNIQUE (D6 / C-M37·6)");
        Assert.Contains("OwnerId", idx.Expr!);
        Assert.Contains("ParentId", idx.Expr!);
        Assert.Contains("Name", idx.Expr!);
    }

    // ── catalog helpers (the BookmarkDocTypesDdlTests / InventoryDocTypesDdlTests shape) ──

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
    /// JSONB expression (the business-key witness). Returns null if no such
    /// index exists. The table is resolved via the <c>pg_class</c> /
    /// <c>pg_namespace</c> join (the repo's own index-query shape).
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
