---
id: dx10-prompt
title: "DX10 prompt — tools/check.ps1: the one-command feedback loop"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX10** — branch **C**, unit 1
> (One-entry-point: **DX10** → DX11 → DX12 → DX13 → DX14). DX09 is committed:
> both test assemblies now share one `postgres:18` container per assembly, and
> `devx-check` is green (3 rules, PASS). **DX10 is the one-command loop:**
> `tools/check.ps1` runs build → tsc → web → core → docs → code → devx, prints
> a timing table, and exits non-zero on any FAIL. **The one trap the plan did
> not anticipate:** on `development` right now, `docs-check`, `code-check` and
> the Web suite are **all red** (pre-existing lane drift / known-reds) — so a
> full `check.ps1` run **exits 1**. That is the runner *faithfully surfacing
> real signal*, not a defect, and **DX10 does not fix those gates.** Read
> **Drift fact 1** before you write the runner.
> **Who is it for:** the clean-slate agent starting DX10.
> **When to read the rest:** before writing `check.ps1`.

# DX10 — `tools/check.ps1`

## What you are doing

You are carrying **DX10** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D3** decision row and
the **DX10** unit section first; they are your spec). So far the loop is
hand-assembled: `docs-check.ps1`, `code-check.ps1`, `devx-check.ps1` each live
in `tools/` and are run by hand or by separate CI jobs. **DX10 gives the
maintainer one command** — `pwsh tools/check.ps1` — that runs the whole chain,
times each step, reports PASS/FAIL/SKIPPED with a reason, and exits non-zero if
any step fails. A missing tool (`dotnet`/`node`/`pwsh`/`git`) is a **FAIL with
the install line**, never a silent skip (E6: the sandbox silently skips gates
today because `pwsh` is missing).

