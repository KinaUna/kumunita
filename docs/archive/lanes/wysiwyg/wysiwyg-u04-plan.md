# U4 — The editing loop (the pane becomes `contenteditable`; the binder keeps the textarea in sync)

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U4 (of U0–U9)
- **Kind:** code (binder wiring + one CSS rule + 2 artifact-string tests)

## Goal

Extend `bindRichEditor` in `client/lib/rich-editor.ts` with the **WY
block** (§2.5): the pane becomes `contenteditable`; the binder
populates the pane from the textarea on load; the binder keeps the
textarea in sync on every pane `input`. Install the **`paste`
handler stub** (U6 owns the internals). Add the **one** new CSS
rule (§2.6). Add the **2** artifact-string tests
(`CompiledRichEditorJs_ContainsContentEditable` +
`CompiledRichEditorJs_ContainsToMarkdown`). **The existing RE/IE
wiring is untouched** — the WY block is **additive** (the `if (pane)
{ … }` guard).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/wysiwyg-editor-design.md` §2.2 (the DOM contract
   — the pane's initial state + the `input` handler + the `paste`
   stub) + §2.5 (the binder contract — the **primary** source) +
   §2.6 (the CSS contract — the one new rule) + §2.7 (the
   artifact-string test names — the 2 tests this unit authors).
2. `src/Kumunita.Web/client/lib/rich-editor.ts` (the full module
   — the `bindRichEditor` function + the IE toggle block + the
   `renderPreview` function — the wiring to extend).
3. `src/Kumunita.Web/client/lib/dom-to-markdown.ts` (U3's
   serializer + sanitizer — the two pure functions to import).
4. `src/Kumunita.Web/Views/Announcement/New.cshtml` (one of the
   10 composer surfaces — the markup the binder must work with:
   the `.rc-editor` wrapper, the toolbar, the textarea, the pane,
   the `data-ie-toggle` button).
5. `tests/Kumunita.Web.Tests/InlineEditorTests.cs` (the
   artifact-string pin pattern — the `CompiledRichEditorJs_*`
   test shape the 2 tests follow).

## Deliverables (2 files)

1. **`src/Kumunita.Web/client/lib/rich-editor.ts`** — the **WY
   block** (§2.5) in `bindRichEditor`. The existing RE/IE wiring
   (the 6 pure functions + `renderPreview` + the IE toggle block
   + the image upload lane) is **untouched** — the WY block is
   **additive** (the `if (pane) { … }` guard). The WY block:
   - (a) imports `toMarkdown` + `sanitizeHtml` from
     `./dom-to-markdown.js`;
   - (b) sets `pane.contentEditable = 'true'`;
   - (c) sets `pane.innerHTML = renderPreview(textarea.value)`
     (the initial population);
   - (d) installs the `input` handler
     (`textarea.value = toMarkdown(pane.innerHTML)`);
   - (e) installs the `paste` handler **stub**
     (`pane.addEventListener('paste', (e) => { e.preventDefault();
     const html = e.clipboardData?.getData('text/html') ??
     e.clipboardData?.getData('text/plain') ?? ''; const clean =
     sanitizeHtml(html); /* insert — U6 owns this */ })` — the
     stub calls `sanitizeHtml` + **does not** insert (U6 owns
     the insert);
   - (f) **does not** rework the toolbar's `click` handlers (that
     is **U5**);
   - (g) **does not** rework the `data-ie-toggle` button's click
     handler (that is **U7**).
2. **`src/Kumunita.Web/wwwroot/css/site.css`** — the **one** new
   CSS rule (§2.6):
   ```css
   .rc-editor-pane[contenteditable="true"] {
     cursor: text;
     outline: 2px solid var(--bs-primary, #0d6efd);
     outline-offset: 1px;
   }
   ```
   The existing `.rc-editor-pane` rule is **untouched** (the a11y
   focus ring is **added**, not re-shaped).
3. **`tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`** (extend —
   add **2** artifact-string tests):
   - `CompiledRichEditorJs_ContainsContentEditable` — assert the
     compiled `wwwroot/js/lib/rich-editor.js` contains the string
     `contentEditable` (the WY·1 invariant: the pane is the
     editing surface).
   - `CompiledRichEditorJs_ContainsToMarkdown` — assert the
     compiled JS contains the string `toMarkdown` (the WY·3 / WY·5
     invariant: the serializer is imported + called).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `cd src/Kumunita.Web; npm run build` (tsc) green.
- The 9 pure-function tests (U3) **still pass** + the 2 new
  artifact-string tests **pass** (11 tests total in
  `WysiwygEditorTests.cs`).
- The compiled JS (`wwwroot/js/lib/rich-editor.js`) contains
  `contentEditable` + the `input` handler + the `paste` handler
  stub.
- The 10 composer surfaces are **unchanged** in shape (the pane
  gains `contenteditable="true"` at runtime, **not** in the Razor).
- Handoff note: a `## U4 — editing loop` section — (a) the WY
  block's 6 sub-steps (a)–(f) (the paste handler is a **stub** in
  U4; U6 owns the insert), (b) the one new CSS rule (the a11y
  focus ring), (c) the 10 composer surfaces (unchanged in shape),
  (d) the 2 new artifact-string tests (pass status), (e) any
  `tsc` warnings.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the 2 modified files (the TS module +
  the CSS) + the test file (extended) + the rebuilt JS.

## Notes / deviations

- The **paste handler stub** in U4 is a **placeholder** — it calls
  `sanitizeHtml` + **does not** insert. U6 replaces the stub with
  the real insert (the `range.insertNode(fragment)` idiom).
- The WY block is **additive** (the `if (pane) { … }` guard) — the
  existing RE/IE wiring is **untouched**.
- The `paste` stub is the **only** place in U4 that touches the
  `paste` event. U6 will **replace** the stub's body (the insert
  logic) — the event listener itself stays.
