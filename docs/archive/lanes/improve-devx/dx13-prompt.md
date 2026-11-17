---
id: dx13-prompt
title: "DX13 prompt — the pwsh-shell-out test `// TODO ❓ D9`: move improve-check into the loop, delete the test"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX13** — branch **C**,
> unit 4 (One-entry-point: DX10 → DX11 → DX12 → **DX13** → DX14). DX10–DX12
> are committed: `tools/check.ps1` (259 lines) is proven, the IDE loop routes
> through it, and `devx-check` carries 4 rules. **DX13 resolves the lane's
> D9 `// TODO ❓`** — `tests/Kumunita.Web.Tests/ImproveHarnessTests.cs` (149
> lines) shells out from a *unit test* to two pwsh gate scripts, the one seam
> that couples the .NET test suite to the shell toolchain and *silently
> skips* when `pwsh` is missing. Default (D9): **run `improve-check.ps1` in
> the CI `docs` job and in `check.ps1`, and delete the test** — no `.NET → pwsh`
> coupling, faster Web suite. The other script it called, `code-check.ps1`,
> **already** runs in the CI `code` job and in `check.ps1`'s `code` step, so
> deleting the test loses no gate coverage. **The one trap:** all three gates
> (`docs-check`, `code-check`, `improve-check`) are **red on the live tree**
> (ADR-index drift, boundary violations) — DX13 *wires* `improve-check` into
> the loop, it does **not fix** those reds. A red gate after DX13 is a
> pre-existing signal, not a DX13 defect. **Who is it for:** the clean-slate
> agent starting DX13. **When to read the rest:** before touching `ci.yml`,
> `check.ps1`, or the test file.

# DX13 — the pwsh-shell-out test `// TODO ❓ D9`

## What you are doing

