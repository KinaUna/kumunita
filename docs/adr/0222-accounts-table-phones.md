# ADR 0222 — Admin Accounts table on phones

Status: Accepted

## Context
At 390px the `/admin/accounts` table was 767px wide inside a 364px `.table-responsive` box: Roles, Scope, Posting and
the Manage / Block / Delete buttons were off-screen behind a sideways swipe, and each row was 130–155px tall.

## Decision
Stacked cards below 576px, CSS-only (`palettes.css`, class `kmb-stack-table`): the header row is visually hidden;
each cell with a `data-label` shows that label (small caps, `--kmb-muted`) above its value; a blank cell
(`kmb-cell-empty`: no roles, not a moderator) disappears; the action cell becomes a wrapping flex row. In
`Admin/Accounts.cshtml` only the table class, three `data-label`s and the empty-cell classes were added; desktop and
tablet are unchanged and the markup stays a real table (accessible name from the visually hidden header row is kept
for assistive tech).

## Verified (live, injected markup and CSS at 390px)
- No horizontal scroll (table 364px = its box); rows 192–246px with all values and actions visible.
- Manage / Block / Delete are 43px tall (the existing touch-target lift), side by side, wrapping.

## Not changed / open
- Labels are English literals, like the page's existing column headers (the admin surface is not localized).
- Dark mode was not re-measured; the card uses the same tokens as the other admin tables.
- Other wide admin tables (error reports, escalation) were not checked; the class is reusable.
