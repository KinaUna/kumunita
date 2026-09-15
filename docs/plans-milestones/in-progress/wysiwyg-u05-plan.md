# U5 — The toolbar rework (splice DOM, not Markdown)

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U5 (of U0–U9)
- **Kind:** code (binder wiring + 2 artifact-string tests)

## Goal

Rework the toolbar's `click` handlers in `bindRichEditor` (§2.5)
to **splice DOM** (the Selection / Range API on the pane) instead
of splicing Markdown into the textarea. The same buttons (B, I, C,
H1, H2, H3, •, 1., Link, Image) stay; their click handlers change.
The image button reuses the **RC upload lane** (`POST
/content-image` via `apiFetch`, the `insert-image.ts` convention
— **no** new route, **no** second upload). Add the **2**
artifact-string tests
(`CompiledRichEditorJs_ContainsSelectionApi` +
`RichEditorExports_AreIntact`).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/wysiwyg-editor-design.md` §2.5 (the binder
   contract — the toolbar's `click` handlers — the **primary**
   source) + §2.2 (the DOM contract — the Selection / Range API) +
   §2.7 (the artifact-string test names — the 2 tests this unit
   authors).
2. `src/Kumunita.Web/client/lib/rich-editor.ts` (the full module
   — the 6 pure functions + the toolbar's `click` handlers — the
   wiring to rework).
3. `src/Kumunita.Web/client/lib/insert-image.ts` (the RC upload
   lane — the `POST /content-image` + the `apiFetch` convention
   the image button reuses).
4. `src/Kumunita.Web/client/lib/dom-to-markdown.ts` (U3's
   serializer — the `toMarkdown` function the toolbar calls after
   each splice to keep the textarea in sync).
5. `tests/Kumunita.Web.Tests/InlineEditorTests.cs` (the
   artifact-string pin pattern — the `CompiledRichEditorJs_*`
   test shape the 2 tests follow).

## Deliverables (2 files)

1. **`src/Kumunita.Web/client/lib/rich-editor.ts`** — the
   toolbar's `click` handlers (§2.5). The **existing** 6 pure
   functions (`applyToggle` / `applyBlock` / `applyLink` /
   `imageLink` / `isSafeImageSrc` + `renderPreview`) are
   **untouched** — the toolbar's `click` handlers change from
   "splice Markdown into the textarea" to "splice DOM into the
   pane, then call `textarea.value = toMarkdown(pane.innerHTML)`".
   **The per-button mapping** (the WY·4 invariant):
   - **B** (bold) → wrap the selection in `<strong>` (the
     Selection / Range API — the `range.surroundContents(new
     Element('strong'))` idiom, with a fallback for a selection
     that crosses element boundaries — the `range.extractContents()`
     + `strong.appendChild(extracted)` + `range.insertNode(strong)`
     construction);
   - **I** (italic) → wrap the selection in `<em>` (the same
     construction);
   - **C** (code) → wrap the selection in `<code>` (the same
     construction);
   - **H1** / **H2** / **H3** → change the current block's tag to
     `<h1>` / `<h2>` / `<h3>` (the `range.startContainer` + the
     `parentNode.replaceChild(new Element('h1'), currentBlock)`
     construction — the caret is placed inside the new heading);
   - **•** (ul) → wrap the current block's text in a `<ul><li>`
     (the `ul.appendChild(li); li.appendChild(text)`
     construction);
   - **1.** (ol) → wrap the current block's text in a `<ol><li>`
     (the same construction);
   - **Link** → `window.prompt('URL:')` + wrap the selection in
     `<a href="…">` (the same construction as B/I/C, with the
     `isSafeUrl` check — a rejected url splices the label as plain
     text);
   - **Image** → the **RC upload lane** (`POST /content-image` via
     `apiFetch` + the `fileInput.click()` idiom from
     `insert-image.ts`; on success, splice an
     `<img src="/content-image/{id}" alt="…">` into the pane at
     the caret; the `isSafeImageSrc` check is reused).
   - **After every splice**, the toolbar calls
     `textarea.value = toMarkdown(pane.innerHTML)` (the binder
     keeps the textarea in sync — the WY·2 invariant).
2. **`tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`** (extend —
   add **2** artifact-string tests):
   - `CompiledRichEditorJs_ContainsSelectionApi` — assert the
     compiled JS contains the string `surroundContents` (the WY·4
     invariant: the toolbar splices DOM via the Selection / Range
     API).
   - `RichEditorExports_AreIntact` — assert the compiled JS
     still exports the 6 RE pure functions (`applyToggle`,
     `applyBlock`, `applyLink`, `imageLink`, `isSafeImageSrc`,
     `renderPreview`) — the **frozen** base (RE·1–RE·3) is
     **untouched**.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `cd src/Kumunita.Web; npm run build` (tsc) green.
- The 11 tests (U3's 9 + U4's 2) **still pass** + the 2 new
  artifact-string tests **pass** (13 tests total in
  `WysiwygEditorTests.cs`).
- The compiled JS (`wwwroot/js/lib/rich-editor.js`) contains the
  10 toolbar buttons' `click` handlers (the Selection / Range API
  splices).
- The 6 pure functions are **untouched** (the
  `RichEditorExports_AreIntact` test still passes).
- The image button reuses the **RC upload lane** (no new route,
  no second upload).
- Handoff note: a `## U5 — toolbar rework` section — (a) the 10
  per-button mappings (B/I/C → `<strong>`/`<em>`/`<code>`;
  H1/H2/H3 → `<h1>`/`<h2>`/`<h3>`; •/1. → `<ul><li>`/`<ol><li>`;
  Link → `<a href="…">`; Image → the RC upload lane + `<img>`),
  (b) the 6 pure functions (untouched), (c) the "after every
  splice" line (`textarea.value = toMarkdown(pane.innerHTML)`),
  (d) the 2 new artifact-string tests (pass status), (e) any
  `tsc` warnings.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the modified TS module + the test
  file (extended) + the rebuilt JS.

## Notes / deviations

- The toolbar's `click` handlers are **reworked** (not re-shaped)
  — the same buttons stay; their **behavior** changes (splice DOM
  into the pane, not Markdown into the textarea).
- The image button **reuses** the RC upload lane (the
  `insert-image.ts` convention) — **no** new route, **no** second
  upload, **no** new server surface.
- The 6 pure functions (RE·1–RE·3) are the **frozen** base — they
  are **untouched** by this unit (the `RichEditorExports_AreIntact`
  test pins them).
