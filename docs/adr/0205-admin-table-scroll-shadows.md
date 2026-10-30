# ADR 0205 — Admin table scroll shadows

Status: Accepted

## Context
On a phone the admin Accounts table is 767px wide in a 364px card; Scope, Posting
and the Manage/Block/Delete actions were off-screen with no cue (ADR 0199 open item).

## Decision
- CSS-only scroll shadows on every `.table-responsive` (admin look): an accent-tinted
  fade on a side that has more content, hidden at the scroll ends. Verified live
  (Ember light and dark, scrolled start and end).
- Stacked rows on phones were considered and not chosen.

## Open
- On a phone the Posting cell wraps to four lines and the row actions stack (tall rows);
  a nowrap/compact treatment is not done.
- Not checked in Forest.
