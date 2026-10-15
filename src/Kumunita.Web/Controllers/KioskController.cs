using System.Globalization;
using Kumunita.Core.Events;
using Kumunita.Core.Kiosk;
using Kumunita.Core.Projects;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M35 (ADR 0164) — the <c>/kiosk</c> display surface (U04). A **signed-in**
/// full-page / full-screen display that auto-cycles the signed-in user's own
/// playlist (the ADR 0164 amendment — per-resident, not a GlobalAdmin-shared
/// one) of the resident's own surfaces (boards, calendar, to-dos) and refreshes
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
/// (<c>POST /settings/kiosk</c>) commits exactly one <c>kiosk.save</c> row
/// (keyed on the acting resident's subject id, <c>Via = Owner</c> — the ADR
/// 0164 amendment, per-resident).
/// </para>
/// </summary>
[Authorize]
public sealed class KioskController : Controller
{
    private readonly IKioskService kiosk;
    private readonly IProjectService projects;
    private readonly IEventService events;
    private readonly EffectiveTimezoneResolver? timezone;
    private readonly IDocumentStore? store;

    public KioskController(
        IKioskService kiosk,
        IProjectService projects,
        IEventService events,
        EffectiveTimezoneResolver? timezone = null,
        IDocumentStore? store = null)
    {
        this.kiosk = kiosk ?? throw new ArgumentNullException(nameof(kiosk));
        this.projects = projects ?? throw new ArgumentNullException(nameof(projects));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.timezone = timezone;
        this.store = store;
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
        var playlist = await kiosk.GetPlaylistAsync(actorId, ct).ConfigureAwait(false);

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
            DisplayMode = playlist?.DisplayMode ?? KioskDisplayMode.FullPage,
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
        var playlist = await kiosk.GetPlaylistAsync(actorId, ct).ConfigureAwait(false);
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
    // Resident settings surface — the /settings/kiosk playlist editor (the
    // ADR 0164 amendment: per-resident, not the GlobalAdmin shared singleton).
    // The gate is [Authorize] (any signed-in user edits **their own**
    // playlist — the per-resident write-lane); the service
    // (KioskService.SavePlaylistAsync) owns the single kiosk.save
    // AccessAudit row (Via = Owner) + the upsert, keyed on the resident's
    // subject id (M35·5); the controller opens the IDocumentSession and
    // passes it in (the M3 same-transaction precedent — the
    // AdminStorageController.Save shape). No new AccessAction / Decide()
    // branch (M35·1 / M35·3).
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /settings/kiosk</c> — the signed-in resident's playlist editor
    /// (M35·2, ADR 0164 amendment — per-resident). Reads the
    /// <see cref="KioskPlaylist"/> for the signed-in viewer via the frozen
    /// <see cref="IKioskService.GetPlaylistAsync"/> (a null / empty row →
    /// the all-defaults form: 30s / 10s intervals, zero slides — the
    /// M35-8 floor), seeds the board picker from the **frozen** M5
    /// <see cref="IProjectService.ListBoardsAsync"/> seam (M35·3 / M35·9 —
    /// reused, not re-shape; the viewer's own Read decision runs on the seam,
    /// so the picker offers only the boards the viewer can already see — a
    /// leak-prevention guard, not a new access branch), and renders
    /// <c>Views/Kiosk/Settings.cshtml</c> (the playlist editor + the two
    /// interval pickers). Anonymous visitors are redirected to sign-in by
    /// <c>[Authorize]</c>.
    /// </summary>
    [HttpGet("/settings/kiosk")]
    public async Task<IActionResult> SettingsGet(CancellationToken ct)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        var playlist = await kiosk.GetPlaylistAsync(actorId, ct).ConfigureAwait(false);

        // The board picker seed (the frozen M5 board-list read — the GlobalAdmin's
        // own Read decision runs on the seam, M35·3; the picker offers only the
        // boards the GlobalAdmin can already see). Best-effort: a missing seam,
        // a read failure, or an empty result degrades to an empty list, so the
        // page always renders (the board picker simply offers no candidates —
        // a board slide with no board id is still a legitimate state, the
        // kiosk renders the kiosk.slide_denied shape for it).
        IReadOnlyList<(string Id, string Title)> boards = [];
        try
        {
            var page = await projects.ListBoardsAsync(
                componentId: null, actorId, page: 1, pageSize: 200, ct: ct)
                .ConfigureAwait(false);
            boards = page.Items
                .Select(b => (b.Id, b.Title))
                .ToList();
        }
        catch
        {
            // A read failure (an unauthorized viewer — unlikely, but the
            // seam's CanSeeAsync is the authority) degrades to an empty
            // picker; the form still renders. No leak: no board id is
            // surfaced.
            boards = [];
        }

        var vm = new KioskSettingsViewModel
        {
            Views = playlist?.Views ?? [],
            ViewIntervalSeconds = playlist?.ViewIntervalSeconds ?? 30,
            RefreshIntervalSeconds = playlist?.RefreshIntervalSeconds ?? 10,
            DisplayMode = playlist?.DisplayMode ?? KioskDisplayMode.FullPage,
            Boards = boards,
            IsValid = true,
        };

        ViewData["Title"] = "Kiosk settings";
        return View("Settings", vm);
    }

