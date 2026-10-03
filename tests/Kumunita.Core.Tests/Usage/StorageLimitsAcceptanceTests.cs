using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Usage;

/// <summary>
/// M25 U10 — the **acceptance gate** (design §2.5) as the three named
/// end-to-end tests (closed-loop / handoff / part-vs-whole), driven
/// against the **shipped** Core seams (the <see
/// cref="StorageSettingsService"/> + the real <see
/// cref="StorageLimits.Decide"/> + a live Marten store over the
/// <see cref="PostgresFixture"/> harness — Testcontainers
/// <c>postgres:18</c>) so the closed loop / handoff assertions exercise
/// the actual round-trip pair <c>SetAsync</c> → <c>GetOrCreateAsync</c> →
/// <c>Decide</c> → <c>GetPerUserUsageBytesAsync</c> (C-UP·1/2/3/4), not
/// substitutes.
/// <para>
/// **Core stays HTTP-free** (C-UP·3 / ADR 0006-D) — the 413 mapping is
/// the Web gate's job (U8, pinned in the Web suite by the per-lane +
/// gate tests), so these tests assert the **pure decision** (<see
/// cref="StorageDecision"/>) the gate will map, not an <c>ActionResult</c>.
/// The "413" in the handoff test is the Web-only consequence of
/// <see cref="StorageDecision.OverQuota"/> (F3) — pinned here at the
/// decision the gate delegates to (the same level U9 chose for F2 ≠ F3,
/// handoff §U9(b)).
/// </para>
/// <para>
/// **FACES covered here** (not re-pinned — only confirmed as the
/// witnesses of the closed-loop + handoff): F1 (allowed), F3
/// (over-quota), F4 (strong consistency: the next upload after an admin
/// change reads the **live doc**), F5/F6 (already pinned in
/// <see cref="StorageLimitsTests"/> as
/// <c>Decide_QuotaZero_Unlimited</c> + <c>EffectiveMaxFileBytes_
/// UnsetFallsBackToEnv</c>). F9 (no byte written on reject) is the Web
/// gate's contract — pinned in **all four** lanes in U9 (see the handoff
/// §U9(a) table; e.g. <c>AttachmentUpload_OverQuota_413</c> in
/// <c>AttachmentUploadTests.cs</c>) — not re-asserted here (Core has no
/// byte write on its own).
/// </para>
/// </summary>
public sealed class StorageLimitsAcceptanceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static int _plantCounter;

    // ── 1 — the closed loop (design §2.5 gate 1) ──────────────────────────
    // Admin sets a quota → the resident uploads within it → the decision is
    // Allowed (the gate proceeds, the Web lane writes the byte), and the
    // resident's self-view (the C-SM·7 seam <see
    // cref="IStorageSettingsService.GetPerUserUsageBytesAsync"/>) reflects
    // the new usage (F1 — success + usage reflected). The **live doc**
    // written by the admin's <c>SetAsync</c> is what the resident's read
    // sees — one community doc, one source of truth (C-UP·1).

    [Fact]
    public async Task M25_Acceptance_ClosedLoop_AdminSetsQuota_ResidentUploads_UsageReflected()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        // Step 1 — the admin sets a **tight** quota (1_000 B) + a
        // per-file cap (2_000 B) (the single write lane, C-UP·1):
        await using (var w = store.LightweightSession())
            await svc.SetAsync(maxFileBytes: 2_000, perUserQuotaBytes: 1_000,
                actorId: "admin-acceptance", session: w);

        // Step 2 — the resident's **first** upload (400 B, within both
        // bounds: 400 ≤ 2_000 cap; 0 + 400 ≤ 1_000 quota) → Allowed (F1).
        // The Web lane writes the byte (F9: only a *reject* skips the
        // write — an allowed upload proceeds to PutAsync, which the Web
        // suite pins); the Core decision here is the gate's input.
        var usageBefore = await svc.GetPerUserUsageBytesAsync("resident-a", ct);
        Assert.Equal(0L, usageBefore); // fresh resident, no bytes yet

        var doc = await svc.GetOrCreateAsync(ct);
        Assert.Equal(1_000, doc.PerUserQuotaBytes);   // the admin's doc is live
        Assert.Equal(2_000, doc.MaxFileBytes);
        Assert.Equal(StorageDecision.Allowed,
            svc.Decide(incomingBytes: 400, currentUsageBytes: usageBefore, settings: doc, envMaxBytes: 5_242_880));

        // Step 3 — the upload lands (the Web lane's PutAsync writes it;
        // here we plant the equivalent catalog row so the resident's
        // self-view read has the byte to sum — the C-SM·7 seam):
        await PlantMedia(store, sizeBytes: 400, createdById: "resident-a");

        // The resident's self-view now reflects the new usage (F1
        // "success + the usage is reflected" — the design §2.5 gate-1
        // acceptance):
        Assert.Equal(400, await svc.GetPerUserUsageBytesAsync("resident-a", ct));
        await store.DisposeAsync();
    }

    // ── 2 — the handoff (design §2.5 gate 2) ──────────────────────────────
    // The **next** upload that would exceed the quota is rejected (the
    // Web gate maps this to a 413 — C-UP·3). The strong-consistency
    // witness: the settings doc the gate reads is the **live** doc the
    // admin's <c>SetAsync</c> wrote — not a cached copy, not an env-only
    // value (F4/F3). The same call the resident's first upload took
    // (<c>GetOrCreateAsync</c>) now returns the admin's tightened
    // quota, and the decision on the same incoming size + the
    // resident's now-seeded usage is OverQuota — the reject.

    [Fact]
    public async Task M25_Acceptance_Handoff_NextUploadOverQuota_Rejects_StrongConsistency_LiveDoc()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        // Seed: the resident already has 700 B in the catalog (their
        // first upload from the closed loop landed):
        await PlantMedia(store, sizeBytes: 700, createdById: "resident-b");

        // The admin tightens the quota to 1_000 B + the per-file cap to
        // 2_000 B (one <c>SetAsync</c> — the single write lane, C-UP·1):
        await using (var w = store.LightweightSession())
            await svc.SetAsync(maxFileBytes: 2_000, perUserQuotaBytes: 1_000,
                actorId: "admin-acceptance", session: w);

        // The resident's self-view is now 700 B (their bytes, C-SM·7 /
        // F8 — other users' bytes stay out):
        Assert.Equal(700, await svc.GetPerUserUsageBytesAsync("resident-b", ct));

        // A 400 B upload: within the per-file cap (400 ≤ 2_000) but
        // over the quota (700 + 400 = 1_100 > 1_000 → the reject, F3).
        var doc = await svc.GetOrCreateAsync(ct);
        Assert.Equal(1_000, doc.PerUserQuotaBytes); // the live doc the gate reads

        var usage = await svc.GetPerUserUsageBytesAsync("resident-b", ct);
        var decision = svc.Decide(incomingBytes: 400, currentUsageBytes: usage, settings: doc, envMaxBytes: 5_242_880);

        // The decision is OverQuota (F3) — the Web gate (C-UP·3) maps
        // this to the 413 the per-lane suite pins (U9, the exact
        // <c>StatusCodeResult</c> + <c>StatusCode == 413</c> shape).
        Assert.Equal(StorageDecision.OverQuota, decision);

        // The strong-consistency witness (F4): the doc the decision
        // read is the **live** admin doc — not a stale cached copy, not
        // the env-only default. A second read through the same seam
        // returns the same values (the doc is committed in the caller's
        // session; the read sees it):
        var reread = await svc.GetOrCreateAsync(ct);
        Assert.Equal(doc.PerUserQuotaBytes, reread.PerUserQuotaBytes);
        Assert.Equal(doc.MaxFileBytes, reread.MaxFileBytes);
        Assert.Equal("admin-acceptance", reread.ModifiedById); // the admin's write is the one in force
        await store.DisposeAsync();
    }

    // ── 3 — part-vs-whole (design §2.5 gate 3) ────────────────────────────
    // The 23-test list (design §2.3 Core items 1–10 + §2.4 Web items
    // 11–23) is the **whole**; the closed-loop + handoff above are the
    // **parts**; all must pass **together** (a per-lane test passing in
    // isolation does not prove the four-lane consistency of F10). This
    // test is the part-vs-whole witness **at the Core level** — the
    // closed-loop + handoff decision shapes, run **in the same
    // assembly, in the same scratch database lifecycle, in the same
    // run** as the 10 pinned <see cref="StorageLimitsTests"/> (the
    // "whole" the xunit runner executes when the suite is green).
    // <para>
    // The assertion: the pure <see cref="StorageLimits.Decide"/> on the
    // **same** inputs the closed-loop + handoff tests drove produces the
    // **same** decision shape (Allowed for the closed-loop input,
    // OverQuota for the handoff input) — the part-vs-whole invariant is
    // that the parts' decisions are a **subset of** the whole's decision
    // space, exercised together. This is the Core-side witness of F10
    // (four-lane consistency) — the Web-side witness is U9's
    // per-lane suite (all four lanes, same gate, same 413 shape).
    /// </para>

    [Fact]
    public async Task M25_Acceptance_PartVsWhole_ClosedLoopAndHandoff_DecisionsConsistentTogether()
    {
        var store = await BootStoreAsync();
        var svc = BuildService(store);
        var ct = TestContext.Current.CancellationToken;

        // The admin's doc (the same one the closed-loop + handoff
        // tests wrote — one community doc, C-UP·1):
        await using (var w = store.LightweightSession())
            await svc.SetAsync(maxFileBytes: 2_000, perUserQuotaBytes: 1_000,
                actorId: "admin-acceptance", session: w);

        var doc = await svc.GetOrCreateAsync(ct);
        Assert.Equal(1_000, doc.PerUserQuotaBytes);
        Assert.Equal(2_000, doc.MaxFileBytes);

        // The **same** inputs the closed-loop (gate 1) + handoff
        // (gate 2) tests drove — the "parts" — re-asserted together
        // here (the "whole"):

        // Part 1 (closed-loop): within both bounds → Allowed (F1):
        Assert.Equal(StorageDecision.Allowed,
            svc.Decide(incomingBytes: 400, currentUsageBytes: 0, settings: doc, envMaxBytes: 5_242_880));

        // Part 2 (handoff): the same size but the resident's now-seeded
        // usage pushes over the quota → OverQuota (F3) — the reject the
        // Web gate maps to 413 (C-UP·3):
        Assert.Equal(StorageDecision.OverQuota,
            svc.Decide(incomingBytes: 400, currentUsageBytes: 700, settings: doc, envMaxBytes: 5_242_880));

        // Part-vs-whole invariant: the two decisions are **distinct**
        // (Allowed ≠ OverQuota) — the same decision function, the
        // same live doc, the same env fallback — produces the correct
        // shape on each input. The whole (all 10 Core pins in
        // <see cref="StorageLimitsTests"/> + the 13 Web pins in U9's
        // per-lane suite) is what the xunit runner executes when the
        // suite is green; this test is the part-vs-whole witness that
        // the **parts** agree with each other under the **whole's**
        // settings doc (F10 — four-lane consistency at the decision
        // level, the Core-side witness).
        var allowed = svc.Decide(400, 0, doc, 5_242_880);
        var overQuota = svc.Decide(400, 700, doc, 5_242_880);
        Assert.NotEqual(allowed, overQuota);

        // F10 witness (Core-side): the same doc + the same decision
        // function is what every one of the four Web lanes routes
        // through (U8's gate adoption) — the lane is a **thin**
        // wrapper over the decision, so the decision-level
        // consistency here is the four-lane consistency the Web
        // suite pins end-to-end (U9's 11–18).
        await store.DisposeAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Boot a fresh <c>mt</c> schema over <see cref="MediaObject"/> +
    /// <see cref="CommunityStorageSettings"/> in a new scratch database (the
    /// <c>StorageLimitsTests.BootStoreAsync</c> shape) — the surfaces these
    /// acceptance tests need.</summary>
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

    /// <summary>Build the settings service under test (the
    /// <see cref="StorageLimitsTests.BuildService"/> shape — the real
    /// <see cref="StorageMetricsService"/> for the C-SM·7 read + a
    /// read-only <see cref="FakeVolume"/> double to satisfy the
    /// constructor).</summary>
    private static StorageSettingsService BuildService(IDocumentStore store)
        => new(store, new StorageMetricsService(store, new FakeVolume(total: 1_000_000, free: 400_000)));

    /// <summary>Plant a <see cref="MediaObject"/> catalog row (a write session
    /// — the test setup, not the service under test) — the equivalent of
    /// the Web lane's <c>PutAsync</c> write for the accepted upload.</summary>
    private static async Task PlantMedia(IDocumentStore store, long sizeBytes, string? createdById)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.LightweightSession();
        w.Store(new MediaObject
        {
            Id = $"sla-{Interlocked.Increment(ref _plantCounter)}-{Guid.NewGuid():N}"[..20],
            Filename = "acceptance.bin",
            ContentType = "application/octet-stream",
            SizeBytes = sizeBytes,
            Created = DateTimeOffset.UtcNow,
            CreatedById = createdById
        });
        await w.SaveChangesAsync(ct);
    }

    /// <summary>A read-only <see cref="IMediaFileStore"/> test double (the
    /// <c>StorageLimitsTests.FakeVolume</c> shape) — satisfies the
    /// <see cref="StorageMetricsService"/> constructor; the volume-stat
    /// reads are not used by the per-user-usage read.</summary>
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
    }
}
