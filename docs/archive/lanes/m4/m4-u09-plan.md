# U09 — The 23 pinned seam tests (Core.Tests)

- **Lane:** Events (`M4`)
- **Unit:** U09 (of U00–U12)
- **Kind:** test (the seam tests — no behavior change)

## Goal

Write the **23 pinned seam tests** for M4 in `tests/Kumunita.Core.Tests/`.
These are the seam tests that pin the `Event` + `EventRsvp` docs, the
`EventToAuditableResource` adapter, the `EventService` read + write
lanes, the RSVP last-write-wins concurrency exception, and the
`EventReminderService` business logic. The tests follow the M3 seam
test pattern (the `PostServiceTests` / `AnnouncementServiceTests`
shape). **No behavior change** — the tests pin the existing behavior.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `tests/Kumunita.Core.Tests/Announcements/AnnouncementServiceTests.cs`
   — the seam test shape to mirror (the `IAnnouncementService` tests;
   the `IMailerStage` mock; the `IAuthorizationService` mock).
2. `tests/Kumunita.Core.Tests/Posts/PostServiceTests.cs` — the post
   seam test shape to mirror (the `IPostService` tests; the
   `IMailerStage` mock; the `IAuthorizationService` mock).
3. `src/Kumunita.Core/Events/Event.cs` — the POCO (U01's output).
4. `src/Kumunita.Core/Events/EventService.cs` — the service
   implementation (U03 + U04's output).
5. `docs/design/m4-events-design.md` §3.7 — the 23 test names (the
   primary source for this unit).

## Deliverables (≤ 3 files)

1. **`tests/Kumunita.Core.Tests/Events/EventServiceTests.cs`** — the
   read + write seam tests (the `AnnouncementServiceTests` shape to
   mirror):
   - `ListUpcomingAsync_ReturnsOnlyNonDraftNonDeletedEvents` — the
     feed filter (the `IsDraft = false` + `IsDeleted = false` filter).
   - `ListUpcomingAsync_FiltersByAudience` — the audience check (the
     `IAuthorizationService.CanAccessAsync` check).
   - `GetAsync_ReturnsEventById` — the detail (the single event by id).
   - `GetAsync_ReturnsNullWhenNotFound` — the 404 case (the
     `KeyNotFoundException` throw).
   - `CreateAsync_SetsIsDraftTrueByDefault` — the create (the
     `IsDraft = true` flag — ADR 0037).
   - `CreateAsync_ThrowsWhenNoStanding` — the create gate (the
     `CheckCreateStanding` gate — the `UnauthorizedAccessException`
     throw).
   - `UpdateAsync_UpdatesEventFields` — the edit (the ADR 0014 / 0016 /
     0017 author-only precedent).
   - `UpdateAsync_ThrowsWhenNotAuthor` — the edit gate (the
     `CheckEditStanding` gate — the `UnauthorizedAccessException`
     throw).
   - `PublishAsync_SetsIsDraftFalse` — the publish (the `IsDraft =
     false` flip).
   - `DeleteAsync_SetsIsDeletedTrue` — the soft-delete (the
     `IsDeleted = true` flag — ADR 0024).
   - `RsvpAsync_CreatesNewRsvpRow` — the RSVP (the last-write-wins
     concurrency exception — the `(EventId, UserId)` unique index).
   - `RsvpAsync_UpdatesExistingRsvpRow` — the RSVP update (the
     last-write-wins concurrency exception — the conflicting RSVP is a
     no-op).
   - `RsvpAsync_ThrowsWhenEventIsDeleted` — the RSVP filter (the
     `IsDeleted = false` filter on the event).
   - `RsvpAsync_DoesNotWriteAccessAuditRow` — the RSVP (no
     `AccessAudit` row — a routine resident action, not an access
     decision).
2. **`tests/Kumunita.Core.Tests/Events/EventReminderServiceTests.cs`**
   — the reminder seam tests (the `AuditPurgeServiceTests` shape to
   mirror):
   - `SendRemindersAsync_RemindsGoingRsvpsWithin24Hours` — the 24-hour
     window (the `now.AddHours(-24) < e.Start <= now` semantics).
   - `SendRemindersAsync_IncludesAuthorEvenWithoutRsvp` — the
     author-inclusion rule (the `Event.AuthorId` always included).
   - `SendRemindersAsync_UsesRemindIdempotencyKey` — the idempotency
     key (the `remind:{eventId}:{userId}` shape).
   - `SendRemindersAsync_StagesEmailViaIMailerStage` — the
     `IMailerStage.StageAsync` call (the `OutboxEmailStager.StageAsync`
     shape).
   - `SendRemindersAsync_SkipsEventsPastReminderEnabledFalse` — the
     `ReminderEnabled = false` filter (the `Event.ReminderEnabled`
     field — U01's output).
3. **`tests/Kumunita.Core.Tests/Events/EventToAuditableResourceTests.
   cs`** — the adapter seam tests (the `PostToAuditableResourceTests`
   shape to mirror):
   - `FromEvent_SetsAllSixMembers` — the 6-member field mapping (the
     `Id` / `Name` / `OwnerId` / `Audience` / `ComponentId` /
     `TargetKind`).
   - `FromEvent_SetsTargetKindToEvent` — the `TargetKind = "event"`
     pin.
   - `FromEvent_SetsNameToFallbackWhenTitleIsNull` — the `Name` 60-char
     fallback (the `PostToAuditableResource` shape verbatim).
   - `FromEvent_SetsAudienceFromEventAudience` — the `Audience` field
     (the exact post `Audience` shape — ADR 0001-B / 0036).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green (the 23 seam tests pass).
- The `EventServiceTests` + `EventReminderServiceTests` +
  `EventToAuditableResourceTests` compile.
- Handoff note: 6 lines starting `## U09 — 23 seam tests` — (a) the
  `EventServiceTests` count (14 tests), (b) the
  `EventReminderServiceTests` count (5 tests), (c) the
  `EventToAuditableResourceTests` count (4 tests), (d) the RSVP
  last-write-wins tests (the `RsvpAsync_CreatesNewRsvpRow` /
  `RsvpAsync_UpdatesExistingRsvpRow` / `RsvpAsync_ThrowsWhenEventIs
  Deleted` / `RsvpAsync_DoesNotWriteAccessAuditRow` tests), (e) the
  reminder tests (the `SendRemindersAsync_RemindsGoingRsvpsWithin24Hours`
  / `SendRemindersAsync_IncludesAuthorEvenWithoutRsvp` /
  `SendRemindersAsync_UsesRemindIdempotencyKey` /
  `SendRemindersAsync_StagesEmailViaIMailerStage` /
  `SendRemindersAsync_SkipsEventsPastReminderEnabledFalse` tests).

## Notes / deviations

- The 23 seam tests follow the **M3 seam test pattern** (the
  `PostServiceTests` / `AnnouncementServiceTests` shape). **No new test
  mechanism.**
- The RSVP tests pin the **last-write-wins concurrency exception** (the
  `docs/ARCHITECTURE.md` §5 exception — keyed per `(EventId, UserId)`;
  a conflicting RSVP is a no-op, the resident's latest status is the
  truth). **No `AccessAudit` row** on an RSVP (a routine resident
  action, not an access decision).
- The reminder tests pin the **24-hour window** (the
  `now.AddHours(-24) < e.Start <= now` semantics) + the **`Going` RSVP
  filter** (the `EventRsvp.Status = Going` rows) + the
  **author-inclusion rule** (the `Event.AuthorId` always included) +
  the **idempotency key** (the `remind:{eventId}:{userId}` shape) + the
  **`IMailerStage.StageAsync` call** (the `OutboxEmailStager.
  StageAsync` shape). **No new reminder mechanism.**
- The adapter tests pin the **6-member field mapping** (the `Id` /
  `Name` / `OwnerId` / `Audience` / `ComponentId` / `TargetKind`) + the
  **`TargetKind = "event"` pin** + the **`Name` 60-char fallback**
  (the `PostToAuditableResource` shape verbatim). **No new adapter
  mechanism.**
