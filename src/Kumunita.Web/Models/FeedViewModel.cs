using Kumunita.Core.Posts;

namespace Kumunita.Web.Models;

/// <summary>
/// The community feed surface (M3, plan U7) — the <b>list</b> for
/// <c>/community/{componentId}</c>. A *projection* of
/// <c>PostService.ListFeedAsync</c>'s <see cref="FeedResult"/> — never a
/// re-derivation of access: the audience decision is already done at the Core
/// layer (the single <c>CanSeeAsync</c> over the component's candidate set,
/// one aggregate <see cref="Kumunita.Core.Authorization.AccessAudit"/> row;
/// invariant C-M3·3). <see cref="Items"/> carries only the allowed posts; a
/// hidden post's <c>Body</c>/<c>AuthorId</c> never reach this model (F1/F2 —
/// the M3 analog of M2's "never hidden-row fields" privacy pin).
/// <para>
/// <b>Component is a feed organizer, never a gate</b> (C-M3·2): the
/// <see cref="ComponentName"/> is the *label* the feed lands under (a display
/// lookup, not an access decision); the component's absence / disabled state
/// is enforced by the controller as a 404 precondition (the §2.3
/// candidate-filter table), not as a post-level gate.
/// <para>
/// <b>Author display name</b> (per <see cref="PostListItem.AuthorDisplayName"/>)
/// is a <b>read-only</b> <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/>
/// lookup (the M2 <see cref="Kumunita.Web.Controllers.GroupsController"/> N+1
/// display-name precedent — "a read, not a decision"). The audience *decision*
/// was already made by <c>ListFeedAsync</c>; this lookup never produces an
/// <c>AccessAudit</c> row for the author (C-M3·2: the candidate filter is not an
/// audit subject either).
/// </para>
/// </summary>
public sealed class FeedViewModel
{
    public string ComponentId { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public IReadOnlyList<PostListItem> Items { get; set; } = [];
    public int Total { get; set; }

    /// <summary>
    /// Whether the current viewer holds a posting right on the community
    /// this feed belongs to (or, for the all-sections feed, on *any*
    /// enabled community) — the same rule the POST gate in
    /// <see cref="Kumunita.Core.Posts.PostService.CreatePostAsync"/> enforces
    /// (GlobalAdmin bypass; a <see cref="Kumunita.Core.Identity.Roles.ModeratorComponent(string)"/>
    /// claim for this component; or a <c>ComponentMembership</c> row). The
    /// view hides the "Write a post" button when this is false rather than
    /// sending the user to a composer they'd only bounce back from.
    /// </summary>
    public bool CanPost { get; set; }

    /// <summary>
    /// The communities the current viewer has access to, as a navigable list
    /// of links to their individual feeds (<c>/community/{Id}</c>). The same
    /// reachable set that drives <see cref="CanPost"/> (membership ∪
    /// <see cref="Kumunita.Core.Identity.Roles.ModeratorComponent(string)"/>
    /// scope ∪ GlobalAdmin) — the directory never lists a community the
    /// viewer cannot reach. Listing it here is a *display* convenience so a
    /// viewer on either the single-community or the all-sections feed can hop
    /// between communities. On the single-community feed the entry for the
    /// current community (when reachable) is present in the list (rendered
    /// highlighted in the view); on the all-sections feed it is the full
    /// reachable directory.
    /// </summary>
    public IReadOnlyList<CommunityLink> Communities { get; set; } = [];
}

/// <summary>
/// One community entry in a <see cref="FeedViewModel.Communities"/> list —
/// the minimum pair needed to render a link: the component's
/// <c>Id</c> (the <c>/community/{id}</c> route value) and its
/// <c>Name</c> (the visible label). No <c>Enabled</c> /
/// <c>SortOrder</c> / other <see cref="Kumunita.Core.UserInfo.Component"/>
/// fields — the view only needs to draw an anchor, and the controller
/// already filtered to the enabled candidate set before projecting here.
/// </summary>
public sealed record CommunityLink(string Id, string Name);

/// <summary>
/// One visible feed row. The low-entropy projection: the <see cref="Post"/>'s
/// <c>Id</c> (for the detail link), <c>Title</c> (nullable), a
/// <see cref="BodyPreview"/> (truncated body, the list's one-line "what's it
/// about"), the <see cref="Post.Created"/> timestamp, and the author's
/// <see cref="AuthorDisplayName"/> (a <c>GetProfileAsync</c> read, not a
/// decision — see <see cref="FeedViewModel"/> doc). No <c>Audience</c>, no
/// <c>ComponentId</c> (the feed is already component-scoped; the component is
/// the feed's *label*, not a per-row gate), no email/phone/contact — never
/// any <see cref="Kumunita.Core.UserInfo.Profile"/>'s own fields beyond a
/// display name.
/// </summary>
public sealed record PostListItem(
    string Id,
    string? Title,
    string BodyPreview,
    DateTimeOffset Created,
    string AuthorDisplayName,
    /// <summary>The author's subject id (the <see cref="Post"/>'s
    /// <c>AuthorId</c>) — a display convenience: the row's avatar links the
    /// audited serving lane <c>GET /profile/avatar/{subjectId}</c> (the same
    /// "a read, not a decision" pin as <see cref="AuthorDisplayName"/>; the
    /// gate + audit run on the endpoint, never on this field).</summary>
    string AuthorSubjectId,
    string? ComponentName = null,
    string? ComponentId = null);
