using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Localization;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// SITE U04 (ADR 0150) — the <see cref="FirstBootSeeder.SeedSiteContentAsync"/>
/// first-boot state pins: a fresh boot stores exactly one
/// <see cref="Kumunita.Core.SiteContent.SiteContent"/> row (<c>Id = "singleton"</c>, the
/// <c>LocaleSettings</c> "one row per instance" shape, ADR 0005 B); the 4
/// text-field defaults are **byte-identical** to the shipped
/// <see cref="KnownTranslationKeys.EnValues"/> text (the
/// <c>home.intro_eyebrow</c> / <c>home.intro_lead</c> / <c>about.eyebrow</c>
/// / <c>about.lead</c> keys — SITE·3); every one of the 9 section toggles is
/// <c>true</c>; and a second boot is idempotent (no duplicate row, and an
/// existing row's fields are left untouched — never-overwrites, SITE·6).
/// </summary>
/// <para>
/// <b>Harness note (the U03 drift note):</b> every <c>*DocTypes.Configure</c>
/// call in this repo lives in the host's <c>AddMarten</c> lambda, not in
/// <c>DependencyInjection.cs</c>. Because the
/// <see cref="SiteContentDocTypes.Configure"/> host line in
/// <c>Program.cs</c> is deferred to U05, this harness wires it in its own
/// <c>AddMarten</c> lambda — the same idiom as every other
/// <c>*DocTypes</c> test harness (<see cref="LS_U04_SeederTests.BootStoreAsync"/>
/// / <see cref="PageServiceTests.BootStoreAsync"/>). The tests exercise the
/// seeder's **public static** surface
/// (<see cref="FirstBootSeeder.SeedSiteContentAsync"/>) directly, so no
/// <c>InternalsVisibleTo</c> is needed (the repo's Core test constraint).
/// </para>
/// <para>
/// No new Core or Web code beyond the U04 seeder change; no new seams.
/// </para>
public class SiteContentSeederTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — a fresh boot stores exactly one SiteContent row ────────────────
    // (the LocaleSettings "one row per instance" shape, ADR 0005 B / SITE·6).

    [Fact(DisplayName = "U04 a fresh boot stores exactly one SiteContent row (Id = 'singleton')")]
    public async Task FreshBoot_HasExactlyOneSiteContentRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await FirstBootSeeder.SeedSiteContentAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<SiteContent.SiteContent>().ToListAsync(ct);

        // Exactly one row, and it carries the singleton sentinel Id (SITE·6).
        Assert.Single(rows);
        Assert.Equal(SiteContent.SiteContent.SingletonId, rows[0].Id);
    }

    // ── 2 — the seeded defaults match the shipped kw-l text (SITE·3) ───────
    // The 4 text fields equal the EXACT en source text from
    // KnownTranslationKeys.EnValues (read from the registry — not retyped),
    // and all 9 section toggles are `true` (every section shown).

    [Fact(DisplayName = "U04 the seeded SiteContent defaults match the shipped kw-l text and every toggle is true")]
    public async Task FreshBoot_DefaultsMatchShippedKwLText()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await FirstBootSeeder.SeedSiteContentAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var row = await q.LoadAsync<SiteContent.SiteContent>(SiteContent.SiteContent.SingletonId, ct);
        Assert.NotNull(row);
        Assert.Equal(SiteContent.SiteContent.SingletonId, row!.Id);

        // The 4 text fields are byte-identical to the shipped `en` registry
        // text (SITE·3) — read from KnownTranslationKeys.EnValues, the same
        // single source the seeder reads, so the comparison is exact.
        Assert.Equal(KnownTranslationKeys.EnValues["home.intro_eyebrow"], row.HomeHeroEyebrow);
        Assert.Equal(KnownTranslationKeys.EnValues["home.intro_lead"], row.HomeHeroLead);
        Assert.Equal(KnownTranslationKeys.EnValues["about.eyebrow"], row.AboutHeroEyebrow);
        Assert.Equal(KnownTranslationKeys.EnValues["about.lead"], row.AboutHeroLead);

        // All 9 section toggles default `true` (every section shown).
        Assert.True(row.HomeShowAboutButton);
        Assert.True(row.HomeShowFeatures);
        Assert.True(row.HomeShowRoadmap);
        Assert.True(row.AboutShowFeatures);
        Assert.True(row.AboutShowScope);
        Assert.True(row.AboutShowPhilosophy);
        Assert.True(row.AboutShowProject);
        Assert.True(row.AboutShowWhatsNew);
        Assert.True(row.AboutShowContactCta);
    }

    // ── 3 — a second boot is idempotent (no duplicate, never-overwrites) ───
    // (the SeedLanguageCatalogAsync / SeedDefaultPagesAsync create-if-missing
    // shape — a second boot leaves an existing row's fields untouched, so an
    // admin's later edit is honored; SITE·6 / SITE·3).

    [Fact(DisplayName = "U04 a second boot is idempotent: no duplicate row and no field change (never-overwrites)")]
    public async Task SecondBoot_IsIdempotent_NoDuplicateRow_NoFieldChange()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // Boot 1 — seed the singleton.
        await FirstBootSeeder.SeedSiteContentAsync(store, NullLogger.Instance, ct);

        // Simulate an admin's later edit (the very thing a warm re-run must
        // NOT clobber): change a text field and a toggle, commit.
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            var row = await session.LoadAsync<SiteContent.SiteContent>(SiteContent.SiteContent.SingletonId, ct);
            Assert.NotNull(row);
            row!.HomeHeroEyebrow = "Welcome to Maplewood";
            row.AboutShowPhilosophy = false;
            // Explicit Store (the repo's test idiom — the LS U04 / PG seeder
            // pins): Marten dirty-tracking is not enabled in this harness, so a
            // mutated loaded doc is not saved by SaveChangesAsync alone; the
            // explicit Store makes the "admin's later edit" real and committed.
            session.Store(row);
            await session.SaveChangesAsync(ct);
        }

        // Boot 2 — a second seed run must be a no-op (create-if-missing).
        await FirstBootSeeder.SeedSiteContentAsync(store, NullLogger.Instance, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<SiteContent.SiteContent>().ToListAsync(ct);

        // No duplicate row from the second boot.
        Assert.Single(rows);
        Assert.Equal(SiteContent.SiteContent.SingletonId, rows[0].Id);

        // The admin's edit survived the second boot (never-overwrites, SITE·6).
        Assert.Equal("Welcome to Maplewood", rows[0].HomeHeroEyebrow);
        Assert.False(rows[0].AboutShowPhilosophy);

        // The fields the admin did NOT touch remain the shipped defaults
        // (the second boot left them exactly as boot 1 wrote them).
        Assert.Equal(KnownTranslationKeys.EnValues["home.intro_lead"], rows[0].HomeHeroLead);
        Assert.Equal(KnownTranslationKeys.EnValues["about.lead"], rows[0].AboutHeroLead);
        Assert.True(rows[0].AboutShowScope);
    }

    // ─── Shared helper ──────────────────────────────────────────────────────

    /// <summary>
    /// Boot a fresh scratch store with the <c>SITE</c> doc type registered in
    /// the <c>AddMarten</c> lambda (the U03 drift note — the host
    /// <c>Program.cs</c> line is deferred to U05, so the harness wires it
    /// here, the same idiom as every other <c>*DocTypes</c> test harness).
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
            SiteContentDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }
}
