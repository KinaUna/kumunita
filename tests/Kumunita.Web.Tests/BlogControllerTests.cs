using System.Security.Claims;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0040 — the <see cref="Kumunita.Web.Controllers.BlogController"/>
/// (the per-resident **blog feed**, <c>/blog</c> + <c>/blog/{userId}</c>).
/// <para>
/// The pins this harness owns:
/// <list type="number">
/// <item><b><c>GET /blog</c> (signed-in)</b> — a self-route that redirects to
///       <c>Feed(userId = the actor's own id)</c> (their own feed is the
///       "home" of their blog).</item>
/// <item><b><c>GET /blog</c> (signed-out)</b> — there is no "own" feed for a
///       visitor; redirect to <c>Directory/Index</c> (the resident catalog,
///       the one place "who is here" is always listed).</item>
/// <item><b><c>GET /blog/{userId}</c></b> — returns the resident's
///       <see cref="PageKind"/> = <c>User</c> pages, each linking to its
///       derived <c>/pages/…</c> href, the author's display name
///       (best-effort), and <c>IsOwner</c> true only when
///       <c>userId == actorId</c>.</item>
/// <item><b>Draft gate (the ADR 0037 pin)</b> — a draft page is its author's:
///       it is <b>absent</b> from another resident's feed (filtered out), but
///       present — badged <c>IsDraft</c> — on the author's own feed.</item>
/// <item><b>Empty feed</b> — a resident with no blog pages yet is a valid
///       feed shape (an empty <c>Posts</c> list), NOT a 404.</item>
/// </list>
/// </para>
/// <para>
/// The harness mirrors the sibling controller-test idiom: NSubstitute seams
/// (<see cref="IPageService"/> + <see cref="IUserInfoService"/>) +
/// <see cref="DefaultHttpContext"/>, no TestServer, no host.
/// </para>
/// </summary>
public class BlogControllerTests
{
    // A resident's blog, rooted at their <c>blog/{userId}</c> root, with one
    // sub-page nested under it (the derived-path walk exercises the ancestor
    // chain).
    private const string BlogRoot = "blog/owner-001";
    private static readonly Page Root = new()
    {
        Id = "page-root-001",
        ParentId = null,
        Slug = "blog",
        Kind = PageKind.User,
        AuthorId = "owner-001",
        Title = "My blog",
        IsDraft = false,
        Created = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
        Modified = DateTimeOffset.Parse("2026-09-01T00:00:00Z"),
    };
    private static readonly Page Sub = new()
    {
        Id = "page-sub-001",
        ParentId = Root.Id,
        Slug = "recipes",
        Kind = PageKind.User,
        AuthorId = "owner-001",
        Title = "Recipes",
        IsDraft = false,
        Created = DateTimeOffset.Parse("2026-09-05T00:00:00Z"),
        Modified = DateTimeOffset.Parse("2026-09-06T00:00:00Z"),
    };
    private static readonly Page Draft = new()
    {
        Id = "page-draft-001",
        ParentId = Root.Id,
        Slug = "wip",
        Kind = PageKind.User,
        AuthorId = "owner-001",
        Title = "A work in progress",
        IsDraft = true,
        Created = DateTimeOffset.Parse("2026-09-07T00:00:00Z"),
        Modified = DateTimeOffset.Parse("2026-09-08T00:00:00Z"),
    };

