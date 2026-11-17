---
id: dx04-prompt
title: "DX04 prompt — trim ci.yml comment prose to ≤ 20 %, add workflow_dispatch + concurrency, fix the stale AGENTS.md pointer, and land the first devx-check rule (ci.yml triggers vs branch-flow.md)"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
updated: 2026-11-14
---

> **Abstract:** the brief for the agent carrying **DX04** — Phase A, unit 2
> of branch **A** (A: DX03 → DX04 → DX05). DX01 + DX02 + DX03 are committed;
> this is the first unit that touches `.github/workflows/ci.yml`. It does
> four things in one file: collapses `ci.yml`'s **56 %** comment prose to ≤
> **20 %**, adds the two missing top-level keywords (`workflow_dispatch`,
> `concurrency`), fixes one stale pointer, and lands the **first** devx-check
> rule (ci.yml's `on:` branches must match `branch-flow.md`'s declared CI
> branch). Carries the verified drift facts (the ADR is **0307**, not the
> plan's "0300"; `branch-flow.md` has a clause this unit supersedes; actionlint
> is absent but PyYAML is present).
> **Who is it for:** the clean-slate agent starting DX04.
> **When to read the rest:** before editing `ci.yml` or `devx-check.ps1`.

# DX04 — Trim `ci.yml`, add manual dispatch

## What you are doing

You are carrying **DX04** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D1** decision row and
the **DX04** unit section first; they are your spec). Phase 0 (DX01 + DX02)
and DX03 (the `docs/dev/branch-flow.md` rule) are committed. DX04 is the unit
that **meets** that rule: it trims `ci.yml`, adds `workflow_dispatch` +
`concurrency`, and lands the first devx-check rule that holds the two together.

**The per-unit contract (plan D8) binds you:** read only the named entry reads;
edit only the deliverables; run only the named checks; pause and say so in the
handoff if an entry read no longer matches this plan or the plan itself; never
edit around a failing test.

## Drift facts you must know (verified 2026-11-14, `development`, HEAD `976339ec`)

1. **The ADR is 0307, not 0300.** The plan's DX04 text says the rationale
   "moves to ADR 0300 by reference." That number is **taken** by
   `0300-issue-resolved-notification.md`. This lane's ADR is **0307**
   (`docs/adr/0307-developer-feedback-loop.md`, D1 = the branch-flow decision),
   landed by DX02. Point the `ci.yml` reference lines at **ADR 0307 §D1**, not
   0300.
2. **`ci.yml` today: 281 lines, 158 comment lines (56 %).** Target ≤ 20 %. The
   `run:` steps (the three pinned `dotnet exec` gate commands, the readiness /
   signup-gate `::notice::` blocks, the `npm`/`docker` steps) are **code, not
   comments** — do not touch them.
3. **No `workflow_dispatch`, no `concurrency`** — confirmed by grep. Both are
   added by this unit. `on:` today is exactly `push.branches:[release]` +
   `pull_request.branches:[release]` — **keep those**; append `workflow_dispatch:`.
4. **Stale pointer at `ci.yml` line 11:** `# … see AGENTS.md § "Running the
   tests")`. After IMPROVE-DOCS `D06`, that doctrine lives in
   [`docs/dev/testing-and-ci.md`](../../docs/dev/testing-and-ci.md) — the xunit.v3
   runner quirk is there now. Fix the pointer to that path.
