---
id: improve-devx-handoff
title: "IMPROVE-DEVX handoff notes — the lane's running log, one section per unit"
tier: living
kind: log
status: current
audiences: [developer, agent]
lane: improve-devx
updated: 2026-11-13
---

> **Abstract:** the IMPROVE-DEVX lane's scratch log — files touched, numbers
> before → after, and anything noticed, one `## DX<n>` section per unit.
> **Who is it for:** the next agent in the lane, and the close reviewer
> (DX35–DX37 read this file). **When to read the rest:** when picking up a
> `DX` unit — start at the newest section.

# IMPROVE-DEVX handoff notes

Lane: IMPROVE-DEVX — an integrative codebase for the people and agents who
maintain it. Plan: [`../plan-improve-devx.md`](../plan-improve-devx.md).
Units append here in order; the lane close (DX37) freezes this file and
moves it to the archive.

## DX01 — port the report to pwsh; add the check skeleton

- **Files:** `tools/devx/devx-report.ps1`, `tools/devx/devx-check.ps1`,
  `tools/devx/devx-baseline.json` (new folder `tools/devx/`).
- **Drift fact 1:** `tools/devx/devx-report.mjs` does not exist — DX00 never
  landed it (the plan's DX01 entry read is stale). The Evidence table in the
  plan is the numbers' source of truth; `devx-report.ps1` is now the live
  baseline generator.
- **Drift fact 2:** ADR 0300 is taken (`0300-issue-resolved-notification.md`;
  index runs to 0306). The ADR for this lane lands as **0307** (DX02).
- **Numbers (plan 2026-11-09 → live 2026-11-13):** IClassFixture files
  181 → 190 (154 Core + 36 Web); test files 381 → 413 (obj/bin excluded);
  root test files 343 → 369; lane-named test files 17 → 16 (one renamed
  between measurements); ADRs 299 → 306; ADRs unreachable by link 104 → 53.
- **Lane-code regex:** the plan's E11 regex is unpinned; `devx-report.ps1`
  carries a candidate (16 451 hits). `devx-baseline.json` keeps the plan's
  3 391 as the shrink-only value until DX32 pins the canonical regex and
  re-baselines.
- **Exit verified:** `devx-report.ps1` prints all Evidence numbers;
  `devx-check.ps1` → "0 rules", exit 0.

## DX02 — ADR 0307: the developer feedback loop

- **Files:** `docs/adr/0307-developer-feedback-loop.md` (new, 72 lines,
  Accepted, records D1–D7); `docs/adr/README.md` index row.
- **Renumber:** the plan's DX02 named "ADR 0300" — that is taken
  (`0300-issue-resolved-notification.md`). The ADR lands as **0307** (next
  free after 0306).
- **Drift fact (index):** the prompt said "append after the 0306 row," but
  the index's actual last row is **0304** — rows 0305 and 0306 are missing
  (pre-existing; `improve-check` gate b already carries them as missing).
  Appended 0307 after the real last row (0304); that reduces gate-b's
  new-missing set from 3 (0305/0306/0307) to 2 (0305/0306). Not mine to
  fix.
- **Exit verified:** docs-check green *for the ADR* — no `0307` line in any
  gate output; the 4 pre-existing reds (archive-stamp, missing front-matter
  on archived prompts, one over-250-line ADR, broken links) are unchanged.

## DX03 — branch flow as a written rule

- **Files:** `docs/dev/branch-flow.md` (new, 56 lines: front-matter + Abstract,
  the three-branch table, CI-on-`release` fact, the pre/post-DX04 signal story,
  owner-ops ledger); `docs/dev/README.md` (sixth table row); `AGENTS.md`
  (one new `Start here` bullet before the `docs/dev/` doctrine bullet).
- **Drift facts relied on:** `ci.yml` has no `workflow_dispatch` yet (the doc
  says "lands in DX04", not "exists"); the `devx` required check is flagged
  in the ledger as joining once DX33 lands.
- **Exit:** docs-check FAIL set byte-for-byte identical to pre-DX03
  (front-matter 17 / ADR-over-250 1 / broken 16 / archive 50 — all
  pre-existing). One nuance: gate 4 names `branch-flow.md` as a target of a
  link *inside the untracked `dx03-prompt.md` brief itself* (its line-94
  README-row example) — pre-existing (present in the stashed-HEAD run),
  resolvable only from `in-progress/`, and not caused by, nor fixable from,
  this unit's deliverables. Gate 5 orphans improved 2 → 1 (the README/AGENTS
  rows de-orphanned the new doc).
