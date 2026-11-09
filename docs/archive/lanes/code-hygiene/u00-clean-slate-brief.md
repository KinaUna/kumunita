---
id: ch00-clean-slate-brief
title: "CH00 clean-slate brief — first working agent: bootstrap + execute CH01"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH00 — Clean-slate brief (first working agent)

## Who you are

You are the **first working agent** on the **CODE-HYGIENE** lane. You are
starting from a **clean slate** — you have no prior lane context, no memory
of what happened before, no half-finished notes. Everything you need is in
this file + the two files it points at. Do not assume; **read, then act.**

The lane is **new** — nothing after this brief is committed yet. Your job in
this session is exactly **one unit: CH01** (the design doc / triage). CH02
through CH14 are later agents' work. Do not start on them.

## What this lane is (one paragraph)

The `improve-sec` lane shipped on 2026-11-09 and left the codebase in a
**known-red, drift-heavy** state: **0 errors, 165 build warnings, 51
ratchet-gate violations, 1 route-conflict bug, and 3 known-red tests**
(1 of which auto-clears when the ratchet is fixed, 2 of which are
**successor-scope** and must stay red). This lane **clears that drift** so
the next lane starts from a green baseline. It adds **no feature**, re-opens
**no** `improve-sec` decision, and fixes **none** of the 2 successor-scope
red tests. That is the whole scope.

## The three files that are your whole world

Read these, in this order, **before touching anything**:

1. **The register** — `docs/plans-milestones/plan-code-hygiene.md`. This is
   the source of truth: the **Decisions** table (D1–D6), the **Evidence**
   table (E1–E6), the **per-unit contract** (the 8 rules + the test-runner
   form), the 14 unit definitions, and the **Close protocol**. Your unit
   (CH01) is `### U1` in this file. **This file is the spec — obey it, do
   not invent.**
2. **The doctrine** — `docs/dev/testing-and-ci.md`. You need two things from
   it: (a) the **xunit.v3 test-runner quirk** (why `dotnet test` reports
   "Zero tests ran" and the `dotnet exec …dll` form that actually works —
   it's in the register's per-unit contract too), and (b) the **two by-design
   CI gates** (`improve-check.ps1` / the `@media` pin) so you don't misread
   a pin failure as a regression.
3. **The conventions** — `docs/DOCS-CONVENTIONS.md` (front-matter, abstract,
   linking rules). CH01 **writes a doc**, so this applies to you directly.

## The starting state (measured 2026-11-09, `development`)

| Metric | Value | Where it lives |
|---|---|---|
| Build errors | **0** | `dotnet build` |
| Build warnings | **165** (155 nullability CS8xxx + 10 mechanical) | `dotnet build --no-incremental` |
| Ratchet violations | **51** (1 file grew, 2 stale methods, 4 methods over ceiling, 3 edges grew, ~38 new boundaries, 3 allowlist) | `pwsh -NoProfile -File tools/code/code-check.ps1` |
| Route conflict | **1** (ASP0023 — `Dissolve` shadows `ApproveEventRsvp`) | `dotnet build` warning |
| Web.Tests | 1322 / **Failed 3** (2 successor-scope + 1 auto-clears) | `dotnet exec …Web.Tests.dll` |
| Core.Tests | 1901 / **Failed 0** (2 skipped, by design) | `dotnet exec …Core.Tests.dll` |
| e2e | 1 passed / 1 skipped (ADR 0050 gate, by design) | `npm --prefix e2e run test` |

