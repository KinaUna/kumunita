# ADR 0265 — Projects controller flashes are localized

Status: Accepted
Date: 2026-10-29

## Context
The Projects controller had 63 literal English `TempData` messages (to-dos, event creation from a to-do, boards, lanes, cards, goals, projects, translations).
Several existing tests assert the exact English text.

## Decision
- All 63 go through `this.FlashAsync(key, english, args)`, in one batch. The English floor is byte-identical to the old text, so the exact-text tests still pass.
- 46 new keys in en/de/fr/da: 41 `projects.flash_*`, `flash.translation_needs_title_or_text`, and four direction words `projects.dir_left/right/up/down`.
  Shared keys reused: `flash.comment_added/_deleted`, `flash.translation_*`, `events.flash_todo_added`.
- "Lane moved {direction}." / "Card moved {direction}.": the service direction token (`left/right/up/down`) is translated through the `projects.dir_*` key
  (floor = the token itself) and formatted into `projects.flash_lane_moved_dir` / `flash_card_moved_dir`. de/fr/da templates are phrased "moved to the <direction>"
  where the grammar needs it; the direction words are inserted as given.
- The inline ternaries (assignee/status/project/event link) call `FlashAsync` in each branch.
- The `improve-check.ps1` gate's `KnownTranslationKeys.cs` pin was re-based 12403 -> 12587 on 2026-10-29 by this ADR (46 new keys x en/de/fr/da).

## Not covered
Not compiled or run (no .NET SDK here). The nested `await FlashAsync(...)` argument and the ternaries are the lines most worth a build check. de/fr/da text is
machine-written, review parked (the direction phrasing especially). `Lane is full`-style messages come from Core as `ex.Message` and stay English.
Remaining literal flashes: 167 across the other controllers.
