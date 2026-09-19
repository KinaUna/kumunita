using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Bootstrap;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// SP U04 (ADR 0043) — the remaining Web surface pins the lane plan names that
/// U01–U03 did not already own. This file composes on the existing seams rather
/// than duplicating them:
/// <list type="number">
/// <item><b>Seeded en body (gap 1):</b> a cookie-less <c>GET /privacy</c> /
///       <c>GET /conduct</c> renders the shared <c>Page</c> view whose body is
///       the canonical <see cref="FirstBootSeeder.EnDefaultPages"/>() text —
///       the Web route is tied to the single source the seeder writes, not a
///       hand-copied string. (U01 pinned "renders the Page view" with a literal
///       body; this closes the "matches the source" half.)</item>
/// <item><b>de/fr chip-swap (gap 2):</b> the ADR 0027 <c>td-variant</c>
///       swap is on the <see cref="PageController.Show"/> surface, not the
///       hard-coded routes — the <c>privacy</c> page with <c>de</c>/<c>fr</c>
///       <see cref="PageTranslation"/> rows carries both variants in the
///       <see cref="PageShowViewModel"/> and the default active variant is the
///       page's <em>authored-in</em> language (<c>en</c>), untouched by the
///       <c>kumunita.locale</c> cookie (ADR 0005 §C — display is never
///       auto-switched by the preference).</item>
/// <item><b>Footer ⇄ admin tie (gap 5):</b> the layout footer's five
///       hard-coded hrefs and the <see cref="AdminController
///       .BuildPlatformPagesAsync"/> seam's five routes name the <em>same</em>
///       five surfaces, and over a seeded first-boot tree the four
///       <c>Page</c>-backed rows resolve to non-null ids while <c>about</c>
///       stays null (ADR 0043 D1).</item>
/// </list>
/// The <c>/about</c> drift pin (gap 3) and the <c>/privacy</c> /
/// <c>/conduct</c> 404-floor pins (gap 4) already exist —
/// <see cref="StaticPagesControllerPgTests"/> (tree-present / tree-absent) and
/// <see cref="PublicLocaleAndAboutTests"/> (the product-story view shape) — and
/// are cited, not re-pinned, here.
/// </summary>
public class StaticPagesSP_U04Tests
{
    // ── (1) /privacy + /conduct render the seeded `en` body (cookie-less) ───

    /// <summary>
    /// A cookie-less <c>GET</c> for <paramref name="slug"/> seeds the page from
    /// the canonical <see cref="FirstBootSeeder.EnDefaultPages"/>() entry (the
    /// exact shape the seeder writes to the tree) and asserts the shared
    /// <c>Page</c> view carries that body verbatim — the Web route is the
    /// reader of the single source, not a second copy.
    /// </summary>
    private static (StaticPagesController controller, string expectedBody) BuildSeeded(string slug)
    {
        var (_, title, body) = FirstBootSeeder.EnDefaultPages()
            .Single(p => p.Slug == slug);

        var pages = Substitute.For<IPageService>();
        // ADR 0040: the canonical page lives under the `system/` root.
        var page = new Page
        {
            Id = $"page-{slug}-seeded",
            ParentId = "system-root",
            Slug = slug,
            Title = title,
            Body = body,
            LanguageCode = "en",
            Audience = null,
            AuthorId = string.Empty,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow,
        };
        pages.ResolvePageAsync($"system/{slug}")
            .Returns(Task.FromResult((page, new List<PageTranslation>() as IReadOnlyList<PageTranslation>)));
        // The bare-slug legacy seam misses (proving the primary path resolves).
        pages.ResolvePageAsync(slug)
            .Returns(Task.FromException<(Page, IReadOnlyList<PageTranslation>)>(new KeyNotFoundException()));

        var controller = StaticPagesControllerHarness.Build(pages);
        return (controller, body);
    }

    [Fact(DisplayName = "SP U04: cookie-less GET /privacy renders the seeded en body from EnDefaultPages (gap 1)")]
    public async Task Privacy_Renders_Seeded_En_Body()
    {
        var (controller, expectedBody) = BuildSeeded("privacy");

        var view = Assert.IsType<ViewResult>(await controller.Privacy());

        Assert.Equal("Page", view.ViewName);
        var model = Assert.IsType<StaticPagesController.StaticPageViewModel>(view.ViewData.Model);
        Assert.Equal("privacy", model.Slug);
        // The body is the canonical source text (the seeder's single source),
        // not a hand-copied literal.
        Assert.Equal(expectedBody, model.Body);
    }

