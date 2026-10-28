# ADR 0210 — Hover and active states pass

Status: Accepted

## Context
A static audit of the 67 `:hover` rules (site.css 42, palettes.css 25) and real mouse hovers in
Ember light and dark. Many rules were fine (buttons, cards, chips, list rows, tables, the
quiet "Edit" link, dropdown hover). The leaks found:
- Top-nav links carry Bootstrap's `text-dark` (`color: #17251d !important`), so the
  `.navbar-light .nav-link:hover` colour never applied: no hover feedback at all, and the nav
  text was a green-black in Ember.
- The active nav pill and active dropdown item used a hard-coded pine tint (`#1c4532` at 8%).
- Footer links, rail buttons/expander and folded-nav dropdown items hovered to `#1c4532`.
- Disabled pagination was Bootstrap grey (#e9ecef), the active page item and active dropdown
  item Bootstrap blue (#0d6efd).
- The footer band, text and brand were Forest values in Ember light.

## Decision (CSS only, palettes.css)
- Nav links: ink-soft at rest (`text-dark` overridden), accent text plus a faint accent pill on
  hover/focus-visible, a stronger (14%) accent pill for the active page (was 8% pine; hover and
  active read as related but distinct). Chosen over text-only or pill-only hover.
- Footer, rail and folded-nav hovers use `--kmb-accent`.
- Pagination: disabled = tint + muted; active = action colours. Dropdown active = accent tint.
- Ember light footer: `--kmb-surface-2` band, soft-ink text, ink brand. Forest unchanged.
- Verified by real hover in Ember light and dark (nav, footer, pagination); Forest by token values.

## Open
- Not hover-tested: rail buttons, folded-nav dropdown items, active pagination (no multi-page data),
  the `.airy-post-card` / `.dir-card` shadows (hover-only shadow already in place).
- Forest light active pill is a touch stronger than before (14% vs 8%).
