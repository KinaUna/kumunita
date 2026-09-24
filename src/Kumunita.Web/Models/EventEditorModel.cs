using System;
using System.ComponentModel.DataAnnotations;
using Kumunita.Core.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Kumunita.Web.Models;

/// <summary>
/// The <b>single</b> composer/edit form-bound model (U05) — one
/// <see cref="Kumunita.Core.Events.Event"/> at a time, shared by the
/// <c>GET</c> / <c>POST</c> / <c>/events/new</c> (create) and
/// <c>GET</c> / <c>POST</c> / <c>/events/{id}/edit</c> (edit) lanes.
/// <para>
/// **Field set (ADR 0054 §3.1, the design-doc §3.1 field-by-field
/// provenance table — each reuses an existing mechanism, none invents one):**
/// <list type="bullet">
/// <item><see cref="Title"/> / <see cref="Body"/> / <see
/// cref="LanguageCode"/> — <c>Post</c> / <c>Announcement</c> (ADR 0018):
/// the authored-in tag + a rich body, edited by the one
/// <c>bindRichEditor</c> (ADR 0031) and rendered by the one
/// <see cref="Kumunita.Web.Security.MarkdownRenderer"/>.</item>
/// <item><see cref="ComponentId"/> — a feed organizer (C-M3·2: a filter,
/// never a gate); the <see cref="Audience"/> is the sole access boundary
/// (ADR 0001-B / 0036).</item>
/// <item><see cref="Start"/> / <see cref="End"/> — the event's time
/// (UTC <see cref="DateTimeOffset"/>); <see cref="Start"/> drives feed
/// ordering.</item>
/// <item><see cref="Location"/> / <see cref="Capacity"/> — display
/// metadata; <see cref="Capacity"/> is **not** a gate (no admission
/// queue; the <c>Going</c> RSVP set is the truth).</item>
/// <item><see cref="Color"/> — the author's picked calendar color (the
/// <see cref="Location"/> / <see cref="Capacity"/> shape — display
/// metadata, never a gate; ADR 0066).</item>
/// <item><see cref="Audience"/> — the M2 reusable
/// <see cref="AudienceEditorModel"/> (the single-source pin — the one
/// form-bound audience editor; <see cref="AudienceEditorModel
/// .BuildAudience()"/> is the one deserialization site that writes the
/// <see cref="Kumunita.Core.Events.Event.Audience"/> row verbatim, ADR
/// 0001-B).</item>
/// <item><see cref="ReminderEnabled"/> — the §6.4 job's per-event
/// opt-out (<c>true</c> floor).</item>
/// <item><see cref="SaveAsDraft"/> — the ADR 0037 draft pin (a new event
/// is a draft until its author publishes it).</item>
/// <item><see cref="TagIds"/> — a <see cref="string"/> form field
/// (posted by <c>client/lib/tag-suggest.ts</c> as a JSON array of label
/// strings, or a CSV fallback), parsed server-side by
/// <see cref="Kumunita.Web.Security.TagSlugs.Parse"/> — the same body-parsed
/// idiom as <c>ImageIds</c> / <c>AttachmentIds</c> (the client never
/// posts a structured shape; the server normalizes before the service
/// sees it).</item>
/// <item><see cref="ImageIds"/> / <see cref="AttachmentIds"/> — the RC
/// (ADR 0025) + ATT (ADR 0034) id lists, <b>server-side</b> (the Web
/// layer parses the body's <c>/content-image/{id}</c> /
/// <c>/attachment/{id}</c> links via
/// <see cref="Kumunita.Web.Security.ContentImageIds
/// .ExtractContentImageIds"/> / <see cref="Kumunita.Web.Security
/// .AttachmentIds.ExtractAttachmentIds"/> before calling the service
/// — Core stays body-parse-free, R·5).</item>
/// </list>
/// </para>
/// <para>
/// **Draft round-trip** (the ADR 0037 <c>DraftId</c> shape, the
/// <see cref="AnnouncementComposeViewModel"/> precedent): <see
/// cref="DraftId"/> is a <c>[BindNever]</c> hidden field that the
/// controller sets after the first "save as draft" so a later save
/// updates the same draft (no duplicate). <see cref="Id"/> is the
/// **existing** event's id (set on the edit lane, <c>null</c> on the
/// create lane); the two are distinct fields — <c>DraftId</c> is the
/// round-trip pin for a *draft just saved*, <c>Id</c> is the
/// *pre-existing* event being edited.
/// </para>
/// <para>
/// **Validation** (<see cref="IsValid"/>): <see cref="Title"/> is
/// required (the feed's display label), <see cref="Body"/> is required
/// (the rich-content body), <see cref="Start"/> <c>≤</c>
/// <see cref="End"/> (a well-formed time range), and <see
/// cref="Audience"/> is well-formed (the editor's <c>IsValid</c> — a
/// missing mode is a malformed post, not a silent default).
/// </para>
/// </summary>
public sealed class EventEditorModel
{
    /// <summary>The existing event's id (set only on the edit lane;
    /// <c>null</c> on the create lane). The edit form posts to
    /// <c>/events/</c> + this value + <c>/edit</c>.</summary>
    public string? Id { get; set; }

