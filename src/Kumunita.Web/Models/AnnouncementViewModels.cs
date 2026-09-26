using Kumunita.Core.Announcements;
using Kumunita.Core.UserInfo;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kumunita.Web.Models;

/// <summary>One row of the /announcements list (the read surface — the
/// "platform announcements" lane).</summary>
public sealed record AnnouncementRow(
    string Id,
    AnnouncementScope Scope,
    string Title,
    string Body,
    DateTimeOffset Created,
    string AuthorDisplayName,
    /// <summary>The author's subject id (the <c>Announcement</c>'s
    /// <see cref="AuthorId"/>) — a display convenience: the row's avatar links
    /// the audited serving lane <c>GET /profile/avatar/{subjectId}</c> (the
    /// same "a read, not a decision" pin as <see cref="AuthorDisplayName"/>).</summary>
    string AuthorSubjectId,
    bool Pinned,
    string? CommunityId,
    string? CommunityDisplayName);

/// <summary>The /announcements read surface (GET): the caller-visible
/// <see cref="Announcement"/> set (public scope always; community scope when
/// signed in — see <see cref="AnnouncementService.ListVisibleAsync"/>),
/// sorted latest-first, with a resolved author display name (null-safe:
/// falls back to the raw subject id if the author's profile row is missing —
/// a display-name lookup, never an access decision).
/// <para>
/// <see cref="Pager"/> (M7, ADR 0090 D5/F2): the paged-seam pager — null on
/// a single page so the <c>_Pager</c> partial renders nothing; the list has
/// no filter form (the D9 inventory row) so the pager's links carry
/// <c>?page=N</c> only.
/// </para>
/// </summary>
public sealed record AnnouncementIndexViewModel(IReadOnlyList<AnnouncementRow> Announcements)
{
    /// <summary>The M7 (ADR 0090 D5) pager — null on a single page (F2
    /// one-page no-render pin; the <c>_Pager</c> partial then renders
    /// nothing). Built by <see cref="Kumunita.Web.Controllers
    /// .AnnouncementController.Index"/> off the
    /// <see cref="Kumunita.Core.Announcements.IAnnouncementService
    /// .ListVisiblePagedAsync"/> seam's <c>HasMore</c> (D1); no filter form
    /// (D9) so the links carry <c>?page=N</c> only.</summary>
    public PagedViewModel? Pager { get; init; }
}

