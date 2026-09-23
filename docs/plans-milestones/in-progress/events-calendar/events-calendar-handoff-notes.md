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

## U00 — design doc Part 1

`docs/design/events-calendar-design.md` Part 1 authored (2026-09-23) per
the plan's U00 Deliverables — §1 what this lane is, §2 the verified
existing surface (9 items read directly), §3 decisions **D1–D7 all
[PROPOSED]**, §4 the 8 invariants. Invariants by id: **C-EV·1**
(non-leak: calendar ≡ feed restricted to window), **C-EV·2** (one
aggregate audit row per render), **C-EV·3** (`componentId` filter, never
gate), **C-EV·4** (overlap client-side only, never an access decision),
**C-EV·5** (anchor in effective zone; zone id display-only), **C-EV·6**
(zero document/schema changes), **C-EV·7** (nav = plain GET links),
**C-EV·8** (list view unchanged; one `Index.cshtml` link).
**Open for U01:** none blocked — (a) confirm the D2 seam parameter
order/names when pinning the exact C# (§3.2 states
`ListInRangeAsync(string actorId, DateTime windowStartUtc, DateTime
windowEndUtc, string? componentId, …)` — verify against
`ListUpcomingAsync`'s `(componentId, actorId, page)` order), (b) the
`WindowDays = 30` const naming, (c) the 8 `events.calendar.*` key names
are pinned in §3.6 — confirm they read right in the ADR.
