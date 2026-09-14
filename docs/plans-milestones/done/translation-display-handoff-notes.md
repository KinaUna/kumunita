# Translation display (`TD`) — rolling handoff notes

> **The scratch tier** of the TD lane's three-tier contract (the design
> doc is primary, the register is secondary, this file is scratch).
> One section per unit, **appended, never rewritten**. Each unit writes
> exactly one short section before it exits; the next unit reads only
> that section + its own entry-reads list. A `## U<m> — Drift pause`
> section is a **blocker**: the next unit reads it first and either
> resolves it (recording the resolution in its own section) or carries
> it forward (naming it in its exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##`
> section from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-12
- **Register:** `docs/plans-milestones/plan-translation-display.md` (U01–U06)
- **Design doc (primary):** `docs/design/translation-display-design.md` (U01 authors)
- **ADR:** `docs/adr/0027-post-reply-translation-display-and-swap.md` (U01 authors; Amends 0018)
- **Scope:** the post/reply **detail** surface on **both** lanes (community
  `Views/Posts/Detail.cshtml`, group `Views/Groups/PostDetail.cshtml`). The
  authored-in language (ADR 0018's `LanguageCode`) becomes the **first,
  always-present, default-visible variant** chip; the title+body (post) /
  body (each reply) render into **hidden variant containers** toggled by a
  **click-to-swap** (`translation-swap.ts`, `tsc`-only); the "Add a …" lane
  **excludes** the authored-in language; soft-deleted rows show no swap.
  **Zero** Core / schema change (TD·7); the shared `LanguageOption` record is
  **untouched** (TD·6 — the code is carried additively on the two VMs +
  `ReplyItem`).
- **Out of scope (the named deferrals for a future lane, if one comes):** the
  announcement surface (no added-translation chip row today), the ADR 0026
  group/community name+description chip row (a different record + surface),
  any **auto**-translation or `kumunita.locale`-driven re-render (ADR 0005 §C
  forbids it — this lane is an explicit click), and the ADR 0022
  add-translation **write** lane (unchanged).

<!-- U01 appends its section below this line. One `##` section per unit, in
     order (U01, U02, … U06). Never rewrite a prior section. -->
