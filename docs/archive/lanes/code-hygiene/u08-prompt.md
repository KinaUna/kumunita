---
id: ch08-prompt
title: "CH08 prompt — Split ImportOwnershipGuard.Check"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH08 — Split `ImportOwnershipGuard.Check`

## What you are doing

You are the **eighth working agent** on the **CODE-HYGIENE** lane.
CH01–CH07 are committed.

This unit splits the **cc-32** method `ImportOwnershipGuard.Check` (over the
15-cc ceiling) into a ≤15-cc public method + extracted private helpers.
**Pure refactoring.**

## The one thing to get right

The method checks ownership of **imported content across multiple types**.
Extract the per-type ownership checks into private helpers. The public
`Check` keeps its name and signature. Target: **cc ≤15**.

Note: the ratchet baseline has a **stale** entry for the *old* `Check`
signature (5-arg). U13 removes the stale entry. This unit splits the
*current* (6-arg) `Check`.

## Entry reads (only these)

- `src/Kumunita.Core/Portability/ImportOwnershipGuard.cs` (the full file;
  the `Check` method is the target — count its cyclomatic complexity).
- `grep -n "ImportOwnershipGuard" tests/Kumunita.Core.Tests/` (the callers /
  tests).
- `grep -n "ScopeRowHoldsSeat" src/Kumunita.Core/Portability/UserPortabilityService.cs`
  (the related method in U9 — confirm the split boundary is not crossed).

## Deliverables (≤ 2 files)

1. `src/Kumunita.Core/Portability/ImportOwnershipGuard.cs` — extract the
   per-type ownership logic into private helpers. The public `Check` keeps
   its name + signature. **No new public seam, no new bounded context.**
2. The test file(s) that test `Check` — update only if needed.

## Exit

`dotnet build` green. The method is cc ≤15.
`dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
green.
Handoff note: 4 lines starting `## U8 — Check split`.
