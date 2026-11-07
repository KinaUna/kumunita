---
id: is02-prompt
title: "IS02 prompt — Design doc Part 2a (pins for phases A, B, D, E)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS02 — Design doc Part 2a: re-verified pins, fix shapes, test names

## What you are doing

You are the **second working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00 (plan + README pointer) and
IS01 (design doc Part 1: Context, Invariants P-1…P-8, 13-row findings
register E1–E13) are committed. Your unit is **IS02** — **exactly one
deliverable**:

**Append Part 2a to `docs/design/improve-sec-design.md`** — the
re-verified file:line pins, the fix shapes with exact public signatures,
the final test-name list, and the drift-guard anchors. **No product code.
No build. No tests.** The only other file that changes in this unit is the
handoff-notes append.

**You are NOT** doing IS03 (Part 2b: the 81-row data-class table), IS04 (the
gate harness), or any phase A–F unit. You do NOT create ADRs 0298/0299
(IS16/IS25), do NOT touch `Program.cs`, `tools/sec/`, or any file under
`src/` or `tests/`.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- Commit style: `docs(improve-sec): IS02 — <short description>` (conventional
  commit, em dash, no ADR in the subject — none exists yet).
- Read **first**: `AGENTS.md` and `.github/copilot-instructions.md` (doctrine:
  closed-loop, handoff test, part-vs-whole, the don't-pause rule).
- Read **second**: `docs/plans-milestones/plan-improve-sec.md` — the
  **IS02** unit spec under "Phase 0" is your contract (where this prompt and
  the plan differ, the plan wins); skim the Decisions (D1–D10) and Evidence
  (E1–E13) tables.
- Read **third**: `docs/design/improve-sec-design.md` — Part 1 (IS01): the
  findings register whose *Where* refs you are re-verifying, and the
  invariants whose units/gates you are pinning.
- Read **fourth**: `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
  — the `## IS01` section is your predecessor's note.

## The work

### 1. Append Part 2a to `docs/design/improve-sec-design.md`

Add a `## Part 2a — pins, fix shapes, test names (IS02)` heading, then
§2.1–§2.4:

**§2.1 — Re-verified register.** For **every one of the 13 rows E1–E13**,
re-check the *Where* file:line refs from Part 1 against the current tree
(using `grep -n` on each named symbol/field, not whole-file reads — the
per-unit read discipline already applies). Record the corrected
file:line for each row. **If a row is already fixed in the tree, mark that
row "n/a (fixed at <commit/observation>)" and name the unit that becomes
"n/a"** — do not fix anything yourself. Entry reads for this unit:

- `grep -n "UseSentry" -A25 src/Kumunita.Web/Program.cs`
- `grep -n "HasBreakGlassAsync" -A30 src/Kumunita.Core/Authorization/AuthorizationService.cs`
- `sed -n 1,60p src/Kumunita.Core/Portability/PortabilityService.cs`
- `ls src/Kumunita.Core/Usage/*Tick.cs src/Kumunita.Core/Media/MediaLifecycleTick.cs`

**§2.2 — Fix shapes and exact new public signatures.** Write each signature
exactly as it will be implemented, so IS05–IS42 implement against them:

- `LogRedact.EmailDomain(string?)`
- `BreakGlassProbe.HasActiveAsync(IQuerySession, string)`
- `PortabilityExportScope { ContentFree, Full }`
- `IPortabilityService.ExportAsync(string actor, PortabilityExportScope scope, CancellationToken ct = default)` — as an overload; the old signature delegates `ContentFree`
- `RetentionOptions` (keys under `Retention__*`, the D6 numbers)
- `RetentionPolicy.CutoffFor(...)`
- `RetentionTick`

For each, one or two lines on the shape (what it returns, who owns it,
which module it lands in), grounded in the entry reads above.

**§2.3 — Final test names.** For **every unit IS05–IS42**, list the final
test names (starting from the seed list in each unit's plan spec, which you
may refine for consistency — but never drop a pin). A unit with no test
records **"no test, because …"** with a reason.

**§2.4 — Drift-guard anchors.** Per unit, the grep-able anchor (public
name, marker string, or test name) that a later gate or reviewer can check
still exists — so a rename does not silently retire a fix.

### 2. Append `## IS02 — Design doc Part 2a` to the handoff notes

3–8 lines in `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`:
what was appended (§2.1–§2.4), how many register rows were corrected vs
confirmed, which rows (if any) are "n/a" and why, anything noticed, and a
pointer that IS03 builds the §2.5 table on top of this.

## Verification (all green before you finish)

- [ ] `dotnet build` is **not** run (design-only unit).
- [ ] `pwsh tools/docs/improve-check.ps1` — **ALL GATES PASS**.
- [ ] The design doc still renders with Part 1's three sections intact, and
    Part 2a's §2.1–§2.4 present in order after them.
- [ ] §2.1 has an entry for **all 13 rows** (confirmed, corrected, or n/a).
- [ ] §2.3 covers **IS05–IS42**, each with a test list or a recorded reason.
- [ ] `git status` shows only `docs/design/improve-sec-design.md` and the
    handoff-notes file modified.

## Definition of done (IS02)

- [ ] Part 2a appended: §2.1 re-verified pins (13/13 rows), §2.2 the seven
      signatures verbatim, §2.3 test names for IS05–IS42, §2.4 anchors.
- [ ] Any already-fixed register row marked n/a with the unit it retires —
      nothing silently fixed by this unit.
- [ ] Handoff note has the `## IS02` section (3–8 lines).
- [ ] `improve-check.ps1` ALL GATES PASS; nothing else in the repo touched.
- [ ] One commit: `docs(improve-sec): IS02 — design doc part 2a (pins, fix shapes, test names)`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently, and do not start IS03's work to make it pass.** A failed check is
a recoverable step, not a stopping point: read the error, adjust within the
unit's scope, continue.
