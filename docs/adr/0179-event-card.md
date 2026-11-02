---
id: adr-0179
title: "Event card with date tile"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m10-pwa-responsive-design.md, docs/adr/0019-timezone-platform-default-and-resident-override.md, docs/adr/0020-date-format-platform-default-and-resident-override.md, docs/adr/0065-events-feed-your-upcoming-events.md, docs/adr/0173-report-targets-lane.md, docs/adr/0175-palettes-and-feed-card.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0179](0179-event-card.md) — Event card with date tile

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR [0173](0173-report-targets-lane.md)/0175 (UI refresh), ADR [0065](0065-events-feed-your-upcoming-events.md) (EV-MINE), ADR [0019](0019-timezone-platform-default-and-resident-override.md)/0020 (zone and format)

## Decisions

**D1 — Card per event.** The `/events` feed and "Your upcoming events" render each event with the
shared `_EventCard` partial instead of ledger rows: a 56×64 date tile (month + day), the title (a
`stretched-link`, so the whole card is the link), the weekday and time range, the location, the
community chip, a 2-line preview and the organizer. Same surface/border/16px radius/hover shadow as
the feed cards. No data changes; the sort control, pager and "show more" behave as before
(`mine-more.ts` now also reveals `.kmb-event-card[hidden]`).

**D2 — Zone-correct tile.** `kw-dt` gains three sentinels beside `timeonly`: `month` (MMM), `day`
(d) and `weekday` (ddd), all in the resident's effective zone (ADR [0019](0019-timezone-platform-default-and-resident-override.md)), invariant culture.
Pinned in `DateTimeTagHelperTests`.

**D3 — Empty states** use `.kmb-empty` (ADR [0177](0177-groups-tile-and-empty-state.md)).

## Open

- A multi-day event shows only its end time; show the end date when it differs from the start.
- Month/weekday names are English (invariant), as every other `kw-dt` render today.
- `/events/calendar` and the event detail page are untouched.
