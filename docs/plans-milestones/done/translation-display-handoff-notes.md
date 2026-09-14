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

## U01 — design doc + ADR 0027

- **Design doc** `docs/design/translation-display-design.md` (primary tier) and **ADR 0027**
  `docs/adr/0027-post-reply-translation-display-and-swap.md` both authored; **no code, no build.**
- **Invariants (8):** TD·1 authored-in is always a variant · TD·2 chip row is a selector ·
  TD·3 server-rendered variants, client-toggled · TD·4 authored-in never offered to add ·
  TD·5 one source of truth per variant · TD·6 shared `LanguageOption` untouched · TD·7 zero
  Core/schema change · TD·8 progressive enhancement.
- **FACES (8):** TD1 no "None yet" · TD2 add-lane excludes authored-in · TD3 post swap to/from ·
  TD4 reply first chip · TD5 reply swap to/from · TD6 group-lane parity · TD7 soft-delete gating ·
  TD8 JS-off degradation.
- **3 VM ADDs:** `PostDetailViewModel.OriginalLanguageCode` · `GroupPostDetailViewModel.
  OriginalLanguageCode` · `ReplyItem.OriginalLanguageCode` (11th positional, after `DeletedAt?`).
- **4 pinned tests:** `PostDetail_OriginalLanguageCode_EqualsPostAuthoredIn` ·
  `Reply_OriginalLanguageCode_EqualsReplyAuthoredIn` · `GroupPostDetail_OriginalLanguageCode_
  EqualsPostAuthoredIn` · `PostDetail_OriginalNotAmongAddedTranslationCodes`.
- **ADR decisions:** (a) authored-in = first-class variant + explicit-click swap (ADR 0005 §C);
  (b) `LanguageOption` untouched, code carried additively. **Amends: 0018.** No drift pause.

## U02 — projection ADDs

- **3 ADDs:** `PostDetailViewModel.OriginalLanguageCode` (additive property) ·
  `GroupPostDetailViewModel.OriginalLanguageCode` (additive property) ·
  `ReplyItem.OriginalLanguageCode` (**11th positional**, after `DeletedAt?`).
- **Call sites:** `PostsController.Detail` — `PostDetailViewModel` initializer +
  `ReplyItem` ctor (both sourced from `result.Post.LanguageCode` /
  `reply.LanguageCode`, the ADR 0018 fields); `GroupsController.GroupPostDetail` —
  the parallel pair.
- **Untouched confirmed:** the shared `LanguageOption` record (TD·6) and all Core
  documents (TD·7) — no Core edits, no migration, no view/TS/test changes.
- **Build:** `dotnet build Kumunita.slnx -c Debug` green (4/4 projects), no
  compile warnings. No drift pause.

## U03 — community view swap

- **`data-td-group` targets:** `"post"` (post variants) and `"reply-@r.Id"`
  (each reply) — the post chip/variants wrap in `<div data-td-group="post">`,
  the reply's in `<div data-td-group="reply-@r.Id" class="pe-3">`.
- **`missingLanguages` exclusion (verbatim):**
  `var missingLanguages = Model.Languages.Where(l => !l.HasTranslation && l.Code != originalCode).ToList();`
  (post); the reply lane inlines `Model.Languages.Where(l => !l.HasTranslation && l.Code != r.OriginalLanguageCode)`.
- **Soft-delete gating (TD7):** the post/reply translation section (chips +
  variant containers + add-lane) renders **only** when live
  (`Model.Post.DeletedAt is null` / `r.DeletedAt is null`); a soft-deleted
  row shows its ADR 0024 placeholder and no swap.
- **Unchanged confirmed:** the add-lane **form** markup (post title+body,
  reply body-only) and the ADR 0022 route actions
  (`/posts/{id}/translations`, `/posts/{id}/replies/{id}/translations`) are
  verbatim — only the candidate **list** changed.
- **For U04 (group view):** mirror the same `data-td-group` /
  `data-td-variant` / `td-variant` container set and the original-chip-first
  + hidden-variant shape; only the group-lane form-action routes + `Model.GroupId`
  differ.
