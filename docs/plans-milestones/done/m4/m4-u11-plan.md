# U11 — Run + record the M4 acceptance gate (closed-loop / handoff /
# part-vs-whole)

- **Lane:** Events (`M4`)
- **Unit:** U11 (of U00–U12)
- **Kind:** gate (the three-test acceptance gate — no new code, no new
  tests)

## Goal

Run the **three-test acceptance gate** for M4: (1) **closed-loop** —
the `Event` → `EventRsvp` → `EventReminderService` → `IMailerStage`
→ `OutboxEmailHandler` chain works end-to-end; (2) **handoff** — the
`EventService` write lanes (create / edit / publish / delete) hand off
to the `EventController` actions (create / edit / publish / delete)
without data loss; (3) **part-vs-whole** — the `Event` doc is a
**part** of the `EventService` read + write surface, and the
`EventService` is a **whole** in the `Kumunita.Core` bounded context.
The gate reuses the existing test infrastructure (the M3 / M2 gate
shape). **No new code, no new tests** — the gate is a **verification
step** that runs the existing tests and records the results in the
handoff notes.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/m4-events-design.md` §3.8 — the three-test acceptance
   gate (the primary source for this unit).
2. `docs/design/m3-posts-design.md` — the M3 gate shape to mirror (the
   closed-loop / handoff / part-vs-whole tests).
3. `tests/Kumunita.Core.Tests/Events/EventServiceTests.cs` — the seam
   tests (U09's output).
4. `tests/Kumunita.Core.Tests/Events/EventReminderServiceTests.cs` — the
   reminder tests (U09's output).
5. `tests/Kumunita.Web.Tests/Events/EventControllerTests.cs` — the
   controller tests (U10's output).

## Deliverables (≤ 1 file)

1. **`docs/plans-milestones/done/m4/m4-handoff-notes.md`** — the
   `## U11 — acceptance gate` section (the M3 / M2 gate section shape to
   mirror):
   - **Closed-loop** — the `Event` → `EventRsvp` →
     `EventReminderService` → `IMailerStage` → `OutboxEmailHandler`
     chain works end-to-end (the `EventServiceTests` + the
     `EventReminderServiceTests` + the `EventControllerTests` all pass).
   - **Handoff** — the `EventService` write lanes (create / edit /
     publish / delete) hand off to the `EventController` actions
     (create / edit / publish / delete) without data loss (the
     `EventControllerTests` all pass).
   - **Part-vs-whole** — the `Event` doc is a **part** of the
     `EventService` read + write surface, and the `EventService` is a
     **whole** in the `Kumunita.Core` bounded context (the
     `EventServiceTests` all pass).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green (the 14 EventService seam tests + the 5 reminder tests + the 4 adapter tests
  pass).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green (the 12 controller tests + the 4 handler tests + the
  `MilestonesTests` family pass).
- The `## U11 — acceptance gate` section is appended to
  `m4-handoff-notes.md` (the M3 / M2 gate section shape to mirror).
- Handoff note: 6 lines starting `## U11 — acceptance gate` — (a) the
  closed-loop test (the `Event` → `EventRsvp` →
  `EventReminderService` → `IMailerStage` → `OutboxEmailHandler` chain),
  (b) the handoff test (the `EventService` write lanes → the
  `EventController` actions), (c) the part-vs-whole test (the `Event`
  doc is a **part** of the `EventService` read + write surface), (d)
  the test counts (Core.Tests: 29 tests; Web.Tests: 16 tests + the
  `MilestonesTests` family), (e) the gate status (all three tests pass).

## Notes / deviations

- The three-test acceptance gate is the **M3 / M2 gate shape to
  mirror** (the closed-loop / handoff / part-vs-whole tests). **No new
  gate mechanism.**
- The gate reuses the **existing test infrastructure** (the
  `EventServiceTests` + the `EventReminderServiceTests` + the
  `EventControllerTests` all pass). **No new test infrastructure.**
- The gate is a **verification step** that runs the existing tests and
  records the results in the handoff notes. **No new code, no new
  tests.**
