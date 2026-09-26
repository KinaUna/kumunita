using System.Security.Claims;
using Kumunita.Core.Announcements;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;
using KumunitaClaimTypes = Kumunita.Core.Identity.ClaimTypes;

namespace Kumunita.Web.Tests;

/// <summary>
/// M7 (ADR 0090) <b>U04</b> — the two <i>newly-paged</i> routes' pager wiring
/// (the U04 exit criterion's three tests) + the D7 filter-reset pin.
/// <para>
/// These pin the Web-side contract that the D6 newly-paged surfaces
/// (the announcements list, the tag-by-tag route) build a
/// <see cref="PagedViewModel"/> off the U01 seam's <c>HasMore</c> signal
/// (D1 — the sole paging signal) and hand it to the shared <c>_Pager</c>
/// partial, and that a filter form submission carries no <c>page</c> param
/// (D7 — the controller floors to 1).
/// </para>
/// <list type="bullet">
/// <item><b>F6_AnnouncementsList_Page2_HasMore_PagerPresent</b> — the
///       <see cref="AnnouncementController.Index"/> harness:
///       <c>ListVisiblePagedAsync</c> returns
///       <c>AnnouncementPage(30 items, HasMore: true)</c>, page 2 →
///       the VM's <c>Pager</c> is non-null with <c>HasNext: true</c>
///       (D1) and <c>HasPrevious: true</c> (D5 — page &gt; 1).</item>
/// <item><b>F7_TagByTag_Page1_Full_BothSectionsPaged</b> — the
///       <see cref="TagController.ByTag"/> harness: both U01 paged seams
///       return a full page → the VM's <c>PagerPosts</c> and
///       <c>PagerPages</c> are both non-null with <c>HasNext: true</c>,
///       <c>HasPrevious: false</c> (the two-section route's both-pagers
///       pin, the Groups.Detail precedent).</item>
/// <item><b>F4_FilterForm_SubmitsWithoutPage</b> — the
///       <see cref="EventController.Index"/> harness with
///       <c>componentId: "safety"</c> and no <c>page</c> param:
///       the seam receives <c>page: 1</c> (the floor) and the VM's
///       <c>Pager.CurrentPage</c> is 1 (the D7 reset pin — a filter
///       submission never carries a page).</item>
/// </list>
/// </summary>
public class M7NewlyPagedTests
{
    // ── F6: announcements list pager ──────────────────────────────────────

    /// <summary>
    /// The announcements-list pager (F6, D1/D5/D9): on page 2 where the
    /// U01 <c>ListVisiblePagedAsync</c> seam reports a further page
    /// (<c>HasMore: true</c>), the <see cref="AnnouncementController.Index"/>
    /// builds a <see cref="PagedViewModel"/> whose <see cref="
    /// PagedViewModel.HasNext"/> mirrors the seam's <c>HasMore</c> (D1) and
    /// whose <see cref="PagedViewModel.HasPrevious"/> is <c>page &gt; 1</c>
    /// (D5); the base URL is the route and there is no filter form (D9) —
    /// the pager's links carry <c>?page=N</c> only.
    /// </summary>
    [Fact]
    public async Task F6_AnnouncementsList_Page2_HasMore_PagerPresent()
    {
        // 30 items, HasMore: true (the page was full — a further page exists).
        const int page = 2;
        var items = Enumerable.Range(0, 30).Select(i => new Announcement
        {
            Id = $"ann-{i:D2}",
            Scope = AnnouncementScope.Public,
            Title = $"Notice {i}",
            Body = "A platform notice.",
            AuthorId = "subj-author-001",
            Created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        }).ToList();

        var announcements = Substitute.For<IAnnouncementService>();
        announcements.ListVisiblePagedAsync(
            null, Arg.Any<IReadOnlySet<string>>(), page, Arg.Any<CancellationToken>())
            .Returns(new AnnouncementPage(Items: items, HasMore: true));

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync("subj-author-001")
            .Returns((Profile?)new() { SubjectId = "subj-author-001", DisplayName = "Admin" });
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());

        var controller = new AnnouncementController(
            announcements, userInfo, DefaultLocalization(), Substitute.For<IDocumentStore>());
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = (await controller.Index(page: page)) as ViewResult;
        Assert.NotNull(result);
        var model = Assert.IsType<AnnouncementIndexViewModel>(result!.ViewData.Model);

