namespace Kumunita.Core.Posts;

/// <summary>
/// A **user-added translation** of a <see cref="PostReply"/> into a language
/// other than the one the reply was authored in (ADR 0022; the reply-lane
/// counterpart of <see cref="PostTranslation"/> — the same "separate, later
/// feature" ADR 0018 deferred).
/// <para>
/// One row per (reply, language) pair — the
/// <c>(ReplyId, LanguageCode)</c> unique index
/// (<see cref="M3DocTypes.Configure"/>) enforces that at the database layer
/// (the <see cref="ComponentMembership"/> business-key convention). A reply
/// is a body-only construct (C-M3·1 — no title of its own), so its
/// translation carries <b>only</b> a <see cref="Body"/>.
/// <para>
/// **Standing (ADR 0022):** mirrors the parent post's — the reply's
/// <b>author</b> or, on the community lane, a <b>component moderator</b> (or a
/// GlobalAdmin); on the group lane, only the <b>author</b> or a GlobalAdmin.
/// The decision + <see cref="Kumunita.Core.Authorization.AccessAudit"/> row are
/// written by <see cref="PostService.AddReplyTranslationAsync"/> in the
/// caller's transaction (C3).
/// <para>
/// **Not an authorization surface (C-M3·1):** no own audience — the reply
/// (and hence this translation) inherits the parent post's single
/// <c>Read</c> decision. Add-only lane (no <see cref="PostReply.Modified"/>
/// analog; re-adding a language is a follow-up).
/// </para>
/// </summary>
public sealed class ReplyTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="PostReply"/> this translation renders.</summary>
    public string ReplyId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is
    /// <b>in</b> (a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>).
    /// Distinct from the reply's authored-in <see cref="PostReply.LanguageCode"/>.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated body (required).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>The subject id of the actor who added the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was added (the initial — and, on this
    /// add-only lane, only) timestamp.</summary>
    public DateTimeOffset Created { get; set; }
}
