using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;

namespace Kumunita.Web.Tests;

/// <summary>
/// Verifies <see cref="AdminHelpController"/> (ADR 0128) — the GlobalAdmin's
/// control plane over the seeded platform help pages. The controller is a thin
/// seam over <see cref="IPageService"/>: the audit row and the destructive
/// overwrite live in the Core service (the <c>PageServiceTests.A128_*</c>
/// family pins them), so here we pin the **shape of the seam**:
/// <list type="bullet">
/// <item><b>Index</b> — the view model carries the seeded-page status list and
/// the affected-count.</item>
/// <item><b>Reset</b> — calls <c>ResetToSeededAsync(pageId, actor, roles,
/// session)</c>, redirects to Index; a non-seeded slug's
/// <see cref="InvalidOperationException"/> is surfaced as a
/// <c>TempData["error"]</c> and still redirects.</item>
/// <item><b>ResetAll</b> — calls <c>ResetAllSeededPagesAsync(actor, roles,
/// session)</c>, redirects to Index, and surfaces the count in
/// <c>TempData["info"]</c>.</item>
/// </list>
/// Mirrors <see cref="AdminSignupControllerTests"/> exactly (the house idiom
/// for a dedicated <c>/admin/*</c> controller: NSubstitute for the service,
/// the <see cref="NoOpTempDataProvider"/> for the redirect bag, the actor
/// resolved from the <see cref="ClaimsPrincipal"/>).
/// </summary>
public class AdminHelpControllerTests
{
    private const string Admin = "admin-help-001";
    private const string PageId = "pg-help-terms";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log / the bag
        }
    }

    private static (AdminHelpController controller, IPageService pages, IDocumentStore store)
        Build(IReadOnlyList<SeededPageStatus>? pagesStatus = null)
    {
        var pages = Substitute.For<IPageService>();
        pages.GetSeededPageStatusAsync().Returns(pagesStatus ?? Array.Empty<SeededPageStatus>());

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = new AdminHelpController(pages, store);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return (controller, pages, store);
    }

    // ── Index ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Index_ReturnsViewModel_WithStatusListAndCount()
    {
        var (controller, _, _) = Build(new[]
        {
            new SeededPageStatus("terms", "pg-1", "Terms of use", true),
            new SeededPageStatus("privacy", "pg-2", "Privacy", false),
            new SeededPageStatus("help", "pg-3", "Help", true),
        });

        var action = await controller.Index();
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<AdminHelpController.HelpAdminViewModel>(view.ViewData.Model);

        Assert.Equal(3, vm.Pages.Count);
        Assert.Equal(2, vm.PagesWithUpdates);   // the two flagged pages
    }

    [Fact]
    public async Task Index_NoUpdates_CountIsZero()
    {
        var (controller, _, _) = Build(new[]
        {
            new SeededPageStatus("terms", "pg-1", "Terms of use", false),
        });

        var action = await controller.Index();
        var vm = Assert.IsType<AdminHelpController.HelpAdminViewModel>(
            Assert.IsType<ViewResult>(action).ViewData.Model);

        Assert.Equal(0, vm.PagesWithUpdates);
    }

    // ── Reset (per-page) ─────────────────────────────────────────────────

    [Fact]
    public async Task Reset_CallsServiceWithActorAndRoles_AndRedirects()
    {
        var (controller, pages, _) = Build();

        var action = await controller.Reset(PageId);
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminHelpController.Index), redirect.ActionName);

        await pages.Received(1)
            .ResetToSeededAsync(Arg.Is(PageId), Arg.Is(Admin), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<IDocumentSession>());
    }

    [Fact]
    public async Task Reset_NonSeededSlug_SurfacesErrorAndStillRedirects()
    {
        var (controller, pages, _) = Build();
        pages.ResetToSeededAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException(
                new InvalidOperationException("This page has no seeded baseline.")));

        var action = await controller.Reset("pg-not-seeded");
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminHelpController.Index), redirect.ActionName);
        Assert.Equal("This page has no seeded baseline.", controller.TempData["error"] as string);
    }

    // ── ResetAll (bulk) ──────────────────────────────────────────────────

    [Fact]
    public async Task ResetAll_CallsServiceWithActorAndRoles_AndRedirects()
    {
        var (controller, pages, _) = Build();
        pages.ResetAllSeededPagesAsync(Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<IDocumentSession>())
            .Returns(5);

        var action = await controller.ResetAll();
        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminHelpController.Index), redirect.ActionName);

        await pages.Received(1)
            .ResetAllSeededPagesAsync(Arg.Is(Admin), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<IDocumentSession>());
        Assert.Equal("5 pages reset to their seeded text.", controller.TempData["info"] as string);
    }

    [Fact]
    public async Task ResetAll_SinglePage_SingularCopy()
    {
        var (controller, pages, _) = Build();
        pages.ResetAllSeededPagesAsync(Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(),
                Arg.Any<IDocumentSession>())
            .Returns(1);

        await controller.ResetAll();
        Assert.Equal("1 page reset to its seeded text.", controller.TempData["info"] as string);
    }
}
