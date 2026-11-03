namespace Kumunita.Core.Kiosk;

/// <summary>
/// The playlist-wide auto-scroll speed (ADR 0285 D4): one setting per
/// playlist, because a readable speed depends on the screen and how far away
/// people stand, not on the item. The pixel rates live in <c>kiosk.ts</c>.
/// Display-only.
/// </summary>
public enum KioskScrollSpeed
{
    Slow,
    Medium,
    Fast,
}
