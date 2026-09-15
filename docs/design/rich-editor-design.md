# Rich editor (`RE`) — WYSIWYG authoring surface + toolbar over the RC Markdown lane

> **Three-tier contract.** This file is the **primary** tier of the RE lane:
> it pins the invariant numbers (RE·1–RE·3), the FACES (RE1–RE5), the exact
> toolbar marker set (the *ceiling*), the `renderPreview` mirror subset, the
> client `IsSafeImageSrc` mirror predicate, the pinned seam-test names, the
> acceptance gate, and the drift guard. The register
> (`docs/plans-milestones/plan-rich-editor.md`) is the **secondary** tier
> (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md` is the
> **scratch** tier (one short section per unit, appended, never rewritten).
> When the three disagree, **this file wins for the pinned shapes**; the
> register wins for *which files exist* and *what each unit does*.
>
> **The frozen base.** This lane is built **on top of** the rich-content lane
> (`RC`, ADR 0025). RC's **R·1–R·7** all still bind **unchanged** (re-anchored
> in §Frozen base below). RE adds **no** re-shape of `MarkdownRenderer`,
> `ContentImageIds`, `IMediaStore`, the four `ImageIds` fields, the
> `/content-image` routes, or the `.rc-body` / `.rc-image` CSS. It is
> **client-only and additive**: one TS module, one CSS block, toolbar +
> preview markup on the composer views, and the `rc.editor.*` `<kw-l>` keys.
> **No** `.csproj` change, **no** `package.json` change (still `typescript`
> only), **no** editor dependency.

## Value chain

RC set out to deliver *"a resident can bold a heading, start a list, or show
a photo"* (RC `Scope`, R1). RC delivered the **read** half: `MarkdownRenderer`
renders `**bold**`, lists, links, and `![alt](/content-image/{id})` images on
every read surface, and `Body` is stored as a Markdown `string` (RC R·7, zero
migrations). RC deliberately left the **write** half at the boring floor —
*"the composer is a textarea with a Markdown hint"* (ADR 0025,
*"Any WYSIWYG / third-party editor"* non-decision) — so a resident who wants
bold still **hand-types `**text**` and watches the raw markers** while
composing.

This lane ships the **write** half:

- **A live preview pane** beside the source `<textarea>` — a resident **sees
  what they will publish while they type it** (D1).
- **A toolbar** — a resident **applies formatting with a button** rather than
  recalling Markdown syntax (D2).

The value chain moves one arrow: from *"the read path renders Markdown once
it is saved"* to **"the write path authoring-feels-like-what-the-read-path
renders."** The saved body is **byte-identical** to what a resident could have
hand-typed — RC's server-side `ContentImageIds` parse and
`MarkdownRenderer` render are **unchanged** (RE·3).

## Invariants (pinned for the RE lane)

Three invariants, **RE·1–RE·3**. Each is one idea, pinned so every unit and
every FACES row references a stable number. **RC R·1–R·7 are not re-stated
here** — they are re-anchored unchanged in §Frozen base and keep binding in
their own number space.

| # | Invariant | Pinned where |
|---|-----------|--------------|
| **RE·1** | **One source of truth = the `Body` `<textarea>`.** The toolbar and the preview are **both pure functions of the textarea's current value**: the preview is a pure render of it, and a toolbar button is a pure text splice into it. **No** separate hidden state, **no** `contenteditable` mirror, **no** `body.innerHTML` as a source of truth (the RC R·1 one-renderer / R·3 source-is-the-`string` stance, held client-side). The form submits the textarea's `value` — **unchanged** from RC. | **U03** (the module) · **U04–U06** (the surfaces) |
| **RE·2** | **Preview ↔ renderer parity (client-side R·1).** `renderPreview` mirrors the **exact** RC-pinned subset — the full `MarkdownRenderer` block + inline set (§`renderPreview` subset) and **only** that; a marker it renders that `MarkdownRenderer` does not is a **drift pause**. The client `isSafeImageSrc` has the **same** accept/reject semantics as `MarkdownRenderer.IsSafeImageSrc` (the route-shape branch + the schemeless-relative branch; every scheme rejected). The client link URL predicate mirrors `IsSafeUrl`. The preview output is **escaped-first** (client-side R·2) — user HTML is never trusted. A toolbar button that emits a marker `renderPreview` doesn't render (or vice-versa) is a **drift pause**, not a feature. | **U03** (the module) · **U07** (the parity tests) |
| **RE·3** | **No new dependency, no new server surface, no new audit row.** `package.json` stays `typescript`-only (verified at lane open); no `.csproj` package reference is added; no new route / `AccessAction` / `IMediaStore` method; the toolbar's only *network* action is the RC image upload (reused, already CSRF-aware + audited-by-RC). The lane is **client-only and additive.** | **U03** · **U08** (the close's "no new dependency" pin) |

## FACES (pinned, 5)

Five resident / admin-facing scenarios, **RE1–RE5**, each exercising one or
more invariants. The seam tests (§Pinned seam tests, U07) cover these 1:1.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| **RE1** | a resident opens the post composer, sees a **toolbar** (B / I / H / list / link / image) above the body field and a **live preview pane** that renders what they type as they type — `**bold**` shows **bold** in the pane while the textarea still holds `**bold**`. | RE·1, RE·2 |
| **RE2** | the resident selects the word `hello` and clicks **B** → the textarea now reads `**hello**` with the selection preserved; the preview shows **hello** bold. Clicking **B** again (deselected, cursor after) toggles it back to `hello`. | RE·1 |
| **RE3** | the resident clicks **Link** with a selection → the selection becomes `[hello](https://…)` (the URL comes from a `prompt`, the existing link rule — §Pinned contract); the preview renders it as an `<a>`. A **remote image** typed `![x](https://evil/i.png)` renders in the preview **as plain text** (the client `isSafeImageSrc` mirror rejects it), not an `<img>`. | RE·2 |
| **RE4** | on a surface where the image button is available (Post New, Group New, static-page editor), the resident clicks **Image** → the RC upload fires (the existing `POST /content-image`), and on success `![{alt}](/content-image/{id})` is spliced in and the preview renders an `<img class="rc-image">`. Saving populates `ImageIds` **exactly as if the resident had hand-typed the link** (RC R·3 unchanged — the server parse is byte-identical). On a surface where RC's image lane is incomplete (Post Edit / Group Edit / Announcement / replies), the button is **absent** (U03's `data-rich-editor-no-image`) so the resident can never produce a 404'ing image — the text toolbar + preview are unchanged there. | RE·1, RE·3, RC R·3 |
| **RE5** | a **signed-in** GlobalAdmin opens the **about** page editor (the static-page authoring surface) → the toolbar + preview are there exactly like a post composer, and saving renders on `/about` for an unauthenticated visitor **unchanged** (RC R6 — the editor is a nicer front to the same `LocalizedPage.Body` RC already serves). | RE·1, RC R6 |

## Pinned contract (the new artifacts — additive only)

Every unit matches these shapes verbatim. A mismatch is a `## U<m> — Drift
pause`, not a silent edit.

### The shared editor module (U03) — `client/lib/rich-editor.ts`

`tsc`-only (RE·3), self-wires at load (the `insert-image.ts` pattern).
Exposes **five PURE functions** (unit-testable, RE·2) + **one binder** (RE·1):

```ts
/** RE·2, D3 — the client preview. Mirrors MarkdownRenderer's supported set
 *  verbatim (§renderPreview subset); escaped-first; never trusts user HTML. */
export function renderPreview(markdown: string): string;

/** RE·1, D2 — wrap/unwrap an inline marker over a selection. Pure.
 *  kind: 'bold' | 'italic' | 'code'. Returns the new value + the new
 *  selection [start, end] (caret-preserving, RE2 FACES). */
export function applyToggle(markdown: string, sel: [number, number],
                            kind: 'bold' | 'italic' | 'code'):
                            { value: string; sel: [number, number] };

/** RE·1, D2 — prepend a block marker at the caret's line. Pure.
 *  kind: 'h1' | 'h2' | 'h3' | 'ul' | 'ol'. (NOT 'quote' — see the drift
 *  pause: the frozen MarkdownRenderer does not render blockquote.) */
export function applyBlock(markdown: string, caret: number,
                           kind: 'h1'|'h2'|'h3'|'ul'|'ol'):
                           { value: string; caret: number };

/** RE·2 — wrap a selection in [label](url). Pure. */
export function applyLink(markdown: string, sel: [number, number],
                          url: string): { value: string; sel: [number, number] };

/** RC R·3 byte-identity — the exact image link form the server parse
 *  (ContentImageIds.FullSrcRe) already understands. */
export function imageLink(alt: string, id: string): string;
//  === `![alt](/content-image/{id})`

/** RE·1, D1/D2 — the binder. Renders the toolbar above a
 *  textarea[data-rich-editor], wires each button to the pure splice above,
 *  re-renders the preview pane (div[data-rich-editor-preview]) on `input`,
 *  and — only when the toolbar does NOT carry data-rich-editor-no-image —
 *  wires the data-md="image" button to the RC POST /content-image upload
 *  (reusing client/lib/api.ts's apiFetch, the insert-image.ts convention). */
export function bindRichEditor(root: HTMLElement): void;
```

### The client image-src mirror (U03) — pinned name `isSafeImageSrc`

The **exact** mirror of `MarkdownRenderer.IsSafeImageSrc` (RE·2). Two accept
branches, everything else **rejects** (the whole `![alt](src)` renders as
plain escaped text):

```ts
/** Mirror of MarkdownRenderer.IsSafeImageSrc — same accept/reject semantics. */
export function isSafeImageSrc(src: string): boolean {
  if (!src) return false;
  // Branch 1: the platform route shape, /content-image/{1-128 lowercase hex}.
  const route = '/content-image/';
  if (src.startsWith(route)) {
    const id = src.slice(route.length);
    return id.length >= 1 && id.length <= 128 && /^[0-9a-f]+$/.test(id);
  }
  // Branch 2: schemeless relative — no ':', no protocol-relative '//',
  //           no whitespace. (Defensive; the route branch is the intended
  //           producer.)
  return !src.includes(':') && !src.startsWith('//') && !/\s/.test(src);
}
```

The **client link URL** predicate mirrors `MarkdownRenderer.IsSafeUrl`
(accept `http` / `https` / `mailto` / schemeless-relative; reject
`javascript:` / `data:` / every other scheme — the rejected link renders as
plain text). This is part of RE·2 parity: the preview must not emit an `<a>`
the server renderer would refuse.

### The editor CSS (U03) — new block in `wwwroot/css/site.css`

```css
.rc-editor         { /* the split: source | preview, responsive */ }
.rc-editor-toolbar { /* the button row: B I H list link image */ }
.rc-editor-toolbar .rc-btn { /* button reset + hover + focus-visible */ }
.rc-editor-pane    { /* the read-only preview — reuses the existing .rc-body
                       for body copy; the preview wraps its output in
                       <div class="rc-body rc-editor-pane"> */ }
/* .rc-image already exists (RC U06) — the preview reuses it. No new image
   CSS. */
```

### The toolbar + preview markup pattern (U04–U06)

One pattern, every composer. The image-gated variant (U04 Post Edit, U05
Group Edit + both Announcement composers, U06 reply composers) is identical
**except** the toolbar carries `data-rich-editor-no-image` (U03's
`bindRichEditor` then **omits** the `data-md="image"` button) and the textarea
drops `data-image-target` (RC's image lane is incomplete on that surface — the
specific RC drift pause is named per-surface in the register's image-button
matrix + the handoff note).

```razor
<div class="rc-editor">
  <div class="rc-editor-toolbar" data-rich-editor>
    <!-- one <button type="button" class="rc-btn" data-md="…"><kw-l
         key="rc.editor.…">…</kw-l></button> per marker in §Exact toolbar
         marker set. The image button is data-md="image" and reuses
         insert-image.ts's upload (U03 wires it). Buttons are type="button"
         (never submit — they must not post the form). -->
  </div>
  <textarea class="form-control" name="Body" data-rich-editor data-image-target>…</textarea>
  <div class="rc-editor-pane rc-body" aria-live="polite" data-rich-editor-preview></div>
</div>
<script type="module" src="~/js/lib/rich-editor.js"></script>
```

**Everything else is frozen** (RC R·1–R·7 + the RC Pinned contract). The
`Body` textarea keeps its `name` and its `value`. Its RC `data-image-target`
attribute is kept **only on the surfaces where the image button is on** (Post
New / Group New / static page); it is dropped on the image-gated surfaces.
**No** new view-model field, **no** new controller action, **no** new Core
type.

## Exact toolbar marker set (the *ceiling*)

The toolbar emits **only** these markers. This is a **ceiling, not a
wishlist**: no button may emit a marker `renderPreview` (§`renderPreview`
subset) does not render (RE·2). `applyToggle` / `applyBlock` / `applyLink`
produce exactly these.

| Button | `data-md` kind | What it emits into the textarea | Rendered by `renderPreview` |
|---|---|---|---|
| Bold | `bold` | `**sel**` (wrap selection; toggle off when already bold) | `<strong>` |
| Italic | `italic` | `*sel*` (wrap selection; toggle off when already italic) | `<em>` |
| Inline code | `code` | `` `sel` `` (wrap selection; toggle off when already code) | `<code>` |
| Heading 1 | `h1` | `# ` prepended at the caret's line | `<h1>` |
| Heading 2 | `h2` | `## ` prepended at the caret's line | `<h2>` |
| Heading 3 | `h3` | `### ` prepended at the caret's line | `<h3>` |
| Unordered list | `ul` | `- ` prepended at the caret's line | `<ul><li>` |
| Ordered list | `ol` | `1. ` prepended at the caret's line | `<ol><li>` |
| Link | `link` | `[sel](url)` (wrap selection; URL from a `prompt`) | `<a>` |
| Image | `image` | `![alt](/content-image/{id})` (insert, after RC upload) | `<img class="rc-image">` |

**Ceiling exclusion (drift pause — RE·2):** **blockquote (`> `) is NOT a
toolbar button and NOT in the preview subset.** The frozen `MarkdownRenderer`
(`src/Kumunita.Web/Security/MarkdownRenderer.cs`) has no blockquote branch —
its block dispatch handles only fenced code, headings, unordered lists,
ordered lists, and paragraphs, and `PlainTextPreview` has no blockquote strip
step. The register (and U0's work order) named a `quote` button + `> ` in the
subset, but RE·2 forbids a marker the server renderer lacks, so it is **cut**
from both sides here. Adding blockquote is a **future lane** that must extend
`MarkdownRenderer` **and** the client preview **in the same commit** (RE·2's
parity holds only if both move together). See the `## U1 — Drift pause`
section in the handoff note.

## `renderPreview` subset (the mirror — and only that)

`renderPreview` mirrors the **full supported set of `MarkdownRenderer`** —
every marker the *frozen* server renderer renders — and **only** that. This is
what makes RE·2 hold: the preview never renders a marker the server lacks, and
every toolbar marker (§Exact toolbar marker set) is a strict subset of this, so
every button's output renders.

- **Headings 1–6** (`#`–`######`) → `<h1>`–`<h6>`. (The toolbar emits a
  **ceiling of h1–h3**; the preview mirrors the renderer's full h1–h6 so a
  body that already contains `####` does not visibly diverge between preview
  and read path.)
- **Paragraphs** — consecutive non-blank, non-special lines → `<p>`.
- **Unordered lists** (`- ` / `* `) → `<ul><li>`.
- **Ordered lists** (`1. `) → `<ol><li>`.
- **Fenced code blocks** (` ``` ` … ` ``` `) → `<pre><code>` (content escaped
  verbatim, no inline rules — the renderer's construction).
- **Inline code** `` `code` `` → `<code>` (applied first, so a backtick inside
  bold is not re-processed — the renderer's order).
- **Bold** `**text**` → `<strong>` (before italic, so `**` is not eaten as two
  italics).
- **Italic** `*text*` → `<em>`.
- **Links** `[label](url)` → `<a>` under the client `IsSafeUrl` mirror
  (http / https / mailto / relative); a rejected URL renders the label as
  plain text.
- **Images** `![alt](src)` → `<img src alt class="rc-image" loading="lazy" />`
  under the client `isSafeImageSrc` mirror (§the client image-src mirror); a
  rejected `src` renders the whole `![alt](src)` as plain escaped text.

**Out on both the toolbar and the preview** (RC's "intentionally out of scope"
set, unchanged): **tables, footnotes, raw HTML, and blockquote** (the last now
also by the RE·2 drift pause). The preview is **escaped-first** — every input
character is escaped before any inline rules run, so a hostile `<script>` /
`onerror=` cannot survive (client-side R·2, the same construction as
`MarkdownRenderer`).

## Pinned seam tests (exact names)

All in `tests/Kumunita.Web.Tests/RichEditorTests.cs` (new, U07). **Harness
reality (RE·3's `tsc`-only + no TS test runner):** the repo has no
`vitest`/`jest`/`node --test`, so U07 implements the 10 behaviors as a small
**C# spec mirror** of `rich-editor.ts`'s pure functions (the mirror encodes
the *same* pinned contract from this doc — it is the executable spec, not a
second product renderer) plus **one artifact pin** asserting the `npm run
build` output (`wwwroot/js/lib/rich-editor.js`) exists and exports the pinned
surface. The `dotnet exec` run (not `dotnet test`) is the gate.

1. `RenderPreview_BoldItalicCode_StillRender` — `**b** *i* ` + `` `c` `` →
   `<strong>`/`<em>`/`<code>` (the RC subset parity, RE·2).
2. `RenderPreview_HeadingsLists_StillRender` — `# H`, `- a`, `1. b` →
   `<h1>`/`<ul><li>`/`<ol><li>` (parity).
3. `RenderPreview_ImagePlatformRoute_RendersImgTag` —
   `![x](/content-image/deadbeef)` → one `<img class="rc-image">` (parity +
   the `src` allowlist accept).
4. `RenderPreview_ImageRemoteSrc_RendersAsPlainText` —
   `![x](https://evil/i.png)` → **no** `<img>`; escaped text (the client
   `isSafeImageSrc` mirror rejects, RE·2 — mirrors RC R·2).
5. `RenderPreview_HostileMarkup_StillEscaped` — `<script>`/`onerror=` →
   escaped, no raw tag survives (client R·2).
6. `ApplyToggle_Bold_WrapsSelection_AndPreservesCaret` — `applyToggle("hello
   world",[6,11],"bold")` → `"hello **world**"`, selection now `[7,12]` (the
   RE1/RE2 FACES — the toggle is a pure, caret-preserving splice).
7. `ApplyToggle_Bold_TogglesOff_WhenAlreadyBold` — selecting an already-bolded
   word and toggling removes the markers (the RE2 "clicking B again" FACES).
8. `ApplyBlock_H1_InsertsHeading_AtCaret` — `applyBlock("",0,"h1")` → `"# "`
   (a block kind prepends its marker + a trailing space at the caret's line).
9. `ApplyLink_WrapsSelection_WithUrl` — `applyLink("hello",[0,5],"https://x")`
   → `"[hello](https://x)"` (the RE3 link FACES).
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
  produces a Markdown string whose `renderPreview` output contains the expected
  rendered tags (`<strong>`/`<h1>`/`<a>`), i.e. *the toolbar emits what the
  preview renders.*
- **Handoff** (RE3/RE4): `imageLink` produces the **exact** RC
  `![alt](/content-image/{id})` form — byte-identical to a hand-typed link, so
  RC's server-side `ContentImageIds.ExtractContentImageIds` parse (the RC R·3
  enforcement point) is **unchanged** by the toolbar.
- **Part-vs-whole** (the 10-test list above passes **together** with the
  inherited RC `MarkdownRendererTests` (7) + `ContentImageUploadTests` (4) +
  `ContentImageServingTests` (4) anchors — the RE lane **regresses nothing**
  RC shipped; the `dotnet exec` `Kumunita.Web.Tests` run is all-green).

## Drift guard

The 3 invariants (RE·1–RE·3), the 5 FACES (RE1–RE5), the **exact toolbar
marker set** (the ceiling), the **`renderPreview` subset**, the **client
`isSafeImageSrc` mirror** + link-URL mirror, the **pure-function export
signatures** above, and the **10 test names** are **frozen** once this file is
written. Any mismatch found by a later unit is a `## U<m> — Drift pause`
section in the handoff note (unit-series rule 6), **not** a silent edit.

**Standing drift pause (U01):** blockquote (`> `) — the register named a
`quote` button + `> ` in the subset, but the frozen `MarkdownRenderer` does not
render it; RE·2 forces it out of both the toolbar and the preview (see
§Exact toolbar marker set + §`renderPreview` subset, and the handoff note).

## Named deferrals (a future RE-2 lane, not a renumber)

- **Undo/redo** (a custom history stack on the textarea) — the browser's native
  `Ctrl+Z` is the floor; a lane names the history API explicitly.
- **Blockquote (`> `) in the toolbar and preview** — the **RE·1 drift pause**:
  requires extending `MarkdownRenderer` **and** the client `renderPreview` in
  the same commit (RE·2's parity holds only if both move together).
- **WYSIWYG contenteditable / third-party editor** (ProseMirror/Quill/Tiptap)
  — **hard non-negotiable** out (D1): it breaks `tsc`-only + the no-HTML-
  round-trip stance + RC's `ImageIds` source parse. Not a deferral; a
  separate-architecture decision.
- **Tables / footnotes** in the toolbar or preview — RC's "intentionally out of
  scope" set; a lane that adds them to `MarkdownRenderer` *and* the client
  preview *together* (RE·2's parity holds only if both move in the same
  commit).
- **Drag-drop image reordering** — the body text is the order (RC stance); a
  future lane may add it if the plain-text order proves insufficient.
- **The gated image lanes** (un-gating the image button on the surfaces where
  RC's image lane is incomplete) — **RC**, not RE. Each is a separate RC
  follow-up with a different missing seam:
  - **Post Edit** — needs `UpdatePostAsync` to populate `post.ImageIds` (RC
    drift pause (c)). Once fixed, remove `data-rich-editor-no-image` on
    `Posts/Edit.cshtml` (one attribute, no RE code).
  - **Group Edit** — needs `UpdateGroupPostAsync` to populate `post.ImageIds`
    (RC drift pause (c) analog). Same un-gate on `Groups/Edit.cshtml`.
  - **Announcement New + Edit** — needs `ContentImageController.Serve` to gain
    an announcement branch (RC U03 drift pause — no
    `AnnouncementToAuditableResource`). Same un-gate on both
    `Announcement/{New,Edit}.cshtml`.
  - **Reply New/Edit (all three Detail views)** — needs both the write seam
    (`CreateReplyAsync`/`UpdateReplyAsync` → `PostReply.ImageIds`, RC drift
    pause (a)) **and** the serve seam (`ContentImageController.Serve` reply
    branch, RC U03). Un-gate on all `name="body"` reply textareas.

## Frozen base (RC R·1–R·7, re-anchored unchanged)

The RE lane **does not re-state** these — they keep binding from the RC design
doc (`docs/design/rich-content-design.md`) and ADR 0025:

- **R·1** — one renderer: `MarkdownRenderer.RenderHtml` is the single
  Markdown → HTML path for every body.
- **R·2** — escape-first stands; the image `src` allowlist is **stricter**
  than the link whitelist (platform route shape only, no remote `src`).
- **R·3** — references are data, not URLs-in-prose; `ImageIds` populated
  **server-side** by parsing the body's route-shaped links; a well-formed link
  with no matching owner renders as plain text and 404s if fetched.
- **R·4** — the serving route defers to the owning resource's decision;
  404-orphan; no new `AccessAction`.
- **R·5** — Core stays HTTP-free.
- **R·6** — upload boundary is ADR 0011's, verbatim.
- **R·7** — zero schema migrations; `Body` stays a `string`.

RE's **RE·3** (no new dependency / no new server surface / no new audit row)
is what keeps the whole frozen base **untouched** by this lane.
