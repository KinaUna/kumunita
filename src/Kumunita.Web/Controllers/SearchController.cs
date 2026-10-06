using Kumunita.Core.Pages;
using Kumunita.Core.Query;
using Kumunita.Core.Search;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The M8 search surface (D1 — one <c>/search</c> page, not per-feed
/// <c>?q=</c>; ADR 0091). <c>GET /search?q=…&amp;surface=…&amp;scope=…&amp;page=…</c>:
/// <list type="bullet">
/// <item><c>surface=all</c> (the default) renders the top
/// <c>MaxPerSurface</c> (5) visible hits per in-scope surface — a
/// search-box answer, no pager.</item>
/// <item><c>surface=posts|events|pages|announcements</c> renders a
/// <b>paged</b> list (the <c>page</c> param discipline, the shared
/// <see cref="PagedViewModel"/>/<c>_Pager</c> idiom — ADR 0090 D1/D3/D7,
/// no new pager idiom).</item>
/// </list>
/// <para>
/// **Read-only, frozen seams (C-M8·1):** this controller composes only the
/// frozen <see cref="ISearchService"/> seam (D8 — the bounded context's
/// single interface) and the frozen <see cref="IPageService"/> read
/// (<see cref="IPageService.GetTreeAsync"/>) for the page-hit detail hrefs
/// (the <c>/pages/{**path}</c> detail route is path-derived —
/// <see cref="PagePaths.Href"/> is the pure Web-side inverse projection the
/// tree browse already uses; a value-level read, never an access decision).
/// No writes, no new <c>AccessAction</c>/<c>AccessVia</c>/adapter.
/// </para>
/// <para>
/// **Display-only inputs (C-M8·5):** <c>q</c>/<c>page</c>/<c>scope</c>/
/// <c>surface</c> are request display values — they are never an input to an
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> call, an
/// <c>Audience</c> evaluation, or an audit row's identity. The service
/// composes the frozen seams (D6); the controller only echoes the values
/// back into the view and the pager links (the ADR 0090 D7 filter-preservation
/// rule — <see cref="PagedViewModel.ForRoute"/>'s <c>FilterParams</c>).
/// </para>
/// <para>
/// **Silent scope degradation (D3/F6):** an anonymous <c>scope=groups</c>
/// request is served the community read (the seam degrades to
/// <see cref="SearchScope.Community"/> internally — no 403, no refusal
/// text). The view reflects the scope the service actually served.
/// </para>
/// <para>
/// **Anonymous can search (F1):** no <c>[Authorize]</c> — the nav box renders
/// for guests, parity with the public feed surface. The per-surface
/// visibility is the service's (the canonical predicates, C-M8·2), not this
/// controller's.
/// </para>
/// </summary>
public sealed class SearchController : Controller
{
    private static readonly string[] KnownSurfaces =
    {
        SearchService.PostsSurface,
        SearchService.EventsSurface,
        SearchService.PagesSurface,
        SearchService.AnnouncementsSurface,
        SearchService.ProjectsSurface,
        SearchService.BoardsSurface,
        SearchService.TodosSurface,
        SearchService.InventorySurface,
        SearchService.DocumentsSurface,
        SearchService.PeopleSurface,
    };

    private readonly ISearchService _search;
    private readonly IPageService _pages;

    // The items-per-page preference seam (FeedPaging). Optional (default
    // null) so test-construction sites that build this controller without it
    // keep compiling (a missing seam falls back to the platform page-size
    // default); DI always supplies it in the app.
    private readonly Kumunita.Core.UserInfo.IUserInfoService? _userInfo;

    public SearchController(
        ISearchService search,
        IPageService pages,
        Kumunita.Core.UserInfo.IUserInfoService? userInfo = null)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
        _userInfo = userInfo;
    }

    /// <summary>
    /// <c>GET /search</c> — the D1 surface. A blank <c>q</c> renders the empty
    /// state <b>without calling the service</b> (D4 — no decision, no audit
    /// row, the C-M7·5 shape extended to <c>q</c>).
    /// </summary>
    /// <param name="q">The query text (trimmed; blank → the empty state).</param>
    /// <param name="surface"><c>all</c> (default) / <c>posts</c> / <c>events</c> / <c>pages</c> /
    /// <c>announcements</c> / <c>projects</c> / <c>boards</c> / <c>todos</c> / <c>inventory</c> /
    /// <c>documents</c> / <c>people</c> (ADR 0124); an unknown value degrades to <c>all</c>
    /// (a display fallback, never an error — the C-DWM·8 discipline).</param>
    /// <param name="scope"><c>community</c> (default) / <c>groups</c> (D3 — anonymous
    /// degrades to community-only, the service's silent lane).</param>
    /// <param name="page">The page (floored to 1); meaningful only on a single-surface view.</param>
    /// <param name="sort">The sort key (M26 U15, C-SORT·3 — Web-only); parsed against the
    /// closed <c>created</c>/<c>title</c> allowlist, or <c>null</c> when the viewer chose
    /// no sort (C-SORT·2, F1 — the seam keeps its pinned Created-desc order exactly).</param>
    /// <param name="dir"><c>asc</c> / <c>desc</c> (M26 U15, C-SORT·3); blank/unknown falls
    /// back to the resolved key's default direction.</param>
    [HttpGet("/search")]
    public async Task<IActionResult> Index(
        string? q, string? surface, string? scope, int? page,
        string? sort = null, string? dir = null)
    {
        var query = (q ?? string.Empty).Trim();
        var isSignedIn = User.Identity?.IsAuthenticated == true;

        var surfaceName = KnownSurfaces.Contains(
            (surface ?? "all").Trim().ToLowerInvariant())
            ? (surface ?? "all").Trim().ToLowerInvariant()
            : "all";

        var wantsGroups = string.Equals(
            (scope ?? "community").Trim().ToLowerInvariant(), "groups",
            StringComparison.OrdinalIgnoreCase);
        // D3/F6 — the scope the *service actually serves*: an anonymous
        // groups request degrades to community (no 403, no refusal surface).
        var effectiveScope = wantsGroups && isSignedIn
            ? SearchScope.Groups
            : SearchScope.Community;

        var pageNum = page is > 0 ? page.Value : 1;

        IReadOnlyDictionary<string, IReadOnlyList<SearchHit>> sections;
        PagedViewModel? pager = null;
        var scopeWire = effectiveScope == SearchScope.Groups ? "groups" : "community";

        // M26 U15 (C-SORT·3) — the ?sort=/?dir= → SortSpec mapping is
        // Web-only: parse against the search surface's closed allowlist
        // (U2 §2.2 row 18 — `created`/`title` only; relevance is **not** a
        // sort key, M8 frozen), or null when the viewer chose no sort
        // (C-SORT·2, F1 — the seam keeps its pinned Created-desc order).
        var feedSort = ParseSort(sort, dir, SearchFeedAllowedKeys);
        SortViewModel? sortVm = null;

        if (surfaceName == "all")
        {
            // D1 — the search-box answer: top MaxPerSurface per surface, no
            // pager (the _Pager partial renders nothing on a null Pager — F2).
            var results = string.IsNullOrEmpty(query)
                ? new SearchResults(new Dictionary<string, IReadOnlyList<SearchHit>>(), query)
                : await _search.SearchAsync(query, effectiveScope,
                    KumunitaPrincipal.SubjectId(User), HttpContext.RequestAborted);
            sections = results.Sections;
        }
        else
        {
            // D1 — the paged single-surface read. A blank q never reaches the
            // service (no decision, no audit row). The resolved SortSpec
            // threads into the seam (the U9 Core seam — `null` keeps the
            // per-surface `OrderByDescending(Created)` byte-for-byte).
            // The resident's items-per-page preference (FeedPaging resolves
            // Profile.PageSize → PageSizer default/clamp); a no-actor read or
            // a missing seam uses the platform default.
            int pageSize = await FeedPaging.PageSizeAsync(_userInfo, KumunitaPrincipal.SubjectId(User));
            SearchSurfacePage sp = string.IsNullOrEmpty(query)
                ? new SearchSurfacePage(surfaceName, Array.Empty<SearchHit>(), 1, false)
                : await _search.SearchSurfaceAsync(
                    surfaceName, query, effectiveScope,
                    KumunitaPrincipal.SubjectId(User) ?? string.Empty,
                    pageNum, HttpContext.RequestAborted,
                    sort: feedSort, pageSize: pageSize);
            sections = new Dictionary<string, IReadOnlyList<SearchHit>>
            {
                [surfaceName] = sp.Hits,
            };
            // ADR 0090 D5/D7 — the pager: null on a one-page surface (the F2
            // no-render pin); the links carry q + surface + scope (D7 filter
            // preservation), page first (the _Pager's PagerLink shape).
            // M26 U15 (C-SORT·8) — the sort/dir pairs join the pager's
            // FilterParams **only** when the request carried a non-blank
            // ?sort= (U11's SortViewModel.SortFilterParams helper, reused —
            // not re-derived); an unsorted read keeps the pre-M26 pairs
            // byte-identical (C-SORT·2).
            if (sp.HasMore || pageNum > 1)
            {
                var filterParams = new Dictionary<string, string>
                {
                    ["q"] = query,
                    ["surface"] = surfaceName,
                    ["scope"] = scopeWire,
                };
                foreach (var (k, v) in SortViewModel.SortFilterParams(sort, dir))
                    filterParams[k] = v;
                pager = PagedViewModel.ForRoute("/search", sp.Page, pageSize, sp.HasMore,
                    filterParams);
            }

            // M26 U15 (D-SORT·5) — the one shared sort control (the U10 _Sort
            // reference, reused verbatim — C-SORT·1): the closed search
            // allowlist (U2 §2.2 row 18 — created/`title` only; **no**
            // relevance key, M8 frozen), no dead options (F9). The control
            // only exists on the single-surface shape (the `all` shape has
            // no pager and the seam's SortSpec applies per surface — a
            // cross-surface control would be a dead option).
            sortVm = SortViewModel.ForRoute(
                "/search",
                currentKey: feedSort?.Key,
                currentDir: feedSort is { } s ? (s.Descending ? "desc" : "asc") : null,
                options: [
                    ("created", "desc"),
                    ("title", "asc"),
                ]);
        }

        var pageHrefs = await PageHrefsForAsync(sections);

        return View(new SearchIndexViewModel(
            Q: query,
            Scope: effectiveScope,
            Surface: surfaceName,
            Page: surfaceName == "all" ? 1 : pageNum,
            Sections: sections,
            PageHrefs: pageHrefs,
            Pager: pager)
        {
            Sort = sortVm,
        });
    }

    // M26 U15 (C-SORT·1) — the search surface's closed sort allowlist (U2
    // §2.2 row 18): `created` + the surface's `title` key only (→
    // `SearchHit.Title ?? ""`, the U9 in-memory ordering). The Core seam's
    // own switch (`SearchService.SearchSurfaceAsync`) resolves exactly
    // these two keys — everything else, incl. `name` and `relevance`,
    // falls back to the pinned created-desc default (C-SORT·1 / F4).
    private static readonly IReadOnlySet<string> SearchFeedAllowedKeys =
        new HashSet<string>(StringComparer.Ordinal) { "created", "title" };

    // M26 U15 (C-SORT·3) — parse the request's ?sort=/?dir= against the
    // search surface's closed allowlist — or null when the viewer chose
    // no sort (C-SORT·2, F1).
    private static SortSpec? ParseSort(string? sort, string? dir, IReadOnlySet<string> allowedKeys)
        => !string.IsNullOrWhiteSpace(sort)
            ? SortKeys.Parse(sort, dir, allowedKeys, "created", defaultDir: true)
            : null;

    /// <summary>
    /// Resolve the <c>/pages/{**path}</c> hrefs for the page-surface hits —
    /// the <c>Page</c> detail route is path-derived (the slug chain), while the
    /// <see cref="SearchHit"/> carries only the page's <see cref="Page.Id"/>.
    /// One <see cref="IPageService.GetTreeAsync"/> read (the ADR 0039 tree —
    /// the same read the <c>/pages</c> browse uses) feeds the pure
    /// <see cref="PagePaths.Href"/> inverse projection (a value-level read,
    /// never an access decision — the hit's visibility already ran in the
    /// service). A page id absent from the tree (a deleted ancestor) degrades
    /// to its own slug; the detail route's own 403/404 gate is the
    /// authoritative deny, so a hit the tree no longer names lands on the same
    /// refuse the feed would have.
    /// </summary>
    private async Task<Dictionary<string, string>> PageHrefsForAsync(
        IReadOnlyDictionary<string, IReadOnlyList<SearchHit>> sections)
    {
        var pageIds = sections
            .Where(kv => kv.Key == SearchService.PagesSurface)
            .SelectMany(kv => kv.Value)
            .Select(h => h.Id)
            .Distinct()
            .ToList();
        if (pageIds.Count == 0) return new Dictionary<string, string>();

        var pages = await _pages.GetTreeAsync();
        var byId = pages.ToDictionary(p => p.Id, StringComparer.Ordinal);
        return pageIds.ToDictionary(
            id => id,
            id => byId.TryGetValue(id, out var p)
                ? PagePaths.Href(byId, p)
                : "/pages/");   // a tree-absent id — the route's gate is the deny.
    }
}
