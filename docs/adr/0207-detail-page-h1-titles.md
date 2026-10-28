# ADR 0207 — Detail page titles are `<h1>`

Status: Accepted

## Context
A rendered heading scan of the detail pages (hidden translation variants excluded)
found three that had no `<h1>`: the announcement detail, the community post detail
and the group post detail all put the title in `<h4 class="kmb-ann-title">`. The
post pages' "Replies" / "Reply" section headings were `<h5>`, skipping levels under
the title. Every other detail page already had one visible `<h1>`.

## Decision
- The three titles (the original and each per-translation `.td-variant-title`) are
  `<h1 class="kmb-ann-title">`. The look is class-driven, and computed styles were
  compared in the live DOM before and after: identical.
- The Replies / Reply headings on the two post pages become `<h2 class="h5 ...">`:
  same look, outline h1 > h2.
- Razor only; no CSS change. Tags balanced; not compiled.

## Open
- Profile edit cards use `<h6>` titles directly under the `<h1>` (skips h2-h5).
- Kanban board lane titles are `<h3>` directly under the `<h1>` (no `<h2>`).
- Translation variants render one `<h1>` per language, but only one is visible at a
  time (the others are `display:none`), so the accessibility tree has one.