    /// <summary>The draft's id (a <c>[BindNever]</c> hidden field — the
    /// ADR 0037 round-trip pin, the <see cref="AnnouncementComposeViewModel
    /// .DraftId"/> shape). Set by the controller after a "save as
    /// draft" so a later save updates the same draft.</summary>
    [BindNever]
    public string? DraftId { get; set; }

    /// <summary>The event's display label (the feed's title — the
    /// <see cref="Kumunita.Core.Events.Event.Title"/> row).</summary>
    [Required(ErrorMessage = "A title is required.")]
    public string? Title { get; set; }

    /// <summary>The rich body (Markdown, rendered by the one
    /// <see cref="Kumunita.Web.Security.MarkdownRenderer"/>, edited by
    /// the one <c>bindRichEditor</c>). The RC content-image + ATT
    /// attachment body idioms live here.</summary>
    [Required(ErrorMessage = "A body is required.")]
    public string? Body { get; set; }

    /// <summary>The feed organizer (a <c>Component</c> id) — a
    /// <b>filter, never a gate</b> (C-M3·2). <c>null</c> ⇒ not scoped to
    /// a component (visible in the unfiltered feed).</summary>
    public string? ComponentId { get; set; }

    /// <summary>The event's start instant (UTC <see cref="DateTimeOffset"/>).
    /// Drives feed ordering + the reminder window.</summary>
    [Required(ErrorMessage = "A start time is required.")]
    public DateTimeOffset Start { get; set; }

    /// <summary>The event's end instant (UTC <see cref="DateTimeOffset"/>).</summary>
    [Required(ErrorMessage = "An end time is required.")]
    public DateTimeOffset End { get; set; }

    /// <summary>A free-text location (display metadata, not a gate).</summary>
    public string? Location { get; set; }

    /// <summary>A display capacity hint (ADR 0054 §3.1). **Not a gate** —
    /// there is no admission queue in M4; the <c>Going</c> RSVP set is
    /// the truth.</summary>
    public int? Capacity { get; set; }

    /// <summary>An optional display color (a CSS color, typically a
    /// normalized <c>#RRGGBB</c> hex value) the author picked in the
    /// composer (the <see cref="Location"/> / <see cref="Capacity"/>
    /// shape — display metadata, never a gate). The calendar renders it
    /// as the event chip/block background; null / empty = the theme
    /// default.</summary>
    public string? Color { get; set; }

    /// <summary>The event's <b>audience</b> editor — the M2 reusable
    /// <see cref="AudienceEditorModel"/> (the single-source pin — the
    /// one form-bound audience editor; <see
    /// cref="AudienceEditorModel.BuildAudience()"/> is the one
    /// deserialization site that writes the
    /// <see cref="Kumunita.Core.Events.Event.Audience"/> row verbatim,
    /// ADR 0001-B). The **sole** access boundary on the composer form
    /// (<see cref="ComponentId"/> is a filter, never a gate).</summary>
    public AudienceEditorModel Audience { get; set; } = new();

