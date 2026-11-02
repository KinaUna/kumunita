# U7 — The code view rework (the `</>` toggle reveals the read-only mirror)

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U7 (of U0–U9)
- **Kind:** code (binder wiring — the `data-ie-toggle` button's click handler)

## Goal

Rework the `data-ie-toggle` button's click handler (§2.5) so the
code view is a **read-only mirror** of the pane (the WY·7
invariant: the textarea is revealed as a read-only mirror; the
pane stays editable + visible; the label swap is kept). Add the **1**
artifact-string test (`WY7_CodeViewIsReadOnlyMirror`).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/wysiwyg-editor-design.md` §2.5 (the binder
   contract — the `data-ie-toggle` button's click handler — the
   **primary** source) + §2.2 (the DOM contract — the textarea is
   the read-only sink) + §2.7 (the test names — the 1 test this
   unit authors).
2. `src/Kumunita.Web/client/lib/rich-editor.ts` (the full module
   — the IE toggle block — the wiring to rework).
3. `src/Kumunita.Web/Views/Shared/_RichEditorToggle.cshtml` (the
   toggle button's markup — the `data-ie-toggle` attribute + the
   `data-ie-label-source` / `data-ie-label-preview` data attributes
   + the `<kw-l>` element — the label swap is kept).
4. `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs` (U3's 9
   pure-function tests + U4's 2 + U5's 2 + U6's 1 — the 14 tests
   that must still pass after U7's change).
5. `tests/Kumunita.Web.Tests/InlineEditorTests.cs` (the
   artifact-string pin pattern — the test shape the 1 test
   follows).

## Deliverables (2 files)

1. **`src/Kumunita.Web/client/lib/rich-editor.ts`** — the
   `data-ie-toggle` button's click handler (the WY·7 invariant).
   The **existing** IE toggle block (the `setView` function + the
   `rc-editor-source-hidden` class + the `rc-editor-pane-active`
   marker + the label swap) is **reworked**: the `setView`
   function's semantics change from "toggle the source's
   visibility" to "toggle the code view's visibility". The
   **two states** (the WY·7 invariant):
   - (1) **pane only** (the default — the resident sees the
     editable pane; the textarea is hidden by the
     `rc-editor-source-hidden` class — **unchanged** from IE);
   - (2) **pane + code view** (the resident sees the editable
     pane + the **read-only** textarea — the textarea is revealed
     by removing the `rc-editor-source-hidden` class; the textarea
     is **read-only** — the `textarea.readOnly = true` property is
     set by the binder (the textarea is **not** editable — the
     WY·2 invariant: the textarea is the read-only sink, not the
     editing surface)).
   The **label swap** (the `rc.editor.source` / `rc.editor.
   showPreview` keys) is **kept** (the `_RichEditorToggle`
   partial resolves them server-side; the binder swaps the
   `<kw-l>`'s `textContent` — **unchanged** from IE). The **pane
   stays editable + visible** in **both** states (the WY·1
   invariant — the pane is the editing surface; the code view is a
   **mirror**, not a **mode switch**).
2. **`tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`** (extend —
   add **1** test):
   - `WY7_CodeViewIsReadOnlyMirror` — assert the compiled JS
     contains the string `readOnly` (the WY·7 invariant: the
     textarea is the read-only mirror) + the string
     `rc-editor-source-hidden` (the IE·1 frozen base: the
     source-hidden class is still present).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `cd src/Kumunita.Web; npm run build` (tsc) green.
- The 14 tests (U3's 9 + U4's 2 + U5's 2 + U6's 1) **still pass**
  + the 1 new test **passes** (15 tests total in
  `WysiwygEditorTests.cs`).
- The compiled JS (`wwwroot/js/lib/rich-editor.js`) contains the
  `data-ie-toggle` button's click handler (the `setView`
  function's reworked semantics).
- The `_RichEditorToggle.cshtml` partial is **unchanged** (the
  label swap is kept).
- The textarea is **read-only** (`textarea.readOnly = true` — the
  WY·2 invariant).
- Handoff note: a `## U7 — code view rework` section — (a) the
  `setView` function's reworked semantics (the two states — pane
  only / pane + code view), (b) the label swap (kept — the
  `_RichEditorToggle` partial is unchanged), (c) the textarea's
  `readOnly` property (set by the binder — the WY·2 invariant),
  (d) the 1 new test (pass status), (e) any `tsc` warnings.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the modified TS module + the test file
  (extended) + the rebuilt JS.

## Notes / deviations

- The `data-ie-toggle` button is **reworked** (not re-shaped) —
  the same button stays; its **semantics** change (the code view
  is now a **read-only mirror**, not an **editable source**).
- The **pane stays editable + visible** in **both** states (the
  WY·1 invariant — the pane is the editing surface; the code view
  is a **mirror**, not a **mode switch**).
- The **label swap** is **kept** (the `_RichEditorToggle` partial
  resolves the labels server-side; the binder swaps the `<kw-l>`'s
  `textContent` — **unchanged** from IE).
