using Kumunita.Core;
using Kumunita.Core.Authorization;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// SITE U08 (ADR 0150) — the <see cref="SiteContentService"/> read + write
/// lane pins (the ADR 0050 single-write-lane shape, SITE·2 / SITE·6):
/// the read degrades to the in-code fallback when the row is absent;
/// a save writes exactly one AccessAudit row (action "site.save",
/// TargetKind "site", Via Admin); strong consistency (live on the next
/// GetAsync); and the singleton is upserted (no duplicate row).
/// </summary>
/// <para>
/// <b>Harness note:</b> mirrors the <see cref="SiteContentSeederTests"/>
/// harness shape — the <c>SiteContentDocTypes.Configure</c> call is
/// wired in the test's own <c>DocumentStore.For</c> lambda (the same
/// idiom as every other <c>*DocTypes</c> test harness).
/// </para>
public class SiteContentServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — a missing row degrades to the in-code fallback (SITE·1) ──────
    // A fresh store (no SiteContent row) + GetAsync returns a SiteContent
    // with every field at its shipped default — the byte-identical kw-l
    // text + every section shown. Never throws, never returns null.

    [Fact(DisplayName = "U08 GetAsync with no row returns the in-code fallback (all defaults)")]
    public async Task GetAsync_MissingStore_ReturnsInCodeFallback()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SiteContent.SiteContentService(store);

        // No row seeded — a fresh boot before the seeder ran.
        var result = await svc.GetAsync(ct);

        Assert.NotNull(result);
        Assert.Equal(SiteContent.SiteContent.SingletonId, result.Id);
        // The 4 text fields are the shipped defaults (byte-identical to kw-l).
        Assert.Equal("A private home for one neighbourhood", result.HomeHeroEyebrow);
        Assert.Equal(
            "One quiet place for everything your street does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.",
            result.HomeHeroLead);
        Assert.Equal("Private by default", result.AboutHeroEyebrow);
        Assert.Equal(
            "One home for everything your neighborhood does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.",
            result.AboutHeroLead);
        // All 9 toggles default true (every section shown).
        Assert.True(result.HomeShowAboutButton);
        Assert.True(result.HomeShowFeatures);
        Assert.True(result.HomeShowRoadmap);
        Assert.True(result.AboutShowFeatures);
        Assert.True(result.AboutShowScope);
        Assert.True(result.AboutShowPhilosophy);
        Assert.True(result.AboutShowProject);
        Assert.True(result.AboutShowWhatsNew);
        Assert.True(result.AboutShowContactCta);
    }

    // ── 2 — a save writes exactly one AccessAudit row (SITE·2) ────────────
    // The audit row: Via = Admin, action "site.save", TargetKind "site",
    // TargetId "singleton" — the signup.set-open / timezone.set-default shape.

    [Fact(DisplayName = "U08 SaveAsync writes exactly one AccessAudit row (site.save, site, Admin)")]
    public async Task SaveAsync_WritesOneAccessAuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SiteContent.SiteContentService(store);

        // Seed a row so the save is not a no-op.
        await SeedRowAsync(store, ct);

        const string actor = "admin-u08-audit";
        await svc.SaveAsync(new SiteContent.SiteContent
        {
            HomeHeroEyebrow = "Welcome to Maplewood",
            HomeHeroLead    = "A community that cares.",
            AboutHeroEyebrow = "Private by default",
            AboutHeroLead    = "One home for everything your neighborhood does.",
        }, actor, ct);

        // Exactly one AccessAudit row with the expected shape.
        await using var q = store.QuerySession();
        var auditRows = await q.Query<AccessAudit>()
            .Where(a => a.Action == "site.save" && a.TargetKind == "site")
            .ToListAsync(ct);

        Assert.Single(auditRows);
        Assert.Equal("site.save", auditRows[0].Action);
        Assert.Equal("site", auditRows[0].TargetKind);
        Assert.Equal(SiteContent.SiteContent.SingletonId, auditRows[0].TargetId);
        Assert.Equal(AccessVia.Admin, auditRows[0].Via);
        Assert.Equal(actor, auditRows[0].ActorId);
        Assert.Equal(actor, auditRows[0].EffectivePrincipalId);
        Assert.Equal(AccessOutcome.Allow, auditRows[0].Outcome);
    }

    // ── 3 — strong consistency: live on the next GetAsync (SITE·2, C4) ────
    // A save is immediately visible on the very next GetAsync call — no
    // restart, no projection lag.

    [Fact(DisplayName = "U08 SaveAsync is strong-consistency (live on the next GetAsync)")]
    public async Task SaveAsync_StrongConsistency_LiveOnNextGetAsync()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SiteContent.SiteContentService(store);

        // Seed a row so the save is not a no-op.
        await SeedRowAsync(store, ct);

        const string actor = "admin-u08-consistency";
        await svc.SaveAsync(new SiteContent.SiteContent
        {
            HomeHeroEyebrow = "The new hero text",
            HomeHeroLead    = "A completely new lead paragraph.",
            AboutHeroEyebrow = "Private by default",
            AboutHeroLead    = "One home for everything your neighborhood does.",
        }, actor, ct);

        // The very next GetAsync reflects the change (strong consistency).
        var result = await svc.GetAsync(ct);
        Assert.Equal("The new hero text", result.HomeHeroEyebrow);
        Assert.Equal("A completely new lead paragraph.", result.HomeHeroLead);
        // Untouched fields remain at their defaults.
        Assert.Equal("Private by default", result.AboutHeroEyebrow);
    }

    // ── 4 — upserts the singleton (no duplicate row) (SITE·6) ─────────────
    // A save never creates a second row — the singleton is upserted in
    // place. Two consecutive saves still yield exactly one row.

    [Fact(DisplayName = "U08 SaveAsync upserts the singleton (two saves, one row)")]
    public async Task SaveAsync_UpsertsSingleton_NoDuplicateRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SiteContent.SiteContentService(store);

        // Seed a row so the save is not a no-op.
        await SeedRowAsync(store, ct);

        const string actor = "admin-u08-upsert";

        // Save 1.
        await svc.SaveAsync(new SiteContent.SiteContent
        {
            HomeHeroEyebrow = "First save",
            HomeHeroLead    = "First lead",
            AboutHeroEyebrow = "Private by default",
            AboutHeroLead    = "One home for everything your neighborhood does.",
        }, actor, ct);

        // Save 2 (a different value).
        await svc.SaveAsync(new SiteContent.SiteContent
        {
            HomeHeroEyebrow = "Second save",
            HomeHeroLead    = "Second lead",
            AboutHeroEyebrow = "Private by default",
            AboutHeroLead    = "One home for everything your neighborhood does.",
        }, actor, ct);

        // Still exactly one row.
        await using var q = store.QuerySession();
        var rows = await q.Query<SiteContent.SiteContent>().ToListAsync(ct);
        Assert.Single(rows);
        Assert.Equal(SiteContent.SiteContent.SingletonId, rows[0].Id);
        // The latest save wins (upsert, not append).
        Assert.Equal("Second save", rows[0].HomeHeroEyebrow);
        Assert.Equal("Second lead", rows[0].HomeHeroLead);
    }

    // ─── Shared helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Boot a fresh scratch store with the SiteContent doc type registered
    /// in the <c>DocumentStore.For</c> lambda (the same idiom as every
    /// other <c>*DocTypes</c> test harness).
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

    /// <summary>
    /// Store a SiteContent singleton row so the save is not a no-op (the
    /// service's SaveAsync is a no-op when the row is absent — SITE·6).
    /// </summary>
    private static async Task SeedRowAsync(IDocumentStore store, CancellationToken ct)
    {
        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session.LoadAsync<SiteContent.SiteContent>(SiteContent.SiteContent.SingletonId, ct);
        if (existing is null)
        {
            session.Store(new SiteContent.SiteContent
            {
                Id = SiteContent.SiteContent.SingletonId,
                // Use the defaults — the save under test will override them.
            });
            await session.SaveChangesAsync(ct);
        }
    }
}