    /// <summary>The §6.4 job's per-event opt-out: when <c>false</c>, the
    /// <c>EventReminders</c> job skips this event's recipients.
    /// <c>true</c> floor — the single 24-hour-before reminder is the M4
    /// surface.</summary>
    public bool ReminderEnabled { get; set; } = true;

    /// <summary>The composer's <b>save-as-draft</b> toggle (ADR 0037).
    /// When checked, the event is written with
    /// <see cref="Kumunita.Core.Events.Event.IsDraft"/> true: it is
    /// saved but visible to <b>no one except its author</b> (not even a
    /// GlobalAdmin) until the author publishes it (<see
    /// cref="Kumunita.Core.Events.IEventService.PublishAsync"/>). Binds
    /// from a checkbox + hidden fallback (the
    /// <see cref="AnnouncementComposeViewModel.SaveAsDraft"/> round-trip
    /// pin) — the flag survives re-renders of an invalid-POST lane.</summary>
    public bool SaveAsDraft { get; set; }

    /// <summary>The event's <b>authored-in language</b> (ADR 0018,
    /// ADR 0005 B) — the BCP-47 code the author is writing this event
    /// in. A form-bound <c>&lt;select&gt;</c> posting this;
    /// empty/unset is materialized from the instance default
    /// server-side at write time (<see cref="Kumunita.Core.Events
    /// .EventService.CreateAsync"/> / <see cref="Kumunita.Core.Events
    /// .EventService.UpdateAsync"/> — the <c>en</c> floor).</summary>
    public string? LanguageCode { get; set; }

    /// <summary>The composer's language *picker* options — the
    /// instance's **enabled** language catalog (<see cref="Kumunita.Core
    /// .Localization.LanguageCatalog"/>), ordered by
    /// <see cref="Kumunita.Core.Localization.LanguageCatalog
    /// .SortOrder"/>, read at <c>GET</c> and re-read at <c>POST</c>
    /// re-render. <b>[BindNever]</b> — the form POSTs a
    /// <see cref="LanguageCode"/>, not a catalog-list shape.</summary>
    [BindNever]
    public IReadOnlyList<(string Code, string NativeName)> Languages { get; set; } = [];

    /// <summary>The composer's component *picker* options — the
    /// <see cref="Kumunita.Core.UserInfo.Component"/> candidate set (the
    /// enabled set from <see cref="Kumunita.Core.UserInfo
    /// .IUserInfoService.GetComponentsAsync(bool)"/>). <b>[BindNever]</b>
    /// — the form POSTs a <see cref="ComponentId"/>, not a component-list
    /// shape; a form-bound list of components would be a *parallel*
    /// component pick next to <see cref="ComponentId"/> — the
    /// single-source pin (never a second component binding on the
    /// form).</summary>
    [BindNever]
    public IReadOnlyList<(string Id, string Name)> Components { get; set; } = [];

    /// <summary>The composer's <b>tag</b> input — a <see cref="string"/>
    /// form field (posted by <c>client/lib/tag-suggest.ts</c> as a JSON
    /// array of label strings, or a CSV fallback), parsed server-side by
    /// <see cref="Kumunita.Web.Security.TagSlugs.Parse"/>. The client
    /// never posts a structured shape; the server normalizes (trim /
    /// dedup / drop-blank) before the service sees it. <see
    /// cref="IReadOnlyList{T}"/> is **not** a form-bindable shape (ASP.NET
    /// Core's default binder binds a single field to one value, not a
    /// JSON array) — a <see cref="string"/> + parse-helper is the
    /// source-compatible shape (the U8b register's "the simplest
    /// source-compatible shape" note).</summary>
    public string? TagIds { get; set; }

