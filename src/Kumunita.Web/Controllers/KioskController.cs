using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Kumunita.Core.Events;
using Kumunita.Core.Kiosk;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Kumunita.Web.Services;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// M35 (ADR 0164) — the <c>/kiosk</c> display surface and the
/// <c>/settings/kiosk</c> playlist editor. A **signed-in** full-page /
/// full-screen display that plays the signed-in user's own playlist (the ADR
/// 0164 amendment — per resident) and checks each slide for changes on a
/// faster cadence. ADR 0285 widened the playlist from three slide kinds
/// (board / calendar / to-dos) to almost any item the resident can see, with a
/// per-entry duration, scroll behaviour and comments toggle.
/// <para>
/// <b>Path-1 mechanism (unchanged):</b> each slide is resolved to the item's
/// **own route** and framed by the chrome-hidden stage as an iframe
/// (same-origin). The iframe's navigation **re-runs the item's own read
/// decision** on every load (M35·3), so a slide never reveals more than the
/// viewer could already see on that route. <b>Reuse, not re-author
/// (M35·6).</b> <b>No new authorization surface (M35·1 / M35·3):</b> no
/// <c>AccessAction</c> / <c>AccessVia</c> / <c>Decide()</c> branch here.
/// </para>
/// <para>
/// The classification below calls the item's read seam for the signed-in
/// viewer (through <see cref="IKioskItemCatalog.ResolveAsync"/>; the calendar
/// through <see cref="IEventService.ListInRangeAsync"/>) to decide which shape
/// to render — a slide the viewer cannot see is the <c>kiosk.slide_denied</c>
/// shape (M35-5: no leak, no 404) — and to fingerprint its content. It emits
/// no <c>kiosk.*</c> audit row (M35·5); the only write lane
/// (<c>POST /settings/kiosk</c>) commits exactly one <c>kiosk.save</c> row.
/// </para>
/// </summary>
[Authorize]
public sealed class KioskController : Controller
{
    /// <summary>The floor for an entry's duration and the default duration (seconds).</summary>
    public const int MinDurationSeconds = 5;

    /// <summary>The ceiling for an entry's duration and the default duration (seconds) — one hour.</summary>
    public const int MaxDurationSeconds = 3600;

    /// <summary>The floor for the refresh interval (seconds).</summary>
    public const int MinRefreshSeconds = 2;

    /// <summary>The most entries a playlist keeps (a display cap, never a gate).</summary>
    public const int MaxEntries = 50;

