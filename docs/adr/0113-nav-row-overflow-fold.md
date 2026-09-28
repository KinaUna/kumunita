# ADR 0113 — the variant-B nav-row overflow fold (Community + Groups into the More menu)

Status: Accepted
Date: 2026-09-28
Extends the **ADR 0111 nav-layout surface** (the two resident-selectable
layouts — variant **B**, the compact top row with a Bootstrap "More ▾"
dropdown, the default; and variant **C**, the icon rail), the **tsc-only
discipline** (ADR 0031 — a `client/lib/*.ts` module, compiled by `tsc` into
`wwwroot/js/lib/`, loaded as a `<script type="module">`, never an inline
script in a Razor view — the `detach-menu.ts` precedent), the **`kw-l`
closed-key registry + en/de/fr/da parity pins** (ADR 0015 — this lane adds
**no** new key: the folded items keep their existing `nav.community` /
`nav.groups` labels), the **site.css media-block boundary pin** (the M10
drift-guard frozen pin #7 + `PwaManifestTests.Site_Css_Media_Block_Boundary_
Pinned` — six `@media` occurrences total), and the **e2e-frozen selectors**
(ADR 0111 D6 / `e2e-pwa-responsive.spec.ts` — `header .navbar`,
`.navbar-toggler`, `a#accountMenu`, `a#notifications-bell`,
`a#languageMenu`). This ADR removes the horizontal scrollbar that appears in
the variant-B ("top row") layout when the window sits between the `sm` and
`md` breakpoints (roughly 576–767 px): when the flat row doesn't fit, the
two resident-only links (Community + Groups) fold into the existing "More ▾"
dropdown, and unfold again when there is room.

## Context

ADR 0111 shipped the two nav layouts. In the default variant B the row
carries, for a signed-in resident, a **fixed natural width** of links:

```
Home · Announcements · Community · Groups · More ▾ · [search] · [account]
```

The links sit in a `container-fluid` flex row that does not wrap. Above the
`md` breakpoint (≥768 px) the available row width (≈753 px) is at least the
natural width (≈746 px), so everything fits. But between `sm` (576 px) and
`md` (768 px) the available width grows from ≈576 px to ≈752 px while the
natural width stays ≈746 px — so for **every** width in the 576–755 range
the row overflows and the page grows a **horizontal scrollbar** (measured
live: at 640 px, `nav.scrollWidth 746` vs `nav.clientWidth 625`). This is a
plain layout-overflow bug, not a breakpoint choice: `navbar-expand-sm`
already collapses the row into a stacked menu *below* 576 px (where the
overflow disappears because the items stack), and it fits *above* 768 px.
The broken band is the in-between one.

The fix must satisfy the standing constraints:

- **No new `kw-l` key** — the folded items are re-homed, not re-labelled,
  so the closed-registry + parity pins are untouched (ADR 0015).
- **No new `@media` block** — the six-`@media` inventory is a frozen M10
  drift-guard pin, so the fold's styling must be a state-scoped,
  non-media rule.
- **No inline script in a Razor view** (SECURITY.md §6) — the fold logic is
  a tsc-only module (ADR 0031).
