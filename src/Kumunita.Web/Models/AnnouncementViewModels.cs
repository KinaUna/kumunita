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

/// <summary>The /announcements/new create form (the write lane). <see cref="AllowedScopes"/>
/// is the caller's role-dependent scope picker (a GlobalAdmin sees both
/// <see cref="AnnouncementScope.Public"/> and <see cref="AnnouncementScope.Community"/>;
/// a Moderator sees only <see cref="AnnouncementScope.Community"/> — the
/// <see cref="AnnouncementService"/> re-checks the same split server-side at
/// POST, so the picker is a shape convenience, not the sole gate).</summary>
public sealed class AnnouncementComposeViewModel
{
    public string? Title { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Scope { get; set; }

    /// <summary>The caller's role-dependent scope options, reseeded by the controller on
    /// every render (not a form field — the POST invalid / POST unauthorized paths
    /// always overwrite from the caller's role set before the view sees this).</summary>
    public IReadOnlyCollection<AnnouncementScope> AllowedScopes { get; set; } = [];
}
