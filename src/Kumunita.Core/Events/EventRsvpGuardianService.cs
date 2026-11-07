using Kumunita.Core.Authorization;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Events;

/// <summary>
/// The <c>guardian event-attendance</c> service (ADR 0290 named seam, C3-00) —
/// the 6-method RSVP cluster extracted from <see cref="EventService"/> as a
/// real part-vs-whole surface (the guardian-RSVP concern: ADR 0028
/// account-scope supervision, ADR 0090 pagination, ADR 0115 guardian-event-link).
/// <para>
/// **What:** the self-lane <see cref="RsvpAsync"/> (last-write-wins, with the
/// guardian gate), the three guardian resolve lanes (approve / deny / veto),
/// and the two guardian read lanes (pending requests, the child's existing
/// rsvps). The <see cref="GuardianEventLinkAsync"/> standing gate (an active
/// <see cref="GuardianLink"/> for the (guardian, child) pair, or
/// <see cref="UnauthorizedAccessException"/> — the GU G·2/G·3 deny-by-default)
/// is the shared resolver.
/// </para>
/// <para>
/// **When:** every event RSVP write and every guardian event-attendance
/// decision (the <c>GuardianController</c> / <c>EventController</c> /
/// <c>GroupsController</c> surface). The <see cref="EventService"/> delegates
/// to this for the 6 methods (the same shape as C2-03's
/// service-delegates-to-seam moves); the <c>Events</c> DI registration
/// supplies the live instance.
/// </para>
/// <para>
/// **Comment rule** (what/when/throws + ADR refs): ADR 0028 (account-scope
/// supervision — the <see cref="GuardianLink"/> /
/// <see cref="EventRsvpMode"/> posture), ADR 0090 (pagination — the pending
/// / child-rsvps reads are small, non-paged at one-neighborhood scale),
/// ADR 0115 (guardian-event-link — the <see cref="GuardianEventRequest"/>
/// row is the link's relationship document).
/// </para>
/// <para>
/// **No new boundary edge** (the seam is in the <c>Events</c> module; the
/// controllers already reference <c>Events</c> types — the gate's boundary
/// check does not rise). Core stays HTTP-free (ADR 0006-D).
/// </para>
/// </summary>
public sealed class EventRsvpGuardianService : IEventRsvpGuardian
{
    private readonly IDocumentStore _store;
    private readonly IUserInfoService _userInfo;
    private readonly INotificationEmitter? _notifications;

    public EventRsvpGuardianService(
        IDocumentStore store,
        IUserInfoService userInfo,
        INotificationEmitter? notifications = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _notifications = notifications;
    }

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
    /// <see cref="KeyNotFoundException"/> — the non-leaky pin, the
    /// <see cref="EventService.GetRsvpsAsync"/> shape). **No <see cref="AccessAudit"/> row**
    /// is stored (the RSVP is a routine resident action, not an access decision
    /// — the same posture as a profile edit; the <see
    /// cref="EventService"/> M4_RsvpWritesNoAccessAuditRow pin). This lane
    /// therefore does **not** route the standing through
    /// <see cref="IAuthorizationService"/> (a <c>CanAsync</c> read-decision
    /// would append a read-audit row, violating the pin) — it enforces
    /// signed-in + presence directly.
    /// </para>
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is empty or the
    /// event is not found / deleted — the 404 "absent" observable (no leak).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor id is empty.</exception>
    /// <exception cref="InvalidOperationException">The self-lane is refused
    /// under the <see cref="EventRsvpMode.GuardianApproves"/> posture (the
    /// user-presentable refusal; the <see cref="GuardianEventRequest"/> row is
    /// stored, the guardians are notified).</exception>
    public async Task<EventRsvp> RsvpAsync(string eventId, string actorId, RsvpStatus status, CancellationToken ct = default)
    {
        // SEC-READ: no disclosure — a write lane with no audience-bearing read: standing is in-method (any signed-in resident, the M4_RsvpWritesNoAccessAuditRow pin — the lane deliberately does not route standing through IAuthorizationService), and the only store reads are the event's presence + the actor's own GuardianLink/Profile rows.
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to RSVP.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null || @event.IsDeleted)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to RSVP to.");