    private static readonly Regex EntryTypeKey = new(@"^Entries\[(\d{1,4})\]\.Type$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IKioskService kiosk;
    private readonly IEventService events;
    private readonly EffectiveTimezoneResolver? timezone;
    private readonly IDocumentStore? store;
    private readonly IKioskItemCatalog? catalog;

    public KioskController(
        IKioskService kiosk,
        IEventService events,
        EffectiveTimezoneResolver? timezone = null,
        IDocumentStore? store = null,
        IKioskItemCatalog? catalog = null)
    {
        this.kiosk = kiosk ?? throw new ArgumentNullException(nameof(kiosk));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.timezone = timezone;
        this.store = store;
        // ADR 0285 — the item picker + the per-entry resolution. Optional so a
        // test can construct the controller without it; DI always supplies it
        // (Program.cs). Without it every non-calendar entry resolves hidden.
        this.catalog = catalog;
    }

    /// <summary>
    /// <c>GET /kiosk</c> — the signed-in viewer's own playlist (the ADR 0164
    /// amendment), classified slide by slide; an empty or missing playlist
    /// renders the <c>kiosk.empty</c> message (M35-8).
    /// </summary>
    [HttpGet("/kiosk")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        var playlist = await kiosk.GetPlaylistAsync(actorId, ct).ConfigureAwait(false);

        var slides = new List<KioskSlideViewModel>();
        if (playlist is not null)
        {
            var roles = KumunitaPrincipal.RoleSet(User);
            for (var i = 0; i < playlist.Entries.Count; i++)
            {
                slides.Add(await Classify(playlist.Entries[i], i, actorId, roles, ct).ConfigureAwait(false));
            }
        }

        var vm = new KioskDisplayViewModel
        {
            Slides = slides,
            DefaultDurationSeconds = playlist?.DefaultDurationSeconds ?? 30,
            RefreshIntervalSeconds = playlist?.RefreshIntervalSeconds ?? 10,
            DisplayMode = playlist?.DisplayMode ?? KioskDisplayMode.FullPage,
            ScrollSpeed = playlist?.ScrollSpeed ?? KioskScrollSpeed.Medium,
        };

        // The full-bleed opt-out (_Layout's ViewData["FullBleed"]) — the stage
        // runs flush to the edges; the view's inline <style> hides the chrome
        // (the ADR 0104 chrome-hide idiom, M35·6).
        ViewData["FullBleed"] = true;
        ViewData["Title"] = "Kiosk";
        return View(vm);
    }

    /// <summary>
    /// <c>GET /kiosk/slide?slide=N&amp;t=&lt;ts&gt;</c> — the single-slide
    /// refresh fetch (<c>client/lib/kiosk.ts</c>). A CSRF-free GET (the cookie
    /// is the authorization, the M12 iCal precedent); <c>t</c> busts caches so
    /// the read seam re-runs. Returns the Nth slide's href, or the
    /// <c>kiosk.slide_denied</c> shape (M35-5, not a 404), with its content
    /// fingerprint.
    /// </summary>
    [HttpGet("/kiosk/slide")]
    public async Task<IActionResult> Slide(int slide, long t = 0, CancellationToken ct = default)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        var playlist = await kiosk.GetPlaylistAsync(actorId, ct).ConfigureAwait(false);
        var refresh = new KioskSlideRefreshViewModel { Index = slide };

        if (playlist is null || slide < 0 || slide >= playlist.Entries.Count)
        {
            // Out of range — the stage falls back client-side. Not a 404 and
            // not a leak (no slide is named).
            refresh.Denied = true;
            refresh.DeniedKey = "kiosk.slide_denied";
            refresh.Fingerprint = KioskFingerprint.Of("denied");
            return Json(refresh);
        }

        var klass = await Classify(playlist.Entries[slide], slide, actorId, KumunitaPrincipal.RoleSet(User), ct).ConfigureAwait(false);
        refresh.Denied = klass.Denied;
        refresh.Href = klass.Href;
        refresh.DeniedKey = klass.Denied ? "kiosk.slide_denied" : null;
        refresh.Fingerprint = klass.Fingerprint;
        return Json(refresh);
    }

    // ─────────────────────────────────────────────────────────────────────
    // The resident's playlist editor (/settings/kiosk). The gate is
    // [Authorize] (any signed-in user edits their own playlist); the service
    // owns the single kiosk.save audit row + the upsert (M35·5).
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /settings/kiosk</c> — the signed-in resident's playlist editor.
    /// Each stored entry is shown with its item's current title as the viewer
    /// can see it (resolved through the item's own read seam — an item that is
    /// gone or no longer visible is flagged, never named).
    /// </summary>
    [HttpGet("/settings/kiosk")]
    public async Task<IActionResult> SettingsGet(CancellationToken ct)
    {
        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        var playlist = await kiosk.GetPlaylistAsync(actorId, ct).ConfigureAwait(false);
        var roles = KumunitaPrincipal.RoleSet(User);

        var rows = new List<KioskEntryEditorRow>();
        foreach (var entry in playlist?.Entries ?? Array.Empty<KioskEntry>())
        {
            var resolved = catalog is null
                ? KioskResolvedItem.Hidden
                : await catalog.ResolveAsync(entry, actorId, roles, ct).ConfigureAwait(false);
            rows.Add(new KioskEntryEditorRow(entry, resolved.Title, resolved.Visible));
        }

        var vm = new KioskSettingsViewModel
        {
            Entries = rows,
            DefaultDurationSeconds = playlist?.DefaultDurationSeconds ?? 30,
            RefreshIntervalSeconds = playlist?.RefreshIntervalSeconds ?? 10,
            DisplayMode = playlist?.DisplayMode ?? KioskDisplayMode.FullPage,
            ScrollSpeed = playlist?.ScrollSpeed ?? KioskScrollSpeed.Medium,
        };

        ViewData["Title"] = "Kiosk settings";
        return View("Settings", vm);
    }

