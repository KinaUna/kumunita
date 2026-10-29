# ADR 0231 — Phone audit of the admin and account pages (tab strips, Analytics overflow, Platform table, headings)

Status: Accepted

## Context
The plan listed "Account > Portability and the individual admin forms" as not yet redesigned, and the admin tables
(error reports, escalation) as unchecked for phones. Audit first: 28 pages (all `/admin/*` pages, `/account/portability`,
`/account/password`, `/account/storage`) were loaded at 390px and measured (page width, offending elements, tables,
forms, headings), then the main ones were looked at in the browser pane.

## Findings
- Error reports and Escalation have no tables (list based) and no overflow; Account > Portability is already cards (export,
  import, danger zone) and reads well on a phone. No redesign needed.
- **Analytics:** the 7/30/90/180/365-day buttons sit in a `d-flex` row without wrapping and widen the page to 437px.
- **Tab strips:** Account has 9 tabs wrapping into 5 rows (~200px), Admin 6 tabs in 2 rows; content starts below the fold.
- **Platform pages table:** not stacked on phones (buttons squeeze into a narrow column).
- **Headings:** Platform used an `h3` under the `h1`; the Communities member panel used an `h6`.
- Languages, Help and Storage tables already sit in `.table-responsive` (scroll shadows from ADR 0205), which is enough.

## Decision
- Analytics button row gets `flex-wrap`.
- Under 768px every `.kmb-tab-under-navbar` strip without a dropdown menu is one horizontally scrolling row (no scrollbar,
  `overscroll-behavior-x: contain`) with edge fades (`mask-image`, driven by `--kmb-fade-l/-r`). New `client/lib/tab-strip.ts`
  (loaded in `_Layout`) scrolls the active tab to the centre and keeps the fades current. A strip that holds a dropdown is
  excluded (overflow would clip the menu). Applies to Account, Admin, Projects, Events and the locale settings tabs.
- Platform pages table: `kmb-stack-table` (ADR 0222), `data-label="Route"`, and action cells start at the left when stacked.
- Headings: Platform `<h2 class="h3">`; Communities member panel `<h2 class="h6 small">`.

## Verified (live, CSS and the tab-strip logic injected at 390px)
- Account strip: one 43px row, the active tab ("My data") scrolled into view, left fade on, right fade off; no page overflow.
- Platform pages: stacked rows with Route caption, buttons wrap under it.
- Analytics `flex-wrap` is a one-class change, not re-rendered (Razor not compiled here).

## Not changed / open
- The admin settings forms (Site, Quiet, Signup, Messaging, ...) were not restyled: they render as single cards with
  readable fields on a phone. If a headed-section pass is wanted for them it is a separate step.
- Dark mode, checked afterwards (follow-up, no code change): Forest and Ember x light and dark at 390px with the strip scrolled
  to the middle show both edge fades and clipped tab text; tab text contrast is 10.4-11.7:1 inactive and 5.9-10.8:1 active
  in all four combinations. The tab outline (1.2-1.5:1) is decorative and text-labelled, as in ADR 0208. A screenshot taken
  right after switching theme showed pale tabs; it was a mid-transition frame (computed backgrounds were already dark).
