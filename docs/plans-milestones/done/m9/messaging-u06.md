# M9 Messaging — U06 · Web tests: `MessagesControllerTests` (NSubstitute, no Postgres)

> **Unit plan (secondary tier, self-contained).** You are a fresh-context
> agent executing one unit of M9 (Messaging). Read **this file + your entry
> reads below** — that is all the context you need. The register is
> `docs/plans-milestones/plan-m9-messaging.md`; the scratch handoff note is
> `docs/plans-milestones/m9-messaging-handoff-notes.md`. **Atomicity
> contract:** ≤ 5 files, ≤ ~400 LOC, exit = one build + the pinned test
> class. **Unit-series rule:** never touch files outside your own Deliverables;
> no tests beyond the pinned list; no new seams on frozen interfaces.

## Goal

Pin the Web shape over the substituted `IMessagingService` (the M8 U03 /
`AnnouncementControllerTests` harness — the controller takes the **interface**,
NSubstitute stands in for it, no Postgres).

## Context (the [PROPOSED] shape you pin)

- **D2 · The toggle gates the surface.** `IsMessagingEnabledAsync()` returning
  `false` ⇒ the resident surfaces 404/redirect and the service list/thread
  seams are **never called** (the controller reads the toggle first). The
  admin surface always renders (it is how the instance flips the gate).
- **C-M9·1 · Non-participants see nothing.** A `GetConversationAsync` that
  throws `KeyNotFoundException` (U03's non-leaky 404) ⇒ the controller maps
  it to a 404 / empty state — **no** view data, **no** exception surfaced to
  the user. The controller adds **no** authorization call of its own.
- **F6 · The admin write is the service's.** `AdminMessaging` POST calls
  `SetMessagingEnabledAsync(enabled, actorId)` — the controller maps the
  current user to `actorId` + the form's `enabled` value and nothing else.
- **F7 · Localized.** The views render `message.*` `kw-l` keys — the tests
  pin the **shape** (route, status, service-call, model), not the rendered
  language.

## Entry reads (4)

1. `docs/design/m9-messaging-design.md` §web + §FACES (the locked surface).
2. `tests/Kumunita.Web.Tests/AnnouncementControllerTests.cs` (the
   NSubstitute-over-service controller harness — **copy its setup**: how it
   constructs the controller with a substituted service, how it asserts
   status codes / view models / service-call counts).
3. `src/Kumunita.Web/Controllers/MessagesController.cs` (U04's code — the
   exact action names + view-model shapes you assert against).
4. `src/Kumunita.Web/Controllers/AdminMessagingController.cs` (U05's code).

## Deliverables (1)

- `tests/Kumunita.Web.Tests/MessagesControllerTests.cs`.

## Pinned tests (10)

`Messages_Index_ToggleOff_RendersDisabled_NoListCall` ·
`Messages_Index_ToggleOn_RendersConversationList` ·
`Messages_Thread_NonParticipant_404_NoView` ·
`Messages_Thread_RendersMessages_WithUnreadMarkers` ·
`Messages_Send_PostsBody_ToService` ·
`Messages_Send_BlankBody_RendersError_NoServiceCall` ·
`Messages_Nav_ToggleOff_EntryHidden` ·
`Messages_Nav_ToggleOn_EntryPresent` ·
`AdminMessaging_Get_RendersCurrentState` ·
`AdminMessaging_Post_FlipsToggle_AuditedByService`.

(The exact assertion for `AdminMessaging_Post_FlipsToggle_AuditedByService`
pins that the controller **calls** `SetMessagingEnabledAsync` with the form's
`enabled` value + the actor id — the audit row itself is the service's
responsibility, pinned in U02's Core tests; this test pins the controller's
hand-off.)

## Exit

`dotnet build Kumunita.slnx -c Debug` green; the class passes
(`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
-class "Kumunita.Web.Tests.MessagesControllerTests"`).

Append a `## U06 — Web tests` section to the handoff note: the harness
pattern copied (the `AnnouncementControllerTests` setup as applied), the 10
test names as written (any rename from the plan — record it), any drift (e.g.
a view-model property U04 named that the design doc didn't, or a route the
controller uses that differs from `/messages`).

**Last action:** once the Exit above is satisfied and the `## U06` section is
appended, move **this unit's own plan file** from
`docs/plans-milestones/in-progress/messaging-u06.md` to
`docs/plans-milestones/done/m9/messaging-u06.md`. Each unit moves only its own
file as it completes — U07's plan is already sitting in `in-progress/`, so
the next agent just reads it there.
