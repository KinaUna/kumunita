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

## U0 — Kickoff verified

- **Date:** 2026-09-15
- **Surface verified (grep `class="rc-editor"` in `Views/`):** **16** editor
  blocks across **10** view files (the register says "15" — arithmetic error:
  8×1 + 2×4 = 16; the file count 10 and per-file breakdown are correct):

  | File | Line(s) | Count |
  |------|---------|-------|
  | `Posts/New.cshtml` | 89 | 1 |
  | `Posts/Edit.cshtml` | 93 | 1 |
  | `Posts/Detail.cshtml` | 169, 332, 390, 478 | 4 |
  | `Groups/New.cshtml` | 61 | 1 |
  | `Groups/Edit.cshtml` | 62 | 1 |
  | `Groups/PostDetail.cshtml` | 163, 306, 364, 432 | 4 |
  | `Announcement/New.cshtml` | 109 | 1 |
  | `Announcement/Edit.cshtml` | 109 | 1 |
  | `Announcement/Detail.cshtml` | 155 | 1 |
  | `Languages/PreviewPage.cshtml` | 50 | 1 |

- **Button set (verified from `Posts/New.cshtml` toolbar, in order):**
  `bold`, `italic`, `code`, `h1`, `h2`, `h3`, `ul`, `ol`, `link`, `image`
  (10 `data-md` buttons; image-gated surfaces drop `image` per RE matrix —
  the toggle is appended after whichever is last).
- **Binder exports (verified from `rich-editor.ts`):** 6 pure/predicate
  functions (`renderPreview`, `applyToggle`, `applyBlock`, `applyLink`,
  `imageLink`, `isSafeImageSrc`) + the `bindRichEditor` binder. The self-wire
  loop (`document.querySelectorAll('.rc-editor')`) calls `bindRichEditor` per
  editor. U1 extends `bindRichEditor` with the toggle wiring; no pure
  function is touched.
- **Closed `rc.editor.*` key set (verified from `KnownTranslationKeys.cs`
  lines 630–645):** `bold`, `italic`, `code`, `h1`, `h2`, `h3`, `list`,
  `olist`, `link`, `image`, `preview` (11 keys). The closed-set comment is
  at line 630. U3 appends `rc.editor.source` + `rc.editor.showPreview`.
- **`package.json` (verified):** `typescript`-only (`"typescript": "^7.0.2"`)
  — RE·3 holds at IE lane open.
- **`<kw-l>` server-rendered (verified):** `LocalizeTagHelper` renders
  server-side; a client-side `key` attribute swap does **not** live-update
  the button label. U1's binder must set the button's text content directly
  as the fallback (the `lastChild.textContent` swap). The `key` attribute
  swap is still done (it is the pin) but the label update path is the
  textContent fallback.

**No drift pause.** All assumptions verified. The "15 blocks" in the
register is an arithmetic error (8+4+4=16); the file count (10) and
per-file breakdown are correct. U2 works against the 16-block / 10-file
list above.

## U1 — Drift pause

- **Seam:** the toggle's **pane behavior** — whether the rendered pane
  **hides** when the source is shown, or **stays visible** (split view).
- **What the U1 plan code does (what I implemented):** toggles ONLY the
  textarea's visibility (via the `rc-editor-source-hidden` class) and
  adds a **no-op** `rc-editor-pane-active` marker class to the pane (no CSS
  rule for it — the design doc's CSS pin + the U1 plan both explicitly
  call it a "no-op marker," so I added only the load-bearing
  `.rc-editor textarea.rc-editor-source-hidden { display: none; }` rule).
  Net effect: when the source is shown, the resident sees **both** the
  source textarea and the rendered pane (a split view).
- **What the primary tier says (the outlier):** the design doc's
  **IE2 FACES** row reads "clicking the toggle reveals the source
  `<textarea>` with the raw markers + **the pane hides**," and the
  register's **D1** reads "The toggle button shows the source and **hides
  the pane**." Both pin that the pane **hides** when the source is shown.
- **The inconsistency is internal to the design doc (authored in U0):**
  IE2 says "the pane hides," but the design doc's own **CSS pin** says the
  `rc-editor-pane-active` marker is a **no-op** (empty body) — i.e. the
  pane stays visible. The U1 plan's code + CSS pin are **consistent** with
  the "pane stays visible" reading; the IE2 FACES wording is the outlier.
- **Test impact:** the 3 IE seam tests + the artifact pin (U4) do **not**
  disambiguate — none assert the pane's visibility. Both behaviors pass.