        // The lane — the guardian's event-attendance gate. A <b>supervised</b>
        // child (an active <see cref="GuardianLink"/>) is gated by their
        // guardian's <see cref="Profile.EventRsvpMode"/> posture:
        //   · <see cref="EventRsvpMode.GuardianApproves"/> — the self-lane is
        //     <b>refused</b>: a <see cref="GuardianEventRequest"/> row is
        //     stored (the child's desired status), the active guardian(s) are
        //     notified, and the write throws a user-presentable error (the GU
        //     group-invitation self-lane refusal shape; the Web surfaces
        //     <c>TempData["error"]</c> and redirects back to the event).
        //   · <see cref="EventRsvpMode.GuardianNotifies"/> — the self-lane is
        //     allowed; the active guardian(s) are notified after the write
        //     (auto-approve + the window to veto).
        //   · <see cref="EventRsvpMode.ChildDecides"/> — the self-lane is
        //     allowed outright, no notification.
        // An <b>unsupervised</b> actor (no active link) is never gated — the
        // lane reduces to today's unconditional RSVP regardless of the stored
        // mode. Fail-protective: a supervised child with a missing profile
        // still requires guardian approval.
        var activeLinks = await session.Query<GuardianLink>()
            .Where(l => l.ChildId == actorId && l.Status == GuardianLinkStatus.Active)
            .ToListAsync(ct).ConfigureAwait(false);
        var supervised = activeLinks.Count > 0;
        var guardianIds = activeLinks.Select(l => l.GuardianId).ToHashSet(StringComparer.Ordinal);
        var childProfile = await session.Query<Profile>()
            .Where(p => p.SubjectId == actorId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        var mode = childProfile?.EventRsvpMode
            ?? (supervised ? EventRsvpMode.GuardianApproves : EventRsvpMode.ChildDecides);
        var supervisedApproves = supervised && mode == EventRsvpMode.GuardianApproves;
        var supervisedNotifies = supervised && mode == EventRsvpMode.GuardianNotifies;

        if (supervisedApproves)
        {
            // The <see cref="EventRsvpMode.GuardianApproves"/> posture: refuse
            // the self-lane, store / refresh the request row to the child's
            // current desired status (a fresh act — the <see cref
            // "GuardianLink"/> "writes a fresh row, not a revival" precedent),
            // notify the active guardian(s) (the
            // <see cref="NotificationKinds.GuardianEventRequest"/> kind, the GU
            // <c>guardian.group_invite</c> fan-out shape), then throw the
            // user-presentable refusal.
            var requestedAt = DateTimeOffset.UtcNow;
            var request = await session.Query<GuardianEventRequest>()
                .Where(r => r.EventId == eventId && r.ChildId == actorId)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (request is null)
            {
                request = new GuardianEventRequest
                {
                    Id = Guid.NewGuid().ToString("N"),
                    EventId = eventId,
                    ChildId = actorId,
                    DesiredStatus = status,
                    Status = GuardianEventRequestStatus.Pending,
                    RequestedAt = requestedAt
                };
            }
            else
            {
                request.DesiredStatus = status;
                request.Status = GuardianEventRequestStatus.Pending;
                request.RequestedAt = requestedAt;
                request.ResolvedAt = null;
                request.ResolvedBy = null;
            }
            session.Store(request);

            if (_notifications is not null)
            {
                var childName = string.IsNullOrWhiteSpace(childProfile?.DisplayName) ? actorId : childProfile!.DisplayName;
                foreach (var guardianId in guardianIds)
                {
                    await _notifications.EmitAsync(
                        session,
                        recipientId: guardianId,
                        kind: NotificationKinds.GuardianEventRequest,
                        idempotencyKey: $"notification:{NotificationKinds.GuardianEventRequest}:{eventId}:{actorId}:{guardianId}",
                        body: $"{childName} is asking to attend an event ({status}).",
                        linkPath: $"/me/children/{actorId}",
                        ct: ct).ConfigureAwait(false);
                }
            }

            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                "Your attendance on this event requires your guardian's approval — the request has been sent to your guardian.");
        }

