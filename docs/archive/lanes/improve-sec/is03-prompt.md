---
id: is03-prompt
title: "IS03 prompt — Design doc Part 2b (the 81-row data-class table)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS03 — Design doc Part 2b: the data-class policy table (§2.5, §2.6)

## What you are doing

You are the **third working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00 (plan), IS01 (design
Part 1: Context, Invariants P-1…P-8, register E1–E13) and IS02 (Part 2a:
re-verified pins, the seven fix-shape signatures, test names IS05–IS42,
drift-guard anchors) are committed. Your unit is **IS03** — **exactly one
deliverable**:

**Append Part 2b to `docs/design/improve-sec-design.md`** — §2.5, **one
row per registered doc type (81)** with type, class, erasure, export,
retention key and reason, and §2.6, the export-scope table (which M11
types and fields are stripped in `ContentFree`). **No product code. No
build. No tests.** The only other file that changes in this unit is the
handoff-notes append.

**You are NOT** doing IS04 (the gate harness), IS16+ (ADR 0298, the
`[DataClass]` attribute, annotations), or any phase A–F unit. You do NOT
create ADRs, do NOT touch `Program.cs`, `tools/sec/`, or any file under
`src/` or `tests/`.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- Commit style: `docs(improve-sec): IS03 — <short description>` (conventional
  commit, em dash, no ADR in the subject — none exists yet).
- Read **first**: `AGENTS.md` and `.github/copilot-instructions.md`.
- Read **second**: the **IS03** unit spec under "Phase 0" of
  `docs/plans-milestones/plan-improve-sec.md` — your contract (where the
  prompt and plan differ, the plan wins); the Decisions D2, D3, D6 are the
  defaults every row follows.
- Read **third**: `docs/design/improve-sec-design.md` — Part 1 (register
  E1–E13, invariants) and **Part 2a §2.2** (the `[DataClass]` enums
  `DataClassKind` / `Erasure` / `ExportScope` and the `RetentionOptions`
  keys your table must name).
- Read **fourth**: `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
  — the `## IS01` and `## IS02` sections are your predecessors' notes.

## The work

### 1. Append Part 2b to `docs/design/improve-sec-design.md`

Add a `## Part 2b — the data-class policy table (IS03)` heading, then
§2.5 and §2.6:

**§2.5 — One row per registered doc type (81).** Columns: type · class
(a–e / operational / platform, per `docs/security/data-classes.md`) ·
erasure (`Delete` / `Pseudonymize` / `Keep` + reason) · export
(`Resident` / `InstanceOnly` / `Never`) · retention key (a
`Retention__*` key from §2.2's `RetentionOptions`, or `Keep(reason)` /
none per D6) · reason. Defaults follow D2, D3 and D6, and every F1 §2C
"decision" row is **decided here**:

- Notifications ×4, `DelegationGrant`, `GuestAccess`, `IdentityToken`,
  `SessionDoc` → `Delete`.
- `Report`, `ErrorReport`, `EscalationToken`, `UsageEvent` →
  `Pseudonymize`.
- A resident-owned `Group` → `Keep`, reason "ownership transfers per ADR
  0142".

**Entry reads** (the 81-type inventory): `python3
tools/sec/sec-measure.py --json` — M2 rows only, through `jq '.m2'` (the
65 principal-bearing types); `docs/design/prod-hardening-f1-gdpr-deletion.md`
§2A–§2C (lines 57–135); `docs/security/data-classes.md` (the five classes);
`grep -rn "Schema.For<\|Configure()" src/Kumunita.Core --include="*DocTypes.cs"`
to enumerate the remaining registered types to 81. Read discipline as
always: `grep -n` / `sed -n`, never whole files.

**§2.6 — The export-scope table.** Which M11 types and fields are stripped
in `ContentFree` (drives IS27's filter via `DataClassCatalog`:
`PrivateContent` types, rows with a non-null restrictive `Audience`, and
the §2.6 PII fields blanked on `Profile`). Rows that need the owner stay
`// TODO ❓` — do not decide them.

### 2. Append `## IS03 — Design doc Part 2b` to the handoff notes

3–8 lines in `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`:
the 81-row count and per-class breakdown, the decided F1 §2C rows, the
open `❓` rows and why, and a pointer that IS04 builds the deny-matrix gate
and IS16–IS20 annotate types against §2.5 verbatim.

## Verification (all green before you finish)

- [ ] `dotnet build` is **not** run (design-only unit).
- [ ] `pwsh tools/docs/improve-check.ps1` — **ALL GATES PASS**.
- [ ] The design doc still renders with Part 1's three sections and Part 2a's
      §2.1–§2.4 intact, and Part 2b's §2.5–§2.6 present in order after them.
- [ ] §2.5 has **81 rows**; every principal-bearing type (65) has a
      non-empty erasure value.
- [ ] `git status` shows only `docs/design/improve-sec-design.md` and the
      handoff-notes file modified.

## Definition of done (IS03)

- [ ] Part 2b appended: §2.5 (81 rows, defaults per D2/D3/D6, every F1 §2C
      decision row decided) and §2.6 (the `ContentFree` strip table, owner
      rows as `// TODO ❓`).
- [ ] No type annotated, no code touched, no ADR created — design-only.
- [ ] Handoff note has the `## IS03` section (3–8 lines).
- [ ] `improve-check.ps1` ALL GATES PASS; nothing else in the repo touched.
- [ ] One commit: `docs(improve-sec): IS03 — design doc part 2b (81-row data-class table)`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently, and do not start IS04's work to make it pass.** A failed check is
a recoverable step, not a stopping point: read the error, adjust within the
unit's scope, continue.
