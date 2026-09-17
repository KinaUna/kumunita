using Kumunita.Core;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// PG U05 (ADR 0039 §3.8/§3.9) — the <see cref="StaticPagesController"/>
/// retarget onto the new <see cref="Page"/> tree. The controller is now
/// **tree-first**: each route (<c>/terms</c>, <c>/help</c>, <c>/about</c>)
/// tries <see cref="IPageService.GetByPathAsync"/> first, and only when the
/// tree reports "absent" (<see cref="KeyNotFoundException"/>) does it fall
/// through to the legacy <see cref="ITranslationProvider.GetPageAsync"/>
/// (the <see cref="LocalizedPage"/> store, retired in U07) and, for
/// <c>/about</c>, to the product-story view.
/// <para>
/// The pins this harness owns:
/// <list type="number">
/// <item><b>Tree-present wins:</b> a <see cref="Page"/> at the slug renders the
///       shared <c>Page</c> view (Title/Body/Updated), and the legacy provider
///       is **not consulted** (one store, not two — the absorb).</item>
/// <item><b>Tree-absent → legacy fallback:</b> a <c>KeyNotFoundException</c>
///       from the tree hands the route to the provider (the
///       "absorb, don't yank" contract until U07 retires
///       <see cref="LocalizedPage"/>).</item>
/// <item><b>Tree-absent + legacy-absent:</b> <c>/about</c> degrades to the
///       product-story view (the U05 drift pin — <c>about</c> is NOT seeded);
///       <c>/terms</c> / <c>/help</c> keep the 404 floor.</item>
/// </list>
/// </para>
/// <para>
/// The harness mirrors the existing <see cref="MLUI_FacesTests"/>
/// <c>BuildAbout</c>: NSubstitute seams + <see cref="DefaultHttpContext"/>,
/// no TestServer, no host.
/// </para>
/// </summary>
public class StaticPagesControllerPgTests
{
    private static (StaticPagesController controller, IPageService pages, ITranslationProvider provider) Build(
        string slug,
        Func<string, Page?>? pageFactory = null,
        Func<string, string?, LocalizedPage?>? legacyFactory = null)
    {
        var pages = Substitute.For<IPageService>();
        var legacy = Substitute.For<ITranslationProvider>();

        if (pageFactory is null)
        {
            // Default: absent from the tree (KeyNotFoundException — the
            // "absent" contract, not an error).
            pages.GetByPathAsync(Arg.Any<string>())
                .Returns(Task.FromException<Page>(new KeyNotFoundException($"no page at '{slug}'")));
        }
        else
        {
            pages.GetByPathAsync(slug).Returns(pageFactory(slug)!);
        }

        if (legacyFactory is null)
        {
            // Default: no legacy row (the provider is not the decision path).
            legacy.GetPageAsync(Arg.Any<string>(), Arg.Any<string?>())
                .Returns(Task.FromResult<LocalizedPage?>(null));
        }
        else
        {
            legacy.GetPageAsync(Arg.Any<string>(), Arg.Any<string?>())
                .Returns(ci => Task.FromResult(legacyFactory(ci.ArgAt<string>(0), ci.ArgAt<string?>(1))));
        }

        var controller = new StaticPagesController(
            pages,
            legacy,
            Options.Create(new CommunityOptions { Name = "Maplewood", SupportEmail = "maps@example.com" }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        return (controller, pages, legacy);
    }

    private static Task<IActionResult> Invoke(StaticPagesController controller, string slug) =>
        slug switch
        {
            "terms" => controller.Terms(),
            "help" => controller.Help(),
            "about" => controller.About(),
            _ => throw new ArgumentException(slug)
        };

    [Fact(DisplayName = "U05 tree-present: a Page at /terms renders the shared Page view and the legacy provider is NOT consulted")]
    public async Task Get_Terms_TreePresent_RendersPageView_ProviderNotConsulted()
    {
        var now = DateTimeOffset.UtcNow;
        Page? page = null;
        (var controller, var pages, var provider) = Build(
            "terms",
            pageFactory: s =>
            {
                page = new Page
                {
                    Id = "page-terms-001",
                    ParentId = null,
                    Slug = "terms",
                    Title = "Terms of Use",
                    Body = "The terms body (markdown).",
                    LanguageCode = "en",
                    Audience = null, // public by construction
                    Created = now,
                    Modified = now
                };
                return page;
            });

        var result = await controller.Terms();

        // The shared static-page view (Title/Body/Updated), NOT a 404 /
        // product-story, and driven by the tree (the absorb).
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Page", view.ViewName);
        Assert.Equal("terms", view.ViewData["Slug"]);

        // The controller adapts the Page to the legacy LocalizedPage view
        // shape (Title/Body/Updated) so the SAME single view + MarkdownRenderer
        // render it byte-identically (the U05 byte-identical gate).
        var model = Assert.IsType<LocalizedPage>(view.ViewData.Model);
        Assert.Equal("terms", model.Slug);
        Assert.Equal("en", model.LanguageCode);
        Assert.Equal("Terms of Use", model.Title);
        Assert.Equal("The terms body (markdown).", model.Body);
        Assert.Equal(now, model.Updated); // Modified ?? Created

        // One store, not two: the legacy provider is never consulted.
        await provider.DidNotReceive().GetPageAsync(Arg.Any<string>(), Arg.Any<string?>());
        await pages.Received(1).GetByPathAsync("terms");
    }

    [Fact(DisplayName = "U05 tree-absent + legacy-present: falls through to the LocalizedPage (the absorb, don't yank, until U07)")]
    public async Task Get_Terms_TreeAbsent_LegacyPresent_RendersLegacyPage()
    {
        var legacyPage = new LocalizedPage
        {
            Slug = "terms",
            LanguageCode = "en",
            Title = "Terms of Use (legacy)",
            Body = "legacy body",
            Updated = DateTimeOffset.UtcNow
        };
        var (controller, pages, provider) = Build(
            "terms",
            pageFactory: null, // absent from the tree (KeyNotFoundException)
            legacyFactory: (slug, code) => legacyPage);

        var result = await controller.Terms();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Page", view.ViewName);
        // The legacy LocalizedPage is rendered verbatim (the pre-U05 contract).
        Assert.Same(legacyPage, view.ViewData.Model);

        await pages.Received(1).GetByPathAsync("terms");
        await provider.Received(1).GetPageAsync("terms", Arg.Any<string?>());
    }

    [Fact(DisplayName = "U05 about: tree-absent + legacy-absent → the product-story view (the U05 drift pin — about is NOT seeded)")]
    public async Task Get_About_TreeAbsent_LegacyAbsent_ProductStoryView()
    {
        // Both stores report absent — the exact "fresh instance /about" state
        // (no Page seeded for `about`, no LocalizedPage about row).
        var (controller, _, _) = Build("about"); // both default to absent

        var result = await controller.About();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("About", view.ViewName); // the full-bleed product story
        var vm = Assert.IsType<Models.HomeViewModel>(view.ViewData.Model);
        Assert.Equal("Maplewood", vm.CommunityName);
    }

    [Fact(DisplayName = "U05 help: tree-absent + legacy-absent → 404 (the page floor, NOT the product-story)")]
    public async Task Get_Help_TreeAbsent_LegacyAbsent_Returns404()
    {
        var (controller, _, _) = Build("help"); // both absent

        var result = await controller.Help();

        Assert.IsType<NotFoundResult>(result);
    }
}
