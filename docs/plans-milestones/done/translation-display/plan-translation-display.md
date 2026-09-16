# Translation display (`TD`) — authored-in as the first variant + click-to-swap — sealed unit register

> **The secondary tier** of the TD lane's three-tier contract. The **primary**
> is `docs/design/translation-display-design.md` (U01 authors); the **scratch**
> is `docs/plans-milestones/done/translation-display-handoff-notes.md`
> (one `##` section per unit, appended in order). When this register and a
> unit plan disagree, **the unit plan wins for what to do**; this register
> wins for *which files exist and in what order*.

## Understanding

A resident can add a translation of a post or reply into a *different*
language (ADR 0022), and the detail surface shows the *added* translations as
chips. But the one language the item was actually **written in** (the
authored-in tag, ADR 0018 — `Post.LanguageCode` / `PostReply.LanguageCode`)
is invisible to the chip row. Three concrete defects result, all reported
against the post/reply detail surfaces (community + group lane):

1. **"None yet" is a lie.** A post authored in English with no *added*
   translations renders `Translations: None yet` — even though the item
   plainly **is** in English. The reader gets no signal that English is the
   language it is looking at.
2. **"Add a English" is offered.** Because `missingLanguages` is computed as
   `Languages.Where(l => !l.HasTranslation)` and the authored-in language has
   no `PostTranslation` row, the very language the item is *already in* is
   offered as a translation to add.
3. **No way to read a translation in place.** A translation's title+body only
   live in an expandable `<details>` panel below the original; there is no
   way to swap the *main* title+body to the selected language, and no way to
   switch back.

This lane makes the authored-in language a **first-class variant** on the
detail surface and turns the language chip row into a **selector**: the
original is always present and default-visible, each added translation is a
clickable chip, and clicking any chip swaps the displayed title+body (post)
or body (reply) to that variant — with a guaranteed path back to the original.

## Assumptions

- **Scope (per user):** the post/reply **detail** surface on **both** lanes —
  community (`Views/Posts/Detail.cshtml`) and group (`Views/Groups/
  PostDetail.cshtml`). **Out of scope:** the announcement surface (ADR 0018
  tags it, but it has no added-translation lane today and no chip row to
  swap — a separate, later lane if wanted), the group/community
  name/description chip row (ADR 0026 — a different record, different surface),
  any auto-translation or cookie-driven re-render (ADR 0005 §C: UGC is never
  machine-translated and never re-rendered by the `kumunita.locale` preference
  — the swap here is an **explicit click**, not a preference), editing/adding
  translations (ADR 0022's write lane is unchanged).
- **`LanguageOption` is shared and stays put.** That record (3 positional
  args) is also the ADR 0026 shape used by the Community / Group-detail /
  Manage surfaces. Reshaping it for this lane would ripple into those
  surfaces. Instead the authored-in code is carried **additively** on the two
  post-detail VMs and on `ReplyItem` (TD·6).
- **Zero Core / schema change.** The original language is *already* stored
  (ADR 0018) and *already* returned by the detail result the controllers use.
  No new document, no `PostTranslation`/`ReplyTranslation` row change, no
  migration (TD·7).
- **The swap is progressive enhancement, not re-fetch.** Every variant is
  rendered **server-side** (the same escape-safe `MarkdownRenderer` the
  current `<details>` panels use) into a hidden container; a small `tsc`-only
  ES module toggles `display`. No `innerHTML` of client data, no
  `data-*` HTML blobs, no navigation, no round-trip (TD·3 / TD·4 / TD·8).
- **Test model (unchanged).** Controller/VM data-shape tests (the
  `OriginalLanguageCode` ADDs) in `Kumunita.Web.Tests`; the view-level
  exclusion and the swap are pinned as FACES (visual acceptance), since the
  codebase unit-tests controller/VM data shape, not Razor markup.

## Approach

Two tracks, sequenced. **Track A (Web projection, U02):** surface the
authored-in code onto the two post-detail VMs + `ReplyItem`. **Track B
(surface, U03–U05):** restructure both detail views into a chip row +
hidden variant containers + the add-lane exclusion, then the one TS swap
module + layout wiring. **U06** records the C# data-shape tests, the FACES
acceptance, and the doc sync (ADR 0027, ARCHITECTURE.md, README roadmap
note). Every unit ends with **build green**.

This is a **named lane** (`TD`), not a milestone letter — the media
(`M4-adjacent`, ADR 0011), group posts (`GP`, ADR 0013), and rich content
(`RC`, ADR 0025) lanes are the precedent. **M4/M5/M6 stay Events /
Projects / Portability.** No roadmap letter moves.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U01–U06 below),
one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary —** `docs/design/translation-display-design.md` — the TD
  invariants (TD·1–TD·8), the FACES (TD1–TD8), and the **pinned contract**
  (the exact VM ADDs, the `data-*` attributes, the variant-container
  structure, the add-lane exclusion rule, the TS module contract, and the
  pinned test names). U01 authors; U02–U06 match it verbatim.
