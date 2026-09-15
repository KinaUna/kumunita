# Rich editor (`RE`) — WYSIWYG body editing + toolbar over the RC Markdown lane

> **Status.** This is the **lane plan** (the register — secondary tier of the
> lane's three-tier contract) for a follow-on lane that completes what the
> **rich-content lane (`RC`, ADR 0025)** deliberately left out: residents can
> *read* bold text, lists, links, and images (the RC render switch, U06), but
> they still **hand-code the Markdown** in a `<textarea>` and **see only the
> raw markup** while typing. This lane ships the authoring half: a **WYSIWYG
> editing surface** (a live rendered preview beside the source) and **toolbar
> buttons** for **bold, italic, headings, lists, inline code, link, and
>
> image** — all of it **plain TypeScript** (`tsc`-only, **no editor
> library enters `package.json`** — the RC `tsc`-only constraint stands) and
> all of it producing **the same Markdown source RC already stores, renders,
> and serves**.
>
> **Why a separate lane and not an "RC reopen"?** RC closed green
> (8/8 units in `done/`, 20 seam tests + the regression pin) and its ADR 0025
> *"Not decided here"* section **explicitly deferred** *"Any WYSIWYG /
> third-party editor — the `tsc`-only constraint stands; the composer is a
> textarea with a Markdown hint."* The deferral named the *boring* floor
> (textarea + hint), not the *ceiling* (a rendered surface + buttons). This
> lane is that ceiling, and it is **additive on top of RC**: it changes **zero**
> stored fields, **zero** `MarkdownRenderer` rules, **zero** routes, **zero**
> `ImageIds` population logic — it only changes **what a resident types with
> and sees while typing**, and it reuses RC's `MarkdownRenderer` for the
> preview. It is named `RE` (the editor half of the RC authoring surface)
> following the **named-lane with a short ID** convention (`ML`, `GP`, `RC`).
> **M4/M5/M6 stay Events / Projects / Portability.** No roadmap letter moves.

## The two gaps this lane closes (verified against the code, 2026-09-15)

These are the facts the plan codes against — each was read directly, not
assumed.

1. **The composer shows raw Markdown only.** Every body authoring surface is a
   `<textarea>` bound to `Body`: `Posts/{New,Edit}`, `Groups/{New,Edit}`,
   `Announcement/{New,Edit}`, `Languages/PreviewPage`, and the reply editors
   (`Posts/Detail`, `Groups/PostDetail`, `Announcement/Detail`). There is no
   rendered preview beside the field, and no toolbar — a resident who wants
   bold types `**text**` and sees `**text**` (the RC `rc.markdown_hint`
   placeholder is the *only* affordance, and even that key was not yet
   registered — see the RC U05 drift pause).
2. **The renderer and the store are already Markdown.** `MarkdownRenderer`
   (`Kumunita.Web.Security`) renders `**bold**`, `*italic*`, headings, lists,
   paragraphs, `code`, `[label](url)`, and `![alt](src)` —
>
   the *exact* set the toolbar must emit (**no blockquote** — `MarkdownRenderer`
>
   has no blockquote branch; see the U1 drift pause). `Body` is stored as a Markdown
   `string` (RC R·7, zero migrations) and the RC render switch
   (`rc-body`/`rc-image` CSS, `PlainTextPreview`) already turns that source
   into rendered HTML on every read surface. So a WYSIWYG surface that emits
   **these** markers into **these** textareas is correct end-to-end with **no
   server change** — the only new artifact is the client-side editing UX.

## Understanding

The value RC set out to deliver — *"a resident can bold a heading, start a
list, or show a photo"* (RC `Scope`, `R1`) — is **half-delivered**: the
*read* side renders it, but the *write* side still asks a resident to know
Markdown syntax. A one-neighborhood platform's residents are not developers;
the honest completion is a surface that **shows what they will publish while
they type it** and **lets them apply formatting with a button**.

This lane moves the platform from *"the renderer shows Markdown once it is
saved"* to **"a resident formats with a toolbar, sees a live rendered preview,
and the saved body is the same Markdown source the read path already
renders."** The value chain moves one arrow: from *the read path renders* to
*the write path authoring-feels-like-what-the-read-path-renders*.

**The one thing every unit must respect:** this lane is **client-only and
additive.** It adds **no** Core field, **no** route, **no** `AccessAction`,
**no** `IMediaStore` method, **no** second renderer, and **no** editor
dependency. The preview *is* `MarkdownRenderer` (RC R·1 — one renderer, now
used for authoring preview too); the toolbar *is* text splicing into the
existing `Body` textarea (RC R·3's server-side `ImageIds` parse is untouched —
the toolbar emits the *same* `![alt](/content-image/{id})` link RC's
`ContentImageIds.ExtractContentImageIds` already understands). Every invariant
RC pinned (R·1–R·7) keeps binding **unchanged**; this lane adds two new ones
(RE·1, RE·2) and a small FACES set (RE1–RE5).

**The design decisions (settled — D1, D2, D3; these are this lane's ADR
payload):**

- **D1 — WYSIWYG = live preview, not a contenteditable.** A `contenteditable`
  (or a `ProseMirror`/`Quill`/`Tiptap` dependency) would (a) break the
  `tsc`-only constraint, (b) introduce an HTML→Markdown *round-trip* the
  codebase has no engine for, and (c) fight RC's `ImageIds` parse (which reads
  the *source* text, not a DOM). The boring, correct choice is a **split view**:
  the existing `<textarea>` (the *source*, the form field the server binds)
  beside a **read-only rendered pane** that re-renders `MarkdownRenderer`
  output on every input. This is exactly the "live preview pane" RC U05's
  `Scope` line promised ("a `<textarea>` + … **live preview pane**") and RC
  never shipped. **No second renderer, no HTML round-trip, no dependency.**