**The one thing to get right (the trap):** `check.ps1` is a **runner**, not a
gate. Its job is to run the steps and report the truth. **It is *correct* that a
full run exits non-zero today** — because `docs-check`, `code-check` and the
Web suite are red on `development` *before* this unit touched anything (pre-
existing lane drift + the documented Web known-reds). The only step you can
assert green is **`devx`**. Do **not** edit `docs-check`/`code-check`, the ADR
index, any source file, or any test to force a green (D8: *never edit around a
failing test*; and those reds belong to other lanes' successors). Record the
red steps in the handoff as pre-existing signal the loop now surfaces.

**The per-unit contract (plan D8) binds you:** read only the named entry reads;
edit only the deliverables; run only the named checks (the in-process `-class` /
full-suite runner form, not `dotnet test`); pause and say so in the handoff if an
entry read no longer matches this plan or the plan itself.

## Drift facts you must know (verified 2026-11-15, `development`, post-DX09)

1. **`tools/check.ps1` does not exist — this unit creates it.** No root
   runner, no `Makefile`, no root `package.json` (E5). `tools/` holds five
   subfolders (`code/`, `devx/`, `docs/`, `sec/`, `ux/`) — `check.ps1` goes in
   `tools/` (top level), not a subfolder.
2. **The reds trap (the plan assumed these green):** measured live just now —
   - `pwsh tools/docs/docs-check.ps1` → **exit 1, 5 gates FAIL**: gate 1
     (front-matter missing: 17), 3 (ADR >250 lines: 1), 4 (broken `.md` links:
     17), 5 (living orphans: 7), 6 (archive not frozen: 50 violations).
   - `pwsh tools/code/code-check.ps1` → **exit 1, 33 violations** (new file/view
     over ceiling, class/method over ceiling, boundary violations — e.g.
     `Moderation->Posts:PostService`, `InstanceStatus->Identity:*`).
   - The **Web suite** (the `web` step) → **4 known-reds** (`2 × SecGate.
     DataClassCoverageTests`, `2 × ImproveHarnessTests`) + **1 intermittent
     Sentry flake** (`SentryBreadcrumbPiiTests.InformationLog_IsNotABreadcrumb`,
     passes in isolation). See `improve-devx-handoff-notes.md` (## DX09) and the
     `web-suite-known-reds-a10` repo memory.
   - **Green right now:** `devx-check` (3 rules, PASS) and the **Core suite**
     (2051 tests, 0 failed). `tsc` is in sync (clean `git diff --exit-code --
     src/Kumunita.Web/wwwroot/js`).
   **Consequence:** `pwsh tools/check.ps1` (full) exits **1** today. That is the
   correct, truthful output — do not chase it.
3. **The step order and commands (your spec, verbatim from the plan):**
   1. `build`: `dotnet build Kumunita.slnx -c Debug`
   2. `tsc`: `npm --prefix src/Kumunita.Web run build`, **then**
      `git diff --exit-code -- src/Kumunita.Web/wwwroot/js` (the compiled-JS
      drift check — `tsconfig.json` has `rootDir: client`, `outDir: wwwroot/js`)
   3. `web`: `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.
      Web.Tests.dll`
   4. `core`: `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.
      Core.Tests.dll`
   5. `docs`: `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File
      tools/docs/docs-check.ps1`
   6. `code`: `…-File tools/code/code-check.ps1`
   7. `devx`: `…-File tools/devx/devx-check.ps1`
   These match `.github/workflows/ci.yml` step names (Test Web, Test Core, the
   docs/code gate jobs).
4. **Toolchain (for the "missing tool = FAIL with install line" rule):**
   sandbox has `node v24.21.0`, `npm 12.2.1`, `pwsh`, `git`, and .NET **10.0.400**
   (the `global.json` pin: `sdk.version = 10.0.400`, `rollForward: latestFeature`).
   `src/Kumunita.Web/node_modules` is present. A missing tool must print the
   install line and mark the step FAIL — **never** `Assert.Skip` / silent skip.
5. **Runner form (D8 contract; `docs/dev/testing-and-ci.md`):** the suites are
   the in-process `dotnet exec <dll>` form, **not** `dotnet test`. Container
   startup ~15 s is normal. Timing budget: `core` ≈ 170–205 s (the slow one),
   `web` ≈ 22–24 s + container, `tsc` ≈ 1 s. A **full** run is ~3–4 min — run it
   in the background or expect the timeout; the *named* fast exit below is ~5 s.
6. **House pwsh style (copy from `tools/docs/docs-check.ps1` header):**
   `$ErrorActionPreference = "Stop"`, `Set-StrictMode -Version Latest`, a
   `Find-RepoRoot` that walks up to `Kumunita.slnx`, `[OK]`/`[FAIL]` colour
   coding, exit 0/1. `check.ps1` is **≤ 200 lines** (plan DX10 ceiling).

## DX10 deliverables (`tools/check.ps1` only)

- `tools/check.ps1` (≤ 200 lines), the seven steps in the order above.
- **Flags:** `-Fast` (skips **`core`** — the slow one — but not `web`);
  `-Only <step[,step]>` (e.g. `-Only docs,devx`, `-Only tsc`).
- **Output:** a summary table — `step | seconds | PASS|FAIL|SKIPPED | why`
  (the `why` carries the install line for a missing tool, or the step's own
  failure headline). Exit 1 on any FAIL.
- **A missing tool is a FAIL that prints the install line** — never a silent skip.

## DX10 exit (the named checks)

- `pwsh tools/check.ps1 -Only devx` → runs `devx-check`, reports **PASS**,
  exits **0**. (The one step you can assert green.)
- `pwsh tools/check.ps1 -Only docs,devx` → **runs** (the plan's literal exit):
  reports `docs` **FAIL** (the 5 pre-existing gate reds above) + `devx` **PASS**,
  exits **1**. Confirm the table shows both steps with their seconds and the
  `why`. This is *correct* — the loop is surfacing real, pre-existing signal.
- **A full run** (`pwsh tools/check.ps1`) is recorded in the handoff:
  `build` PASS, `tsc` PASS, `web` FAIL (known-reds), `core` PASS, `docs` FAIL
  (pre-existing), `code` FAIL (pre-existing), `devx` PASS → exit 1. Note the
  total wall-clock and each step's seconds.
- `pwsh tools/check.ps1 -Only devx -Fast` and `-Only tsc` parse and run (the
  flags work).

## Handoff

Append a `## DX10 — check.ps1` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice. It must state: the seven steps + `-Fast`/`-Only`; the **reds
trap** (docs-check 5 gates / code-check 33 / Web known-reds are all pre-existing
on `development`, so a full run exits 1 — that is the runner surfacing real
signal, not a DX10 defect; the only green step is `devx`); `-Only devx` exits 0;
the full-run wall-clock + per-step seconds; and that **no gate or test was
edited to force green** (D8). Cite **ADR 0307** (D3); the plan body's "0300" is
a stale number (consistent with the DX04/DX06/DX08/DX09 handoffs).

## Commit

One commit, conventional-commits + lane tag:

- `feat(devx): DX10 — tools/check.ps1 one-command loop (build→tsc→web→core→docs→code→devx) (IMPROVE-DEVX DX10)`

Stage explicitly (`git add tools/check.ps1` + the handoff note), commit with
`git commit -F <file>` (write the message to `.tmp/dx10-commit-msg.txt` first,
then `rm` it), and end with a clean `git status --short` (the untracked
`dx*-prompt.md` briefs stay untracked — DX37 archives them).

## Definition of Done

1. `tools/check.ps1` exists (≤ 200 lines), runs the seven steps in order with
   `-Fast` (skips `core`) and `-Only <step[,step]>`.
2. A missing tool is a FAIL with the install line — never a silent skip.
3. Output is a summary table (step, seconds, PASS/FAIL/SKIPPED, why); exit 1 on
   any FAIL.
4. `-Only devx` exits 0 (green); `-Only docs,devx` **runs** and exits 1 (docs
   pre-existing red surfaced, devx green); a full run is recorded in the handoff
   with per-step seconds.
5. **No gate, test, or source file was edited to force a green.** The 4 Web
   known-reds + 1 Sentry flake, the 5 docs gates, and the 33 code violations are
   reported as pre-existing signal.
6. `git status --short` shows only `tools/check.ps1` + the handoff note (plus the
   untracked prompt briefs); one commit on `development`.
