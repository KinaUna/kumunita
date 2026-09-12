using Kumunita.Core;
using Kumunita.Core.Localization;
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

    private static (StaticPagesController controller, ITranslationProvider provider) BuildAbout(
        LocalizedPage? page, string? cookie)
    {
        var provider = Substitute.For<ITranslationProvider>();
        provider.GetPageAsync("about", Arg.Any<string?>())
            .Returns(Task.FromResult(page));

        var controller = new StaticPagesController(
            provider,
            Options.Create(new CommunityOptions { Name = "Maplewood", SupportEmail = "maps@example.com" }));

        var httpContext = new DefaultHttpContext();
        if (cookie is not null)
            // IRequestCookieCollection is read-only; seed the preference via the raw
            // Cookie header (the cookie collection parses it) — same value LocaleCookie.Read sees.
            httpContext.Request.Headers["Cookie"] = $"kumunita.locale={cookie}";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return (controller, provider);
    }

    [Fact]
    public async Task Get_About_NoPage_Renders_ProductStory_View()
    {
        var (controller, _) = BuildAbout(page: null, cookie: null);

        var result = await controller.About();

        // L9's "truly absent" branch: a fresh instance's /about is the product
        // pitch (Views/Home/About), not a 404.
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("~/Views/Home/About", view.ViewName);
        var vm = Assert.IsType<Models.HomeViewModel>(view.ViewData.Model);
        Assert.Equal("Maplewood", vm.CommunityName);
    }

    // ── (d) GET /about — page present → localized page in preferred language ─

    [Fact]
    public async Task Get_About_PagePresent_Renders_LocalizedPage_In_Preference()
    {
        var page = new LocalizedPage
        {
            Slug = "about",
            LanguageCode = "pl",
            Title = "O nas",
            Body = "Jesteśmy Twoim sąsiedztwem.",
            Updated = DateTimeOffset.UtcNow,
        };
        var (controller, provider) = BuildAbout(page: page, cookie: "pl");

        var result = await controller.About();

        // The page renders through the shared static-page engine, and the
        // preferred code (the pl cookie) is what was handed to the provider.
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Page", view.ViewName);
        Assert.Same(page, view.ViewData.Model);

        await provider.Received(1).GetPageAsync("about", "pl");
    }

    // ── /terms and /help keep the 404 floor (the additive Slugs guard is
    //      non-regressive — terms/help still NotFound when absent, not
    //      product-story) ──────────────────────────────────────────────────

    [Fact]
    public async Task Get_Terms_NoPage_Still_404s()
    {
        var provider = Substitute.For<ITranslationProvider>();
        provider.GetPageAsync("terms", Arg.Any<string?>())
            .Returns(Task.FromResult<LocalizedPage?>(null));

        var controller = new StaticPagesController(
            provider,
            Options.Create(new CommunityOptions { Name = "Kumunita" }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await controller.Terms();

        Assert.IsType<NotFoundResult>(result);
    }
}
