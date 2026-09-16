# TD U01 — Design doc (`translation-display-design.md`) + ADR 0027

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/
> plan-translation-display.md`) is the cross-reference; when the two
> disagree, **this file wins for what to do** and the register wins for
> *which files exist and in what order*.

## Goal

Author the TD lane's **primary tier** —
`docs/design/translation-display-design.md` (one file, like
`docs/design/rich-content-design.md` / `docs/design/multilingual-design.md`
— **NOT** a two-part split) and the ADR that settles the two open design
questions: **(a)** the display model (the authored-in language is the first,
always-present, default-visible variant; the language chip row is a
**selector** that swaps the displayed title+body / body to the chosen
variant, with a guaranteed path back to the original), and **(b)** leaving
the shared `LanguageOption` record untouched and instead carrying the
authored-in code **additively** on the two post-detail VMs + `ReplyItem`.
**No code, no build.**

## Context you need (read these first, in this order)

1. `src/Kumunita.Web/Views/Posts/Detail.cshtml` — the **defect surface**.
   Note the helpers block (`langNames`, `missingLanguages = Model.Languages.
   Where(l => !l.HasTranslation)`, `LangName`), the post title+body render
   targets (the `<h4>` with `Model.Post.Title` / `PlainTextPreview` fallback,
   and `<div class="mb-3 rc-body">@Html.Raw(MarkdownRenderer.
   RenderHtml(Model.Post.Body))`), the post translation block (the
   "None yet" empty state, the chips, the `<details>` panels, the
   "Add a …" `missingLanguages` loop), the **reply** translation block (the
   parallel "none yet" / chips / `<details>` / add-lane), and the **ADR 0024
   soft-delete branches** (the post body placeholder + the per-reply body
   placeholder — the swap must not render on a deleted row).
2. `src/Kumunita.Web/Models/PostDetailViewModel.cs` — the **shared
   `LanguageOption` record** (3 positional args: `Code`, `NativeName`,
   `HasTranslation`) and the two records this lane must extend
   **additively**: `PostDetailViewModel` (has `PostTranslations`, `Languages`,
   `CanTranslate`) and the positional `ReplyItem` (10 args today, ending in
   `DeletedAt?`). The **group** twin `GroupPostDetailViewModel` is in the
   next read.
3. `src/Kumunita.Web/Models/GroupViewModel.cs` §`GroupPostDetailViewModel`
   — the group-lane twin (has `PostTranslations`, `Languages`, `CanTranslate`)
   — the second VM that must gain the authored-in code.
4. `src/Kumunita.Core/Posts/Post.cs` + `PostReply.cs` +
   `PostTranslation.cs` + `ReplyTranslation.cs` — the **authored-in** tag
   (`Post.LanguageCode` / `PostReply.LanguageCode`, ADR 0018 — "the language
   the item was written in") versus the **translation's** `LanguageCode`
   (ADR 0022 — "the language the item is translated *into*"). The distinction
   is the whole lane: the original is *not* a `PostTranslation` row.
5. `docs/adr/0018-ugc-authored-in-language-tag.md` + `docs/adr/
   0022-user-added-post-reply-translations.md` + `docs/adr/
   0005-multilingual-support.md` **§C** — the constraints this lane must
   honor: ADR 0018 (the authored-in tag exists but is invisible to the chip
   row today), ADR 0022 (add-only write lane; reads inherit the parent's
   `Read`), and ADR 0005 §C (**UGC is never machine-translated and display is
   not auto-switched by the `kumunita.locale` cookie** — the swap here is an
   **explicit click**, not a preference, and must say so in the design doc).
6. `src/Kumunita.Web/client/lib/avatar.ts` — the **`tsc`-only ES-module**
   `addEventListener` pattern U05 mirrors (no bundler, no framework, `document.
   querySelectorAll` + `addEventListener`); the design doc's TS contract must
   describe a module of the same shape.

## Deliverables (2 files, both new)

### 1. `docs/design/translation-display-design.md` (~200 lines)

Header: the same **three-tier contract** note the RC/multilingual design docs
have (this file = primary; the register `docs/plans-milestones/
plan-translation-display.md` = secondary; `docs/plans-milestones/done/
translation-display-handoff-notes.md` = scratch). Then, in order:

- `## Context` — ADR 0022 shipped the *add-a-translation* lane and the detail
  surface shows the added rows as chips, but the **one language the item is
  written in** (ADR 0018's `LanguageCode`) is invisible to that chip row.
  Hence the three defects: "None yet" when the item *is* in a language;
  "Add a <that language>" offered; no way to read a translation in place or
  switch back. The platform already renders every body through one
  escape-safe renderer (`MarkdownRenderer`), so the swap is a **display**
  concern, not a data or rendering concern.
- `## Scope` — **In:** the post/reply **detail** surface on **both** lanes
  (community `Views/Posts/Detail.cshtml`, group `Views/Groups/
  PostDetail.cshtml`); the authored-in chip as the first/default variant;
  the hidden variant containers; the click-to-swap TS toggle; the add-lane
  exclusion of the authored-in language; the soft-delete gating. **Out
  (named deferrals, not a renumber):** the announcement surface (no added-
  translation chip row today), the ADR 0026 group/community name+description
  chip row (a different record + surface), any **auto**-translation or
  cookie-driven re-render (ADR 0005 §C forbids it; this lane is an explicit
  click), and the ADR 0022 **write** lane (unchanged).
- `## Invariants (pinned for the TD lane)` — **TD·1–TD·8**, each with a
  one-line "Pinned where" column:
  - **TD·1** — **The authored-in language is always a variant.** The item's
    own `LanguageCode` (ADR 0018) renders as the **first** chip and the
    **default-visible** title+body (post) / body (reply), even when **zero**
    `PostTranslation`/`ReplyTranslation` rows exist. **No "None yet" when the
    original is present.** Pinned where: U03, U04, U06.
  - **TD·2** — **The chip row is a selector, not just a list.** Clicking any
    language chip (original or added) swaps the *main* title+body (post) /
    body (reply) to that variant; clicking the original chip returns to it.
    The swap is **display-only** (visibility toggle), never a re-render,
    fetch, or navigation. Pinned where: U03, U04, U05.
  - **TD·3** — **Server-rendered variants, client-toggled.** Every variant's
    title+body is rendered **server-side** (the same `MarkdownRenderer` the
    current `<details>` panels use) into a hidden container; the TS only
    toggles `display`. **No `innerHTML` of client data; no `data-*` HTML
    blobs; no re-fetch.** Pinned where: U03, U04, U05.
  - **TD·4** — **The authored-in language is never offered to add.** The
    "Add a …" candidate list **excludes** the item's own `LanguageCode`
    (post: `Model.OriginalLanguageCode`; reply: `r.OriginalLanguageCode`).
    Pinned where: U03, U04.
  - **TD·5** — **One source of truth per variant.** The original variant
    reads the post/reply's own `Title`/`Body`; each added variant reads its
    `PostTranslation`/`ReplyTranslation` row. No variant is synthesized or
    translated — UGC renders as authored (ADR 0005 §C, M·3 untouched).
    Pinned where: U03, U04.
  - **TD·6** — **The shared `LanguageOption` record is untouched.** The
    authored-in code is carried **additively** on `PostDetailViewModel`,
    `GroupPostDetailViewModel`, and `ReplyItem` — **not** by reshaping the
    3-positional-arg `LanguageOption` (also the ADR 0026 shape). Pinned
    where: U02.
  - **TD·7** — **Zero Core / schema change.** No new document, no
    `PostTranslation`/`ReplyTranslation` row change, no migration (ADR 0004
    §B.1 is not even engaged — the field already exists, ADR 0018). The ADR
    0022 **write** lane and its route actions are unchanged. Pinned where:
    U02, U06.
  - **TD·8** — **Progressive enhancement.** With JS disabled the **original**
    variant is visible and every added variant is present in the DOM (the
    `<details>`-less swap degrades to "original shown, added variants as
    static text"); the swap is an enhancement, not a requirement. Pinned
    where: U03, U04, U05.
- `## FACES (pinned, 8)` — **TD1–TD8**, each bound to an invariant:
  - **TD1** — post authored in English, no added translations → the chip row
    shows **English** (first), the title+body are the original, and **no**
    "None yet" text appears. — TD·1
  - **TD2** — the "Add a …" lane on that English post does **not** list
    English (it lists the other enabled languages only). — TD·4
  - **TD3** — post authored in English + an added French translation → chips
    show **English** then **French**; clicking **French** swaps the
    title+body to the French row; clicking **English** swaps back. — TD·2/TD·3
  - **TD4** — reply authored in Polish (no added translation) → the reply's
    chip row shows **Polish** (first, default-visible body), no "none yet".
    — TD·1
  - **TD5** — reply authored in Polish + added German → clicking **German**
    swaps the reply body to the German row; **Polish** swaps back. — TD·2/TD·5
  - **TD6** — the **group** post/reply detail (group lane) behaves **identically**
    to the community lane (same first-chip, swap, and exclusion). — TD·1–TD·5
  - **TD7** — a **soft-deleted** post (ADR 0024) shows its placeholder and
    **no** translation chips/swap; a soft-deleted reply shows its placeholder
    and **no** swap. — TD·1 (absence on delete)
  - **TD8** — with JS **disabled**, the original is visible and the added
    variants are present as static text (no broken/blank content). — TD·8
- `## Pinned contract` — the exact shapes U02–U06 match verbatim:
  - `### VM ADDs (exact C#)` — `PostDetailViewModel`: `public string
    OriginalLanguageCode { get; set; } = string.Empty;`.
    `GroupPostDetailViewModel`: the same. `ReplyItem`: a **trailing positional**
    `string OriginalLanguageCode` appended **after** `DeletedAt?` (11th of 11).
    Population source: `Post.LanguageCode` (post VMs) and `PostReply.
    LanguageCode` (each `ReplyItem`) — the ADR 0018 fields, already returned
    by the detail result the controllers use.
  - `### data-* attribute set (exact)` — a **group wrapper** per item:
    `<div data-td-group="post">` (post) / `<div data-td-group="reply-
    {replyId}">` (each reply). A **chip** is a `<button type="button">` with
    `data-translation-chip`, the **same** `data-td-group` as its item, and
    `data-td-variant="{code}"` (the BCP-47 code to show). A **variant
    container** carries `class="td-variant"` + `data-td-variant="{code}"`.
    The TS toggles within `chip.closest('[data-td-group]')`: show the
    container whose `data-td-variant` equals the clicked chip's, hide the
    rest.
  - `### variant-container structure (exact)` — one container per variant:
    `<div class="td-variant" data-td-variant="{code}" [style="display:none"
    for non-original]><h4>{title}</h4><div class="rc-body">{body}</div></div>`
    (post, which has a title) / `<div class="td-variant" data-td-variant="{
    code}" [style="display:none" for non-original]><div class="rc-body">{body}
    </div></div>` (reply, body-only). The **original** container carries
    **no** `style="display:none"` (default-visible, TD·8).
  - `### add-lane exclusion (exact rule)` — the "Add a …" loop iterates
    `Model.Languages.Where(l => !l.HasTranslation && l.Code != originalCode)`
    (post) / `l.Code != replyOriginalCode` (reply) — the authored-in code is
    dropped from the candidate set even when `CanTranslate` is true.
  - `### soft-delete gating (exact rule)` — the entire translation section
    (chips + variant containers + add-lane) renders **only** when the row is
    live: post `Model.Post.DeletedAt is null`; reply `r.DeletedAt is null`.
    A deleted row shows its existing ADR 0024 placeholder and no swap.
  - `### TS module contract (exact)` — `client/lib/translation-swap.ts`,
    `tsc`-only ES module, no imports, ~40–70 LOC: for each
    `[data-translation-chip]`, on `click`, `g = chip.closest('[data-td-
    group]')`; `v = chip.dataset.tdVariant`; then
    `g.querySelectorAll('[data-td-variant]').forEach(c => { c.style.display
    = (c.dataset.tdVariant === v) ? '' : 'none'; })`. No `innerHTML`, no
    `fetch`, no navigation. Loaded in `_Layout.cshtml` as `<script type=
    "module" src="~/js/lib/translation-swap.js"></script>`.
  - `### pinned tests (exact names)` — `tests/Kumunita.Web.Tests/
    TranslationDisplayTests.cs` (U06 authors):
    1. `PostDetail_OriginalLanguageCode_EqualsPostAuthoredIn`
    2. `Reply_OriginalLanguageCode_EqualsReplyAuthoredIn`
    3. `GroupPostDetail_OriginalLanguageCode_EqualsPostAuthoredIn`
    4. `PostDetail_OriginalNotAmongAddedTranslationCodes`
    All four assert the U02 data ADDs from the detail controller result; the
    FACES TD1–TD8 are the **visual** acceptance (observed by the author, not
    unit-tested — the codebase tests controller/VM data shape, not Razor
    markup).
- `## Drift guard` — the TD·1–TD·8 invariants, the TD1–TD8 FACES, the three
  VM ADDs, the `data-*` attribute set, the variant-container structure, the
  add-lane exclusion rule, the soft-delete gating, the TS module contract,
  and the four pinned test names — **all frozen pins**. The **untouched**
  set is also frozen: the shared `LanguageOption` record (TD·6), all Core
  documents (TD·7), the ADR 0022 add-translation **write** lane + its route
  actions, and the M4/M5/M6 roadmap letters.

### 2. `docs/adr/0027-post-reply-translation-display-and-swap.md` (new)

Format: `## Status` / `## Context` / `## Decision` / `## Consequences`
(match the other ADRs).
- **Status:** `Accepted`.
- **Context:** ADR 0018 added the authored-in tag; ADR 0022 added the
  user-added-translation chip row; but the detail surface's chip row is
  computed only from the *added* rows, so the authored-in language is
  invisible — "None yet" is a lie, "Add a <authored-in>" is offered, and a
  translation can't be read in place.
- **Decision (two):** **(a)** the authored-in language is a **first-class
  variant** on the post/reply detail surface — the first, always-present,
  default-visible chip, with the title+body (post) / body (reply) swapped to
  the selected language on **explicit click** (never auto, ADR 0005 §C) and a
  guaranteed path back to the original; **(b)** the shared `LanguageOption`
  record is **left untouched** (ADR 0026 still consumes it) — the authored-in
  code is carried **additively** on `PostDetailViewModel`,
  `GroupPostDetailViewModel`, and `ReplyItem`. The swap is a server-rendered,
  client-toggled display enhancement (TD·3/TD·8); **zero** Core or schema
  change (TD·7).
- **Consequences:** the ADR 0022 add-lane candidate list excludes the
  authored-in language; the ADR 0018 tag gains a visible home; the detail
  views render one variant container per language (the `<details>` panels are
  replaced by the chip+variant model on this surface); JS-off still shows the
  original; no migration, no new document, no new read seam.
- **Amends:** `0018` (the authored-in tag is now the first/always-present
  variant, not just a stored tag).

## Exit

Both files exist with every section named above. **No build.** Handoff note
(append to `docs/plans-milestones/done/translation-display-handoff-notes.md`):
6–8 lines starting `## U01 — design doc + ADR 0027`, listing (a) the **8
invariants** (TD·1–TD·8, by id), (b) the **8 FACES** (TD1–TD8), (c) the
**three VM ADDs** (by name + `ReplyItem` ordinal), (d) the **four pinned test
names**, (e) the two ADR decisions + the `Amends: 0018` flag, and (f) any
`## U01 — Drift pause` (there should be none).
