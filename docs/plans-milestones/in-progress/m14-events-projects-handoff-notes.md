# M14 — Living handoff note (scratch tier)

> One `## U#` section per unit, **appended, never rewritten**. This is the
> scratch tier the unit agents read before they start (the register is
> `docs/plans-milestones/plan-m14-events-projects.md`, secondary tier; the
> design doc `docs/design/m14-events-projects-design.md` is the primary
> tier — the only authority after U00). A unit agent appends its own
> `## U#` section at the end when it finishes; it never rewrites an
> earlier section.

## Status

- **M14 scope (locked at U00):** the **two coordination surfaces
  interlock** — a `TodoItem` gains an `EventId?` association (the
  `ProjectId` shape, filter-never-gate) + a reverse read seam
  `ListTodosForEventAsync` + a `set-event` write lane + both-direction
  display links; **and (D4, [PROPOSED — veto before U00])** a
  `VTODO` iCal surface (`TodoIcsWriter` + two `.ics` routes + `kw-l`).
  **Out:** `RRULE` / recurring — **M18's home** (D6, the `Milestones.cs`
  source of truth).
- **Decision record:** ADR 0115 (confirm free first).
- **Unit series:** U00 (design + ADR, the gate) → U01 (field + index +
  seam) → U02 (`set-event`) → U03 (display links) → U04 (picker) → U05
  (D4: `TodoIcsWriter`) → U06 (D4: routes) → U07 (acceptance + parity).
  If D4 is vetoed: U05 + U06 are skipped, U07 keeps its number.

## U00 — design + ADR 0115

_(U00 seeds this section with: the locked D1–D8 or the veto + replacement,
the three acceptance test names, any drift from the register, and
confirmation ADR 0115 was verified free.)_
