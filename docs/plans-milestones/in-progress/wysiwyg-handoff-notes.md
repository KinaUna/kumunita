# WYSIWYG inline editing (`WY`) — rolling handoff notes

> **The scratch tier** of the WY lane's three-tier contract (the design
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
- **Register:** `docs/plans-milestones/plan-wysiwyg.md` (U0–U9)
- **Design doc (primary):** `docs/design/wysiwyg-editor-design.md` (U1/U2
  author)
- **ADR:** `docs/adr/0033-wysiwyg-inline-editing.md` (U2 authors; U9 moves
  it to Accepted; **Amends** 0031 — the "hard non-negotiable" non-decision is
  **reversed** by user approval 2026-09-15; **Amends** 0032 — the
  rendered-by-default view is kept, now editable). Numbers after 0032.
- **Scope:** the **editable rendered** half of the RE/IE split view — the
  rendered pane (`rc-editor-pane` / `data-rich-editor-preview`) becomes the
  **editable surface** (`contenteditable="true"`, set by the binder at
  runtime, **not** in the Razor), the Markdown source `<textarea>` becomes a
  **read-only mirror** behind the same `data-ie-toggle` toggle, on **every**
  existing RE/IE composer surface (the **10 view files / 16 editor blocks**
  RE U04–U06 + IE shipped). **Client-only and additive:** one new pure
  serializer (`toMarkdown`) + one new pure sanitizer (`sanitizeHtml`) in
  `client/lib/dom-to-markdown.ts` (the inverse of `renderPreview`), one
  additive `WY block` in `bindRichEditor` (pane → `contenteditable`, initial
  `renderPreview` population, `input` sync to the textarea, `paste`
  handler, toolbar rework to splice DOM, code-view rework to read-only
  mirror), and one CSS focus-ring rule
  (`.rc-editor-pane[contenteditable="true"] { cursor: text; outline: … }`).
  **No new dependency in `package.json`** (still `typescript`-only), **no**
  `.csproj` change, **no** new route, **no** new server surface, **no**
  stored field, **no** second renderer on the read path
  (`MarkdownRenderer` is untouched). The saved body is **byte-identical**
  Markdown the RC read path (`MarkdownRenderer`) already renders.
- **Out of scope (named deferrals for a future WY-2 lane, if one comes):**
  nested lists, blockquotes, tables, footnotes, strikethrough,
  `execCommand`-based undo/redo (the browser's native `contenteditable` undo
  is the floor; a custom undo/redo is a future lane), mobile-specific editing
  UX, caret-mapping between the pane and the code view, and localStorage
  persistence of the editing preference. Each is a **future** lane, not a WY
  re-open.
- **Frozen base (RC + RE + IE, unchanged):** `MarkdownRenderer` ·
  `ContentImageIds` · `IMediaStore` · `MediaObject` · the `ImageIds` fields ·
  the `GET`/`POST /content-image` routes · the `.rc-body`/`.rc-image`/
  `.rc-editor-*` CSS · `insert-image.ts` · the 6 RE pure functions +
  `bindRichEditor` · RC R·1–R·7 invariants · RE·1–RE·3 invariants · IE·1
  (the source is hidden via a **CSS class**, never `disabled`/`hidden`-
  attributed/removed — the textarea stays the live form field the server
  binds). This lane adds **nine** invariants (WY·1–WY·9) and a small FACES
  set (WY1–WY10). **WY·1–WY·9 (pinned):**
  - **WY·1** — the pane is the editing surface (`contenteditable="true"`).
  - **WY·2** — the textarea is the read-only sink (RC R·3 / RE·1 unchanged;
    never removed, disabled, or re-shaped; the server binds it on submit).
  - **WY·3** — the serializer emits **exactly** the RC-pinned subset (P,
    H1–H6, UL/OL, LI, STRONG, EM, CODE, A, IMG, PRE/CODE) and **nothing
    more** (the inverse of `renderPreview`).
  - **WY·4** — the toolbar splices **DOM** (not Markdown) into the pane (the
    Selection / Range API on the `contenteditable`).
  - **WY·5** — the saved body is **byte-identical** to what a resident could
    have hand-typed in the code view (RC R·3 / RC R·1 unchanged; the read path
    is untouched).
  - **WY·6** — paste / raw HTML is **sanitized** to the WY·3 subset before
    insertion (the escape-first / `isSafeImageSrc` / `isSafeUrl` semantics
    already in `rich-editor.ts` are reused).
  - **WY·7** — the code view (the `</>` toggle) is a **read-only mirror** of
    the pane (the textarea's `.value` is the serialized Markdown; the label
    swap is kept).
  - **WY·8** — the `tsc`-only constraint stands unchanged (no editor
    dependency in `package.json`; no `.csproj` change).
  - **WY·9** — a11y: the pane is keyboard-operable, reachable in tab order,
    and carries `role="textbox"` + `aria-multiline`; the toolbar buttons stay
    `<button type="button">` + localized via `<kw-l>`.

