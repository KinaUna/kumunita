namespace Kumunita.Core.Kiosk;

/// <summary>
/// The closed set of kiosk playlist item types (ADR 0285 — the kiosk playlist
/// rework). Each playlist entry names one of these and then one specific item
/// of that type the resident can see. The kiosk still frames the item's own
/// route (ADR 0164 M35·6), so a type is really "which detail route the slide
/// iframes":
/// <list type="bullet">
/// <item><see cref="Board"/> — a Kanban board, shown as the board or as a
/// list of its to-dos grouped by lane (<c>/projects/boards/{id}</c>).</item>
/// <item><see cref="Todo"/> — one to-do (<c>/projects/todos/{id}</c>).</item>
/// <item><see cref="Project"/> — one project (<c>/projects/projects/{id}</c>).</item>
/// <item><see cref="Calendar"/> — the community calendar, "All" or one
/// community component, as a day / week / month view
/// (<c>/events/calendar</c>).</item>
/// <item><see cref="Event"/> — one event, community or group
/// (<c>/events/{id}</c> or <c>/groups/{groupId}/events/{id}</c>).</item>
/// <item><see cref="Page"/> — one page (<c>/pages/{path}</c>).</item>
/// <item><see cref="Announcement"/> — one announcement
/// (<c>/announcements/{id}</c>).</item>
/// <item><see cref="CommunityPost"/> — one community post
/// (<c>/posts/{id}</c>).</item>
/// <item><see cref="GroupPost"/> — one group post
/// (<c>/groups/{groupId}/posts/{id}</c>).</item>
/// </list>
/// The type is a display selector, never an access input: every slide still
/// re-runs the item's own read decision when its route loads (M35·3).
/// </summary>
public enum KioskItemType
{
    Board,
    Todo,
    Project,
    Calendar,
    Event,
    Page,
    Announcement,
    CommunityPost,
    GroupPost,
}
