namespace Kumunita.Web.Models;

/// <summary>
/// The shared home/about view model. <see cref="Feed"/> is the optional
/// signed-in "what's new" surface (the /home page only; the About view
/// ignores it). Null ⇒ no feed (signed-out visitor, or the feed seams are
/// absent in a test construction) — the view renders hero + roadmap only,
/// exactly as before the feed existed.
/// </summary>
public sealed record HomeViewModel(
    string CommunityName,
    string? SupportEmail,
    HomeFeed? Feed = null);

/// <summary>
/// The /home "what's new" feed (signed-in visitors only): the latest of
/// each surface — posts (the /community all-sections feed), announcements
/// (the /announcements visible list), pages (the /pages tree). Each row is
/// display-shaped: the title/preview in the viewer's language when a
/// translation exists (the ADR 0051 / 0022 floor), else the authored-in
/// text. No decision data leaks — only rows the viewer's own feed reads
/// already surfaced.
/// </summary>
public sealed record HomeFeed(
    IReadOnlyList<HomeFeedRow> Posts,
    IReadOnlyList<HomeFeedRow> Announcements,
    IReadOnlyList<HomeFeedRow> Pages)
{
    /// <summary>The empty feed (signed-in, but nothing visible yet).</summary>
    public static HomeFeed Empty => new([], [], []);

    /// <summary>True when all three surfaces are empty — the view's "nothing yet" note.</summary>
    public bool IsEmpty => Posts.Count == 0 && Announcements.Count == 0 && Pages.Count == 0;
}

/// <summary>
/// One display row in a <see cref="HomeFeed"/> column.
/// <see cref="Author"/> / <see cref="AuthorSubjectId"/> are the row author's
/// display name + avatar identity (the /profile/avatar pattern; null for
/// rows with no meaningful author); <see cref="Badge"/> is an optional
/// contextual label (e.g. a pinned announcement, the post's community).
/// </summary>
public sealed record HomeFeedRow(
    string Title,
    string? Preview,
    DateTimeOffset? When,
    string Href,
    string? Author = null,
    string? AuthorSubjectId = null,
    string? Badge = null);
