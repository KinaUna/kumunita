# Inline editor (`IE`) — rolling handoff notes

> **The scratch tier** of the IE lane's three-tier contract (the design
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

- **Date:** (U0 stamps the date on first authoring)
- **Register:** `docs/plans-milestones/plan-inline-editor.md` (U0–U5)
- **Design doc (primary):** `docs/design/inline-editor-design.md` (U0 authors)
- **ADR:** `docs/adr/0032-inline-editor-rendered-default-view.md` (U5
  authors; **Amends** 0031)
- **Scope:** the **rendered-by-default** half of the RE split view — the
  rendered preview pane becomes the **default view** (the Markdown source
  `<textarea>` is **hidden** behind one toolbar toggle), on **every**
  existing RE composer surface (the 10 view files / 15 editor blocks
  RE U04–U06 shipped). **Client-only and additive:** one CSS class
  (`.rc-editor-source-hidden`, `display: none`), one
  `<button … data-ie-toggle>` appended to each `.rc-editor-toolbar`, one
  binder extension in `bindRichEditor` (find the button, set the initial
  hidden state, wire the click to toggle, reuse the existing `renderPane`
  on `input`), and two `<kw-l>` keys (`rc.editor.source` = `</>`,
  `rc.editor.showPreview` = `Preview`). **No new dependency in
  `package.json`** (still `typescript`-only), **no new route**, **no new
  server surface**, **no stored field**, **no re-shape of any RE pure
  function** (`renderPreview` / `applyToggle` / `applyBlock` / `applyLink` /
  `imageLink` / `isSafeImageSrc` stay **byte-identical**). The saved body is
  **byte-identical** Markdown the RC read path (`MarkdownRenderer`) already
  renders.
- **Out of scope (named deferrals for a future IE-2 lane, if one comes):**
  **true `contenteditable`** / a third-party editor (hard non-negotiable per
  ADR 0031 D1 — the textarea is the source of truth, the pane is a pure
  render), localStorage persistence of the toggle state, a keyboard shortcut
  for the toggle, caret-mapping between the source and rendered views, and a
  mobile-specific toggle UI. Each is a **future** lane, not an IE re-open.
- **Frozen base (RC + RE, unchanged):** `MarkdownRenderer` · `ContentImageIds`
  · `IMediaStore` · `MediaObject` · the `ImageIds` fields · the
  `GET`/`POST /content-image` routes · the `.rc-body`/`.rc-image`/
  `.rc-editor-*` CSS · `insert-image.ts` · the 6 RE pure functions +
  `bindRichEditor` · RC R·1–R·7 invariants · RE·1–RE·3 invariants. This lane
  adds **one** invariant (IE·1: the source is hidden via a **CSS class**,
  never `disabled`/`hidden`-attributed/removed — the textarea stays the
  live form field the server binds) and a small FACES set (IE1–IE5).

<!-- U0 appends its section below this line. One `##` section per unit, in
     order (U0, U1, … U5). Never rewrite a prior section. -->
