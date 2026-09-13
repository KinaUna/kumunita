namespace Kumunita.Core.Posts;

/// <summary>
/// A one-level reply to a post (M3). **No <c>Audience</c> field** (invariant
/// C-M3·1): a reply's visibility inherits its parent post's single <c>Read</c>
/// decision — there is no second authorization evaluation for the reply and
/// the reply produces **no** <c>Authorization.AccessAudit</c> row of its own.
/// <para>
/// <see cref="Modified"/> is the ADR 0016 reply-edit-lane ADD (ADR 0004 §B.1
/// additive — delta-detected, idempotent): written **only** by
/// <see cref="PostService.UpdateReplyAsync"/>, mirroring <c>Post.Modified</c>.
/// </para>
/// </summary>
public sealed class PostReply
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }

    // ADR 0016 reply-edit ADD (ADR 0004 §B.1 additive; the single new PostReply
    // field after M3's Created):
    /// <summary>
    /// The reply's last edit time (ADR 0016). Written **only** by the
    /// author-only <see cref="PostService.UpdateReplyAsync"/> lane, mirroring
    /// <c>Post.Modified</c>. Null until first edited (the reply's
    /// <see cref="Created"/> is the initial timestamp).
    /// </summary>
    public DateTimeOffset? Modified { get; set; }
}
