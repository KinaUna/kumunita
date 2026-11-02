---
id: adr-0209
title: "Focus rings: the two `outline: 0/none` rules"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: []
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0209](0209-focus-rings.md) — Focus rings: the two `outline: 0/none` rules

Status: Accepted

## Context
`site.css` had exactly two rules that removed the focus outline: `.action-glyph-btn:focus-visible`
(outline: 0) and `.kanban-card:focus / :focus-within` (outline: none). Both substituted a
`--bs-focus-ring-color` glow (rgba green at 0.28 / 0.40 alpha) that measures 1.65:1 (light) and
2.5:1 (dark) against the surface, under the 3:1 minimum for focus indicators; the card's
border also turned `--bs-primary`, which is green in Ember and 2.6-2.7:1 on dark. The global
`html[data-kmb-palette] :focus-visible` accent outline (ADR [0175](0175-palettes-and-feed-card.md)) was being switched off.

## Decision
- ⋮ action button: no outline override and no glow; the accent outline (2px, offset 2px) shows
  on keyboard focus. The soft tint stays.
- Kanban card: the accent outline when the card itself is `:focus-visible`, and an accent border
  when something inside has `:focus-visible` (`:has()`); keyboard only (chosen over ringing the
  card on any focus). Mouse clicks no longer highlight the whole card.
- Accent outline contrast vs the lane/card surfaces: Forest light 10.0, Forest dark 9.4,
  Ember light 5.3, Ember dark 9.4.
- Verified live by Tab in Ember dark (button and card). Light/Forest by the contrast figures.

## Open
- `:has()` needs a current browser (the card border tint degrades to none, the outline remains).
- Other custom focus styles (`outline: 2px solid var(--bs-primary)` at ~line 2682, the
  dashed outline at ~4038) are drag/flash states, not focus removal, and were not changed.
- `.rc-editor-toolbar .rc-btn:focus-visible` (rich-editor toolbar) is a visible outline but uses
  `--bs-primary`, i.e. green in Ember; not changed here (candidate for the accent).
