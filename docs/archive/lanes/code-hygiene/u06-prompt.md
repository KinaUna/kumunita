---
id: ch06-prompt
title: "CH06 prompt — Split ResidentAccountRowsEraser.EraseAsync"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH06 — Split `ResidentAccountRowsEraser.EraseAsync`

## What you are doing

You are the **sixth working agent** on the **CODE-HYGIENE** lane.
CH01–CH05 are committed.

This unit splits the **142-line / cc-21** method
`ResidentAccountRowsEraser.EraseAsync` (over the 80-line / 15-cc ceiling)
into a ≤80-line public method + extracted private helpers. **Pure
refactoring** — the public signature and behavior are unchanged.

## The one thing to get right

The method erases rows for **multiple doc types**. Extract the per-type
erasure into private helpers (e.g. `EraseIdentityTokensAsync`,
`EraseSessionsAsync`, `EraseNotificationsAsync`). The public `EraseAsync`
keeps its name and signature (callers don't change) and delegates to the
helpers. Target: **≤80 lines, cc ≤15**.

## Entry reads (only these)

- `src/Kumunita.Core/Identity/ResidentAccountRowsEraser.cs` (the full file;
  the `EraseAsync` method is the target — count its lines).
- `grep -n "EraseAsync" tests/Kumunita.Core.Tests/` (the callers / tests
  that pin the behavior).
- `head -20 tools/code/code-baseline.json` (the `ceilings` block — confirm
  80 lines / 15 cc).

## Deliverables (≤ 2 files)

1. `src/Kumunita.Core/Identity/ResidentAccountRowsEraser.cs` — extract the
   per-type erasure into private helpers. The public `EraseAsync` keeps
   its name + signature. **No new public seam, no new bounded context.**
2. The test file(s) that test `EraseAsync` — update only if the split
   changes internal behavior (it shouldn't). If the tests test only the
   public behavior, no change.

## Exit

`dotnet build` green. The method is ≤80 lines / cc ≤15 (count the lines).
`dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
green (0 new failures).
Handoff note: 4 lines starting `## U6 — EraseAsync split`.
