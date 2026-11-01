# ADR 0282 — Community feed: widen the feed column, keep the communities rail on the left

Status: Accepted
Date: 2026-11-01

A one-line presentation change to the `/community` feed
(`src/Kumunita.Web/Views/Posts/Index.cshtml`). The user's ask is explicit:
*"On the community page, move the communities list to the left on larger
screen sizes where it isn't on the top, so the posts list has more space in
the center."*

## Context

The feed page is wrapped in the shared `_Layout` `.container` (Bootstrap
max-width 1320px). Inside it the content block is
`<div class="row justify-content-center"><div class="col-md-10 col-lg-9">…</div></div>`.
Within that, the body is the two-column grid `.airy-comm-layout`
(`site.css`): `grid-template-columns: 230px minmax(0, 1fr)` at ≥768px with the
communities `<aside>` as the **first** grid child (the left rail), and the
single-column collapse to `1fr` (the rail on top as horizontal chips) below
768px.

So the communities list was *already* the left column at desktop width — the
user's literal "move it to the left" described the current state, not a defect.
The real lever for "more space in the center" was the **width cap**: at ≥lg
(1024px) the content block was pinned to 75% of the container (`col-lg-9`)
with `justify-content-center`, leaving dead margin on both sides *outside* the
grid and, because the grid's feed track is `minmax(0, 1fr)`, the feed itself
was narrower than it needed to be. The communities rail is a fixed 230px, so
any width the cap frees goes entirely to the feed track — which is exactly
what the user wanted, without touching the rail or its left position.

## Decision

Widen the content block column on the feed page from
`col-md-10 col-lg-9` to `col-md-12 col-lg-11`:

- **≥lg (1024px):** the block goes from 75% → ~91.7% of the `.container`. The
  communities rail stays the fixed 230px left column; the freed width flows to
  the `minmax(0, 1fr)` feed track, so the posts list is wider and reads more
  central. `justify-content-center` still centers the (now wider) block, so
  the outer margins stay roughly symmetric (a touch wider than before on each
  side — acceptable, and the feed is the thing that gains).
- **≥md <lg (768–1023px):** the block goes 83.3% → 100% (full container
  width), the same rail-on-left / feed-right grid. Slightly wider feed.
- **<md (<768px):** unchanged — the grid collapses to `1fr` and the
  communities list stays the top chip row (the user's "where it isn't on the
  top" case is untouched).

This is presentation-only: no view-model, service, routing, schema, audit, or
translation-key change. The `.airy-comm-layout` grid, the 230px rail, the
mobile collapse, and the `@media` block count in `site.css` are all
**unchanged** (no new `@media` block — the pin in `PwaManifestTests` holds).
No `Milestones.cs` / README Roadmap / `WhatsNew.cs` / `STATUS.md` flip is
required: this is not a shipped capability, it is a layout refinement to an
already-shipped surface.

## Verification

Verified against the **served** page (rendered HTML + served `site.css`), per
the Razor verification doctrine: the served `.airy-comm-layout` carries the
`<aside>` (communities) as its first grid child and the feed as the second;
the served `site.css` rule is `grid-template-columns: 230px minmax(0, 1fr)` at
≥768px and `1fr` below. The change is the column width only, so the rail's
left position and the feed's right position are structurally unchanged; the
feed track simply resolves to a wider `1fr`.
