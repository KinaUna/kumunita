using Kumunita.Core.Pages;
using Kumunita.Core.Tags;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>TG</c> lane's Web surface (ADR 0044 D5; the design doc §2.1
/// <c>GET /tags</c> + <c>GET /tags/{slug}</c> + <c>POST /api/tags/suggest</c>
/// routes). A *thin* HTTP layer (ADR 0006-D): routes + authz + shape; all
/// access decisions come from the <see cref="ITagService"/> read lane (the
/// content's existing <c>Read</c> decision — never a tag-specific gate,
/// C-TG·1). The controller never re-derives access (the M3
/// <see cref="PostsController"/> thin-controller precedent).
/// <para>
/// **Access-scoped by construction (C-TG·1 / C-TG·2):** both browse actions
/// render only what the access-scoped read seam returns. A tag used only on
/// content the viewer cannot read is **absent** from the list, the by-tag
/// results, and autocomplete — no title, no name, no "hidden" placeholder
/// (F3 / F4). The <c>ByTag</c> action maps the empty shape (slug unknown, or
/// the tag used only on unread content) to a **404** (the register's U7
/// 404-floor pin) — a 404 is the right floor here (a 403 would confirm the
/// tag exists, leaking a subject's existence, C-TG·1).
/// <para>
/// **No <c>AccessAudit</c> row (C-TG·8, D7):** the read lane methods emit no
/// <c>tag.*</c> row of their own; the content's own <c>Read</c>-decision
/// rows are the content's. The controller adds no audit of its own.
/// <para>
/// **404-floor (the register's U7 pin):** <c>ByTag</c> returns
/// <see cref="NotFoundResult"/> when both <c>Posts</c> and <c>Pages</c> are
/// empty — covering both "slug unknown" and "tag used only on unread
/// content" (the two are indistinguishable to the viewer; both map to 404,
/// not a blank page).
/// </summary>
[Authorize]
public sealed class TagController(
    ITagService tags,
    IPageService pages) : Controller
{
    private static string? ActorId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    // ── GET /tags — the tag list (F12: empty on a fresh instance) ─────────

    /// <summary>
    /// <c>GET /tags</c> — the tag list: the distinct tags used on ≥ 1 post
    /// / blog page the viewer may already read, each with its use-count and
    /// display name resolved in the viewer's language (the C-TG·2 base query;
    /// the ADR 0005 preference order). F12: a fresh instance (zero tags)
    /// renders an empty list, not a 404 (the "no tags yet" shape).
    /// No <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    [HttpGet("/tags")]
    public async Task<IActionResult> Index()
    {
        var actor = ActorId(User) ?? string.Empty;
        var items = await tags.ListForActorAsync(actor);
        return View(new TagListViewModel { Tags = items });
    }

    // ── GET /tags/{slug} — the by-tag view (404-floor) ────────────────────

    /// <summary>
    /// <c>GET /tags/{slug}</c> — the posts and blog pages tagged with
    /// <paramref name="slug"/> that the viewer may already read (C-TG·3:
    /// the post's own <c>Read</c> decision is applied before the post is
    /// returned). Both <c>Posts</c> and <c>Pages</c> empty ⇒ **404** (the
    /// register's U7 404-floor pin — the tag is unknown, or used only on
    /// unread content; both map to 404, not a blank page, not a 403 — a 403
    /// would confirm the tag exists, C-TG·1). No <c>AccessAudit</c> row of
    /// the tag lane's own (C-TG·8).
    /// </summary>
    [HttpGet("/tags/{slug}")]
    public async Task<IActionResult> ByTag([FromRoute] string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return NotFound();

        var actor = ActorId(User) ?? string.Empty;
        var posts = await tags.ListPostsByTagAsync(slug, actor);
        var blogPages = await tags.ListPagesByTagAsync(slug, actor);

        // 404-floor: both lists empty = slug unknown or used only on unread
        // content (C-TG·1 / C-TG·2 — the two are indistinguishable and both
        // map to 404, not 403 — a 403 would confirm the tag exists).
        if (posts.Count == 0 && blogPages.Count == 0)
            return NotFound();

        // Display name (the ADR 0005 preference order, resolved to the
        // viewer's language): find the tag in the accessor's readable tag
        // set (one extra base-query call — N+1 acceptable for a neighborhood
        // scale, the M2 GroupsController display-name precedent).
        var tagItems = await tags.ListForActorAsync(actor);
        var displayName = tagItems
            .FirstOrDefault(t => t.Tag.Slug == slug)?.DisplayedName
            ?? slug;

        // The id → page map for PagePaths.Href (the derived /pages/… path —
        // the BlogController precedent: load the full non-deleted tree so
        // an ancestor chain is always resolvable).
        var tree = await pages.GetTreeAsync();
        var pageById = tree.ToDictionary(p => p.Id, StringComparer.Ordinal);

        return View(new TagByTagViewModel
        {
            Slug = slug,
            DisplayedName = displayName,
            Posts = posts,
            Pages = blogPages,
            PageById = pageById,
        });
    }

    // ── POST /api/tags/suggest — the autocomplete seam (F9 / F10) ─────────

    /// <summary>
    /// <c>POST /api/tags/suggest</c> — the <see cref="ITagService
    /// .SuggestAsync"/> seam (F9: viewer-language display name; F10: ≤ 10
    /// cap — both enforced in Core). CSRF-aware: the client
    /// (<c>client/lib/api.ts</c>) sends the
    /// <c>RequestVerificationToken</c> header (ADR 0015 §7). No
    /// <c>AccessAudit</c> row (C-TG·8).
    /// </summary>
    [HttpPost("/api/tags/suggest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suggest([FromBody] SuggestRequest request)
    {
        var actor = ActorId(User) ?? string.Empty;
        var suggestions = await tags.SuggestAsync(request?.Prefix ?? string.Empty, actor);
        return Json(new TagSuggestViewModel { Suggestions = suggestions });
    }
}

/// <summary>
/// The <c>POST /api/tags/suggest</c> request body: the typed prefix to match
/// against (<c>starts_with(displayName, prefix) OR starts_with(slug, prefix)</c>,
/// C-TG·2 / C-TG·4). The client (the <c>client/lib</c> tag-suggest dropdown,
/// U8) sends <c>{ "prefix": "san" }</c> as JSON.
/// </summary>
public sealed record SuggestRequest(string Prefix);
