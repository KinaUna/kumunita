# M25 — Upload limits · rolling handoff notes

> Every unit appends a short `## U#` section before it moves its own plan to
> `done/`. This is the cross-unit memory: read the `## U#` sections before
> yours, append yours after. Do not re-derive what an earlier unit already
> settled (the invariant ids/names, the FACES F1–F10, the seam signatures, the
> pinned test names). This file is created by U1 and moved to `done/m25/` by
> the close unit (U12).

## U1 — Drift pause (BLOCKED — C-UP·6 precondition failed; re-run 2026-10-03)

**Outcome: BLOCKED — the design doc was NOT authored (2nd attempt, 2026-10-03
re-run).** U1 was re-run after M24's close was expected to have landed. Step 0's
C-UP·6 precondition check against `src/Kumunita.Web/Milestones.cs` **still
fails** — M24 has *not* been promoted to `StatusDone` and M25 *not* to
`StatusNext` (the exact same state as the 1st attempt, so the blocker is
unchanged). Per the unit's own rule ("STOP and report BLOCKED … **do not**
author the design doc") and the operator instruction, U1 stops here without
writing `docs/design/m25-upload-limits-design.md`.

**Exact current statuses (read verbatim from `Milestones.cs` on this 2026-10-03 re-run):**
- **M24** ("Storage metrics — an admin view of storage…") = **`StatusNext`**
  (rendered "In progress")
- **M25** ("Upload limits — admin-set per-file size limit and per-user total
  content quota…") = **`StatusPlanned`** (rendered "Planned")

**Why this is a blocker, not a fixable detail:** the C-UP·6 invariant (the
single-in-progress milestone contract, `MilestonesTests.cs`) requires **M24 to
be `StatusDone`** before M25 may begin, and U1's Step 0 expects M24 to already
have been promoted to `StatusDone` and M25 promoted to `StatusNext` by M24's
close unit. Neither is true: **M24 is still `StatusNext` (not `StatusDone`)**
and **M25 is still `StatusPlanned` (not `StatusNext`)**. M24 has therefore not
closed, and this unit is explicitly told *not* to "fix" the milestones itself.

**Nothing was written for the design doc:** no
`docs/design/m25-upload-limits-design.md` was created; the 7 invariants
(C-UP·1–7) and 10 FACES (F1–F10) were **not** authored/frozen. This pause is
recorded *instead of* the normal `## U1 — design doc Part 1` section.

**Unblocked by:** M24's close unit promoting M24 → `StatusDone` **and**
M25 → `StatusNext` in `src/Kumunita.Web/Milestones.cs` (kept in step with
`README.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs`). Once that holds,
re-run U1: Step 0 will pass and U1 authors the design doc Part 1
(Context / Scope / Invariants C-UP·1–7 / FACES F1–F10) and replaces this Drift
pause with the normal `## U1 — design doc Part 1` section.

**Next unit:** `m25-u02.md` (Seams & contracts, design doc Part 2) — **do not
start it**; it depends on U1's design doc Part 1 existing.

## U2 — Drift pause (BLOCKED — C-UP·6 precondition regressed; 2026-10-03)

**Outcome: BLOCKED — Part 2 was NOT authored.** U2's guardrail requires U1's
design doc Part 1 to exist in
`docs/design/m25-upload-limits-design.md` **and** the C-UP·6 single-in-progress
contract to hold (M24 `StatusDone` / M25 `StatusNext`). **Both fail**, exactly
as U1's Drift pause above recorded:
- **Design doc:** `docs/design/m25-upload-limits-design.md` **does not exist**
  (file + folder search returned nothing) — Part 1 was never authored, so the
  frozen ids (C-UP·1–7, F1–F10) this unit is told not to re-derive have no
  canonical source.
- **C-UP·6 (read verbatim from `src/Kumunita.Web/Milestones.cs` on 2026-10-03):**
  **M24 = `StatusNext`** (rendered "In progress") and **M25 = `StatusPlanned`**
  (rendered "Planned") — M24 is not `StatusDone`, M25 is not `StatusNext`.

Per U2's own guardrail ("STOP and report BLOCKED — record `## U2 — Drift
pause` in the handoff note; do not author Part 2"), this unit stopped with
**no files touched beyond this section**: no design doc Part 2, no seam
signatures transcribed, no test names pinned anywhere.

**Unblocked by:** the same two things as U1 — M24's close unit must land
(promote M24 → `StatusDone` **and** M25 → `StatusNext` in
`src/Kumunita.Web/Milestones.cs`, kept in step with `README.md` +
`tests/Kumunita.Web.Tests/MilestonesTests.cs`), **then re-run U1** (authores
Part 1 and replaces its Drift pause), **then re-run U2**.

**Next unit:** `m25-u03.md` (Core doc + doc-surface + boot-path wiring) —
**do not start it**; it depends on U2's Part 2 seams.