<!-- U0 appends its section below this line. One `##` section per unit, in
     order (U0, U1, … U9). Never rewrite a prior section. -->

## U0 — Kickoff verified

- **Date:** 2026-09-15
- **Surface verified (grep `class="rc-editor"` in `Views/`):** **16** editor
  blocks across **10** view files (the register's "10 composer surfaces" =
  the 10 view files; the IE handoff U0 already flagged the analogous register
  "15" figure as an arithmetic error — the real count is 16 blocks):

  | File | Line(s) | Count |
  |------|---------|-------|
  | `Posts/New.cshtml` | 89 | 1 |
  | `Posts/Edit.cshtml` | 93 | 1 |
  | `Posts/Detail.cshtml` | 169, 333, 392, 481 | 4 |
  | `Groups/New.cshtml` | 61 | 1 |
  | `Groups/Edit.cshtml` | 62 | 1 |
  | `Groups/PostDetail.cshtml` | 163, 307, 366, 435 | 4 |
  | `Announcement/New.cshtml` | 109 | 1 |
  | `Announcement/Edit.cshtml` | 109 | 1 |
  | `Announcement/Detail.cshtml` | 155 | 1 |
  | `Languages/PreviewPage.cshtml` | 50 | 1 |

  (`data-rich-editor-preview` matches the same 16 blocks / 10 files. The
  `data-ie-toggle` button lives in the shared `_RichEditorToggle.cshtml`
  partial, included per surface.)
- **RC-pinned subset verified (the `renderPreview` mirror ⇔
  `MarkdownRenderer.RenderHtml` C# — the two match verbatim):** headings 1–6
  (`<h1>`–`<h6>`), paragraphs (`<p>`), unordered lists (`<ul>`/`<li>`),
  ordered lists (`<ol>`/`<li>`), fenced code (`<pre><code>` + optional
  `class="language-{lang}"`), inline code (`<code>`), bold (`<strong>`),
  italic (`<em>`), links (`<a href="…">`), images (`<img src="/content-image/
  {1–128 hex}" … class="rc-image" loading="lazy">`). **Out (no branch in
  `MarkdownRenderer`):** blockquote, tables, footnotes, raw HTML. This is the
  WY·3 ceiling the serializer must emit — **nothing more**.
- **Binder / pure-function surface (verified from `rich-editor.ts`):** 6
  pure/predicate functions (`renderPreview`, `applyToggle`, `applyBlock`,
  `applyLink`, `imageLink`, `isSafeImageSrc`) + the `bindRichEditor` binder.
  `isSafeImageSrc` is the exported client mirror of
  `MarkdownRenderer.IsSafeImageSrc` (the `/content-image/{1–128 hex}` route
  shape + schemeless-relative; every scheme rejected). `imageLink` emits the
  exact `![alt](/content-image/{id})` form `ContentImageIds.FullSrcRe`
  (`/content-image/([0-9a-f]{1,128})(?![0-9a-f])`) already parses — RC R·3
  byte-identity holds. U3's new `toMarkdown` / `sanitizeHtml` must be the
  inverse of `renderPreview` and reuse these same predicates.
- **`ContentImageIds` (verified):** `ExtractContentImageIds(body)` scans the
  Markdown body for `/content-image/{id}` and returns the distinct ids in
  first-occurrence order. The serializer stays byte-compatible with this
  parse — zero server change.
- **ADR number:** **0033** (next after 0032; filename
  `0033-wysiwyg-inline-editing.md` per the U00 plan note; U9 verifies name +
  number against the ADR index before accepting). **User-approval date of the
  ADR 0031 D1 / ADR 0032 "hard non-negotiable" reversal: 2026-09-15.**
- **`package.json` (per RE·3 / WY·8):** `typescript`-only — the no-editor-
  dependency constraint holds at WY lane open.

**No drift pause.** All assumptions verified. U1 authors the design doc
Part 1 against the 16-block / 10-file list and the RC subset above.