**Re-measure, don't trust this table.** Your first act is to re-run the
build + ratchet gate and confirm the numbers still match (they should — the
tree hasn't changed since 2026-11-09). If a number has drifted, that's the
starting condition, not a failure: note the delta in your handoff section.

## The rules that bind every unit (including CH01)

From the register's **per-unit contract** — internalize all 8 before acting:

1. Never modify a file not in the unit's **Deliverables** list.
2. Never re-open an `improve-sec` decision (D1–D10).
3. Never raise a ceiling number (the ratchet only goes down).
4. Never delete a gate or test to silence a warning.
5. Never introduce a new public seam or bounded context.
6. Entry read reveals drift from the plan → **pause** + record
   `## U<m> — Drift pause` in the handoff note.
7. Build **green** (your unit's warnings gone, 0 errors) **before** the
   handoff note.
8. Handoff note written **before** any follow-up action (commit / next unit).

## Your unit: CH01 (the design doc / triage)

**Goal** (one sentence): author `docs/design/code-hygiene-design.md` — the
triage table for every warning, every ratchet violation, and the 3 known-red
tests, each with a decision (fix / re-baseline / successor-scope) and the
owning unit. This is the single source of truth CH02–CH14 reference. **No
code, no build.**

**Entry reads (only these — do not scan the repo):**
- `docs/plans-milestones/plan-code-hygiene.md` (the register — D1–D6, E1–E6).
- `dotnet build Kumunita.slnx --no-incremental 2>&1 | grep -oE "warning [A-Z0-9]+:" | sort | uniq -c`
  (the **live** warning histogram — your source of truth, not my table).
- `pwsh -NoProfile -File tools/code/code-check.ps1 2>&1 | head -60` (the
  **live** ratchet violations).
- `docs/dev/testing-and-ci.md` §"CI known-failure cheatsheet" +
  §"CI gates and anti-regression pins" (so the 3 known-reds are triaged
  correctly, not "fixed").
- `docs/archive/lanes/improve-sec/improve-sec-handoff-notes.md` — the
  **IS43/IS44** sections only (they name the 3 known-reds verbatim and mark
  the 2 M2 reds successor-scope: IS21–IS23 / IS31–IS32).
- `head -60 tools/code/code-baseline.json` (the `ceilings` + `files` shape —
  so the re-baseline plan is concrete).

**Deliverable (exactly one new file):** `docs/design/code-hygiene-design.md`
(~200 lines) with these sections — this is the full spec, from the register
`### U1`:
- `## Context` — `improve-sec` shipped, leaving 165 warnings + 51 ratchet
  violations + 1 route conflict; this lane clears the drift.
- `## Scope` — **In:** the 1 route fix, the 4 method splits, the warning
  fixes, the re-baseline, the 3 allowlist entries. **Out (successor-scope):**
  the 2 M2 agreement reds (`EveryResidentExportType_IsInUserScopeInventory`,
  `EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`).
- `## Triage table` — one row per warning **code × file**, with the decision
  (fix in U<m> / re-baseline in U13 / successor) + a one-line rationale.
- `## Ratchet re-baseline plan` — the `--tighten` item list, the ADR
  annotations (ADR 0298/0299), the 3 allowlist controllers, the 2 stale
  method baselines to remove, the 1 file-growth re-baseline.
- `## Known-reds (successor-scope)` — the 2 M2 reds verbatim + their
  `improve-sec` owners. Note that `CodeCheck_Gate_Passes` **auto-clears** by
  U13 (it is not a successor-scope red).
- `## Unit map` — CH01–CH14, one line each (unit → files → count it clears).

**Exit:** the file exists with all 6 sections, front-matter per
`docs/DOCS-CONVENTIONS.md` (`tier: living`, `kind: design`, the `related:`
list pointing at the register + `docs/dev/testing-and-ci.md` + the two ADRs).
**No build.** Then append the handoff note (below). **Stop.** Do not start
CH02.

## Handoff note (write it before you exit)

Append a section to
`docs/plans-milestones/in-progress/code-hygiene-handoff-notes.md` (it exists
with a `## U0` seed):

```
## U1 — design doc
- design doc authored: docs/design/code-hygiene-design.md
- live warning histogram: <N codes> (build) / <N> ratchet violations (code-check)
- triage rows: <count>; re-baseline items: <count>; successor-scope reds: 2 (named)
- any drift vs plan: <none | describe>
```

## What "done" looks like (the bar for this session)

- `docs/design/code-hygiene-design.md` exists, all 6 sections, valid
  front-matter, links resolve.
- The triage table's "owning unit" column is consistent with the register's
  U1–U14 (no row points at a unit that doesn't exist).
- The 2 successor-scope reds are named **verbatim** and marked Out-of-scope.
- The handoff note's `## U1` section is appended.
- **You have touched no other file.** (CH01 is docs-only by design.)

If anything in the entry reads contradicts the register (a warning count, a
violation name, a successor-scope marking), **stop and record a Drift pause**
rather than reconciling it yourself — a fresh-context agent with the register
in hand is the right one to arbitrate, not you guessing.
