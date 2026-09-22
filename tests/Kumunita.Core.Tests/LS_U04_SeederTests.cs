using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// LS U04 (ADR 0042 D1/D2/D4) — the first-boot state pins for the bundled
/// initial language pack: the seeder now ships the <c>de</c> / <c>fr</c>
/// catalog rows, the full <c>de</c> / <c>fr</c> UI-string baselines (one
/// <see cref="TranslationResource"/> row per key, from
/// <see cref="KnownTranslationKeys.DeValues"/> /
/// <see cref="KnownTranslationKeys.FrValues"/>), and the <c>de</c> /
/// <c>fr</c> bodies of the seeded <c>terms</c> / <c>help</c> system pages
/// (as <see cref="PageTranslation"/> rows attached to the pages' **own**
/// ids — the read path queries by <c>PageId == page.Id</c>, never the
/// <c>system</c> root container's).
/// </summary>
/// <para>
/// <b>Seeding note (the <see cref="MLUI_FacesTests"/> D8-5 precedent):</b>
/// <c>FirstBootSeeder.SeedAsync</c> needs the whole bootstrap dependency set
/// (Identity <c>UserManager</c>, mailer, …), so the tests exercise the
/// seeder's **public static surface** directly —
/// <see cref="FirstBootSeeder.SeedDefaultPagesAsync"/> (returns the slug →
/// page-Id map) + <see cref="FirstBootSeeder.SeedPageTranslationsAsync"/> —
/// plus direct catalog/translation-resource plants that mirror
/// <c>SeedLanguageCatalogAsync</c> / <c>SeedTranslationResourcesAsync</c>
/// exactly (the registry is the single source both the seeder and these
/// tests read, so mirroring is exact — the same precedent
/// <see cref="MLUI_FacesTests.SeedEnFloor"/> uses).
/// </para>
/// <para>
/// No new Core or Web code beyond the U04 seeder change; no new seams.
/// </para>
/// </summary>
public class LS_U04_SeederTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── 1 — the first-boot catalog state (ADR 0042 D4) ─────────────────────
    // After a seeded boot: the catalog is exactly en(sort 0) / de(sort 1) /
    // fr(sort 2) / da(sort 3); en/de/fr enabled, da seeded DISABLED (the
    // pre-seeded lane — available in the supported list, awaiting an admin's
    // enable). LocaleSettings.DefaultLanguageCode == "en" (the default stays
    // `en` — the lane is about *available* languages, not switching the default).

    [Fact(DisplayName = "U04 the first-boot catalog is en/de/fr/da (sort 0/1/2/3), en-de-fr enabled, da disabled, default stays en")]
    public async Task U04_Catalog_EnDeFrDa_DaDisabled_DefaultStaysEn()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await using (var session = store.OpenSession(new SessionOptions()))
        {
            // Mirror SeedLanguageCatalogAsync (the catalog rows the seeder
            // now writes — the LS U04 additions + the pre-seeded `da` lane).
            session.Store(new LanguageCatalog { Id = "en", NativeName = "English", Enabled = true, SortOrder = 0 });
            session.Store(new LanguageCatalog { Id = "de", NativeName = "Deutsch", Enabled = true, SortOrder = 1 });
            session.Store(new LanguageCatalog { Id = "fr", NativeName = "Français", Enabled = true, SortOrder = 2 });
            session.Store(new LanguageCatalog { Id = "da", NativeName = "Dansk", Enabled = false, SortOrder = 3 });

            var existingSettings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct);
            session.Store(existingSettings ?? new LocaleSettings
            {
                Id = LocaleSettings.SingletonId,
                DefaultLanguageCode = FirstBootSeeder.SourceLanguage,
            });
            await session.SaveChangesAsync(ct);
        }

        await using var q = store.QuerySession();
        var catalog = await q.Query<LanguageCatalog>().ToListAsync(ct);

        // Exactly the four rows, in sort order (en 0 / de 1 / fr 2 / da 3).
        var ordered = catalog.OrderBy(c => c.SortOrder).ToList();
        Assert.Equal(4, ordered.Count);
        Assert.Equal(
            new[] { "en", "de", "fr", "da" },
            ordered.Select(c => c.Id).ToArray());
        Assert.Equal(
            new[] { 0, 1, 2, 3 },
            ordered.Select(c => c.SortOrder).ToArray());
        // en/de/fr enabled, da seeded DISABLED (the pre-seeded lane).
        Assert.True(ordered.Single(c => c.Id == "en").Enabled);
        Assert.True(ordered.Single(c => c.Id == "de").Enabled);
        Assert.True(ordered.Single(c => c.Id == "fr").Enabled);
        Assert.False(ordered.Single(c => c.Id == "da").Enabled);
        Assert.Equal("Deutsch", ordered.Single(c => c.Id == "de").NativeName);
        Assert.Equal("Français", ordered.Single(c => c.Id == "fr").NativeName);
        Assert.Equal("Dansk", ordered.Single(c => c.Id == "da").NativeName);

        // The instance default stays `en` (ADR 0042 D4).
        var localeSettings = await q.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct);
        Assert.NotNull(localeSettings);
        Assert.Equal("en", localeSettings!.DefaultLanguageCode);
    }

    // ── 2 — the M·12 completeness view is 100% for de AND fr on a fresh boot ──
    // (the plan's direct pin for "it clearly shows how the language features
    // work from the first bootup" — 0 missing keys for both baselines).

    [Fact(DisplayName = "U04 the completeness view is 100% present for de, fr and da on a fresh boot")]
    public async Task U04_Completeness_100Percent_ForDeFrDa()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // Mirror SeedTranslationResourcesAsync (en floor + de/fr/da baselines —
        // create-if-missing, the seeder's exact shape as of LS U04).
        await SeedFloorAndBaselinesAsync(store);

        var svc = new LocalizationService(store);

        foreach (var code in new[] { "de", "fr", "da" })
        {
            var completeness = await svc.GetCompletenessAsync(code);
            Assert.Empty(completeness.MissingKeys);
            Assert.Equal(KnownTranslationKeys.AllKeys.Count, completeness.PresentKeys.Count);
        }

        // `en` stays 100% too (the floor is itself the universe).
        var en = await svc.GetCompletenessAsync("en");
        Assert.Empty(en.MissingKeys);
    }

    // ── 3 — the provider resolves EVERY key to the de / fr baseline ──────────
    // (not the `en` floor) under a `de` preference and under an `fr`
    // preference.

    [Fact(DisplayName = "U04 every key resolves to the de baseline under de, the fr baseline under fr")]
    public async Task U04_Provider_ResolvesEveryKeyToTheBaseline()
    {
        var store = await BootStoreAsync();
        await SeedFloorAndBaselinesAsync(store);
        // The catalog rows (provider resolution requires an enabled catalog
        // row for the preference — the seeder's step 4).
        await SeedCatalogRowsAsync(store);

        var provider = new TranslationProvider(store);

        foreach (var (key, deText) in KnownTranslationKeys.DeValues)
            Assert.Equal(deText, await provider.GetAsync(key, "de"));

        foreach (var (key, frText) in KnownTranslationKeys.FrValues)
            Assert.Equal(frText, await provider.GetAsync(key, "fr"));
        // Note: `da` is seeded DISABLED (the pre-seeded lane) — a disabled
        // preference does NOT resolve to its rows (the provider gates on
        // catalog.Enabled), so the da baseline text is exercised via the
        // completeness pin (test 2) and the PageTranslation parity pin (test 6)
        // instead of a provider-resolution assertion here.
    }

    // ── 4 — per-string fallback still lands on `en` ─────────────────────────
    // Delete one `de` row: that one key renders the `en` text, the rest of
    // the `de` page stays `de`.

    [Fact(DisplayName = "U04 a deleted de row falls back per-string to en, the rest stays de")]
    public async Task U04_PerStringFallback_StillLandsOnEn()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedFloorAndBaselinesAsync(store);
        await SeedCatalogRowsAsync(store);

        // Pick two deterministic registry keys (the first two, by AllKeys order).
        var keys = KnownTranslationKeys.AllKeys.ToList();
        var victim = keys[0];
        var sibling = keys[1];

        // Delete the victim's `de` row (simulate a missing translation).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            var row = await session
                .Query<TranslationResource>()
                .Where(t => t.Key == victim && t.LanguageCode == "de")
                .FirstOrDefaultAsync(ct);
            Assert.NotNull(row);
            session.Delete(row!);
            await session.SaveChangesAsync(ct);
        }

        var provider = new TranslationProvider(store);

        // The victim key falls back to its `en` value (M·2 per-string).
        Assert.Equal(KnownTranslationKeys.EnValues[victim], await provider.GetAsync(victim, "de"));
        // The sibling keeps its `de` baseline (the "rest of the page stays de").
        Assert.Equal(KnownTranslationKeys.DeValues[sibling], await provider.GetAsync(sibling, "de"));

        // The completeness view now shows exactly that one key missing.
        var svc = new LocalizationService(store);
        var de = await svc.GetCompletenessAsync("de");
        Assert.Equal(new[] { victim }, de.MissingKeys.ToArray());
        Assert.DoesNotContain(sibling, de.MissingKeys);
    }

    // ── 5 — warm-boot no-op: a second seeder-style run leaves an admin's edit ─
    // of a `de` value unchanged (ADR 0042 D1 — create-if-missing, never
    // overwrite). The `de` / `fr` loops are create-if-missing by construction;
    // a re-run of the seeder step's shape (the <see cref="MLUI_FacesTests"/>
    // SeedEnFloor precedent) must leave the admin's edit intact.

    [Fact(DisplayName = "U04 a re-run of the de seeding leaves an admin-edited value unchanged")]
    public async Task U04_WarmBootNoOp_AdminEditSurvives()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        await SeedFloorAndBaselinesAsync(store);

        const string key = "nav.home";   // a real registry key
        Assert.True(KnownTranslationKeys.EnValues.ContainsKey(key));

        // An admin edits the `de` value in-app (the ADR 0021 editor — a direct
        // row write, mirroring the editor's service write shape).
        await UpsertTranslationAsync(store, key, "de", "ADMIN-EDIT");

        // A warm re-run: the seeder step's de-loop shape again (create-if-missing).
        await SeedFloorAndBaselinesAsync(store);

        // The admin's edit survives — the re-run skipped the existing row.
        await using var session = store.QuerySession();
        var row = await session
            .Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == "de")
            .FirstOrDefaultAsync(ct);
        Assert.NotNull(row);
        Assert.Equal("ADMIN-EDIT", row!.Text);

        // And no duplicate row was created for (key, de).
        var count = await session
            .Query<TranslationResource>()
            .Where(t => t.Key == key && t.LanguageCode == "de")
            .CountAsync(ct);
        Assert.Equal(1, count);
    }

    // ── 6 — the PageTranslation pin (ADR 0042 D2; the PG U06 lane shape) ─────
    // All four seeded pages (terms / help / privacy / conduct — the ADR 0043
    // D1 four-page set, the four `Page`-backed surfaces of the five-surface
    // set) each have a `de` and a `fr` PageTranslation whose PageId is the
    // CORRECT page (the page under the `system` root — NOT the root's own Id),
    // body non-empty, parity with the canonical De/FrDefaultPages() sources.
    // (SP U02 — extended from the U04 two-page terms/help shape to the full
    // four-page set; the loop is generic over the baseline arrays, ADR 0043
    // D3 — no seeder-branch change.)

    [Fact(DisplayName = "U04 the four seeded pages each have a de and a fr PageTranslation on the page's own Id (not the system root)")]
    public async Task U04_PageTranslations_AttachedToPageOwnId_NonEmptyBody()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // The seeder's exact page-seeding surface (the two public statics).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            var pageIds = await FirstBootSeeder.SeedDefaultPagesAsync(
                session, FirstBootSeeder.EnDefaultPages(), DateTimeOffset.UtcNow, ct);
            await FirstBootSeeder.SeedPageTranslationsAsync(session, pageIds, DateTimeOffset.UtcNow, ct);
            await session.SaveChangesAsync(ct);
        }

        await using var q = store.QuerySession();

        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstAsync(ct);

        var slugs = new[] { "terms", "help", "privacy", "conduct" };   // ADR 0043 D1 four-page set
        var pages = new Dictionary<string, Page>();
        foreach (var slug in slugs)
        {
            pages[slug] = await q.Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == systemRoot.Id)
                .FirstAsync(ct);
        }

        // The CRITICAL parentage: the PageTranslations attach to the page's
        // OWN id (the read path GetTranslationsAsync(page.Id) finds them),
        // never to the `system` root container's id.
        Assert.All(slugs, slug => Assert.NotEqual(systemRoot.Id, pages[slug].Id));

        foreach (var (slug, page) in slugs.Select(s => (s, pages[s])))
        {
            var de = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "de")
                .FirstOrDefaultAsync(ct);
            var fr = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "fr")
                .FirstOrDefaultAsync(ct);
            var da = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "da")
                .FirstOrDefaultAsync(ct);

            Assert.NotNull(de);
            Assert.NotNull(fr);
            Assert.NotNull(da);
            Assert.False(string.IsNullOrWhiteSpace(de!.Body));
            Assert.False(string.IsNullOrWhiteSpace(fr!.Body));
            Assert.False(string.IsNullOrWhiteSpace(da!.Body));
            Assert.False(string.IsNullOrWhiteSpace(de.Title));
            Assert.False(string.IsNullOrWhiteSpace(fr.Title));
            Assert.False(string.IsNullOrWhiteSpace(da.Title));
            // Baseline parity with the canonical sources (structure preserved —
            // the ADR 0042 D2 bar holds for all four pages, all three baselines).
            var deBaseline = FirstBootSeeder.DeDefaultPages().Single(p => p.Slug == slug);
            var frBaseline = FirstBootSeeder.FrDefaultPages().Single(p => p.Slug == slug);
            var daBaseline = FirstBootSeeder.DaDefaultPages().Single(p => p.Slug == slug);
            Assert.Equal(deBaseline.Body, de.Body);
            Assert.Equal(frBaseline.Body, fr.Body);
            Assert.Equal(daBaseline.Body, da.Body);

            // Exactly one row per (page, language) — no duplicates from the
            // create-if-missing idiom (ADR 0042 D1).
            var deCount = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "de")
                .CountAsync(ct);
            var daCount = await q.Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == "da")
                .CountAsync(ct);
            Assert.Equal(1, deCount);
            Assert.Equal(1, daCount);
        }

        // And NO PageTranslation is attached to the `system` root itself
        // (the root is a container — no content, no translations; ADR 0042 D6
        // parentage).
        var onRoot = await q.Query<PageTranslation>()
            .Where(t => t.PageId == systemRoot.Id)
            .CountAsync(ct);
        Assert.Equal(0, onRoot);

        // The read path (IPageService) resolves each page's translations for
        // both baselines — the exact seam the Web renders through.
        var svc = new PageService(store);
        foreach (var slug in slugs)
        {
            var translations = await svc.GetTranslationsAsync(pages[slug].Id);
            Assert.Contains(translations, t => t.LanguageCode == "de");
            Assert.Contains(translations, t => t.LanguageCode == "fr");
            Assert.Contains(translations, t => t.LanguageCode == "da");
        }
    }

    // ── 7 — the warm-boot backfill (ADR 0043 D7 / ADR 0044 D5) ──────────────
    // A deployment seeded before the de/fr/da baselines shipped has the four
    // pages but no PageTranslation rows. BackfillPageTranslationsAsync adds
    // the missing de/fr/da rows (create-if-missing, ADR 0042 D1) and is
    // idempotent — a second run finds every row and skips.

    [Fact(DisplayName = "Warm-boot backfill: pages seeded en-only gain de/fr/da rows, idempotent on re-run")]
    public async Task Backfill_CreatesMissingDeFrDaRows_Idempotent()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // Seed the en pages ONLY (no translations) — the warm-deployment state
        // (a pre-LS-U04 deployment had the pages but not the baselines).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            await FirstBootSeeder.SeedDefaultPagesAsync(
                session, FirstBootSeeder.EnDefaultPages(), DateTimeOffset.UtcNow, ct);
            // Deliberately NOT calling SeedPageTranslationsAsync — the state
            // a deployment seeded before the de/fr/da baselines shipped has.
            await session.SaveChangesAsync(ct);
        }

        // Before the backfill: no PageTranslation rows for the four pages.
        await using (var q = store.QuerySession())
        {
            var systemRoot = await q.Query<Page>()
                .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
                .FirstAsync(ct);
            var slugs = new[] { "terms", "help", "privacy", "conduct" };
            foreach (var slug in slugs)
            {
                var page = await q.Query<Page>()
                    .Where(p => p.Slug == slug && p.ParentId == systemRoot.Id)
                    .FirstAsync(ct);
                var count = await q.Query<PageTranslation>()
                    .Where(t => t.PageId == page.Id)
                    .CountAsync(ct);
                Assert.Equal(0, count);
            }
        }

        // Run the backfill (the warm-boot path in SchemaBootstrap).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            await FirstBootSeeder.BackfillPageTranslationsAsync(session, ct);
        }

        // After the backfill: each page has exactly de + fr + da rows (3 total),
        // matching the canonical baselines.
        await using (var q = store.QuerySession())
        {
            var systemRoot = await q.Query<Page>()
                .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
                .FirstAsync(ct);
            var slugs = new[] { "terms", "help", "privacy", "conduct" };
            foreach (var slug in slugs)
            {
                var page = await q.Query<Page>()
                    .Where(p => p.Slug == slug && p.ParentId == systemRoot.Id)
                    .FirstAsync(ct);

                var total = await q.Query<PageTranslation>()
                    .Where(t => t.PageId == page.Id)
                    .CountAsync(ct);
                Assert.Equal(3, total);

                // Baseline parity.
                var de = await q.Query<PageTranslation>()
                    .Where(t => t.PageId == page.Id && t.LanguageCode == "de")
                    .FirstAsync(ct);
                var deBaseline = FirstBootSeeder.DeDefaultPages().Single(p => p.Slug == slug);
                Assert.Equal(deBaseline.Body, de.Body);

                var fr = await q.Query<PageTranslation>()
                    .Where(t => t.PageId == page.Id && t.LanguageCode == "fr")
                    .FirstAsync(ct);
                var frBaseline = FirstBootSeeder.FrDefaultPages().Single(p => p.Slug == slug);
                Assert.Equal(frBaseline.Body, fr.Body);

                var da = await q.Query<PageTranslation>()
                    .Where(t => t.PageId == page.Id && t.LanguageCode == "da")
                    .FirstAsync(ct);
                var daBaseline = FirstBootSeeder.DaDefaultPages().Single(p => p.Slug == slug);
                Assert.Equal(daBaseline.Body, da.Body);
            }
        }

        // Re-run the backfill (idempotency — the ADR 0042 D1 skip path).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            await FirstBootSeeder.BackfillPageTranslationsAsync(session, ct);
        }

        // After the re-run: still exactly 3 rows per page (no duplicates).
        await using (var q = store.QuerySession())
        {
            var systemRoot = await q.Query<Page>()
                .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
                .FirstAsync(ct);
            var slugs = new[] { "terms", "help", "privacy", "conduct" };
            foreach (var slug in slugs)
            {
                var page = await q.Query<Page>()
                    .Where(p => p.Slug == slug && p.ParentId == systemRoot.Id)
                    .FirstAsync(ct);
                var total = await q.Query<PageTranslation>()
                    .Where(t => t.PageId == page.Id)
                    .CountAsync(ct);
                Assert.Equal(3, total);
            }
        }
    }

    // ── 8 — the warm-boot UI-string baseline backfill (ADR 0052) ────────────
    // A deployment seeded before a baseline was added to the registry has the
    // `en` row (and the provider floor renders its English) but no de/fr/da
    // row for that key. BackfillUiStringBaselinesAsync adds the missing
    // de/fr/da rows (create-if-missing, ADR 0042 D1), never touches an
    // existing row (an admin's in-place edit keeps its Id and is skipped),
    // never touches the `en` row, and is idempotent on re-run.

    [Fact(DisplayName = "Warm-boot UI-string backfill: creates missing de/fr/da rows, skips existing, leaves en untouched, idempotent")]
    public async Task BackfillUiStrings_CreatesMissingDeFrDa_SkipsExisting_Idempotent()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        // Simulate the warm-deployment state: the `en` floor for every key
        // (the pristine-boot `en` upsert), plus de/fr/da baselines EXCEPT two
        // keys — one missing all three baselines (a brand-new registry key) and
        // one missing only `fr` (a partial baseline). A de row on the partial
        // key carries an admin's in-place edit (the editor's update path
        // refreshes Text in place, keeping the Id — the skip branch must find
        // it and leave it alone).
        var missingAll = KnownTranslationKeys.EnValues.Keys.First();
        var partialKey = KnownTranslationKeys.EnValues.Keys.Skip(1).First();
        const string adminEditedText = "ADMIN-EDITED — must survive the backfill";

        await using (var session = store.OpenSession(new SessionOptions()))
        {
            foreach (var (key, enText) in KnownTranslationKeys.EnValues)
            {
                session.Store(new TranslationResource
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Key = key,
                    LanguageCode = "en",
                    Text = enText,
                });
            }

            foreach (var (code, baseline) in new[]
            {
                ("de", KnownTranslationKeys.DeValues),
                ("fr", KnownTranslationKeys.FrValues),
                ("da", KnownTranslationKeys.DaValues),
            })
            {
                foreach (var (key, text) in baseline)
                {
                    // Skip the two simulated gaps.
                    if ((key == missingAll) || (key == partialKey && code == "fr"))
                        continue;

                    session.Store(new TranslationResource
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Key = key,
                        LanguageCode = code,
                        Text = key == partialKey && code == "de" ? adminEditedText : text,
                    });
                }
            }
            await session.SaveChangesAsync(ct);
        }

        // Run the backfill (the warm-boot path in SchemaBootstrap).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            await FirstBootSeeder.BackfillUiStringBaselinesAsync(session, ct);
        }

        await using (var q = store.QuerySession())
        {
            // 1. The brand-new key now has all three baselines, each matching
            //    its language's registry value.
            var expectedByCode = new Dictionary<string, string>
            {
                ["de"] = KnownTranslationKeys.DeValues[missingAll],
                ["fr"] = KnownTranslationKeys.FrValues[missingAll],
                ["da"] = KnownTranslationKeys.DaValues[missingAll],
            };
            foreach (var (code, expected) in expectedByCode)
            {
                var row = await q.Query<TranslationResource>()
                    .Where(t => t.Key == missingAll && t.LanguageCode == code)
                    .SingleAsync(ct);
                Assert.Equal(expected, row.Text);
            }

            // 2. The partial key's new `fr` row matches the baseline…
            var fr = await q.Query<TranslationResource>()
                .Where(t => t.Key == partialKey && t.LanguageCode == "fr")
                .SingleAsync(ct);
            Assert.Equal(KnownTranslationKeys.FrValues[partialKey], fr.Text);

            // …while the admin-edited `de` row survived verbatim (never clobbered).
            var de = await q.Query<TranslationResource>()
                .Where(t => t.Key == partialKey && t.LanguageCode == "de")
                .SingleAsync(ct);
            Assert.Equal(adminEditedText, de.Text);

            // 3. The `en` rows were never touched — count is exactly one per key.
            var enCount = await q.Query<TranslationResource>()
                .Where(t => t.LanguageCode == "en")
                .CountAsync(ct);
            Assert.Equal(KnownTranslationKeys.EnValues.Count, enCount);

            // 4. No duplicate rows were created anywhere: exactly one row per
            //    (key, language) pair.
            var total = await q.Query<TranslationResource>().CountAsync(ct);
            Assert.Equal(
                KnownTranslationKeys.EnValues.Count
                    + KnownTranslationKeys.DeValues.Count
                    + KnownTranslationKeys.FrValues.Count
                    + KnownTranslationKeys.DaValues.Count,
                total);
        }

        // Re-run the backfill (idempotency — the ADR 0042 D1 skip path).
        await using (var session = store.OpenSession(new SessionOptions()))
        {
            await FirstBootSeeder.BackfillUiStringBaselinesAsync(session, ct);
        }

        await using (var q = store.QuerySession())
        {
            var total = await q.Query<TranslationResource>().CountAsync(ct);
            Assert.Equal(
                KnownTranslationKeys.EnValues.Count
                    + KnownTranslationKeys.DeValues.Count
                    + KnownTranslationKeys.FrValues.Count
                    + KnownTranslationKeys.DaValues.Count,
                total);
        }
    }

    // ─── Shared helpers ─────────────────────────────────────────────────────

    /// <summary>Boot a fresh scratch store (M1 + M3 + Page doc types) — the
    /// <see cref="MLUI_FacesTests.BootStoreAsync"/> / <see
    /// cref="PageServiceTests.BootStoreAsync"/> shape (the Page surface
    /// needs <c>PageDocTypes.Configure</c>).</summary>
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
    /// Mirror <c>FirstBootSeeder.SeedLanguageCatalogAsync</c> (the three
    /// catalog rows) — load-then-Store idempotent shape, the seeder's
    /// exact form as of LS U04.
    /// </summary>
    private static async Task SeedCatalogRowsAsync(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());

        foreach (var (id, name, sort) in new[]
        {
            ("en", "English", 0),
            ("de", "Deutsch", 1),
            ("fr", "Français", 2),
        })
        {
            var existing = await session.LoadAsync<LanguageCatalog>(id, ct);
            session.Store(existing ?? new LanguageCatalog
            {
                Id = id,
                NativeName = name,
                Enabled = true,
                SortOrder = sort,
            });
        }
        await session.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Mirror <c>FirstBootSeeder.SeedTranslationResourcesAsync</c> as of LS
    /// U04: the <c>en</c> floor (code-wins, the <see cref="MLUI_FacesTests
    /// .SeedEnFloor"/> shape) + the <c>de</c> / <c>fr</c> baselines
    /// (create-if-missing — the seeder's exact U04 shape). Runs once per
    /// test's fresh scratch DB (the pristine-boot state), or again to
    /// simulate the warm re-run (test 5).
    /// </summary>
    private static async Task SeedFloorAndBaselinesAsync(IDocumentStore store)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = store.OpenSession(new SessionOptions());

        // The `en` floor (code-wins — the seedEnFloor precedent, unchanged).
        foreach (var (key, enText) in KnownTranslationKeys.EnValues)
        {
            var existing = await session
                .Query<TranslationResource>()
                .Where(t => t.Key == key && t.LanguageCode == "en")
                .FirstOrDefaultAsync(ct);
            if (existing is null)
            {
                session.Store(new TranslationResource
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Key = key,
                    LanguageCode = "en",
                    Text = enText,
                });
            }
            else
            {
                existing.Text = enText;   // code wins
                session.Store(existing);
            }
        }

        // The `de` / `fr` / `da` baselines (create-if-missing — the U04 seeder
        // shape: an existing row is skipped, never refreshed).
        foreach (var (code, baseline) in new[]
        {
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        })
        {
            foreach (var (key, text) in baseline)
            {
                var existing = await session
                    .Query<TranslationResource>()
                    .Where(t => t.Key == key && t.LanguageCode == code)
                    .FirstOrDefaultAsync(ct);
                if (existing is null)
                {
                    session.Store(new TranslationResource
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        Key = key,
                        LanguageCode = code,
                        Text = text,
                    });
                }
                // else: skip (create-if-missing — the U04 seeder behavior).
            }
        }

        await session.SaveChangesAsync(ct);
    }

    /// <summary>Upsert a <see cref="TranslationResource"/> row (the
    /// <see cref="MLUI_FacesTests.UpsertTranslation"/> shape — the in-app
    /// editor's write form).</summary>
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
        {
            session.Store(new TranslationResource
            {
                Id = Guid.NewGuid().ToString("N"),
                Key = key,
                LanguageCode = languageCode,
                Text = text,
            });
        }
        else
        {
            existing.Text = text;
            session.Store(existing);
        }
        await session.SaveChangesAsync(ct);
    }
}
