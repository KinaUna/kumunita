---
id: dx33-prompt
title: "DX33 prompt — the `devx` CI job (D7, CI half) + the owner-ops ledger firm-up"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX33** —
> **phase G, unit 1** (DX32 is committed, `development` @ `6582d082`;
> `devx-check` is at **13 rules**, rule 13 `lane-codes-ratchet` green).
> DX33 lands **D7's CI half**: a `devx` **job** in `ci.yml` that runs
> `devx-check.ps1` in CI (the gate has 13 rules but **no CI job yet** — it
> runs in `check.ps1` and locally, never on the merge path). Two
> deliverables: the `devx` job, and the owner-ops ledger line in
> `branch-flow.md` that already names `devx` as a required check "once DX33
> lands." **The one drift this brief exists to prevent:** `devx-check` reads
> **no base ref** (unlike the `code` job's ratchet), so the job is shaped
> like the **`docs`** job (pwsh, no .NET, no Docker), *not* the `code` job —
> but the plan still asks for `fetch-depth: 0`, so keep it (harmless, and it
> matches the plan's wording). **Who is it for:** the clean-slate agent
> starting DX33. **When to read the rest:** before touching `ci.yml` or
> `branch-flow.md`.

# DX33 — the `devx` CI job (D7, CI half)

## What you are doing

You are carrying **DX33** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **DX33** unit section
and **D7** *"Gate form"* first; they are your spec). The gate exists and
passes locally (`devx-check`, 13 rules) — but **no CI run has ever executed
it**, which is exactly the *signals without loops* gap this lane closes
(D1/S1). DX33 lands the loop for the devx gate. **Two deliverables:**

1. **`.github/workflows/ci.yml`** — a new `devx` job:
   - `runs-on: ubuntu-latest`;
   - `Checkout` with `fetch-depth: 0` (the plan's wording; `devx-check`
     reads no base ref, so this is belt-and-suspenders — keep it);
   - one step: `pwsh -NoProfile -NonInteractive -ExecutionPolicy Bypass -File
     tools/devx/devx-check.ps1`;
   - **no** `setup-dotnet`, **no** Docker, **no** `env:` block — shaped like
     the existing **`docs`** job (pwsh only), not the `code` job.
   - Place it after the `docs` job, before `ux-report`, so the read-only
     Markdown/pwsh-layer jobs stay grouped.
2. **`docs/dev/branch-flow.md`** — the owner-ops ledger line for `release`
   already reads *"…the `test`, `docs`, `code`, `e2e` **and `devx`** checks
   to pass. `devx` joins the required set once **DX33** lands."* Now that
   DX33 lands, **firm it up**: drop the future-tense "once DX33 lands" and
   state `devx` as a required check (the job exists in `ci.yml`). One or two
   sentences; this is a ledger line, not a restate.

**No** ADR (D7's ADR is ADR 0300, landed at DX02). **No** change to
`devx-check.ps1` (the 13 rules stand). **No** baseline edit. **No** code
changes. The `devx` job *runs* the existing gate; it does not add a rule.

The per-unit contract (plan D8) binds you: read only the named entry reads;
edit only the deliverables; run only the named checks; **never edit around a
failing gate.** The red docs gates are pre-existing (recorded in the
DX23–DX32 handoffs) and are **not** yours to fix.

## Drift facts you must know (verified 2026-11-16, `development` @ `6582d082`, post-DX32)

1. **`ci.yml` has no `devx` job today.** Its jobs are `test`, `docs`,
   `ux-report`, `code`, `e2e` (in that order). `ux-report` is report-only
   (not a required check); the required-checks set is `test`, `docs`, `code`,
   `e2e` — `devx` is the one missing, and this unit adds it. Triggers stay
   `release` (push + PR) + `workflow_dispatch`; `concurrency` and
   `permissions: contents: read` already at the top level — **do not**
   re-declare them per job.
2. **`devx-check` reads no base ref.** Unlike the `code` job (whose ratchet
   needs `CODE_BASELINE_BASE` / `fetch-depth: 0`), none of the 13 devx rules
   reads git history — they parse the baseline JSON and count the live tree.
   So the job needs **no `env:`** and **no `setup-dotnet`**; it is
   structurally the `docs` job (pwsh preinstalled on `ubuntu-latest`, no
   setup step). The plan's `fetch-depth: 0` is kept for parity with its
   wording, not because any rule needs it.
3. **The `branch-flow.md` line already exists** (owner-ops ledger, the
   `release` bullet). It currently says `devx` joins "once DX33 lands." Your
   edit is to flip that to a landed fact. Do **not** add a second ledger line
   or a new section — one fact, one place.
4. **Keep THIS brief link-free — it is the gate-4 trap.** The gate-4 link
   extractor matches a markdown link with a `.md` target. **Backtick paths
   are NOT extracted.** Reference every `.md` / `.yml` file in this brief
   **in backticks only**, never as `[label](path)`. That way this brief adds
   **+1 orphan** (expected) and **zero** broken links.
5. **The `-Only docs` reds are pre-existing and unchanged** (front-matter
   **17**, ADR >250 **1**, broken links **21**, orphans **30** — incl. the
   DX32 brief; archive **50**; `improve-check` gate `b` `0305`/`0306`). After
   this brief lands, orphans go **30 → 31** (this brief adds +1;
   backtick-only, zero broken links). `ci.yml` and `branch-flow.md` are
   already tracked and linked, so they add **no** new orphan.

## DX33 deliverables

- **`.github/workflows/ci.yml`** — the `devx` job (≈ 8 lines, including its
  comment): checkout `fetch-depth: 0`, one `pwsh … tools/devx/devx-check.ps1`
  step. One `# see ADR 0300 §… / DX33` comment line, house style (the
  `docs`/`code` jobs each carry one). Triggers, concurrency, permissions
  unchanged.
- **`docs/dev/branch-flow.md`** — the `release` owner-ops bullet: `devx` is a
  required check (landed), not "once DX33 lands."

## DX33 exit (the named checks)

- **YAML parses** — `actionlint` if available, else a YAML parse (the DX04 /
  DX05 exit convention). No duplicate job key, no broken `with:` block.
- **`devx-check` → PASS, 13 rules, exit 0** (unchanged — the job runs the
  same script; re-run it to prove the gate still passes).
- **`branch-flow.md`** ledger bullet states `devx` as required (landed);
  docs-check stays as it was (the two pre-existing `branch-flow.md` reds, if
  any, are unchanged).
- **`-Only docs` reds pre-existing and unchanged** (front-matter **17**,
  ADR >250 **1**, broken links **21**, orphans **31** — incl. this brief, one
  more than DX32's **30**; archive **50**; `improve-check` gate `b`
  `0305`/`0306`).
- **The job running on `workflow_dispatch` from a branch is OWNER-OPS** — it
  cannot run in the sandbox (no GitHub). Record it as owner-ops in the
  handoff, not as a local check.
- **No ADR, no `devx-check.ps1` edit, no baseline edit, no code changes.**
  `git status` shows changes to `ci.yml`, `branch-flow.md`, and the handoff
  note only.

## Handoff

Append a `## DX33 — the `devx` CI job (D7, CI half)` section (3–8 lines) to
`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` in the
established voice (match the DX25–DX32 sections). It must state: the `devx`
job landed in `ci.yml` (shaped like the `docs` job — pwsh, no .NET, no
Docker; checkout `fetch-depth: 0` per the plan; runs `devx-check.ps1`); the
`branch-flow.md` owner-ops ledger now lists `devx` as a required check
(landed, not "once DX33 lands"); **`devx-check` PASS, 13 rules, exit 0**
(re-verified, unchanged); the `workflow_dispatch` run is owner-ops;
`-Only docs` reds pre-existing (orphans **31** incl. this brief). Cite
**ADR 0307** (D1/D7); the plan body's "0300" is a stale number (consistent
with the DX04–DX32 handoffs).

## Commit

One commit. Stage the 2 changed files + the handoff note:

```
git add .github/workflows/ci.yml docs/dev/branch-flow.md \
      docs/plans-milestones/in-progress/improve-devx-handoff-notes.md
```

Commit with `git commit -F <file>` (write the message to
`.tmp/dx33-commit-msg.txt` first, then `rm` it), and end with a clean
`git status --short` (the untracked `dx*-prompt.md` briefs stay untracked —
DX37 archives them).

Suggested subject:

- `ci(devx): DX33 — the `devx` CI job (runs devx-check on the merge path) + owner-ops ledger firm-up (IMPROVE-DEVX DX33)`

## Definition of Done

1. **`ci.yml` carries a `devx` job** (checkout `fetch-depth: 0`; one `pwsh …
   tools/devx/devx-check.ps1` step; no .NET/Docker/env), grouped with the
   other read-only pwsh-layer jobs; YAML parses.
2. **`branch-flow.md`** owner-ops ledger lists `devx` as a required check
   (landed); the "once DX33 lands" future tense is gone.
3. **`devx-check` → PASS, 13 rules, exit 0** (unchanged — re-verified).
4. **`-Only docs` reds pre-existing** (17 / 1 / 21 / **31** orphans incl. this
   brief / 50; `improve-check` `b` `0305`/`0306`).
5. **No ADR, no `devx-check.ps1` edit, no baseline edit, no code changes.**
   `git status` shows only `ci.yml`, `branch-flow.md`, and the handoff note.
6. One commit on `development`; the handoff section states the job's shape,
   the ledger firm-up, the 13-rule re-verify, and the owner-ops
   `workflow_dispatch` line.
