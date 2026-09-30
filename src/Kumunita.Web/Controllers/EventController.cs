using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.Tags;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kumunita.Web.Controllers;

/// <summary>
/// The <c>/events</c> surface (M4 — the first *content-with-time* lane, ADR 0054):
/// a resident sees the neighborhood's upcoming events, RSVPs, and (their own
/// draft) publishes it. A *thin* HTTP layer (ADR 0006-D): routes + authz +
/// shape; every access decision comes from the frozen <see cref="IEventService"/>
/// seam (the single decision path, C1–C6) — the controller never re-derives
/// access (the M3 <see cref="PostsController"/> precedent, "route + authz +
/// shape").
/// <list type="bullet">
/// <item><c>GET /events</c> — the feed: the upcoming events the caller may
/// read (<see cref="IEventService.ListUpcomingAsync"/>'s <c>CanSeeAsync(Read)</c>
/// gate is the sole reader), ordered by <see cref="Event.Start"/>, paged; the
/// <c>componentId</c> query is a *filter, never a gate* (C-M3·2).</item>
/// <item><c>GET /events/{id}</c> — the detail: one event's full body (a single
/// <c>CanAsync(Read)</c> decision, the 404-vs-403 split, C3). A draft is
/// visible to its author only (ADR 0037).</item>
/// <item><c>GET/POST /events/new</c> — the composer (any signed-in resident
/// becomes the author, §3.4 <c>Owner</c>); a WYSIWYG rich body (the one
/// <c>bindRichEditor</c>, ADR 0031) + the M2 <see
/// cref="AudienceEditorModel"/> (the sole access boundary) + the authored-in
/// language picker (ADR 0018) + the component feed-organizer picker.</item>
/// <item><c>GET/POST /events/{id}/edit</c> — the edit lane (author or
/// GlobalAdmin, §3.4); the stored row is the gate's source.</item>
/// <item><c>POST /events/{id}/publish</c> — the ADR 0037 author-only publish
/// (a non-author, even a GlobalAdmin, is denied).</item>
/// <item><c>POST /events/{id}/delete</c> — the ADR 0024 author soft-delete
/// lane.</item>
/// <item><c>POST /events/{id}/rsvp</c> — the last-write-wins RSVP (no audit
/// row — a routine resident action, not an access decision).</item>
/// <item><c>POST /events/{id}/translations[...]</c> — the ADR 0059
/// user-added translation lanes (add / update / remove; standing — author /
/// Translator / GlobalAdmin — re-pinned server-side; each write commits an
/// <c>eventtranslation.*</c> audit row atomically, C3).</item>
/// </list>
/// <para>
/// **ADR 0006-D:** a thin HTTP layer — the visibility split is the service's,
/// never re-derived here. The principal's subject id + role set is minted from
/// the signed-in cookie (<see cref="KumunitaPrincipal"/>), passed to the
/// service as the <c>actorId</c> (+ the role set for the §3.4 standing
/// affordances) — no DB re-read for role shape (the claim set is the
/// principal, ADR 0006-B); the author's *display name* (a read surface only,
/// never an access decision) is a plain <see
/// cref="IUserInfoService.GetProfileAsync"/> read.
/// </para>
/// </summary>
[Authorize]
public sealed class EventController : Controller
{
    private readonly IEventService events;
    private readonly IUserInfoService userInfo;
    private readonly ILocalizationService localization;
    private readonly IDocumentStore store;
    private readonly EffectiveTimezoneResolver timezone;
    private readonly ITranslationProvider? translationProvider;
    // M12 (ADR 0112, §field-map) — the tag read seam, resolving the
    // <c>CATEGORIES</c> display names the iCal lanes hand to the pure
    // <see cref="IcsWriter"/> emitter (a read, not a decision — C-TG·8).
    // **Optional** (default null) so the existing test-construction sites
    // that build this controller without a tag service keep compiling —
    // the CATEGORIES surface is a no-op when the seam is absent (the
    // <see cref="PostsController"/> idiom). DI always supplies the live
    // <c>ITagService</c> in the app.
    private readonly ITagService? tags;

    // ADR 0115 D2 (M14 interlock) — the event detail's **linked to-dos**
    // section (the reverse read seam's page, mapped to rows) resolves through
    // the <see cref="IProjectService"/><see
    // cref="IProjectService.ListTodosForEventAsync"/> seam (U01) — the seam's
    // <c>CanSeeAsync(Read)</c> pass is the gate (C-M14·4 — no new
    // authorization surface; the composition stays in this controller, never
    // a <c>Core.Events</c> → <c>Core.Projects</c> dependency). **Optional**
    // (default null) so the existing test-construction sites that build this
    // controller without a project service keep compiling (the
    // <see cref="ProjectsController"/> idiom); absent, the section simply
    // omits.
    private readonly IProjectService? projects;

    public EventController(
        IEventService events,
        IUserInfoService userInfo,
        ILocalizationService localization,
        IDocumentStore store,
        EffectiveTimezoneResolver timezone,
        ITranslationProvider? translationProvider = null,
        ITagService? tags = null,
        IProjectService? projects = null)
    {
        this.events = events;
        this.userInfo = userInfo;
        this.localization = localization;
        this.store = store;
        this.timezone = timezone;
        this.translationProvider = translationProvider;
        this.tags = tags;
        this.projects = projects;
    }
    private static string? SubjectId(ClaimsPrincipal user) =>
        KumunitaPrincipal.SubjectId(user);

    private static IReadOnlySet<string> RoleSet(ClaimsPrincipal user) =>
        KumunitaPrincipal.RoleSet(user);

    // ── Seed helpers (the M2 / M3 single-source pin — mirrored, not re-invented)

