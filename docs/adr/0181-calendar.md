# ADR 0181 — Calendar tab restyle

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0063/0064/0081 (calendar), ADR 0173/0175 (UI refresh), ADR 0179/0180 (events)

## Decisions

CSS plus one tiny module; the calendar's markup, JS hooks (`#events-calendar`, `.event-chip`,
`.event-block`, the pools) and server model are untouched.

- **View toggle** is one segmented pill; the active segment uses the selected-chip colours (the old
  active state was dark green on dark, effectively unreadable). The Community filter label is small
  and quiet.
- **Grid lines** use `--kmb-border` instead of `--kmb-border-soft` (about 1.1:1 on the card surface in
  dark) for the month lattice, the week columns and the hour lines; month cells use `--kmb-surface`.
- **Today** is highlighted: `calendar-today.ts` adds `.is-today` to the day cell / week column whose
  `data-date` is today in the resident's zone (`#events-calendar[data-time-zone]`); CSS tints it and
  bolds the date.
- **Event chips and blocks** lose the full outline and underline: a soft tint, an 8px radius and a
  3px leading bar in the author's colour (`--event-color`, default the palette accent). The overlap
  flag stays a ring but is 1px accent instead of a 2px gold ring that made every clash look selected.

## Open

- Chips still use the per-author colour (no per-community colour system yet).
- The two-row toolbar structure is kept (Prev/Today/Next + filter on the second row); merging them
  into one row would need markup moves.
- Full-page / full-screen / kiosk rules in site.css are untouched; check them once.
