---
id: adr-0201
title: "Verification round 3 fixes"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related: [docs/adr/0175-palettes-and-feed-card.md, docs/adr/0199-admin-shared-look.md, docs/adr/0200-projects-forms-board-header.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0201](0201-verification-round-3.md) — Verification round 3 fixes

Status: **Proposed** (scaffolding landed)
Date: 2026-10-28
Relates to: ADR [0200](0200-projects-forms-board-header.md) (board header), ADR [0199](0199-admin-shared-look.md) (admin / chips), ADR [0175](0175-palettes-and-feed-card.md) (palettes)

## Findings and fixes

Method: a scan of computed colours (green-leaning text, fills and borders) across 17 pages in Ember dark, plus phone-width
checks of the board header, announcement detail, Pages tree and member page.

- Board header byline wrapped the name under the avatar on phones when the "Modified" date was present: the byline is
  now `nowrap` with a shrinkable author block (board header only; announcement bylines wrap on purpose for their chip).
- Ember `btn-outline-primary` (New announcement, Send a message, Create ..., Download) rendered Bootstrap green. It now
  follows the amber accent: accent text and 60% border, solid action fill on hover.
- Ember checkboxes, radios and switches used Forest's green-grey controls. Unchecked: warm surface + control border.
  Checked: action fill (amber) with a dark mark (white marks fail on amber).

- Home milestone list (Ember only): the `M35`-style id chip kept Forest's dark pine (light) or a green tint (dark), and the
  Done pills were bright green. Both now use the accent on a 16% accent tint (a tint of the accent, because the band behind
  them is already `--kmb-chip-selected-bg`, which would make a selected-bg chip vanish). Forest is unchanged.

## Left alone

- Group monogram colours include a green hue by design (six stable hues).

## Open

- Light mode for the checked/unchecked controls and the outline buttons is verified by injection only.
- Forest is unchanged by this ADR.