    /// <summary>The composer's <b>content-image</b> id list (RC ADR
    /// 0025) — the <c>MediaObject</c> ids appearing as
    /// <c>/content-image/{id}</c> links in the rendered body.
    /// <b>Server-side</b> (the Web layer parses the body via
    /// <see cref="Kumunita.Web.Security.ContentImageIds
    /// .ExtractContentImageIds"/> before calling the service — the
    /// client never sends a *form field* — a form field would be
    /// spoofable). <b>[BindNever]</b> — the model binder must not
    /// attempt to bind this from the form; the controller populates it
    /// from the body parse.</summary>
    [BindNever]
    public IReadOnlyList<string> ImageIds { get; set; } = [];

    /// <summary>The composer's <b>attachment</b> id list (ATT ADR
    /// 0034) — the <c>MediaObject</c> ids appearing as
    /// <c>/attachment/{id}</c> links in the rendered body.
    /// <b>Server-side</b> (the Web layer parses the body via
    /// <see cref="Kumunita.Web.Security.AttachmentIds
    /// .ExtractAttachmentIds"/> before calling the service — the
    /// client never sends a *form field*). <b>Separate from</b>
    /// <see cref="ImageIds"/> (C-ATT·5). <b>[BindNever]</b>.</summary>
    [BindNever]
    public IReadOnlyList<string> AttachmentIds { get; set; } = [];

    /// <summary>
    /// true when the model is well-formed for a round-trip.
    /// <see cref="Title"/> and <see cref="Body"/> are required (the
    /// feed's display label + the rich body); <see cref="Start"/>
    /// <c>≤</c> <see cref="End"/> (a well-formed time range — an
    /// <c>End</c> before the <c>Start</c> is a malformed shape, not a
    /// silent swap); <see cref="Audience"/> is well-formed (the
    /// editor's <see cref="AudienceEditorModel.IsValid"/> — a missing
    /// mode is a malformed post, not a silent default to
    /// <c>Any</c>, which would <b>change the audience's meaning</b> in
    /// the All-mode-deny-on-empty-grants invariant (C1)).
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Title))
                return false;
            if (string.IsNullOrWhiteSpace(Body))
                return false;
            if (End < Start)
                return false;
            if (Audience is null || !Audience.IsValid)
                return false;
            return true;
        }
    }
}

/// <summary>
/// The <b>feed</b> row (the <c>GET /events</c> read surface — one
/// upcoming event with a resolved author display name + the component's
/// display name (a "a read, not a decision" surface — the
/// <see cref="Kumunita.Core.Events.Event.Audience"/> gate already ran
/// in <see cref="Kumunita.Core.Events.IEventService
/// .ListUpcomingAsync"/>; the display-name lookups are never access
/// decisions).</summary>
/// <param name="Id">The event's id.</param>
/// <param name="Title">The event's display label.</param>
/// <param name="Body">The event's rich body (Markdown — the view
/// renders a <see cref="Kumunita.Web.Security.MarkdownRenderer
/// .PlainTextPreview"/> preview, not the full body).</param>
/// <param name="Start">The event's start instant (UTC — the feed's
/// ordering key).</param>
/// <param name="End">The event's end instant (UTC).</param>
/// <param name="Location">A free-text location (display metadata, not a
/// gate).</param>
/// <param name="AuthorId">The author's subject id (the
/// <see cref="Kumunita.Core.Events.Event.AuthorId"/>).</param>
/// <param name="AuthorDisplayName">The author's display name (a
/// <c>GetProfileAsync</c> read — null-safe: falls back to the raw
/// subject id if the author's profile row is missing — a display-name
/// lookup, never an access decision).</param>
/// <param name="ComponentId">The event's component id (a feed organizer,
/// C-M3·2 — a filter, never a gate).</param>
/// <param name="ComponentDisplayName">The component's display name
/// (a <c>GetComponentsAsync</c> read — null-safe: null when the
/// component is null or not found).</param>
/// <param name="IsDraft">True when the event is a draft (ADR 0037) —
/// the feed excludes drafts unconditionally for non-authors; a draft
/// appearing in a feed means the viewer is the author (the service
/// filters non-author drafts out, so this is true only on the
/// author's own feed view of their own draft).</param>
/// <param name="IsDeleted">True when the event is soft-deleted (ADR
/// 0024) — the feed excludes deleted events unconditionally; this is
/// present for the shape's completeness (the service filters it out,
/// so it is false in every feed row).</param>
/// <param name="Color">The author's picked display color for the
/// calendar (the <c>Location</c> shape — display metadata, never a
/// gate); null = the theme default.</param>
public sealed record EventRow(
    string Id,
    string Title,
    string Body,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Location,
    string AuthorId,
    string AuthorDisplayName,
    string? ComponentId,
    string? ComponentDisplayName,
    bool IsDraft,
    bool IsDeleted,
    DateTimeOffset StartUtc = default,
    DateTimeOffset EndUtc = default,
    string? Color = null);

