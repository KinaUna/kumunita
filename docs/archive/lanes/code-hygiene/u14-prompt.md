---
id: ch14-prompt
title: "CH14 prompt — Close: verification + handoff + archive"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH14 — Close: gate verification + handoff + archive

## What you are doing

You are the **fourteenth and final working agent** on the **CODE-HYGIENE**
lane. CH01–CH13 are committed.

This unit **verifies the close criteria**, appends the final handoff section,
and **moves the lane to the archive** (the `improve-sec` close-flip
precedent). It writes no code.

## The one thing to get right

The 2 M2 agreement reds
(`EveryResidentExportType_IsInUserScopeInventory`,
`EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`) are **successor-scope**
— they must remain **red** and must **not** be fixed or skipped here. Only
`CodeCheck_Gate_Passes` should have flipped green (via U13). If **any
other** test is failing, that is new drift — **stop** and record it.

## Entry reads (only these)

- `docs/plans-milestones/in-progress/code-hygiene-handoff-notes.md` (the
  rolling log — U0–U13).
- `docs/plans-milestones/plan-code-hygiene.md` §"Close protocol" (the 6
  close criteria).
- `docs/archive/README.md` (the archive conventions — the folder-name
  shape, the `tier: archive` / `status: archived` front-matter flip).
- `docs/plans-milestones/README.md` (the lane-register layout to update).

## Deliverables (≤ 4 files)

1. `docs/plans-milestones/in-progress/code-hygiene-handoff-notes.md` —
   append `## U14 — close` with: the test totals (Web: **Failed 2** — the
   2 M2 reds only; Core: **Failed 0**), the warning count (0 or ≤5
   documented), the ratchet status (0 violations), the route conflict
   (fixed), and the successor-scope annotation naming the 2 M2 reds + their
   `improve-sec` owners (IS21–IS23 / IS31–IS32).
2. **Move** the lane files to `docs/archive/lanes/code-hygiene/`:
   `plan-code-hygiene.md`, `code-hygiene-handoff-notes.md`, and all
   `u*-prompt.md`. Flip each front-matter to `tier: archive` /
   `status: archived` + bump `updated`.
3. `docs/plans-milestones/README.md` — add the CODE-HYGIENE "shipped" entry
   pointing at the new archive folder (mirror the `improve-sec` /
   `improve-code-3` entries), and note the lane is closed.
4. `docs/design/code-hygiene-design.md` — **stays** in `docs/design/`
   (a design doc, not a lane file). No move.

## Verification (run before writing the close note)

```bash
dotnet build Kumunita.slnx -c Debug
dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll   # expect Failed: 2
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll  # expect Failed: 0
pwsh -NoProfile -File tools/code/code-check.ps1                                  # expect exit 0
npm --prefix e2e run test                                                        # expect 1 passed, 1 skipped
```

## Exit

All 6 close criteria green. The archive folder exists with the lane files.
Handoff note: 6 lines starting `## U14 — close` — the test totals, the
warning count, the ratchet status, the archive path, "lane closed".
