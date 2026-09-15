# Inline editor (`IE`) — the rendered view is the default editor; the Markdown source is hidden behind a toggle

> **Status.** This is the **lane register** (secondary tier of the lane's
> three-tier contract) for a follow-on lane to the **rich editor lane
> (`RE`, ADR 0031)** that shipped the split-view authoring surface (toolbar
> + source `<textarea>` + rendered preview pane, all on 10 composer
> surfaces). RE made the *rendered* pane **visible** while composing; this
> lane finishes the job from the resident's point of view: the rendered
> pane becomes the **default view** (the Markdown source is **hidden**
> until the resident asks to see it) — the "WYSIWYG by default, source on
> demand" half of what RE's split view promised but did not deliver.
> **M4/M5/M6 stay Events / Projects / Portability** — no roadmap letter
> moves.

## The gap this lane closes (to be re-verified against the code in U0)

- **The rendered pane is not the default.** RE (U03–U06) shipped a
  split view: toolbar row, source `<textarea>` row, rendered preview row —
  **all three visible at once**, the source in a monospace font, the raw
  `**bold**` markers sitting in the middle of the composition experience. A
  resident's eyes land on the raw Markdown first; the rendered pane is
  visually *below* it and reads as a secondary "nice to see" pane, not as
  **the** editing surface.
- **There is no way to hide the source.** A resident who wants to read
  what they will publish, with the raw markers out of the way, cannot
  collapse the source today — the only control is the toolbar (which
  *formats* the source) and the submit button.

This lane closes exactly that gap with **one toggle** and **zero new
machinery**: the source stays the single source of truth (RE·1,
unchanged); the rendered pane becomes the default view; a toolbar button
reveals the source when the resident wants it — and the RE toolbar's
existing splice buttons + the image upload keep working, because they act
on the (possibly hidden) textarea, not on the rendered DOM.

**True in-DOM editing of the rendered view is explicitly out** — ADR 0031
D1 is the load-bearing non-negotiable: no `contenteditable`, no
HTML→Markdown round-trip engine, no editor dependency. This lane does not
re-open D1; it changes **which view is default** and **adds the one button
that switches views** — D1's *architecture* (the textarea is the source of
truth; the pane is a pure render of it) is **untouched and still binds**.

## Understanding

RE moved the platform from *"the read path renders Markdown once it is
saved"* to *"a resident formats with a toolbar, sees a live rendered
preview, and the saved body is the same Markdown source the read path
already renders."* The arrow this lane moves: **"seeing the rendered
output is the default experience; the Markdown is a power-user view, not
the starting point."** A resident opens a composer and sees *what they
will publish* filling the surface — not a wall of `**` and `#`. The
Markdown is one button-press away (and the toolbar's formatting buttons
still write Markdown into it, transparently).

**The one thing every unit must respect:** this lane is **client-only,
additive, and one-button.** It changes **zero** pure functions in
`rich-editor.ts` (`renderPreview`/`applyToggle`/`applyBlock`/`applyLink`/
`imageLink`/`isSafeImageSrc` all stay byte-identical), **zero** server
surface, **zero** routes, **zero** `package.json`/`.csproj` changes, and
**zero** stored fields. The only new artifacts are: one CSS class
(`.rc-editor-source-hidden`), one `<button>` added to each existing
toolbar, one `data-ie-toggle` attribute on that button, the binder
extension in `bindRichEditor` that wires it, and two `<kw-l>` keys
(`rc.editor.source`, `rc.editor.showPreview`). Every RC R·1–R·7 and
RE·1–RE·3 invariant keeps binding **unchanged**; this lane adds one new
one (IE·1) and a small FACES set (IE1–IE5).

**The design decisions (settled — D1, D2, D3; this is this lane's ADR
payload, ADR 0032):**

- **D1 — Rendered-by-default, source on demand.** On load (and after a
  form validation round-trip), the source `<textarea>` is **hidden**
  (`.rc-editor-source-hidden`, `display: none`) and the rendered pane is
  the **visible, interactive** composition surface. The toggle button
  (far right of the toolbar) shows the source and hides the pane;
  clicking it again restores the default. This is **not** a mode switch
  with two editing engines — it is a **visibility toggle** over the
  single existing source of truth (the textarea) and its single existing
  render (the pane). D1 (ADR 0031) still binds: no `contenteditable`, no
  round-trip engine, the textarea is still the form field the server
  binds.
