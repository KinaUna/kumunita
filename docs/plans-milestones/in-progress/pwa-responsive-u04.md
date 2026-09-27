# M10 U04 — Responsive pass: chrome + shared surfaces

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m10-pwa-responsive.md`; the scratch handoff
note is `docs/plans-milestones/m10-pwa-handoff-notes.md`; the atomicity
contract and unit-series rule are in the register header. **Runs after
U01/U02/U03** (the PWA substrate is in place; this unit is the responsive
half, independent of the PWA half — the design doc §Responsive is the
shared authority for both U04 + U05).

## Goal

D6/D7/D8 rendered for the **chrome + shared surfaces** half of the design
doc §Responsive inventory: the navbar + account nav (the
`navbar-expand-sm` collapse + the `_AccountNav` partial's dense link-row
at 360 px), the flash toast (`_FlashToast`), the pinned announcement
(the existing block at `site.css` line 578 — extend, don't rewrite), and
the footer (the `kmb-footer`'s 4-column grid at 360 px). Each surface's
failures + fix class are the design doc §Responsive's closed list for
**this half** (U04 copies verbatim, fixes exactly those, and no more —
the C-M10·4 pin).

## Context (the locked set — from the design doc §Responsive, the U04 half)

- **C-M10·4 (the responsive pass is closed + pinned):** only the surfaces
  on the design doc §Responsive inventory are touched in U04/U05; only
  under the single `@media (max-width: 767.98px)` block; each rule group
  names the surface it serves (a comment). A surface not on the list is a
  `## U04 — Drift pause`, not a drive-by fix.
- **C-M10·5 (the two a11y floors hold):** (a) ≥ 44×44 px hit areas at the
  360 px viewport for the pinned interactive elements (D8a); (b) visible
  `:focus-visible` on the pinned custom `.kmb-*` pieces (D8b). **U04's
  half of the a11y floor list** is the design doc §a11y floors' entries
  that apply to the chrome + shared surfaces (the nav links, the flash
  toast's close button, the account nav's links, the footer's links — the
  exact element list is locked in the design doc; copy it, do not
  re-derive).
- **D6 (one breakpoint rule, pinned):** `@media (max-width: 767.98px)`
  is the **single** phone rule. **U04 adds rules only under this one
  block** (extend the existing block groups; do not introduce a new
  `@media` boundary — that would break U06's block-count pin).
- **D7 (the closed surface inventory, the U04 half):** the design doc
  §Responsive's **chrome + shared** list, each surface with its pinned
  failures at 360 px (horizontal overflow, sub-44 px targets, wrapped
  labels, clipped menus) + the fix class (stack, shrink, truncate,
  hide-optional, scroll-inside). **Copy this half verbatim into your
  working context; fix exactly these surfaces, and only these.**

## Entry reads (5)

1. `docs/design/m10-pwa-responsive-design.md` — §Responsive (the chrome
   + shared half of the inventory — **copy verbatim**) + §a11y floors
   (the U04-applicable entries — the exact elements + properties).
   **The authority.**
2. `src/Kumunita.Web/wwwroot/css/site.css` — the six existing
   `@media (max-width: 767.98px)` blocks (lines 578 / 1058 / 1407 / 1694
   / 2105 — note 1058 is the `prefers-reduced-motion` block, the others
   are the width blocks). The `.kumunita-pinned-announcement` block at
   line 578 is the shape to **extend, not replace** (the D6/D7 fix-class
   precedent). **This is the file U04 edits** (plus optionally the
   `_AccountNav` markup, below).
3. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the navbar + the
   footer markup (the surfaces' structure — the `navbar-expand-sm`
   collapse, the `kmb-footer`'s `col-6 col-md-4 col-lg-*` grid).
4. `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — the dense
   link-row the D8a 44 px floor applies to (the `d-none d-sm-inline` /
   `d-sm-none` toggles + the `.nav-link` padding — the sub-44 px
   exception to fix).
5. `src/Kumunita.Web/Views/Shared/_FlashToast.cshtml` — the toast's
   markup (the D8a/D8b floor's target — the close button's hit area +
   the focus ring).

## Deliverables (≤ 2)

- `src/Kumunita.Web/wwwroot/css/site.css` — extend the existing
  `@media (max-width: 767.98px)` block groups with the chrome +
  shared-surface rules, **each rule group commented with the surface
  name** per D6 (e.g. `/* U04 · navbar */`, `/* U04 · account nav */`,
  `/* U04 · flash toast */`, `/* U04 · pinned announcement (extend the
  existing .kumunita-pinned-announcement block) */`, `/* U04 · footer */`).
  **Do not touch any other `site.css` rule** (unit-series rule; U05's
  content + composer half is a different unit — even though it edits the
  same file, U04 and U05 run in sequence and each owns only its half's
  rule groups; the surface-name comment is the boundary witness).
- `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — **only if** the
  D8a 44 px floor requires a markup change (the `min-height` / `padding`
  on the `.nav-link`s). The design doc §Responsive's chrome half names
  whether a markup change is needed or a pure-CSS one suffices — **if it
  names a markup change, make it; if it names pure-CSS, do not touch this
  file** (the closed-list pin; a drive-by markup "improvement" is a
  drift pause).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green (CSS + optional markup);
  the 5 pins from U01/U02 still pass (U04 adds no xUnit pins — the
  behavioral 360 px half is U06's Playwright spec).
- **360 px smoke (the browser devtools device emulator, or the
  Playwright spec's viewport pin — the `.tmp/` harness discipline applies
  if you build a scratch page, never the system temp folder):** the
  navbar collapses to the hamburger + the account nav's links are ≥ 44 px
  tall + the flash toast's close button is ≥ 44 px + the pinned
  announcement wraps without horizontal overflow + the footer's 4 columns
  stack to 1 without clipping. **Capture the evidence** (a screenshot
  path, or the computed `getBoundingClientRect` heights for each pinned
  element) — this is the U06 spec's §`Viewport_360px_…` baseline.
- Handoff entry: the exact CSS rules added (each with its surface name,
  verbatim), any markup change to `_AccountNav` (the line + the reason),
  the 360 px smoke's result per surface (pass/fail + the evidence), and
  any drift (a surface the design doc's inventory named that U04 found
  already-handled by Bootstrap — record it, the design doc's list stands
  as the pin even if the fix was unnecessary; a surface U04 found broken
  that the inventory did **not** name — the latter is a
  `## U04 — Drift pause`, do not silently fix it).
- **Last action:** move this unit's own plan file
  (`docs/plans-milestones/in-progress/pwa-responsive-u04.md`) to
  `docs/plans-milestones/done/` (a `git mv`).
