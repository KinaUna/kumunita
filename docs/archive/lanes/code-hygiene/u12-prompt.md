---
id: ch12-prompt
title: "CH12 prompt — Nullability batch 3 (remaining)"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH12 — Nullability batch 3 (src + tests: remaining)

## What you are doing

You are the **twelfth working agent** on the **CODE-HYGIENE** lane.
CH01–CH11 are committed.

This unit fixes the **remaining nullability warnings** in 3 src files:
`ProjectsController.cs` (~6), `EventController.cs` (~6),
`BoardsController.Translations.cs` (~6). If there are remaining test-file
nullability warnings (`DocumentServiceTests`, `DocumentControllerTests`,
`KioskItemCatalogTests`, `KioskControllerTests`,
`CommunityControllerTranslationTests`, `BreakGlassProbeTests`,
`HealthControllerTests`), fix the worst 2 in the same unit.

## The one thing to get right

After this unit, `dotnet build` should report **0 warnings** (or ≤5, all
documented as false positives). Run the histogram one final time to confirm
which warnings remain, and fix the ones in the named files.

## Entry reads (only these)

- `dotnet build Kumunita.slnx --no-incremental 2>&1 | grep -oE "warning [A-Z0-9]+:" | sort | uniq -c`
  (the remaining warning histogram — this is the source of truth for what
  this unit must clear).
- `dotnet build Kumunita.slnx --no-incremental 2>&1 | grep -E "ProjectsController|EventController|BoardsController.Translations"`
  (the exact warning lines + codes).
- For each file: read only each warning line ± 5.

## Deliverables (≤ 4 files)

1. `src/Kumunita.Web/Controllers/ProjectsController.cs` — fix the
   nullability warnings (mechanical).
2. `src/Kumunita.Web/Controllers/EventController.cs` — fix the nullability
   warnings (mechanical).
3. `src/Kumunita.Web/Controllers/BoardsController.Translations.cs` — fix
   the nullability warnings (mechanical).
4. (Optional) up to 1 test file with the worst remaining nullability
   warnings.

## Exit

`dotnet build` green. `dotnet build --no-incremental 2>&1 | grep -c warning`
returns 0 (or ≤5, documented). Handoff note: 3 lines starting
`## U12 — nullability batch 3` — the warning count before/after, the files
touched.
