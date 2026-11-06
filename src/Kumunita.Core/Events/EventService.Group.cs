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
/// The <c>/events</c> group-events lane — the group-scoped
/// event read + write actions (ADR 0115 events↔projects,
/// the group-events surface). A partial class of
/// <see cref="EventService"/> extracted so the main file stays
/// under the 600 <c>cs_file</c> ceiling (C3-04).
/// </summary>
public partial class EventService
{
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
    public async Task<GroupEventFeedResult> ListGroupEventsAsync(string groupId, string actorId, int page,
        CancellationToken ct = default, SortSpec? sort = null, int? pageSize = null)
    {
        if (string.IsNullOrEmpty(groupId)) throw new ArgumentException("A group events feed requires a groupId.", nameof(groupId));
        if (string.IsNullOrEmpty(actorId)) throw new ArgumentException("Core expects an authenticated actor (the Web layer enforces [Authorize]).", nameof(actorId));
        if (page < 1) page = 1;
        int ps = PageSizer.ResolveOverride(pageSize, PageSize);

        await using var session = _store.QuerySession();
        IQueryable<Event> q = session
            .Query<Event>()
            .Where(e => e.GroupId == groupId && !e.IsDeleted && !e.IsDraft);
        var candidates = await OrderByEventSort(q, sort, defaultAscending: true)
            .Skip((page - 1) * ps)
            .Take(ps)
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
            // ADR 0090 D1 / design doc §7.4 — HasMore: candidates.Count == ps.
            return new GroupEventFeedResult(Visible: candidates, HiddenCount: 0, Page: page, Total: candidates.Count, HasMore: candidates.Count == ps);

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
        session.Store(AccessAuditFactory.SingleTarget(actorId, "event.create", "event", @event.Id, AccessVia.Owner));

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
}
