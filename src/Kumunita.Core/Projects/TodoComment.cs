namespace Kumunita.Core.Projects;

/// <summary>
/// A comment (or a reply to another comment) on a <see cref="TodoItem"/>
/// (ADR 0100). **No <c>Audience</c> field** (invariant C-M3·1, carried from
/// the <see cref="Posts.PostReply"/> precedent): a comment's visibility
/// inherits its parent to-do's single <c>Read</c> decision — there is no
/// second authorization evaluation for the comment and the comment produces
/// **no** <c>Authorization.AccessAudit</c> row of its own on READ (the audit
/// rows it does carry are the write-lane rows, the C3 shape).
/// <para>
/// <see cref="ParentId"/> is the **sole hierarchy mechanism** (the
/// <see cref="TodoItem.ParentId"/> / C-M5·7 shape carried to the comment
/// lane): a <c>null</c> <see cref="ParentId"/> is a top-level comment on the
/// to-do; a non-null <see cref="ParentId"/> is a **reply** to another
/// <see cref="TodoComment"/>. Two levels at most (a reply to a reply is not
/// a distinct shape — a reply's parent is always a top-level comment).
/// </para>
/// <para>
/// <see cref="LanguageCode"/> is the ADR 0018 authored-in tag: written
/// **only** at create time (the <see cref="ProjectService
/// .CreateTodoCommentAsync"/> lane); materialized from the instance default
/// when the author leaves it unchosen, so no stored row is empty. It is a
/// **tag**, not a translation mechanism (ADR 0005 C unchanged).
/// </para>
/// <para>
/// <see cref="DeletedAt"/> is the ADR 0024 author soft-delete: set when the
/// **author** soft-deletes this comment (the <see cref="ProjectService
/// .DeleteTodoCommentAsync"/> lane); <c>null</c> while it is live. The
/// record is kept (never hard-deleted) and the detail view renders a
/// placeholder in place of the body — mirroring
/// <see cref="Posts.PostReply.DeletedAt"/>.
/// </para>
/// </summary>
public sealed class TodoComment
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The to-do this comment (or reply) is on (the parent surface —
    /// the comment's visibility inherits the to-do's single <c>Read</c>
    /// decision, C-M3·1).</summary>
    public string TodoId { get; set; } = string.Empty;

    /// <summary>The parent <see cref="TodoComment"/> id (the sole hierarchy
    /// mechanism — C-M5·7): <c>null</c> = a top-level comment on the to-do;
    /// non-null = a reply to that comment.</summary>
    public string? ParentId { get; set; }

    public string AuthorId { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }

    /// <summary>The BCP-47 code of the language this comment was **authored in**
    /// (ADR 0018) — its **own** tag, independent of the to-do's. Written only at
    /// create time; materialized from the instance default when unchosen.</summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>Set when the **author** soft-deletes this comment (ADR 0024);
    /// <c>null</c> while it is live. The record is kept and the detail view
    /// shows a placeholder in place of the body.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
