namespace Kumunita.Core.Announcements;

/// <summary>
/// A resident's comment on a platform <see cref="Announcement"/> (ADR 0101).
/// **No <c>Audience</c> field of its own:** a comment's visibility is exactly
/// the announcement's — it is read only under the announcement it belongs to,
/// after the announcement's flat <see cref="AnnouncementScope"/> read gate has
/// already run, so there is no second authorization evaluation for the comment
/// and **no** <c>Authorization.AccessAudit</c> row of its own on READ (the
/// audit rows it does carry are the write-lane rows, the C3 shape — the
/// <see cref="TodoComment"/> C-M3·1 precedent, carried to the Announcements
/// lane with the flat scope split standing in for the to-do's <c>CanAsync
/// (Read)</c> decision).
/// <para>
/// <b>Signed-in only, admin-toggle-gated (ADR 0101).</b> Unlike the to-do
/// comment lane (any resident who can see the to-do may comment), an
/// announcement comment is a **new** capability that an admin may switch on or
/// off instance-wide (<see cref="Kumunita.Core.Localization.LocaleSettings
/// .AnnouncementCommentsEnabled"/>): when the toggle is off, no signed-in user
/// may comment and the comment list is hidden. When it is on, commenting and
/// reading the list require the actor to be **signed in** (a visitor can never
/// comment, and cannot even see the comments), and the comment is visible
/// under the announcement's own flat scope split — so even a
/// <see cref="AnnouncementScope.Public"/> announcement's comments are visible
/// to signed-in users only.
/// </para>
/// <para>
/// <see cref="LanguageCode"/> is the ADR 0018 authored-in tag: written
/// **only** at create time (the <see cref="AnnouncementService
/// .CreateAnnouncementCommentAsync"/> lane); materialized from the instance
/// default when the author leaves it unchosen, so no stored row is empty. It
/// is a **tag**, not a translation mechanism (ADR 0005 C unchanged).
/// </para>
/// <para>
/// <see cref="DeletedAt"/> is the ADR 0024 author soft-delete: set when the
/// **author** soft-deletes this comment (the <see cref="AnnouncementService
/// .DeleteAnnouncementCommentAsync"/> lane); <c>null</c> while it is live.
/// The record is kept (never hard-deleted) and the detail view renders a
/// placeholder in place of the body — mirroring
/// <see cref="Kumunita.Core.Posts.PostReply.DeletedAt"/>.
/// </para>
/// <para>
/// <b>No <c>ParentId</c></b> (ADR 0101 scope): a comment on an announcement is
/// top-level only — the ask was "let residents comment on announcements," not
/// "reply to a discussion," so the sole-hierarchy mechanism the to-do lane
/// carries (C-M5·7) is deliberately absent here.
/// </para>
/// </summary>
public sealed class AnnouncementComment
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The announcement this comment is on (the parent surface — the
    /// comment's visibility inherits the announcement's flat
    /// <see cref="AnnouncementScope"/> read decision).</summary>
    public string AnnouncementId { get; set; } = string.Empty;

    public string AuthorId { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }

    /// <summary>The BCP-47 code of the language this comment was **authored in**
    /// (ADR 0018) — its **own** tag, independent of the announcement's. Written
    /// only at create time; materialized from the instance default when
    /// unchosen.</summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>Set when the **author** soft-deletes this comment (ADR 0024);
    /// <c>null</c> while it is live. The record is kept and the detail view
    /// shows a placeholder in place of the body.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
