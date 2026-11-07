using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Query;
using Kumunita.Core.Translations;
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
public partial class EventService : IEventService
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
    private readonly INotificationEmitter? _notifications;

    // M36 (ADR 0165, U05) — the mention-resolution read seam (the U03
    // `Mentions.IMentionService`, the M36·1 / M36·2 / M36·9 pins). **Optional**
    // (nullable default, the same CS1736 shape as `_notifications`) so the
    // existing test call sites keep compiling unchanged.
    private readonly Mentions.IMentionService? _mentions;

    // C06 (ADR 0288) — the one shared translation-variant write lane. The events
    // surface's add/update/remove translation methods are thin forwarders into
    // this (the descriptor names the events quirks: author / Translator /
    // GlobalAdmin standing via EventStandingPolicy, blank-title-normalize on
    // Create, AuthorId/Created restamp on ApplyUpdate). The session is opened by
    // the forwarder (the EventService owns its own write session, unlike PostService).
    private readonly TranslationLane<Event, EventTranslation> _translationLane;

    public EventService(IDocumentStore store, IAuthorizationService authorization, IUserInfoService userInfo,
        INotificationEmitter? notifications = null,
        Mentions.IMentionService? mentions = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        _userInfo = userInfo ?? throw new ArgumentNullException(nameof(userInfo));
        _notifications = notifications;
        _mentions = mentions;
        _translationLane = new TranslationLane<Event, EventTranslation>(new TranslationDescriptor<Event, EventTranslation>
        {
            TargetKind = "event",
            ActionPrefix = "eventtranslation",
            Noun = "Event",
            LoadStandingExtra = (_, _) => Task.FromResult<object?>(null),
            ResolveStanding = (Event e, object? _, string actorId, IReadOnlySet<string> actorRoles)
                => EventStandingPolicy.ResolveTranslationStanding(e.AuthorId, actorId, actorRoles),
            LoadParent = (IDocumentSession session, string id) => session.LoadAsync<Event>(id),
            Find = (IDocumentSession session, string id, string lang) => session.Query<EventTranslation>()
                .Where(t => t.EventId == id && t.LanguageCode == lang)
                .FirstOrDefaultAsync(),
            Create = (string id, string lang, string? title, string body, string actorId, DateTimeOffset created) => new EventTranslation
            {
                Id = Guid.NewGuid().ToString("N"),
                EventId = id,
                LanguageCode = lang,
                Title = string.IsNullOrWhiteSpace(title) ? null : title,
                Body = body,
                AuthorId = actorId,
                Created = created
            },
            ApplyUpdate = (EventTranslation row, string? title, string body, string actorId, DateTimeOffset now) =>
            {
                row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
                row.Body = body;
                row.AuthorId = actorId;
                row.Created = now;
            }
        });
    }

    /// <summary>
    /// M26 (C-SORT·2/5) — the event-feed ordering for the three event seams
    /// (upcoming / past / group): a <c>null</c> spec keeps the pinned
    /// <c>OrderBy(Start)</c> / <c>OrderByDescending(Start)</c> line
    /// **byte-for-byte** per sub-surface (C-SORT·2 — the upcoming-asc vs
    /// past-desc split is preserved via <paramref name="defaultAscending"/>);
    /// a non-null spec applies the closed event allowlist
    /// (U2 §2.2 rows 4–6: <c>start</c> / <c>created</c> / <c>title</c>) and
    /// ends with the <c>.ThenBy(e => e.Id)</c> tie-breaker (C-SORT·5).
    /// <para>
    /// The <c>ThenBy</c> is invoked fully-qualified as
    /// <c>Queryable.ThenBy(…)</c> — on Marten's
    /// <c>IAsyncQueryable</c>/<c>IOrderedAsyncQueryable</c> the unqualified
    /// <c>.ThenBy(…)</c> form is ambiguous (CS0411, the U4 carry-forward), so
    /// the fully-qualified <c>System.Linq.Queryable</c> overload is the one
    /// that resolves.
    /// </para>
    /// <para>
    /// All three keys (<see cref="Event.Start"/> / <see cref="Event.Created"/>
    /// / <see cref="Event.Title"/>) are **non-null** in the
    /// <see cref="Event"/> model (U2 §2.2 rows 4–6), so U4's Marten
    /// 9.31.2 <c>?? sentinel</c> limit does not apply here — no sentinel
    /// forms are used.
    /// </para>
    /// </summary>
    private static IQueryable<Event> OrderByEventSort(IQueryable<Event> q, SortSpec? sort, bool defaultAscending)
    {
        if (sort is null)
            // ← the pinned line, verbatim per sub-surface (C-SORT·2):
            // upcoming / group = OrderBy(Start) asc, past = OrderByDescending(Start) desc.
            return defaultAscending
                ? q.OrderBy(e => e.Start)
                : q.OrderByDescending(e => e.Start);

        // C-SORT·5 — the .ThenBy(e => e.Id) tie-breaker on every non-null sort
        // path (U2 §2.2 rows 4–6: start/created direct non-null, title
        // non-null with OrdinalIgnoreCase).
        return sort.Key switch
        {
            "start" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(e => e.Start), e => e.Id)
                : Queryable.ThenBy(q.OrderBy(e => e.Start), e => e.Id),
            "created" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(e => e.Created), e => e.Id)
                : Queryable.ThenBy(q.OrderBy(e => e.Created), e => e.Id),
            "title" => sort.Descending
                ? Queryable.ThenBy(q.OrderByDescending(e => e.Title, StringComparer.OrdinalIgnoreCase), e => e.Id)
                : Queryable.ThenBy(q.OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase), e => e.Id),
            _ => defaultAscending
                ? Queryable.ThenBy(q.OrderBy(e => e.Start), e => e.Id)
                : Queryable.ThenBy(q.OrderByDescending(e => e.Start), e => e.Id), // C-SORT·1 — unreachable (Parse already fell back); the default is pinned anyway
        };
    }

    // --- Read lanes (U03) -------------------------------------------------------

    /// <summary>
    /// The upcoming-events feed (ADR 0054 §4 — U03). Mirrors
    /// <see cref="Posts.PostService.ListFeedAsync"/>: the candidate set is the
    /// non-draft, non-deleted events whose <see cref="Event.Start"/> is at or
    /// after now (the <c>Start &gt;= nowUtc</c> time-window predicate — an
    /// event is "upcoming" until it starts; the <see cref="ListPastAsync"/>
    /// lane is its exact mirror on <c>Start &lt; nowUtc</c>), ordered by
    /// <see cref="Event.Start"/> ascending (the feed-ordering index,
    /// <see cref="EventDocTypes.Configure"/>); the
    /// <paramref name="componentId"/> is a *filter, never a gate*
    /// (C-M3·2); the survivors are <see
    /// cref="IAuthorizationService.CanSeeAsync(string, AccessAction, System.Collections.Generic.IEnumerable{IAuditableResource})"/>
    /// -filtered (C6, the one shared matching pass; C3, the single aggregate
    /// <see cref="AccessAudit"/> row with <c>TargetKind = "event"</c> via the
    /// <see cref="EventToAuditableResource"/>, U02).
    /// </summary>
    public async Task<EventPage> ListUpcomingAsync(string? componentId, string actorId, int page,
        CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        var nowUtc = DateTimeOffset.UtcNow;
        IQueryable<Event> q = session.Query<Event>()
            .Where(e => !e.IsDeleted && !e.IsDraft && e.GroupId == string.Empty) // GE·2 (ADR 0089) — a group-channel event (non-empty GroupId) never reaches the community feed.
            .Where(e => e.Start >= nowUtc); // upcoming lane: not yet started (the ListPastAsync mirror, <c>Start &lt; nowUtc</c>).
        if (componentId is not null)
            q = q.Where(e => e.ComponentId == componentId);
        var candidates = await OrderByEventSort(q, sort, defaultAscending: true)
            .Skip((page - 1) * ps).Take(ps).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return runs **before** any
        // decision (no audit row) and reports no further page (ADR 0090 D1).
        if (candidates.Count == 0)
            return new EventPage(Array.Empty<Event>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

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
    /// The <b>past events</b> lane (ADR 0109, <c>EV-PAST</c>) — mirrors
    /// <see cref="ListUpcomingAsync"/> verbatim except the time-window predicate
    /// (<c>Start &lt; nowUtc</c>) and the ordering (<c>Start</c> <b>descending</b> —
    /// the most recent past event first, the "history" reading order). Same
    /// candidate filter (<c>!IsDeleted &amp;&amp; !IsDraft</c>, group-channel
    /// exclusion GE·2, optional <c>ComponentId</c> filter — C-M3·2, a filter
    /// never a gate), the same single standalone <see
    /// cref="IAuthorizationService.CanSeeAsync(string, AccessAction, System.Collections.Generic.IEnumerable{IAuditableResource})"/>
    /// gate (C6, one matching pass; C3, one aggregate <see
    /// cref="AccessAudit"/> row <c>TargetKind = "event"</c> via the <see
    /// cref="EventToAuditableResource"/>), and the same 0-candidate
    /// no-decision early return (C-M7·5) + <c>HasMore</c> signal (ADR 0090
    /// D1 / D3).
    /// </summary>
    public async Task<EventPage> ListPastAsync(string? componentId, string actorId, int page,
        CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null)
    {
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        var nowUtc = DateTimeOffset.UtcNow;
        IQueryable<Event> q = session.Query<Event>()
            .Where(e => !e.IsDeleted && !e.IsDraft && e.GroupId == string.Empty) // GE·2 (ADR 0089) — a group-channel event (non-empty GroupId) never reaches the community feed.
            .Where(e => e.Start < nowUtc);
        if (componentId is not null)
            q = q.Where(e => e.ComponentId == componentId);
        var candidates = await OrderByEventSort(q, sort, defaultAscending: false)
            .Skip((page - 1) * ps).Take(ps).ToListAsync(ct).ConfigureAwait(false);

        // C-M7·5 (D8) — the 0-candidate early return runs **before** any
        // decision (no audit row) and reports no further page (ADR 0090 D1).
        if (candidates.Count == 0)
            return new EventPage(Array.Empty<Event>(), false);

        // ADR 0090 D1 / D3 — the sole paging signal: the page's candidate
        // list filled the page (candidates is the pre-CanSeeAsync list).
        var hasMore = candidates.Count == ps;

        // C6 — one shared matching pass; C3 — one aggregate audit row
        // (TargetKind "event"), from that single call (the ListUpcomingAsync shape).
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
        // SEC-READ: caller-gated by EventController.Detail — that action is author-scoped (the isAuthor branch) before it calls this seam (EventController.cs:968-971: `if (isAuthor) { rsvpRows = await this.events.GetRsvpsAsync(id, …) }`).
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
        // SEC-READ: owner-only — the candidate set is scoped to the actor's own RSVP rows ∪ the actor's authored events (EventService.cs:489/495: `r.UserId == actorId` / `e.AuthorId == actorId`), so every row is one the actor may already read.
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

    /// <inheritdoc cref="IEventService.ListDraftsAsync"/>
    public async Task<IReadOnlyList<Event>> ListDraftsAsync(string actorId, CancellationToken ct = default)
    {
        // SEC-READ: owner-only — the query is scoped to the actor's own drafts (EventService.cs:524: `e.IsDraft && e.AuthorId == actorId && !e.IsDeleted`).
        if (string.IsNullOrEmpty(actorId))
            throw new UnauthorizedAccessException("An actor is required to read their draft events.");

        await using var session = _store.QuerySession();
        return await session.Query<Event>()
            .Where(e => e.IsDraft && e.AuthorId == actorId && !e.IsDeleted)
            .OrderByDescending(e => e.Created)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    // ─── Standing-matrix gate helpers (ADR 0054 §3.4 — pure, no store) ───
    //
    // C04: these pure decisions moved to the <see cref="EventStandingPolicy"/>
    // static (the D6-allowed cross-module target). The service keeps a thin
    // forwarder so the Core write lanes and the <c>EventController</c> keep the
    // exact same call surface; the rule, the throws, and the <c>AccessVia</c>
    // derivation are the policy's, byte-identical. The Web no longer calls the
    // concrete service for a decision (the E9 / E12 anti-pattern this unit
    // exists to kill).

    /// <summary>
    /// The **create** standing (ADR 0054 §3.4) — a thin forwarder to
    /// <see cref="EventStandingPolicy.CheckCreate"/> (C04). Throws
    /// <see cref="KeyNotFoundException"/> (null event, the Web 404) and
    /// <see cref="UnauthorizedAccessException"/> (null/empty actor, the Web 403).
    /// </summary>
    public static void CheckCreateStanding(string actorId, IReadOnlySet<string> actorRoles, Event? @event)
        => EventStandingPolicy.CheckCreate(actorId, actorRoles, @event);

    /// <summary>
    /// The **edit** standing (ADR 0054 §3.4, the ADR 0014 / 0016 / 0017
    /// precedent) — a thin forwarder to
    /// <see cref="EventStandingPolicy.CheckEdit"/> (C04). Author (<c>Owner</c>)
    /// ∪ GlobalAdmin (<c>Admin</c> override). Throws
    /// <see cref="KeyNotFoundException"/> (null event, the Web 404) and
    /// <see cref="UnauthorizedAccessException"/> (a denied actor, the Web 403).
    /// </summary>
    public static void CheckEditStanding(string actorId, IReadOnlySet<string> actorRoles, Event? @event)
        => EventStandingPolicy.CheckEdit(actorId, actorRoles, @event);

    /// <summary>
    /// The **translation** display pin (ADR 0059) — a thin forwarder to
    /// <see cref="EventStandingPolicy.CanAddTranslation"/> (C04): <c>true</c>
    /// when <paramref name="actorId"/> may add / edit / remove a translation of
    /// the event authored by <paramref name="eventAuthorId"/>. A <b>display</b>
    /// pin, not a gate — the real deny is the
    /// <see cref="AddEventTranslationAsync"/> / <see cref="UpdateEventTranslationAsync"/>
    /// / <see cref="RemoveEventTranslationAsync"/> standing check, which re-runs
    /// the same rule server-side (the
    /// <see cref="EventStandingPolicy"/> resolver — display and gates can never
    /// drift apart).
    /// </summary>
    public static bool CanAddTranslation(string eventAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => EventStandingPolicy.CanAddTranslation(eventAuthorId, actorId, actorRoles);

    /// <summary>
    /// The ADR 0059 translation standing resolver (shared by
    /// <see cref="AddEventTranslationAsync"/> / <see cref="UpdateEventTranslationAsync"/>
    /// / <see cref="RemoveEventTranslationAsync"/> /
    /// <see cref="CanAddTranslation"/>) — a thin forwarder to the
    /// <see cref="EventStandingPolicy"/> resolver (C04). Returns the
    /// <see cref="AccessVia"/> the actor qualifies under, or <c>null</c> to deny
    /// (author → <see cref="AccessVia.Owner"/>; a <see cref="Roles.Translator"/>
    /// (ADR 0021) or a <see cref="Roles.GlobalAdmin"/> →
    /// <see cref="AccessVia.Admin"/>).
    /// </summary>
    private static AccessVia? ResolveTranslationStanding(string eventAuthorId, string actorId, IReadOnlySet<string> actorRoles)
        => EventStandingPolicy.ResolveTranslationStanding(eventAuthorId, actorId, actorRoles);

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
    // the <see cref="StandingMatrix.AuditVia"/> derivation; and the ADR 0059 translation
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
        // SEC-READ: no disclosure — a write lane with no audience-bearing read (the only store read is the LocaleSettings singleton for the authored-in code, EventService.cs:693); standing is in-method (CheckCreateStanding, EventService.cs:698 — any signed-in resident).
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

        // M18 (ADR 0119, D1/D3) — if the author chose a non-None recurrence rule,
        // materialize the full series (head + siblings) in this same session.
        // The expander is pure (C-M18·2); this lane only *calls* it and
        // *persists* the returned rows (U02's scope — the head-edit cascade is
        // U04). The head is occurrence #1 (carrying the rule + RecurrenceHeadId =
        // null, D3); each sibling carries RecurrenceHeadId = head.Id + a null
        // rule. The expander returns the head as row 0 (same object) and non-head
        // rows with placeholder Ids (string.Empty) — this lane assigns fresh ids
        // (the U01 contract).
        //
        // **Zero-change branch** (GATE-2's second pin): if the rule is null or
        // Recurrence.None, the code path below is byte-for-byte identical to the
        // pre-M18 create path (one row, RecurrenceHeadId = null, RecurrenceRule
        // = null).
        if (request.Recurrence is { } rule && rule.Recurrence != Recurrence.None)
        {
            @event.RecurrenceRule = rule;
            @event.RecurrenceHeadId = null; // the head is occurrence #1 (D3)
            var occurrences = EventRecurrenceExpander.ExpandRecurrence(@event, now);
            // Assign fresh ids to the non-head rows (the expander returns
            // placeholder Ids — string.Empty — per the U01 contract).
            for (var i = 1; i < occurrences.Count; i++)
                occurrences[i].Id = Guid.NewGuid().ToString("N");
            session.Store(@event);
            for (var i = 1; i < occurrences.Count; i++)
                session.Store(occurrences[i]);
        }
        else
        {
            // Zero-change branch (no rule / Recurrence.None) — byte-for-byte
            // unchanged (the existing create path).
            session.Store(@event);
        }
        session.Store(AccessAuditFactory.SingleTarget(actorId, "event.create", "event", @event.Id, AccessVia.Owner));

        // M36 (ADR 0165, U05) — the event mention emitter (Part 2.3 lane 6,
        // `event.mention`). Additive, *inside* the create lane (the frozen
        // method signature + audit row untouched): extract candidate
        // `@`-tokens from the event body, resolve them against the frozen
        // directory read (the author is never a recipient — M36-5 self pin),
        // and emit one `event.mention` notification per resolved recipient on
        // the write session. The per-recipient idempotency key (M36·4) keeps a
        // re-emission of the same (event, recipient) pair a no-op (ADR 0076
        // D4 / F10). `EmitAsync` does not commit; the `SaveChangesAsync`
        // below is the single commit (C3). No new authorization surface, no
        // `AccessAudit` row (M36·5 — a mention is a personal read).
        if (_mentions is not null && _notifications is not null)
        {
            var mentionTokens = Mentions.MentionTokens.ExtractTokens(@event.Body);
            if (mentionTokens.Count > 0)
            {
                var mentionRecipients = await _mentions.ResolveAsync(mentionTokens, actorId, ct)
                    .ConfigureAwait(false);
                foreach (var recipientId in mentionRecipients)
                {
                    await _notifications.EmitAsync(
                        session,
                        recipientId: recipientId,
                        kind: NotificationKinds.EventMention,
                        idempotencyKey: $"notification:event.mention:{@event.Id}:{recipientId}",
                        body: UgcSnippets.Truncate(@event.Body),
                        targetId: null,
                        linkPath: $"/events/{@event.Id}",
                        ct: ct).ConfigureAwait(false);
                }
            }
        }

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
        // SEC-READ: owner-only — the load is followed by the in-method standing gate CheckEditStanding (author ∪ GlobalAdmin, EventService.cs:810), then the write.
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

        // M18 (ADR 0119, D4) — capture the prior rule / Start / End **before**
        // the field-assignment block below mutates them. Used by the cascade
        // branch (which runs after the assignment block) to detect a rule or
        // Start / End change (the re-materialization trigger, D4).
        var priorRule = existing.RecurrenceRule;
        var priorStart = existing.Start;
        var priorEnd = existing.End;

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

        // M18 (ADR 0119, D4) — head-edit cascade.
        // A "head" is the row whose RecurrenceRule is non-null (D3). A head edit
        // cascades text / display / audience / language fields to every
        // non-deleted sibling (RecurrenceHeadId == head.Id && IsDeleted ==
        // false) in the same transaction (one SaveChangesAsync below — F4 / D4).
        // If the rule itself (D2 closed shape) or the head's Start / End
        // changed, the series is re-materialized: old non-head siblings are
        // soft-deleted (C-M18·8 — reversible, never hard-deleted), the pure
        // U01 expander re-expands from the updated head (C-M18·2), and the new
        // set is inserted with fresh Ids. The head's own Id is stable (it was
        // updated in place above, D4 — never reassigned).
        // A non-head row (RecurrenceRule == null) or a head with no rule takes
        // the existing single-row edit path unchanged (the zero-change branch —
        // the pre-M18 byte-for-byte shape).
        // Standing (C-M18·4 / C-M18·5): unchanged — CheckEditStanding (author ∪
        // GlobalAdmin) already ran above; no new AccessAction / AccessVia /
        // adapter is added by the cascade. The audit row is the same single
        // event.update row the existing lane commits (not one per occurrence).
        if (priorRule is not null)
        {
            var ruleChanged = !RulesEqual(priorRule, request.Recurrence);
            var startEndChanged = priorStart != existing.Start || priorEnd != existing.End;

            // Update the head's RecurrenceRule to the new rule (null if the
            // author chose "stop repeating" — request.Recurrence null/None,
            // D4). Always set it: if the rule is unchanged the values are the
            // same (just a different object instance from the request record);
            // if the rule changed this carries the new values.
            var newRule = request.Recurrence is { Recurrence: Recurrence.None } ? null : request.Recurrence;
            existing.RecurrenceRule = newRule;

            if (ruleChanged || startEndChanged)
                existing.Modified = DateTimeOffset.UtcNow;

            var siblings = await session.Query<Event>()
                .Where(e => e.RecurrenceHeadId == existing.Id && e.IsDeleted == false)
                .ToListAsync(ct).ConfigureAwait(false);

            if (ruleChanged || startEndChanged)
            {
                // Re-materialization (D4): soft-delete all existing non-head
                // siblings (C-M18·8 — reversible soft-delete, never hard-delete),
                // then re-expand from the updated head via the pure U01
                // expander (C-M18·2) and insert the new set with fresh Ids.
                // The head's Id is stable (updated in place above, D4 — never
                // reassigned).
                foreach (var sib in siblings)
                {
                    sib.IsDeleted = true;
                    session.Store(sib);
                }

                // Only re-expand if the new rule is a real (non-null, non-None)
                // rule — a "stop repeating" edit clears the rule + deletes
                // siblings and leaves no new set.
                if (newRule is not null && newRule.Recurrence != Recurrence.None)
                {
                    var newOccurrences = EventRecurrenceExpander.ExpandRecurrence(existing, DateTimeOffset.UtcNow);
                    // Row 0 is the head (same object — Id stable). Rows 1+ are
                    // new non-head occurrences with placeholder Ids (string.Empty)
                    // — assign fresh Ids (the U01 contract).
                    for (var i = 1; i < newOccurrences.Count; i++)
                    {
                        newOccurrences[i].Id = Guid.NewGuid().ToString("N");
                        session.Store(newOccurrences[i]);
                    }
                }
            }
            else
            {
                // Text / display / audience / language cascade (D4, F4): the
                // rule + Start/End are unchanged, so the existing sibling rows
                // stay — but their display fields must reflect the new head
                // values. Update them in place, in the same transaction.
                // The expander's CloneForOccurrence already copies these from
                // the head at creation time, but on a head edit the siblings
                // already exist and must be updated in place.
                foreach (var sib in siblings)
                {
                    sib.Title = existing.Title;
                    sib.Body = existing.Body;
                    sib.Location = existing.Location;
                    sib.Capacity = existing.Capacity;
                    sib.Color = existing.Color;
                    sib.Audience = existing.Audience;
                    sib.ReminderEnabled = existing.ReminderEnabled;
                    sib.LanguageCode = existing.LanguageCode;
                    if (changed)
                        sib.Modified = DateTimeOffset.UtcNow;
                    session.Store(sib);
                }
            }
        }

        session.Store(existing);
        // ADR 0054 §3.4 — the audit row tags the branch the actor qualified
        // under: the author → Owner, a non-author GlobalAdmin override → Admin.
        session.Store(AccessAuditFactory.SingleTarget(actorId, "event.update", "event", existing.Id, StandingMatrix.AuditVia(actorId, existing.AuthorId)));
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
        // SEC-READ: owner-only — the load is followed by the in-method author gate (ADR 0037 author-only publish, GlobalAdmin explicitly denied; EventService.cs:999-1000).
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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "event.publish", "event", existing.Id, AccessVia.Owner));
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
        // SEC-READ: owner-only — the load is followed by the in-method standing gate CheckEditStanding (author ∪ GlobalAdmin, ADR 0017; EventService.cs:1052), then the write.
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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "event.delete", "event", existing.Id, StandingMatrix.AuditVia(actorId, existing.AuthorId)));
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    // ─── M18 (ADR 0119, D5) — the author-only skip / undelete seams (U03) ───
    //
    // Skip is a soft-delete of ONE row (IsDeleted = true, C-M18·8); undelete is
    // its inverse (C-M18·8). Standing (C-M18·4 / D5): author ∪ GlobalAdmin — the
    // ADR 0014 / 0016 / 0017 edit-lane matrix. The locked seam signature carries
    // no actorRoles (the PublishAsync author-check precedent), so a non-author —
    // including a GlobalAdmin whose override is a Web-layer [Authorize] concern —
    // gets a 404 (KeyNotFoundException, the "absent" no-leak shape, NOT a 403,
    // C-M18·4 / D5). A head row (RecurrenceRule non-null) is not skippable / not
    // undeletable via this seam (the head is edited via the existing edit lane,
    // D5 / C-M18·8). No AccessAudit row (a write side-effect, not an access
    // decision — the unit plan's posture); no new AccessAction / AccessVia /
    // adapter (C-M18·5).

    /// <inheritdoc cref="IEventService.SkipOccurrenceAsync"/>
    /// <summary>
    /// M18 (ADR 0119, D5 / C-M18·8) — **soft-delete one occurrence**: sets
    /// <see cref="Event.IsDeleted"/> = <c>true</c> on the named row only. Never a
    /// hard-delete (C-M18·8), never a cascade, never a touch of the head's
    /// <see cref="Event.RecurrenceRule"/> or any sibling (C-M18·8). Standing
    /// (server-side, C3): **author ∪ GlobalAdmin** (C-M18·4 — the ADR 0014 /
    /// 0016 / 0017 edit-lane matrix); the locked seam signature carries no
    /// <c>actorRoles</c> (the <see cref="PublishAsync"/> author-check precedent),
    /// so a non-author gets a <c>KeyNotFoundException</c> (404, the "absent"
    /// no-leak shape, **not** a 403, C-M18·4 / D5). A head row (<see
    /// cref="Event.RecurrenceRule"/> non-null) is not skippable — the head is
    /// edited via the existing edit lane (D5 / C-M18·8). **No** <see
    /// cref="AccessAudit"/> row; **no** new <c>AccessAction</c> / <c>AccessVia</c>
    /// / adapter (C-M18·5).
    /// </summary>
    public async Task<Event> SkipOccurrenceAsync(string eventId, string actorId, CancellationToken ct = default)
    {
        // SEC-READ: owner-only — the load is followed by the in-method author gate (ADR 0037 author-only; a non-author gets the no-leak 404, EventService.cs:1113-1115).
        if (string.IsNullOrEmpty(eventId))
            throw new KeyNotFoundException("An event id is required.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        // Standing (C-M18·4 / D5): author-only at the Core level (the locked seam
        // signature carries no actorRoles — the PublishAsync author-check
        // precedent). A non-author gets a 404 (the "absent" no-leak shape — NOT a
        // 403, C-M18·4); the same observable as a missing id, so a stranger cannot
        // tell that the row exists.
        if (!string.Equals(existing.AuthorId, actorId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        // Head-row guard (design doc §6, D5 / C-M18·8): the head (RecurrenceRule
        // non-null) is edited via the existing edit lane, never skipped via this
        // seam (the "no delete-entire-series button" pin). Same 404 observable.
        if (existing.RecurrenceRule is not null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        if (!existing.IsDeleted)
        {
            existing.IsDeleted = true;                 // C-M18·8 — the soft-delete flag (one row).
            existing.Modified = DateTimeOffset.UtcNow;
        }

        session.Store(existing);
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    /// <inheritdoc cref="IEventService.UndeleteOccurrenceAsync"/>
    /// <summary>
    /// M18 (ADR 0119, D5 / C-M18·8) — the **inverse of
    /// <see cref="SkipOccurrenceAsync"/>**: sets <see cref="Event.IsDeleted"/> =
    /// <c>false</c> on the named row only. Same standing (C-M18·4), same one-row
    /// scope (C-M18·8), same 404 "absent" no-leak shape (C-M18·4 / D5). A skip is
    /// always reversible by the author (C-M18·8). A head row ( <see
    /// cref="Event.RecurrenceRule"/> non-null) is not undeletable via this seam (the
    /// head's <c>IsDeleted</c> is set by the existing edit / delete lanes). **No**
    /// <see cref="AccessAudit"/> row; **no** new <c>AccessAction</c> /
    /// <c>AccessVia</c> / adapter (C-M18·5).
    /// </summary>
    public async Task<Event> UndeleteOccurrenceAsync(string eventId, string actorId, CancellationToken ct = default)
    {
        // SEC-READ: owner-only — the load is followed by the in-method author gate (ADR 0037 author-only, the SkipOccurrenceAsync shape, EventService.cs:1159).
        if (string.IsNullOrEmpty(eventId))
            throw new KeyNotFoundException("An event id is required.");

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var existing = await session.LoadAsync<Event>(eventId, ct).ConfigureAwait(false);
        if (existing is null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        // Standing + head-row guard — the same 404 "absent" no-leak shape as
        // SkipOccurrenceAsync (C-M18·4 / D5; the head is edited via the existing
        // edit lane, D5 / C-M18·8).
        if (!string.Equals(existing.AuthorId, actorId, StringComparison.Ordinal))
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        if (existing.RecurrenceRule is not null)
            throw new KeyNotFoundException($"Event '{eventId}' was not found.");

        if (existing.IsDeleted)
        {
            existing.IsDeleted = false;                // C-M18·8 — reversible (one row).
            existing.Modified = DateTimeOffset.UtcNow;
        }

        session.Store(existing);
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
        // SEC-READ: caller-gated by EventController.Detail / the feed reads — the parent event's single Read decision (GetAsync's CanAsync(Read) / the feed's CanSeeAsync) already ran before the caller reads this seam (C-M3·1: a translation carries no own audience, it inherits the parent's decision).
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
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        return await _translationLane.AddAsync(session, eventId, languageCode, title, body, actorId, actorRoles).ConfigureAwait(false);
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
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        return await _translationLane.UpdateAsync(session, eventId, languageCode, title, body, actorId, actorRoles).ConfigureAwait(false);
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
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        await _translationLane.RemoveAsync(session, eventId, languageCode, actorId, actorRoles).ConfigureAwait(false);
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
    // M18 (ADR 0119, D4) — rule equality for the head-edit cascade.
    // Two rules are "equal" when Recurrence / Interval / Count / Ends all match.
    // null and Recurrence.None are treated as equivalent (both mean "no
    // recurrence"). The cascade uses this to detect whether a re-materialization
    // is needed (a rule change triggers soft-delete + re-expand, D4).
    private static bool RulesEqual(EventRecurrenceRule? a, EventRecurrenceRule? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        var aRec = a.Recurrence == Recurrence.None ? (Recurrence?)null : a.Recurrence;
        var bRec = b.Recurrence == Recurrence.None ? (Recurrence?)null : b.Recurrence;
        return aRec == bRec
            && a.Interval == b.Interval
            && a.Count == b.Count
            && a.Ends == b.Ends;
    }

    private static readonly IReadOnlySet<string> StaticEmptyRoles = new HashSet<string>(StringComparer.Ordinal);

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
