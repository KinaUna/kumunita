using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.UserInfo;
using Marten;

namespace Kumunita.Core.Events;

/// <summary>
/// The <c>M4</c> (Events) composition service (ADR 0054). A store-composing
/// service kept behind <see cref="IEventService"/> so the Web-side consumer (the
/// <c>EventController</c>, U05) can be tested without a live Postgres (NSubstitute),
/// mirroring the <see cref="Announcements.AnnouncementService"/> /
/// <see cref="Posts.PostService"/> shape.
/// <para>
/// **U01 — this unit — declares the seam + this skeleton and registers it** (the
/// design doc §4 / plan U01). **The read + write lanes are implemented in U03 / U04**;
/// every method body here is a <see cref="NotImplementedException"/> placeholder
/// (the "skeleton, logic lands in U03/U04" pin from the user's brief). The
/// constructor composes the **frozen** seams the lanes will need:
/// <see cref="IDocumentStore"/> (reads open their own <c>QuerySession</c>, writes go
/// through the caller's in-flight <c>IDocumentSession</c> — the C3 same-transaction
/// shape, mirroring <see cref="Announcements.AnnouncementService"/>),
/// <see cref="IAuthorizationService"/> (the frozen read/write decision path, ADR
/// 0006, via the <c>EventToAuditableResource</c> adapter U02), and
/// <see cref="IUserInfoService"/> (standing — role / membership probes).
/// </para>
/// <para>
/// **No new seam on a frozen interface** is opened here (ADR 0054 §3.9 / ADR 0006
/// §A) — the constructor only *consumes* the existing <c>IAuthorizationService</c> /
/// <c>IUserInfoService</c> surfaces. The <c>EventService</c> is the *adapter*
/// (bounded context), not a *branch*.
/// </para>
/// </summary>
public sealed class EventService : IEventService
{
    private const int PageSize = 30;

    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authorization;
    private readonly IUserInfoService _userInfo;

    public EventService(IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
    }

    // --- Read lanes (U03) -------------------------------------------------------

