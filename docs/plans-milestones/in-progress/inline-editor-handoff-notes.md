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
  - I will **not** silently pick either way (drift-pause protocol + this
    touches the IE2 FACES pin). Confirm **(a)** or **(b)** and I will
    align the code + design doc + register wording in one pass, then
    finish U1's exit (build + handoff + move).
