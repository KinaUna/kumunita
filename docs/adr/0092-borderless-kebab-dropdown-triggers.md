# ADR 0092 — Borderless ⋮ dropdown triggers (all views)

Status: Accepted
Date: 2026-09-26

## Context

The app's per-item action surfaces all use a single `⋮` Bootstrap dropdown
(the ADR 0069 / ADR 0071 / ADR 0082 convention). ADR 0070 introduced a
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
show first (the ADR 0082 motivation).

## Decision

- **One `⋮` trigger look, site-wide: borderless.** The
  `.kanban-glyph-btn` class from ADR 0070 is renamed
  **`.action-glyph-btn`** — it describes the *role* (a `⋮` action-dropdown
  trigger) rather than one board, and the class body is unchanged
  (border: 0, transparent ground, `rgba(0,0,0,0.06)` hover/focus/active
  tint, `--bs-body-color` text; Bootstrap loads after `site.css`, so the
  hover/focus/active ground and border-clearing pins stay in place).
- **All twelve remaining `⋮` triggers adopt the class**, replacing
  `btn-outline-secondary` — the ADR 0082 "content actions dropdown"
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
- The ADR 0082 "affected files" list gains `site.css` (the rename) and the
  other trigger views; the ADR 0070 class name is recorded as amended by
  this ADR (the class's original home).
- No routes, no service seams, no `kw-l` keys, no schema, no client
  modules, no tests change — the e2e selectors target the `⋮` glyph and
  the dropdown items, not the button class.
- `Milestones.cs` / the README Roadmap / `MilestonesTests.cs`
  **untouched** (a surface polish, not a milestone).

## Affected files

- `src/Kumunita.Web/wwwroot/css/site.css` (rename + comment)
- `src/Kumunita.Web/Views/Announcement/Detail.cshtml`
- `src/Kumunita.Web/Views/Event/Detail.cshtml`
- `src/Kumunita.Web/Views/Groups/Detail.cshtml`
- `src/Kumunita.Web/Views/Groups/PostDetail.cshtml`
- `src/Kumunita.Web/Views/Page/Show.cshtml`
- `src/Kumunita.Web/Views/Posts/Detail.cshtml` (post + reply triggers)
- `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml` (rename only)
- `src/Kumunita.Web/Views/Projects/BoardIndex.cshtml`
- `src/Kumunita.Web/Views/Projects/ProjectDetail.cshtml`
- `src/Kumunita.Web/Views/Projects/TodoDetail.cshtml`
- `src/Kumunita.Web/Views/Projects/TodosIndex.cshtml`
- `docs/adr/0070-board-edit-lane-and-board-polish.md` (class name note)
- `docs/adr/0082-content-actions-dropdown.md` (trigger shape)
- `docs/adr/README.md` (index)

Amends 0070 (the class's home — renamed) + 0082 (the trigger shape it
prescribed); additive on 0069 / 0071 (the `⋮` menu convention, unchanged).
No changes to `Kumunita.Core`, no new routes, no new `kw-l` keys, no new
client modules.
