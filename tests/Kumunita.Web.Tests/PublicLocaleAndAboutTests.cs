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
            pages.GetByPathAsync(Arg.Any<string>())
                .Returns(Task.FromException<Page>(new KeyNotFoundException()));
        }
        else
        {
            // ADR 0040: the `about` page lives under the `system/` root, so
            // the controller resolves `system/about` first.
            pages.GetByPathAsync("system/about").Returns(page);
            pages.GetByPathAsync("about")
                .Returns(Task.FromException<Page>(new KeyNotFoundException()));
        }

        var controller = new StaticPagesController(
            pages,
            Options.Create(new CommunityOptions { Name = "Maplewood", SupportEmail = "maps@example.com" }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

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
        pages.GetByPathAsync("terms")
            .Returns(Task.FromException<Page>(new KeyNotFoundException()));

        var controller = new StaticPagesController(
            pages,
            Options.Create(new CommunityOptions { Name = "Kumunita" }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.Terms();

        Assert.IsType<NotFoundResult>(result);
    }

    // ── (e) U05: the about view wraps exactly the 17 D5 keys ─────────────────

    /// <summary>
    /// The ADR 0042 D5 closed contract: the 17 <c>about.*</c> keys are the
    /// stable key list U01–U05 code against. U01 registered them in
    /// <see cref="KnownTranslationKeys.EnValues"/>, U02/U03 translated them,
    /// and U05 wraps the view against exactly these names. The
    /// <see cref="KwLRegistryConsistencyTests"/> cover the *registry-side*
    /// half (every key the views emit is registered); this test covers the
    /// *view-side* half (every D5 key the contract names is wrapped in
    /// About.cshtml), so a future view edit that drops a wrap is caught
    /// immediately.
    /// </summary>
    [Fact(DisplayName = "About.cshtml wraps all 17 ADR 0042 D5 about.* keys in kw-l")]
    public void About_View_Wraps_All_D5_Keys()
    {
        var aboutView = ResolveAboutView();
        var text = File.ReadAllText(aboutView);

        // The closed D5 key list — exactly these 17, in order (ADR 0042 D5).
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
            "about.stats.neighbors",
            "about.stats.groups",
            "about.stats.posts",
            "about.stats.pinned",
            "about.project.eyebrow",
            "about.project.heading",
            "about.project.lead",
        };

        Assert.True(d5Keys.Length == 17,
            "The D5 list length changed — update this test to match ADR 0042 D5.");

        // Every D5 key must appear as kw-l key="…" in the view.
        var missing = d5Keys
            .Where(k => !Regex.IsMatch(text, $@"kw-l\b[^>]*\bkey=""{Regex.Escape(k)}"""))
            .ToList();

        Assert.True(missing.Count == 0,
            "D5 about.* key(s) not wrapped in kw-l in About.cshtml:\n" +
            string.Join("\n", missing.Select(k => $"  {k}")));

        // The view must NOT wrap any about.* key beyond the 17 D5 keys —
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
