namespace Kumunita.Web.Models;

using Kumunita.Core.Kiosk;

/// <summary>
/// M35 (ADR 0164) — the <c>GET /kiosk</c> chrome-hidden display model (U04).
/// The kiosk is a **signed-in** wall display that auto-cycles a
/// GlobalAdmin-set playlist of the resident's own surfaces (boards, calendar,
/// to-dos) and refreshes each slide's content on a faster cadence.
/// <para>
/// <b>Path-1 drift resolution (U04-resume, 2026-10-12):</b> the three slide
/// surfaces are **full MVC views**, not partials (the U04 drift pause), so the
/// design doc §2.3 <c>object? FirstSlideContent</c> shape is **superseded**.
/// Instead each slide resolves to a <see cref="KioskSlideViewModel"/> whose
/// <see cref="KioskSlideViewModel.Href"/> is the surface's **own route**
/// (<c>/projects/boards/{id}</c> / <c>/events/calendar?view=…</c> /
/// <c>/projects/todos</c>), rendered inside the chrome-hidden stage as an
/// iframe. The iframe's navigation **re-runs the surface's own <c>Read</c>
/// decision** (the frozen board / calendar / todos read seam — M35·3), so a
/// slide never reveals more than that viewer could already see on that
/// surface's own feed. A slide the viewer **cannot** see resolves to
/// <see cref="KioskSlideViewModel.Denied"/> = <c>true</c> (the
/// <c>kiosk.slide_denied</c> shape — M35-5 FACE: no content leak, no 404).
/// </para>
/// <para>
/// The two interval members are **display-only, never an access input**
/// (M35·4, ADR 0164 D3): they configure the client-side auto-cycle /
/// auto-refresh cadence; they are never a DB access column and never an
/// authorization input.
/// </para>
/// </summary>
public sealed class KioskDisplayViewModel
{
    /// <summary>The resolved slide list (ordered — list position = the slide
    /// order, matching the <see cref="Kumunita.Core.Kiosk.KioskPlaylist.Views"/>
    /// order). Each entry is the frozen seam's classification for the
    /// signed-in viewer (a <see cref="KioskSlideViewModel.Href"/> to iframe, or
    /// a <see cref="KioskSlideViewModel.Denied"/> shape).</summary>
    public IReadOnlyList<KioskSlideViewModel> Slides { get; init; } = [];

    /// <summary>The slide-advance cadence in seconds (display-only, M35·4).
    /// <c>&gt;= 5</c>; the <see cref="Kumunita.Core.Kiosk.KioskPlaylist"/>
    /// default is <c>30</c>.</summary>
    public int ViewIntervalSeconds { get; init; } = 30;

    /// <summary>The content-refresh cadence in seconds (display-only, M35·4;
    /// **faster** than the view cadence). The <see cref="Kumunita.Core.Kiosk.KioskPlaylist"/>
    /// default is <c>10</c>.</summary>
    public int RefreshIntervalSeconds { get; init; } = 10;

    /// <summary>The display mode (display-only, never an access input — M35·4;
    /// the ADR 0164 amendment "full page vs. full screen" pin):
    /// <see cref="Kumunita.Core.Kiosk.KioskDisplayMode.FullPage"/> fills the
    /// browser viewport; <see cref="Kumunita.Core.Kiosk.KioskDisplayMode.FullScreen"/>
    /// requests the browser's Fullscreen API (the stage owns the whole monitor,
    /// the M35 "shared physical display" question). The <see cref="Kumunita.Core.Kiosk.KioskPlaylist"/>
    /// default is <c>FullPage</c> (entering true full-screen needs a user
    /// gesture, so the display degrades to full-page where it is unavailable).</summary>
    public KioskDisplayMode DisplayMode { get; init; } = KioskDisplayMode.FullPage;

    /// <summary>True when at least one slide is configured (false → the view
    /// renders the <c>kiosk.empty</c> message — M35-8 FACE).</summary>
    public bool HasSlides => Slides.Count > 0;

    /// <summary>The first visible slide (null when the playlist is empty). Its
    /// <see cref="KioskSlideViewModel.Href"/> seeds the stage's initial
    /// iframe; <see cref="KioskSlideViewModel.Denied"/> = the
    /// <c>kiosk.slide_denied</c> shape.</summary>
    public KioskSlideViewModel? FirstSlide => Slides.Count > 0 ? Slides[0] : null;
}