/// <summary>
/// The <b>feed</b> view model (the <c>GET /events</c> read surface —
/// the caller-visible upcoming-events set, ordered by
/// <see cref="Kumunita.Core.Events.Event.Start"/> ascending, paged).
/// <para>
/// **Author display names** are resolved once per distinct author (a
/// *display* lookup, never an access decision — the audience gate is
/// the service's, not the display-name's). **Component display names**
/// are resolved once for the enabled set (a display lookup, never a
/// gate — C-M3·2: the component is a *feed organizer*, not a decision).
/// A missing profile row falls back to the raw subject id (null-safe:
/// no null-coalescing exception); a missing component falls back to
/// null (the row renders no component badge).
/// </para>
/// <para>
/// **<see cref="Components"/>** (the filter picker's options) is the
/// enabled <c>Component</c> set (the <see cref="Kumunita.Core.UserInfo
/// .IUserInfoService.GetComponentsAsync(bool)"/> read) — a *filter*
/// (C-M3·2), never a gate. <see cref="CurrentComponentId"/> is the
/// currently-selected filter (null ⇒ unfiltered). <see
/// cref="CurrentPage"/> is the current page number (1-based).
/// </para>
/// <para>
/// **<see cref="MyEvents"/>** (ADR 0065, the <c>EV-MINE</c> lane) is the
/// viewer's **own upcoming events** — the union of their RSVPed events (any
/// <see cref="Kumunita.Core.Events.RsvpStatus"/> — the row exists, the
/// resident signed up) and their authored events, restricted to upcoming
/// (<c>Start</c> in the future) and live (<c>!IsDeleted</c>) — from
/// <see cref="Kumunita.Core.Events.IEventService.ListMineAsync"/> (no
/// <c>AccessAudit</c> row — the per-row write lane already committed its
/// decision, the <c>GetMyRsvpAsync</c> posture). The view renders this
/// section first, before the full feed, and **only** when non-empty.
/// </para>
/// </summary>
public sealed record EventIndexViewModel(
    IReadOnlyList<EventRow> Events,
    IReadOnlyList<(string Id, string Name)> Components,
    string? CurrentComponentId,
    int CurrentPage,
    // ADR 0065 (EV-MINE) — default `null!` (a valid compile-time constant;
    // a collection expression `[]` is not a constant and is illegal here):
    // callers that omit the argument treat it as the empty "no section"
    // case — the view's `Count > 0` guard is null-safe against it.
    IReadOnlyList<EventRow> MyEvents = null!);