- **Secondary — this file** — the unit register with each unit's
  deliverables and exit criteria.
- **Scratch —** `docs/plans-milestones/done/translation-display-handoff-notes.md`.
  One section per unit, appended (never rewritten). Each unit writes exactly
  one short section before it exits; the next unit reads only that section +
  its own entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list,
3–5 files, no full-repo scan; the design-doc section cited is named);
**Deliverables** (a closed set of new/modified files, ≤ ~4 files / ~500 LOC,
no misc cleanups); **Exit** (`dotnet build` green for the touched projects —
plus `npm run build` for U05 — and a handoff-note entry appended *before* any
follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside a `## U<m> —
Drift pause`; (3) never reshapes the shared `LanguageOption` record (TD·6)
or any Core document (TD·7); (4) never introduces a test whose exact name is
not in the design doc's pinned-test section; (5) if entry reads reveal the
design doc is out of date, the unit pauses and records `## U<m> — Drift
pause` in the handoff note.

---

## Units (6 total)

### U01 — Design doc (`translation-display-design.md`) + ADR 0027
- **Goal:** author the TD lane's **primary tier** —
  `docs/design/translation-display-design.md` (one file, like the
  `rich-content-design.md` / `multilingual-design.md` precedent — NOT a
  two-part split) and the ADR that settles the display model
  (*authored-in is a first-class variant + click-to-swap*; *the shared
  `LanguageOption` record is left untouched*). **No code, no build.**
- **Entry reads:** the defect surfaces — `src/Kumunita.Web/Views/Posts/
  Detail.cshtml` (the `missingLanguages` / `LangName` helpers, the post +
  reply translation blocks, the `<h4>`/`.rc-body` render targets, the ADR
  0024 soft-delete branch); `src/Kumunita.Web/Models/PostDetailViewModel.cs`
  (the shared `LanguageOption` record, `PostDetailViewModel`, the positional
  `ReplyItem`); `src/Kumunita.Web/Models/GroupViewModel.cs` §`GroupPostDetailViewModel`;
  `src/Kumunita.Core/Posts/Post.cs` + `PostReply.cs` + `PostTranslation.cs`
  + `ReplyTranslation.cs` (the authored-in `LanguageCode` vs. the
  translation's `LanguageCode`); `docs/adr/0018-ugc-authored-in-language-tag.md`
  + `docs/adr/0022-user-added-post-reply-translations.md` +
  `docs/adr/0005-multilingual-support.md` §C (the no-auto-translation pin);
  `src/Kumunita.Web/client/lib/avatar.ts` (the `tsc`-only ES-module
  `addEventListener` pattern U05 mirrors).
- **Deliverables (2 files, both new):** `docs/design/
  translation-display-design.md` (~200 lines) + `docs/adr/
  0027-post-reply-translation-display-and-swap.md`. The design doc must pin,
  in order: `## Context`; `## Scope` (in/out per the Assumptions above);
  `## Invariants (pinned for the TD lane)` — **TD·1–TD·8**; `## FACES
  (pinned, 8)` — **TD1–TD8**; `## Pinned contract` — the VM ADDs (exact C#),
  the `data-*` attribute set, the variant-container structure, the
  add-lane exclusion rule, the soft-delete gating, the TS module contract,
  and the **pinned test names** (U06). The ADR records the two decisions
  (the display model + the `LanguageOption`-untouched / additive-VM-field
  choice), Status Accepted, **Amends: 0018** (extends the authored-in tag's
  meaning from "a stored tag" to "the first, always-present, default-visible
  variant on the detail surface"). **No build.** Handoff note: 6–8 lines
  starting `## U01 — design doc + ADR 0027`, listing the **8 invariants**
  (by id), the **8 FACES** (TD1–TD8), the **3 VM ADDs** (by name), the
  **pinned-test names**, and any drift pause.

### U02 — Web projection: surface the authored-in language
- **Goal:** add the authored-in code to the two post-detail VMs and to
  `ReplyItem`, and populate it in the two detail controllers — the data
  layer the views (U03/U04) and the tests (U06) both depend on. **Touch no
  view, no Core, no `LanguageOption`.**
- **Entry reads:** `docs/design/translation-display-design.md` §Pinned
  contract (the exact VM ADDs + the `ReplyItem` positional position);
  `src/Kumunita.Web/Models/PostDetailViewModel.cs` (`PostDetailViewModel`,
  the positional `ReplyItem` and its current 10-arg order); `src/Kumunita.
  Web/Models/GroupViewModel.cs` §`GroupPostDetailViewModel`; `src/Kumunita.
  Web/Controllers/PostsController.cs` §`Detail` (the `ReplyItem` ctor call +
  the `PostDetailViewModel` object initializer); `src/Kumunita.Web/
  Controllers/GroupsController.cs` §`GroupPostDetail` (the parallel `ReplyItem`
  ctor call + `GroupPostDetailViewModel` initializer).
- **Deliverables (4 files, all modified):**
  - `src/Kumunita.Web/Models/PostDetailViewModel.cs` — add `public string
    OriginalLanguageCode { get; set; } = string.Empty;` to
    `PostDetailViewModel` (additive property) **and** append a trailing
    positional `string OriginalLanguageCode` to `ReplyItem` (11th, after
    `DeletedAt`). Doc-comments anchor TD·1 / TD·6. **`LanguageOption` is
    not touched.**
  - `src/Kumunita.Web/Models/GroupViewModel.cs` — add the same `public
    string OriginalLanguageCode { get; set; } = string.Empty;` to
    `GroupPostDetailViewModel`.
  - `src/Kumunita.Web/Controllers/PostsController.cs` — set `OriginalLanguageCode
    = result.Post.LanguageCode` in the `PostDetailViewModel` initializer, and
    append `reply.LanguageCode` as the 11th `ReplyItem` arg in the ctor call.
  - `src/Kumunita.Web/Controllers/GroupsController.cs` — set
    `OriginalLanguageCode = result.Post.LanguageCode` in the
    `GroupPostDetailViewModel` initializer, and append `reply.LanguageCode`
    as the 11th `ReplyItem` arg in the ctor call.
- **Exit:** `dotnet build` green. The three ADDs exist and are populated.
  **No new test** (U06 is the first TD test). Handoff note: 4–5 lines
  starting `## U02 — projection ADDs` — (a) the three ADD names + the
  `ReplyItem` positional ordinal (11th, after `DeletedAt`), (b) the two
  controller call sites touched (file + the `LanguageCode` source), (c) a
  confirmation `LanguageOption` and all Core documents were **not** touched
  (TD·6 / TD·7), (d) any compile warnings.

### U03 — Community view: chip row + variant swap + add-lane exclusion
- **Goal:** restructure `Views/Posts/Detail.cshtml` so the authored-in
  language is the first, default-visible chip and the title+body (post) /
  body (each reply) live in hidden variant containers the swap toggles — and
  the "Add a …" lane no longer offers the authored-in language. **Touch no
  C#, no TS, no group view.**
- **Entry reads:** `docs/design/translation-display-design.md` §Pinned
  contract (the `data-*` attributes, the variant-container structure, the
  add-lane exclusion, the soft-delete gating) + §FACES TD1/TD2/TD3/TD4/TD5/TD7/TD8;
  `src/Kumunita.Web/Views/Posts/Detail.cshtml` (the full current markup — the
  helpers block, the post title+body `<h4>`/`.rc-body`, the post translation
  block, the reply translation block, the ADR 0024 soft-delete branches);
  `src/Kumunita.Web/Models/PostDetailViewModel.cs` (confirm the
  `OriginalLanguageCode` ADDs from U02 are present — `Model.
  OriginalLanguageCode` and `r.OriginalLanguageCode`).
- **Deliverables (1 file, modified):** `src/Kumunita.Web/Views/Posts/
  Detail.cshtml`. Concretely, per the pinned contract:
  - In the `@{ ... }` helpers block: `var originalCode = Model.
    OriginalLanguageCode;` and redefine
    `missingLanguages = Model.Languages.Where(l => !l.HasTranslation &&
    l.Code != originalCode).ToList();` (the TD·5 / TD2 exclusion).
  - **Wrap the post surface element** (title+body + chip row) in a group
    wrapper `<div data-td-group="post">` — the toggle scope for the post's
    variants.
  - **Post title+body:** replace the single `<h4>` + `.rc-body` render with
    the **original** variant container
    `<div class="td-variant" data-td-variant="@Model.OriginalLanguageCode">`
    (visible by default) holding the title + body, and add one
    `<div class="td-variant" data-td-variant="@t.LanguageCode" style=
    "display:none">` per `PostTranslation` holding `t.Title` + `t.Body`.
  - **Post chip row:** the original chip
    `<button type="button" class="badge text-bg-light border" data-
    translation-chip data-td-group="post" data-td-variant="@Model.
    OriginalLanguageCode">` (always present — TD·1/TD·2) plus one chip per
    `PostTranslation` (`data-td-variant="@t.LanguageCode"`, same
    `data-td-group="post"`). **Drop the "None yet" empty state** (TD1 — the
    original chip is always there).
  - **Reply block (per `r`):** the same structure in a group wrapper
    `<div data-td-group="reply-@r.Id">`; the original chip uses
    `data-td-group="reply-@r.Id" data-td-variant="@r.OriginalLanguageCode"`;
    the reply body lives in a
    `<div class="td-variant" data-td-variant="@r.OriginalLanguageCode">`
    (default-visible) + one hidden variant per `ReplyTranslation` (each
    `data-td-variant="@t.LanguageCode"`); the reply "Add a …" lane uses
    `l.Code != r.OriginalLanguageCode` (TD·4/TD5).
  - **Soft-delete gating (TD7):** the translation section (chips + variants +
    add-lane) renders **only** when `Model.Post.DeletedAt is null`; a
    soft-deleted post shows its placeholder and no swap. (Same for a
    soft-deleted reply row.)
  - Keep the existing "Add a …" form markup (title input for the post, body
    only for the reply) and the ADR 0022 route actions verbatim — only the
    candidate **list** changes.
- **Exit:** `dotnet build` green. The community view renders the original
  chip + variant containers + the excluded add-lane, and a soft-deleted post
  shows no swap. **No new test** (the FACES are the visual gate; U06 adds the
  C# data-shape tests). Handoff note: 5–6 lines starting `## U03 —
  community view swap` — (a) the `data-td-group` targets used ("post",
  "reply-{id}"), (b) the `missingLanguages` exclusion line (verbatim), (c)
  the soft-delete gating decision (section renders only when live), (d) a
  confirmation the add-lane **form** markup and ADR 0022 routes were
  unchanged, (e) any markup the group view (U04) must mirror.

### U04 — Group view parity
- **Goal:** apply the **identical** restructure to `Views/Groups/
  PostDetail.cshtml` (the group lane), using `Model.OriginalLanguageCode`
  and `r.OriginalLanguageCode` — byte-for-byte the same contract as U03, the
  only differences being the existing group-lane form-action routes and the
  already-present `Model.GroupId`. **Touch no C#, no TS, no community view.**
- **Entry reads:** `docs/design/translation-display-design.md` §Pinned
  contract (same contract U03 applied) + §FACES TD6; `src/Kumunita.Web/
  Views/Groups/PostDetail.cshtml` (the full current markup — mirror of the
  community view's helpers + post + reply blocks); the just-landed
  `src/Kumunita.Web/Views/Posts/Detail.cshtml` (U03's result — the exact
  markup to replicate, adjusting only the form actions);
  `src/Kumunita.Web/Models/GroupViewModel.cs` §`GroupPostDetailViewModel`
  (confirm `OriginalLanguageCode` is present from U02).
- **Deliverables (1 file, modified):** `src/Kumunita.Web/Views/Groups/
  PostDetail.cshtml` — the U03 contract applied verbatim for the group lane
  (original chip first + default-visible, hidden variant containers, chip
  row per post/reply, add-lane excluding `r.OriginalLanguageCode` /
  `Model.OriginalLanguageCode`, soft-delete gating), keeping the group lane's
  existing form-action routes and `Model.GroupId`.
- **Exit:** `dotnet build` green. The group-lane post/reply detail behaves
  exactly as the community lane (TD6). **No new test.** Handoff note: 4–5
  lines starting `## U04 — group view parity` — (a) a confirmation the
  markup is structurally identical to U03's (same `data-*` attributes, same
  variant-container shape, same exclusion), (b) the group-lane form-action
  routes preserved, (c) any deliberate divergence from U03 (there should be
  none beyond the routes / `GroupId`), (d) a confirmation the two lanes now
  share the contract U05's TS toggles against.

### U05 — TS swap module + layout wiring
- **Goal:** add the one `tsc`-only ES module that toggles the visible
  variant per chip click (display-only, no HTML injection, no re-fetch), and
  load it in the shared layout. **Touch no C#, no Razor view markup.**
- **Entry reads:** `docs/design/translation-display-design.md` §Pinned
  contract (the TS module contract + the `data-*` attribute set) + §
  Invariants TD·3 / TD·4 / TD·8; `src/Kumunita.Web/client/lib/avatar.ts`
  (the `addEventListener` ES-module pattern to mirror — no bundler, no
  framework); `src/Kumunita.Web/client/lib/avatar-upload.ts` (a second module
  example — how a module scopes to its own elements); `src/Kumunita.Web/
  Views/Shared/_Layout.cshtml` (the `<script type="module" src="~/js/lib/
  *.js">` load block to append to); the `tsconfig.json` + `package.json`
  (confirm `rootDir=client`, `outDir=wwwroot/js`, and that `npm run build`
  compiles `client/**`).
- **Deliverables (2 files — 1 new, 1 modified):**
  - `src/Kumunita.Web/client/lib/translation-swap.ts` (new) — for each
    `[data-translation-chip]`, on `click`: `g = chip.closest('[data-td-
    group]')` (the group wrapper U03/U04 render); `v = chip.dataset.tdVariant;
    g.querySelectorAll('[data-td-variant]').forEach(c => { c.style.display =
    c.dataset.tdVariant === v ? '' : 'none'; })`. Optionally mark the active
    chip (e.g. `aria-pressed` / a class). **No** `innerHTML`, no fetch, no
    navigation. ~40–70 LOC, no imports.
  - `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — add one line to the
    module-load block: `<script type="module" src="~/js/lib/
    translation-swap.js"></script>`.
- **Exit:** `npm run build` green (emits `wwwroot/js/lib/translation-
  swap.js`) **and** `dotnet build` green. Clicking a chip swaps the
  visible variant and clicking the original returns to it; with JS disabled
  the original remains visible and all variants are present in the DOM
  (TD·8). Handoff note: 4–5 lines starting `## U05 — TS swap module` — (a)
  the selector strings the module uses (verbatim, must match U03/U04),
  (b) the layout script line added (file + the exact `<script>` tag), (c)
  a confirmation there is **no** `innerHTML`/`fetch`/navigation (TD·4/TD·3),
  (d) the `npm run build` + `dotnet build` result, (e) the emitted JS path.

### U06 — C# data-shape tests + acceptance gate + doc sync
- **Goal:** pin the U02 data ADDs with controller-level tests, record the
  FACES acceptance gate, and sync the durable docs (the ADR index, the
  ARCHITECTURE.md persistence/VM note, and the README roadmap note) so the
  lane is honest. **Touch no view markup, no TS.**
- **Entry reads:** `docs/design/translation-display-design.md` §Pinned
  contract (the **pinned test names** + the three VM ADDs) + §FACES TD1–TD8;
  the existing controller-test harness — `tests/Kumunita.Web.Tests/
  CommunityControllerTests.cs` or a posts/groups controller test in the same
  folder (the fake-store / fake-`IUserInfoService` setup to mirror); the two
  detail controllers (confirm the exact `OriginalLanguageCode` source from
  U02); `docs/adr/README.md` (the ADR index table to append 0027 to);
  `docs/ARCHITECTURE.md` (the persistence / bounded-context section to note
  the additive VM fields — no new document); `README.md` (the **Roadmap**
  section — the "multilingual is next" note; the TD lane is a named lane, so
  the M-letter roadmap is untouched, but a one-line "Translation display
  (TD)" entry under the current in-progress lane is the honest sync).
- **Deliverables (≤ 4 files — 1 new test file, doc edits):**
  - `tests/Kumunita.Web.Tests/TranslationDisplayTests.cs` (new) — the
    **pinned** tests (exact names from the design doc): a post authored in a
    given language → `PostDetailViewModel.OriginalLanguageCode` equals that
    language; a reply authored in a language → `ReplyItem.OriginalLanguageCode`
    equals it; the group lane → `GroupPostDetailViewModel.OriginalLanguageCode`
    equals the post's authored-in language; and a post with an added
    translation in a *different* language → `OriginalLanguageCode` is not
    among the `PostTranslations`' codes (the original is distinct from the
    added rows). Mirror the existing controller-test harness; no new Core
    test (TD·7 — no Core change).
  - `docs/adr/README.md` — append the 0027 row (Accepted) after 0026.
  - `docs/ARCHITECTURE.md` — one short note in the persistence / detail-VM
    area: the TD lane adds **no** document and **no** migration; the
    authored-in code is surfaced on `PostDetailViewModel` /
    `GroupPostDetailViewModel` / `ReplyItem` from the existing ADR 0018
    field (reference ADR 0027).
  - `README.md` — the **Roadmap** section: add the TD lane as a named
    (non-M-letter) entry under the current in-progress work, mirroring how
    the group-posts (`GP`) and rich-content (`RC`) lanes are noted. **Do not
    move M4/M5/M6.**
- **Exit:** `dotnet build` green **and** the two test assemblies run green
  via the repo's xunit.v3 in-process runner (per `AGENTS.md`'s "Running the
  tests" note — `dotnet exec` on each `*.dll`, **not** `dotnet test` /
  Test Explorer). The pinned tests pass. The ADR index, ARCHITECTURE.md, and
  README roadmap reflect the lane. **This is the final unit:** append the
  closing `## U06 — tests + doc sync` handoff section with the acceptance
  verdict (TD1–TD8 observed) and the "lane complete" line, so the next
  reader sees the lane is closed.

---

## Drift guard (frozen once U01 is written)

The TD invariants (TD·1–TD·8), the FACES (TD1–TD8), the three VM ADDs
(`PostDetailViewModel.OriginalLanguageCode`, `GroupPostDetailViewModel.
OriginalLanguageCode`, `ReplyItem.OriginalLanguageCode`), the `data-*`
attribute set (chip `data-translation-chip` + `data-td-group` + `data-td-
variant`; group wrapper `data-td-group`; variant container `data-td-variant`;
targets `"post"` / `"reply-{id}"`), the add-lane exclusion rule, the soft-
delete gating, the TS module contract, and the pinned test names — all frozen
pins in `docs/design/translation-display-design.md`. The **untouched** set is
equally pinned: the shared `LanguageOption` record (TD·6), the Core
documents (TD·7), the ADR 0022 add-translation **write** lane and its
route actions, and the M4/M5/M6 roadmap letters. Any mismatch between an
entry read and these pins is a `## U<m> — Drift pause` per unit-series rule
§5.
