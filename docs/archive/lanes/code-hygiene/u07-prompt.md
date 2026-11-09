---
id: ch07-prompt
title: "CH07 prompt — Split RetentionService.RunAsync"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH07 — Split `RetentionService.RunAsync`

## What you are doing

You are the **seventh working agent** on the **CODE-HYGIENE** lane.
CH01–CH06 are committed.

This unit splits the **118-line / cc-38** method
`RetentionService.RunAsync` (over the 80-line / 15-cc ceiling) into a
≤80-line public method + extracted private helpers. **Pure refactoring.**

## The one thing to get right

The method runs retention policies for **multiple doc types**. Extract the
per-type retention logic into private helpers (e.g.
`RunSessionRetentionAsync`, `RunOutboxRetentionAsync`,
`RunNotificationRetentionAsync`). The public `RunAsync` keeps its name and
signature. Target: **≤80 lines, cc ≤15**.

## Entry reads (only these)

- `src/Kumunita.Core/Privacy/RetentionService.cs` (the full file; the
  `RunAsync` method is the target — count its lines).
- `grep -n "RunAsync" tests/Kumunita.Core.Tests/` (the callers / tests).
- `src/Kumunita.Core/Privacy/RetentionOptions.cs` (the options shape — the
  per-type retention configs).

## Deliverables (≤ 2 files)

1. `src/Kumunita.Core/Privacy/RetentionService.cs` — extract the per-type
   retention into private helpers. The public `RunAsync` keeps its name +
   signature. **No new public seam, no new bounded context.**
2. The test file(s) that test `RunAsync` — update only if the split changes
   internal behavior (it shouldn't).

## Exit

`dotnet build` green. The method is ≤80 lines / cc ≤15.
`dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
green.
Handoff note: 4 lines starting `## U7 — RunAsync split`.
