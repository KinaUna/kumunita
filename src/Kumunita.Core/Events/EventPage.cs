namespace Kumunita.Core.Events;

/// <summary>
/// <see cref="IEventService.ListUpcomingAsync"/>'s result (M7 §3.1, ADR 0090
/// D1). <see cref="Items"/> holds the source <see cref="Event"/> documents the
/// single <c>CanSeeAsync</c> call over the page's candidate set allowed.
/// <see cref="HasMore"/> is the sole paging signal (D1, C-M7·4): <c>true</c>
/// iff the page's *candidate* set filled the page (<c>candidates.Count ==
/// PageSize</c>) — a full page means "more might exist", a partial page means
/// "this is the last one". A 0-candidate page reports <c>HasMore: false</c>
/// and returns an empty list before any decision runs (C-M7·5).
/// <para>
/// <b>Record shape, not an <c>out</c> param (CS1988):</b> C# forbids
/// <c>out</c> parameters on <c>async</c> methods, so the paging signal rides
/// alongside the rows in the returned record — the same shape as
/// <see cref="Kumunita.Core.Posts.FeedResult"/>'s <c>HasMore</c>,
/// <see cref="GroupEventFeedResult"/>, and the D6
/// <c>AnnouncementPage</c> / <c>TagPostPage</c> records.
/// </para>
/// </summary>
public sealed record EventPage(
    IReadOnlyList<Event> Items,
    bool HasMore);
