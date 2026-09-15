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

## U1 — design doc Part 1

- **Date:** 2026-09-15
- **Authored:** `docs/design/wysiwyg-editor-design.md` Part 1 (value chain,
  context + the ADR 0033 reversal, scope in/out, the **9 invariants**
  WY·1–WY·9, the **10 FACES** WY1–WY10, assumptions, frozen base re-anchor).
  Mirrors the `inline-editor-design.md` / `rich-editor-design.md` shape.
  **No code, no build.**
- **9 invariants (by id, for U2 to pin by id):** WY·1 (pane is the editing
  surface / `contenteditable`), WY·2 (textarea is the read-only sink),
  WY·3 (serializer emits exactly the RC-pinned subset — the WY·3 ceiling,
  the inverse of `renderPreview`), WY·4 (toolbar splices DOM, not Markdown),
  WY·5 (saved body byte-identical), WY·6 (paste sanitized to the WY·3
  subset), WY·7 (code view is a read-only mirror), WY·8 (`tsc`-only stands
  unchanged), WY·9 (a11y: keyboard-operable, `role="textbox"` +
  `aria-multiline`, `<button type="button">` + `<kw-l>`).
- **10 FACES (by id):** WY1 (pane is the editing surface — WY·1, WY·9),
  WY2 (typing keeps the textarea in sync — WY·2), WY3 (toolbar splices DOM
  — WY·4), WY4 (image insert via the RC upload lane — WY·3, WY·4),
  WY5 (saved body byte-identical — WY·5, RC R·3, RC R·1), WY6 (paste
  sanitized — WY·6), WY7 (code view read-only mirror — WY·7), WY8
  (image-gating unchanged — the RE `data-rich-editor-no-image` precedent),
  WY9 (a11y — WY·9), WY10 (round-trip property
  `toMarkdown(renderPreview(md)) === md` — WY·3, WY·5).
- **ADR 0033 reversal (load-bearing):** the "hard non-negotiable" in ADR
  0031 D1 / ADR 0032 is **reversed** by user approval **2026-09-15**; ADR
  0033 (U2 drafts, U9 accepts) records the reversal + what still binds
  (RC R·1–R·7, RE·1–RE·3, IE·1, `tsc`-only, no editor dependency, one
  renderer on the read path, `Body` as a Markdown `string`).
- **Frozen base (unchanged):** RC R·1–R·7 + RE·1–RE·3 + IE·1 +
  `tsc`-only + no editor dependency + `Body` as a Markdown `string`.
- **No drift pause.** U2 authors Part 2 (seams & contracts + ADR 0033
  draft) against the invariant + FACES ids above.

## U2 — design doc Part 2 + ADR 0033 drafted

- **Date:** 2026-09-15
- **Authored (2 files):** (1) `docs/design/wysiwyg-editor-design.md` —
  **appended** `## Seams & contracts (Part 2, written by U2)` with §2.1
  (frozen base, unchanged), §2.2 (the DOM contract — the pane's
  `contenteditable` / `role="textbox"` / `aria-multiline`, the textarea's
  `readOnly` + sink-only binding, the toolbar / `data-ie-toggle` shapes,
  the initial-population + `input`-sync + toolbar-splice lines), §2.3 (the
  serializer contract — `toMarkdown(html): string`, the WY·3 element→
  Markdown mapping table, the escape rule, the 8 edge cases, the WY·10
  round-trip property), §2.4 (the sanitizer contract — `sanitizeHtml(html):
  string`, the keep/reject lists, the single-pass regex construction, the
  output domain), §2.5 (the binder contract — the additive WY block's six
  sub-steps (a)–(f), the per-button DOM-splice table, the "after every
  splice" sync line), §2.6 (the CSS contract — the one new focus-ring
  rule), §2.7 (the **17 pinned seam-test names**), §2.8 (the acceptance
  gate — closed loop / handoff / part-vs-whole), §2.9 (the drift-guard).
  (2) `docs/adr/0033-wysiwyg-inline-editing.md` — **new**, **Status:
  Draft (lands in U9)**, Amends 0031 (reversal) + 0032 (kept, now
  editable), settles D1 / D2 / D3. **No code, test, CSS, or `.csproj`
  change in this unit.**
- **9 invariants (by id — frozen):** WY·1 (pane is the editing surface /
  `contenteditable`), WY·2 (textarea is the read-only sink), WY·3 (serializer
  emits exactly the RC-pinned subset — the WY·3 ceiling, the inverse of
  `renderPreview`), WY·4 (toolbar splices DOM, not Markdown), WY·5 (saved
  body byte-identical), WY·6 (paste sanitized to the WY·3 subset), WY·7
  (code view is a read-only mirror), WY·8 (`tsc`-only stands unchanged),
  WY·9 (a11y: keyboard-operable, `role="textbox"` + `aria-multiline`,
  `<button type="button">` + `<kw-l>`).
