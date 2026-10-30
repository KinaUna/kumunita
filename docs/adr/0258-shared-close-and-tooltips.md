# ADR 0258 — Alert/modal Close labels and the "only you can see this" tooltips are localized

Status: Accepted
Date: 2026-10-29

## Context
After the Groups pages (ADR 0257) the scan for hard-coded English left a set of small, shared strings: `aria-label="Close"` on the close buttons of
alerts and modals in 29 more views (41 buttons) and the English `title` tooltips on draft/pinned/scoped chips (Event/Detail, Announcement/Detail
and Index, Page/Show and the page tree). Eight board and shared views already localized the close label through the `a11y.close` key.

## Decision
- Every remaining `aria-label="Close"` becomes `aria-label="@ariaClose"`, with `ariaClose` resolved from `a11y.close` (the key the board views
  already use; it exists in en/de/fr/da). The Groups views from ADR 0257, which used `common.close`, are switched to `a11y.close` as well so there is one key.
- The pattern is the per-view local variable (`var ariaClose = Translation is null ? "Close" : await Translation.GetAsync(...)`). Views that
  already resolved a language reuse it; 14 views and partials that did not get the two `@inject` lines and a small `@{ … }` block.
- Tooltips: `common.draft_tooltip` (existing from ADR 0257: "…not yet public") on Event/Detail, Announcement/Detail and Page/Show;
  new keys `pages.draft_tooltip` ("…not yet published", page tree and page list), `pages.scoped_tooltip` ("Scoped to a community") and
  `announcements.pinned_tooltip` ("Shown as a banner at the top of every page"), with en/de/fr/da text (machine-written, review parked).
- Rejected: a new attribute TagHelper (new shared code, not needed for this batch).

## Not covered
Applied and verified in the repo: the solution builds 0 errors (the `CS8602` warnings on the new `ariaClose` lines match the existing
`optionalPlaceholder` pattern in the same views) and `Kumunita.Web.Tests` passes 1152 / 0 failed / 1 skipped. Admin views (Admin/Accounts,
Admin/Index, AdminSite) got the Close label too but their other English is untouched.
Remaining from the scan: Kiosk Settings, Account/Signup, Guardian status labels, controller flash/error messages, admin pages.

## IMPROVE-gate note
The three new tooltip keys x en/de/fr/da grew `KnownTranslationKeys.cs` (11835 → 11859); the `improve-check.ps1` gate (a) ceiling was
re-based to 11859 on 2026-10-29 by ADR 0258 (same commit).
