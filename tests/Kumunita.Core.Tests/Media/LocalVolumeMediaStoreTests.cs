using Kumunita.Core.Media;
using Microsoft.Extensions.Options;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Media;

/// <summary>
/// Store-seam tests (media U3; design doc §2.5 pinned names) for
/// <see cref="LocalVolumeMediaStore"/> — the C-MED·4 dedup lane and the
/// C-MED·7 fail-closed read, tested against a <see cref="PostgresFixture"/>
/// <c>mt</c> schema over <see cref="MediaObject"/> (fresh scratch database
/// per test, the <c>AnnouncementServiceTests.BootStoreAsync</c> shape) + a
/// temp-dir <see cref="IMediaFileStore"/> (no live volume).
/// </summary>
public sealed class LocalVolumeMediaStoreTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>, IDisposable
{
    private static readonly List<string> Roots = [];

    private const string KnownHash =
        "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

    /// <summary>Removes every temp dir this test class created (recursive) —
    /// runs after all <c>await using</c> streams are closed, so Windows path
    /// deletion cannot wedge on a failing test.</summary>
    public void Dispose()
    {
        foreach (var root in Roots)
        {
            try { Directory.Delete(root, recursive: true); }
            catch { /* best effort */ }
        }
    }

    /// <summary>
    /// The dedup lane (C-MED·4): putting identical bytes a second time
    /// returns the <em>first</em> catalog doc — same id, the first actor's
    /// <c>CreatedById</c> — never a second volume file.
    /// </summary>
    [Fact]
    public async Task LocalVolumeMediaStore_Put_dedups_by_content_hash()
    {
        var store = await NewMediaStoreAsync();
        var content = new byte[] { 1, 2, 3, 250, 251 };

        var first = await store.PutAsync(content, "cat.png", "image/png", "actor-a");
        var second = await store.PutAsync(content, "cat-copy.png", "image/png", "actor-b");

        // Same content id (lowercase-hex SHA-256 of the payload).
        Assert.Equal(first.Id, second.Id);
        var expectedId = System.Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content)).ToLowerInvariant();
        Assert.Equal(expectedId, first.Id);

        // The first doc wins: the second put is a return, not an overwrite.
        Assert.Equal("actor-a", second.CreatedById);
        Assert.Equal(first.Created, second.Created);
        Assert.Equal("cat.png", second.Filename);

        // And the payload still round-trips from the single volume file.
        await using var stream = await store.OpenReadAsync(first.Id);
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    /// <summary>
    /// Serving a content id with no catalog doc fails closed with
    /// <see cref="KeyNotFoundException"/> before any file read is attempted
    /// (C-MED·7 — the doc is the surviving catalog reference).
    /// </summary>
    [Fact]
    public async Task LocalVolumeMediaStore_OpenRead_missing_doc_throws_KeyNotFound()
    {
        var store = await NewMediaStoreAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.OpenReadAsync(KnownHash));

        // And nothing was written behind that id (no orphan to serve).
        Assert.Null(await store.GetAsync(KnownHash));
    }

    /// <summary>
    /// A stored doc records the actor who first stored the unique file and
    /// the exact payload size (C-MED·4/8 metadata pin), and
    /// <see cref="IMediaStore.GetAsync"/> returns the catalog row.
    /// </summary>
    [Fact]
    public async Task LocalVolumeMediaStore_Put_sets_CreatedById_and_SizeBytes()
    {
        var store = await NewMediaStoreAsync();
        var content = new byte[321]; // non-trivial size
        for (var i = 0; i < content.Length; i++)
            content[i] = (byte)(i % 256);

        var doc = await store.PutAsync(content, "pic.jpg", "image/jpeg", "actor-x");
        var loaded = await store.GetAsync(doc.Id);

        Assert.NotNull(loaded);
        Assert.Equal("image/jpeg", doc.ContentType);
        Assert.Equal("pic.jpg", doc.Filename);
        Assert.Equal((long)content.Length, doc.SizeBytes);
        Assert.Equal("actor-x", doc.CreatedById);
        Assert.Equal("actor-x", loaded!.CreatedById);
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Boot a fresh <c>mt</c> schema over <see cref="MediaObject"/> in a new
    /// scratch database (the <see cref="PostgresFixture"/>, same shape as
    /// <c>AnnouncementServiceTests.BootStoreAsync</c>) and return the seam
    /// under test, composed over a temp-dir file store.
    /// </summary>
    private async Task<LocalVolumeMediaStore> NewMediaStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var docStore = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            // MediaObject's ADR 0004 §B.1 surface — the only doc this class
            // needs (no M1/M3 lanes under test here).
            MediaDocTypes.Configure(opts);
        });
        await docStore.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        var root = Path.Combine(Path.GetTempPath(), "kumunita-media-tests-" + Guid.NewGuid().ToString("n")[..10]);
        Roots.Add(root);
        var files = new LocalVolumeFileStore(Options.Create(new MediaOptions { RootPath = root }));
        return new LocalVolumeMediaStore(files, docStore);
    }
}
