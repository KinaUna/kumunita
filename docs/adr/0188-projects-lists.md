# ADR 0188 — Projects landing, goal / project detail, shared list rows

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0183 (announcement cards), ADR 0184 (detail pages)

## Decision

- **Tab strip** (`.kmb-tab-under-navbar`, used by ~20 pages): the navbar-attached shape is kept (Per's
  call); the hover / active label uses `--kmb-accent` instead of hard-coded pine, so Ember light is
  no longer green. Dark scopes already followed the palette.
- **Shared list rows**: `.airy-ann-list` (Projects landing, goal and project detail lists, To-dos,
  Boards, Documents ...) becomes a stack of bordered cards with the feed gap, hover border and shadow,
  in palettes.css, so every user changes with no markup edit. The Bookmarks `<ul class="airy-ann-list">`
  is excluded (`:not(ul)`), and the in-list empty state becomes a dashed card.
- **Neutral badges**: `.badge.text-bg-light.border` (component, status, language labels) is restyled to
  the chip look globally; `button.badge...[aria-pressed=true]` gets the selected-chip colours.
- Markup: component / status labels on Projects landing, goal detail, project detail use `.kmb-chip`;
  project title translation chips are chip buttons with server-rendered `aria-pressed`; description
  cards are `.kmb-detail-card`; the goal's empty state uses `.kmb-empty`.

## Open

- To-dos (list + detail), Boards (list + kanban), project/goal New and Edit forms are not hand-restyled
  here; they inherit the row cards, chips and form look.
- Goal and project detail headers are unchanged `airy-head` blocks (title + meta + action buttons).
- Status text is shown verbatim (the M5 status-badge shape); no status colours yet.
