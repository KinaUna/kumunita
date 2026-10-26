# ADR 0180 — Event detail: rich header

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0179 (event card), ADR 0173/0175 (UI refresh), ADR 0054 (RSVP)

## Decisions

**D1 — Rich header, structure unchanged.** The detail card keeps its order (title row, body,
translation chips, author band; then RSVP, linked to-dos/boards, add-to-do). The title row gains the
feed's 56×64 date tile (`.kmb-event-date`, `kw-dt` sentinels from ADR 0179) beside the title; the
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
