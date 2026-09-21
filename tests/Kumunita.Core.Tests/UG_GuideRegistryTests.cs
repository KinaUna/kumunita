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
/// exact seeded set + idempotency across two live sessions, the en-only
/// floor, and the "guides under <c>help</c>, not under <c>system</c> and not
/// new roots" shape — the two invariants that keep the ADR 0043 D1 exact-set
/// pin and the ADR 0040 namespace guard green.
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
    /// <c>help</c>) into a session, commit. Mirrors the production boot order
    /// (ADR 0057 D1: the guides are seeded right after the four-surface set,
    /// same session, same commit — C3).
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
                await FirstBootSeeder.SeedUserGuidesAsync(s, helpId, DateTimeOffset.UtcNow, ct);
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

        // The guides are the EXACT children of `help` (the single source the
        // test reads is GuidePages() — the ADR 0057 D4 drift pin), one per
        // slug, no duplicate from the second boot.
        var helpChildren = await q.Query<Page>()
            .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
            .ToListAsync(ct);
        Assert.Equal(
            guideSlugs.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
            helpChildren.Select(p => p.Slug).OrderBy(s => s, StringComparer.Ordinal).ToArray());
        Assert.Equal(guideSlugs.Length, helpChildren.Count);
        foreach (var slug in guideSlugs)
        {
            Assert.Equal(1, helpChildren.Count(p => p.Slug == slug));
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

        foreach (var (slug, title, body) in guides)
        {
            var page = await q.Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == help.Id)
                .FirstAsync(ct);

            // ADR 0057 D1/D2 shape: nested under `help` (grandchild of
            // `system`), public (Audience null — a how-to is not
            // audience-gated), authored-in `en` (the floor), platform content
            // (no resident author), a system page (the ADR 0040 standing
            // matrix), not a draft / not deleted.
            Assert.Equal(help.Id, page.ParentId);
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
    public async Task UG_Guides_NoPageTranslationRows_AtFirstBoot()
    {
        // ADR 0057 D2 — the guides ship `en`-only (the ADR 0042 D1 "code wins
        // for `en`" floor; a non-`en` guide body is community-owned, added
        // later by a human Translator — the ADR 0005 C "never
        // machine-translated" clause). So a first boot attaches NO
        // PageTranslation row to a guide, and this keeps the ADR 0044
        // page-baseline parity green (the PageTranslation count is unchanged
        // by the guides — they add Page docs, not translation rows).
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
        var guideIds = await q.Query<Page>()
            .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
            .Select(p => p.Id)
            .ToListAsync(ct);

        // Exactly the guide set (sanity: the guides are present).
        Assert.Equal(guideSlugs.Length, guideIds.Count);

        // No PageTranslation row is attached to any guide's own Id (the
        // en-only floor — a non-`en` guide body, once a human adds it, would
        // attach here, but a first boot has none).
        var translationsOnGuides = await q.Query<PageTranslation>()
            .Where(t => guideIds.Contains(t.PageId))
            .CountAsync(ct);
        Assert.Equal(0, translationsOnGuides);
    }
}
