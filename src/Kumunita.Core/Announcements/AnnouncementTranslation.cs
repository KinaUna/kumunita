namespace Kumunita.Core.Announcements;

/// <summary>
/// A **user-added translation** of a <see cref="Announcement"/> into a language
/// other than the one it was authored in (ADR 0029; the same
/// user-authored-not-machine-translated lane ADR 0022 shipped for posts/replies
/// and ADR 0026 for group/community names, carried over to the
/// <c>Kumunita.Core.Announcements</c> bounded context).
/// <para>
/// One row per (announcement, language) pair — the
/// <c>(AnnouncementId, LanguageCode)</c> unique index
/// (<see cref="M3DocTypes.Configure"/>) enforces that at the database layer
/// (Marten's document identity is the surrogate <see cref="Id"/>, the same
/// <see cref="Kumunita.Core.UserInfo.GroupMembership"/> business-key
/// convention as M1). The translation carries its own optional
/// <see cref="Title"/> (the announcement's <see cref="Announcement.Title"/> is
/// the fallback when this is absent) and <see cref="Body"/> (required).
/// <para>
/// **Standing (ADR 0029):** a translation is added by a
/// <b>GlobalAdmin</b> or a <b>Translator</b> (both an
/// <see cref="Authorization.AccessVia.Admin"/> audit tag — instance-wide,
/// the ADR 0021/0026 Translator standing), and — for a
/// <see cref="AnnouncementScope.Community"/> announcement
/// <em>targeted</em> at one community — by a
/// <see cref="Kumunita.Core.Identity.Roles.Moderator"/> scoped to that
/// community (an <see cref="Authorization.AccessVia.Moderator"/> audit tag).
/// A flat community-scope announcement (no <see cref="Announcement
/// .CommunityId"/>) and a <see cref="AnnouncementScope.Public"/> announcement
/// have no community to moderate, so the component-moderator standing does
/// not qualify for them (a moderator governs a community's members, ADR
/// 0012, not a platform-wide notice). The decision + its
/// <see cref="Authorization.AccessAudit"/> row are written by
/// <see cref="AnnouncementService.AddAnnouncementTranslationAsync"/> in the
/// caller's transaction (C3).
/// <para>
/// **Not an authorization surface (the ADR 0022 read-pin carried over):** like
/// its parent announcement, a translation has <b>no own audience</b> — its
/// visibility inherits the announcement's flat two-way
/// <see cref="AnnouncementScope"/> split. A translation row that is not under
/// an announcement the viewer may read is simply unreachable (the Web reads
/// it only after <see cref="AnnouncementService.GetAsync"/> returned the
/// announcement).
/// <para>
/// Add-only lane (ADR 0029, mirroring ADR 0022 / ADR 0026): there is no
/// edit/delete seam here — re-adding a language overwrites that row; the lane
/// today is "add a translation of a language the announcement does not yet
/// have."
/// </para>
/// </summary>
public sealed class AnnouncementTranslation
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The parent <see cref="Announcement"/> this translation renders.</summary>
    public string AnnouncementId { get; set; } = string.Empty;

    /// <summary>
    /// The BCP-47 code of the language this translation is <b>in</b> (a
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog.Id"/> — a
    /// supported, enabled language). Distinct from the announcement's
    /// authored-in <see cref="Announcement.LanguageCode"/>: that is the
    /// language the announcement was written in (the base); this is the
    /// language the translation renders it into.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    /// <summary>The translated title (optional — the announcement's title is the
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
