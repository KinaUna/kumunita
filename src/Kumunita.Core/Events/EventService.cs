using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
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
    // <c>event.publish</c> / <c>event.delete</c>, <c>Via</c> =
    // <see cref="AccessVia.Owner"/> (create, publish) or
    // <see cref="AccessVia.Admin"/> (edit / delete by a non-author GlobalAdmin —
    // the <see cref="AuditViaFor"/> derivation), <c>Outcome</c> =
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
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        return rsvp;
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