/// <summary>The /announcements/{id} detail view (the full-body read
/// surface — the list shows a truncated preview and links here): the
/// announcement's own shape plus the resolved author display name
/// (null-safe: falls back to the raw subject id if the author's profile
/// row is missing — a display-name lookup, never an access decision) and
/// the community name (when the row is community-targeted; null otherwise).</summary>
public sealed record AnnouncementDetailViewModel(
    string Id,
    AnnouncementScope Scope,
    string Title,
    string Body,
    DateTimeOffset Created,
    DateTimeOffset? Modified,
    string AuthorDisplayName,
    /// <summary>The author's subject id (the <c>Announcement</c>'s
    /// <see cref="AuthorId"/>) — a display convenience: the author's avatar
    /// links the audited serving lane <c>GET /profile/avatar/{subjectId}</c>
    /// (the same "a read, not a decision" pin as
    /// <see cref="AuthorDisplayName"/>).</summary>
    string AuthorSubjectId,
    bool Pinned,
    string? CommunityDisplayName,
    /// <summary>
    /// Whether the signed-in caller may edit this announcement (the same
    /// scope-vs-role split the
    /// <see cref="Kumunita.Core.Announcements.AnnouncementService"/>
    /// write-lane re-checks server-side at POST, evaluated against the
    /// *stored* row: a <c>Public</c> scope requires GlobalAdmin; a
    /// <c>Community</c> scope with no target requires GlobalAdmin or
    /// Moderator; a <c>Community</c> scope with a target requires GlobalAdmin
    /// or the <c>moderator:{CommunityId}</c> standing claim). A shape
    /// convenience for the detail page's Edit button (the
    /// <see cref="Kumunita.Web.Controllers.AnnouncementController"/>
    /// <c>EnsureWritePermissionAsync</c> split is the real gate — the button
    /// is just the affordance, so a non-authorized viewer never sees it).
    /// </summary>
    bool CanEdit,

    // ── ADR 0029 — user-added announcement translations ──

    /// <summary>The announcement's user-added translations (a "a read, not a
    /// decision" surface; the announcement's flat
    /// <see cref="AnnouncementScope"/> gate already ran in
    /// <see cref="Kumunita.Core.Announcements.AnnouncementService.GetAsync"/>).
    /// Renders as the hidden chip-swappable variants on the detail page.</summary>
    IReadOnlyList<Kumunita.Core.Announcements.AnnouncementTranslation> Translations,

    /// <summary>Every enabled <see cref="Kumunita.Core.Localization
    /// .LanguageCatalog"/> language (in <c>SortOrder</c>) with its
    /// <see cref="LanguageOption.HasTranslation"/> flag — the set the
    /// "available translations" chips and the "add a translation" candidate
    /// list render from (the shared <see cref="PostDetailViewModel"/>
    /// record, the ADR 0022 shape).</summary>
    IReadOnlyList<LanguageOption> Languages,

    /// <summary>Whether the signed-in actor holds standing to <b>add</b> a
    /// translation of this announcement (ADR 0029 — a GlobalAdmin, a
    /// Translator, and — for a targeted <c>Community</c> scope — that
    /// community's moderator). A display pin, not a gate: the real deny is
    /// <see cref="Kumunita.Core.Announcements.AnnouncementService
    /// .AddAnnouncementTranslationAsync"/>'s standing check. When false, no
    /// "add a translation" affordance renders.</summary>
    bool CanTranslate,

    /// <summary>The language the announcement was **authored in** (ADR
    /// 0018, <see cref="Announcement.LanguageCode"/>) — the detail surface
    /// renders this code as the **first, default-visible** variant chip, and
    /// the "Add a …" candidate list excludes it (the ADR 0027 / TD shape).</summary>
    string OriginalLanguageCode,

    // ── ADR 0037 — draft mode (author-only) ──

    /// <summary>
    /// Whether the signed-in caller is the announcement's <b>author</b> (the
    /// ADR 0037 author-only draft surface) — <c>AuthorId == actorId</c>. A
    /// shape convenience for the detail page's draft badge + Publish button:
    /// only the author ever sees a draft (<see
    /// cref="Kumunita.Core.Announcements.AnnouncementService.GetAsync"/>
    /// returns a draft to the author only), and only the author may publish it
    /// (the ADR 0037 author-only pin).
    /// </summary>
    bool IsAuthor,

    /// <summary>
    /// Whether the announcement is a <b>draft</b> (ADR 0037) —
    /// <see cref="Announcement.IsDraft"/> true. Only reachable in this view
    /// when <see cref="IsAuthor"/> is also true (a draft is author-only), so
    /// the detail page renders the draft badge + Publish button for exactly
    /// the one viewer who may act on it.
    /// </summary>
    bool IsDraft,

    // ── ADR 0101 — resident comments on the announcement (top-level only) ──

    /// <summary>Whether the comment surface may render at all for this viewer
    /// (ADR 0101): the viewer is **signed in** *and* the admin
    /// announcement-comments toggle is **on** (a
    /// <see cref="Kumunita.Core.Localization.LocaleSettings
    /// .AnnouncementCommentsEnabled"/> read). When false, the detail page
    /// renders neither the comment list nor the composer (a visitor, or a
    /// signed-in user with the toggle off, sees only the announcement body).
    /// A shape convenience — the service's read/write lanes are the real gate;
    /// this flag only keeps the affordance in step with both.</summary>
    bool CanComment,

    /// <summary>The announcement's resident comments (ADR 0101), ordered by
    /// <c>Created</c> ascending, each with a resolved author display name
    /// (null-safe: falls back to the raw subject id if the author's profile
    /// row is missing — a display-name lookup, never an access decision) and
    /// the author-only delete affordance (<see
    /// cref="AnnouncementCommentRow.IsAuthor"/>). Populated only when
    /// <see cref="CanComment"/> is true (otherwise the detail page renders
    /// nothing here, and the controller passes an empty list).</summary>
    IReadOnlyList<AnnouncementCommentRow> Comments,

    /// <summary>The enabled <see cref="Kumunita.Core.Localization
    /// .LanguageCatalog"/> languages for the comment composer's
    /// <see cref="Kumunita.Core.Localization.LanguageOption"/> picker (ADR 0018
    /// — the authored-in tag the author is writing the comment in). The shared
    /// <see cref="LanguageOption"/> record (the ADR 0022 / post-reply shape);
    /// the <c>HasTranslation</c> flag is unused for a comment (a tag, not a
    /// translation) but is set to <c>false</c> for the shared record's shape.
    /// Populated only when <see cref="CanComment"/> is true (the controller
    /// passes an empty list otherwise).</summary>
    IReadOnlyList<LanguageOption> CommentLanguages);

