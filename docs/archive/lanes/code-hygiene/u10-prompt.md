---
id: ch10-prompt
title: "CH10 prompt — Nullability batch 1 (Groups/Event/Boards)"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH10 — Nullability batch 1 (src: Groups/Event/Boards)

## What you are doing

You are the **tenth working agent** on the **CODE-HYGIENE** lane.
CH01–CH09 are committed (the 4 method splits are done, the ratchet is still
red until U13).

This unit fixes the **nullability warnings** in 3 src files:
`GroupsController.cs` (~22), `Views/Groups/Detail.cshtml` (1),
`Views/Event/Calendar.cshtml` (1).

## The one thing to get right

The fixes are **mechanical nullability** (add `?` to nullable params, add `!`
to non-null assertions, add `null!` to required args). Do NOT change runtime
behavior. If a warning requires a behavior change (a null-check that would
change what the code does), **pause** and record `## U10 — Drift pause`
in the handoff note.

## Entry reads (only these)

- `dotnet build Kumunita.slnx --no-incremental 2>&1 | grep -E "GroupsController|Groups/Detail|Event/Calendar"`
  (the exact warning lines + codes).
- `sed -n '1,30p' src/Kumunita.Web/Views/Event/Calendar.cshtml`
  (the CS8602 at line 9).
- `sed -n '410,425p' src/Kumunita.Web/Views/Groups/Detail.cshtml`
  (the CS8604 at line 416).
- For `GroupsController.cs`: read only each warning line ± 5 (the file is
  1641 lines — do **not** read it whole).

## Deliverables (3 files)

1. `src/Kumunita.Web/Controllers/GroupsController.cs` — fix the nullability
   warnings (mechanical).
2. `src/Kumunita.Web/Views/Groups/Detail.cshtml` — fix the CS8604 (line 416).
3. `src/Kumunita.Web/Views/Event/Calendar.cshtml` — fix the CS8602 (line 9).

## Exit

`dotnet build` green. The 24 warnings in these 3 files are gone:
`dotnet build --no-incremental 2>&1 | grep -E "GroupsController|Groups/Detail|Event/Calendar" | grep warning`
returns nothing.
Handoff note: 3 lines starting `## U10 — nullability batch 1`.