    [Fact(DisplayName = "SP U04: cookie-less GET /conduct renders the seeded en body from EnDefaultPages (gap 1)")]
    public async Task Conduct_Renders_Seeded_En_Body()
    {
        var (controller, expectedBody) = BuildSeeded("conduct");

        var view = Assert.IsType<ViewResult>(await controller.Conduct());

        Assert.Equal("Page", view.ViewName);
        var model = Assert.IsType<StaticPagesController.StaticPageViewModel>(view.ViewData.Model);
        Assert.Equal("conduct", model.Slug);
        Assert.Equal(expectedBody, model.Body);
    }

    // ── (1b) the ADR 0043 D7 localized-body lane: a PageTranslation row for
    //     the effective language is picked over the authored-in en body ─────

    /// <summary>
    /// The effective-language read seam (ADR 0043 D7 / ADR 0044 D5) is what
    /// lets a German-speaking resident see the German body of <c>/privacy</c>
    /// even on a warm deployment whose page was authored in <c>en</c>. The
    /// controller resolves the effective language (stubbed here to <c>de</c>)
    /// and picks the matching <see cref="PageTranslation"/> row's
    /// (Title, Body) — the authored-in <c>en</c> body is the floor only when
    /// no row matches.
    /// </summary>
    [Fact(DisplayName = "ADR 0043 D7: effective language de → the de PageTranslation body is picked over the en body")]
    public async Task Privacy_EffectiveLanguageDe_RendersDeTranslationBody()
    {
        var (enTitle, enBody) = (FirstBootSeeder.EnDefaultPages().Single(p => p.Slug == "privacy").Title,
                                FirstBootSeeder.EnDefaultPages().Single(p => p.Slug == "privacy").Body);
        var (deTitle, deBody) = (FirstBootSeeder.DeDefaultPages().Single(p => p.Slug == "privacy").Title,
                                FirstBootSeeder.DeDefaultPages().Single(p => p.Slug == "privacy").Body);

        var pages = Substitute.For<IPageService>();
        var page = new Page
        {
            Id = "page-privacy-de",
            ParentId = "system-root",
            Slug = "privacy",
            Title = enTitle,
            Body = enBody,
            LanguageCode = "en",
            Audience = null,
            AuthorId = string.Empty,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow,
        };
        var deTranslation = new PageTranslation
        {
            Id = "t-privacy-de",
            PageId = page.Id,
            LanguageCode = "de",
            Title = deTitle,
            Body = deBody,
            AuthorId = string.Empty,
            Created = DateTimeOffset.UtcNow,
        };
        pages.ResolvePageAsync("system/privacy")
            .Returns(Task.FromResult((page, new List<PageTranslation> { deTranslation } as IReadOnlyList<PageTranslation>)));
        pages.ResolvePageAsync("privacy")
            .Returns(Task.FromException<(Page, IReadOnlyList<PageTranslation>)>(new KeyNotFoundException()));

        // effectiveLanguage "de" — the harness stubs the provider's
        // ResolveEffectiveLanguageAsync to return it (the cookie-less path:
        // no Accept-Language on the DefaultHttpContext, so the
        // IReadOnlyCollection overload is the one the controller calls).
        var controller = StaticPagesControllerHarness.Build(pages, effectiveLanguage: "de");

        var view = Assert.IsType<ViewResult>(await controller.Privacy());

        var model = Assert.IsType<StaticPagesController.StaticPageViewModel>(view.ViewData.Model);
        Assert.Equal("privacy", model.Slug);
        // The de row's (Title, Body) is what the resident sees — not the en
        // authored-in floor.
        Assert.Equal(deTitle, model.Title);
        Assert.Equal(deBody, model.Body);
    }

    // ── (2) the ADR 0027 td-variant chip-swap (Show surface) ────────────────
    // The hard-coded /privacy route renders the simple shared Page view (en
    // body only). The de/fr baselines ride the PageTranslation rows, whose
    // read + chip-swap home is PageController.Show (Views/Page/Show.cshtml).
    // ADR 0005 §C: display is never auto-switched by the locale cookie — the
    // default active variant is the page's authored-in language.

