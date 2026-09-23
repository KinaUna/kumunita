# Events calendar (`EV-CAL`) — rolling handoff log

> One `## U#` section per unit, appended (never rewritten). Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-read list.

## Lane open

Lane plan created 2026-09-23. The **`EV-CAL`** named lane is an
**additive, display-only** view surface on the already-shipped **M4**
events context (ADR 0054): a month-anchored calendar at
`GET /events/calendar` — a rolling 30-day window of the caller's visible
events, overlap pairs highlighted client-side, prev/next/today navigation
to go back in time. **12 units (U00–U11)**, each sized for a ~32K-context
fresh agent. The design doc `docs/design/events-calendar-design.md` is
authored in U00–U01 (U00 Part 1, U01 Part 2 + ADR 0063 + the roadmap-trio
open). The one seam is `IEventService.ListInRangeAsync` (U02); the gate
is U09; the close + the move to `done/` is U10/U11.

**No open decisions are carried into U00** — the scope (D1–D7) is listed
in the plan's Assumptions and is locked by U01's ADR 0063.
