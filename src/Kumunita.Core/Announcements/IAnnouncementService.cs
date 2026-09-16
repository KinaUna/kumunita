using Kumunita.Core.Identity;
using Marten;

namespace Kumunita.Core.Announcements;

/// <summary>
/// The <c>/announcements</c> bounded-context's service seam (M3b — the "platform
/// announcements" lane, part of M3's roadmap scope). The public surface of <see cref="AnnouncementService"/>:
/// the <see cref="ListVisibleAsync"/> / <see cref="GetAsync"/> /
/// <see cref="CreateAsync"/> / <see cref="UpdateAsync"/> /
/// <see cref="DeleteAsync"/> surface.
/// <para>
/// Kept behind an interface so the Web-side consumer (the
/// <see cref="Kumunita.Web.Controllers.AnnouncementController"/>) can be tested
/// without a live Postgres: the real implementation opens
/// <c>IQuerySession</c>/<c>LightweightSession</c> against the store, and a test
/// double (NSubstitute) returns a canned list / throws the seam's contract
/// exception (<see cref="UnauthorizedAccessException"/> on a denied scope-vs-role
/// split, <see cref="KeyNotFoundException"/> on a missing delete id). This mirrors
/// the repo's existing service-seam convention (<see cref="IUserInfoService"/>,
/// <see cref="IAuthorizationService"/>, <see cref="IEmailDeadLetterCounter"/> —
/// the last one being the closest analog: a store-composing service that a Web
/// controller test must substitute).
/// </para>
/// </summary>
public interface IAnnouncementService
{
    /// <summary>
    /// The set of <see cref="Announcement"/>s visible at the caller's
    /// authentication state: <see cref="AnnouncementScope.Public"/> always,
    /// <see cref="AnnouncementScope.Community"/> when signed in.
    /// Sorted by <c>Created</c> descending (latest first); no
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row (announcements
    /// are not audience-restricted). See <see cref="AnnouncementService.ListVisibleAsync"/>
    /// for the full contract.
    /// </summary>
    Task<IReadOnlyList<Announcement>> ListVisibleAsync(string? actorId, IReadOnlySet<string> roles);

    /// <summary>
    /// A single caller-visible <see cref="Announcement"/> by id — the
    /// <c>/announcements/{id}</c> detail view's read shape. The visibility
    /// gate is the same as <see cref="ListVisibleAsync"/>:
    /// <see cref="AnnouncementScope.Public"/> always;
    /// <see cref="AnnouncementScope.Community"/> when signed in (community-
    /// targeted rows: that community's moderator, its members, or a
    /// <see cref="Roles.GlobalAdmin"/>). Returns null when the id is missing
    /// <em>or</em> not visible to the caller (announcements are not
    /// audience-restricted content, so no
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row — the Web
    /// layer maps both to a 404). See <see cref="AnnouncementService.GetAsync"/>
    /// for the full contract.
    /// </summary>
    Task<Announcement?> GetAsync(string id, string? actorId, IReadOnlySet<string> roles);

    /// <summary>
    /// The single announcement to render as a site-wide banner
    /// most-recently-created announcement with <see cref="Announcement.Pinned"/>
    /// true that passes the caller's authentication state (the same
    /// <see cref="ListVisibleAsync"/> visibility gate: <see cref="AnnouncementScope.Public"/>
    /// always; <see cref="AnnouncementScope.Community"/> only when
    /// signed in). Returns null when no pinned
    /// announcement passes the gate (the Web layer skips the banner in
    /// that case). No <see cref="Kumunita.Core.Authorization.AccessAudit"/> row.
    /// See <see cref="AnnouncementService.PinnedAsync"/> for the full
    /// contract.
    /// </summary>
    Task<Announcement?> PinnedAsync(string? actorId, IReadOnlySet<string> roles);

    /// <summary>
    /// Creates an <see cref="Announcement"/> in the <b>caller's</b> in-flight
    /// session (invariant C3). Enforces the scope-vs-role split — a
    /// <see cref="Roles.GlobalAdmin"/> may author either scope; a
    /// <see cref="Roles.Moderator"/> may author
    /// <see cref="AnnouncementScope.Community"/> only. A denied split is a hard
    /// <see cref="UnauthorizedAccessException"/> (the Web layer maps that to a
    /// 403). See <see cref="AnnouncementService.CreateAsync"/> for the full
    /// contract.
    /// </summary>
    Task<Announcement> CreateAsync(
        Announcement announcement,
        string actorId,
        IReadOnlySet<string> authorRoles,
        IDocumentSession session);

    /// <summary>
    /// Edits an existing <see cref="Announcement"/> in the <b>caller's</b>
    /// in-flight session (invariant C3). The edit gate is <em>distinct</em>
    /// from <see cref="CreateAsync"/>'s scope-vs-role split and is evaluated
    /// against the <em>stored</em> row: a <see cref="Roles.GlobalAdmin"/> may
    /// edit any row; a community moderator may edit a
    /// <em>community-targeted</em> row they moderate; but a flat "all
    /// residents" row (Community scope, no <c>CommunityId</c>) is editable
    /// only by its <see cref="Announcement.AuthorId"/> or a
    /// <see cref="Roles.GlobalAdmin"/> — a moderator who did not author it is
    /// denied (ADR 0017). A denied actor is a hard
    /// <see cref="UnauthorizedAccessException"/> (the Web layer maps that to a
    /// 403); a missing id is a <see cref="KeyNotFoundException"/> (the Web
    /// layer maps that to a 404). <c>AuthorId</c>/<c>Created</c> are preserved
    /// untouched (the author of record is who created it, not who last edited
    /// it); <see cref="Announcement.Modified"/> is stamped when the edit
    /// actually changes Title/Body/Scope. See
    /// <see cref="AnnouncementService.UpdateAsync"/> for the full contract.
    /// </summary>
    Task<Announcement> UpdateAsync(
        Announcement updated,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session);
    /// <summary>
    /// <b>Reverse-lookup</b> read seam (RC U03, R·5): the first
    /// <see cref="Announcement"/> (by <c>Created</c> ascending) whose
    /// <see cref="Announcement.ImageIds"/> contains <paramref name="mediaId"/>
    /// — the serving route's owner resolution. **Un-audited** (RC R·5); null
    /// when no announcement references the id (the route 404s). See
    /// <see cref="AnnouncementService.FindByImageIdAsync"/> for the full
    /// contract.
    /// </summary>
    Task<Announcement?> FindByImageIdAsync(string mediaId);
    /// <summary>
    /// <b>Reverse-lookup</b> read seam (ATT U3, C-ATT·4): the first
    /// <see cref="Announcement"/> (by <c>Created</c> ascending) whose
    /// <see cref="Announcement.AttachmentIds"/> contains <paramref name="mediaId"/>
    /// — the attachment serving route's owner resolution. **Un-audited**;
    /// announcements are not audience-restricted, so there is no
    /// <c>AccessAudit</c> lane on this bounded context. Null when no
    /// announcement references the id (the route 404s). See
    /// <see cref="AnnouncementService.FindByAttachmentIdAsync"/> for the full
    /// contract.
    /// </summary>
    Task<Announcement?> FindByAttachmentIdAsync(string mediaId);
    /// <summary>
    /// Deletes an <see cref="Announcement"/> in the <b>caller's</b> in-flight
    /// session (invariant C3). Hard delete (no soft-hidden state). A missing id
    /// is a <see cref="KeyNotFoundException"/> (the Web layer maps that to a 404).
    /// See <see cref="AnnouncementService.DeleteAsync"/> for the full contract.
    /// </summary>
    Task DeleteAsync(string announcementId, IDocumentSession session);

    /// <summary>
    /// The announcement's user-added translations (ADR 0029): the
    /// <see cref="AnnouncementTranslation"/> rows under
    /// <paramref name="announcementId"/>, ordered by <c>LanguageCode</c>.
    /// A "a read, not a decision" surface — no
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> row (the
    /// visibility gate already ran in <see cref="GetAsync"/>). See
    /// <see cref="AnnouncementService.GetAnnouncementTranslationsAsync"/>
    /// for the full contract.
    /// </summary>
    Task<IReadOnlyList<AnnouncementTranslation>> GetAnnouncementTranslationsAsync(string announcementId);

    /// <summary>
    /// Adds a **user-added translation** of an announcement in the
    /// <b>caller's</b> in-flight session (invariant C3). Standing
    /// (ADR 0029): a <see cref="Roles.GlobalAdmin"/> or
    /// <see cref="Roles.Translator"/> (both
    /// <see cref="Kumunita.Core.Authorization.AccessVia.Admin"/>); and —
    /// for a <see cref="AnnouncementScope.Community"/> announcement
    /// <em>targeted</em> at one community — a <see cref="Roles.Moderator"/>
    /// scoped to that community
    /// (<see cref="Kumunita.Core.Authorization.AccessVia.Moderator"/>). A
    /// flat community or a <see cref="AnnouncementScope.Public"/>
    /// announcement has no community to moderate, so the component-moderator
    /// standing does not qualify. A denied actor is a hard
    /// <see cref="UnauthorizedAccessException"/> (the Web layer maps that to a
    /// 403); a missing id is a <see cref="KeyNotFoundException"/> (mapped to
    /// a 404). The new row + its <c>AccessAudit</c> row commit atomically.
    /// See <see cref="AnnouncementService.AddAnnouncementTranslationAsync"/>
    /// for the full contract.
    /// </summary>
    Task<AnnouncementTranslation> AddAnnouncementTranslationAsync(
        string announcementId,
        string languageCode,
        string? title,
        string body,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session);
}
