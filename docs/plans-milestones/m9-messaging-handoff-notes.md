# M9 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the register header). Each
entry: files written/touched, decisions locked or refined (with the design-doc
/ ADR section it maps to), anything that drifted from the plan's text, and the
next unit's entry reads.

The register is `docs/plans-milestones/plan-m9-messaging.md`. Each unit ships
its own self-contained plan in `in-progress/messaging-uNN.md`; when a unit is
done its plan file moves to `done/`. The **next unit's** agent reads its own
unit plan + this file's most recent `## U#` section + its entry reads — it does
not re-derive the register.

## Kickoff — M9 inserted as the in-progress milestone

- **What this is.** M9 (Messaging) was inserted as the next milestone by
  shifting the existing tail: PWA and responsive design M9 → **M10**,
  Portability M10 → M11, iCal M11 → M12, Logging and analytics M12 → M13,
  Integration of Events and Projects M13 → **M14**. M9 Messaging is the
  **single** `StatusNext` milestone.
- **Files touched (the doc-parity trio, moved together):**
  - `src/Kumunita.Web/Milestones.cs` — M9 Messaging `StatusNext`; M10–M14
    `StatusPlanned`.
  - `tests/Kumunita.Web.Tests/MilestonesTests.cs` — order pin now runs
    `…M8, M9, M10, M11, M12, M13, M14`; the in-progress test renamed to
    `M9_Is_The_Single_InProgress_Milestone_And_M10_Through_M14_Are_Planned`
    (planned list `M10–M14`).
  - `README.md` — Status line → "M9 in progress (Messaging…)"; Roadmap M9
    entry `**In progress.**` (ADR 0105), tail renumbered to M10–M14.
  - `docs/STATUS.md` — "next is M9 — Messaging … Then M10–M14 (…)".
  - `docs/ARCHITECTURE.md` — value-chain table: M9 messaging row inserted;
    M9–M13 → M10–M14.
- **Verified:** `dotnet build Kumunita.slnx -c Debug` green;
  `Kumunita.Web.Tests` 511/511 (the `MilestonesTests` re-pin included).
- **Not touched (historical records, per the AGENTS.md "don't edit done
  records" discipline):** the M6 bullet's "push / PWA push (M9)" deferral note
  (a done-milestone record), every `done/` handoff note referencing the old
  M9–M13 tail, and ADRs that name "M9" as PWA (ADR 0076/0083/0084 deferrals).
- **ADR number for M9:** **0105** (the index ran 0001–0104; `0105` was free).
  U00 will author `docs/adr/0105-messaging.md` + the `docs/adr/README.md` row.
- **Next:** U00 (see `in-progress/messaging-u00.md`).