- **Self-scoped** — it must be a clean no-op in variant C (the icon rail),
  for signed-out users (Community/Groups aren't rendered), and wherever the
  hooks are absent. It must not disturb the e2e-frozen selectors.
- **Behavioural** (the fold actually firing at a given width) is the
  Playwright e2e surface (the ADR 0107/0111 `e2e-pwa-responsive.spec.ts`
  precedent); xUnit pins the structural contract only (string pins, no
  TestServer).

## Decision

**D1 — One tsc-only module re-homes the two flat links; no new DOM, no new
key, no state.** A new module, `client/lib/nav-more-fold.ts`, is loaded in
`_Layout.cshtml` after `bootstrap.bundle.min.js` (so it runs alongside the
rest of the `client/lib/*.js` modules). It scopes to the variant-B row only
(`.kmb-nav-row` — present only in variant B, absent in variant C and
unsigned). It finds the two fold items via a `data-nav-fold` hook and the
"More" menu via a `data-nav-more` hook, all three of which are new **inert**
attributes on the existing signed-in `<li>`s (absent when signed out, absent
in variant C). On overflow it moves the Community and Groups `<li>` nodes
into the More `.dropdown-menu` (prepended, in order, so the menu reads
Community, Groups, Events, …), retagging their anchors
`nav-link text-dark` → `dropdown-item text-dark` (Bootstrap's own
dropdown-item classes, so they render as normal menu items) and adds the
state class `.kmb-nav-folded` to the nav. When there is room again it moves
them back (restoring their original anchor classes) and removes the state
class. The module adds **no routes, no state, no persistence, and no new
`kw-l` key**.

**D2 — Overflow is detected on the navbar element, not the page.** The
module compares `nav.scrollWidth` vs `nav.clientWidth` on the `.kmb-nav-row`
element (a block-level flex container whose width is pinned to the
available space), with a 2 px tolerance to absorb sub-pixel rounding and the
appearance/disappearance of a vertical scrollbar. Because `.kmb-nav-row` is
the row that actually overflows, `scrollWidth > clientWidth` on that element
is a faithful, **page-scoped** signal that the row — and only the row — is
the thing overflowing, decoupled from any other content that might
independently overflow the document. The module is driven by a
`ResizeObserver` on the nav (with a `window.resize` fallback), coalesced to
one read+write per frame via `requestAnimationFrame`, and re-checked once
after `document.fonts.ready` (web-font swap can change link widths after
first paint). It sets the correct state immediately on load, and is a clean
no-op when the hooks are absent.

**D3 — The fold state is styled with a state-scoped, non-`@media` rule.**
`site.css` gains a single state-scoped rule under the existing navbar
section: `.kmb-nav-folded .navbar-nav .dropdown-item` re-asserts the navbar
link voice (`font-size: 0.9375rem` + the `--kmb-ink-soft` colour, pine on
hover/focus) so the two folded items read as the same links, now inside the
More menu. The rule is **deliberately not inside an `@media` block** — it
preserves the six-`@media` inventory pinned by
`Site_Css_Media_Block_Boundary_Pinned` (M10 drift-guard frozen pin #7) — and
it is inert unless `.kmb-nav-folded` is present, so variant C and signed-out
layouts are untouched. Bootstrap's own `navbar-expand-sm` collapse (below
576 px) already de-boxes the dropdown into the stacked mobile menu, so the
folded items inherit the correct mobile treatment there for free.

**D4 — No new `kw-l` key; the folded items keep their existing labels.** The
fold is a **re-homing**, not a re-labelling: the two anchors keep their
existing `<kw-l key="nav.community">` / `<kw-l key="nav.groups">` content
(registered in en/de/fr/da, already pinned). No key is added, removed, or
re-scoped, so the ADR 0015 closed-registry + parity pins are untouched.

## Consequences

- The 576–767 px variant-B row no longer overflows: between `sm` and `md`
  the resident sees `Home · Announcements · More ▾` with Community and
  Groups inside the More menu (reachable one click away), and the
  horizontal scrollbar is gone. Above `md` the two links unfold back to the
  flat row (measured: fits cleanly at 768 px and wider). Below `sm` the
  existing `navbar-expand-sm` collapse stacks the row, unchanged.
- **Zero Core change, zero schema, zero new authorization surface, zero new
  `kw-l` key, zero new `@media` block, zero new dependency.** The change is
  one tsc-only module + two inert `data-*` attributes + one state-scoped CSS
  rule + the module `<script type="module">` tag.
- **Additive / self-scoped.** A signed-out visitor, a variant-C resident, and
  any page without the hooks see exactly what they saw before — the module
  finds no hooks and is a no-op. The e2e-frozen selectors
  (`header .navbar`, `.navbar-toggler`, `a#accountMenu`,
  `a#notifications-bell`, `a#languageMenu`) are untouched (they live in
  unchanged elements; only two `data-*` attributes are added to the
  signed-in `<li>`s, which no e2e pin references).
- **Structurally pinned.** A new test class `NavMoreFoldTests`
  (`tests/Kumunita.Web.Tests/`) string-pins the whole contract without a
  server: the module exists and is a strict IIFE; `_Layout.cshtml` carries
  exactly two `data-nav-fold` hooks, one `data-nav-more` hook, the module
  `<script type="module">` tag, and the two existing `kw-l` labels; `site.css`
  styles `.kmb-nav-folded` and keeps exactly six `@media` occurrences (the
  M10 boundary); and ADR 0113 is recorded + indexed.
- **Follow-on (out of scope).** The behavioural guarantee (fold fires at a
  given width) is the Playwright e2e surface, the ADR 0107/0111
  `e2e-pwa-responsive.spec.ts` precedent. If a future nav addition widens the
  flat row beyond two foldable links, the fold target set is a data-attribute
  extension, not a module change.

## Affected files

- **New:** `src/Kumunita.Web/client/lib/nav-more-fold.ts` (the module)
- **New:** `tests/Kumunita.Web.Tests/NavMoreFoldTests.cs` (the structural
  pins)
- **New:** `docs/adr/0113-nav-row-overflow-fold.md` (this ADR)
- **Changed:** `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (two
  `data-nav-fold` hooks on the Community/Groups `<li>`s, one `data-nav-more`
  hook on the More `<li>`, one `<script type="module"
  src="~/js/lib/nav-more-fold.js">` tag)
- **Changed:** `src/Kumunita.Web/wwwroot/css/site.css` (one state-scoped
  `.kmb-nav-folded` rule, non-`@media`)
- **Changed:** `src/Kumunita.Web/WhatsNew.cs` (the 0.10.0 "Navigation
  layout" line annotated)
- **Changed:** `docs/adr/README.md` (the ADR 0113 index row)
- **Untouched (by design):** the `kw-l` registry + the en/de/fr/da parity
  pins (no new key); the six-`@media` inventory (the fold rule is
  state-scoped, not a media block); variant C; `_AccountNav.cshtml`; the
  e2e-frozen selectors; Core / schema / authorization.
