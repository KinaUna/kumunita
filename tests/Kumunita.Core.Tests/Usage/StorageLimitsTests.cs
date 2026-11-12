using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Usage;

/// <summary>
/// The 10 pinned M25 Core tests (design §2.3, items 1–10). Tests 1–6 exercise
/// the **pure** <see cref="StorageLimits"/> module (no DB); tests 7–9 exercise
/// <see cref="StorageSettingsService"/> reads/writes over the
/// <see cref="PostgresFixture"/> harness (Testcontainers <c>postgres:18</c>)
/// with the <see cref="MediaDocTypes"/> + <see cref="StorageSettingsDocTypes"/>
/// surfaces over <see cref="MediaObject"/> / <see cref="CommunityStorageSettings"/>.
/// The <see cref="StorageDecision"/> result is asserted — the
/// <c>413</c> mapping is the Web gate's (U8) job (C-UP·3).
/// </summary>
public sealed class StorageLimitsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static int _plantCounter;

    // ── Pin 1 (F2) ─────────────────────────────────────────────────────────
    // Over the admin-set per-file limit → Oversize. Pure / no DB.

    [Fact]
    public void Decide_Oversize_Rejects()
    {
        var settings = new CommunityStorageSettings
        {
            MaxFileBytes = 100,
            PerUserQuotaBytes = 0   // unlimited quota — isolates the size gate
        };

        Assert.Equal(StorageDecision.Oversize,
            StorageLimits.Decide(incomingBytes: 200, currentUsageBytes: 0, settings, envMaxBytes: 1_000_000));
    }

    // ── Pin 2 (F3) ─────────────────────────────────────────────────────────
    // Within the per-file limit but over the per-user quota → OverQuota.
    // Pure / no DB.

    [Fact]
    public void Decide_OverQuota_Rejects()
    {
        var settings = new CommunityStorageSettings
        {
            MaxFileBytes = 1_000,
            PerUserQuotaBytes = 1_000
        };

        Assert.Equal(StorageDecision.OverQuota,
            StorageLimits.Decide(incomingBytes: 600, currentUsageBytes: 500, settings, envMaxBytes: 1_000_000));
    }

    // ── Pin 3 (F1) ─────────────────────────────────────────────────────────
    // Within both the per-file limit and the per-user quota → Allowed.
    // Pure / no DB.

    [Fact]
    public void Decide_WithinBoth_Allows()
    {
        var settings = new CommunityStorageSettings
        {
            MaxFileBytes = 1_000,
            PerUserQuotaBytes = 1_000
        };

        Assert.Equal(StorageDecision.Allowed,
            StorageLimits.Decide(incomingBytes: 400, currentUsageBytes: 300, settings, envMaxBytes: 1_000_000));
    }

    // ── Pin 4 (F5) ─────────────────────────────────────────────────────────
    // Quota 0 = unlimited; the size limit still applies. Pure / no DB.

    [Fact]
    public void Decide_QuotaZero_Unlimited()
    {
        var settings = new CommunityStorageSettings
        {
            MaxFileBytes = 10_000,      // a real per-file limit
            PerUserQuotaBytes = 0       // unlimited quota
        };

        // Far over where a small quota would bite, but under the per-file limit.
        Assert.Equal(StorageDecision.Allowed,
            StorageLimits.Decide(incomingBytes: 5_000, currentUsageBytes: 9_000, settings, envMaxBytes: 1_000_000));
    }

    // ── Pin 5 (F4) ─────────────────────────────────────────────────────────
    // The admin override beats the env fallback. Pure / no DB.

    [Fact]
    public void EffectiveMaxFileBytes_AdminOverrideBeatsEnv()
    {
        var settings = new CommunityStorageSettings { MaxFileBytes = 5_000 };

        Assert.Equal(5_000, StorageLimits.EffectiveMaxFileBytes(settings, envMaxBytes: 1_000));
    }

    // ── Pin 6 (F6) ─────────────────────────────────────────────────────────
    // An unset (null) admin limit falls back to the env <c>Media__MaxBytes</c>.
    // Pure / no DB.

    [Fact]
    public void EffectiveMaxFileBytes_UnsetFallsBackToEnv()
    {
        var settings = new CommunityStorageSettings { MaxFileBytes = null };

        Assert.Equal(7_777, StorageLimits.EffectiveMaxFileBytes(settings, envMaxBytes: 7_777));
    }

    // ── Pin 7 (F7/F8) ──────────────────────────────────────────────────────
    // A user's usage is Σ SizeBytes WHERE CreatedById == that user — only that
    // subject's bytes. Uses the C-SM·7 seam via the settings service.

    [Fact]
    public async Task GetPerUserUsage_SumsCreatedByIdOnly()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        await PlantMedia(store, sizeBytes: 100, createdById: "alice");
        await PlantMedia(store, sizeBytes: 50,  createdById: "alice");
        await PlantMedia(store, sizeBytes: 999, createdById: "bob");

        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(150, await svc.GetPerUserUsageBytesAsync("alice", ct));
        await store.DisposeAsync();
    }

    // ── Pin 8 (F8) ─────────────────────────────────────────────────────────
    // A byte uploaded by one subject is never in another subject's usage
    // (first-storer attribution). Uses the C-SM·7 seam via the settings service.

    [Fact]
    public async Task GetPerUserUsage_OtherUsersExcluded()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        await PlantMedia(store, sizeBytes: 100, createdById: "alice");
        await PlantMedia(store, sizeBytes: 999, createdById: "bob");

        // Alice's usage excludes Bob's bytes, and vice versa.
        var ct = TestContext.Current.CancellationToken;
        Assert.Equal(100, await svc.GetPerUserUsageBytesAsync("alice", ct));
        Assert.Equal(999, await svc.GetPerUserUsageBytesAsync("bob", ct));
        await store.DisposeAsync();
    }

    // ── Pin 9 (C-UP·1) ─────────────────────────────────────────────────────
    // SetAsync is the single admin write lane: one in-caller-session write that
    // persists (MaxFileBytes + PerUserQuotaBytes + actor stamp) and is read
    // back by GetOrCreateAsync.

    [Fact]
    public async Task SetAsync_PersistsInCallerSession()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);

        await using var w = store.LightweightSession();
        await svc.SetAsync(maxFileBytes: 1234, perUserQuotaBytes: 5678,
            actorId: "admin", session: w);

        var readBack = await svc.GetOrCreateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1234, readBack.MaxFileBytes);
        Assert.Equal(5678, readBack.PerUserQuotaBytes);
        Assert.Equal("admin", readBack.ModifiedById);
        await store.DisposeAsync();
    }

    // ── Pin 10 (C-UP·2 ordering) ───────────────────────────────────────────
    // Size is checked **before** quota: an input that is over BOTH the per-file
    // limit and the quota reports Oversize (the size decision wins, test 10).
    // Pure / no DB.

    [Fact]
    public void Decide_SizeCheckedBeforeQuota()
    {
        var settings = new CommunityStorageSettings
        {
            MaxFileBytes = 100,
            PerUserQuotaBytes = 50
        };

        // 200 is over the size limit (100) AND would be over the quota (50).
        // Size-first → Oversize, not OverQuota.
        Assert.Equal(StorageDecision.Oversize,
            StorageLimits.Decide(incomingBytes: 200, currentUsageBytes: 0, settings, envMaxBytes: 1_000_000));
    }

    // ── IMPROVE-UX A15 — the per-resident quota override (the admin half) ───
    // The override lives on the single settings row (ResidentQuotaOverrides
    // map, keyed by subjectId) — a map, not a new doc type. Sentinel (C-UP·5)
    // preserved per-resident: unset (null) is distinct from 0 (unlimited for
    // that resident). The effective quota is override ?? global, resolved once
    // in StorageLimits.EffectivePerUserQuota and folded into the 5-arg Decide.

    // A15 · pin 1 — a non-null override wins over the global per-user quota.
    [Fact]
    public void EffectivePerUserQuota_Override_WinsOverGlobal()
    {
        var settings = new CommunityStorageSettings { PerUserQuotaBytes = 2L * 1024 * 1024 };
        var overrideQuota = 5L * 1024 * 1024;
        Assert.Equal(overrideQuota,
            StorageLimits.EffectivePerUserQuota(overrideQuota, settings));
    }

    // A15 · pin 2 — an unset (null) override falls back to the global quota.
    [Fact]
    public void EffectivePerUserQuota_Unset_FallsBackToGlobal()
    {
        var settings = new CommunityStorageSettings { PerUserQuotaBytes = 7_777 };
        Assert.Equal(7_777,
            StorageLimits.EffectivePerUserQuota(perUserQuotaOverride: null, settings));
    }

    // A15 · pin 3 — an override of 0 = unlimited for that resident (the C-UP·5
    // sentinel preserved per-resident, distinct from the unset null).
    [Fact]
    public void EffectivePerUserQuota_Zero_UnlimitedForResident()
    {
        var settings = new CommunityStorageSettings { PerUserQuotaBytes = 2L * 1024 * 1024 };
        Assert.Equal(0,
            StorageLimits.EffectivePerUserQuota(perUserQuotaOverride: 0, settings));
    }

    // A15 · pin 4 — the 5-arg Decide: an upload over the GLOBAL quota is
    // ALLOWED when the resident has a higher override (the override is
    // honoured), and REFUSED when they have none (it falls back to global).
    // The per-file cap is independent (unlimited here) — only the quota path
    // is exercised.
    [Fact]
    public void Decide_WithOverride_AllowsOverGlobalWhenOverrideIsHigher_AndRefusedWhenNoOverride()
    {
        var settings = new CommunityStorageSettings
        {
            MaxFileBytes = long.MaxValue,          // per-file cap untouched
            PerUserQuotaBytes = 100                // global: 100 bytes
        };
        const long usage = 50;
        const long incoming = 60;   // 50 + 60 = 110 > 100 global

        // A resident with a 5 GB override: 110 is well under → Allowed (the
        // override is honoured — the upload would be refused against the
        // global quota).
        Assert.Equal(StorageDecision.Allowed,
            StorageLimits.Decide(incoming, usage, settings, long.MaxValue,
                perUserQuotaOverride: 5L * 1024 * 1024 * 1024));

        // A resident with no override: 110 > 100 global → OverQuota.
        Assert.Equal(StorageDecision.OverQuota,
            StorageLimits.Decide(incoming, usage, settings, long.MaxValue,
                perUserQuotaOverride: null));
    }

    // A15 · pin 5 — an override of 0 = unlimited for that resident: a huge
    // upload is Allowed even though it would be over the global quota.
    [Fact]
    public void Decide_WithZeroOverride_UnlimitedForResident()
    {
        var settings = new CommunityStorageSettings
        {
            MaxFileBytes = long.MaxValue,
            PerUserQuotaBytes = 100
        };
        // 50 + 10_000 > 100 global, but the override is 0 = unlimited → Allowed.
        Assert.Equal(StorageDecision.Allowed,
            StorageLimits.Decide(incomingBytes: 10_000, currentUsageBytes: 50,
                settings, long.MaxValue, perUserQuotaOverride: 0));
    }

    // A15 · pin 6 — the service round-trip: SetResidentQuotaOverrideAsync
    // stores the override (a write in the caller's session) and
    // GetResidentQuotaOverrideAsync reads it back; a null write clears it
    // (back to the global — the read returns null); an unset key reads null
    // (distinct from a stored 0).
    [Fact]
    public async Task SetResidentQuotaOverride_StoresAndClears_ReadsBackNullWhenUnset()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        // Unset → null (the override ?? global fallback signal).
        Assert.Null(await svc.GetResidentQuotaOverrideAsync("alice", ct));

        // Set a cap (in the caller's session — the C-UP·1 shape) → reads back.
        await using var w = store.LightweightSession();
        await svc.SetResidentQuotaOverrideAsync("alice", 5_000, "admin", w);
        Assert.Equal(5_000, await svc.GetResidentQuotaOverrideAsync("alice", ct));

        // A stored 0 (unlimited for that resident) is distinct from unset null.
        await svc.SetResidentQuotaOverrideAsync("alice", 0, "admin", w);
        Assert.Equal(0, await svc.GetResidentQuotaOverrideAsync("alice", ct));

        // Clear (null) → back to the global: the read returns null again.
        await svc.SetResidentQuotaOverrideAsync("alice", null, "admin", w);
        Assert.Null(await svc.GetResidentQuotaOverrideAsync("alice", ct));

        // One resident's override is not another's (keyed by subjectId).
        await svc.SetResidentQuotaOverrideAsync("bob", 9_000, "admin", w);
        Assert.Equal(9_000, await svc.GetResidentQuotaOverrideAsync("bob", ct));
        Assert.Null(await svc.GetResidentQuotaOverrideAsync("alice", ct));

        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh <c>mt</c> schema over <see cref="MediaObject"/> +
    /// <see cref="CommunityStorageSettings"/> in a new scratch database (the
    /// <c>StorageMetricsTests.BootStoreAsync</c> shape) — the surfaces these
    /// tests need.</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            MediaDocTypes.Configure(opts);            // MediaObject's ADR 0004 §B.1 surface
            StorageSettingsDocTypes.Configure(opts);  // CommunityStorageSettings' M25 surface
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>Build the settings service under test. The C-SM·7 seam is the
    /// real <see cref="StorageMetricsService"/> (whose <see
    /// cref="GetPerUserUsageBytesAsync"/> read is what pins 7–8 assert); the
    /// volume seam is a read-only <see cref="FakeVolume"/> double (unused by the
    /// per-user read, present to satisfy the constructor).</summary>
    private static StorageSettingsService BuildService(IDocumentStore store)
        => new(store, new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000)));

    /// <summary>Plant a <see cref="MediaObject"/> catalog row (a write session —
    /// the test setup, not the service under test).</summary>
    private static async Task PlantMedia(IDocumentStore store, long sizeBytes, string? createdById)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.LightweightSession();
        w.Store(new MediaObject
        {
            Id = $"sl-{Interlocked.Increment(ref _plantCounter)}-{Guid.NewGuid():N}"[..20],
            Filename = "seed.bin",
            ContentType = "application/octet-stream",
            SizeBytes = sizeBytes,
            Created = DateTimeOffset.UtcNow,
            CreatedById = createdById
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>A read-only <see cref="IMediaFileStore"/> test double (the
    /// <c>StorageMetricsTests.FakeVolume</c> shape) — satisfies the
    /// <see cref="StorageMetricsService"/> constructor; the volume-stat reads are
    /// not used by the per-user-usage read.</summary>
    private sealed class FakeVolume(long total, long free) : IMediaFileStore
    {
        public Task<long> GetTotalSpaceBytesAsync(CancellationToken ct = default) => Task.FromResult(total);
        public Task<long> GetFreeSpaceBytesAsync(CancellationToken ct = default) => Task.FromResult(free);
        public string RootPath => "";
        public Task PutAsync(string contentId, byte[] content, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only.");
        public Task<bool> ExistsAsync(string contentId, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task<Stream> OpenReadAsync(string contentId, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only.");
        public Task DeleteFileAsync(string contentId, CancellationToken ct = default)
            => throw new NotSupportedException("FakeVolume is read-only.");
        public Task<bool> DeleteIfExistsAsync(string contentId, CancellationToken ct = default)
            => Task.FromResult(false);
    }
}