/// <summary>
/// One <see cref="Kumunita.Core.Announcements.AnnouncementComment"/> as a
/// **detail row** (the <c>GET /announcements/{id}</c> comments surface —
/// ADR 0101). The comment inherits the announcement's flat
/// <see cref="Kumunita.Core.Announcements.AnnouncementScope"/> read decision
/// (the ADR 0100 C-M3·1 "comment-inherits the parent's single decision" rule,
/// with the flat scope split standing in for the to-do's <c>Read</c>); its
/// <see cref="AuthorDisplayName"/> is a **read** lookup (a display
/// convenience, never an access decision — the M4/M5 idiom).
/// <para>
/// **<see cref="IsAuthor"/>** is the row-level **delete affordance** —
/// whether this actor may delete this comment now (author-only, ADR 0024 — a
/// convenience mirror for rendering the button; the authoritative decision is
/// the service's <see cref="Kumunita.Core.Announcements.AnnouncementService
/// .DeleteAnnouncementCommentAsync"/> lane). **<see cref="DeletedAt"/>** is
/// the ADR 0024 soft-delete stamp (a non-null value renders the deleted
/// placeholder in place of the body).
/// </para>
/// </summary>
public sealed record AnnouncementCommentRow(
    string Id,
    string AuthorId,
    string AuthorDisplayName,
    string Body,
    string LanguageCode,
    DateTimeOffset Created,
    DateTimeOffset? DeletedAt,
    bool IsAuthor);

/// <summary>The /announcements/new create form (the write lane) — also reused for the
/// /announcements/{id}/edit edit lane (with <see cref="Id"/> set), since both share
/// the same Title/Body/Scope shape and both enforce the scope-vs-role split server-side
/// (<see cref="Kumunita.Core.Announcements.AnnouncementService"/>).</summary>
public sealed class AnnouncementComposeViewModel
{
    /// <summary>The announcement id (set only for the edit lane; null/empty for create).
    /// The edit form posts to <c>/announcements/</c> + this value + <c>/edit</c>.</summary>
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Scope { get; set; }

    /// <summary>
    /// The announcement's <b>authored-in language</b> (ADR 0018, ADR 0005 B) —
    /// the BCP-47 code the author is writing this announcement in. A form-bound
    /// <c>&lt;select&gt;</c> posting <see cref="LanguageCode"/>; empty/unset is
    /// materialized from the instance default server-side at write time
    /// (<see cref="Kumunita.Core.Announcements.AnnouncementService.CreateAsync"/>).
    /// Editable on the edit lane (unlike the post/reply edit lanes — ADR 0018).
    /// </summary>
    public string? LanguageCode { get; set; }

    /// <summary>
    /// The compose form's language *picker* options — the instance's
    /// **enabled** language catalog (<see cref="Kumunita.Core.Localization.LanguageCatalog"/>),
    /// ordered by <see cref="Kumunita.Core.Localization.LanguageCatalog.SortOrder"/>,
    /// reseeded by the controller on every render. <b>[BindNever]</b> — the form
    /// POSTs a <see cref="LanguageCode"/>, not a catalog-list shape.
    /// </summary>
    [BindNever]
    public IReadOnlyList<(string Code, string NativeName)> Languages { get; set; } = [];

