---
id: ch01-prompt
title: "CH01 prompt — Design doc / triage"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH01 — Design doc / triage

## What you are doing

You are the **first working agent** on the **CODE-HYGIENE** lane
(plan: `docs/plans-milestones/plan-code-hygiene.md`). No prior units.

This unit authors the **design doc** — the triage table for every warning,
every ratchet violation, and the 3 known-red tests. Each item gets a
decision (fix / re-baseline / successor-scope) and the unit that owns it.
This is the single source of truth U2–U14 reference. **No code, no build.**

## Deliverables (1 file, new)

`docs/design/code-hygiene-design.md` (~200 lines). Sections:

- `## Context` — the `improve-sec` lane shipped, leaving 165 warnings +
  51 ratchet violations + 1 route conflict. This lane clears the drift.
- `## Scope` — **In:** the 1 route fix, the 4 method splits, the 165
  warning fixes, the re-baseline, the 3 allowlist entries. **Out
  (successor-scope):** the 2 M2 agreement reds.
- `## Triage table` — one row per warning code × file, with the decision
  (fix in U<m> / re-baseline in U13 / successor) and a one-line rationale.
- `## Ratchet re-baseline plan` — the list of items to `--tighten`, the
  ADR annotations to add, the 3 allowlist entries, the 2 stale method
  baselines to remove, the 1 file-growth re-baseline.
- `## Known-reds (successor-scope)` — the 2 M2 agreement reds, named
  verbatim. `CodeCheck_Gate_Passes` auto-clears by U13.
- `## Unit map` — U1–U14, one line each.

## Entry reads (only these)

- `docs/plans-milestones/plan-code-hygiene.md` (the decisions + evidence).
- `dotnet build Kumunita.slnx --no-incremental 2>&1 | grep -oE "warning [A-Z0-9]+:" | sort | uniq -c`
  (the warning code histogram).
- `pwsh -NoProfile -File tools/code/code-check.ps1 2>&1 | head -60` (the
  ratchet violations).
- `docs/dev/testing-and-ci.md` (the known-failure cheatsheet).
- `docs/archive/lanes/improve-sec/improve-sec-handoff-notes.md` (the
  IS43/IS44 sections — the 3 known-reds).
- `tools/code/code-baseline.json` (the `ceilings` block, first 60 lines).

## Exit

File exists with all sections. **No build.**
Handoff note: 5 lines starting `## U1 — design doc`.
