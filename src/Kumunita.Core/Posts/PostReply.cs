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

    // ADR 0018 authored-in-language ADD (ADR 0004 §B.1 additive — the second
    // additive PostReply field after ADR 0016's Modified):
    /// <summary>
    /// The BCP-47 code of the language this reply was **authored in** (ADR 0018,
    /// ADR 0005 B) — its **own** tag, independent of the parent post's. Written
    /// **only** at create time (<see cref="PostService.CreateReplyAsync"/>); the
    /// ADR 0016 reply-edit lane (body-only) deliberately does **not** touch it.
    /// Materialized from the instance default when the replier leaves it
    /// unchosen, so no stored row is empty. **Not a translation mechanism**
    /// (ADR 0005 C unchanged); it is the tag a future search surface and any
    /// user-added translations key off.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    // ADR 0024 author soft-delete ADD (ADR 0004 §B.1 additive — the third
    // additive PostReply field after ADR 0016's Modified and ADR 0018's
    // LanguageCode):
    /// <summary>
    /// Set when the <b>author</b> soft-deletes this reply (ADR 0024);
    /// <c>null</c> while it is live. The record is kept (never hard-deleted)
    /// and the detail view shows a placeholder in place of the body — but it
    /// still counts as a reply (the parent's "reply count" stays honest). A
    /// reply soft-deleted under a live post stays visible (it is its own
    /// document, inheriting the parent's <c>Read</c> decision); when its
    /// parent post is soft-deleted the whole thread leaves the feeds, and
    /// this field is the author's own action on the reply. Additive — Marten's
    /// schema builder picks up the new field on the existing doc-type surface
    /// (ADR 0004 §B.1); no seed reset, no schema-file change.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    // RC U03 content-image ADD (ADR 0004 §B.1 additive — the 4th additive
    // PostReply field after ADR 0016's Modified, ADR 0018's LanguageCode,
    // ADR 0024's DeletedAt):
    /// <summary>
    /// The content images referenced by <see cref="Body"/> — the
    /// <c>MediaObject</c> ids appearing as <c>/content-image/{id}</c> links in
    /// the rendered body (RC R·3). Populated server-side by the owning write
    /// lane (RC U04/U05); the serving route's reverse lookup
    /// (<see cref="PostService.FindReplyByImageIdAsync"/>) reads this (RC R·4).
    /// The 4th additive field after <see cref="Modified"/> (ADR 0016),
    /// <see cref="LanguageCode"/> (ADR 0018), <see cref="DeletedAt"/> (ADR 0024)
    /// (ADR 0004 §B.1 — additive, delta-detected, idempotent, no seed reset;
    /// RC R·7).
    /// </summary>
    public IReadOnlyList<string> ImageIds { get; set; } = [];
}
