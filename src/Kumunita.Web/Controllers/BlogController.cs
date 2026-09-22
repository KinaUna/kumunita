using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <b>blog feed</b> surface of the <c>PG</c> lane (ADR 0040) — the
/// per-user "blog" view that lists a resident's own
/// <see cref="Page"/>s (<see cref="PageKind"/> = <c>User</c>) and is the
/// anchor a post links its pages to. Two routes, over the frozen
/// <see cref="IPageService"/> seams (CQRS-lite, ADR 0039):
/// <list type="bullet">
/// <item><c>GET /blog</c> — a signed-in resident's <b>own</b> feed (a
///       convenience self-route; a signed-out visitor has no "own" feed, so
///       this redirects to <c>/directory</c> — the one place a resident set
///       is always listed).</item>
/// <item><c>GET /blog/{userId}</c> — a <b>specific</b> resident's feed:
///       their blog pages (newest-first), each linking to its
///       <c>/pages/…</c> href. A draft page is <b>absent</b> from another
///       resident's feed (ADR 0037 — a draft is its author's); the author's
///       own feed badges drafts. An empty feed (no blog pages yet) is a
///       valid shape — the view renders a "no blog posts yet" note, not a
///       404.</item>
/// </list>
/// <para>
/// **The feed is read-only.** The write lanes (create / edit / move /
/// delete) live on the <see cref="PageController"/> — the feed's only
/// affordance is the "New blog page" button on the author's own feed, which
/// routes to <c>/pages/new</c> (the ADR 0040 composer, kind = blog). A
/// resident's blog pages are personal content (ADR 0040 — no Moderator
/// lane), so the feed exposes no standing controls; the page's own
/// <c>/pages/{**path}</c> view is where edit / translate / publish affordances
/// live.
/// </para>
/// <para>
/// **Draft gate (author-only, the ADR 0037 pin):** a draft page is its
/// author's, until published. The feed filters drafts out of another
/// resident's listing (they simply never appear); on the author's own feed
/// a draft appears, badged <see cref="BlogPostRow.IsDraft"/> (the same
/// gate the <see cref="PageController.Show"/> post view runs — a non-author
/// hitting a draft by path is a 403).
/// </para>
/// </summary>
[Route("blog")]
public sealed class BlogController(
    IPageService pages,
    IUserInfoService userInfo) : Controller
{
    /// <summary>
    /// <c>GET /blog</c> — the signed-in resident's own feed (a self-route).
    /// Signed-out visitors have no "own" feed; they are redirected to
    /// <c>/directory</c> (the resident catalog) — the one place "who is
    /// here" is always listed (ADR 0040 — a blog is a resident's personal
    /// namespace, so the feed's home is the resident's own).
    /// </summary>
    [HttpGet]
    [Route("")]
    [Authorize]
    public async Task<IActionResult> Index()
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        if (string.IsNullOrEmpty(actorId))
            return RedirectToAction("Index", "Directory");
        return RedirectToAction(nameof(Feed), new { userId = actorId });
    }

    /// <summary>
    /// <c>GET /blog/{userId}</c> — a specific resident's blog feed: their
    /// <see cref="Page"/>s (<see cref="PageKind"/> = <c>User</c>, authored by
    /// that resident), newest-first, each linking to its <c>/pages/…</c>
    /// href. A draft page is absent from another resident's feed (ADR 0037);
    /// on the author's own feed a draft appears, badged. The author's
    /// display name is resolved best-effort (a missing profile is a display
    /// gap, not an error — the feed still renders). An unknown
    /// <paramref name="userId"/> (no such resident, or no blog pages) is a
    /// valid empty feed, not a 404 — the feed is a listing, and "no blog
    /// posts yet" is a real state a resident is in before writing their
    /// first page.
    /// </summary>
    [HttpGet]
    [Route("{userId}")]
    public async Task<IActionResult> Feed(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        bool isOwner = string.Equals(userId, actorId, StringComparison.Ordinal);

        // The blog read lane (ADR 0040) — the resident's own User-kind pages,
        // newest-first. No access decision here: a blog page's own
        // /pages/{**path} view runs the Read gate (the feed is a listing of
        // the resident's pages; a denied page's <b>title</b> is not secret —
        // the feed is the resident's own, and the page's body is gated at the
        // post view, the ADR 0039 §3.8 split). A draft is its author's —
        // filter it out of another resident's feed (the ADR 0037 author-only
        // pin, the same gate PageController.Index / Show run).
        var all = await pages.GetBlogPagesAsync(userId).ConfigureAwait(false);
        var visible = all
            .Where(p => isOwner || !p.IsDraft)
            .ToList();

        // The /pages/… hrefs (the derived path, the ADR 0039 §3.3 ancestor
        // slug chain). Load the full live tree so an ancestor chain is always
        // resolvable (a blog root's parent is the resident's blog root; a
        // sub-page's chain walks up to it).
        var tree = await pages.GetTreeAsync().ConfigureAwait(false);
        var byId = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);

        var rows = visible
            .Select(p => new BlogPostRow(
                p.Id,
                p.Title,
                PagePaths.Href(byId, p),
                p.Modified ?? p.Created,
                p.IsDraft))
            .ToList();

        // The author's display name (best-effort — a missing profile is a
        // display gap, not an error; the feed still renders under the id).
        string? displayName = null;
        var profile = await userInfo.GetProfileAsync(userId).ConfigureAwait(false);
        displayName = profile?.DisplayName;

        return View(new BlogViewModel(userId, displayName, rows, isOwner));
    }
}
