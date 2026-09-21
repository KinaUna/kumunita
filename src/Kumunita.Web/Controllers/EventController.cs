using System.Security.Claims;
using System.Text.Json;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
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

    public EventController(
        IEventService events,
        IUserInfoService userInfo,
        ILocalizationService localization,
        IDocumentStore store)
    {
        this.events = events;
        this.userInfo = userInfo;
        this.localization = localization;
        this.store = store;
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
    /// page uses). The composer leaves the selection empty by default so the
    /// *instance default* is what the service materializes server-side at write
    /// time — the picker is the set of choices, not the choice.
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

    // ── Read lanes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>GET /events</c> — the feed (upcoming events the caller may read). The
    /// service's <see cref="IEventService.ListUpcomingAsync"/> <c>CanSeeAsync
    /// (Read)</c> gate is the sole reader (drafts + deleted rows excluded for
    /// non-authors); <see cref="Event.Start"/> ascending order; paged. The
    /// <paramref name="componentId"/> query is a filter (C-M3·2), never a gate.
    /// Author + component display names are *read* lookups (never access
    /// decisions).
    /// </summary>
    [HttpGet("/events")]
    public async Task<IActionResult> Index(string? componentId, int page = 1)
    {
        var actorId = SubjectId(User) ?? string.Empty;

        IReadOnlyList<Event> events;
        try
        {
            events = await this.events.ListUpcomingAsync(componentId, actorId, page, HttpContext.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return new ForbidResult();
        }

        // Author display names (a *read* lookup, never an access decision — the
        // audience gate already ran in ListUpcomingAsync). Missing profile →
        // the raw subject id.
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
                IsDeleted: e.IsDeleted))
            .ToList();

        var vm = new EventIndexViewModel(
            Events: rows,
            Components: allComponents
                .Select(c => (c.Id, Name: string.IsNullOrWhiteSpace(c.Name) ? c.Id : c.Name))
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CurrentComponentId: componentId,
            CurrentPage: page);

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

        var vm = new EventDetailViewModel(
            Event: ev,
            AuthorDisplayName: authorName,
            ComponentDisplayName: componentName,
            CanEdit: canEdit,
            CanDelete: canDelete,
            CanPublish: canPublish,
            MyRsvp: myRsvp,
            Rsvps: rsvps);

        return View(vm);
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
        var model = new EventEditorModel
        {
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
                CommunityVisible = true,
            },
            ReminderEnabled = true,
            SaveAsDraft = true, // ADR 0037 — a new event is a draft until published.
            Languages = await SeedLanguagePickerAsync(),
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
            return View("Create", model);
        }

        var request = new CreateEventRequest
        {
            Title = model.Title,
            Body = model.Body,
            ComponentId = model.ComponentId,
            Start = model.Start,
            End = model.End,
            Location = model.Location,
            Capacity = model.Capacity,
            Audience = model.Audience.BuildAudience(), // ADR 0001-B — the single deserialization site.
            ReminderEnabled = model.ReminderEnabled,
            IsDraft = model.SaveAsDraft, // ADR 0037 — the composer's save-as-draft toggle.
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? string.Empty : model.LanguageCode,
            TagIds = TagSlugs.Parse(model.TagIds), // TG (ADR 0044) — server-side parse + normalize.
            ImageIds = ContentImageIds.ExtractContentImageIds(model.Body), // RC R·3 — server-side body parse.
            AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body), // ATT U5 — server-side body parse.
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

        var model = new EventEditorModel
        {
            Id = ev.Id,
            Title = ev.Title,
            Body = ev.Body,
            ComponentId = ev.ComponentId,
            Start = ev.Start,
            End = ev.End,
            Location = ev.Location,
            Capacity = ev.Capacity,
            Audience = AudienceEditorModel.FromAudience(ev.Audience),
            ReminderEnabled = ev.ReminderEnabled,
            LanguageCode = ev.LanguageCode,
            TagIds = ev.TagIds.Count > 0 ? JsonSerializer.Serialize(ev.TagIds) : "[]",
            Languages = await SeedLanguagePickerAsync(),
            Components = await SeedComponentPickerAsync(),
        };
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
            return View("Edit", model);
        }

        var request = new UpdateEventRequest
        {
            Title = model.Title,
            Body = model.Body,
            ComponentId = model.ComponentId,
            Start = model.Start,
            End = model.End,
            Location = model.Location,
            Capacity = model.Capacity,
            Audience = model.Audience.BuildAudience(),
            ReminderEnabled = model.ReminderEnabled,
            LanguageCode = string.IsNullOrWhiteSpace(model.LanguageCode) ? string.Empty : model.LanguageCode,
            TagIds = TagSlugs.Parse(model.TagIds),
            ImageIds = ContentImageIds.ExtractContentImageIds(model.Body),
            AttachmentIds = AttachmentIds.ExtractAttachmentIds(model.Body),
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
}
