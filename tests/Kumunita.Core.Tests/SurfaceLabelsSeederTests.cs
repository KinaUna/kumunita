using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M29 U04 (ADR 0152) — the <see cref="FirstBootSeeder.SeedSurfaceLabelsAsync"/>
/// first-boot state pins: a fresh boot stores exactly one
/// <see cref="Kumunita.Core.SurfaceLabels.SurfaceLabels"/> row
/// (<c>Id = "singleton"</c>, the <c>SiteContent</c> / <c>LocaleSettings</c>
/// "one row per instance" shape, ADR 0005 B / ADR 0150); **all 13** label
/// overrides are <c>null</c> (the "use the <c>kw-l</c> fallback" default —
/// M29·3 / M29·4, byte-identical to a fresh instance that never touches the
/// surface, because every label resolves to its <c>kw-l</c> registry key); and
/// a second boot is idempotent (no duplicate row, and an existing row's fields
/// are left untouched — never-overwrites, M29·6).
/// </summary>
/// <para>
/// <b>Harness note (the U03 drift note):</b> every <c>*DocTypes.Configure</c>
/// call in this repo lives in the host's <c>AddMarten</c> lambda, not in
/// <c>DependencyInjection.cs</c>. Because the
/// <see cref="SurfaceLabelsDocTypes.Configure"/> host line in
/// <c>Program.cs</c> (U03, <c>Program.cs:143</c>) registers the doc type for
/// the *host*, this harness wires the SAME
/// <see cref="SurfaceLabelsDocTypes.Configure"/> call in its own
/// <c>DocumentStore.For</c> lambda — without it the <c>SurfaceLabels</c> doc is
/// invisible to Marten (the <see cref="SiteContentSeederTests.BootStoreAsync"/>
/// / <see cref="LS_U04_SeederTests"/> / <see cref="PageServiceTests"/> idiom).
/// The tests exercise the seeder's **public static** surface
/// (<see cref="FirstBootSeeder.SeedSurfaceLabelsAsync"/>) directly, so no
/// <c>InternalsVisibleTo</c> is needed (the repo's Core test constraint).
/// </para>
/// <para>
/// <b>Collision idiom:</b> the doc type is fully qualified as
/// <c>SurfaceLabels.SurfaceLabels</c> (the type and its parent namespace share
/// the name <c>SurfaceLabels</c>; the unqualified name in this namespace
/// resolves to the *namespace*) — the exact <c>SiteContent.SiteContent</c>
/// idiom used throughout <see cref="SiteContentSeederTests"/>.
/// </para>
/// <para>
/// No new Core or Web code beyond the U04 seeder change; no new seams.
/// </para>
public class SurfaceLabelsSeederTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — a fresh boot stores exactly one SurfaceLabels row ───────────────
    // (the SiteContent / LocaleSettings "one row per instance" shape,
    // ADR 0005 B / M29·6).

    [Fact(DisplayName = "U04 a fresh boot stores exactly one SurfaceLabels row (Id = 'singleton')")]
    public async Task FreshBoot_HasExactlyOneSurfaceLabelsRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await FirstBootSeeder.SeedSurfaceLabelsAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<SurfaceLabels.SurfaceLabels>().ToListAsync(ct);

        // Exactly one row, and it carries the singleton sentinel Id (M29·6).
        Assert.Single(rows);
        Assert.Equal(SurfaceLabels.SurfaceLabels.SingletonId, rows[0].Id);
    }

    // ── 2 — all 13 label overrides are null (M29·3 / M29·4) ─────────────────
    // The seeded default is the all-null row — every one of the 13 fields is
    // unset, so each resolves to its `kw-l` registry key at resolution time
    // (byte-identical to a fresh instance that never touches the surface).

    [Fact(DisplayName = "U04 a fresh boot seeds all 13 SurfaceLabels overrides as null (the kw-l fallback default)")]
    public async Task FreshBoot_AllLabelsNull()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await FirstBootSeeder.SeedSurfaceLabelsAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var row = await q.LoadAsync<SurfaceLabels.SurfaceLabels>(SurfaceLabels.SurfaceLabels.SingletonId, ct);
        Assert.NotNull(row);
        Assert.Equal(SurfaceLabels.SurfaceLabels.SingletonId, row!.Id);

        // All 13 label overrides are null (the "use the `kw-l` fallback"
        // default, M29·3 / M29·4 — the floor is the kw-l registry key each
        // resolves to, which stays in the registry, ADR 0152 D3).
        Assert.Null(row.Home);
        Assert.Null(row.Announcements);
        Assert.Null(row.Community);
        Assert.Null(row.Groups);
        Assert.Null(row.Events);
        Assert.Null(row.Projects);
        Assert.Null(row.Inventory);
        Assert.Null(row.Bookmarks);
        Assert.Null(row.Documents);
        Assert.Null(row.Pages);
        Assert.Null(row.Tags);
        Assert.Null(row.Directory);
        Assert.Null(row.People);

        // The 13-key ceiling: every closed-set key resolves to the (null)
        // stored override via the row's own helper, and an out-of-set key
        // resolves to null (the ADR 0152 D1 drift guard).
        Assert.Null(row.GetLabel("home"));
        Assert.Null(row.GetLabel("announcements"));
        Assert.Null(row.GetLabel("people"));
        Assert.Null(row.GetLabel("not-a-surface"));
    }

    // ── 3 — a second boot is idempotent (no duplicate, never-overwrites) ────
    // (the exact SeedSiteContentAsync create-if-missing shape — a second boot
    // leaves an existing row untouched, so an admin's later rename is honored;
    // M29·6).

    [Fact(DisplayName = "U04 a second boot is idempotent: no duplicate row and an admin rename is honored (never-overwrites)")]
    public async Task SecondBoot_IsIdempotent_NoDuplicateRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // Boot 1 — seed the all-null singleton.
        await FirstBootSeeder.SeedSurfaceLabelsAsync(store, NullLogger.Instance, ct);

        // Simulate an admin's later edit (the very thing a warm re-run must
        // NOT clobber): set two labels, commit.
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            var row = await session.LoadAsync<SurfaceLabels.SurfaceLabels>(SurfaceLabels.SurfaceLabels.SingletonId, ct);
            Assert.NotNull(row);
            row!.Announcements = "News";
            row.Projects = "Board";
            // Explicit Store (the repo's test idiom — the SITE / LS U04 / PG
            // seeder pins): Marten dirty-tracking is not enabled in this
            // harness, so a mutated loaded doc is not saved by SaveChangesAsync
            // alone; the explicit Store makes the "admin's later edit" real.
            session.Store(row);
            await session.SaveChangesAsync(ct);
        }

        // Boot 2 — a second seed run must be a no-op (create-if-missing).
        await FirstBootSeeder.SeedSurfaceLabelsAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<SurfaceLabels.SurfaceLabels>().ToListAsync(ct);

        // No duplicate row from the second boot (M29·6).
        Assert.Single(rows);
        Assert.Equal(SurfaceLabels.SurfaceLabels.SingletonId, rows[0].Id);

        // The admin's edits survived the second boot (never-overwrites, M29·6).
        Assert.Equal("News", rows[0].Announcements);
        Assert.Equal("Board", rows[0].Projects);

        // The labels the admin did NOT touch remain the all-null seeded
        // default (the second boot left them exactly as boot 1 wrote them).
        Assert.Null(rows[0].Home);
        Assert.Null(rows[0].Community);
        Assert.Null(rows[0].People);
    }

    // ─── Shared helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Boot a fresh scratch store with the <c>M29</c> doc type registered in
    /// the <c>DocumentStore.For</c> lambda (the U03 drift note — the host
    /// <c>Program.cs</c> line registers the doc type for the host; the harness
    /// wires the SAME <see cref="SurfaceLabelsDocTypes.Configure"/> call here,
    /// the same idiom as every other <c>*DocTypes</c> test harness). Without
    /// it the <c>SurfaceLabels</c> doc is invisible to Marten.
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
            SurfaceLabelsDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