- **NEEDS A DECISION (U1 is blocked on this):**
  - **(a) pane hides** when source shows — matches IE2 + register D1; a
    clean visibility switch. Requires adding a CSS rule
    `.rc-editor-pane.rc-editor-pane-active { display: none; }` (the
    `paneActive` class the code already toggles becomes the hide hook).
  - **(b) pane stays visible** (split view) — matches the U1 plan code +
    the CSS pin (as-is); the design doc IE2 wording is corrected to
    "the pane stays visible as a live reference."
  - **Evidence weight:** "pane hides" is stated **explicitly** in two
    places (register **D1**: "shows the source and hides the pane";
    design doc **IE2**: "the pane hides") **plus** the user prompt's
    framing of the toggle as "one button that **switches views**" (a
    switch reads as A/B, one view at a time). "pane stays visible" is
    only the *effect of an omission* — the U1 plan code + the CSS pin
    never implement the pane-hide that the two intent docs call for;
    `paneActive` is left a no-op marker. So **3 explicit statements vs.
    2 omissions** → the weight of evidence favors **(a) pane hides**.
  - **My lean: (a) pane hides** — a clean A/B visibility switch is the
    most consistent reading of the register D1 + IE2 + "switches views."
    Under (a), the one-line CSS change is
    `.rc-editor-pane.rc-editor-pane-active { display: none; }` (the
    `paneActive` class the code already toggles becomes the hide hook),
    and IE2 stays as written.
  - **RESOLVED (2026-09-15):** **(b) split-view (pane stays visible)**
    is authoritative. The code already implements this correctly (the
    `paneActive` class is toggled as a no-op marker; no CSS rule is
    needed for it). The design doc IE2, the register D1, and the
    design doc's toggle-contract table have been updated to match.
    U1's drift pause is **resolved** — U1 proceeds to its exit
    (build + handoff section + move).

## U1 — Module toggle

- **Binder toggle block (additive, after `renderPane()` + the `input`
  listener, before the `button[data-md]` loop):** finds
  `button[data-ie-toggle]`; defines `setView(showSource)` which
  toggles the textarea's `rc-editor-source-hidden` class (and the
  pane's no-op `rc-editor-pane-active` marker) + swaps the `<kw-l>`'s
  `key` + label; calls `setView(false)` on init (IE·1 default = source
  hidden); wires the `click` to `setView(!isHidden)`. Guarded by
  `if (toggle)` — a view without the button is a no-op.
- **CSS rule added:** `src/Kumunita.Web/wwwroot/css/site.css` (the
  load-bearing rule, appended after `.rc-editor textarea[data-rich-editor]`):
  `.rc-editor textarea.rc-editor-source-hidden { display: none; }`. The
  optional `.rc-editor-pane.rc-editor-pane-active` no-op marker is
  **not** added (the design doc does not pin it; split-view (b) needs
  no pane rule).
- **Two `<kw-l>` keys the button references:** `rc.editor.source` +
  `rc.editor.showPreview` (U3 registers both).
- **Label-swap path (the load-bearing one):** the `<kw-l>` element's
  `textContent` is set directly (e.g. `Preview` / `</>`). The `<kw-l>`
  `key` attribute is also swapped (the pin for HTML-source correctness +
  a11y). **Correction to U0's note:** `toggle.lastChild` is a trailing
  **whitespace text node**, not the `<kw-l>` element — the code targets
  the `<kw-l>` element itself (`toggle.querySelector('kw-l')`), not
  `lastChild`. The `<kw-l>` is server-rendered (LocalizeTagHelper), so a
  `key` change does **not** live-update; the textContent swap is what
  the resident sees.
- **Byte-identity confirmed:** the 6 pure functions
  (`renderPreview` / `applyToggle` / `applyBlock` / `applyLink` /
  `imageLink` / `isSafeImageSrc`) + the existing binder wiring (the
  `data-md` loop, the `noImage` removal, the `image` upload branch, the
  self-wire loop) are **byte-identical** in the diff — only the toggle
  block + the CSS rule are added. No new `export`.
- **Build:** `dotnet build Kumunita.slnx -c Debug` **green** +
  `npm run build` **green**; the compiled
  `wwwroot/js/lib/rich-editor.js` contains both `data-ie-toggle` and
  `rc-editor-source-hidden`. No `.csproj` / `package.json` change (RE·3
  holds).

## U2 — Toggle buttons

- **10 view files, 16 toggle buttons** (1 each, except
  `Posts/Detail.cshtml` = 4 and `Groups/PostDetail.cshtml` = 4):
  `Posts/New`, `Posts/Edit`, `Posts/Detail`(4), `Groups/New`,
  `Groups/Edit`, `Groups/PostDetail`(4), `Announcement/New`,
  `Announcement/Edit`, `Announcement/Detail`, `Languages/PreviewPage`.
- **Button markup (verbatim, the design doc pin):**
  `<button type="button" class="rc-btn" data-ie-toggle><kw-l
  key="rc.editor.source">&lt;/&gt;</kw-l></button>` — appended after the
  last `data-md` button (the `image` button) in each toolbar.
- **Existing buttons byte-identical in the diff:** `git --no-pager diff
  --stat` shows 10 files changed, 16 insertions(+), 0 deletions(-) —
  perfectly additive.