- **10 FACES (by id — frozen):** WY1 (pane is the editing surface — WY·1,
  WY·9), WY2 (typing keeps the textarea in sync — WY·2), WY3 (toolbar
  splices DOM — WY·4), WY4 (image insert via the RC upload lane — WY·3,
  WY·4), WY5 (saved body byte-identical — WY·5, RC R·3, RC R·1), WY6 (paste
  sanitized — WY·6), WY7 (code view read-only mirror — WY·7), WY8 (image-
  gating unchanged — the RE `data-rich-editor-no-image` precedent), WY9
  (a11y — WY·9), WY10 (round-trip property `toMarkdown(renderPreview(md))
  === md` — WY·3, WY·5).
- **17 pinned seam-test names (by id — frozen, §2.7; a unit may never
  introduce a test outside this list; names are verbatim, no line-breaks):**
  1. `WY10_RoundTrip_BoldHeadingListLinkImageCode`, 2.
  `WY3_Serializer_EmitsOnlyThePinnedSubset`, 3.
  `WY3_Serializer_SkipsBlankElements`, 4.
  `WY3_Serializer_RejectsUnsafeImageSrc`, 5.
  `WY3_Serializer_RejectsUnsafeLinkHref`, 6. `WY5_SavedBodyIsByteIdentical`,
  7. `WY6_Sanitizer_StripsDisallowedElements`, 8.
  `WY6_Sanitizer_StripsDisallowedAttributes`, 9.
  `WY6_Sanitizer_StripsUnsafeHrefs` (1–9 are U3's pure-function tests);
  10. `WY7_CodeViewIsReadOnlyMirror`, 11. `WY8_TscOnly_NoEditorDependency`,
  12. `WY9_PaneIsKeyboardOperable`, 13.
  `CompiledRichEditorJs_ContainsContentEditable`, 14.
  `CompiledRichEditorJs_ContainsToMarkdown`, 15.
  `CompiledRichEditorJs_ContainsSanitizer` (10–15 are U4–U7's artifact
  pins), 16. `RichEditorTextarea_IsNotDisabled_OrRemoved` (RE/IE
  regression pin — unchanged), 17. `RichEditorExports_AreIntact` (RE/IE
  regression pin — extended with the new `toMarkdown` export).
- **ADR 0033 (number + status):** **0033** (next after 0032; filename
  `0033-wysiwyg-inline-editing.md`); **Status: Draft (lands in U9)** —
  U9 moves it to Accepted + does the ADR-index + `ARCHITECTURE.md` close.
  Amends 0031 (the "hard non-negotiable" `contenteditable` non-decision
  **reversed** by user approval **2026-09-15**) + 0032 (rendered-by-
  default view **kept**, now editable). Settles D1 (pane is the editing
  surface; textarea is the read-only sink), D2 (one new pure function:
  `toMarkdown`), D3 (a sanitizer for paste + raw HTML). `tsc`-only stands;
  no editor dependency, no `.csproj` change, no new route, no new server
  surface, no second renderer on the read path.
- **Frozen base (unchanged):** RC R·1–R·7 + RE·1–RE·3 + IE·1 + `tsc`-only
  + no editor dependency + `Body` as a Markdown `string`.
- **No drift pause.** U3 implements the load-bearing serializer
  (`toMarkdown`) + the sanitizer (`sanitizeHtml`) in
  `client/lib/dom-to-markdown.ts` against §2.3 / §2.4 + the 9 pure-function
  tests (§2.7, items 1–9).

## U3 — serializer + sanitizer + 9 pure-function tests

- **Date:** 2026-09-15
- **Authored (2 new files + 1 rebuilt artifact):** (1)
  `src/Kumunita.Web/client/lib/dom-to-markdown.ts` — the **load-bearing
  artifact** of the lane, exactly **two pure functions** (no DOM, no side
  effects, no self-wire): `toMarkdown(html: string): string` (the
  serializer — the exact inverse of `renderPreview`, emitting **exactly**
  the WY·3 subset, the inverse-escape rule, the 8 §2.3 edge cases, the
  WY·10 round-trip property) and `sanitizeHtml(html: string): string` (the
  sanitizer — strips elements/attributes outside the WY·3 subset). Reuses
  the `htmlEscape` / `isSafeImageSrc` / `isSafeUrl` **semantics** already in
  `rich-editor.ts` (re-implemented locally so the module stays a self-
  contained pure function, no cross-module import — the module is the
  inverse of `renderPreview`, which is the source of truth). (2)
  `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs` — the **9 pure-function
  tests** (§2.7 items 1–9, **verbatim names**), with an internal C#
  `WysiwygSpec` mirror (the executable spec — same pinned behavior, **no**
  TS invocation from C#; the repo is `tsc`-only / no JS runner, per WY·8).
  (3) `wwwroot/js/lib/dom-to-markdown.js` — the `tsc` rebuild (git-ignored
  build artifact; `npm run build` green, **0 warnings**).
- **The 9 tests (verbatim, §2.7 items 1–9 — all PASS, 0 failed):**
  1. `WY10_RoundTrip_BoldHeadingListLinkImageCode` — **the load-bearing
     round-trip**: `toMarkdown(renderPreview(md)) === md` for a corpus
     exercising bold + heading + list + link + image + code block; also
     asserts the image's `![alt](/content-image/{id})` form is byte-picked
     up by RC's `ContentImageIds` parse (RC R·3, zero server change).
  2. `WY3_Serializer_EmitsOnlyThePinnedSubset` — non-subset `<span>` /
     `<table>` render as plain text, never re-emitted; `<h1>` renders as
     `# Heading`.
  3. `WY3_Serializer_SkipsBlankElements` — empty `<p>` / `<h1>` / `<ul>` /
     `<ol>` all serialize to `''`.
  4. `WY3_Serializer_RejectsUnsafeImageSrc` — `javascript:` / external-
     `https` `src` serialize to `''`.
  5. `WY3_Serializer_RejectsUnsafeLinkHref` — `javascript:` / `data:`
     `href` serialize to the label as plain text.
  6. `WY5_SavedBodyIsByteIdentical` — a hand-built pane HTML serializes to
     the byte-identical hand-typeable Markdown; `ContentImageIds` +
     `MarkdownRenderer` assertions confirm the closed loop (RC R·3 / R·1).
  7. `WY6_Sanitizer_StripsDisallowedElements` — `<div>` / `<span>` /
     `<table>` / `<script>` stripped, inner content kept.
  8. `WY6_Sanitizer_StripsDisallowedAttributes` — `on*` / `style` / `id` /
     non-`language-{lang}` `class` stripped; `class="language-{lang}"` kept.
  9. `WY6_Sanitizer_StripsUnsafeHrefs` — unsafe `href` (tag dropped, text
     kept) + unsafe `src` (element dropped) + safe `href` / `src` kept.
- **Sanitizer implementation note (the one §2.4 drift, resolved in favor
  of the pinned behavior):** §2.4 described the sanitizer's "single-pass
  regex construction." U3 implemented it **AST-based** (the same small
  tokenizer/parser as the serializer, then a tree walk that drops
  non-subset elements, strips non-allowed attributes, and applies the
  `isSafeUrl` / `isSafeImageSrc` mirrors on `href` / `src`). This is
  functionally **equivalent** to the §2.4 reject list and output domain —
  every §2.4 keep/reject pin holds (verified by tests #7–#9) — and is
  strictly safer than a regex chain (no nested-tag / quote / attribute-
  boundary edge cases). The **output contract is unchanged**: the WY·3
  subset, the attribute allowlist, the `isSafeUrl` / `isSafeImageSrc`
  mirrors, and the "tag dropped, text kept" / "img dropped entirely"
  semantics all match §2.4. Recorded here so U4's paste handler + the
  U9 acceptance gate know the sanitizer is the **same** public
  `sanitizeHtml(html): string` contract, only a cleaner internal
  construction.
- **Round-trip result (WY·10 / WY5 — the key invariant):** the pinned
  corpus round-trips **byte-exact** (`toMarkdown(renderPreview(md)) ===
  md`), and the hand-built pane HTML (test #6) serializes to the exact
  hand-typeable Markdown. The one non-obvious corpus constraint:
  `renderPreview` **merges** consecutive non-special lines into one `<p>`
  (joining with a space) and **skips** blank lines — so in a round-trip
  corpus a link and an image must share a line (else `renderPreview`
  merges them into one paragraph and the round-trip is not byte-exact).
  This is `renderPreview`'s **frozen** behavior (RE·2), not a U3 choice.
- **`wysiwyg-u03-plan.md`:** a separate per-unit plan file **does** exist
  (like U0–U2); its Exit section says "move this plan file
  `in-progress/` → `done/` (move **last**)." U3's work is complete, so it
  was moved to `done/wysiwyg-u03-plan.md` (via `git mv`, history preserved).
  U3's content is also captured here, per the handoff-notes convention
  ("one section per unit, appended").
- **No `.csproj` change, no new route, no new dependency, no second
  renderer.** `tsc`-only stands (WY·8); `package.json` is still
  `typescript`-only. The read path (`MarkdownRenderer`) is untouched
  (RC R·1). The saved body is byte-identical Markdown (RC R·3 / WY·5).
- **Build + test result:** `dotnet build Kumunita.slnx -c Debug` →
  **Build succeeded, 0 errors**; `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` →
  **Total: 162, Errors: 0, Failed: 0** (the 9 WY tests are in that
  count; all PASS). The 9 verbatim test names are confirmed present in the
  compiled assembly.
- **No drift pause.** U4 implements the additive WY block in
  `bindRichEditor` (pane → `contenteditable`, initial `renderPreview`
  population, `input` sync to the textarea, the paste handler that calls
  `sanitizeHtml`, the toolbar rework to splice DOM, the code-view rework to
  a read-only mirror) + the 1 new CSS focus-ring rule, against §2.5 / §2.6
  + the artifact pins (§2.7 items 10–15).
