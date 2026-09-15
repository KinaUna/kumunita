# Inline editor (`IE`) — the rendered view is the default editor; the Markdown source is hidden behind a toggle

> **Three-tier contract.** This file is the **primary** tier of the IE lane:
> it pins the invariant (IE·1), the FACES (IE1–IE5), the exact toggle
> contract, the CSS pin, the Razor button pin, the two `<kw-l>` keys, the
> pinned seam-test names, the acceptance gate, and the drift guard. The
> register (`docs/plans-milestones/plan-inline-editor.md`) is the
> **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md` is the
> **scratch** tier (one short section per unit, appended, never rewritten).
> When the three disagree, **this file wins for the pinned shapes**; the
> register wins for *which files exist* and *what each unit does*.
>
> **The frozen base.** This lane is built **on top of** the rich editor
> lane (`RE`, ADR 0031) and the rich-content lane (`RC`, ADR 0025). RC's
> **R·1–R·7** and RE's **RE·1–RE·3** all still bind **unchanged**
> (re-anchored in §Frozen base below). IE adds **no** re-shape of
> `MarkdownRenderer`, `ContentImageIds`, `IMediaStore`, the `ImageIds`
> fields, the `/content-image` routes, the `.rc-body` / `.rc-image` /
> `.rc-editor-*` CSS, `insert-image.ts`, or any of the 6 RE pure
> functions + `bindRichEditor`. It is **client-only and additive**: one
> CSS rule, one `<button>` per toolbar, one binder extension in
> `bindRichEditor`, and two `<kw-l>` keys. **No** `.csproj` change, **no**
> `package.json` change (still `typescript` only), **no** editor
> dependency.

## Value chain

RC set out to deliver *"a resident can bold a heading, start a list, or
show a photo"* (RC Scope, R1). RC delivered the **read** half:
`MarkdownRenderer` renders Markdown on every read surface, and `Body` is
stored as a Markdown `string` (RC R·7, zero migrations). RE shipped the
**write** half's *visibility*: a split view with the toolbar row, the
source `<textarea>` row, and the rendered preview row — **all three
visible at once** (RE·1, RE·2). A resident sees the rendered output while
composing, but the raw `**bold**` markers still sit in the middle of the
composition experience (the source `<textarea>` is the first thing their
eyes land on).

This lane finishes the job from the resident's point of view:

- **The rendered pane becomes the default view** — a resident opens a
  composer and sees *what they will publish* filling the surface, not a
  wall of `**` and `#` (D1).
- **The Markdown source is one button-press away** — a toolbar toggle
  reveals the source for power users, and the toolbar's formatting buttons
  still write Markdown into it, transparently (D2).

The value chain moves one arrow: from *"seeing the rendered output is a
nice-to-have preview pane"* to **"the rendered output IS the editing
surface; the Markdown is a power-user view, not the starting point."**
The saved body is **byte-identical** to what a resident could have
hand-typed — RC's server-side `ContentImageIds` parse and
`MarkdownRenderer` render are **unchanged** (RE·3, RC R·3).

## Invariant (pinned for the IE lane)

One invariant, **IE·1**. The RC R·1–R·7 and RE·1–RE·3 are **not**
re-stated here — they are re-anchored unchanged in §Frozen base and keep
binding in their own number spaces.

| # | Invariant | Pinned where |
|---|-----------|--------------|
| **IE·1** | **The rendered pane is the default view; the Markdown source is hidden behind the toggle.** On load (and after a form validation round-trip), the source `<textarea>` is **hidden** via the CSS class `rc-editor-source-hidden` (`display: none`) and the rendered pane is the visible composition surface. The toggle button reveals the source and hides the pane; clicking it again restores the default. Hiding is a **CSS class** — never `disabled` (which would break form binding on submit) and never the `hidden` attribute (which would remove it from tab order + the form-submit set). The textarea is still the **single source of truth** (RE·1 unchanged); the toggle is a **visibility control** over that single source, not a mode switch with two editing engines. The toolbar's splice buttons still act on the (possibly hidden) textarea (RE·1 unchanged); the preview is still a pure render of the textarea (RE·2 unchanged). | **U1** (binder + CSS) · **U2** (the button) · **U4** (the tests) |

## FACES (pinned, 5)

