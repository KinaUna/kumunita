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
    /// The <see cref="ListVisibleAsync"/> feed, **paged** (ADR 0090 D6, M7
    /// U01) — the same in-memory visibility filter, the same
    /// <c>Created</c> descending order, then a
    /// <c>Skip((page - 1) * PageSize).Take(PageSize)</c> window
    /// (<c>PageSize = 30</c>, the D4 shape). <see cref="AnnouncementPage.HasMore"/>
    /// is the sole paging signal (D1 — <c>pageCount == PageSize</c>); there is
    /// no <c>Total</c> and **no** <see cref="Kumunita.Core.Authorization.AccessAudit"/>
    /// row (announcements have no audit lane — the
    /// <see cref="ListVisibleAsync"/> pin; C-M7·1 vacuously satisfied). The
    /// non-paged <see cref="ListVisibleAsync"/> is unmodified (the banner +
    /// admin surfaces keep the whole-list read). See
    /// <see cref="AnnouncementService.ListVisiblePagedAsync"/> for the full
    /// contract.
    /// </summary>
    Task<AnnouncementPage> ListVisiblePagedAsync(string? actorId, IReadOnlySet<string> roles, int page, CancellationToken ct = default);

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
    /// Publishes a draft announcement in the <b>caller's</b> in-flight session
    /// (invariant C3) — clears <see cref="Announcement.IsDraft"/> so it becomes
    /// visible under its normal <see cref="AnnouncementScope"/> split
    /// (ADR 0037). **Author-only**: a non-author (even a
    /// <see cref="Roles.GlobalAdmin"/>) is a hard
    /// <see cref="UnauthorizedAccessException"/> (the Web layer maps that to a
    /// 403); a missing id is a <see cref="KeyNotFoundException"/> (the Web
    /// layer maps that to a 404). Idempotent — a second publish on an
    /// already-live announcement is a no-op (it does not stamp
    /// <see cref="Announcement.Modified"/>). See
    /// <see cref="AnnouncementService.PublishAsync"/> for the full contract.
    /// </summary>
    Task<Announcement> PublishAsync(string announcementId, string actorId, IDocumentSession session);

    /// <summary>
    /// The actor's own draft announcements (ADR 0037): the
    /// <see cref="Announcement"/>s with <see cref="Announcement.IsDraft"/> true
    /// and <see cref="Announcement.AuthorId"/> == <paramref name="actorId"/>,
    /// sorted by <c>Created</c> descending. The "My drafts" discoverability
    /// surface — <see cref="ListVisibleAsync"/> deliberately excludes drafts.
    /// Not an authorization surface (announcements have no
    /// <see cref="Kumunita.Core.Authorization.AccessAudit"/> lane; the
    /// <c>AuthorId == actorId</c> match in the query is the sole decision —
    /// ADR 0037's author-only pin). See
    /// <see cref="AnnouncementService.ListMyDraftsAsync"/> for the full
    /// contract.
    /// </summary>
    Task<IReadOnlyList<Announcement>> ListMyDraftsAsync(string actorId);

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
    /// <summary>
    /// **Updates** an existing user-added translation of an announcement (ADR
    /// 0048). Standing is the same as <see cref="AddAnnouncementTranslationAsync"/>;
    /// a denied actor is a <see cref="UnauthorizedAccessException"/>; a missing
    /// id or row is a <see cref="KeyNotFoundException"/>. One
    /// <c>SaveChangesAsync</c>; a hand-written <c>AccessAudit</c> row (action
    /// <c>announcementtranslation.update</c>) is stored in the caller's session.
    /// </summary>
    Task<AnnouncementTranslation> UpdateAnnouncementTranslationAsync(
        string announcementId,
        string languageCode,
        string? title,
        string body,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session);

    /// <summary>
    /// **Removes** an existing user-added translation of an announcement (ADR
    /// 0048). Standing is the same as <see cref="AddAnnouncementTranslationAsync"/>;
    /// a denied actor is a <see cref="UnauthorizedAccessException"/>; a missing
    /// id or row is a <see cref="KeyNotFoundException"/>. One
    /// <c>SaveChangesAsync</c>; a hand-written <c>AccessAudit</c> row (action
    /// <c>announcementtranslation.remove</c>) is stored in the caller's session.
    /// </summary>
    Task RemoveAnnouncementTranslationAsync(
        string announcementId,
        string languageCode,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session);

    // ── ADR 0101 — resident comments on an announcement (top-level only) ──

    /// <summary>
    /// Whether the signed-in residents may comment on platform announcements
    /// (ADR 0101) — the <c>true</c> floor on
    /// <see cref="Kumunita.Core.Localization.LocaleSettings
    /// .AnnouncementCommentsEnabled"/> (a missing singleton row reads as
    /// <c>true</c>). A read seam only; the write lane is
    /// <see cref="SetAnnouncementCommentsEnabledAsync"/>.
    /// </summary>
    Task<bool> AreAnnouncementCommentsEnabledAsync();

    /// <summary>
    /// Sets the ADR 0101 announcement-comments gate (the
    /// <see cref="Kumunita.Core.Identity.IIdentityService.SetSignupOpenAsync"/>
    /// singleton-toggle shape): reads-or-mints the
    /// <see cref="Kumunita.Core.Localization.LocaleSettings"/> singleton, sets
    /// <see cref="Kumunita.Core.Localization.LocaleSettings
    /// .AnnouncementCommentsEnabled"/>, and commits exactly one
    /// <see cref="Authorization.AccessAudit"/> row
    /// (<c>announcementcomments.set-enabled</c>, <c>Via = Admin</c>,
    /// <c>TargetKind = "announcementcomments"</c>) in the same session
    /// (C3). A GlobalAdmin-only write surface (the ASP.NET gate narrows the
    /// actor; the service is the single audited write lane).
    /// </summary>
    Task SetAnnouncementCommentsEnabledAsync(bool enabled, string actorId);

    /// <summary>
    /// The announcement's resident comments (ADR 0101): the
    /// <see cref="AnnouncementComment"/> rows under
    /// <paramref name="announcementId"/>, ordered by <c>Created</c> ascending,
    /// including soft-deleted rows so the view can render a placeholder in
    /// place of the body. **Read gate (ADR 0101):** the caller must be
    /// **signed in** (an anonymous visitor is refused — <see
    /// cref="UnauthorizedAccessException"/> / 403) and must be able to see the
    /// announcement under its own flat <see cref="AnnouncementScope"/> split
    /// (the same <see cref="ResolveReadVisibilityAsync"/>-derived predicate
    /// <see cref="GetAsync"/> applies) — so a comment is never visible where
    /// the announcement is not, and never to a visitor, even on a
    /// public-scope announcement. A missing id is a
    /// <see cref="KeyNotFoundException"/> (404, non-leaky). **No** read-time
    /// <c>AccessAudit</c> row (the announcement's flat scope gate already ran
    /// — the C-M3·1 "comment-inherits the parent's single decision" rule).
    /// </summary>
    Task<IReadOnlyList<AnnouncementComment>> GetAnnouncementCommentsAsync(
        string announcementId, string? actorId, IReadOnlySet<string> actorRoles);

    /// <summary>
    /// **Adds a comment** on an announcement in the <b>caller's</b> in-flight
    /// session (ADR 0101, invariant C3). **Standing:** the actor must be
    /// **signed in** (an anonymous actor is a hard
    /// <see cref="UnauthorizedAccessException"/> / 403), the announcement
    /// must exist and be visible to the actor under its flat
    /// <see cref="AnnouncementScope"/> split (a missing / not-visible id is a
    /// <see cref="KeyNotFoundException"/> / 404, non-leaky), and the ADR 0101
    /// gate must be <c>true</c> (<see cref="AreAnnouncementCommentsEnabledAsync"/>
    /// — a hard <see cref="UnauthorizedAccessException"/> / 403 when off). The
    /// <c>LanguageCode</c> is materialized from the instance default when
    /// unchosen (ADR 0018). One
    /// <see cref="Authorization.AccessAudit"/> row
    /// (<c>announcementcomment.create</c>, <c>TargetKind = "announcement"</c>,
    /// <c>Via = Owner</c>) commits atomically with the write (C3).
    /// </summary>
    Task<AnnouncementComment> CreateAnnouncementCommentAsync(
        string announcementId,
        string actorId,
        IReadOnlySet<string> actorRoles,
        string body,
        string? languageCode,
        IDocumentSession session);

    /// <summary>
    /// **Soft-deletes a comment** the actor authored on an announcement
    /// (ADR 0101, the ADR 0024 shape carried from
    /// <see cref="Kumunita.Core.Posts.PostReply.DeletedAt"/>): stamps
    /// <see cref="AnnouncementComment.DeletedAt"/> forward (the record is
    /// kept, never hard-deleted). **Standing:** author-only (the comment's
    /// <see cref="AnnouncementComment.AuthorId"/> == the actor — the ADR 0016
    /// reply-delete precedent; a non-author is refused, there is no moderator /
    /// GlobalAdmin override branch on a comment's own delete). The comment
    /// must exist and be under the given announcement (a comment on a
    /// different announcement is a <see cref="KeyNotFoundException"/> — 404,
    /// non-leaky). One <see cref="Authorization.AccessAudit"/> row
    /// (<c>announcementcomment.delete</c>, <c>TargetKind = "announcement"</c>,
    /// <c>Via = Owner</c>) commits atomically with the write (C3).
    /// </summary>
    Task<AnnouncementComment> DeleteAnnouncementCommentAsync(
        string announcementId,
        string commentId,
        string actorId,
        IReadOnlySet<string> actorRoles,
        IDocumentSession session);}
