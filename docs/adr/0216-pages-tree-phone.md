# ADR 0216 — Pages tree and page detail at phone width

Status: Accepted

## Context
The remaining verification gap from ADR 0196: the Pages tree and a nested page at 390px (touch
emulation, Ember light, sample data with 12 branches of up to 8 leaves).

## Findings
- No horizontal overflow; long titles (top level and leaf) wrap cleanly and the count chip stays in view.
- Branch rows already have a 44px summary (ADR 0196). Leaf rows are 61px tall but only the 22px
  title text was tappable.
- Page detail: the "·" between "Back to the pages" and the breadcrumb dangled at a line end;
  the back link (26px), breadcrumb links (20px) and the Translations chips (26px) were small targets.

## Changes (CSS, plus three class hooks in `Page/Show.cshtml`)
- Leaf rows: the title link is stretched over the whole row (`::after { inset: 0 }`), draft chip unaffected.
- Under 576px the "·" is hidden and the breadcrumb takes its own row.
- Under `(pointer: coarse)`: back link min-height 44px, breadcrumb links padded to about 36px,
  `.kmb-chip-btn` min-height 2.25rem (this also lifts the same chips on post, group and event detail).
- New hooks: `kmb-page-trail`, `kmb-page-back`, `kmb-page-trail-sep`, `kmb-page-crumbs`.

## Verified
By injecting the rules into the live page: all page-detail controls at least 36px, no overflow,
a tap at the right edge of a leaf row hits its link.

## Not verified
Rendered from the patched build (2026-10-28, docker-compose, 390px): the ≤576px rules (separator
hidden, breadcrumb on its own row) and the stretched leaf link (the 131px link still catches a tap
at the row's right edge, `li` computes `position: relative`) applied from the served CSS. The
`(pointer: coarse)` block was injected on the page (a desktop browser cannot trigger that media
query): back link 44px, breadcrumb links 42px, chips 36px. Not checked: dark mode, Forest, desktop
(the rules are scoped to narrow/coarse, except the stretched leaf link), a three-level tree (sample
data is two levels).