/// <summary>
/// The <b>calendar</b> view model (the <c>GET /events/calendar</c> read
/// surface — ADR 0063; ADR 0064 <c>EV-DWM</c> adds the Day/Week/Month
/// views over the same window-agnostic
/// <see cref="Kumunita.Core.Events.IEventService
/// .ListInRangeAsync"/> seam).
/// <para>
/// **<see cref="FromAnchor"/> / <see cref="PrevAnchor"/> /
/// <see cref="NextAnchor"/>** are the anchor date and its nav neighbors
/// (shifted by the <b>view's unit</b> — ±1 day / ±1 week / ±1 month),
/// each <c>yyyy-MM-dd</c> in the viewer's effective zone —
/// pre-rendered plain-GET-link targets (C-DWM·6: nav is plain GET links;
/// the server re-renders, the authorization re-runs per request).
/// <see cref="Label"/> is the view-appropriate display string in the UI
/// culture — a full date for Day, a date range for Week, a month name +
/// year for Month (a display string, not a zone-driven calculation, not a
/// registry key — C-DWM·9).
/// </para>
/// <para>
/// **<see cref="View"/>** (default <c>"month"</c> — the backward-
/// compatible EV-CAL default, C-DWM·8) echoes the resolved
/// <c>?view=</c> selector back to the view so the toggle can render the
/// active button pressed. **<see cref="WindowDays"/>** is the ordered
/// list of the view's grid day-columns (date-only
/// <see cref="DateTime"/>): Day → 1 entry (the anchor); Week → 7 entries,
/// Monday-first (C-DWM·5); Month → the 5–6 full weeks covering the
/// calendar month, starting on the Monday on or before the 1st (D4). The
/// per-chip instants remain <see cref="EventRow.StartUtc"/> /
/// <see cref="EventRow.EndUtc"/> (ADR 0063, unchanged).
/// </para>
/// <para>
/// **<see cref="Components"/>** (the filter picker's options) is the
/// enabled <c>Component</c> set — a *filter* (C-M3·2), never a gate;
/// <see cref="CurrentComponentId"/> is the selected filter (null ⇒
/// unfiltered).
/// </para>
/// <para>
/// **<see cref="TimeZoneId"/>** is the effective zone id (ADR 0019) —
/// C-EV·5: a **display** input only (the client distributes chips into
/// day-columns via <c>Intl</c>), **never** an authorization input.
/// </para>
/// </summary>
public sealed record EventCalendarViewModel(
    IReadOnlyList<EventRow> Events,
    string FromAnchor,
    string? PrevAnchor,
    string? NextAnchor,
    string Label,
    string? CurrentComponentId,
    IReadOnlyList<(string Id, string Name)> Components,
    string TimeZoneId,
    string View = "month",
    IReadOnlyList<DateTime> WindowDays = null!);

