# ADR 0236 — site.css component dark patches collapse into light-dark() (step 2 of the light-dark cleanup)

Status: Accepted

## Context
ADR 0235 moved the 64 colour tokens of `site.css` into `light-dark()`. The component patch groups (about 230 lines) were still
written twice, once under `@media (prefers-color-scheme: dark) { html:not([data-kmb-theme='light']) … }` and once under
`html[data-kmb-theme='dark'] …`, with `!important` at (0,4,1) specificity to beat Bootstrap and, as a side effect, `palettes.css`.

## Decision
- **Base rules are edited in place.** Where `site.css` already has a rule for the element, the dark colour becomes the second
  argument: `background-color: light-dark(<light>, <dark token>)`. `background` and `background-color` are treated as
  equivalent when matching. Patches that only restated the light value were deleted (`.kmb-footer` border, the Airy post and
  group cards, `.kmb-rail-btn.active` background, part of `.btn-outline-secondary`).
- **New `:root` tokens** `--kmb-surface-2` and `--kmb-band` (dark-only until now) get light values:
  `light-dark(var(--bs-light-bg-subtle, #faf9f5), #1b2d24)` and `light-dark(#142e22, #0c1510)`. A `light-dark()` whose dark branch
  names an undefined variable is invalid for the whole declaration, so every token used in a dark branch must exist in light.
- **One unified block for Bootstrap utilities and components** that have no base rule of ours: `html SEL { P: light-dark(<Bootstrap
  light value>, <token>) }`, grouped per selector (`.bg-white`, `.text-muted`, `.text-body-secondary`, `.text-dark`, `.badge.*`,
  `.card.text-bg-light`, `.page-item.disabled .page-link`, `.dropdown-menu`/`.dropdown-item` hover and focus, `.link-light`,
  the fullscreen close buttons). `!important` mirrors Bootstrap's own rule for the property, no more.
  Form-check border and switch background are merged into one rule each.
- **Still dark-only** (non-colour, exactly two `@media (prefers-color-scheme: dark)` blocks, so the M10 drift-guard counts hold,
  2 dark and 12 in total): the navbar toggler image, the `.btn-close` filter and the switch `url()` background.
- `palettes.css`: the Ember features band and the active nav link background use `light-dark()` with the new tokens.
- Result: `site.css` 4,317 → 3,920 lines.

## Safety check: old-versus-new computed diff
Two Playwright oracles over OS scheme x palette (forest, ember, none) x theme attribute (auto, light, dark):
- Tokens: 10,170 comparisons, 15 differ, all `--kmb-surface-2` / `--kmb-band` in light (the intentional new light values).
- Components: 62,712 comparisons of computed colour, background, border and outline on the representative elements, **165 differ**.
  All are one cause: the old dark patches carried `!important` and masked `palettes.css` accent rules. Now the palette accent wins.

## Intentional visual changes (dark only)
- Forest: `#86c9a3` → `#9ad0ae` on active and hover tab-under-navbar links, `.kmb-rail-btn.active:hover` and `.kmb-feat-icon`.
- `.kmb-feat-icon-warm`: Forest `(240,201,126)` → the green accent `(154,208,174)`; Ember `(240,201,126)` → `(240,180,92)`.
  Light mode is unchanged.

## Not covered
Real pages in the running app (the container serves the old stylesheet until rebuilt); auto mode with OS light was only exercised
by the oracle. Browsers without `light-dark()` are out of scope, as for ADR 0228 and 0235.

## Follow-ups
- Look at the two warm feature icons in a browser after the next build.