    private static (PageController controller, Page page) BuildShowSurface(
        string path, Page page, IReadOnlyList<PageTranslation> translations,
        IEnumerable<LanguageCatalog> catalog, string? cookieLocale = null)
    {
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync(path).Returns(page);
        pages.GetTranslationsAsync(page.Id).Returns(translations);
        pages.GetTreeAsync().Returns(new List<Page> { page });

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(catalog.ToList());
        localization.GetDefaultLanguageCodeAsync().Returns("en");

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());
        userInfo.GetProfilesAsync(true).Returns(new List<Profile>());
        userInfo.GetPublicGroupsAsync().Returns(new List<Group>());

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<Page>(page.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Page?>(page));
        store.QuerySession().Returns(readSession);

        var controller = new PageController(
            pages, Substitute.For<IAuthorizationService>(), localization, userInfo, store);
        var httpContext = new DefaultHttpContext();
        // ADR 0005 §C — the cookie is a display *preference*; Show must ignore
        // it for variant selection (the default active variant is the
        // authored-in language, not the cookie).
        if (cookieLocale is not null)
            httpContext.Request.Headers["Cookie"] = $"kumunita.locale={cookieLocale}";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, page);
    }

    private static Page SeedPrivacy() => new()
    {
        Id = "page-privacy-seed",
        ParentId = "system-root",
        Slug = "privacy",
        Title = FirstBootSeeder.EnDefaultPages().Single(p => p.Slug == "privacy").Title,
        Body = FirstBootSeeder.EnDefaultPages().Single(p => p.Slug == "privacy").Body,
        LanguageCode = "en",
        Audience = null,
        AuthorId = string.Empty,
        ComponentId = null,
        IsDraft = false,
        IsDeleted = false,
    };

    private static IReadOnlyList<PageTranslation> PrivacyDeFrTranslations()
    {
        var de = FirstBootSeeder.DeDefaultPages().Single(p => p.Slug == "privacy");
        var fr = FirstBootSeeder.FrDefaultPages().Single(p => p.Slug == "privacy");
        return new List<PageTranslation>
        {
            new() { Id = "tr-privacy-de", PageId = "page-privacy-seed", LanguageCode = "de", Title = de.Title, Body = de.Body, AuthorId = string.Empty },
            new() { Id = "tr-privacy-fr", PageId = "page-privacy-seed", LanguageCode = "fr", Title = fr.Title, Body = fr.Body, AuthorId = string.Empty },
        };
    }

    private static IReadOnlyList<LanguageCatalog> FirstBootCatalog() => new List<LanguageCatalog>
    {
        new() { Id = "en", NativeName = "English",  Enabled = true, SortOrder = 0 },
        new() { Id = "de", NativeName = "Deutsch",  Enabled = true, SortOrder = 1 },
        new() { Id = "fr", NativeName = "Français", Enabled = true, SortOrder = 2 },
    };

    [Fact(DisplayName = "SP U04: Show /pages/system/privacy carries the de+fr td-variant rows; default active variant is the authored-in language (gap 2)")]
    public async Task Show_Privacy_Carries_DeFr_Variants_DefaultIsAuthoredIn()
    {
        var (controller, _) = BuildShowSurface(
            "system/privacy", SeedPrivacy(), PrivacyDeFrTranslations(), FirstBootCatalog());

        var view = Assert.IsType<ViewResult>(await controller.Show("system/privacy"));
        var model = Assert.IsType<PageShowViewModel>(view.ViewData.Model);

        // The authored-in language is the first, default-visible variant (the
        // ADR 0027 TD·1 shape): it is the page's own LanguageCode, `en`.
        Assert.Equal("en", model.OriginalLanguageCode);

        // The de + fr baselines are present as stored translation variants
        // (the td-variant chip-swap containers the Show view renders).
        var codes = model.Translations.Select(t => t.LanguageCode).OrderBy(c => c).ToArray();
        Assert.Equal(new[] { "de", "fr" }, codes);

        // Each variant's body is the canonical baseline from the seeder source
        // (De/FrDefaultPages) — the read path resolves the PageTranslation rows.
        Assert.Contains(model.Translations, t => t.LanguageCode == "de"
            && t.Body == FirstBootSeeder.DeDefaultPages().Single(p => p.Slug == "privacy").Body);
        Assert.Contains(model.Translations, t => t.LanguageCode == "fr"
            && t.Body == FirstBootSeeder.FrDefaultPages().Single(p => p.Slug == "privacy").Body);

        // The Languages chip set marks de + fr as having a translation, and en
        // as the authored-in language — the chip row the view renders from.
        var langs = model.Languages.ToDictionary(l => l.Code, l => l.HasTranslation);
        Assert.True(langs["en"], "the authored-in language is always available");
        Assert.True(langs["de"], "a stored de PageTranslation marks de translatable");
        Assert.True(langs["fr"], "a stored fr PageTranslation marks fr translatable");
    }

    [Fact(DisplayName = "SP U04: the kumunita.locale cookie does NOT auto-switch the active variant (ADR 0005 §C)")]
    public async Task Show_Privacy_LocaleCookie_DoesNotAutoSwitchVariant()
    {
        // A resident with a `de` preference requests the page. The model's
        // default active variant is STILL the authored-in language (`en`) —
        // the cookie is a display preference, not a selector (ADR 0005 §C;
        // ADR 0027 — the swap is an explicit click, never auto).
        var (controller, _) = BuildShowSurface(
            "system/privacy", SeedPrivacy(), PrivacyDeFrTranslations(),
            FirstBootCatalog(), cookieLocale: "de");

        var view = Assert.IsType<ViewResult>(await controller.Show("system/privacy"));
        var model = Assert.IsType<PageShowViewModel>(view.ViewData.Model);

        // The authored-in variant is unchanged by the cookie — `en`, not `de`.
        Assert.Equal("en", model.OriginalLanguageCode);
        // The de variant is present (for click-to-swap) but is a *stored
        // translation*, not the default-active variant.
        Assert.Contains(model.Translations, t => t.LanguageCode == "de");
        Assert.DoesNotContain(model.Translations, t => t.LanguageCode == "en");
    }

    // ── (5) footer ⇄ admin seam name the same five surfaces ─────────────────

    [Fact(DisplayName = "SP U04: the footer's five hrefs and the admin seam's five routes name the same surfaces; over a seeded tree four resolve, about stays null (gap 5)")]
    public async Task Footer_Hrefs_Match_Admin_Routes_SeededTree_Resolve()
    {
        // The admin seam's five routes (footer order).
        var pages = Substitute.For<IPageService>();
        pages.GetByPathAsync(Arg.Any<string>()).Returns(ci =>
        {
            var p = ci.ArgAt<string>(0);
            var slug = p.StartsWith("system/") ? p["system/".Length..] : p;
            // about is never seeded (ADR 0043 D1 — a view, not a Page doc).
            var seeded = new[] { "terms", "help", "privacy", "conduct" };
            return seeded.Contains(slug)
                ? Task.FromResult(new Page { Id = $"id-{slug}", Slug = slug })
                : Task.FromException<Page>(new KeyNotFoundException());
        });

        var rows = await AdminController.BuildPlatformPagesAsync(pages);

        // The five surfaces in footer order (about first — ADR 0043 D4).
        Assert.Equal(
            new[] { "/about", "/terms", "/help", "/privacy", "/conduct" },
            rows.Select(r => r.Route).ToArray());

        // The layout footer hard-codes exactly these five hrefs — assert the
        // _Layout.cshtml footer block references all five (the unconditional
        // render, the floor-honesty invariant).
        var layout = ResolveLayout();
        foreach (var route in rows.Select(r => r.Route))
            Assert.True(layout.Contains($"href=\"{route}\""),
                $"the footer must link {route} unconditionally");

        // Over a seeded first-boot tree: the four Page-backed surfaces resolve
        // to non-null ids (edit targets); about stays null (preview-only).
        var bySlug = rows.ToDictionary(r => r.Slug, r => r.PageId);
        Assert.True(bySlug["about"] is null,
            "about is a view, not a seeded Page (ADR 0043 D1)");
        foreach (var s in new[] { "terms", "help", "privacy", "conduct" })
            Assert.False(string.IsNullOrWhiteSpace(bySlug[s]), $"{s} did not resolve to a page id");
    }

    /// <summary>Locates <c>Views/Shared/_Layout.cshtml</c> at the repo root.</summary>
    private static string ResolveLayout()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
            dir = dir.Parent;
        Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
        var layout = Path.Combine(dir!.FullName, "src", "Kumunita.Web", "Views", "Shared", "_Layout.cshtml");
        Assert.True(File.Exists(layout), $"_Layout.cshtml not found at {layout}.");
        return File.ReadAllText(layout);
    }

    // ── harness ─────────────────────────────────────────────────────────────

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object>? values) { }
    }
}
