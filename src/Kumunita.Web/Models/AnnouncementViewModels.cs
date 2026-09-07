using Kumunita.Core.Announcements;

namespace Kumunita.Web.Models;

/// <summary>One row of the /announcements list (the read surface — the
/// "platform announcements" lane).</summary>
public sealed record AnnouncementRow(
    string Id,
    AnnouncementScope Scope,
    string Title,
    string Body,
    DateTimeOffset Created,
    string AuthorDisplayName);

/// <summary>The /announcements read surface (GET): the caller-visible
/// <see cref="Announcement"/> set (public scope always; community scope when
/// signed in — see <see cref="AnnouncementService.ListVisibleAsync"/>),
/// sorted latest-first, with a resolved author display name (null-safe:
/// falls back to the raw subject id if the author's profile row is missing —
/// a display-name lookup, never an access decision).</summary>
public sealed record AnnouncementIndexViewModel(IReadOnlyList<AnnouncementRow> Announcements);

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

    /// <summary>The caller's role-dependent scope options, reseeded by the controller on
    /// every render (not a form field — the POST invalid / POST unauthorized paths
    /// always overwrite from the caller's role set before the view sees this).</summary>
    public IReadOnlyCollection<AnnouncementScope> AllowedScopes { get; set; } = [];
}
