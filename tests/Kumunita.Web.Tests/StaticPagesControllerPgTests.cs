using Kumunita.Core;
using Kumunita.Core.Pages;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// PG U07 (ADR 0039 §3.8/§3.9) — the <see cref="StaticPagesController"/> is
/// now **tree-only**: each route (<c>/terms</c>, <c>/help</c>, <c>/about</c>)
/// reads <see cref="IPageService.GetByPathAsync"/> (one store — the absorb,
/// complete). A path absent from the tree is a 404 (the page floor), except
/// <c>/about</c>, which degrades to the product-story view. The legacy
/// fallback seam was retired in U07 — the controller no longer takes an
/// <c>ITranslationProvider</c>.
/// <para>
/// The pins this harness owns:
/// <list type="number">
/// <item><b>Tree-present:</b> a <see cref="Page"/> at the slug renders the
///       shared <c>Page</c> view (the <c>StaticPageViewModel</c> shape
///       Title/Body/Updated), driven by the tree (the absorb).</item>
/// <item><b>Tree-absent:</b> a <c>KeyNotFoundException</c> from the tree →
///       <c>/about</c> degrades to the product-story view (the U05 drift pin —
///       <c>about</c> is NOT seeded); <c>/terms</c> / <c>/help</c> keep the
///       404 floor.</item>
/// </list>
/// </para>
/// <para>
/// The harness mirrors the existing controller-test idiom: NSubstitute seams
/// + <see cref="DefaultHttpContext"/>, no TestServer, no host.
/// </para>
/// </summary>
public class StaticPagesControllerPgTests
{
    private static (StaticPagesController controller, IPageService pages) Build(
        string slug,
        Func<string, Page?>? pageFactory = null)
    {
        var pages = Substitute.For<IPageService>();

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

        var controller = new StaticPagesController(
            pages,
            Options.Create(new CommunityOptions { Name = "Maplewood", SupportEmail = "maps@example.com" }));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        return (controller, pages);
    }

    [Fact(DisplayName = "U07 tree-present: a Page at /terms renders the shared Page view, driven by the tree (the absorb)")]
    public async Task Get_Terms_TreePresent_RendersPageView()
    {
        var now = DateTimeOffset.UtcNow;
        var (controller, pages) = Build(
            "terms",
            pageFactory: s => new Page
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
            });

        var result = await controller.Terms();

        // The shared static-page view (Title/Body/Updated), NOT a 404 /
        // product-story, and driven by the tree (the absorb).
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Page", view.ViewName);
        Assert.Equal("terms", view.ViewData["Slug"]);

        // The controller adapts the Page to the StaticPageViewModel shape
        // (Title/Body/Updated) so the SAME single view + MarkdownRenderer
        // render it (the byte-identical gate).
        var model = Assert.IsType<StaticPagesController.StaticPageViewModel>(view.ViewData.Model);
        Assert.Equal("terms", model.Slug);
        Assert.Equal("Terms of Use", model.Title);
        Assert.Equal("The terms body (markdown).", model.Body);
        Assert.Equal(now, model.Updated); // Modified ?? Created

        await pages.Received(1).GetByPathAsync("terms");
    }

    [Fact(DisplayName = "U07 about: tree-absent → the product-story view (the U05 drift pin — about is NOT seeded)")]
    public async Task Get_About_TreeAbsent_ProductStoryView()
    {
        // Absent from the tree — the exact "fresh instance /about" state (no
        // Page seeded for `about`).
        var (controller, _) = Build("about"); // default to absent

        var result = await controller.About();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("About", view.ViewName); // the full-bleed product story
        var vm = Assert.IsType<Models.HomeViewModel>(view.ViewData.Model);
        Assert.Equal("Maplewood", vm.CommunityName);
    }

    [Fact(DisplayName = "U07 help: tree-absent → 404 (the page floor, NOT the product-story)")]
    public async Task Get_Help_TreeAbsent_Returns404()
    {
        var (controller, _) = Build("help"); // absent

        var result = await controller.Help();

        Assert.IsType<NotFoundResult>(result);
    }
}
