---
id: dx05-prompt
title: "DX05 prompt — add guard-main.yml: a pull_request workflow that fails any PR into main whose head is not release"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX05** — Phase A, unit 3
> (the last on branch **A**: DX03 → DX04 → DX05). DX01–DX04 are committed;
> this unit delivers **one new file**, `.github/workflows/guard-main.yml`,
> that fails a PR into `main` whose source branch is not `release` (the
> CI-side tripwire for the `main` ruleset in ADR 0307 §D1). It touches no
> other file — `branch-flow.md` already names this workflow, and there is no
> devx-check rule or baseline change here.
> **Who is it for:** the clean-slate agent starting DX05.
> **When to read the rest:** before writing `guard-main.yml`.

# DX05 — Guard PRs into `main`

## What you are doing

You are carrying **DX05** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D1** decision row
and the **DX05** unit section first; they are your spec). Phase 0 (DX01–DX02)
and branch A's DX03 + DX04 are committed. DX05 is a **one-file** unit: a new
GitHub Actions workflow that acts as the CI-side guard for the owner-ops
branch rule "`main` accepts PRs from `release` only." It is **separate from
`ci.yml`** — a dedicated tripwire, not a step added to the existing CI.

**The per-unit contract (plan D8) binds you:** read only the named entry
reads; edit only the deliverables; run only the named checks; pause and say
so in the handoff if an entry read no longer matches this plan or the plan
itself; never edit around a failing test.

## Drift facts you must know (verified 2026-11-14, `development`, HEAD `31750df3`)

1. **`guard-main.yml` does not exist yet.** `.github/workflows/` holds only
   `ci.yml`. DX05 creates the second file as a **new, standalone** workflow —
   do **not** edit `ci.yml` (it is touched by DX13 and DX33, and its triggers
   stay `release` per DX04).
2. **`branch-flow.md` already names this workflow.** Its owner-ops ledger
   (added by DX03) reads: "*The CI-side tripwire is `guard-main.yml` (DX05):
   a `pull_request` workflow that fails any PR into `main` whose head is not
   `release`.*" You are **delivering** that line's target state. Do **not**
   edit `branch-flow.md` and do **not** duplicate the line — it is not a DX05
   deliverable.
3. **The trigger is `pull_request` to `main` only** (base branch `main`).
   `github.head_ref` is the **source** branch name (`release`, `development`,
   or any feature branch). The job must **pass iff `head_ref == 'release'`**
   and fail otherwise. (A fork's PR carries its fork-branch name in
   `head_ref`, so the guard holds there too.)
4. **`actionlint` is NOT installed** in the sandbox; **PyYAML 6.0.1 is
   present.** Use the PyYAML parse as the exit check (or `actionlint` if you
   happen to have it).
5. **No devx-check rule and no `devx-baseline.json` change here.** DX04
   (ci-triggers) and DX08 (IClassFixture) are the lane's rule units; DX05
   lands neither. This unit is the workflow file + the handoff note, nothing
   else.
6. **The GitHub confirmation is owner-ops, not yours.** The plan's exit
   includes "the owner confirms on GitHub with a throwaway PR from
   `development`." You **cannot** open PRs from the sandbox — record it as
   owner-ops in the handoff; do not block the unit on it.

## DX05 deliverables

1. **`.github/workflows/guard-main.yml`** (~15 lines). Suggested shape —
   adapt, don't paste blindly, and keep it this small:
   ```yaml
   # Guard main — a PR into `main` must come from `release` (ADR 0307 §D1).
   # The CI-side tripwire for the `main` owner-ops ruleset; see docs/dev/branch-flow.md.
   name: Guard main

   on:
     pull_request:
       branches:
         - main

   # Least-privilege token: read-only (mirrors ci.yml's SEC U25 note).
   permissions:
     contents: read

   jobs:
     guard:
       runs-on: ubuntu-latest
       steps:
         # Skipped (job passes) when the source branch is `release`; runs and
         # fails for any other head — the guard's whole job.
         - name: Require source branch `release`
           if: github.head_ref != 'release'
           run: |
             echo "::error::PRs into main must come from the release branch — see docs/dev/branch-flow.md (ADR 0307 §D1)."
             exit 1
   ```
   - **Pass logic:** with `if: github.head_ref != 'release'`, the failing
     step is *skipped* (→ job passes) for a `release` PR and *runs +
     `exit 1`* (→ job fails) for anything else.
   - **The failure message links `docs/dev/branch-flow.md`** (the plan
     requires it) and names ADR 0307 §D1.
   - **`permissions: contents: read`** (the plan specifies it).
   - Triggers are **`pull_request` / `main` only** — no `push`, no
     `workflow_dispatch`, no `concurrency` needed (there is no long build to
     cancel).

**DX05 exit:**
- `python3 -c "import yaml,sys; d=yaml.safe_load(open('.github/workflows/guard-main.yml')); print('YAML OK', d[True] if True in d else d.get('on'))"` → `YAML OK` with `on: {pull_request: {branches: [main]}}`.
- `git status --short` shows **only** the two expected changes
  (`.github/workflows/guard-main.yml` new; the handoff note modified) —
  **`ci.yml` and `branch-flow.md` are untouched**.

## Handoff note (append, don't create)

`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` already has
`## DX01` … `## DX04`. Append a `## DX05 — guard PRs into main` section: the
one new file; that the pass/fail logic is `head_ref != 'release'` → fail
(skipped for a `release` PR); that `branch-flow.md` already names this
workflow (line ~52) so it was **not** edited; that no devx-check rule /
baseline changed; and that the throwaway-PR confirmation from `development`
is **owner-ops** (deferred). 3–8 lines.

## Commit

One commit, conventional-commits + lane tag:

- `ci(devx): DX05 — guard-main.yml, fail a non-release PR into main (IMPROVE-DEVX DX05)`

Stage explicitly (`git add .github/workflows/guard-main.yml
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (not an inline `-m` long body — write the message to
`.tmp/dx05-commit-msg.txt` first, then `rm` it), and end with
`git status --short` clean (the untracked `dx0N-prompt.md` briefs stay
untracked — the DX37 archive moves them).

## Definition of Done

1. `.github/workflows/guard-main.yml` exists (~15 lines): `on:` =
   `pull_request` on `main` **only**; `permissions: contents: read`; a single
   guard step that is skipped for a `release` source branch and fails for any
   other, with a message that links `docs/dev/branch-flow.md` and names
   ADR 0307 §D1.
2. `ci.yml` and `branch-flow.md` are **unchanged** (not part of this unit).
3. YAML parses (PyYAML); `git status --short` shows only the new workflow +
   the handoff note.
4. The handoff note has a `## DX05` section (logic, the "branch-flow already
   names it" fact, the owner-ops deferred confirmation).
5. `git status --short` clean; one commit on `development`.
