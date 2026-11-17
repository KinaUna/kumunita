---
id: dx03-prompt
title: "DX03 prompt — the branch flow as a written rule (docs/dev/branch-flow.md), the docs/dev/README.md row, and one line in AGENTS.md"
tier: archive
kind: prompt
status: done
audiences: [developer, agent]
lane: improve-devx
---

> **Abstract:** the brief for the agent carrying **DX03** — Phase A, unit 1
> (the written branch-flow rule + the docs/dev index row + the AGENTS.md
> pointer). DX01 + DX02 are already committed; this is the first
> **branch-A** unit (A: DX03 → DX04 → DX05). Carries the verified drift
> facts (branch-flow.md does not exist yet; ci.yml has no
> `workflow_dispatch` — that is DX04; docs-check is red at HEAD for reasons
> that are not yours) and the exit criteria.
> **Who is it for:** the clean-slate agent starting DX03.
> **When to read the rest:** before writing `docs/dev/branch-flow.md`.

# DX03 — Branch flow as a written rule

## What you are doing

You are carrying **DX03** of the **IMPROVE-DEVX** lane (plan:
`docs/plans-milestones/plan-improve-devx.md` — read the **D1** decision row
and the **DX03** unit section first; they are your spec). Phase 0 (DX01 +
DX02) is committed. DX03 is a **docs-only** unit: one new doctrine file, one
index row, one pointer line. **No CI file is touched here** — `ci.yml` is
DX04 (the next unit), and `guard-main.yml` is DX05. You are only *writing the
rule* that DX04/DX05 and DX33 will later be measured against.

**The per-unit contract (plan D8) binds you:** read only the named entry
reads; edit only the deliverables; run only the named checks; pause and say
so in the handoff if an entry read no longer matches this plan or the plan
itself; never edit around a failing test.

## Drift facts you must know (verified 2026-11-14, `development`)

1. **`docs/dev/branch-flow.md` does not exist yet.** It is referenced in the
   plan (D1, DX03, DX05, DX20, DX33) but DX03 is the unit that creates it.
   Nothing links to it yet; you add the first link (the `docs/dev/README.md`
   row).
2. **`ci.yml` has NO `workflow_dispatch` today.** Its `on:` block is
   `push: { branches: [release] }` + `pull_request: { branches: [release] }`
   only. The plan's DX03 text says the doc must describe "how to get a CI
   signal on a feature branch (`workflow_dispatch`, **from DX04**)." Write it
   that way — *coming in DX04*, not as an existing capability. Do **not**
   claim the dispatch trigger exists.
3. **`guard-main.yml` (DX05) and the `devx` CI job (DX33) do not exist
   yet.** The owner-ops ledger you write names both as the *target* state:
   the `main` ruleset (accept PRs from `release` only) and the `release`
   ruleset (require the `test`, `docs`, `code`, `e2e` **and `devx`** checks).
   Note in the ledger that the `devx` check joins the required set once DX33
   lands — do not present it as already required.
4. **docs-check is red at HEAD for reasons that are NOT yours to fix:** the
   four failing gates are archive-stamp violations, missing front-matter on
   archived prompts, one over-250-line ADR, and broken relative links.
   Pre-existing, out of scope. Your bar: **no NEW failure** — `branch-flow.md`
   must not appear in any gate output, and the two link edits (README row,
   AGENTS.md line) must resolve.
5. **`docs/dev/README.md` carries a `| File | What it covers |` table** (five
   rows, `running-locally` through `agent-environment-quirks`). You append a
   sixth row. **`AGENTS.md` has no `## Commands` section yet** (that is DX16)
   — DX03 adds only **one pointer line** to the branch-flow doc, not a
   commands block.

## DX03 deliverables

1. **New `docs/dev/branch-flow.md`, ~40 lines**, from the sibling doctrine
   files' shape (front-matter + `> **Abstract:**` blockquote + body). It
   covers:
   - the three branches and who merges what: `development` (feature work) →
     `release` (the CI + gate gate) → `main` (shipped);
   - that **CI runs on `release`** (push + PR) — the current `ci.yml` `on:`
     block;
   - how to get a CI signal on a feature branch **before DX04 lands** (none
     today) and **after DX04** (`workflow_dispatch` on `ci.yml`);
   - the **owner-ops ledger** (a short list, each item one line):
     - a ruleset on `main` that accepts PRs from `release` only (DX05's
       `guard-main.yml` is the CI-side tripwire; the ruleset is owner-ops);
     - a ruleset on `release` that requires the `test`, `docs`, `code`, `e2e`
       and `devx` checks (`devx` joins once DX33 lands).
   - link the ADR: `../adr/0307-developer-feedback-loop.md` (D1 records this
     decision).
   Front-matter: `id: dev-branch-flow`, `tier: living`, `kind: guide`
   (match the siblings — `testing-and-ci.md`, `running-locally.md`, etc. all
   use `kind: guide`), `status: current`, `audiences: [developer, agent,
   platform-maintainer]`, `lane: improve-devx`, `updated: 2026-11-14`,
   `related:` the plan + ADR 0307. (Gate 1 requires front-matter on every
   doc; the Abstract blockquote matches the sibling files.)
2. **A row in `docs/dev/README.md`** — append after the
   `agent-environment-quirks.md` row:
   `| [`branch-flow.md`](branch-flow.md) | The `development` → `release` →
   `main` flow, which branch CI watches, and the owner-ops branch-protection
   ledger (ADR 0307 D1). |`
3. **One line in `AGENTS.md`** — in the existing `## Start here` bullet list,
   add a single bullet linking `docs/dev/branch-flow.md` ("the branch flow +
   owner-ops ledger, ADR 0307 D1"). Do **not** add a `## Commands` section
   (that is DX16) and do **not** restate the doctrine (one fact, one place).

**DX03 exit:** `pwsh tools/docs/docs-check.ps1` adds **no new failure** —
`branch-flow.md` is not named in any gate output, and neither of the two link
edits is a new broken link. (The four pre-existing reds stay exactly as they
are — fact 4.)

## Handoff note (append, don't create)

`docs/plans-milestones/in-progress/improve-devx-handoff-notes.md` already has
`## DX01` and `## DX02`. Append a `## DX03 — branch flow as a written rule`
section: the three files touched, the two drift facts you relied on (no
`workflow_dispatch` yet; `devx` check lands DX33), and that docs-check added
no new failure. 3–8 lines.

## Commit

One commit, conventional-commits + lane tag:

- `docs(devx): DX03 — branch-flow.md + docs/dev row + AGENTS.md pointer (IMPROVE-DEVX DX03)`

Stage explicitly (`git add docs/dev/branch-flow.md docs/dev/README.md AGENTS.md
docs/plans-milestones/in-progress/improve-devx-handoff-notes.md`), commit with
`git commit -F <file>` (not an inline `-m` long body), and end with
`git status --short` clean.

## Definition of Done

1. `docs/dev/branch-flow.md` exists (~40 lines), front-matter + Abstract,
   names the three branches, the CI-on-`release` fact, the DX04
   `workflow_dispatch` pointer, and the owner-ops ledger (with the `devx`
   check flagged as DX33).
2. `docs/dev/README.md` has the new row; `AGENTS.md` has exactly one new
   pointer line (no `## Commands` section added).
3. `pwsh tools/docs/docs-check.ps1` — no `branch-flow.md` in any gate output;
   the pre-existing reds are unchanged.
4. The handoff note has a `## DX03` section.
5. `git status --short` clean; one commit on `development`.
