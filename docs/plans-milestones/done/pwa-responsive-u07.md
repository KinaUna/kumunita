# M10 PWA + responsive — U07 · Close the milestone

> **Unit plan (secondary tier, self-contained).** This is the **final**
> unit of M10 (PWA and responsive design). Per the register's convention
> (U07's close is authored directly in the register's `## U07` section —
> no separate in-progress file), this file is the close's own record,
> authored at the close. The register is
> `docs/plans-milestones/plan-m10-pwa-responsive.md`; the scratch
> handoff note is `docs/plans-milestones/m10-pwa-handoff-notes.md`.
> **Atomicity contract:** the doc-parity trio moves together (the house
> contract), exit = **everything green** (both test assemblies in full).
> **Unit-series rule:** never touch files outside your own Deliverables;
> no tests beyond the pinned list; no new seams on frozen interfaces.
>
> **You are the closer.** This unit flips the roadmap state (the
> AGENTS.md doc-parity trio: README ↔ `Milestones.cs` ↔
> `MilestonesTests.cs` move **together**), lands the design-doc drift-log
> fold (the U03 / U04 / U05 mechanism clarifications), writes the
> handoff `## Summary`, and confirms the completed unit plans
> `done/`-archived.

## Goal

Flip the roadmap state and land the doc parity. M10 becomes **done**;
the single-in-progress pointer passes to **M11 (Portability,
import/export)**; M12–M14 remain planned. The U03 / U04 / U05 drift
pauses are folded into the design doc §drift log; the register is
**sealed**.

## Context (the state this close moves)

The roadmap already carried M10 as `StatusNext` ("In progress") — that
flip happened **at milestone kickoff** (the M9 U07 close moved it
there). This unit's job is the **completion** flip: M10 `StatusNext` →
`StatusDone`, M11 `StatusPlanned` → `StatusNext` (the single-in-progress
invariant now points at M11), M12–M14 stay `StatusPlanned`. The
`MilestonesTests` pin that asserted "M10 is the single in-progress
milestone" is retargeted to **M11**, and the shipped-done list gains
M10.

## Entry reads (6)

1. `docs/plans-milestones/done/messaging-u07.md` (the M9 U07 close —
   the exact shape to mirror: the flip's file list, the
   `MilestonesTests` re-pin's exact test-name change, the handoff
   `## Summary`'s shape).
2. `src/Kumunita.Web/Milestones.cs` (the M10 row — the `StatusNext` →
   `StatusDone` flip + the M11 row's `StatusPlanned` → `StatusNext`
   flip).
3. `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the order pin + the
   single-`StatusNext` pin — the re-pin's target).
4. `README.md` (the Roadmap section — the M10 entry's `**In progress.**`
   → `**Done.**` flip + the M11 entry's `**Planned.**` →
   `**In progress.**` flip + the Status line).
5. `docs/STATUS.md` (the "next is M10" line — the flip to "next is M11").
6. `docs/ARCHITECTURE.md` (the value-chain table's M10 row — **no status
   column** in this table, and M10 adds zero Core surface, so no change
   is needed; the register's "if present" wording is the pin).

## Deliverables (6)

- `src/Kumunita.Web/Milestones.cs` — M10 → `StatusDone`; M11 (portability)
  → `StatusNext`; M12–M14 stay `StatusPlanned`.
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — retarget the
  in-progress pin to M11 (the shipped-done list gains M10; the planned
  list is M12–M14); keep the exact-order pin in step (M0…M14). The
  single-`StatusNext` pin is renamed
  `M10_Is_The_Single_InProgress_Milestone_And_M11_Through_M14_Are_Planned`
  → `M11_Is_The_Single_InProgress_Milestone_And_M12_Through_M14_Are_Planned`
  (the M9 U07 precedent's exact test-name change pattern).
- `README.md` — Status line: the "M10 in progress" sentence moves to
  "M11 in progress (portability — import/export); M1–M10 and all named
  lanes are done …"; Roadmap: M10 → `**Done.**` citing ADR 0107; M11 →
  `**In progress.**`.
- `docs/STATUS.md` — the "M9 is done …; next is M10 — PWA and responsive
  design. Then M11–M14" sentence becomes
  "M10 is done — PWA and responsive design (ADR 0107); next is M11 —
  portability (import/export). Then M12–M14 (iCal, logging & analytics,
  Events+Projects integration — see the 'Roadmap' below)."
- `docs/design/m10-pwa-responsive-design.md` — the §drift log fold: the
  U03 (install-label resolution moved server-side), U04 (close-button
  hit-area `::after`-vs-`::before`), and U05 (WYSIWYG 1-column-only)
  mechanism clarifications are folded into the log (chronological
  order: U00, U03, U04, U05, U06, U07); the U07 entry names the one
  **open** drift pause (the `Admin/Platform.cshtml:56` table lacking a
  `.table-responsive` wrap) as carried to the deferred-lane list, and
  seals the log (the milestone's plan is fully archived).
- `docs/ARCHITECTURE.md` — **no change** (the value-chain table carries
  no status column, and M10 adds zero Core surface; the register's "if
  present" wording is the pin). Recorded here so the close is explicit
  that the ARCHITECTURE.md row was read and judged, not skipped.

## Exit

**Everything green.** `dotnet build Kumunita.slnx -c Debug` — 0 errors.
Both test assemblies pass in full:
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
(`MilestonesTests` green — M11 sole in-progress; the 7
`PwaManifestTests` pins green) and
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
(unchanged — M10 adds no Core tests, but the run is part of the green
gate per the register's test contract).

Append a `## U07 — close` + `## Summary` section to the handoff note
(the M9 U07 shape): the milestone flip's file list (the 5 files flipped
+ the 7 unit plans already under `done/`), the `MilestonesTests`
re-pin's exact test name, the deferred-lane list (PWA push, the tablet
pass, offline authenticated pages, per-OS adaptive icons,
`screenshots` / `shortcuts` manifest enrichment — each named, the ADR
0107 Consequences hand-off), the Playwright spec's run-status (authored
+ the author-not-run precedent recorded, the M3 U13 / M4 U11 precedent
— the `kumunita` fixture is a documented throw), and the **M11 pointer**
(the next milestone's register, to be authored when M11 kicks off).
**This `## Summary` is the last line the handoff note receives** —
after it, the milestone is closed.

**Last action:** once the Exit above is satisfied — the green build +
both test assemblies, the five flipped files + the design-doc drift-log
fold, and the `## Summary` appended — confirm this unit's own plan file
is under `done/` (this file), and mark the register
`docs/plans-milestones/plan-m10-pwa-responsive.md` **sealed** (the M9
U07 precedent's "sealed unit register" close, the
`done/messaging-u07.md` shape).
