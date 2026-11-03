using Kumunita.Core.Kiosk;
using Kumunita.Core.Search;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Services;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0285 — the kiosk playlist item picker's catalog. The catalog adds no
/// access logic: these pins check it routes each item type to the right M8
/// search surface + scope (browse for a blank query, search otherwise), merges
/// the two event scopes, labels group items with the actor's own group name,
/// and lists the enabled components for a calendar.
/// </summary>
public class KioskItemCatalogTests
{
    private const string Actor = "u-285-actor";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(KioskItemType.Board, SearchService.BoardsSurface, SearchScope.Community)]
    [InlineData(KioskItemType.Todo, SearchService.TodosSurface, SearchScope.Community)]
    [InlineData(KioskItemType.Project, SearchService.ProjectsSurface, SearchScope.Community)]
    [InlineData(KioskItemType.Page, SearchService.PagesSurface, SearchScope.Community)]
    [InlineData(KioskItemType.Announcement, SearchService.AnnouncementsSurface, SearchScope.Community)]
    [InlineData(KioskItemType.CommunityPost, SearchService.PostsSurface, SearchScope.Community)]
    [InlineData(KioskItemType.GroupPost, SearchService.PostsSurface, SearchScope.Groups)]
    public async Task BlankQuery_Browses_TheTypesSurface(KioskItemType type, string surface, SearchScope scope)
    {
        var (catalog, search, _) = Build();
        search.BrowseSurfaceAsync(surface, scope, Actor, IKioskItemCatalog.MaxResults, Arg.Any<CancellationToken>())
            .Returns(Hits(Hit("a", "Alpha", DaysAgo(1))));

        var items = await catalog.SearchAsync(type, "  ", Actor, Ct);

        Assert.Equal("a", Assert.Single(items).Id);
        await search.DidNotReceiveWithAnyArgs().SearchSurfaceAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Query_Searches_WithTheResultCap()
    {
        var (catalog, search, _) = Build();
        search.SearchSurfaceAsync(SearchService.TodosSurface, "bins", SearchScope.Community, Actor, 1,
                Arg.Any<CancellationToken>(), null, IKioskItemCatalog.MaxResults)
            .Returns(new SearchSurfacePage(SearchService.TodosSurface, [Hit("t1", "Put the bins out", DaysAgo(2))], 1, false));

        var items = await catalog.SearchAsync(KioskItemType.Todo, " bins ", Actor, Ct);

        Assert.Equal("Put the bins out", Assert.Single(items).Title);
        await search.DidNotReceiveWithAnyArgs().BrowseSurfaceAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task Event_Merges_CommunityAndGroupScopes_NewestFirst_WithGroupName()
    {
        var (catalog, search, userInfo) = Build();
        search.BrowseSurfaceAsync(SearchService.EventsSurface, SearchScope.Community, Actor, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Hits(Hit("e-old", "Street party", DaysAgo(5))));
        search.BrowseSurfaceAsync(SearchService.EventsSurface, SearchScope.Groups, Actor, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Hits(Hit("e-new", "Choir practice", DaysAgo(1), groupId: "g-1")));
        userInfo.GetGroupsForUserAsync(Actor).Returns(Task.FromResult<IReadOnlyList<Group>>([new Group { Id = "g-1", Name = "Choir" }]));

        var items = await catalog.SearchAsync(KioskItemType.Event, null, Actor, Ct);

        Assert.Equal(["e-new", "e-old"], items.Select(i => i.Id).ToArray());
        Assert.Equal("g-1", items[0].GroupId);
        Assert.Equal("Choir", items[0].Subtitle);
        Assert.Null(items[1].GroupId);
    }

    [Fact]
    public async Task UntitledPost_UsesTheBodyExcerpt_OnOneLine()
    {
        var (catalog, search, _) = Build();
        search.BrowseSurfaceAsync(SearchService.PostsSurface, SearchScope.Community, Actor, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Hits(new SearchHit("p1", SearchService.PostsSurface, null, null, "Lost cat\n  near the park", DaysAgo(1))));

        var items = await catalog.SearchAsync(KioskItemType.CommunityPost, "", Actor, Ct);

        Assert.Equal("Lost cat near the park", Assert.Single(items).Title);
    }

    [Fact]
    public async Task Calendar_Lists_EnabledComponents_FilteredByName()
    {
        var (catalog, search, userInfo) = Build();
        userInfo.GetComponentsAsync(true).Returns(Task.FromResult<IReadOnlyList<Component>>(
        [
            new Component { Id = "c-2", Name = "Garden club", SortOrder = 2 },
            new Component { Id = "c-1", Name = "Main street", SortOrder = 1 },
        ]));

        var all = await catalog.SearchAsync(KioskItemType.Calendar, "", Actor, Ct);
        var filtered = await catalog.SearchAsync(KioskItemType.Calendar, "garden", Actor, Ct);

        Assert.Equal(["c-1", "c-2"], all.Select(i => i.Id).ToArray());
        Assert.Equal("c-2", Assert.Single(filtered).Id);
        await search.DidNotReceiveWithAnyArgs().BrowseSurfaceAsync(default!, default, default!, default);
    }

    [Fact]
    public async Task BlankActor_ReturnsNothing()
    {
        var (catalog, search, _) = Build();

        var items = await catalog.SearchAsync(KioskItemType.Board, "", "", Ct);

        Assert.Empty(items);
        await search.DidNotReceiveWithAnyArgs().BrowseSurfaceAsync(default!, default, default!, default);
    }

    private static (KioskItemCatalog catalog, ISearchService search, IUserInfoService userInfo) Build()
    {
        var search = Substitute.For<ISearchService>();
        var userInfo = Substitute.For<IUserInfoService>();
        return (new KioskItemCatalog(search, userInfo), search, userInfo);
    }

    private static SearchHit Hit(string id, string title, DateTimeOffset created, string? groupId = null)
        => new(id, "x", groupId, title, null, created);

    private static Task<IReadOnlyList<SearchHit>> Hits(params SearchHit[] hits)
        => Task.FromResult<IReadOnlyList<SearchHit>>(hits);

    private static DateTimeOffset DaysAgo(int days) => DateTimeOffset.UtcNow.AddDays(-days);
}
