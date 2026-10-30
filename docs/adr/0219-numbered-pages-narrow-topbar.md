# ADR 0219 — Numbered pager pages; the variant-B top row at 576–767px

Status: Accepted

## Context
ADR 0218 found two things: the shared pager rendered only Newer/Older (so the active-page styling had nothing to
style), and at ~600px the top-row search shrank to ~50px with the link icons wrapping above their labels.

## Decisions
1. **Numbered pages, count-free.** ADR 0090 D1 stands (the seam returns only `hasMore`; no `TotalPages`, no
   count query). `_Pager` now renders a window between Newer and Older: `1 … (n-1) [n] (n+1)` where `1` shows
   from page 3, the ellipsis from page 4 and `n+1` only when `HasNext`. The current page is
   `.page-item.active` with `aria-current="page"` and is not a link; the ellipsis is `aria-hidden`. Numerals
   need no translation keys. `PagedViewModel` is unchanged and `rel="prev"` / `rel="next"` stay on Newer/Older.
   `.pagination` gets `flex-wrap` so the row wraps rather than overflowing at 360px.
2. **Top row at 576–767px.** Measured at 576px the row needed ~80px more than it had, and the fold only moves
   Community/Groups. In `palettes.css`: `.nav-link { white-space: nowrap }` (the icon can no longer wrap above its
   label, and `nav-more-fold.ts` gets the real overflow it relies on); the search form keeps a 9rem floor
   (`flex: 0 1 auto`); and between 576 and 767px the link labels (new `.kmb-nav-label` span in `_Layout`) and the
   account name (`.kmb-acct-name`, only when an avatar is shown) are visually hidden, not removed, so accessible names
   remain. "More" has no icon and keeps its text.

## Verified (live, injected CSS/markup)
- 576, 600 and 810px: no horizontal scroll, search 210px (≥ 9rem), the row folds at 810px as before.
- Pager contrast, Forest/Ember × light/dark: active 5.2–10.8:1, links 9.7–10.3:1 (dark Forest ≈ high), ellipsis ≥ 5:1.

## Not changed / open
- Icon-only links carry no tooltip (a `title` beside a visible label elsewhere would be redundant).
- Razor and C# are not compiled in this environment; no tests pin the pager markup (they pin the view model).
