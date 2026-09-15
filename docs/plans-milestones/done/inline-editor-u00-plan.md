# U0 — Kickoff: verify the current surface + author the design doc

- **Lane:** Inline editor (`IE`)
- **Unit:** U0 (of U0–U5)
- **Kind:** kickoff / verification + design (doc-only — no code)

## Goal

Confirm, against the current code, that the RE surface is exactly as the
register's **Assumptions** describe (the `.rc-editor` view instances, the
toolbar/button set, the binder's existing shape, the closed
`rc.editor.*` key set, the CSS block), and author the **design doc**
(`docs/design/inline-editor-design.md`) that pins the invariant
(**IE·1**), the FACES (**IE1–IE5**), the **exact toggle contract**
(initial state = source hidden; the button's label swap; the
`renderPane` reuse), the **CSS pin**, the **Razor button pin**, the
**two `<kw-l>` keys**, the **pinned seam-test names**, the
**acceptance gate**, and the **drift guard**.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/plans-milestones/plan-inline-editor.md` — **this lane's
   register** (the primary source of truth for what this lane is and is
   not; the D1/D2/D3 decisions + the unit-series rules).
2. `docs/plans-milestones/done/rich-editor-handoff-notes.md` — the
   **RE lane's close** (the frozen base: `rich-editor.ts`'s six pure
   functions + `bindRichEditor`'s existing wiring + the
   `data-rich-editor-no-image` image-button gating + the `rc-editor-*`
   CSS block + the `rc.editor.*` key set). **Read the `## Lane open` +
   the U07 close sections** — they name the per-surface image-button
   matrix (which is **unaffected** by IE — the toggle is a view
   control, not a content control).
3. `src/Kumunita.Web/client/lib/rich-editor.ts` — the binder
   (`bindRichEditor`) + the self-wire loop — the **exact shape U1
   extends** (the toolbar/pane/textarea lookups, the
   `data-rich-editor-no-image` removal, the `input`-driven `renderPane`,
   the `button[data-md]` loop, the self-wire over `.rc-editor`).
4. `src/Kumunita.Web/Views/Posts/New.cshtml` — **one** representative
   `.rc-editor` block (the toolbar + the 10 `data-md` buttons + the
   textarea + the pane) — the markup U2's toggle button is appended
   after.
5. `docs/design/rich-editor-design.md` — the **RE design doc's shape**
   (invariants / FACES / Pinned contract / seam-tests / gate /
   drift-guard) — the **template U0's design doc mirrors** (IE is
   smaller: 1 invariant, 5 FACES, the toggle contract in place of the
   marker-set ceiling).

**Also grep** (not a full read) for the `.rc-editor` surface set:
`grep -rn "class=\"rc-editor\"" src/Kumunita.Web/Views/` — record the
**exact file + line** list (the register assumes 15 editor blocks
across 10 files, counting the multi-instance files `Posts/Detail` and
`Groups/PostDetail` as 4 each). This is the verified surface U2 works
against.

## Deliverables (closed set — 2 files)

1. **`docs/plans-milestones/in-progress/inline-editor-handoff-notes.md`**
   — the `## Lane open` section is **pre-authored** (see the seed). U0
   **appends** a `## U0 — Kickoff verified` section recording:
   - the **exact file + line** of each `.rc-editor` block (the
     grep-verified list; the count — expected 15 editor blocks across
     10 files, but record what the grep actually finds);
   - the **exact current button set** in one toolbar (the `data-md`
     values, **in order** — bold, italic, code, h1, h2, h3, ul, ol,
     link, image; note that image-gated surfaces drop the image button
     per the RE matrix);
   - the binder's **existing exports** (the 6 pure functions:
     `renderPreview` / `applyToggle` / `applyBlock` / `applyLink` /
     `imageLink` / `isSafeImageSrc`, + the `bindRichEditor` binder);
   - the **closed** `rc.editor.*` key set (the
     `KnownTranslationKeys.cs` registration site + its "closed set"
     comment — the exact key names: `rc.editor.bold` … `rc.editor.image`
     + `rc.editor.preview`);
   - a one-line confirmation that `package.json` is still
     `typescript`-only (RE·3 holds at IE lane open).
