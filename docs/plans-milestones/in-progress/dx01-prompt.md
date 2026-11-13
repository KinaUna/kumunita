---
id: dx01-prompt
title: "DX01 prompt — port the devx report to pwsh + the check skeleton + baseline (DX01), then ADR 0307 (plan's 0300, taken) recording D1–D7 (DX02)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the first working agent on IMPROVE-DEVX — the
> two Phase-0 units (the pwsh report + check skeleton + baseline, then the
> ADR recording D1–D7), with the verified drift facts (the .mjs never landed;
> ADR 0300 is taken → use 0307) and the exit criteria.
> **Who is it for:** the clean-slate agent starting the lane.
> **When to read the rest:** before writing the first `DX` file.

# DX01 — Port the report to pwsh; add the check skeleton

## What you are doing

You are the **first working agent** on the **IMPROVE-DEVX** lane
(plan: `docs/plans-milestones/plan-improve-devx.md` — read it first; it is
the spec). DX00 (the plan itself) is already committed. You carry the
**Phase 0 foundations** — two sealed units, one commit each:

- **DX01** — port the devx report to PowerShell and stand up the check
  skeleton + the shrink-only baseline.
- **DX02** — the ADR recording decisions D1–D7.

This is **tooling + one ADR only**: no product code, no test code, no CI
changes (the `devx` CI job is DX33, far ahead).

**The per-unit contract (plan D8) binds you:** read only the named entry
reads; edit only the deliverables; run only the named checks; pause and say
so in the handoff if an entry read no longer matches this plan or the plan
itself; never edit around a failing test.

## Drift facts you must know (verified 2026-11-13, `development`)

1. **`tools/devx/devx-report.mjs` does not exist — DX00 never landed it.**
   The plan's DX01 entry read for it is stale. The baseline numbers instead
   live in the plan's **Evidence table** (`## Evidence / starting numbers`)
   and its **Cold-start baseline** paragraph. The plan also says the .mjs was
   "the measuring script" — treat the Evidence table as the numbers' source
   of truth and record this drift in the handoff. If you ever *do* find a
   .mjs, compare against it; otherwise the Evidence table is the reference.
