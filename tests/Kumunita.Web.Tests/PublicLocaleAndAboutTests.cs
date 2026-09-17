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
}
