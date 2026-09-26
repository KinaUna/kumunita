using Kumunita.Core.Pages;
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
    };

    private readonly ISearchService _search;
    private readonly IPageService _pages;

    public SearchController(ISearchService search, IPageService pages)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _pages = pages ?? throw new ArgumentNullException(nameof(pages));
    }

    /// <summary>
    /// <c>GET /search</c> — the D1 surface. A blank <c>q</c> renders the empty
    /// state <b>without calling the service</b> (D4 — no decision, no audit
    /// row, the C-M7·5 shape extended to <c>q</c>).
    /// </summary>
    /// <param name="q">The query text (trimmed; blank → the empty state).</param>
    /// <param name="surface"><c>all</c> (default) / <c>posts</c> / <c>events</c> / <c>pages</c> /
    /// <c>announcements</c>; an unknown value degrades to <c>all</c> (a display fallback,
    /// never an error — the C-DWM·8 discipline).</param>
    /// <param name="scope"><c>community</c> (default) / <c>groups</c> (D3 — anonymous
    /// degrades to community-only, the service's silent lane).</param>
    /// <param name="page">The page (floored to 1); meaningful only on a single-surface view.</param>
    [HttpGet("/search")]
    public async Task<IActionResult> Index(
        string? q, string? surface, string? scope, int? page)
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
            // service (no decision, no audit row).
            SearchSurfacePage sp = string.IsNullOrEmpty(query)
                ? new SearchSurfacePage(surfaceName, Array.Empty<SearchHit>(), 1, false)
                : await _search.SearchSurfaceAsync(
                    surfaceName, query, effectiveScope,
                    KumunitaPrincipal.SubjectId(User) ?? string.Empty,
                    pageNum, HttpContext.RequestAborted);
            sections = new Dictionary<string, IReadOnlyList<SearchHit>>
            {
                [surfaceName] = sp.Hits,
            };
            // ADR 0090 D5/D7 — the pager: null on a one-page surface (the F2
            // no-render pin); the links carry q + surface + scope (D7 filter
            // preservation), page first (the _Pager's PagerLink shape).
            if (sp.HasMore || pageNum > 1)
            {
                pager = PagedViewModel.ForRoute("/search", sp.Page, SearchService.PageSize, sp.HasMore,
                    new Dictionary<string, string>
                    {
                        ["q"] = query,
                        ["surface"] = surfaceName,
                        ["scope"] = scopeWire,
                    });
            }
        }

        var pageHrefs = await PageHrefsForAsync(sections);

        return View(new SearchIndexViewModel(
            Q: query,
            Scope: effectiveScope,
            Surface: surfaceName,
            Page: surfaceName == "all" ? 1 : pageNum,
            Sections: sections,
            PageHrefs: pageHrefs,
            Pager: pager));
    }

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