- **Build:** `dotnet build Kumunita.slnx -c Debug` green (4/4). No drift pause.

## U04 — group view parity

- **Structure identical to U03 (TD6):** the group post/reply markup mirrors the
  community view byte-for-byte — same `data-td-group` targets (`"post"` /
  `"reply-@r.Id"`), same `td-variant` container shape (original default-visible +
  hidden per-translation), same original-chip-first row, same exclusion
  (`missingLanguages` post / inline reply), and the same TD7 soft-delete gating.
- **Group-lane routes preserved:** the add-lane forms post to
  `/groups/{Model.GroupId}/posts/{postId}/translations` and
  `/groups/{Model.GroupId}/posts/{postId}/replies/{r.Id}/translations` — the
  group route actions and `Model.GroupId` are the **only** divergence from U03.
- **No deliberate divergence** beyond those routes / `GroupId` (the community
  view's `<h4>`-first order and "None yet" states were dropped here, exactly as
  in U03 — no other drift).
- **Shared contract confirmed:** both lanes now render the identical
  `data-translation-chip` + `data-td-group` + `data-td-variant` set U05's
  `translation-swap.ts` toggles against.
- **Build:** `dotnet build Kumunita.slnx -c Debug` green (4/4). No drift pause.

## U05 — TS swap module

- **Deliverables:** `client/lib/translation-swap.ts` (new, ~46 LOC, no imports,
  `tsc`-only) + one line added to `Views/Shared/_Layout.cshtml` (the
  module-load block). No C#, no Razor view markup, no Core touched.
- **Layout line added (verbatim):** `Views/Shared/_Layout.cshtml`, in the
  `<script>` module block after `avatar-upload.js` —
  `<script type="module" src="~/js/lib/translation-swap.js"></script>`
  (comment above it: "Translation display swap … TD / ADR 0027: click-to-swap
  toggles the pre-rendered .td-variant containers; display-only, no fetch").
- **Selectors (verbatim):** iterate `[data-translation-chip]`; on click,
  `chip.dataset.tdVariant`; scope `chip.closest('div[data-td-group]')`; toggle
  `querySelectorAll('.td-variant')` on `container.style.display` by
  `container.dataset.tdVariant === code ? '' : 'none'`; also set
  `aria-pressed` on the `[data-translation-chip]` set. **No `innerHTML`, no
  `fetch`, no navigation** (TD·3 / TD·4 / TD·8).
- **⚠ Two deviations from the design doc's literal TS line — flagged for U06 to
  reconcile the design doc's "TS module contract (exact)":**
  (1) The doc's `g.querySelectorAll('[data-td-variant]')` would **also match
  the chips** (the chips carry `data-td-variant` and sit *inside* the
  `div[data-td-group]` wrapper per the doc's own pinned markup), so the literal
  line would hide the non-active chip and break FACES TD3/TD5 (can't click
  back). Implemented as `.td-variant` — the doc's *variant-container
  structure* section defines the containers as `class="td-variant"` +
  `data-td-variant`, so this honors the more-specific pin, not a contradiction.
  (2) The doc's `chip.closest('[data-td-group]')` resolves to **the chip itself**
  (closest starts at the element; the chip is a `<button data-td-group>`
  inside the wrapper), so it would find no containers and the swap would no-op.
  Implemented as `chip.closest('div[data-td-group]')` — the wrapper is the
  pinned `<div data-td-group>`, so this still matches the doc's pinned markup.
  Both changes are DOM-correct realizations of the same intent; **no frozen
  pin (attribute set, container structure, FACES) is reshaped** — only the two
  pseudocode lines are corrected so the FACES are actually reachable.
- **Build:** `npm run build` green (emits `wwwroot/js/lib/translation-swap.js`,
  2544 B) **and** `dotnet build Kumunita.slnx -c Debug` green (4/4).
- **Acceptance (FACES TD3/TD5/TD8):** clicking a chip swaps the visible
  `.td-variant`; clicking the original chip returns to it, on **both** lanes
  (same module, same selectors); with JS disabled the original is
  default-visible and every added `.td-variant` remains in the DOM.