    /// <summary>
    /// <c>GET /admin/kiosk</c> — retired address (the ADR 0164 amendment moved
    /// the kiosk editor out of the admin area to the per-resident
    /// <c>/settings/kiosk</c> surface). Kept as a redirect so a saved link,
    /// bookmark, or an already-open old tab still lands the user on the new
    /// page whose form posts to the live route (the <c>/settings/email-language</c>
    /// → Language-tab redirect precedent, ADR 0080) — rather than a hard 404
    /// when the stale page's form is submitted.
    /// </summary>
    [HttpGet("/admin/kiosk")]
    public IActionResult AdminKiosk()
    {
        return Redirect("/settings/kiosk");
    }

    /// <summary>
    /// <c>POST /settings/kiosk</c> — saves the signed-in resident's playlist
    /// (M35·2, M35·5, ADR 0164 amendment — per-resident). Binds the
    /// <see cref="KioskSettingsViewModel"/>; validates the two interval floors
    /// (the <c>ViewIntervalSeconds</c> <c>&gt;= 5</c> + the
    /// <c>RefreshIntervalSeconds</c> <c>&gt;= 2</c> +
    /// <c>&lt; ViewIntervalSeconds</c> — the M35·4 display-only pin; the
    /// <see cref="KioskPlaylist"/> floor defaults are 30 / 10); on valid,
    /// opens the <c>IDocumentStore.LightweightSession</c> (the M3
    /// same-transaction lane) + delegates to
    /// <see cref="IKioskService.SavePlaylistAsync"/> (the **one**
    /// <c>kiosk.save</c> <c>AccessAudit</c> row, M35·5 — the service owns
    /// the audit row + the upsert, keyed on the resident's subject id,
    /// <c>Via = Owner</c>); on invalid, re-renders with the
    /// <c>TempData["error"]</c> message. Anonymous visitors are redirected to
    /// sign-in by <c>[Authorize]</c>. **No** new <c>AccessAction</c> /
    /// <c>Decide()</c> branch (M35·1 / M35·3).
    /// </summary>
    [HttpPost("/settings/kiosk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettingsPost(CancellationToken ct)
    {
        // Parse the two interval floors (M35·4 — display-only, never an access
        // input; the KioskPlaylist's documented floors) from the form (the
        // AdminStorageController.Save raw-form idiom — the binder's int
        // fallback on a blank would silently pass 0, which we want to reject
        // loudly instead of persisting). A sub-floor value → the error toast
        // + a redirect back to the GET (which re-renders from the stored
        // playlist — the AdminStorageController.Save redirect-after-error
        // shape).
        if (!int.TryParse(Request.Form["ViewIntervalSeconds"], out var viewInterval) || viewInterval < 5)
        {
            TempData["error"] = "The view-interval must be at least 5 seconds.";
            return RedirectToAction(nameof(SettingsGet));
        }
        if (!int.TryParse(Request.Form["RefreshIntervalSeconds"], out var refreshInterval)
            || refreshInterval < 2 || refreshInterval >= viewInterval)
        {
            TempData["error"] = "The refresh-interval must be at least 2 seconds and less than the view-interval.";
            return RedirectToAction(nameof(SettingsGet));
        }

        // Parse the display mode (the ADR 0164 amendment "full page vs. full
        // screen" pin — display-only, never an access input, M35·4). A blank /
        // invalid value falls back to the KioskPlaylist default (FullPage)
        // rather than failing the save — a display preference is not a
        // validation gate (the M35·4 "display-only" precedent: the two
        // interval floors are the only hard validations on the form).
        var displayMode = Enum.TryParse<KioskDisplayMode>(
            Request.Form["DisplayMode"].ToString(), ignoreCase: true, out var parsedMode)
            ? parsedMode
            : KioskDisplayMode.FullPage;

        // Parse the playlist rows from the form (the Views[i].Kind /
        // Views[i].BoardId / Views[i].CalendarView / Views[i].ComponentId /
        // Views[i].Label shape — the view's indexed-name convention). A row
        // with a blank / invalid Kind is dropped (the non-nullable
        // KioskViewKind enum has no "none" value — a blank Kind row is a
        // "not yet set" row, the admin removes it; we never persist an
        // enum-less view). The kind is the row's identity; the other fields
        // are kind-conditional (the KioskView 5-member ceiling, M35·2).
        var views = new List<KioskView>();
        for (var i = 0; ; i++)
        {
            var kindRaw = Request.Form[$"Views[{i}].Kind"];
            if (kindRaw.Count == 0) break;
            if (!Enum.TryParse<KioskViewKind>(kindRaw.ToString(), ignoreCase: true, out var kind))
            {
                continue; // a blank / invalid Kind row — skip (no "none" value)
            }
            views.Add(new KioskView
            {
                Kind = kind,
                BoardId = FormOrNull($"Views[{i}].BoardId"),
                CalendarView = kind == KioskViewKind.Calendar
                    ? (FormOrNull($"Views[{i}].CalendarView") ?? "week")
                    : null,
                ComponentId = kind == KioskViewKind.Todos
                    ? FormOrNull($"Views[{i}].ComponentId")
                    : null,
                Label = FormOrNull($"Views[{i}].Label"),
            });
        }

        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        if (store is null)
        {
            // A test construction with no IDocumentStore — fail loudly (the
            // service's ArgumentNullException is the same contract; a real
            // deployment always carries the store).
            TempData["error"] = "The document store is unavailable.";
            return RedirectToAction(nameof(SettingsGet));
        }

        // Session shape (the M3 same-transaction precedent — the
        // AdminStorageController.Save shape): the controller owns the
        // LightweightSession; the service's SaveChanges is the single
        // in-caller-session write (M35·5 — exactly one kiosk.save audit row).
        await using var session = store.LightweightSession();
        await kiosk.SavePlaylistAsync(
            views,
            viewInterval,
            refreshInterval,
            displayMode,
            actorId,
            session,
            ct).ConfigureAwait(false);

        TempData["info"] = "The kiosk playlist has been saved.";
        return RedirectToAction(nameof(SettingsGet));
    }

    /// <summary>
    /// Reads an optional form field (a blank value → <c>null</c>). The
    /// kind-conditional fields (<see cref="KioskView.BoardId"/> /
    /// <see cref="KioskView.CalendarView"/> / <see cref="KioskView.ComponentId"/>
    /// / <see cref="KioskView.Label"/>) use this so a blank input is a
    /// legitimate "not set" state, not an empty-string sentinel.
    /// </summary>
    private string? FormOrNull(string key)
    {
        var raw = Request.Form[key];
        return raw.Count > 0 && !string.IsNullOrWhiteSpace(raw.ToString()) ? raw.ToString() : null;
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
