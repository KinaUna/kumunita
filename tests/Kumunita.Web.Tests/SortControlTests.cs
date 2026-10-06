using System.Collections.Generic;
using System.Linq;
using Kumunita.Core.Posts;
using Kumunita.Core.Query;
using Kumunita.Web.Models;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M26 U10 (design §2.6 pins 1–3) — the 3 shared Web sort-control pins for
/// the community post feed (<c>PostsController.Index</c>, the reference
/// surface U11–U15 copy). <see cref="PostsController"/> is <c>sealed</c> and
/// opens its own <see cref="Marten.IDocumentStore"/> sessions, so — the same
/// wall the <c>M7PagerWiringTests</c> precedent walks around — these pin the
/// <b>exact</b> controller wiring (the <c>HasSortParam</c> gate, the
/// <c>SortKeys.Parse</c> call, the <c>BuildFeedPager</c> expression, the
/// <c>SortViewModel.ForRoute</c> shape) as data-shape assertions over the
/// same <see cref="FeedResult"/> / <see cref="PagedViewModel"/> /
/// <see cref="SortViewModel"/> contracts the action feeds them:
/// <list type="bullet">
/// <item><b>CommunityFeed_SortControl_Renders_AllowedKeys</b> (F9) — the
///       control's <see cref="SortViewModel.Options"/> is the surface's
///       <b>closed</b> allowlist exactly ({created, modified, title}, each
///       with its default direction) — no dead options.</item>
/// <item><b>CommunityFeed_Pager_Carries_Sort_And_Dir</b> (C-SORT·8 / F8) —
///       when the request carried <c>?sort=</c>/<c>?dir=</c>, those pairs
///       join the pager's <see cref="PagedViewModel.FilterParams"/> so
///       prev/next preserve the sort across a <c>HasMore</c> window.</item>
/// <item><b>CommunityFeed_SortParam_DefaultsWhenAbsent</b> (F1) — an absent
///       sort param → a <b>null</b> <see cref="SortSpec"/> (the seam keeps
///       its current order exactly, C-SORT·2) and the pager carries <b>no</b>
///       sort/dir pair (byte-identical to pre-M26).</item>
/// </list>
/// </summary>
public class SortControlTests
{
    // The community post feed's locked allowlist (design §2.2 row 1) — the
    // same set the controller pins in PostFeedAllowedKeys; mirrored here so
    // the data-shape pin is self-contained (the controller is sealed).
    private static readonly IReadOnlySet<string> PostFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title" };
    private const string DefaultKey = "created";
    private const bool DefaultDir = true;

    // The exact controller wiring (PostsController.Index): a sort param is
    // "carried" only when the request specified a non-blank ?sort= key.
    private static bool HasSortParam(string? sort, string? dir)
        => !string.IsNullOrWhiteSpace(sort);

    // The exact controller wiring (PostsController.Index): parse the request's
    // ?sort=/?dir= against the closed allowlist (C-SORT·3) — or null when the
    // viewer chose no sort (C-SORT·2, F1).
    private static SortSpec? ParseFeedSort(string? sort, string? dir)
        => HasSortParam(sort, dir)
            ? SortKeys.Parse(sort, dir, PostFeedAllowed, DefaultKey, DefaultDir)
            : null;

    // The exact controller wiring (PostsController.BuildFeedPager): the
    // one-page no-render null (page 1 + no HasMore) is preserved; the
    // sort/dir pairs join FilterParams only when the request carried them
    // (C-SORT·8). Mirrors the controller's /community/{id} BaseUrl.
    private static PagedViewModel? BuildPager(
        string componentId, int page, bool hasMore, string? sort, string? dir)
    {
        if (page <= 1 && !hasMore)
            return null;

        var filterParams = new Dictionary<string, string>();
        if (HasSortParam(sort, dir))
        {
            filterParams["sort"] = sort!.Trim().ToLowerInvariant();
            filterParams["dir"] = string.IsNullOrWhiteSpace(dir) ? "desc" : dir.Trim().ToLowerInvariant();
        }

        return PagedViewModel.ForRoute(
            $"/community/{componentId}", page, 30, hasMore, filterParams);
    }