    /// <summary>
    /// <c>GET /settings/kiosk/items?type=&lt;KioskItemType&gt;&amp;q=&lt;text&gt;</c>
    /// — the playlist editor's item picker (ADR 0285). Returns
    /// <c>{ items: [{ id, title, groupId, subtitle }] }</c>: the items of the
    /// type the signed-in resident can see, the most recent first when
    /// <c>q</c> is blank, a title / body match otherwise (at most
    /// <see cref="IKioskItemCatalog.MaxResults"/>). For a Calendar the first
    /// option is "All" (empty id), then the enabled community components.
    /// A CSRF-free GET (a read, the <c>/kiosk/slide</c> shape); an unknown type
    /// is a 400. The catalog composes the existing read seams only, so no item
    /// the resident cannot already see is ever offered.
    /// </summary>
    [HttpGet("/settings/kiosk/items")]
    public async Task<IActionResult> Items(string? type, string? q, CancellationToken ct = default)
    {
        if (!TryParseType(type, out var itemType))
            return BadRequest();

        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        var options = new List<KioskItemOption>();

        if (itemType == KioskItemType.Calendar)
        {
            var allLabel = await this.FlashAsync("kiosk.settings.calendar_all", "All communities");
            var query = (q ?? string.Empty).Trim();
            if (query.Length == 0 || allLabel.Contains(query, StringComparison.OrdinalIgnoreCase))
                options.Add(new KioskItemOption(string.Empty, allLabel, null, null));
        }

        if (catalog is not null)
        {
            options.AddRange(await catalog.SearchAsync(itemType, q, actorId, ct).ConfigureAwait(false));
        }

        return Json(new
        {
            items = options.Select(o => new { id = o.Id, title = o.Title, groupId = o.GroupId, subtitle = o.Subtitle }),
        });
    }

    /// <summary>
    /// <c>GET /admin/kiosk</c> — retired address (the ADR 0164 amendment moved
    /// the editor to <c>/settings/kiosk</c>); kept as a redirect so an old link
    /// still lands on the live page.
    /// </summary>
    [HttpGet("/admin/kiosk")]
    public IActionResult AdminKiosk()
    {
        return Redirect("/settings/kiosk");
    }

