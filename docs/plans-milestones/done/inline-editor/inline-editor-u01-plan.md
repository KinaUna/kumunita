# U1 — Core module: the toggle button + default-hidden state in `rich-editor.ts`

- **Lane:** Inline editor (`IE`)
- **Unit:** U1 (of U0–U5)
- **Kind:** code (the client module + the one CSS rule)

## Goal

Extend `bindRichEditor` with the toggle wiring (initial state = source
hidden; the click handler; the label swap) — **no** pure-function
change, **no** new export, **no** re-shape of the existing wiring
(the toolbar's `data-md` buttons, the image button's
`data-rich-editor-no-image` gating, the `input`-driven `renderPane`
all keep their exact behavior) — and append the one load-bearing CSS
rule that hides the source.

## Entry reads (≤ 3 files, each < ~300 lines)

1. `docs/design/inline-editor-design.md` — the **binder extension
   contract** + the **CSS pin** + the **exact toggle contract** (the
   primary pin for this unit).
2. `src/Kumunita.Web/client/lib/rich-editor.ts` — the **current**
   binder (`bindRichEditor`) + the self-wire loop — the **exact code
   U1 extends** (read the whole binder: the toolbar/pane/textarea
   lookups, the `noImage` removal, the `renderPane` + `input`
   listener, the `button[data-md]` loop, the image upload branch, and
   the trailing self-wire `for (const editor of … .rc-editor …)` loop).
3. `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md` —
   the **`## U0 — Kickoff verified`** section (the verified surface:
   the binder's existing exports, the closed `rc.editor.*` key set the
   button's `<kw-l>` references, the `package.json` confirmation).

## Deliverables (closed set — 2 files)

1. **`src/Kumunita.Web/client/lib/rich-editor.ts`** — **extend**
   `bindRichEditor` (do **not** replace or re-order the existing
   body). The exact additions, in order **after** the existing
   `renderPane` + `input`-listener wiring and **before** (or after —
   either is fine; record which in the handoff) the `button[data-md]`
   loop, guarded so the toggle is a no-op if the button is absent:

   ```ts
   // IE·1, D1/D2 — the view toggle (additive; the existing wiring is
   // untouched). Finds the data-ie-toggle button, sets the initial
   // state (source hidden, pane visible), and wires the click to
   // toggle the two classes + swap the button's <kw-l> key.
   const toggle = root.querySelector<HTMLButtonElement>('button[data-ie-toggle]');
   if (toggle) {
     const srcHidden = 'rc-editor-source-hidden';
     const paneActive = 'rc-editor-pane-active';
     const setView = (showSource: boolean): void => {
       textarea.classList.toggle(srcHidden, !showSource);
       if (previewPane) previewPane.classList.toggle(paneActive, showSource);
       // Label swap: the button shows "source" while hidden, "preview" while visible.
       const kw = toggle.querySelector('kw-l');
       if (kw) kw.setAttribute('key', showSource ? 'rc.editor.showPreview' : 'rc.editor.source');
       // Fallback: if the platform's <kw-l> does not live-update on a key
       // attribute change, set the button's text directly. (Record which
       // path the platform actually uses in the handoff note.)
       toggle.lastChild && (toggle.lastChild.textContent = showSource ? 'Preview' : '</>');
     };
     setView(false); // IE·1: the rendered pane is the default view.
     toggle.addEventListener('click', () => {
       setView(!textarea.classList.contains(srcHidden));
     });
   }
   ```

   **Constraints (the load-bearing pins):**
   - **No** pure function (`renderPreview` / `applyToggle` /
     `applyBlock` / `applyLink` / `imageLink` / `isSafeImageSrc`) is
     touched.
   - **No** new `export` is added.
   - The existing `renderPane` function + the `input` listener are
     **reused verbatim** — **not** re-wired, **not** re-ordered.
   - The existing `button[data-md]` loop + the image upload branch are
     **byte-identical** in the diff.
   - The self-wire loop (`document.querySelectorAll('.rc-editor')`) is
     **unchanged** — it already calls `bindRichEditor`, which now also
     wires the toggle.
   - `textarea.disabled` is **never** set; the `hidden` attribute is
     **never** added — only the `rc-editor-source-hidden` **CSS class**
     (IE·1's load-bearing pin: the textarea stays a live form field +
     in tab order + in the form-submit set).
2. **`src/Kumunita.Web/wwwroot/css/site.css`** — **append** to the
   existing `.rc-editor` block (do **not** re-order or re-style the
   existing rules):

   ```css
   /* --- Inline editor (IE U1) — the source is hidden by default (IE·1) ---
      Hiding is a CSS class (display: none), never `disabled` (which would
      break form binding) and never the `hidden` attribute (which would
      remove it from tab order + the form-submit set). RE·1 unchanged: the
      textarea is still the single source of truth the server binds. */
   .rc-editor textarea.rc-editor-source-hidden {
     display: none;
   }
   ```

   (The optional no-op `.rc-editor-pane.rc-editor-pane-active { }`
   marker is **added only if** the design doc pins it; if the design
   doc omits it, U1 adds only the load-bearing rule.)

## Exit

- `dotnet build Kumunita.slnx -c Debug` **green** (the C# compile is
  unaffected — the TS change is separate) **and** `npm run build`
  **green** (the `tsc` compile of the extended module succeeds — the
  added block type-checks against the existing `textarea` /
  `previewPane` / `root` locals).
- The `rich-editor.ts` diff is **additive only** (the existing pure
  functions + the existing binder wiring are **byte-identical** in the
  diff; only the new toggle block + the new CSS rule are added).
- The compiled `wwwroot/js/lib/rich-editor.js` contains the string
  `data-ie-toggle` **and** `rc-editor-source-hidden` (a quick
  `Select-String` check — the artifact U4's tests will pin formally).
- **No** `.csproj` change, **no** `package.json` change (RE·3 holds).
- Handoff note: 4–6 lines starting `## U1 — Module toggle` — (a) the
  binder's new toggle block (the `data-ie-toggle` selector + the
  `setView` + the `setView(false)` initial call + the `click` handler),
  (b) the CSS rule added (file + line), (c) the two `<kw-l>` key names
  the button references (`rc.editor.source` + `rc.editor.showPreview`
  — U3 registers them), (d) the **label-swap path** used (the `<kw-l>`
  `key` attribute swap vs. the `lastChild.textContent` fallback — record
  which the platform actually renders, so U4's tests + U5's ADR are
  accurate), (e) confirmation that the 6 pure functions + the existing
  binder wiring are byte-identical in the diff.
- Move this plan file `in-progress/` → `done/` (**last**).

## Notes / deviations

- **If the platform's `<kw-l>` TagHelper does not live-update its
  rendered text when the `key` attribute changes** (likely — it is a
  server-rendered tag helper, not a reactive component), the
  `lastChild.textContent` fallback is the **load-bearing** path and the
  `key` swap is cosmetic (for the HTML source's correctness + the
  a11y label). **Record which is load-bearing** in the handoff note —
  U5's ADR consequence ("the button's label swaps") must match the
  mechanism actually used.
- **Do not** touch the `data-md` buttons' order, the `noImage` removal
  branch, or the image upload branch — IE is additive; the RE surface
  is byte-identical (RE·3 / D3).
- **Do not** introduce a `contenteditable`, a second textarea, or a
  dual-pane with independent state — IE·1 / RE·1 / D1 (ADR 0031) forbid
  it. If tempted, record a `## U1 — Drift pause` naming it as the
  "true contenteditable" future-lane deferral.
