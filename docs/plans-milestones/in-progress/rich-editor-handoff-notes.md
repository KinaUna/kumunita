# Rich editor (`RE`) — rolling handoff notes

> **The scratch tier** of the RE lane's three-tier contract (the design
> doc is primary, the register is secondary, this file is scratch).
> One section per unit, **appended, never rewritten**. Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-reads list. A `## U<m> — Drift pause` section is a
> **blocker**: the next unit reads it first and either resolves it (recording
> the resolution in its own section) or carries it forward (naming it in its
> exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##` section
> from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-15
- **Register:** `docs/plans-milestones/plan-rich-editor.md` (U0–U8)
- **Design doc (primary):** `docs/design/rich-editor-design.md` (U01 authors)
- **ADR:** `docs/adr/0031-wysiwyg-editor-and-toolbar.md` (U08 authors; Amends
  0025)
- **Scope:** a **WYSIWYG authoring surface** over the RC Markdown lane — a
  **split-view live preview** (`renderPreview` mirroring the RC pinned
  subset) + a **toolbar** (`bold`/`italic`/`code`/`h1`–`h3`/`ul`/`ol`/
  `quote`/`link`/`image`) in one shared `tsc`-only TS module
  (`client/lib/rich-editor.ts`) + the `.rc-editor-*` CSS + the toolbar/preview
  markup on the post / group-post / announcement / static-page composers
  **and the reply composers** (`Posts/Detail`, `Groups/PostDetail`,
  `Announcement/Detail`). The **text toolbar + live preview are on every
  surface** (all those bodies already render via the one `MarkdownRenderer`).
  The **image button is on only where RC's image lane is complete** — Post
  New (U04), Group New (U05), static page (U06); **gated off** (via U03's
  `data-rich-editor-no-image`) on Post Edit (U04), Group Edit + both
  Announcement composers (U05), and the reply composers (U06) — each a
  pre-existing RC drift pause (see the register's image-button matrix).
  **Client-only and additive:** no Core field, no route, no `AccessAction`,
  no `IMediaStore` method, no second server renderer, **no editor dependency
  in `package.json`**. `Body` stays a Markdown `string`; RC's
  `ContentImageIds` parse is byte-identical to the toolbar's image link.
- **Out of scope (named deferrals for a future RE-2 lane, if one comes):**
  undo/redo history stack, spellcheck/dictionary, drag-drop image reordering
  (RC stance), **contenteditable / third-party editor** (hard non-negotiable,
  D1), tables/footnotes in the toolbar or preview (RC out-of-scope set), and
  the **gated image lanes** (un-gating the image button on Post Edit / Group
  Edit / Announcement / replies — a **RC** follow-up per surface: the missing
  RC write-lane `ImageIds` seam [drift pause (a)/(c) analogs] and/or the
  missing RC serve-branch adapter [RC U03 inert-404]; until then those
  surfaces' image button stays gated via `data-rich-editor-no-image`, and
  un-gating is a one-attribute removal, no RE code).
- **Frozen base (RC, unchanged):** `MarkdownRenderer` · `ContentImageIds` ·
  `IMediaStore` · `MediaObject` · the four `ImageIds` fields · the
  `GET`/`POST /content-image` routes · the `.rc-body`/`.rc-image` CSS ·
  `insert-image.ts` · RC R·1–R·7 invariants.

<!-- U0 appends its section below this line. One `##` section per unit, in
     order (U0, U1, … U8). Never rewrite a prior section. -->

## U0 — Kickoff verified

**Date:** 2026-09-15

**Gap 1 — every composer shows raw Markdown only (no preview pane, no toolbar):**

| Surface | Body `<textarea>` (file + line) | Preview pane? | Toolbar? |
|---|---|---|---|
| Post New | `Views/Posts/New.cshtml:89` | ❌ | ❌ |
| Post Edit | `Views/Posts/Edit.cshtml:93` | ❌ | ❌ |
| Group New | `Views/Groups/New.cshtml:61` | ❌ | ❌ |
| Group Edit | `Views/Groups/Edit.cshtml:62` | ❌ | ❌ |
| Announcement New | `Views/Announcement/New.cshtml:109` | ❌ | ❌ |
| Announcement Edit | `Views/Announcement/Edit.cshtml:109` | ❌ | ❌ |
| Static page | `Views/Languages/PreviewPage.cshtml:42` | ❌ | ❌ |
| Reply New (Posts) | `Views/Posts/Detail.cshtml:423` | ❌ | ❌ |
| Reply Edit (Posts) | `Views/Posts/Detail.cshtml:351` | ❌ | ❌ |
| Reply New (Groups) | `Views/Groups/PostDetail.cshtml:379` | ❌ | ❌ |
| Reply Edit (Groups) | `Views/Groups/PostDetail.cshtml:327` | ❌ | ❌ |

**Grep proof:** `data-rich-editor\|rc-editor\|rich-editor` in `src/Kumunita.Web/**` → **0 hits**. No `rc-editor-*` CSS block in `site.css`. No `client/lib/rich-editor.ts` file. The only body-adjacent control on any surface is the RC `rc-insert-image` block (a file-input for uploading an image and splicing a Markdown link) — **not** a preview pane or a formatting toolbar.

**Gap 2 — the frozen RC base is intact at its claimed locations:**

| Seam | Confirmed location | Status |
|---|---|---|
| `MarkdownRenderer` | `src/Kumunita.Web/Security/MarkdownRenderer.cs` | ✅ present |
| `ContentImageIds` | `src/Kumunita.Web/Security/ContentImageIds.cs:25` (`public static class ContentImageIds`, same namespace as `MarkdownRenderer`) | ✅ present |
| `.rc-body` CSS | `src/Kumunita.Web/wwwroot/css/site.css:733` (`.rc-body` through `.rc-body a` at line 782) | ✅ present |
| `.rc-image` CSS | `src/Kumunita.Web/wwwroot/css/site.css:783` | ✅ present |
| `insert-image.ts` | `src/Kumunita.Web/client/lib/insert-image.ts` | ✅ present |
| `package.json` (tsc-only) | `src/Kumunita.Web/package.json` — `devDependencies`: `typescript: ^7.0.2` **only**; `scripts.build`: `tsc`; no bundler, no editor dep | ✅ `typescript`-only (RE·3 holds at lane open) |

**RC drift pauses carried forward (determine per-surface image-button gating via `data-rich-editor-no-image`):**

| Surface | RC drift pause (from RC handoff notes) | Image button (RE) |
|---|---|---|
| Post New | — (RC lane complete: write `PostsController:692`, serve post branch) | **ON** |
| Post Edit | U04 (c): `UpdatePostAsync` never sets `ImageIds` (discrete-field shape) | **OFF** (U04 gates) |
| Group New | — (RC lane complete: U05 wired `CreateGroupPostAsync`; serve post branch) | **ON** |
| Group Edit | U05 drift pause: `UpdateGroupPostAsync` never sets `ImageIds` (discrete-field shape) | **OFF** (U05 gates) |
| Announcement New | U03 drift pause: no `AnnouncementToAuditableResource` → serve branch inert-404 | **OFF** (U05 gates) |
| Announcement Edit | U03 drift pause (same) | **OFF** (U05 gates) |
| Static page | — (RC lane complete: write `LanguagesController:271`, serve page branch, public) | **ON** (U06) |
| Reply New/Edit (all 3 Detail views) | U04 (a): `CreateReplyAsync`/`UpdateReplyAsync` never set `PostReply.ImageIds` + U03: serve branch inert-404 | **OFF** (U06 gates) |

**Known RC debt (flagged, not folded into `rc.editor.*`):** `rc.markdown_hint` is **not registered** in `KnownTranslationKeys` (RC U05's hint deferral — the RC design doc never named the key). U08 will register `rc.editor.*` keys; `rc.markdown_hint` remains an **RC debt** to be resolved by the RC lane owner.

**Drift pause:** none — both gaps hold as stated in the register; the frozen RC base is intact at every claimed location. U03 can build against the base as pinned.

## U1 — Design doc + ADR 0031 drafted

**Date:** 2026-09-15

- **Primary tier authored:** `docs/design/rich-editor-design.md` — the RE·1–RE·3 invariants, the FACES RE1–RE5, the **exact toolbar marker set** (the *ceiling*), the **`renderPreview` subset**, the **client `isSafeImageSrc` mirror** (+ the client link-URL mirror of `IsSafeUrl`), the 5 pure-function + binder export signatures, the 10 pinned seam-test names, the acceptance gate, the drift guard, and the named deferrals. It re-anchors **RC R·1–R·7** as the frozen base (unchanged).
- **ADR 0031 drafted (status "Draft (lands in U08)"):** `docs/adr/0031-wysiwyg-editor-and-toolbar.md` — settles **D1** (split-view live preview, not `contenteditable`), **D2** (toolbar-as-Markdown-splice in one `tsc`-only TS module), **D3** (client preview mirrors the RC subset + `IsSafeImageSrc` semantics). States the `tsc`-only constraint **stands unchanged**; only the composer surface changes. **Amends** ADR 0025 (does not supersede); numbers after 0030. U08 owns the ADR-accept + ADR-index update.
- **The two load-bearing pins** (U03 must code against these without re-deriving the RC subset):
  1. **The exact toolbar marker set (ceiling)** — `bold` `**` · `italic` `*` · `code` `` ` `` · `h1`–`h3` `#`–`###` · `ul` `- ` · `ol` `1. ` · `link` `[label](url)` · `image` `![alt](/content-image/{id})`. **No** blockquote button (see the drift pause below). Every button's marker is a strict subset of what `renderPreview` renders.
  2. **The client `isSafeImageSrc` mirror** — accept **only** (a) `/content-image/` + 1–128 lowercase hex `[0-9a-f]{1,128}`, or (b) a schemeless relative (no `:`, no leading `//`, no whitespace); **reject every scheme** (`http:`/`https:`/`data:`/`javascript:`/…) and every malformed id — a rejected `src` renders the whole `![alt](src)` as plain escaped text. Verbatim mirror of `MarkdownRenderer.IsSafeImageSrc`; the client link-URL predicate mirrors `IsSafeUrl` (http/https/mailto/relative).
- **The `renderPreview` subset** (and only that) — headings 1–6, paragraphs, `- `/`* ` lists, `1. ` lists, fenced code blocks, inline code / bold / italic, `[label](url)` links (under the client `IsSafeUrl` mirror), and `![alt](src)` images (under the client `isSafeImageSrc` mirror, `rc-image` class). **Out on both toolbar and preview:** tables, footnotes, raw HTML, **blockquote**.
- `rc.markdown_hint` left **out** of `rc.editor.*` (it is RC debt, per U0 — U08 registers only the `rc.editor.*` set).
- **U03 can now build the module from the design doc alone.** The pure-function signatures, the `data-md` kinds, the `data-rich-editor` / `data-image-target` / `data-rich-editor-preview` / `data-rich-editor-no-image` attribute contract, and the two load-bearing pins are all pinned concretely.

## U1 — Drift pause (blockquote / `> `)

**Status: recorded, not resolved — carried to U08 as a named deferral.**

The register (`plan-rich-editor.md`) and U0's work order named a **`quote` toolbar button** and `> ` in the **`renderPreview` subset**. U1's entry read of the frozen base — `src/Kumunita.Web/Security/MarkdownRenderer.cs` — shows **`MarkdownRenderer` has no blockquote branch**: its block dispatch loop handles only fenced code, headings (1–6), unordered lists, ordered lists, and paragraphs, and `PlainTextPreview` has **no blockquote strip step** (its pinned 7-step strip order is image/link → inline markers → heading `#`s → list markers → collapse whitespace → truncate). A resident-typed `> quote` therefore renders on the **read path** as a **plain paragraph** (the leading `> ` left in the text), not a `<blockquote>`.

This directly collides with **RE·2** ("*do not add a marker to the preview subset the server renderer lacks*") and the unit's hard constraint ("*the marker set is a ceiling, not a wishlist*"). **Resolution (per the constraint, a `cut the button` outcome, not a silent feature):** blockquote is **removed from both sides** in the design doc — **no `quote` button** in the toolbar marker set and **no `> `** in the `renderPreview` subset. `applyBlock`'s `kind` union is therefore `'h1'|'h2'|'h3'|'ul'|'ol'` (no `'quote'`). Adding blockquote is a **future lane** that must extend `MarkdownRenderer` **and** the client `renderPreview` **in the same commit** (RE·2's parity holds only if both move together) — recorded in the design doc's §Named deferrals and ADR 0031's "Not decided here."

**Carried to:** U08 (the close — keep blockquote out of the `rc.editor.*` key set and out of any roadmap/README copy of the marker list). No U03/U04/U05/U06 action is blocked by this pause; the remaining 9-button toolbar + preview mirror are fully codeable from the design doc.

## U2 — Mirror checklist (pinned)

**Date:** 2026-09-15

**Purpose (RE·2 made checkable before any TS exists):** this is the verbatim
pin U03's `renderPreview` implements *from this section alone* — the exact
marker ceiling, the exact `src` allowlist predicate, and the exact CSS
classes the preview output must carry. Every line below was read directly
against the frozen base on 2026-09-15 (`MarkdownRenderer.cs` full read;
`site.css` `.rc-body`/`.rc-image`; `ContentImageIds.cs`; the 7 pinned
`MarkdownRendererTests`). **Drift pause: none** — all seams intact and
matching RC's close + U1's design doc. The one carried constraint: **no
blockquote** (`> `) on either side (U1 drift pause; `MarkdownRenderer` has no
blockquote branch — see below).

**1. The marker set `MarkdownRenderer` renders (the CEILING).** `renderPreview`
must render **exactly** this set and **nothing more** — a toolbar button that
emits a marker outside this set is a drift pause (RE·2). Source:
`MarkdownRenderer.RenderHtml` block dispatch + `Inline`/`InlineText`.

- Headings **1–6** — `#` through `######` (a space after the `#`s, non-empty
  content) → `<h1>`…`<h6>` (`MatchHeading`).
- **Paragraph** — consecutive non-blank, non-special lines collapse into one
  `<p>…</p>`.
- **Unordered list** — `- ` / `* ` lines → `<ul><li>…</li></ul>`.
- **Ordered list** — `1. ` / `2. ` … (`\d+\. `) → `<ol><li>…</li></ol>`.
- **Fenced code block** — ` ``` ` … ` ``` ` → `<pre><code …>…</code></pre>`
  (content escaped verbatim, no inline rules; optional `language-{lang}` class).
- `**bold**` → `<strong>…</strong>`.
- `*italic*` → `<em>…</em>`.
- `` `code` `` → `<code>…</code>` (applied first, so a backtick inside bold is
  not re-processed).
- `[label](url)` → `<a href="…">label</a>` (under the link-URL allowlist).
- `![alt](src)` → `<img src="…" alt="…" class="rc-image" loading="lazy" />`
  (under the image `src` allowlist below).

**Out on both toolbar and preview (the ceiling is a cap, not a wishlist):**
blockquote / `> `, tables, footnotes, raw HTML. `MarkdownRenderer` has **no
blockquote branch** (block dispatch = fenced code, headings, ul, ol, paragraph
only; `PlainTextPreview` has no blockquote strip step) — a resident-typed
`> ` renders as a plain paragraph on the read path, so RE·2 forbids adding it
to either side (U1 drift pause; carried to U08).

**2. The client `IsSafeImageSrc` mirror (verbatim predicate).** `renderPreview`'s
image branch accepts **only** these two shapes and rejects everything else — a
rejected `src` renders the **whole** `![alt](src)` as plain escaped text (no
`<img>`, no `src=`). This is a verbatim copy of
`MarkdownRenderer.IsSafeImageSrc` (RC R·2):

- **Branch 1 — the platform route shape:** `src` starts with the literal
  `/content-image/`, and the remainder (the `{id}`) is **1–128 lowercase hex**
  chars, `[0-9a-f]{1,128}` (exact — a query string or trailing slash is a
  malformed id → reject, **not** a fallback to branch 2).
- **Branch 2 — a schemeless relative path:** no `:` anywhere, **no** leading
  `//` (protocol-relative), and **no** whitespace.
- **Rejected (always):** every URL scheme — `http:`, `https:`, `data:`,
  `blob:`, `javascript:` (i.e. any `://` or bare `scheme:` form) — and any
  empty/malformed id. (`data:` URIs are the XSS vector the 7 pinned tests pin
  to plain-text.)
- The `alt` is HTML-escaped **verbatim** (no inline rules inside the attribute).

(The client **link-URL** predicate mirrors `IsSafeUrl`, not `IsSafeImageSrc`:
accept `http`/`https`/`mailto`/relative, reject `javascript:`/`data:`/any
other scheme. Keep the two distinct — images are strictly route/relative; links
are the broader scheme whitelist.)

**3. The CSS classes the preview output must carry (so it looks like the
read path).** The preview pane is the renderer's output wrapped so it inherits
the RC body typography:

- A wrapper `<div class="rc-body rc-editor-pane">` around the whole
  `renderPreview` output — `.rc-body` (site.css, the RC U06 block: Lora body
  serif, type scale, `h1`–`h6` / `ul` / `ol` / `code` / `pre` / `a` rules)
  gives the preview body copy identical styling to the read path.
- Every accepted image carries `class="rc-image"` (the RC rule: `max-width:
  100%; height: auto; border-radius: 6px;` block) — the same `<img>` attribute
  set the read path emits (`src`, `alt`, `class="rc-image"`,
  `loading="lazy"`).

**4. The escape-first stance (client-side R·2).** `renderPreview` **escapes
HTML entities before** applying the marker→HTML map — every input character is
entity-escaped (`&` `<` `>` `"` `'`) **first**, and the inline rules
(code, then bold, then italic) run on the **already-escaped** result, exactly
as `MarkdownRenderer.HtmlEscape` + `InlineText` do. A hostile `<script>` /
`onerror=` typed in the textarea must therefore appear in the preview as
escaped text, never as a live tag/attribute. The one ordering nuance to keep:
images/links are extracted **before** the surrounding text is escaped, so their
`src`/`url` values survive the allowlist check intact (the
`ImageOrLinkPattern` single document-order pass), while the *text around* them
is escaped-first.

**5. The preview is a *function of* the textarea value (RE·1).** The textarea
is the **single source of truth**; the preview is a pure render of its current
value, **re-rendered on every `input`**. `renderPreview` is pure (no hidden
state, no `contenteditable` mirror, no `body.innerHTML` as a source) — a
toolbar button is a pure text splice **into the textarea**, and the form still
submits the textarea's `value` exactly as RC does.

## U3 — Editor module + CSS

**Date:** 2026-09-15

**Deliverables shipped (exactly 2 files, the closed set):**

1. `src/Kumunita.Web/client/lib/rich-editor.ts` (new) — a `tsc`-only,
   self-wiring ES module (the `insert-image.ts` pattern) exporting
   **exactly** the register's pinned contract:
   - `renderPreview(markdown: string): string` — RE·2, D3. Escapes HTML
     first (the 5 HTML-significant chars, the `MarkdownRenderer.HtmlEscape`
     set), then maps **only** the RC-pinned subset (headings 1–6,
     paragraphs, `- `/`* ` lists, `1. ` lists, fenced code, `**bold**` →
     `<strong>`, `*italic*` → `<em>`, `` `code` `` → `<code>`,
     `[label](url)` → `<a>` under the client `IsSafeUrl` mirror,
     `![alt](src)` → `<img class="rc-image" loading="lazy" />` under the
     client `isSafeImageSrc` mirror). Emits **only** that set. **No**
     blockquote (`> `), tables, footnotes, or raw HTML (the U2 mirror
     checklist ceiling).
   - `applyToggle(md, sel, kind:'bold'|'italic'|'code') → {value, sel}` —
     RE·1. Wraps the selection in the kind's markers, or **removes** them
     if the selection is already wrapped (the RE2 toggle-off FACES —
     implemented as **two** toggle-off cases: (A) the selection **is** the
     markers + content; (B) the selection is **inside** the markers).
     Preserves/returns the caret (the selection lands on the content,
     shifted by the marker length on each side).
   - `applyBlock(md, caret, kind:'h1'|'h2'|'h3'|'ul'|'ol') → {value, caret}`
     — RE·1. Inserts the block marker + trailing space at the start of the
     caret's line. **No** `'quote'` (U1 drift pause: `MarkdownRenderer`
     has no blockquote branch; RE·2 forbids a marker the server renderer
     lacks). The register's `applyBlock` signature includes `'quote'` —
     **deviation from the register, in favor of the primary tier (the
     design doc) + the U2 mirror checklist**, which both set the union to
     `'h1'|'h2'|'h3'|'ul'|'ol'`.
   - `applyLink(md, sel, url) → {value, sel}` — RE·2. Wraps the selection
     as `[label](url)`; the selection is preserved on the label.
   - `imageLink(alt, id): string` — RC R·3 byte-identical. Returns exactly
     `![alt](/content-image/{id})` — `ContentImageIds.FullSrcRe` picks it
     up unchanged.
   - `isSafeImageSrc(src): boolean` — **exported** (the design doc pins
     this as a separate export for U07's parity tests). Verbatim mirror of
     `MarkdownRenderer.IsSafeImageSrc`: (a) `/content-image/` + 1–128
     lowercase hex, or (b) schemeless relative (no `:`, no `//`, no
     whitespace). Every scheme + every malformed id **rejects**.
   - `bindRichEditor(root: HTMLElement): void` — RE·1, D1/D2. Finds the
     `textarea[data-rich-editor]` + the `.rc-editor-toolbar` + the
     `[data-rich-editor-preview]` pane under `root`. **Omits the image
     button** (removes `button[data-md="image"]` from the DOM) if the
     toolbar or `root` carries `data-rich-editor-no-image`. Renders the
     preview on every `input` (RE·1: the preview is a pure render of the
     textarea's current value). Wires each `button[data-md]` to the right
     `apply*` splice (re-focus + restore selection — the
     `insert-image.ts` `selectionStart`/`selectionEnd` idiom). The
     `data-md="image"` button reuses the **RC upload lane**
     (`POST /content-image` via `apiFetch`, the `insert-image.ts`
     convention — **no** new `api.ts` method, **no** second upload seam)
     and splices `imageLink(alt, id)` at the cursor on success. **No**
     `document.write`, **no** untrusted user HTML, **no** package import.
     The preview output is set via `innerHTML` on the
     `data-rich-editor-preview` element — the content is the
     **escaped-first** output of `renderPreview` (the same construction
     as `MarkdownRenderer`), never raw user HTML.
   - The pure functions (`renderPreview`, `apply*`, `imageLink`,
     `isSafeImageSrc`) are **exported** and **side-effect-free**. The
     self-wire at module load is **guarded** by
     `typeof document !== 'undefined'` so the pure functions remain
     importable in a non-DOM test environment (the `insert-image.ts`
     module does not guard its self-wire; this is the **one deliberate
     deviation**, a pre-planned seam for RE·2's testability precondition,
     **not** a drift pause).
   - **`rich-editor-core.ts` split was NOT needed.** The module is a
     single file; the `typeof document` guard on the self-wire is
     sufficient for the pure functions to be importable in a test.
     Recorded as a pre-planned seam, not a drift pause.
2. `src/Kumunita.Web/wwwroot/css/site.css` (appended one block after the
   `.rc-image` rule, ~line 791+) — the `.rc-editor` (grid split: source
   | preview, responsive — 1 column on mobile, 2 columns on
   `min-width: 768px`) / `.rc-editor-toolbar` + `.rc-editor-toolbar
   .rc-btn` (button reset, hover, `:focus-visible`) /
   `.rc-editor-pane` (the read-only preview wrapper — **reuses** the
   existing `.rc-body` for body copy; the existing `.rc-image` styles
   the preview's `<img>` — **no** re-definition of either). The textarea
   gets a monospace font (the source pane). Boring: no shadows, no new
   palette.

**Build status (both green):**

- `dotnet build Kumunita.slnx -c Debug` — **Build succeeded in 12.8s**
  (no C# touched — this re-verifies the build is unbroken).
- `npm run build --prefix src/Kumunita.Web` — **tsc green** (the new
  module type-checks under `tsc`; the compiled artifact
  `wwwroot/js/lib/rich-editor.js` is emitted, 25.7 KB).

**Pin typo recorded (for U07):** the register's pinned seam test
`ApplyToggle_Bold_WrapsSelection_AndPreservesCaret` asserts
`applyToggle("hello world",[6,11],"bold")` →
`{value:"hello **world**", sel:[7,12]}`. With a **2-char** marker
(`**`), the content `world` (5 chars) is at indices `[8,13]` in the
result `hello **world**` — **not** `[7,12]`. `[7,12]` matches a **1-char**
marker. The implementation is semantically correct (the selection lands
on the content, shifted by the marker length on each side — the RE2 FACES
"the selection is preserved on the content"). **U07 should use `[8,13]`**
for the 2-char bold marker (or `[6+1, 6+1+5] = [7,12]` for a 1-char
italic/code marker). The pin's value + selection are **inconsistent** —
this is a **pin typo**, not a feature deviation.

**Drift pause: none.** Both gaps hold as pinned in U2's mirror checklist.
The `applyBlock` union deviates from the register **in favor of the
primary tier (the design doc) + U2's mirror checklist** (no `'quote'`) —
this is the U1 drift pause carried forward, not a new one. The
`typeof document` guard on the self-wire is a pre-planned seam for
testability, not a drift pause.

**Drift-pause rule (not triggered):** the RC upload surface
(`POST /content-image`) is intact and unchanged; the U2 mirror checklist
still matches `MarkdownRenderer`.

**For U04 (include decision):** `_Layout.cshtml` does **not** include
`insert-image.js` (verified by grep — the `client/lib` convention is a
**per-view** `<script type="module" src="~/js/lib/insert-image.js"></script>`
include, not a layout-wide include). The `rich-editor.js` include is
therefore a **per-view** concern for **U04** to add to the post composer
(`<script type="module" src="~/js/lib/rich-editor.js"></script>`). This
module is dependency-free + self-wiring, so it needs **no** layout change
on its own. The self-wire loops over
`document.querySelectorAll('.rc-editor')` at load, so each view that
includes the module gets its editor wired automatically.

**For U05/U06 (image-button gating):** the `data-rich-editor-no-image`
attribute on the toolbar or the `root` causes `bindRichEditor` to **remove
the `button[data-md="image"]`** from the DOM at bind time (the text toolbar
+ preview are **unaffected**). U05 (Post Edit, Group Edit, both
Announcement composers) and U06 (all reply composers) should carry
`data-rich-editor-no-image` on their toolbar markup. The
`textarea[data-rich-editor]` keeps its `data-rich-editor` attribute on
**every** surface (the text toolbar + preview are on everywhere); the
`data-image-target` attribute is a **RC** attribute (the
`insert-image.ts` module's contract) and is **not** used by
`rich-editor.ts` — U04–U06 should drop `data-image-target` on the
image-gated surfaces (the RC image lane is incomplete there) and keep it
on the image-ON surfaces (Post New, Group New, static page) so the RC
`insert-image.ts` module still works on those surfaces.

**Git status (expected, before move):**
- `src/Kumunita.Web/client/lib/rich-editor.ts` (new, untracked)
- `src/Kumunita.Web/wwwroot/css/site.css` (modified)
- `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md`
  (modified — the U3 section appended)

  (`wwwroot/js/lib/rich-editor.js` is the `tsc` build artifact —
  gitignored, not part of the tree.)

**Note for U08:** the `wwwroot/js/` folder is gitignored
(`.gitignore:20` → `src/Kumunita.Web/wwwroot/js/`) — the compiled
artifacts are **not** tracked (the other `client/lib` modules' compiled
`.js` files are present locally but are build output, not source).
`rich-editor.js` should **not** be committed; only `rich-editor.ts` is
the source. The `npm run build` step regenerates `rich-editor.js` on
demand.

## U4 — Post composer wired

**Date:** 2026-09-15

**Deliverables shipped (exactly 2 files, the closed set):**

1. `src/Kumunita.Web/Views/Posts/New.cshtml` (modified) — the body
   `<textarea name="Body">` is now wrapped in the register's pinned
   `.rc-editor` split-view pattern:
   - `<div class="rc-editor">` containing:
     - `<div class="rc-editor-toolbar" data-rich-editor>` with the
       **full 10-button** toolbar (bold, italic, code, h1, h2, h3, ul,
       ol, link, image — **no** `quote`/blockquote, U1 drift pause).
     - The original `<textarea>` (preserved: `name="Body"`,
       `id="body"`, `class="form-control"`, `rows="6"`, `required`,
       `placeholder="…"`, **plus** `data-rich-editor` and
       `data-image-target`).
     - `<div class="rc-editor-pane rc-body" aria-live="polite"
       data-rich-editor-preview></div>`.
   - The RC `rc-insert-image` file-input block is **kept** (it carries
     the hand-typed-image hint + the `insert-image.ts` upload path —
     the toolbar's image button is the primary path, but the file-input
     is a valid secondary affordance and the RC `insert-image.ts`
     module still self-wires it).
   - `<script type="module" src="~/js/lib/rich-editor.js"></script>`
     added to `@section Scripts` (per-view, **not** layout-level —
     the U3 handoff note's include decision).
   - **Image button is ON** (full 10-button toolbar + `data-image-target`
     on the textarea). Post New's RC image lane is complete:
     `PostsController:692` populates `ImageIds`; serve via the post
     branch of `ContentImageController`.

2. `src/Kumunita.Web/Views/Posts/Edit.cshtml` (modified) — the **same**
   `.rc-editor` split-view pattern around the body textarea, **except**:
   - The toolbar carries `data-rich-editor-no-image` (the U03 module
     removes the `data-md="image"` button from the DOM at bind time).
   - The textarea **drops** `data-image-target` (the RC image lane is
     incomplete on this surface: `UpdatePostAsync` (RC drift pause (c))
     never sets `post.ImageIds`).
   - The `rc-insert-image` block is **removed** (the file-input is
     inert without `data-image-target` on the textarea — leaving it
     would be dead UI). The text toolbar + preview are fully functional.
   - The `@Model.Body` value binding is **preserved** in the textarea.
   - `<script type="module" src="~/js/lib/rich-editor.js"></script>`
     added to `@section Scripts` (same per-view include).
   - The text toolbar (9 visible buttons after the module removes the
     image button) + live preview are **fully on** (the read path
     renders `Post.Body` regardless of how it was authored).

**Include decision:** per-view (both `Posts/New` and `Posts/Edit`),
in `@section Scripts`, **not** moved to `_Layout.cshtml`. The `insert-image.js`
include is kept in both views (New: the `rc-insert-image` block still
needs it; Edit: the include is inert without `data-image-target`, but
removing it is out of scope for this unit — the `insert-image.js`
include is RC's, not RE's, and RE·3 forbids touching frozen RC seams).

**The `rc-insert-image` block choice:** **kept** on New (functional
secondary image affordance, the `insert-image.ts` module self-wires it
via `data-image-target`); **removed** on Edit (inert without
`data-image-target` — leaving a file input that does nothing is worse
than removing it). Recorded here for U05/U06's copy-verify: the
drift-guard checks the **expected** delta (New: `rc-insert-image`
present + `data-image-target` present; Edit: `rc-insert-image` absent
+ `data-image-target` absent + `data-rich-editor-no-image` on toolbar),
not byte-identity.

**Toolbar pin (U2/U3 conformance):** exactly **10** buttons (bold,
italic, code, h1, h2, h3, ul, ol, link, image) — **no** `quote`
(U1 drift pause: the frozen `MarkdownRenderer` has no blockquote
branch; RE·2 forbids a button that emits a marker it can't render).
This matches the U2 mirror checklist + U3's module `applyBlock` union
(`'h1'|'h2'|'h3'|'ul'|'ol'`, no `'quote'`).

**Build status (both green):**

- `dotnet build Kumunita.slnx -c Debug` — **Build succeeded in 12.3s**
  (Razor views compile; no C# touched).
- `npm run build --prefix src/Kumunita.Web` — **tsc green** (the
  module U03 shipped still compiles; this unit adds only Razor markup,
  no TS change).

**Drift pause: none.** Both views' body fields are plain
`<textarea name="Body">` as expected. The U03 module's `bindRichEditor`
contract (`textarea[data-rich-editor]` + `.rc-editor-toolbar` +
`[data-rich-editor-preview]` under `root`) is satisfied by the markup.
The `data-rich-editor-no-image` gate works as pinned (the module
removes the image button from the DOM at bind time). No frozen RC
seam is re-shaped.

**Git status (expected, before move):**
- `src/Kumunita.Web/Views/Posts/New.cshtml` (modified)
- `src/Kumunita.Web/Views/Posts/Edit.cshtml` (modified)
- `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md`
  (modified — the U4 section appended)
- `docs/plans-milestones/in-progress/rich-editor-u04-plan.md` →
  `docs/plans-milestones/done/rich-editor-u04-plan.md` (moved last)

**For U05/U06 (pattern to copy):** the markup pattern is the
`.rc-editor` → `.rc-editor-toolbar[data-rich-editor]` → 10 buttons →
`textarea[data-rich-editor]` → `[data-rich-editor-preview]` sequence.
The **only** per-surface variation is the image-button gate: image-ON
surfaces (Group New, static page) carry the full 10-button toolbar +
`data-image-target`; image-OFF surfaces (Group Edit, both Announcement
composers, all reply composers) carry `data-rich-editor-no-image` on
the toolbar + **no** `data-image-target` on the textarea. The
`rc-insert-image` block is kept only where `data-image-target` is
present (functional); removed where it is absent (inert). The
`rich-editor.js` include is per-view in `@section Scripts` (not
layout-level). The `rc.editor.*` `<kw-l>` keys are **referenced** but
**not yet registered** in `KnownTranslationKeys` (U08 lands them).

## U5 — Group-post + announcement composers wired

**Date:** 2026-09-15

**Deliverables shipped (exactly 4 files, the closed set):**

1. `src/Kumunita.Web/Views/Groups/New.cshtml` (modified) — the body
   `<textarea name="Body">` is now wrapped in **U04's canonical**
   `.rc-editor` split-view pattern:
   - `<div class="rc-editor">` → `<div class="rc-editor-toolbar"
     data-rich-editor>` with the **full 10-button** toolbar (bold,
     italic, code, h1, h2, h3, ul, ol, link, image — **no**
     `quote`/blockquote, the U1 drift pause holds).
   - The original `<textarea>` preserved (`name="Body"`, `id="body"`,
     `class="form-control"`, `rows="6"`, `required`, `placeholder="…"`,
     **plus** `data-rich-editor` **and** `data-image-target`), with
     `@Model.Body` value binding kept.
   - `<div class="rc-editor-pane rc-body" aria-live="polite"
     data-rich-editor-preview></div>`.
   - The RC `rc-insert-image` file-input block + the "Pick a photo"
     hint are **kept** (mirrors U04's Post New — a functional secondary
     image affordance, the `insert-image.ts` module self-wires it via
     `data-image-target`).
   - `<script type="module" src="~/js/lib/rich-editor.js"></script>`
     added to `@section Scripts` (per-view, not layout-level — U04's
     include decision).
   - **Image button ON** (full 10-button toolbar + `data-image-target`).
     The RC group-new write lane is wired (`GroupsController:1085`
     `CreateGroupPostAsync` → `ImageIds: ExtractContentImageIds(...)`);
     serve works via the post branch of `ContentImageController`.

2. `src/Kumunita.Web/Views/Groups/Edit.cshtml` (modified) — the **same**
   pattern, **except**:
   - The toolbar carries `data-rich-editor-no-image` (the U03 module
     removes the `data-md="image"` button from the DOM at bind time).
   - The textarea **drops** `data-image-target` (keeps
     `data-rich-editor`).
   - The `rc-insert-image` block **and** the "Pick a photo" hint are
     **removed** (dead UI — the file-input is inert without
     `data-image-target`; mirrors U04's Post Edit choice).
   - **Image button OFF.** **Why:** `UpdateGroupPostAsync` (RC drift
     pause (c) analog) sets `post.Body` / `post.Title` /
     `post.LanguageCode` / `post.Modified` but **never** `post.ImageIds`
     — a new image would store in `Body` but the post's `ImageIds`
     collection stays stale → the read path 404s. **No**
     `ImageIds = ExtractContentImageIds(...)` line added (that is an RC
     follow-up, not RE scope — RE·3).
   - The `@Model.Body` value binding is **preserved**. The
     `rich-editor.js` include added (per-view); the RC
     `insert-image.js` include is **left in place** (inert without
     `data-image-target`, but removing it is out of RE scope — RE·3;
     recorded in the view's comment).

3. `src/Kumunita.Web/Views/Announcement/New.cshtml` (modified) — same
   pattern, **except**:
   - Toolbar carries `data-rich-editor-no-image`; the textarea **drops**
     `data-image-target`.
   - The `rc-insert-image` block **and** the "Pick a photo" hint are
     **removed** (dead UI).
   - The textarea's original attributes are preserved as-is — **note**
     the announcement body textarea has **no** `placeholder` attribute
     (unlike the post/group ones), and that is preserved (no
     `placeholder` added; the copy keeps each view's exact textarea
     shape).
   - **Image button OFF.** **Why:** RC U03 drift pause — the write lane
     **does** populate `ImageIds` (verified: `AnnouncementController:455`
     New → `ImageIds = ContentImageIds.ExtractContentImageIds(model.Body)`),
     but `ContentImageController.Serve` has **no** announcement branch
     (inert-404: returns `NotFound()` for an `Announcement` resource — no
     `AnnouncementToAuditableResource` adapter). An image would store in
     `ImageIds` but 404 on serve.
   - The `rich-editor.js` include added (per-view); the RC
     `insert-image.js` include left in place (inert, RE·3).

4. `src/Kumunita.Web/Views/Announcement/Edit.cshtml` (modified) — **same**
   as #3 (the announcement **edit** write lane also populates `ImageIds`
   (`AnnouncementController:575` → `ImageIds =
   ContentImageIds.ExtractContentImageIds(model.Body)`), but the serve
   branch is the **same** inert-404 gap). Image button OFF;
   `rc-insert-image` block + hint removed; textarea shape preserved
   (no `placeholder`).

**Copy-verify (RE·2 — one pattern):** the text toolbar (10 buttons, **no**
`quote`) + the `.rc-editor-pane rc-body` preview pane + the
`aria-live="polite"` + the `data-rich-editor` / `data-rich-editor-preview`
attribute contract are **byte-identical** to U04's `Posts/New` +
`Posts/Edit` across all four views. The **only** expected delta is the
image-button gate:

| View | Image button | Toolbar | `data-image-target` | `rc-insert-image` |
|---|---|---|---|---|
| `Groups/New` | **ON** | full 10 (incl. `data-md="image"`) | **kept** | **kept** (mirrors U04 Post New) |
| `Groups/Edit` | **OFF** | `data-rich-editor-no-image` | **dropped** | **removed** (dead UI) |
| `Announcement/New` | **OFF** | `data-rich-editor-no-image` | **dropped** | **removed** (dead UI) |
| `Announcement/Edit` | **OFF** | `data-rich-editor-no-image` | **dropped** | **removed** (dead UI) |

The drift guard checks this **expected** delta (the image-button gate +
`rc-insert-image` presence), not byte-identity — exactly as U04 recorded.

**Announcement save-action parity (recorded, not re-shaped):** both
announcement save lanes bind `Body` **identically** to the post save
convention — `AnnouncementController` New (`:448`) and Edit (`:568`) both
set `Body = model.Body!` **and** `ImageIds =
ContentImageIds.ExtractContentImageIds(model.Body)`. So the announcement
`Body` binding is **not** a drift from the post save — the **only** gap on
the announcement surfaces is the RC U03 **serve** branch (inert-404),
which is what gates the image button. **No** save action was re-shaped (RC
R·1 holds).

**The `insert-image.js` include (RC's, not RE's):** found **present** in
all four views (pre-existing RC U05 include in each `@section Scripts`). On
the three image-OFF views it is **inert** (no `data-image-target` + no
`rc-insert-image` file-input), but it is **left in place** per U04's
reasoning — removing RC's include is out of RE scope (RE·3 forbids touching
frozen RC seams). Each image-OFF view's comment now records *why* it is
inert on that surface (the named RC drift pause).

**The `rc.editor.*` `<kw-l>` keys:** referenced in all four views' button
labels; **not** registered in `KnownTranslationKeys` (U08 owns that — the
TagHelper `en` fallback renders the key string until then, per U04).

**Build status (both green):**

- `dotnet build Kumunita.slnx -c Debug` — **Build succeeded in 7.3s**
  (the 4 Razor views compile; no C# touched).
- `npm run build --prefix src/Kumunita.Web` — **tsc green** (the U03
  module still compiles; this unit adds only Razor markup, no TS change).

**Drift pause: none.** All four views' body fields are plain
`<textarea name="Body">` bound to `@Model.Body` as expected (the
announcement ones simply lack a `placeholder`, which was preserved). The
U03 module's `bindRichEditor` contract
(`textarea[data-rich-editor]` + `.rc-editor-toolbar` +
`[data-rich-editor-preview]` under `root`) is satisfied by all four
markups. The `data-rich-editor-no-image` gate works as pinned (the module
removes the image button from the DOM at bind time). No frozen RC seam is
re-shaped; no C# touched.

**Scope check (hard constraints honored):** no new dependency, no
`.csproj` change, no `package.json` change (RE·3); **no**
`quote`/blockquote button (U1 drift pause); `rc.editor.*` keys **not**
registered (U08); `Milestones.cs` / `README` / `MilestonesTests.cs` / ADR
index untouched (U08); the reply composers (`Posts/Detail`,
`Groups/PostDetail`, `Announcement/Detail`) **not** wired (U06's surface);
the `UpdateGroupPostAsync` / `AnnouncementController` save lanes left
frozen (no `ImageIds` line added on the group-edit gap).

**Git status (expected, before move):**
- `src/Kumunita.Web/Views/Groups/New.cshtml` (modified)
- `src/Kumunita.Web/Views/Groups/Edit.cshtml` (modified)
- `src/Kumunita.Web/Views/Announcement/New.cshtml` (modified)
- `src/Kumunita.Web/Views/Announcement/Edit.cshtml` (modified)
- `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md`
  (modified — the U5 section appended)
- `docs/plans-milestones/in-progress/rich-editor-u05-plan.md` →
  `docs/plans-milestones/done/rich-editor-u05-plan.md` (moved last)

## U6 — Static-page editor + reply composers wired

**Date:** 2026-09-15

**Deliverables shipped (exactly 4 files, the closed set):**

1. `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml` (modified) —
   the **static-page editor** (RC R6 authoring side, the one
   admin-only, image-ON surface). The body `<textarea name="body">` is
   wrapped in U04's canonical `.rc-editor` split-view pattern:
   - `<div class="rc-editor">` → `<div class="rc-editor-toolbar"
     data-rich-editor>` with the **full 10-button** toolbar (bold,
     italic, code, h1, h2, h3, ul, ol, link, image — **no**
     `quote`/blockquote, U1 drift pause holds).
   - The original `<textarea>` preserved: `name="body"` (**lowercase** —
     `LanguagesController.SavePage` binds lowercase `body`; do **not**
     normalize), `id="body"`, `class="form-control"`, `rows="16"`,
     `style="min-height: 12rem;"`, `@Model.Body` value binding — **plus**
     `data-rich-editor` **and** `data-image-target`.
   - The `font-monospace` class is **dropped** (the split view *is* the
     source/preview affordance; the RC `rc-insert-image` hint block is
     still present as the secondary affordance — see below).
   - `<div class="rc-editor-pane rc-body" aria-live="polite"
     data-rich-editor-preview></div>`.
   - The RC `rc-insert-image` file-input block + the "Supports: …" hint
     paragraph are **kept** (mirrors U04's Post New — a functional
     secondary image affordance; the `insert-image.ts` module still
     self-wires it via `data-image-target`).
   - `<script type="module" src="~/js/lib/rich-editor.js"></script>`
     added to `@section Scripts` (per-view, not layout-level — U04's
     include decision). The pre-existing RC `insert-image.js` include is
     **kept** (functional — `data-image-target` is present on the
     textarea).
   - **Image button ON** (full 10-button toolbar + `data-image-target`).
     RC's static-page image lane is complete: `LanguagesController`
     `SavePage` runs `ContentImageIds.ExtractContentImageIds` →
     `ImageIds`; serve via the page branch of `ContentImageController`
     (public, no owner check needed).

2. `src/Kumunita.Web/Views/Posts/Detail.cshtml` (modified) — **all four**
   `name="body"` reply textareas wrapped in U04's pattern with
   `data-rich-editor-no-image` on every toolbar. **Every**
   `<textarea name="reason">` (report-reason) is **untouched** (2
   instances: the post-report form ~line 232, the per-reply-report form
   ~line 443). The four `name="body"` reply textareas wired:
   - `#post-t-body` — the post's "add a translation" body (line ~183
     after wiring, was ~164 before), inside
     `action="/posts/{postId}/translations"`.
   - `#reply-t-body` — the per-reply "add a translation" body (line ~346
     after wiring, was ~310 before), inside
     `action="/posts/{postId}/replies/{r.Id}/translations"`.
   - The unnamed `name="body"` **edit-reply** textarea (line ~403 after
     wiring, was ~351 before), inside
     `action="/posts/{postId}/replies/{r.Id}/edit"`, value
     `@r.Body` preserved.
   - `#reply-body` — the new-reply textarea (line ~491 after wiring, was
     ~423 before), inside `action="/posts/{postId}/replies"`.

   Each wrapper: `.rc-editor` → `.rc-editor-toolbar
   data-rich-editor data-rich-editor-no-image` → **full 10 buttons
   rendered in markup** (the module removes `data-md="image"` at bind
   time → 9 visible) → textarea with `data-rich-editor` added and
   `data-image-target` **absent** (replies have no RC image lane) →
   `.rc-editor-pane rc-body aria-live="polite"
   data-rich-editor-preview`. **No** `rc-insert-image` block (none
   existed pre-RE; RE does not add dead UI).
   `<script type="module" src="~/js/lib/rich-editor.js"></script>` added
   to a **new** `@section Scripts` at the end of the view (the view had
   no `@section Scripts` before — the U03 module's self-wire handles
   every `.rc-editor` root on the page, so one include covers all four
   blocks).

3. `src/Kumunita.Web/Views/Groups/PostDetail.cshtml` (modified) — **the
   identical** reply wiring to #2. **All four** `name="body"` reply
   textareas wired with `data-rich-editor-no-image`; **zero**
   `name="reason"` textareas in this view (verified by enumeration —
   the group-post detail view has no per-reply report intake on the
   group lane, only on the post lane `Posts/Detail`). The four
   `name="body"` reply textareas wired:
   - `#post-t-body` — the post's "add a translation" body (line ~177
     after wiring, was ~159 before).
   - `#reply-t-body` — the per-reply "add a translation" body (line ~320
     after wiring, was ~286 before).
   - The unnamed `name="body"` **edit-reply** textarea (line ~377 after
     wiring, was ~327 before), value `@r.Body` preserved.
   - `#reply-body` — the new-reply textarea (line ~445 after wiring, was
     ~379 before).
   `<script type="module" src="~/js/lib/rich-editor.js"></script>` added
   to a **new** `@section Scripts` at the end of the view.

4. `src/Kumunita.Web/Views/Announcement/Detail.cshtml` (modified) —
   **the identical** reply wiring to #2. **One** `name="body"` textarea
   in this view (verified by enumeration — the announcement detail is a
   public read surface with a single "add a translation" body; no
   per-reply lane, no report intake, no edit lane):
   - `#ann-t-body` — the announcement's "add a translation" body (line
     ~169 after wiring, was ~152 before), inside
     `action="/announcements/{Model.Id}/translations"`.
   **Zero** `name="reason"` textareas in this view (verified by
   enumeration). `data-rich-editor-no-image` on the toolbar; **no**
   `data-image-target`; **no** `rc-insert-image` block.
   `<script type="module" src="~/js/lib/rich-editor.js"></script>` added
   to a **new** `@section Scripts` at the end of the view.

**The `rc-insert-image` block choice (per view):**

| View | `rc-insert-image` | `data-image-target` | Image button |
|---|---|---|---|
| `Languages/PreviewPage` | **kept** (functional — RC's static-page image lane is complete) | **kept** | **ON** |
| `Posts/Detail` | **none added** (none existed pre-RE; would be dead UI without `data-image-target`) | **absent** | **OFF** |
| `Groups/PostDetail` | **none added** (same) | **absent** | **OFF** |
| `Announcement/Detail` | **none added** (same) | **absent** | **OFF** |

This matches U04's rule: **keep** the RC `rc-insert-image` block only
where `data-image-target` is present (functional); **omit** it where
`data-image-target` is absent (dead UI). The three reply views had no
pre-RE `rc-insert-image` block at all, so RE does not add one — it
would be dead UI without `data-image-target`.

**The `name="body"` enumeration (per view, pre-RE → post-RE):**

| View | Pre-RE `name="body"` count | Post-RE wired count | `name="reason"` count (untouched) |
|---|---|---|---|
| `Posts/Detail.cshtml` | 4 | 4 | 2 (line ~232, ~443 — **both untouched**) |
| `Groups/PostDetail.cshtml` | 4 | 4 | 0 (verified — no `name="reason"` in this view) |
| `Announcement/Detail.cshtml` | 1 | 1 | 0 (verified — no `name="reason"` in this view) |
| `Languages/PreviewPage.cshtml` | 1 | 1 | 0 (verified — no `name="reason"` in this view) |

**Total:** 10 `name="body"` textareas wired (4 + 4 + 1 + 1);
**zero** `name="reason"` textareas touched (2 in `Posts/Detail`,
verified untouched). No `name="body"` textarea missed, no
`name="reason"` textarea accidentally wired.

**Copy-verify (RE·2 — one pattern):** the text toolbar (10 buttons,
**no** `quote`) + the `.rc-editor-pane rc-body` preview pane +
`aria-live="polite"` + the `data-rich-editor` /
`data-rich-editor-preview` attribute contract are **byte-identical**
to U04's `Posts/New` + `Posts/Edit` across all four views. The **only**
expected delta is the image-button gate:

| View | Image button | Toolbar | `data-image-target` | `rc-insert-image` |
|---|---|---|---|---|
| `Languages/PreviewPage` | **ON** | full 10 (incl. `data-md="image"`) | **kept** | **kept** (functional) |
| `Posts/Detail` | **OFF** | `data-rich-editor-no-image` | **absent** | **none** |
| `Groups/PostDetail` | **OFF** | `data-rich-editor-no-image` | **absent** | **none** |
| `Announcement/Detail` | **OFF** | `data-rich-editor-no-image` | **absent** | **none** |

The drift guard checks this **expected** delta (the image-button gate +
`rc-insert-image` presence), not byte-identity — exactly as U04/U05
recorded.

**The `insert-image.js` include (RC's, not RE's):**

- `Languages/PreviewPage.cshtml` — **present** (pre-existing RC U05
  include), **functional** (`data-image-target` + `rc-insert-image`
  file-input both present). Kept.
- `Posts/Detail.cshtml`, `Groups/PostDetail.cshtml`,
  `Announcement/Detail.cshtml` — **absent** (none of these views had a
  pre-RE `insert-image.js` include — the RC reply-image lane is
  unshipped, so RC never added it). RE does **not** add it (would be
  dead UI without `data-image-target`). **Zero** new `insert-image.js`
  includes added in U06.

**The `rc.editor.*` `<kw-l>` keys:** referenced in all four views' button
labels (the 10-button toolbar); **not** registered in
`KnownTranslationKeys` (U08 owns that — the TagHelper `en` fallback
renders the key string until then, per U04). **RC's**
`rc.markdown_hint` key (RC U05's drift pause — the placeholder never
registered) is **RC's** debt; U06 **does not** reference it (the
`PreviewPage.cshtml` "Supports: …" hint paragraph is a static string,
not a `<kw-l>` key — no RC debt touched).

**Build status (both green):**

- `dotnet build Kumunita.slnx -c Debug` — **Build succeeded in 8.8s**
  (the 4 Razor views compile; no C# touched).
- `npm run build --prefix src/Kumunita.Web` — **tsc green** (the U03
  module still compiles; this unit adds only Razor markup, no TS
  change).

**Drift pause: none.** All four views' body fields are plain
`<textarea>` as expected (each with the exact attribute set the U03
module's `bindRichEditor` contract requires: `textarea[data-rich-editor]`
+ `.rc-editor-toolbar` + `[data-rich-editor-preview]` under the
`.rc-editor` root). The `data-rich-editor-no-image` gate works as pinned
(the module removes the `data-md="image"` button from the DOM at bind
time). The `name="body"` lowercase binding is preserved exactly on
`PreviewPage` (the RC save action binds lowercase `body`) and on all
three reply views (the RC reply write actions bind lowercase `body`).
No frozen RC seam is re-shaped; no C# touched.

**Scope check (hard constraints honored):** no new dependency, no
`.csproj` change, no `package.json` change (RE·3); **no**
`quote`/blockquote button (U1 drift pause); `rc.editor.*` keys **not**
registered (U08 owns them); `Milestones.cs` / `README` /
`MilestonesTests.cs` / ADR index untouched (U08 owns); **no** C# file
modified (the static-page save action + the reply write actions + the
serve branch all stay frozen — RE·3); **no** `PostReply.ImageIds`
change (the reply image lane stays unshipped — RE·3 forbids inventing
it); **no** other view wired (U4/U5 already wired the post/group-post/
announcement composers; U7 is the parity tests; U8 is the close).

**Git status (expected, before move):**
- `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml` (modified)
- `src/Kumunita.Web/Views/Posts/Detail.cshtml` (modified)
- `src/Kumunita.Web/Views/Groups/PostDetail.cshtml` (modified)
- `src/Kumunita.Web/Views/Announcement/Detail.cshtml` (modified)
- `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md`
  (modified — the U6 section appended)
- `docs/plans-milestones/in-progress/rich-editor-u06-plan.md` →
  `docs/plans-milestones/done/rich-editor-u06-plan.md` (moved last)

**Pre-existing modification on the moved plan file (not made by U06):**
`docs/plans-milestones/done/rich-editor-u06-plan.md` carries a
pre-existing content correction (the Deliverables §1 bullet reads
"**10** `data-md` buttons … **no `quote`/blockquote**, U1 drift pause"
where the committed version reads "the same **11** `data-md` buttons
**including image**, the same `rc.editor.*` `<kw-l>` labels"). The
correction is **correct** — it matches the actual U4/U5/U6 markup
(10 buttons, no `quote`). It was already present in the working tree
before U06's edits (file mtime predates U06's first edit). Left
unstaged per the "do not stage or commit" instruction — the reviewer
should include it in the U06 commit (it is a content fix to the U06
plan file, not a new artifact).