2. **`docs/design/inline-editor-design.md`** — the primary tier.
   Required sections (mirror the `rich-editor-design.md` shape, scaled
   to IE's smaller scope):
   - **The three-tier contract header** (this file is the primary tier;
     the register is secondary; the handoff note is scratch; the
     frozen RC + RE base re-anchored unchanged).
   - **Value chain** — RE made the rendered pane *visible*; IE makes it
     the *default view*; the source is one button-press away (the
     power-user view, not the starting point).
   - **Invariants (1)** — **IE·1**: **the rendered pane is the default
     view; the Markdown source is hidden behind the toggle.** The
     textarea is still the **single source of truth** (RE·1
     unchanged) — hiding it is a **CSS class** (`display: none`), never
     `disabled` (which would break form binding) and never the `hidden`
     attribute (which would remove it from tab order + the form-submit
     set). The toggle is a **visibility control**, not a mode switch
     with two editing engines — the pane is still a pure render of the
     textarea (RE·1/RE·2 unchanged); the toolbar's splice buttons still
     act on the (possibly hidden) textarea (RE·1 unchanged).
   - **FACES (5)** — **IE1** (a resident opens a composer and sees the
     **rendered pane** as the main surface — the raw `**bold**` markers
     are **not** the first thing they see), **IE2** (clicking the
     toggle reveals the source `<textarea>` with the raw markers + the
     pane hides — the resident can now hand-type Markdown or use the
     toolbar's splice buttons on the visible source), **IE3** (clicking
     the toggle again restores the default — the pane is visible, the
     source hidden, the preview in sync with the current value), **IE4**
     (the toggle is present on **every** editor, image-gated or not —
     it is a view control, not a content control; the image button's
     `data-rich-editor-no-image` gating is unaffected), **IE5** (the
     saved body is **byte-identical** to what the resident could have
     hand-typed in the source view — RC R·3 + RE·1 unchanged; the
     server parse + render are untouched).
   - **Pinned contract** — the exact artifacts (additive only):
     - **The binder extension** (U1): `bindRichEditor` finds
       `button[data-ie-toggle]`; sets the **initial** state (textarea
       gets `rc-editor-source-hidden`, pane does not); wires the
       button's `click` to toggle the two classes + swap the button's
       `<kw-l>` `key` between `rc.editor.source` and
       `rc.editor.showPreview`; **reuses** the existing `renderPane`
       verbatim (the `input` listener is not re-wired). **No** pure
       function touched; **no** new export added.
     - **The CSS class** (U1): `.rc-editor textarea.rc-editor-source-hidden
       { display: none; }` (the load-bearing rule; an optional
       no-op `.rc-editor-pane.rc-editor-pane-active` marker if the
       design pins it).
     - **The Razor button** (U2): `<button type="button" class="rc-btn"
       data-ie-toggle><kw-l key="rc.editor.source">&lt;/&gt;</kw-l></button>`
       — **appended** after the last existing `data-md` button in each
       of the 10 toolbars; **never** re-ordering the existing buttons.
     - **The two `<kw-l>` keys** (U3): `rc.editor.source` (value
       `</>`) + `rc.editor.showPreview` (value `Preview`) — appended to
       the closed `rc.editor.*` set in `KnownTranslationKeys.cs` + the
       closed-set comment updated.
   - **The exact toggle contract** — initial state (source hidden, pane
     visible, button labeled `rc.editor.source`); the click handler
     (swap the two classes + swap the button's `key` + the `<kw-l>`'s
     rendered text updates via the platform's existing `<kw-l>`
     mechanism — U1 does **not** re-render the `<kw-l>` manually; the
     `key` attribute swap is the pin, and if the platform's `<kw-l>`
     does not live-update on a `key` attribute change, U1's
     handoff notes the deviation + U1 sets the button's text
     content directly as the fallback — **record which** in the
     handoff note); the `renderPane` reuse (the preview stays in sync
     on every `input` — the existing wiring is untouched).
   - **Pinned seam tests (3 + 1 artifact pin)** — the exact names
     (U4 implements them):
     1. `CompiledRichEditorJs_ContainsToggleButton`
     2. `CompiledRichEditorJs_ContainsSourceHiddenClass`
     3. `RichEditorTextarea_IsNotDisabled_OrRemoved`
     4. `CompiledRichEditorJs_StillExportsRePureFunctions` (artifact
        pin — guards RE·3: the IE lane is additive only; the RE
        surface is byte-identical).
   - **Acceptance gate** (U4 records) — `dotnet build Kumunita.slnx -c
     Debug` **green** + `npm run build` **green** + `dotnet exec
     tests/Kumunita.Web.Tests/…/Kumunita.Web.Tests.dll` **green**
     (the 3 new IE tests + the 10 existing RE tests + the existing
     RC/M3/etc. tests — no regression) + `dotnet exec
     tests/Kumunita.Core.Tests/…/Kumunita.Core.Tests.dll` **green**
     (the U3 `KnownTranslationKeys.cs` change — no regression).
   - **Drift guard** (frozen once written) — the IE·1 invariant, the
     5 FACES, the binder extension contract (the `data-ie-toggle`
     selector + the initial-state line + the click handler + the
     `renderPane` reuse), the CSS class, the Razor button markup, the
     two `<kw-l>` key names + values, the 3 seam-test names + the
     artifact pin, the 10 view-instance file + line list — all frozen
     pins; any mismatch is a `## U<m> — Drift pause` per the unit-series
     rule §6.
   - **Named deferrals** (each a **future lane**, the ADR 0011 / 0025
     / 0031 precedent): **localStorage persistence** (remember the
     resident's view preference across sessions), **keyboard shortcut**
     (a key binding for the toggle), **caret mapping between views**
     (when the resident toggles, the caret/selection in the source view
     could map to the rendered position — a power feature, not a
     floor), **mobile-specific toggle UI** (a thumb-reachable toggle on
     small screens), **true contenteditable / third-party editor**
     (ProseMirror/Quill/Tiptap — **hard non-negotiable** out, D1 ADR
     0031; not a deferral; a separate-architecture decision).
   - **Frozen base (re-anchored unchanged)** — **RC R·1–R·7** (one
     renderer, escape-first, source-is-the-`string`, `ImageIds`
     server parse, the `/content-image` routes, the `.rc-body` /
     `.rc-image` CSS) + **RE·1–RE·3** (one source of truth,
     preview↔renderer parity, no new dependency / no new server
     surface). IE adds **no** re-shape of any of them.

## Exit

- Both files present; the design doc's toggle contract + CSS pin +
  Razor button pin + the 2 key names are stated **concretely** (a
  fresh agent can implement U1/U2 from the doc alone without
  re-deriving the RE surface).
- The handoff note's `## U0 — Kickoff verified` section is present and
  its evidence (the grep-verified file + line list, the button set,
  the binder exports, the closed key set) matches what a re-read finds.
- If **any** assumption is out of date (e.g. the view count is not 10,
  or the binder's shape has changed, or the `rc.editor.*` set is not
  closed), this unit **pauses** and appends `## U0 — Drift pause`
  naming the exact seam + what changed, and the register's
  Assumptions are corrected in the same section.
- **No** code, test, CSS, or `.csproj` change in this unit. **No**
  build is required (doc-only). **No** `npm run build` (no TS touched).
- Move this plan file `in-progress/` → `done/` (**last**, after the
  handoff append). `git status` clean except the two new doc files +
  the handoff append.

## Notes / deviations

- **IE is not a new editing engine.** It is a **visibility toggle**
  over the RE lane's single existing source (the textarea) + its
  single existing render (the pane). If U0's design doc is tempted to
  introduce a second editing surface (a `contenteditable`, a
  dual-pane with independent state, a mode with two carets), **cut it**
  (IE·1 / RE·1 / D1 ADR 0031) — or record a `## U0 — Drift pause`
  naming it as a **future lane** (the "caret mapping between views"
  deferral is the nearest named one).
- U0 does **not** implement the module (U1), does **not** add the
  buttons (U2), does **not** register the keys (U3), does **not**
  write the tests (U4), and does **not** touch the ADR / README /
  `Milestones.cs` / `MilestonesTests.cs` (U5).
- The ADR 0032 filename is **`0032-inline-editor-rendered-default-view.md`**
  (the register's close section names it); U5 verifies the name +
  number against the ADR index (the current highest is **0031**) before
  accepting.