    /// <summary>
    /// Seeds the composer's "Who to grant to" option lists for the shared
    /// <c>Views/Shared/_GrantPickers</c> partial — the same option source the
    /// M2 <see cref="ProfileController"/> editor + M3 <see cref="PostsController"/>
    /// composer use: <b>Users</b> — every visible, non-blocked, verified
    /// <c>Profile</c> except the actor themselves; <b>Groups</b> — the platform
    /// public group list (a private group is an organizing unit, never granted as
    /// an audience, ADR 0010). Stored on <see cref="Controller.ViewData"/>
    /// (read-only view data — never model properties on
    /// <see cref="EventEditorModel"/>; the only form-bound grants field remains
    /// the partial's hidden <c>Audience.Grants</c> textarea).
    /// </summary>
    private async Task SeedGrantPickerOptionsAsync()
    {
        var profiles = await userInfo.GetProfilesAsync(verifiedOnly: true);
        var selfId = SubjectId(User);
        var userOptions = profiles
            .Where(p => !p.Blocked)
            .Where(p => !string.Equals(p.SubjectId, selfId, StringComparison.Ordinal))
            .Select(p => new GrantOption
            {
                Id    = p.SubjectId,
                Label = string.IsNullOrWhiteSpace(p.DisplayName) ? p.SubjectId : p.DisplayName,
                Kind  = "User",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var groups = await userInfo.GetPublicGroupsAsync();
        var groupOptions = groups
            .Select(g => new GrantOption
            {
                Id    = g.Id,
                Label = string.IsNullOrWhiteSpace(g.Name) ? g.Id : g.Name,
                Kind  = "Group",
            })
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        ViewData["Audience_Users"] = userOptions;
        ViewData["Audience_Groups"] = groupOptions;
    }

    /// <summary>
    /// Seeds the composer's <b>authored-in language</b> picker (ADR 0018,
    /// ADR 0005 B) — the instance's **enabled** <see cref="LanguageCatalog"/>,
    /// ordered by <c>SortOrder</c>, read through
    /// <see cref="ILocalizationService.ListLanguagesAsync"/> (the HTTP-free seam,
    /// ADR 0005 D — the exact catalog read the <see cref="LocaleController.Index"/>
    /// page uses). The create lane pre-selects the actor's current effective
    /// language (ADR 0049 — <see cref="ResolveComposeDefaultLanguageAsync"/>)
    /// so the picker highlights the language the resident is reading the
    /// platform in.
    /// </summary>
    private async Task<IReadOnlyList<(string Code, string NativeName)>> SeedLanguagePickerAsync()
    {
        var catalog = await localization.ListLanguagesAsync().ConfigureAwait(false);
        return catalog
            .Where(l => l.Enabled)
            .OrderBy(l => l.SortOrder)
            .Select(l => (l.Id, l.NativeName))
            .ToList();
    }

    /// <summary>
    /// The create-lane composer's <b>default authored-in language</b> (the
    /// picker's pre-selection): the actor's per-request **effective**
    /// language (ADR 0049 — the <c>kumunita.locale</c> cookie → first enabled
    /// <c>Accept-Language</c> match → instance default → <c>en</c> floor, the
    /// exact <see cref="EffectiveLanguageCode.ResolveAsync"/> chain the
    /// <c>&lt;kw-l&gt;</c> TagHelper resolves UI strings through) — "write in
    /// the language you're reading in", the ADR 0018 "instance default"
    /// pre-selection generalized. A null <see cref="ITranslationProvider"/>
    /// (test-construction site) falls back to the instance default — the
    /// legacy behavior, so the existing mock sites keep passing.
    /// </summary>
    private async Task<string> ResolveComposeDefaultLanguageAsync()
    {
        if (translationProvider is null)
            return await localization.GetDefaultLanguageCodeAsync().ConfigureAwait(false);
        return await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider).ConfigureAwait(false);
    }

    /// <summary>
    /// Seeds the composer's component *feed-organizer* picker (C-M3·2) — the
    /// instance's **enabled** <see cref="Component"/> set (a *filter, never a
    /// gate*; the audience is the sole access boundary on the form).
    /// </summary>
    private async Task<IReadOnlyList<(string Id, string Name)>> SeedComponentPickerAsync()
    {
        var components = await userInfo.GetComponentsAsync(enabledOnly: true);
        return components
            .Select(c => (c.Id, Name: string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// ADR 0051 — in place, swap <paramref name="event"/>'s Title/Body to the
    /// translation row in the viewer's current language when one exists: the
    /// translation's body (required on the row), and its title when non-blank
    /// (otherwise the authored title is kept — the ADR 0022 floor). A read, not
    /// a decision: the event's <c>CanSeeAsync</c> already ran in the feed read.
    /// No content is generated, rewritten, or fetched — only the exact
    /// human-authored row (ADR 0018/0022) is selected. A null
    /// <see cref="translationProvider"/> (test construction site) is a no-op —
    /// the authored-in text stays, exactly the <see cref="PostsController
    /// .ApplyTranslationToPostAsync"/> shape.
    /// </summary>
    private async Task ApplyEventTranslationAsync(Event @event, string effLang)
    {
        if (translationProvider is null)
            return;
        var translations = await this.events.GetEventTranslationsAsync(@event.Id);
        var match = translations.FirstOrDefault(t => String.Equals(t.LanguageCode, effLang, StringComparison.OrdinalIgnoreCase));
        if (match is null)
            return;
        if (!string.IsNullOrWhiteSpace(match.Title))
            @event.Title = match.Title; // blank translation title → the authored-in title
        @event.Body = match.Body; // Body is required on a translation row
    }

    /// <summary>
    /// Resolves a BCP-47 code to its catalog <c>NativeName</c> for a
    /// <c>TempData</c> confirmation message (a display convenience — a
    /// <see cref="Kumunita.Core.Localization.ILocalizationService
    /// .ListLanguagesAsync"/> read, not a decision). Falls back to the raw
    /// code when the language is not in the catalog (a never-blank shape — the
    /// <see cref="PostsController.SeedLanguageName"/> idiom).
    /// </summary>
    private async Task<string> SeedLanguageName(string code)
    {
        var catalog = await localization.ListLanguagesAsync();
        return catalog.FirstOrDefault(l => l.Id == code)?.NativeName ?? code;
    }

    /// <summary>
    /// M18 (ADR 0119, D2 / D7 / D3) — maps the composer's recurrence picker
    /// (<see cref="EventEditorModel.Recurrence"/> /
    /// <see cref="EventEditorModel.RecurrenceInterval"/> /
    /// <see cref="EventEditorModel.EndsAfterCount"/> /
    /// <see cref="EventEditorModel.EndsOnDate"/>) onto the Core
    /// <see cref="EventRecurrenceRule"/> the U02 / U04 create / edit lanes
    /// read (<see cref="CreateEventRequest.Recurrence"/> /
    /// <see cref="UpdateEventRequest.Recurrence"/>). A "none" / empty /
    /// unparseable <c>Recurrence</c> posts <c>null</c> (the zero-change
    /// branch, GATE-2's <c>Create_With_No_Rule_Behaves_Exactly_As_Today</c>
    /// pin). Otherwise: the parsed <see cref="Recurrence"/> enum, the
    /// interval (≥ 1, default 1), and the mutually-exclusive end condition
    /// (D7 — <c>EndsAfterCount</c> → <c>Count</c>, <c>EndsOnDate</c> →
    /// <c>Ends</c>). The <c>Ends</c> wall-clock date is converted to a UTC
    /// instant using the actor's effective zone (the ADR 0019 floor — the
    /// same <c>GetUtcOffset</c> idiom the <c>Start</c> / <c>End</c> map
    /// through), so the "Ends before the head's Start" comparison the design
    /// doc §7 pins is zone-consistent. Standing is unchanged (C-M18·5) —
    /// this is a shape mapping, never an access decision.
    /// </summary>
    private static EventRecurrenceRule? BuildRecurrenceRule(
        EventEditorModel model, TimeZoneInfo zone)
    {
        // Zero-change branch (D2 / GATE-2): "none" / empty / unparseable →
        // null (a single, non-recurring event — the existing behavior).
        if (!Enum.TryParse<Recurrence>(model.Recurrence, ignoreCase: true, out var rec)
            || rec == Recurrence.None)
        {
            return null;
        }
        var interval = model.RecurrenceInterval ?? 1;
        if (interval < 1) interval = 1;
        DateTimeOffset? ends = null;
        if (model.EndsOnDate is { } d)
            ends = new DateTimeOffset(d, zone.GetUtcOffset(d));
        return new EventRecurrenceRule
        {
            Recurrence = rec,
            Interval = interval,
            Count = model.EndsAfterCount,
            Ends = ends,
        };
    }

    // ── Read lanes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /events</c> — the feed (upcoming events the caller may read). The
    /// service's <see cref="IEventService.ListUpcomingAsync"/> <c>CanSeeAsync
    /// (Read)</c> gate is the sole reader (drafts + deleted rows excluded for
    /// non-authors); <see cref="Event.Start"/> ascending order; paged. The
    /// <paramref name="componentId"/> query is a filter (C-M3·2), never a gate.
    /// Author + component display names are *read* lookups (never access
    /// decisions).
    /// <para>
    /// **ADR 0065 (the <c>EV-MINE</c> lane):** the same read also resolves the
    /// viewer's **own upcoming events** (<see cref="IEventService
    /// .ListMineAsync"/> — their RSVPed events, any status, ∪ their authored
    /// events, upcoming + live only) and hands them to the view as
    /// <see cref="EventIndexViewModel.MyEvents"/>. The section is rendered
    /// first, before the feed, and only when non-empty. No
    /// <c>AccessAudit</c> row (the per-row write lane already committed its
    /// decision — the <see cref="IEventService.GetMyRsvpAsync"/> posture); the
    /// union is inherently non-leaky (a non-author can only hold an RSVP row on
    /// an event they may already read — <c>RsvpAsync</c> gates standing first).
    /// </para>
    /// </summary>
    [HttpGet("/events")]
    public async Task<IActionResult> Index(string? componentId, int page = 1, bool past = false)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        IReadOnlyList<Event> events;
        bool hasMore;
        try
        {
            // ADR 0109 (EV-PAST) — the "Past" option is an additive read lane on
            // the same seam: past = true routes to ListPastAsync (Start < now,
            // most-recent-first); the default (false) keeps ListUpcomingAsync
            // verbatim (the ADR 0097 additive-surface precedent).
            var pageResult = past
                ? await this.events.ListPastAsync(componentId, actorId, page, HttpContext.RequestAborted)
                : await this.events.ListUpcomingAsync(componentId, actorId, page, HttpContext.RequestAborted); // ADR 0090 D1/D3 — the paging signal is the page's .HasMore.
            events = pageResult.Items;
            hasMore = pageResult.HasMore;
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // ADR 0065 (the EV-MINE lane) — the viewer's own upcoming events: the
        // union of their RSVPed events (any status — the row is the sign-up)
        // and their authored events, upcoming + live only. No AccessAudit row
        // (the GetMyRsvpAsync posture — each row's write lane already committed
        // its decision); the union is inherently non-leaky (a non-author can
        // only see an event they hold an RSVP row on — and RsvpAsync gates
        // standing first, so a denied actor never holds such a row). Empty for
        // a viewer with no sign-ups — the view hides the whole section then.
        IReadOnlyList<Event> myEvents = Array.Empty<Event>();
        if (actorId.Length > 0)
        {
            try
            {
                myEvents = await this.events.ListMineAsync(actorId, HttpContext.RequestAborted);
            }
            catch (UnauthorizedAccessException)
            {
                return new ForbidResult();
            }
        }

        // ADR 0051 — extend ADR 0049's default-visible-variant rule to the feed
        // surface: each row shows the event's title/body in the viewer's current
        // language when a translation exists (else the authored-in text — the
        // ADR 0022 floor). One read of the shared per-request chain; a "a read,
        // not a decision" surface (the feed's CanSeeAsync already ran). No Core /
        // schema change (the posts feed's ADR 0051 idiom). The EV-MINE rows get
        // the same treatment — the viewer's own events are a list surface.
        if (translationProvider is not null)
        {
            var effLang = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            foreach (var e in events)
                await ApplyEventTranslationAsync(e, effLang);
            foreach (var e in myEvents)
                await ApplyEventTranslationAsync(e, effLang);
        }

        // Author display names (a *read* lookup, never an access decision — the
        // audience gate already ran in ListUpcomingAsync; the EV-MINE union is
        // inherently the viewer's own rows). Missing profile → the raw subject
        // id.
        var authorIds = events
            .Concat(myEvents)
            .Select(e => e.AuthorId).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();
        var authorName = new Dictionary<string, string>();
        foreach (var authorId in authorIds)
        {
            var profile = await userInfo.GetProfileAsync(authorId);
            authorName[authorId] =
                profile?.DisplayName is not null && profile.DisplayName.Length > 0
                    ? profile.DisplayName : authorId;
        }

        // Component display names (a *read* lookup, never a gate — C-M3·2).
        var componentIds = events
            .Concat(myEvents)
            .Select(e => e.ComponentId).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var allComponents = await userInfo.GetComponentsAsync(enabledOnly: true);
        var componentById = allComponents
            .Where(c => componentIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name);

        static EventRow ProjectRow(Event e, IReadOnlyDictionary<string, string> authorName,
                                   IReadOnlyDictionary<string, string> componentById)
            => new EventRow(
                Id: e.Id,
                Title: e.Title,
                Body: e.Body,
                Start: e.Start,
                End: e.End,
                Location: e.Location,
                AuthorId: e.AuthorId,
                AuthorDisplayName: authorName.TryGetValue(e.AuthorId, out var an) ? an : e.AuthorId,
                ComponentId: e.ComponentId,
                ComponentDisplayName: e.ComponentId is not null && componentById.TryGetValue(e.ComponentId, out var cn) ? cn : null,
                IsDraft: e.IsDraft,
                IsDeleted: e.IsDeleted,
                Color: e.Color);

        var rows = events.Select(e => ProjectRow(e, authorName, componentById)).ToList();
        var myRows = myEvents.Select(e => ProjectRow(e, authorName, componentById)).ToList();

        // ADR 0109 (D7) — the pager's prev/next links carry the *current* filter
        // values so they don't drop the selection. The community filter is a
        // FilterParams pair (ADR 0090 D7); when the Past lane is active the
        // past=true selector is carried too, so prev/next stay on the Past view.
        var pagerFilters = new Dictionary<string, string>();
        if (componentId is not null)
            pagerFilters["componentId"] = componentId;
        if (past)
            pagerFilters["past"] = "true";

        var vm = new EventIndexViewModel(
            Events: rows,
            Components: allComponents
                .Select(c => (c.Id, Name: string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CurrentComponentId: componentId,
            CurrentPage: page,
            MyEvents: myRows,
            // M7 (ADR 0090 D5) — the pager (F2 one-page no-render pin): null on a
            // single page so the _Pager partial renders nothing. The community
            // filter + the Past selector are carried across prev/next (D7).
            Pager: (hasMore || page > 1)
                ? PagedViewModel.ForRoute("/events", page, 30, hasMore,
                    pagerFilters.Count > 0 ? pagerFilters : null)
                : null,
            Past: past);

        return View(vm);
    }

    /// <summary>
    /// <c>GET /events/calendar</c> — the EV-CAL calendar (ADR 0063) with the
    /// EV-DWM Day/Week/Month views (ADR 0064): a display-only overview of the
    /// caller's visible events over a **view-appropriate window**. The
    /// <paramref name="view"/> query is a **display selector, never an
    /// access input** (C-DWM·3): <c>day</c> (the anchor day), <c>week</c>
    /// (the anchor's **Monday-start** week, C-DWM·5), or <c>month</c> (the
    /// anchor's calendar month) — missing / invalid / out-of-set values fall
    /// back to <c>week</c> (the default view, ADR 0081 amending C-DWM·8;
    /// a display fallback, not an error). The <paramref name="from"/>
    /// query is the anchor **date in the viewer's effective zone** (C-EV·5;
    /// default: the zone's today) — each window bound is that date's
    /// zone-local midnight as a UTC instant; the *span* (1d / 7d /
    /// days-in-month) is this controller's policy, the seam's
    /// <c>Take(WindowCap)</c> is only a backstop. The window is **window-
    /// agnostic on the seam** (C-DWM·1 / D1) — <see cref="IEventService
    /// .ListInRangeAsync"/> is called unchanged. The visibility split is the
    /// seam's — the single <c>CanSeeAsync(Read)</c> gate (C-EV·1 non-leak
    /// pin); the controller is thin (ADR 0006-D). The
    /// <paramref name="componentId"/> query is a filter, never a gate
    /// (C-M3·2 / C-EV·3). <see cref="EventCalendarViewModel.PrevAnchor"/> /
    /// <see cref="EventCalendarViewModel.NextAnchor"/> are the anchor shifted
    /// by the **view's unit** (±1 day / ±1 week / ±1 month), <c>yyyy-MM-dd</c>
    /// — pre-rendered plain-GET-link targets (C-DWM·6); <see
    /// cref="EventCalendarViewModel.TimeZoneId"/> is the effective zone id
    /// shipped to the view as a **display** input only (C-EV·5 — never an
    /// authorization input).
    /// </summary>
    [HttpGet("/events/calendar")]
    public async Task<IActionResult> Calendar(string? from, string? componentId, string? view = null)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        var zone = await this.timezone.GetAsync();

        // `view` = the EV-DWM display selector (ADR 0064 §6.2 step 1):
        // exactly {"day","week","month"} (case-insensitive); missing /
        // invalid / out-of-set falls back to "week" (the default view —
        // ADR 0081 amends the original C-DWM·8 "month" default; a display
        // fallback, not an error — C-DWM·3). Never an access input (C-DWM·3).
        var resolvedView = view?.Trim()?.ToLowerInvariant() switch
        {
            "day" => "day",
            "week" => "week",
            "month" => "month",
            _ => "week",
        };

        // Anchor = the `from` query parsed as a date in the viewer's effective
        // zone (default: the zone's today). Invalid/missing values fall back
        // to today — a display anchor, never an access decision.
        var nowInZone = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        var anchorDate = DateTime.TryParse(from, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed)
            ? parsed.Date
            : nowInZone.Date;

        // Per-view window (ADR 0064 §6.2 step 3): each bound = the window's
        // first/last day's zone-local midnight → UTC instant (the anchor's
        // local midnight is a display anchor, not a stored value — a
        // DST-ambiguous midnight is still a well-defined zone-local time).
        // The span is this controller's policy; the seam's Take(WindowCap)
        // is only a backstop.
        static DateTimeOffset LocalMidnightUtc(DateTime date, TimeZoneInfo z) =>
            new DateTimeOffset(
                new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Unspecified),
                z.GetUtcOffset(date)).ToUniversalTime();

        DateTime windowStartLocal, windowEndLocalExclusive;
        List<DateTime> windowDays;
        switch (resolvedView)
        {
            case "day":
            {
                // Day (F1): [anchorLocalStartUtc, anchorLocalStartUtc + 1d);
                // WindowDays = [anchorDate].
                windowStartLocal = anchorDate;
                windowEndLocalExclusive = anchorDate.AddDays(1);
                windowDays = [anchorDate];
                break;
            }
            case "week":
            {
                // Week (F2, C-DWM·5): the anchor's ISO **Monday-start** week —
                // Monday = anchor minus ((int)DayOfWeek + 6) % 7 days;
                // [mondayLocalStartUtc, mondayLocalStartUtc + 7d); WindowDays
                // = the 7 days monday .. monday+6 (Monday-first).
                var monday = anchorDate.AddDays(-(((int)anchorDate.DayOfWeek + 6) % 7));
                windowStartLocal = monday;
                windowEndLocalExclusive = monday.AddDays(7);
                windowDays = Enumerable.Range(0, 7).Select(n => monday.AddDays(n)).ToList();
                break;
            }
            default:
            {
                // Month (F3): the anchor's calendar month —
                // [1stLocalStartUtc, 1stLocalStartUtc + DaysInMonth);
                // WindowDays = the 5–6 full weeks covering the month,
                // starting on the Monday on or before the 1st (D4).
                var monthStart = new DateTime(anchorDate.Year, anchorDate.Month, 1);
                var daysInMonth = DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month);
                var gridMonday = monthStart.AddDays(-(((int)monthStart.DayOfWeek + 6) % 7));
                var monthEndInclusive = monthStart.AddDays(daysInMonth - 1);
                var gridEndExclusive = monthEndInclusive.AddDays(1 + ((6 - (int)monthEndInclusive.DayOfWeek) % 7));
                windowStartLocal = monthStart;
                windowEndLocalExclusive = monthStart.AddDays(daysInMonth);
                windowDays = new List<DateTime>();
                for (var d = gridMonday; d < gridEndExclusive; d = d.AddDays(1))
                    windowDays.Add(d);
                break;
            }
        }

        var windowStartUtc = LocalMidnightUtc(windowStartLocal, zone);
        var windowEndUtc = LocalMidnightUtc(windowEndLocalExclusive, zone);

        IReadOnlyList<Event> events;
        try
        {
            events = await this.events.ListInRangeAsync(windowStartUtc, windowEndUtc, componentId, actorId, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // ADR 0049 — the calendar is a list surface, so each row shows the
        // event's title in the viewer's current language when a translation
        // exists, else the authored-in title (the ADR 0022 floor) — exactly
        // the feed's <see cref="ApplyEventTranslationAsync"/> idiom (ADR 0051).
        // A read, not a decision: the <c>CanSeeAsync</c> gate already ran in
        // ListInRangeAsync. The body is not on the chip, so only the title is
        // surfaced here; one effective-language read per request, a null
        // <c>translationProvider</c> (test construction) is a no-op (the
        // authored-in title stays). The resolved code is also reused below to
        // render the nav label (day/month names) in the same language.
        string? effectiveLangCode = null;
        if (translationProvider is not null)
        {
            effectiveLangCode = await EffectiveLanguageCode.ResolveAsync(HttpContext?.Request, localization, translationProvider);
            foreach (var e in events)
                await ApplyEventTranslationAsync(e, effectiveLangCode);
        }

        // Author display names (a *read* lookup, never an access decision —
        // the audience gate already ran in ListInRangeAsync). Missing profile
        // → the raw subject id (the feed action's idiom).
        var authorIds = events.Select(e => e.AuthorId).Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();
        var authorName = new Dictionary<string, string>();
        foreach (var authorId in authorIds)
        {
            var profile = await userInfo.GetProfileAsync(authorId);
            authorName[authorId] =
                profile?.DisplayName is not null && profile.DisplayName.Length > 0
                    ? profile.DisplayName : authorId;
        }

        // Component display names (a *read* lookup, never a gate — C-M3·2).
        var componentIds = events.Select(e => e.ComponentId).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToList();
        var allComponents = await userInfo.GetComponentsAsync(enabledOnly: true);
        var componentById = allComponents
            .Where(c => componentIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name);

        // The U03 additive fields (StartUtc / EndUtc) are set **explicitly
        // here** — the calendar path is the only call site that sets them
        // (design §5.2); the feed action's call site keeps them defaulted.
        var rows = events
            .Select(e => new EventRow(
                Id: e.Id,
                Title: e.Title,
                Body: e.Body,
                Start: e.Start,
                End: e.End,
                Location: e.Location,
                AuthorId: e.AuthorId,
                AuthorDisplayName: authorName.TryGetValue(e.AuthorId, out var an) ? an : e.AuthorId,
                ComponentId: e.ComponentId,
                ComponentDisplayName: e.ComponentId is not null && componentById.TryGetValue(e.ComponentId, out var cn) ? cn : null,
                IsDraft: e.IsDraft,
                IsDeleted: e.IsDeleted,
                StartUtc: e.Start,
                EndUtc: e.End,
                Color: e.Color))
            .ToList();

        // Nav anchors (C-DWM·6 — plain pre-rendered GET links): the anchor
        // shifted by the **view's unit** (day: ±1 day; week: ±7 days —
        // preserving the Monday-start alignment; month: ±1 month),
        // yyyy-MM-dd. The view + component filter ride along in the view's
        // NavHref (C-DWM·6 / C-EV·3).
        var prevDate = resolvedView switch
        {
            "day" => anchorDate.AddDays(-1),
            "week" => anchorDate.AddDays(-7),
            _ => anchorDate.AddMonths(-1),
        };
        var nextDate = resolvedView switch
        {
            "day" => anchorDate.AddDays(1),
            "week" => anchorDate.AddDays(7),
            _ => anchorDate.AddMonths(1),
        };
        var fromAnchor = anchorDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var prevAnchor = prevDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var nextAnchor = nextDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // Label — the view-appropriate display string in the viewer's language
        // (ADR 0064 §6.2 step 6; a computed display string, **not** a registry
        // key — C-DWM·9): Day → full date; Week → "Mon d – Mon d" range;
        // Month → month name + year (the EV-CAL shape). Rendered in the
        // request's effective language (ADR 0049) so day/month names match the
        // language the resident is reading the platform in; a null
        // <c>effectiveLangCode</c> (no translation provider — test
        // construction) falls to the ambient current culture (the
        // <c>Calendar_Label_IsViewAppropriate</c> pin, invariant/en-GB).
        CultureInfo labelCulture = effectiveLangCode is not null
            ? CultureInfo.GetCultureInfo(effectiveLangCode)
            : CultureInfo.CurrentCulture;
        var fmt = labelCulture.DateTimeFormat;
        string label;
        if (resolvedView == "day")
        {
            label = $"{fmt.GetDayName(anchorDate.DayOfWeek)} {anchorDate.Day} {fmt.GetMonthName(anchorDate.Month)} {anchorDate.Year}";
        }
        else if (resolvedView == "week")
        {
            var monday = windowDays[0];
            var sunday = windowDays[^1];
            label = $"{fmt.GetAbbreviatedDayName(monday.DayOfWeek)} {monday:d} – {fmt.GetAbbreviatedDayName(sunday.DayOfWeek)} {sunday:d}";
        }
        else
        {
            label = $"{fmt.GetMonthName(anchorDate.Month)} {anchorDate.Year}";
        }

        var vm = new EventCalendarViewModel(
            Events: rows,
            FromAnchor: fromAnchor,
            PrevAnchor: prevAnchor,
            NextAnchor: nextAnchor,
            Label: label,
            CurrentComponentId: componentId,
            Components: allComponents
                .Select(c => (c.Id, Name: string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            // C-EV·5 — the effective zone id: a DISPLAY input for the view's
            // TS day-distribution (Intl via zone id) only; never an
            // authorization input.
            TimeZoneId: zone.Id,
            // EV-DWM (ADR 0064 §6.1) — the resolved view echoed back to the
            // view (the toggle's active button) + the ordered grid
            // day-columns (the view's columns, C-DWM·3 / D4).
            View: resolvedView,
            WindowDays: windowDays);

        return View(vm);
    }

    /// <summary>
    /// <c>GET /events/{id}</c> — the detail (one event's full body). A single
    /// <see cref="IEventService.GetAsync"/> <c>CanAsync(Read)</c> decision (the
    /// 404-vs-403 split, C3); a draft is author-only (ADR 0037). The author +
    /// component display names are *read* lookups. The <see
    /// cref="EventDetailViewModel.CanEdit"/> / <see
    /// cref="EventDetailViewModel.CanDelete"/> / <see
    /// cref="EventDetailViewModel.CanPublish"/> affordances are the §3.4
    /// standing matrix (author ∪ GlobalAdmin for edit/delete; author-only for
    /// publish) evaluated via the frozen <see cref="EventService"/> helpers with
    /// the principal's *real* role set — a shape convenience for the buttons;
    /// the real gate is the service at POST.
    /// </summary>
    [HttpGet("/events/{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        var roles = RoleSet(User);

        Event ev;
        try
        {
            ev = await this.events.GetAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        var isAuthor = !string.IsNullOrEmpty(actorId) && string.Equals(ev.AuthorId, actorId, StringComparison.Ordinal);

        // §3.4 standing affordances (the buttons' rule, in step with the service).
        var canEdit = false;
        var canDelete = false;
        try { EventService.CheckEditStanding(actorId, roles, ev); canEdit = true; }
        catch (KeyNotFoundException) { canEdit = false; }
        catch (UnauthorizedAccessException) { canEdit = false; }
        // The §3.4 standing matrix's edit + delete rows are identical (author ∪
        // GlobalAdmin) — the service's DeleteAsync applies the same standing, so
        // the delete button's affordance is the edit one.
        canDelete = canEdit;
        var canPublish = isAuthor && ev.IsDraft;

        // The author's display name (a *read* lookup, never an access decision).
        var profile = await userInfo.GetProfileAsync(ev.AuthorId);
        var authorName = profile?.DisplayName is not null && profile.DisplayName.Length > 0
            ? profile.DisplayName : ev.AuthorId;

        // The component's display name (a *read* lookup, never a gate — C-M3·2).
        string? componentName = null;
        if (!string.IsNullOrEmpty(ev.ComponentId))
        {
            var allComponents = await userInfo.GetComponentsAsync(enabledOnly: true);
            var match = allComponents.FirstOrDefault(c => c.Id == ev.ComponentId);
            componentName = match is not null && !string.IsNullOrWhiteSpace(match.Name) ? match.Name : null;
        }

        // The RSVP surface: the owner-only list + the viewer's own RSVP.
        var myRsvp = default(EventRsvp);
        try
        {
            myRsvp = await this.events.GetMyRsvpAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException) { myRsvp = null; }
        catch (UnauthorizedAccessException) { myRsvp = null; }

        var rsvpRows = default(IReadOnlyList<EventRsvp>);
        if (isAuthor)
        {
            try
            {
                rsvpRows = await this.events.GetRsvpsAsync(id, HttpContext.RequestAborted);
            }
            catch (KeyNotFoundException) { rsvpRows = []; }
        }
        rsvpRows ??= [];

        // The RSVP list's display names — one read lookup per RSVPing resident
        // (null-safe: falls back to the raw subject id, the same shape as the
        // author name above — a display lookup, never an access decision).
        var rsvps = new List<EventRsvpEntry>(rsvpRows.Count);
        foreach (var rsvp in rsvpRows)
        {
            var p = await userInfo.GetProfileAsync(rsvp.UserId);
            var name = p?.DisplayName is not null && p.DisplayName.Length > 0
                ? p.DisplayName : rsvp.UserId;
            rsvps.Add(new EventRsvpEntry(rsvp, name));
        }

        // ADR 0059 — the event's user-added translations (a "a read, not a
        // decision" surface; the parent event's single Read decision already ran
        // in GetAsync above — C-M3·1) and the enabled-catalog language set the
        // chips / "add a translation" candidate list render from (the ADR 0027
        // chip-swap + ADR 0022 add-form shape). CanTranslate is the ADR 0059
        // display pin (author / Translator / GlobalAdmin) — the real gate is the
        // server-side re-check in the Add/Update/Remove lanes at POST.
        var eventTranslations = await this.events.GetEventTranslationsAsync(id);
        var enabledLanguages = await SeedLanguagePickerAsync();
        var translationCodes = eventTranslations.Select(t => t.LanguageCode).ToHashSet();
        var languages = enabledLanguages
            .Select(l => new LanguageOption(l.Code, l.NativeName, translationCodes.Contains(l.Code)))
            .ToList();
        var canTranslate = EventService.CanAddTranslation(ev.AuthorId, actorId, roles);

        // ADR 0115 D2 (M14 interlock) — the **linked to-dos** section (the
        // reverse read seam's page, mapped to <see cref="LinkedTodoRow"/>
        // rows): the seam is the gate (its <c>CanSeeAsync(Read)</c> pass
        // already ran on every row — C-M14·1 / C-M14·4, no new surface). Page
        // 0 — the detail surface, not a paged list; the <c>HasMore</c>
        // signal is ignored (the section is display-only, the
        // <c>BlockerChip</c> / <c>ProjectLink</c> "one section, not a feed"
        // precedent). An empty <c>Items</c> ⇒ <c>LinkedTodos</c> stays
        // <c>null</c> and the view renders nothing (no empty-state section
        // for an absent linkage — C-M14·2).
        IReadOnlyList<LinkedTodoRow>? linkedTodos = null;
        if (projects is not null)
        {
            var linkedPage = await projects.ListTodosForEventAsync(ev.Id, actorId, 0, HttpContext.RequestAborted);
            if (linkedPage.Items.Count > 0)
            {
                linkedTodos = linkedPage.Items
                    .Select(t => new LinkedTodoRow(
                        TodoId: t.Id,
                        Title: t.Title,
                        Status: t.Status,
                        DueAt: t.DueAt,
                        LinkPath: "/projects/todos/" + t.Id))
                    .ToList();
            }
        }

        // M18 (ADR 0119, D7 / F2 / F3 / C-M18·4) — the detail-page series
        // shape (U06). The head carries the rule (D3); a non-head sibling
        // carries <c>RecurrenceHeadId</c> = head.Id. The series chip (F2) is
        // shown on **every** row in a series (head + non-head alike); the chip
        // reads the head's rule (a single <c>GetAsync(head.Id)</c> when the
        // row is a non-head). The skip / restore buttons (F3) are shown **only**
        // on a non-head row the actor may edit (C-M18·4 — the existing
        // <c>canEdit</c> standing = author ∪ GlobalAdmin, re-pinned server-side
        // by the U03 seam at POST); the head shows the chip but **neither**
        // button (F3 — the head is edited via the existing edit lane, D4).
        bool isHead = ev.RecurrenceRule is not null;
        bool isNonHead = ev.RecurrenceHeadId is not null;
        bool isPartOfSeries = isHead || isNonHead;
        EventRecurrenceRule? rule = ev.RecurrenceRule;
        if (isNonHead && rule is null)
        {
            // F2 — the chip reads the head's rule (a single GetAsync(head.Id);
            // C-M18·1: the read seams are concrete-only, this is the one
            // detail-page read of the head's rule for the chip).
            try
            {
                var head = await this.events.GetAsync(ev.RecurrenceHeadId!, actorId, HttpContext.RequestAborted);
                rule = head.RecurrenceRule;
            }
            catch (KeyNotFoundException) { rule = null; }
            catch (UnauthorizedAccessException) { rule = null; }
        }

        var vm = new EventDetailViewModel(
            Event: ev,
            AuthorDisplayName: authorName,
            ComponentDisplayName: componentName,
            CanEdit: canEdit,
            CanDelete: canDelete,
            CanPublish: canPublish,
            MyRsvp: myRsvp,
            Rsvps: rsvps,
            EventTranslations: eventTranslations,
            Languages: languages,
            CanTranslate: canTranslate,
            OriginalLanguageCode: ev.LanguageCode,
            LinkedTodos: linkedTodos,
            IsPartOfSeries: isPartOfSeries,
            IsHead: isHead,
            IsNonHead: isNonHead,
            CanSkipOrUndelete: canEdit,
            Rule: rule);

        return View(vm);
    }

    // ── M12 (ADR 0112) — the iCal lane (lane 1: the per-event file) ──────────

    /// <summary>
    /// <c>GET /events/{id}.ics</c> — the M12 per-event calendar file (the D3
    /// lane-1 contract, <c>docs/design/m12-ical-design.md</c> §routes, locked):
    /// the frozen <see cref="IEventService.GetAsync"/> (a single
    /// <c>CanAsync(Read)</c> decision) renders one <c>VEVENT</c> through the
    /// pure <see cref="IcsWriter"/> over the already-authorized row. The
    /// decision never leaks into the file (C-M12·2) — the ICS text is display
    /// fields only (C-M12·4 closed subset).
    /// <para>
    /// **404-not-403 (C3 / C-M12·6, the non-leaky pin):** <em>both</em>
    /// <see cref="KeyNotFoundException"/> (absent) *and*
    /// <see cref="UnauthorizedAccessException"/> (denied) map to
    /// <see cref="NotFoundResult"/> — an event a caller cannot see neither
    /// downloads nor 403s into existence. (Unlike the in-app <see
    /// cref="Detail"/> lane, which 403s a denial, the file lane must not even
    /// distinguish the two.)
    /// </para>
    /// <para>
    /// **Anonymous ⇒ the standard sign-in challenge** (the class-level
    /// <c>[Authorize]</c> default — F3; there is no anonymous iCal surface).
    /// </para>
    /// <para>
    /// **Serve shape (locked, ADR 0034 / 0108 idiom —
    /// <c>AttachmentController.ServeFile</c>):**
    /// <c>Content-Type: text/calendar; charset=utf-8</c> (the
    /// <see cref="FileResult"/> second arg) +
    /// <c>Content-Disposition: attachment;
    /// filename="kumunita-event-{id}.ics"</c> +
    /// <c>Cache-Control: no-store</c> (per-caller, re-authorized content —
    /// never cached by a proxy or the PWA service worker) +
    /// <c>X-Content-Type-Options: nosniff</c>, the three headers set on
    /// <see cref="HttpResponse.Headers"/> before the return.
    /// </para>
    /// </summary>
    [HttpGet("/events/{id}.ics")]
    public async Task<IActionResult> EventIcs(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        Event ev;
        try
        {
            ev = await this.events.GetAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(); // absent — the non-leaky split (C3)
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound(); // denied — the file lane does not 403 into existence (C-M12·6)
        }

        // §field-map — resolve CATEGORIES for the caller before the pure
        // emitter (a read, not a decision — C-TG·8; a dangling TagId is
        // dropped, the C-TG·1 "renders as nothing" pin; stable Ordinal order).
        // A null <see cref="ITagService"/> (a test-construction site) ⇒ no
        // CATEGORIES on the VEVENT (the PostsController no-op idiom).
        Dictionary<string, IReadOnlyList<string>>? categories = null;
        if (tags is not null && ev.TagIds.Count > 0)
        {
            var readable = await tags.ListForActorAsync(actorId);
            var readableById = readable.ToDictionary(t => t.Tag.Id, StringComparer.Ordinal);
            var names = ev.TagIds
                .Where(readableById.ContainsKey)
                .Select(tid => readableById[tid].DisplayedName)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            if (names.Count > 0)
                categories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    [ev.Id] = names,
                };
        }

        var icsText = IcsWriter.Build([ev], DateTimeOffset.UtcNow, categories);

        // The serve shape (locked, §routes) — the ADR 0034 / 0108 idiom:
        // Content-Disposition + nosniff + no-store on the response, the
        // Content-Type as the File(...) second arg.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"kumunita-event-" + id + ".ics\"";
        Response.Headers["Cache-Control"] = "no-store";
        return File(System.Text.Encoding.UTF8.GetBytes(icsText), "text/calendar; charset=utf-8");
    }

    /// <summary>
    /// <c>GET /events.ics</c> — the M12 subscription feed (the D3
    /// lane-2 contract, <c>docs/design/m12-ical-design.md</c> §routes,
    /// locked): the frozen <see cref="IEventService.ListUpcomingAsync"/>
    /// (<c>componentId = null</c>, page 0 — C-M12·1: **exactly** the feed's
    /// visible upcoming set, the candidate filter + the
    /// <c>CanSeeAsync(Read)</c> gate all inside it) renders through the
    /// pure <see cref="IcsWriter"/> over the already-authorized rows.
    /// <para>
    /// **Always <c>200</c>** (the feed is a valid <c>VCALENDAR</c> — an
    /// empty visible set is a valid empty calendar, C-M12·4; the seam
    /// filters rather than throws for an individual denial, so there is
    /// no 404/403 on this lane). **Anonymous ⇒ the standard sign-in
    /// challenge** (the class-level <c>[Authorize]</c> default — F3).
    /// </para>
    /// <para>
    /// **Serve shape (locked, §routes — the same ADR 0034 / 0108 idiom as
    /// the lane-1 sibling):** <c>Content-Type: text/calendar;
    /// charset=utf-8</c> + <c>Content-Disposition: attachment;
    /// filename="kumunita-events.ics"</c> + <c>Cache-Control: no-store</c>
    /// + <c>X-Content-Type-Options: nosniff</c>.
    /// </para>
    /// </summary>
    [HttpGet("/events.ics")]
    public async Task<IActionResult> CalendarFeed()
    {
        var actorId = SubjectId(User) ?? string.Empty;

        // C-M12·1 — the feed seam, called exactly as the in-app feed calls it:
        // componentId null (no filter), page 0 (the service clamps to 1).
        var page = await this.events.ListUpcomingAsync(null, actorId, 0, HttpContext.RequestAborted);
        var items = page.Items;

        // §field-map — resolve CATEGORIES for the caller before the pure
        // emitter (a read, not a decision — C-TG·8; a dangling TagId is
        // dropped, the C-TG·1 "renders as nothing" pin; stable Ordinal
        // order). A null <see cref="ITagService"/> (a test-construction
        // site) ⇒ no CATEGORIES on any VEVENT (the PostsController no-op
        // idiom, the lane-1 sibling's shape).
        Dictionary<string, IReadOnlyList<string>>? categories = null;
        if (tags is not null && items.Any(e => e.TagIds.Count > 0))
        {
            var readable = await tags.ListForActorAsync(actorId);
            var readableById = readable.ToDictionary(t => t.Tag.Id, StringComparer.Ordinal);
            categories = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var e in items)
            {
                var names = e.TagIds
                    .Where(readableById.ContainsKey)
                    .Select(tid => readableById[tid].DisplayedName)
                    .OrderBy(n => n, StringComparer.Ordinal)
                    .ToList();
                if (names.Count > 0)
                    categories[e.Id] = names;
            }
        }

        var icsText = IcsWriter.Build(items, DateTimeOffset.UtcNow, categories);

        // The serve shape (locked, §routes) — the ADR 0034 / 0108 idiom,
        // the lane-2 filename.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Disposition"] =
            "attachment; filename=\"kumunita-events.ics\"";
        Response.Headers["Cache-Control"] = "no-store";
        return File(System.Text.Encoding.UTF8.GetBytes(icsText), "text/calendar; charset=utf-8");
    }

    // ── Write lanes ────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /events/new</c> — the composer (any signed-in resident, §3.4
    /// <c>Owner</c>). Seeds the audience editor (community-visible by default,
    /// ADR 0036), the authored-in language picker (ADR 0018), the component
    /// feed-organizer picker (C-M3·2), and the grant-picker option lists
    /// (the M2 single source).
    /// </summary>
    [HttpGet("/events/new")]
    public async Task<IActionResult> CreateGet()
    {
        // Default the composer's time range to the actor's *current* date and
        // time in their effective time zone (resident override → platform
        // default → UTC floor, via EffectiveTimezoneResolver) rather than the
        // framework's <see cref="DateTimeOffset"/> default (0001-01-01 00:00).
        // Start is now rounded to the whole minute; End defaults to one hour
        // after Start (the author adjusts both on the form).
        var zone = await timezone.GetAsync();
        // Current (DST-aware) offset for the zone at this instant, so the
        // seeded wall-clock time is correct across DST transitions.
        var nowInZone = DateTimeOffset.UtcNow.ToOffset(zone.GetUtcOffset(DateTimeOffset.UtcNow));
        var start = new DateTimeOffset(
            nowInZone.Year, nowInZone.Month, nowInZone.Day,
            nowInZone.Hour, nowInZone.Minute, 0, nowInZone.Offset);

        var model = new EventEditorModel
        {
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
                CommunityVisible = true,
            },
            // The model is a wall-clock DateTime in the actor's effective zone
            // (ADR 0019) — `start` is already the author's local wall-clock
            // instant, so `.DateTime` is the correct wall-clock seeding.
            Start = start.DateTime,
            End = start.DateTime.AddHours(1),
            ReminderEnabled = true,
            SaveAsDraft = true, // ADR 0037 — a new event is a draft until published.
            Languages = await SeedLanguagePickerAsync(),
            // ADR 0018 / ADR 0049 — pre-select the actor's current effective
            // language so the picker highlights the right option and a
            // no-change submit is a concrete BCP-47 code (never an empty row).
            LanguageCode = await ResolveComposeDefaultLanguageAsync(),
            Components = await SeedComponentPickerAsync(),
        };
        await SeedGrantPickerOptionsAsync();
        return View("Create", model);
    }

    /// <summary>
    /// <c>POST /events/new</c> — the create write lane (any signed-in resident
    /// becomes the author). Validates the shape (<see
    /// cref="EventEditorModel.IsValid"/> — a guard is a shape, not a
    /// controller-assert), writes through
    /// <see cref="IEventService.CreateAsync"/> (the service opens its own
    /// write session + audit row, C3), redirects to the new event's
    /// <c>/events/{id}</c> (the M2 "redirect after write" precedent). The
    /// audience's <see cref="AudienceEditorModel.BuildAudience()"/> is the
    /// single deserialization site (the M2 single-source pin, ADR 0001-B); the
    /// <c>ImageIds</c> / <c>AttachmentIds</c> are the server-side body parse
    /// (RC R·3 / ATT U5 — the client never sends them); the <c>TagIds</c> are
    /// the server-side <see cref="TagSlugs.Parse"/> (TG, ADR 0044).
    /// </summary>
    [HttpPost("/events/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePost([FromForm] EventEditorModel model)
    {
        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
        {
            ModelState.AddModelError(string.Empty, "You must sign in to create an event.");
            return View("Create", model);
        }

        // ADR 0081 — the quick-create marker (the calendar's quick-create
        // modal's hidden QuickCreate=true field): a **shape signal only**,
        // read before the IsValid gate. When set and no body was typed, the
        // title seeds the body — a title-only quick-create is a well-formed
        // shape, and the full composer's "a body is required" rule (and its
        // re-render path) is untouched for non-quick-create posts.
        if (model.QuickCreate && string.IsNullOrWhiteSpace(model.Body)
            && !string.IsNullOrWhiteSpace(model.Title))
        {
            model.Body = model.Title;
        }

        // Re-seed the pickers so a failed-shape re-render below still shows the
        // language + component + grant options.
        model.Components = await SeedComponentPickerAsync();
        model.Languages = await SeedLanguagePickerAsync();
        await SeedGrantPickerOptionsAsync();

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (string.IsNullOrWhiteSpace(model.Body))
                ModelState.AddModelError(nameof(model.Body), "A body is required.");
            if (model.End < model.Start)
                ModelState.AddModelError(nameof(model.End), "The end time must be after the start time.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            // M18 (ADR 0119, D2 / D7) — the recurrence picker's shape errors.
            if (!string.IsNullOrWhiteSpace(model.Recurrence)
                && !model.Recurrence.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                if (model.RecurrenceInterval is < 1)
                    ModelState.AddModelError(nameof(model.RecurrenceInterval), "The interval must be at least 1.");
                if (model.EndsAfterCount.HasValue == model.EndsOnDate.HasValue)
                    ModelState.AddModelError(nameof(model.EndsAfterCount),
                        "Pick exactly one end: \"Ends after\" a number of occurrences, or \"Ends on\" a date.");
            }
            return View("Create", model);
        }

        // ADR 0019 — the form's Start/End are wall-clock (no offset) in the
        // actor's effective zone (resident override → platform default → UTC
        // floor). Convert to a UTC instant using the zone's DST-aware offset
        // at that wall-clock moment (the `ToOffset` idiom the `CreateGet`
        // seeding uses in reverse).
        var authorZone = await timezone.GetAsync();
        var startUtc = new DateTimeOffset(model.Start, authorZone.GetUtcOffset(model.Start)).UtcDateTime;
        var endUtc = new DateTimeOffset(model.End, authorZone.GetUtcOffset(model.End)).UtcDateTime;

        var request = new CreateEventRequest
        {
            // `!` — Title/Body are guaranteed non-null: the `if (!model.IsValid)` gate
            // above returns unless both are non-whitespace (the [Required] pin), so
            // this assignment can never actually store a null (CS8601).
            Title = model.Title!,
            Body = model.Body!,
            ComponentId = model.ComponentId,
            Start = new DateTimeOffset(startUtc, TimeSpan.Zero),
            End = new DateTimeOffset(endUtc, TimeSpan.Zero),
            Location = model.Location,
            Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim(), // display metadata (the Location shape).
            Capacity = model.Capacity,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            ReminderEnabled = model.ReminderEnabled,
            IsDraft = model.SaveAsDraft, // ADR 0037 — the composer's save-as-draft toggle.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? string.Empty : model.LanguageCode,
            TagIds = TagSlugs.Parse(model.TagIds), // TG (ADR 0044) — server-side parse + normalize.
            ImageIds = ContentImageIds.ExtractContentImageIds(model.Body), // RC R·3 — server-side body parse.
            AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body), // ATT U5 — server-side body parse.
            // M18 (ADR 0119, D2 / D7) — the composer's recurrence picker, mapped
            // onto the U02 create lane's Recurrence field (null for "none" — the
            // zero-change branch).
            Recurrence = BuildRecurrenceRule(model, authorZone),
        };

        Event ev;
        try
        {
            ev = await this.events.CreateAsync(actorId, request, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "You do not have permission to create an event.");
            return View("Create", model);
        }
        catch (ArgumentException ex) when (ex.ParamName == "input")
        {
            // A bad tag slug (TagService.DeriveSlug, C-TG·4) — mapped to a form
            // error on the TagIds field (the M3 "a form is a shape" precedent).
            ModelState.AddModelError(nameof(model.TagIds),
                "A tag name is invalid (use letters, digits, hyphens, underscores).");
            return View("Create", model);
        }

        TempData["info"] = model.SaveAsDraft
            ? "Event saved as a draft. It is visible to you only until you publish it."
            : "Event created.";
        return Redirect($"/events/{ev.Id}");
    }

    /// <summary>
    /// <c>GET /events/{id}/edit</c> — the edit lane's shape. The form is seeded
    /// from the stored event's Title/Body/Audience (the
    /// <see cref="AudienceEditorModel.FromAudience"/> round-trip — the inverse
    /// of <c>BuildAudience</c>). The §3.4 standing gate (author or GlobalAdmin,
    /// ADR 0014/0016/0017) is checked here as a Web-layer shape gate via the
    /// frozen <see cref="EventService.CheckEditStanding"/> helper (the real gate
    /// is the service at POST — defense-in-depth). A missing id is 404; a
    /// denied actor is 403 (the M3 "403 on denied, not a blank page" pin).
    /// </summary>
    [HttpGet("/events/{id}/edit")]
    public async Task<IActionResult> EditGet(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        var roles = RoleSet(User);

        Event ev;
        try
        {
            ev = await this.events.GetAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            EventService.CheckEditStanding(actorId, roles, ev);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // ADR 0019 — the model is a wall-clock DateTime in the *editor's*
        // effective zone (resident override → platform default → UTC floor).
        // The stored `ev.Start` / `ev.End` are UTC instants; `ToOffset(zone)`
        // re-expresses them in that zone (DST-aware) so the form shows the
        // author the local wall-clock they (or a prior editor) chose.
        var editorZone = await timezone.GetAsync();
        var model = new EventEditorModel
        {
            Id = ev.Id,
            Title = ev.Title,
            Body = ev.Body,
            ComponentId = ev.ComponentId,
            Start = TimeZoneInfo.ConvertTime(ev.Start, editorZone).DateTime,
            End = TimeZoneInfo.ConvertTime(ev.End, editorZone).DateTime,
            Location = ev.Location,
            Color = ev.Color,
            Capacity = ev.Capacity,
            Audience = AudienceEditorModel.FromAudience(ev.Audience),
            ReminderEnabled = ev.ReminderEnabled,
            LanguageCode = ev.LanguageCode,
            TagIds = ev.TagIds.Count > 0 ? JsonSerializer.Serialize(ev.TagIds) : "[]",
            Languages = await SeedLanguagePickerAsync(),
            Components = await SeedComponentPickerAsync(),
            // M18 (ADR 0119, D3) — a non-head occurrence's rule is null (the
            // head carries the rule), so the composer hides the picker on a
            // non-head row (the @if guard in Edit.cshtml); false on the create
            // lane and on a head row.
            IsNonHeadOccurrence = ev.RecurrenceHeadId is not null,
        };

        // M18 (ADR 0119, D2 / D3) — round-trip the head's existing rule into
        // the composer's picker (a non-head row has a null rule — the picker
        // is hidden by the IsNonHeadOccurrence guard above, so this is a
        // no-op for non-head rows).
        if (ev.RecurrenceRule is { } headRule)
        {
            model.Recurrence = headRule.Recurrence.ToString().ToLowerInvariant();
            model.RecurrenceInterval = headRule.Interval;
            if (headRule.Count is { } headCount)
                model.EndsAfterCount = headCount;
            if (headRule.Ends is { } headEnds)
                model.EndsOnDate = TimeZoneInfo.ConvertTime(headEnds, editorZone).DateTime;
        }

        await SeedGrantPickerOptionsAsync();
        return View("Edit", model);
    }

    /// <summary>
    /// <c>POST /events/{id}/edit</c> — the edit write lane (author or
    /// GlobalAdmin, §3.4). The <see cref="IEventService.UpdateAsync"/> service
    /// re-pins the standing server-side at POST against the stored row
    /// (defense-in-depth); the <c>AuthorId</c> / <c>Created</c> are preserved
    /// untouched, <c>Modified</c> stamped on a real change (ADR 0014/0016/0017).
    /// </summary>
    [HttpPost("/events/{id}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPost(string id, [FromForm] EventEditorModel model)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        var roles = RoleSet(User);

        Event ev;
        try
        {
            ev = await this.events.GetAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            EventService.CheckEditStanding(actorId, roles, ev);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        model.Id = id;
        model.Components = await SeedComponentPickerAsync();
        model.Languages = await SeedLanguagePickerAsync();
        await SeedGrantPickerOptionsAsync();

        if (!model.IsValid)
        {
            if (string.IsNullOrWhiteSpace(model.Title))
                ModelState.AddModelError(nameof(model.Title), "A title is required.");
            if (string.IsNullOrWhiteSpace(model.Body))
                ModelState.AddModelError(nameof(model.Body), "A body is required.");
            if (model.End < model.Start)
                ModelState.AddModelError(nameof(model.End), "The end time must be after the start time.");
            if (model.Audience is null || !model.Audience.IsValid)
                ModelState.AddModelError("Audience.Mode", "Audience mode is required (Any or All).");
            // M18 (ADR 0119, D2 / D7) — the recurrence picker's shape errors
            // (a head row only — a non-head row's picker is hidden, D3).
            if (!model.IsNonHeadOccurrence
                && !string.IsNullOrWhiteSpace(model.Recurrence)
                && !model.Recurrence.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                if (model.RecurrenceInterval is < 1)
                    ModelState.AddModelError(nameof(model.RecurrenceInterval), "The interval must be at least 1.");
                if (model.EndsAfterCount.HasValue == model.EndsOnDate.HasValue)
                    ModelState.AddModelError(nameof(model.EndsAfterCount),
                        "Pick exactly one end: \"Ends after\" a number of occurrences, or \"Ends on\" a date.");
            }
            return View("Edit", model);
        }

        // ADR 0019 — the form's Start/End are wall-clock (no offset) in the
        // editor's effective zone. Convert to a UTC instant using the
        // zone's DST-aware offset at that wall-clock moment (the same
        // `ToOffset` idiom the `EditGet` seeding uses in reverse).
        var editorZone = await timezone.GetAsync();
        var startUtc = new DateTimeOffset(model.Start, editorZone.GetUtcOffset(model.Start)).UtcDateTime;
        var endUtc = new DateTimeOffset(model.End, editorZone.GetUtcOffset(model.End)).UtcDateTime;

        var request = new UpdateEventRequest
        {
            // `!` — Title/Body are guaranteed non-null: the `if (!model.IsValid)` gate
            // above returns unless both are non-whitespace (the [Required] pin), so
            // this assignment can never actually store a null (CS8601).
            Title = model.Title!,
            Body = model.Body!,
            ComponentId = model.ComponentId,
            Start = new DateTimeOffset(startUtc, TimeSpan.Zero),
            End = new DateTimeOffset(endUtc, TimeSpan.Zero),
            Location = model.Location,
            Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim(), // display metadata (the Location shape).
            Capacity = model.Capacity,
            Audience = model.Audience.BuildAudience(),
            ReminderEnabled = model.ReminderEnabled,
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? string.Empty : model.LanguageCode,
            TagIds = TagSlugs.Parse(model.TagIds),
            ImageIds = ContentImageIds.ExtractContentImageIds(model.Body),
            AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body),
            // M18 (ADR 0119, D2 / D4) — the composer's recurrence picker, mapped
            // onto the U04 edit lane's Recurrence field (a head edit cascades /
            // re-materializes; null for "none" / a non-head row — the zero-change
            // branch).
            Recurrence = model.IsNonHeadOccurrence ? null : BuildRecurrenceRule(model, editorZone),
        };

        try
        {
            await this.events.UpdateAsync(id, actorId, roles, request, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (ArgumentException ex) when (ex.ParamName == "input")
        {
            ModelState.AddModelError(nameof(model.TagIds),
                "A tag name is invalid (use letters, digits, hyphens, underscores).");
            return View("Edit", model);
        }

        TempData["info"] = "Event updated.";
        return Redirect($"/events/{id}");
    }

    /// <summary>
    /// <c>POST /events/{id}/publish</c> — the ADR 0037 author-only publish
    /// (flips <c>IsDraft = false</c>; a non-author, even a GlobalAdmin, is
    /// denied). The <see cref="IEventService.PublishAsync"/> service is the
    /// sole gate; a missing id is 404, a denied actor is 403.
    /// </summary>
    [HttpPost("/events/{id}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.events.PublishAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        TempData["info"] = "Event published.";
        return Redirect($"/events/{id}");
    }

    /// <summary>
    /// <c>POST /events/{id}/delete</c> — the ADR 0024 author soft-delete
    /// (sets <c>IsDeleted = true</c>; the record is kept, the read lanes filter
    /// it out). The <see cref="IEventService.DeleteAsync"/> service re-pins the
    /// standing (author ∪ GlobalAdmin, §3.4) server-side; a missing id is 404,
    /// a denied actor is 403.
    /// </summary>
    [HttpPost("/events/{id}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        var roles = RoleSet(User);

        Event ev;
        try
        {
            ev = await this.events.GetAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            EventService.CheckEditStanding(actorId, roles, ev);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            await this.events.DeleteAsync(id, actorId, roles, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        TempData["info"] = "Event deleted.";
        return Redirect("/events");
    }

    // ── M18 (ADR 0119, D5 / F3) — the author's skip / restore lane ────────────
    //
    // Two thin Web lanes (ADR 0006-D: routes + shape) that delegate the write
    // + standing decision to the frozen <see cref="IEventService"/> U03 seams
    // (<see cref="IEventService.SkipOccurrenceAsync"/> /
    // <see cref="IEventService.UndeleteOccurrenceAsync"/>). The standing
    // (C-M18·4 — author ∪ GlobalAdmin) + the head-row guard + the no-leak 404
    // split are all enforced **server-side** by the seam (a non-author, a
    // missing id, and a head row all surface as the seam's
    // <see cref="KeyNotFoundException"/> → a non-leaky <c>404</c>, never a
    // <c>403</c>). No new <see cref="AccessAction"/> /
    // <see cref="AccessVia"/> / adapter (C-M18·5). The detail page's
    // <see cref="EventDetailViewModel.CanSkipOrUndelete"/> is only the display
    // affordance (F3); the seam's 404 is the backstop.

    /// <summary>
    /// <c>POST /events/{id}/skip</c> — M18 (ADR 0119, D5 / F3) the author's
    /// "Skip this occurrence" (soft-delete <b>one</b> occurrence row,
    /// C-M18·8). A thin Web lane delegating to
    /// <see cref="IEventService.SkipOccurrenceAsync"/>. A non-author, a
    /// missing id, or a head row is a non-leaky <c>404</c> (the seam's
    /// <see cref="KeyNotFoundException"/>; C-M18·4 / D5 — never a <c>403</c>).
    /// On success, redirects back to the detail page.
    /// </summary>
    [HttpPost("/events/{id}/skip")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SkipOccurrence(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.events.SkipOccurrenceAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        TempData["info"] = "Occurrence skipped.";
        return Redirect($"/events/{id}");
    }

    /// <summary>
    /// <c>POST /events/{id}/undelete</c> — M18 (ADR 0119, D5 / F3) the author's
    /// "Restore this occurrence" (the inverse of
    /// <see cref="SkipOccurrence"/> — set <c>IsDeleted = false</c> on one row
    /// only, C-M18·8). A thin Web lane delegating to
    /// <see cref="IEventService.UndeleteOccurrenceAsync"/>. Same standing
    /// (C-M18·4), same head-row guard, same non-leaky <c>404</c> split as
    /// <see cref="SkipOccurrence"/>. On success, redirects back to the detail
    /// page.
    /// </summary>
    [HttpPost("/events/{id}/undelete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UndeleteOccurrence(string id)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.events.UndeleteOccurrenceAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        TempData["info"] = "Occurrence restored.";
        return Redirect($"/events/{id}");
    }

    /// <summary>
    /// <c>POST /events/{id}/rsvp</c> — the last-write-wins RSVP (§3.2).
    /// <b>No <c>AccessAudit</c> row</b> — a routine resident action, not an
    /// access decision (the <see cref="IEventService.RsvpAsync"/> service
    /// stores no audit row; the same posture as a profile edit). A missing id
    /// is 404, a denied actor is 403.
    /// </summary>
    [HttpPost("/events/{id}/rsvp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rsvp(string id, [FromForm] RsvpStatus status)
    {
        var actorId = SubjectId(User) ?? string.Empty;
        try
        {
            await this.events.RsvpAsync(id, actorId, status, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        return Redirect($"/events/{id}");
    }

    // ── Translations (ADR 0059) — user-added event translations ──────────────
    //
    // Thin Web lanes (ADR 0006-D: routes + shape) that delegate the write +
    // standing decision to the frozen <see cref="IEventService"/> translation
    // seams (the standing — author / Translator / GlobalAdmin — is re-pinned
    // server-side; the detail page's <see cref="EventDetailViewModel
    // .CanTranslate"/> is only the display affordance). A denied standing actor
    // is a 403 (<see cref="UnauthorizedAccessException"/> → <c>ForbidResult</c>);
    // a missing event/row is a 404 (<see cref="KeyNotFoundException"/> →
    // <c>NotFound</c>). The M4 convention: the service opens its **own** write
    // session (C3) — the controller does **not** wrap the call in
    // <see cref="IDocumentStore.LightweightSession"/> (contrast the M3
    // <see cref="PostsController"/> translation lanes).

    /// <summary>
    /// Adds a **user-added translation** of the event into
    /// <paramref name="languageCode"/> (ADR 0059):
    /// <c>POST /events/{id}/translations</c>. A thin Web lane (ADR 0006-D)
    /// delegating to <see cref="IEventService.AddEventTranslationAsync"/>.
    /// <para>
    /// <b>Precondition (C-M3·1):</b> the viewer must be able to see the event
    /// (re-run the parent's single <c>Read</c> decision via
    /// <see cref="IEventService.GetAsync"/> — the exact
    /// <see cref="Rsvp"/> / <see cref="Delete"/> precedent; a
    /// <c>KeyNotFoundException</c> is a 404, a
    /// <c>UnauthorizedAccessException</c> a 403 — the C3 non-leaky posture).
    /// </para>
    /// </summary>
    [HttpPost("/events/{id}/translations")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/events/{id}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/events/{id}");
        }

        // C-M3·1 precondition: the viewer must be able to see the event.
        try
        {
            await this.events.GetAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            await this.events.AddEventTranslationAsync(
                id,
                languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body,
                actorId,
                RoleSet(User),
                HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation added ({name}).";
        return Redirect($"/events/{id}");
    }

    /// <summary>
    /// **Updates** the existing user-added translation of the event in
    /// <paramref name="languageCode"/> (ADR 0059, the ADR 0048 edit-lane
    /// shape): <c>POST /events/{id}/translations/update</c>. Thin Web lane;
    /// delegates to <see cref="IEventService.UpdateEventTranslationAsync"/>.
    /// Precondition + failure shapes mirror <see cref="AddTranslation"/>.
    /// </summary>
    [HttpPost("/events/{id}/translations/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode,
        [FromForm] string? title,
        [FromForm] string? body)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/events/{id}");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            TempData["error"] = "A translation needs some text.";
            return Redirect($"/events/{id}");
        }

        try
        {
            await this.events.GetAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            await this.events.UpdateEventTranslationAsync(
                id,
                languageCode,
                string.IsNullOrWhiteSpace(title) ? null : title,
                body,
                actorId,
                RoleSet(User),
                HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation updated ({name}).";
        return Redirect($"/events/{id}");
    }

    /// <summary>
    /// **Removes** the existing user-added translation of the event in
    /// <paramref name="languageCode"/> (ADR 0059, the ADR 0048 remove-lane
    /// shape): <c>POST /events/{id}/translations/remove</c>. Thin Web lane;
    /// delegates to <see cref="IEventService.RemoveEventTranslationAsync"/>.
    /// Failure shapes mirror <see cref="AddTranslation"/>.
    /// </summary>
    [HttpPost("/events/{id}/translations/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTranslation(
        [FromRoute] string id,
        [FromForm] string? languageCode)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var actorId = SubjectId(User);
        if (string.IsNullOrEmpty(actorId))
            return new ForbidResult();

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            TempData["error"] = "Choose a language for the translation.";
            return Redirect($"/events/{id}");
        }

        try
        {
            await this.events.GetAsync(id, actorId, HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        try
        {
            await this.events.RemoveEventTranslationAsync(id, languageCode, actorId, RoleSet(User), HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var name = await SeedLanguageName(languageCode);
        TempData["info"] = $"Translation removed ({name}).";
        return Redirect($"/events/{id}");
    }
}
