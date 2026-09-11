using Kumunita.Core.Media;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kumunita.Core.Tests.Media;

/// <summary>
/// Byte-store seam tests (media U3; design doc §2.5 pinned names) for
/// <see cref="LocalVolumeFileStore"/> — the C-MED·4 atomic/idempotent write
/// path and the C-MED·7 fail-closed read/delete behaviour, tested on a
/// **temp dir** (<c>MediaOptions.RootPath</c>), no live volume.
/// </summary>
public class LocalVolumeFileStoreTests : IDisposable
{
    private static readonly List<string> Roots = [];

    private const string ContentId =
        "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

    /// <summary>Removes every temp dir this test class created (recursive),
    /// so a failing test cannot wedge Windows path deletion. Streams are
    /// closed by the tests' <c>await using</c> before dispose runs.</summary>
    public void Dispose()
    {
        foreach (var root in Roots)
        {
            try { Directory.Delete(root, recursive: true); }
            catch { /* best effort — an open stream would be a test bug, not a cleanup bug */ }
        }
    }

    /// <summary>
    /// <see cref="IMediaFileStore.PutAsync"/> then <see cref="IMediaFileStore.OpenReadAsync"/>
    /// round-trips the exact bytes, and the payload lands at the sharded path
    /// <c>{RootPath}/{id[0..2]}/{id}</c> (the design doc §2.2 path contract).
    /// </summary>
    [Fact]
    public async Task LocalVolumeFileStore_Put_open_read_roundtrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var (opts, store) = NewStoreInTempDir();
        var content = new byte[] { 0, 1, 2, 255, 254, 253, 42 };

        await store.PutAsync(ContentId, content, ct);

        Assert.True(await store.ExistsAsync(ContentId, ct));
        await using var stream = await store.OpenReadAsync(ContentId, ct);
        Assert.Equal(content, await ReadAllAsync(stream));

        // Sharding: the payload is at {RootPath}/{id[0..2]}/{id}, never a
        // filename on the path (C-MED·3).
        Assert.True(File.Exists(Path.Combine(opts.RootPath, "a1", ContentId)));
    }

    /// <summary>
    /// A second <see cref="IMediaFileStore.PutAsync"/> for the same content
    /// id with the same bytes is an idempotent rewrite that leaves exactly
    /// one payload file and no <c>.tmp</c> litter (C-MED·4: the content id
    /// *is* the SHA-256 of the payload, so same id ⇒ same bytes; U1's
    /// atomic tmp-rename write is the guarantee).
    /// </summary>
    [Fact]
    public async Task LocalVolumeFileStore_Put_idempotent_same_id_no_second_writer()
    {
        var ct = TestContext.Current.CancellationToken;
        var (opts, store) = NewStoreInTempDir();
        var content = new byte[] { 7, 7, 7, 7 };

        await store.PutAsync(ContentId, content, ct);
        await store.PutAsync(ContentId, content, ct); // same id + same bytes again

        await using var stream = await store.OpenReadAsync(ContentId, ct);
        Assert.Equal(content, await ReadAllAsync(stream));

        // No writer litter in the shard dir: exactly one file (the payload).
        var files = Directory.GetFiles(Path.Combine(opts.RootPath, "a1"));
        Assert.Single(files);
        Assert.DoesNotContain(files, f => f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reading a payload that was never stored fails closed with
    /// <see cref="FileNotFoundException"/> (C-MED·7 — the file store never
    /// serves a path that was not written through the seam).
    /// </summary>
    [Fact]
    public async Task LocalVolumeFileStore_OpenRead_missing_throws_FileNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, store) = NewStoreInTempDir();

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadAsync(ContentId, ct));
    }

    /// <summary>
    /// Deleting a payload that was never stored fails closed with
    /// <see cref="FileNotFoundException"/> (C-MED·7 orphan-safe: a bare hash
    /// with no file is nothing to delete).
    /// </summary>
    [Fact]
    public async Task LocalVolumeFileStore_Delete_missing_throws_FileNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (_, store) = NewStoreInTempDir();

        await Assert.ThrowsAsync<FileNotFoundException>(() => store.DeleteFileAsync(ContentId, ct));
    }

    // ── Shared helpers ─────────────────────────────────────────────────────

    /// <summary>Fresh temp-dir <see cref="MediaOptions"/> + file store; the
    /// directory is removed (recursive) on dispose — close any open streams
    /// first so Windows path deletion cannot wedge.</summary>
    private static (MediaOptions Opts, LocalVolumeFileStore Store) NewStoreInTempDir()
    {
        var root = Path.Combine(Path.GetTempPath(), "kumunita-media-tests-" + Guid.NewGuid().ToString("n")[..10]);
        Roots.Add(root);
        var opts = new MediaOptions { RootPath = root };
        return (opts, new LocalVolumeFileStore(Options.Create(opts)));
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }
}
