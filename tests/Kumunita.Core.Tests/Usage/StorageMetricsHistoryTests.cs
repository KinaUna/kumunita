using Kumunita.Core;
using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Usage;

/// <summary>
/// The 7 pinned M33 Core tests (design doc §2.4, verbatim names) — the
/// <see cref="StorageMetricsCaptureService"/> write half (one sample per UTC
/// day + the retention purge) and the <see cref="StorageMetricsService"/>
/// <c>GetHistoryAsync</c> read half. <see cref="PostgresFixture"/> harness
/// (Testcontainers <c>postgres:18</c>), a fresh scratch database per test,
/// the <see cref="StorageMetricsTests"/> (M24) <c>BootStoreAsync</c> +
/// <c>FakeVolume</c> shape. The service is the code under test (U03/U04);
/// these pin the *results* (the design doc §2.2 shapes), not the SQL emitted.
/// </summary>
public sealed class StorageMetricsHistoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static int _mediaCounter;

    // ── Pin 1 ─────────────────────────────────────────────────────────────
    // M33·2 (FACES M33-4): a single capture run stores exactly ONE
    // StorageMetricsSample row for the current UTC day, with the
    // deterministic "smh-" + yyyy-MM-dd Id.

    [Fact]
    public async Task M33_2_Capture_Stores_One_Sample_Per_Day()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        var now = DateTimeOffset.UtcNow;

        await StorageMetricsCaptureService.CaptureAndPurgeAsync(store, svc, now);

        var rows = await AllSamples(store);
        var expectedId = "smh-" + now.ToUniversalTime().Date.ToString("yyyy-MM-dd");
        Assert.Single(rows);
        Assert.Equal(expectedId, rows[0].Id);
        await store.DisposeAsync();
    }

    // ── Pin 2 ─────────────────────────────────────────────────────────────
    // M33·2 idempotent-by-construction (FACES M33-5): the capture run TWICE
    // the same day stores the SAME single row — the deterministic Id
    // (the day) overwrites, so there is exactly one row, never two.

    [Fact]
    public async Task M33_5_Capture_SameDayTwice_Dedups()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        var now = DateTimeOffset.UtcNow;

        // Two runs for the same UTC day — both resolve to the same
        // deterministic Id, so the second run overwrites the first.
        await StorageMetricsCaptureService.CaptureAndPurgeAsync(store, svc, now);
        await StorageMetricsCaptureService.CaptureAndPurgeAsync(store, svc, now);

        var rows = await AllSamples(store);
        var expectedId = "smh-" + now.ToUniversalTime().Date.ToString("yyyy-MM-dd");
        Assert.Single(rows);
        Assert.Equal(expectedId, rows[0].Id);
        await store.DisposeAsync();
    }

    // ── Pin 3 ─────────────────────────────────────────────────────────────
    // M33·4 (FACES M33-6): the capture writes ZERO AccessAudit rows — the
    // sample is platform telemetry, not an auditable resource (the M13
    // C-M13·6 "telemetry is not an auditable resource" + the M24 C-SM·6
    // "read = no row" discipline). A fresh store has zero audit rows, and the
    // capture must not add any.

    [Fact]
    public async Task M33_4_Capture_Writes_No_AuditRow()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        var now = DateTimeOffset.UtcNow;
        await StorageMetricsCaptureService.CaptureAndPurgeAsync(store, svc, now);

        // The capture stores exactly one sample (sanity) and ZERO audit rows.
        Assert.Single(await AllSamples(store));
        Assert.Equal(0, await AuditCount(store));
        await store.DisposeAsync();
    }

    // ── Pin 4 ─────────────────────────────────────────────────────────────
    // M33·7 (FACES M33-4): the capture purges StorageMetricsSample rows older
    // than RetentionDays (365) and keeps recent ones. A sample 400 days old is
    // expired; one 10 days old is kept; the run also stores today's sample.

    [Fact]
    public async Task M33_7_Purge_Deletes_Expired_Samples()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        var now = DateTimeOffset.UtcNow;
        var expiredDate = now.AddDays(-StorageMetricsCaptureService.RetentionDays - 35); // 400 days old
        var recentDate = now.AddDays(-10);

        // Plant one expired sample + one recent sample (distinct days).
        await PlantSample(store, expiredDate);
        await PlantSample(store, recentDate);

        // The capture run: store today's sample + purge rows older than
        // now − RetentionDays (the expired one is deleted, the recent one
        // survives).
        var purged = await StorageMetricsCaptureService.CaptureAndPurgeAsync(store, svc, now);
        Assert.Equal(1, purged);

        var ids = (await AllSamples(store)).Select(s => s.Id).ToHashSet();
        Assert.Contains("smh-" + now.ToUniversalTime().Date.ToString("yyyy-MM-dd"), ids);      // today's sample
        Assert.Contains("smh-" + recentDate.ToUniversalTime().Date.ToString("yyyy-MM-dd"), ids); // recent kept
        Assert.DoesNotContain("smh-" + expiredDate.ToUniversalTime().Date.ToString("yyyy-MM-dd"), ids); // expired purged
        await store.DisposeAsync();
    }

    // ── Pin 5 ─────────────────────────────────────────────────────────────
    // M33·2 (FACES M33-8): the stored sample's field set is the 8-member
    // M33·2 ceiling — the Id + SampleDate + the 7 data members mirroring the
    // M24 StorageMetricsSnapshot (the sample IS the snapshot frozen at the
    // day). Seed a known catalog + a known volume, capture, and assert every
    // field of the stored row matches the frozen snapshot.

    [Fact]
    public async Task M33_2_Sample_FieldSet_Snapshot_Shape()
    {
        const long fakeTotal = 9_876_543;
        const long fakeFree  = 5_432_109;
        var store = await BootStoreAsync();
        var svc = new StorageMetricsService(store, new FakeVolume(total: fakeTotal, free: fakeFree));

        // Two >0-byte rows across two distinct creators → TotalUsedBytes 300,
        // 2 unique files, 2 distinct users (UserContentUsedBytes == TotalUsedBytes).
        await PlantMedia(store, sizeBytes: 100, createdById: "u1");
        await PlantMedia(store, sizeBytes: 200, createdById: "u2");

        var now = DateTimeOffset.UtcNow;
        await StorageMetricsCaptureService.CaptureAndPurgeAsync(store, svc, now);

        var sample = (await AllSamples(store)).Single();
        // The 8-member ceiling (design doc §2.2): Id + SampleDate + the 7 data
        // members mirroring the M24 snapshot (AsOf replaced by SampleDate).
        Assert.Equal("smh-" + now.ToUniversalTime().Date.ToString("yyyy-MM-dd"), sample.Id);
        Assert.Equal(now, sample.SampleDate);
        Assert.Equal(300,      sample.TotalUsedBytes);
        Assert.Equal(fakeTotal, sample.TotalVolumeBytes);
        Assert.Equal(fakeFree,  sample.FreeVolumeBytes);
        Assert.Equal(300,      sample.UserContentUsedBytes);   // == TotalUsedBytes (M24)
        Assert.Equal(2,        sample.TotalUniqueFiles);
        Assert.Equal(2,        sample.TotalDistinctUsers);
        await store.DisposeAsync();
    }

    // ── Pin 6 ─────────────────────────────────────────────────────────────
    // M33-9 FACE (M33·2 / M33·8): GetHistoryAsync(30) over a window with gaps
    // (two non-adjacent days, the days between missing) returns ONLY the days
    // present — no fabricated zero rows fill the gap.

    [Fact]
    public async Task M33_8_GetHistory_Returns_Only_Days_Present()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        var now = DateTimeOffset.UtcNow;
        // Two non-adjacent days, both inside the trailing 30-day window, with
        // the intervening days absent (a real gap).
        await PlantSample(store, now.AddDays(-5));
        await PlantSample(store, now.AddDays(-20));

        var result = await svc.GetHistoryAsync(days: 30);

        Assert.Equal(2, result.Points.Count);
        // Ascending by SampleDate (the §2.1 seam pin): the older day first.
        Assert.Equal("smh-" + now.AddDays(-20).Date.ToString("yyyy-MM-dd"), result.Points[0].Id);
        Assert.Equal("smh-" + now.AddDays(-5).Date.ToString("yyyy-MM-dd"),  result.Points[1].Id);
        await store.DisposeAsync();
    }

    // ── Pin 7 ─────────────────────────────────────────────────────────────
    // M33·8 (FACES M33-10): GetHistoryAsync with an unknown window throws
    // ArgumentOutOfRangeException (the M13 windowDays "unknown value throws,
    // not a 0-row query" precedent) — never a silent empty result.

    [Fact]
    public async Task M33_8_GetHistory_Unknown_Window_Throws()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => svc.GetHistoryAsync(days: 999));
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh <c>mt</c> schema in a new scratch database — the
    /// surfaces these tests need: <see cref="MediaObject"/> (the snapshot
    /// read), <see cref="StorageMetricsSample"/> (the M33 doc) and
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> (the M33·4
    /// zero-audit assertion queries it, so it must be registered).</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            MediaDocTypes.Configure(opts);            // MediaObject (the M24 snapshot read)
            StorageHistoryDocTypes.Configure(opts);   // StorageMetricsSample (the M33 doc)
            M1DocTypes.Configure(opts);               // AccessAudit (the M33·4 zero-audit query)
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>A <see cref="StorageMetricsService"/> wired to the test
    /// <see cref="FakeVolume"/> (the M24 read-only volume-stat double).</summary>
    private static StorageMetricsService BuildService(IDocumentStore store)
        => new(store, new FakeVolume(total: 1_000_000, free: 400_000));

    /// <summary>Query the whole <see cref="StorageMetricsSample"/> table
    /// (the read side of the M33 seam, unsorted — the test sorts/inspects).</summary>
    private static async Task<IReadOnlyList<StorageMetricsSample>> AllSamples(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<StorageMetricsSample>().ToListAsync(ct);
    }

    /// <summary>The total <see cref="Kumunita.Core.Authorization.AccessAudit"/>
    /// row count in the store (the M33·4 zero-audit pin).</summary>
    private static async Task<int> AuditCount(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<Kumunita.Core.Authorization.AccessAudit>().CountAsync(ct);
    }

    /// <summary>Plant a <see cref="StorageMetricsSample"/> row directly (a
    /// write session — the test setup, not the code under test). The
    /// deterministic Id is the sample's UTC day, so two calls on the same
    /// day overwrite (the M33·2 dedup).</summary>
    private static async Task PlantSample(IDocumentStore store, DateTimeOffset sampleDate)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.LightweightSession();
        w.Store(new StorageMetricsSample
        {
            Id = "smh-" + sampleDate.ToUniversalTime().Date.ToString("yyyy-MM-dd"),
            SampleDate = sampleDate,
            TotalUsedBytes = 1,
            TotalVolumeBytes = 2,
            FreeVolumeBytes = 3,
            UserContentUsedBytes = 1,
            TotalUniqueFiles = 1,
            TotalDistinctUsers = 1,
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>Plant a <see cref="MediaObject"/> catalog row (the M24
    /// <c>PlantMedia</c> shape) so the snapshot read the capture service
    /// reuses (M33·1) returns non-trivial values.</summary>
    private static async Task PlantMedia(IDocumentStore store, long sizeBytes, string? createdById)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.LightweightSession();
        w.Store(new MediaObject
        {
            Id = $"sm-{Interlocked.Increment(ref _mediaCounter)}-{Guid.NewGuid():N}"[..20],
            Filename = "seed.bin",
            ContentType = "application/octet-stream",
            SizeBytes = sizeBytes,
            Created = DateTimeOffset.UtcNow,
            CreatedById = createdById
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>A read-only <see cref="IMediaFileStore"/> test double (the M24
    /// <c>FakeVolume</c> shape): the two volume-stat reads return canned values
    /// (a filesystem-stat seam, not a Postgres query); the byte-I/O members are
    /// unreachable here and throw if mistaken for a volume read.</summary>
    private sealed class FakeVolume(long total, long free) : IMediaFileStore
    {
        public Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default) => Task.FromResult(total);
        public Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default) => Task.FromResult(free);
        public string RootPath => "";
        public Task PutAsync(string contentId, byte[] content, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only (M24 C-SM·2).");
        public Task<bool> ExistsAsync(string contentId, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<Stream> OpenReadAsync(string contentId, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only (M24 C-SM·2).");
        public Task DeleteFileAsync(string contentId, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only (M24 C-SM·2).");
    }
}
