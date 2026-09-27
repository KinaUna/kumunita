# M10 U05 — Responsive pass: content + composer surfaces

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/plan-m10-pwa-responsive.md`; the scratch handoff
note is `docs/plans-milestones/m10-pwa-handoff-notes.md`; the atomicity
contract and unit-series rule are in the register header. **Runs after
U04** (the chrome + shared half is done — its rule groups in `site.css`
are the boundary witness: U05 adds only its own half's rule groups, each
commented with its surface name; the two halves never merge).

## Goal

D6/D7/D8 rendered for the **content + composer** half of the design doc
§Responsive inventory: the post/reply cards (the rendered body + the `⋮`
action-glyph triggers, ADR 0092 — the D8a 44 px floor's densest target),
the group cards, the events calendar grid, the Kanban board, and the
WYSIWYG split-view. Each surface's failures + fix class are the design
doc §Responsive's closed list for **this half** (U05 copies verbatim,
fixes exactly those, and no more — the C-M10·4 pin).

## Context (the locked set — from the design doc §Responsive, the U05 half)

- **C-M10·4 (the responsive pass is closed + pinned):** only the surfaces
  on the design doc §Responsive inventory are touched; only under the
  single `@media (max-width: 767.98px)` block; each rule group names the
  surface it serves (a comment). A surface not on the list is a
  `## U05 — Drift pause`, not a drive-by fix.
- **C-M10·5 (the two a11y floors hold):** (a) ≥ 44×44 px hit areas at the
  360 px viewport for the pinned interactive elements (D8a — the `⋮`
  `.action-glyph-btn` triggers are the densest case); (b) visible
  `:focus-visible` on the pinned custom `.kmb-*` pieces (D8b). **U05's
  half of the a11y floor list** is the design doc §a11y floors' entries
  that apply to the content + composer surfaces (the exact element list
  is locked in the design doc; copy it, do not re-derive).
- **D6 (one breakpoint rule, pinned):** `@media (max-width: 767.98px)`
  is the **single** phone rule. **U05 adds rules only under this one
  block** (extend the existing block groups alongside U04's, with its own
  surface-name comments — do not introduce a new `@media` boundary; that
  would break U06's block-count pin).
- **D7 (the closed surface inventory, the U05 half):** the design doc
  §Responsive's **content + composer** list, each surface with its
  pinned failures at 360 px + the fix class. **Copy this half verbatim
  into your working context; fix exactly these surfaces, and only these.**

## Entry reads (5)

1. `docs/design/m10-pwa-responsive-design.md` — §Responsive (the content
   + composer half of the inventory — **copy verbatim**) + §a11y floors
   (the U05-applicable entries — the exact elements + properties).
   **The authority.**
2. `src/Kumunita.Web/wwwroot/css/site.css` — the content-surface rules
   (the card / group / calendar / board / editor classes) + the existing
   `@media` blocks that touch them + **U04's rule groups** (the boundary
   witness — U05 adds below/alongside them with its own comments).
   **This is the file U05 edits** (plus optionally the post Detail markup,
   below).
3. `src/Kumunita.Web/Views/Posts/Detail.cshtml` — the post/reply card
   markup — the D8a 44 px floor's target + the `⋮` trigger's current hit
   area (the densest sub-44 px case; the design doc §Responsive's content
   half names whether a markup change is needed or a pure-CSS one
   suffices).
4. `src/Kumunita.Web/Views/Projects/BoardDetail.cshtml` — the Kanban
   board markup — the D6/D8a floor's target + the existing
   `:fullscreen` block's interaction with the phone-width pass (the
   board's `min-height: 60vh` + internal-scroll behavior at 360 px).
5. `src/Kumunita.Web/client/lib/rich-editor.ts` — the WYSIWYG split-view's
   markup + its existing "1 column on mobile, 2 columns on desktop"
   behavior (the drift check: U05's CSS change must not break the JS's
   layout assumptions — read only, do **not** edit this file unless the
   design doc §Responsive's editor entry names a markup/JS change; if it
   does, that entry is a `## U05 — Drift pause` candidate, because
   editing a `client/lib` file changes the Deliverables count and the
   unit-series rule — record it, don't silently expand scope).

## Deliverables (≤ 2)

- `src/Kumunita.Web/wwwroot/css/site.css` — extend the existing
  `@media (max-width: 767.98px)` block groups with the
  content-surface rules, **each rule group commented with the surface
  name** per D6 (e.g. `/* U05 · post/reply card */`, `/* U05 · group
  card */`, `/* U05 · events calendar */`, `/* U05 · Kanban board */`,
  `/* U05 · WYSIWYG split */`). **Do not touch U04's rule groups or any
  other `site.css` rule** (unit-series rule; the surface-name comments
  are the boundary witness).
- `src/Kumunita.Web/Views/Posts/Detail.cshtml` — **only if** the D8a
  44 px floor requires a markup change on the `⋮` trigger (the
  `min-height` / `padding` on the `.action-glyph-btn`). The design doc
  §Responsive's content half names whether a markup change is needed or
  a pure-CSS one suffices — **if it names a markup change, make it; if it
  names pure-CSS, do not touch this file** (the closed-list pin; a
  drive-by markup "improvement" is a drift pause).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green (CSS + optional markup);
  the 5 pins from U01/U02 still pass (U05 adds no xUnit pins — the
  behavioral 360 px half is U06's Playwright spec).
- **360 px smoke (the browser devtools device emulator, or the Playwright
  spec's viewport pin — the `.tmp/` harness discipline applies if you
  build a scratch page, never the system temp folder):** the
  post/reply card's `⋮` trigger is ≥ 44 px + the rendered body wraps
  without horizontal overflow + the group card's name + address stack
  without clipping + the events calendar's month grid is readable at
  360 px + the Kanban board's lanes are usable (≥ ~280 px wide or
  scroll-inside, per the design doc's fix class) + the WYSIWYG split-view
  is 1 column at 360 px (source full-width, preview hidden or below —
  the design doc's fix class). **Capture the evidence** (a screenshot
  path, or the computed `getBoundingClientRect` sizes for each pinned
  element) — this is the U06 spec's §`Viewport_360px_…` baseline.
- Handoff entry: the exact CSS rules added (each with its surface name,
  verbatim), any markup change to `Detail.cshtml` (the line + the
  reason), the 360 px smoke's result per surface (pass/fail + the
  evidence), and any drift (a surface the design doc's inventory named
  that U05 found already-handled — record it, the list stands as the pin;
  a surface U05 found broken that the inventory did **not** name — a
  `## U05 — Drift pause`, do not silently fix it; a `client/lib` file the
  design doc's editor entry implies must change — a `## U05 — Drift
  pause`, because it would break this unit's Deliverables + unit-series
  shape).
- **Last action:** move this unit's own plan file
  (`docs/plans-milestones/in-progress/pwa-responsive-u05.md`) to
  `docs/plans-milestones/done/` (a `git mv`).
