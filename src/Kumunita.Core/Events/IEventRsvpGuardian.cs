namespace Kumunita.Core.Events;

/// <summary>
/// The <c>guardian event-attendance</c> seam (ADR 0290 named seam, C3-00) —
/// the 6-method RSVP cluster extracted from <see cref="IEventService"/> as a
/// real part-vs-whole surface (the guardian-RSVP concern: ADR 0028
/// account-scope supervision, ADR 0090 pagination, ADR 0115 guardian-event-link).
/// <para>
/// The Web consumer (the <c>GuardianController</c>, <c>EventController</c>,
/// <c>GroupsController</c>) codes against this interface, not the concrete
/// <see cref="EventRsvpGuardianService"/> (the C03 <c>INotificationEmitter</c>
/// pattern — a named seam the service implements behind it).
/// </para>
/// <para>
/// <see cref="EventRsvpGuardianService"/> owns the cluster:
/// the self-lane <see cref="RsvpAsync"/> (last-write-wins, the guardian gate),
/// the three guardian resolve lanes (approve / deny / veto), and the two
/// guardian read lanes (pending requests, the child's existing rsvps).
/// </para>
/// <para>
/// **No new boundary edge** (the seam is in the <c>Events</c> module; the
/// controllers already reference <c>Events</c> types — verified against the
/// gate's boundary check).
/// </para>
/// </summary>
public interface IEventRsvpGuardian
{
    /// <summary>
    /// RSVP to an event (ADR 0054 §3.2) — the **last-write-wins** concurrency
    /// exception, keyed per <c>(EventId, UserId)</c>: upserts the actor's single
    /// RSVP row with the latest <see cref="RsvpStatus"/>. A resident's latest
    /// status is simply the truth; a conflicting write is a no-op or
    /// self-converging (the <see cref="EventDocTypes"/> unique
    /// <c>(EventId, UserId)</c> index is the DB-enforced business key).
    /// <para>
    /// Standing (server-side, C3): **any signed-in resident** who may RSVP —
    /// a null/empty actor is a <see cref="UnauthorizedAccessException"/> (403).
    /// The event must be present and **not deleted** (a missing/deleted id is a
    /// <see cref="KeyNotFoundException"/> — the non-leaky pin). **No
    /// <see cref="Authorization.AccessAudit"/> row** is stored (the RSVP is a
    /// routine resident action, not an access decision — the same posture as a
    /// profile edit).
    /// </para>
    /// <para>
    /// **Guardian gate (ADR 0028 / ADR 0144):** a supervised child (an active
    /// <see cref="UserInfo.GuardianLink"/>) is gated by their guardian's
    /// <see cref="UserInfo.Profile.EventRsvpMode"/> posture:
    /// <see cref="UserInfo.EventRsvpMode.GuardianApproves"/> refuses the
    /// self-lane and stores a <see cref="GuardianEventRequest"/> row;
    /// <see cref="UserInfo.EventRsvpMode.GuardianNotifies"/> allows the
    /// self-lane and notifies the guardians;
    /// <see cref="UserInfo.EventRsvpMode.ChildDecides"/> allows the self-lane
    /// outright. An unsupervised actor is never gated.
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found or is
    /// deleted — the 404 "absent" observable (no leak).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor id is empty.</exception>
    /// <exception cref="InvalidOperationException">The self-lane is refused
    /// under the <see cref="UserInfo.EventRsvpMode.GuardianApproves"/> posture
    /// (the user-presentable refusal; the request row is stored).</exception>
    Task<EventRsvp> RsvpAsync(string eventId, string actorId, RsvpStatus status, CancellationToken ct = default);

    /// <summary>
    /// The guardian's <b>approval</b> of a supervised child's event-attendance
    /// request (the <see cref="RsvpStatus"/> the child asked for, stored on the
    /// <see cref="GuardianEventRequest"/>). The child's
    /// <see cref="EventRsvp"/> row is written with the request's
    /// <see cref="GuardianEventRequest.DesiredStatus"/>, the request row moves
    /// <see cref="UserInfo.GuardianEventRequestStatus.Pending"/> →
    /// <see cref="UserInfo.GuardianEventRequestStatus.Approved"/>, and one
    /// <c>guardian.event_rsvp_approve</c> audit row
    /// (<see cref="Authorization.AccessVia.Guardian"/>) is written.
    /// <para>
    /// **Standing gate:** the actor must hold an <b>active</b>
    /// <see cref="UserInfo.GuardianLink"/> over the request's child (the
    /// GU G·2/G·3 deny-by-default; a non-guardian / dissolved link is a
    /// <see cref="UnauthorizedAccessException"/> — the Web's 404).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// there is no <see cref="GuardianEventRequest"/> for this (event, child)
    /// pair — the 404 "absent" observable (no leak).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="UserInfo.GuardianLink"/> over the request's child.</exception>
    /// <exception cref="InvalidOperationException">The request is not
    /// <see cref="UserInfo.GuardianEventRequestStatus.Pending"/> (already
    /// resolved — a re-approval over a resolved row is refused).</exception>
    Task<GuardianEventRequest> ApproveEventRsvpAsync(
        string eventId, string childId, string guardianId, CancellationToken ct = default);

