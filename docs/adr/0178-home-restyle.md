# ADR 0178 — Home restyle (no structural change)

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0175 (UI refresh), ADR 0150 (site content customization)

## Decision

Restyle only. Section order and every visibility switch (`HomeShowFeatures`, `HideIntro`,
`HomeShowRoadmap`, ...) stay as they are: residents can hide the hero and admins can customise
the page, so the refresh must not reorder or remove anything.

- **Ember hero, feature band and CTA band** are warm brown (`palettes.css`) instead of green.
  Forest keeps its green. `--kmb-hero-bg` still overrides the hero background (photo hook).
- **Feature cards** use `--kmb-surface`, the 16px radius and the palette accent for icons.
- **"Coming up" / "What's new" columns** become cards (`.kmb-home-card`, one inner wrapper per
  column): surface, border, 16px radius, soft shadow on hover only; list titles use the ink colour
  with accent on hover instead of the global link colour.

## Open

- Ember dark uses green for ordinary links (`--bs-link-color: #8fcaa6`) while Ember light uses
  brown-amber (#8a5a12), so "View all →" and other links look out of place on warm surfaces.
  Not changed here; decide whether Ember dark links should use the amber accent.
- Roadmap section and the intro hero copy are untouched.
