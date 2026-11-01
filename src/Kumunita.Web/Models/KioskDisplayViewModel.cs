namespace Kumunita.Web.Models;

using Kumunita.Core.Kiosk;

/// <summary>
/// The <c>/kiosk</c> display's view model (ADR 0164, reworked by ADR 0285):
/// the viewer's classified slides in play order, plus the playlist-wide
/// display settings. Every member is display-only (M35·4).
/// </summary>
public sealed class KioskDisplayViewModel
{
    /// <summary>The classified slides, in play order.</summary>
    public IReadOnlyList<KioskSlideViewModel> Slides { get; init; } = [];

    /// <summary>The playlist's default entry duration (seconds) — the
    /// fallback for a slide without its own.</summary>
    public int DefaultDurationSeconds { get; init; } = 30;

    /// <summary>The change-detection cadence (seconds).</summary>
    public int RefreshIntervalSeconds { get; init; } = 10;

    /// <summary>Full page or full screen (the ADR 0164 amendment).</summary>
    public KioskDisplayMode DisplayMode { get; init; } = KioskDisplayMode.FullPage;

    /// <summary>The auto-scroll speed for slides set to scroll (ADR 0285 D4).</summary>
    public KioskScrollSpeed ScrollSpeed { get; init; } = KioskScrollSpeed.Medium;

    public bool HasSlides => Slides.Count > 0;

    public KioskSlideViewModel? FirstSlide => Slides.Count > 0 ? Slides[0] : null;
}

/// <summary>
/// One classified slide (ADR 0164; ADR 0285 added the per-entry duration,
/// scroll behaviour and comments flag). <see cref="Href"/> is the route the
/// iframe frames (<c>null</c> when <see cref="Denied"/>);
/// <see cref="Fingerprint"/> is the 8-char content digest the client compares
/// on each refresh tick to reload only a changed slide (the ADR 0164
/// amendment); <see cref="Title"/> is the item's own title (the iframe's
/// accessible name when there is no <see cref="Label"/>).
/// </summary>
public sealed record KioskSlideViewModel(
    int Index,
    KioskItemType Type,
    string? Label,
    string? Href,
    bool Denied,
    string? Fingerprint = null,
    int DurationSeconds = 30,
    KioskScrollBehavior Scroll = KioskScrollBehavior.None,
    bool ShowComments = false,
    string? Title = null);

/// <summary>
/// The <c>GET /kiosk/slide?slide=N</c> refresh payload (the ADR 0164
/// amendment): the Nth slide's href, or the denied shape, plus its content
/// fingerprint.
/// </summary>
public sealed class KioskSlideRefreshViewModel
{
    public int Index { get; set; }

    public bool Denied { get; set; }

    public string? Href { get; set; }

    public string? DeniedKey { get; set; }

    public string? Fingerprint { get; set; }
}
