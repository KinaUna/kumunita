# GA U07 — close: `ARCHITECTURE.md` flip + `## GA — Closed (recorded)` + the
roadmap trio flip + move to `done/`

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-assignment.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Flip the `Identity/` + `UserInfo/` lines in `docs/ARCHITECTURE.md` to record
the GA lane's seams + the `GuardianItem` VM, write the **`## GA — Closed
(recorded)`** section in the design doc, **flip the roadmap trio** (README /
`Milestones.cs` / `MilestonesTests.cs` — the AGENTS.md contract), and **move
the lane's files to `done/`** (the register, the unit plans
`guardian-assignment-uNN-plan.md`, and the handoff notes) — the
loop-closing step (the GU lane's U11 analog).

## Context you need (read these first, in this order)

1. `docs/ARCHITECTURE.md` §2 — the `Identity/` line to extend (add
   `FindSubjectByEmailAsync` — the GA seam, ADR 0038) + the `UserInfo/`
   line to extend (add `GuardianItem` + the
   `MembershipEditorModel.GuardianItems` field). The `Posts/` line is the
   "✓ live" flip precedent. The `Events/`/`Projects/` lines are
   **untouched**.
2. `docs/design/guardian-assignment-design.md` — full. The invariants, the
   FACES, the gate, the drift-guard the close summarizes.
3. `docs/plans-milestones/in-progress/plan-guardian-assignment.md` — this
   register, full. The 7 units (U01–U07) + the scope + the assumptions.
4. `docs/plans-milestones/in-progress/guardian-assignment-handoff-notes.md`
   — all `##` sections U01–U06 wrote. The pass/red counts + any drift
   pauses the close summarizes.
5. `README.md` §Roadmap — the GU line (the precedent for the GA line U07
   appends).
6. `src/Kumunita.Web/Milestones.cs` — the GU line (the precedent for the
   GA line U07 appends — `new("GU", "…", StatusDone)`). The GA line goes
   **after** the `GU` line, **before** `M4`.
7. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the "single-in-progress"
   + "order" assertions. The GA line is `StatusDone` (not `StatusNext`), so
   the "single-in-progress" assertion is **unchanged** (M4 is still the
   single `StatusNext`). The "order" assertion gains the GA line (after GU,
   before M4).
8. `docs/adr/0038-guardian-assignment.md` §E (the non-decisions the close
   names).

## Deliverables (5 files modify + 9 file moves)

### 1. `docs/ARCHITECTURE.md` (modify)

- **The `Identity/` line:** add `FindSubjectByEmailAsync` (the GA seam,
  ADR 0038) to the module's seam list, marked ✓, with a one-line note:
  "the email → subject id resolution (GA, ADR 0038; G-A·2 no-leak shape)".
- **The `UserInfo/` line:** add `GuardianItem` (the GA VM, the Detail view's
  "other guardians" list) + the `MembershipEditorModel.GuardianItems`
  field, marked ✓, with a one-line note: "the assigned guardian's display
  name (GA, ADR 0038; G-A·3 identical-in-kind pin)".
- **`Events/`/`Projects/` lines untouched.**

### 2. `docs/design/guardian-assignment-design.md` (modify)

Append `## GA — Closed (recorded)` at the end of the file:

- The three tests (U07's record — the closed-loop / handoff /
  part-vs-whole gate): the 3 Core tests from U03 + the 5 Web tests from
  U06, their pass/red status (from the handoff notes), and the date.
- The `ARCHITECTURE.md` flip (this unit) — the `Identity/` + `UserInfo/`
  lines updated.
- The ADR 0038 §E non-decisions (each named): no remove path; no
  acceptance/consent step; no bulk assign; no self-assignment (refused, not
  a lane); no second audit verb; no email notification.
- The "GA is closed; the three tests are recorded in §Pinned contract;
  the named deferrals are the ADR 0038 §E list" line.

### 3. `README.md` §Roadmap (modify)

Append the GA line (the GU line's precedent — the "named lane, not a
milestone letter" convention):

```
- **GA** — Guardian assignment — an existing guardian assigns a second
  guardian to a child's account (email-driven; one `IIdentityService` ADD
  + one `GuardianController` action + the Detail view's assign form + ADR
  0038)
```

The status is **done** (the lane is closed in this unit). Append it
**after** the GU line, **before** the M4 line.

### 4. `src/Kumunita.Web/Milestones.cs` (modify)

Append the GA line (the GU line's precedent — the "named lane, not a
milestone letter" convention):

```csharp
new("GA", "Guardian assignment — an existing guardian assigns a second guardian to a child's account (email-driven; one IIdentityService ADD + one GuardianController action + the Detail view's assign form + ADR 0038)", StatusDone),
```

Append it **after** the `GU` line, **before** the `M4` line. The
`MilestonesTests.cs`'s "single-in-progress" + "order" assertions are
updated in the **same commit** (the AGENTS.md contract).

### 5. `tests/Kumunita.Web.Tests/MilestonesTests.cs` (modify)

Update the "order" assertion (the GA line's position: after GU, before
M4). The "single-in-progress" assertion is **unchanged** (M4 is still the
single `StatusNext` — the GA line is `StatusDone`).

### 6. File moves (PowerShell `Move-Item`, one self-contained command)

```powershell
$done = "docs/plans-milestones/done/guardian-assignment"
New-Item -ItemType Directory -Path $done -Force
Move-Item "docs/plans-milestones/in-progress/plan-guardian-assignment.md" $done
Move-Item "docs/plans-milestones/in-progress/guardian-assignment-u01-plan.md" $done
Move-Item "docs/plans-milestones/in-progress/guardian-assignment-u02-plan.md" $done
Move-Item "docs/plans-milestones/in-progress/guardian-assignment-u03-plan.md" $done
Move-Item "docs/plans-milestones/in-progress/guardian-assignment-u04-plan.md" $done
Move-Item "docs/plans-milestones/in-progress/guardian-assignment-u05-plan.md" $done
Move-Item "docs/plans-milestones/in-progress/guardian-assignment-u06-plan.md" $done
Move-Item "docs/plans-milestones/in-progress/guardian-assignment-u07-plan.md" $done
Move-Item "docs/plans-milestones/in-progress/guardian-assignment-handoff-notes.md" $done
```

Confirm `in-progress/` is empty afterward.

## Exit

`ARCHITECTURE.md`'s `Identity/` + `UserInfo/` lines are flipped; the design
doc ends with `## GA — Closed (recorded)`; the README §Roadmap +
`Milestones.cs` + `MilestonesTests.cs` trio is flipped (the GA line is
`StatusDone`, the "single-in-progress" assertion is unchanged, the "order"
assertion gains the GA line); the lane's files are in
`done/guardian-assignment/` and `in-progress/` is empty. The handoff note's
`## Summary` (this unit) is the sole GA→next handoff artifact. **No build**
(docs + moves only). Handoff note: the `## Summary` section is present — a
table of the shipped units (U01–U07) with their one-liner goal + test count
+ any deviations + the ADR 0038 §E deferral list (each named).
