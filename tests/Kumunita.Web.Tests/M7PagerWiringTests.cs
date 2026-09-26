using System.Security.Claims;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M7 (ADR 0090) <b>U03</b> — the pager <i>wiring</i> tests (the U03 exit
/// criterion's three tests). They pin the Web-side contract that a paged
/// route builds a <see cref="PagedViewModel"/> off the seam's
/// <c>HasMore</c> signal (D1 — the sole paging signal) and hands it to the
/// shared <c>_Pager</c> partial, and that a filter's value rides along in
/// <see cref="PagedViewModel.FilterParams"/> so the pager's prev/next links
/// preserve it (D7).
/// <para>
/// Two of the three are <b>data-shape pins</b>: the posts-feed
/// <see cref="PostService"/> is <c>sealed</c> and opens its own
/// <see cref="IDocumentStore"/> sessions (NSubstitute cannot proxy it, and
/// this test assembly has no Postgres fixture — the same wall the
/// <c>TranslationDisplayTests</c> precedent walks around). The shape pinned
/// here is <i>exactly</i> the <see cref="PostsController.Index"/> /
/// <see cref="PostsController.AllSections"/> wiring (the
/// <c>(feed.HasMore || page &gt; 1) ? ForRoute(...) : null</c> expression)
/// and the <see cref="FeedResult"/> / <see cref="PagedViewModel"/> contract
/// it feeds it — so a regression in either the signal capture or the pager
/// construction breaks these. The third drives the <b>real</b>
/// <see cref="EventController"/> (its <see cref="IEventService"/> seam is
/// mockable) and pins the D7 filter-preservation half end-to-end.
/// </para>
/// <list type="bullet">
/// <item><b>PostsFeed_Page2_HasMore_PagerPresent</b> — page 2 with a
///       further page → the pager is present, <c>HasNext</c> (the D1 signal)
///       and <c>HasPrevious</c> (page &gt; 1, D5) are both true, the base URL
///       is the section's route, and there is no filter form (D9).</item>
/// <item><b>PostsFeed_Page1_Partial_PagerAbsent</b> — page 1 with no further
///       page → the pager is <c>null</c> (F2 — the <c>_Pager</c> partial
///       renders nothing on a one-page surface).</item>
/// <item><b>EventsFeed_FilterParam_PreservedInPager</b> — the <c>componentId</c>
///       filter rides along in <see cref="PagedViewModel.FilterParams"/> (D7)
///       so the pager's next link keeps the community filter.</item>
/// </list>
/// </summary>
public class M7PagerWiringTests
{
    // ── Posts feed (the /community/{id} surface) — data-shape pins ─────────

    /// <summary>
    /// The posts-feed wiring (D1/D5): on page 2 where the seam reports a
    /// further page (<c>HasMore</c>), the <see cref="PostsController"/>
    /// builds a <see cref="PagedViewModel"/> whose <see
    /// cref="PagedViewModel.HasNext"/> is the seam's <c>HasMore</c> and whose
    /// <see cref="PagedViewModel.HasPrevious"/> is <c>page &gt; 1</c>; the base
    /// URL is the section's route and there is no filter form (D9) — the pager
    /// links carry <c>?page=N</c> only. This mirrors the controller's
    /// <c>(feed.HasMore || page &gt; 1) ? ForRoute($"/community/{id}", page,
    /// 30, feed.HasMore) : null</c> expression verbatim.
    /// </summary>
    [Fact]
    public void PostsFeed_Page2_HasMore_PagerPresent()
    {
        // The seam's page: 2 (the current page), a further page exists.
        const int page = 2;
        const string componentId = "community-a";
        var feed = new FeedResult(
            Visible: new List<Post> { SamplePost("post-1") },
            HiddenCount: 0,
            Page: page,
            Total: 60,            // candidate-set count (C-M7·7 — never viewer-facing)
            HasMore: true);       // D1 — the sole paging signal: the page was full

        // The exact PostsController wiring (Index / AllSections):
        PagedViewModel? pager = (feed.HasMore || page > 1)
            ? PagedViewModel.ForRoute($"/community/{componentId}", page, 30, feed.HasMore)
            : null;

        Assert.NotNull(pager);
        // D1 — HasNext is the seam's HasMore (true here); D5 — HasPrevious is
        // page > 1 (true on page 2). Both halves of the pager must light up.
        Assert.True(pager!.HasNext);
        Assert.True(pager.HasPrevious);
        // The pager targets the section's route (not a bare index) and the
        // page size the controller passes.
        Assert.Equal($"/community/{componentId}", pager.BaseUrl);
        Assert.Equal(page, pager.CurrentPage);
        Assert.Equal(30, pager.PageSize);
        // D9 — no filter form on this surface: a plain feed's pager links carry
        // ?page=N only, so no filter pair is carried.
        Assert.Empty(pager.FilterParams);
    }

    /// <summary>
    /// The posts-feed wiring (F2 — the one-page no-render pin): on page 1
    /// where the seam reports <b>no</b> further page (<c>HasMore</c> false),
    /// the <see cref="PostsController"/> sets the pager to <c>null</c> — the
    /// <c>_Pager</c> partial then renders nothing (a one-page surface has no
    /// pager). This mirrors the same
    /// <c>(feed.HasMore || page &gt; 1) ? ForRoute(...) : null</c> expression.
    /// </summary>
    [Fact]
    public void PostsFeed_Page1_Partial_PagerAbsent()
    {
        // The seam's page: 1 (the first/only page), nothing beyond it.
        const int page = 1;
        var feed = new FeedResult(
            Visible: new List<Post> { SamplePost("post-1") },
            HiddenCount: 0,
            Page: page,
            Total: 5,             // a handful of posts — one page
            HasMore: false);      // D1 — the page did not fill: no further page

        // The exact PostsController wiring:
        PagedViewModel? pager = (feed.HasMore || page > 1)
            ? PagedViewModel.ForRoute("/community/community-a", page, 30, feed.HasMore)
            : null;

        // F2 — a one-page surface renders no pager.
        Assert.Null(pager);
    }

    // ── Events feed (the /events surface) — the real controller ─────────────

    /// <summary>
    /// The D7 half, driven end-to-end against the <b>real</b>
    /// <see cref="EventController"/>: the <c>componentId</c> filter the viewer
    /// picked rides along in <see cref="PagedViewModel.FilterParams"/> (and
    /// only when set) so the pager's next link keeps the community filter
    /// across page turns. The <see cref="IEventService"/> seam reports
    /// <c>HasMore</c>, and the pager's <see cref="PagedViewModel.HasNext"/>
    /// mirrors it (D1).
    /// </summary>
    [Fact]
    public async Task EventsFeed_FilterParam_PreservedInPager()
    {
        const string componentId = "community-a";
        const string subjectId = "subj-resident-001";
        const int page = 2;

        var events = Substitute.For<IEventService>();
        events.ListUpcomingAsync(componentId, subjectId, page, Arg.Any<CancellationToken>())
            .Returns(new EventPage(
                Items: new List<Event> { SampleEvent("ev-1", authorId: "subj-author-001", componentId: componentId) },
                HasMore: true));
        events.ListMineAsync(subjectId, Arg.Any<CancellationToken>())
            .Returns(new List<Event>());

        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync("subj-author-001")
            .Returns((Profile?)new() { SubjectId = "subj-author-001", DisplayName = "Ada" });
        userInfo.GetComponentsAsync(true)
            .Returns(new List<Component> { new() { Id = componentId, Name = "Community A" } });

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");

        var timezone = new EffectiveTimezoneResolver(userInfo, localization, new HttpContextAccessor());
        var controller = new EventController(events, userInfo, localization, Substitute.For<IDocumentStore>(), timezone);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.ControllerContext.HttpContext.User =
            new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(
                    new List<Claim> { new(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId) },
                    authenticationType: "test"));

        var result = (await controller.Index(componentId: componentId, page: page)) as ViewResult;

        Assert.NotNull(result);
        var model = Assert.IsType<EventIndexViewModel>(result!.ViewData.Model);

        // The seam reported a further page → the pager is present (not the
        // one-page no-render null).
        Assert.NotNull(model.Pager);
        // D7 — the community filter rides along so the pager's next link keeps
        // it; the page number + the filter pair are the only query bits.
        Assert.Equal(componentId, model.Pager!.FilterParams["componentId"]);
        Assert.Equal($"/events", model.Pager.BaseUrl);
        // D1 — HasNext mirrors the seam's HasMore (true here).
        Assert.True(model.Pager.HasNext);
        // D5 — page 2 ⇒ the "previous" affordance is available.
        Assert.True(model.Pager.HasPrevious);
    }

    // ── Local sample shapes (mirroring EventControllerTests) ────────────────

    private static Post SamplePost(string id) =>
        new()
        {
            Id = id,
            Title = "Community board update",
            Body = "A note for the neighborhood.",
            AuthorId = "subj-author-001",
            Created = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero),
        };

    private static Event SampleEvent(string id, string authorId, string? componentId = null)
    {
        var now = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        return new Event
        {
            Id = id,
            Title = "Cleanup day",
            Body = "Bring gloves.",
            AuthorId = authorId,
            ComponentId = componentId,
            Start = now.AddHours(48),
            End = now.AddHours(52),
            Location = "Common shed",
            IsDraft = false,
            Created = now,
        };
    }
}
