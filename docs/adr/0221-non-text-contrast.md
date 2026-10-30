# ADR 0221 — Non-text contrast pass (WCAG 1.4.11)

Status: Accepted

## Context
Controls, icons, focus rings, chips, outline buttons, cards and pager links were measured (border or fill against
the colour behind them) in Forest/Ember × light/dark on /events, /events/new, /posts/new, /groups, /groups/create
and /announcements, using the browser's computed styles.

## Findings
- **Failed (fixed):**
  - Unchecked checkbox / radio / switch edges were 2.2–2.5:1. Older `site.css` pins (`#a3b2a8` light, `#4e6357`
    dark, the latter `!important`) beat the `--kmb-control-border` token, and Ember borrowed Forest's green-grey.
  - The checked Ember box in light mode was amber on cream (1.8:1; mark and fill are fine, the edge was not).
  - The `.form-select` chevron kept Bootstrap's `#343a40` on the dark field (~1.6:1).
- **Passed:** text inputs and selects (3.2–3.7:1), focus ring `--kmb-accent` 2px outline (5.5–10.8:1 on the canvas),
  filled primary buttons except Ember light (see below), tooltips, pager active/disabled.
- **Below 3:1 on purpose (not required by 1.4.11):** outline buttons (1.1–2.0), chips (1.1–1.3), event/feed cards
  (1.2–1.5), pager links (1.2–1.5). These are identified by their text or are decorative, and the 16px bordered
  cards use soft borders by design (also stated in the `palettes.css` control-border comment). The Ember light
  primary button is dark text on amber (fill 1.7:1 against the cream page); the label identifies it.

## Fix (`palettes.css`, end of file)
- Unchecked controls: `border-color: var(--kmb-control-border) !important` (specificity ties the `site.css`
  pins, palettes.css loads later) — measured 3.2–3.5:1 in all four combinations.
- Ember light checked: edge uses `--kmb-control-border` (3.5:1); fill and mark unchanged.
- Dark theme: `--bs-form-select-bg-img` with a `#d5d3c8` chevron (both the OS-dark and explicit-dark scopes).

## Not changed / open
- The FAQ accordion chevron (`--bs-accordion-btn-icon`, `#212529`) was not reachable on a live page, so it was not
  measured; if the FAQ is used in dark mode, check it next.
- Off-state switch tracks were not on the sampled pages; they use the same edge rule now.
