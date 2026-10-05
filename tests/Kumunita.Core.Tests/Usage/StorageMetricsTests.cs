using Kumunita.Core;
using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Usage;

/// <summary>
/// The 10 pinned M24 Core read-shape tests for <see cref="StorageMetricsService"/>
/// (design doc §2.4, verbatim names). <see cref="PostgresFixture"/> harness
/// (Testcontainers <c>postgres:18</c>), the <c>LocalVolumeMediaStoreTests</c>
/// <c>mt</c> schema over <see cref="MediaObject"/> (fresh scratch database per
/// test) + a <see cref="FakeVolume"/> test double for the two
/// <c>IMediaFileStore</c> volume-stat reads (C-SM·2/4 — a filesystem stat, not
/// a Postgres query). The service is read-only (C-SM·2); these assert the
/// *results* (the design doc shapes), not the SQL emitted.
/// </summary>
public sealed class StorageMetricsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static int _plantCounter;

    // ── Pin 1 ─────────────────────────────────────────────────────────────
    // The community total is Σ MediaObject.SizeBytes over the whole catalog
    // (C-SM·2/4). The unknown-bucket row contributes to it too.

    [Fact]
    public async Task GetSnapshot_TotalUsed_SumsAllMediaObjects()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000));

        await PlantMedia(store, sizeBytes: 100, createdById: "u1");
        await PlantMedia(store, sizeBytes: 200, createdById: "u2");
        await PlantMedia(store, sizeBytes: 50,  createdById: null);

        var snap = await svc.GetSnapshotAsync();

        Assert.Equal(350, snap.TotalUsedBytes);
        await store.DisposeAsync();
    }

    // ── Pin 2 ─────────────────────────────────────────────────────────────
    // UserContentUsedBytes == TotalUsedBytes by design (C-SM·2/4) — M24 counts
    // the whole catalog as user content (the "orphan file" caveat is a named
    // non-decision).

    [Fact]
    public async Task GetSnapshot_UserContentUsed_Equals_TotalUsed()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000));

        await PlantMedia(store, sizeBytes: 120, createdById: "u1");
        await PlantMedia(store, sizeBytes: 30,  createdById: "u2");

        var snap = await svc.GetSnapshotAsync();

        Assert.Equal(snap.TotalUsedBytes, snap.UserContentUsedBytes);
        Assert.Equal(150, snap.UserContentUsedBytes);
        await store.DisposeAsync();
    }

    // ── Pin 3 ─────────────────────────────────────────────────────────────
    // NAMED ASSERTION (C-SM·4 / F7): the two volume totals are read from the
    // IMediaFileStore seam (a DriveInfo/statvfs filesystem stat), NOT from the
    // QuerySession — a Postgres query could not produce these canned values.
    // The catalog bytes are deliberately far from them, so the match is proof
    // the value came from the volume seam, not the catalog.

    [Fact]
    public async Task GetSnapshot_VolumeTotals_AreNotInQuerySession()
    {
        const long fakeTotal = 9_876_543;   // a value no catalog sum could produce
        const long fakeFree  = 5_432_109;
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: fakeTotal, free: fakeFree));

        await PlantMedia(store, sizeBytes: 100, createdById: "u1");
        await PlantMedia(store, sizeBytes: 200, createdById: "u2");

        var snap = await svc.GetSnapshotAsync();

        // The volume totals are exactly the fake volume's canned values (read
        // from IMediaFileStore, not the session), and are NOT the catalog sum
        // (300) or a function of it.
        Assert.Equal(fakeTotal, snap.TotalVolumeBytes);
        Assert.Equal(fakeFree,  snap.FreeVolumeBytes);
        Assert.NotEqual(300, snap.TotalVolumeBytes);
        await store.DisposeAsync();
    }

    // ── Pin 4 ─────────────────────────────────────────────────────────────
    // The per-user table is paged (the M7 HasMore discipline) and sorted
    // descending by bytes used (C-SM·4 / F4).

    [Fact]
    public async Task GetPerUserList_PagesAndSortsByBytesDesc()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000));

        await PlantMedia(store, sizeBytes: 50,  createdById: "u1");
        await PlantMedia(store, sizeBytes: 300, createdById: "u2");
        await PlantMedia(store, sizeBytes: 100, createdById: "u3");
        await PlantMedia(store, sizeBytes: 200, createdById: "u4");

        var page1 = await svc.GetPerUserListAsync(page: 1, pageSize: 2);

        Assert.Equal(4, page1.TotalUsers);
        Assert.True(page1.HasMore);
        Assert.Equal(2, page1.Items.Count);
        // Descending by bytes: u2 (300) then u4 (200).
        Assert.Equal(300, page1.Items[0].Bytes);
        Assert.Equal(200, page1.Items[1].Bytes);

        var page2 = await svc.GetPerUserListAsync(page: 2, pageSize: 2);
        Assert.False(page2.HasMore);
        Assert.Equal(100, page2.Items[0].Bytes);
        Assert.Equal(50,  page2.Items[1].Bytes);
        await store.DisposeAsync();
    }

    // ── Pin 5 ─────────────────────────────────────────────────────────────
    // CreatedById of null AND "" both fold into the single "unknown / not
    // captured" bucket (C-SM·5 / F5) — the two sentinel forms are grouped into
    // one row with summed bytes, never two separate rows.

    [Fact]
    public async Task PerUser_UnknownBucketGrouped()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000));

        await PlantMedia(store, sizeBytes: 60, createdById: null); // null sentinel
        await PlantMedia(store, sizeBytes: 40, createdById: "");   // "" sentinel
        await PlantMedia(store, sizeBytes: 50, createdById: "u1"); // a known user

        var page = await svc.GetPerUserListAsync(page: 1, pageSize: 10);

        // null + "" merged into ONE row; its bytes are the grouped sum (60 + 40).
        var unknown = Assert.Single(page.Items, r => r.CreatedById is null);
        Assert.Equal(100, unknown.Bytes);
        Assert.Equal(2, page.TotalUsers);       // one unknown row + one known row
        await store.DisposeAsync();
    }

    // ── Pin 6 ─────────────────────────────────────────────────────────────
    // SizeBytes == 0 rows are excluded from the per-user table (C-SM·5 / F6) —
    // a catalog row with no payload contributes nothing, and a user whose only
    // row is zero-byte does not appear at all.

    [Fact]
    public async Task PerUser_ZeroSizeRowsExcluded()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000));

        await PlantMedia(store, sizeBytes: 0,   createdById: "ghost"); // only a zero-byte row
        await PlantMedia(store, sizeBytes: 100, createdById: "real");  // a real row

        var page = await svc.GetPerUserListAsync(page: 1, pageSize: 10);

        // "ghost" (only a zero-byte row) is excluded entirely; "real" remains.
        Assert.Single(page.Items);
        Assert.Equal("real", page.Items[0].CreatedById);
        Assert.Equal(100, page.Items[0].Bytes);
        Assert.Equal(1, page.TotalUsers);
        await store.DisposeAsync();
    }

    // ── Pin 7 ─────────────────────────────────────────────────────────────
    // The C-SM·7 handoff seam (F10): GetPerUserUsageBytesAsync returns
    // Σ SizeBytes WHERE CreatedById == subjectId — only that subject's bytes,
    // never another subject's.

    [Fact]
    public async Task GetPerUserUsage_ReturnsOnlySubjectBytes()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000));

        await PlantMedia(store, sizeBytes: 100, createdById: "alice");
        await PlantMedia(store, sizeBytes: 50,  createdById: "alice");
        await PlantMedia(store, sizeBytes: 999, createdById: "bob");

        Assert.Equal(150, await svc.GetPerUserUsageBytesAsync("alice"));
        Assert.Equal(999, await svc.GetPerUserUsageBytesAsync("bob"));
        await store.DisposeAsync();
    }

    // ── Pin 8 ─────────────────────────────────────────────────────────────
    // The C-SM·7 handoff seam for a subject with no rows: Σ over the empty
    // set is 0 (never a crash, never another subject's bytes).

    [Fact]
    public async Task GetPerUserUsage_ReturnsZeroForUnknownSubject()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000));

        await PlantMedia(store, sizeBytes: 100, createdById: "alice");

        Assert.Equal(0, await svc.GetPerUserUsageBytesAsync("nobody"));
        await store.DisposeAsync();
    }

    // ── Pin 9 ─────────────────────────────────────────────────────────────
    // An empty catalog returns zero metrics (C-SM·2) — no crash, all
    // catalog-derived metrics are zero.

    [Fact]
    public async Task GetSnapshot_ZeroRows_ReturnsZeroMetrics()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 0, free: 0));

        var snap = await svc.GetSnapshotAsync();

        Assert.Equal(0, snap.TotalUsedBytes);
        Assert.Equal(0, snap.UserContentUsedBytes);
        Assert.Equal(0, snap.TotalUniqueFiles);
        Assert.Equal(0, snap.TotalDistinctUsers);
        await store.DisposeAsync();
    }

    // ── Pin 10 ────────────────────────────────────────────────────────────
    // The "unknown" bucket is exactly ONE row on the per-user table (C-SM·5 /
    // F5) — N null-created rows collapse to a single row, never N rows.

    [Fact]
    public async Task GetPerUserList_UnknownBucketIsOneRow()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000));

        // Five distinct null-created rows — all one "unknown" bucket.
        foreach (var b in new[] { 10, 20, 30, 40, 50 })
            await PlantMedia(store, sizeBytes: b, createdById: null);
        await PlantMedia(store, sizeBytes: 100, createdById: "u1");

        var page = await svc.GetPerUserListAsync(page: 1, pageSize: 10);

        // 5 distinct null-created rows collapse to exactly ONE "unknown" row.
        Assert.Single(page.Items, r => r.CreatedById is null);
        Assert.Equal(2, page.TotalUsers);                            // unknown + u1
        await store.DisposeAsync();
    }

    // ── Platform-space decision input (the platform-limit lane) ────────────
    // GetPlatformSpaceAsync reports IsFull = (limit reached) OR (free space
    // below the 100 MiB floor). These pin the four combinations of those two
    // triggers. The floor constant is MediaOptions.MinFreeSpaceFloor (100 MiB).

    // (a) limit unlimited (0) + free space comfortably above the floor → not full.
    [Fact]
    public async Task GetPlatformSpace_Unlimited_AboveFloor_NotFull()
    {
        var store = await BootStoreAsync();
        // Plant some content — with no limit it is irrelevant to IsFull.
        await PlantMedia(store, sizeBytes: 100, createdById: "u1");
        var svc = new StorageMetricsService(
            store, new FakeVolume(total: 1_000_000, free: MediaOptions.MinFreeSpaceFloor + 1_000));

        var sp = await svc.GetPlatformSpaceAsync(platformLimitBytes: 0);

        Assert.False(sp.IsFull);
        Assert.Equal(0, sp.LimitBytes);
        Assert.Equal(MediaOptions.MinFreeSpaceFloor + 1_000, sp.FreeVolumeBytes);
        await store.DisposeAsync();
    }

    // (b) limit unlimited (0) + free space below the 100 MiB floor → full,
    //     even though no limit was set (the physical-safety-margin trigger).
    [Fact]
    public async Task GetPlatformSpace_Unlimited_BelowFloor_Full()
    {
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(
            store, new FakeVolume(total: 1_000_000, free: MediaOptions.MinFreeSpaceFloor - 1_000));

        var sp = await svc.GetPlatformSpaceAsync(platformLimitBytes: 0);

        Assert.True(sp.IsFull);
        Assert.Equal(MediaOptions.MinFreeSpaceFloor - 1_000, sp.FreeVolumeBytes);
        await store.DisposeAsync();
    }

    // (c) limit set + used space at/above the limit → full (limit trigger wins
    //     even though free space is still above the floor).
    [Fact]
    public async Task GetPlatformSpace_LimitReached_Full()
    {
        var store = await BootStoreAsync();
        await PlantMedia(store, sizeBytes: 600, createdById: "u1");
        await PlantMedia(store, sizeBytes: 400, createdById: "u2");   // Σ = 1000
        var svc = new StorageMetricsService(
            store, new FakeVolume(total: 1_000_000, free: MediaOptions.MinFreeSpaceFloor + 10_000));

        // used (1000) == limit (1000) → "at or above" → full.
        var sp = await svc.GetPlatformSpaceAsync(platformLimitBytes: 1000);

        Assert.True(sp.IsFull);
        Assert.Equal(1000, sp.UsedBytes);
        Assert.Equal(1000, sp.LimitBytes);
        await store.DisposeAsync();
    }

    // (d) limit set + used below limit + free above floor → not full (the
    //     happy path: within budget, room to spare).
    [Fact]
    public async Task GetPlatformSpace_WithinLimit_AboveFloor_NotFull()
    {
        var store = await BootStoreAsync();
        await PlantMedia(store, sizeBytes: 100, createdById: "u1");
        var svc = new StorageMetricsService(
            store, new FakeVolume(total: 1_000_000, free: MediaOptions.MinFreeSpaceFloor + 10_000));

        var sp = await svc.GetPlatformSpaceAsync(platformLimitBytes: 500);   // 100 < 500

        Assert.False(sp.IsFull);
        Assert.Equal(100, sp.UsedBytes);
        Assert.Equal(500, sp.LimitBytes);
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh <c>mt</c> schema over <see cref="MediaObject"/> in
    /// a new scratch database (the <c>LocalVolumeMediaStoreTests.
    /// NewMediaStoreAsync</c> shape) — the only surface these tests need.</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            MediaDocTypes.Configure(opts); // MediaObject's ADR 0004 §B.1 surface
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>Plant a <see cref="MediaObject"/> catalog row (a write session —
    /// the test setup, not the service under test). <paramref name="createdById"/>
    /// may be <c>null</c> or <c>""</c> to seed the "unknown" bucket (C-SM·5).</summary>
    private static async Task PlantMedia(IDocumentStore store, long sizeBytes, string? createdById)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.LightweightSession();
        w.Store(new MediaObject
        {
            Id = $"sm-{Interlocked.Increment(ref _plantCounter)}-{Guid.NewGuid():N}"[..20],
            Filename = "seed.bin",
            ContentType = "application/octet-stream",
            SizeBytes = sizeBytes,
            Created = DateTimeOffset.UtcNow,
            CreatedById = createdById
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>A read-only <see cref="IMediaFileStore"/> test double: the two
    /// volume-stat reads return canned values (the filesystem-stat seam, C-SM·2/4);
    /// the byte-I/O members are unreachable here and throw if mistaken for a volume
    /// read. This is what pins the "volume totals are not in the QuerySession"
    /// assertion (pin 3) — the catalog cannot produce the canned values.</summary>
    private sealed class FakeVolume(long total, long free) : IMediaFileStore
    {
        public Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default) => Task.FromResult(total);
        public Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default) => Task.FromResult(free);
        public string RootPath => "";
        public Task PutAsync(string contentId, byte[] content, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only (C-SM·2).");
        public Task<bool> ExistsAsync(string contentId, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<Stream> OpenReadAsync(string contentId, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only (C-SM·2).");
        public Task DeleteFileAsync(string contentId, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only (C-SM·2).");
    }
}
