---
id: ch11-prompt
title: "CH11 prompt — Nullability batch 2 (Posts/Portability/Todos)"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH11 — Nullability batch 2 (src: Posts/Portability/Todos)

## What you are doing

You are the **eleventh working agent** on the **CODE-HYGIENE** lane.
CH01–CH10 are committed.

This unit fixes the **nullability warnings** in 3 src files:
`PostService.cs` (~8), `UserPortabilityService.cs` (~8),
`TodosController.cs` (~10).

## The one thing to get right

The fixes are **mechanical nullability**. Do NOT change runtime behavior.
`UserPortabilityService.cs` was touched in CH09 (the `ScopeRowHoldsSeat`
split) — read only the warning lines, do not re-split.

## Entry reads (only these)

- `dotnet build Kumunita.slnx --no-incremental 2>&1 | grep -E "PostService|UserPortabilityService|TodosController"`
  (the exact warning lines + codes).
- For each file: read only each warning line ± 5. Do **not** read any file
  whole (all are >400 lines).

## Deliverables (3 files)

1. `src/Kumunita.Core/Posts/PostService.cs` — fix the nullability warnings
   (mechanical).
2. `src/Kumunita.Core/Portability/UserPortabilityService.cs` — fix the
   nullability warnings (mechanical; do not re-split CH09's method).
3. `src/Kumunita.Web/Controllers/TodosController.cs` — fix the nullability
   warnings (mechanical).

## Exit

`dotnet build` green. The 26 warnings in these 3 files are gone.
Handoff note: 3 lines starting `## U11 — nullability batch 2`.
