using Kumunita.Core.Posts;

namespace Kumunita.Web.Models;

/// <summary>
/// The post detail surface (M3, plan U7) — <c>/posts/{id}</c> + its
/// one-level reply list. A *projection* of
/// <c>PostService.GetPostAsync</c>'s <see cref="PostDetailResult"/> — the
/// single decision row (invariant C-M3·3; one <c>CanAsync</c> call per visit,
/// C6) has already been written at the Core layer, and the
/// <see cref="Replies"/> list is the **already-authorized** one-level set
/// returned *as-is* under the parent post's single <c>Read</c> decision
/// (invariant C-M3·1). The controller does **no** re-check on a reply (the
/// C-M3·1 "no second <c>Can*Async</c> on the reply" pin) and surfaces
/// <see cref="Post"/>/ <see cref="AuthorDisplayName"/>/ <see
/// cref="ReplyItem.AuthorDisplayName"/> as display names (each a
/// <see cref="Kumunita.Core.UserInfo.IUserInfoService.GetProfileAsync"/> read,
/// not a decision — the M2 <see cref="Kumunita.Web.Controllers.GroupsController"/>
/// display-name precedent).
/// <para>
/// A <b>Denied</b> or <b>missing</b> post is mapped by the controller to a
/// 403 / 404 (the §2.3 candidate-filter shape) before this model is built;
/// the view never receives a <see cref="PostDetailViewModel"/> for a post the
/// viewer may not read. The model itself therefore never carries a
/// "hidden" sentinel — the two fail-closed cases are controller responses,
/// not view-model states (the M2 <see cref="Kumunita.Web.Models.DirectoryViewModel.Detail"/>
/// §9 analog: the view has *no channel* to render a denied post).
/// </para>
/// </summary>
public sealed class PostDetailViewModel
{
    public Post Post { get; set; } = null!;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public IReadOnlyList<ReplyItem> Replies { get; set; } = [];
    public bool IsAuthor { get; set; }
}

/// <summary>
/// One visible reply row. The low-entropy projection: the
/// <see cref="Kumunita.Core.Posts.PostReply"/>'s <c>Id</c>, a
/// <see cref="AuthorDisplayName"/> (a <c>GetProfileAsync</c> read — the same
/// "a read, not a decision" pin as the parent post's), the reply's
/// <see cref="Kumunita.Core.Posts.PostReply.Body"/> (verbatim — no
/// truncation for the detail surface; the list's preview truncation is a
/// list-surface concern), and the <see cref="Kumunita.Core.Posts.PostReply.Created"/>
/// timestamp. No <c>Audience</c> of its own (C-M3·1: a reply has *no* own
/// audience field); a <c>Reply</c> inherits visibility from its parent
/// post's single <c>Read</c> decision, not from a second authorization call.
/// </summary>
public sealed record ReplyItem(
    string Id,
    string AuthorDisplayName,
    string Body,
    DateTimeOffset Created);