    /// <summary>The community this announcement targets (bound from a
    /// <c>&lt;select&gt;</c> in <c>New</c>/<c>Edit</c>). Empty → null = the flat
    /// "everyone" target (no specific community); a <c>Component</c> id →
    /// targeted at that community (visible to that community's members or
    /// moderators, or a GlobalAdmin; authorable by that community's moderator
    /// or a GlobalAdmin). A public-scope announcement must keep this null —
    /// the service rejects a Public + CommunityId shape.</summary>
    public string? CommunityId { get; set; }

    /// <summary>The <see cref="Component"/> options for <see cref="CommunityId"/>
    /// (a GlobalAdmin may target any community; a Moderator only the ones they
    /// moderate). Shape convenience only — the service pins the split
    /// server-side at POST and the ASP.NET gate narrows the author.</summary>
    public IReadOnlyCollection<Component> TargetCommunities { get; set; } = [];

    /// <summary>Whether this announcement is pinned to the top of all pages (site-wide banner).
    /// Binds from a pair of form fields in <c>New</c>/<c>Edit</c>: a checkbox
    /// <c>&lt;input type="checkbox" name="Pinned" value="true"/&gt;</c> (posted
    /// only when checked) followed by the always-posted hidden
    /// <c>&lt;input type="hidden" name="Pinned" value="false"/&gt;</c>. With a
    /// single-valued <see cref="bool"/> target, ASP.NET's model binder reads the
    /// <em>first</em> value for the form key from the form body, so the checkbox
    /// (first, when checked) wins over the hidden fallback (last). This pattern
    /// guarantees the flag round-trips exactly across re-renders of the edit
    /// invalid-POST lane: unchecked → <c>false</c> from the hidden, checked →
    /// <c>true</c> from the checkbox — no hidden state loss on re-render.</summary>
    public bool Pinned { get; set; } = false;

    /// <summary>
    /// The compose form's <b>save-as-draft</b> toggle (ADR 0037). When true,
    /// the announcement is written with <see
    /// cref="Kumunita.Core.Announcements.Announcement.IsDraft"/> true: it is
    /// saved but visible to <b>no one except its author</b> — not even a
    /// GlobalAdmin or the community it targets — until the author publishes it
    /// (<see cref="Kumunita.Core.Announcements.IAnnouncementService.PublishAsync"/>).
    /// The draft gate runs before the scope/role gate
    /// (<see cref="Kumunita.Core.Announcements.AnnouncementService.GetAsync"/>),
    /// so the scope split is moot until publish. Binds from a checkbox
    /// <c>&lt;input type="checkbox" name="SaveAsDraft" value="true"/&gt;</c>
    /// followed by the always-posted hidden
    /// <c>&lt;input type="hidden" name="SaveAsDraft" value="false"/&gt;</c> (the
    /// <see cref="Pinned"/> single-valued-bool round-trip pattern, so the flag
    /// survives edit-lane re-renders).
    /// </summary>
    public bool SaveAsDraft { get; set; } = false;

    /// <summary>
    /// ADR 0037 — the id of an already-saved draft this composer is continuing
    /// to edit. The <see cref="Kumunita.Web.Controllers.AnnouncementController"/>
    /// POST sets it after the first draft save; a subsequent draft save then
    /// <b>updates</b> the same draft (never minting a duplicate) because the id
    /// round-trips through a hidden form field across the stateless re-renders.
    /// Empty on a fresh composer (the first save creates the draft).
    /// </summary>
    public string? DraftId { get; set; }

    /// <summary>The caller's role-dependent scope options, reseeded by the controller on
    /// every render (not a form field — the POST invalid / POST unauthorized paths
    /// always overwrite from the caller's role set before the view sees this).</summary>
    public IReadOnlyCollection<AnnouncementScope> AllowedScopes { get; set; } = [];
}
