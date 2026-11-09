---
id: ch13-prompt
title: "CH13 prompt — Ratchet re-baseline + allowlist"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH13 — Ratchet re-baseline + allowlist

## What you are doing

You are the **thirteenth working agent** on the **CODE-HYGIENE** lane.
CH01–CH12 are committed (the 4 method splits + all warning fixes are done).

This unit makes the **ratchet gate green**: re-baseline the legitimate
violations, remove the stale entries, add the 3 Marten-allowlist controllers,
and annotate the ADRs. **This is the unit that clears
`CodeCheck_Gate_Passes`.**

## The one thing to get right

The boundary violations are the **legitimate consequence** of the
`improve-sec` decisions (D2 data-classes, D5 decision-free reads, D6
retention) — account deletion, retention, portability, and logging all cross
contexts **by design**. Re-baselining them is correct. Do **not** refactor
them away, do **not** delete the gate. The ratchet doctrine: the baseline
only goes **down** in the sense of *fewer violations*; re-baselining
grandfathered drift with an ADR annotation is the sanctioned path (see
`docs/dev/testing-and-ci.md` §"CI gates and anti-regression pins").

## Entry reads (only these)

- `pwsh -NoProfile -File tools/code/code-check.ps1 2>&1` (the **current**
  violation list — CH06–CH09 removed the 4 method-ceiling violations, so
  the list is shorter than CH01 saw it; use this as the source of truth).
- `head -60 tools/code/code-baseline.json` (the `ceilings` + `files` shape).
- `grep -n "allowlist\|Allowlist" tools/code/code-baseline.json` (the
  allowlist section — or find the allowlist mechanism via
  `grep -rn "controller on Marten without an allowlist" tools/code/`).
- `docs/adr/0298-data-classes-in-code.md` (the ADR to annotate).
- `docs/adr/0299-instance-export-operator-gated.md` (the ADR to annotate).

## Deliverables (≤ 4 files)

1. `tools/code/code-baseline.json` — regenerate with
   `dotnet run --project tools/code/CodeMetrics -- --tighten
   tools/code/code-baseline.json .`, then **verify** the diff: (a) the 2
   stale method entries (`ImportOwnershipGuard.Check` old 5-arg signature,
   `UserPortabilityService.ScopeRowHoldsSeat` old 5-arg signature) are
   **gone**; (b) the split methods from CH06–CH09 are baselined at their
   **new** (lower) line/CC; (c) the 3 grown boundary edges are re-baselined;
   (d) the ~38 new boundary violations are present. If `--tighten` does not
   remove a stale entry, remove it by hand.
2. The **Marten allowlist** (wherever `code-check.ps1` reads it) — add the
   3 controllers: `AdminGuestsController`, `AdminPortabilityController`,
   `ErrorReportAdminController`.
3. `docs/adr/0298-data-classes-in-code.md` — one line: "boundary edges +
   method baselines re-baselined to the current count on 2026-11-09 by
   CODE-HYGIENE U13 (ADR 0298/0299)."
4. `docs/adr/0299-instance-export-operator-gated.md` — the same annotation.

## Exit

`pwsh -NoProfile -File tools/code/code-check.ps1` **exits 0** (0
violations). Handoff note: 5 lines starting `## U13 — re-baseline` — the
violation count before/after (e.g. 51 → 0), the files touched, the ADR
annotations added, "gate is green".