    /// <summary>
    /// The guardian's <b>denial</b> of a supervised child's event-attendance
    /// request: the request row moves
    /// <see cref="UserInfo.GuardianEventRequestStatus.Pending"/> →
    /// <see cref="UserInfo.GuardianEventRequestStatus.Denied"/>, <b>no</b>
    /// <see cref="EventRsvp"/> row is written (the child did not attend), and
    /// one <c>guardian.event_rsvp_deny</c> audit row
    /// (<see cref="Authorization.AccessVia.Guardian"/>) is written.
    /// <para>
    /// **Standing gate:** the actor must hold an <b>active</b>
    /// <see cref="UserInfo.GuardianLink"/> over the request's child (the
    /// GU G·2/G·3 deny-by-default; a non-guardian / dissolved link is a
    /// <see cref="UnauthorizedAccessException"/> — the Web's 404).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// there is no <see cref="GuardianEventRequest"/> for this (event, child)
    /// pair — the 404 "absent" observable (no leak).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="UserInfo.GuardianLink"/> over the request's child.</exception>
    /// <exception cref="InvalidOperationException">The request is not
    /// <see cref="UserInfo.GuardianEventRequestStatus.Pending"/> (already
    /// resolved — a re-denial over a resolved row is refused).</exception>
    Task<GuardianEventRequest> DenyEventRsvpAsync(
        string eventId, string childId, string guardianId, CancellationToken ct = default);

    /// <summary>
    /// The guardian's <b>veto</b> (removal) of a supervised child's
    /// <b>existing</b> <see cref="EventRsvp"/> row, in the
    /// <see cref="UserInfo.EventRsvpMode.GuardianNotifies" /> posture
    /// (auto-approve + veto): the child's <see cref="EventRsvp"/> row is
    /// deleted (a fresh self-RSVP re-creates it — the veto is one-time over
    /// that row, not a standing ban). One
    /// <c>guardian.event_rsvp_veto</c> audit row
    /// (<see cref="Authorization.AccessVia.Guardian"/>) is written.
    /// <para>
    /// **Standing gate:** the actor must hold an <b>active</b>
    /// <see cref="UserInfo.GuardianLink"/> over the child (the
    /// GU G·2/G·3 deny-by-default; a non-guardian / dissolved link is a
    /// <see cref="UnauthorizedAccessException"/> — the Web's 404).
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// the child has no <see cref="EventRsvp"/> row for it — the 404
    /// "absent" observable (no leak; a veto over nothing is a no-op that the
    /// Web surfaces as a user-presentable error).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="UserInfo.GuardianLink"/> over the child.</exception>
    Task VetoEventRsvpAsync(string eventId, string childId, string guardianId, CancellationToken ct = default);

    /// <summary>
    /// The guardian's read of a supervised child's <b>pending</b>
    /// event-attendance requests (the
    /// <see cref="UserInfo.GuardianEventRequestStatus.Pending"/> rows where
    /// <see cref="GuardianEventRequest.ChildId"/> = <paramref name="childId"/>,
    /// the <see cref="UserInfo.EventRsvpMode.GuardianApproves"/> posture's
    /// approve/deny list). A read, not a decision: no
    /// <see cref="Authorization.IAuthorizationService"/> call, no audit row;
    /// the standing gate (an active <see cref="UserInfo.GuardianLink" /> over
    /// the child) is the Web layer's responsibility. Ordered by
    /// <see cref="GuardianEventRequest.RequestedAt"/> descending (most recent
    /// first — the pending-requests card's shape).
    /// </summary>
    Task<IReadOnlyList<GuardianEventRequest>> GetPendingEventRsvpRequestsAsync(
        string childId, CancellationToken ct = default);

    /// <summary>
    /// The guardian's read of a supervised child's <b>existing</b>
    /// <see cref="EventRsvp"/> rows (the
    /// <see cref="UserInfo.EventRsvpMode.GuardianNotifies"/> posture's
    /// veto list: every event the child currently has an RSVP on). A read,
    /// not a decision: no <see cref="Authorization.IAuthorizationService"/>
    /// call, no audit row; the standing gate (an active
    /// <see cref="UserInfo.GuardianLink" /> over the child) is the Web layer's
    /// responsibility. Ordered by <see cref="EventRsvp.At"/> descending
    /// (most recent first).
    /// </summary>
    Task<IReadOnlyList<EventRsvp>> GetChildRsvpsAsync(
        string childId, CancellationToken ct = default);
}
