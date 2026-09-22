namespace Kumunita.Core.Posts;

/// <summary>
/// A **user-added translation** of a <see cref="Post"/> into a language other
/// than the one it was authored in (ADR 0022; the "separate, later feature"
/// that ADR 0018's authored-in tag explicitly deferred, and that ADR 0005 §C
/// kept out of scope as *user-authored* rather than *machine-translated*).
/// <para>
/// One row per (post, language) pair — the
/// <c>(PostId, LanguageCode)</c> unique index
/// (<see cref="M3DocTypes.Configure"/>) enforces that at the database layer
/// (Marten's document identity is the surrogate <c>Id</c>, the same
/// <see cref="ComponentMembership"/> business-key convention as M1). The
/// translation carries its own <see cref="Title"/> (optional — the base post's
/// <see cref="Post.Title"/> is the fallback when absent) and
/// <see cref="Body"/> (required).
/// <para>
/// **Standing (ADR 0022):** a translation is added by the post's
/// <b>author</b> or, on the community lane, a <b>component moderator</b> (or a
/// GlobalAdmin); on the group lane, only the <b>author</b> or a GlobalAdmin
/// (the group lane has no component-moderator standing, ADR 0007). The
/// decision + its <see cref="Kumunita.Core.Authorization.AccessAudit"/> row are
/// written by <see cref="PostService.AddPostTranslationAsync"/> in the
/// caller's transaction (C3).
/// <para>
/// **Not an authorization surface (C-M3·1 carried over):** like its parent
/// post, a translation has <b>no own audience</b> — its visibility inherits the
/// post's single <c>Read</c> decision. A translation row that is not under a
/// post the viewer may read is simply unreachable (the Web reads it only after
/// <see cref="PostService.GetPostAsync"/> returned the post).
/// <para>
/// Add-only lane (ADR 0022): there is no <see cref="Post.Modified"/> analog
/// here — re-adding a language is not yet a lane (a follow-up); the lane today
/// is "add a translation of a language the post does not yet have."
/// </para>
/// </summary>
public sealed class PostTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="Post"/> this translation renders.</summary>
    public string PostId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is
    /// <b>in</b> (a <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/>
    /// — a supported, enabled language). Distinct from the post's authored-in
    /// <see cref="Post.LanguageCode"/>: that is the language the post was
    /// written in (the base); this is the language the translation renders it
    /// into.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated title (optional — the base post's title is the
    /// fallback when this is absent).</summary>
    public string? Title { get; set; }

    /// <summary>The translated body (required).</summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>The subject id of the actor who added the translation.</summary>
    public string AuthorId { get; set; } = string.Empty;

    /// <summary>When the translation was added (the initial — and, on this
    /// add-only lane, only) timestamp.</summary>
    public DateTimeOffset Created { get; set; }
}
