using Kumunita.Core.Search;

namespace Kumunita.Web.Models;

/// <summary>
/// The M8 <c>/search</c> surface's one view model (design doc §6 / D1). Two
/// render shapes over the frozen <see cref="ISearchService"/> seam:
/// <list type="bullet">
/// <item><c>surface=all</c> (the default) — <see cref="Sections"/> carries the
/// top <c>MaxPerSurface</c> (5) visible hits per in-scope surface (a
/// search-box answer, no pager: <see cref="Pager"/> is null).</item>
/// <item><c>surface=&lt;one&gt;</c> — <see cref="Sections"/> carries the one
/// surface's paged hits and <see cref="Pager"/> is the shared
/// <see cref="PagedViewModel"/> (the <c>_Pager</c> partial, ADR 0090 D1/D3 —
/// <c>HasMore</c> is the sole paging signal).</item>
/// </list>
/// <para>
/// **C-M8·4 — no hidden-count leak:** the model carries only visible hits +
/// the <c>HasMore</c> signal. There is no <c>Total</c>, no <c>HiddenCount</c>,
/// no candidate count anywhere on this surface (the <c>HiddenCount</c> lives
/// on the stored aggregate <see cref="Kumunita.Core.Authorization.AccessAudit"/>
/// row, never rendered).
/// </para>
/// <para>
/// **C-M8·5 — display-only inputs:** <c>Q</c> / <c>Scope</c> / <c>Surface</c> /
/// <c>Page</c> here are the request's display values echoed back for the form
/// and the pager links — never an input to an <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>
/// call or an audit row's identity (the service composes the frozen seams).
/// </para>
/// </summary>
/// <param name="Q">The trimmed query (echoed for the empty state / "no results for …").</param>
/// <param name="Scope">The effective scope (<c>community</c> default / <c>groups</c>; an anonymous
/// <c>groups</c> request degrades to <c>community</c> — D3/F6).</param>
/// <param name="Surface">The surface discriminator (<c>all</c> default / <c>posts</c> /
/// <c>events</c> / <c>pages</c> / <c>announcements</c>).</param>
/// <param name="Page">The page (floored to 1; meaningful only for the single-surface shape).</param>
/// <param name="Sections">The per-surface visible hit lists (≤ <c>MaxPerSurface</c> each on
/// <c>all</c>; the one surface's page on a single-surface view).</param>
/// <param name="PageHrefs">The page-hit <c>Id</c> → <c>/pages/{**path}</c> href map (the
/// <c>Page</c> detail route is path-derived, not id-derived — <see cref="Kumunita.Web.Security.PagePaths.Href"/>);
/// empty when no page hits render. A display projection — the hit's visibility already
/// ran in the service (a value-level read, never an access decision).</param>
/// <param name="Pager">The <c>_Pager</c> model for the single-surface shape (null on
/// <c>all</c> / a one-page surface — the F2 no-render pin).</param>
public sealed record SearchIndexViewModel(
    string Q,
    SearchScope Scope,
    string Surface,
    int Page,
    IReadOnlyDictionary<string, IReadOnlyList<SearchHit>> Sections,
    IReadOnlyDictionary<string, string> PageHrefs,
    PagedViewModel? Pager)
{
    /// <summary>True on the <c>surface=all</c> shape (per-surface sections, no pager — D1).</summary>
    public bool IsAll => string.Equals(Surface, "all", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The detail href for a hit — the page surface resolves through
    /// <see cref="PageHrefs"/> (path-derived), every other surface is its
    /// canonical id route (<c>/posts/{id}</c> / <c>/events/{id}</c> /
    /// <c>/announcements/{id}</c>; group-scope hits use their
    /// <c>/groups/{groupId}/posts|events/{id}</c> route).
    /// </summary>
    public string HrefFor(SearchHit hit)
    {
        if (string.Equals(hit.Surface, "pages", StringComparison.Ordinal))
            return PageHrefs.TryGetValue(hit.Id, out var href) ? href : "/pages/";
        if (hit.GroupId is { } gid)
            return $"/groups/{gid}/{hit.Surface}/{hit.Id}";
        return $"/{hit.Surface}/{hit.Id}";
    }
}
