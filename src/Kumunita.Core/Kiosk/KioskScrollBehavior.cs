namespace Kumunita.Core.Kiosk;

/// <summary>
/// What a kiosk entry does when its content is taller than the screen
/// (ADR 0285 D4). <see cref="None"/> — no scrolling (the content is cut off).
/// <see cref="Auto"/> — the kiosk scrolls the page at the playlist's
/// <see cref="KioskScrollSpeed"/>, pausing at the top and the bottom; the
/// entry's duration becomes a minimum (the slide stays until the scroll has
/// finished), and a scroll that finishes early returns to the top for the
/// rest of the duration. Display-only.
/// </summary>
public enum KioskScrollBehavior
{
    None,
    Auto,
}
