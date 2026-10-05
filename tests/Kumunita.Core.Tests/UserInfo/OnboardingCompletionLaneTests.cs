using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.UserInfo;

/// <summary>
/// M22 (ADR 0132, D1/D2) — GATE-1 / GATE-2 pin for
/// <see cref="UserInfoService.CompleteOnboardingAsync"/> +
/// <see cref="UserInfoService.GetOnboardingCompletedAsync"/>: the *single*
/// owner-scope completion-stamp write lane and its read. Pinned at the service
/// layer (owner scope is the Web boundary's job; the lane writes
/// <see cref="Profile.OnboardingCompletedAt"/> only). Mirrors the
/// <see cref="ProfileTimezoneLaneTests"/> idiom established in this assembly.
/// <para>
/// The invariants pinned here:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>GATE-1 — one additive field, <c>null</c> = not completed.</b> A fresh
/// seeded profile reads <c>OnboardingCompletedAt == null</c> (the floor)
/// through both the existing <c>GetProfileAsync</c> read and the new
/// <c>GetOnboardingCompletedAsync</c> read.
/// </item>
/// <item>
/// <b>Write stamps, read returns it (strong consistency).</b> After
/// <c>CompleteOnboardingAsync</c>, the very next read (both seams) sees a
/// non-null stamp; the write touched no other field of the profile.
/// </item>
/// <item>
/// <b>GATE-2 — missing profile fails closed (throw, never create).</b>
/// <see cref="KeyNotFoundException"/> for an unknown subject, and *no*
/// profile row is created (the lane never loads or creates).
/// </item>
/// <item>
/// <b>C-M22·3 — zero <see cref="AccessAudit"/> rows.</b> The completion stamp
/// is a profile-field write, not an access decision (the
/// <c>UpsertProfileAsync</c> shape): the write appends no audit row.
/// </item>
/// </list>
/// </summary>
public sealed class OnboardingCompletionLaneTests(PostgresFixture fixture)
    : IClassFixture<PostgresFixture>
{
    // ── GATE-1 — fresh profile reads null (the floor) on both seams ──────
    [Fact]
    public async Task Onboarding_FreshProfile_Reads_NotCompleted()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-onboard-fresh";
        await SeedProfileAsync(svc, subject);

        // Both the existing GetProfileAsync read and the new read seam agree:
        // a new profile is *not* completed (the floor — the banner shows).
        Assert.Null((await svc.GetProfileAsync(subject))?.OnboardingCompletedAt);
        Assert.Null(await svc.GetOnboardingCompletedAsync(subject));
    }

    // ── Write stamps, read returns it (strong consistency C4) ────────────
    [Fact]
    public async Task Onboarding_Complete_ThenRead_ReturnsStamp()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-onboard-complete";
        await SeedProfileAsync(svc, subject);

        // Fresh seed ⇒ not completed (GATE-1 floor).
        Assert.Null(await svc.GetOnboardingCompletedAsync(subject));

        var before = DateTimeOffset.UtcNow;
        await svc.CompleteOnboardingAsync(subject, subject);
        var after = DateTimeOffset.UtcNow;

        // The write is live on the very next read — both the new read seam and
        // the existing GetProfileAsync read see the same non-null stamp. The
        // fresh read through the service's own read lane is what proves the
        // save landed — not an in-memory assertion.
        var viaSeam = await svc.GetOnboardingCompletedAsync(subject);
        var viaProfile = (await svc.GetProfileAsync(subject))!.OnboardingCompletedAt;

        Assert.NotNull(viaSeam);
        Assert.Equal(viaSeam, viaProfile);
        Assert.InRange(viaSeam!.Value, before, after);

        // The write touched no other field of the profile (the single-write-
        // lane pin — it writes OnboardingCompletedAt *only*).
        var profile = await svc.GetProfileAsync(subject);
        Assert.Equal("Resident " + subject, profile!.DisplayName);
        Assert.True(profile.Verified);
    }

    // ── GATE-2 — missing profile fails closed (throw, never create) ──────
    [Fact]
    public async Task Onboarding_Complete_MissingProfile_Throws_KeyNotFound()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        // The ADR 0132 pin: KeyNotFoundException (the same pinned type as the
        // timezone lane — load-throw-stamp-save, never load-or-create).
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.CompleteOnboardingAsync("u-onboard-missing", "u-onboard-any"));

        // Fail closed = fail *without side effects*: no profile row was created
        // (the lane never loads or creates on this path) — and the read seam
        // returns the floor (null) for a non-existent profile.
        Assert.Null(await svc.GetProfileAsync("u-onboard-missing"));
        Assert.Null(await svc.GetOnboardingCompletedAsync("u-onboard-missing"));
    }

    // ── C-M22·3 — the completion stamp writes no AccessAudit row ─────────
    [Fact]
    public async Task Onboarding_Complete_WritesNoAccessAuditRow()
    {
        var store = await BootStoreAsync();
        var svc = new UserInfoService(store);

        const string subject = "u-onboard-audit";
        await SeedProfileAsync(svc, subject);

        await svc.CompleteOnboardingAsync(subject, subject);

        // C-M22·3 (D2) — a profile-field write, not an access decision (the
        // UpsertProfileAsync shape): the completion stamp appends *no*
        // AccessAudit row, even though the lane is a committed write.
        await using var session = store.QuerySession();
        var audits = await session.Query<Authorization.AccessAudit>()
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(audits);
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    /// <summary>Seed a bootstrap profile through the service's own
    /// <c>UpsertProfileAsync</c> (all-null patch ⇒ the record supplies every
    /// field — the exact idiom at <c>ProfileTimezoneLaneTests</c>).</summary>
    private static async Task SeedProfileAsync(IUserInfoService svc, string subject)
    {
        var profile = new Profile
        {
            SubjectId = subject,
            DisplayName = "Resident " + subject,
            Verified = true,
        };
        await svc.UpsertProfileAsync(profile, new ProfileUpdate(null, null, null, null, null));
    }

    /// <summary>One scratch Postgres DB + a <c>mt</c> schema over the M1 docs
    /// (the <see cref="Profile"/> + <see cref="AccessAudit"/> docs — the lane
    /// touches nothing else). Mirrors
    /// <see cref="ProfileTimezoneLaneTests.BootStoreAsync"/> in this assembly.</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