- **D2 — One button, one attribute, one binder extension.** The toggle is
  a `<button type="button" class="rc-btn" data-ie-toggle>` appended to
  **each** existing `.rc-editor-toolbar` (15 editor blocks across 10
  view files — `Posts/Detail` and `Groups/PostDetail` having 4 each).
  `bindRichEditor` extends (does not replace) to
  (a) find the button, (b) set the initial hidden state, (c) wire the
  click to toggle, and (d) keep the preview in sync on every `input`
  (already wired — the binder's `renderPane` is reused verbatim). No
  new `data-md` value is introduced for the toggle (it is not a
  Markdown splice — it is a view control; `data-ie-toggle` distinguishes
  it from the `button[data-md]` set the existing binder loops over).
- **D3 — Additive only.** No pure function is touched; no new export is
  added (the binder already handles it internally); the toolbar's existing
  buttons keep their exact positions (the toggle is **appended**, never
  re-ordering existing buttons); the image button's
  `data-rich-editor-no-image` gating is **unaffected** (the toggle is
  present on **every** editor, image-gated or not — it is a view control,
  not a content control); the `rc-editor-pane` / `rc-body` / `rc-image`
  classes are **reused**, not re-defined.

## Assumptions (pinned)

- **RE is frozen and correct.** `rich-editor.ts`'s six pure functions +
  `bindRichEditor`'s existing behavior (toolbar wiring, the
  `data-rich-editor-no-image` image-button removal, the `input`-driven
  `renderPane`, the self-wire loop over `.rc-editor`) — **all stay as
  they are.** This lane **extends** `bindRichEditor` with the toggle
  wiring; it does not re-shape the existing wiring. **The only new
  artifacts are:** one CSS class, one `<button>` per toolbar, the binder
  extension, and two `<kw-l>` keys. **No `.csproj` change. No
  `package.json` change (still `typescript`-only).**
- **The 10 view files are the surface set (15 editor blocks).** Post
  New/Edit, Group New/Edit, Announcement New/Edit/Detail (reply), the
  static-page editor (`Languages/PreviewPage`), `Posts/Detail`
  (replies — 4 editor blocks), and `Groups/PostDetail` (replies — 4
  editor blocks) are the **complete** set of `.rc-editor` surfaces
  U1/U2's grep confirms (U0
  re-verifies the exact count + file list and records it in the handoff
  note; if the count differs from 10, U0's Drift pause names the diff
  and U1's deliverable set is corrected to the verified list — the
  toggle is per-toolbar, so the count is a U1 mechanical detail, not a
  design question).
- **The `<kw-l>` key set is closed and registered in
  `KnownTranslationKeys.cs`.** RE U08 registered `rc.editor.bold` …
  `rc.editor.image` + `rc.editor.preview` (a closed set; the comment at
  the registration site pins it as closed). This lane adds **two** keys —
  `rc.editor.source` (the button label while the source is **hidden**,
  i.e. "show the source") and `rc.editor.showPreview` (the button label
  while the source is **visible**, i.e. "back to the rendered view") —
  and updates that closed-set comment to name them (U3).
