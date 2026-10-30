# ADR 0196 — Pages

Status: **Proposed** (scaffolding landed)
Date: 2026-10-27
Relates to: ADR 0192 (documents), ADR 0184 (detail pages), ADR 0183 (chips)

## Decision

- The collapsible tree is kept (native `<details>`, collapsed by default); it is restyled, not restructured.
  Each section (Community / Platform) is one surface card (`--kmb-surface`, `--kmb-border`, 16px radius) of
  soft ledger rows: titles without underline, accent on hover, per-row hover tint on branch summaries.
- The chevron takes the accent colour; the child count is a quiet pill on `--kmb-tint-a`.
- Draft is the shared outlined `.kmb-chip-draft`; the community label on the page view is a plain `.kmb-chip`.
- Page view (`Show.cshtml`): `.kmb-detail-card`, translation chips are `.kmb-chip.kmb-chip-btn` with a
  server-rendered `aria-pressed`, matching the announcement detail.
- Section headings carry `.kmb-section-title`.

## Open

- Light mode not verified for the tree; check both palettes, expand/collapse and phone width.
- Page form (New/Edit) relies on the form look (ADR 0182).
