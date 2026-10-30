# ADR 0246 — The events week grid scrolls inside its own box on narrow screens

Status: Accepted
Date: 2026-10-29

## Context
A sweep of 48 pages at 600px and 720px (each loaded in a same-origin iframe, page-level horizontal overflow measured) found one
offender: `/events/calendar` in Week view. The grid was 704px wide in a 516px box, so the page overflowed by 139px at 600px and 79px
at 720px (and the same below 576px). The "verified existing fix" in `site.css` (M10 U05) puts `min-width: 44rem` on
`.events-time-grid--week` and `overflow-x: auto` on the same element. A scroll container with a `min-width` larger than its parent
does not scroll; it grows, and the page scrolls instead.

## Decision
- Below 768px the week grid's minimum width moves onto the day-column track:
  `.events-time-grid--week { grid-template-columns: 3.5rem minmax(40.5rem, 1fr); }` (3.5rem ruler + 40.5rem = the old 44rem).
  The grid is now exactly as wide as its box and scrolls sideways inside it, with the sticky hour ruler staying pinned.
- The rule replaces the old `min-width` inside the existing `@media (max-width: 767.98px)` block in `site.css`, so the M10 drift-guard
  counts (2 dark `@media` blocks, 12 total) are unchanged.
- Day and Month views are unaffected. The kiosk overrides (`body.kmb-kiosk`) are unchanged.

## Verified
Live with the rule injected: grid 516px wide inside a 600px viewport, 704px of content scrolling inside it, ruler offset 1px after
scrolling 120px, page width equal to the viewport at 390, 600 and 720px. The patch itself was not rebuilt or looked at after applying.
All other 47 pages swept had no page-level overflow at 600px and 720px.

## Not covered
Pages that need data or ids were not swept (post, event, document, project and board detail pages, Edit pages for existing items), and
the sweep measures overflow, not layout quality. `/profile` and `/groups/new` returned empty titles (a redirect or a different route)
and were not checked.