- **D2 — Toolbar = Markdown text splicing, in one shared TS module.** A
  `client/lib/rich-editor.ts` ES module (the `insert-image.ts` / `avatar.ts`
  convention — `tsc`-only, self-wiring, `addEventListener`) renders a toolbar
  above any `textarea[data-rich-editor]` and, per button, **splices the
  corresponding Markdown markers** into the textarea's selection (or wraps the
  selection, or inserts a block at the cursor) — the same `selectionStart` /
  `selectionEnd` / `selectionStart/End` re-focus idiom RC's `insert-image.ts`
  already uses for the image link. Buttons are **`<button type="button">`**
  (never `submit` — they must not post the form), keyboard-accessible, and
  localized via the `<kw-l>` TagHelper (ML-UI M·10). The module exposes
  **`bindRichEditor(root: HTMLElement): void`** and self-wires at load (the
  `insert-image.ts` pattern), so a Razor view only needs the module include.
- **D3 — The preview is a *separate* re-implementation of the RC render,
  for the client only — it reuses the *server* rules, not the server
  code.** The client cannot call C#. So the preview pane renders the body with
  a **small client-side Markdown → HTML function** (`renderPreview(md: string)`)
  that **mirrors RC's pinned subset verbatim** (bold/italic/code, headings,
  ul/ol lists, links, the `![alt](/content-image/{id})` image under the same
  `rc-image` class + same `IsSafeImageSrc` allowlist semantics, paragraphs,
  fenced code). This is **not** a second *renderer on the server* (R·1 still
  holds — the *read* path is still only `MarkdownRenderer`); it is the
  **authoring-side preview** RC's `Scope` promised. The drift guard pins the
  exact marker set + the exact `src` allowlist predicate so the preview and
  the server renderer can never visibly diverge (a toolbar button that emits a
  marker the preview doesn't render is a drift pause). The preview output is
  **HTML-escaped first** (the RC R·2 escape-first stance, client-side) and
  shown in a `role="region" aria-label` pane — **never** `document.write`,
  **never** user HTML is trusted (the preview *is* the escape-first
  construction, same as `MarkdownRenderer`).

## Assumptions (pinned)

- **RC is frozen and correct.** `MarkdownRenderer`, `ContentImageIds`,
  `IMediaStore`, `MediaObject`, `PostService`/`AnnouncementService`, the four
  `ImageIds` fields, the `GET`/`POST /content-image` routes, the `.rc-body` /
  `.rc-image` CSS — **all stay as they are.** This lane adds **no** re-shape of
  any of them. **The only new artifact is client-side:** one TS module
  (`rich-editor.ts`), one CSS block (`.rc-editor-*`), the toolbar + preview
  markup on the composer views, and one `<kw-l>` key (`rc.editor.*`). **No
  `.csproj` change. No `package.json` change (still `typescript` only).**
- **The toolbar emits only the RC-pinned marker set.** Bold `**`, italic `*`,
  inline code `` ` ``, headings `#`–`###`, unordered list `- `, ordered list
  `1. `, link `[label](url)`, and the **RC image link**
  `![alt](/content-image/{id})` (the image button reuses RC's *existing*
  upload lane — `insert-image.ts` / `POST /content-image` — it does **not**
  invent a second upload). Every marker the toolbar can emit must be one
  `MarkdownRenderer` already renders (RC R·1) — a button that emits a
  marker the renderer doesn't handle is a **drift pause**, not a feature.
- **The preview mirrors the RC subset exactly** (D3): the 7-step
  `PlainTextPreview` strip set is the *inverse* of the toolbar's 7+ buttons —
  the same markers, both directions, both pinned by name in the design doc.
  Fenced code blocks, tables, footnotes, and raw HTML stay **out** on **both**
  the toolbar and the preview (RC's "intentionally out of scope" set holds).
- **`Body` stays a `string`; the server still parses `ImageIds` the RC way.**
  The toolbar splicing an `![alt](/content-image/{id})` link is byte-identical
  to what a resident hand-typed would be — RC's `ContentImageIds` regex
  (`/content-image/([0-9a-f]{1,128})`) picks it up on save exactly as before.
  **Zero server change, zero migration, zero new audit row** (the authoring
  surface is the same authenticated form post RC already guards).
- **Every surface the toolbar lands on is an authenticated composer** (post,
  group-post, announcement, static-page editor, and the reply composers) —
  all `[Authorize]`d, so the toolbar's own button clicks (which are local
  DOM text edits, **not** fetches, except the image button) need **no**
  additional CSRF surface. The **image button** is the one that uploads and
  it reuses RC's already-CSRF-aware `apiFetch` upload (the RC `U04` drift
  note's anti-forgery convention) — **no new** `api.ts` method.
- **Every composer surface gets the text toolbar + live preview** (Post
  New/Edit, Group New/Edit, Announcement New/Edit, the static-page editor,
  **and the reply composers** `Posts/Detail` / `Groups/PostDetail` /
  `Announcement/Detail` — wired in U04–U06). This is the WYSIWYG value, and it
  works on **all** of them because the one `MarkdownRenderer` already renders
  every one of those bodies (the read path is unchanged, so RE·2 preview↔
  renderer parity holds everywhere).
- **The image button is available *only* where RC's content-image lane is
  complete** (the write lane populates `ImageIds` **and** the
  `GET /content-image/{id}` route has a working owner branch for that owner).
  Verified against the code (2026-09-15), independent of the toolbar — these
  are **pre-existing RC gaps** (drift pauses) that RE·3 does not invent:

  | Surface | Write lane (`ImageIds`) | Serve branch | **Image button** |
  |---|---|---|---|
  | Post **New** (`PostsController:692`) | ✅ | ✅ post branch | **ON** |
  | Post **Edit** (`UpdatePostAsync`) | ❌ RC drift pause (c) | ✅ post branch | **OFF** |
  | Group **New** (`GroupsController:1085`) | ✅ (drift pause (b) resolved) | ✅ post branch | **ON** |
  | Group **Edit** (`UpdateGroupPostAsync`) | ❌ RC drift pause (c) | ✅ post branch | **OFF** |
  | Announcement **New/Edit** (`AnnouncementController:455,575`) | ✅ | ❌ RC U03 drift pause (inert-404) | **OFF** |
  | Static page (`LanguagesController:271`) | ✅ | ✅ page branch (public) | **ON** |
  | Reply **New/Edit** (`Create/UpdateReplyAsync`) | ❌ RC drift pause (a) | ❌ RC U03 drift pause (inert-404) | **OFF** |

  So the image button is **ON** on Post New, Group New, and the static-page
  editor only; **OFF** (via U03's `data-rich-editor-no-image` option) on Post
  Edit, Group Edit, both Announcement composers, and all reply composers —
  each gate names the specific RC drift pause in the handoff note. The text
  toolbar + preview are **unaffected** (ON everywhere). RE ships a working
  button only where RC can actually serve the image; it does **not** ship a
  button that produces 404s.
- **Out of scope (named deferrals for a future RE-2 lane, not a renumber):**
  **undo/redo** (the browser's native `Ctrl+Z` on the textarea is the floor;
  a custom history stack is a future lane), **spellcheck / dictionary**
  (native `textarea` behavior holds), **drag-drop image reordering** (RC
  deferral, unchanged), **WYSIWYG *contenteditable* / third-party editor
  (ProseMirror/Quill/Tiptap)** — the `tsc`-only + no-HTML-round-trip stance
  (D1) is a **hard** non-negotiable, not a deferral, and **tables / footnotes**
  in the toolbar or preview (RC's out-of-scope set, unchanged on both sides).
  The **gated image lanes** (un-gating the image button on Post Edit / Group
  Edit / Announcement / replies) are **RC follow-ups**, not RE ones: each needs
  the missing RC write-lane `ImageIds` seam (drift pause (a)/(c)) and/or the
  missing RC serve-branch adapter (RC U03) — both RC seams RE·3 does not
  invent. Once an RC lane is complete, that surface's image button un-gates by
  removing `data-rich-editor-no-image` (a one-attribute change, no RE code).
- **Test / acceptance model (unchanged, RC/ML-UI convention).** `xunit.v3`;
  on this machine the discovery path (VS Test Explorer / `dotnet test`) is the
  known-broken bug — the reliable runner is `dotnet build` + `dotnet exec
  <assembly>.dll` (AGENTS.md). The TS unit's acceptance is **`npm run build`
  green** + a **client-side test** (the `Kumunita.Web.Tests` assembly gains a
  small `RichEditorTests` that asserts `renderPreview` / the toolbar splice
  helpers produce the pinned Markdown — the helpers are exported pure
  functions so they are unit-testable without a browser). **No** e2e browser
  automation (none exists in this repo; the RC/ML-UI lanes set that floor).

## Invariants (this lane)

The RC lane's **R·1–R·7** all still bind and are **not re-stated**. Two new
invariants are added for the surface this lane ships:

| # | Invariant | Pinned where |
|---|-----------|--------------|
| **RE·1** | **One source of truth = the `Body` textarea.** The toolbar and the preview are both **functions of the textarea's current value** — the textarea is the single source, the preview is a pure render of it, and a toolbar button is a pure text splice into it. **No** separate hidden state, **no** contenteditable mirror, **no** `body.innerHTML` as a source of truth (the RC R·1 one-renderer / R·3 source-is-the-`string` stance, held client-side). The form submits the textarea's `value` — **unchanged** from RC. | **U03** (the module) · **U04–U06** (the surfaces) |
| **RE·2** | **Preview ↔ renderer parity (client-side R·1).** `renderPreview` mirrors the **exact** RC-pinned subset (bold/italic/code, `h1`–`h3`, `ul`/`ol`, `> `, link, the `/content-image/{hex}` image under `rc-image`, paragraph, fenced code) and **only** that set; the `src` allowlist predicate on the client is the **same** accept/reject semantics as `MarkdownRenderer.IsSafeImageSrc` (route-shape `+` relative, every scheme rejected). A toolbar button that emits a marker `renderPreview` doesn't render (or vice-versa) is a **drift pause**, not a feature. The preview output is **escaped-first** (client-side R·2). | **U03** (the module) · **U07** (the parity tests) |
| **RE·3** | **No new dependency, no new server surface, no new audit row.** `package.json` stays `typescript`-only; no `.csproj` package reference is added; no new route / `AccessAction` / `IMediaStore` method; the toolbar's only *network* action is the RC image upload (reused, already CSRF-aware + audited-by-RC). The lane is **client-only and additive.** | **U03** · **U08** (the close's "no new dependency" pin) |

## FACES (pinned, 5)

Five resident/admin-facing scenarios, **RE1–RE5**, each exercising one or more
invariants. The seam tests (§Pinned seam tests, U07) cover these 1:1.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| **RE1** | a resident opens the post composer, sees a **toolbar** (B / I / H / list / link / image) above the body field and a **live preview pane** that renders what they type as they type — `**bold**` shows **bold** in the pane while the textarea still holds `**bold**`. | RE·1, RE·2 |
| **RE2** | the resident selects the word `hello` and clicks **B** → the textarea now reads `**hello**` with the selection preserved; the preview shows **hello** bold. Clicking **B** again (deselected, cursor after) toggles it back to `hello`. | RE·1 |
| **RE3** | the resident clicks **Link** with a selection → the selection becomes `[hello](https://…)` (a prompt for the URL, or the existing link rule — pinned in the design doc); the preview renders it as an `<a>`. A **remote image** typed `![x](https://evil/i.png)` renders in the preview **as plain text** (the client allowlist mirrors `IsSafeImageSrc`), not an `<img>`. | RE·2 |
| **RE4** | on a surface where the image button is available (Post New, Group New, static-page editor), the resident clicks **Image** → the RC upload fires (the existing `POST /content-image`), and on success `![{alt}](/content-image/{id})` is spliced in and the preview renders an `<img class="rc-image">`. Saving populates `ImageIds` **exactly as if the resident had hand-typed the link** (RC R·3 unchanged — the server parse is byte-identical). On a surface where RC's image lane is incomplete (Post Edit / Group Edit / Announcement / replies), the button is **absent** (U03's `data-rich-editor-no-image`) so the resident can never produce a 404'ing image — the text toolbar + preview are unchanged there. | RE·1, RE·3, RC R·3 |
| **RE5** | a **signed-in** GlobalAdmin opens the **about** page editor (`/admin/languages/…` static-page surface) → the toolbar + preview are there exactly like a post composer, and saving renders on `/about` for an unauthenticated visitor **unchanged** (RC R6 — the editor is a nicer front to the same `LocalizedPage.Body` RC already serves). | RE·1, RC R6 |

## Pinned contract (the **new** artifacts this lane adds — additive only)

```ts
// ── U03: the shared editor module (NEW — client/lib/rich-editor.ts) ───────
// tsc-only (RE·3). Self-wires at load (the insert-image.ts pattern). Exports
// two PURE functions (unit-testable, RE·2) + one binder (RE·1).
export function renderPreview(markdown: string): string;     // RE·2, D3
export function applyToggle(markdown: string, sel: [number, number],
                            kind: 'bold' | 'italic' | 'code'):
                            { value: string; sel: [number, number] };  // RE·1
export function applyBlock(markdown: string, caret: number,
                           kind: 'h1'|'h2'|'h3'|'ul'|'ol'):
                            { value: string; caret: number };          // RE·1 (no 'quote' — MarkdownRenderer has no blockquote branch; U1 drift pause)
export function applyLink(markdown: string, sel: [number, number],
                          url: string): { value: string; sel: [number, number] }; // RE·2
export function imageLink(alt: string, id: string): string;  // = `![alt](/content-image/{id})` (RC R·3 byte-identical)
export function bindRichEditor(root: HTMLElement): void;      // RE·1, D1/D2
```

```css
/* ── U03: the editor CSS (NEW block in wwwroot/css/site.css) ───────────── */
.rc-editor        { /* the split: source | preview, responsive */ }
.rc-editor-toolbar{ /* the button row: B I H list link image */ }
.rc-editor-toolbar .rc-btn { /* button reset + hover + focus-visible */ }
.rc-editor-pane   { /* the read-only preview: reuses .rc-body for body copy */ }
.rc-editor-pane   { /* + .rc-image rule already exists (RC U06) — the preview
                       wraps its output in <div class="rc-body rc-editor-pane"> */ }
```

```razor
<!-- ── U04–U06: the toolbar + preview markup (one pattern, every composer) -->
<div class="rc-editor">
  <div class="rc-editor-toolbar" data-rich-editor><!-- U06's reply views add
         data-rich-editor-no-image here: the image button is then omitted -->
    <!-- <button type="button" class="rc-btn" data-md="bold"><kw-l key="rc.editor.bold">B</kw-l></button>
         … one per marker … the image button is data-md="image" and reuses
             insert-image.ts's upload (U03's bindRichEditor wires it). -->
  </div>
  <textarea class="form-control" name="Body" data-rich-editor data-image-target>…</textarea>
  <div class="rc-editor-pane rc-body" aria-live="polite" data-rich-editor-preview></div>
</div>
<script type="module" src="~/js/lib/rich-editor.js"></script>
```
**The image-gated variant (U04 Post Edit, U05 Group Edit + both Announcement
composers, U06 reply composers):** identical block, **except** the toolbar
carries `data-rich-editor-no-image` (U03's `bindRichEditor` then **omits** the
`data-md="image"` button) and the textarea drops `data-image-target` (RC's
image lane is incomplete on that surface — the specific drift pause is named
per-surface in the register's image-button matrix + the unit plan). The text
toolbar + preview are byte-identical to the composer pattern on **every**
surface (gated or not).

**Everything else is frozen** (RC R·1–R·7 + the RC `Pinned contract`). The
`Body` textarea keeps its `name` and its `value`. Its RC `data-image-target`
attribute is kept **only on the surfaces where the image button is on** (Post
New / Group New / static page — RC's write + serve are complete there, so the
image button + a hand-typed image link both feed the same RC `ImageIds`
parse); it is dropped on the image-gated surfaces. **No** new view-model
field, **no** new controller action, **no** new Core type.

## Approach

Three tracks, sequenced. **Track A (the module + CSS, U03):** the pure
functions (`renderPreview`, `applyToggle`, `applyBlock`, `applyLink`,
`imageLink`) + `bindRichEditor` in one self-wiring ES module, the
`.rc-editor-*` CSS block, and the two unit-testable exports. **Track B
(wire the surfaces, U04–U06):** the toolbar + preview markup on the post
composer (U04), the group-post + announcement composers (U05), and the
static-page editor (U06) — **one pattern, copy-verified** (the drift-guard
checks no surface drifts from the pinned pattern). **Track C (governance,
U07–U08):** the parity tests + acceptance gate (U07), the close — ADR 0031
(settles D1/D2/D3), `Milestones.cs` / README / `MilestonesTests.cs` sync,
folder moves (U08).

Each **code** unit ends **build green** (`dotnet build Kumunita.slnx -c
Debug`) **and `npm run build` green** (a TS unit). The **test** unit (U07)
verifies with `dotnet exec` (not `dotnet test`). Doc units (U08) never build
beyond the re-verify.

---

## Workflow — handoff protocol for fresh-context agents

**Per-unit template** (each unit plan file follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal file
list, 3–5 files <~300 lines each — the design/seam it cites is named);
**Deliverables** (a closed set of new/modified files, ≤ ~4 files / ~500 LOC,
no misc cleanups); **Exit** (build green for the touched projects + `npm run
build` green for a TS unit; the handoff-note entry appended *before* the
folder move; the unit plan file moved to `done/`).

**Shared state (three-tier contract):**
- **Primary — `docs/design/rich-editor-design.md`** (U01) — pins the
  invariant numbers (**RE·1–RE·3**), the FACES (**RE1–RE5**), the **exact
  marker set** the toolbar emits + the `renderPreview` subset + the client
  `IsSafeImageSrc`-mirror predicate, the **pinned seam-test names**, the
  **acceptance gate**, and the **drift guard**. It re-anchors RC R·1–R·7
  (unchanged) as the frozen base this lane builds on.
- **Secondary — this file** (`docs/plans-milestones/plan-rich-editor.md`)
  — the unit registry with each unit's deliverables and exit criteria (and
  pointers to the per-unit plan files).
- **Scratch — `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md`**
  (**created by U0** — the kickoff). One section per unit, appended (never
  rewritten); each unit writes exactly one short section before it exits;
  the next unit reads only that section + its own entry-read list.

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never re-shapes a frozen RC seam (`MarkdownRenderer` /
`ContentImageIds` / `IMediaStore` / the four `ImageIds` fields / the
`/content-image` routes / `.rc-body`·`.rc-image` CSS / `insert-image.ts`) —
**any re-shape is a `## U<m> — Drift pause`**; (3) never emits a toolbar
marker the RC-pinned subset doesn't render (RE·2) — a new marker is a **future
lane**, not a button; (4) never adds a package dependency or a `.csproj`
reference (RE·3); (5) never makes the preview a *source of truth* or the
textarea read the preview (RE·1) — the textarea is the one source; (6) if
entry reads reveal a frozen RC seam missing or different than RC's close
claims, the unit **pauses** and records `## U<m> — Drift pause` in the
handoff note.

**When a unit is done:** the agent appends its handoff section, **then** moves
its plan file from `docs/plans-milestones/in-progress/` to
`docs/plans-milestones/done/` (move **last** — the `done/` folder is the
"this is finished" signal the next agent checks).

---

## Units (9 total — one plan file each, created by each unit as it starts)

| U | Goal (one line) | Plan file (in-progress → done on exit) |
|---|---|---|
| U0 | Kickoff — create the scratch handoff note, verify the two RC gaps still hold (raw-markup-only composers, no preview/toolbar), author this file's per-unit entry-read lists | `rich-editor-u00-plan.md` |
| U1 | The design doc (`rich-editor-design.md`) + **ADR 0031** (settles D1 split-view / D2 toolbar-splice / D3 client preview parity) — the RE·1–RE·3 + RE1–RE5 pins, the marker set, the client `IsSafeImageSrc` mirror, the pinned seam-test names, the gate, the drift guard | `rich-editor-u01-plan.md` |
| U2 | (No code) the **preview render lane**: confirm `MarkdownRenderer` + `ContentImageIds` + `.rc-body`/`.rc-image` are the frozen render baseline the client preview must mirror — a **read-only** verification + a one-paragraph "mirror checklist" in the handoff note (guards against U03 inventing a divergent marker set) | `rich-editor-u02-plan.md` |
| U3 | The **shared editor module** (`client/lib/rich-editor.ts`: `renderPreview` / `applyToggle` / `applyBlock` / `applyLink` / `imageLink` / `bindRichEditor`) + the `.rc-editor-*` CSS block — the **only** new code, the **only** unit that writes TS | `rich-editor-u03-plan.md` |
| U4 | Wire the toolbar + preview on the **post** composer (`Posts/{New,Edit}`) — the pattern the rest copy. Image button **on** for Post New (RC write lane wired), **off** for Post Edit (RC drift pause (c): `UpdatePostAsync` doesn't populate `ImageIds`) | `rich-editor-u04-plan.md` |
| U5 | Spread the toolbar + preview to the **group-post** (`Groups/{New,Edit}`) + **announcement** (`Announcement/{New,Edit}`) composers — copy-verified against U04's pattern. Image button **on** for Group New, **off** for Group Edit (RC drift pause (c) analog) and **off on both Announcement composers** (RC U03 drift pause: serve branch inert-404) | `rich-editor-u05-plan.md` |
| U6 | Wire the toolbar + preview on the **static-page editor** (`Languages/PreviewPage`, the RC R6 authoring side — image button **on**, RC lane complete) **and** the **reply composers** (`Posts/Detail`, `Groups/PostDetail`, `Announcement/Detail`) — the reply surfaces get the same text toolbar + preview, with the **image button gated off** (RC's reply-image store+serve lane is an unresolved RC drift pause; RE·3 forbids inventing it) | `rich-editor-u06-plan.md` |
| U7 | The **parity + splice** seam tests (RE1–RE5 as `renderPreview` / `applyToggle` / `applyBlock` / `applyLink` / `imageLink` unit tests in `Kumunita.Web.Tests`) + run + record the acceptance gate | `rich-editor-u07-plan.md` |
| U8 | Close — **ADR 0031** landing + `Milestones.cs` `RE` roadmap + README + `MilestonesTests.cs` + the `rc.editor.*` `<kw-l>` key registration + folder moves | `rich-editor-u08-plan.md` |

**Execution order is strict** U0 → U8. A unit may run against whatever the
previous state is (every exit is self-verified: code units by a green build
+ `npm run build`; the test unit by the recorded `dotnet exec` run; doc units
by section presence). U2 is **no-code** (a read-only mirror-checklist unit,
like RC's U01 design-doc split — it keeps U03's entry-reads small for a 32K
window and pins the "what the preview must mirror" list before any client
code exists).

### Per-unit entry-read list (the minimal files, each unit's "start here")

- **U0** — `docs/plans-milestones/plan-rich-editor.md` (this file),
  `docs/plans-milestones/done/rich-content-handoff-notes.md` (the RC close +
  its five drift pauses — **read the drift-pause sections**; they name the
  per-surface `ImageIds` write gaps **and** the serve-branch gaps the image
  button will be gated against: Post Edit [drift pause (c)], Group Edit [the
  `UpdateGroupPostAsync` analog], Announcement [RC U03], Replies [drift pause
  (a) + RC U03]), `src/Kumunita.Web/client/lib/insert-image.ts` (the TS module
  + self-wire + cursor-splice pattern to mirror),
  `src/Kumunita.Web/Views/Posts/New.cshtml` (the body textarea + the
  `rc-insert-image` block the toolbar replaces/complements), and
  `src/Kumunita.Web/Controllers/ContentImageController.cs` (the per-owner
  serve branches — the reason the image button is gated where RC's serve is
  inert-404).
- **U1** — `docs/adr/0025-rich-content-markdown-and-content-images.md` (the ADR
  shape + the *"Any WYSIWYG / third-party editor"* non-decision this ADR
  amends), `src/Kumunita.Web/Security/MarkdownRenderer.cs` (the **exact**
  supported subset — the preview's mirror list + the toolbar's marker ceiling),
  `src/Kumunita.Web/Security/ContentImageIds.cs` (the `ImageIds` parse the
  image button must stay byte-compatible with), `docs/design/rich-content-design.md`
  §Pinned contract (the `.rc-body`/`.rc-image` + `IsSafeImageSrc` the preview
  reuses), `docs/adr/0030-role-independence-….md` (the **current highest** ADR
  number — ADR 0031 is the next).
- **U2** — `src/Kumunita.Web/Security/MarkdownRenderer.cs` (full read),
  `src/Kumunita.Web/wwwroot/css/site.css` (the `.rc-body`/`.rc-image` block,
  ~lines 733–790), `src/Kumunita.Web/Security/ContentImageIds.cs`,
  `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` (the 7 pinned tests —
  the parity the preview must hold).
- **U3** — `src/Kumunita.Web/client/lib/insert-image.ts` + `api.ts` (the
  module + `apiFetch` + self-wire conventions), `src/Kumunita.Web/Security/MarkdownRenderer.cs`
  (the marker → HTML mapping `renderPreview` mirrors),
  `src/Kumunita.Web/wwwroot/css/site.css` (the `.rc-editor-*` block's home +
  the `.rc-body` it reuses), `src/Kumunita.Web/Views/Shared/_Layout.cshtml`
  (the `type="module"` script include pattern — **the layout already includes
  `insert-image.js`? confirm by grep; if not, U04 adds the include, not U03).
- **U4** — `src/Kumunita.Web/Views/Posts/{New,Edit}.cshtml` (the two body
  textareas + the existing `rc-insert-image` block), U03's
  `client/lib/rich-editor.ts` (the `data-rich-editor` contract the markup
  must satisfy), `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the module
  include), the `<kw-l>` TagHelper (`src/Kumunita.Web/TagHelpers/`) +
  `KnownTranslationKeys` (the `rc.editor.*` keys — **U08** registers them,
  U04 uses them with the TagHelper's `en`-fallback so the keys may be
  un-registered on a fresh boot without breaking — **confirm** the TagHelper
  floor is the key itself, ML-UI M·1).
- **U5** — `src/Kumunita.Web/Views/{Groups/{New,Edit},Announcement/{New,Edit}}.cshtml`
  (the four body textareas + their `rc-insert-image` blocks), U04's post
  markup (the pattern to copy **verbatim**), U03's module.
- **U6** — `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml` (the
  static-page body textarea + its `rc-insert-image` block + the `font-monospace`
  source styling to replace with the split view), U04's pattern, U03's module
  (incl. the `data-rich-editor-no-image` option the reply views use),
  `src/Kumunita.Web/Controllers/LanguagesController.cs` (confirm the save
  action binds `Body` the RC way — **no change** expected, drift pause if it
  doesn't), `src/Kumunita.Web/Views/{Posts/Detail,Groups/PostDetail,
  Announcement/Detail}.cshtml` (the reply `name="body"` textareas — **multiple
  per view**: new-reply + edit-reply + translate-edit-reply; the unit wires the
  body ones only, **not** the `report-reason` fields), and
  `docs/plans-milestones/done/rich-content-handoff-notes.md` **drift pause (a)
  + the U03 drift pause** (the reply `ImageIds` write lane is unpopulated +
  the `GET /content-image/{id}` reply branch is inert-404 — the reason the
  reply image button is gated, RE·3).
- **U7** — `src/Kumunita.Web/client/lib/rich-editor.ts` (the pure functions to
  test — **the exports must be importable in the test; if they are not
  (module-scope side effects), the unit records a drift pause and extracts
  the pure cores into a separate `rich-editor-core.ts`**), `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs`
  (the test file convention + the RC subset the parity asserts against),
  `tests/Kumunita.Web.Tests/AnnouncementControllerTests.cs` (the NSubstitute
  harness, if any test needs a controller — most are pure-function tests).
- **U8** — `docs/adr/0030-role-independence-composable-elevated-roles.md`
  (the current highest ADR — the shape/numbering to follow; ADR 0031 is the
  next after 0030) + `docs/adr/0025-rich-content-markdown-and-content-images.md`
  (the ADR 0031 will **Amend** — the *"Any WYSIWYG / third-party editor"*
  non-decision) + `docs/adr/README.md` (the ADR index — a numbered table
  ending at **0030**; U08 adds the **0031** row),
  `src/Kumunita.Web/Milestones.cs` + `tests/Kumunita.Web.Tests/MilestonesTests.cs`
  (the roadmap + its pin — the `RE` named lane, not a renumber), `README.md`
  (the Roadmap section to keep in step), `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
  (the `rc.editor.*` key registration — the U04–U06 views reference these keys
  via `<kw-l>`; U08 lands them so a fresh-boot `en` floor is present),
  `docs/plans-milestones/` (the folder moves).

## Pinned seam tests (exact names — U07 implements, U01 names)

All in `tests/Kumunita.Web.Tests/RichEditorTests.cs` (new), **pure-function**
tests (no browser, no Postgres — the module's exports are the unit of test).
**Harness reality (RE·3's `tsc`-only + no TS test runner):** the repo has no
`vitest`/`jest`/`node --test`, so U07 implements the 10 behaviors as a small
**C# spec mirror** of `rich-editor.ts`'s pure functions (the mirror
encodes the *same* pinned contract from the design doc — it is the executable
spec, not a second product renderer) plus **one artifact pin** asserting the
`npm run build` output (`wwwroot/js/lib/rich-editor.js`) exists and exports
the pinned surface. The `dotnet exec` run (not `dotnet test`) is the gate.

1. `RenderPreview_BoldItalicCode_StillRender` — `**b** *i* ` + `` `c` `` →
   `<strong>`/`<em>`/`<code>` (the RC subset parity, RE·2).
2. `RenderPreview_HeadingsLists_StillRender` — `# H`, `- a`, `1. b` →
   `<h1>`/`<ul><li>`/`<ol><li>` (parity).
3. `RenderPreview_ImagePlatformRoute_RendersImgTag` —
   `![x](/content-image/deadbeef)` → one `<img class="rc-image">` (parity +
   the `src` allowlist accept).
4. `RenderPreview_ImageRemoteSrc_RendersAsPlainText` —
   `![x](https://evil/i.png)` → **no** `<img>`; escaped text (the client
   `IsSafeImageSrc` mirror rejects, RE·2 — mirrors RC R·2).
5. `RenderPreview_HostileMarkup_StillEscaped` — `<script>`/`onerror=` →
   escaped, no raw tag survives (client R·2).
6. `ApplyToggle_Bold_WrapsSelection_AndPreservesCaret` — `applyToggle("hello
   world",[6,11],"bold")` → `"hello **world**"`, selection now `[7,12]`
   (the RE1/RE2 FACES — the toggle is a pure, caret-preserving splice).
7. `ApplyToggle_Bold_TogglesOff_WhenAlreadyBold` — selecting an already-
   bolded word and toggling removes the markers (the RE2 "clicking B again"
   FACES).
8. `ApplyBlock_H1_InsertsHeading_AtCaret` — `applyBlock("",0,"h1")` →
   `"# "` (a block kind inserts its marker + a trailing space at the caret).
9. `ApplyLink_WrapsSelection_WithUrl` — `applyLink("hello",[0,5],
   "https://x")` → `"[hello](https://x)"` (the RE3 link FACES).
10. `ImageLink_Is_RcByteIdentical` — `imageLink("fence","deadbeef")` ===
    `"![fence](/content-image/deadbeef)"` (RC R·3 byte-identity — the
    `ContentImageIds` regex picks it up unchanged).

**Runner (per AGENTS.md):** `dotnet build Kumunita.slnx -c Debug` green,
`npm run build` green, then `dotnet exec` on `Kumunita.Web.Tests.dll`
(**not** `dotnet test` / VS Test Explorer — the xunit.v3 discovery quirk on
this machine).

## Acceptance gate (U07 records — the RC/ML-UI § three-test shape, re-anchored)

- **Closed loop** (the RE1/RE2 FACES): `applyToggle` + `applyBlock` +
  `applyLink` + `renderPreview` — a sequence of toolbar operations on a body
  produces a Markdown string whose `renderPreview` output contains the
  expected rendered tags (`<strong>`/`<h1>`/`<a>`), i.e. *the toolbar emits
  what the preview renders.*
- **Handoff** (RE3/RE4): `imageLink` produces the **exact** RC
  `![alt](/content-image/{id})` form — byte-identical to a hand-typed link, so
  RC's server-side `ContentImageIds.ExtractContentImageIds` parse (the RC R·3
  enforcement point) is **unchanged** by the toolbar.
- **Part-vs-whole** (the 10-test list above passes **together** with the
  inherited RC `MarkdownRendererTests` (7) + `ContentImageUploadTests` (4) +
  `ContentImageServingTests` (4) anchors — the RE lane **regresses nothing**
  RC shipped; the `dotnet exec` `Kumunita.Web.Tests` run is all-green).

## Close (U08) — the doc/roadmap consistency set

- **ADR 0031** — "WYSIWYG editor + toolbar over the RC Markdown lane" (settles
  **D1** = split-view live preview (not contenteditable), **D2** =
  toolbar-as-Markdown-splice in one shared TS module, **D3** = the client
  preview mirrors the RC pinned subset + `IsSafeImageSrc` semantics). Numbered
  after the current highest (0030). **Amends** ADR 0025's
  *"Any WYSIWYG / third-party editor — the `tsc`-only constraint stands"*
  non-decision: the `tsc`-only constraint **stands unchanged**; what changes is
  that the *composer surface* is now a split-view + toolbar (still
  `tsc`-only, still no editor dependency) rather than "a textarea with a
  Markdown hint."
- **`Milestones.cs`** — the `RE` named lane (the `GP`/`RC` precedent: a named
  lane, **not** a renumber; M4/M5/M6 stay Events/Projects/Portability). Keep
  the README **Roadmap** section in step (the `Milestones.cs` doc-comment
  names the README as the source of truth).
- **`MilestonesTests.cs`** — the test that pins the exact order + the
  single-in-progress milestone; update it **in the same commit** as the
  `Milestones.cs` change (the AGENTS.md contract).
- **`rc.editor.*` keys** — register in `KnownTranslationKeys` (`en` values:
  `rc.editor.bold`, `.italic`, `.code`, `.h1`, `.h2`, `.h3`, `.list`,
  `.olist`, `.link`, `.image`, `.preview` — **no `.quote`**: there is no
  blockquote button, so no `rc.editor.quote` key; blockquote is a future
  lane that must extend `MarkdownRenderer` first) so the U04–U06 views'
  `<kw-l>` keys have a fresh-boot `en` floor (ML-UI M·9 precedent). **No**
  non-`en` rows (the admin translates per key at runtime).
- **Folder moves** — the 9 unit plan files `in-progress/` → `done/`; the
  handoff note's `## Summary` section is the **sole** RE→future-lane handoff
  artifact.

## When a future lane wants more (the named deferrals)

- **Undo/redo** (a custom history stack on the textarea) — the browser's
  native `Ctrl+Z` is the floor; a lane names the history API explicitly.
- **WYSIWYG contenteditable / third-party editor** (ProseMirror/Quill/Tiptap)
  — **hard non-negotiable** out (D1): it breaks `tsc`-only + the no-HTML-
  round-trip stance + RC's `ImageIds` source parse. Not a deferral; a
  separate-architecture decision.
- **Tables / footnotes** in the toolbar or preview — RC's "intentionally out
  of scope" set; a lane that adds them to `MarkdownRenderer` *and* the client
  preview *together* (RE·2's parity holds only if both move in the same
  commit).
- **Drag-drop image reordering** — the body text is the order (RC stance); a
  future lane may add it if the plain-text order proves insufficient.
- **The gated image lanes** (un-gating the image button on the surfaces
  where RC's image lane is incomplete) — **RC**, not RE. Each is a separate
  RC follow-up with a different missing seam:
  - **Post Edit** — needs `UpdatePostAsync` to populate `post.ImageIds` (RC
    drift pause (c)). Once fixed, remove `data-rich-editor-no-image` on
    `Posts/Edit.cshtml` (one attribute, no RE code).
  - **Group Edit** — needs `UpdateGroupPostAsync` to populate
    `post.ImageIds` (RC drift pause (c) analog). Same un-gate on
    `Groups/Edit.cshtml`.
  - **Announcement New + Edit** — needs `ContentImageController.Serve` to
    gain an announcement branch (RC U03 drift pause — no
    `AnnouncementToAuditableResource`). Same un-gate on both
    `Announcement/{New,Edit}.cshtml`.
  - **Reply New/Edit (all three Detail views)** — needs both the write seam
    (`CreateReplyAsync`/`UpdateReplyAsync` → `PostReply.ImageIds`, RC drift
    pause (a)) **and** the serve seam (`ContentImageController.Serve` reply
    branch, RC U03). Un-gate on all `name="body"` reply textareas.
