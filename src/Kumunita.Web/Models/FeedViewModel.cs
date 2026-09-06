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
}

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
    string? ComponentName = null);
