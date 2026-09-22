using Kumunita.Core;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// ADR 0044 — the bundled baseline language is a closed, code-owned value:
/// adding a bundled baseline language (de / fr) on a warm instance seeds its
/// full baseline (UI-string + system-page) create-if-missing, and removing it
/// resets the rows so re-adding re-seeds from the code. The read seam
/// (<see cref="LocalizationService.GetBundledBaselineAsync"/>) returns
/// <c>null</c> for a non-bundled code (its content is admin-authored, not
/// code-shipped).
/// <para>
/// The D7 test plan (ADR 0044 §D7) — four pins, each over a fresh scratch
/// Postgres (the <see cref="PostgresFixture"/> harness, the same template
/// <see cref="LocalizationServiceTests"/> uses, extended with
/// <c>PageDocTypes.Configure</c> for the <see cref="Page"/> /
/// <see cref="PageTranslation"/> surface the page-baseline half touches):
/// </para>
/// <ol>
/// <li><b>Parity</b> — <c>AddLanguageAsync("de")</c> on an <c>en</c>-seeded
/// store → the <c>de</c> <see cref="TranslationResource"/> rows equal
/// <see cref="KnownTranslationKeys.DeValues"/> key-for-key and value-for-value,
/// and the <c>de</c> <see cref="PageTranslation"/> rows equal
/// <see cref="FirstBootSeeder.DeDefaultPages"/>; the <c>en</c> rows are
/// unchanged. Same pin for <c>fr</c>.</li>
/// <li><b>Warm-add is a no-op</b> — re-adding an already-added bundled code
/// does not overwrite an admin edit (create-if-missing, the ADR 0042 D1
/// invariant).</li>
/// <li><b>Reset</b> — <c>RemoveLanguageAsync("de")</c> on a bundled code
/// deletes its <see cref="TranslationResource"/> +
/// <see cref="PageTranslation"/> rows; re-adding re-seeds from the code (the
/// round-trip). A non-bundled code's rows are retained (M·7) on removal.</li>
/// <li><b>Non-bundled code</b> — <c>AddLanguageAsync("pl")</c> adds only the
/// catalog row (no UI-string or page-baseline seed), and
/// <c>GetBundledBaselineAsync("pl")</c> is <c>null</c>.</li>
/// </ol>
/// </summary>
public class ADR_0044_BaselineTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — Parity (the headline): warm-add seeds the full baseline, exact ──
    [Theory]
    [InlineData("de", "Deutsch")]
    [InlineData("fr", "Français")]
    [InlineData("da", "Dansk")]
    public async Task ADR0044_Parity_WarmAdd_SeedsFullBaseline_Exact(
        string code, string nativeName)
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // The pristine-boot state: the en catalog row + the en UI-string floor
        // + the en system pages (terms / help / privacy / conduct). de / fr are
        // NOT in the catalog yet (a warm instance that never added them).
        await SeedEnPristineStateAsync(store);

        var svc = new LocalizationService(store);
        await svc.AddLanguageAsync(code, nativeName, "admin-a1");

        await using var q = store.QuerySession();

        // The catalog row exists (enabled).
        var catalog = await q.LoadAsync<LanguageCatalog>(code, ct);
        Assert.NotNull(catalog);
        Assert.True(catalog!.Enabled);

        // UI strings: the de / fr / da set equals the code baseline, key-for-key
        // and value-for-value (the headline parity — no drift).
        var baselineUi = code switch
        {
            "de" => KnownTranslationKeys.DeValues,
            "fr" => KnownTranslationKeys.FrValues,
            "da" => KnownTranslationKeys.DaValues,
            _ => throw new NotSupportedException(code)
        };
        var baselinePages = code switch
        {
            "de" => FirstBootSeeder.DeDefaultPages(),
            "fr" => FirstBootSeeder.FrDefaultPages(),
            "da" => FirstBootSeeder.DaDefaultPages(),
            _ => throw new NotSupportedException(code)
        };

        var uiRows = await q.Query<TranslationResource>()
            .Where(t => t.LanguageCode == code)
            .ToListAsync(ct);
        Assert.Equal(baselineUi.Count, uiRows.Count);
        Assert.Equal(baselineUi.Count, uiRows.Select(t => t.Key).Distinct().Count());

        var uiByKey = uiRows.ToDictionary(t => t.Key, t => t.Text);
        foreach (var (key, text) in baselineUi)
            Assert.Equal(text, uiByKey[key]);

        // Pages: one PageTranslation per seeded page, attached to the page's own
        // id, body + title matching the code baseline exactly.
        var pageTranslations = await q.Query<PageTranslation>()
            .Where(t => t.LanguageCode == code)
            .ToListAsync(ct);
        Assert.Equal(baselinePages.Length, pageTranslations.Count);

        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstAsync(ct);

        foreach (var (slug, title, body) in baselinePages)
        {
            var page = await q.Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == systemRoot.Id)
                .FirstAsync(ct);

            var row = pageTranslations.Single(t => t.PageId == page.Id);
            Assert.NotEqual(systemRoot.Id, page.Id);   // not on the root container
            Assert.Equal(title, row.Title);
            Assert.Equal(body, row.Body);
        }

        // The en rows are unchanged (the source language was not touched).
        var enUiCount = await q.Query<TranslationResource>()
            .Where(t => t.LanguageCode == "en")
            .CountAsync(ct);
        Assert.Equal(KnownTranslationKeys.EnValues.Count, enUiCount);

        // The other bundled language was NOT seeded (only `code` was added).
        var otherCode = code == "de" ? "fr" : "de";
        var otherUi = await q.Query<TranslationResource>()
            .Where(t => t.LanguageCode == otherCode)
            .CountAsync(ct);
        Assert.Equal(0, otherUi);
    }

    // ── 2 — Warm-add is a no-op (create-if-missing, ADR 0042 D1) ─────────────
    [Fact]
    public async Task ADR0044_WarmAdd_NoOp_AdminEditSurvives()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedEnPristineStateAsync(store);

        var svc = new LocalizationService(store);
        const string code = "de";
        await svc.AddLanguageAsync(code, "Deutsch", "admin-a2");

        // An admin edits one de UI-string value in-app (the ADR 0021 editor —
        // a direct row write).
        const string key = "nav.home";
        Assert.True(KnownTranslationKeys.DeValues.ContainsKey(key));
        await UpsertTranslationAsync(store, key, code, "ADMIN-EDIT");

        // A re-add of the same code (the warm re-run shape — the catalog row
        // refreshes, the baseline seed is create-if-missing → skips the edit).
        await svc.AddLanguageAsync(code, "Deutsch", "admin-a2");

        await using var q = store.QuerySession();

        // The admin's edit survived — the re-add skipped the existing row.
        var row = await q.Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == code)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(row);
        Assert.Equal("ADMIN-EDIT", row!.Text);

        // Exactly one row per (key, code) — no duplicate from the re-add.
        var count = await q.Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == code)
            .CountAsync(ct);
        Assert.Equal(1, count);

        // The full de set is still exactly the baseline size (no growth).
        var uiCount = await q.Query<TranslationResource>()
            .Where(t => t.LanguageCode == code)
            .CountAsync(ct);
        Assert.Equal(KnownTranslationKeys.DeValues.Count, uiCount);
    }

    // ── 3 — Reset: remove deletes the bundled rows; re-add re-seeds ─────────
    [Fact]
    public async Task ADR0044_Reset_RemoveDeletes_ReaddReseeds()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedEnPristineStateAsync(store);

        var svc = new LocalizationService(store);
        const string code = "de";
        await svc.AddLanguageAsync(code, "Deutsch", "admin-a3");

        // Admin edits a de UI-string (an edit that the reset should discard).
        const string key = "nav.home";
        await UpsertTranslationAsync(store, key, code, "ADMIN-EDIT");

        // Remove de — the reset: its UI-string + page rows are deleted, the
        // catalog row is removed. (de is not the default — en is — so the
        // M·7 fail-closed check does not block this.)
        await svc.RemoveLanguageAsync(code, "admin-a3");

        await using (var q = store.QuerySession())
        {
            // The catalog row is gone.
            var catalog = await q.LoadAsync<LanguageCatalog>(code, ct);
            Assert.Null(catalog);

            // The de UI-string rows are deleted (the reset).
            var uiCount = await q.Query<TranslationResource>()
                .Where(t => t.LanguageCode == code)
                .CountAsync(ct);
            Assert.Equal(0, uiCount);

            // The de page rows are deleted too.
            var pageCount = await q.Query<PageTranslation>()
                .Where(t => t.LanguageCode == code)
                .CountAsync(ct);
            Assert.Equal(0, pageCount);

            // The en rows are untouched (the reset only touched de).
            var enUiCount = await q.Query<TranslationResource>()
                .Where(t => t.LanguageCode == "en")
                .CountAsync(ct);
            Assert.Equal(KnownTranslationKeys.EnValues.Count, enUiCount);
        }

        // Re-add de — the baseline is re-seeded from the code (the round-trip).
        await svc.AddLanguageAsync(code, "Deutsch", "admin-a3");

        await using var q2 = store.QuerySession();
        var uiCount2 = await q2.Query<TranslationResource>()
            .Where(t => t.LanguageCode == code)
            .CountAsync(ct);
        Assert.Equal(KnownTranslationKeys.DeValues.Count, uiCount2);

        var pageCount2 = await q2.Query<PageTranslation>()
            .Where(t => t.LanguageCode == code)
            .CountAsync(ct);
        Assert.Equal(FirstBootSeeder.DeDefaultPages().Length, pageCount2);
    }

    // ── 3b — Reset: a non-bundled code's rows are RETAINED (M·7) ────────────
    [Fact]
    public async Task ADR0044_Reset_NonBundledRowsRetained()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedEnPristineStateAsync(store);

        var svc = new LocalizationService(store);
        const string code = "pl";
        await svc.AddLanguageAsync(code, "Polski", "admin-a3b");

        // An admin authors a pl UI string (a custom code's content).
        const string key = "nav.home";
        await UpsertTranslationAsync(store, key, code, "Strona główna");

        // Remove pl — the M·7 retention: the pl UI-string row is kept.
        await svc.RemoveLanguageAsync(code, "admin-a3b");

        await using (var q = store.QuerySession())
        {
            // The catalog row is gone.
            var catalog = await q.LoadAsync<LanguageCatalog>(code, ct);
            Assert.Null(catalog);

            // The pl UI-string row is RETAINED (a custom code's content is not
            // the code's baseline — re-adding restores it).
            var uiCount = await q.Query<TranslationResource>()
                .Where(t => t.LanguageCode == code)
                .CountAsync(ct);
            Assert.Equal(1, uiCount);
            Assert.Equal("Strona główna",
                (await q.Query<TranslationResource>()
                    .Where(t => t.Key == key && t.LanguageCode == code)
                    .FirstAsync(ct)).Text);
        }

        // Re-add pl — the retained row is restored (the M·7 invariant).
        await svc.AddLanguageAsync(code, "Polski", "admin-a3b");

        await using var q2 = store.QuerySession();
        var row = await q2.Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == code)
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(row);
        Assert.Equal("Strona główna", row!.Text);
    }

    // ── 4 — Non-bundled code: no baseline seeded; the read is null ──────────
    [Fact]
    public async Task ADR0044_NonBundled_NoBaseline_ReadIsNull()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedEnPristineStateAsync(store);

        var svc = new LocalizationService(store);

        // The read seam returns null for a non-bundled code (a pure read — no
        // database access, so it works on a pristine store).
        Assert.Null(await svc.GetBundledBaselineAsync("pl"));

        // Add pl — only the catalog row (no UI-string or page-baseline seed).
        await svc.AddLanguageAsync("pl", "Polski", "admin-a4");

        await using var q = store.QuerySession();
        var uiCount = await q.Query<TranslationResource>()
            .Where(t => t.LanguageCode == "pl")
            .CountAsync(ct);
        Assert.Equal(0, uiCount);

        var pageCount = await q.Query<PageTranslation>()
            .Where(t => t.LanguageCode == "pl")
            .CountAsync(ct);
        Assert.Equal(0, pageCount);

        // And the bundled reads are non-null (the seam distinguishes the two).
        var de = await svc.GetBundledBaselineAsync("de");
        Assert.NotNull(de);
        Assert.Equal(KnownTranslationKeys.DeValues.Count, de!.UiStrings.Count);
        Assert.Equal(FirstBootSeeder.DeDefaultPages().Length, de.PageBaselines.Count);
    }

    // ─── Shared helpers ─────────────────────────────────────────────────────

    /// <summary>Boot a fresh scratch store (M1 + M3 + Page doc types) — the
    /// <see cref="LS_U04_SeederTests.BootStoreAsync"/> shape.</summary>
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
            PageDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>
    /// The pristine-boot state an <c>en</c>-defaulted instance carries: the
    /// <c>en</c> catalog row + the <c>en</c> UI-string floor + the <c>en</c>
    /// system pages (terms / help / privacy / conduct) under the <c>system</c>
    /// root. Mirrors the <see cref="FirstBootSeeder"/> first-boot writes (the
    /// <see cref="LS_U04_SeederTests.SeedFloorAndBaselinesAsync"/> /
    /// <see cref="FirstBootSeeder.SeedDefaultPagesAsync"/> shape) — de / fr are
    /// NOT seeded (the warm-instance state this test drives).
    /// </summary>
    private static async Task SeedEnPristineStateAsync(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());
        var now = DateTimeOffset.UtcNow;

        // The en catalog row + the LocaleSettings singleton (default en).
        session.Store(new LanguageCatalog
        {
            Id = "en", NativeName = "English", Enabled = true, SortOrder = 0
        });
        session.Store(new LocaleSettings
        {
            Id = LocaleSettings.SingletonId,
            DefaultLanguageCode = "en"
        });

        // The en UI-string floor (the code-wins seed).
        foreach (var (key, enText) in KnownTranslationKeys.EnValues)
            session.Store(new TranslationResource
            {
                Id = Guid.NewGuid().ToString("N"),
                Key = key,
                LanguageCode = "en",
                Text = enText
            });

        // The en system pages (terms / help / privacy / conduct under the
        // `system` root) — the seeder's exact page-seeding surface.
        await FirstBootSeeder.SeedDefaultPagesAsync(
            session, FirstBootSeeder.EnDefaultPages(), now, ct);

        await session.SaveChangesAsync(ct);
    }

    /// <summary>Upsert a <see cref="TranslationResource"/> row via a direct
    /// write (test fixture seeding — the ADR 0021 editor's service-write
    /// shape).</summary>
    private static async Task UpsertTranslationAsync(
        IDocumentStore store, string key, string languageCode, string text)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());
        var existing = await session
            .Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct);
        if (existing is null)
            session.Store(new TranslationResource
            {
                Id = Guid.NewGuid().ToString("N"),
                Key = key,
                LanguageCode = languageCode,
                Text = text
            });
        else
        {
            existing.Text = text;
            session.Store(existing);
        }
        await session.SaveChangesAsync(ct);
    }
}