Five resident-facing scenarios, **IE1–IE5**, each exercising IE·1.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| **IE1** | A resident opens a composer and sees the **rendered pane** as the main surface — the raw `**bold**` markers are **not** the first thing they see. The source `<textarea>` is hidden (`.rc-editor-source-hidden`, `display: none`). The toolbar is visible above the pane. | IE·1 |
| **IE2** | The resident clicks the toggle button (the far-right toolbar button, labeled `</>`) → the source `<textarea>` becomes visible (the class is removed). The pane stays visible as a live reference (a split view — the resident sees both the raw source and the rendered output side by side). The resident can now hand-type Markdown or use the toolbar's splice buttons on the visible source. The button's label changes to "Preview". | IE·1 |
| **IE3** | The resident clicks the toggle again → the pane is visible again, the source hidden, the preview in sync with the current value. The button's label is back to `</>`. | IE·1 |
| **IE4** | The toggle is present on **every** editor, image-gated or not — it is a view control, not a content control. The image button's `data-rich-editor-no-image` gating is **unaffected** (the toggle is appended after the last existing button, whatever it is). | IE·1 |
| **IE5** | The saved body is **byte-identical** to what the resident could have hand-typed in the source view — RC R·3 + RE·1 unchanged; the server parse (`ContentImageIds`) and render (`MarkdownRenderer`) are untouched. The toggle changes **which view is default**, not **what is saved**. | IE·1, RE·1, RC R·3 |

## Pinned contract (the new artifacts — additive only)

Every unit matches these shapes verbatim. A mismatch is a `## U<m> —
Drift pause`, not a silent edit.

### The binder extension (U1) — `client/lib/rich-editor.ts`

`tsc`-only (RE·3, unchanged). **Extends** `bindRichEditor` (does **not**
replace). The 6 pure functions (`renderPreview` / `applyToggle` /
`applyBlock` / `applyLink` / `imageLink` / `isSafeImageSrc`) and the
existing binder wiring (toolbar `data-md` buttons, the
`data-rich-editor-no-image` image-button removal, the `input`-driven
`renderPane`, the self-wire loop) are all **byte-identical** in the diff.
The extension adds, **after** the existing toolbar/pane/textarea lookups
and **before** the button-wiring loop:

```ts
// IE·1, D1/D2 — the view toggle (additive; the existing wiring is
// untouched). Finds the data-ie-toggle button, sets the initial
// state (source hidden, pane visible), and wires the click to
// toggle the source visibility + swap the button's label.
const toggle = root.querySelector<HTMLButtonElement>('button[data-ie-toggle]');
if (toggle) {
  const srcHidden = 'rc-editor-source-hidden';
  const paneActive = 'rc-editor-pane-active';
  const setView = (showSource: boolean): void => {
    textarea.classList.toggle(srcHidden, !showSource);
    if (previewPane) previewPane.classList.toggle(paneActive, showSource);
    // Label swap: the <kw-l> is server-rendered (LocalizeTagHelper);
    // a client-side key attribute change does NOT live-update the label.
    // The key swap is the pin (HTML-source correctness + a11y); the
    // lastChild.textContent swap is the load-bearing path.
    const kw = toggle.querySelector('kw-l');
    if (kw) kw.setAttribute('key', showSource ? 'rc.editor.showPreview' : 'rc.editor.source');
    toggle.lastChild && (toggle.lastChild.textContent = showSource ? 'Preview' : '</>');
  };
  setView(false); // IE·1: the rendered pane is the default view (source hidden).
  toggle.addEventListener('click', () => {
    setView(!textarea.classList.contains(srcHidden));
  });
}
```

**Key points:**
- **No** pure function is touched. **No** new `export` is added.
- The self-wire loop is **unchanged** — it already calls `bindRichEditor`,
  which now also wires the toggle.
- The `renderPane` function is **reused verbatim** — the `input` listener
  is **not** re-wired.
