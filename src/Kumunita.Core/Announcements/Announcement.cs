namespace Kumunita.Core.Announcements;

/// <summary>
/// Who sees a platform-wide <see cref="Announcement"/> (as opposed to a community
/// post's per-user <see cref="Kumunita.Core.Authorization.Audience"/>):
/// <see cref="Public"/> is visible to <em>every</em> visitor — authenticated or
/// not (e.g. scheduled-maintenance notices); <see cref="Community"/> is visible to
/// every logged-in user (e.g. "help us with X" calls from a community team).
/// </summary>
/// <remarks>
/// The visibility split is deliberately flat (two fixed audiences) rather than a
/// per-resource <c>Audience</c>: announcements are platform announcements, not
/// audience-restricted content, so the audience-restriction machinery (and with
/// it the empty-audience-denies invariant) does not apply to them — the audience
/// is a compile-time role-shaped constant, enforced at the read gate, with no
/// per-user grant data to enumerate or audit.
/// </remarks>
public enum AnnouncementScope
{
    /// <summary>Visible to everyone, including unauthenticated visitors.</summary>
    Public,
    /// <summary>Visible to every signed-in user, regardless of verification or role.</summary>
    Community
}

/// <summary>
/// A platform-wide announcement (bounded context <c>Kumunita.Core.Announcements</c>).
/// Written by elevated role only (<see cref="Kumunita.Core.Identity.Roles.GlobalAdmin"/>
/// for both scopes; <see cref="Kumunita.Core.Identity.Roles.Moderator"/> restricted to
/// <see cref="AnnouncementScope.Community"/> — the moderation write lane, enforced at
/// the Web layer's <c>[Authorize(Roles=...)]</c> and re-checked by
/// <see cref="AnnouncementService.CreateAsync"/> against the author's role).
/// <see cref="AuthorId"/> is the acting user's <c>SubjectId</c> (the audit lane's
/// "who wrote this" — announcements are not per-user audience content, so there is
/// no owner/audience <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>
/// decision on the read path; visibility is the flat two-way <see cref="Scope"/> split).
/// <see cref="Modified"/> is null until the announcement is edited after creation.
/// </summary>
public sealed class Announcement
{
    public string Id { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>The fixed visibility audience for the announcement (see <see cref="AnnouncementScope"/>).</summary>
    public AnnouncementScope Scope { get; set; } = AnnouncementScope.Public;

    /// <summary>
    /// When <see cref="Scope"/> is <see cref="AnnouncementScope.Community"/> and this is set, the
    /// announcement is targeted at one community (a <see cref="Kumunita.Core.UserInfo.Component"/> by
    /// id) instead of every resident: only members and moderators of that community see it. When
    /// <see cref="Scope"/> is <see cref="AnnouncementScope.Public"/> this must stay <c>null</c>
    /// (public announcements are platform-wide). A targeted announcement may be authored only by
    /// a GlobalAdmin or a moderator of exactly that community (<c>moderator:{CommunityId}</c>).
    /// </summary>
    public string? CommunityId { get; set; }

    /// <summary>
    /// When true, this announcement is "pinned" — the Web layer may render it
    /// as a site-wide banner at the top of every page (including the home page)
    /// in addition to its place in the regular <c>/announcements</c> list.
    /// The visibility pin follows the same gate as <see cref="Scope"/>:
    /// a pinned <see cref="AnnouncementScope.Public"/> announcement is pinned
    /// for every visitor; a pinned <see cref="AnnouncementScope.Community"/>
    /// announcement is pinned only for signed-in users. The most-recently-created
    /// pinned announcement that passes the caller's auth gate is the one rendered
    /// (see <see cref="IAnnouncementService.PinnedAsync"/>).</summary>
    public bool Pinned { get; set; } = false;

    // ADR 0018 authored-in-language ADD (ADR 0004 §B.1 additive — the first
    // additive Announcement field on this POCO):
    /// <summary>
    /// The BCP-47 code of the language this announcement was **authored in**
    /// (ADR 0018, ADR 0005 B). Written at create time by
    /// <see cref="AnnouncementService.CreateAsync"/> and editable on the edit
    /// lane (<see cref="AnnouncementService.UpdateAsync"/> extends its
    /// changed-detection to this field) — the announcement edit surface is not
    /// ADR-frozen the way the post/reply edit lanes are. Materialized from the
    /// instance default (<see cref="Kumunita.Core.Localization.LocaleSettings.DefaultLanguageCode"/>,
    /// <c>en</c> floor) when the author leaves it unchosen, so no stored row is
    /// empty. **Not a translation mechanism** (ADR 0005 C unchanged — the body
    /// is never machine-translated by the platform); it is the tag a future
    /// search surface and any user-added translations key off.
    /// </summary>
    public string LanguageCode { get; set; } = string.Empty;

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // RC U03 content-image ADD (ADR 0004 §B.1 additive — the 2nd additive
    // Announcement field after ADR 0018's LanguageCode; the design doc's
    // "1st additive" ordinal is corrected to 2nd per the actual field order —
    // ADR 0018's LanguageCode is the 1st additive on this POCO, so this is the
    // 2nd; the ordinal is documentation, not behavior):
    /// <summary>
    /// The content images referenced by <see cref="Body"/> — the
    /// <c>MediaObject</c> ids appearing as <c>/content-image/{id}</c> links in
    /// the rendered body (RC R·3). Populated server-side by the owning write
    /// lane (RC U04/U05); the serving route's reverse lookup
    /// (<see cref="IAnnouncementService.FindByImageIdAsync"/>) reads this
    /// (RC R·4). The 2nd additive field after <see cref="LanguageCode"/>
    /// (ADR 0018) (ADR 0004 §B.1 — additive, delta-detected, idempotent, no
    /// seed reset; RC R·7).
    /// </summary>
    public IReadOnlyList<string> ImageIds { get; set; } = [];
    // ATT U3 file-attachment ADD (ADR 0034, ADR 0004 §B.1 additive — the 3rd
    // additive Announcement field after ADR 0018's LanguageCode, RC's ImageIds):
    /// <summary>
    /// The attachment file ids referenced by <see cref="Body"/> — the
    /// <c>MediaObject</c> ids appearing as <c>/attachment/{id}</c> links in the
    /// rendered body (C-ATT·1/2). Populated server-side by the owning write
    /// lane (ATT U5); the serving route's reverse lookup
    /// (<see cref="IAnnouncementService.FindByAttachmentIdAsync"/>) reads this
    /// (C-ATT·4). The 3rd additive field after <see cref="LanguageCode"/>
    /// (ADR 0018), <see cref="ImageIds"/> (RC) — **separate from**
    /// <see cref="ImageIds"/> (C-ATT·5; an announcement's images stay in
    /// <see cref="ImageIds"/>, its files in <see cref="AttachmentIds"/>)
    /// (ADR 0004 §B.1 — additive, delta-detected, idempotent, no seed reset;
    /// ADR 0034, C-ATT·5).
    /// </summary>
    public IReadOnlyList<string> AttachmentIds { get; set; } = [];}
