# ADR 0218 — Hover states: icon rail tooltips, folded nav, pagination

Status: Accepted

## Context
Hover and focus states of the icon rail (nav variant C), the folded nav (≤ ~600 px) and the pager were
checked live in both palettes and both themes (computed colours, WCAG ratio).

## Verified
- Rail button, footer and folded-nav hover/active colours read correctly in Forest/Ember, light/dark.
- Pager: Newer/Older hover and the disabled state are fine.

## Findings and fixes
1. **Rail tooltips never showed.** `.kmb-rail-tip` was `position: absolute` inside a fixed column with
   `overflow-y: auto`; that also clips horizontally, so the tip was cut off. It is now `position: fixed`
   (left from `--kmb-rail-w`), and the new `client/lib/rail-tips.ts` supplies the vertical centre
   (`--kmb-rail-tip-y`) on hover/focus and on rail scroll. site.css shows tips only under `.kmb-rail-tips-js`
   (added by the module), so without JS no misplaced tip appears. Verified live: "Groups" tip sits beside its icon.
2. **Folded "More" dropdown items** kept Bootstrap's `text-dark` (!important) on hover; they now take the accent
   (`palettes.css`).

## Not changed / open
- The pager has no numbered or "active" page item in markup (`_Pager` renders only Newer/Older), so the
  active-page CSS is dead and untestable. Say if numbered pages are wanted.
- At ~600 px the top-bar search input shrinks to ~50 px and nav icons stack above their labels.
