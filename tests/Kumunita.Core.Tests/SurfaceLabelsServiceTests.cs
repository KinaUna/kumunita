using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Marten;
using Marten.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M29 U09 (ADR 0152) — the <see cref="Kumunita.Core.SurfaceLabels.SurfaceLabelsService"/>
/// read + write lane pins (the ADR 0150 single-write-lane shape + the read-seam
/// contract, M29·2 / M29·5):
/// <list type="number">
/// <item>A missing store or a missing row degrades to the all-null fallback
///       (never throws, never null — M29·2 / M29·6).</item>
/// <item>A save writes exactly one <c>AccessAudit</c> row (<c>Via = Admin</c>,
///       action <c>surface_labels.save</c>, <c>TargetKind</c> "surface-labels",
///       <c>TargetId</c> "singleton") (M29·5).</item>
/// <item>Strong consistency — the saved value is live on the very next
///       <c>GetAsync</c> (invariant C4, M29·5).</item>
/// <item>The singleton is upserted (no duplicate row) (M29·6).</item>
/// <item>A blank stored label is stored blank and falls back to the <c>kw-l</c>
///       key at resolution (M29·3 / M29·4).</item>
/// <item>A non-blank admin label is returned as the resolved label (all
///       languages — M29·8); an unset label resolves to the <c>kw-l</c>
///       fallback (M29·3 / M29·4).</item>
/// </list>
/// </summary>
/// <para>
/// <b>Harness note (the U03 drift note):</b> mirrors <see
/// cref="SiteContentServiceTests"/> — the <c>SurfaceLabelsDocTypes.Configure</c>
/// call is wired in the test's own <c>DocumentStore.For</c> lambda (without it
/// the <c>SurfaceLabels</c> doc is invisible to Marten). The <c>SaveAsync_*</c>
/// pins <b>seed a row first</b> (the U03 pin — <c>SaveAsync</c> is a no-op on a
/// missing row; the seeder is the only row-creator, M29·6), then assert. These
/// pins never assert that a save <i>creates</i> a row.
/// </para>
/// <para>
/// <b>Collision idiom:</b> the doc type is fully qualified as
/// <c>SurfaceLabels.SurfaceLabels</c> (the type and its parent namespace share
/// the name <c>SurfaceLabels</c>; the unqualified name in this namespace
/// resolves to the *namespace*) — the exact <c>SiteContent.SiteContent</c>
/// idiom used throughout <see cref="SiteContentSeederTests"/>.
/// </para>
public class SurfaceLabelsServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — a missing store degrades to the all-null fallback (M29·2/M29·6) ──

    [Fact(DisplayName = "U09 GetAsync with no store degrades to the all-null fallback")]
    public async Task GetAsync_MissingStore_ReturnsAllNullFallback()
    {
        var ct = TestContext.Current.CancellationToken;

        // A store whose QuerySession throws (a test construction with no store) —
        // GetAsync is best-effort and degrades to the in-code all-null fallback.
        var store = Substitute.For<IDocumentStore>();
        store.When(x => x.QuerySession()).Do(x => throw new InvalidOperationException("no store wired"));
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        var result = await svc.GetAsync(ct);

        Assert.NotNull(result);
        Assert.Equal(SurfaceLabels.SurfaceLabels.SingletonId, result.Id);
        AssertAllNulls(result);
    }

    // ── 2 — a missing row degrades to the all-null fallback (M29·2/M29·6) ──

    [Fact(DisplayName = "U09 GetAsync with no row degrades to the all-null fallback")]
    public async Task GetAsync_MissingRow_ReturnsAllNullFallback()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        // No row seeded — a fresh boot before the seeder ran.
        var result = await svc.GetAsync(ct);

        Assert.NotNull(result);
        Assert.Equal(SurfaceLabels.SurfaceLabels.SingletonId, result.Id);
        AssertAllNulls(result);
    }

    // ── 3 — a save writes exactly one AccessAudit row (M29·5) ─────────────

    [Fact(DisplayName = "U09 SaveAsync writes exactly one AccessAudit row (surface_labels.save, surface-labels, Admin)")]
    public async Task SaveAsync_WritesOneAccessAuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        // Seed a row so the save is not a no-op (the U03 pin, M29·6).
        await SeedRowAsync(store, ct);

        const string actor = "admin-u09-audit";
        await svc.SaveAsync(new SurfaceLabels.SurfaceLabels
        {
            Home        = "Front",
            Announcements = "News",
        }, actor, ct);

        await using var q = store.QuerySession();
        var auditRows = await q.Query<AccessAudit>()
            .Where(a => a.Action == "surface_labels.save" && a.TargetKind == "surface-labels")
            .ToListAsync(ct);

        Assert.Single(auditRows);
        Assert.Equal("surface_labels.save", auditRows[0].Action);
        Assert.Equal("surface-labels", auditRows[0].TargetKind);
        Assert.Equal(SurfaceLabels.SurfaceLabels.SingletonId, auditRows[0].TargetId);
        Assert.Equal(AccessVia.Admin, auditRows[0].Via);
        Assert.Equal(actor, auditRows[0].ActorId);
        Assert.Equal(actor, auditRows[0].EffectivePrincipalId);
        Assert.Equal(AccessOutcome.Allow, auditRows[0].Outcome);
    }

    // ── 4 — strong consistency: live on the next GetAsync (C4, M29·5) ─────

    [Fact(DisplayName = "U09 SaveAsync is strong-consistency (live on the next GetAsync)")]
    public async Task SaveAsync_StrongConsistency_LiveOnNextGetAsync()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        await SeedRowAsync(store, ct);

        const string actor = "admin-u09-consistency";
        await svc.SaveAsync(new SurfaceLabels.SurfaceLabels
        {
            Home   = "Front Door",
            People = "Neighbours",
        }, actor, ct);

        // The very next GetAsync reflects the change (strong consistency).
        var result = await svc.GetAsync(ct);
        Assert.Equal("Front Door", result.Home);
        Assert.Equal("Neighbours", result.People);
        // Untouched fields remain null (the all-null default, M29·4).
        Assert.Null(result.Announcements);
    }

    // ── 5 — upserts the singleton (no duplicate row) (M29·6) ──────────────

    [Fact(DisplayName = "U09 SaveAsync upserts the singleton (two saves, one row)")]
    public async Task SaveAsync_UpsertsSingleton_NoDuplicateRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        await SeedRowAsync(store, ct);

        const string actor = "admin-u09-upsert";

        // Save 1.
        await svc.SaveAsync(new SurfaceLabels.SurfaceLabels { Groups = "First" }, actor, ct);

        // Save 2 (a different value).
        await svc.SaveAsync(new SurfaceLabels.SurfaceLabels { Groups = "Second" }, actor, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<SurfaceLabels.SurfaceLabels>().ToListAsync(ct);

        // Still exactly one row (upsert, not append, M29·6).
        Assert.Single(rows);
        Assert.Equal(SurfaceLabels.SurfaceLabels.SingletonId, rows[0].Id);
        // The latest save wins.
        Assert.Equal("Second", rows[0].Groups);
    }

    // ── 6 — a blank stored label is stored blank + falls back at resolution ──
    // (M29·3 / M29·4 — the §2.5 pin 6 shape)

    [Fact(DisplayName = "U09 a blank stored label is stored blank and falls back to the kw-l key at resolution")]
    public async Task SaveAsync_BlankLabel_StoredBlank_FallsBackAtResolution()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var translations = Substitute.For<ITranslationProvider>();
        translations.GetAsync("nav.home", Arg.Any<string?>()).Returns("Home");
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations);

        await SeedRowAsync(store, ct);

        const string actor = "admin-u09-blank";
        // Save a whitespace-only label for Home (a "blank" override).
        await svc.SaveAsync(new SurfaceLabels.SurfaceLabels { Home = "   " }, actor, ct);

        // Stored blank (stored verbatim, not normalized to null — M29·3).
        var stored = await svc.GetAsync(ct);
        Assert.True(string.IsNullOrWhiteSpace(stored.Home));

        // Falls back to the kw-l key at resolution (M29·3 / M29·4).
        var resolved = await svc.GetLabelAsync("home", "nav.home", "en", ct);
        Assert.Equal("Home", resolved);
    }

    // ── 7 — a non-blank admin label is returned as the resolved label ──────
    // (M29·3, M29·8 — a single-string label shown in ALL languages)

    [Fact(DisplayName = "U09 a non-blank admin label is returned as the resolved label (all languages)")]
    public async Task GetLabelAsync_ReturnsOverrideWhenSet()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var translations = Substitute.For<ITranslationProvider>();
        // A set override never falls back, so the provider value is never used
        // here — but the floor is wired to prove the override wins over it.
        translations.GetAsync("nav.announcements", Arg.Any<string?>()).Returns("Ankündigungen");
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations);

        // Seed a row with a non-blank override.
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new SurfaceLabels.SurfaceLabels
            {
                Id            = SurfaceLabels.SurfaceLabels.SingletonId,
                Announcements = "News",
            });
            await session.SaveChangesAsync(ct);
        }

        // The override is returned verbatim — a single-string label shown in
        // ALL languages (M29·8): the viewer's language only matters for the
        // kw-l fallback, which never fires when a label is set.
        Assert.Equal("News", await svc.GetLabelAsync("announcements", "nav.announcements", "en", ct));
        Assert.Equal("News", await svc.GetLabelAsync("announcements", "nav.announcements", "de", ct));
    }

    // ── 8 — an unset label resolves to the kw-l fallback (M29·3/M29·4) ────

    [Fact(DisplayName = "U09 an unset label's stored value is null and it resolves to the kw-l fallback")]
    public async Task GetLabelAsync_ReturnsNullWhenNotSet()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var translations = Substitute.For<ITranslationProvider>();
        translations.GetAsync("nav.home", Arg.Any<string?>()).Returns("Home");
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations);

        // Seed a row where Home is left unset (null).
        await SeedRowAsync(store, ct);

        // The stored override for Home is null (the "not set" state, M29·3/M29·4).
        var stored = await svc.GetAsync(ct);
        Assert.Null(stored.Home);

        // The resolved label falls back to the kw-l key (M29·3 / M29·4).
        var resolved = await svc.GetLabelAsync("home", "nav.home", "en", ct);
        Assert.Equal("Home", resolved);
    }

    // ─── Shared helpers ─────────────────────────────────────────────────────

    /// <summary>Asserts all 13 label overrides are null (the all-null fallback, M29·3/M29·4).</summary>
    private static void AssertAllNulls(SurfaceLabels.SurfaceLabels row)
    {
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
    }

    /// <summary>
    /// Boot a fresh scratch store with the M29 doc type registered in the
    /// <c>DocumentStore.For</c> lambda (the U03 drift note — without it the
    /// <c>SurfaceLabels</c> doc is invisible to Marten).
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

    /// <summary>
    /// Store a <c>SurfaceLabels</c> singleton row so the save is not a no-op
    /// (the U03 pin — <c>SaveAsync</c> is a no-op when the row is absent, M29·6;
    /// the seeder is the only row-creator, so this stands in for the seeder).
    /// </summary>
    private static async Task SeedRowAsync(IDocumentStore store, CancellationToken ct)
    {
        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session.LoadAsync<SurfaceLabels.SurfaceLabels>(SurfaceLabels.SurfaceLabels.SingletonId, ct);
        if (existing is null)
        {
            session.Store(new SurfaceLabels.SurfaceLabels
            {
                Id = SurfaceLabels.SurfaceLabels.SingletonId,
            });
            await session.SaveChangesAsync(ct);
        }
    }
}
