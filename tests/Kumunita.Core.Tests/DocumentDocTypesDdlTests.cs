using System.Collections.Generic;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Documents;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The <c>M21</c> U01 pin (ADR 0122 D1 / D2, the design doc §4 + §5): the
/// <see cref="DocumentDocTypes"/> surface registers the <see cref="Document"/>
/// catalog row (conventional <c>string Id</c> — no non-default convention or
/// business-key index is pinned, the <c>UsageDocTypes</c> / <c>PageDocTypes</c>
/// parallel-surface shape) and the <see cref="DocumentToAuditableResource"/>
/// adapter presents the frozen 6-member <see cref="IAuditableResource"/> surface
/// with the **exact** <c>TargetKind</c> (<c>"document"</c> — the
/// <c>AccessAudit.TargetKind</c> discriminator, C-M21·3) and <c>ComponentId</c>
/// <c>null</c> (a document is repository-level, never component-scoped,
/// C-M21·2/C-M21·3). Verified against the *live* Postgres catalog (the
/// <c>M16DocTypesDdlTests</c> / <c>M17DocTypesDdlTests</c> shape), not the
/// in-memory object graph. The test name is a doc-registration shape pin
/// (outside the design doc's §9 gate list — the M16/U01 precedent; GATE-4 is
/// the zero-new-authorization-surface pin this unit's adapter upholds).
/// </summary>
public class DocumentDocTypesDdlTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private string _connection = "";

    public async ValueTask InitializeAsync() => _connection = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task DocumentDocTypes_RegistersDocument_CatalogRow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            // The sibling harnesses (M16DocTypesDdlTests / M17DocTypesDdlTests)
            // add the two storage features before a *DocTypes surface — that is
            // the established shape that actually applies the document DDL.
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            DocumentDocTypes.Configure(opts);
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // ── Force the document's schema to materialize (the M16/M17/M9/M5
        // doc-shape test shape): in this stack document DDL is applied lazily
        // on first save — a bare ApplyAllConfiguredChangesToDatabaseAsync only
        // applies the storage features + Weasel migrations, so the sibling
        // tests round-trip-store their docs first. Store + load the Document so
        // mt."mt_doc_document" exists, then inspect the live catalog.
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new Document
            {
                Id = "doc-1",
                Title = "Building bylaws",
                Summary = "The adopted bylaws, rev. 2",
                MediaId = "deadbeef",
                Filename = "bylaws.pdf",
                ContentType = "application/pdf",
                SizeBytes = 12345,
                Audience = new Audience(),
                OwnerId = "u-uploader",
                Created = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
                Modified = null,
            });
            await w.SaveChangesAsync(ct);
        }
        await using (var q = store.QuerySession())
        {
            var doc = (await q.LoadAsync<Document>("doc-1", ct))!;
            Assert.Equal("Building bylaws", doc.Title);
            Assert.Equal("bylaws.pdf", doc.Filename);
            Assert.Equal("application/pdf", doc.ContentType);
            Assert.Equal("u-uploader", doc.OwnerId);
            Assert.Null(doc.Modified);
        }

        // ── The Document catalog row registered. Marten's default doc-table
        // convention is the <c>mt_doc_</c> prefix + lowercase class name, so
        // the table is mt.mt_doc_document. No business-key index is asserted —
        // the conventional string Id means Marten's defaults apply (D1, the
        // UsageDocTypes / PageDocTypes parallel-surface shape).
        var cols = await QueryColumnsAsync("mt_doc_document");
        Assert.True(cols.Count > 0, "mt_doc_document table not found in mt");
        Assert.Contains("id", cols);
        Assert.Contains("data", cols);
    }

    [Fact]
    public void Adapter_Presents_Frozen6MemberSurface_With_DocumentTargetKind()
    {
        var doc = new Document
        {
            Id = "doc-1",
            Title = "Building bylaws",
            Summary = "The adopted bylaws, rev. 2",
            MediaId = "deadbeef",
            Filename = "bylaws.pdf",
            ContentType = "application/pdf",
            SizeBytes = 12345,
            Audience = new Audience(),
            OwnerId = "u-uploader",
            Created = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
        };

        // The **exact** 6-member IAuditableResource projection — the only
        // M21 differences vs the PostToAuditableResource shape are ComponentId
        // (null — a document is repository-level, never component-scoped) and
        // the TargetKind string ("document", C-M21·3 / GATE-5).
        IAuditableResource adapter = new DocumentToAuditableResource(doc);
        Assert.Equal("doc-1", adapter.Id);
        Assert.Equal("Building bylaws", adapter.Name);   // = Title (the audit row's label)
        Assert.Equal("u-uploader", adapter.OwnerId);     // the owner branch (C-M21·1)
        Assert.NotNull(adapter.Audience);                // projected verbatim (ADR 0001-B)
        Assert.Null(adapter.ComponentId);                // repository-level (C-M21·2/C-M21·3)
        Assert.Equal("document", adapter.TargetKind);    // the EXACT string (C-M21·3 / GATE-5)

        // The adapter never mutates the document's audience (ADR 0001-B): the
        // same Audience instance is projected by reference, not copied.
        var audience = doc.Audience;
        audience.Grants.Add(new AudienceGrant(GrantKind.User, "u-grantee"));
        Assert.Same(audience, adapter.Audience);
        Assert.Contains(new AudienceGrant(GrantKind.User, "u-grantee"), adapter.Audience.Grants);
    }

    [Fact]
    public async Task DocumentDocTypes_RegistersDocumentFolder_CatalogRow()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            DocumentDocTypes.Configure(opts);
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        // Store + load a DocumentFolder so mt."mt_doc_documentfolder" exists
        // (document DDL is applied lazily on first save — the M16/M17 shape).
        await using (var w = store.OpenSession(new Marten.Services.SessionOptions()))
        {
            w.Store(new DocumentFolder
            {
                Id = "folder-1",
                ParentId = null,
                Name = "Contracts",
                OwnerId = "u-admin",
                Created = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
                Modified = null,
            });
            await w.SaveChangesAsync(ct);
        }
        await using (var q = store.QuerySession())
        {
            var folder = (await q.LoadAsync<DocumentFolder>("folder-1", ct))!;
            Assert.Equal("Contracts", folder.Name);
            Assert.Null(folder.ParentId);
            Assert.Equal("u-admin", folder.OwnerId);
            Assert.Null(folder.Modified);
        }

        var cols = await QueryColumnsAsync("mt_doc_documentfolder");
        Assert.True(cols.Count > 0, "mt_doc_documentfolder table not found in mt");
        Assert.Contains("id", cols);
        Assert.Contains("data", cols);
    }

    [Fact]
    public async Task Document_CarriesTagIdsAndFolderId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var store = DocumentStore.For(opts =>
        {
            opts.Connection(_connection);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            DocumentDocTypes.Configure(opts);
        });

        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Document
        {
            Id = "doc-tags-folders",
            Title = "Bylaws with organization",
            MediaId = "deadbeef",
            ContentType = "application/pdf",
            SizeBytes = 1,
            Audience = new Audience(),
            OwnerId = "u-uploader",
            Created = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            TagIds = new List<string> { "t1", "t2" },
            FolderId = "folder-1",
        });
        await w.SaveChangesAsync(ct);

        await using var q = store.QuerySession();
        var doc = (await q.LoadAsync<Document>("doc-tags-folders", ct))!;
        Assert.Equal(new[] { "t1", "t2" }, doc.TagIds);
        Assert.Equal("folder-1", doc.FolderId);
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
}