- **No drift:** all 10 files' toolbar shapes match the
  `Posts/New.cshtml` representative (same button set, same indentation
  pattern per file). No file's toolbar shape differs.
- **Build:** `dotnet build Kumunita.slnx -c Debug` **green** (Razor
  compile).

## U3 — kw-l keys

- **Two keys registered** in `KnownTranslationKeys.cs` (appended to the
  existing `rc.editor.*` block, after `rc.editor.preview`):
  `["rc.editor.source"] = "</>"` + `["rc.editor.showPreview"] = "Preview"`.
  The closed-set comment above the block now names them (`+ IE (ADR 0032):
  rc.editor.source + rc.editor.showPreview`).
- **Design doc agrees:** U0's design doc already pinned exactly
  `rc.editor.source` = `</>` / `rc.editor.showPreview` = `Preview` — the
  second deliverable (doc update) is a **no-op** (recorded here).
- **Existing 11 keys byte-identical** in the diff (values unchanged, no
  re-ordering).
- **Key-registry completeness test:** `MLUI_FacesTests.cs` (both
  `Kumunita.Core.Tests` + `Kumunita.Web.Tests`) use
  `KnownTranslationKeys.AllKeys` **dynamically** (not a hardcoded count),
  so the 2 new keys are picked up automatically — no test changes needed.
  U4's run is the confirmation.
- **Build:** `dotnet build Kumunita.slnx -c Debug` **green** (the
  `Kumunita.Core` compile).

## U4 — Seam tests + gate

- **4 IE artifact tests landed** in `tests/Kumunita.Web.Tests/InlineEditorTests.cs`
  (new) + the 1 RE-regression pin, exact names:
  `CompiledRichEditorJs_ContainsToggleButton`,
  `CompiledRichEditorJs_ContainsSourceHiddenClass`,
  `RichEditorTextarea_IsNotDisabled_OrRemoved`,
  `CompiledRichEditorJs_StillExportsRePureFunctions`. No
  `RichEditorSpec`-style mirror (IE adds no pure functions). The
  `CandidateArtifactPaths()` helper mirrors `RichEditorTests` verbatim.
- **Absence-needles (the #3 test, scoped to the textarea):**
  `textarea.disabled`, `textarea.remove(`, `textarea.hidden` — all
  confirmed **absent** from the compiled `rich-editor.js` (the textarea is
  hidden via the `rc-editor-source-hidden` CSS class / `classList`, never
  disabled/removed/hidden-attribute).
- **Acceptance gate (the `dotnet exec` exit codes):**
  `dotnet build Kumunita.slnx -c Debug` **green** (0 errors);
  `npm run build` **green** (the JS artifact is fresh);
  `Kumunita.Web.Tests.dll` → **Total: 153, Errors: 0, Failed: 0** (the 4
  new IE tests + the 10 RE tests + RC/M3/etc. — **no regression**; the
  `MLUI_FacesTests` key-registry checks pass with the 2 new keys);
  `Kumunita.Core.Tests.dll` → **Total: 400, Errors: 0, Failed: 0**
  (Testcontainers/postgres; containers cleaned up on exit). **Both exit
  codes 0.**
- **No test deviation** from the design doc's pin (all 4 test names +
  needles match §Pinned seam tests exactly).

## U5 — ADR 0032 + close

- **ADR 0032 authored** (`docs/adr/0032-inline-editor-rendered-default-view.md`):
  **Status: Accepted**; **Amends 0031** (D1's *layout* — the split view with
  all three rows visible; the `tsc`-only / no-`contenteditable` /
  textarea-is-source-of-truth stance all keep binding). Settles **D1**
  (rendered-by-default, source on demand — **pane stays visible as a split
  view when the source is revealed**, per the resolved (b) drift pause),
  **D2** (one button, one `data-ie-toggle` attribute, one additive
  `bindRichEditor` block — the six RE pure functions byte-identical), **D3**
  (additive only: no route, no `AccessAction`, no stored field, no new
  dependency, no new server surface, no second renderer). Named deferrals
  carried from the design doc: localStorage persistence, keyboard shortcut,
  caret mapping, mobile toggle UI, true `contenteditable` (hard out).
- **ADR index** (`docs/adr/README.md`): 0032 row appended after 0031 (current
  highest confirmed 0031; shape mirrors the 0031 row).
- **README Roadmap** (`README.md`): the `IE` Done line appended after the
  `RE` line (mirrors the RE line's shape). **No** `Milestones.cs` /
  `MilestonesTests.cs` change — the IE lane is a named lane, not a roadmap
  letter (the register's U5 explicitly does not move a roadmap letter).
- **Doc-only:** no code, test, CSS, or `.csproj` / `package.json` change in
  U5.
- **Folder move (last step):** the remaining unit plan file
  (`inline-editor-u05-plan.md`) + this handoff note moved
  `in-progress/` → `done/` (u00–u04 already there). The register
  `plan-inline-editor.md` **stays** in `plans-milestones/`. **Lane closed.**