- **The test model is the RE U07 precedent (C# mirror + artifact pin).**
  There is **no** TS test runner in this repo (the `tsc`-only
  constraint, RE·3). U4's tests are a **C# spec mirror** of the toggle's
  behavior (the pin is the *contract*: the button's `data-ie-toggle`
  attribute is present in the compiled JS, the `.rc-editor-source-hidden`
  class is emitted, and the textarea is **not** `disabled`/`removed` —
  the RE·1 single-source-of-truth pin at the artifact level) + a
  **file-existence + export-presence** test on the compiled
  `wwwroot/js/lib/rich-editor.js` (the same shape as RE U07's
  `CompiledRichEditorJs_Exists_And_Exports`).
- **The test-runner quirk holds** (AGENTS.md §Running the tests):
  `dotnet test` / VS Test Explorer are **not** the reliable path on this
  machine; the reliable path is `dotnet build Kumunita.slnx -c Debug`
  + `npm run build` + `dotnet exec tests/Kumunita.Web.Tests/…/Kumunita.Web.Tests.dll`
  + `dotnet exec tests/Kumunita.Core.Tests/…/Kumunita.Core.Tests.dll`.

## Understanding — the two things the lane must never do

1. **Never remove, disable, or `hidden`-attribute the textarea.** The
   textarea is the form field the server binds (RE·1). Hiding it is a
   **CSS class** (`display: none`), never `disabled` (which would
   break the form's model binding on submit) and never the `hidden`
   attribute (which would also remove it from the tab order and from
   the browser's native `Ctrl+Z` / form-submit interaction set). This is
   the load-bearing RE·1 pin at the UI level.
2. **Never re-order or re-style the existing toolbar buttons.** The
   toggle is **appended** after the last existing button (the image
   button, or the link button on image-gated surfaces). No existing
   button's position, label, or `data-md` value changes. The toolbar's
   `flex-wrap` layout already handles the extra button — no CSS
   re-flow needed (a trivial `margin-left: auto` to push the toggle to
   the far right is the **only** layout consideration, and it is
   optional; the default is the appended position).

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U0–U5 below),
one unit per fresh agent with a **~32K context window** (the M3
precedent's ~64K is halved deliberately — the units are small; the
discipline is the same).

**Shared state (three-tier contract):**

- **Primary — the design doc** (`docs/design/inline-editor-design.md`,
  authored by U0): pins the invariant (IE·1), the FACES (IE1–IE5), the
  exact binder contract, the CSS pin, the Razor button pin, the two
  `<kw-l>` keys, the **pinned seam-test names**, the **acceptance
  gate**, and the **drift guard**.
- **Secondary — this file** (`docs/plans-milestones/plan-inline-editor.md`)
  — the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/inline-editor-handoff-notes.md`) —
  one section per unit, appended (never rewritten). Each unit writes
  exactly one short section before it exits; the next unit reads only
  that section + its own entry-reads list. A `## U<m> — Drift pause`
  section is a **blocker**: the next unit reads it first and either
  resolves it (recording the resolution in its own section) or carries
  it forward (naming it in its exit criteria).

**Per-unit template** (each unit follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file
list, ≤ 5 files < ~300 lines each, no full-repo scan); **Deliverables**
(a closed set of new/modified files, ≤ ~4 files / ~300 LOC, no misc
cleanups); **Exit** (`dotnet build` green for the touched projects;
handoff-note entry appended *before* any follow-up action; the unit's
plan file moved `in-progress/` → `done/` **last**).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the drift
guard; (3) never introduces a test whose exact name is not in the
design doc's seam list; (4) never re-shapes a pure function in
`rich-editor.ts` (U1's binder extension is the **only** change to that
file); (5) never adds a `package.json` / `.csproj` dependency; (6) if
entry reads reveal the design doc is out of date, the unit pauses and
records `## U<m> — Drift pause` in the handoff note.

---

## Units (6 total)

### U0 — Kickoff: verify the current surface + author the design doc

- **Goal:** confirm the RE surface is exactly as the register's
  **Assumptions** describe (the 10 view instances, the toolbar/button
  set, the binder's existing shape, the closed `rc.editor.*` key set,
  the CSS block), and author the **design doc**
  (`docs/design/inline-editor-design.md`) that pins IE·1, the FACES
  IE1–IE5, the binder contract, the CSS pin, the Razor button pin, the
  two `<kw-l>` keys, the **pinned seam-test names**, the **acceptance
  gate**, and the **drift guard**.
- **Entry reads:** `docs/plans-milestones/plan-inline-editor.md` (this
  file — the register), `docs/plans-milestones/done/rich-editor-handoff-notes.md`
  (the RE lane's close — the frozen base + the per-surface image-button
  matrix), `src/Kumunita.Web/client/lib/rich-editor.ts` (the binder +
  the self-wire — the exact shape U1 extends),
  `src/Kumunita.Web/Views/Posts/New.cshtml` (one representative
  `.rc-editor` block — the toolbar + textarea + pane markup U2 copies),
  `docs/design/rich-editor-design.md` (the RE design doc's shape —
  invariants/FACES/Pinned-contract/seam-tests/gate/drift-guard — the
  template U0's design doc mirrors).
- **Deliverables (2 files):**
  - `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md` —
    the `## Lane open` section is pre-authored (part of the up-front
    setup). U0 **appends** a `## U0 — Kickoff verified` section recording:
    the exact **file + line** of each of the 10 `.rc-editor` blocks
    (the grep-verified list), the **exact** current button set in one
    toolbar (the `data-md` values, in order), the binder's existing
    exports (the 6 pure functions + `bindRichEditor`), the **closed**
    `rc.editor.*` key set (the `KnownTranslationKeys.cs` registration
    site + its "closed set" comment), and a one-line confirmation that
    `package.json` is still `typescript`-only (RE·3 holds).
  - `docs/design/inline-editor-design.md` — the primary tier. Required
    sections (mirror the `rich-editor-design.md` shape): the value
    chain; **Invariants IE·1**; **FACES IE1–IE5**; the **Pinned
    contract** (the `rich-editor.ts` binder extension + the
    `.rc-editor-source-hidden` CSS class + the toolbar's appended
    `<button data-ie-toggle>` markup); the **exact toggle contract**
    (initial state = source hidden; the button's label swap; the
    `renderPane` reuse); the **pinned seam-test names** (the 3 in the
    register §Pinned seam tests); the **acceptance gate**; the
    **drift guard**; the **named deferrals** (localStorage persistence,
    keyboard shortcut, caret mapping between views, mobile-specific
    toggle UI). It re-anchors **RC R·1–R·7** and **RE·1–RE·3** as the
    frozen base (unchanged) this lane builds on.
- **Exit:** both files present; the design doc's binder contract + CSS
  pin + Razor button pin are stated **concretely** (a fresh agent can
  implement U1/U2 from the doc alone without re-deriving the RE surface).
  No code, test, CSS, or `.csproj` change in this unit. If the
  entry-reads reveal any drift (e.g. the view count is not 10, or the
  binder's shape has changed), U0 records a `## U0 — Drift pause`
  naming the exact seam + what changed, and the register's Assumptions
  are corrected in U0's handoff section. Move this plan file
  `in-progress/` → `done/` **last** (after the handoff append). `git
  status` clean except the two new doc files + the handoff append.

### U1 — Core module: the toggle button + default-hidden state in `rich-editor.ts`

- **Goal:** extend `bindRichEditor` with the toggle wiring (initial
  state = source hidden; the click handler; the label swap) — **no**
  pure-function change, **no** new export, **no** re-shape of the
  existing wiring (the toolbar's `data-md` buttons, the image
  button's `data-rich-editor-no-image` gating, the `input`-driven
  `renderPane` all keep their exact behavior).
- **Entry reads:** `docs/design/inline-editor-design.md` (the binder
  contract + the CSS pin — the primary pin),
  `src/Kumunita.Web/client/lib/rich-editor.ts` (the current binder +
  self-wire — the exact code U1 extends),
  `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md`
  (the `## U0 — Kickoff verified` section — the verified surface).
- **Deliverables (2 files):**
  - `src/Kumunita.Web/client/lib/rich-editor.ts` — **extend**
    `bindRichEditor` (do **not** replace): (a) after the existing
    toolbar/pane/textarea lookups, find the `button[data-ie-toggle]`
    (if absent, return — the toggle is optional at the module level;
    U2 adds it to the views); (b) set the **initial** state: the
    textarea gets the `rc-editor-source-hidden` class, the pane does
    not; (c) wire the button's `click` to **toggle** the two classes
    and swap the button's `<kw-l>` `key` attribute between
    `rc.editor.source` and `rc.editor.showPreview` (the label swap is
    the *only* text change on the button; the `type="button"` and
    `class="rc-btn"` are unchanged); (d) the `renderPane` function is
    **reused verbatim** — the `input` listener is **not** re-wired
    (the existing `textarea.addEventListener('input', renderPane)`
    stays). **No** pure function (`renderPreview`/`applyToggle`/
    `applyBlock`/`applyLink`/`imageLink`/`isSafeImageSrc`) is touched.
    **No** new `export` is added. The self-wire loop
    (`document.querySelectorAll('.rc-editor')`) is **unchanged** — it
    already calls `bindRichEditor`, which now also wires the toggle.
  - `src/Kumunita.Web/wwwroot/css/site.css` — **append** one rule to
    the existing `.rc-editor` block (do **not** re-order or re-style
    the existing rules): `.rc-editor textarea.rc-editor-source-hidden
    { display: none; }` + a companion `.rc-editor-pane.rc-editor-pane-active
    { /* no new styling — the pane's existing .rc-editor-pane styles
    apply; this rule is a no-op marker for future per-state styling */ }`
    (the second rule is **optional** — the first is the load-bearing
    one; if the design doc pins the second as a no-op, U1 adds it; if
    the design doc omits it, U1 adds only the first).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **green** + `npm run
  build` **green** (the `tsc` compile of the extended module). The
  `rich-editor.ts` diff is **additive only** (the existing pure
  functions + the existing binder wiring are byte-identical in the
  diff; only the new toggle wiring + the new CSS rule are added).
  Handoff note: 4–6 lines starting `## U1 — Module toggle` — (a) the
  binder's new toggle block (the `data-ie-toggle` selector + the
  initial-state line + the click handler), (b) the CSS rule added
  (file + line), (c) the two `<kw-l>` keys the button references
  (the U3 registration's exact key names), (d) confirmation that the
  6 pure functions + the existing binder wiring are byte-identical in
  the diff. Move this plan file `in-progress/` → `done/` **last**.

### U2 — The toggle button on all 10 view instances

- **Goal:** add the `<button type="button" class="rc-btn"
  data-ie-toggle>` to **each** existing `.rc-editor-toolbar` in the 10
  view files (the exact file + line list from U0's verified surface) —
  **appended** after the last existing button (the image button, or the
  link button on image-gated surfaces), **never re-ordering** the
  existing buttons.
- **Entry reads:** `docs/design/inline-editor-design.md` (the Razor
  button pin — the exact markup), `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md`
  (the `## U0 — Kickoff verified` section — the 10 file + line list +
  the exact current button set), **one** representative view
  (`src/Kumunita.Web/Views/Posts/New.cshtml` — the toolbar block U2
  copies) to confirm the exact button markup + the `<kw-l>` tag shape.
- **Deliverables (the 10 view files, one button each):**
  - `src/Kumunita.Web/Views/Posts/New.cshtml`
  - `src/Kumunita.Web/Views/Posts/Edit.cshtml`
  - `src/Kumunita.Web/Views/Posts/Detail.cshtml` (4 editor instances —
    4 buttons)
  - `src/Kumunita.Web/Views/Groups/New.cshtml`
  - `src/Kumunita.Web/Views/Groups/Edit.cshtml`
  - `src/Kumunita.Web/Views/Groups/PostDetail.cshtml` (4 editor
    instances — 4 buttons)
  - `src/Kumunita.Web/Views/Announcement/New.cshtml`
  - `src/Kumunita.Web/Views/Announcement/Edit.cshtml`
  - `src/Kumunita.Web/Views/Announcement/Detail.cshtml`
  - `src/Kumunita.Web/Views/Languages/PreviewPage.cshtml`

  **The exact button markup** (the design doc's pin, verbatim):
  ```html
  <button type="button" class="rc-btn" data-ie-toggle>
    <kw-l key="rc.editor.source">&lt;/&gt;</kw-l>
  </button>
  ```
  (the `<kw-l>` fallback text is `</>` — the HTML-escaped form of the
  button's initial label; U3 registers the `rc.editor.source` key with
  a translated value if the platform's languages need one, but the
  fallback is the ASCII `</>`). **Appended** after the last existing
  `data-md` button in each toolbar. **No** other markup change in
  these files (the textarea, the pane, the label, the form — all
  unchanged).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **green** (the Razor
  compile). Each of the 10 files (15 buttons total, counting the
  multi-instance files) has exactly **one** `data-ie-toggle` button per
  toolbar, appended after the last `data-md` button, with the exact
  markup
  above. The existing buttons' order, labels, and `data-md` values are
  **byte-identical** in the diff. Handoff note: 4–6 lines starting
  `## U2 — Toggle buttons` — (a) the 10 file names + the button count
  per file (1 each, except `Posts/Detail` = 4, `Groups/PostDetail` = 4),
  (b) confirmation that the existing buttons are byte-identical in the
  diff, (c) any drift (e.g. a file's toolbar shape differs from the
  `Posts/New.cshtml` representative — name the file + the diff). Move
  this plan file `in-progress/` → `done/` **last**.

### U3 — The two `<kw-l>` keys in `KnownTranslationKeys.cs`

- **Goal:** register the two new keys (`rc.editor.source`,
  `rc.editor.showPreview`) in the **closed** `rc.editor.*` set, and
  update the closed-set comment at the registration site to name them.
- **Entry reads:** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
  (the `rc.editor.*` registration block + the "closed set" comment —
  the exact shape U3 extends), `docs/design/inline-editor-design.md`
  (the two key names + their English values — the pin),
  `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md`
  (the `## U2 — Toggle buttons` section — confirmation the button
  markup is in place and references exactly these two key names).
- **Deliverables (2 files):**
  - `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — append
    the two keys to the existing `rc.editor.*` block (do **not**
    re-order the existing keys):
    ```csharp
    ["rc.editor.source"]      = "</>",   // the toggle button's label while the source is hidden
    ["rc.editor.showPreview"] = "Preview", // the toggle button's label while the source is visible
    ```
    (the exact values are the design doc's pin; if the design doc
    names different values, U3 uses the doc's values and records the
    deviation in the handoff note). Update the closed-set comment to
    name the two new keys (the comment currently names the RE set; U3
    appends `+ rc.editor.source + rc.editor.showPreview`).
  - `docs/design/inline-editor-design.md` — if the design doc's key
    names or values differ from the register's assumption, U3 updates
    the doc to match the **registered** values (the doc is the primary
    tier; the code is the artifact; they must agree — U3 is the unit
    that makes them agree, and the handoff note records the final
    values).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **green** (the
  `Kumunita.Core` compile). The two keys are present in
  `KnownTranslationKeys.cs` with the exact values the design doc pins.
  The closed-set comment names them. Handoff note: 3–4 lines starting
  `## U3 — kw-l keys` — (a) the two key names + their exact registered
  values, (b) the comment update (file + line), (c) confirmation the
  existing `rc.editor.*` keys are byte-identical in the diff. Move
  this plan file `in-progress/` → `done/` **last**.

### U4 — The parity + toggle seam tests + the acceptance gate

- **Goal:** land the IE lane's **pinned seam tests** (IE1–IE5) as an
  executable `Kumunita.Web.Tests` file, run the full
  `Kumunita.Web.Tests` suite via `dotnet exec` (not `dotnet test`),
  and record the **acceptance gate** in the handoff note — proving the
  toggle button is in the compiled JS, the hidden-state class is
  emitted, and the textarea is **not** disabled/removed (the RE·1
  single-source-of-truth pin at the artifact level).
- **Environment reality (pinned — read before writing tests):** the
  RE U07 precedent — there is **no** TS test runner in this repo (the
  `tsc`-only constraint, RE·3). The IE tests are a **C# spec mirror**
  of the toggle's behavior (the pin is the *contract*: the
  `data-ie-toggle` attribute is present in the compiled JS, the
  `.rc-editor-source-hidden` class is emitted, and the textarea is
  **not** `disabled`/`removed`) + a **file-existence +
  export-presence** test on the compiled
  `wwwroot/js/lib/rich-editor.js` (the same shape as RE U07's
  `CompiledRichEditorJs_Exists_And_Exports`). **This is a
  test-harness choice, not a second renderer** (RE·2's "one renderer"
  stance is untouched).
- **Entry reads:** `src/Kumunita.Web/client/lib/rich-editor.ts` (U1's
  binder extension — the `data-ie-toggle` selector + the
  `rc-editor-source-hidden` class — the signatures the artifact pin
  asserts), `tests/Kumunita.Web.Tests/RichEditorTests.cs` (RE U07's
  10 tests + the artifact pin — the test-file convention + the
  `CompiledRichEditorJs_Exists_And_Exports` shape U4 mirrors),
  `src/Kumunita.Web/wwwroot/js/lib/rich-editor.js` (the **compiled**
  output — confirm the `data-ie-toggle` string + the
  `rc-editor-source-hidden` class are present post-`npm run build`),
  `docs/design/inline-editor-design.md` (the 3 pinned seam-test names
  + the acceptance gate), `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md`
  (the `## U1`/`## U2`/`## U3` sections — what shipped + the key
  names).
- **Deliverables (1 file):**
  - `tests/Kumunita.Web.Tests/InlineEditorTests.cs` (new) — the 3
    pinned IE behavior tests (the design doc §Pinned seam tests,
    **exact names**):
    - `CompiledRichEditorJs_ContainsToggleButton` — the compiled
      `wwwroot/js/lib/rich-editor.js` contains the string
      `data-ie-toggle` (the button's attribute is in the compiled
      artifact — the U2 markup + the U1 binder wiring are both
      present).
    - `CompiledRichEditorJs_ContainsSourceHiddenClass` — the compiled
      JS contains the string `rc-editor-source-hidden` (the
      initial-state class is emitted — the U1 CSS rule + the
      binder's initial-state line are both present).
    - `RichEditorTextarea_IsNotDisabled_OrRemoved` — the compiled JS
      does **not** contain `textarea.disabled = true` or
      `textarea.remove()` (the RE·1 single-source-of-truth pin at the
      artifact level — the textarea is hidden via a CSS class, never
      disabled or removed).
    - Plus **one artifact pin**: `CompiledRichEditorJs_StillExportsRePureFunctions`
      — the compiled JS still contains the 5 RE pure function names
      (`renderPreview`, `applyToggle`, `applyBlock`, `applyLink`,
      `imageLink`) + `isSafeImageSrc` + `bindRichEditor` (guards
      RE·3: the IE lane is additive only — the RE surface is
      byte-identical).
    The C# mirror is a **small internal static class** in the test
    file (≤ ~60 lines) that reads the compiled JS file (the same
    `File.ReadAllText` shape as RE U07's artifact pin) and asserts the
    string presence/absence — it is the executable spec, not a second
    product renderer.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` **green** + `npm run
  build` **green** (the JS artifact the tests read is fresh) +
  `dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  **green** (the 3 new IE tests + the 10 existing RE tests + the
  existing RC/M3/etc. tests all pass — no regression) + `dotnet exec
  tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  **green** (the U3 `KnownTranslationKeys.cs` change is a
  `Kumunita.Core` compile + the Core suite passes — no regression).
  Handoff note: 5–6 lines starting `## U4 — Seam tests + gate` — (a)
  the 3 IE test names + the artifact pin name, (b) the
  `dotnet exec` exit codes for both test projects (the acceptance
  gate), (c) confirmation the 10 existing RE tests still pass (no
  regression), (d) any test that needed a deviation from the design
  doc's pin (name the test + the deviation). Move this plan file
  `in-progress/` → `done/` **last**.

### U5 — ADR 0032 + the close (roadmap + ADR index + folder move)

- **Goal:** author **ADR 0032** (settling D1 rendered-by-default /
  D2 one-button-one-attribute / D3 additive-only; amending ADR 0031's
  D1 *layout* — "the toolbar, the source textarea, and the preview
  each sit on their own full-width row" — to "the rendered pane is
  the default view; the source is hidden behind the toggle"), update
  the ADR index, add the **Done** status line to the README's
  roadmap, move the 6 unit plan files (U0–U5, incl. this one)
  `in-progress/` → `done/`, and move the handoff note `in-progress/`
  → `done/`.
- **Entry reads:** `docs/adr/0031-wysiwyg-editor-and-toolbar.md` (the
  ADR shape + the exact D1 line this ADR amends), `docs/adr/README.md`
  (the index — the current highest ADR number is **0031**; 0032 is
  the next), `README.md` (the Roadmap section — the `RE` Done line to
  mirror for the `IE` line), `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md`
  (the full lane's handoff — the `## Lane open` + the 5 unit sections
  — the close's evidence), `docs/design/inline-editor-design.md`
  (the named deferrals — the ADR's "Not decided here" section's
  source).
- **Deliverables (closed set — 7 files moved + 3 files authored/updated):**
  - `docs/adr/0032-inline-editor-rendered-default-view.md` (new) — the
    ADR. Settles **D1** (rendered-by-default, source on demand),
    **D2** (one button, one attribute, one binder extension), **D3**
    (additive only — no pure-function change, no new dependency).
    States the `tsc`-only constraint **stands unchanged**; what
    changes is the **default view** (the rendered pane is the
    default; the source is hidden behind the toggle) — the
    *architecture* (the textarea is the source of truth; the pane is
    a pure render of it) is **untouched**. **Amends** ADR 0031 (not
    supersedes). Numbers after 0031.
  - `docs/adr/README.md` — append the 0032 row to the index (the exact
    shape of the 0031 row, mirrored).
  - `README.md` — append the **Done** status line to the Roadmap
    section (after the `RE` Done line, the exact shape of the `RE`
    line, mirrored): `**Inline editor** (\`IE\`, ADR 0032) — the
    rendered view is the default editor; the Markdown source is hidden
    behind a toolbar toggle. No new dependency, no new route, no
    re-shape of the RE pure functions — the saved body is
    byte-identical Markdown the RC read path already renders.
    **Done.**`
  - **Folder moves (last, after all the above are authored):**
    `docs/plans-milestones/in-progress/inline-editor-u00-plan.md`
    → `done/`, `…u01…` → `done/`, `…u02…` → `done/`, `…u03…` →
    `done/`, `…u04…` → `done/`, `…u05…` → `done/`,
    `docs/plans-milestones/in-progress/inline-editor-handoff-notes.md`
    → `done/`, and this register
    (`docs/plans-milestones/plan-inline-editor.md`) **stays** in
    `plans-milestones/` (the register is not moved — the RE
    precedent: `plan-rich-editor.md` is in `plans-milestones/`,
    `done/` holds the unit plans + the handoff note).
- **Exit:** ADR 0032 is **present** with **Status: Accepted** (U5 is
  the unit that accepts it — the U0–U4 units did not touch the ADR).
  The ADR index row is present. The README Roadmap line is present.
  All 6 unit plan files (U0–U5) + the handoff note are in `done/`; the
  register is in `plans-milestones/`. `git status` clean except the
  ADR + the ADR index + the README + the 6 moved files (the `git
  mv` / rename of the unit plans + the handoff note). **No** code,
  test, CSS, or `.csproj` change in this unit (the close is doc-only).
  Handoff note: 5–6 lines starting `## U5 — ADR 0032 + close` — (a)
  the ADR number + the exact "Amends 0031" line, (b) the ADR index
  row (file + line), (c) the README Roadmap line (file + line), (d)
  the 6 moved files (the exact `from` → `to` paths), (e) the
  `git status` summary (the expected clean set). **This is the
  final unit** — after U5, the lane is **closed**.

## The close (U5's exit is the lane's exit)

- ADR 0032 is **Accepted**; the ADR index is updated; the README
  Roadmap is updated (the `IE` Done line).
- All 6 unit plan files (U0–U5) + the handoff note are in `done/`; the
  register is in `plans-milestones/` (the RE precedent).
- The frozen RC base is **unchanged** (RC R·1–R·7, RE·1–RE·3 all
  bind; the `rich-editor.ts` pure functions are byte-identical; the
  10 view instances' existing buttons are byte-identical; the
  `rc.editor.*` key set is the RE set + the 2 IE keys; the CSS is
  the RE block + the 1 IE rule; `package.json` is still
  `typescript`-only).
- `git status` is clean except the ADR + the ADR index + the README
  + the 7 moved files (the 6 unit plans + the handoff note).
- The **named deferrals** (localStorage persistence, keyboard
  shortcut, caret mapping between views, mobile-specific toggle UI,
  true contenteditable / third-party editor) are the IE lane's
  *future-lane* list — the ADR 0011 / 0025 / 0031 precedent holds:
  each is a **future lane**, named, not a deferral to be quietly
  dropped.
