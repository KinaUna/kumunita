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

    // --- Write lanes (U04) — standing re-checked server-side (§3.4, C3) --------

    /// <summary>
    /// Create an event — the author's choice is written verbatim (ADR 0001-B); the
    /// <c>IsDraft</c> flag is the ADR 0037 pin (a newly created event is a draft). The
    /// author becomes the standing owner (the <c>AuthorId</c> branch). The
    /// <c>AccessAudit</c> row (<c>event.create</c>, <c>TargetKind = "event"</c>) is
    /// stored in the caller's session (C3).
    /// </summary>
    Task<Event> CreateAsync(string actorId, CreateEventRequest request, CancellationToken ct = default);

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
