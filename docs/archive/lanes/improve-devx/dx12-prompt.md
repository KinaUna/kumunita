---
id: dx12-prompt
title: "DX12 prompt — VS Code tasks + Copilot pointer: route the loop through check.ps1"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX12** — branch **C**,
> unit 3 (One-entry-point: DX10 → DX11 → **DX12** → DX13 → DX14). DX10 and
> DX11 are committed: `tools/check.ps1` is **259 lines**, its seven steps,
> `-Fast`, `-Only` and the `-Doctor` mode are proven. **DX12 routes the
> maintainer's *IDE* loop through that one entry point** — the VS Code `test`
> task today runs `dotnet test` (the path `testing-and-ci.md` calls broken),
> and `copilot-instructions.md` (41 lines) restates doctrine that already
> lives in `AGENTS.md` + `docs/dev/` (one fact, one place). You (a) change
> the `test` task to call `check.ps1` and add `check` / `check:fast` tasks,
> (b) trim `copilot-instructions.md` to a read-`AGENTS.md` + `docs/dev/` link
> (≤ 15 lines), and (c) **land a devx-check rule that fails if any VS Code
> task runs `dotnet test`** — that gate is what keeps (a) from drifting.
> **The one trap:** the new devx-check rule *catches the current `test`
> task*, so add it **after** you rewrite `tasks.json` — or it goes red the
> moment it lands and you may mistake your own red for a DX12 defect.
> **Who is it for:** the clean-slate agent starting DX12.
> **When to read the rest:** before touching `tasks.json`,
> `copilot-instructions.md`, or `devx-check.ps1`.

# DX12 — VS Code tasks + Copilot pointer

## What you are doing

You are carrying **DX12** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D3** decision row
and the **DX12** unit section first; they are your spec). DX10 gave the
maintainer the one-command loop (`build → tsc → web → core → docs → code →
devx`), but the *IDE* loop they actually use day-to-day (the "Run Test"
command in VS Code) still runs `dotnet test` — the path the testing doc calls
broken — and the Copilot pointer restates doctrine that lives in `AGENTS.md`.
Two seams, each with no test, so each drifts. **DX12 closes both:** route the
IDE loop through `check.ps1`, collapse the Copilot pointer to "read
`AGENTS.md`", and **land the gate that proves the routing stuck** — a
`devx-check` rule that fails if any VS Code task runs `dotnet test`.

**The one thing to get right (the trap):** the new devx-check rule and the
`tasks.json` edit are *one change seen from two sides*. If you land the rule
*before* rewriting the `test` task, `devx-check` goes red on your own
commit — and you may mistake that red for a defect in the rule and "fix" the
rule (or worse, the `tasks.json`) to make it pass. **Do the `tasks.json` edit
first, then add the rule, then prove `-Only devx` is still green.** If the
rule and the edit disagree, the *edit* wins; the rule is there to stop
*future* drift, not to veto the unit that fixes the gap.

**The per-unit contract (plan D8) binds you:** read only the named entry
reads; edit only the two deliverable files (plus the handoff note); run only
the named checks; **never edit around a failing gate**; a gate that fires on
its own work is a *sequencing* problem, not a gate to weaken.

## Drift facts you must know (verified 2026-11-15, `development`, post-DX11)

1. **The current state of the two files:**
   - `.vscode/tasks.json` is **~40 lines**. The `test` task (label `"test"`,
     `command: "dotnet test"`, `group.kind: test`, **default test task**) is
     the one DX12 must rewrite. The `build` task runs `dotnet build` and stays.
     The `run` task (`dotnet run --project src/Kumunita.Web --launch-profile
     http`) stays — it is a *run* task, not a *check* task, and is outside
     DX12's scope. The `ts:build` / `ts:watch` tasks (npm) stay.
   - `.github/copilot-instructions.md` is **41 lines** of restated doctrine.
     The plan's target is **≤ 15 lines**: "read `AGENTS.md`" plus a
     `docs/dev/` link.
