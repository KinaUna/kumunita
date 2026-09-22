using System.Text.RegularExpressions;
using Kumunita.Core;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// U7 (ML-UI) Web-layer tests for the public language picker
/// (<see cref="PublicLocaleController"/>) and the <c>/about</c> wiring
/// (<see cref="StaticPagesController.About"/>). Uses the project's existing
/// direct-construction harness (NSubstitute seams + <see cref="DefaultHttpContext"/>),
/// no TestServer.
///
/// **FACES coverage:** L3's *picker-reachable-unsigned-out + catalog-shape*
/// half (GET /language) and L9 (about → product-story fallback / about →
/// localized page) are exercised here. **Deferred to U8 (the authoritative
/// FACES gate):** the <c>POST /language</c> → cookie-write assertion (M·11) —
/// this harness has no <see cref="ITempDataProvider"/>, so the action's
/// <c>TempData</c> write NREs before the cookie can be observed (same reason
/// the existing controller tests avoid any TempData-writing branch). U8's
/// TestServer-backed gate verifies the actual cookie write end-to-end.
/// </summary>
public class PublicLocaleAndAboutTests
{
    // ── (a) GET /language — anonymous, enabled catalog in SortOrder ───────

    private static PublicLocaleController BuildPicker(
        IEnumerable<LanguageCatalog> catalog, LocaleSettings? settings)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync()
            .Returns(Task.FromResult<IReadOnlyList<LanguageCatalog>>(catalog.ToList()));

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(settings));
        store.QuerySession().Returns(readSession);

        var controller = new PublicLocaleController(localization, store);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact]
    public async Task Get_Language_Anonymous_Returns_Enabled_Catalog_In_SortOrder()
    {
        var controller = BuildPicker(
            new[]
            {
                new LanguageCatalog { Id = "en", NativeName = "English",  Enabled = true,  SortOrder = 0 },
                new LanguageCatalog { Id = "pl", NativeName = "Polski",   Enabled = true,  SortOrder = 2 },
                new LanguageCatalog { Id = "fr", NativeName = "Français", Enabled = false, SortOrder = 1 },
            },
            settings: new LocaleSettings { DefaultLanguageCode = "en" });

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LocaleController.LocaleSettingsViewModel>(view.ViewData.Model);

        // Only enabled rows, ordered by SortOrder (en before pl; fr disabled → excluded).
        Assert.Equal(new[] { "en", "pl" }, model.Languages.Select(l => l.Code).ToArray());
        // The settings singleton's default flows into the model.
        Assert.Equal("en", model.DefaultCode);
    }

    // ── (c) GET /about — page truly absent → product-story view ────────────

    private static (StaticPagesController controller, IPageService pages) BuildAbout(Page? page)
    {
        var pages = Substitute.For<IPageService>();

        if (page is null)
        {
            // Absent from the tree (KeyNotFoundException) — the U05 drift pin:
            // a fresh instance has no `about` Page. Both the ADR 0040 primary
            // path (system/about) and the legacy fallback (about) miss.
            pages.ResolvePageAsync(Arg.Any<string>())
                .Returns(Task.FromException<(Page, IReadOnlyList<PageTranslation>)>(
                    new KeyNotFoundException()));
        }
        else
        {
            // ADR 0040: the `about` page lives under the `system/` root, so
            // the controller resolves `system/about` first.
            pages.ResolvePageAsync("system/about")
                .Returns(Task.FromResult((page, new List<PageTranslation>() as IReadOnlyList<PageTranslation>)));
            pages.ResolvePageAsync("about")
                .Returns(Task.FromException<(Page, IReadOnlyList<PageTranslation>)>(
                    new KeyNotFoundException()));
        }

        var controller = StaticPagesControllerHarness.Build(pages);
        return (controller, pages);
    }

    [Fact]
    public async Task Get_About_NoPage_Renders_ProductStory_View()
    {
        var (controller, _) = BuildAbout(page: null);

        var result = await controller.About();

        // L9's "truly absent" branch: a fresh instance's /about is the product
        // pitch (Views/StaticPages/About), not a 404.
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("About", view.ViewName);
        var vm = Assert.IsType<Models.HomeViewModel>(view.ViewData.Model);
        Assert.Equal("Maplewood", vm.CommunityName);
    }

    // ── (d) GET /about — page present → the shared Page view ───────────────

    [Fact]
    public async Task Get_About_PagePresent_RendersPageView()
    {
        var (controller, _) = BuildAbout(page: new Page
        {
            Id = "page-about-001",
            ParentId = null,
            Slug = "about",
            Title = "O nas",
            Body = "Jesteśmy Twoim sąsiedztwem.",
            LanguageCode = "pl",
            Audience = null,
            Created = DateTimeOffset.UtcNow,
            Modified = DateTimeOffset.UtcNow,
        });

        var result = await controller.About();

        // The page renders through the shared static-page engine (the
        // StaticPageViewModel shape).
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Page", view.ViewName);
        var model = Assert.IsType<StaticPagesController.StaticPageViewModel>(view.ViewData.Model);
        Assert.Equal("about", model.Slug);
        Assert.Equal("O nas", model.Title);
    }

    // ── /terms and /help keep the 404 floor (terms/help still NotFound when
    //      absent from the tree, not product-story) ─────────────────────────

    [Fact]
    public async Task Get_Terms_NoPage_Still_404s()
    {
        var pages = Substitute.For<IPageService>();
        pages.ResolvePageAsync(Arg.Any<string>())
            .Returns(Task.FromException<(Page, IReadOnlyList<PageTranslation>)>(new KeyNotFoundException()));

        var controller = StaticPagesControllerHarness.Build(pages,
            community: new CommunityOptions { Name = "Kumunita" });

        var result = await controller.Terms();

        Assert.IsType<NotFoundResult>(result);
    }

    // ── (f) LS U06: the Web surface pins the first-boot catalog end-to-end ──
    // ADR 0042 D4: a fresh instance boots with the catalog en(sort 0) / de(sort
    // 1) / fr(sort 2), all enabled, default en. The Web surface must list all
    // three in SortOrder — the public /language picker AND the settings page —
    // and the admin completeness view (M·12) must report 100% present for de
    // and fr. The Core half (the seeder state + provider resolution) is pinned
    // in LS_U04_SeederTests; these pins close the Web read path.

    /// <summary>
    /// The first-boot catalog exactly as the seeder writes it (LS U04, ADR
    /// 0042 D4) — the same shape the <see cref="FirstBootSeeder"/>
    /// <c>SeedLanguageCatalogAsync</c> step materializes.
    /// </summary>
    private static IReadOnlyList<LanguageCatalog> FirstBootCatalog() =>
    [
        new LanguageCatalog { Id = "en", NativeName = "English",  Enabled = true, SortOrder = 0 },
        new LanguageCatalog { Id = "de", NativeName = "Deutsch",  Enabled = true, SortOrder = 1 },
        new LanguageCatalog { Id = "fr", NativeName = "Français", Enabled = true, SortOrder = 2 },
    ];

    [Fact(DisplayName = "LS U06 the public /language picker lists en/de/fr in SortOrder for the first-boot catalog")]
    public async Task LS_U06_PublicPicker_ListsFirstBootCatalog_InSortOrder()
    {
        var controller = BuildPicker(FirstBootCatalog(),
            settings: new LocaleSettings { DefaultLanguageCode = "en" });

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LocaleController.LocaleSettingsViewModel>(view.ViewData.Model);

        // All three enabled rows, in SortOrder (en 0 / de 1 / fr 2) — the ADR
        // 0042 D4 first-boot catalog, native names intact.
        Assert.Equal(new[] { "en", "de", "fr" }, model.Languages.Select(l => l.Code).ToArray());
        Assert.Equal(new[] { "English", "Deutsch", "Français" }, model.Languages.Select(l => l.NativeName).ToArray());

        // The default stays `en` (ADR 0042 D4 — the lane is about *available*
        // languages, not switching the default).
        Assert.Equal("en", model.DefaultCode);
    }

    // ── The settings page (/settings/language) lists the same catalog ───────
    // (LocaleController.Index — the [Authorize]d settings surface; the
    // language section shares the exact model shape with the public picker.)

    private static LocaleController BuildSettingsController(
        IEnumerable<LanguageCatalog> catalog, LocaleSettings? settings)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync()
            .Returns(Task.FromResult<IReadOnlyList<LanguageCatalog>>(catalog.ToList()));
        // The timezone / date-format sections read these even for a
        // signed-out subject (the arguments are evaluated before the
        // null-subject short-circuit inside the build helpers).
        localization.GetDefaultTimezoneAsync().Returns(Task.FromResult("UTC"));
        localization.GetDefaultDateFormatAsync()
            .Returns(Task.FromResult(Kumunita.Core.Localization.DateFormat.FloorFormat));

        var userInfo = Substitute.For<Kumunita.Core.UserInfo.IUserInfoService>();

        var store = Substitute.For<IDocumentStore>();
        var readSession = Substitute.For<IQuerySession>();
        readSession.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(settings));
        store.QuerySession().Returns(readSession);

        var controller = new LocaleController(localization, userInfo, store);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact(DisplayName = "LS U06 the settings language surface lists en/de/fr in SortOrder for the first-boot catalog")]
    public async Task LS_U06_SettingsSurface_ListsFirstBootCatalog_InSortOrder()
    {
        var controller = BuildSettingsController(FirstBootCatalog(),
            settings: new LocaleSettings { DefaultLanguageCode = "en" });

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LocaleController.LocaleSettingsViewModel>(view.ViewData.Model);

        Assert.Equal(new[] { "en", "de", "fr" }, model.Languages.Select(l => l.Code).ToArray());
        Assert.Equal("en", model.DefaultCode);
    }

    // ── The admin completeness view (M·12) reports de / fr at 100% ──────────
    // (LanguagesController.Index — the one admin surface a resident's gap is
    // surfaced before they hit it; the LS lane's direct pin for "it clearly
    // shows how the language features work from the first bootup".)

    private static LanguagesController BuildAdminShell(
        IEnumerable<LanguageCatalog> catalog,
        Dictionary<string, (IReadOnlyList<string> present, IReadOnlyList<string> missing)> completeness)
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync()
            .Returns(Task.FromResult<IReadOnlyList<LanguageCatalog>>(catalog.ToList()));
        localization.GetCompletenessAsync(Arg.Any<string>())
            .Returns(callInfo =>
            {
                var code = callInfo.Arg<string>();
                var (present, missing) = completeness[code];
                return Task.FromResult(new LanguageCompleteness(code, present, missing));
            });

        var controller = new LanguagesController(localization);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    [Fact(DisplayName = "LS U06 the admin completeness view reports en/de/fr at 100% on the first-boot catalog")]
    public async Task LS_U06_AdminCompleteness_ReportsFirstBootAt100Percent()
    {
        var allKeys = KnownTranslationKeys.AllKeys.ToList();

        // A fresh boot: every language has every registered key (0 missing —
        // the completeness universe equals the registry exactly).
        var completeness = new Dictionary<string, (IReadOnlyList<string>, IReadOnlyList<string>)>
        {
            ["en"] = (allKeys, []),
            ["de"] = (allKeys, []),
            ["fr"] = (allKeys, []),
        };

        var controller = BuildAdminShell(FirstBootCatalog(), completeness);

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        var rows = Assert.IsAssignableFrom<IEnumerable<LanguagesController.LanguageRowViewModel>>(view.ViewData.Model);
        var list = rows.ToList();

        // The shell lists the catalog in SortOrder — en / de / fr.
        Assert.Equal(new[] { "en", "de", "fr" }, list.Select(r => r.Code).ToArray());

        // 100% present for each — the direct first-boot pin.
        Assert.All(list, r =>
        {
            Assert.Empty(r.MissingKeys);
            Assert.Equal(KnownTranslationKeys.AllKeys.Count, r.PresentKeys.Count);
        });
    }

    // ── (e) U05: the about view wraps exactly the 13 D5 keys ─────────────────

    /// <summary>
    /// The ADR 0042 D5 closed contract: the 13 <c>about.*</c> keys are the
    /// stable key list U01–U05 code against (the four about.stats.* keys were
    /// removed with the stats band on 2026-09-22). U01 registered them in
    /// <see cref="KnownTranslationKeys.EnValues"/>, U02/U03 translated them,
    /// and U05 wraps the view against exactly these names. The
    /// <see cref="KwLRegistryConsistencyTests"/> cover the *registry-side*
    /// half (every key the views emit is registered); this test covers the
    /// *view-side* half (every D5 key the contract names is wrapped in
    /// About.cshtml), so a future view edit that drops a wrap is caught
    /// immediately.
    /// </summary>
    [Fact(DisplayName = "About.cshtml wraps all 13 ADR 0042 D5 about.* keys in kw-l")]
    public void About_View_Wraps_All_D5_Keys()
    {
        var aboutView = ResolveAboutView();
        var text = File.ReadAllText(aboutView);

        // The closed D5 key list — exactly these 13, in order (ADR 0042 D5).
        var d5Keys = new[]
        {
            "about.eyebrow",
            "about.lead",
            "about.cta_feed",
            "about.cta_notes",
            "about.features.one.title",
            "about.features.one.body",
            "about.features.groups.title",
            "about.features.groups.body",
            "about.features.pinned.title",
            "about.features.pinned.body",
            "about.project.eyebrow",
            "about.project.heading",
            "about.project.lead",
        };

        Assert.True(d5Keys.Length == 13,
            "The D5 list length changed — update this test to match ADR 0042 D5.");

        // Every D5 key must appear as kw-l key="…" in the view.
        var missing = d5Keys
            .Where(k => !Regex.IsMatch(text, $@"kw-l\b[^>]*\bkey=""{Regex.Escape(k)}"""))
            .ToList();

        Assert.True(missing.Count == 0,
            "D5 about.* key(s) not wrapped in kw-l in About.cshtml:\n" +
            string.Join("\n", missing.Select(k => $"  {k}")));

        // The view must NOT wrap any about.* key beyond the 13 D5 keys —
        // a stray wrap of an unregistered key would render the raw key to
        // the resident (the PG-lane regression KwLRegistryConsistencyTests
        // guards against, checked here for the about.* namespace specifically).
        var allAboutKeys = Regex
            .Matches(text, @"kw-l\b[^>]*\bkey=""(about\.[^""]+)""")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        var stray = allAboutKeys.Except(d5Keys).ToList();
        Assert.True(stray.Count == 0,
            "About.cshtml wraps about.* key(s) not in the ADR 0042 D5 contract:\n" +
            string.Join("\n", stray.Select(k => $"  {k}")));
    }

    /// <summary>
    /// Locates <c>Views/StaticPages/About.cshtml</c> by walking up from the
    /// test assembly's output directory to the repo root (the dir that holds
    /// <c>Kumunita.slnx</c>) — the same pattern as
    /// <see cref="KwLRegistryConsistencyTests"/>.
    /// </summary>
    private static string ResolveAboutView()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            "Could not locate the repo root (Kumunita.slnx) above the test output directory.");

        var about = Path.Combine(dir!.FullName, "src", "Kumunita.Web", "Views", "StaticPages", "About.cshtml");
        Assert.True(File.Exists(about), $"About.cshtml not found at {about}.");
        return about;
    }
}