    /// <summary>
    /// <c>POST /settings/kiosk</c> — saves the signed-in resident's playlist
    /// (ADR 0285). Reads the display settings (display mode, default duration
    /// &gt;= 5, refresh interval &gt;= 2, scroll speed) and the
    /// <c>Entries[i].*</c> rows (in ascending index order — the editor's DOM
    /// order; gaps from removed rows are fine): Type, ItemId, GroupId,
    /// BoardView, CalendarView, DurationSeconds, Scroll, ShowComments, Label.
    /// A row without a type is skipped; a row of any type but Calendar needs an
    /// item; a Group post needs its group; a duration outside 5–3600 seconds is
    /// refused. Then <see cref="IKioskService.SavePlaylistAsync"/> (the one
    /// <c>kiosk.save</c> audit row, <c>Via = Owner</c>, M35·5) and
    /// redirect-after-POST.
    /// </summary>
    [HttpPost("/settings/kiosk")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SettingsPost(CancellationToken ct)
    {
        if (!int.TryParse(Request.Form["DefaultDurationSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var defaultDuration)
            || defaultDuration < MinDurationSeconds || defaultDuration > MaxDurationSeconds)
        {
            TempData["error"] = await this.FlashAsync("kiosk.flash_default_duration", "The default duration must be between 5 and 3600 seconds.");
            return RedirectToAction(nameof(SettingsGet));
        }
        if (!int.TryParse(Request.Form["RefreshIntervalSeconds"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var refreshInterval)
            || refreshInterval < MinRefreshSeconds)
        {
            TempData["error"] = await this.FlashAsync("kiosk.flash_refresh_interval", "The refresh interval must be at least 2 seconds.");
            return RedirectToAction(nameof(SettingsGet));
        }

        // Display preferences — a blank / invalid value falls back to the
        // default (a display preference is not a validation gate).
        var displayMode = ParseEnum(Request.Form["DisplayMode"].ToString(), KioskDisplayMode.FullPage);
        var scrollSpeed = ParseEnum(Request.Form["ScrollSpeed"].ToString(), KioskScrollSpeed.Medium);

        var indices = Request.Form.Keys
            .Select(k => EntryTypeKey.Match(k))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
            .Distinct()
            .Order()
            .ToList();

        var entries = new List<KioskEntry>();
        foreach (var i in indices)
        {
            if (!TryParseType(Request.Form[$"Entries[{i}].Type"].ToString(), out var type))
                continue;   // a row with no type chosen — left out

            var itemId = FormOrNull($"Entries[{i}].ItemId");
            var groupId = FormOrNull($"Entries[{i}].GroupId");
            if ((type != KioskItemType.Calendar && itemId is null)
                || (type == KioskItemType.GroupPost && groupId is null))
            {
                TempData["error"] = await this.FlashAsync("kiosk.flash_entry_item", "Pick an item for every entry, or remove the entry.");
                return RedirectToAction(nameof(SettingsGet));
            }

            var duration = defaultDuration;
            var durationRaw = FormOrNull($"Entries[{i}].DurationSeconds");
            if (durationRaw is not null
                && (!int.TryParse(durationRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out duration)
                    || duration < MinDurationSeconds || duration > MaxDurationSeconds))
            {
                TempData["error"] = await this.FlashAsync("kiosk.flash_entry_duration", "Each entry must be shown for between 5 and 3600 seconds.");
                return RedirectToAction(nameof(SettingsGet));
            }

            entries.Add(new KioskEntry
            {
                Type = type,
                ItemId = itemId,
                // The group id rides only on the two group-routed types.
                GroupId = type is KioskItemType.GroupPost or KioskItemType.Event ? groupId : null,
                BoardView = type == KioskItemType.Board
                    ? ParseEnum(Request.Form[$"Entries[{i}].BoardView"].ToString(), KioskBoardView.Board)
                    : KioskBoardView.Board,
                CalendarView = type == KioskItemType.Calendar
                    ? NormalizeCalendarView(FormOrNull($"Entries[{i}].CalendarView"))
                    : null,
                DurationSeconds = duration,
                Scroll = ParseEnum(Request.Form[$"Entries[{i}].Scroll"].ToString(), KioskScrollBehavior.None),
                ShowComments = IsChecked($"Entries[{i}].ShowComments"),
                Label = FormOrNull($"Entries[{i}].Label"),
            });

            if (entries.Count >= MaxEntries)
                break;
        }

        var actorId = KumunitaPrincipal.SubjectId(User) ?? string.Empty;
        if (store is null)
        {
            TempData["error"] = await this.FlashAsync("kiosk.flash_store_unavailable", "The document store is unavailable.");
            return RedirectToAction(nameof(SettingsGet));
        }

        await using var session = store.LightweightSession();
        await kiosk.SavePlaylistAsync(
            new KioskPlaylistDraft(entries, defaultDuration, refreshInterval, displayMode, scrollSpeed),
            actorId,
            session,
            ct).ConfigureAwait(false);

        TempData["info"] = await this.FlashAsync("kiosk.flash_saved", "The kiosk playlist has been saved.");
        return RedirectToAction(nameof(SettingsGet));
    }

    /// <summary>Reads an optional form field (blank → <c>null</c>).</summary>
    private string? FormOrNull(string key)
    {
        var raw = Request.Form[key];
        return raw.Count > 0 && !string.IsNullOrWhiteSpace(raw.ToString()) ? raw.ToString().Trim() : null;
    }

    /// <summary>A checkbox: checked when any submitted value is "true" / "on"
    /// (a checkbox + hidden-false pair posts both).</summary>
    private bool IsChecked(string key) =>
        Request.Form[key].Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(v, "on", StringComparison.OrdinalIgnoreCase));

    private static bool TryParseType(string? raw, out KioskItemType type) =>
        Enum.TryParse(raw ?? string.Empty, ignoreCase: true, out type)
        && Enum.IsDefined(type)
        && !int.TryParse(raw, out _);   // names only — a bare number is not a type

    private static T ParseEnum<T>(string? raw, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(raw ?? string.Empty, ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
        && !int.TryParse(raw, out _)
            ? parsed
            : fallback;

    // ─────────────────────────────────────────────────────────────────────
    // Slide classification — the item's read seam, re-run for the signed-in
    // viewer (M35·3). A slide the viewer cannot see → the denied shape
    // (M35-5: no leak, no 404). A display decision only (no kiosk.* row).
    // ─────────────────────────────────────────────────────────────────────

    private async Task<KioskSlideViewModel> Classify(
        KioskEntry entry, int index, string actorId, IReadOnlySet<string> roles, CancellationToken ct)
    {
        KioskResolvedItem resolved = catalog is null
            ? KioskResolvedItem.Hidden
            : await catalog.ResolveAsync(entry, actorId, roles, ct).ConfigureAwait(false);

        if (!resolved.Visible || resolved.Href is not { } baseHref)
            return DeniedSlide(index, entry);

        var version = resolved.Version;
        if (entry.Type == KioskItemType.Calendar)
        {
            // The calendar's content is the events in the viewer's window
            // (zone-dependent), so its version is built here.
            try
            {
                version = await CalendarVersionAsync(entry, actorId, ct).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return DeniedSlide(index, entry);
            }
        }

        // ADR 0285 D5 — comments / replies / RSVP lists are hidden on a slide
        // unless the entry shows them; the flag rides to the framed page as a
        // query parameter that _Layout turns into the kmb-kiosk-comments body
        // class (display-only — the framed route's own read is unchanged).
        //
        // ADR 0285 D7 — every slide also carries ?kiosk=1, which _Layout turns
        // into the kmb-kiosk body class. This is the AUTHORITATIVE kiosk signal:
        // it is part of the framed route's URL, so it arrives intact regardless
        // of the site's global Referrer-Policy: no-referrer (F7), which means a
        // slide iframe sends NO Referer header and the old referrer-based test
        // in _Layout never fired — so the kiosk CSS never activated. The
        // onboarding modal's iframes do NOT carry this param, so they stay
        // interactive (the kmb-embed floor still applies to both).
        var href = AppendQuery(baseHref, KioskQueryKey, "1");
        if (entry.ShowComments && HasDiscussion(entry.Type))
            href = AppendQuery(href, CommentsQueryKey, "1");

        return new KioskSlideViewModel(
            index, entry.Type, entry.Label, href, Denied: false,
            Fingerprint: KioskFingerprint.Of(version),
            DurationSeconds: entry.DurationSeconds,
            Scroll: entry.Scroll,
            ShowComments: entry.ShowComments,
            Title: resolved.Title);
    }

    /// <summary>The query parameter the kiosk appends to EVERY slide href to
    /// mark the framed page as a kiosk slide (ADR 0285 D7; _Layout turns it into
    /// the kmb-kiosk body class). It is the authoritative kiosk signal — part of
    /// the URL, so it survives the global Referrer-Policy: no-referrer (F7) that
    /// suppresses the Referer header a referrer-based test would have relied on.
    /// The onboarding modal's iframes never carry it, so they stay interactive.</summary>
    public const string KioskQueryKey = "kiosk";

    /// <summary>The query parameter that asks a framed page to keep its
    /// comments / replies / RSVP list visible (ADR 0285 D5; read by _Layout).</summary>
    public const string CommentsQueryKey = "kioskComments";

    /// <summary>The types whose detail page has a comment thread, replies or
    /// an RSVP list (ADR 0285 D5).</summary>
    internal static bool HasDiscussion(KioskItemType type) => type is
        KioskItemType.Todo or KioskItemType.Event or KioskItemType.Announcement
        or KioskItemType.CommunityPost or KioskItemType.GroupPost;

    private static string AppendQuery(string href, string key, string value) =>
        href + (href.Contains('?') ? "&" : "?") + Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value);

    private async Task<string> CalendarVersionAsync(KioskEntry entry, string actorId, CancellationToken ct)
    {
        var viewName = NormalizeCalendarView(entry.CalendarView);
        var zone = timezone is not null ? await timezone.GetAsync().ConfigureAwait(false) : null;
        var (start, end) = CalendarWindow(viewName, zone);
        var componentId = string.IsNullOrWhiteSpace(entry.ItemId) ? null : entry.ItemId;
        var eventsInWindow = await events.ListInRangeAsync(start, end, componentId, actorId, ct).ConfigureAwait(false);
        return BuildCalendarFingerprint(viewName, eventsInWindow);
    }

    /// <summary>
    /// A denied slide's stable shape — a fixed fingerprint distinct from any
    /// content digest, so a visible↔denied flip is always detected. The
    /// duration / scroll still apply (the denied message is shown for the
    /// entry's time).
    /// </summary>
    private static KioskSlideViewModel DeniedSlide(int index, KioskEntry entry)
    {
        return new KioskSlideViewModel(
            index, entry.Type, entry.Label, Href: null, Denied: true,
            Fingerprint: KioskFingerprint.Of("denied"),
            DurationSeconds: entry.DurationSeconds,
            Scroll: KioskScrollBehavior.None,
            ShowComments: false,
            Title: null);
    }

    /// <summary>The calendar window's display-relevant content as a canonical
    /// string (the view unit + each in-range event's id / title / start, in
    /// window order) for the <see cref="KioskFingerprint"/> digest.</summary>
    private static string BuildCalendarFingerprint(string viewName, IReadOnlyList<Event> events)
    {
        var sb = new StringBuilder(64);
        sb.Append('E').Append('/').Append(viewName);
        foreach (var e in events)
        {
            sb.Append('|').Append(e.Id).Append('/').Append(e.Title).Append('/').Append(e.Start.ToString("O"));
        }
        return sb.ToString();
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