2. **ADR 0300 is already taken** (`0300-issue-resolved-notification.md`; the
   index runs to **0306**). The plan's DX02 "ADR 0300" is a stale number.
   **Use the next free number, 0307** (`ls docs/adr/ | sort | tail` to
   re-verify — the house rule is: the number follows the repo head, not the
   plan's draft). Record the renumber in the handoff and the commit message.
3. `pwsh` (7.6.9) and `node` (v24) are available in this sandbox; the house
   gate style to copy is `tools/docs/docs-check.ps1` (header comment,
   `Find-RepoRoot`, `$ErrorActionPreference = "Stop"`,
   `Set-StrictMode -Version Latest`, `[OK]`/`[FAIL]` lines, exit 0/1).
4. **`docs-check` is red at HEAD for reasons that are NOT yours to fix:**
   archive-stamp complaints on `docs/archive/lanes/improve-sec/*` (that lane
   closed without re-stamping its archive files). Pre-existing reds, out of
   scope. Your bar: **no NEW failures** — your new/changed files must not
   appear in any gate output.

## DX01 deliverables

All under `tools/devx/` (new folder), plus the handoff append.

1. **`tools/devx/devx-report.ps1`** — the report, in the house gate style.
   Sections + numbers, taken from the plan's Evidence table (E1–E13) and the
   "Measured fine and kept" line. Each computed number must be *computed*
   (grep/count over the tree), not pasted — the report is the baseline
   generator; the ones that are CI measurements (job seconds, e2e seconds)
   are recorded values with their source (CI run window) in a comment.
2. **`tools/devx/devx-check.ps1`** — the check skeleton. Reads
   `devx-baseline.json`, prints the rule count, **runs zero rules**
   (prints "0 rules"), exits 0. House style; runs from the repo root.
   (Rules land with their fixing units — DX04 and later — so a green
   skeleton is correct, not a hole.)
3. **`tools/devx/devx-baseline.json`** — the E-table numbers, each value
   with its `source` (plan Evidence row) and a `shrink_only: true` marker.
   Must at minimum carry: `iclassfixture_classes` = **347** (147 Core +
   34 Web, E2); `test_files` = **381**, `test_files_at_project_root` =
   **343** (E9); `lane_named_test_files` = **17** (E9); `lane_code_hits` =
   **3391** (E11); `adrs_unreachable_by_link` = **104** of **299** (E12);
   `tracked_out_log_trx_artifacts` = **4** (E12); CI seconds — `build`
   ~72, `core` 391–428, `web` 60–66, `e2e` 127–146, `test_job` 468–580
   (E2, CI runs 2026-10-27…10-31); cold-start baseline — **32 calls /
   7 wrong turns / ~123K tokens** (the DX36 comparison, recorded,
   not shrink-only).
4. **Delete `devx-report.mjs`** — it is absent in this checkout; do nothing
   and note that in the handoff (the plan's "Exit" compares against "the
   .mjs did" — your reference is the Evidence table instead).

**DX01 exit:** `pwsh tools/devx/devx-report.ps1` prints the numbers above;
`pwsh tools/devx/devx-check.ps1` exits 0 and prints "0 rules".

## DX02 deliverables (the ADR)

1. **`docs/adr/0307-developer-feedback-loop.md`**, ≤ 80 lines, from
   `docs/adr/TEMPLATE.md`:
   - front-matter `id: adr-0307`, `tier: reference`, `kind: adr`,
     `status: accepted`;
   - **Status: Accepted**, **Date: 2026-11-13**;
   - **Decision** records D1–D7 in one line each (D8/D9/D10 are unit-shape /
     held decisions — name them in Consequences, not the Decision):
     - **D1** the branch flow: `development` → `release` → `main`; CI stays
       on `release` (push + PR); a guard workflow fails any PR into `main`
       whose head is not `release`; branch protection is owner-ops.
     - **D2** one Postgres container **per test assembly**
       (`[assembly: AssemblyFixture]`), each class keeps a fresh database;
       server-scoped tests stay on a class fixture via a named allowlist.
     - **D3** `tools/check.ps1` as the one entry point (build → tsc → web →
       core → docs → code → devx); a missing tool is a FAIL with the install
       line, never a silent skip; `-Fast`, `-Only`, `-Doctor`.
     - **D4** a **generated** `docs/dev/code-map.md`; `AGENTS.md` links it
       instead of listing contexts; the map is checked for staleness.
     - **D5** the test trees mirror the source folders (context folders in
       Core.Tests, controller folders in Web.Tests); namespaces unchanged.
     - **D6** lane-code ratchet: the count only goes down; new comments cite
       ADR numbers, not unit codes; a generated glossary resolves each code
       prefix to its archived lane.
     - **D7** `devx-check.ps1` (exit 0/1) + `devx-report.ps1` + a
       shrink-only baseline + a `devx` CI job; each rule lands **with the
       unit that fixes its gap**, never before.
   - **Consequences:** one line each — e.g. "CI signals on the merge path
     again (E1 closed)"; "the Core suite target is ≤ 90 s locally (E2)";
     "unit codes remain in comments until the ratchet drives them out —
     the glossary (D6) is the lookup"; "held decisions: the
     `ImproveHarnessTests` pwsh shell-out (D9, `// TODO ❓` at DX13) and the
     translation-surface rename (D10, `// TODO ❓` at DX21)".
2. **The index row** in `docs/adr/README.md`: `| 0307 | … | Accepted |`,
   appended after the `0306` row.

**DX02 exit:** `pwsh tools/docs/docs-check.ps1` — green **for the new ADR**
(no 0307 line in any gate output); the pre-existing archive-stamp reds stay
as they are (fact 4).

## Handoff notes (create, then append)

Create **`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`**
— the lane's scratch log (three-tier contract: plan register /
handoff notes / design docs live elsewhere). Front-matter:
`id: improve-devx-handoff`, `title:`, `tier: living`, `kind: log`,
`status: current`, `lane: improve-devx`, plus a `> **Abstract:**` block
(one sentence + who it is for + when to read it). Then append, per unit:

```
## DX01 — <title>
files, numbers before → after, anything noticed (incl. the two drift facts)
```

3–8 lines per unit. The plan's per-unit contract requires this.

## Commits

Two commits, one per unit, conventional-commits + lane tag, e.g.:

- `feat(devx): DX01 — devx-report.ps1 + devx-check skeleton + shrink-only baseline (IMPROVE-DEVX DX01)`
- `docs(adr): 0307 developer feedback loop (D1–D7) (IMPROVE-DEVX DX02)` — note the 0300→0307 renumber in the body.

Stage explicitly (`git add tools/devx/ …`), commit with `git commit -F <file>`
(not an inline `-m` long body), and end with `git status --short` clean.

## Definition of Done

1. `tools/devx/` has `devx-report.ps1`, `devx-check.ps1`, `devx-baseline.json` — no `.mjs`.
2. `pwsh tools/devx/devx-report.ps1` prints the Evidence numbers; `pwsh tools/devx/devx-check.ps1` → "0 rules", exit 0.
3. `docs/adr/0307-developer-feedback-loop.md` ≤ 80 lines, Accepted, index row present; docs-check adds no new failure for it.
4. The handoff notes exist with `## DX01` and `## DX02` sections (both drift facts recorded).
5. `git status --short` clean; two commits, in order.
