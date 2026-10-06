using System.Collections.Generic;
using System.Linq;
using Kumunita.Core.Query;
using Kumunita.Web.Models;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M26 U15 (design §2.6, the <c>Search_*</c> Web pins) — the Web
/// sort-control pins for the **search** surface
/// (<c>/search?surface=&lt;one&gt;</c>, <see cref="Kumunita.Web.Controllers.SearchController.Index"/>).
/// Same harness as <see cref="SortControlTests"/> (U10) /
/// <see cref="PostEventSortWebTests"/> (U11) /
/// <see cref="ProjectSortWebTests"/> (U12) /
/// <see cref="MiscListSortWebTests"/> (U13) /
/// <see cref="TagPeopleSortWebTests"/> (U14): the controllers are
/// <c>sealed</c> and open their own <see cref="Marten.IDocumentStore"/>
/// sessions, so these pin the <b>exact</b> controller wiring (the
/// <c>ParseSort</c> call, the <see cref="SortViewModel"/> shape, the
/// pager-carry rule) as data-shape assertions over the same Core /
/// <see cref="PagedViewModel"/> / <see cref="SortViewModel"/> contracts
/// the action feeds them.
/// <para>
/// **Relevance is NOT a sort key** (M8 frozen — the named non-decision):
/// the closed allowlist is <c>created</c> + <c>title</c> only (U2 §2.2
/// row 18, the U9-locked Core switch — <c>name</c> is **not** a Core key
/// for this seam, so it is not offered either — see the U15 drift pause).
/// </para>
/// </summary>
public class SearchSortWebTests
{
    // ── Allowlist (U2 §2.2 row 18) — the search surface's own closed set
    //    (C-SORT·1/F9): created desc + title asc — the **exact** set the
    //    Core's `SearchService.SearchSurfaceAsync` switch resolves. ──
    private static readonly IReadOnlySet<string> SearchFeedAllowed =
        new HashSet<string>(StringComparer.Ordinal) { "created", "title" };

    private const string DefaultKey = "created";
    private const bool DefaultDir = true;

    // ── The exact controller wiring (SearchController) ───────────────────
    // M26 U15 (C-SORT·3) — the sort param is "carried" only when the
    // request actually specified a non-blank ?sort= key (?dir= alone is
    // not a sort choice). Mirrors the controller's HasSortParam exactly.
    private static bool HasSortParam(string? sort)
        => !string.IsNullOrWhiteSpace(sort);

    // M26 U15 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // search surface's closed allowlist — or null when the viewer chose
    // no sort (C-SORT·2, F1). Mirrors the controller's ParseSort exactly.
    private static SortSpec? ParseSort(
        string? sort, string? dir, IReadOnlySet<string> allowedKeys)
        => HasSortParam(sort)
            ? SortKeys.Parse(sort, dir, allowedKeys, DefaultKey, DefaultDir)
            : null;

    // The shared pager-carry rule (C-SORT·8): the sort/dir pairs for a
    // surface's FilterParams — only when the request carried a non-blank
    // ?sort= key (C-SORT·3). Reuses U11's SortViewModel.SortFilterParams
    // helper (the canonical normalization), not a re-derivation.
    private static IReadOnlyDictionary<string, string> SortFilterParams(string? sort, string? dir)
        => SortViewModel.SortFilterParams(sort, dir);