/// <summary>
/// The <b>detail</b> view model (the <c>GET /events/{id}</c> read
/// surface — the full-body read; the list shows a truncated preview
/// and links here).
/// <para>
/// **<see cref="AuthorDisplayName"/>** is a <c>GetProfileAsync</c>
/// read (null-safe: falls back to the raw subject id if the author's
/// profile row is missing — a display-name lookup, never an access
/// decision — the audience gate already ran in <see
/// cref="Kumunita.Core.Events.IEventService.GetAsync"/>).
/// </para>
/// <para>
/// **<see cref="CanEdit"/>** / **<see cref="CanDelete"/>** are the
/// standing-matrix affordances (ADR 0054 §3.4) — the
/// <see cref="Kumunita.Core.Events.EventService.CheckEditStanding"/>
/// helper (the <see cref="Kumunita.Core.Events.IEventService
/// .UpdateAsync"/> / <see cref="Kumunita.Core.Events.IEventService
/// .DeleteAsync"/> server-side re-check at POST) evaluated against the
/// *stored* event with the principal's real role set (the GlobalAdmin
/// override, ADR 0017, is the Web boundary's job — the frozen seam's
/// lanes pin the author branch only). A shape convenience for the
/// detail page's Edit / Delete buttons (the real gate is the service
/// — a non-authorized viewer never sees the button and never has to
/// hit the 403).
/// </para>
/// <para>
/// **<see cref="CanPublish"/>** is the ADR 0037 author-only publish
/// affordance — the <see cref="Kumunita.Core.Events.IEventService
/// .PublishAsync"/> lane is the sole gate (a non-author, even a
/// GlobalAdmin, is denied). True when the viewer is the author AND the
/// event is a draft (a published event has nothing to publish).
/// </para>
/// <para>
/// **<see cref="MyRsvp"/>** is the viewer's own RSVP (the
/// <see cref="Kumunita.Core.Events.IEventService
/// .GetMyRsvpAsync"/> read — <c>null</c> if the viewer has not
/// RSVPed). **<see cref="Rsvps"/>** is the owner-only RSVP list (the
/// <see cref="Kumunita.Core.Events.IEventService
/// .GetRsvpsAsync"/> read — the author sees their own event's RSVPs; a
/// non-author sees an empty list). The RSVP form (Going / Maybe / No)
/// is **owner-only** visible (a non-author sees only their own RSVP).
/// </para>
/// <para>
/// **<see cref="EventRsvpEntry.DisplayName"/>** is the RSVPing
/// resident's display name — a <c>GetProfileAsync</c> read (null-safe:
/// falls back to the raw subject id if the profile row is missing — a
/// display-name lookup, never an access decision, the same shape as
/// <see cref="AuthorDisplayName"/>). The avatar is served by the same
/// audited lane as every other avatar (<c>GET /profile/avatar/{subjectId}</c>,
/// the Directory pattern — the view's <c>data-avatar-fallback</c> monogram).
/// </para>
/// <para>
/// **<see cref="EventTranslations"/>** is the event's user-added translations
/// (ADR 0059 — the "follow-on lane" ADR 0054 deferred; the
/// <see cref="Kumunita.Core.Events.IEventService
/// .GetEventTranslationsAsync"/> read; **not** an authorization surface — C-M3·1
/// — the Web reads it only after <see cref="Kumunita.Core.Events
/// .IEventService.GetAsync"/> returned the event). **<see cref="Languages"/>**
/// is the enabled language catalog (the
/// <see cref="LanguageOption"/> set — each with its
/// <see cref="LanguageOption.HasTranslation"/> flag set from
/// <see cref="EventTranslations"/>), the chip-swap + "add a translation"
/// candidate list (the ADR 0027 chip-swap + ADR 0022 add-form shape).
/// **<see cref="CanTranslate"/>** is the ADR 0059 display pin
/// (<see cref="Kumunita.Core.Events.EventService.CanAddTranslation"/> — the
/// author / Translator / GlobalAdmin matrix) — true renders the add / edit /
/// remove affordances (the real gate is the server-side re-check in
/// <see cref="Kumunita.Core.Events.IEventService
/// .AddEventTranslationAsync"/> / <see cref="Kumunita.Core.Events.IEventService
/// .UpdateEventTranslationAsync"/> / <see cref="Kumunita.Core.Events.IEventService
/// .RemoveEventTranslationAsync"/>). **<see cref="OriginalLanguageCode"/>**
/// is the event's authored-in tag (ADR 0018) — the "original" variant's
/// language name in the chip-swap + the base row's identity.
/// </para>
/// </summary>
public sealed record EventDetailViewModel(
    Kumunita.Core.Events.Event Event,
    string AuthorDisplayName,
    string? ComponentDisplayName,
    bool CanEdit,
    bool CanDelete,
    bool CanPublish,
    Kumunita.Core.Events.EventRsvp? MyRsvp,
    IReadOnlyList<EventRsvpEntry> Rsvps,
    IReadOnlyList<Kumunita.Core.Events.EventTranslation>? EventTranslations = null,
    IReadOnlyList<LanguageOption>? Languages = null,
    bool CanTranslate = false,
    string OriginalLanguageCode = "")
{
    /// <summary>The event's translations, coalesced to a non-null empty list (a
    /// never-blank shape for the view).</summary>
    public IReadOnlyList<Kumunita.Core.Events.EventTranslation> Translations
        => EventTranslations ?? [];

    /// <summary>The enabled language options, coalesced to a non-null empty list
    /// (a never-blank shape for the view).</summary>
    public IReadOnlyList<LanguageOption> LanguageOptions
        => Languages ?? [];
}

/// <summary>
/// One RSVP row enriched with its resident's <see cref="DisplayName"/>
/// (the display-name lookup the owner-only RSVP list needs to render an
/// avatar + name instead of the raw subject id — a read convenience, no
/// access decision; the avatar's serving lane is the audited
/// <c>GET /profile/avatar/{subjectId}</c> shared by every other avatar).
/// </summary>
public sealed record EventRsvpEntry(
    Kumunita.Core.Events.EventRsvp Rsvp,
    string DisplayName);