/// <summary>
/// M35 (ADR 0164) — one resolved kiosk slide (U04). The classification re-runs
/// the **frozen** board / calendar / todos read seam for the **signed-in
/// viewer** (M35·3 — the same <c>Read</c> decision as that surface's own
/// feed); the result is either a <see cref="Href"/> the stage iframes, or a
/// <see cref="Denied"/> shape (the viewer cannot see the slide — the
/// <c>kiosk.slide_denied</c> message, M35-5 FACE).
/// </summary>
/// <param name="Index">The slide's position in the playlist (0-based).</param>
/// <param name="Kind">The surface kind (<see cref="KioskViewKind"/>).</param>
/// <param name="Label">The optional per-slide display label (null = the
/// surface's own header).</param>
/// <param name="Href">The surface's own route (the iframe <c>src</c>) —
/// <c>null</c> when <see cref="Denied"/>.</param>
/// <param name="Denied">True when the viewer cannot see this slide (render the
/// <c>kiosk.slide_denied</c> shape instead of an iframe — M35-5 FACE).</param>
public sealed record KioskSlideViewModel(
    int Index,
    KioskViewKind Kind,
    string? Label,
    string? Href,
    bool Denied,
    // ADR 0164 amendment — the slide's **content fingerprint** (an 8-char
    // FNV-1a 32 hex digest of the display-relevant fields, see the
    // KioskFingerprint helper). The /kiosk stage's client (client/lib/kiosk.ts)
    // stores the last rendered fingerprint; on each REFRESH tick it fetches
    // GET /kiosk/slide?slide=N, compares the returned Fingerprint to the
    // stored one, and **only** reloads the iframe when they differ. This
    // keeps the display from flickering / re-navigating on a tick where the
    // underlying content has not changed (the user-facing "kiosk reload is
    // too disrupting" fix). Denied slides carry a stable denied-shape
    // fingerprint; a slide that flips visible→denied changes its fingerprint
    // (the kind prefix guarantees they never collide).
    string? Fingerprint = null);

/// <summary>
/// M35 (ADR 0164) — the <c>GET /kiosk/slide?slide=N</c> single-slide refresh
/// fetch response (U04). A **CSRF-free GET** (the M12 iCal "the cookie is the
/// authorization" precedent); the <c>t</c> query param busts any cache so the
/// frozen read seam re-runs on every fetch (M35·3). <c>client/lib/kiosk.ts</c>
/// (U07) consumes it to re-point the stage's iframe at <see cref="Href"/> (or
/// to swap in the <see cref="DeniedKey"/> shape when <see cref="Denied"/>).
/// Emitted as JSON (a GET content response — <c>connect-src 'self'</c>, no
/// body parsing required on the client).
/// </summary>
public sealed class KioskSlideRefreshViewModel
{
    /// <summary>The slide's position in the playlist (0-based).</summary>
    public int Index { get; set; }

    /// <summary>True when the viewer cannot see this slide (U07 swaps the
    /// <see cref="DeniedKey"/> shape into the stage — M35-5 FACE).</summary>
    public bool Denied { get; set; }

    /// <summary>The surface's own route (the iframe <c>src</c>) — <c>null</c>
    /// when <see cref="Denied"/>.</summary>
    public string? Href { get; set; }

    /// <summary>The <c>kw-l</c> key U07 resolves for the denied shape
    /// (<c>kiosk.slide_denied</c>) — <c>null</c> when the slide is visible.</summary>
    public string? DeniedKey { get; set; }

    /// <summary>The slide's **content fingerprint** (ADR 0164 amendment):
    /// an 8-char FNV-1a 32 hex digest of the display-relevant fields for
    /// this slide, computed server-side in
    /// <c>KioskController.Classify*</c> via the <c>KioskFingerprint</c>
    /// helper. <c>client/lib/kiosk.ts</c> compares this to the last rendered
    /// fingerprint on each REFRESH tick and **skips the iframe reload** when
    /// they match — the user-facing fix for the "kiosk reload is too
    /// disruptive" symptom (the unconditional re-render every
    /// <c>data-refresh-interval</c> s that the M35 U04 implementation
    /// shipped). The fingerprint is a **display-only** signal (M35·4 — never
    /// an access input): it is computed from the same already-Read content
    /// the controller already loaded for the classification, so no new seam,
    /// no new audit row (M35·5), and no new authorization branch (M35·1 /
    /// M35·3).</summary>
    public string? Fingerprint { get; set; }
}
