using System.Globalization;
using Kumunita.Core.Events;
using Kumunita.Core.Kiosk;
using Kumunita.Core.Projects;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M35 (ADR 0164) — the <c>/kiosk</c> display surface (U04). A **signed-in**
/// full-page / full-screen display that auto-cycles a GlobalAdmin-set playlist
/// of the resident's own surfaces (boards, calendar, to-dos) and refreshes
/// each slide's content on a faster cadence.
/// <para>
/// <b>Path-1 mechanism (U04-resume drift resolution, 2026-10-12):</b> the
/// three slide surfaces are **full MVC views**, not partials, so each slide is
/// resolved to the surface's **own route** and rendered inside the
/// chrome-hidden stage as an iframe (<c>frame-ancestors 'self'</c>,
/// <c>Program.cs</c> — same-origin iframing is permitted). The iframe's
/// navigation **re-runs the surface's own <c>Read</c> decision** — the frozen
/// board / calendar / todos read seam (M35·3) — on every load, so a slide
/// never reveals more than that viewer could already see on that surface's own
/// feed. <b>Reuse, not re-author (M35·6):</b> the kiosk adds no new view for a
/// slide; it frames the surface's own view. <b>No new authorization surface
/// (M35·1 / M35·3):</b> no <c>AccessAction</c> / <c>AccessVia</c> /
/// <c>Decide()</c> branch is added here; the per-view decision is the frozen
/// seam's, re-run by the surface's own controller on the iframe's navigation.
/// </para>
/// <para>
/// The classification below re-runs the **frozen** board / calendar / todos
/// read seam for the **signed-in viewer** *in order to decide visibility*
/// (a slide the viewer cannot see resolves to the <c>kiosk.slide_denied</c>
/// shape — M35-5 FACE: no content leak, no 404). This classification is a
/// **display** concern (which shape to render for the slide); it emits no
/// <c>kiosk.*</c> <c>AccessAudit</c> row of its own (M35·5) — the content's
/// own audit rides the surface's feed (the iframe's navigation). The display
/// reads emit **zero** <c>kiosk.*</c> rows; the only write lane
/// (<c>POST /admin/kiosk</c>, U05) commits exactly one <c>kiosk.save</c> row.
/// </para>
/// </summary>
[Authorize]
public sealed class KioskController : Controller
{
    private readonly IKioskService kiosk;
    private readonly IProjectService projects;
    private readonly IEventService events;
    private readonly EffectiveTimezoneResolver? timezone;

    public KioskController(
        IKioskService kiosk,
        IProjectService projects,
        IEventService events,
        EffectiveTimezoneResolver? timezone = null)
    {
        this.kiosk = kiosk ?? throw new ArgumentNullException(nameof(kiosk));
        this.projects = projects ?? throw new ArgumentNullException(nameof(projects));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.timezone = timezone;
    }

    /// <summary>
    /// <c>GET /kiosk</c> — the signed-in chrome-hidden display (M35-2 FACE).
    /// Reads the <see cref="KioskPlaylist"/> via the frozen
    /// <see cref="IKioskService.GetPlaylistAsync"/> (a null / empty row → the
    /// <c>kiosk.empty</c> message — M35-8 FACE), classifies each slide via the
    /// frozen read seam for the signed-in viewer, and renders
    /// <c>Views/Kiosk/Index.cshtml</c> (the chrome-hidden stage + the first
    /// visible slide). Anonymous visitors are redirected to sign-in by
    /// <c>[Authorize]</c> (M35-6 FACE).
    /// </summary>
    [HttpGet("/kiosk")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        var playlist = await kiosk.GetPlaylistAsync(ct).ConfigureAwait(false);

        var slides = new List<KioskSlideViewModel>();
        if (playlist is not null)
        {
            for (var i = 0; i < playlist.Views.Count; i++)
            {
                slides.Add(await Classify(playlist.Views[i], i, actorId, ct).ConfigureAwait(false));
            }
        }

        var vm = new KioskDisplayViewModel
        {
            Slides = slides,
            ViewIntervalSeconds = playlist?.ViewIntervalSeconds ?? 30,
            RefreshIntervalSeconds = playlist?.RefreshIntervalSeconds ?? 10,
        };

        // The landing-style full-bleed opt-out (the _Layout.cshtml
        // ViewData["FullBleed"] mechanism) — the kiosk stage runs flush to the
        // edges; the chrome (navbar / footer / flash / pinned-announcement) is
        // hidden by the body.kmb-page-expanded rules the view's inline <style>
        // applies (the ADR 0104 chrome-hide idiom, M35·6).
        ViewData["FullBleed"] = true;
        ViewData["Title"] = "Kiosk";
        return View(vm);
    }

