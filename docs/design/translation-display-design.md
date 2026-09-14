# Translation display (`TD`) — authored-in as a first-class variant + click-to-swap

> **Three-tier contract.** This file is the **primary** tier of the TD lane:
> it pins the invariant numbers (TD·1–TD·8), the FACES (TD1–TD8), the exact
> C# of the VM ADDs, the `data-*` attribute set, the variant-container
> structure, the add-lane exclusion rule, the soft-delete gating, the TS
> module contract, the pinned test names, and the drift guard. The register
> (`docs/plans-milestones/plan-translation-display.md`) is the **secondary**
> tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/done/translation-display-handoff-notes.md` is the
> **scratch** tier (one short section per unit, appended, never rewritten).
> When the three disagree, **this file wins for the pinned shapes**; the
> register wins for *which files exist* and *what each unit does*.

## Context

ADR 0022 shipped the *add-a-translation* lane and the post/reply detail
surface shows the **added** rows as chips. But the **one language the item is
written in** — ADR 0018's `Post.LanguageCode` / `PostReply.LanguageCode` — is
invisible to that chip row. The chip row is computed only from the
*added* `PostTranslation`/`ReplyTranslation` rows, so the authored-in
language has no home on the surface. Three concrete defects result, all on
the post/reply detail surfaces (community + group lane):

1. **"None yet" is a lie.** An item authored in a language with no *added*
   translations renders `Translations: None yet` — even though the item
   plainly **is** in that language. The reader gets no signal of it.
2. **"Add a <that language>" is offered.** Because `missingLanguages` is
   `Languages.Where(l => !l.HasTranslation)` and the authored-in language has
   no translation row, the very language the item is *already in* is offered
   as a translation to add.
3. **No way to read a translation in place.** A translation's title+body only
   live in an expandable `<details>` panel below the original; there is no
   way to swap the *main* title+body to the selected language and back.

The platform already renders every body through one escape-safe renderer
(`MarkdownRenderer`, `Kumunita.Web.Security`). The swap is therefore a
**display** concern — a visibility toggle over server-rendered variants — not
a data, rendering, or authorization concern.

## Scope

**In (this lane ships):**

- The post/reply **detail** surface on **both** lanes — community
  (`Views/Posts/Detail.cshtml`) and group (`Views/Groups/PostDetail.cshtml`).
- The authored-in language rendered as the **first, always-present,
  default-visible** variant chip.
- The **hidden variant containers** (one per variant, server-rendered).
- The **click-to-swap** TS toggle (`translation-swap.ts`, `tsc`-only) and its
  `_Layout.cshtml` load.
- The **add-lane exclusion** of the authored-in language.
- The **soft-delete gating** (no chip row / swap on a deleted row, ADR 0024).

**Out (named deferrals — not a renumber):**

- The **announcement** surface (ADR 0018 tags it, but it has no
  added-translation chip row today and no chip row to swap — a separate,
  later lane if wanted).
- The **ADR 0026** group/community name+description chip row (a different
  record — `Component` — and a different surface).
- Any **auto**-translation or `kumunita.locale`-driven re-render — ADR 0005
  §C forbids both; the swap here is an **explicit click**, not a preference.
- The **ADR 0022 write lane** (add-translation standing + route actions) —
  unchanged.
- Editing/adding translations themselves — ADR 0022's write lane is untouched.

## Invariants (pinned for the TD lane)

| id | Invariant | Pinned where |
| --- | --- | --- |
| **TD·1** | **The authored-in language is always a variant.** The item's own `LanguageCode` (ADR 0018) renders as the **first** chip and the **default-visible** title+body (post) / body (reply), even when **zero** `PostTranslation`/`ReplyTranslation` rows exist. **No "None yet" when the original is present.** | U03, U04, U06 |
| **TD·2** | **The chip row is a selector, not just a list.** Clicking any language chip (original or added) swaps the *main* title+body (post) / body (reply) to that variant; clicking the original chip returns to it. The swap is **display-only** (visibility toggle), never a re-render, fetch, or navigation. | U03, U04, U05 |
| **TD·3** | **Server-rendered variants, client-toggled.** Every variant's title+body is rendered **server-side** (the same `MarkdownRenderer` the current `<details>` panels use) into a hidden container; the TS only toggles `display`. **No `innerHTML` of client data; no `data-*` HTML blobs; no re-fetch.** | U03, U04, U05 |
| **TD·4** | **The authored-in language is never offered to add.** The "Add a …" candidate list **excludes** the item's own `LanguageCode` (post: `Model.OriginalLanguageCode`; reply: `r.OriginalLanguageCode`). | U03, U04 |
| **TD·5** | **One source of truth per variant.** The original variant reads the post/reply's own `Title`/`Body`; each added variant reads its `PostTranslation`/`ReplyTranslation` row. No variant is synthesized or translated — UGC renders as authored (ADR 0005 §C, M·3 untouched). | U03, U04 |
| **TD·6** | **The shared `LanguageOption` record is untouched.** The authored-in code is carried **additively** on `PostDetailViewModel`, `GroupPostDetailViewModel`, and `ReplyItem` — **not** by reshaping the 3-positional-arg `LanguageOption` (also the ADR 0026 shape). | U02 |
| **TD·7** | **Zero Core / schema change.** No new document, no `PostTranslation`/`ReplyTranslation` row change, no migration (ADR 0004 §B.1 is not even engaged — the field already exists, ADR 0018). The ADR 0022 **write** lane and its route actions are unchanged. | U02, U06 |
| **TD·8** | **Progressive enhancement.** With JS disabled the **original** variant is visible and every added variant is present in the DOM (the swap degrades to "original shown, added variants as static text"); the swap is an enhancement, not a requirement. | U03, U04, U05 |

## FACES (pinned, 8)

| id | Face | Invariant |
| --- | --- | --- |
| **TD1** | post authored in English, no added translations → the chip row shows **English** (first), the title+body are the original, and **no** "None yet" text appears. | TD·1 |
| **TD2** | the "Add a …" lane on that English post does **not** list English (it lists the other enabled languages only). | TD·4 |
| **TD3** | post authored in English + an added French translation → chips show **English** then **French**; clicking **French** swaps the title+body to the French row; clicking **English** swaps back. | TD·2/TD·3 |
| **TD4** | reply authored in Polish (no added translation) → the reply's chip row shows **Polish** (first, default-visible body), no "none yet". | TD·1 |
| **TD5** | reply authored in Polish + added German → clicking **German** swaps the reply body to the German row; **Polish** swaps back. | TD·2/TD·5 |
| **TD6** | the **group** post/reply detail (group lane) behaves **identically** to the community lane (same first-chip, swap, and exclusion). | TD·1–TD·5 |
| **TD7** | a **soft-deleted** post (ADR 0024) shows its placeholder and **no** translation chips/swap; a soft-deleted reply shows its placeholder and **no** swap. | TD·1 (absence on delete) |
| **TD8** | with JS **disabled**, the original is visible and the added variants are present as static text (no broken/blank content). | TD·8 |

## Pinned contract

The exact shapes U02–U06 match verbatim.

### VM ADDs (exact C#)

- `PostDetailViewModel`: `public string OriginalLanguageCode { get; set; } = string.Empty;`
- `GroupPostDetailViewModel`: `public string OriginalLanguageCode { get; set; } = string.Empty;`
- `ReplyItem`: a **trailing positional** `string OriginalLanguageCode` appended **after** `DeletedAt?` (11th of 11).

Population source: `Post.LanguageCode` (both post VMs) and `PostReply.
LanguageCode` (each `ReplyItem`) — the ADR 0018 fields, already returned by
the detail result the controllers use.

### data-* attribute set (exact)

- A **group wrapper** per item: `<div data-td-group="post">` (post) /
  `<div data-td-group="reply-{replyId}">` (each reply).
- A **chip** is a `<button type="button">` with `data-translation-chip`, the
  **same** `data-td-group` as its item, and `data-td-variant="{code}"` (the
  BCP-47 code to show).
- A **variant container** carries `class="td-variant"` + `data-td-variant="{code}"`.

The TS toggles within `chip.closest('[data-td-group]')`: show the container
whose `data-td-variant` equals the clicked chip's, hide the rest.

### variant-container structure (exact)

One container per variant:

- **post** (has a title):
  `<div class="td-variant" data-td-variant="{code}" [style="display:none" for non-original]><h4>{title}</h4><div class="rc-body">{body}</div></div>`
- **reply** (body-only):
  `<div class="td-variant" data-td-variant="{code}" [style="display:none" for non-original]><div class="rc-body">{body}</div></div>`

The **original** container carries **no** `style="display:none"`
(default-visible, TD·8).

### add-lane exclusion (exact rule)

The "Add a …" loop iterates
`Model.Languages.Where(l => !l.HasTranslation && l.Code != originalCode)`
(post) / `l.Code != replyOriginalCode` (reply) — the authored-in code is
dropped from the candidate set even when `CanTranslate` is true.

### soft-delete gating (exact rule)

The entire translation section (chips + variant containers + add-lane) renders
**only** when the row is live: post `Model.Post.DeletedAt is null`; reply
`r.DeletedAt is null`. A deleted row shows its existing ADR 0024 placeholder
and no swap.

### TS module contract (exact)

`client/lib/translation-swap.ts`, `tsc`-only ES module, no imports,
~40–70 LOC: for each `[data-translation-chip]`, on `click`:
`g = chip.closest('[data-td-group]')`; `v = chip.dataset.tdVariant`; then
`g.querySelectorAll('[data-td-variant]').forEach(c => { c.style.display =
(c.dataset.tdVariant === v) ? '' : 'none'; })`. No `innerHTML`, no `fetch`,
no navigation. Loaded in `_Layout.cshtml` as
`<script type="module" src="~/js/lib/translation-swap.js"></script>`.

### pinned tests (exact names)

`tests/Kumunita.Web.Tests/TranslationDisplayTests.cs` (U06 authors):

1. `PostDetail_OriginalLanguageCode_EqualsPostAuthoredIn`
2. `Reply_OriginalLanguageCode_EqualsReplyAuthoredIn`
3. `GroupPostDetail_OriginalLanguageCode_EqualsPostAuthoredIn`
4. `PostDetail_OriginalNotAmongAddedTranslationCodes`

All four assert the U02 data ADDs from the detail controller result; the
FACES TD1–TD8 are the **visual** acceptance (observed by the author, not
unit-tested — the codebase tests controller/VM data shape, not Razor markup).

## Drift guard

The TD·1–TD·8 invariants, the TD1–TD8 FACES, the three VM ADDs
(`PostDetailViewModel.OriginalLanguageCode`,
`GroupPostDetailViewModel.OriginalLanguageCode`,
`ReplyItem.OriginalLanguageCode`), the `data-*` attribute set, the
variant-container structure, the add-lane exclusion rule, the soft-delete
gating, the TS module contract, and the four pinned test names — **all frozen
pins** in this file. The **untouched** set is equally frozen: the shared
`LanguageOption` record (TD·6), all Core documents (TD·7), the ADR 0022
add-translation **write** lane + its route actions, and the M4/M5/M6 roadmap
letters. Any mismatch between an entry read and these pins is a
`## U<m> — Drift pause` per the unit-series rule.
