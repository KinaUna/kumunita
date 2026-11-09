---
id: ch09-prompt
title: "CH09 prompt — Split UserPortabilityService.ScopeRowHoldsSeat"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH09 — Split `UserPortabilityService.ScopeRowHoldsSeat`

## What you are doing

You are the **ninth working agent** on the **CODE-HYGIENE** lane.
CH01–CH08 are committed.

This unit splits the **cc-33** method
`UserPortabilityService.ScopeRowHoldsSeat` (over the 15-cc ceiling) into a
≤15-cc public method + extracted private helpers. **Pure refactoring.**

## The one thing to get right

The method checks whether a **scope row holds a seat** across multiple
scope types. Extract the per-scope-type checks into private helpers. The
public `ScopeRowHoldsSeat` keeps its name and signature. Target: **cc ≤15**.

Note: the ratchet baseline has a **stale** entry for the *old*
`ScopeRowHoldsSeat` signature (5-arg). U13 removes the stale entry. This
unit splits the *current* (6-arg) `ScopeRowHoldsSeat`.

## Entry reads (only these)

- `grep -n "ScopeRowHoldsSeat" -A60 src/Kumunita.Core/Portability/UserPortabilityService.cs`
  (the `ScopeRowHoldsSeat` method — the file is 806 lines, use this targeted
  read, not a full-file read).
- `grep -n "ScopeRowHoldsSeat" tests/Kumunita.Core.Tests/` (the callers /
  tests).
- `grep -n "ScopeRowHoldsSeat" src/Kumunita.Core/Portability/ImportOwnershipGuard.cs`
  (the related method in U8).

## Deliverables (≤ 2 files)

1. `src/Kumunita.Core/Portability/UserPortabilityService.cs` — extract the
   per-scope-type checks into private helpers. The public
   `ScopeRowHoldsSeat` keeps its name + signature. **No new public seam,
   no new bounded context.**
2. The test file(s) — update only if needed.

## Exit

`dotnet build` green. The method is cc ≤15.
`dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
green.
Handoff note: 4 lines starting `## U9 — ScopeRowHoldsSeat split`.
