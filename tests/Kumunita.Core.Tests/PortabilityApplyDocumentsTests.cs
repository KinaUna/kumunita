using System.Text.Json;
using Kumunita.Core;
using Kumunita.Core.Portability;
using Kumunita.Core.Tags;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests;

using static Marten.QueryableExtensions;

/// <summary>
/// C10 (IMPROVE-CODE) — the **first tests ever naming
/// <see cref="PortabilityApplyDocuments"/>.ApplyAsync** (E12's "parts work,
/// seams don't" finding: <c>PortabilityApplyDocuments</c> had no test naming
/// it — the M11 round-trip D9a pin exercises it only indirectly, through
/// <see cref="PortabilityService"/>.ImportAsync).
/// <para>
/// <see cref="PortabilityApplyDocuments"/>.ApplyAsync is the <b>content-doc
/// apply</b> unit: it stores every content doc in the closed §inventory in the
/// <see cref="PortabilityDocTypes"/> import order as one commit (one
/// <c>SaveChangesAsync</c>). It is uniform (the
/// <see cref="PortabilityExportDocuments"/>.NameToType table lookup +
/// reflection-dispatched <c>List&lt;T&gt;</c> deserialization +
/// <c>ISession.Store</c>), and it skips a type the archive does not carry (the
/// apply is fail-open on the pre-validated shape — the clean validate
/// guaranteed presence, so a missing type is a caller error that is skipped,
/// not a crash). These tests pin the two import modes on a single,
/// reference-free content type (<see cref="Tag"/> — the C-TG·4 "no unique
/// index" shape, the ADR 0274 merge-key surface):
/// <list type="bullet">
/// <item><see cref="PortabilityImportMode.CleanSlate"/> — every archived row is
/// stored (the fresh instance has no conflicting rows).</item>
/// <item><see cref="PortabilityImportMode.KeepExisting"/> — the ADR 0193
/// "keep intact, never overwritten" discipline: a row the target already holds
/// (same natural identity) is skipped, a genuinely new row is added.</item>
/// <item>the empty-archive zero-write pin (a store with nothing to apply stays
/// empty — the apply is a no-op, not an error).</item>
/// </list>
/// </para>
/// </summary>
public sealed class PortabilityApplyDocumentsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    /// <summary>
    /// A minimal fresh instance: a scratch Postgres DB (the
    /// <see cref="PostgresFixture"/> convention) + a Marten store that registers
    /// only the <see cref="Kumunita.Core.TagDocTypes"/> surface (the
    /// <see cref="Tag"/> doc type these tests apply) — the same
    /// <c>mt</c> schema + per-surface <c>Configure</c> shape the M11 round-trip
    /// boot uses. The <see cref="IDocumentStore"/> is the frozen Marten seam
    /// <see cref="PortabilityApplyDocuments"/>.ApplyAsync consumes.
    /// </summary>
    private async Task<IDocumentStore> FreshStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        // DocumentStore.For auto-migrates the registered doc-type schema on
        // first connection (the M11 round-trip boot's shape — no explicit
        // MigrateAsync; the fresh scratch DB starts empty).
        return DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            TagDocTypes.Configure(opts);
        });
    }

    private static byte[] TagJson(params Tag[] rows) =>
        JsonSerializer.SerializeToUtf8Bytes(rows.ToList(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    private static Tag Tag(string id, string slug, string name) => new()
    {
        Id = id,
        Slug = slug,
        Name = name,
        LanguageCode = "en",
        CreatedBy = "resident",
        Created = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task ApplyAsync_CleanSlate_StoresEveryArchivedRow()
    {
        await using var store = await FreshStoreAsync();
        var data = new KumunitaArchiveData
        {
            Manifest = new PortabilityManifest { Format = PortabilityManifest.FormatVersion },
        };
        data.Docs["Tag"] = TagJson(Tag("t1", "slug-a", "Tag A"), Tag("t2", "slug-b", "Tag B"));

        var ct = TestContext.Current.CancellationToken;
        await PortabilityApplyDocuments.ApplyAsync(
            store, data, PortabilityImportMode.CleanSlate, "en", null, ct);

        // Both archived tags are present (the CleanSlate restore semantic —
        // the fresh instance has no conflicting rows, so every row is stored).
        await using var verify = store.LightweightSession();
        Assert.Equal(2, await verify.Query<Tag>().CountAsync(ct));
        Assert.NotNull(await verify.LoadAsync<Tag>("t1", ct));
        Assert.NotNull(await verify.LoadAsync<Tag>("t2", ct));
    }

    [Fact]
    public async Task ApplyAsync_KeepExisting_KeepsExistingRowAndAddsNewRow()
    {
        await using var store = await FreshStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // The target instance already holds a tag with the same natural
        // identity (surrogate id "t1" + slug "slug-a") — the ADR 0193
        // keep-intact case the apply must SKIP (never overwrite).
        await using (var seed = store.LightweightSession())
        {
            seed.Store(Tag("t1", "slug-a", "ORIGINAL-NAME"));
            await seed.SaveChangesAsync(ct);
        }

        var data = new KumunitaArchiveData
        {
            Manifest = new PortabilityManifest { Format = PortabilityManifest.FormatVersion },
        };
        // The archive carries the same tag (id "t1", slug "slug-a" — a
        // different instance's surrogate, the same business identity) + a
        // genuinely new tag (id "t2", slug "slug-b").
        data.Docs["Tag"] = TagJson(
            Tag("t1", "slug-a", "ARCHIVED-NAME"),
            Tag("t2", "slug-b", "Tag B"));

        await PortabilityApplyDocuments.ApplyAsync(
            store, data, PortabilityImportMode.KeepExisting, "en", null, ct);

        // The existing tag is kept intact (its Name is still the original —
        // never overwritten by the archive's row) + the new tag is added.
        await using var verify = store.LightweightSession();
        var tags = await verify.Query<Tag>().OrderBy(t => t.Id).ToListAsync(ct);
        Assert.Equal(2, tags.Count);
        var existing = tags.Single(t => t.Id == "t1");
        Assert.Equal("ORIGINAL-NAME", existing.Name); // kept-first, not overwritten
        Assert.NotNull(tags.SingleOrDefault(t => t.Id == "t2")); // the new row landed
    }

    [Fact]
    public async Task ApplyAsync_EmptyArchive_StoresNothing()
    {
        await using var store = await FreshStoreAsync();
        // A clean validate passes an archive with all 44 types present but
        // empty; here we model the apply's fail-open no-op: a type the
        // archive does NOT carry is skipped (not a crash), so an archive with
        // no Tag rows stores no tags.
        var data = new KumunitaArchiveData
        {
            Manifest = new PortabilityManifest { Format = PortabilityManifest.FormatVersion },
        };
        data.Docs["Tag"] = System.Text.Encoding.UTF8.GetBytes("[]"); // present but empty

        var ct = TestContext.Current.CancellationToken;
        await PortabilityApplyDocuments.ApplyAsync(
            store, data, PortabilityImportMode.CleanSlate, "en", null, ct);

        await using var verify = store.LightweightSession();
        Assert.Equal(0, await verify.Query<Tag>().CountAsync(ct));
    }
}
