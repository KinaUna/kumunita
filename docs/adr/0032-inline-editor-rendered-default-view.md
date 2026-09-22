# ADR 0032 — Inline editor: the rendered pane is the default view

Status: Accepted
Date: 2026-09-15
Amends: 0031 (D1's *layout* — the "toolbar, source textarea, and preview each
sit on their own full-width row, **all three visible at once**" split view.
The `tsc`-only constraint, D1's no-`contenteditable` / no-HTML-round-trip /
textarea-is-the-source-of-truth stance, D2's toolbar-as-Markdown-splice, D3's
client-preview-mirrors-RC, and the no-second-renderer / no-new-server-surface
consequences all **keep binding**. Does **not** supersede 0031; it changes
which of the two already-shipped rows is the *default* one the resident lands
on, and adds one toggle to switch between them.)

## Context

ADR 0031 (the rich-editor lane, `RE`) shipped the split view: a toolbar row,
a source `<textarea>` row, and a rendered preview row — **all three visible
at once**. That is correct as *architecture*, but the default the resident
lands on is the **raw Markdown**. A resident composing a post sees
`**bold**`, `## Heading`, `![alt](/content-image/…)` first, and the rendered
pane reads as a secondary strip below — a *preview*, not **the** editing
surface. The promise the platform makes is that a resident writes what they
*see*; landing on the markers inverts that.

ADR 0031's own *"Not decided here"* set the floor (a textarea + a live
preview + a toolbar) but did not settle which of the two visible rows is the
**default view**. This ADR settles it. The constraints are unchanged and all
already hold:

- **The `tsc`-only client convention** (ADR 0031) — still no editor
  dependency, still no `.csproj` / `package.json` change.
- **One source of truth** (RE·1) — the `<textarea>` is the form field the
  server binds and the source of the body; it is **never** disabled, removed,
  or its value replaced.
- **Preview↔renderer parity** (RE·2) — the pane is `renderPreview`, byte-
  faithful to `MarkdownRenderer`; this ADR does not change *what* the pane
  renders, only *whether it is the default one seen*.
- **The saved body is byte-identical Markdown** (RC R·1/R·3) — the server
  `ImageIds` parse and `MarkdownRenderer` render are untouched.

## Decision

**D1 — Rendered-by-default, source on demand.** On load (and after a form
validation round-trip re-renders the composer), the source `<textarea>` is
**hidden** (`.rc-editor-source-hidden`, `display: none`) and the rendered
pane is the **visible composition surface**. One toolbar button
(`data-ie-toggle`) reveals the source; the button's `<kw-l>` label swaps
between `rc.editor.source` (`</>`) and `rc.editor.showPreview` (`Preview`) to
say what clicking it *will do*.

When the source is revealed, **the pane stays visible as a live reference — a
split view.** Toggling never removes or hides the rendered pane; it only
toggles the source row. So the two states are: (1) *pane only* (the default —
the resident sees the rendered composition), and (2) *pane + source* (the
split view ADR 0031 already shipped — the resident edits Markdown with the
rendered result beside it). The pane is present in **both**; the source is
the only row that toggles. (This is the "pane stays visible" reading — not a
pane-hides-on-source-reveal state.)

**D2 — One button, one attribute, one binder extension.** The toggle is a
single `<button type="button" class="rc-btn" data-ie-toggle>` appended to the
existing toolbar, wired by a small additive block inside `bindRichEditor`
(`if (toggle) { … }`) in `client/lib/rich-editor.ts`. No new exported
function, no new module, no re-shape of the six RE pure functions
(`renderPreview` / `applyToggle` / `applyBlock` / `applyLink` / `imageLink` /
`isSafeImageSrc`) — all byte-identical. The button splices nothing; it only
toggles the source row's CSS class and swaps its own label.

**D3 — Additive only.** IE changes the *default view* and adds one toggle.
It adds **no** new route, **no** `AccessAction`, **no** stored field, **no**
new dependency, **no** new `.csproj` / `package.json` entry, **no** new
server surface, and **no** second renderer. The only artifacts are: the
additive `bindRichEditor` block, one CSS rule
(`textarea.rc-editor-source-hidden { display: none; }`), the toggle button
on the 10 composer surfaces, and two `<kw-l>` keys.

