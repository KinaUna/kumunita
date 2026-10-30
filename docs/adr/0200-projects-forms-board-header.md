# ADR 0200 — Projects forms and board header

Status: **Proposed** (scaffolding landed)
Date: 2026-10-28
Relates to: ADR 0190 (kanban), ADR 0184 (detail pages), ADR 0182 (forms)

## Decision

- Board header (`Projects/BoardDetail.cshtml`): the existing `<header class="airy-head">` becomes a surface card
  (`.kmb-board-head`) in the normal page width above the board. The author line is the shared byline (avatar with
  stable hue, name, created / modified), the community, project link and event link are `.kmb-chip`s on one row
  (links keep the ADR 0086 / 0115 dangling rules), tags are chips, translation chips are `.kmb-chip.kmb-chip-btn` with a
  server-rendered `aria-pressed`. The Full page / Full screen buttons and the menu are unchanged.
- Projects forms (Board, Goal, Project New/Edit) already follow the form look (ADR 0182). CSS-only additions: the
  audience card's heading uses the heading font; the "Everyone" switch hint aligns under its label; the divider is hidden
  while the restricted panel is hidden; the submit row centres the primary button and a quiet Cancel.

## Not included

- The shared community hint says "this event appears under" on board, goal and project forms (wording bug in a shared
  translation key); left for a copy fix.
- The leftover ADR 0086 comment above the removed project-link block in `BoardDetail.cshtml` is historical.

## Open

- Not verified in light mode, on a board with a project or event link, or at phone width. `:has()` rules need a current browser.
