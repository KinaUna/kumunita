namespace Kumunita.Core.Kiosk;

/// <summary>
/// The kiosk playlist (M35·2 — the **5-member ceiling**, no field
/// outside this set, ADR 0164 D1 + the amendment's <see cref="DisplayMode"/>).
/// A **per-resident** row (the
/// <c>LocaleSettings</c> / per-user preference shape, ADR 0004
/// §B.1): one row per signed-in user, <see cref="Id"/> set to the
/// user's <b>subject id</b> (the actor id that configured it), so each
/// resident's kiosk playlist is independent — the <c>/kiosk</c> display
/// resolves the *viewer's* playlist, not a shared GlobalAdmin singleton.
/// <para>
/// The <see cref="Views"/> is the **ordered** slide list (list position =
/// the slide order, not a stored field — the <see cref="KioskView"/> entries
/// are a value object, not a standalone doc, M35·9). The two interval
/// members are **display-only, never an access input** (M35·4, ADR 0164 D3):
/// they configure the kiosk's auto-cycle / auto-refresh cadence; they are
/// never a DB access column and never an authorization input.
/// </para>
/// <para>
/// The <see cref="KioskPlaylist"/> is a **resident-set** config (the
/// <c>LocaleSettingsService</c> / per-user preference write-lane
/// convention): the standing to write it is any signed-in user, enforced by
/// the Web gate (<c>[Authorize]</c>), not re-checked in the Core service
/// (M35·1 — the <c>KioskService</c> adds **no** new <c>AccessAction</c> /
/// <c>Decide()</c> branch / <c>IAuditableResource</c> adapter). The
/// <see cref="KioskPlaylist"/> read emits **zero** <c>AccessAudit</c> rows
/// (M35·5, ADR 0164 D5); the write commits exactly **one**
/// <c>kiosk.save</c> <c>AccessAudit</c> row (<c>Via = Owner</c>, M35·5).
/// </para>
/// </summary>
public sealed class KioskPlaylist
{
    /// <summary>The per-resident identity key (the <b>subject id</b> of the
    /// user who configured this playlist — one row per user, keyed on this
    /// id).</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The **ordered** slide list (list position = the slide order,
    /// not a stored field — the <see cref="KioskView"/> entries are a value
    /// object, not a standalone doc, M35·9). An empty list (the default)
    /// means "no slides configured" — the display renders the
    /// <c>kiosk.empty</c> message (M35-8 FACE).</summary>
    public IReadOnlyList<KioskView> Views { get; set; } = [];

    /// <summary>The slide-advance cadence in seconds (the kiosk's
    /// <c>setInterval</c> view-advance timer). **Display-only, never an
    /// access input** (M35·4, ADR 0164 D3). <c>&gt;= 5</c>; default
    /// <c>30</c>.</summary>
    public int ViewIntervalSeconds { get; set; } = 30;

    /// <summary>The content-refresh cadence in seconds (the kiosk's
    /// <c>setInterval</c> content-refresh timer — **faster** than the view
    /// cadence). **Display-only, never an access input** (M35·4, ADR 0164
    /// D3). <c>&gt;= 2</c> and <c>&lt; ViewIntervalSeconds</c>; default
    /// <c>10</c>.</summary>
    public int RefreshIntervalSeconds { get; set; } = 10;

    /// <summary>The display mode (the ADR 0164 amendment "full page vs. full
    /// screen" pin — a per-resident <em>display</em> preference, **display-only,
    /// never an access input**, M35·4): <see cref="KioskDisplayMode.FullPage"/>
    /// (the browser viewport, the ADR 0104 chrome-hide idiom) or
    /// <see cref="KioskDisplayMode.FullScreen"/> (the browser's Fullscreen API
    /// — the stage owns the whole monitor, the "shared physical display"
    /// question M35 settles). Default <see cref="KioskDisplayMode.FullPage"/>
    /// (a fresh row is the in-browser full-page shape; entering true
    /// full-screen needs a user gesture, so the display degrades to full-page
    /// where the gesture is unavailable).</summary>
    public KioskDisplayMode DisplayMode { get; set; } = KioskDisplayMode.FullPage;
}
