# ADR 0243 — The admin Audit table keeps cells on one line on small screens

Status: Accepted
Date: 2026-10-29

## Context
A survey of the admin tables at 375px (the ADR 0222 follow-up) found one real problem: `/admin/audit`. Its 32-character actor,
effective and target ids carry `text-break`, so on a phone they wrap one character per line and each row is 236px tall (the table is
830px wide in a 349px box). Help, Kiosk settings, Analytics, Storage and Languages rows are 51 to 78px; Error reports, Escalation and
Moderation have no tables.

## Decision
- New reusable class `kmb-nowrap` for a table: below 768px every header and body cell is `white-space: nowrap`
  (`palettes.css`, one new `@media (max-width: 767.98px)` block; `site.css` and its M10 media-block count are untouched).
  Rows stay one line (51px measured), the `.table-responsive` wrapper scrolls sideways, and the existing edge fades show that there is
  more. Desktop and the 768px-and-up layout are unchanged.
- `Views/Admin/Audit.cshtml` adds the class to its table. Nothing is truncated or hidden, so every id stays selectable in full.
- Alternatives not taken: stacked cards (`kmb-stack-table`; 50 cards of 8 lines each) and shortened ids (no hover on a phone).

## Verified
Live at 375px with the rule injected into the running page: row height 236px to 51px, table 1738px wide in a 349px box, no page-level
horizontal overflow. The patch itself was not rebuilt or looked at after applying.

## Not covered
Languages (1050px wide) and the UI strings table (991px wide) also scroll sideways with 68 to 78px rows; left as they are. Tablet
widths from 576px to 767px were not looked at.