    [Fact]
    public void CommunityFeed_SortControl_Renders_AllowedKeys()
    {
        // F9 — the control offers exactly the surface's closed allowlist,
        // each key with its default direction (created desc, modified desc,
        // title asc — design §2.2 row 1). No dead options; the keys the
        // Core switch recognizes are the only ones offered.
        var vm = SortViewModel.ForRoute(
            "/community/community-a",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        // The control's options are a subset of (here equal to) the locked
        // allowlist — never a key the Core switch cannot resolve.
        Assert.All(keys, k => Assert.Contains(k, PostFeedAllowed));
        // Each option's dir matches the surface's default for that key.
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        // The current (resolved) selection echoes back.
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The link set's base is the surface's own route (the /community/{id}
        // feed, not a bare index).
        Assert.Equal("/community/community-a", vm.BaseUrl);
    }

    [Fact]
    public void CommunityFeed_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 / F8 — the request carried ?sort=title&dir=asc; the pager
        // (present: page 2, a further page exists) gains exactly those two
        // pairs in FilterParams so prev/next preserve the sort across the
        // HasMore window. No other pair is added (D9 — no filter form).
        const string componentId = "community-a";
        const int page = 2;
        var feed = new FeedResult(
            Visible: new List<Post> { SamplePost("post-1") },
            HiddenCount: 0,
            Page: page,
            Total: 60,
            HasMore: true);

        var pager = BuildPager(componentId, page, feed.HasMore, sort: "title", dir: "asc");

        Assert.NotNull(pager);
        Assert.True(pager!.HasNext);       // D1 — mirrors the seam's HasMore
        Assert.True(pager.HasPrevious);    // D5 — page 2
        // C-SORT·8 — the two pairs ride along, lowercased as the request's
        // resolved values (the way the M7 filters ride along).
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        // D9 — no other filter pair on this surface: exactly the two.
        Assert.Equal(2, pager.FilterParams.Count);
        // The pager's target is the section's own route.
        Assert.Equal($"/community/{componentId}", pager.BaseUrl);
        // The existing paging contract (C-SORT·8: untouched otherwise).
        Assert.Equal(page, pager.CurrentPage);
        Assert.Equal(30, pager.PageSize);
    }

    [Fact]
    public void CommunityFeed_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — the request carried no ?sort= (and no ?dir=): the
        // seam sees a null SortSpec (the current hardcoded order exactly) and
        // the pager carries no sort/dir pair (byte-identical to pre-M26,
        // D9 — ?page=N only).
        const string componentId = "community-a";
        const int page = 2;
        var feed = new FeedResult(
            Visible: new List<Post> { SamplePost("post-1") },
            HiddenCount: 0,
            Page: page,
            Total: 60,
            HasMore: true);

        var sortSpec = ParseFeedSort(sort: null, dir: null);
        Assert.Null(sortSpec); // F1 — the seam's default-order case

        var pager = BuildPager(componentId, page, feed.HasMore, sort: null, dir: null);
        Assert.NotNull(pager);
        Assert.True(pager!.HasNext);
        // C-SORT·2 — an unsorted read's pager links stay byte-identical: no
        // sort/dir pair (the empty FilterParams the M7 D9 pin expects).
        Assert.Empty(pager.FilterParams);

        // A ?dir= alone (no key) is not a sort choice either — the F1 shape:
        // the seam sees null, the pager carries nothing.
        Assert.Null(ParseFeedSort(sort: null, dir: "desc"));
        Assert.Empty(BuildPager(componentId, page, feed.HasMore, sort: null, dir: "desc")!.FilterParams);
    }

    private static Post SamplePost(string id) =>
        new()
        {
            Id = id,
            Title = "Community board update",
            Body = "A note for the neighborhood.",
            AuthorId = "subj-author-001",
        };
}
