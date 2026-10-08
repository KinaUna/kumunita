using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M30 U03 (ADR 0153) — the
/// <see cref="FirstBootSeeder.SeedAdminOnboardingAsync"/> first-boot state
/// pins: a fresh boot stores exactly one
/// <see cref="Kumunita.Core.AdminOnboarding.AdminOnboarding"/> row
/// (<c>Id = "singleton"</c>, the <c>SiteContent</c> / <c>LocaleSettings</c>
/// "one row per instance" shape, ADR 0005 B / ADR 0150); <c>CompletedAt</c>
/// is <c>null</c> (the "not-yet-guided" floor — M30·2); and a second boot is
/// idempotent (no duplicate row, and an existing row's <c>CompletedAt</c>
/// field is left untouched — never-overwrites, the exact
/// <see cref="FirstBootSeeder.SeedSiteContentAsync"/> shape).
/// </summary>
/// <para>
/// <b>Harness note:</b> every <c>*DocTypes.Configure</c> call in this repo
/// lives in the host's <c>AddMarten</c> lambda, not in
/// <c>DependencyInjection.cs</c>. Because the
/// <see cref="AdminOnboardingDocTypes.Configure"/> host line in
/// <c>Program.cs</c> (U03) registers the doc type for the *host*, this
/// harness wires the SAME <see cref="AdminOnboardingDocTypes.Configure"/>
/// call in its own <c>DocumentStore.For</c> lambda — without it the
/// <c>AdminOnboarding</c> doc is invisible to Marten (the
/// <see cref="SiteContentSeederTests.BootStoreAsync"/> /
/// <see cref="SurfaceLabelsSeederTests"/> idiom). The tests exercise the
/// seeder's **public static** surface
/// (<see cref="FirstBootSeeder.SeedAdminOnboardingAsync"/>) directly, so no
/// <c>InternalsVisibleTo</c> is needed (the repo's Core test constraint).
/// </para>
/// <para>
/// <b>Collision idiom:</b> the doc type is fully qualified as
/// <c>AdminOnboarding.AdminOnboarding</c> (the type and its parent namespace
/// share the name <c>AdminOnboarding</c>; the unqualified name in this
/// namespace resolves to the *namespace*) — the exact
/// <c>SiteContent.SiteContent</c> / <c>SurfaceLabels.SurfaceLabels</c> idiom.
/// </para>
/// <para>
/// No new Core or Web code beyond the U03 seeder change; no new seams.
/// </para>
public class AdminOnboardingSeederTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — a fresh boot stores exactly one AdminOnboarding row ────────────
    // (the SiteContent / LocaleSettings "one row per instance" shape,
    // ADR 0005 B / ADR 0150 / M30·2).

    [Fact(DisplayName = "U03 a fresh boot stores exactly one AdminOnboarding row (Id = 'singleton')")]
    public async Task FreshBoot_HasExactlyOneAdminOnboardingRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await FirstBootSeeder.SeedAdminOnboardingAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<AdminOnboarding.AdminOnboarding>().ToListAsync(ct);

        // Exactly one row, and it carries the singleton sentinel Id (M30·2).
        Assert.Single(rows);
        Assert.Equal(AdminOnboarding.AdminOnboarding.SingletonId, rows[0].Id);
    }

    // ── 2 — a fresh boot seeds CompletedAt as null (the not-yet-guided
    // floor, M30·2). The POCO's `CompletedAt` defaults to `null`; the seeder
    // creates the row with that default (the "use the `null` floor" shape —
    // byte-identical to a fresh instance that never touches the surface).

    [Fact(DisplayName = "U03 a fresh boot seeds AdminOnboarding.CompletedAt as null (the not-yet-guided floor)")]
    public async Task FreshBoot_CompletedAtIsNull()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await FirstBootSeeder.SeedAdminOnboardingAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var row = await q.LoadAsync<AdminOnboarding.AdminOnboarding>(
            AdminOnboarding.AdminOnboarding.SingletonId, ct);

        Assert.NotNull(row);
        Assert.Equal(AdminOnboarding.AdminOnboarding.SingletonId, row!.Id);

        // CompletedAt is null (the not-yet-guided floor, M30·2 — the banner
        // shows, the walk-through is available).
        Assert.Null(row.CompletedAt);
    }

    // ── 3 — a second boot is idempotent (no duplicate row, never-overwrites) ─
    // (the exact SeedSiteContentAsync create-if-missing shape — a second boot
    // leaves an existing row untouched, so a GlobalAdmin's later completion
    // stamp is honored; M30·2).

    [Fact(DisplayName = "U03 a second boot is idempotent: no duplicate row and a completion stamp is honored (never-overwrites)")]
    public async Task SecondBoot_IsIdempotent_NoDuplicateRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // Boot 1 — seed the not-yet-guided singleton.
        await FirstBootSeeder.SeedAdminOnboardingAsync(store, NullLogger.Instance, ct);

        // Simulate a GlobalAdmin's later completion stamp (the very thing a
        // warm re-run must NOT clobber): stamp CompletedAt, commit.
        var stampedAt = DateTimeOffset.UtcNow;
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            var row = await session.LoadAsync<AdminOnboarding.AdminOnboarding>(
                AdminOnboarding.AdminOnboarding.SingletonId, ct);
            Assert.NotNull(row);
            row!.CompletedAt = stampedAt;
            // Explicit Store (the repo's test idiom — the SITE / M29 / LS U04
            // seeder pins): Marten dirty-tracking is not enabled in this
            // harness, so a mutated loaded doc is not saved by SaveChangesAsync
            // alone; the explicit Store makes the "admin's later edit" real.
            session.Store(row);
            await session.SaveChangesAsync(ct);
        }

        // Boot 2 — a second seed run must be a no-op (create-if-missing).
        await FirstBootSeeder.SeedAdminOnboardingAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<AdminOnboarding.AdminOnboarding>().ToListAsync(ct);

        // No duplicate row from the second boot (M30·2).
        Assert.Single(rows);
        Assert.Equal(AdminOnboarding.AdminOnboarding.SingletonId, rows[0].Id);

        // The completion stamp survived the second boot (never-overwrites,
        // M30·2).
        Assert.Equal(stampedAt, rows[0].CompletedAt);
    }

    // ─── Shared helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Boot a fresh scratch store with the <c>M30</c> doc type registered in
    /// the <c>DocumentStore.For</c> lambda (the host <c>Program.cs</c> line
    /// registers the doc type for the host; the harness wires the SAME
    /// <see cref="AdminOnboardingDocTypes.Configure"/> call here, the same
    /// idiom as every other <c>*DocTypes</c> test harness). Without it the
    /// <c>AdminOnboarding</c> doc is invisible to Marten.
    /// </summary>
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
            M3DocTypes.Configure(opts);
            AdminOnboardingDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
