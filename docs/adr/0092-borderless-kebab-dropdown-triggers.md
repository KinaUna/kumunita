---
id: adr-0092
title: "Borderless ⋮ dropdown triggers (all views)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m10-pwa-responsive-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

## Context

The app's per-item action surfaces all use a single `⋮` Bootstrap dropdown
(the ADR [0069](0069-kanban-drag-dnd-fixed-statuses-and-add-lane.md) / ADR [0071](0071-lane-edit-arrow-autosubmit-and-menu-dividers.md) / ADR [0082](0082-content-actions-dropdown.md) convention). ADR [0070](0070-board-edit-lane-and-board-polish.md) introduced a
**borderless** trigger class for the Kanban board (`.kanban-glyph-btn`:
border: 0, transparent ground, a faint hover/focus tint) so the glyph reads
as a quiet affordance next to the title rather than as a button — and ADR
0071's amendment notes the card-head ⋮ "already had" the same quiet
affordance.

But every *other* `⋮` trigger in the app kept the bordered
`btn-outline-secondary` shape — the twelve triggers on the announcement,
event, group, group-post, page, post (post + each reply), board index,
project detail, and todo detail/index surfaces. The same affordance
("open the per-item action menu") therefore rendered two ways: borderless
on the board, boxed-with-a-border everywhere else. The border read as a
button and drew the eye away from the content the surface is meant to
show first (the ADR [0082](0082-content-actions-dropdown.md) motivation).

## Decision

- **One `⋮` trigger look, site-wide: borderless.** The
  `.kanban-glyph-btn` class from ADR [0070](0070-board-edit-lane-and-board-polish.md) is renamed
  **`.action-glyph-btn`** — it describes the *role* (a `⋮` action-dropdown
  trigger) rather than one board, and the class body is unchanged
  (border: 0, transparent ground, `rgba(0,0,0,0.06)` hover/focus/active
  tint, `--bs-body-color` text; Bootstrap loads after `site.css`, so the
  hover/focus/active ground and border-clearing pins stay in place).
- **All twelve remaining `⋮` triggers adopt the class**, replacing
  `btn-outline-secondary` — the ADR [0082](0082-content-actions-dropdown.md) "content actions dropdown"
  surfaces (announcement detail, event detail, group detail, group-post
  detail, page show, post detail + per-reply) and the M5/PL surfaces
  (board index, project detail, todo detail, todo index). The board
  detail's three triggers (board head, lane head, card head) just get
  the rename.
- **Everything else about the triggers is unchanged**: `btn btn-sm`,
  `dropdown-toggle`, `data-bs-toggle="dropdown"`, the `aria-label`s, the
  `⋮` glyph, `ms-auto` right-alignment, and the menus themselves. Pure
  presentation.

## Consequences

- Every `⋮` dropdown in the app now renders as the same quiet, borderless
  affordance — the Kanban board and the content/project/todo surfaces
  agree, and the content reads first in every view.
- The ADR [0082](0082-content-actions-dropdown.md) "affected files" list gains `site.css` (the rename) and the
  other trigger views; the ADR [0070](0070-board-edit-lane-and-board-polish.md) class name is recorded as amended by
  this ADR (the class's original home).
- No routes, no service seams, no `kw-l` keys, no schema, no client
  modules, no tests change — the e2e selectors target the `⋮` glyph and
  the dropdown items, not the button class.
- `Milestones.cs` / the README Roadmap / `MilestonesTests.cs`
  **untouched** (a surface polish, not a milestone).
