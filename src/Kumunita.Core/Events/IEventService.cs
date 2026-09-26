using Marten;

namespace Kumunita.Core.Events;

/// <summary>
/// The <c>M4</c> (Events) service seam (ADR 0054 §4 — the exact C# signatures U03 /
/// U04 implement; the design doc's §4 block is the authoritative pin).
/// <para>
/// **U01 registers this interface + a skeleton <see cref="EventService"/> — the
/// methods throw <see cref="NotImplementedException"/> for now** (U03 / U04 land the
/// logic). The seam is the **frozen** surface the Web consumer
/// (<c>EventController</c>, U05) codes against and the <c>EventServiceTests</c>
/// (U09) assert against — a rename or re-scope of a method after U01 is a drift
/// event (ADR 0054 §3.9).
/// </para>
/// <para>
/// The read lanes (U03) route every access decision through the **frozen**
/// <c>IAuthorizationService</c> (ADR 0006) via the <c>EventToAuditableResource</c>
/// adapter (U02) — <c>CanSeeAsync(Read)</c> for the feed, <c>CanAsync(Read)</c> for
/// the detail (the 404-vs-403 split, C3). The write lanes (U04) **re-check standing
/// server-side** (the <c>AnnouncementService.CreateAsync</c> C3 pattern — the Web
/// <c>[Authorize]</c> is a convenience pre-gate only, never the source of truth).
/// </para>
/// </summary>
public interface IEventService
{
    // --- Read lanes (U03) -------------------------------------------------------