You are carrying **DX13** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D9** decision row and
the **DX13** unit section first; they are your spec). DX10 gave the maintainer
the one-command loop; DX12 routed the IDE loop through it. But the Web test
suite still hides a seam: `ImproveHarnessTests.cs` runs
`tools/docs/improve-check.ps1` **and** `tools/code/code-check.ps1` *from
xunit.v3 facts*, `Assert.Skip`-ing when `pwsh` is not on PATH. That is the
"silently skip in the sandbox" gap (E6) — a gate that a box without pwsh
*quietly drops* instead of failing with the install line. **DX13 removes the
coupling:** `improve-check.ps1` joins the CI `docs` job and `check.ps1`'s
`docs` step (where a missing tool is a hard FAIL, DX10's rule); `code-check.ps1`
needs no new wiring (already in the CI `code` job and `check.ps1`); and the
test file is **deleted**.

**The one thing to get right (the trap):** DX13 *wires a gate into the loop*,
it does **not make that gate green.** `improve-check.ps1` is red today (ADR
index drift: `0305`/`0306` on disk but not in `docs/adr/README.md`), and
`docs-check.ps1` / `code-check.ps1` are red too (5 gates / boundary violations)
— all pre-existing, all outside DX13. After your change, `check.ps1 -Only docs`
runs **two** red scripts and the CI `docs` job stays red. **Do not edit
`improve-check.ps1`, `docs-check.ps1`, `code-check.ps1`, or the ADR index to
make them pass** — that is a different unit's work. Land the wiring, delete the
test, and report the reds as the pre-existing baseline they are.

**The per-unit contract (plan D8) binds you:** read only the named entry reads;
edit only the deliverables (plus the handoff note); run only the named checks;
**never edit around a failing gate.** The red gates here are the *target* of
the wiring, not the defect in your change.

## Drift facts you must know (verified 2026-11-15, `development`, post-DX12)

1. **HEAD is `0f107752` (DX12).** `tools/check.ps1` is **259 lines**; its
   `docs` step (lines 207–208) runs **only** `tools/docs/docs-check.ps1`.
   `devx-check.ps1` has **4 rules** and is **PASS**. `ci.yml`'s `docs` job
   (lines 49–57) runs **only** `docs-check.ps1`; the `code` job (line 92) runs
   `code-check.ps1`. Neither job runs `improve-check.ps1` yet. `ubuntu-latest`
   has pwsh preinstalled — the CI jobs need no pwsh setup step.
2. **`ImproveHarnessTests.cs` is 149 lines with exactly two `[Fact]`s:**
   `ImproveCheck_Gate_Passes` (shells to `improve-check.ps1`, 120 s timeout)
   and `CodeCheck_Gate_Passes` (shells to `code-check.ps1`, 300 s timeout).
   Both `Assert.Skip` when `PwshAvailable()` is false. It is the **only**
   `src/`+`tests/` file that references `ImproveHarness` or `improve-check.ps1`
   (verified by grep). **Delete the whole file** — do not keep one fact and
   drop the other.
3. **`code-check.ps1` is already covered** in CI (the `code` job) and in
   `check.ps1` (the `code` step). So removing its harness fact loses **nothing**
   — the gate is enforced in both places it matters. `improve-check.ps1` is the
   one that is **not** in CI today; DX13 adds it to the `docs` job and the
   `check.ps1` `docs` step. **Net: no gate loses coverage; one `.NET → pwsh`
   seam is removed.**
4. **The live gate states (measured this session, all red — pre-existing, not
   yours to fix):** `improve-check.ps1` → **exit 1**, gate `b` ADR-index drift
   (`0305`, `0306` on disk but not in `docs/adr/README.md`; the DX02 handoff
   records these as pre-existing missing rows). `docs-check.ps1` → **exit 1**
   (5 gates). `code-check.ps1` → **exit 1** (new boundary violations: e.g.
   `InstanceStatus→Notifications:SuppressedAddress`, `Web→Identity:Invitation`,
   `ModerationController` on Marten without an allowlist entry).
5. **`ci.yml` is touched by DX04, DX13, and DX33 — merge those one at a time**
   (the plan's run-order rule). DX13's edit is **only the `docs` job**: add one
   step running `improve-check.ps1` after the existing `Run docs gate` step.
   The DX04 `ci-triggers` rule reads only the `on:` block (untouched by a new
   step), so it stays green. Do **not** touch the `test`/`code`/`e2e` jobs or
   the `on:` block.
6. **D9 is a `// TODO ❓` with a named alternative.** The **default** (what you
   do) is: wire `improve-check.ps1` into the loop + delete the test. If the
   owner has chosen the **alternative** ("keep the test, turn its skip into a
   fail"), do that instead: in `ImproveHarnessTests.cs`, replace each
   `Assert.Skip("pwsh is not on PATH…")` with
   `Assert.Fail("install pwsh: …")` — but **ask the owner first**; the brief
   defaults to the delete path. Do not guess.

## DX13 deliverables

- **`.github/workflows/ci.yml`** — in the **`docs` job only**, add one step
  (after `Run docs gate`) that runs
  `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File tools/docs/improve-check.ps1`.
  No change to the `on:` block, the `test`/`code`/`e2e` jobs, or `workflow_dispatch`.
- **`tools/check.ps1`** — the `docs` step now runs `improve-check.ps1` **as
  well as** `docs-check.ps1` (run both; report each's pass/fail in the summary
  table; the step is FAIL if either is non-zero). Do not add a new step — the
  `docs` step is the "docs-family gates" slot. Keep `-Only docs` and `-Only`
  semantics intact; a missing pwsh is a FAIL with the install line (DX10's rule),
  never a skip.
- **`tests/Kumunita.Web.Tests/ImproveHarnessTests.cs`** — **deleted** (`git rm`).
  No other file references it (drift fact 2).
- **The handoff note** (`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`):
  a `## DX13 — …` section, 3–8 lines, in the established voice.

## DX13 exit (the named checks)

- **`git rm` landed:** `ImproveHarnessTests.cs` is gone;
  `grep -rln "ImproveHarness\|improve-check.ps1" src tests --include=*.cs`
  returns nothing (the only in-repo caller was the deleted file).
- **`ci.yml` still valid** and the `docs` job now runs both `docs-check.ps1`
  and `improve-check.ps1`; PyYAML parse OK (or `actionlint` if available); the
  `on:` block is byte-identical to HEAD (so the DX04 `ci-triggers` rule stays green).
- **`check.ps1 -Only devx` → PASS, exit 0, 4 rules** (DX12 unregressed).
- **`check.ps1 -Only docs`** runs **both** `docs-check.ps1` and
  `improve-check.ps1` and reports each — it will be **FAIL** today because both
  are red (drift fact 4); that is the *expected* pre-existing state, recorded in
  the handoff, **not** a DX13 defect.
- **`check.ps1 -Doctor` → all 5 checks OK, exit 0** (DX11 unregressed).
- **The Web suite no longer contains the two pwsh-shell-out facts.** Run the
  Web suite (in-process dll, per D8); the two `*Gate_Passes` facts are gone.
  If any *other* Web test is red, it is pre-existing and not DX13's to fix —
  record it.

## Handoff

Append a `## DX13 — the pwsh-shell-out test (D9)` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice. It must state: the three edits (the `improve-check.ps1` step
added to the CI `docs` job; the `check.ps1` `docs` step now running
`improve-check.ps1` in addition to `docs-check.ps1`; `ImproveHarnessTests.cs`
deleted); that **no gate loses coverage** because `code-check.ps1` was already in
the CI `code` job + `check.ps1` and `improve-check.ps1` is now in the CI `docs`
job + `check.ps1`; that the **red gates are pre-existing** (name the `improve-check`
ADR-index `0305`/`0306` drift and the fact `docs`/`code`-check are red) and were
**not** fixed; that **`-Only devx` still exits 0 (4 rules)** and **`-Doctor`
still all OK** (DX10/DX11/DX12 unregressed); the Web-suite result. Cite **ADR
0307** (D9); the plan body's "0300" is a stale number (consistent with the
DX04/DX06/DX08/DX09/DX10/DX11/DX12 handoffs).

## Commit

One commit, conventional-commits + lane tag:

- `refactor(devx): DX13 — improve-check into the docs job + check.ps1; delete the pwsh-shell-out harness test (IMPROVE-DEVX DX13)`

Stage explicitly (`git add .github/workflows/ci.yml tools/check.ps1 && git rm tests/Kumunita.Web.Tests/ImproveHarnessTests.cs && git add docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with `git commit -F <file>` (write the message to `.tmp/dx13-commit-msg.txt` first, then `rm` it), and end with a clean `git status --short` (the untracked `dx*-prompt.md` briefs stay untracked — DX37 archives them).

## Definition of Done

1. The CI `docs` job runs both `docs-check.ps1` and `improve-check.ps1`; the
   `on:` block and the `test`/`code`/`e2e` jobs are byte-identical to HEAD.
2. `check.ps1`'s `docs` step runs `improve-check.ps1` in addition to
   `docs-check.ps1`; `-Only devx` still PASSes (4 rules); `-Doctor` still all OK.
3. `ImproveHarnessTests.cs` is deleted; nothing in `src/` or `tests/` references
   `ImproveHarness` or `improve-check.ps1`.
4. No gate coverage was lost: `code-check.ps1` remains in the CI `code` job +
   `check.ps1`; `improve-check.ps1` is now in the CI `docs` job + `check.ps1`.
5. The red gates are reported as pre-existing (not fixed, not hidden).
6. `git status --short` shows the changed/deleted files (plus the untracked
   prompt briefs); one commit on `development`.
