# ADR 0235 — site.css colour tokens collapse into light-dark() (second half of the light-dark cleanup)

Status: Accepted

## Context
ADR 0228 collapsed the palette tokens in `palettes.css` into `light-dark()`. `site.css` still held its own Forest dark token set
twice: once under `@media (prefers-color-scheme: dark) { html:not([data-kmb-theme='light']) { … } }` and once under
`html[data-kmb-theme='dark'] { … }`, about 180 lines each and required to stay identical by hand.

## Decision (step 1 of 2: tokens only)
- The two blocks were parsed and proved identical (104 tokens). 64 colour tokens that also have a light value in a `:root`
  block now carry both values in that `:root` declaration: `--x: light-dark(<light>, <dark>)`. `color-scheme` already follows
  the OS in auto mode and the `data-kmb-theme` attribute when forced (the same three declarations `palettes.css` has are added to
  `site.css` so it no longer depends on the load order).
- What `light-dark()` cannot express stays in the two small dark scopes (still exactly two `@media (prefers-color-scheme: dark)`
  blocks, so the M10 drift-guard count is unchanged): the 38 `--bs-*-rgb` triplets (used inside `rgba(var(--x-rgb), a)`) and
  the two dark-only tokens with no light value (`--kmb-surface-2`, `--kmb-band`).
- The component patch groups (L416 onward, ~230 lines, also written once per scope) are **not** touched here; they are step 2.

## Safety check: old-versus-new computed diff
A Playwright script loads Bootstrap + the old or new `site.css` + `palettes.css` and, for every combination of OS scheme
(light, dark) x palette (forest, ember, none) x theme attribute (auto, light, dark), reads the resolved value of every custom
property in either file (colour tokens resolved through a probe element) plus computed colour / background / border / outline
of 24 representative elements (footer, features band, navbar, buttons, badges, alerts, card, dropdown, tab strip, post card,
rail, table, utilities).
- Result: 10,170 comparisons, **0 differences**.
- The first run of the script compared nothing (the stylesheets did not load from `about:blank`); a deliberate one-digit change
  to a token (`#16241c` → `#16241d`) is now reported (18 diffs), so the 0 above is a real 0.

## Not covered
Real pages (markup that sets variables on non-root elements was not exercised), and nothing was rendered in the running app
(the container serves the old stylesheet until rebuilt). Browsers without `light-dark()` support are out of scope, as for ADR 0228.

## Follow-ups
- Step 2: the component patch groups, using the same diff script.
- The header comment above the dark section still says the block is "written for each scope"; that is now true of the patch
  groups only.