    /// <summary>
    /// The upcoming-events feed (ADR 0054 §4 — U03). Mirrors
    /// <see cref="Posts.PostService.ListFeedAsync"/>: the candidate set is the
    /// non-draft, non-deleted events ordered by <see cref="Event.Start"/>
    /// ascending (the feed-ordering index, <see cref="M4DocTypes.Configure"/>);
    /// the <paramref name="componentId"/> is a *filter, never a gate*
    /// (C-M3·2); the survivors are <see
    /// cref="IAuthorizationService.CanSeeAsync(string, AccessAction, System.Collections.Generic.IEnumerable{IAuditableResource})"/>
    /// -filtered (C6, the one shared matching pass; C3, the single aggregate
    /// <see cref="AccessAudit"/> row with <c>TargetKind = "event"</c> via the
    /// <see cref="EventToAuditableResource"/>, U02).
    /// </summary>
    public async Task<IReadOnlyList<Event>> ListUpcomingAsync(string? componentId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<Event> q = session.Query<Event>()
            .Where(e => !e.IsDeleted && !e.IsDraft);
        if (componentId is not null)
            q = q.Where(e => e.ComponentId == componentId);
        var candidates = await q.OrderBy(e => e.Start).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        if (candidates.Count == 0)
            return Array.Empty<Event>();

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "event"), from that single call (the PostService shape).
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 ListAsync precedent), so
        // the standalone method's own commit is the correct C3 lane.
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(e => new EventToAuditableResource(e)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return candidates.Where(e => visibleIds.Contains(e.Id)).ToList();
    }

    /// <summary>
    /// One event (the detail view) — a single
    /// <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// decision over the <see cref="EventToAuditableResource"/> (U02) (C6, one
    /// matching pass; C3, the single decision audit row). <see
    /// cref="KeyNotFoundException"/> (404) on a missing id, <see
    /// cref="UnauthorizedAccessException"/> (403) on a Deny — the announcement
    /// 404-vs-403 split (the RC serving-route convention).
    /// <para>
    /// A **draft** event bypasses the authorization decision: it is visible to
    /// **everyone except its author** (ADR 0037 — the author-only draft pin);
    /// no <c>CanAsync</c> call, no audit row (the
    /// <see cref="Announcements.AnnouncementService.GetAsync"/> shape — a pure
    /// <c>AuthorId == actorId</c> ordinal check). The non-leaky 404 / 403 split
    /// holds: a non-author (or an anonymous caller) is denied.
    /// </para>
    /// </summary>
    public async Task<Event> GetAsync(string eventId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");

        await using var session = _store.QuerySession();
        var ev = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (ev is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        if (ev.IsDeleted)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        // ADR 0037 — draft gate (author-only, no audit row): a draft event is
        // invisible to everyone except its author — a pure ordinal check, no
        // CanAsync, no AccessAudit row. A non-author (including a GlobalAdmin)
        // is denied. The Web layer maps both "missing" and "not visible" to a
        // 404 (the non-leaky pin, the AnnouncementService.GetAsync shape).
        if (ev.IsDraft)
        {
            if (string.IsNullOrEmpty(actorId) || !string.Equals(ev.AuthorId, actorId, StringComparison.Ordinal))
                throw new KeyNotFoundException($"Event '{eventId}' was not found.");
            return ev;
        }

        // C3 — one decision row from this single call; C6 — one matching pass.
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 GetAsync precedent).
        var decision = await _authorization
            .CanAsync(actorId, AccessAction.Read, new EventToAuditableResource(ev))
            .ConfigureAwait(false);

        if (!decision.Allowed)
            throw new UnauthorizedAccessException($"Actor may not read event '{eventId}'.");

        return ev;
    }

    /// <summary>
    /// The RSVP **list** for an event (ADR 0054 §3.2). The <see cref="IEventService"/>
    /// seam carries <em>no</em> actor — the **owner-only** gate is the Web
    /// <c>EventController</c>'s job (U05): the controller verifies the current
    /// user is the event's author (the <c>Owner</c> branch) <em>before</em>
    /// calling, and this method simply returns the event's RSVPs once the event
    /// is confirmed present. A missing id is <see cref="KeyNotFoundException"/>
    /// (the Web layer's 404); a deleted event is likewise a 404 (the non-leaky
    /// pin). No authorization decision and no audit row here — the owner
    /// standing is the sole decision, enforced upstream of this seam.
    /// </summary>
    public async Task<IReadOnlyList<EventRsvp>> GetRsvpsAsync(string eventId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");

        await using var session = _store.QuerySession();
        var ev = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (ev is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");
        if (ev.IsDeleted)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        return await session.Query<EventRsvp>()
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.At)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The actor's **own** RSVP for an event (ADR 0054 §3.2) — the
    /// last-write-wins read: <c>null</c> when the actor has not RSVPed. The
    /// event's <see cref="IAuthorizationService.CanAsync(string, AccessAction, IAuditableResource)"/>
    /// (Read) decision gates the read (a resident who may not read the event
    /// may not read their RSVP of it either — the owner branch of
    /// <see cref="Decide"/> covers the author); the RSVP itself carries no
    /// audience (it inherits the event's decision, C-M3·1).
    /// </summary>
    public async Task<EventRsvp?> GetMyRsvpAsync(string eventId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An actor is required to read their RSVP.");

        await using var session = _store.QuerySession();
        var ev = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (ev is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");
        if (ev.IsDeleted)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        if (ev.IsDraft)
        {
            // ADR 0037 — draft gate (author-only, no audit row): a draft event's
            // RSVP is invisible to everyone except its author — a pure ordinal
            // check (the GetAsync draft shape). A non-author is denied (404).
            if (!string.Equals(ev.AuthorId, actorId, StringComparison.Ordinal))
                throw new KeyNotFoundException($"Event '{eventId}' was not found.");
        }
        else
        {
            // C3 — one decision row from this single call; C6 — one matching pass.
            // Standalone form (no IDocumentSession overload): plain read, no
            // in-flight caller transaction (the M2 GetAsync precedent).
            var decision = await _authorization
                .CanAsync(actorId, AccessAction.Read, new EventToAuditableResource(ev))
                .ConfigureAwait(false);
            if (!decision.Allowed)
                throw new UnauthorizedAccessException($"Actor may not read event '{eventId}'.");
        }

        return await session.Query<EventRsvp>()
            .Where(r => r.EventId == eventId && r.UserId == actorId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    // ─── Standing-matrix gate helpers (ADR 0054 §3.4 — pure, no store) ───
    //
    // The C3 server-side re-check pattern (the PageService.CheckCreateStanding /
    // CheckEditStanding shape; the AnnouncementService.CreateAsync C3 pattern): a
    // Web [Authorize(Roles=…)] is a convenience pre-gate, not the source of truth.
    // Each helper is a **pure** role-claim check (no DB access), so it is directly
    // testable and callable from any of U04's write lanes. A null event is a
    // KeyNotFoundException (the Web layer's 404); a denied actor is an
    // UnauthorizedAccessException (the Web layer's 403). The existing role claim
    // (Roles.GlobalAdmin) is consulted — **no** new AccessAction, no new AccessVia,
    // no branch in the frozen IAuthorizationService (ADR 0006 §A; ADR 0054 §3.4).

    /// <summary>
    /// The **create** standing (ADR 0054 §3.4): <b>any signed-in resident</b> may
    /// create an event (they become the author — the <c>Owner</c> branch). The
    /// "signed-in" constraint is the <c>actorId</c> being non-empty (the Web
    /// <c>[Authorize]</c> enforces the sign-in; this re-checks it at the Core
    /// layer — a null/empty actor is a denial, the Web layer's 403). A non-null
    /// <paramref name="event"/> is required (the U04 write lane passes the
    /// <c>CreateEventRequest</c>-derived document; a null is a data bug — the
    /// KeyNotFoundException the Web maps to a 404).
    /// </summary>
    public static void CheckCreateStanding(string actorId, IReadOnlySet<string> actorRoles, Event? @event)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (@event is null)
            throw new KeyNotFoundException("An event is required for the create standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to create an event.");
    }

    /// <summary>
    /// The **edit** standing (ADR 0054 §3.4, the ADR 0014 / 0016 / 0017
    /// precedent): the event's <see cref="Event.AuthorId"/> (the
    /// <c>Owner</c> branch) <b>or</b> a <see cref="Roles.GlobalAdmin"/>
    /// (<c>Admin</c> override). Evaluated against the <em>stored</em> event
    /// (loaded before the gate, so its <c>AuthorId</c> is available — the
    /// <see cref="Announcements.AnnouncementService"/> edit-gate shape). A null
    /// event is a <see cref="KeyNotFoundException"/> (the Web layer's 404); a
    /// denied actor is an <see cref="UnauthorizedAccessException"/> (the Web
    /// layer's 403). A <see cref="Roles.GlobalAdmin"/> is allowed to edit any
    /// event (the ADR 0017 override shape — contrast ADR 0037's publish lane,
    /// where a GlobalAdmin is <em>denied</em>).
    /// </summary>
    public static void CheckEditStanding(string actorId, IReadOnlySet<string> actorRoles, Event? @event)
    {
        ArgumentNullException.ThrowIfNull(actorRoles);
        if (@event is null)
            throw new KeyNotFoundException("An event is required for the edit standing check.");
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to edit an event.");

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return;
        if (string.Equals(@event.AuthorId, actorId, StringComparison.Ordinal))
            return;

        throw new UnauthorizedAccessException(
            "Only the author or a GlobalAdmin may edit an event.");
    }

    // --- Write lanes (U04) — standing re-checked server-side (§3.4, C3) --------

    /// <inheritdoc cref="IEventService.CreateAsync"/> — **U04** implements.
    public Task<Event> CreateAsync(string actorId, CreateEventRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the create lane (U04) has not landed yet.");

    /// <inheritdoc cref="IEventService.UpdateAsync"/> — **U04** implements.
    public Task<Event> UpdateAsync(string eventId, string actorId, UpdateEventRequest request, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the edit lane (U04) has not landed yet.");

    /// <inheritdoc cref="IEventService.PublishAsync"/> — **U04** implements.
    public Task<Event> PublishAsync(string eventId, string actorId, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the publish lane (U04) has not landed yet.");

    /// <inheritdoc cref="IEventService.DeleteAsync"/> — **U04** implements.
    public Task DeleteAsync(string eventId, string actorId, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the soft-delete lane (U04) has not landed yet.");

    /// <inheritdoc cref="IEventService.RsvpAsync"/> — **U04** implements.
    public Task<EventRsvp> RsvpAsync(string eventId, string actorId, RsvpStatus status, CancellationToken ct = default)
        => throw new NotImplementedException("M4 U01 skeleton — the RSVP lane (U04) has not landed yet.");
}