        // Last-write-wins upsert (the (EventId, UserId) unique-index business
        // key): load the actor's existing row; mutate Status/At, else create.
        // One SaveChangesAsync (C3). No audit row (the pin above).
        var rsvp = await session.Query<EventRsvp>()
            .Where(r => r.EventId == eventId && r.UserId == actorId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var now = DateTimeOffset.UtcNow;
        if (rsvp is null)
        {
            rsvp = new EventRsvp
            {
                Id = Guid.NewGuid().ToString("N"),
                EventId = eventId,
                UserId = actorId,
                Status = status,
                At = now
            };
        }
        else
        {
            rsvp.Status = status;
            rsvp.At = now;
        }
        // Re-Store on both branches: a row loaded (or created) and then mutated
        // is not reliably carried to the DB by SaveChangesAsync alone — the
        // PostService "re-store + save" quirk. Storing the freshly-created row
        // here too keeps the idiom uniform.
        session.Store(rsvp);

        // The lane — the <see cref="EventRsvpMode.GuardianNotifies"/> posture:
        // the self-lane was allowed, so now every active guardian is told the
        // child attended / changed their attendance (auto-approve + the window
        // to <see cref="VetoEventRsvpAsync"/>). The recipient is the guardian
        // (one row per active <see cref="GuardianLink"/>), the LinkPath points
        // at the event detail page (where the child's current RSVP is shown).
        // The idempotency key is per-(guardian, event, child) (the
        // <see cref="Notifications.NotificationService"/> dedups on it; a re-RSVP within the
        // same window refreshes the child's row, not the guardian's inbox).
        if (supervisedNotifies && _notifications is not null)
        {
            var notifiedName = string.IsNullOrWhiteSpace(childProfile?.DisplayName) ? actorId : childProfile!.DisplayName;
            foreach (var guardianId in guardianIds)
            {
                await _notifications.EmitAsync(
                    session,
                    recipientId: guardianId,
                    kind: NotificationKinds.GuardianEventRsvp,
                    idempotencyKey: $"notification:{NotificationKinds.GuardianEventRsvp}:{eventId}:{actorId}:{guardianId}",
                    body: $"{notifiedName} RSVP'd {status} (you may veto it on the manage-child page).",
                    linkPath: $"/events/{@event.Id}",
                    ct: ct).ConfigureAwait(false);
            }
        }

        // M6 (ADR 0076, plan U04) — the F3 event-rsvp emitter. The **event's**
        // author is the recipient (the RSVPing resident is the *sender*). The
        // UGC snippet is the RSVP status + the resident's display name (ADR
        // 0018 — the sender's own content). The idempotency key is the §6.3
        // `notification:event.rsvp:{rsvpId}` shape (D4, F10). `EmitAsync` runs
        // on the caller's session and does **not** commit — the
        // `SaveChangesAsync` below is the single commit (C3). A self-RSVP (the
        // author RSVPing to their own event) is skipped — no self-notification.
        if (_notifications is not null
            && !string.IsNullOrWhiteSpace(@event.AuthorId)
            && !string.Equals(@event.AuthorId, actorId, StringComparison.Ordinal))
        {
            var profile = await _userInfo.GetProfileAsync(actorId).ConfigureAwait(false);
            var name = string.IsNullOrWhiteSpace(profile?.DisplayName) ? actorId : profile!.DisplayName;
            await _notifications.EmitAsync(
                session,
                recipientId: @event.AuthorId,
                kind: NotificationKinds.EventRsvp,
                idempotencyKey: $"notification:event.rsvp:{rsvp.Id}",
                body: $"{name} RSVP'd {status}",
                targetId: null,
                linkPath: $"/events/{@event.Id}",
                ct: ct).ConfigureAwait(false);
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return rsvp;
    }

    // ─── The guardian's event-attendance resolve + read lanes ────────────────
    // The GU standing gate re-expressed over events (the ADR 0028 G·2/G·3
    // deny-by-default; the ADR 0038 §F conferral shape). A non-guardian /
    // dissolved link is a <see cref="UnauthorizedAccessException"/> (the Web's
    // 404 — the actor learns nothing). The <see cref="GuardianLink"/> document
    // is the standing's relationship row; the standing is a simple active-link
    // existence check (the same read <see cref="RsvpAsync"/> performs for the
    // gate), so <see cref="GuardianEventLinkAsync"/> queries the
    // <see cref="GuardianLink"/> row directly (no new frozen-surface seam,
    // ADR 0006 §A). ───

    /// <summary>
    /// The lane's standing gate: an <b>active</b> <see cref="GuardianLink"/>
    /// for the (guardian, child) pair, or <see cref="UnauthorizedAccessException"/>
    /// (the GU G·2/G·3 deny-by-default).
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">No active
    /// <see cref="GuardianLink"/> for this (guardian, child) pair.</exception>
    private static async Task<GuardianLink> GuardianEventLinkAsync(
        IDocumentSession session, string guardianId, string childId, CancellationToken ct)
    {
        var link = await session.Query<GuardianLink>()
            .Where(l => l.GuardianId == guardianId
                        && l.ChildId == childId
                        && l.Status == GuardianLinkStatus.Active)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (link is null)
            throw new UnauthorizedAccessException(
                $"No active GuardianLink for {guardianId} over {childId}; no standing to act on the child's event attendance.");
        return link;
    }

    /// <summary>
    /// The lane — the guardian's <b>approval</b> of a supervised child's
    /// event-attendance request (the GU <see cref="IUserInfoService
    /// .ApproveGroupInvitationAsync" /> / <see cref
    /// "IUserInfoService.RejectGroupInvitationAsync" />
    /// pair re-expressed over events): standing gate first (G·2/G·3), then the
    /// request-row resolution (Pending → Approved), then the child's
    /// <see cref="EventRsvp" /> write (the request's
    /// <see cref="GuardianEventRequest.DesiredStatus"/>), then one
    /// <c>guardian.event_rsvp_approve</c> audit row (<see
    /// cref="Authorization.AccessVia.Guardian"/>) — all in one
    /// session / one <c>SaveChangesAsync</c> (C3). The approval is a standing
    /// the GU lane confers, not a content read: the child's
    /// <see cref="EventRsvp" /> is written on their behalf, but the lane never
    /// reads the child's content (the ADR 0028 G·1 cardinal rule).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// there is no <see cref="GuardianEventRequest"/> for this (event, child)
    /// pair.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="GuardianLink"/> over the request's child.</exception>
    /// <exception cref="InvalidOperationException">The request is not
    /// <see cref="GuardianEventRequestStatus.Pending"/>.</exception>
    public async Task<GuardianEventRequest> ApproveEventRsvpAsync(
        string eventId, string childId, string guardianId, CancellationToken ct = default)
    {
        // SEC-READ: owner-only — the standing gate is in-method (GuardianEventLinkAsync's active-link check, EventRsvpGuardianService.cs:340) before the event/request reads; the lane is the guardian's own scope (a write, not a content read — ADR 0028 G·1).
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        if (string.IsNullOrEmpty(childId)) throw new KeyNotFoundException("A child id is required.");
        if (string.IsNullOrEmpty(guardianId)) throw new UnauthorizedAccessException("A guardian id is required.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var now = DateTimeOffset.UtcNow;

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardianEventLinkAsync(session, guardianId, childId, ct).ConfigureAwait(false);

        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null || @event.IsDeleted)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to approve.");

        var request = await session.Query<GuardianEventRequest>()
            .Where(r => r.EventId == eventId && r.ChildId == childId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (request is null)
            throw new KeyNotFoundException(
                $"No event-attendance request for {childId} on event {eventId}; nothing to approve.");
        if (request.Status != GuardianEventRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Request {request.Id} is already {request.Status}; only a Pending request can be approved.");

        // Resolve the request row (Pending → Approved) + write the child's
        // RSVP with the request's DesiredStatus (the last-write-wins upsert
        // the self-lane would have used).
        request.Status = GuardianEventRequestStatus.Approved;
        request.ResolvedAt = now;
        request.ResolvedBy = guardianId;
        session.Store(request);

        var rsvp = await session.Query<EventRsvp>()
            .Where(r => r.EventId == eventId && r.UserId == childId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (rsvp is null)
        {
            rsvp = new EventRsvp
            {
                Id = Guid.NewGuid().ToString("N"),
                EventId = eventId,
                UserId = childId,
                Status = request.DesiredStatus,
                At = now
            };
        }
        else
        {
            rsvp.Status = request.DesiredStatus;
            rsvp.At = now;
        }
        session.Store(rsvp);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.event_rsvp_approve",
            TargetKind = "event",
            TargetId = eventId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return request;
    }

    /// <summary>
    /// The lane — the guardian's <b>denial</b> of a supervised child's
    /// event-attendance request (the GU <see cref="IUserInfoService
    /// .RejectGroupInvitationAsync" /> shape verbatim):
    /// standing gate first (G·2/G·3), then the request-row resolution
    /// (Pending → Denied) + one <c>guardian.event_rsvp_deny</c> audit row
    /// (<see cref="Authorization.AccessVia.Guardian"/>) — all in
    /// one session / one <c>SaveChangesAsync</c> (C3). A denial writes <b>no</b>
    /// <see cref="EventRsvp"/> row (the child did not attend; the self-lane
    /// refusal left no content behind).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// there is no <see cref="GuardianEventRequest"/> for this (event, child)
    /// pair.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="GuardianLink"/> over the request's child.</exception>
    /// <exception cref="InvalidOperationException">The request is not
    /// <see cref="GuardianEventRequestStatus.Pending"/>.</exception>
    public async Task<GuardianEventRequest> DenyEventRsvpAsync(
        string eventId, string childId, string guardianId, CancellationToken ct = default)
    {
        // SEC-READ: owner-only — the standing gate is in-method (GuardianEventLinkAsync's active-link check, EventRsvpGuardianService.cs:431) before the event/request reads; the lane is the guardian's own scope (a write, not a content read — ADR 0028 G·1).
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        if (string.IsNullOrEmpty(childId)) throw new KeyNotFoundException("A child id is required.");
        if (string.IsNullOrEmpty(guardianId)) throw new UnauthorizedAccessException("A guardian id is required.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var now = DateTimeOffset.UtcNow;

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardianEventLinkAsync(session, guardianId, childId, ct).ConfigureAwait(false);

        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null || @event.IsDeleted)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to deny.");

        var request = await session.Query<GuardianEventRequest>()
            .Where(r => r.EventId == eventId && r.ChildId == childId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (request is null)
            throw new KeyNotFoundException(
                $"No event-attendance request for {childId} on event {eventId}; nothing to deny.");
        if (request.Status != GuardianEventRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Request {request.Id} is already {request.Status}; only a Pending request can be denied.");

        request.Status = GuardianEventRequestStatus.Denied;
        request.ResolvedAt = now;
        request.ResolvedBy = guardianId;
        session.Store(request);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.event_rsvp_deny",
            TargetKind = "event",
            TargetId = eventId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return request;
    }

    /// <summary>
    /// The lane — the guardian's <b>veto</b> (removal) of a supervised child's
    /// existing <see cref="EventRsvp" /> (the <see
    /// cref="EventRsvpMode.GuardianNotifies" />
    /// posture's window to undo an auto-approved attendance — the
    /// <see cref="Profile.MessagingRestricted" /> veto
    /// shape): standing gate first (G·2/G·3), then the child's
    /// <see cref="EventRsvp" /> deletion + one <c>guardian.event_rsvp_veto</c>
    /// audit row (<see cref="Authorization.AccessVia.Guardian"/>)
    /// — all in one session / one <c>SaveChangesAsync</c> (C3). A veto over
    /// nothing (no <see cref="EventRsvp"/> row) is a
    /// <see cref="KeyNotFoundException"/> (the Web's user-presentable 404).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or
    /// the child has no <see cref="EventRsvp"/> row for it.</exception>
    /// <exception cref="UnauthorizedAccessException">The actor has no active
    /// <see cref="GuardianLink"/> over the child.</exception>
    public async Task VetoEventRsvpAsync(
        string eventId, string childId, string guardianId, CancellationToken ct = default)
    {
        // SEC-READ: owner-only — the standing gate is in-method (GuardianEventLinkAsync's active-link check, EventRsvpGuardianService.cs:497) before the event/RSVP reads; the lane is the guardian's own scope (a write, not a content read — ADR 0028 G·1).
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        if (string.IsNullOrEmpty(childId)) throw new KeyNotFoundException("A child id is required.");
        if (string.IsNullOrEmpty(guardianId)) throw new UnauthorizedAccessException("A guardian id is required.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var now = DateTimeOffset.UtcNow;

        // Standing gate first (G·2/G·3): an ACTIVE link for this exact pair.
        await GuardianEventLinkAsync(session, guardianId, childId, ct).ConfigureAwait(false);

        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null || @event.IsDeleted)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to veto.");

        var rsvp = await session.Query<EventRsvp>()
            .Where(r => r.EventId == eventId && r.UserId == childId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (rsvp is null)
            throw new KeyNotFoundException(
                $"No RSVP for {childId} on event {eventId}; nothing to veto.");

        session.Delete(rsvp);

        session.Store(new Authorization.AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = now,
            ActorId = guardianId,
            EffectivePrincipalId = guardianId,
            Action = "guardian.event_rsvp_veto",
            TargetKind = "event",
            TargetId = eventId,
            Via = Authorization.AccessVia.Guardian,
            Outcome = Authorization.AccessOutcome.Allow
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The lane — the guardian's read of a supervised child's
    /// <b>pending</b> event-attendance requests (a read, not a decision — the
    /// <see cref="EventService.GetMyRsvpAsync"/> posture: no <see cref
    /// "IAuthorizationService" /> call, no audit row). The standing gate
    /// (an active <see cref="GuardianLink" /> over the child) is the Web
    /// layer's (the Detail GET's <c>ActiveLinkAsync</c> check — a non-guardian
    /// learns nothing; this read itself is a plain query). Ordered by
    /// <see cref="GuardianEventRequest.RequestedAt"/> descending.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The child id is empty.</exception>
    public async Task<IReadOnlyList<GuardianEventRequest>> GetPendingEventRsvpRequestsAsync(
        string childId, CancellationToken ct = default)
    {
        // SEC-READ: caller-gated by GuardianController.Detail — that action runs ActiveLinkAsync(subject, childId) (the guardian's active-link standing, GuardianController.cs:129) before this seam; a non-guardian learns nothing.
        if (string.IsNullOrEmpty(childId)) throw new KeyNotFoundException("A child id is required.");

        await using var session = _store.QuerySession();
        return await session.Query<GuardianEventRequest>()
            .Where(r => r.ChildId == childId && r.Status == GuardianEventRequestStatus.Pending)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The lane — the guardian's read of a supervised child's <b>existing</b>
    /// <see cref="EventRsvp"/> rows (a read, not a decision — the
    /// <see cref="EventService.GetMyRsvpAsync"/> posture: no <see cref
    /// "IAuthorizationService" /> call, no audit row). The standing gate
    /// (an active <see cref="GuardianLink" /> over the child) is the Web
    /// layer's (the Detail GET's <c>ActiveLinkAsync</c> check — a non-guardian
    /// learns nothing; this read itself is a plain query). Ordered by
    /// <see cref="EventRsvp.At"/> descending.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The child id is empty.</exception>
    public async Task<IReadOnlyList<EventRsvp>> GetChildRsvpsAsync(
        string childId, CancellationToken ct = default)
    {
        // SEC-READ: caller-gated by GuardianController.Detail — that action runs ActiveLinkAsync(subject, childId) (the guardian's active-link standing, GuardianController.cs:129) before this seam; a non-guardian learns nothing.
        if (string.IsNullOrEmpty(childId)) throw new KeyNotFoundException("A child id is required.");

        await using var session = _store.QuerySession();
        return await session.Query<EventRsvp>()
            .Where(r => r.UserId == childId)
            .OrderByDescending(r => r.At)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