    private static (BlogController controller, IPageService pages, IUserInfoService userInfo) Build(
        bool signedIn,
        string? actorId = null)
    {
        var pages = Substitute.For<IPageService>();
        var userInfo = Substitute.For<IUserInfoService>();
        var controller = new BlogController(pages, userInfo);
        var http = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        if (signedIn && actorId is not null)
            http.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, actorId)],
                authenticationType: "test"));
        return (controller, pages, userInfo);
    }

    [Fact(DisplayName = "GET /blog (signed-in) redirects to the actor's OWN feed (the self-route)")]
    public async Task Index_SignedIn_RedirectsToOwnFeed()
    {
        var (controller, _, _) = Build(signedIn: true, actorId: "owner-001");

        var result = await controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(BlogController.Feed), redirect.ActionName);
        Assert.Equal("owner-001", redirect.RouteValues?["userId"]);
    }

    [Fact(DisplayName = "GET /blog (signed-out) redirects to the resident catalog (Directory)")]
    public async Task Index_SignedOut_RedirectsToDirectory()
    {
        var (controller, _, _) = Build(signedIn: false);

        var result = await controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Directory", redirect.ControllerName);
    }

    [Fact(DisplayName = "GET /blog/{userId} lists the resident's User-kind pages, each linking to its /pages/… href")]
    public async Task Feed_ReturnsResidentPages_WithDerivedHrefs()
    {
        var (controller, pages, userInfo) = Build(signedIn: false);

        // The blog read lane returns the author's pages in the service's own
        // order (newest-first in production); the controller preserves it, so
        // the harness plants in the order it then asserts.
        pages.GetBlogPagesAsync("owner-001").Returns(new List<Page> { Root, Sub });
        // The live tree (for the derived-path walk — the full ancestor chain).
        pages.GetTreeAsync().Returns(new List<Page> { Root, Sub });
        userInfo.GetProfileAsync("owner-001").Returns(new Profile { DisplayName = "Robin Resident" });

        var result = await controller.Feed("owner-001");

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<BlogViewModel>(view.ViewData.Model);
        Assert.Equal("owner-001", model.AuthorId);
        Assert.Equal("Robin Resident", model.AuthorDisplayName);
        Assert.False(model.IsOwner); // the visitor is not the author
        Assert.Equal(2, model.Posts.Count);

        // Each row links to its derived /pages/… path (root-first slug chain).
        Assert.Equal("My blog", model.Posts[0].Title);
        Assert.Equal("/pages/blog", model.Posts[0].Href);
        Assert.Equal("Recipes", model.Posts[1].Title);
        Assert.Equal("/pages/blog/recipes", model.Posts[1].Href);
        // The modified stamp (Modified ?? Created).
        Assert.Equal(Sub.Modified, model.Posts[1].Modified);

        await pages.Received(1).GetBlogPagesAsync("owner-001");
        await userInfo.Received(1).GetProfileAsync("owner-001");
    }

    [Fact(DisplayName = "GET /blog/{userId} — IsOwner is true only when userId == actorId")]
    public async Task Feed_IsOwner_FalseForAnotherResident()
    {
        // Signed-in as a DIFFERENT resident viewing owner-001's feed.
        var (controller, pages, userInfo) = Build(signedIn: true, actorId: "visitor-002");
        pages.GetBlogPagesAsync("owner-001").Returns(new List<Page> { Root });
        pages.GetTreeAsync().Returns(new List<Page> { Root });
        userInfo.GetProfileAsync("owner-001").Returns(new Profile { DisplayName = "Robin Resident" });

        var result = await controller.Feed("owner-001");

        var model = Assert.IsType<BlogViewModel>(Assert.IsType<ViewResult>(result).ViewData.Model);
        Assert.False(model.IsOwner);
    }

    [Fact(DisplayName = "GET /blog/{own id} — IsOwner true; the author sees their own feed")]
    public async Task Feed_Owner_IdentifiesAsOwner()
    {
        var (controller, pages, userInfo) = Build(signedIn: true, actorId: "owner-001");
        pages.GetBlogPagesAsync("owner-001").Returns(new List<Page> { Root });
        pages.GetTreeAsync().Returns(new List<Page> { Root });
        userInfo.GetProfileAsync("owner-001").Returns(new Profile { DisplayName = "Robin Resident" });

        var result = await controller.Feed("owner-001");

        var model = Assert.IsType<BlogViewModel>(Assert.IsType<ViewResult>(result).ViewData.Model);
        Assert.True(model.IsOwner);
    }

    [Fact(DisplayName = "Draft gate — a draft page is ABSENT from another resident's feed (the ADR 0037 pin)")]
    public async Task Feed_NonOwner_DraftIsFilteredOut()
    {
        var (controller, pages, userInfo) = Build(signedIn: true, actorId: "visitor-002");
        // The service returns the author's full set, including a draft.
        pages.GetBlogPagesAsync("owner-001").Returns(new List<Page> { Sub, Draft });
        pages.GetTreeAsync().Returns(new List<Page> { Sub, Draft });
        userInfo.GetProfileAsync("owner-001").Returns(new Profile { DisplayName = "Robin Resident" });

        var result = await controller.Feed("owner-001");

        var model = Assert.IsType<BlogViewModel>(Assert.IsType<ViewResult>(result).ViewData.Model);
        // Only the published page is listed; the draft never appears.
        Assert.Single(model.Posts);
        Assert.Equal("Recipes", model.Posts[0].Title);
        Assert.False(model.Posts[0].IsDraft);
    }

    [Fact(DisplayName = "Draft gate — on the author's OWN feed a draft appears, badged IsDraft")]
    public async Task Feed_Owner_DraftIsListed_AndBadged()
    {
        var (controller, pages, userInfo) = Build(signedIn: true, actorId: "owner-001");
        pages.GetBlogPagesAsync("owner-001").Returns(new List<Page> { Draft, Sub });
        // The tree must include the draft's parent (Root) so its derived path
        // (blog/wip) resolves — the controller walks the ancestor chain.
        pages.GetTreeAsync().Returns(new List<Page> { Root, Sub, Draft });
        userInfo.GetProfileAsync("owner-001").Returns(new Profile { DisplayName = "Robin Resident" });

        var result = await controller.Feed("owner-001");

        var model = Assert.IsType<BlogViewModel>(Assert.IsType<ViewResult>(result).ViewData.Model);
        Assert.Equal(2, model.Posts.Count);
        var draftRow = model.Posts.Single(p => p.Id == Draft.Id);
        Assert.True(draftRow.IsDraft);
        Assert.Equal("/pages/blog/wip", draftRow.Href);
    }

    [Fact(DisplayName = "Empty feed — a resident with no blog pages is a valid shape (NOT a 404)")]
    public async Task Feed_Empty_ReturnsValidEmptyModel_NotNotFound()
    {
        var (controller, pages, userInfo) = Build(signedIn: false);
        pages.GetBlogPagesAsync("owner-001").Returns(new List<Page>());
        pages.GetTreeAsync().Returns(new List<Page>());
        userInfo.GetProfileAsync("owner-001").Returns((Profile?)null); // no profile

        var result = await controller.Feed("owner-001");

        // A valid feed (the "no blog posts yet" state), NOT a 404.
        var model = Assert.IsType<BlogViewModel>(Assert.IsType<ViewResult>(result).ViewData.Model);
        Assert.Empty(model.Posts);
        // Best-effort name resolution: a missing profile is a display gap, not
        // an error (the feed renders under the id).
        Assert.Null(model.AuthorDisplayName);
    }

    [Fact(DisplayName = "GET /blog/{userId} with a blank id is a 404")]
    public async Task Feed_BlankUserId_IsNotFound()
    {
        var (controller, _, _) = Build(signedIn: false);

        var result = await controller.Feed("   ");

        Assert.IsType<NotFoundResult>(result);
    }
}
