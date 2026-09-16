using Kumunita.Core.Announcements;
using Kumunita.Core.Posts;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;
using Kumunita.Web.Security; // MarkdownRenderer (a static in this namespace, used for the body preview).
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <b>My drafts</b> surface (ADR 0037) — <c>GET /my/drafts</c>. The
/// author's own drafts across both post lanes (community + group) and their
/// draft announcements, in one list. A *thin* HTTP layer (ADR 0006-D): route
/// + authz + shape; all the author-scoping lives in the services'
/// <see cref="PostService.ListMyDraftsAsync"/> and
/// <see cref="IAnnouncementService.ListMyDraftsAsync"/> (pure
/// <c>AuthorId == actorId</c> reads — the ADR 0037 author-only pin; no
/// <c>AccessAudit</c> lane, no role input).
/// <para>
/// <b>[Authorize]:</b> a signed-in actor is required — the drafts are the
/// actor's own, so an empty subject maps to a 404 (there is no "whose drafts
/// am I listing?" question to answer for an anonymous visitor; the
/// <see cref="Kumunita.Web.Controllers.PostsController"/> "Core expects an
/// authenticated actor" pin applied at the Web layer).
/// </para>
/// <para>
/// <b>Route shape:</b> <c>GET /my/drafts</c> is a single read lane. The
/// drafts are reachable per-row through their normal detail lanes
/// (<c>/posts/{id}</c> for a community post, <c>/groups/{id}/posts/{id}</c>
/// for a group post, <c>/announcements/{id}</c> for an announcement) — each
/// of which author-only-gates the draft and offers the author a Publish
/// button (the ADR 0037 publish lane). This list is the discoverability
/// surface: feeds and the announcement index deliberately exclude drafts.
/// </para>
/// </summary>
[Authorize]
public sealed class MyDraftsController(
    PostService posts,
    IAnnouncementService announcements,
    IUserInfoService userInfo) : Controller
{
    private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    /// <summary>
    /// <c>GET /my/drafts</c> — the actor's own drafts (community + group
    /// posts, and announcements), each with a link to its detail lane (where
    /// the author-only Publish button lives) and its author display name (a
    /// <see cref="IUserInfoService.GetProfileAsync"/> read — "a read, not a
    /// decision"; every row is the actor's own, so the name is trivially
    /// the actor's, kept for the row's consistency with the feed rows).
    /// </summary>
    [HttpGet("/my/drafts")]
    public async Task<IActionResult> Index()
    {
        var actor = SubjectId(User);
        if (string.IsNullOrEmpty(actor))
            return NotFound();

        // The actor's own draft posts (community + group lanes combined — the
        // query is a pure AuthorId == actorId read; no role, no audit row).
        var draftPosts = await posts.ListMyDraftsAsync(actor);

        // Resolve the lane labels (a read, not a decision — every row is the
        // actor's own): community posts carry a ComponentId (empty GroupId) →
        // the component's name; group posts carry a GroupId (non-empty) → the
        // group's name (the group lane's label, the same GetGroupAsync read
        // the /groups feed uses). The PostListItem's ComponentId/ComponentName
        // pair doubles as the "which lane + its id" hint the view uses to
        // build the detail link.
        var components = await userInfo.GetComponentsAsync(enabledOnly: false);
        var componentById = components.ToDictionary(c => c.Id, c => c.Name);
        var authorProfile = await userInfo.GetProfileAsync(actor);
        var authorName = authorProfile?.DisplayName is { Length: > 0 } dn ? dn : actor;

        var postItems = new List<PostListItem>(draftPosts.Count);
        foreach (var p in draftPosts)
        {
            bool isGroupLane = !string.IsNullOrEmpty(p.GroupId);
            string? laneLabel = null;
            if (isGroupLane)
            {
                var group = await userInfo.GetGroupAsync(p.GroupId);
                laneLabel = group?.Name;
            }
            else if (!string.IsNullOrEmpty(p.ComponentId)
                     && componentById.TryGetValue(p.ComponentId, out var cn))
            {
                laneLabel = cn;
            }

            postItems.Add(new PostListItem(
                p.Id,
                p.Title,
                MarkdownRenderer.PlainTextPreview(p.Body, 80),
                p.Created,
                authorName,
                p.AuthorId,
                laneLabel,
                isGroupLane ? p.GroupId : (p.ComponentId is { Length: > 0 } ? p.ComponentId : null)));
        }

        // The actor's own draft announcements (the same author-only read).
        var draftAnnouncements = await announcements.ListMyDraftsAsync(actor);

        return View(new MyDraftsViewModel
        {
            Posts = postItems,
            Announcements = draftAnnouncements,
        });
    }
}