    /// <summary>
    /// <c>GET /kiosk/slide?slide=N&amp;t=&lt;ts&gt;</c> — the single-slide
    /// refresh fetch (U07's <c>client/lib/kiosk.ts</c> consumes it). A
    /// **CSRF-free GET** (the M12 iCal "the cookie is the authorization"
    /// precedent); the <c>t</c> query param busts any cache so the frozen read
    /// seam re-runs on every fetch (M35·3). Returns the Nth slide's
    /// classification (a <c>Href</c> the client re-points the iframe at, or the
    /// <c>kiosk.slide_denied</c> shape when the viewer cannot see it — M35-5
    /// FACE, **not** a 404). Emits as JSON (a GET content response — no body
    /// parsing on the client).
    /// </summary>
    [HttpGet("/kiosk/slide")]
    public async Task<IActionResult> Slide(int slide, long t = 0, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        var playlist = await kiosk.GetPlaylistAsync(ct).ConfigureAwait(false);
        var refresh = new KioskSlideRefreshViewModel { Index = slide };

        if (playlist is null || slide < 0 || slide >= playlist.Views.Count)
        {
            // Out-of-range index — the stage falls back to the first slide
            // (client-side). Not a 404 (the M35-5 no-404 FACE) and not a leak
            // (no slide is named).
            refresh.Denied = true;
            refresh.DeniedKey = "kiosk.slide_denied";
            return Json(refresh);
        }

        var klass = await Classify(playlist.Views[slide], slide, actorId, ct).ConfigureAwait(false);
        refresh.Denied = klass.Denied;
        refresh.Href = klass.Href;
        refresh.DeniedKey = klass.Denied ? "kiosk.slide_denied" : null;
        return Json(refresh);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Slide classification — the frozen read seam, re-run for the signed-in
    // viewer (M35·3). A slide the viewer cannot see → the denied shape
    // (M35-5 FACE: no content leak, no 404). This is a **display** decision
    // (which shape to render); it adds no AccessAction / AccessVia / Decide()
    // branch and emits no kiosk.* audit row (M35·1 / M35·5).
    // ─────────────────────────────────────────────────────────────────────

    private async Task<KioskSlideViewModel> Classify(KioskView view, int index, string actorId, CancellationToken ct)
    {
        switch (view.Kind)
        {
            case KioskViewKind.Board:
                return await ClassifyBoard(view, index, actorId, ct).ConfigureAwait(false);
            case KioskViewKind.Calendar:
                return await ClassifyCalendar(view, index, actorId, ct).ConfigureAwait(false);
            case KioskViewKind.Todos:
                return await ClassifyTodos(view, index, actorId, ct).ConfigureAwait(false);
            default:
                return new KioskSlideViewModel(index, view.Kind, view.Label, Href: null, Denied: true);
        }
    }

    private async Task<KioskSlideViewModel> ClassifyBoard(KioskView view, int index, string actorId, CancellationToken ct)
    {
        try
        {
            // The frozen M5 board-detail read seam — the board's single Read
            // decision runs here (C-M5·3); a denied / missing board throws
            // (the BoardDetail action's exact catch shape).
            var board = await projects.GetBoardAsync(view.BoardId ?? string.Empty, actorId, ct).ConfigureAwait(false);
            var id = board.Board?.Id ?? view.BoardId;
            return new KioskSlideViewModel(index, view.Kind, view.Label, $"/projects/boards/{id}", Denied: false);
        }
        catch (KeyNotFoundException)
        {
            // A missing board — the denied shape (M35-5 FACE: no 404 crash).
            return new KioskSlideViewModel(index, view.Kind, view.Label, Href: null, Denied: true);
        }
        catch (UnauthorizedAccessException)
        {
            // A board the viewer may not read — the denied shape (M35-5 FACE).
            return new KioskSlideViewModel(index, view.Kind, view.Label, Href: null, Denied: true);
        }
    }

    private async Task<KioskSlideViewModel> ClassifyTodos(KioskView view, int index, string actorId, CancellationToken ct)
    {
        try
        {
            // The frozen M5 todos-list read seam — the single Read decision
            // runs here; a denied read throws (the TodosIndex action's exact
            // catch shape). A visible empty feed is a legitimate state, not a
            // deny (the seam returns an empty page, not an exception).
            await projects.ListTodosAsync(view.ComponentId, assigneeId: null, actorId, page: 1, ct: ct).ConfigureAwait(false);
            // The page's contents are never rendered here (M35·6) — the iframe
            // loads /projects/todos and re-runs the same seam for the viewer.
            var href = view.ComponentId is not null
                ? $"/projects/todos?componentId={Uri.EscapeDataString(view.ComponentId)}"
                : "/projects/todos";
            return new KioskSlideViewModel(index, view.Kind, view.Label, href, Denied: false);
        }
        catch (UnauthorizedAccessException)
        {
            return new KioskSlideViewModel(index, view.Kind, view.Label, Href: null, Denied: true);
        }
    }

    private async Task<KioskSlideViewModel> ClassifyCalendar(KioskView view, int index, string actorId, CancellationToken ct)
    {
        try
        {
            // The frozen M4 calendar-range read seam — the single Read decision
            // runs here (the Calendar action's exact catch shape). The window
            // is display-only (the view's unit: day / week / month, default
            // week); the seam's CanSeeAsync is the authority, re-run by the
            // surface on the iframe's navigation (M35·3).
            var viewName = NormalizeCalendarView(view.CalendarView);
            var zone = timezone is not null ? await timezone.GetAsync().ConfigureAwait(false) : null;
            var (start, end) = CalendarWindow(viewName, zone);
            await events.ListInRangeAsync(start, end, componentId: view.ComponentId, actorId, ct).ConfigureAwait(false);
            return new KioskSlideViewModel(index, view.Kind, view.Label, $"/events/calendar?view={viewName}", Denied: false);
        }
        catch (UnauthorizedAccessException)
        {
            return new KioskSlideViewModel(index, view.Kind, view.Label, Href: null, Denied: true);
        }
    }

    private static string NormalizeCalendarView(string? view) =>
        (view ?? "week").Trim().ToLowerInvariant() switch
        {
            "day" => "day",
            "month" => "month",
            _ => "week",
        };

    // The calendar window (display-only — the seam's window cap is a backstop,
    // the surface's own window policy is the authority, re-run on the iframe's
    // navigation). The zone is a display input, never an access input
    // (C-CAL·3); a null zone degrades to the ambient local zone.
    private static (DateTimeOffset StartUtc, DateTimeOffset EndUtcExclusive) CalendarWindow(
        string viewName, TimeZoneInfo? zone)
    {
        var nowLocal = zone is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone)
            : DateTime.Now;
        var anchor = nowLocal.Date;
        DateTime startLocal, endLocalExclusive;
        switch (viewName)
        {
            case "day":
                startLocal = anchor;
                endLocalExclusive = anchor.AddDays(1);
                break;
            case "month":
                startLocal = new DateTime(anchor.Year, anchor.Month, 1);
                endLocalExclusive = anchor.AddMonths(1).Date;
                break;
            default:
                var monday = anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7));
                startLocal = monday;
                endLocalExclusive = monday.AddDays(7);
                break;
        }
        return (ToUtc(startLocal, zone), ToUtc(endLocalExclusive, zone));
    }

    private static DateTimeOffset ToUtc(DateTime localDate, TimeZoneInfo? zone)
    {
        // A zone-local midnight → UTC instant (the Calendar action's
        // LocalMidnightUtc shape; a DST-ambiguous midnight is still a
        // well-defined zone-local time). A null zone degrades to the ambient
        // local zone (a display input, never an access input).
        var z = zone ?? TimeZoneInfo.Local;
        return new DateTimeOffset(
            new DateTime(localDate.Year, localDate.Month, localDate.Day, 0, 0, 0, DateTimeKind.Unspecified),
            z.GetUtcOffset(localDate)).ToUniversalTime();
    }
}
