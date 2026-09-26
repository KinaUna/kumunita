using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
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

    // EV-CAL (ADR 0063 D2) — the calendar window's result-count backstop
    // (re-purposing the PageSize = 30 precedent as a window bound; the 30-day
    // *span* is the controller's policy, not the service's).
    private const int WindowCap = 30;

    // EV-MINE (ADR 0065) — the "your upcoming events" section's result-count
    // backstop: the per-actor RSVP/authorship set is small at one-neighborhood
    // scale, and the section does not page — the cap guards against a
    // misconfigured or abusive RSVP history, it is not a page size.
    private const int MineCap = 50;

    private readonly IDocumentStore _store;
    private readonly IAuthorizationService _authorization;
    private readonly IUserInfoService _userInfo;

    // M6 (ADR 0076, plan U04) — the M6 notification emitter seam (F3 event-rsvp).
    // **Optional** (nullable default) so the existing test call sites that
    // construct `EventService` positionally (store, authz, userInfo) keep
    // compiling unchanged — the same CS1736 shape the TG lane used on
    // `PostService`. The DI registration passes the live
    // `Notifications.NotificationService`; the frozen service's `EmitAsync`
    // surface is untouched (unit-series rule 4).
    private readonly NotificationService? _notifications;

    public EventService(IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        NotificationService? notifications = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _notifications = notifications;
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
    public async Task<EventPage> ListUpcomingAsync(string? componentId, string actorId, int page, CancellationToken ct = default)
    {
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        IQueryable<Event> q = session.Query<Event>()
            .Where(e => !e.IsDeleted && !e.IsDraft && e.GroupId == string.Empty); // GE·2 (ADR 0089) — a group-channel event (non-empty GroupId) never reaches the community feed.
        if (componentId is not null)
            q = q.Where(e => e.ComponentId == componentId);
        var candidates = await q.OrderBy(e => e.Start).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return runs **before** any
        // decision (no audit row) and reports no further page (ADR 0090 D1).
        if (candidates.Count == 0)
            return new EventPage(Array.Empty<Event>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == PageSize;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "event"), from that single call (the PostService shape).
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the M2 ListAsync precedent), so
        // the standalone method's own commit is the correct C3 lane.
        var visibleSet = await _authorization
            .CanSeeAsync(actorId, AccessAction.Read, candidates.Select(e => new EventToAuditableResource(e)))
            .ConfigureAwait(false);

        var visibleIds = new HashSet<string>(visibleSet.Visible.Select(v => v.Id));
        return new EventPage(candidates.Where(e => visibleIds.Contains(e.Id)).ToList(), hasMore);
    }

    /// <summary>
    /// The <c>EV-CAL</c> calendar window (ADR 0063 D2) — <see
    /// cref="ListUpcomingAsync"/> restricted to the window predicate
    /// <c>Start &gt;= windowStartUtc &amp;&amp; Start &lt; windowEndUtc</c>
    /// (an event is in the window on the day it <b>starts</b>; the multi-day
    /// chip repeat is a display concern, U06). Mirrors that method's body
    /// verbatim: same candidate filter (<c>!IsDeleted &amp;&amp; !IsDraft</c>,
    /// optional <c>ComponentId</c> filter — C-M3·2, a filter never a gate,
    /// C-EV·3), the same single <see cref="IAuthorizationService.CanSeeAsync(string, AccessAction, System.Collections.Generic.IEnumerable{IAuditableResource})"/>
    /// standalone gate (C6, one matching pass; C-EV·2, one aggregate
    /// <see cref="AccessAudit"/> row <c>TargetKind = "event"</c> via the
    /// <see cref="EventToAuditableResource"/>), then the <c>visibleIds</c>
    /// filter. <b>C-EV·1</b>: shows exactly what <see
    /// cref="ListUpcomingAsync"/> would for this window. The result is capped
    /// at <see cref="WindowCap"/> (a backstop, not the 30-day policy — the span
    /// lives in the controller, U04).
    /// </summary>
    public async Task<IReadOnlyList<Event>> ListInRangeAsync(
        DateTimeOffset windowStartUtc,
        DateTimeOffset windowEndUtc,
        string? componentId,
        string actorId,
        CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        IQueryable<Event> q = session.Query<Event>()
            .Where(e => !e.IsDeleted && !e.IsDraft && e.GroupId == string.Empty) // GE·2 (ADR 0089) — a group-channel event (non-empty GroupId) never reaches the community calendar window.
            .Where(e => e.Start >= windowStartUtc && e.Start < windowEndUtc);
        if (componentId is not null)
            q = q.Where(e => e.ComponentId == componentId);
        var candidates = await q.OrderBy(e => e.Start).Take(WindowCap).ToListAsync(ct).ConfigureAwait(false);

        if (candidates.Count == 0)
            return Array.Empty<Event>();

        // C6 — one shared matching pass; C-EV·2 — one aggregate audit row
        // (TargetKind "event"), from that single call (the ListUpcomingAsync shape).
        // Standalone form (no IDocumentSession overload): this is a plain read
        // with no in-flight caller transaction (the ListUpcomingAsync precedent).
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

    /// <summary>
    /// The actor's **own upcoming events** (ADR 0065, the <c>EV-MINE</c> lane) —
    /// the <c>/events</c> feed's "your events" section. The candidate set is
    /// the **union** of the actor's RSVP rows (any
    /// <see cref="RsvpStatus"/> — a <c>No</c> / <c>Maybe</c> RSVP is still a
    /// sign-up, the row exists) and the events they authored, restricted to
    /// **upcoming** (<c>Start &gt; now</c>) and **live** (<c>!IsDeleted</c> —
    /// the ADR 0024 read-lane shape). **Drafts are included**: ADR 0037's
    /// author-only draft gate makes the union inherently non-leaky — every
    /// result row is authored by the actor or has an
    /// <see cref="EventRsvp"/> row keyed to them (an RSVP row can only exist
    /// on an event the actor may already read — <see cref="RsvpAsync"/>
    /// verifies standing first), so no other actor can ever see an event
    /// through this seam. <b>No <c>AccessAudit</c> row</b> (the
    /// <see cref="GetMyRsvpAsync"/> posture): the row's own write lane already
    /// committed its decision; no <see cref="IAuthorizationService"/> call
    /// here. Ordered by <see cref="Event.Start"/> ascending, capped at
    /// <see cref="MineCap"/> (a backstop, not a page).
    /// </summary>
    public async Task<IReadOnlyList<Event>> ListMineAsync(string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An actor is required to read their events.");

        await using var session = _store.QuerySession();
        var now = DateTimeOffset.UtcNow;

        var mineIds = await (
            from r in session.Query<EventRsvp>()
            where r.UserId == actorId
            select r.EventId
        ).Distinct().ToListAsync(ct).ConfigureAwait(false);

        var authoredIds = await (
            from e in session.Query<Event>()
            where e.AuthorId == actorId
            select e.Id
        ).ToListAsync(ct).ConfigureAwait(false);

        if (mineIds.Count == 0 && authoredIds.Count == 0)
            return Array.Empty<Event>();

        var idSet = new HashSet<string>(mineIds);
        foreach (var id in authoredIds)
            idSet.Add(id);

        var nowLocal = now;
        return await session.Query<Event>()
            .Where(e => !e.IsDeleted && e.Start > nowLocal && e.GroupId == string.Empty && idSet.Contains(e.Id)) // GE·2 (ADR 0089) — ListMineAsync stays community-scoped (the ADR's named deferral); a group event is reached via its group page.
            .OrderBy(e => e.Start)
            .Take(MineCap)
            .ToListAsync(ct)
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

    /// <summary>
    /// Maps the branch the actor qualified under to the <see cref="AccessVia"/>
    /// audit tag (ADR 0054 §3.4): the event's <see cref="Event.AuthorId"/>
    /// → <see cref="AccessVia.Owner"/>; a non-author actor (only reachable when
    /// a <see cref="Roles.GlobalAdmin"/> took the ADR 0017 override branch)
    /// → <see cref="AccessVia.Admin"/>.
    /// </summary>
    private static AccessVia AuditViaFor(string actorId, string authorId)
        => string.Equals(authorId, actorId, StringComparison.Ordinal)
            ? AccessVia.Owner
            : AccessVia.Admin;

    /// <summary>
    /// The **translation** display pin (ADR 0059, the
    /// <see cref="Posts.PostService.CanAddTranslation"/> shape): <c>true</c> when
    /// <paramref name="actorId"/> may add / edit / remove a translation of the
    /// event authored by <paramref name="eventAuthorId"/>. A <b>display</b> pin,
    /// not a gate — the real deny is the
    /// <see cref="AddEventTranslationAsync"/> / <see cref="UpdateEventTranslationAsync"/>
    /// / <see cref="RemoveEventTranslationAsync"/> standing check, which re-runs the
    /// same rule server-side. Delegates to the same
    /// <see cref="ResolveTranslationStanding"/> the write lanes use, so the display
    /// and the three gates can never drift apart.
    /// </summary>
    public static bool CanAddTranslation(string eventAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => ResolveTranslationStanding(eventAuthorId, actorId, actorRoles) is not null;

    /// <summary>
    /// The ADR 0059 translation standing resolver (shared by
    /// <see cref="AddEventTranslationAsync"/> / <see cref="UpdateEventTranslationAsync"/>
    /// / <see cref="RemoveEventTranslationAsync"/> /
    /// <see cref="CanAddTranslation"/>). Returns the <see cref="AccessVia"/> the
    /// actor qualifies under, or <c>null</c> to deny. Precedence (most specific
    /// standing first, so the audit row records the narrowest right that applied):
    /// the event's **author** (<see cref="AccessVia.Owner"/>); a
    /// <see cref="Roles.Translator"/> (ADR 0021, <see cref="AccessVia.Admin"/>);
    /// or a <see cref="Roles.GlobalAdmin"/> (<see cref="AccessVia.Admin"/>).
    /// Events have **no** component-moderator standing (ADR 0054 §5 — the M4
    /// community lane has no component-moderator branch, ADR 0007's group-lane
    /// precedent).
    /// </summary>
    private static AccessVia? ResolveTranslationStanding(string eventAuthorId, string actorId, IReadOnlySet<string> actorRoles)
    {
        if (string.Equals(eventAuthorId, actorId, StringComparison.Ordinal))
            return AccessVia.Owner;

        if (actorRoles.Contains(Roles.Translator))
            return AccessVia.Admin;

        if (actorRoles.Contains(Roles.GlobalAdmin))
            return AccessVia.Admin;

        return null;
    }

    // ─── Write lanes (U04) — standing re-checked server-side (§3.4, C3) ───
    //
    // **Seam shape (ADR 0054 §4, the <see cref="AnnouncementService"/> /
    // <see cref="PageService"/> precedent):** the <see cref="IEventService"/>
    // write lanes carry <c>actorId</c> + the principal's real role set
    // (<c>actorRoles</c>, the Web layer's <c>RoleSet(User)</c>) but open their
    // **own** write session (no caller <c>IDocumentSession</c> — the M4 service
    // composes its own C3 session, the standalone
    // <see cref="AuthorizationService.CanAsync"/> shape). Two consequences:
    //   1. Each lane opens its **own** write session and stores the domain write
    //      + the <see cref="AccessAudit"/> row in that one session, committing
    //      atomically (invariant C3 — the write and the audit row commit or roll
    //      back together, never un-audited).
    //   2. Standing is enforced server-side via the **same** pure helper the
    //      U03 read/standing tests pin (<see cref="CheckCreateStanding"/> /
    //      <see cref="CheckEditStanding"/>) — the C3 single-source pin (no second
    //      copy of the matrix). The **edit/delete** lanes pass the actor's real
    //      role set, so **both** the author (Owner) and the ADR 0017
    //      GlobalAdmin-override (Admin) branches are exercised **in this
    //      service** — the <see cref="AnnouncementService"/> /
    //      <see cref="PageService"/> precedent (the Web hands in real roles; Core
    //      enforces the matrix), as ADR 0054 §3.4 commits.
    //      The **create** lane (any resident) and the **publish** lane
    //      (author-only, ADR 0037) consult no roles — they pass
    //      <see cref="StaticEmptyRoles"/> (a non-null sentinel).
    //
    // **Audit-row shape** (C3, §3.4): <c>TargetKind = "event"</c> (the exact
    // string — the <see cref="EventToAuditableResource"/> discriminator),
    // <c>Action</c> = <c>event.create</c> / <c>event.update</c> /
    // <c>event.publish</c> / <c>event.delete</c> / <c>eventtranslation.add</c> /
    // <c>eventtranslation.update</c> / <c>eventtranslation.remove</c> (ADR 0059),
    // <c>Via</c> =
    // <see cref="AccessVia.Owner"/> (create, publish) or
    // <see cref="AccessVia.Admin"/> (edit / delete by a non-author GlobalAdmin —
    // the <see cref="AuditViaFor"/> derivation; and the ADR 0059 translation
    // lanes — author → Owner, Translator / GlobalAdmin → Admin, the
    // <see cref="ResolveTranslationStanding"/> derivation), <c>Outcome</c> =
    // <see cref="AccessOutcome.Allow"/>. **No**
    // <c>event.rsvp</c> action: <see cref="RsvpAsync"/> stores **no** audit row
    // (a routine resident action, not an access decision — the
    // <see cref="M4_RsvpWritesNoAccessAuditRow"/> pin).
    //
    // **Media ids** (RC R·3/R·7 ADR 0025 + ATT ADR 0034): <see cref="Event.ImageIds"/>
    // / <see cref="Event.AttachmentIds"/> are written server-side — the Web
    // layer parses the body's <c>/content-image/{id}</c> / <c>/attachment/{id}</c>
    // links (the <c>ContentImageIds.ExtractContentImageIds</c> /
    // <c>AttachmentIds.ExtractAttachmentIds</c> idiom) and passes the ids in the
    // request (the client never sends a *form field* — a form field would be
    // spoofable); Core null-coalesces <c>null</c> → the POCO's non-null empty
    // list (<c>?? []</c>, the <see cref="Announcements.AnnouncementService"/>
    // edit-lane shape). The <c>ImageIds</c> / <c>AttachmentIds</c> fields were
    // added to <see cref="CreateEventRequest"/> / <see cref="UpdateEventRequest"/>
    // in this unit (the <see cref="Posts.PostDraft"/> / <see cref="Announcement"/>
    // precedent) — see the U04 handoff note.

    /// <inheritdoc cref="IEventService.CreateAsync"/>
    /// <summary>
    /// Create an event (ADR 0054 §3.4 / §3.5): the author's choices are written
    /// **verbatim** (ADR 0001-B — <see cref="Event.Audience"/> is copied as-is,
    /// never mutated), the <c>IsDraft</c> flag is the ADR 0037 pin (a new event
    /// is a draft), and the author becomes the standing owner
    /// (<see cref="Event.AuthorId"/> = <paramref name="actorId"/>). Standing
    /// (server-side, C3): **any signed-in resident** — the
    /// <see cref="CheckCreateStanding"/> helper (the actorId-expressible branch;
    /// a null/empty actor is a 403). One <see cref="AccessAudit"/> row
    /// (<c>event.create</c>, <c>TargetKind = "event"</c>, <c>Via Owner</c>) is
    /// stored in the same session (C3) and commits atomically with the write.
    /// </summary>
    public async Task<Event> CreateAsync(string actorId, CreateEventRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to create an event.");

        var now = DateTimeOffset.UtcNow;
        var @event = new Event
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = request.Title,
            Body = request.Body,
            ComponentId = request.ComponentId,
            AuthorId = actorId,                       // ADR 0054 §3.4 — the author becomes the standing owner.
            Start = request.Start,
            End = request.End,
            Location = request.Location,
            Capacity = request.Capacity,
            Color = request.Color,                    // display metadata (the Location shape) — written verbatim.
            Audience = request.Audience,              // ADR 0001-B — written verbatim; never mutated here.
            ReminderEnabled = request.ReminderEnabled,
            IsDraft = request.IsDraft,                // ADR 0037 — a newly created event is a draft by default.
            LanguageCode = request.LanguageCode,      // ADR 0018 — materialized below (instance default floor).
            TagIds = request.TagIds ?? [],            // ADR 0044 — null-coalesce to the POCO's non-null empty list.
            ImageIds = request.ImageIds ?? [],        // RC ADR 0025 — server-side; the client never sends a form field.
            AttachmentIds = request.AttachmentIds ?? [], // ATT ADR 0034 — server-side; separate from ImageIds.
            Created = now
        };

        // ADR 0018 — a null/empty authored code is materialized from the
        // instance default (the <see cref="LocaleSettings.DefaultLanguageCode"/>
        // singleton, loaded in the write session) with <c>en</c> the floor, so
        // no stored row is left empty (the <see cref="Announcements.AnnouncementService"/>
        // ResolveLanguageCodeAsync shape).
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        @event.LanguageCode = await ResolveLanguageCodeAsync(@event.LanguageCode, session, ct).ConfigureAwait(false);

        // Standing re-check (server-side, C3 single-source — the Web [Authorize]
        // is a convenience pre-gate only): any signed-in resident may create.
        // The helper throws before anything is stored.
        CheckCreateStanding(actorId, StaticEmptyRoles, @event);

        session.Store(@event);
        StoreAuditRow(session, actorId, "event.create", @event.Id, AccessVia.Owner);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return @event;
    }

    /// <inheritdoc cref="IEventService.UpdateAsync"/>
    /// <summary>
    /// Edit an event (ADR 0054 §3.4, the ADR 0014 / 0016 / 0017 precedent): the
    /// author's choices are written verbatim (ADR 0001-B), <see
    /// cref="Event.AuthorId"/> / <see cref="Event.Created"/> are preserved
    /// untouched, and <see cref="Event.Modified"/> is stamped **only on a real
    /// change** (a no-op re-save does not bump the stamp — the
    /// <see cref="Announcements.AnnouncementService.UpdateAsync"/> shape).
    /// Standing (server-side, C3): **author ∪ GlobalAdmin** — the <see
    /// cref="CheckEditStanding"/> helper's Owner branch and the ADR 0017
    /// GlobalAdmin-override branch are both exercised here (the <paramref name="actorRoles"/>
    /// carries the principal's real role set from the Web layer, matching the
    /// <see cref="Announcements.AnnouncementService"/> / <see
    /// cref="Pages.PageService"/> precedent). A missing id is <see
    /// cref="KeyNotFoundException"/> (404); a non-authorized actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>event.update</c>, <c>TargetKind =
    /// "event"</c>, <c>Via Owner</c>) commits atomically with the write (C3).
    /// </summary>
    public async Task<Event> UpdateAsync(string eventId, string actorId, IReadOnlySet<string> actorRoles, UpdateEventRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to edit an event.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to edit.");

        // Standing re-check (server-side, C3 single-source) against the
        // **stored** event (loaded first, so its AuthorId is available — the
        // AnnouncementService edit-gate shape): author (Owner branch) OR
        // GlobalAdmin (ADR 0017 override). The actorRoles carries the principal's
        // real role set (the Web layer's RoleSet(User), the
        // AnnouncementService / PageService precedent).
        CheckEditStanding(actorId, actorRoles, existing);

        // ADR 0018 — resolve the authored-in tag on **both** sides before
        // comparing (the AnnouncementService.UpdateAsync shape): a no-op
        // re-save that leaves the picker at the instance default must compare
        // as "unchanged" (resolving "" → the default on the stored side and the
        // default → the default on the updated side makes them equal), so it
        // does not falsely stamp Modified.
        var existingLanguageCode = await ResolveLanguageCodeAsync(existing.LanguageCode, session, ct).ConfigureAwait(false);
        var updatedLanguageCode = await ResolveLanguageCodeAsync(request.LanguageCode, session, ct).ConfigureAwait(false);

        // A "real change" is any of the editable fields differing from the
        // stored row (the AnnouncementService.UpdateAsync `changed` shape — a
        // no-op re-save leaves the stamp untouched).
        var changed = existing.Title != request.Title
            || existing.Body != request.Body
            || !string.Equals(existing.ComponentId, request.ComponentId, StringComparison.Ordinal)
            || existing.Start != request.Start
            || existing.End != request.End
            || !string.Equals(existing.Location, request.Location, StringComparison.Ordinal)
            || existing.Capacity != request.Capacity
            || !string.Equals(existing.Color, request.Color, StringComparison.Ordinal)
            || !AudiencesEqual(existing.Audience, request.Audience)
            || existing.ReminderEnabled != request.ReminderEnabled
            || existingLanguageCode != updatedLanguageCode
            || !ListsEqual(existing.TagIds, request.TagIds ?? [])
            || !ListsEqual(existing.ImageIds, request.ImageIds ?? [])
            || !ListsEqual(existing.AttachmentIds, request.AttachmentIds ?? []);

        // Apply the author's choices verbatim (ADR 0001-B). AuthorId / Created /
        // IsDraft / IsDeleted are **deliberately not** reassigned here — the
        // author of record is whoever created it, the draft/delete state is
        // owned by the publish (ADR 0037) / delete (ADR 0024) lanes.
        existing.Title = request.Title;
        existing.Body = request.Body;
        existing.ComponentId = request.ComponentId;
        existing.Start = request.Start;
        existing.End = request.End;
        existing.Location = request.Location;
        existing.Color = request.Color;                 // display metadata (the Location shape) — written verbatim.
        existing.Capacity = request.Capacity;
        existing.Audience = request.Audience;           // ADR 0001-B — written verbatim; never mutated.
        existing.ReminderEnabled = request.ReminderEnabled;
        existing.LanguageCode = updatedLanguageCode;    // ADR 0018
        existing.TagIds = request.TagIds ?? [];         // ADR 0044 — null-coalesce to the POCO's non-null empty list.
        existing.ImageIds = request.ImageIds ?? [];     // RC ADR 0025 — server-side; the re-parse (replace-style) is authoritative.
        existing.AttachmentIds = request.AttachmentIds ?? []; // ATT ADR 0034 — server-side; separate from ImageIds.
        if (changed)
            existing.Modified = DateTimeOffset.UtcNow;

        session.Store(existing);
        // ADR 0054 §3.4 — the audit row tags the branch the actor qualified
        // under: the author → Owner, a non-author GlobalAdmin override → Admin.
        StoreAuditRow(session, actorId, "event.update", existing.Id, AuditViaFor(actorId, existing.AuthorId));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    /// <inheritdoc cref="IEventService.PublishAsync"/>
    /// <summary>
    /// Publish a draft event (ADR 0037 pin): flips <see cref="Event.IsDraft"/>
    /// to <c>false</c> so the event becomes visible under its
    /// <see cref="Event.Audience"/> split. **Author-only** — the sole decision
    /// is <c>existing.AuthorId == actorId</c> (ordinal); a non-author is denied
    /// (<see cref="UnauthorizedAccessException"/>, the Web 403) **even at
    /// GlobalAdmin** (ADR 0037's author-only pin: publishing is the author's
    /// choice, not an admin's lever — contrast ADR 0017's edit lane). A missing
    /// id is <see cref="KeyNotFoundException"/> (404). **Idempotent**: a second
    /// publish on an already-live event is a no-op — it does not stamp
    /// <see cref="Event.Modified"/> when <c>IsDraft</c> was already <c>false</c>.
    /// One <see cref="AccessAudit"/> row (<c>event.publish</c>,
    /// <c>TargetKind = "event"</c>, <c>Via Owner</c>) commits atomically
    /// (C3).
    /// </summary>
    public async Task<Event> PublishAsync(string eventId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to publish an event.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to publish.");

        // ADR 0037 — author-only (GlobalAdmin explicitly denied). A pure
        // ordinal check; the Web layer maps the exception to a 403.
        if (!string.Equals(existing.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author may publish this event.");

        // Idempotent (the no-op-re-save pin): a second publish does not stamp
        // Modified when the event is already live.
        if (existing.IsDraft)
        {
            existing.IsDraft = false;
            existing.Modified = DateTimeOffset.UtcNow;
        }

        session.Store(existing);
        StoreAuditRow(session, actorId, "event.publish", existing.Id, AccessVia.Owner);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    /// <inheritdoc cref="IEventService.DeleteAsync"/>
    /// <summary>
    /// **Soft-delete** an event (ADR 0024, the author-lane shape): sets
    /// <see cref="Event.IsDeleted"/> to <c>true</c>; the record is kept (never
    /// hard-deleted) and the read lanes (<see cref="ListUpcomingAsync"/> /
    /// <see cref="GetAsync"/>) filter it out (U03's non-leaky pin — a deleted
    /// event is a 404, not a 403). Standing (server-side, C3): **author ∪
    /// GlobalAdmin** (ADR 0054 §3.4, the ADR 0017 override branch) — the
    /// <paramref name="actorRoles"/> carries the principal's real role set from the Web
    /// layer, so both the Owner and Admin branches of <see
    /// cref="CheckEditStanding"/> are exercised here (the
    /// <see cref="Announcements.AnnouncementService"/> / <see
    /// cref="Pages.PageService"/> precedent). A missing id is <see
    /// cref="KeyNotFoundException"/> (404); a non-authorized actor is <see
    /// cref="UnauthorizedAccessException"/> (403). One <see
    /// cref="AccessAudit"/> row (<c>event.delete</c>, <c>TargetKind =
    /// "event"</c>, <c>Via Owner</c>) commits atomically (C3). **Idempotent**:
    /// deleting an already-deleted event is a no-op (it does not re-stamp
    /// <see cref="Event.Modified"/>).
    /// </summary>
    public async Task DeleteAsync(string eventId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to delete an event.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to delete.");

        // Standing re-check (server-side, C3 single-source): author (Owner
        // branch) OR GlobalAdmin (ADR 0017 override). The actorRoles carries the
        // principal's real role set (the Web layer's RoleSet(User), the
        // AnnouncementService / PageService precedent).
        CheckEditStanding(actorId, actorRoles, existing);

        if (!existing.IsDeleted)
        {
            existing.IsDeleted = true;                 // ADR 0024 — the soft-delete flag (read lanes filter it).
            existing.Modified = DateTimeOffset.UtcNow;
        }

        session.Store(existing);
        // ADR 0054 §3.4 — tag the branch the actor qualified under:
        // author → Owner, non-author GlobalAdmin override → Admin.
        StoreAuditRow(session, actorId, "event.delete", existing.Id, AuditViaFor(actorId, existing.AuthorId));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IEventService.RsvpAsync"/>
    /// <summary>
    /// RSVP to an event (ADR 0054 §3.2) — the **last-write-wins** concurrency
    /// exception, keyed per <c>(EventId, UserId)</c>: upserts the actor's single
    /// RSVP row with the latest <see cref="RsvpStatus"/>. A resident's latest
    /// status is simply the truth; a conflicting write is a no-op or
    /// self-converging (the <see cref="M4DocTypes"/> unique
    /// <c>(EventId, UserId)</c> index is the DB-enforced business key).
    /// <para>
    /// Standing (server-side, C3): **any signed-in resident** who may RSVP —
    /// a null/empty actor is a <see cref="UnauthorizedAccessException"/> (403).
    /// The event must be present and **not deleted** (a missing/deleted id is a
    /// <see cref="KeyNotFoundException"/> — the non-leaky pin, the
    /// <see cref="GetRsvpsAsync"/> shape). **No <see cref="AccessAudit"/> row**
    /// is stored (the RSVP is a routine resident action, not an access decision
    /// — the same posture as a profile edit; the <see
    /// cref="M4_RsvpWritesNoAccessAuditRow"/> pin). This lane therefore does
    /// **not** route the standing through <see cref="IAuthorizationService"/>
    /// (a <c>CanAsync</c> read-decision would append a read-audit row, violating
    /// the pin) — it enforces signed-in + presence directly.
    /// </para>
    /// </summary>
    public async Task<EventRsvp> RsvpAsync(string eventId, string actorId, RsvpStatus status, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to RSVP.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null || @event.IsDeleted)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to RSVP to.");

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

    // ─── Group events lane (ADR 0089) — the ADR 0013 membership lane applied to
    //     the M4 event surface. The lane owns every access read (ADR 0006-D); the
    //     frozen group seams (CanSeeGroupAsync / CanSeeGroupFeedAsync) are the
    //     sole decision — no audience / moderate / break-glass branch (GE·1/GE·4).
    //     Publish reuses the existing PublishAsync; RSVP reuses RsvpAsync;
    //     translations reuse the ADR 0059 seams (all keyed by EventId, no group
    //     branch). Per-member publish notifications are a named ADR 0089
    //     deferral (M6-scope, like group posts) — NOT emitted here. ───

    /// <inheritdoc cref="IEventService.ListGroupEventsAsync"/>
    /// <summary>
    /// A group's events feed (GE1–GE4 FACES): the candidate set is the group's
    /// published events — <c>Event.GroupId == groupId</c>, <c>!IsDeleted</c>,
    /// <c>!IsDraft</c>, ordered by <see cref="Event.Start"/> ascending (the
    /// event idiom — contrast the group-post feed's <c>Created desc</c>), paged
    /// with the class's existing <see cref="PageSize"/>. Exactly one
    /// <see cref="IAuthorizationService.CanSeeGroupFeedAsync(string, string, int)"/>
    /// (the **standalone** form — a plain read with no in-flight caller
    /// transaction, the <see cref="ListUpcomingAsync"/> precedent) writes the
    /// visit's **single aggregate** <c>AccessAudit</c> row (GE·5: TargetKind
    /// "grouppost", TargetId null, counts). Allow ⇒ the paged candidates (GE1);
    /// Deny ⇒ a <see cref="GroupEventFeedResult"/> with an **empty** visible
    /// list and <see cref="GroupEventFeedResult.HiddenCount"/> = the candidate
    /// count (GE2). **0 candidates ⇒** empty result, no decision, **no** row (the
    /// <see cref="ListUpcomingAsync"/> 0-candidate shape). **No audience
    /// evaluation of any kind** (GE·1/GE·8 — membership is the sole decision).
    /// </summary>
    public async Task<GroupEventFeedResult> ListGroupEventsAsync(string groupId, string actorId, int page, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("A group events feed requires a groupId.", nameof(groupId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;

        await using var session = _store.QuerySession();
        var candidates = await session
            .Query<Event>()
            .Where(e => e.GroupId == groupId && !e.IsDeleted && !e.IsDraft)
            .OrderBy(e => e.Start)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (candidates.Count == 0)
            return new GroupEventFeedResult(Visible: Array.Empty<Event>(), HiddenCount: 0, Page: page, Total: 0, HasMore: false);

        // GE·5 (the C-M3·3 analog) — one standalone whole-channel call over the
        // paged candidate set writes the visit's single aggregate AccessAudit
        // row. Standalone form (the ListUpcomingAsync precedent). The channel is
        // all-or-nothing for a principal, so the paged candidates are returned
        // as-is on Allow.
        var decision = await _authorization
            .CanSeeGroupFeedAsync(actorId, groupId, candidates.Count)
            .ConfigureAwait(false);

        if (decision.Allowed)
            // ADR 0090 D1 / design doc §7.4 — HasMore: candidates.Count == PageSize.
            return new GroupEventFeedResult(Visible: candidates, HiddenCount: 0, Page: page, Total: candidates.Count, HasMore: candidates.Count == PageSize);

        // GE2 — Deny: empty visible list, HiddenCount = the candidate count (the
        // aggregate Deny row **is** the audit evidence — GE·1/GE·5); never an
        // event's fields. HasMore is false on Deny (the design doc §7.4 pin).
        return new GroupEventFeedResult(Visible: Array.Empty<Event>(), HiddenCount: candidates.Count, Page: page, Total: 0, HasMore: false);
    }

    /// <inheritdoc cref="IEventService.GetGroupEventAsync"/>
    /// <summary>
    /// A group event's detail (GE1–GE4, GE7–GE11 FACES): the event is loaded
    /// first (the M3 fail-closed shape: missing ⇒ <c>null</c>, no decision,
    /// **no** row); an event with an **empty** <c>GroupId</c> (not a group
    /// event) or <c>GroupId != groupId</c> (route/lane mismatch) ⇒ <c>null</c>,
    /// no row (fail-closed). Otherwise the **ADR 0037 draft gate** (author-only,
    /// a pure <c>AuthorId == actorId</c> ordinal, **no** audit row) runs
    /// **before** the membership check (a draft is invisible to every member
    /// except its author). For a non-draft, exactly one
    /// <see cref="IAuthorizationService.CanSeeGroupAsync(string, string, string?)"/>
    /// (standalone) — the detail decision row (TargetKind "grouppost",
    /// **TargetId = eventId**, GE·5). Allow ⇒ the event; Deny ⇒ <c>null</c>
    /// (Web 404) — the decision's row **was** written (C3). **No** audience
    /// evaluation (GE·1/GE·8).
    /// </summary>
    public async Task<Event?> GetGroupEventAsync(string groupId, string eventId, string actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new ArgumentException("An event id is required.", nameof(eventId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));

        await using var session = _store.QuerySession();
        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null)
            // Fail-closed: the event does not exist ⇒ no decision, no row.
            return null;

        // Lane fail-closed: an event with an empty GroupId (not a group event)
        // or a route/lane mismatch (GroupId != groupId) is denied with **no**
        // decision and **no** row (the PostService.GetGroupPostAsync shape).
        if (string.IsNullOrEmpty(@event.GroupId) || @event.GroupId != groupId)
            return null;

        if (@event.IsDeleted)
            return null; // a soft-deleted event is a 404 (the M3 non-leaky pin).

        // ADR 0037 — draft gate (author-only, no audit row): a group-lane draft
        // is invisible to every member and to any moderator/admin except its
        // author. The membership decision is NOT consulted — a pure ordinal
        // check, no CanSeeGroupAsync, no AccessAudit row.
        if (@event.IsDraft)
        {
            if (!string.Equals(@event.AuthorId, actorId, StringComparison.Ordinal))
                return null;
            return @event;
        }

        // Exactly one standalone single-target call → the detail decision row
        // (TargetId = eventId, GE·5). No audience evaluation of any kind
        // (GE·1/GE·8).
        var decision = await _authorization.CanSeeGroupAsync(actorId, groupId, eventId).ConfigureAwait(false);

        if (!decision.Allowed)
            return null; // Deny ⇒ null (Web 404 — GE·3/GE·4); the row was written (C3).

        return @event;
    }

    /// <inheritdoc cref="IEventService.CreateGroupEventAsync"/>
    /// <summary>
    /// Create a group event, in the **caller's** in-flight session (invariant
    /// C3). The **create gate is the group-lane decision** (GE·3): one
    /// <see cref="IAuthorizationService.CanSeeGroupAsync(string, string, string?, IDocumentSession)"/>
    /// with <c>targetEventId: null</c>, in the caller's transaction — **deny**:
    /// the row is committed by a <c>SaveChangesAsync()</c> **before**
    /// <see cref="UnauthorizedAccessException"/> throws (the gate row must
    /// survive — GE6 FACES; Web maps it to 404); **allow**: the gate row + the
    /// new event commit in **one** <c>SaveChangesAsync()</c> (atomic with the
    /// write, C3). The gate is the **sole** decision (GE·3): **no**
    /// <c>actorRoles</c> parameter (a non-member GlobalAdmin is **denied**,
    /// GE8 FACES/GE·4), **no** break-glass, **no** membership read here (the
    /// lane owns its reads — ADR 0006-D). The write pins GE·2/GE·8:
    /// <c>GroupId</c> = the lane marker, <c>ComponentId = string.Empty</c>,
    /// <c>Audience = new Audience()</c> (non-null, **empty**),
    /// <c>IsDraft = true</c> (the ADR 0037 pin — a group event is a draft until
    /// its author publishes it, reusing the existing
    /// <see cref="PublishAsync"/>). One <c>event.create</c> audit row
    /// (TargetKind "event", Via Owner, GE·5) + the gate row. One
    /// <c>SaveChangesAsync()</c>.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">The actor (or, under an
    /// in-scope <c>read</c> grant, the owner) is not a member of
    /// <c>draft.GroupId</c> — thrown **after** the gate row is persisted.</exception>
    public async Task<Event> CreateGroupEventAsync(GroupEventDraft draft, string actorId, IDocumentSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("An authoring actor is required.", nameof(actorId));
        if (string.IsNullOrEmpty(draft.GroupId))
            throw new ArgumentException("A group event requires a non-empty GroupId (the group lane).", nameof(draft.GroupId));
        ArgumentNullException.ThrowIfNull(session);

        // GE·3 — the create gate **is** the group-lane decision: one session-variant
        // call with targetEventId: null (⇒ the row's TargetId = the group id, the
        // channel as the gate's target), in the caller's transaction. Deny ⇒ the
        // row is persisted by this SaveChangesAsync **before** the throw (the
        // gate row must survive — GE6 FACES; Web maps the exception to 404).
        // Allow ⇒ the gate row + the new event commit in one SaveChangesAsync (C3).
        var decision = await _authorization
            .CanSeeGroupAsync(actorId, draft.GroupId, null, session)
            .ConfigureAwait(false);

        if (!decision.Allowed)
        {
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
            throw new UnauthorizedAccessException(
                $"You are not a member of the group '{draft.GroupId}'; " +
                "only group members may create events for a group channel.");
        }

        // ADR 0018 — a null/empty authored code is materialized from the
        // instance default (the write session's LocaleSettings, en floor).
        var languageCode = await ResolveLanguageCodeAsync(draft.LanguageCode, session, ct).ConfigureAwait(false);

        var @event = new Event
        {
            Id = Guid.NewGuid().ToString("N"),
            GroupId = draft.GroupId,             // GE·2 — the non-empty group lane.
            ComponentId = string.Empty,          // GE·2 — lane exclusivity (the Post.ComponentId empty-pin shape; Event.ComponentId is string?, so string.Empty not null).
            AuthorId = actorId,                  // ADR 0054 §3.4 — the author becomes the standing owner.
            Title = draft.Title,
            Body = draft.Body,
            Start = draft.StartUtc,
            End = draft.EndUtc,
            Location = draft.Location,
            Capacity = draft.Capacity,
            Color = draft.Color,
            Audience = new Audience(),           // GE·8 — written non-null **empty**; never authored here.
            ReminderEnabled = draft.ReminderEnabled ?? true,
            IsDraft = true,                      // ADR 0037 — a group event is a draft until its author publishes it (reusing PublishAsync).
            LanguageCode = languageCode,         // ADR 0018
            Created = DateTimeOffset.UtcNow
        };

        session.Store(@event);

        // GE·5 — the write lane's own event.create row (TargetKind "event", Via
        // Owner). The gate's membership row was written by the session-variant
        // CanSeeGroupAsync above (TargetKind "grouppost") — two rows on create,
        // mirroring how the community create stores a write row and the
        // group-post create stores a gate row (ADR 0089 Decision).
        StoreAuditRow(session, actorId, "event.create", @event.Id, AccessVia.Owner);

        // One SaveChangesAsync — the C3 same-transaction lane: the gate decision
        // row + the new event commit atomically.
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return @event;
    }

    /// <inheritdoc cref="IEventService.UpdateGroupEventAsync"/>
    /// <summary>
    /// Edit a group event (ADR 0089 GE·4, the ADR 0016 / 0037 group-lane
    /// precedent): **author-only** — the sole decision is
    /// <c>existing.AuthorId == actorId</c> (ordinal); a non-author (even a
    /// GlobalAdmin) is denied (<see cref="UnauthorizedAccessException"/>, the Web
    /// 404). Stamps only the editable surface (<see cref="GroupEventUpdate"/>),
    /// re-stamps <see cref="Event.Modified"/> only on a real change, and leaves
    /// the lane markers (<see cref="Event.GroupId"/> /
    /// <see cref="Event.ComponentId"/> / <see cref="Event.Audience"/> /
    /// <see cref="Event.AuthorId"/> / <see cref="Event.Created"/> /
    /// <see cref="Event.IsDraft"/> / <see cref="Event.IsDeleted"/>) untouched
    /// (the <c>PostService.UpdateGroupPostAsync</c> shape). Runs in the
    /// **caller's** <paramref name="session"/>; stores **no** audit row (the
    /// ADR 0016 author-lane precedent — the author's own lane).
    /// </summary>
    /// <exception cref="KeyNotFoundException">The event id is not found, or it
    /// is not a group event (empty <c>GroupId</c>).</exception>
    /// <exception cref="UnauthorizedAccessException">The actor is not the
    /// author.</exception>
    public async Task<Event> UpdateGroupEventAsync(string eventId, string actorId, GroupEventUpdate update, IDocumentSession session, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId)) throw new KeyNotFoundException("An event id is required.");
        ArgumentNullException.ThrowIfNull(update);
        if (string.IsNullOrEmpty(actorId)) throw new UnauthorizedAccessException("An acting actor is required to edit a group event.");
        ArgumentNullException.ThrowIfNull(session);

        var existing = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found; nothing to edit.");
        if (string.IsNullOrEmpty(existing.GroupId))
            // Not a group event (the lane is fail-closed): a 404, never a
            // cross-lane edit (the PostService.UpdateGroupPostAsync shape).
            throw new KeyNotFoundException($"Event '{eventId}' is not a group event; nothing to edit.");

        // GE·4 — author-only (GlobalAdmin explicitly denied). A pure ordinal
        // check; the Web layer maps the exception to a 404.
        if (!string.Equals(existing.AuthorId, actorId, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Only the author may edit a group event.");

        // ADR 0018 — resolve the authored-in tag on both sides before comparing
        // (the UpdateAsync shape): a no-op re-save leaves the stamp untouched.
        var existingLanguageCode = await ResolveLanguageCodeAsync(existing.LanguageCode, session, ct).ConfigureAwait(false);
        var updatedLanguageCode = await ResolveLanguageCodeAsync(update.LanguageCode, session, ct).ConfigureAwait(false);

        var changed = existing.Title != update.Title
            || existing.Body != update.Body
            || existing.Start != update.StartUtc
            || existing.End != update.EndUtc
            || !string.Equals(existing.Location, update.Location, StringComparison.Ordinal)
            || existing.Capacity != update.Capacity
            || !string.Equals(existing.Color, update.Color, StringComparison.Ordinal)
            || existing.ReminderEnabled != (update.ReminderEnabled ?? existing.ReminderEnabled)
            || existingLanguageCode != updatedLanguageCode;

        // Apply the author's choices (the editable surface only). The lane
        // markers are **deliberately not** reassigned here (GE·8) — the author of
        // record is whoever created it, the draft/delete state is owned by the
        // publish (ADR 0037) / delete (ADR 0024) lanes.
        existing.Title = update.Title;
        existing.Body = update.Body;
        existing.Start = update.StartUtc;
        existing.End = update.EndUtc;
        existing.Location = update.Location;
        existing.Capacity = update.Capacity;
        existing.Color = update.Color;
        if (update.ReminderEnabled is not null)
            existing.ReminderEnabled = update.ReminderEnabled.Value;
        existing.LanguageCode = updatedLanguageCode;
        if (changed)
            existing.Modified = DateTimeOffset.UtcNow;

        session.Store(existing);
        // GE·5 — **no** audit row (the ADR 0016 author-lane precedent; the
        // author's own lane). One SaveChangesAsync (C3).
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    // ─── Translation lanes (ADR 0059 — the "follow-on lane" ADR 0054 deferred) ───
    //
    // Mirrors the PostService / AnnouncementService translation lanes (ADR 0022 /
    // 0029 / 0048) on the **M4 self-composed-session convention**: the write lanes
    // open their **own** write session (no caller IDocumentSession — the M4 service
    // composes its own C3 session), store the domain write + the AccessAudit row in
    // that one session, and commit atomically (invariant C3). Standing is enforced
    // server-side via the same pure helper the U03 read/standing tests pin
    // (ResolveTranslationStanding) — the C3 single-source pin (no second copy of the
    // matrix). Audit-row shape: TargetKind = "event" (the exact string — the
    // EventToAuditableResource discriminator), Action = eventtranslation.add /
    // eventtranslation.update / eventtranslation.remove, Via = Owner (author) or
    // Admin (Translator / GlobalAdmin), Outcome = Allow.

    /// <inheritdoc cref="IEventService.GetEventTranslationsAsync"/>
    /// <summary>
    /// The **read** seam for an event's user-added translations (ADR 0059). Owns its
    /// own <c>QuerySession</c> (C3 read lane); **not** an authorization surface and
    /// writes **no** audit row (C-M3·1 — the translation has no own audience; its
    /// visibility inherits the parent event's single <c>Read</c> decision, which the
    /// caller has already made). The Web reads this only after
    /// <see cref="GetAsync"/> returned the event.
    /// </summary>
    public async Task<IReadOnlyList<EventTranslation>> GetEventTranslationsAsync(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
            throw new ArgumentException("An event id is required.", nameof(eventId));

        await using var session = _store.QuerySession();
        return await session
            .Query<EventTranslation>()
            .Where(t => t.EventId == eventId)
            .OrderBy(t => t.LanguageCode)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    /// <inheritdoc cref="IEventService.AddEventTranslationAsync"/>
    /// <summary>
    /// Add a **user-added translation** of an event in this service's own write
    /// session (invariant C3 — the same-transaction lane; the write + the
    /// <see cref="AccessAudit"/> row commit or roll back atomically).
    /// <para>
    /// <b>Standing (ADR 0059, the approved default):</b> the event's
    /// <b>author</b> (<see cref="AccessVia.Owner"/>); a
    /// <see cref="Identity.Roles.Translator"/> (ADR 0021,
    /// <see cref="AccessVia.Admin"/>); or a <see cref="Identity.Roles.GlobalAdmin"/>
    /// (<see cref="AccessVia.Admin"/>). Events have no component-moderator standing
    /// (ADR 0054 §5 — the M4 community lane has no component-moderator branch). A
    /// denied actor throws <see cref="UnauthorizedAccessException"/> before anything
    /// is stored.
    /// </para>
    /// <paramref name="languageCode"/> is the **target** language (written
    /// verbatim — a blank target is a caller error). One <c>SaveChangesAsync</c>.
    /// </summary>
    public async Task<EventTranslation> AddEventTranslationAsync(
        string eventId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId))
            throw new ArgumentException("An event id is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to add a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to translate.");

        // Standing re-check (server-side, C3 single-source): author (Owner) OR
        // Translator / GlobalAdmin (Admin). Events have no component-moderator
        // standing (ADR 0054 §5).
        var via = ResolveTranslationStanding(@event.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the event's author (or a Translator, or a GlobalAdmin) " +
                "may add a translation of it.");

        var now = DateTimeOffset.UtcNow;
        var translation = new EventTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            EventId = eventId,
            LanguageCode = languageCode,
            Title = string.IsNullOrWhiteSpace(title) ? null : title,
            Body = body,
            AuthorId = actorId,
            Created = now
        };

        session.Store(translation);
        StoreAuditRow(session, actorId, "eventtranslation.add", eventId, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return translation;
    }

    /// <inheritdoc cref="IEventService.UpdateEventTranslationAsync"/>
    /// <summary>
    /// Edit an existing event translation (ADR 0059, the ADR 0048 edit-lane shape):
    /// updates the <see cref="EventTranslation.Title"/> /
    /// <see cref="EventTranslation.Body"/> for the <c>(eventId, languageCode)</c>
    /// pair, re-stamps <see cref="EventTranslation.Created"/> and re-records
    /// <see cref="EventTranslation.AuthorId"/> (the last actor to edit).
    /// <para>
    /// <b>Standing (ADR 0059):</b> the same matrix as
    /// <see cref="AddEventTranslationAsync"/> (author / Translator / GlobalAdmin).
    /// A missing row is a <see cref="KeyNotFoundException"/> (404). One
    /// <c>AccessAudit</c> row (<c>eventtranslation.update</c>,
    /// <c>TargetKind = "event"</c>) commits atomically with the write (C3).
    /// </para>
    /// </summary>
    public async Task<EventTranslation> UpdateEventTranslationAsync(
        string eventId, string languageCode, string? title, string body,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId))
            throw new ArgumentException("An event id is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("A translation requires a non-empty body.", nameof(body));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to edit a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to edit.");

        var via = ResolveTranslationStanding(@event.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the event's author (or a Translator, or a GlobalAdmin) " +
                "may edit a translation of it.");

        var row = await session.Query<EventTranslation>()
            .Where(t => t.EventId == eventId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Event '{eventId}' has no translation for '{languageCode}'; nothing to edit.");

        row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
        row.Body = body;
        row.AuthorId = actorId;
        row.Created = DateTimeOffset.UtcNow;

        session.Store(row);
        StoreAuditRow(session, actorId, "eventtranslation.update", eventId, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return row;
    }

    /// <inheritdoc cref="IEventService.RemoveEventTranslationAsync"/>
    /// <summary>
    /// Remove an event translation (ADR 0059, the ADR 0048 remove-lane shape):
    /// deletes the <c>(eventId, languageCode)</c> row.
    /// <para>
    /// <b>Standing (ADR 0059):</b> the same matrix as
    /// <see cref="AddEventTranslationAsync"/> (author / Translator / GlobalAdmin).
    /// A missing row is a <see cref="KeyNotFoundException"/> (404). One
    /// <c>AccessAudit</c> row (<c>eventtranslation.remove</c>,
    /// <c>TargetKind = "event"</c>) commits atomically with the write (C3).
    /// </para>
    /// </summary>
    public async Task RemoveEventTranslationAsync(
        string eventId, string languageCode,
        string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(eventId))
            throw new ArgumentException("An event id is required.", nameof(eventId));
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("A translation requires a concrete target language code.", nameof(languageCode));
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An acting actor is required to remove a translation.");
        ArgumentNullException.ThrowIfNull(actorRoles);

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var @event = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (@event is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found in the session; nothing to remove.");

        var via = ResolveTranslationStanding(@event.AuthorId, actorId, actorRoles);
        if (via is null)
            throw new UnauthorizedAccessException(
                "Only the event's author (or a Translator, or a GlobalAdmin) " +
                "may remove a translation of it.");

        var row = await session.Query<EventTranslation>()
            .Where(t => t.EventId == eventId && t.LanguageCode == languageCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (row is null)
            throw new KeyNotFoundException($"Event '{eventId}' has no translation for '{languageCode}'; nothing to remove.");

        session.Delete(row);
        StoreAuditRow(session, actorId, "eventtranslation.remove", eventId, via.Value);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ─── U04 private helpers ───────────────────────────────────────────────

    /// <summary>
    /// A non-null, empty role set for the standing helpers'
    /// <c>actorRoles</c> parameter on the lanes that consult no roles
    /// (create — any resident — and publish — author-only, ADR 0037). The
    /// edit / delete lanes receive the principal's real role set from the Web
    /// layer (the <see cref="AnnouncementService"/> / <see cref="PageService"/>
    /// precedent) so the GlobalAdmin override (ADR 0017) is exercised
    /// server-side; this sentinel is a non-null stand-in for the lanes that
    /// never look at roles.
    /// </summary>
    private static readonly IReadOnlySet<string> StaticEmptyRoles = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Appends the single <see cref="AccessAudit"/> row for a write lane
    /// (invariant C3): <c>TargetKind = "event"</c> (the exact string — the
    /// <see cref="EventToAuditableResource"/> discriminator), the given
    /// <c>Action</c>, <c>Via</c>, <c>Outcome = Allow</c>, single-target
    /// (<c>TargetId</c> set; counts null). Stored in the caller's write
    /// session (it commits atomically with the domain write — C3).
    /// </summary>
    private static void StoreAuditRow(IDocumentSession session, string actorId, string action, string targetId, AccessVia via)
    {
        session.Store(new AccessAudit
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,   // the author acts as themself (no delegation in these lanes).
            Action = action,
            TargetKind = "event",             // the exact string (C3 — the adapter's discriminator).
            TargetId = targetId,
            Via = via,
            Outcome = AccessOutcome.Allow
        });
    }

    /// <summary>
    /// ADR 0018 — resolves the authored-in <see cref="Event.LanguageCode"/>:
    /// a non-empty authored code is used verbatim (BCP-47 tag); a null/empty
    /// code is materialized from the instance default (<see
    /// cref="LocaleSettings.DefaultLanguageCode"/>, loaded in the write
    /// session) with <c>en</c> the floor. The result is always a concrete BCP-47
    /// code — no stored row is left empty (the
    /// <see cref="Announcements.AnnouncementService"/> ResolveLanguageCodeAsync
    /// shape).
    /// </summary>
    private async Task<string> ResolveLanguageCodeAsync(string? languageCode, IDocumentSession session, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(languageCode))
            return languageCode;

        var settings = await session.LoadAsync<LocaleSettings>(LocaleSettings.SingletonId, ct).ConfigureAwait(false);
        if (settings is not null && !string.IsNullOrWhiteSpace(settings.DefaultLanguageCode))
            return settings.DefaultLanguageCode;

        return "en";
    }

    /// <summary>
    /// Structural equality for the <see cref="Event.Audience"/> on the change
    /// detection (the <see cref="Audience"/> is a mutable class, not a value
    /// type): two audiences are equal iff both are <c>null</c>, or both are
    /// non-null and their <c>Mode</c> / <c>Community</c> / <c>AllResidents</c>
    /// and their <c>Grants</c> (order-insensitive, by <see
    /// cref="AudienceGrant"/>'s value equality) are equal.
    /// </summary>
    private static bool AudiencesEqual(Audience? a, Audience? b)
    {
        if (a is null || b is null)
            return a is null && b is null;

        if (a.Mode != b.Mode || a.Community != b.Community || a.AllResidents != b.AllResidents)
            return false;

        var aGrants = new HashSet<AudienceGrant>(a.Grants);
        var bGrants = new HashSet<AudienceGrant>(b.Grants);
        return aGrants.SetEquals(bGrants);
    }

    /// <summary>Order-insensitive equality for two <see cref="IReadOnlyList{T}"/>
    /// of value-equality elements (the change-detection comparison for the
    /// <c>TagIds</c> / <c>ImageIds</c> / <c>AttachmentIds</c> lists).</summary>
    private static bool ListsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
        => new HashSet<string>(a, StringComparer.Ordinal).SetEquals(b);
}
