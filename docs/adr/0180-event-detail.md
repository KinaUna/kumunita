---
id: adr-0180
title: "Event detail: rich header"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m10-pwa-responsive-design.md, docs/adr/0054-events-rsvp-reminders.md, docs/adr/0173-report-targets-lane.md, docs/adr/0175-palettes-and-feed-card.md, docs/adr/0179-event-card.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0180](0180-event-detail.md) — Event detail: rich header

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR [0179](0179-event-card.md) (event card), ADR [0173](0173-report-targets-lane.md)/0175 (UI refresh), ADR [0054](0054-events-rsvp-reminders.md) (RSVP)

## Decisions

**D1 — Rich header, structure unchanged.** The detail card keeps its order (title row, body,
translation chips, author band; then RSVP, linked to-dos/boards, add-to-do). The title row gains the
feed's 56×64 date tile (`.kmb-event-date`, `kw-dt` sentinels from ADR [0179](0179-event-card.md)) beside the title; the
community badge becomes the shared `.kmb-chip`; the single muted date line becomes two icon rows
("when": weekday + the resident's effective date format, "where") above a quiet "Add to calendar"
button. RSVP stays below the description (decided with the owner).

**D2 — Card language.** All cards on the page use `.kmb-detail-card` (`--kmb-surface`, border, 16px
radius); the author band uses `--kmb-surface-2` instead of a hard-coded `bg-white`.

**D3 — RSVP choices.** The Going / Maybe / No radios render as pill chips; the selected one uses the
palette's selected-chip colours with an accent border (was Bootstrap's green fill in every palette).
"Update" stays the one primary button. Markup, names and handlers are unchanged.

## Open

- Translation chips (`badge text-bg-light border`) and RSVP response badges still use Bootstrap
  badge colours.
- The event edit/create forms and the Calendar tab are untouched.
