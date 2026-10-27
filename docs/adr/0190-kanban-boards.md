# ADR 0190 — Kanban boards

Status: **Proposed** (scaffolding landed)
Date: 2026-10-27
Relates to: ADR 0067/0069 (kanban), ADR 0188/0189 (Projects)

## Context

The board lived in the standard `col-lg-9` page column, so three lanes at `flex: 1 1 0` were squeezed and card
text wrapped a word per line.

## Decision (CSS only, palettes.css; no markup change)

- At >= 992px the lanes row breaks out of the column (`width: min(1400px, 100vw - 2rem)`, centred with
  `left: 50% / translateX(-50%)`); the header and description keep the column. Not applied in full-screen
  (`:fullscreen`, `.kanban-full`), expanded-page, kiosk or embed modes, which have their own layouts, nor
  on phones.
- Lanes are a fixed 18rem and scroll sideways; the add-lane slot is a dashed 14rem column.
- Lane surface is a faint tint of the canvas (`color-mix` of `--kmb-border` over the body background) so
  cards (`--kmb-surface`, 12px radius, border, hover shadow) always read lighter than the lane in all four
  palette/mode combinations; the lane head uses the heading font; the at-limit lane keeps its amber rule.
- Card focus and lane drag-over use `--kmb-accent` instead of Bootstrap primary.
- Count and status badges pick up the chip styles from ADR 0188.

## Open

- Card meta (assignee, start, due) is still one run-on line; a stacked meta block would need markup in
  `_BoardCard.cshtml`.
- Board detail header, board New / Edit forms and the modals are not hand-restyled.
- Rail nav (variant C) shifts the centre; the breakout can reach under the rail on narrow desktops.
- Not verified in light mode or with many lanes (horizontal scroll) beyond the dark Ember preview.
