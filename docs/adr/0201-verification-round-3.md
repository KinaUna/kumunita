# ADR 0201 — Verification round 3 fixes

Status: **Proposed** (scaffolding landed)
Date: 2026-10-28
Relates to: ADR 0200 (board header), ADR 0199 (admin / chips), ADR 0175 (palettes)

## Findings and fixes

Method: a scan of computed colours (green-leaning text, fills and borders) across 17 pages in Ember dark, plus phone-width
checks of the board header, announcement detail, Pages tree and member page.

- Board header byline wrapped the name under the avatar on phones when the "Modified" date was present: the byline is
  now `nowrap` with a shrinkable author block (board header only; announcement bylines wrap on purpose for their chip).
- Ember `btn-outline-primary` (New announcement, Send a message, Create ..., Download) rendered Bootstrap green. It now
  follows the amber accent: accent text and 60% border, solid action fill on hover.
- Ember checkboxes, radios and switches used Forest's green-grey controls. Unchecked: warm surface + control border.
  Checked: action fill (amber) with a dark mark (white marks fail on amber).

## Left alone

- Home milestone "Done" pills are `badge text-bg-success`: a semantic green, kept.
- Group monogram colours include a green hue by design (six stable hues).

## Open

- Light mode for the checked/unchecked controls and the outline buttons is verified by injection only.
- Forest is unchanged by this ADR.
