# U5 — ADR 0032 + the close (roadmap + ADR index + folder move)

- **Lane:** Inline editor (`IE`)
- **Unit:** U5 (of U0–U5) — **the final unit; after U5 the lane is closed**
- **Kind:** doc-only (author ADR 0032, update the ADR index, update the
  README Roadmap, move the 6 unit plans + the handoff note `done/`)

## Goal

Settle the lane in the record: author **ADR 0032** (D1 rendered-by-default /
D2 one-button-one-attribute / D3 additive-only; **amending** ADR 0031's D1
*layout*), register it in the ADR index, add the **Done** line to the README
Roadmap, and move the 6 unit plan files (U0–U5, **incl. this one**) + the
handoff note from `in-progress/` to `done/`. **No code, test, CSS, or
`.csproj` change** — the close is doc-only.

## Entry reads (≤ 5 files)

1. `docs/adr/0031-wysiwyg-editor-and-toolbar.md` — the **ADR shape**
   (Status / Context / Decisions / Consequences / "Not decided here"
   structure) + the **exact D1 line** this ADR amends (the "the toolbar,
   the source textarea, and the preview each sit on their own full-width
   row" layout sentence) — U5's ADR **amends**, does not supersede, this.
2. `docs/adr/README.md` — the **ADR index** (the 0031 row's exact shape to
   mirror for the 0032 row; confirm the current highest number is **0031**
   so 0032 is next).
3. `README.md` (the **Roadmap** section) — the **`RE` Done line** (the
   exact shape to mirror for the `IE` Done line; also the
   `Milestones.cs` pairing — see Notes: the register's U5 does **not**
   move a roadmap letter, so **no** `Milestones.cs` change).
4. `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md` —
   the full lane's handoff (`## Lane open` + the 5 unit sections
   `## U0`…`## U4`) — the close's **evidence** (the ADR's D1/D2/D3 are
   drawn from what U0–U4 actually shipped).
5. `docs/design/inline-editor-design.md` — the **named deferrals**
   (localStorage persistence, keyboard shortcut, caret mapping between
   views, mobile-specific toggle UI, true `contenteditable` / third-party
   editor) — the ADR's **"Not decided here"** section's source.

## Deliverables (closed set — 2 files authored/updated + 7 files moved)

### 1. `docs/adr/0032-inline-editor-rendered-default-view.md` (new) — the ADR

**Status: Accepted** (U5 is the unit that accepts it — U0–U4 did not touch
the ADR). **Amends ADR 0031** (the "Amends: 0031" line is explicit; this
is *not* a supersedure — ADR 0031's architecture, D1's *no-
contenteditable / no-round-trip / textarea-is-source-of-truth* stance,
D2's toolbar-as-splice, D3's client-preview-mirrors-RC, and the `tsc`-only
constraint all **keep binding**). Mirrors ADR 0031's structure:

- **Context.** RE (ADR 0031) shipped the split view — toolbar row, source
  `<textarea>` row, rendered preview row — **all three visible at once**.
  The resident's eyes land on the raw Markdown first; the rendered pane
  reads as a secondary pane, not **the** editing surface. This ADR settles
  the default: the rendered pane is the **default view**; the source is
  hidden behind one toggle.
- **D1 — Rendered-by-default, source on demand.** On load (and after a
  form validation round-trip), the source `<textarea>` is **hidden**
  (`.rc-editor-source-hidden`, `display: none`) and the rendered pane is
  the **visible** composition surface. The toggle (far right of the
  toolbar) reveals the source; clicking again restores the default. This
  is a **visibility toggle** over the single existing source of truth
  (the textarea) and its single existing render (the pane) — **not** a
  mode switch with two editing engines. **Amends** ADR 0031 D1's *layout*
  (the "each sit on their own full-width row" sentence) to "the rendered
  pane is the default view; the source is hidden behind the toggle."
- **D2 — One button, one attribute, one binder extension.** The toggle is
  `<button type="button" class="rc-btn" data-ie-toggle>` appended to
  **each** existing `.rc-editor-toolbar`. `bindRichEditor` extends (does
  not replace) to find the button, set the initial hidden state, wire the
  click to toggle, and keep the preview in sync on every `input` (the
  existing `renderPane` is reused verbatim). No new `data-md` value —
  `data-ie-toggle` distinguishes it from the `button[data-md]` splice set.
- **D3 — Additive only.** No pure function is touched (`renderPreview` /
  `applyToggle` / `applyBlock` / `applyLink` / `imageLink` /
  `isSafeImageSrc` stay **byte-identical**); no new export; the toolbar's
  existing buttons keep their exact positions (the toggle is **appended**,
  never re-ordering); the `data-rich-editor-no-image` gating is
  **unaffected** (the toggle is present on **every** editor, image-gated
  or not — a view control, not a content control); the `rc-editor-pane` /
  `rc-body` / `rc-image` classes are **reused**, not re-defined.
- **Consequences.** `tsc`-only **stands unchanged** (no editor dependency,
  no bundler, no JS test runner — the RE·3 constraint is untouched; what
  changes is the **default view**, not the *architecture*). The two new
  `<kw-l>` keys (`rc.editor.source` = `</>`, `rc.editor.showPreview` =
  `Preview`) extend the closed `rc.editor.*` set. The saved body is
  **byte-identical** Markdown the RC read path (`MarkdownRenderer`)
  already renders — the IE lane adds **zero** stored fields, **zero**
  routes, **zero** server surface.
