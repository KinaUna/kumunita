# ADR 0247 — The Kanban board contains its screen-reader labels, so the page stops scrolling sideways

Status: Accepted
Date: 2026-10-29

## Context
A second overflow sweep (detail and edit pages, 600px and 720px; 47 URLs from the live list pages) found one offender: every board page
(`/projects/boards/{id}`), 300px too wide at 600px and 240px at 720px. The board itself behaves: `.kanban-board` is
`overflow-x: auto`, 516px wide with 1136px of lanes scrolling inside it. The extra width came from absolutely positioned
`span.visually-hidden` labels inside lanes that are scrolled out of view. The board is `position: static`, so those labels' containing
block is outside it; an `overflow` box only clips descendants whose containing block is inside it, so the labels escaped and extended
the page (document scroll width 907px in a 600px viewport). On a phone that makes the browser zoom the page out to fit.

## Decision
- `.kanban-board { position: relative }` (`site.css`, inside the existing rule, no new media block). The hidden labels now belong to
  the board and are clipped by its scroll box.
- The lane and card ⋮ menus are not affected: `client/lib/detach-menu.ts` re-homes an open menu onto `<body>` and positions it against
  the body, and in place they already sat inside a `position: relative` `.dropdown` wrapper.

## Verified
Live at 600px with the rule injected: document scroll width 907px to 600px, board still 516px wide with 1136px scrolling inside.
The open-menu position was not re-verified live with the rule (the pane's synthetic clicks did not open the menu reliably); it is
argued from `detach-menu.ts` above. The patch itself was not rebuilt or looked at after applying.
All other detail and edit pages swept (announcement, post, document, event, inventory, goal, project, to-do, group, admin account,
directory profile, and the edit pages of announcements, documents, events, inventory, goals, boards, to-dos and projects) had no
page-level overflow at 600px or 720px.

## Not covered
The sweep loads pages in an iframe, where the app switches to its embed layout (`kmb-embed`); it measures page content, not the navbar
(checked separately). `/posts/{id}/edit` returned "Access denied" for the post it tried and `/groups/{id}/edit` an empty title; both
unchecked. Pages that need a particular state (a failed form re-render, empty lists) were not swept.