    [Fact]
    public void Search_SortControl_Renders_AllowedKeys()
    {
        // F9 — the search surface (row 18) offers exactly the
        // created/title allowlist, no dead options — in particular **no**
        // relevance key and **no** name key (the Core's switch does not
        // resolve either — the drift pause).
        var vm = SortViewModel.ForRoute(
            "/search",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("title", "asc"),
            ]);

        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.Equal(new[] { "created", "title" }, keys);
        Assert.All(keys, k => Assert.Contains(k, SearchFeedAllowed));
        Assert.Equal("desc", vm.Options.First(o => o.Key == "created").Dir);
        Assert.Equal("asc", vm.Options.First(o => o.Key == "title").Dir);
        // The Core-unresolved keys are **not** offered (F9).
        Assert.DoesNotContain("name", SearchFeedAllowed);
        Assert.DoesNotContain("relevance", SearchFeedAllowed);
        // The current (resolved) selection echoes back.
        Assert.Equal("created", vm.CurrentKey);
        Assert.Equal("desc", vm.CurrentDir);
        // The link set's base is the search surface's own route.
        Assert.Equal("/search", vm.BaseUrl);
    }

    [Fact]
    public void Search_Pager_Carries_Sort_And_Dir()
    {
        // C-SORT·8 — the request carried ?sort=title&dir=asc; the pager
        // (present: page 2, a further page exists) gains exactly those two
        // pairs alongside the frozen q + surface + scope filter pairs
        // (C-SORT·4 — the frozen filter is carried, not dropped).
        const int page = 2;
        var sortFilterParams = SortFilterParams(sort: "title", dir: "asc");
        Assert.Equal("title", sortFilterParams["sort"]);
        Assert.Equal("asc", sortFilterParams["dir"]);

        // The FilterParams shape is the frozen q/surface/scope pairs (the
        // ADR 0090 D7 filter-preservation rule the pre-M26 pager carried)
        // + the sort/dir pairs, joined the way the controller does.
        var filterParams = new Dictionary<string, string>
        {
            ["q"] = "bench",
            ["surface"] = "posts",
            ["scope"] = "community",
        };
        foreach (var (k, v) in SortFilterParams("title", "asc"))
            filterParams[k] = v;

        var pager = PagedViewModel.ForRoute(
            "/search", page, 20, hasMore: true, filterParams);
        Assert.NotNull(pager);
        Assert.True(pager.HasNext);
        Assert.True(pager.HasPrevious);
        // The frozen filter pairs are still there (C-SORT·4).
        Assert.Equal("bench", pager.FilterParams["q"]);
        Assert.Equal("posts", pager.FilterParams["surface"]);
        Assert.Equal("community", pager.FilterParams["scope"]);
        // The sort/dir pairs are carried.
        Assert.Equal("title", pager.FilterParams["sort"]);
        Assert.Equal("asc", pager.FilterParams["dir"]);
        Assert.Equal(5, pager.FilterParams.Count);
        Assert.Equal("/search", pager.BaseUrl);
    }

    [Fact]
    public void Search_SortParam_DefaultsWhenAbsent()
    {
        // F1 / C-SORT·2 — an absent sort param → null SortSpec (the seam
        // keeps its pinned Created-desc order exactly) and the pager
        // carries no sort/dir pair (byte-identical to pre-M26 — the frozen
        // q/surface/scope pairs only).
        Assert.Null(ParseSort(sort: null, dir: null, SearchFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: null));
        Assert.Null(ParseSort(sort: null, dir: "desc", SearchFeedAllowed));
        Assert.Empty(SortFilterParams(sort: null, dir: "desc"));

        // A ?sort= key outside the allowlist (incl. relevance and the
        // Core-unresolved `name`) falls back to the surface's default
        // key (`created`), never a dead option, never an error
        // (C-SORT·1/F4). The request's dir applies to the resolved key
        // (the U8 defaultDir shape).
        var fallback = ParseSort(sort: "name", dir: "asc", SearchFeedAllowed);
        Assert.NotNull(fallback);
        Assert.Equal("created", fallback!.Key);
        Assert.False(fallback.Descending);
    }

    [Fact]
    public void Search_SortControl_Excludes_Relevance()
    {
        // M8 frozen — the relevance-untouched pin: the Options never
        // include a relevance key, and a ?sort=relevance request invents
        // **no** order — it falls back to the surface default (created,
        // desc, identical to sort = null — the U9
        // `Search_RelevanceNotASortKey` Core pin's Web-side twin).
        Assert.DoesNotContain("relevance", SearchFeedAllowed);

        var vm = SortViewModel.ForRoute(
            "/search",
            currentKey: "created",
            currentDir: "desc",
            options: [
                ("created", "desc"),
                ("title", "asc"),
            ]);
        var keys = vm.Options.Select(o => o.Key).ToList();
        Assert.DoesNotContain("relevance", keys);

        // A relevance request on a blank dir resolves to the
        // created-desc default — byte-for-byte the no-sort order
        // (C-SORT·2 — the U9 Core seam's own fallback shape).
        var feedSort = ParseSort(sort: "relevance", dir: null, SearchFeedAllowed);
        Assert.NotNull(feedSort);
        Assert.Equal("created", feedSort!.Key);
        Assert.True(feedSort.Descending);
    }
}