2. **The `devx-check.ps1` rule structure you are extending:** the file is
   ~149 lines; it lands **3 rules** today — `ci-triggers` (DX04, line 72),
   `iclassfixture` (DX08, line 105), `iclassfixture-web` (DX09, line 125) —
   each a `Check "gate" "message" $cond` followed by `$ruleCount = N`. Append
   a fourth rule **`vscode-task-dotnet-test`** after the DX09 block, before
   the `--- summary` section. **Do not touch the first 3 rules** (they are
   green and out of DX12's scope). Bump `$ruleCount` to 4.
3. **The exact rule the plan names (D3 / DX12):** *"devx-check fails if any
   VS Code task runs `dotnet test`."* Implement it by scanning the *command*
   strings in `.vscode/tasks.json` (the only VS Code tasks file in this repo
   today — if a new one appears, the rule should cover it; use `Get-ChildItem
   -Recurse` with a `tasks.json` filter from the repo root and exclude
   `node_modules` / `obj` / `bin`). Fail if any `command` value matches the
   plain text `dotnet test` (a substring is enough — `dotnet test` is a
   command, not a word to word-bound; `dotnet test /p:...` would still match
   the substring, which is what we want). The message should name the offending
   task label so a future fix is one hop.
4. **The `-Doctor` mode is already in `check.ps1` (DX11)** — its five checks
   are unrelated to DX12 and should remain green after your change. If a
   sandbox check reds, that is a *sandbox* issue, not a DX12 defect (see
   DX11's handoff for the two failure-mode probes).
5. **The "test" task is the default test task.** When you rewrite its command
   to `pwsh tools/check.ps1 -Only build,web,core`, it becomes the IDE's
   one-command loop — which is the point. Keep `group.kind: test` and
   `isDefault: true` so the IDE still routes "Run Test" to it. (The
   `problemMatcher` can stay `[]` — `check.ps1` already prints a summary
   table; a `$msCompile` matcher would just re-parse build output the step
   already handles.)
6. **The `check` / `check:fast` tasks are *new*.** `check` runs
   `pwsh tools/check.ps1` (the full loop); `check:fast` runs
   `pwsh tools/check.ps1 -Fast` (skips `core`). Both should be `shell` tasks
   with no `group` (they are not the *default* build/test/run — the
   `test` task is). Keep the `problemMatcher: []` (same reason as 5).
7. **`copilot-instructions.md` target content (≤ 15 lines):** one line that
   this file is read by Copilot; one line pointing to `AGENTS.md` as the
   single source of truth; one line pointing to `docs/dev/` for the working
   doctrine (razor-verification, testing-and-ci, live-server-debugging,
   agent-environment-quirks); one line saying "add VS Code- or
   Copilot-specific rules **here**, not in `AGENTS.md` / `docs/dev/`" (keep
   that rule — it is the *only* VS Code-specific content, and it is the file's
   reason to exist). Delete the restated doctrine bullets.
8. **Regression guard (DX10/DX11's greens are yours to keep):** after DX12,
   `-Only devx` must **still exit 0** (now with **4 rules** instead of 3),
   `-Only tsc` must **still PASS**, and `-Doctor` must **still be all OK,
   exit 0** in this sandbox. If `-Only devx` goes red, your new rule is
   catching something in `tasks.json` you missed — fix the `tasks.json`, not
   the rule.

## DX12 deliverables

- **`.vscode/tasks.json`** (only the `test` task rewritten; `check` /
  `check:fast` added; `build` / `run` / `ts:*` untouched). The `test` task's
  command becomes `pwsh tools/check.ps1 -Only build,web,core`.
- **`.github/copilot-instructions.md`** trimmed to ≤ 15 lines per Drift-fact 7.
- **`tools/devx/devx-check.ps1`** gains the `vscode-task-dotnet-test` rule
  (4th rule, appended before the summary; `$ruleCount` → 4). First 3 rules
  untouched.
- **The handoff note** (`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`):
  a `## DX12 — …` section, 3–8 lines, in the established voice.

## DX12 exit (the named checks)

- `pwsh tools/check.ps1 -Only devx` → **PASS, exit 0**, now with **4 rules**
  (the new `vscode-task-dotnet-test` is green because the `test` task no
  longer runs `dotnet test`).
- `pwsh tools/check.ps1 -Only tsc` → **PASS** (DX10 unregressed).
- `pwsh tools/check.ps1 -Doctor` → **all 5 checks OK, exit 0** (DX11
  unregressed).
- **The new rule actually fires when it should:** to prove it, temporarily
  revert the `test` task's command back to `dotnet test` in a scratch copy of
  `tasks.json` (or `git stash` it), run `-Only devx`, confirm it **FAILs** with
  a message naming the `test` task, then restore. **Record this probe in the
  handoff** — the gate must catch the thing it was built to catch.
- `wc -l .github/copilot-instructions.md` → **≤ 15**.
- `pwsh tools/devx/devx-report.ps1` (if available) — the rule count moves
  3 → 4; record it.

## Handoff

Append a `## DX12 — VS Code tasks + Copilot pointer` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice. It must state: the three edits (the `test` task's new
command, the two new `check` / `check:fast` tasks, the `copilot-instructions.md`
trim 41 → ≤ 15 lines); that the **new devx-check rule** (`vscode-task-dotnet-test`)
is the 4th rule and is **green** because the `test` task no longer runs
`dotnet test`; the **failure probe** (reverting the `test` task's command in a
scratch copy → rule FAILs naming the task; restoring → green) and where you
forced it to fire; that **`-Only devx` still exits 0** (now with 4 rules) and
**`-Doctor` still all OK** (DX10/DX11 unregressed); the finished
`copilot-instructions.md` line count. Cite **ADR 0307** (D3); the plan body's
"0300" is a stale number (consistent with the DX04/DX06/DX08/DX09/DX10/DX11
handoffs).

## Commit

One commit, conventional-commits + lane tag:

- `feat(devx): DX12 — VS Code test task → check.ps1; add check / check:fast; trim copilot-instructions.md; land the dotnet-test rule (IMPROVE-DEVX DX12)`

Stage explicitly (`git add .vscode/tasks.json .github/copilot-instructions.md tools/devx/devx-check.ps1 docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with `git commit -F <file>` (write the message to `.tmp/dx12-commit-msg.txt` first, then `rm` it), and end with a clean `git status --short` (the untracked `dx*-prompt.md` briefs stay untracked — DX37 archives them).

## Definition of Done

1. `.vscode/tasks.json`'s `test` task runs `pwsh tools/check.ps1 -Only
   build,web,core`; `check` and `check:fast` tasks exist; `build` / `run` /
   `ts:*` untouched.
2. `.github/copilot-instructions.md` is ≤ 15 lines and points at `AGENTS.md`
   + `docs/dev/` (no restated doctrine).
3. `tools/devx/devx-check.ps1` has a 4th rule `vscode-task-dotnet-test` that
   fails on any `dotnet test` in a VS Code task command; the first 3 rules are
   untouched; `$ruleCount` is 4.
4. **The new rule is verified to fire** (scratch revert of the `test` task's
   command → FAIL naming the task; restore → green) and recorded in the
   handoff.
5. **DX10/DX11 exits unregressed:** `-Only devx` → PASS, exit 0, **4 rules**;
   `-Only tsc` → PASS; `-Doctor` → all OK, exit 0.
6. `git status --short` shows the four staged files (plus the untracked prompt
   briefs); one commit on `development`.
