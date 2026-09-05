namespace Kumunita.Core.Posts;

/// <summary>
/// A one-level reply to a post (M3). **No <c>Audience</c> field** (invariant
/// C-M3·1): a reply's visibility inherits its parent post's single <c>Read</c>
/// decision — there is no second authorization evaluation for the reply and
/// the reply produces **no** <c>Authorization.AccessAudit</c> row of its own.
/// </summary>
public sealed class PostReply
{
    public string Id { get; set; } = string.Empty;
    public string PostId { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset Created { get; set; }
}
