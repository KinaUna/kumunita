# ADR 0184 — Announcement detail

Status: **Proposed** (scaffolding landed)
Date: 2026-10-26
Relates to: ADR 0183 (announcement cards), ADR 0180 (event detail)

## Decision

- Scope / pinned / Draft badges become `.kmb-chip`s above the card (pinned uses the selected-chip
  colours, Draft an outlined chip); the Publish button is unchanged.
- The main card is `.kmb-detail-card`; the title is larger (1.625rem, heading font).
- The author byline moves from a footer strip to under the title: the feed header (44px avatar with
  per-resident hue, name, date, "edited" date).
- The translation selector is chip buttons; the shown language is `aria-pressed="true"` and
  highlighted. Initial `aria-pressed` is now server-rendered (previously set only after a click).
- Comments are one bordered card each (`.kmb-ann-comment`); the composer is a `.kmb-detail-card`
  with the rounded inputs and heading-font labels of ADR 0182.

## Open

- Title headings are still `<h4>`; they should be `<h1>` (needs a check of the translation-swap
  selectors and the heading outline).
- Modals (translation add/edit) and the audience disclosure are unchanged.
- Comment delete stays a text link.