**The `tsc`-only constraint stands unchanged.** What this ADR changes is the
**default view** of the already-shipped split view. There is still **no
editor dependency in `package.json`**, still **no `.csproj` change**, and the
saved body is **byte-identical** to what a resident could hand-type — RC's
server-side `ContentImageIds` parse and `MarkdownRenderer` render are
untouched.

## Consequences

- **The resident lands on the rendered composition.** The default state is
  pane-only; a resident who does not know Markdown sees what they will
  publish, and the formatting toolbar (RE D2) still splices Markdown into the
  (hidden) source so the rendered result updates live.
- **The source is a toggle away, never lost.** The `<textarea>` remains the
  single source of truth (RE·1): hidden by a CSS class, still bound, still
  submitted, its value unchanged. A resident who prefers raw Markdown clicks
  once and gets the ADR 0031 split view back.
- **No regression surface.** The six RE pure functions and the existing
  toolbar buttons are byte-identical; the only behavioral change is the
  initial `setView(false)` (source hidden) plus the toggle's click handler.
  The Web-test suite pins the artifact: the toggle is present, the
  source-hidden class is present, the textarea is *not* disabled/removed/
  hidden-by-attribute, and all six RE exports still exist.
- **The label swap is a `<kw-l>` `textContent` write** — the button
  carries both labels as `data-ie-label-source` / `data-ie-label-preview`
  data attributes, resolved server-side by the shared `_RichEditorToggle`
  partial (the `ITranslationProvider.GetManyAsync` path, en floor — the
  same mechanism the `<kw-l>` TagHelper uses). The `<kw-l>` element's
  `textContent` is the load-bearing live path; the `<kw-l>` `key`
  attribute is also swapped (the a11y / HTML-source pin). If the
  `data-*` attributes are absent (a legacy button), the binder falls
  back to the known en-floor values (`</>` / `Preview`).
- **The dead `rc.editor.preview` key is removed** — RE (ADR 0031)
  registered it but never emitted it through `<kw-l>` in any view. IE's
  `rc.editor.showPreview` is the canonical "show preview" label.
  (2026-09-15, post-close amendment.)

## Not decided here (explicit non-decisions)

Each is a **future lane**, named — the ADR 0011 / 0025 / 0031 precedent holds:

- **localStorage persistence** — remember the resident's view preference
  (source vs. pane) across sessions. A `localStorage` read on init + write
  on toggle; a one-liner once the toggle is in.
- **Keyboard shortcut** — a key binding (e.g. `Ctrl+Shift+E`) for the
  toggle, discoverable via a tooltip or a `<kbd>` in the button's title.
- **Caret mapping between views** — when the resident toggles from source
  to pane, the caret/selection could map to the corresponding rendered
  position (and vice versa). A power feature, not a floor; requires a
  source-position → DOM-position mapping algorithm.
- **Mobile-specific toggle UI** — a thumb-reachable toggle on small screens
  (a floating action button, a bottom-sheet, or a sticky-positioned toggle).
  A responsive CSS + JS concern.
- **True `contenteditable` / third-party editor** (ProseMirror/Quill/Tiptap)
  — **hard non-negotiable** out (D1, ADR 0031): it breaks `tsc`-only + the
  no-HTML-round-trip stance + RC's `ImageIds` source parse. Not a deferral;
  a separate-architecture decision that would supersede ADR 0031.

## Revisit when

- A resident asks to *remember* their view preference, or for a keyboard
  shortcut / caret mapping — open the named IE-2 lane (a design doc + ADR),
  not an amendment to this ADR.
- The **`tsc`-only / no-editor-dependency** stance is challenged — that is the
  load-bearing constraint of ADR 0031's D1; relaxing it is itself a new ADR,
  not a code change.
- The pane-only default proves *too* much of a shift (a resident repeatedly
  can't find the source) — that is a UX tuning of the default, revisitable
  without touching the source-of-truth (RE·1) or the renderer (RC R·1).
