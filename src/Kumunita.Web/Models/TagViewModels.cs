using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Tags;

namespace Kumunita.Web.Models;

/// <summary>
/// The <c>TG</c> lane's Web surface view models (ADR 0044 D5; the
/// <see cref="Kumunita.Core.Tags.ITagService"/> read lane's projection —
/// the design doc §2.1 <c>TagListViewModel</c> / <c>TagByTagViewModel</c>
/// / <c>TagSuggestViewModel</c> shapes, verbatim).
/// <para>
/// **Read-only by construction (C-TG·1 / C-TG·8):** every row here was
/// returned by the access-scoped read seam
/// (<see cref="Kumunita.Core.Tags.ITagService.ListForActorAsync"/> /
/// <see cref="Kumunita.Core.Tags.ITagService.ListPostsByTagAsync"/> /
/// <see cref="Kumunita.Core.Tags.ITagService.ListPagesByTagAsync"/> /
/// <see cref="Kumunita.Core.Tags.ITagService.SuggestAsync"/>) — the
/// content's own <c>Read</c> decision already ran in Core (the ADR 0035
/// <c>PostReadDecision</c> routing), so a denied post's or page's fields
/// never reach these models. The tag grants nothing (C-TG·1); the read
/// emits no <c>AccessAudit</c> row of its own (C-TG·8). The controller does
/// **no** re-check and the view renders **only** what the seam returned —
/// no blanking, no "hidden" sentinel (the M3
/// <see cref="Kumunita.Web.Models.FeedViewModel"/> analog).
/// </para>
/// </summary>

/// <summary>
/// The <c>GET /tags</c> tag list (F12 — empty on a fresh instance,
/// C-TG·7): the <see cref="Kumunita.Core.Tags.TagItem"/> rows (the tag,
/// its use-count over the actor's readable content, and the display name
/// resolved to the actor's language — ADR 0005 preference order, D5), in
/// the seam's order (display name, then <c>Slug</c>).
/// </summary>
public sealed class TagListViewModel
{
    /// <summary>The readable tags (name + use-count, viewer language).</summary>
    public IReadOnlyList<TagItem> Tags { get; set; } = [];
}

/// <summary>
/// The <c>GET /tags/{slug}</c> by-tag view: the actor-readable
/// <see cref="Post"/>s (<c>Posts</c>) and <c>PageKind.User</c> blog
/// pages (<c>Pages</c>) whose <c>TagIds</c> contains the tag resolved from
/// the slug — each row links to its own detail route (a group post to
/// <c>/groups/{groupId}/posts/{id}</c>, a community post to
/// <c>/posts/{id}</c>, a blog page to its derived <c>/pages/…</c> path
/// via <see cref="Kumunita.Web.Security.PagePaths"/>). <see cref="PageById"/>
/// is the id → page map the view uses to render those
/// <c>/pages/…</c> hrefs (the derived ancestor-slug chain, ADR 0039 §3.3) —
/// a naming convenience, never an access surface.
/// <para>
/// **404-floor (the register's U7 pin):** when the slug is unknown, or the
/// tag is used only on content this viewer cannot read, both lists are
/// empty — the <see cref="Kumunita.Web.Controllers.TagController.ByTag"/>
/// action maps that shape to a 404 *before* this model is rendered (the
/// view never receives an empty by-tag model; the empty state is a
/// controller response, the M3 "403 on denied, not a blank page" analog —
/// here a 404, because a tag behind unread content must not confirm its
/// existence, C-TG·1 / C-TG·2).
/// </para>
/// </summary>
public sealed class TagByTagViewModel
{
    /// <summary>The slug the view was requested for (the <c>Slug</c>
    /// business key, C-TG·4).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>The tag's display name resolved in the viewer's language
    /// (the ADR 0005 preference order — the
    /// <see cref="Kumunita.Core.Tags.ITagService.ListForActorAsync"/> row's
    /// <see cref="Kumunita.Core.Tags.TagItem.DisplayedName"/>, falling back
    /// to the slug when the tag has no readable use).</summary>
    public string DisplayedName { get; set; } = string.Empty;

    /// <summary>The actor-readable posts tagged with it (community + group;
    /// each row carries its own <c>GroupId</c> for the detail link).</summary>
    public IReadOnlyList<Post> Posts { get; set; } = [];

    /// <summary>The actor-readable <c>PageKind.User</c> blog pages tagged
    /// with it (a <c>PageKind.System</c> page can never appear here —
    /// its <c>TagIds</c> are always empty, C-TG·6).</summary>
    public IReadOnlyList<Page> Pages { get; set; } = [];

    /// <summary>The id → <see cref="Page"/> map for
    /// <see cref="Kumunita.Web.Security.PagePaths.Href"/> (the derived
    /// <c>/pages/…</c> paths; the full non-deleted tree, so an ancestor
    /// chain is always resolvable — the <see cref="BlogController"/>
    /// precedent).</summary>
    public IReadOnlyDictionary<string, Page> PageById { get; set; }
        = new Dictionary<string, Page>(StringComparer.Ordinal);
}

/// <summary>
/// The <c>POST /api/tags/suggest</c> autocomplete response (F9 / F10 — the
/// viewer-language display name + the ≤ 10 cap, both enforced by the
/// <see cref="Kumunita.Core.Tags.ITagService.SuggestAsync"/> seam): the
/// matched <see cref="Kumunita.Core.Tags.TagItem"/> rows, capped at 10.
/// The client (the <c>client/lib</c> tag-suggest dropdown, U8) renders
/// <see cref="Kumunita.Core.Tags.TagItem.DisplayedName"/> and submits the
/// <see cref="Kumunita.Core.Tags.Tag.Slug"/> (the write seam derives the
/// <c>Slug</c> from the typed string, C-TG·4).
/// </summary>
public sealed class TagSuggestViewModel
{
    /// <summary>The matched suggestions (≤ 10, the C-TG·2 cap).</summary>
    public IReadOnlyList<TagItem> Suggestions { get; set; } = [];
}
