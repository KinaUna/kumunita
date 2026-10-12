using Kumunita.Core.SiteContent;

namespace Kumunita.Web.Models;

/// <summary>
/// The shared home/about view model. <see cref="Feed"/> is the optional
/// signed-in "what's new" surface (the /home page only; the About view
/// ignores it). Null ⇒ no feed (signed-out visitor, or the feed seams are
/// absent in a test construction) — the view renders hero + roadmap only,
/// exactly as before the feed existed. <see cref="Site"/> is the optional
/// admin-settled landing-surface content (the <c>SiteContent</c> singleton,
/// ADR 0150): null ⇒ the view degrades to the in-code fallback (the
/// byte-identical shipped <c>kw-l</c> text + every section shown), so a
/// missing singleton never blanks the page (SITE·1).
/// </summary>
public sealed record HomeViewModel(
    string CommunityName,
    string? SupportEmail,
    HomeFeed? Feed = null,
    bool HideIntro = false,
    SiteContent? Site = null,
    HomeUpcoming? Upcoming = null);

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

/// <summary>
/// The /home "upcoming" section (the resident's
/// <c>Profile.HomeShowUpcoming</c> preference, ADR 0004 §B.1 additive) — the
/// next upcoming <c>Events</c> + the top <c>to-dos</c>, rendered just above
/// the "What's new" feed (signed-in visitors only; <see cref="Upcoming"/>
/// is <c>null</c> for a signed-out visitor, a disabled preference, or a
/// missing seam). Each column is the same authorization-aware read its
/// surface's own feed uses (<see cref="Kumunita.Core.Events.IEventService
/// .ListUpcomingAsync"/> / <see cref="Kumunita.Core.Projects.IProjectService
/// .ListTodosAsync"/>), so a row is visible here only when the viewer could
/// already see it in that surface — no decision data leaks, a seam that is
/// absent or throws degrades that column to empty, never the page (the
/// <see cref="HomeFeed"/> best-effort posture). The view renders the first
/// <see cref="HomeUpcoming.Columns"/> rows of each column and, when a column
/// overflows, a "show more" affordance that reveals the rest client-side
/// (the <c>mine-more</c> / <c>roadmap-more</c> disclosure idiom).
/// </summary>
public sealed record HomeUpcoming(
    IReadOnlyList<HomeUpcomingRow> Events,
    IReadOnlyList<HomeUpcomingRow> Todos)
{
    /// <summary>How many rows of each column show before "show more"
    /// appears (the ADR 0090 feed-page default is 10; the home quick-scan
    /// window is deliberately smaller — the "next 5" the preference
    /// promises).</summary>
    public const int Columns = 5;
}

/// <summary>
/// One display row in a <see cref="HomeUpcoming"/> column.
/// <see cref="When"/> is the row's time anchor (an event's
/// <c>Start</c>, a to-do's <c>DueAt</c> — or <c>null</c> when the row has
/// neither a meaningful time nor a date), rendered by the one <c>kw-dt</c>
/// TagHelper. <see cref="Meta"/> is an optional one-line context (an event's
/// location, a to-do's status label) — display metadata only, never a
/// decision (the <see cref="Kumunita.Core.Events.Event.Location"/> /
/// <see cref="Kumunita.Core.Projects.TodoItem.Status"/> posture).
/// </summary>
public sealed record HomeUpcomingRow(
    string Title,
    string? Preview,
    DateTimeOffset? When,
    string Href,
    string? Meta = null);
