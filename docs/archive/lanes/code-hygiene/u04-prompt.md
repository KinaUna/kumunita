---
id: ch04-prompt
title: "CH04 prompt — Src dead code + unread params"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: code-hygiene
updated: 2026-11-09


# CH04 — Src dead code + unread params (CS8321/CS9113)

## What you are doing

You are the **fourth working agent** on the **CODE-HYGIENE** lane.
CH01–CH03 are committed.

This unit fixes the **4 src warnings**: 2 unused local functions in a Razor
view (CS8321), 2 unread parameters (CS9113).

## Entry reads (only these)

- `sed -n '75,85p' src/Kumunita.Web/Views/Boards/BoardDetail.cshtml`
  (the 2 unused local functions `StatusGlyph` / `StatusLabelKey`).
- `grep -n "StatusGlyph\|StatusLabelKey" src/Kumunita.Web/Views/Boards/BoardDetail.cshtml`
  (confirm they are truly unused).
- `sed -n '10,20p' src/Kumunita.Core/UserInfo/ProfileSettingsService.cs`
  (the CS9113 unread `services` param at line 14).
- `sed -n '44,55p' src/Kumunita.Web/Controllers/ReportTargetsController.cs`
  (the CS9113 unread `posts` param at line 48).

## Deliverables (3 files)

1. `src/Kumunita.Web/Views/Boards/BoardDetail.cshtml` — remove the 2
   unused local functions (lines 78, 79) **or** if the grep shows they
   are used elsewhere in the view, record "false positive" in the
   handoff note and suppress with a `#pragma warning disable CS8321`
   around the functions.
2. `src/Kumunita.Core/UserInfo/ProfileSettingsService.cs` — prefix the
   unread `services` param with `_` (line 14).
3. `src/Kumunita.Web/Controllers/ReportTargetsController.cs` — prefix the
   unread `posts` param with `_` (line 48).

## Exit

`dotnet build` green. The 4 warnings are gone.
Handoff note: 3 lines starting `## U4 — src dead code`.
