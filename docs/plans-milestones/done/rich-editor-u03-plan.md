# U3 — The shared editor module (`rich-editor.ts`) + the `.rc-editor-*` CSS

- **Lane:** Rich editor (`RE`)
- **Unit:** U3 (of U0–U8)
- **Kind:** code (the **only** unit in the lane that writes TS)

## Goal

Implement the **one** new artifact this lane adds to the client: the shared
`client/lib/rich-editor.ts` module (the pure functions `renderPreview` /
`applyToggle` / `applyBlock` / `applyLink` / `imageLink` + the `bindRichEditor`
binder) and the `.rc-editor-*` CSS block in `site.css`. This is the **only**
new code in the lane (RE·3) — every later unit wires **this** module, so its
exports must be correct + unit-testable (RE·1/RE·2).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/plans-milestones/in-progress/rich-editor-handoff-notes.md` — **the
   `## U2 — Mirror checklist (pinned)` section** (the marker ceiling + the
   client `IsSafeImageSrc` mirror predicate + the CSS classes + the
   escape-first stance — implement *against* this pin, not against the full
   renderer).
2. `src/Kumunita.Web/client/lib/insert-image.ts` — the **module + self-wire +
   cursor-splice** conventions to mirror (the `selectionStart`/`selectionEnd`
   re-focus idiom the `apply*` splices use; the `type="module"` self-wire at
   load).
3. `src/Kumunita.Web/client/lib/api.ts` — `apiFetch` + the CSRF header (the
   image button reuses the RC upload through `apiFetch`; **no** new `api.ts`
   method).
4. `src/Kumunita.Web/wwwroot/css/site.css` — the `.rc-body` / `.rc-image`
   block (~lines 733–790) the `.rc-editor-pane` reuses; the home for the new
   `.rc-editor-*` block.
5. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — **consult only** for the
   marker→HTML mapping `renderPreview` mirrors (the U2 checklist is the
   primary pin; the renderer is the reference of record).

## Deliverables (closed set — 2 files)

1. **`src/Kumunita.Web/client/lib/rich-editor.ts`** (new) — a `tsc`-only ES
   module, self-wiring at load (the `insert-image.ts` pattern), exporting
   **exactly** the register's pinned contract:
   - `renderPreview(markdown: string): string` — **RE·2/D3**: escapes first,
     then maps the **RC-pinned subset** (bold `**`→`<strong>`, italic `*`→
     `<em>`, inline code `` ` ``→`<code>`, `#`–`###`→`<h1>`–`<h3>`, `- `/`* `
     →`<ul><li>`, `1.`→`<ol><li>`, `> `→`<blockquote>`, `[label](url)`→`<a>`,
     `![alt](/content-image/{hex})`→`<img class="rc-image">` **only if** the
     client `IsSafeImageSrc` mirror accepts the `src` (else plain text),
     paragraph, fenced code→`<pre><code>`). Emits **only** that set. Returns
     the HTML string; the binder wraps it in
     `<div class="rc-body rc-editor-pane">`.
   - `applyToggle(md, sel:[n,n], kind:'bold'|'italic'|'code') → {value, sel}`
     — **RE·1**: wraps the selection in the kind's markers, or **removes**
     them if the selection is already wrapped (the toggle-off FACES RE2);
     preserves/returns the caret.
   - `applyBlock(md, caret:n, kind:'h1'|'h2'|'h3'|'ul'|'ol'|'quote') →
     {value, caret}` — **RE·1**: inserts the block marker + a trailing space
     at the caret (a new line if mid-text).
   - `applyLink(md, sel:[n,n], url:string) → {value, sel}` — **RE·2**: wraps
     the selection as `[label](url)`.
   - `imageLink(alt:string, id:string): string` — **RC R·3 byte-identical**:
     returns exactly `` `![alt](/content-image/{id})` ``.
   - `bindRichEditor(root: HTMLElement): void` — **RE·1/D1/D2**: finds the
     `textarea[data-rich-editor]` + the toolbar + the preview pane under
     `root`; renders the preview on every `input`; wires each
     `button[data-md]` to the right `apply*` splice (re-focus + restore
     selection); the `data-md="image"` button reuses the **RC upload lane**
     (the `insert-image.ts` `POST /content-image` via `apiFetch`) and splices
     `imageLink(alt, id)` on success. **No** `document.write`, **no**
     untrusted user HTML, **no** package import. If the toolbar (or the view's
     `root`) carries **`data-rich-editor-no-image`**, the image button is
     **omitted entirely**. This option is used on every surface where RC's
     image lane is incomplete (U04 Post Edit — drift pause (c); U05 Group
     Edit — drift pause (c) analog, both Announcement composers — RC U03
     serve-inert; U06 all reply composers — drift pause (a) + RC U03). The
     text toolbar + preview are **unaffected** by this option. RE·3: the
     module does not invent a new image seam for these surfaces.
   - The pure functions (`renderPreview`, `apply*`, `imageLink`) are
     **exported** and **side-effect-free** so `Kumunita.Web.Tests` can import
     and unit-test them without a DOM (RE·2's testability precondition). If
     the self-wire side effects make the module non-importable in the test,
     **extract the pure cores into `client/lib/rich-editor-core.ts`** and keep
     `rich-editor.ts` as the thin self-wiring shell that imports the core —
     record the split in the handoff note (this is a *pre-planned* seam, not a
     drift pause).
2. **`src/Kumunita.Web/wwwroot/css/site.css`** (append one block, ~lines
   791+) — the `.rc-editor` (the split: source | preview, responsive) /
   `.rc-editor-toolbar` + `.rc-btn` (button reset, hover, `:focus-visible`) /
   `.rc-editor-pane` (wraps the preview in the **existing** `.rc-body` for
   body copy; the **existing** `.rc-image` rule styles the preview's `<img>`
   — **no** re-definition of `.rc-body`/`.rc-image`).

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** (no C# touched — this is a
  re-verify that the build is unbroken) **and** `npm run build` in
  `src/Kumunita.Web` **green** (the new module type-checks under `tsc`).
- The module's exports match the register's **pinned contract** verbatim
  (the 5 pure functions + `bindRichEditor`, the exact signatures).
- No `package.json` change, no `.csproj` change, **no** new dependency
  (RE·3). No route / `AccessAction` / `IMediaStore` change.
- Append a `## U3 — Editor module + CSS` section to the handoff note (the
  exports shipped + whether the `rich-editor-core.ts` split was needed)
  **before** the folder move.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the new module (+ core split if needed) + the
  `site.css` append + the handoff append.

## Notes / deviations

- **U3 does NOT wire any view** (U04–U06 do). The module is inert until a
  view includes `<script type="module" src="~/js/lib/rich-editor.js">` — U04
  adds that include (or the layout, per the drift check below).
- **Confirm by grep** whether `_Layout.cshtml` already includes
  `insert-image.js` (the RC image lane). If `rich-editor.js` needs the same
  per-view include treatment, **U04** adds it to the post composer (U03 keeps
  the module dependency-free + self-wiring so it needs no layout change on
  its own). Record the include decision in the handoff note.
- The **image button** must not invent a second upload — it reuses the RC
  `POST /content-image` (the RC `U04` drift note's anti-forgery convention
  holds: `apiFetch` reads the `RequestVerificationToken` meta). If the RC
  upload surface is missing/changed, **drift pause**.
- The toggle-off behavior (RE2 FACES) is load-bearing for the U07 parity test
  `ApplyToggle_Bold_TogglesOff_WhenAlreadyBold` — implement it, don't defer.
