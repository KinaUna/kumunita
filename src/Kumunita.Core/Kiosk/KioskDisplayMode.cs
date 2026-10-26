namespace Kumunita.Core.Kiosk;

/// <summary>
/// The closed 2-member set of kiosk display modes (the ADR 0164 amendment
/// "full page vs. full screen" pin — a per-resident <em>display</em> preference,
/// never an access input, M35·4). <see cref="FullPage"/> — the kiosk fills the
/// browser viewport (page chrome hidden, the stage owns 100% of the visible
/// area; the ADR 0104 <c>body.kmb-page-expanded</c> chrome-hide idiom).
/// <see cref="FullScreen"/> — the kiosk requests the browser's Fullscreen API
/// on its own stage element (the board / calendar <c>requestFullscreen</c>
/// precedent), so the browser chrome (tabs / address bar) is also hidden and
/// the surface owns the whole monitor (a wall-display TV, the M35
/// "shared physical display" question). Both modes hide the page chrome; they
/// differ only in whether the browser's own chrome is hidden too.
/// </summary>
public enum KioskDisplayMode
{
    /// <summary>Fill the browser viewport (page chrome hidden, browser chrome
    /// kept). The default.</summary>
    FullPage,

    /// <summary>Request the browser's Fullscreen API (browser chrome hidden
    /// too, the stage owns the whole monitor). A user gesture is required to
    /// enter it; when the gesture is unavailable the display degrades to
    /// <see cref="FullPage"/>.</summary>
    FullScreen,
}
