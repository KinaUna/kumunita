namespace Kumunita.Core.Kiosk;

/// <summary>
/// One entry in a resident's kiosk playlist (ADR 0285) — a value object
/// nested in <see cref="KioskPlaylist.Entries"/> (not a standalone doc,
/// M35·9). List position is the play order.
/// <para>
/// Every member is **display-only, never an access input** (M35·4): the
/// entry names an item, and the slide that shows it iframes the item's own
/// route, which runs its own read decision on every load (M35·3). An item
/// the viewer can no longer see renders the <c>kiosk.slide_denied</c> shape.
/// </para>
/// </summary>
public sealed class KioskEntry
{
    /// <summary>Which kind of item the entry shows.</summary>
    public KioskItemType Type { get; set; }

    /// <summary>
    /// The item's id (board / to-do / project / event / page / announcement /
    /// post). For <see cref="KioskItemType.Calendar"/> it is the community
    /// component id, or <c>null</c> for "All communities". Required for every
    /// other type.
    /// </summary>
    public string? ItemId { get; set; }

    /// <summary>
    /// The owning group — set for a <see cref="KioskItemType.GroupPost"/> and
    /// for a group <see cref="KioskItemType.Event"/> (the group routes carry
    /// the group id); <c>null</c> otherwise.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>Board or list — only for <see cref="KioskItemType.Board"/>.</summary>
    public KioskBoardView BoardView { get; set; } = KioskBoardView.Board;

    /// <summary><c>"day"</c> | <c>"week"</c> | <c>"month"</c> — only for
    /// <see cref="KioskItemType.Calendar"/>; <c>null</c> otherwise.</summary>
    public string? CalendarView { get; set; }

    /// <summary>
    /// How long the entry is shown, in seconds (<c>&gt;= 5</c>). With
    /// <see cref="KioskScrollBehavior.Auto"/> this is a minimum: the slide
    /// stays until the scroll has finished.
    /// </summary>
    public int DurationSeconds { get; set; } = 30;

    /// <summary>What happens when the content does not fit the screen.</summary>
    public KioskScrollBehavior Scroll { get; set; } = KioskScrollBehavior.None;

    /// <summary>
    /// Show comment threads / replies (and an event's RSVP list) on the slide.
    /// Off by default: the slide shows the item only.
    /// </summary>
    public bool ShowComments { get; set; }

    /// <summary>An optional label; <c>null</c> = the item's own title.</summary>
    public string? Label { get; set; }
}
