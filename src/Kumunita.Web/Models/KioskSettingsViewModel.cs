namespace Kumunita.Web.Models;

using Kumunita.Core.Kiosk;

/// <summary>
/// M35 (ADR 0164) — the <c>GET /settings/kiosk</c> / <c>POST
/// /settings/kiosk</c> per-resident settings model (U05, ADR 0164 amendment —
/// any signed-in user edits their own playlist, not a GlobalAdmin-shared
/// singleton). The playlist editor binds to the
/// <see cref="Views"/> ordered list (list position = the slide order, M35·2),
/// and the two interval pickers bind to the <c>ViewIntervalSeconds</c>
/// (slide-advance cadence, <c>&gt;= 5</c>) + <c>RefreshIntervalSeconds</c>
/// (content-refresh cadence, <c>&gt;= 2</c> and <c>&lt; ViewIntervalSeconds</c>)
/// members (both **display-only, never an access input** — M35·4).
/// <para>
/// The <see cref="Boards"/> is the **board picker seed** — the frozen M5
/// <c>IProjectService.ListBoardsAsync</c> seam (M35·3 / M35·9 — reused, not
/// re-shape), resolved for the **signed-in resident** on the GET (a read —
/// the surface's own audit; the picker offers only the boards the resident
/// can already see, so the kiosk cannot name a board the resident cannot
/// see — a leak-prevention guard, not a new access branch).
/// </para>
/// <para>
/// **Any signed-in user** (ADR 0164 amendment — per-resident): the class gate
/// on <c>KioskController.SettingsGet</c> / <c>SettingsPost</c> is
/// <c>[Authorize]</c> (a resident edits their own playlist); the service
/// (<see cref="IKioskService.SavePlaylistAsync"/>) owns the single
/// <c>kiosk.save</c> <c>AccessAudit</c> row (<c>Via = Owner</c>) + the
/// per-resident upsert (M35·5), and the controller opens the
/// <c>IDocumentSession</c> and passes it in (the M3 same-transaction
/// precedent). **No** new <c>AccessAction</c> / <c>Decide()</c> branch
/// (M35·1 / M35·3).
/// </para>
/// </summary>
public sealed class KioskSettingsViewModel
{
    /// <summary>The **ordered** slide list (list position = the slide order,
    /// M35·2). Each entry is a <see cref="KioskView"/> — the 5-member ceiling
    /// (M35·2): <c>Kind</c> + <c>BoardId?</c> + <c>CalendarView?</c> +
    /// <c>ComponentId?</c> + <c>Label?</c>. An empty list is legal (the
    /// display renders the <c>kiosk.empty</c> message — M35-8).</summary>
    public IReadOnlyList<KioskView> Views { get; set; } = [];

    /// <summary>The slide-advance cadence in seconds (display-only, M35·4).
    /// <c>&gt;= 5</c>; the <see cref="KioskPlaylist"/> default is
    /// <c>30</c>.</summary>
    public int ViewIntervalSeconds { get; set; } = 30;

    /// <summary>The content-refresh cadence in seconds (display-only, M35·4;
    /// **faster** than the view cadence). <c>&gt;= 2</c> and
    /// <c>&lt; ViewIntervalSeconds</c>; the <see cref="KioskPlaylist"/>
    /// default is <c>10</c>.</summary>
    public int RefreshIntervalSeconds { get; set; } = 10;

    /// <summary>The display mode (display-only, never an access input — M35·4;
    /// the ADR 0164 amendment "full page vs. full screen" pin). The resident
    /// picks <see cref="KioskDisplayMode.FullPage"/> (fill the browser
    /// viewport) or <see cref="KioskDisplayMode.FullScreen"/> (the browser's
    /// Fullscreen API — the stage owns the whole monitor). The
    /// <see cref="KioskPlaylist"/> default is <c>FullPage</c>.</summary>
    public KioskDisplayMode DisplayMode { get; set; } = KioskDisplayMode.FullPage;

    /// <summary>The board picker's options — seeded from the **frozen** M5
    /// <c>IProjectService.ListBoardsAsync</c> (M35·3 / M35·9 — reused, not
    /// re-shape). <c>Title</c> is the <c>KanbanBoard.Title</c> (the
    /// <c>KanbanBoardToAuditableResource.Name</c> shape — <c>Title ??
    /// Truncate(Description)</c>).</summary>
    public IReadOnlyList<(string Id, string Title)> Boards { get; set; } = [];

    /// <summary>Server-side validation flag (the controller sets it before
    /// rendering on a failed POST — a true value means the model is
    /// well-formed and <see cref="IKioskService.SavePlaylistAsync"/> can be
    /// called; a false value means the form is re-rendered with the
    /// <c>TempData["error"]</c> message). The form itself never reads this
    /// (the admin view's local convention — the admin shell is mostly
    /// non-kw-l English, so an interpolated flag would break the
    /// <c>KwLRegistryConsistencyTests</c> static key scan).</summary>
    public bool IsValid { get; set; } = true;
}
