# U6 — The sanitizer + the `paste` handler (WY·6)

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U6 (of U0–U9)
- **Kind:** code (binder wiring — replace U4's paste stub with the real insert)

## Goal

Replace U4's `paste` handler **stub** with the **real** sanitizer
+ insert (the WY·6 invariant: paste / raw HTML is sanitized to the
WY·3 subset before insertion). The sanitizer (`sanitizeHtml`) is
already present (U3); U6 wires it into the `paste` handler +
installs the insert (the `range.insertNode(fragment)` idiom) + the
XSS-protection (the escape-first / `isSafeImageSrc` / `isSafeUrl`
semantics already in `rich-editor.ts` are reused). Add the **1**
artifact-string test
(`CompiledRichEditorJs_ContainsSanitizer`).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/wysiwyg-editor-design.md` §2.4 (the sanitizer
   contract — the **primary** source: the reject list, the
   construction, the output) + §2.5 (the binder contract — the
   `paste` handler) + §2.7 (the artifact-string test names — the
   1 test this unit authors).
2. `src/Kumunita.Web/client/lib/dom-to-markdown.ts` (U3's
   sanitizer — the `sanitizeHtml` function to wire in).
3. `src/Kumunita.Web/client/lib/rich-editor.ts` (U4's `paste`
   handler stub — the wiring to **replace** with the real insert
   logic; the `htmlEscape` / `isSafeImageSrc` / `isSafeUrl`
   functions — the construction to reuse).
4. `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs` (U3's 9
   pure-function tests + U4's 2 + U5's 2 — the 13 tests that must
   still pass after U6's change).
5. `tests/Kumunita.Web.Tests/InlineEditorTests.cs` (the
   artifact-string pin pattern — the `CompiledRichEditorJs_*`
   test shape the 1 test follows).

## Deliverables (2 files)

1. **`src/Kumunita.Web/client/lib/rich-editor.ts`** — the `paste`
   handler (the WY·6 invariant). The **stub** from U4 is replaced
   with the **real** handler:
   - (a) intercepts the `paste` event on the pane
     (`pane.addEventListener('paste', (e) => { … })`);
   - (b) reads the clipboard HTML
     (`e.clipboardData.getData('text/html')`);
   - (c) **sanitizes** it (`sanitizeHtml(html)` — the WY·3 subset
     only);
   - (d) **inserts** the sanitized HTML into the pane at the
     selection (the `range.insertNode(fragment)` idiom — the
     sanitized HTML is parsed into a `DocumentFragment` via
     `DOMParser` + the fragment's children are inserted);
   - (e) **prevents** the default paste
     (`e.preventDefault()` — the browser's native paste is
     suppressed; the sanitized insert is the only path);
   - (f) **keeps** the textarea in sync
     (`textarea.value = toMarkdown(pane.innerHTML)`).
   **The XSS protection** (the WY·6 invariant): the sanitizer
   strips every `on*` event-handler attribute, every `style`
   attribute, every `class` attribute (except `language-{lang}` on
   `<code>`), every `id` attribute, every `href` that is not a safe
   url (the `isSafeUrl` reject), every `src` that is not a safe
   image src (the `isSafeImageSrc` reject), and every element
   outside the WY·3 subset (`<script>`, `<iframe>`, `<object>`,
   `<embed>`, `<form>`, `<input>`, `<button>`, `<table>`, `<tr>`,
   `<td>`, `<div>`, `<span style=…>`). The **construction** is a
   **single pass** over the HTML (a regex-based strip, the same
   construction as `htmlEscape` / `isSafeImageSrc` / `isSafeUrl`
   already in `rich-editor.ts`).
2. **`tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`** (extend —
   add **1** artifact-string test):
   - `CompiledRichEditorJs_ContainsSanitizer` — assert the
     compiled JS contains the string `sanitizeHtml` (the WY·6
     invariant: the sanitizer is wired into the `paste` handler).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `cd src/Kumunita.Web; npm run build` (tsc) green.
- The 13 tests (U3's 9 + U4's 2 + U5's 2) **still pass** + the 1
  new artifact-string test **passes** (14 tests total in
  `WysiwygEditorTests.cs`).
- The compiled JS (`wwwroot/js/lib/rich-editor.js`) contains the
  `paste` handler (the `sanitizeHtml` call + the `range.
  insertNode` insert + the `e.preventDefault()`).
- The 3 `WY6_Sanitizer_*` tests (U3's pure-function tests) still
  pass (the sanitizer is unchanged — U6 only wires it into the
  `paste` handler).
- Handoff note: a `## U6 — sanitizer + paste handler` section —
  (a) the 6 sub-steps (a)–(f) (the `paste` handler's internals),
  (b) the XSS protection (the reject list — the elements/attributes
  the sanitizer strips), (c) the 3 `WY6_Sanitizer_*` tests (still
  passing), (d) the 1 new artifact-string test (pass status),
  (e) any `tsc` warnings.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the modified TS module + the test file
  (extended) + the rebuilt JS.

## Notes / deviations

- The `paste` handler is the **only** place in U6 that touches the
  `paste` event. U4's stub (the `e.preventDefault()` + the
  `sanitizeHtml` call) is **replaced** with the full handler (the
  insert logic + the `textarea` sync).
- The sanitizer itself (`sanitizeHtml`) is **untouched** by U6
  (U3 authored it; U6 only **wires** it into the `paste` handler).
- The XSS-protection is the **load-bearing** part of U6 — the
  reject list (the elements/attributes the sanitizer strips) is
  the security floor for the `contenteditable` pane.
