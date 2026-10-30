# ADR 0220 — Tooltips for the icon-only top-row links

Status: Accepted

## Context
ADR 0219 hides the link labels (Home, Announcements, Community, Groups) between 576 and 767px so the variant-B
top row fits. The icons then had no visible name.

## Decision
CSS only, in `palettes.css` inside the same 576–767px media block: on `:hover` / `:focus-visible` of the link, the
already-hidden `.kmb-nav-label` span is un-clipped into a bubble below the icon (`--kmb-ink` background,
`--kmb-surface` text, 6px radius, same look as the rail tips). It is the same element that gives the link its
accessible name, so screen readers hear it once and there is no `title` duplicate. The link becomes
`position: relative` in that range; the bubble is `pointer-events: none` with `z-index: 1040`.

## Verified (live, injected CSS at 600px)
- Hovering Announcements shows the bubble centred under the icon, above the tab strip.
- Contrast: Forest light 14.8:1, Forest dark 12.8:1, Ember light 15.5:1, Ember dark 13.4:1.

## Not changed / open
- Touch devices get no hover; a tap navigates, as before.
- "More" keeps its text and needs no tooltip.