    /// <summary>
    /// The feed (upcoming events) — the <c>componentId</c> is a *filter, never a
    /// gate* (C-M3·2); the survivors are <c>CanSeeAsync(Read)</c>-filtered (C6 / C3),
    /// ordered by <c>Start</c> ascending, paged. Drafts and deleted events are
    /// excluded for non-authors.
    /// </summary>
    Task<IReadOnlyList<Event>> ListUpcomingAsync(string? componentId, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// The <c>EV-CAL</c> calendar window (ADR 0063 D2) — the feed's candidate set
    /// restricted to <c>[windowStartUtc, windowEndUtc)</c>: an event is in the
    /// window on the day it <b>starts</b> (<c>Start &gt;= windowStartUtc &amp;&amp;
    /// Start &lt; windowEndUtc</c>, inclusive start / exclusive end). Same candidate
    /// filter as <see cref="ListUpcomingAsync"/> (<c>!IsDeleted &amp;&amp; !IsDraft</c>,
    /// optional <c>ComponentId</c> filter — C-M3·2, a filter never a gate), the
    /// same single <c>CanSeeAsync(Read)</c> gate over
    /// <see cref="EventToAuditableResource"/> (C-EV·2, one aggregate
    /// <c>AccessAudit</c> row, <c>TargetKind = "event"</c>), the same standalone
    /// form (no in-flight caller transaction). <b>C-EV·1</b>: shows exactly what
    /// <see cref="ListUpcomingAsync"/> would for this window.
    /// </summary>
    Task<IReadOnlyList<Event>> ListInRangeAsync(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        string? componentId,
        string actorId,
        CancellationToken ct = default);

    /// <summary>
    /// One event (the detail view) — a single <c>CanAsync(actorId, Read, adapter)</c>
    /// decision (the <c>EventToAuditableResource</c>, U02). <c>KeyNotFoundException</c>
    /// (404) on absent, <c>UnauthorizedAccessException</c> (403) on denied — the
    /// announcement 404-vs-403 split (C3).
    /// </summary>
    Task<Event> GetAsync(string eventId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// The RSVP **list** for an event — the **owner-only** read: the author sees their
    /// own event's RSVPs (a non-author caller is denied, §3.2).
    /// </summary>
    Task<IReadOnlyList<EventRsvp>> GetRsvpsAsync(string eventId, CancellationToken ct = default);

    /// <summary>
    /// The actor's **own** RSVP for an event — the last-write-wins read (§3.2).
    /// <c>null</c> if the actor has not RSVPed.
    /// </summary>
    Task<EventRsvp?> GetMyRsvpAsync(string eventId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// The actor's **own upcoming events** (ADR 0065, the <c>EV-MINE</c> lane) —
    /// the <c>/events</c> feed's "your events" section. The set is the **union**
    /// of:
    /// <list type="bullet">
    /// <item>the actor's <b>RSVPs</b> — every <see cref="EventRsvp"/> row keyed
    /// to <paramref name="actorId"/>'s <c>SubjectId</c>, resolved to its
    /// <see cref="Event"/>, regardless of <see cref="RsvpStatus"/> (the resident
    /// has *signed up* — the row exists — whether <c>Going</c> /
    /// <c>Maybe</c> / <c>No</c>; the status is surfaced by the caller, not
    /// filtered here); and</item>
    /// <item>the actor's **authored** events (<see cref="Event.AuthorId"/> =
    /// <paramref name="actorId"/>) — a resident's own events are always on their
    /// "my events" list, whether or not they also RSVPed.</item>
    /// </list>
    /// restricted to **upcoming** — <see cref="Event.Start"/> strictly in the
    /// future (a past event is no longer "upcoming" — it leaves the section
    /// rather than being re-rendered) — and to **live** events
    /// (<c>!IsDeleted</c>; a soft-deleted event is invisible to its author here
    /// too, the ADR 0024 read-lane shape). **Draft** events are *included*
    /// (ADR 0037 author-only visibility — the author's own draft is on their
    /// list; no other actor can see it, so the union is inherently non-leaky:
    /// every event in the result is either authored by the actor or has an
    /// <see cref="EventRsvp"/> row keyed to them, and <see cref="RsvpAsync"/>
    /// requires the event to be visible to the actor — a denied actor never
    /// gets an RSVP row they could later read back). Ordered by
    /// <see cref="Event.Start"/> ascending; capped at <c>50</c> (a backstop,
    /// not a page — the per-actor set is small at one-neighborhood scale, and
    /// a single section should not page).
    /// <para>
    /// **No <c>AccessAudit</c> row** (the ADR 0054 §3.2 / §3.4 posture for
    /// RSVP-shaped reads): this read inherits the visibility decision of the
    /// write that created each row — the RSVP write lane and the
    /// <see cref="CreateAsync"/> / <see cref="UpdateAsync"/> write lanes each
    /// commit their own audit row — so this surface does not re-commit a
    /// decision. The same posture as <see cref="GetMyRsvpAsync"/>'s read of a
    /// single row; no <c>IAuthorizationService</c> call here.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Event>> ListMineAsync(string actorId, CancellationToken ct = default);

    // --- Write lanes (U04) — standing re-checked server-side (§3.4, C3) --------

    /// <summary>
    /// Create an event — the author's choice is written verbatim (ADR 0001-B); the
    /// <c>IsDraft</c> flag is the ADR 0037 pin (a newly created event is a draft). The
    /// author becomes the standing owner (the <c>AuthorId</c> branch). The
    /// <c>AccessAudit</c> row (<c>event.create</c>, <c>TargetKind = "event"</c>) is
    /// stored in the caller's session (C3).
    /// </summary>
    Task<Event> CreateAsync(string actorId, CreateEventRequest request, CancellationToken ct = default);

    // --- Group events lane (ADR 0089) — the ADR 0013 membership lane applied to
    //     the M4 event surface. Reuses the frozen group seams verbatim (zero new
    //     authorization surface); publish / RSVP / translations reuse the existing
    //     lane-neutral seams (PublishAsync / RsvpAsync / the ADR 0059 translation
    //     seams — all keyed by EventId, no group branch). -------------------------

    /// <summary>
    /// The group's upcoming **published** events, membership-scoped (ADR 0089 GE·1/GE·5):
    /// the candidate set is <c>GroupId == groupId &amp;&amp; !IsDeleted &amp;&amp;
    /// !IsDraft</c>, ordered by <c>Start</c> ascending, paged. One standalone
    /// <c>CanSeeGroupFeedAsync(actorId, groupId)</c> call — Allow ⇒ the events
    /// (aggregate row, TargetKind <c>"grouppost"</c>), Deny ⇒ empty +
    /// <see cref="GroupEventFeedResult.HiddenCount"/> (the Deny row survives, GE·5).
    /// **Zero** candidates ⇒ an empty result, **no** row (a non-member's empty feed
    /// is the same shape, distinguished only by the audit row).
    /// </summary>
    Task<GroupEventFeedResult> ListGroupEventsAsync(string groupId, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// One group event, **fail-closed** (ADR 0089 GE·1/GE·4): <c>null</c> for a
    /// non-existent event, an empty/mismatched <c>GroupId</c>, a non-member, or a
    /// draft the actor did not author (the ADR 0037 draft gate runs **before** the
    /// membership check — a pure <c>AuthorId == actorId</c> ordinal, **no** audit
    /// row). A member (or an in-scope <c>read</c> delegate acting with the owner's
    /// standing, GE·6) gets the event with one
    /// <c>CanSeeGroupAsync(actorId, groupId, eventId)</c> decision row (GE·5).
    /// </summary>
    Task<Event?> GetGroupEventAsync(string groupId, string eventId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Create a group event — the create gate **is** the group-lane decision
    /// (ADR 0089 GE·3): Allow ⇒ the event is written; Deny ⇒
    /// <see cref="UnauthorizedAccessException"/> **after** the gate's audit row is
    /// committed (so the Deny row survives, C3). Runs in the **caller's**
    /// <paramref name="session"/> (the standalone-forms commit themselves; the
    /// session-forms commit in the caller's transaction — the
    /// <c>PostService.CreateGroupPostAsync</c> precedent). Pins the write shape
    /// (GE·8): <see cref="Event.GroupId"/> = the lane marker,
    /// <c>ComponentId = string.Empty</c>, <c>Audience = new Audience()</c>,
    /// <c>IsDraft = true</c>. Stores the <c>event.create</c> row (TargetKind
    /// <c>"event"</c>, <c>Via Owner</c>) in the session (GE·5).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The actor is not a member (the
    /// Deny audit row is committed before the throw).</exception>
    Task<Event> CreateGroupEventAsync(GroupEventDraft draft, string actorId, IDocumentSession session, CancellationToken ct = default);

    /// <summary>
    /// Edit a group event — **author-only** (ADR 0089 GE·4, the ADR 0016 / 0037
    /// group-lane precedent — a non-author, even a GlobalAdmin, is denied): stamps
    /// only the editable surface (<see cref="GroupEventUpdate"/>), re-stamps
    /// <c>Modified</c>, leaves the lane markers (<c>GroupId</c> / <c>ComponentId</c>
    /// / <c>Audience</c> / <c>AuthorId</c> / <c>Created</c> / <c>IsDraft</c> /
    /// <c>IsDeleted</c>) untouched, and stores **no** audit row (the ADR 0016
    /// author-lane precedent). Runs in the **caller's** <paramref name="session"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or it is not
    /// a group event (empty <c>GroupId</c>).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the author.</exception>
    Task<Event> UpdateGroupEventAsync(string eventId, string actorId, GroupEventUpdate update, IDocumentSession session, CancellationToken ct = default);

    /// <summary>
    /// Edit an event — **author ∪ GlobalAdmin** (the ADR 0014 / 0016 / 0017
    /// precedent, enforced server-side per ADR 0054 §3.4). <c>AuthorId</c> /
    /// <c>Created</c> preserved untouched; <c>Modified</c> stamped on a real change.
    /// The <c>actorRoles</c> parameter carries the principal's real role set (the Web
    /// layer passes <c>RoleSet(User)</c>) so the GlobalAdmin override branch of
    /// <see cref="EventService.CheckEditStanding"/> is exercised in this service —
    /// the <c>AnnouncementService.UpdateAsync</c> / <c>PageService.CheckEditStanding</c>
    /// precedent (the Web hands in real roles; Core enforces the matrix).
    /// The <c>AccessAudit</c> row (<c>event.update</c>) is stored in the caller's
    /// session (C3).
    /// </summary>
    Task<Event> UpdateAsync(string eventId, string actorId, IReadOnlySet<string> actorRoles, UpdateEventRequest request, CancellationToken ct = default);

    /// <summary>
    /// Publish a draft — **author-only** (the ADR 0037 pin — a non-author, even a
    /// GlobalAdmin, is denied): flips <c>IsDraft = false</c>. The <c>AccessAudit</c>
    /// row (<c>event.publish</c>) is stored in the caller's session (C3).
    /// </summary>
    Task<Event> PublishAsync(string eventId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** an event — sets <c>IsDeleted = true</c> (the ADR 0024
    /// author-lane shape); the record is kept and the read lanes filter it out.
    /// Standing: **author ∪ GlobalAdmin** (ADR 0054 §3.4, the ADR 0017 override
    /// branch) — the <c>actorRoles</c> parameter carries the principal's real role set
    /// so the GlobalAdmin override is enforced server-side (the
    /// <c>AnnouncementService</c> / <c>PageService</c> precedent). The
    /// <c>AccessAudit</c> row (<c>event.delete</c>) is stored in the caller's session
    /// (C3).
    /// </summary>
    Task DeleteAsync(string eventId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// RSVP — the **last-write-wins** concurrency exception (§3.2): upserts the
    /// actor's <c>(EventId, UserId)</c> row with the latest <see cref="RsvpStatus"/>;
    /// a conflicting write is a no-op or self-converging. **No <c>AccessAudit</c>
    /// row** (the RSVP is a routine resident action, not an access decision — the
    /// same posture as a profile edit).
    /// </summary>
    Task<EventRsvp> RsvpAsync(string eventId, string actorId, RsvpStatus status, CancellationToken ct = default);

    // --- Translation lanes (ADR 0059 — the "follow-on lane" ADR 0054 deferred) --
    //
    // Mirrors the PostService / AnnouncementService translation seams (ADR 0022 /
    // 0029 / 0048) on the M4 self-composed-session convention (no caller
    // IDocumentSession — EventService opens its own write session).

    /// <summary>
    /// The **read** seam for an event's user-added translations (ADR 0059): every
    /// <see cref="EventTranslation"/> whose <see cref="EventTranslation.EventId"/>
    /// is <paramref name="eventId"/>, ordered by <see cref="EventTranslation.LanguageCode"/>.
    /// Owns its own <c>QuerySession</c> (C3 read lane); **not** an authorization
    /// surface and writes **no** audit row (C-M3·1 — inherits the parent event's
    /// single <c>Read</c> decision, which the caller has already made; the Web
    /// reads this only after <see cref="GetAsync"/> returned the event).
    /// </summary>
    Task<IReadOnlyList<EventTranslation>> GetEventTranslationsAsync(string eventId);

    /// <summary>
    /// Add a **user-added translation** of an event into a language other than the
    /// one it was authored in (ADR 0059). One row per <c>(eventId, languageCode)</c>
    /// pair (the <see cref="M4DocTypes.Configure"/>'s <c>(EventId, LanguageCode)</c>
    /// unique index).
    /// <para>
    /// <b>Standing (ADR 0059, the approved default):</b> the event's
    /// <b>author</b> (<see cref="AccessVia.Owner"/>); a
    /// <see cref="Identity.Roles.Translator"/> (ADR 0021, <see cref="AccessVia.Admin"/>);
    /// or a <see cref="Identity.Roles.GlobalAdmin"/> (<see cref="AccessVia.Admin"/>).
    /// Events have no component-moderator standing (ADR 0054 §5 — the M4 community
    /// lane has no component-moderator branch). A denied actor throws
    /// <see cref="UnauthorizedAccessException"/> before anything is stored.
    /// </para>
    /// The <c>AccessAudit</c> row (<c>eventtranslation.add</c>,
    /// <c>TargetKind = "event"</c>) commits atomically with the write (C3).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// author / Translator / GlobalAdmin standings.</exception>
    Task<EventTranslation> AddEventTranslationAsync(
        string eventId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Edit an existing event translation (ADR 0059, the ADR 0048 edit-lane shape):
    /// updates the <see cref="EventTranslation.Title"/> /
    /// <see cref="EventTranslation.Body"/> for the <c>(eventId, languageCode)</c>
    /// pair, stamps <see cref="EventTranslation.Created"/> and re-records
    /// <see cref="EventTranslation.AuthorId"/>.
    /// <para>
    /// <b>Standing (ADR 0059):</b> the same matrix as
    /// <see cref="AddEventTranslationAsync"/> (author / Translator / GlobalAdmin).
    /// A missing row is a <see cref="KeyNotFoundException"/> (404). The
    /// <c>AccessAudit</c> row (<c>eventtranslation.update</c>,
    /// <c>TargetKind = "event"</c>) commits atomically with the write (C3).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event or the (event, language)
    /// row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// author / Translator / GlobalAdmin standings.</exception>
    Task<EventTranslation> UpdateEventTranslationAsync(
        string eventId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Remove an event translation (ADR 0059, the ADR 0048 remove-lane shape):
    /// deletes the <c>(eventId, languageCode)</c> row.
    /// <para>
    /// <b>Standing (ADR 0059):</b> the same matrix as
    /// <see cref="AddEventTranslationAsync"/> (author / Translator / GlobalAdmin).
    /// A missing row is a <see cref="KeyNotFoundException"/> (404). The
    /// <c>AccessAudit</c> row (<c>eventtranslation.remove</c>,
    /// <c>TargetKind = "event"</c>) commits atomically with the write (C3).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event or the (event, language)
    /// row is not found.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor holds none of the
    /// author / Translator / GlobalAdmin standings.</exception>
    Task RemoveEventTranslationAsync(
        string eventId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);
}
