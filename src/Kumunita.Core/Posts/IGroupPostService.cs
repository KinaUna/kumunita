using Kumunita.Core.Posts;
using Marten;

namespace Kumunita.Core.Posts;

/// <summary>
/// The <c>group-post</c> lane (ADR 0013, C3-03) — the 4-method group-post
/// surface extracted from <see cref="PostService"/> as a real part-vs-whole
/// surface (the group-lane concern: ADR 0013 group posts + membership lane,
/// ADR 0016 author-only group-post edit, ADR 0037 draft-mode group posts,
/// ADR 0076 group-post notifications, ADR 0165 group-post mentions).
/// <para>
/// **What:** the group channel feed read (<see cref="ListGroupFeedAsync"/>),
/// the group post detail read (<see cref="GetGroupPostAsync"/>), the group
/// post create lane (<see cref="CreateGroupPostAsync"/>), and the group post
/// author-only edit lane (<see cref="UpdateGroupPostAsync"/>).
/// </para>
/// <para>
/// **When:** every group-post read and write (the <c>GroupsController</c>
/// surface, the <c>KioskItemCatalog</c> detail read). The
/// <see cref="PostService"/> no longer owns these 4 methods — the
/// <c>Posts</c> DI registration supplies the live instance.
/// </para>
/// <para>
/// **Comment rule** (what/when/throws + ADR refs): ADR 0013 (group posts +
/// membership lane — the <see cref="GroupPostDraft"/> shape, the
/// <see cref="Post.GroupId"/> non-empty pin, the
/// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>
/// <c>CanSeeGroupAsync</c> / <c>CanSeeGroupFeedAsync</c> decisions),
/// ADR 0016 (author-only group-post edit — the sole decision is
/// <c>Post.AuthorId == actorId</c>, no moderator / GlobalAdmin / break-glass),
/// ADR 0037 (draft-mode — the author-only gate on the group lane),
/// ADR 0076 (group-post notifications — per-member emission, the
/// <c>notification:group.post:</c> idempotency key shape),
/// ADR 0165 (group-post mentions — the <c>group.post.mention</c> lane, the
/// <see cref="Mentions.MentionTokens"/> extraction).
/// </para>
/// <para>
/// **No new boundary edge** (the seam is in the <c>Posts</c> module; the
/// controllers already reference <c>Posts</c> types — the gate's boundary
/// check does not rise). Core stays HTTP-free (ADR 0006-D).
/// </para>
/// </summary>
public interface IGroupPostService
{
    /// <summary>
    /// A group's channel feed (G1–G4 FACES, design doc §2.3(a)): the candidate
    /// set is the group's posts — <c>Post.GroupId == groupId</c>,
    /// <c>Created desc</c>, paged. Exactly one
    /// <see cref="Kumunita.Core.Authorization.IAuthorizationService.CanSeeGroupFeedAsync(string, string, int)"/>
    /// (the **standalone** form) writes the visit's **single aggregate**
    /// <c>AccessAudit</c> row (G·5). Allow ⇒ the paged candidates (G1);
    /// Deny ⇒ a <see cref="FeedResult"/> with an **empty** visible list and
    /// <see cref="FeedResult.HiddenCount"/> = the candidate count (G2).
    /// </summary>
    Task<FeedResult> ListGroupFeedAsync(string groupId, string actorId, int page,
        Query.SortSpec? sort = null, int? pageSize = null);

    /// <summary>
    /// A group post's detail + its one-level replies (G11 FACES, design doc
    /// §2.3(b)): the post is loaded first (the M3 fail-closed shape); a post
    /// with an **empty** <c>GroupId</c> or <c>GroupId != groupId</c> ⇒
    /// <c>Post = null</c>, no row (fail-closed). Otherwise **exactly one**
    /// <see cref="Kumunita.Core.Authorization.IAuthorizationService.CanSeeGroupAsync(string, string, string?)"/>
    /// (standalone) — the detail decision row (TargetId = postId, G·5).
    /// </summary>
    Task<PostDetailResult> GetGroupPostAsync(string groupId, string postId, string actorId);

    /// <summary>
    /// Creates a group post, in the **caller's** in-flight session (invariant
    /// C3). The **create gate is the group-lane decision** (G·3): one
    /// <see cref="Kumunita.Core.Authorization.IAuthorizationService.CanSeeGroupAsync(string, string, string?, IDocumentSession)"/>
    /// with <c>targetPostId: null</c>, in the caller's transaction.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The actor is not a member
    /// of <c>draft.GroupId</c> — thrown **after** the gate row is persisted.</exception>
    Task<Post> CreateGroupPostAsync(GroupPostDraft draft, string actorId, IDocumentSession session);

    /// <summary>
    /// Re-write a **group-lane** post's title and body, **author-only**
    /// (ADR 0016). Only the post's own <see cref="Post.AuthorId"/> may edit;
    /// there is **no** moderator, GlobalAdmin, or break-glass branch.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The post id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the post's author.</exception>
    Task<Post> UpdateGroupPostAsync(
        string postId,
        string actorId,
        string? title,
        string body,
        string? languageCode,
        IDocumentSession session,
        IReadOnlyList<string>? attachmentIds = null);
}