        // The pager is present (not the F2 one-page null).
        Assert.NotNull(model.Pager);
        // D1 — HasNext mirrors the seam's HasMore (true).
        Assert.True(model.Pager!.HasNext);
        // D5 — page 2 ⇒ HasPrevious is true.
        Assert.True(model.Pager.HasPrevious);
        // The route + page size.
        Assert.Equal("/announcements", model.Pager.BaseUrl);
        Assert.Equal(page, model.Pager.CurrentPage);
        Assert.Equal(30, model.Pager.PageSize);
        // D9 — no filter form: the links carry ?page=N only.
        Assert.Empty(model.Pager.FilterParams);
    }

    // ── F7: tag-by-tag both-sections pager ─────────────────────────────────

    /// <summary>
    /// The tag-by-tag both-sections pager (F7, D1/D5): on page 1 where
    /// <b>both</b> U01 paged seams (<c>ListPostsByTagPagedAsync</c> +
    /// <c>ListPagesByTagPagedAsync</c>) report a further page
    /// (<c>HasMore: true</c>), the <see cref="TagController.ByTag"/> action
    /// sets <b>both</b> the <c>PagerPosts</c> and <c>PagerPages</c> VM fields
    /// (the two-section route's both-pagers pin — the Groups.Detail
    /// precedent). <see cref="PagedViewModel.HasNext"/> mirrors each seam's
    /// <c>HasMore</c> (D1); <see cref="PagedViewModel.HasPrevious"/> is
    /// <c>page &gt; 1</c> (D5 — false on page 1). No filter form (D9) —
    /// the tag is the route; the links carry <c>?page=N</c> only.
    /// </summary>
    [Fact]
    public async Task F7_TagByTag_Page1_Full_BothSectionsPaged()
    {
        const string slug = "garden";
        const string actorId = "subj-resident-001";
        const int page = 1;

        var posts = Enumerable.Range(0, 30).Select(i => new Post
        {
            Id = $"post-{i:D2}",
            Title = $"Garden post {i}",
            Body = "A note about the garden.",
            AuthorId = "subj-author-001",
            Created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        }).ToList();

        var pages = Enumerable.Range(0, 30).Select(i => new Page
        {
            Id = $"page-{i:D2}",
            Title = $"Garden page {i}",
            Body = "A blog post about the garden.",
            AuthorId = "subj-author-001",
            Kind = PageKind.User,
            Created = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        }).ToList();

        var tags = Substitute.For<ITagService>();
        tags.ListPostsByTagPagedAsync(slug, actorId, page, Arg.Any<CancellationToken>())
            .Returns(new TagPostPage(Items: posts, HasMore: true));
        tags.ListPagesByTagPagedAsync(slug, actorId, page, Arg.Any<CancellationToken>())
            .Returns(new TagPagePage(Items: pages, HasMore: true));
        // No tag in the readable set → tag is null → SeedTranslationFormAsync
        // is not called (no real store needed).
        tags.ListForActorAsync(actorId).Returns(new List<TagItem>());

        var pageService = Substitute.For<IPageService>();
        pageService.GetTreeAsync().Returns(new List<Page>());

        var controller = new TagController(
            tags, pageService, DefaultLocalization(), Substitute.For<IDocumentStore>());
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new List<Claim> { new(KumunitaClaimTypes.Subject, actorId) },
                authenticationType: "test"));

        var result = (await controller.ByTag(slug, page: page)) as ViewResult;
        Assert.NotNull(result);
        var model = Assert.IsType<TagByTagViewModel>(result!.ViewData.Model);

        // Both section pagers are present (the two-section both-pagers pin).
        Assert.NotNull(model.PagerPosts);
        Assert.NotNull(model.PagerPages);

        // D1 — HasNext mirrors each seam's HasMore (true for both).
        Assert.True(model.PagerPosts!.HasNext);
        Assert.True(model.PagerPages!.HasNext);
        // D5 — page 1 ⇒ HasPrevious is false (no "previous" affordance).
        Assert.False(model.PagerPosts.HasPrevious);
        Assert.False(model.PagerPages.HasPrevious);
        // The section-scoped BaseUrl (the tag is the route, D9).
        Assert.Equal($"/tags/{slug}", model.PagerPosts.BaseUrl);
        Assert.Equal($"/tags/{slug}", model.PagerPages.BaseUrl);
        Assert.Equal(page, model.PagerPosts.CurrentPage);
        Assert.Equal(page, model.PagerPages.CurrentPage);
        // D9 — no filter form: the links carry ?page=N only.
        Assert.Empty(model.PagerPosts.FilterParams);
        Assert.Empty(model.PagerPages.FilterParams);
    }

    // ── F4: filter form submits without page (D7 reset pin) ────────────────

    /// <summary>
    /// The D7 reset pin (F4): a filter form submission (the <c>componentId</c>
    /// picker on <c>/events</c>) carries <b>no</b> <c>page</c> param — the
    /// controller's <c>int page = 1</c> default is the floor. The
    /// <see cref="EventController.Index"/> harness drives the real controller
    /// with <c>componentId: "safety"</c> and no explicit <c>page</c>: the
    /// <see cref="IEventService.ListUpcomingAsync"/> seam receives
    /// <c>page: 1</c> (the floor, not a carried page number), and the VM's
    /// <c>Pager.CurrentPage</c> is 1 (the D7 reset — a filter change lands
    /// on page 1, not on the viewer's previous page).
    /// </summary>
    [Fact]
    public async Task F4_FilterForm_SubmitsWithoutPage()
    {
        const string componentId = "safety";
        const string subjectId = "subj-resident-001";

        // The seam reports a full page (HasMore: true) so the pager is
        // present (not the F2 one-page null) — the pin is on CurrentPage.
        var events = Enumerable.Range(0, 30).Select(i => new Event
        {
            Id = $"ev-{i:D2}",
            Title = $"Event {i}",
            Body = "A community event.",
            AuthorId = "subj-author-001",
            ComponentId = componentId,
            Start = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 14, 0, 0, TimeSpan.Zero),
            Location = "Common shed",
            IsDraft = false,
            Created = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        }).ToList();

        var iEvents = Substitute.For<IEventService>();
        // page: 1 is the floor (the default) — the filter submission did not
        // carry a page number (D7).
        iEvents.ListUpcomingAsync(componentId, subjectId, 1, Arg.Any<CancellationToken>())
            .Returns(new EventPage(Items: events, HasMore: true));
        iEvents.ListMineAsync(subjectId, Arg.Any<CancellationToken>())
            .Returns(new List<Event>());

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync("subj-author-001")
            .Returns((Profile?)new() { SubjectId = "subj-author-001", DisplayName = "Ada" });
        userInfo.GetComponentsAsync(true)
            .Returns(new List<Component> { new() { Id = componentId, Name = "Safety" } });

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");

        var timezone = new EffectiveTimezoneResolver(userInfo, localization, new HttpContextAccessor());
        var controller = new EventController(
            iEvents, userInfo, localization, Substitute.For<IDocumentStore>(), timezone);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(
                new ClaimsIdentity(
                    new List<Claim> { new(KumunitaClaimTypes.Subject, subjectId) },
                    authenticationType: "test"));

        // No `page` arg — the controller's `int page = 1` default is the floor.
        var result = (await controller.Index(componentId: componentId)) as ViewResult;
        Assert.NotNull(result);
        var model = Assert.IsType<EventIndexViewModel>(result!.ViewData.Model);

        // D7 — the seam received page: 1 (the floor), not a carried page.
        await iEvents.Received(1).ListUpcomingAsync(
            componentId, subjectId, 1, Arg.Any<CancellationToken>());
        // The VM's pager reflects page 1 (the filter submission landed on
        // page 1, not the viewer's previous page).
        Assert.NotNull(model.Pager);
        Assert.Equal(1, model.Pager!.CurrentPage);
        // D1 — HasNext mirrors the seam's HasMore (true).
        Assert.True(model.Pager.HasNext);
        // D5 — page 1 ⇒ HasPrevious is false.
        Assert.False(model.Pager.HasPrevious);
        // D7 — the community filter rides along (the _Pager carries it).
        Assert.Equal(componentId, model.Pager.FilterParams["componentId"]);
    }

    // ── Local helpers ──────────────────────────────────────────────────────

    private static ILocalizationService DefaultLocalization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");
        return localization;
    }
}
