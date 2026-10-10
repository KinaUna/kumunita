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

    // ─── ADR 0158 — the LBL-2 surface-label translation lanes ─────────────
    // The ADR 0152 §D8 "future LBL-2 translation lane", implemented on the
    // ADR 0157 SITE-2 hero-translation shape (the GetTranslationsAsync read
    // seam + the Add / Update / RemoveTranslationAsync write lanes), carried
    // onto the surface-labels surface. The resolver overlay (the
    // SurfaceLabelTranslation row wins over the singleton, then the kw-l
    // floor) and the "at least one non-blank" write-seam rule are pinned
    // here.

    // ── 9 — a set translation wins over the singleton (the resolver overlay) ──

    [Fact(DisplayName = "ADR0158 a set translation for the surface in the language wins over the singleton")]
    public async Task GetLabelAsync_TranslationWinsOverSingleton()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var translations = Substitute.For<ITranslationProvider>();
        // The kw-l floor (only used when neither the translation nor the
        // singleton is set for this surface).
        translations.GetAsync("nav.announcements", Arg.Any<string?>()).Returns("Ankündigungen");
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations);

        // Seed the singleton with a non-blank override (the M29·8 set-label
        // state).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new SurfaceLabels.SurfaceLabels
            {
                Id            = SurfaceLabels.SurfaceLabels.SingletonId,
                Announcements = "News",
            });
            await session.SaveChangesAsync(ct);
        }

        // The translation row (in "de") carries its own value for Announcements
        // (the ADR 0158 D5 overlay — the per-language row wins over the
        // singleton, which is a single-string override shown in all languages,
        // M29·8).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new SurfaceLabels.SurfaceLabelTranslation
            {
                Id            = Guid.NewGuid().ToString("N"),
                LanguageCode  = "de",
                Announcements = "Meldungen",
            });
            await session.SaveChangesAsync(ct);
        }

        // The translation wins in "de".
        Assert.Equal("Meldungen", await svc.GetLabelAsync("announcements", "nav.announcements", "de", ct));

        // In "en" (no translation row), the singleton's set label still wins
        // (the M29·8 shape is unchanged — a set label is shown in all
        // languages).
        Assert.Equal("News", await svc.GetLabelAsync("announcements", "nav.announcements", "en", ct));
    }

    // ── 10 — a blank translation field falls back to the singleton ──────────

    [Fact(DisplayName = "ADR0158 a blank translation field falls back to the singleton's value")]
    public async Task GetLabelAsync_BlankTranslationField_FallsBackToSingleton()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var translations = Substitute.For<ITranslationProvider>();
        translations.GetAsync("nav.home", Arg.Any<string?>()).Returns("Home");
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations);

        // Seed the singleton with a non-blank Home override.
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new SurfaceLabels.SurfaceLabels
            {
                Id   = SurfaceLabels.SurfaceLabels.SingletonId,
                Home = "Front Door",
            });
            await session.SaveChangesAsync(ct);
        }

        // The translation row (in "de") exists but leaves Home blank (the
        // ADR 0157 optional-field shape — a blank field means "fall back to
        // the singleton's value for that surface").
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            session.Store(new SurfaceLabels.SurfaceLabelTranslation
            {
                Id           = Guid.NewGuid().ToString("N"),
                LanguageCode = "de",
                People       = "Nachbarn", // a non-blank field (so the row is valid)
                Home         = "   ",      // blank → falls back to the singleton
            });
            await session.SaveChangesAsync(ct);
        }

        // Home: the translation's blank field falls back to the singleton's
        // "Front Door" (the ADR 0158 D5 two-layer fallback).
        Assert.Equal("Front Door", await svc.GetLabelAsync("home", "nav.home", "de", ct));

        // People: the translation's non-blank value wins (the ADR 0158 D5
        // overlay).
        Assert.Equal("Nachbarn", await svc.GetLabelAsync("people", "nav.people", "de", ct));
    }

    // ── 11 — a fresh instance (no translation row) resolves the singleton /
    //    kw-l floor exactly as before ADR 0158 (the ADR 0158 D9 pin) ─────────

    [Fact(DisplayName = "ADR0158 a fresh instance (no translation row) resolves exactly as before ADR 0158")]
    public async Task GetLabelAsync_NoTranslationRow_ResolvesAsBefore()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var translations = Substitute.For<ITranslationProvider>();
        translations.GetAsync("nav.groups", Arg.Any<string?>()).Returns("Groups");
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations);

        // No singleton row, no translation row (a fresh boot before the
        // seeder ran) — the resolver degrades to the kw-l floor (the M29·2 /
        // M29·3 best-effort shape, unchanged by ADR 0158).
        Assert.Equal("Groups", await svc.GetLabelAsync("groups", "nav.groups", "en", ct));
        Assert.Equal("Groups", await svc.GetLabelAsync("groups", "nav.groups", "de", ct));
    }

    // ── 12 — AddTranslationAsync writes a row + one audit row (upsert) ──────

    [Fact(DisplayName = "ADR0158 AddTranslationAsync writes one row + one surface_labels_translation.add audit row")]
    public async Task AddTranslationAsync_WritesRowAndAuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        const string actor = "admin-adr0158-add";
        await svc.AddTranslationAsync(
            "de",
            new SurfaceLabels.SurfaceLabelTranslation { Announcements = "Meldungen", People = "Nachbarn" },
            actor, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<SurfaceLabels.SurfaceLabelTranslation>()
            .Where(t => t.LanguageCode == "de")
            .ToListAsync(ct);
        Assert.Single(rows);
        Assert.Equal("Meldungen", rows[0].Announcements);
        Assert.Equal("Nachbarn", rows[0].People);
        Assert.Equal(actor, rows[0].AuthorId);

        // Exactly one audit row (the surface_labels_translation.add action).
        var auditRows = await q.Query<AccessAudit>()
            .Where(a => a.Action == "surface_labels_translation.add" && a.TargetKind == "surface-labels")
            .ToListAsync(ct);
        Assert.Single(auditRows);
        Assert.Equal("surface_labels_translation.add", auditRows[0].Action);
        Assert.Equal("de", auditRows[0].TargetId);
        Assert.Equal(AccessVia.Admin, auditRows[0].Via);
        Assert.Equal(actor, auditRows[0].ActorId);
        Assert.Equal(AccessOutcome.Allow, auditRows[0].Outcome);
    }

    // ── 13 — AddTranslationAsync is an upsert (re-adding overwrites, one row) ──

    [Fact(DisplayName = "ADR0158 AddTranslationAsync is an upsert (two adds, one row)")]
    public async Task AddTranslationAsync_Upserts_OneRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        const string actor = "admin-adr0158-upsert";
        await svc.AddTranslationAsync(
            "de", new SurfaceLabels.SurfaceLabelTranslation { Home = "First" }, actor, ct);
        await svc.AddTranslationAsync(
            "de", new SurfaceLabels.SurfaceLabelTranslation { Home = "Second" }, actor, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<SurfaceLabels.SurfaceLabelTranslation>()
            .Where(t => t.LanguageCode == "de")
            .ToListAsync(ct);
        Assert.Single(rows);
        Assert.Equal("Second", rows[0].Home); // the latest add wins
    }

    // ── 14 — an all-blank translation is rejected (the "at least one
    //    non-blank" write-seam rule, ADR 0157 / ADR 0026) ───────────────────

    [Fact(DisplayName = "ADR0158 an all-blank translation is rejected (ArgumentException)")]
    public async Task AddTranslationAsync_AllBlank_ThrowsArgumentException()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => svc.AddTranslationAsync(
            "de", new SurfaceLabels.SurfaceLabelTranslation { Home = "   " }, "actor", ct));
        Assert.Contains("at least one surface label", ex.Message);
    }

    // ── 15 — UpdateTranslationAsync replaces the row's fields + one audit
    //    row ────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "ADR0158 UpdateTranslationAsync replaces the row's fields + one surface_labels_translation.update audit row")]
    public async Task UpdateTranslationAsync_ReplacesFieldsAndWritesAuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        // Seed the row first (the update lane is not an upsert — the add lane
        // is the row-creator).
        const string actor = "admin-adr0158-update";
        await svc.AddTranslationAsync(
            "de", new SurfaceLabels.SurfaceLabelTranslation { Home = "Erste" }, actor, ct);

        await svc.UpdateTranslationAsync(
            "de", new SurfaceLabels.SurfaceLabelTranslation { Home = "Zweite", People = "Nachbarn" }, actor, ct);

        await using var q = store.QuerySession();
        var row = await q.Query<SurfaceLabels.SurfaceLabelTranslation>()
            .Where(t => t.LanguageCode == "de")
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(row);
        Assert.Equal("Zweite", row!.Home);
        Assert.Equal("Nachbarn", row.People);

        var auditRows = await q.Query<AccessAudit>()
            .Where(a => a.Action == "surface_labels_translation.update")
            .ToListAsync(ct);
        Assert.Single(auditRows);
        Assert.Equal("de", auditRows[0].TargetId);
    }

    // ── 16 — a missing row is a KeyNotFoundException (a double-update is a
    //    shape error for the route) ─────────────────────────────────────────

    [Fact(DisplayName = "ADR0158 UpdateTranslationAsync on a missing row is a KeyNotFoundException")]
    public async Task UpdateTranslationAsync_MissingRow_ThrowsKeyNotFound()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.UpdateTranslationAsync(
            "fr", new SurfaceLabels.SurfaceLabelTranslation { Home = "X" }, "actor", ct));
    }

    // ── 17 — RemoveTranslationAsync deletes the row + one audit row ─────────

    [Fact(DisplayName = "ADR0158 RemoveTranslationAsync deletes the row + one surface_labels_translation.remove audit row")]
    public async Task RemoveTranslationAsync_DeletesRowAndWritesAuditRow()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        const string actor = "admin-adr0158-remove";
        await svc.AddTranslationAsync(
            "de", new SurfaceLabels.SurfaceLabelTranslation { Home = "X" }, actor, ct);

        await svc.RemoveTranslationAsync("de", actor, ct);

        await using var q = store.QuerySession();
        var rows = await q.Query<SurfaceLabels.SurfaceLabelTranslation>()
            .Where(t => t.LanguageCode == "de")
            .ToListAsync(ct);
        Assert.Empty(rows);

        var auditRows = await q.Query<AccessAudit>()
            .Where(a => a.Action == "surface_labels_translation.remove")
            .ToListAsync(ct);
        Assert.Single(auditRows);
        Assert.Equal("de", auditRows[0].TargetId);
        Assert.Equal(AccessVia.Admin, auditRows[0].Via);
    }

    // ── 18 — a missing row is a KeyNotFoundException (a double-remove is a
    //    shape error for the route) ─────────────────────────────────────────

    [Fact(DisplayName = "ADR0158 RemoveTranslationAsync on a missing row is a KeyNotFoundException")]
    public async Task RemoveTranslationAsync_MissingRow_ThrowsKeyNotFound()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var svc = new SurfaceLabels.SurfaceLabelsService(store, translations: null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => svc.RemoveTranslationAsync(
            "fr", "actor", ct));
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
