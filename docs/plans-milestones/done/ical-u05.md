# M12 · U05 — Close: acceptance gate recorded + docs flip + register close

> **You are the U05 agent — the last unit.** Read **this file + your
> entry reads** and you can execute. U00–U04 have shipped the design +
> the two routes + the affordances; you record the gate, flip the
> docs, and close the register.

## Goal

The C-M12·7 close (the M11 close precedent, verbatim):

1. **Record the acceptance gate.** Run **both** test assemblies in
   full and append to `docs/design/m12-ical-design.md` a
   `### Run result (M12 acceptance gate — <date>)` section recording
   the three acceptance tests (D7 — the design doc's exact names):
   - **(a) closed loop** — plant an event ⇒ `GET /events/{id}.ics`
     returns 200 `text/calendar` whose `VEVENT` carries its `SUMMARY`
     / `LOCATION` / `DTSTART` (the event the resident sees in-app
     lands in a calendar file).
   - **(b) handoff** — the feed fetch by a grantee *after* the author
     added them to the audience contains the event; *after* removal
     it does not (the strong-consistency handoff **out** of the
     platform — C4 carried to the file form).
   - **(c) part-vs-whole** — the full pinned test list (U01's pure
     pins + U03's composition pin + the Web pins + U04's parity pin)
     passes together with `MilestonesTests` green.
   Record the pass/fail line per test + the totals (the M11 close's
   exact shape).
2. **The docs flip** — all four targets in **this one unit**:
   - `src/Kumunita.Web/Milestones.cs` — M12 → `StatusDone`, M13 →
     `StatusNext`.
   - `README.md` (Roadmap) — M12 done, M13 in progress.
   - `docs/STATUS.md` — the "next is M13" line (the M11 close shape).
   - `docs/ARCHITECTURE.md` — the M12 row's status, if it carries one
     (read to confirm first).
   - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
     single-in-progress re-pin: **M13** becomes the single in-progress
     milestone (`M13_Is_The_Single_InProgress_Milestone_And_M14_Is_Planned`
     — the M10/M11 rename precedent, verbatim from the existing test
     file's shape; the exact-order pin M0…M14 is **unchanged**).
3. **The register close.** Move the six unit plan files
   (`ical-u00.md` … `ical-u05.md`) from
   `docs/plans-milestones/in-progress/` → `docs/plans-milestones/done/`
   (one `Move-Item` per file, a single terminal command — no
   here-strings, per the repo's PowerShell discipline). Append the
   handoff note's `## Summary` (a table of U00–U05: goal + one line
   each + test counts + any drift pauses, resolved). Flip this
   register's header (the `docs/plans-milestones/plan-m12-ical.md`
   file) from "In progress." to "**Done** (closed U05, <date>)".

## Entry reads (5)

1. `docs/design/m12-ical-design.md` — §gate (the three acceptance test
   names + definitions) + §drift-guard (the frozen list to confirm
   untouched).
2. `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` — all
   of `## U00`…`## U04` (the drift pauses to resolve or carry, the
   pinned names to cross-check against the design doc).
3. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the exact current
   shape (the `M12_Is_The_Single_InProgress_Milestone_And_
   M13_Through_M14_Are_Planned` test + the exact-order pin M0…M14 —
   the re-pin's source of truth).
4. `src/Kumunita.Web/Milestones.cs` + `README.md` (Roadmap) +
   `docs/STATUS.md` — the flip targets (the M11 close's exact lines,
   the M10/M11 close precedent).
5. `docs/adr/0112-ical-calendar-export.md` — U00's record (confirm
   its Consequences list matches the design doc's deferred lanes —
   the parity check before the flip).

## Deliverables (7 files + the file moves)

1. `docs/design/m12-ical-design.md` — the `### Run result (M12
   acceptance gate)` section appended.
2. `src/Kumunita.Web/Milestones.cs` — the two status flips.
3. `README.md` — the Roadmap M12/M13 lines.
4. `docs/STATUS.md` — the next-is-M13 line.
5. `docs/ARCHITECTURE.md` — the M12 row, if it carries a status (read
   first; the drift note if it does not).
6. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the re-pin.
7. `docs/plans-milestones/in-progress/m12-ical-handoff-notes.md` — the
   `## Summary` appended.
   **+** the six `ical-u*.md` files moved to
   `docs/plans-milestones/done/` (a `Move-Item` per file, one terminal
   command) **+** the register header flip in
   `docs/plans-milestones/plan-m12-ical.md`.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- **Both** test assemblies green in full:
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  (the `MilestonesTests` re-pin passing is the proof the flip is
  consistent — the single-in-progress is now M13, the exact order
  M0…M14 unchanged) and
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  (the full M12 Core list — U01's pure pins + U03's composition pin —
  passing; `docker container prune` after).
- The six `ical-u*.md` files exist under `docs/plans-milestones/done/`
  and are **gone** from `in-progress/` (the directory holds only the
  handoff note + any in-flight sibling).
- The design doc ends with its `### Run result (M12 acceptance gate)`
  section; the handoff note ends with `## Summary`; the register's
  header reads "Done". **This unit writes the last M12 handoff entry —
  it is for the M13 agent** (logging and analytics); there is no U06.

**Rules of engagement** (restated): never touch files outside these
deliverables + the file moves; the drift-guard's frozen list is
confirmed untouched, not rewritten; no new tests (you **record** the
existing pins' results, you do not add M12 test code); no new
authorization surface or content-decision seam; no new dependency.
