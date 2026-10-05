using System.Collections.Generic;
using System.Linq;
using Kumunita.Core.Query;
using Kumunita.Web.Models;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M26 U11 (design §2.6, the <c>AllFeed_*</c> / <c>GroupPostFeed_*</c> /
/// <c>Event*_*</c> pins) — the Web sort-control pins for the **remaining**
/// post-feed surfaces (the all-sections feed, the group posts feed) and the
/// **events** surfaces (the upcoming + past list, the group events feed).
/// Same harness as <see cref="SortControlTests"/> (U10): the controllers are
/// <c>sealed</c> and open their own <see cref="Marten.IDocumentStore"/>
/// sessions, so these pin the <b>exact</b> controller wiring (the
/// <c>SortKeys.Parse</c> call, the <see cref="SortViewModel"/> shape, the
/// pager-carry rule) as data-shape assertions over the same Core /
/// <see cref="PagedViewModel"/> / <see cref="SortViewModel"/> contracts the
/// actions feed them.
/// </summary>
public class PostEventSortWebTests
{
    // ── Allowlists (U2 §2.2) — each surface's own closed set (C-SORT·1/F9) ──

    // The post-feed rows 1–3 (community / all-sections / group posts):
    // created desc default, modified, title (OrdinalIgnoreCase).
    private static readonly IReadOnlySet<string> PostFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "modified", "title" };
    private const string PostFeedDefaultKey = "created";
    private const bool PostFeedDefaultDir = true;

    // The event-feed rows 4–6 (upcoming / past / group events):
    // start (default, direction differs per sub-surface), created, title.
    // The group events surface (row 6) is <c>start</c> + <c>created</c> only.
    private static readonly IReadOnlySet<string> EventFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "start", "created", "title" };
    private static readonly IReadOnlySet<string> GroupEventFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "start", "created" };
    private const string EventFeedDefaultKey = "start";

    // ── The exact controller wiring (reused, not forked) ────────────────────
    // The shared pager-carry rule (C-SORT·8): the sort/dir pairs for a
    // surface's FilterParams — only when the request carried a non-blank
    // ?sort= key (C-SORT·3). Mirrors SortViewModel.SortFilterParams exactly.
    private static IReadOnlyDictionary<string, string> SortFilterParams(string? sort, string? dir)
        => string.IsNullOrWhiteSpace(sort)
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>
            {
                ["sort"] = sort.Trim().ToLowerInvariant(),
                ["dir"] = string.IsNullOrWhiteSpace(dir) ? "desc" : dir.Trim().ToLowerInvariant(),
            };

    // The exact controller wiring: parse the request's ?sort=/?dir= against
    // the surface's closed allowlist (C-SORT·3) — or null when the viewer
    // chose no sort (C-SORT·2, F1). Mirrors SortKeys.Parse's call shape.
    private static SortSpec? ParseFeedSort(
        string? sort, string? dir,
        IReadOnlySet<string> allowed, string defaultKey, bool defaultDir)
        => string.IsNullOrWhiteSpace(sort)
            ? null
            : SortKeys.Parse(sort, dir, allowed, defaultKey, defaultDir);

    // ── AllFeed (the all-sections feed, PostsController.AllSections) ───────

    [Fact]
    public void AllFeed_SortControl_Renders_AllowedKeys()
    {
        // F9 — the all-sections feed offers exactly the post-feed allowlist
        // (created desc, modified desc, title asc — the U10 Index surface's
        // row 1 set, design §2.2 row 2), no dead options.
        var vm = SortViewModel.ForRoute(
            "/community",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, PostFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        // The link set's base is the all-sections feed's own route (the
        // bare /community, not /community/{id}).
        Assert.Equal("/community", vm.BaseUrl);
    }

    [Fact]
    public void AllFeed_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=title&dir=asc; the pager
        // (present: page 2, a further page exists) gains exactly those two
        // pairs in FilterParams so prev/next preserve the sort across the
        // HasMore window. D9 — no other pair is added on this surface.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "title", dir: "asc");
        Assert.Equal("title", sortFilterParams["sort"]);
        Assert.Equal("asc", sortFilterParams["dir"]);

        var pager = PagedViewModel.ForRoute("/community", page, 30, hasMore: true, sortFilterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal(2, pager.FilterParams.Count);
        Assert.Equal("/community", pager.BaseUrl);
    }

    [Fact]
    public void AllFeed_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager carries
        // no sort/dir pair (byte-identical to pre-M26, D9 — ?page=N only).
        const int page = 2;

        var sortSpec = ParseFeedSort(sort: null, dir: null,
            PostFeedAllowed, PostFeedDefaultKey, PostFeedDefaultDir);
        Assert.Null(sortSpec);

        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseFeedSort(sort: null, dir: "desc",
            PostFeedAllowed, PostFeedDefaultKey, PostFeedDefaultDir));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    // ── GroupPostFeed (the group posts feed, GroupsController.Detail) ─────

    [Fact]
    public void GroupPostFeed_SortControl_Renders_AllowedKeys()
    {
        // F9 — the group posts section offers exactly the post-feed
        // allowlist (created desc, modified desc, title asc — the U10
        // reference's row 1 set, design §2.2 row 3), no dead options.
        var vm = SortViewModel.ForRoute(
            "/groups/group-a",
            currentKey: "modified",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("modified", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "modified", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, PostFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "modified").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        // The current (resolved) selection echoes back.
        Assert.Equal("modified", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The link set's base is the group's own route (the group detail
        // page hosts the group posts section).
        Assert.Equal("/groups/group-a", vm.BaseUrl);
    }

    [Fact]
    public void GroupPostFeed_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=title&dir=asc; the group
        // posts section's pager gains exactly those two pairs in
        // FilterParams (so its prev/next preserve the sort). D9 — the group
        // is the route (no filter form), so exactly the two pairs.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "title", dir: "asc");

        var pager = PagedViewModel.ForRoute("/groups/group-a", page, 30, hasMore: true, sortFilterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal(2, pager.FilterParams.Count);
        Assert.Equal("/groups/group-a", pager.BaseUrl);
    }

    [Fact]
    public void GroupPostFeed_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the group
        // posts section keeps its pinned Created-desc order exactly) and the
        // section's pager carries no sort/dir pair (byte-identical to
        // pre-M26, D9 — ?page=N only).
        const int page = 2;

        var sortSpec = ParseFeedSort(sort: null, dir: null,
            PostFeedAllowed, PostFeedDefaultKey, PostFeedDefaultDir);
        Assert.Null(sortSpec);

        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseFeedSort(sort: null, dir: "desc",
            PostFeedAllowed, PostFeedDefaultKey, PostFeedDefaultDir));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    // ── EventUpcoming (the upcoming events feed, EventController.Index) ────

    [Fact]
    public void EventUpcoming_SortControl_Renders_AllowedKeys()
    {
        // F9 — the upcoming events feed offers exactly the event-feed
        // allowlist (design §2.2 row 4: start / created / title, all
        // non-null in the Event model), no dead options. The per-sub-surface
        // default direction is start <b>asc</b> (the U5 rule).
        var vm = SortViewModel.ForRoute(
            "/events",
            currentKey: "start",
            currentDir: "asc",
            options: [
                ("start", "asc"),
                ("created", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "start", "created", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, EventFeedAllowed));
        // The surface default: start is offered in its default asc direction.
        Assert.Equal("asc", vm.Options.First(o => o.Key == "start").Dir);
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        Assert.Equal("/events", vm.BaseUrl);
    }

    [Fact]
    public void EventUpcoming_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=title&dir=asc; the upcoming
        // feed's pager gains exactly those two pairs (so its prev/next
        // preserve the sort). ADR 0090 D7 / ADR 0109 — the existing
        // componentId filter + past selector are still carried when present.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "title", dir: "asc");

        // The exact controller wiring: the sort/dir pairs are merged into the
        // surface's existing pager Filters (componentId / past) — not replaced.
        var pagerFilters = new Dictionary<string, string> { ["past"] = "false" };
        foreach (var (k, v) in sortFilterParams)
            pagerFilters[k] = v;

        var pager = PagedViewModel.ForRoute("/events", page, 30, hasMore: true, pagerFilters);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal("/events", pager.BaseUrl);
    }

    [Fact]
    public void EventUpcoming_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the upcoming
        // feed keeps its pinned Start-asc order exactly) and the pager
        // carries no sort/dir pair (byte-identical to pre-M26). The per-sub-
        // surface default key is start (the U5 rule); the default dir is asc.
        var sortSpec = ParseFeedSort(sort: null, dir: null,
            EventFeedAllowed, EventFeedDefaultKey, defaultDir: false);
        Assert.Null(sortSpec);

        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseFeedSort(sort: null, dir: "desc",
            EventFeedAllowed, EventFeedDefaultKey, defaultDir: false));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    // ── EventPast (the past events feed, EventController.Index) ────────────

    [Fact]
    public void EventPast_SortControl_Renders_AllowedKeys()
    {
        // F9 — the past events feed offers the same closed event-feed
        // allowlist (design §2.2 row 5: start / created / title), no dead
        // options. The per-sub-surface default direction is start
        // <b>desc</b> (the U5 rule — the "history" reading order).
        var vm = SortViewModel.ForRoute(
            "/events",
            currentKey: "start",
            currentDir: "desc",
            options: [
                ("start", "desc"),
                ("created", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "start", "created", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, EventFeedAllowed));
        // The surface default: start is offered in its default desc direction.
        Assert.Equal("desc", vm.Options.First(o => o.Key == "start").Dir);
        Assert.Equal("/events", vm.BaseUrl);
    }

    [Fact]
    public void EventPast_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=created&dir=desc; the past
        // feed's pager gains exactly those two pairs (so its prev/next
        // preserve the sort). ADR 0109 — the past=true selector is carried
        // too, so prev/next stay on the Past view.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "created", dir: "desc");

        // The exact controller wiring: the sort/dir pairs are merged into the
        // surface's existing pager Filters (past) — not replaced.
        var pagerFilters = new Dictionary<string, string> { ["past"] = "true" };
        foreach (var (k, v) in sortFilterParams)
            pagerFilters[k] = v;

        var pager = PagedViewModel.ForRoute("/events", page, 30, hasMore: true, pagerFilters);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("created", pager.FilterParams["sort"]);
        Assert.Equal("desc", pager.FilterParams["dir"]);
        Assert.Equal("true", pager.FilterParams["past"]);
        Assert.Equal("/events", pager.BaseUrl);
    }

    [Fact]
    public void EventPast_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the past
        // feed keeps its pinned Start-desc order exactly) and the pager
        // carries no sort/dir pair (byte-identical to pre-M26). The per-sub-
        // surface default key is start (the U5 rule); the default dir is desc.
        var sortSpec = ParseFeedSort(sort: null, dir: null,
            EventFeedAllowed, EventFeedDefaultKey, defaultDir: true);
        Assert.Null(sortSpec);

        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseFeedSort(sort: null, dir: "asc",
            EventFeedAllowed, EventFeedDefaultKey, defaultDir: true));
        Assert.Empty(SortFilterParams(sort: null, dir: "asc"));
    }

    // ── EventGroup (the group events feed, GroupsController.Detail) ────────

    [Fact]
    public void EventGroup_SortControl_Renders_AllowedKeys()
    {
        // F9 — the group events section offers exactly its **own** closed
        // allowlist (design §2.2 row 6: start / created — <b>no</b> title,
        // that key is not in the group events allowlist), no dead options.
        // The per-sub-surface default direction is start <b>asc</b> (the U5
        // rule — the same as the upcoming feed).
        var vm = SortViewModel.ForRoute(
            "/groups/group-a",
            currentKey: "start",
            currentDir: "asc",
            options: [
                ("start", "asc"),
                ("created", "desc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "start", "created" }, keys);
        Assert.All(keys, k => Assert.Contains(k, GroupEventFeedAllowed));
        // title is NOT offered (F9 — no dead options on this section).
        Assert.DoesNotContain("title", keys);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "start").Dir);
        Assert.Equal("/groups/group-a", vm.BaseUrl);
    }

    [Fact]
    public void EventGroup_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=created&dir=asc; the group
        // events section's pager gains exactly those two pairs (so its
        // prev/next preserve the sort). D9 — the group is the route (no
        // filter form), so exactly the two pairs.
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "created", dir: "asc");

        var pager = PagedViewModel.ForRoute("/groups/group-a", page, 30, hasMore: true, sortFilterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        Assert.Equal("created", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal(2, pager.FilterParams.Count);
        Assert.Equal("/groups/group-a", pager.BaseUrl);
    }

    [Fact]
    public void EventGroup_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the group
        // events section keeps its pinned Start-asc order exactly) and the
        // section's pager carries no sort/dir pair (byte-identical to
        // pre-M26, D9 — ?page=N only).
        var sortSpec = ParseFeedSort(sort: null, dir: null,
            GroupEventFeedAllowed, "start", defaultDir: false);
        Assert.Null(sortSpec);

        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseFeedSort(sort: null, dir: "desc",
            GroupEventFeedAllowed, "start", defaultDir: false));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));
    }

    // C-SORT·1 cross-surface fallback (pin, not a 16th test): a ?sort= key
    // outside the group events allowlist (e.g. title — valid on the event
    // feed, NOT on the group events section) falls back to that surface's
    // default order (start, its default dir) without error — SortKeys.Parse's
    // out-of-allowlist rule is what keeps the group events section from
    // inventing a `title` sort the section doesn't offer. The U10 reference
    // pins this on the community feed (`CommunityFeed_SortParam_DefaultsWhenAbsent`);
    // the same `SortKeys.Parse` call the group events controller uses
    // (`SortKeys.Parse(sort, dir, GroupEventFeedAllowedKeys, "start", defaultDir: false)`)
    // carries the same guarantee here.
}
