using Kumunita.Core.Announcements;

namespace Kumunita.Web.Models;

/// <summary>
/// The <b>My drafts</b> surface (ADR 0037) — <c>GET /my/drafts</c>. The
/// author's own drafts across <b>both</b> post lanes (community posts with
/// empty <see cref="Kumunita.Core.Posts.Post.GroupId"/> and group posts with
/// a set <see cref="Kumunita.Core.Posts.Post.GroupId"/>) plus their own draft
/// announcements. A single, author-scoped discoverability list — the surface
/// that makes drafts findable, since feeds deliberately exclude them
/// (<see cref="Kumunita.Core.Posts.PostService.ListFeedAsync"/> /
/// <see cref="Kumunita.Core.Announcements.AnnouncementService.ListVisibleAsync"/>
/// both filter <c>IsDraft == false</c>).
/// <para>
/// <b>Author-only by construction (ADR 0037):</b> every row is the signed-in
/// actor's own draft (<see cref="Kumunita.Core.Posts.Post.AuthorId"/> /
/// <see cref="Announcement.AuthorId"/> == the actor's subject id, ordinal
/// match in the query). No other actor can ever list this page's rows — the
/// route is <c>[Authorize]</c>-gated and the services'
/// <see cref="Kumunita.Core.Posts.PostService.ListMyDraftsAsync"/> /
/// <see cref="Kumunita.Core.Announcements.IAnnouncementService.ListMyDraftsAsync"/>
/// are pure <c>AuthorId == actorId</c> reads with no role input (a GlobalAdmin
/// querying someone else's drafts gets only their own — the author-only pin
/// holds end-to-end).
/// </para>
/// </summary>
public sealed class MyDraftsViewModel
{
    /// <summary>The actor's draft posts (community + group lanes combined),
    /// sorted by <c>Created</c> descending. Each row carries its
    /// <see cref="PostListItem.ComponentId"/> (empty ⇒ community lane,
    /// non-empty ⇒ group lane) so the detail link points at the right lane.</summary>
    public IReadOnlyList<PostListItem> Posts { get; set; } = [];

    /// <summary>The actor's draft announcements, sorted by <c>Created</c>
    /// descending.</summary>
    public IReadOnlyList<Announcement> Announcements { get; set; } = [];
}
