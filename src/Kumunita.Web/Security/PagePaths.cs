using Kumunita.Core.Pages;

namespace Kumunita.Web.Security;

/// <summary>
/// The pure <c>Page</c> → **derived path** seam (the Pages lane <c>PG</c>,
/// ADR 0039 §3.3): the path is the chain of ancestor slugs
/// (<c>about</c> → <c>"about"</c>; <c>help/getting-started</c> →
/// <c>"help" → "getting-started"</c>), **derived** from the
/// <see cref="Page.ParentId"/>/<see cref="Page.Slug"/> chain and never stored.
/// <para>
/// **Web-only + pure:** the <see cref="Kumunita.Core.Pages.PageService"/>
/// read lanes resolve a path <em>downward</em> (root-to-leaf, the
/// <see cref="Kumunita.Core.Pages.IPageService.GetByPathAsync"/> lane); this
/// helper is the inverse projection the Web layer needs to <em>render</em> a
/// link — the tree browse's <c>&lt;a href="/pages/…"&gt;</c> and the
/// layout's mount-point resolver both need "given this page, what is its
/// canonical path?" It is a value-level read (no store, no HTTP, no
/// authorization — the caller has already loaded the tree), so it lives
/// beside <see cref="PostReadDecision"/> / <see cref="AttachmentIds"/> /
/// <see cref="MarkdownRenderer"/> and is directly unit-testable.
/// </para>
/// <para>
/// The helper does **not** decide access (a page's visibility is the frozen
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService.CanAsync"/> /
/// <c>CanSeeAsync</c> decision, not this projection) — it only names the
/// path. A page whose parent was soft-deleted (excluded from the browse
/// tree) still resolves its path from the full <paramref name="byId"/> map
/// the caller supplies (a path is a naming concern, independent of the
/// browse filter).
/// </para>
/// </summary>
public static class PagePaths
{
    /// <summary>
    /// The derived path (ADR 0039 §3.3) of <paramref name="page"/> — the
    /// slash-joined chain of ancestor slugs, root-first. <paramref name="byId"/>
    /// is the id → page map the caller built (typically the full browse
    /// tree, so an ancestor chain is always resolvable even if a mid-chain
    /// page was later excluded from the *display* filter). A guard caps the
    /// walk (a corrupted <see cref="Page.ParentId"/> cycle terminates — the
    /// same defensive bound <see cref="Kumunita.Core.Pages.PageService"/>
    /// uses on its <c>ParentId</c> walks).
    /// </summary>
    public static string Derive(IReadOnlyDictionary<string, Page> byId, Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(byId);

        var parts = new List<string>();
        Page? current = page;
        int guard = 0;
        while (current is not null && ++guard <= 64)
        {
            if (!string.IsNullOrWhiteSpace(current.Slug))
                parts.Insert(0, current.Slug);

            current = current.ParentId is { } pid
                && byId.TryGetValue(pid, out var parent)
                ? parent
                : null;
        }
        return string.Join('/', parts);
    }

    /// <summary>
    /// The app-relative href for <paramref name="page"/> — <c>/pages/</c>
    /// + the derived path. The tree browse and the mount-point resolver both
    /// <c>&lt;a href&gt;</c> this; a root page is simply <c>/pages/about</c>.
    /// </summary>
    public static string Href(IReadOnlyDictionary<string, Page> byId, Page page) =>
        "/pages/" + Derive(byId, page);
}
