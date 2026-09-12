using Kumunita.Core.Announcements;
using Kumunita.Core.UserInfo;

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
/// a display-name lookup, never an access decision).</summary>
public sealed record AnnouncementIndexViewModel(IReadOnlyList<AnnouncementRow> Announcements);

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
    bool CanEdit);

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

    /// <summary>The caller's role-dependent scope options, reseeded by the controller on
    /// every render (not a form field — the POST invalid / POST unauthorized paths
    /// always overwrite from the caller's role set before the view sees this).</summary>
    public IReadOnlyCollection<AnnouncementScope> AllowedScopes { get; set; } = [];
}