5. **`devx-check.ps1` is the DX01 skeleton: "0 rules".** Its header comment says
   "DX04 adds the first (ci.yml triggers vs branch-flow.md)." That is your rule.
   `branch-flow.md` (DX03) declares: "**CI runs on `release` only**" and notes
   "There is no `workflow_dispatch` yet (lands in **DX04**)." After you land,
   that clause is superseded — **do not edit `branch-flow.md`** (it is the
   rule's *source of truth* and not a DX04 deliverable); note it in the handoff
   as a one-line reword to sweep at DX35.
6. **`actionlint` is NOT installed** in the sandbox; **PyYAML 6.0.1 is present.**
   Use the PyYAML parse as the exit check (or `actionlint` if you happen to have it).
7. **Five jobs:** `test`, `docs`, `ux-report`, `code`, `e2e`. The feedback-loop
   rationale (branch flow / CI-on-release) maps to **ADR 0307 §D1**. The
   *pre-existing* pins keep their own home: F14 gate commands →
   `docs/design/prod-hardening-f14-ci.md`; the SHA-pin / least-privilege rationale
   → the SEC lane; the e2e signup-gate precondition →
   `docs/design/prod-hardening-f19-e2e.md`. Do **not** fold those into ADR 0307
   (it does not cover them) and do **not** delete their pointer — collapse each
   multi-line prose block to **one** pointer line to its real home.

## DX04 deliverables

1. **`.github/workflows/ci.yml`** (the only workflow edit):
   - **`on:` block** — keep `push` + `pull_request` on `release`; **append**
     `workflow_dispatch:` (no inputs) so a feature branch can get a CI signal.
   - **Top-level `concurrency`** (sibling of `on`/`permissions`, near the top):
     ```yaml
     concurrency:
       group: ci-${{ github.ref }}
       cancel-in-progress: true
     ```
   - **Comment trim (158 → ≤ 20 % of the file):** replace each job's multi-line
     prose preamble with a **single pointer line** to the home of that rationale —
     `# see ADR 0307 §D1 (branch flow, CI-on-release)` where the rationale is the
     feedback loop, else one line to the existing design / SEC doc (fact 7).
     Keep the load-bearing one-liners (the "three pinned F14 gate commands"
     identity, the `CODE_BASELINE_BASE` + `fetch-depth: 0` note, the e2e
     readiness / signup-gate note). **The `run:` steps and `uses:`/`with:`/`env:`
     are byte-identical before and after.**
   - **Fix line 11's stale pointer** → `docs/dev/testing-and-ci.md` (fact 4).
   - Triggers stay `release` (D1). No new jobs, no changed step order.
2. **`tools/devx/devx-check.ps1`** — append the first rule (a function, per the
   header's own note):
   - extract the branch names under `on.push.branches` + `on.pull_request.branches`
     in `ci.yml` (ignore `workflow_dispatch` — it has no branches);
   - read `docs/dev/branch-flow.md` and extract its declared CI branch
     (the backticked `release` in the "Which branch CI watches" section);
   - call `Check "ci-triggers" "ci.yml on: branches match branch-flow.md's declared CI branch" $match`;
   - bump the summary's `rules landed` count from `0` to `1` (currently
     hardcoded — set it to `1`, or make it count). The rule reads
     `branch-flow.md` at check time, so it needs no baseline entry.
3. **`tools/devx/devx-baseline.json`** — *optional*, for traceability: a
   `ci_triggers` rule entry recording the expected branch (`release`) and
   `gate: "DX04"`. Not required for the rule to pass.
4. **The handoff note** — see below.

**DX04 exit:**
- `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml')); print('YAML OK')"` → `YAML OK`.
- `pwsh tools/devx/devx-check.ps1` → `rules landed : 1` and `result : PASS`.
- Comment ratio ≤ 20 % (measure: `awk '{t++; if($0 ~ /^[[:space:]]*#/)c++} END{printf "%.0f%%\n", c/t*100}' .github/workflows/ci.yml`).
- The 5 jobs' `run:`/`uses:`/`with:`/`env:` lines are **byte-identical** to HEAD (verify with `git diff .github/workflows/ci.yml | grep -E '^[+-][[:space:]]*(run|uses|with|env):'` → empty).

## Handoff note (append, don't create)

`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` already has
`## DX01`, `## DX02`, `## DX03`. Append a `## DX04 — trim ci.yml, add dispatch, land first rule`
section: the files touched; comment ratio before → after (56 % → ?); that the
five `run:` steps are untouched; that the rule now reads `branch-flow.md` (0307 §D1,
not the plan's "0300"); and that `branch-flow.md`'s "workflow_dispatch lands in DX04"
clause is now superseded (one-line reword deferred to DX35). 3–8 lines.

## Commit

One commit, conventional-commits + lane tag:

- `ci(devx): DX04 — trim ci.yml, add workflow_dispatch + concurrency, land first devx-check rule (IMPROVE-DEVX DX04)`

Stage explicitly (`git add .github/workflows/ci.yml tools/devx/devx-check.ps1
tools/devx/devx-baseline.json docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`
— include `devx-baseline.json` only if you edited it), commit with `git commit -F <file>`
(not an inline `-m` long body), and end with `git status --short` clean.

## Definition of Done

1. `ci.yml` — `on:` has `push` + `pull_request` (both `release`) **and**
   `workflow_dispatch`; top-level `concurrency: { group: ci-${{ github.ref }},
   cancel-in-progress: true }` present; comment ratio ≤ 20 %; the line-11 pointer
   now targets `docs/dev/testing-and-ci.md`; the five jobs' `run:`/`uses:`/`with:`/`env:`
   are unchanged.
2. `devx-check.ps1` — `rules landed : 1`, `result : PASS`; the rule compares
   `ci.yml`'s `on:` branches to `branch-flow.md`'s declared CI branch and would
   FAIL if they diverged.
3. YAML parses (PyYAML); no gate command, step order, or trigger branch changed.
4. The handoff note has a `## DX04` section.
5. `git status --short` clean; one commit on `development`.
