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
    /// The feed (upcoming events) — the candidate set is the non-draft,
    /// non-deleted, non-group-channel events whose <c>Start</c> is at or after
    /// now (the <c>Start &gt;= nowUtc</c> time window — an event is "upcoming"
    /// until it starts; the <see cref="ListPastAsync"/> lane is its exact
    /// mirror on <c>Start &lt; nowUtc</c>); the <c>componentId</c> is a *filter,
    /// never a gate* (C-M3·2); the survivors are <c>CanSeeAsync(Read)</c>-filtered
    /// (C6 / C3), ordered by <c>Start</c> ascending, paged.
    /// <para>
    /// <see cref="EventPage.HasMore"/> (ADR 0090 D1 / D3) is the sole paging
    /// signal: <c>true</c> iff the page's candidate set filled the page
    /// (<c>candidates.Count == PageSize</c>); <c>false</c> on an empty page
    /// (C-M7·5).
    /// </para>
    /// </summary>
    Task<EventPage> ListUpcomingAsync(string? componentId, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// The <b>past events</b> lane (ADR 0109, the <c>EV-PAST</c> lane) — the
    /// <c>/events</c> feed's "Past" option: the candidate set is the
    /// <see cref="ListUpcomingAsync"/> filter plus a time window predicate
    /// <c>Start &lt; nowUtc</c> (an event is past on the moment it starts,
    /// mirroring <see cref="ListInRangeAsync"/>'s start-day rule), ordered by
    /// <c>Start</c> <b>descending</b> (the most recent past event first — the
    /// "history" reading order, unlike the feed's "next" reading order). The
    /// <paramref name="componentId"/> is a *filter, never a gate* (C-M3·2);
    /// group-channel events never reach the community feed (GE·2, ADR 0089);
    /// drafts and deleted events are excluded (the same candidate filter as the
    /// feed — the actor's own draft is surfaced by
    /// <see cref="ListMineAsync"/>/the draft lanes, not by this one).
    /// <para>
    /// Same authorization posture as the feed (C-EV·2 / C6): one standalone
    /// <c>CanSeeAsync(Read)</c> over the page's candidates via
    /// <see cref="EventToAuditableResource"/> — one aggregate
    /// <c>AccessAudit</c> row, <c>TargetKind = "event"</c>; the 0-candidate
    /// early return runs <b>before</b> any decision (no audit row, C-M7·5) and
    /// reports no further page (ADR 0090 D1).
    /// </para>
    /// <para>
    /// <see cref="EventPage.HasMore"/> (ADR 0090 D1 / D3) is the sole paging
    /// signal: <c>true</c> iff the page's candidate set filled the page
    /// (<c>candidates.Count == PageSize</c>). Additive on the frozen seam —
    /// <see cref="ListUpcomingAsync"/> is untouched (the ADR 0097 named-lane
    /// precedent).
    /// </para>
    /// </summary>
    Task<EventPage> ListPastAsync(string? componentId, string actorId, int page, CancellationToken ct = default);

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
    /// M18 (ADR 0119, D5 / C-M18·4 / C-M18·8) — soft-delete **one** occurrence:
    /// set <see cref="Event.IsDeleted"/> = <c>true</c> on the named row only.
    /// Never a hard-delete, never a cascade, never a touch of the head's
    /// <see cref="Event.RecurrenceRule"/> or any sibling (C-M18·8). Standing is
    /// **author ∪ GlobalAdmin** (C-M18·4 — the ADR 0014 / 0016 / 0017 edit-lane
    /// matrix): at the Core level the locked seam signature carries no
    /// <c>actorRoles</c> (the <see cref="PublishAsync"/> author-check precedent),
    /// so a non-author — including a GlobalAdmin whose override is a Web-layer
    /// <c>[Authorize]</c> concern — gets a <c>KeyNotFoundException</c> (the frozen
    /// seam's "absent" 404 shape, **not** a 403, C-M18·4 / D5). A head row
    /// (<see cref="Event.RecurrenceRule"/> non-null) is not skippable — it is
    /// edited via the existing edit lane (the "no delete-entire-series button"
    /// pin). **No** new <c>AccessAction</c> / <c>AccessVia</c> / adapter (C-M18·5).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, the actor
    /// is not the author, or the row is a head row — the same 404 "absent"
    /// observable (no leak, C-M18·4).</exception>
    Task<Event> SkipOccurrenceAsync(string eventId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// M18 (ADR 0119, D5 / C-M18·4 / C-M18·8) — the inverse of
    /// <see cref="SkipOccurrenceAsync"/>: set <see cref="Event.IsDeleted"/> =
    /// <c>false</c> on the named row only. Same standing (C-M18·4), same one-row
    /// scope (C-M18·8), same 404 "absent" no-leak shape (C-M18·4 / D5). A skip is
    /// always reversible by the author (C-M18·8). A head row ( <see
    /// cref="Event.RecurrenceRule"/> non-null) is not undeletable via this seam (the
    /// head's <c>IsDeleted</c> is set by the existing edit / delete lanes). **No**
    /// new <c>AccessAction</c> / <c>AccessVia</c> / adapter (C-M18·5).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, the actor
    /// is not the author, or the row is a head row — the same 404 "absent"
    /// observable (no leak, C-M18·4).</exception>
    Task<Event> UndeleteOccurrenceAsync(string eventId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// RSVP — the **last-write-wins** concurrency exception (§3.2): upserts the
    /// actor's <c>(EventId, UserId)</c> row with the latest <see cref="RsvpStatus"/>;
    /// a conflicting write is a no-op or self-converging. **No <c>AccessAudit</c>
    /// row** (the RSVP is a routine resident action, not an access decision — the
    /// same posture as a profile edit).
    /// </summary>
    Task<EventRsvp> RsvpAsync(string eventId, string actorId, RsvpStatus status, CancellationToken ct = default);

    /// <summary>
    /// The lane — the guardian's <b>approval</b> of a supervised child's
    /// event-attendance request (the <see cref="RsvpStatus"/> the child asked
    /// for, stored on the <see cref="GuardianEventRequest"/>). The child's
    /// own <see cref="RsvpAsync"/> self-lane was refused (the
    /// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianApproves"/>
    /// posture) and a <see cref="GuardianEventRequest"/> row was stored
    /// instead; this seam resolves it: the child's <see cref="EventRsvp"/>
    /// row is written with the request's <see cref="GuardianEventRequest
    /// .DesiredStatus"/>, the request row moves <see
    /// cref="GuardianEventRequestStatus.Pending"/> →
    /// <see cref="GuardianEventRequestStatus.Approved"/>, and one
    /// <c>guardian.event_rsvp_approve</c> audit row (<see
    /// cref="Kumunita.Core.Authorization.AccessVia.Guardian"/>) is written —
    /// the GU <see cref="Kumunita.Core.UserInfo.IUserInfoService
    /// .ApproveGroupInvitationAsync"/> / <see cref
    /// "Kumunita.Core.UserInfo.IUserInfoService.RejectGroupInvitationAsync" />
    /// pair re-expressed over events.
    /// <para>
    /// **Standing gate:** the actor must hold an <b>active</b>
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> over the request's
    /// child (the GU G·2/G·3 deny-by-default; a non-guardian / dissolved link
    /// is a <see cref="UnauthorizedAccessException"/> — the Web's 404).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// there is no <see cref="GuardianEventRequest"/> for this (event, child)
    /// pair — the 404 "absent" observable (no leak).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> over the request's
    /// child — the GU deny-by-default (the Web's 404).</exception>
    /// <exception cref="InvalidOperationException">The request is not
    /// <see cref="GuardianEventRequestStatus.Pending"/> (already resolved — a
    /// re-approval over a resolved row is refused; the
    /// <see cref="GuardianEventRequest"/> state-machine pin).</exception>
    Task<GuardianEventRequest> ApproveEventRsvpAsync(
        string eventId, string childId, string guardianId, CancellationToken ct = default);

    /// <summary>
    /// The lane — the guardian's <b>denial</b> of a supervised child's
    /// event-attendance request: the request row moves <see
    /// cref="GuardianEventRequestStatus.Pending"/> →
    /// <see cref="GuardianEventRequestStatus.Denied"/>, <b>no</b>
    /// <see cref="EventRsvp"/> row is written (the child did not attend), and
    /// one <c>guardian.event_rsvp_deny</c> audit row (<see
    /// cref="Kumunita.Core.Authorization.AccessVia.Guardian"/>) is written —
    /// the GU <see cref="Kumunita.Core.UserInfo.IUserInfoService
    /// .RejectGroupInvitationAsync"/> shape verbatim (a self-lane refusal,
    /// no content write).
    /// <para>
    /// **Standing gate:** the actor must hold an <b>active</b>
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> over the request's
    /// child (the GU G·2/G·3 deny-by-default; a non-guardian / dissolved link
    /// is a <see cref="UnauthorizedAccessException"/> — the Web's 404).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// there is no <see cref="GuardianEventRequest"/> for this (event, child)
    /// pair — the 404 "absent" observable (no leak).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> over the request's
    /// child — the GU deny-by-default (the Web's 404).</exception>
    /// <exception cref="InvalidOperationException">The request is not
    /// <see cref="GuardianEventRequestStatus.Pending"/> (already resolved — a
    /// re-denial over a resolved row is refused; the
    /// <see cref="GuardianEventRequest"/> state-machine pin).</exception>
    Task<GuardianEventRequest> DenyEventRsvpAsync(
        string eventId, string childId, string guardianId, CancellationToken ct = default);

    /// <summary>
    /// The lane — the guardian's <b>veto</b> (removal) of a supervised
    /// child's <b>existing</b> <see cref="EventRsvp"/> row, in the
    /// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianNotifies" />
    /// posture (auto-approve + veto): the child's own <see cref="RsvpAsync"
    /// /> self-lane wrote the row freely, the guardian was notified, and now
    /// the guardian removes it (a hard ceiling over the child's own choice —
    /// the <see cref="Kumunita.Core.UserInfo.Profile
    /// .MessagingRestricted" /> veto shape). The child's <see
    /// cref="EventRsvp"/> row is deleted (a fresh self-RSVP re-creates it —
    /// the veto is one-time over that row, not a standing ban; the lane's
    /// <see cref="Kumunita.Core.UserInfo.EventRsvpMode" />
    /// <see cref="Kumunita.Core.UserInfo.EventRsvpMode.GuardianApproves"/>
    /// posture is the standing-ban shape). One
    /// <c>guardian.event_rsvp_veto</c> audit row (<see
    /// cref="Kumunita.Core.Authorization.AccessVia.Guardian"/>) is written.
    /// <para>
    /// **Standing gate:** the actor must hold an <b>active</b>
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> over the child (the
    /// GU G·2/G·3 deny-by-default; a non-guardian / dissolved link is a
    /// <see cref="UnauthorizedAccessException"/> — the Web's 404).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// the child has no <see cref="EventRsvp"/> row for it — the 404
    /// "absent" observable (no leak; a veto over nothing is a no-op that the
    /// Web surfaces as a user-presentable error).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="Kumunita.Core.UserInfo.GuardianLink"/> over the child — the
    /// GU deny-by-default (the Web's 404).</exception>
    Task VetoEventRsvpAsync(string eventId, string childId, string guardianId, CancellationToken ct = default);

    /// <summary>
    /// The lane — the guardian's read of a supervised child's <b>pending</b>
    /// event-attendance requests (the <see cref
    /// "GuardianEventRequestStatus.Pending"/> rows where
    /// <see cref="GuardianEventRequest.ChildId"/> =
    /// <paramref name="childId"/>, the <see cref="GuardianApproves"/> posture's
    /// approve/deny list). A read, not a decision (the
    /// <see cref="GetMyRsvpAsync"/> posture): no
    /// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> call, no
    /// audit row; the standing gate (an active <see cref
    /// "Kumunita.Core.UserInfo.GuardianLink" /> over the child) is the
    /// Web layer's responsibility (the Detail GET's
    /// <c>ActiveLinkAsync</c> check — a non-guardian learns nothing).
    /// Ordered by <see cref="GuardianEventRequest.RequestedAt"/> descending
    /// (most recent first — the pending-requests card's shape).
    /// </summary>
    Task<IReadOnlyList<GuardianEventRequest>> GetPendingEventRsvpRequestsAsync(
        string childId, CancellationToken ct = default);

    /// <summary>
    /// The lane — the guardian's read of a supervised child's <b>existing</b>
    /// <see cref="EventRsvp"/> rows (the <see cref
    /// "Kumunita.Core.UserInfo.EventRsvpMode.GuardianNotifies"/> posture's
    /// veto list: every event the child currently has an RSVP on). A read, not
    /// a decision (the <see cref="GetMyRsvpAsync"/> posture): no
    /// <see cref="Kumunita.Core.Authorization.IAuthorizationService"/> call,
    /// no audit row; the standing gate (an active <see cref
    /// "Kumunita.Core.UserInfo.GuardianLink" /> over the child) is the Web
    /// layer's responsibility. Ordered by <see cref="EventRsvp.At"/> descending
    /// (most recent first).
    /// </summary>
    Task<IReadOnlyList<EventRsvp>> GetChildRsvpsAsync(
        string childId, CancellationToken ct = default);

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