- **Not decided here** (the design doc's named deferrals — **out of scope**
  for this lane; each is a *future* lane, not an IE re-open): localStorage
  persistence of the toggle state; a keyboard shortcut for the toggle;
  caret-mapping between the source and rendered views; a mobile-specific
  toggle UI; **true `contenteditable`** / a third-party editor (a hard
  non-negotiable per ADR 0031 D1).

### 2. `docs/adr/README.md` — append the 0032 row

The **exact shape** of the 0031 row, mirrored (same columns, same link
target, same "Amends 0031" / "Accepted" annotation style), appended **after**
the 0031 row. The row names the file
`0032-inline-editor-rendered-default-view.md`.

### 3. `README.md` — append the Done line to the Roadmap

After the existing `RE` Done line, the **exact shape of the `RE` line,
mirrored** (same bold-lead, same `(\`IE\`, ADR 0032)` ID style, same
**Done** suffix):

> `**Inline editor** (\`IE\`, ADR 0032) — the rendered view is the
> default editor; the Markdown source is hidden behind a toolbar toggle.
> No new dependency, no new route, no re-shape of the RE pure functions —
> the saved body is byte-identical Markdown the RC read path already
> renders. **Done.**`

**M4/M5/M6 stay Events / Projects / Portability** — no roadmap letter
moves, so **no** `Milestones.cs` change (the register is explicit; this
differs from a letter-milestone lane).

### 4. Folder moves (last, after all the above are authored)

Move the 6 unit plan files + the handoff note `in-progress/` → `done/`:

- `docs/plans-milestones/in-progress/inline-editor-u00-plan.md` → `done/`
- `docs/plans-milestones/in-progress/inline-editor-u01-plan.md` → `done/`
- `docs/plans-milestones/in-progress/inline-editor-u02-plan.md` → `done/`
- `docs/plans-milestones/in-progress/inline-editor-u03-plan.md` → `done/`
- `docs/plans-milestones/in-progress/inline-editor-u04-plan.md` → `done/`
- `docs/plans-milestones/in-progress/inline-editor-u05-plan.md` → `done/`
- `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md` →
  `done/`

The **register** (`docs/plans-milestones/plan-inline-editor.md`) **stays**
in `plans-milestones/` (the RE precedent: `plan-rich-editor.md` is in
`plans-milestones/`; `done/` holds the unit plans + the handoff note).
Use `git mv` for each move so the rename is tracked (not delete + create).

## Exit

- ADR 0032 is **present** with **Status: Accepted** and the explicit
  **"Amends 0031"** line; it names the D1/D2/D3 decisions + the "Not
  decided here" deferrals.
- The ADR index row (the 0032 row) is present in `docs/adr/README.md`.
- The README Roadmap `IE` Done line is present, mirroring the `RE` line.
- All 6 unit plan files (U0–U5) + the handoff note are in `done/`; the
  register is in `plans-milestones/`.
- `git status` clean **except**: ADR 0032 (new) + the ADR index + the
  README + the 7 renames (6 unit plans + handoff note, via `git mv`).
  **No** code, test, CSS, or `.csproj` change in this unit.
- Handoff note (append before the move): 5–6 lines starting `## U5 — ADR
  0032 + close` — (a) the ADR number + the exact "Amends 0031" line,
  (b) the ADR index row (file + line), (c) the README Roadmap line
  (file + line), (d) the 7 moved files (the exact `from` → `to` paths),
  (e) the `git status` summary (the expected clean set). **This is the
  final unit — after U5, the lane is closed.**

## Notes / deviations

- **The close is doc-only by the register's pin.** If U5's `git status`
  reveals an **unexpected** code/test/CSS/`.csproj` change (e.g. U1's
  binder or U4's test file is not committed, or a stray edit landed in
  `rich-editor.ts`), that is a `## U5 — Drift pause` naming the file +
  the diff — **not** a silent absorption. The U0–U4 units own the code;
  U5 only records it.
- **Amend, do not supersede.** ADR 0032's header must say it
  **Amends** 0031 (the two ADRs are *both* accepted and *both* binding).
  Writing "Supersedes 0031" or re-stating ADR 0031's D1/D2/D3 as
  retired is a **drift pause** — it would contradict the register's
  "D1 (ADR 0031) still binds" pin and the design doc's "untouched and
  still binds" invariant.
- **No `Milestones.cs` change.** Unlike a letter-milestone, the IE lane
  does not move a roadmap letter (M4/M5/M6 stay Events/Projects/
  Portability). If a temptation arises to also touch `Milestones.cs` or
  `MilestonesTests.cs`, **stop** — the register is explicit that no
  roadmap letter moves, and the `MilestonesTests` order-pinning is
  untouched.
- **Order the moves *after* authoring.** Author ADR 0032 + the index row
  + the README line **first**, then `git mv` the 7 files. Moving the
  handoff note `done/` before its `## U5` section is written would mean
  editing a file in `done/` — do the `## U5` append to the handoff note
  while it is still in `in-progress/`, then move it.
- **The handoff note's `## U5` section is written *before* the move**
  (it is the close's evidence). Sequence: (1) write ADR 0032, (2) the
  ADR index row, (3) the README line, (4) append `## U5 — ADR 0032 +
  close` to the handoff note, (5) `git mv` the 7 files, (6) `git status`
  to confirm the clean set.
