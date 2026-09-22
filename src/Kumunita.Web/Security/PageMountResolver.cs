using Kumunita.Core.Pages;

namespace Kumunita.Web.Security;

/// <summary>
/// The layout's <b>mount-point resolver</b> (ADR 0039 §3.8): given a UI slot
/// (e.g. <c>"footer/community"</c>, <c>"help/account"</c>), resolve "the page
/// mounted at this slot" to its <c>/pages/…</c> href, or <c>null</c> when the
/// slot is unmounted (the layout then omits / falls back).
/// <para>
/// **Display, not access (ADR 0039 §3.8):** a mount point is where to
/// <em>surface a link</em>, never an access boundary. This resolver only
/// <em>names</em> the href — it runs <b>no</b> <c>CanAsync</c>/<c>CanSeeAsync</c>
/// decision. A mounted page the actor can't read still resolves to a href here;
/// <em>opening</em> it is the separate <c>Read</c> decision in
/// <see cref="Kumunita.Web.Controllers.PageController.Show"/> (403 on denied,
/// 404 on absent).
/// </para>
/// <para>
/// **Pure + seam-driven (testable):** the caller supplies the
/// <see cref="IPageService"/> (a layout partial injects it; the unit tests
/// substitute it — no live Postgres, no host). The resolver itself holds no
/// dependencies, so it is a direct unit target: "a page mounted at
/// <c>footer/community</c> → the about href + its title"; "an unmounted slot
/// → <c>null</c>". It reuses the pure <see cref="PagePaths"/> path derivation
/// (the path is the ancestor-slug chain, not stored) over the live tree.
/// </para>
/// </summary>
public static class PageMountResolver
{
    /// <summary>
    /// The resolved link for the page mounted at
    /// <paramref name="slot"/> — its <c>/pages/…</c> href and display
    /// <c>title</c> (the link's label) — or <c>null</c> when the slot is
    /// unmounted (<see cref="IPageService.GetByMountPointAsync"/> returns
    /// <c>null</c>) or the mounted page is not in the live tree (soft-deleted
    /// — the browse tree excludes it, so there is no valid href to offer).
    /// A display concern only: this runs no access decision (the
    /// <c>Read</c> decision is the page's own post view, ADR 0039 §3.8).
    /// </summary>
    public static async Task<(string Href, string Title)?> ResolveAsync(IPageService pages, string slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return null;

        var page = await pages.GetByMountPointAsync(slot).ConfigureAwait(false);
        if (page is null)
            return null;   // unmounted — omit the link

        // Derive the page's canonical path from the live tree (the path is the
        // ancestor-slug chain — a nested mounted page needs its ancestors to
        // be named). If the mounted page is not in the tree (soft-deleted),
        // there is no valid path → omit.
        var tree = await pages.GetTreeAsync().ConfigureAwait(false);
        var byId = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);
        if (!byId.ContainsKey(page.Id))
            return null;

        return (PagePaths.Href(byId, page), page.Title);
    }
}
