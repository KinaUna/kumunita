namespace Kumunita.Core.Pages;

/// <summary>
/// A **user-added translation** of a <see cref="Page"/> into a language other
/// than the one it was authored in (ADR 0039; the ADR 0022/0026/0029 lane
/// carried over from <see cref="Posts.PostTranslation"/> /
/// <see cref="Announcements.AnnouncementTranslation"/>).
/// <para>
/// One row per (page, language) pair — the <c>(PageId, LanguageCode)</c>
/// unique index (<see cref="PageDocTypes.Configure"/>) enforces that at the
/// database layer (Marten's document identity is the surrogate
/// <see cref="Id"/>, the same <see cref="UserInfo.GroupMembership"/>
/// business-key convention as M1). The translation carries its own optional
/// <see cref="Title"/> (the page's <see cref="Page.Title"/> is the fallback
/// when this is absent) and <see cref="Body"/> (required).
/// </para>
/// <para>
/// **Standing (ADR 0040, amending ADR 0039 §3.7):** a translation is added
/// by a <b>GlobalAdmin</b> or a <b>Translator</b> (both an
/// <see cref="Authorization.AccessVia.Admin"/> audit tag — instance-wide, the
/// ADR 0021/0026 Translator standing) — on **either** page kind. A community
/// Moderator has no standing here (ADR 0040 retires the ADR 0039 §3.7
/// community-moderator lane). The decision + its
/// <see cref="Authorization.AccessAudit"/> row are written by
/// <c>AddTranslationAsync</c> (U03) in the caller's transaction (C3).
/// </para>
/// <para>
/// **Not an authorization surface (the ADR 0022 read-pin carried over):**
/// like its parent page, a translation has **no own audience** — its
/// visibility inherits the page's <see cref="Page.Audience"/> split. A
/// translation row that is not under a page the viewer may read is simply
/// unreachable (the Web reads it only after the page read returned the page).
/// </para>
/// <para>
/// **Add-only lane (ADR 0039, mirroring ADR 0022 / ADR 0026 / ADR 0029):**
/// there is no edit/delete seam here — re-adding a language overwrites that
/// row; the lane today is "add a translation of a language the page does not
/// yet have."
/// </para>
/// </summary>
public sealed class PageTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="Page"/> this translation renders.</summary>
    public string PageId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is <b>in</b> (a
    /// <see cref="Localization.LanguageCatalog.Id"/> — a supported, enabled
    /// language). Distinct from the page's authored-in
    /// <see cref="Page.LanguageCode"/>: that is the language the page was
    /// written in (the base); this is the language the translation renders it
    /// into.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated title (optional — the page's title is the
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