- The toggle is **optional** at the module level: if
  `button[data-ie-toggle]` is absent (a view that hasn't been updated yet),
  the binder returns normally (no error, no change to existing behavior).
- The **label swap** mechanism: the `<kw-l>` is server-rendered and a
  `key` attribute change does NOT live-update the label. The load-bearing
  path is setting the button's `textContent` directly. The
  `data-ie-preview-label` attribute carries the "show preview" label
  (the second label) since the initial `<kw-l>` carries the "show source"
  label. See §The exact toggle contract for the full mechanism.

### The CSS rule (U1) — `wwwroot/css/site.css`

**Append** one rule to the existing `.rc-editor` block (do **not**
re-order or re-style the existing rules):

```css
.rc-editor textarea.rc-editor-source-hidden { display: none; }
```

This is the **load-bearing** rule. An optional companion marker rule:

```css
.rc-editor-pane.rc-editor-pane-active { /* no new styling — placeholder */ }
```

The second rule is **optional** (a no-op marker for future per-state
styling); U1 adds it only if this doc pins it. **This doc does not pin
it**, so U1 adds **only the first rule**.

### The Razor button (U2) — appended to each `.rc-editor-toolbar`

The **exact** markup (the pin, verbatim):

```html
<button type="button" class="rc-btn" data-ie-toggle>
  <kw-l key="rc.editor.source">&lt;/&gt;</kw-l>
</button>
```

- **Appended** after the last existing `data-md` button in each toolbar
  (the image button, or the link button on image-gated surfaces).
- **Never** re-ordering the existing buttons.
- **No** other markup change in the view files (the textarea, the pane,
  the label, the form — all unchanged).

### The two `<kw-l>` keys (U3) — `KnownTranslationKeys.cs`

**Append** the two keys to the existing `rc.editor.*` block (do **not**
re-order the existing keys):

```csharp
["rc.editor.source"]      = "</>",   // the toggle button's label while the source is hidden
["rc.editor.showPreview"] = "Preview", // the toggle button's label while the source is visible
```

Update the closed-set comment (currently at line ~630, naming the RE set)
to append `+ rc.editor.source + rc.editor.showPreview`.

## The exact toggle contract

| Aspect | Pinned behavior |
|--------|----------------|
| **Initial state (on load)** | `setView(false)` is called on init: the textarea has class `rc-editor-source-hidden` (`display: none`), the pane does **not** have `rc-editor-pane-active`. The toggle button's label is the `<kw-l>`'s server-rendered text (the "show source" label, `</>` by default). |
| **Click → show source** | `setView(true)`: remove `rc-editor-source-hidden` from the textarea, add `rc-editor-pane-active` to the pane. Set the `<kw-l>`'s `key` to `rc.editor.showPreview` + set the button's `lastChild.textContent` to `"Preview"`. |
| **Click → show pane** | `setView(false)`: add `rc-editor-source-hidden` to the textarea, remove `rc-editor-pane-active` from the pane. Set the `<kw-l>`'s `key` to `rc.editor.source` + set the button's `lastChild.textContent` to `"</>"`. |
| **Label swap mechanism** | The `<kw-l>` is **server-rendered** (LocalizeTagHelper). A client-side `key` attribute change does **not** live-update the label. The `key` swap is the pin (HTML-source correctness + a11y); the `<kw-l>` element's `textContent` swap is the **load-bearing** live path (the button may have whitespace text nodes around the `<kw-l>`, so target the element directly, not `lastChild`). The two labels are hardcoded in the binder: `"</>"` (show source) and `"Preview"` (show preview). |
| **`renderPane` reuse** | The existing `textarea.addEventListener('input', renderPane)` wiring is **untouched**. The binder's `renderPane` function is **reused verbatim** (not re-wired, not re-created). The pane's visibility is **not** controlled by the toggle — it stays visible in both states (the split-view behavior, IE2). The `rc-editor-pane-active` class is toggled as a no-op marker for future per-state styling; it has no CSS rule in this lane. |
| **Toggle absent** | If `button[data-ie-toggle]` is not found in the root, the binder **skips the toggle wiring** (the `if (toggle)` guard). The editor works exactly as it did before IE (split view, all three rows visible). |

## Pinned seam tests (3 + 1 artifact pin)

All in `tests/Kumunita.Web.Tests/InlineEditorTests.cs` (new, U4).
**Harness reality (RE·3's `tsc`-only + no TS test runner):** the repo has
no `vitest`/`jest`/`node --test`. U4 implements the 3 behaviors as a
**C# spec mirror** of the toggle's artifact-level contract (the pin is
the *contract*: the strings are present/absent in the compiled JS) + one
artifact pin asserting the RE surface is byte-identical.

1. `CompiledRichEditorJs_ContainsToggleButton` — the compiled
   `wwwroot/js/lib/rich-editor.js` contains the string `data-ie-toggle`
   (the button's attribute is in the compiled artifact — the U2 markup +
   the U1 binder wiring are both present).
2. `CompiledRichEditorJs_ContainsSourceHiddenClass` — the compiled JS
   contains the string `rc-editor-source-hidden` (the initial-state class
   is emitted — the U1 CSS rule + the binder's initial-state line are both
   present).
3. `RichEditorTextarea_IsNotDisabled_OrRemoved` — the compiled JS does
   **not** contain `textarea.disabled = true` or `textarea.remove()`
   (the RE·1 single-source-of-truth pin at the artifact level — the
   textarea is hidden via a CSS class, never disabled or removed).
4. `CompiledRichEditorJs_StillExportsRePureFunctions` — the compiled JS
   still contains the 5 RE pure function names (`renderPreview`,
   `applyToggle`, `applyBlock`, `applyLink`, `imageLink`) +
   `isSafeImageSrc` + `bindRichEditor` (guards RE·3: the IE lane is
   additive only — the RE surface is byte-identical).

**Runner (per AGENTS.md):** `dotnet build Kumunita.slnx -c Debug` green,
`npm run build` green (in `src/Kumunita.Web`), then `dotnet exec` on
`Kumunita.Web.Tests.dll` and `Kumunita.Core.Tests.dll` (**not**
`dotnet test` / VS Test Explorer — the xunit.v3 discovery quirk on this
machine).

## Acceptance gate (U4 records)

- `dotnet build Kumunita.slnx -c Debug` **green**.
- `npm run build` (in `src/Kumunita.Web`) **green** (the `tsc` compile of
  the extended module; the JS artifact the tests read is fresh).
- `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  **green** (the 3 new IE tests + the artifact pin + the 10 existing RE
  tests + the existing RC/M3/etc. tests — no regression).
- `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  **green** (the U3 `KnownTranslationKeys.cs` change — the Core suite
  passes — no regression).

## Drift guard

The IE·1 invariant, the 5 FACES (IE1–IE5), the binder extension contract
(the `data-ie-toggle` selector + the initial-state line + the click
handler + the `renderPane` reuse), the CSS class
(`rc-editor-source-hidden`), the Razor button markup (the `data-ie-toggle`
+ `data-ie-preview-label` attributes + the `<kw-l>` key), the two `<kw-l>`
key names + values (`rc.editor.source` = `</>`, `rc.editor.showPreview` =
`Preview`), the 3 seam-test names + the artifact pin, and the 10
view-instance file list (16 blocks) are **frozen** once this file is
written. Any mismatch found by a later unit is a `## U<m> — Drift pause`
section in the handoff note (unit-series rule 6), **not** a silent edit.

## Named deferrals (a future IE-2 lane, not a renumber)

Each is a **future lane**, named, not a deferral to be quietly dropped
(the ADR 0011 / 0025 / 0031 precedent):

- **localStorage persistence** — remember the resident's view preference
  (source vs. pane) across sessions. A `localStorage` read on init +
  write on toggle. A one-liner once the toggle is in.
- **Keyboard shortcut** — a key binding (e.g. `Ctrl+Shift+E`) for the
  toggle, discoverable via a tooltip or a `<kbd>` in the button's title.
- **Caret mapping between views** — when the resident toggles from source
  to pane, the caret/selection in the source view could map to the
  corresponding rendered position (and vice versa). A power feature, not
  a floor; requires a source-position → DOM-position mapping algorithm.
- **Mobile-specific toggle UI** — a thumb-reachable toggle on small
  screens (a floating action button, a bottom-sheet, or a
  sticky-positioned toggle). A responsive CSS + JS concern.
- **True `contenteditable` / third-party editor** (ProseMirror/Quill/
  Tiptap) — **hard non-negotiable** out (D1, ADR 0031): it breaks
  `tsc`-only + the no-HTML-round-trip stance + RC's `ImageIds` source
  parse. **Not a deferral**; a separate-architecture decision that would
  supersede ADR 0031.

## Frozen base (re-anchored unchanged)

**RC R·1–R·7** (one renderer, escape-first, source-is-the-`string`,
`ImageIds` server parse, the `/content-image` routes, the `.rc-body` /
`.rc-image` CSS) + **RE·1–RE·3** (one source of truth = the textarea,
preview↔renderer parity, no new dependency / no new server surface).
IE adds **no** re-shape of any of them. The `rich-editor.ts` pure
functions are byte-identical; the 10 view instances' existing buttons are
byte-identical; the `rc.editor.*` key set is the RE set + the 2 IE keys;
the CSS is the RE block + the 1 IE rule; `package.json` is still
`typescript`-only.
