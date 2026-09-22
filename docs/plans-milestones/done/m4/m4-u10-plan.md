# U10 — `EventControllerTests` (Web.Tests)

- **Lane:** Events (`M4`)
- **Unit:** U10 (of U00–U12)
- **Kind:** test (the controller tests — no behavior change)

## Goal

Write the **`EventControllerTests`** for M4 in
`tests/Kumunita.Web.Tests/`. These are the controller tests that pin the
`EventController` actions (feed / detail / composer / edit / publish /
delete / RSVP). The tests follow the M3 controller test pattern (the
`AnnouncementControllerTests` / `PostControllerTests` shape). **No
behavior change** — the tests pin the existing behavior.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `tests/Kumunita.Web.Tests/Announcements/AnnouncementControllerTests.
   cs` — the controller test shape to mirror (the `EventController`
   tests; the `IEventService` mock; the `AuthorizationService` mock).
2. `tests/Kumunita.Web.Tests/Posts/PostControllerTests.cs` — the post
   controller test shape to mirror (the `PostController` tests; the
   `IPostService` mock; the `AuthorizationService` mock).
3. `src/Kumunita.Web/Controllers/EventController.cs` — the controller
   implementation (U05's output).
4. `src/Kumunita.Web/Models/EventEditorModel.cs` — the editor model
   (U05's output).
5. `docs/design/m4-events-design.md` §3.8 — the three-test acceptance
   gate (the primary source for this unit).

## Deliverables (≤ 2 files)

1. **`tests/Kumunita.Web.Tests/Events/EventControllerTests.cs`** — the
   controller tests (the `AnnouncementControllerTests` shape to mirror):
   - `Index_ReturnsFeedOfUpcomingEvents` — the feed (the
     `IEventService.ListUpcomingAsync` call; the `ViewBag` setup).
   - `Index_FiltersByComponent` — the feed filter (the `componentId`
     parameter).
   - `Detail_ReturnsEventById` — the detail (the
     `IEventService.GetAsync` call; the `ViewBag` setup).
   - `Detail_ReturnsNotFoundWhenEventIsDeleted` — the 404 case (the
     `KeyNotFoundException` throw).
   - `Create_ReturnsComposerView` — the composer (the
     `AudienceEditorModel` + the `bindRichEditor` script block).
   - `Edit_ReturnsEditView` — the edit (the `IEventService.GetAsync`
     call; the `AudienceEditorModel` + the `bindRichEditor` script
     block).
   - `Edit_ReturnsForbiddenWhenNotAuthor` — the edit gate (the
     `CheckEditStanding` gate — the 403 case).
   - `Publish_CallsEventServicePublishAsync` — the publish (the
     `IEventService.PublishAsync` call; the `Redirect` shape).
   - `Delete_CallsEventServiceDeleteAsync` — the soft-delete (the
     `IEventService.DeleteAsync` call; the `Redirect` shape).
   - `Rsvp_CallsEventServiceRsvpAsync` — the RSVP (the
     `IEventService.RsvpAsync` call; the `Redirect` shape).
   - `Rsvp_ReturnsForbiddenWhenEventIsDeleted` — the RSVP filter (the
     `IsDeleted = false` filter on the event).
   - `Drafts_ReturnsListOfDraftEvents` — the drafts lane (the
     `IEventService.ListDraftsAsync` call; the `ViewBag` setup).
2. **`tests/Kumunita.Web.Tests/Events/EventReminderHandlerTests.cs`**
   — the handler tests (the `AuditPurgeHandlerTests` shape to mirror):
   - `Handle_CallsEventReminderServiceSendRemindersAsync` — the handler
     (the `EventReminderService.SendRemindersAsync` call).
   - `Handle_SelfReschedulesNextTick` — the self-rescheduling (the
     `return new[] { new EventReminderTick() };` line).
   - `Handle_LogsNumberOfRemindersSent` — the logging (the
     `Wolverine.Logging.Info` call).
   - `Handle_PropagatesExceptions` — the error handling (the exception
     propagation shape).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green (the controller tests + the handler tests pass).
- The `EventControllerTests` + `EventReminderHandlerTests` compile.
- Handoff note: 5 lines starting `## U10 — EventControllerTests +
  EventReminderHandlerTests` — (a) the `EventControllerTests` count
  (12 tests), (b) the `EventReminderHandlerTests` count (4 tests), (c)
  the RSVP tests (the `Rsvp_CallsEventServiceRsvpAsync` /
  `Rsvp_ReturnsForbiddenWhenEventIsDeleted` tests), (d) the drafts
  tests (the `Drafts_ReturnsListOfDraftEvents` test), (e) the handler
  tests (the `Handle_CallsEventReminderServiceSendRemindersAsync` /
  `Handle_SelfReschedulesNextTick` / `Handle_LogsNumberOfRemindersSent`
  / `Handle_PropagatesExceptions` tests).

## Notes / deviations

- The 16 controller + handler tests follow the **M3 controller test
  pattern** (the `AnnouncementControllerTests` / `PostControllerTests`
  shape). **No new test mechanism.**
- The RSVP tests pin the **last-write-wins concurrency exception** (the
  `docs/ARCHITECTURE.md` §5 exception — keyed per `(EventId, UserId)`;
  a conflicting RSVP is a no-op, the resident's latest status is the
  truth). **No `AccessAudit` row** on an RSVP (a routine resident
  action, not an access decision).
- The handler tests pin the **self-rescheduling shape** (the `return
  new[] { new EventReminderTick() };` line) + the **`Wolverine.
  Logging.Info` call** (the `AuditPurgeHandler` logging shape to
  mirror). **No new handler mechanism.**
- The drafts tests pin the **`IEventService.ListDraftsAsync` call**
  (the `IAnnouncementService.ListDraftsAsync` shape to mirror). **No
  new drafts mechanism.**
