# M3b U10 — E2E spec + acceptance gate record

This is the **execution** plan for M3b unit U10. Three-tier shape M2/M3
held: the sealed unit register (`plan-m3b-moderation.md` § `### U10`), the
design-doc pins (`docs/design/m3b-moderation.md` §2.5 row 14–16, §2.6
G1/G2/G3, §2.7 drift-guard), and this file. When in doubt about **what U10
authors** or **what U10 records**, the sealed register + design doc win
(§2.7 "This file is the contract").

## Deliverables (two files, in order — per plan § U10 Deliverables)

1. **`tests/Kumunita.Web.Tests/e2e-m3.spec.ts` — NEW** (the file the M3
   deferral list *named verbatim*; M3's U10 record + U11's `## Summary`
   both name `e2e-m3.spec.ts`, and the M3b Assumptions + design-doc Scope
   item 6 reuse that name). Not a new `e2e-m3b.spec.ts` — M3b's own plan
   §168–201 + §297 say "path per the existing `tests/Kumunita.Web.Tests/`
   convention — confirm exact location in the entry read before naming it",
   and the existing convention is `e2e-m<milestone>.spec.ts` (e.g.
   `e2e-m2.spec.ts`). The spec's **contract** mirrors `e2e-m2.spec.ts`
   exactly: the `kumunita` fixture is a **documented throw** (not invented
   by U10), and the test bodies pin the frozen selector / route / status
   literals against the six M3b FACES rows (`docs/design/m3b-moderation.
   md` § FACES F1–F6).

   - **`import { test as baseTest, expect, type Page } from '@playwright/
     test';`** (same as `e2e-m2.spec.ts`, which the `testMatch: ['**/*.
     spec.ts']` glob already picks up alongside it).
   - **Three specs** mirroring `e2e-m2.spec.ts:156/202/258` (a. / b. / c.):
     - `a. closed-loop (G1)` — file a report → GlobalAdmin assign →
       unlock → resolve; the two hide/remove lanes; the `POST /posts/{
       id}/replies` route — **one serial test** that exercises all six
       FACES rows in a single Playwright flow (the §2.6 G1
       "closed-loop" pin, verbatim).
     - `b. handoff (G2)` — the `Via = Report` read branch (C-M3b·2): a
       *separate* test that renders a post **before** the
       `ResolveReportAsync` lane's flag-flip, then re-renders after and
       asserts the second render sees the C5 carve-out flipped on
       (the §2.6 G2 "next render sees the flag-flip" pin).
     - `c. per-lane (G3's "per-lane" half)` — one test per FACES row:
       the `FileReportAsync` filing, the two `HidePostAsync` /
       `RemovePostAsync` lanes, the `AssignReportAsync` SoD-denied
       case, the `ResolveReportAsync` flag-flip. Each is an isolated
       test (the §2.6 G3 "part-vs-whole" pin).
   - **Fixture contract (documented throw, same pattern as `e2e-m2.
     spec.ts`)**: the `kumunita` fixture throws a descriptive
     `Error` that names the M3b U10 entry-read list (the six FACES rows;
     the six surfaces in `docs/design/m3b-moderation.md` § Surfaces)
     and points back at `m3b-handoff-notes.md § U10`. U10 **does not
     implement** the fixture — mirroring the M2 U13 / M3 U10
     precedent, and mirroring the sealed-register Exit ("the e2e pass
     count **or** the fixture-throw status if still open").

2. **`docs/design/m3b-moderation.md` — MODIFY** — append
   `### Run result (M3b acceptance gate — 2026-09-12)` **above**
   the current `## Part 1 ends here...` trailer (which is at line 324
   of the design doc). Mirror the M3 shape *verbatim* (`m3-posts-design.
   md` § `Run result (M3 acceptance gate — 2026-09-04)`, lines 697–760):

   - `Command: VS Test Explorer run_tests (filter Project=Kumunita.Core.
     Tests, Project=Kumunita.Web.Tests)` + the Testcontainers note.
   - A two-part **pass counts** line: `Kumunita.Core.Tests 118/118
     passed, 0 failed (13 M3b-pinned + 105 inherited)` · `Kumunita.
     Web.Tests 37/37 passed, 0 failed` · `total 155/155`. The Core
     count is **U9's handoff** (U9's line L1444–L1449: `Kumunita.
     Core.Tests: 118/118 passed, 0 failed`). U10 **re-runs** both
     projects in this unit to confirm (the gate is "verified run, not
     assumed").
   - A **three-test table** mirroring M3's table:
     | # | Test | Evidence (actual test names) |
     |---|------|------------------------------|
     | 1 | **Closed-loop** (the six FACES rows are reachable end-to-end: file → assign → unlock → resolve + hide + remove in a single Playwright flow) | §2.5 rows 1–8 (`ModerationServiceTests.cs`) + rows 9–12 (`PostServiceTests.cs` M3b ADDs), anchored to F1 / F2 / F3 / F4 / F5 / F6 in `docs/design/m3b-moderation.md` § FACES. |
     | 2 | **Handoff** (the `Via = Report` read branch: a *second* render after the flag-flip sees the C5 carve-out flipped on — strong consistency, not a projection) | §2.5 row 3 (`CanReadWithReportAsync_ModeratorWithReport_Allowed_ViaTagIsReport`) + row 7 (`ResolveReportAsync_GlobalAdmin_WritesStatusResolved_FlipsFlagSameTxn`). |
     | 3 | **Part-vs-whole** (the 16-test §2.5 list is the whole; the closed-loop + handoff are the parts; all three — plus M1/M2/M3 anchors — must pass together) | U9's 13 pinned `[Fact]`s (rows 1–13) verbatim, plus the M1- / M2- / M3-inherited anchors re-run unchanged in the same execution, still passing. |
   - An **E2E status** paragraph mirroring M3's, but **M3b-specific**:
     `npx playwright test --list` enumerates **4 M2 specs (e2e-m2.spec
     .ts) + 5 M3b specs (e2e-m3.spec.ts: closed-loop, handoff,
     filing, hide-remove, resolve-SoD)**, the `kumunita` fixture is a
     **documented throw** (same as `e2e-m2.spec.ts:118`), and the
     M2 D2 deviation is **still open** in U10 (U10 re-records rather
     than silently re-defers per M3b plan § U10 Exit + U1's handoff
     entry item 6 + design-design §2.6 G3 note).

## Entry-reads (per plan § U10 Entry list)

- `docs/design/m3b-moderation.md` §2.5 (rows 14–16 are the **Web-layer
  surface tests** U10's gate consumes as *evidence*; rows 1–13 are U9's
  13 tests that pass). §2.6 (G1/G2/G3 + the G3 note on the D2 fixture).
  §2.7 (the drift-guard "this file is the contract" rule).
- `docs/design/m3-posts-design.md` § `Run result (M3 acceptance gate —
  2026-09-04)` — **the shape to mirror verbatim** (the three-test
  table + the E2E-status paragraph + the drift-status paragraph).
- `docs/plans-milestones/m2-handoff-notes.md` lines 239–250 (U13
  e2e authored, paused; the D2 deviation) — the documented-throw
  context.
- `docs/plans-milestones/m3-handoff-notes.md` § `## U10 — gate recorded`
  (U10's own M3 precedent — the M3 shape is the *same* shape M3b
  reuses, with the Core.Tests count updated from 105/105 to
  **U9's 118/118**).
- `tests/Kumunita.Web.Tests/e2e-m2.spec.ts` (the 299-line M2 spec —
  the exact fixture-throw pattern + the three-spec shape U10
  mirrors).
- `tests/Kumunita.Web.Tests/playwright.config.ts` (`testMatch: ['**/
  *.spec.ts']` — a new `e2e-m3.spec.ts` is picked up automatically).
- `tests/Kumunita.Web.Tests/package.json` + `package-lock.json`
  (`@playwright/test 1.62.1` in `devDependencies`, `node_modules/`
  present on-disk).
- `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (U9's 8 tests —
  the *evidence* U10 cites in the gate table row 1) and
  `tests/Kumunita.Core.Tests/PostServiceTests.cs` (U9's 5 M3b ADDs —
  evidence for row 1).

## What NOT to do

- **Do not implement the `kumunita` fixture.** U10's Deliverable #1 is
  the spec's *file* with *documented-throw fixtures*, mirroring U13's
  M2 spec exactly. Implementing the fixture is the M4/M5/M6
  "Playwright runtime" unit's work — same as M3's U10 explicitly
  deferred it ("the unit that lands the runtime records the pass count
  in a later § Run result (M3 e2e — date)").
- **Do not rename §2.5 rows 14–16.** Per §2.7 "This file is the
  contract", U7 (`ModerationController`, U6 reply route) did not land
  the §2.5 rows 14–16 (confirmed by grep — no `PostReply_Controller_
  DelegatesToExistingCreateReplyAsync` or `ModerationController_
  QueueRead` anywhere in `tests/Kumunita.Web.Tests/*.cs`). U10's
  gate-table row 3 cites U9's **13** (rows 1–13) as the "part-vs-whole"
  set — rows 14–16 are the *web-layer* surface tests (their
  §2.5 "Anchored to" column points at the Web controllers, which
  U6/U7 ship; they are **not** part of U9's 13 and **not** part of
  U10's gate-table row 3). Record this in the E2E-status paragraph
  (the "still-open drift" bullet), **not** as a §2.6 drift-pause.
- **Do not rewrite `e2e-m2.spec.ts`.** M2's §2.7 "frozen once written"
  rule applies. U10's spec is a sibling (`e2e-m3.spec.ts` in the same
  folder, picked up by the same `testMatch` glob).
- **Do not run `npx playwright test` to actually execute the spec** —
  that would trigger the documented `kumunita` fixture throw. Run
  `npx playwright test --list` to prove enumerability (the M3
  precedent).
- **Do not touch the design-doc's `## Part 1` or `## Part 2`
  sections.** U10 appends a new top-level `### Run result (M3b
  acceptance gate — 2026-09-12)` section *above* the final
  `*Part 1 ends here...*` trailer at line 324.
- **Do not modify `package.json`.** The `@playwright/test` /
  `typescript` dep is already there (M2 U13's scaffolding). Node 22 +
  npx 11 are available.

## Exit criteria (per plan § U10 Exit)

- `run_build` green on the whole solution (no source change expected
  in U10, so this is a no-op verification — the two files touched
  are a new `.ts` and a `.md`).
- `npx playwright test --list` (from `tests/Kumunita.Web.Tests/`)
  reports **4 M2 specs + 5 M3b specs = 9 total** (M2's three at
  `e2e-m2.spec.ts:156/202/258` + M3b's five). This is the
  "enumerable" half of the M3 precedent's "present and enumerable"
  pin.
- `run_tests` for both test projects passes (re-run of U9's handoff
  counts — **118/118 on `Kumunita.Core.Tests`**, **37/37 on
  `Kumunita.Web.Tests`**, **total 155/155** vs. M3's 105/105 + 37/37
  = 142/142 — the +13 is U9's 13 seam-test ADDs).
- `docs/design/m3b-moderation.md` § `### Run result (M3b acceptance
  gate — 2026-09-12)` landed with the three-test table + the E2E-
  status paragraph (fixture-throw, not pass count, because the M2 D2
  deviation is still open).
- `docs/plans-milestones/m3b-handoff-notes.md` → `## U10` appended,
  with the gate result, the e2e pass count **or** the fixture-throw
  status, the re-runs, and the "still-open drift" bullets **for U11's
  close**.

## Drift note (to be appended to the `## U10` handoff section)

- **§2.5 rows 14–16 unlanded.** U9's two-file deliverable (M3b plan
  § U9, 2 files) covers rows 1–13 **only** — the plan text explicitly
  says "rows 14–16 are U10's / U7's Web-layer surface, *not* U9's
  scope". U7 shipped `ModerationController` / posts surface **without
  landing those 3 tests** (U7's `## U7` section does not list them in
  the test count); the sealed register § U9 says "tests 14–16 are
  U10's / U7's Web-layer surface, *not* U9's scope" — so the register
  leaves them open. U10's gate-table row 3 cites U9's 13 as the
  *part-vs-whole* set (M3's U10 did the same with 18). This is a
  **plan-documentation finding** (M3's U10 precedent for exactly this
  shape), not a §2.6 drift-pause — no frozen pin is broken, and the
  register's own line 1422 "U9's two-file Deliverables — tests
  14–16 are U10's / U7's Web-layer surface" confirms the register
  anticipated this.
