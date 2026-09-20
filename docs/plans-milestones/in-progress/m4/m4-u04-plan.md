# U04 — `EventService` write lanes (create / edit / publish / delete + RSVP)

- **Lane:** Events (`M4`)
- **Unit:** U04 (of U00–U12)
- **Kind:** behavior (write + RSVP — no controller, no tests)

## Goal

Implement the `EventService` **write lanes** (the `CreateAsync` /
`UpdateAsync` / `PublishAsync` / `DeleteAsync` methods) + the **RSVP
lane** (the `RsvpAsync` method) in `Kumunita.Core`. Mirror the
`AnnouncementService` write lanes (the `CreateAsync` C3 pattern; the
`UpdateAsync` shape; the `DeleteAsync` soft-delete shape). The RSVP lane
is the **last-write-wins** concurrency exception (the
`docs/ARCHITECTURE.md` §5 exception — keyed per `(EventId, UserId)`; a
conflicting RSVP is a no-op, the resident's latest status is the truth).
**No controller, no tests** (U05's controller; U09's tests).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Core/Announcements/AnnouncementService.cs` (the write
   lanes: `CreateAsync` / `UpdateAsync` / `DeleteAsync`) — the shape to
   mirror (the `CreateAsync` C3 pattern; the `UpdateAsync` shape; the
   `DeleteAsync` soft-delete shape).
2. `src/Kumunita.Core/Posts/PostService.cs` (the `CreateAsync` /
   `UpdateAsync` / `DeleteAsync` methods) — the post shape to mirror.
3. `src/Kumunita.Core/Events/Event.cs` — the POCO (U01's output).
4. `src/Kumunita.Core/Events/EventRsvp.cs` — the RSVP POCO (U01's
   output).
5. `docs/design/m4-events-design.md` §3.2 + §3.4 — the RSVP shape + the
   standing matrix (the primary source for this unit).

## Deliverables (≤ 3 files)

1. **`src/Kumunita.Core/Events/IEventService.cs`** — add the write lanes
   + the RSVP lane to the interface (the `IAnnouncementService` write
   lanes to mirror):
   - `Task<Event> CreateAsync(string actorId, EventDraft draft,
     CancellationToken ct)` — the create (the `AnnouncementService.
     CreateAsync` C3 pattern; the `CheckCreateStanding` gate; the
     `IsDraft = true` flag — ADR 0037).
   - `Task<Event> UpdateAsync(string actorId, string eventId,
     EventUpdate update, CancellationToken ct)` — the edit (the ADR 0014 /
     0016 / 0017 author-only precedent; the `CheckEditStanding` gate; the
     `Modified` timestamp update).
   - `Task<Event> PublishAsync(string actorId, string eventId,
     CancellationToken ct)` — the publish (the `IsDraft = false` flip;
     the `CheckEditStanding` gate).
   - `Task DeleteAsync(string actorId, string eventId, CancellationToken
     ct)` — the soft-delete (the `IsDeleted = true` flag — ADR 0024; the
     `CheckEditStanding` gate).
   - `Task<EventRsvp> RsvpAsync(string actorId, string eventId,
     EventRsvpStatus status, CancellationToken ct)` — the RSVP (the
     last-write-wins concurrency exception; the `(EventId, UserId)`
     unique index; the `IsDeleted = false` filter on the event).
2. **`src/Kumunita.Core/Events/EventService.cs`** — implement the write
   lanes + the RSVP lane (the `AnnouncementService` write lanes to
   mirror):
   - `CreateAsync` — the create (the `AnnouncementService.CreateAsync`
     C3 pattern; the `CheckCreateStanding` gate; the `IsDraft = true`
     flag — ADR 0037; the `IMailerStage.StageAsync` call for the
     notification email — the M1 step 7 seam; the idempotency key
     `create:{eventId}:{userId}`).
   - `UpdateAsync` — the edit (the ADR 0014 / 0016 / 0017 author-only
     precedent; the `CheckEditStanding` gate; the `Modified` timestamp
     update; the `IMailerStage.StageAsync` call for the notification
     email — the M1 step 7 seam; the idempotency key
     `update:{eventId}:{userId}`).
   - `PublishAsync` — the publish (the `IsDraft = false` flip; the
     `CheckEditStanding` gate; the `IMailerStage.StageAsync` call for
     the notification email — the M1 step 7 seam; the idempotency key
     `publish:{eventId}:{userId}`).
   - `DeleteAsync` — the soft-delete (the `IsDeleted = true` flag —
     ADR 0024; the `CheckEditStanding` gate; the `IMailerStage.StageAsync`
     call for the notification email — the M1 step 7 seam; the
     idempotency key `delete:{eventId}:{userId}`).
   - `RsvpAsync` — the RSVP (the last-write-wins concurrency exception;
     the `(EventId, UserId)` unique index; the `IsDeleted = false`
     filter on the event; **no `AccessAudit` row** — a routine resident
     action, not an access decision).
3. **`src/Kumunita.Core/Events/EventDraft.cs`** — the draft POCO (the
   `AnnouncementDraft` shape to mirror — the `Title` / `Body` /
   `ComponentId?` / `Start` / `End` / `Location?` / `Capacity?` /
   `Audience` / `ReminderEnabled` / `LanguageCode` / `TagIds` /
   `ImageIds` / `AttachmentIds` fields).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- The `IEventService` write lanes + RSVP lane + the `EventService`
  implementation + the `EventDraft` POCO compile.
- **No new test** (U09's seam tests are the first M4 tests).
- Handoff note: 6 lines starting `## U04 — EventService write lanes +
  RSVP` — (a) the `IEventService` method count (9: `ListUpcomingAsync` /
  `GetAsync` / `CheckCreateStanding` / `CheckEditStanding` /
  `CreateAsync` / `UpdateAsync` / `PublishAsync` / `DeleteAsync` /
  `RsvpAsync`), (b) the RSVP last-write-wins concurrency exception (the
  `docs/ARCHITECTURE.md` §5 exception — keyed per `(EventId, UserId)`),
  (c) the idempotency keys (`create:{eventId}:{userId}` /
  `update:{eventId}:{userId}` / `publish:{eventId}:{userId}` /
  `delete:{eventId}:{userId}` / `remind:{eventId}:{userId}`), (d) the
  `IMailerStage.StageAsync` calls (the M1 step 7 seam), (e) the
  `EventDraft` POCO shape (the `AnnouncementDraft` shape to mirror).

## Notes / deviations

- The RSVP lane is the **last-write-wins** concurrency exception (the
  `docs/ARCHITECTURE.md` §5 exception — keyed per `(EventId, UserId)`;
  a conflicting RSVP is a no-op, the resident's latest status is the
  truth). **No `AccessAudit` row** on an RSVP (a routine resident
  action, not an access decision).
- The write lanes are the **`AnnouncementService` write lanes to
  mirror** (the `CreateAsync` C3 pattern; the `UpdateAsync` shape; the
  `DeleteAsync` soft-delete shape). **No new write mechanism.**
- The `IMailerStage.StageAsync` calls are the **M1 step 7 seam** (the
  `IMailerStage` interface + the `OutboxEmail` doc + the
  `OutboxEmailHandler` durable handler). **No new email mechanism.**
- The idempotency keys are the **M1 step 7 seam** (the `verify:{userId}:
  {attempt}` / `setup:{userId}` / `invite:{componentId}:{email}` /
  `remind:{eventId}:{userId}` key scheme). **No new key mechanism.**
- The `EventDraft` POCO is the **`AnnouncementDraft` shape to mirror**
  (the `Title` / `Body` / `ComponentId?` / `Start` / `End` / `Location?`
  / `Capacity?` / `Audience` / `ReminderEnabled` / `LanguageCode` /
  `TagIds` / `ImageIds` / `AttachmentIds` fields). **No new draft
  mechanism.**
