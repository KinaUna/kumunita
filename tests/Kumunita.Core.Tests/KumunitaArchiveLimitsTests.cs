using System.IO;
using System.IO.Compression;
using Kumunita.Core.Portability;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// SEC-lane U13 (M2) — the <see cref="KumunitaArchive.ReadAsync"/>
/// zip-bomb guard tests. The four pinned names from the design doc
/// §2.3 (U13 row):
/// <list type="bullet">
/// <item><see cref="Rejects_TotalDecompressedOverLimit"/> — an entry whose decompressed size exceeds the per-entry or total limit throws <see cref="InvalidDataException"/>.</item>
/// <item><see cref="Rejects_EntryCountOverLimit"/> — more entries than <see cref="ArchiveLimits.MaxEntries"/> throws before any bytes are read.</item>
/// <item><see cref="Rejects_DuplicateEntryNames"/> — two entries with the same name throws.</item>
/// <item><see cref="NormalArchive_StillReads"/> — a small valid archive (the writer's own layout) reads end-to-end.</item>
/// </list>
/// All hostile fixtures are in-memory zips built in code
/// (unit-series rule 8 — no binary fixtures checked in).
/// </summary>
public sealed class KumunitaArchiveLimitsTests
{
    /// <summary>Builds a hostile zip in memory with one entry of the given name carrying the given compressed payload bytes.</summary>
    private static MemoryStream BuildZip(params (string Name, byte[] Bytes)[] entries)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var sink = entry.Open();
                sink.Write(bytes);
            }
        }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void Rejects_TotalDecompressedOverLimit()
    {
        // 256 KB of zero bytes compresses to a few hundred bytes, but
        // decompresses to 256 KB — over a 16 KB per-entry limit.
        var payload = new byte[256 * 1024];
        var archive = BuildZip(("media/aa/bbbbbbbb", payload));
        var limits = new ArchiveLimits(1_000, 16L * 1024, 512L * 1024 * 1024);

        var ex = Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            KumunitaArchive.ReadAsync(archive, limits: limits)).Result;
        Assert.Contains("16384", ex.Message);
    }

    [Fact]
    public void Rejects_EntryCountOverLimit()
    {
        // 4 entries against a limit of 2 — the count is checked before
        // any bytes are read (the entry count is the zip header, not a
        // per-entry stream).
        var archive = BuildZip(
            ("a1", new byte[] { 1 }),
            ("a2", new byte[] { 2 }),
            ("a3", new byte[] { 3 }),
            ("a4", new byte[] { 4 }));

        var ex = Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            KumunitaArchive.ReadAsync(archive, limits: new ArchiveLimits(2, 1L * 1024 * 1024, 1L * 1024 * 1024))).Result;
        Assert.Contains("4", ex.Message);
        Assert.Contains("2", ex.Message);
    }

    [Fact]
    public void Rejects_DuplicateEntryNames()
    {
        var archive = BuildZip(
            ("docs/Post.json", new byte[] { 1 }),
            ("docs/Post.json", new byte[] { 2 }));

        var ex = Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            KumunitaArchive.ReadAsync(archive, limits: new ArchiveLimits(100, 1L * 1024 * 1024, 1L * 1024 * 1024))).Result;
        Assert.Contains("duplicate", ex.Message);
        Assert.Contains("docs/Post.json", ex.Message);
    }

    [Fact]
    public void NormalArchive_StillReads()
    {
        // A genuine archive in the writer's own layout — manifest, one doc
        // type, one media object, principals, config — reads end-to-end.
        var manifest = new PortabilityManifest { CommunityName = "Test Community" };
        var manifestBytes = KumunitaArchive.ToJson(manifest);
        var docBytes = KumunitaArchive.ToJson(new[] { new { Id = "post-1", AuthorId = "user-a" } });
        var mediaId = "bbbbbbbb";
        var mediaBytes = new byte[] { 1, 2, 3, 4 };
        var principals = KumunitaArchive.ToJson(new[] { new PortabilityPrincipal { SubjectId = "user-a" } });
        var config = KumunitaArchive.ToJson(new PortabilityConfig());

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string name, byte[] bytes)
            {
                var sink = zip.CreateEntry(name).Open();
                sink.Write(bytes);
                sink.Dispose();
            }
            Add("manifest.json", manifestBytes);
            Add("docs/Post.json", docBytes);
            Add($"media/{mediaId[..2]}/{mediaId}", mediaBytes);
            Add("identity/principals.json", principals);
            Add("config.json", config);
        }
        ms.Position = 0;

        var data = KumunitaArchive.ReadAsync(ms).Result;

        Assert.NotNull(data.Manifest);
        Assert.Equal("Test Community", data.Manifest.CommunityName);
        Assert.True(data.Docs.ContainsKey("Post"));
        Assert.True(data.Media.ContainsKey(mediaId));
        Assert.Equal(mediaBytes, data.Media[mediaId]);
        Assert.NotEmpty(data.Principals);
        Assert.NotEmpty(data.Config);
    }
}
