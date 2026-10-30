# ADR 0245 — The document list meta row keeps each item whole on phones

Status: Accepted
Date: 2026-10-29

## Context
Since ADR 0239 the document list row reads `uploader · size · date` in `.airy-ann-meta`, a non-wrapping flex line. At 390px the three
spans shrank and broke inside themselves: "Alex / Admin", "30 / B" and the date over two lines, in a 46px-tall block. The detail page
meta (a stacked list) was checked and is fine.

## Decision
- `palettes.css`, scoped to `.kmb-doc-main .airy-ann-meta` (documents only; announcement rows use the same base class and are untouched):
  the row wraps, each span is `white-space: nowrap`, so wrapping happens between items.
- Below 576px the `·` separators (the even children) are hidden and the column gap grows to 0.875rem, so no dot dangles at a line end.
  Tablet and desktop keep the dots.
- Alternatives not taken: keep the dots (one dangling dot when the date wraps), or stack the three items on separate lines.

## Verified
Live at 390px with the rule injected: "Alex Admin  30 B" on line 1 and the date on line 2 (48px), or all three on one line when they fit
(23px); no horizontal overflow. The patch itself was not rebuilt or looked at after applying.

## Not covered
The dots are identified by position (`:nth-child(even)`), so a fourth item added to the row would need the rule revisited.
