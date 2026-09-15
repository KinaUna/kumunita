# WYSIWYG inline editing (`WY`) — the rendered pane is the editable surface; the Markdown source becomes a read-only mirror

> **Status.** This is the **lane register** (secondary tier of the lane's
> three-tier contract) for a follow-on lane to the **inline-editor lane
> (`IE`, ADR 0032)** and the **rich-editor lane (`RE`, ADR 0031)** that
> together shipped the split-view authoring surface (toolbar + source
> `<textarea>` + rendered preview pane on 10 composer surfaces). IE made
> the rendered pane the **default view** and the source **hidden** behind a
> toggle. This lane completes the arc from the resident's point of view:
> the rendered pane becomes **the editable surface** — click it, type it,
> format it — and the Markdown source becomes a **read-only mirror** (the
> "code view") behind the same toggle. **M4/M5/M6 stay Events / Projects /
> Portability** — no roadmap letter moves.

## The gap this lane closes

- **The rendered pane is not editable.** IE (ADR 0032) shipped the
  rendered-by-default experience — a resident opens a composer and sees
  *what they will publish* filling the surface. But the pane is **read-only**;
  the only way to edit is to click the `</>` toggle, reveal the raw Markdown
  `<textarea>`, and hand-type `**bold**` / `## Heading` / `![alt](/content-image/…)`
  there. That is the exact inverse of what Gmail/Outlook do: a resident
  clicks the rendered text, puts the caret in it, and types. The toggle
  exists, but it is the **only** edit path, and it lands the resident on raw
  markers — the thing IE was meant to hide.
- **There is no WYSIWYG editing path.** A resident who does not know Markdown
  cannot bold a word, start a list, or insert a link *in the rendered text*.
  The toolbar (RE D2) splices Markdown into the hidden textarea; the resident
  watches the pane update, but they never typed in the pane. The promise the
  platform makes — *"a resident writes what they see"* — is met for *seeing*,
  not for *writing*.

This lane closes exactly that gap with **one architectural change** (the pane
becomes `contenteditable`) and **one new pure function** (a DOM→Markdown
serializer, the inverse of `renderPreview`). The Markdown source stays the
single source of truth the server binds (RC R·3 / RE·1, **unchanged**); the
pane becomes the editable surface; the serializer keeps the two in sync; the
code view (the `</>` toggle) becomes a **read-only mirror** of the pane.

**The one thing every unit must respect:** this lane **deliberately reverses
the "hard non-negotiable" in ADR 0031 D1 / ADR 0032** — *"true `contenteditable`
/ a third-party editor — hard non-negotiable out"*. The user has explicitly
approved this reversal (2026-09-15). The new ADR (**0033**) records the
reversal and **what still binds**: `Body` stays a Markdown `string` (RC R·7),
the `tsc`-only constraint (RE·3 / RC), **no editor dependency** (`package.json`
still `typescript`-only), **one renderer on the read path** (RC R·1 —
`MarkdownRenderer` is untouched), and the saved body is **byte-identical**
Markdown (RC R·3). What changes is the **composer surface only**: the pane
is now `contenteditable`, the textarea is now a read-only mirror, and there
is a new hand-rolled, pure, unit-tested **DOM→Markdown serializer** (the
inverse of `renderPreview`) + a **sanitizer** for paste. **No** `.csproj`
change, **no** `package.json` change, **no** new route, **no** new server
surface, **no** second renderer on the read path.

**The design decisions (settled — D1, D2, D3; this is this lane's ADR
payload, ADR 0033):**

- **D1 — The pane is the editing surface; the textarea is the read-only
  sink.** The `rc-editor-pane` (the same element IE made the default view)
  carries `contenteditable="true"` and becomes the **editable** surface.
  The `<textarea data-rich-editor>` is **not removed, not disabled, not
  re-shaped** — it stays the live form field the server binds (RC R·3 /
  RE·1 unchanged), but the binder now **keeps it in sync** as a
  **read-only mirror**: on every pane `input`, the binder serializes the
  pane's constrained HTML to Markdown and writes it into the textarea
  (`.value = toMarkdown(pane.innerHTML)`). On load, the binder populates
  the pane from the textarea (`pane.innerHTML = renderPreview(textarea.value)`).
  The pane is **authoritative**; the textarea is the **sink** the server
  reads on submit. **No bidirectional sync** — the textarea is never
  typed into; it is only *written by the binder*.
- **D2 — One new pure function: `toMarkdown(html): string`.** A
  dependency-free, pure, unit-tested **DOM→Markdown serializer** in
  `client/lib/dom-to-markdown.ts` that is the **inverse of
  `renderPreview`** — it emits **exactly** the RC-pinned subset
  (headings 1–6, paragraphs, `- `/`* ` lists, `1. ` lists, fenced code,
  `` `code` ``, `**bold**`/`*italic*`, `[label](url)`,
  `![alt](/content-image/{hex})`) and **nothing more**. It is the load-
  bearing new artifact; it must be **pure** (no DOM, no side effects) so
  it is unit-testable without a browser, and **round-trip-stable** with
  `renderPreview` (the WY·10 / WY5 invariant).
- **D3 — A sanitizer for paste + raw HTML.** The pane is
  `contenteditable`; a resident can **paste** rich HTML (from Gmail,
  Word, etc.) that contains elements/attributes outside the WY·3 subset
  (`<span style=…>`, `<div>`, `<script>`, `onerror=`, …). The binder
  installs a `paste` handler that **sanitizes** the clipboard HTML down
  to the WY·3 subset **before** inserting it (the escape-first /
  IsSafeImageSrc / IsSafeUrl semantics already in `rich-editor.ts` are
  reused). This is **not** a second renderer; it is a **DOM-shape
  normalizer** so `toMarkdown` only ever sees the constrained subset.

**The `tsc`-only constraint stands unchanged.** What this ADR changes is
the **composer surface only**: the pane is now editable, the textarea is
now a read-only mirror, and there is a new pure serializer + sanitizer.
There is still **no editor dependency in `package.json`**, still **no
`.csproj` change**, and the saved body is **byte-identical** to what a
resident could have hand-typed in the code view — RC's server-side
`ContentImageIds` parse and `MarkdownRenderer` render are **untouched**.

## Assumptions

- **The reversal is approved and recorded.** The user explicitly approved
  reversing ADR 0031 D1 / ADR 0032's "hard non-negotiable" (2026-09-15).
  ADR 0033 records the reversal and what still binds. **This is not a
  deferral; it is a new policy.**
- **`Body` stays a Markdown `string`.** RC R·7 is unchanged. The server
  still derives `ImageIds` by parsing the *body text* (RC R·3); the read
  path still runs `MarkdownRenderer` (RC R·1). **Zero server change.**
- **The textarea is never removed or disabled.** It stays the live form
  field the server binds (RC R·3 / RE·1). It is hidden in the DOM (a CSS
  class, as IE already does) but **present in the form** — the server
  reads `Body` from it on submit, exactly as today.
- **The pane is the same element IE made the default view.** The
  `rc-editor-pane` (the `data-rich-editor-preview` element) is the one
  that becomes `contenteditable`. No new element, no re-shape of the
  `.rc-body` / `.rc-image` CSS (RC frozen base).
- **The serializer is the load-bearing artifact.** It is pure, unit-
  tested, and round-trip-stable with `renderPreview`. It is the inverse of
  `renderPreview` — the same subset, the same escape-first construction,
  the same `IsSafeImageSrc` / `IsSafeUrl` semantics.
- **The sanitizer is a DOM-shape normalizer, not a renderer.** It strips
  pasted HTML to the WY·3 subset. It reuses the escape-first / IsSafeImageSrc
  / IsSafeUrl semantics already in `rich-editor.ts`. It does **not** add
  a second renderer or a new dependency.
- **The toolbar is reworked, not re-shape.** The same buttons (B, I, C,
  H1, H2, H3, •, 1., Link, Image) stay; their click handlers change from
  "splice Markdown into the textarea" to "splice DOM into the pane" (the
  Selection / Range API on the `contenteditable`). The image button reuses
  the **RC upload lane** (`POST /content-image` via `apiFetch`, the
  `insert-image.ts` convention — **no** new route, **no** second upload).
- **The code view (the `</>` toggle) becomes a read-only mirror.** The
  `data-ie-toggle` button (IE, ADR 0032) is kept; its semantics change
  from "reveal the editable source" to "reveal the **read-only** Markdown
  mirror". The label swap (`</>` / `Preview`) is kept.
- **The 10 composer surfaces are unchanged in shape.** They keep the
  same `.rc-editor` wrapper, the same toolbar, the same textarea, the
  same pane. The only markup change is the pane gains
  `contenteditable="true"` (added by the binder at runtime, **not** in the
  Razor — the binder is the single place that knows the pane is editable).
- **The test model is unchanged.** The artifact-string pins (the
  `InlineEditorTests` / `RichEditorTests` shape) are extended: the
  compiled JS contains `contenteditable` + `toMarkdown` + the sanitizer,
  the textarea is **not** disabled/removed/hidden-by-attribute, and the 6
  RE exports + the new `toMarkdown` export are present. The round-trip
  property (WY·10) is pinned as a **pure-function** test (no browser
  needed — `toMarkdown` is pure).
- **The PowerShell / terminal constraints in `AGENTS.md` and
  `copilot-instructions.md` bind** — no here-strings, no multi-line
  terminal commands, `$`-variables don't survive between commands, the
  `dotnet test` discovery bug on this machine (use the in-process
  `dotnet exec tests\…\bin\Debug\net10.0\*.dll` path).

## Approach

One track, **client-only, additive**, sequenced. **U0** verifies the
surface + the RC subset. **U1/U2** author the primary-tier design doc
(invariants + FACES, then the exact TS shapes + the pinned test names +
the acceptance gate + the drift guard) and draft **ADR 0033**. **U3**
implements the pure serializer + its unit tests (the load-bearing
artifact). **U4** implements the editing loop (the pane becomes
`contenteditable`; the binder keeps the textarea in sync). **U5** reworks
the toolbar to splice DOM. **U6** implements the sanitizer + the `paste`
handler. **U7** reworks the code view (the `</>` toggle reveals the
read-only mirror). **U8** runs + records the acceptance gate. **U9**
closes the lane (ADR 0033 → Accepted + the ADR index + the
`ARCHITECTURE.md` flip + the handoff `## Summary`).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug` + `npm run build` in `src/Kumunita.Web`). The last unit (U9)
appends the final handoff section so the lane is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U0–U9 below),
one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/wysiwyg-editor-design.md`,
  U1/U2 author) — pins the invariants (WY·1–WY·9), the FACES (WY1–WY10),
  the exact DOM contract, the serializer contract, the sanitizer contract,
  the pinned test names, the acceptance gate, and the drift guard.
- **Secondary — this file** (`docs/plans-milestones/plan-wysiwyg.md`) —
  the unit registry with each unit's deliverables and exit criteria.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md`) — one
  section per unit, appended (never rewritten). Each unit writes exactly
  one short section before it exits; the next unit reads only that
  section + its own entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal
file list, 3–5 files < ~300 lines each, no full-repo scan; the design-doc
section cited is named); **Deliverables** (a closed set of new/modified
files, ≤ ~4 files / ~600 LOC, no misc cleanups); **Exit** (`dotnet build`
+ `npm run build` green for the touched projects; handoff-note entry
appended *before* any follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the §pinned-test-names
list; (4) never re-shapes the serializer contract (the WY·3 subset)
outside the design doc; (5) never adds an element/attribute outside the
WY·3 subset to the pane (WY·3); (6) never re-shapes the textarea (RC R·3 /
RE·1 — it stays the live form field the server binds); (7) if entry reads
reveal the design doc is out of date, the unit pauses and records
`## U<m> — Drift pause` in the handoff note.

---

## Units (10 total: U0–U9)

### U0 — Kickoff verification

- **Goal:** verify the surface (the 10 composer surfaces / the
  `rc-editor` blocks / the pane element / the toolbar / the textarea /
  the `data-ie-toggle` button) and the RC-pinned subset (the `renderPreview`
  mirror + the `MarkdownRenderer` C#) are as the design doc will pin them.
  **No code, no build.**
- **Entry reads:** `src/Kumunita.Web/Views/` (grep `class="rc-editor"` +
  `data-rich-editor-preview` + `data-ie-toggle` to count the surfaces);
  `src/Kumunita.Web/client/lib/rich-editor.ts` (the full module — the
  `renderPreview` subset + the 6 pure functions + `bindRichEditor` +
  the IE toggle block); `src/Kumunita.Web/Security/MarkdownRenderer.cs`
  (the C# renderer — the reference of record for the subset);
  `src/Kumunita.Web/Security/ContentImageIds.cs` (the `ImageIds` parse the
  serializer must stay byte-compatible with); `docs/adr/0031-wysiwyg-editor-
  and-toolbar.md` + `docs/adr/0032-inline-editor-rendered-default-view.md`
  (the two ADRs this lane reverses / builds on); `docs/adr/0030-role-
  independence-composable-elevated-roles.md` (the **current highest** ADR —
  0033 is the next).
- **Deliverables (1 file, new):** `docs/plans-milestones/in-progress/
  wysiwyg-handoff-notes.md` — the **skeleton only** (the header + the
  "Lane open" section + the `<!-- U0 appends its section below this line.
  One ## section per unit, in order (U0, U1, … U9). Never rewrite a prior
  section. -->` marker). The skeleton mirrors the
  `inline-editor-handoff-notes.md` shape (the "Lane open" section names
  the register, the design doc, the ADR, the scope, the out-of-scope
  deferrals, and the frozen base). **No** `## U<m> —` section yet (U0
  appends its own section after this one).
- **Exit:** the handoff-note skeleton is present. The `## Lane open`
  section names (a) the 10 composer surfaces (the grep count), (b) the
  RC-pinned subset (the `renderPreview` mirror list), (c) the frozen base
  (RC R·1–R·7 + RE·1–RE·3 + IE·1 — **unchanged**), (d) the one new
  invariant (WY·1–WY·9 — the pane is editable; the textarea is the read-
  only sink; the serializer is the inverse of `renderPreview`; the saved
  body is byte-identical Markdown), (e) the **ADR 0033 reversal** (the
  "hard non-negotiable" in ADR 0031 D1 / ADR 0032 is **reversed** by user
  approval 2026-09-15; the new ADR records the reversal + what still binds).
  Handoff note: a `## U0 — Kickoff verified` section with the grep counts
  + the RC subset list + the ADR number (0033) + the user-approval date
  (2026-09-15). Move this plan file `in-progress/` → `done/` (move
  **last**). `git status` clean except the one new handoff-note file.

### U1 — Design doc Part 1 (invariants + FACES)

- **Goal:** author `docs/design/wysiwyg-editor-design.md` Part 1 — the
  value chain, the **invariants (WY·1–WY·9)**, the **FACES (WY1–WY10)**,
  and the **assumptions** (the ADR 0033 reversal + what still binds).
  Mirrors the `inline-editor-design.md` / `rich-editor-design.md` shape.
  **No code, no build.**
- **Entry reads:** U0's handoff-note `## U0 — Kickoff verified` section
  (the grep counts + the RC subset list), `docs/design/inline-editor-
  design.md` (the IE design doc — the FACES/invariant template to emulate),
  `docs/design/rich-editor-design.md` (the RE design doc — the
  `renderPreview` subset + the toolbar marker ceiling),
  `docs/philosophy/templates/design-doc.md` (the required section set),
  `src/Kumunita.Web/Security/MarkdownRenderer.cs` (the C# renderer —
  the reference of record for the subset the serializer must emit),
  `src/Kumunita.Web/client/lib/rich-editor.ts` (the `renderPreview`
  function — the inverse the serializer must be).
- **Deliverables (1 file, new):** `docs/design/wysiwyg-editor-design.md`
  (~250 lines). Sections:
  - `## Value chain` — IE shipped the rendered-by-default experience;
    WY ships the **editable** rendered experience. The arrow moves from
    *"the resident sees what they will publish"* to **"the resident
    writes what they see"** — the Gmail/Outlook model.
  - `## Context` — the gap (the pane is read-only; the only edit path is
    the `</>` toggle → raw Markdown; a resident who does not know Markdown
    cannot bold / list / link *in the rendered text*); the ADR 0033
    reversal (the "hard non-negotiable" in ADR 0031 D1 / ADR 0032 is
    reversed by user approval 2026-09-15; the new ADR records the
    reversal + what still binds); the constraints that still bind
    (RC R·1–R·7, RE·1–RE·3, IE·1, `tsc`-only, no editor dependency,
    one renderer on the read path, `Body` as a Markdown `string`).
  - `## Scope` — **In:** the pane becomes `contenteditable`; the
    serializer (`toMarkdown`) + its unit tests; the editing loop (the
    binder keeps the textarea in sync); the toolbar rework (splice DOM,
    not Markdown); the sanitizer + the `paste` handler; the code view
    rework (the `</>` toggle reveals the read-only mirror); the ADR 0033
    (reversal + what still binds); the design doc (this file); the
    artifact-string pins (the compiled JS contains `contenteditable` +
    `toMarkdown` + the sanitizer; the textarea is not disabled/removed;
    the 6 RE exports + the new `toMarkdown` export are present); the
    round-trip property (WY·10) as a pure-function test. **Out (named
    deferrals for a future WY-2 lane, if one comes):** nested lists,
    blockquotes, tables, footnotes, strikethrough, `execCommand`-based
    undo/redo (the browser's native `contenteditable` undo is the floor;
    a custom undo/redo is a future lane), mobile-specific editing UX,
    caret-mapping between the pane and the code view, localStorage
    persistence of the editing preference. Each is a **future** lane,
    not a WY re-open.
  - `## Invariants (pinned for WY)` — **WY·1–WY·9**, each with a one-
    line WY note:
    - **WY·1** — the pane is the editing surface (`contenteditable="true"`).
    - **WY·2** — the textarea is the read-only sink (RC R·3 / RE·1
      unchanged; never removed, disabled, or re-shaped; the server binds
      it on submit).
    - **WY·3** — the serializer emits **exactly** the RC-pinned subset
      (the WY·3 subset: P, H1–H6, UL/OL, LI, STRONG, EM, CODE, A, IMG,
      PRE/CODE) and **nothing more** (the inverse of `renderPreview`).
    - **WY·4** — the toolbar splices **DOM** (not Markdown) into the
      pane (the Selection / Range API on the `contenteditable`).
    - **WY·5** — the saved body is **byte-identical** to what a resident
      could have hand-typed in the code view (RC R·3 / RC R·1 unchanged;
      the read path is untouched).
    - **WY·6** — paste / raw HTML is **sanitized** to the WY·3 subset
      before insertion (the escape-first / IsSafeImageSrc / IsSafeUrl
      semantics already in `rich-editor.ts` are reused).
    - **WY·7** — the code view (the `</>` toggle) is a **read-only
      mirror** of the pane (the textarea's `.value` is the serialized
      Markdown; the label swap is kept).
    - **WY·8** — the `tsc`-only constraint stands unchanged (no editor
      dependency in `package.json`; no `.csproj` change).
    - **WY·9** — a11y: the pane is keyboard-operable, reachable in tab
      order, and carries `role="textbox"` + `aria-multiline`; the toolbar
      buttons stay `<button type="button">` + localized via `<kw-l>`.
  - `## FACES (pinned, 10)` — **WY1–WY10**, each bound to an invariant:
    - **WY1** — the pane is the editing surface (WY·1, WY·9)
    - **WY2** — typing in the pane keeps the textarea in sync (WY·2)
    - **WY3** — formatting via the toolbar splices DOM (WY·4)
    - **WY4** — image insert via the RC upload lane (WY·3, WY·4)
    - **WY5** — the saved body is byte-identical (WY·5, RC R·3, RC R·1)
    - **WY6** — paste is sanitized to the WY·3 subset (WY·6)
    - **WY7** — the code view is a read-only mirror (WY·7)
    - **WY8** — image-gating is unchanged (the RE `data-rich-editor-
      no-image` precedent)
    - **WY9** — a11y (WY·9)
    - **WY10** — the round-trip property: `toMarkdown(renderPreview(md))
      === md` for any `md` in the RC-pinned subset (WY·3, WY·5)
- **Exit:** the file exists with all sections. **No build.** Handoff note:
  a `## U1 — design doc Part 1` section listing the **9 invariants** (by
  id) and the **10 FACES** (WY1–WY10) so U2 can pin them by id.

### U2 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard)

- **Goal:** append `## Seams & contracts (Part 2, written by U2)` to the
  design doc — the exact TS shapes U3–U7 must match, the DOM contract,
  the serializer contract, the sanitizer contract, the **pinned seam-test
  names**, the **acceptance gate**, and the **drift-guard**. Plus **ADR
  0033** (draft). **No code, no build.**
- **Entry reads:** U1's Part 1 (the invariant table is the primary source),
  `docs/design/inline-editor-design.md` §Pinned contract (the shape to
  emulate), `src/Kumunita.Web/client/lib/rich-editor.ts` (the `renderPreview`
  function — the inverse the serializer must be; the 6 pure functions —
  the pattern the serializer follows), `src/Kumunita.Web/Security/
  MarkdownRenderer.cs` (the C# renderer — the reference of record),
  `src/Kumunita.Web/Security/ContentImageIds.cs` (the `ImageIds` parse
  the serializer must stay byte-compatible with), `docs/adr/0032-inline-
  editor-rendered-default-view.md` (the ADR shape to emulate for 0033).
- **Deliverables (2 files, new):**
  1. **`docs/design/wysiwyg-editor-design.md`** (append Part 2).
     Sub-sections:
     - `### 2.1 frozen base (unchanged)` — RC R·1–R·7 + RE·1–RE·3 +
       IE·1 + `tsc`-only + no editor dependency + `Body` as a Markdown
       `string` — all **keep binding unchanged**.
     - `### 2.2 the DOM contract (exact)` — the `rc-editor-pane`
       (the `data-rich-editor-preview` element) carries
       `contenteditable="true"` (added by the binder at runtime). The
       textarea (`textarea[data-rich-editor]`) is **not** re-shaped;
       it stays the live form field the server binds. The toolbar
       (`.rc-editor-toolbar`) is **not** re-shaped; the buttons stay
       `<button type="button" data-md="…">`. The `data-ie-toggle`
       button is **not** re-shaped; its semantics change (the code view
       is now a read-only mirror). The pane's initial state is
       `pane.innerHTML = renderPreview(textarea.value)` (the binder
       populates it on load). The pane's `input` handler is
       `textarea.value = toMarkdown(pane.innerHTML)` (the binder
       keeps the textarea in sync). The toolbar's `click` handlers
       splice DOM (the Selection / Range API) into the pane, then call
       `textarea.value = toMarkdown(pane.innerHTML)` (the binder keeps
       the textarea in sync).
     - `### 2.3 the serializer contract (exact TS)` — the
       `toMarkdown(html: string): string` function in
       `client/lib/dom-to-markdown.ts`. **Pure** (no DOM, no side
       effects — it takes the pane's `innerHTML` as a string and
       returns a Markdown string). **The subset** (the WY·3 subset):
       `<p>`, `<h1>`–`<h6>`, `<ul>`/`<ol>`, `<li>`, `<strong>`,
       `<em>`, `<code>`, `<a>`, `<img>`, `<pre><code>`. **The
       mapping** (each element → its Markdown form, the inverse of
       `renderPreview`): `<p>` → the inline content + `\n\n`;
       `<h1>`–`<h6>` → `#`–`######` + ` ` + the inline content +
       `\n\n`; `<ul>`/`<ol>` → `- ` / `1. ` per `<li>` + `\n\n`;
       `<li>` → the inline content (no nesting — WY·3); `<strong>`
       → `**` + the inline content + `**`; `<em>` → `*` + the
       inline content + `*`; `<code>` → `` ` `` + the text + `` ` ``;
       `<a href="…">` → `[` + the inline content + `](` + the href
       + `)`; `<img src="/content-image/{id}" alt="…">` → `!` + `[`
       + the alt + `](` + the src + `)`; `<pre><code class="language-
       {lang}">` → ` ```{lang}` + `\n` + the text + `\n` + ` ``` ` +
       `\n\n`. **The escape rule** (the inverse of `htmlEscape`):
       `&amp;` → `&`, `&lt;` → `<`, `&gt;` → `>`, `&quot;` → `"`,
       `&#39;` → `'` (the serializer must un-escape the HTML entities
       before emitting the Markdown). **The edge cases** (the WY·3
       edge cases): (a) a blank `<p></p>` → an empty string (the
       serializer skips it); (b) a heading with no content
       (`<h1></h1>`) → an empty string (the serializer skips it);
       (c) a list with no items (`<ul></ul>`) → an empty string (the
       serializer skips it); (d) a link with no href (`<a>label</a>`)
       → the label as plain text (the serializer emits the inline
       content, not a `[label]()` form); (e) an image with an unsafe
       src (the `isSafeImageSrc` reject) → the whole `<img>` as plain
       escaped text (the serializer reuses the `isSafeImageSrc`
       predicate); (f) a link with an unsafe url (the `isSafeUrl`
       reject) → the label as plain text (the serializer reuses the
       `isSafeUrl` predicate); (g) a `<pre><code>` with no language
       → the fenced form with no language (` ``` ` + `\n` + the text
       + `\n` + ` ``` `); (h) a `<li>` with mixed inline content
       (bold + italic + code) → the inline content in the same order
       as `renderPreview` emits it. **The round-trip property** (the
       WY·10 / WY5 invariant): for any `md` in the RC-pinned subset,
       `toMarkdown(renderPreview(md)) === md` (the serializer is the
       **exact** inverse of `renderPreview` — the same subset, the
       same escape-first construction, the same `isSafeImageSrc` /
       `isSafeUrl` semantics).
     - `### 2.4 the sanitizer contract (exact TS)` — the
       `sanitizeHtml(html: string): string` function in
       `client/lib/dom-to-markdown.ts` (co-located with the
       serializer — the two are the same module). **Pure** (no DOM,
       no side effects). **The rule:** strip every element/attribute
       **outside** the WY·3 subset; keep the WY·3 subset verbatim.
       **The reject list** (the elements/attributes the sanitizer
       strips): `<span style=…>`, `<div>`, `<table>`, `<tr>`, `<td>`,
       `<script>`, `<iframe>`, `<object>`, `<embed>`, `<form>`,
       `<input>`, `<button>`, every `on*` event-handler attribute
       (`onerror`, `onclick`, …), every `style` attribute, every
       `class` attribute (the WY·3 subset has no `class` except
       `language-{lang}` on `<code>` — the sanitizer keeps that
       specific one, strips every other), every `id` attribute, every
       `href` that is not a safe url (the `isSafeUrl` reject), every
       `src` that is not a safe image src (the `isSafeImageSrc`
       reject). **The construction:** the sanitizer is a **single
       pass** over the HTML (a regex-based strip, the same
       construction as `htmlEscape` / `isSafeImageSrc` / `isSafeUrl`
       already in `rich-editor.ts` — the escape-first / allowlist
       semantics). **The output:** a string of HTML that contains
       **only** the WY·3 subset (the serializer's input domain).
     - `### 2.5 the binder contract (exact TS)` — the
       `bindRichEditor` function in `client/lib/rich-editor.ts` is
       **extended** (not re-shaped — the existing RE/IE wiring is
       untouched) with a new **WY block** (`if (pane) { … }`) that:
       (a) sets `pane.contentEditable = 'true'` (the pane becomes
       the editing surface); (b) sets `pane.innerHTML =
       renderPreview(textarea.value)` (the initial population);
       (c) installs the `input` handler (`textarea.value =
       toMarkdown(pane.innerHTML)`); (d) installs the `paste` handler
       (the sanitizer + the insert); (e) reworks the toolbar's
       `click` handlers (the Selection / Range API on the pane,
       then `textarea.value = toMarkdown(pane.innerHTML)`); (f)
       reworks the `data-ie-toggle` button's click handler (the code
       view is now a read-only mirror — the textarea is revealed,
       the pane stays editable + visible). **The existing RE/IE
       wiring** (the 6 pure functions + the `renderPreview` function
       + the IE toggle block + the image upload lane) is **untouched**
       — the WY block is **additive** (the `if (pane) { … }` guard
       means a view that hasn't updated the pane yet still works
       exactly as it did before WY).
     - `### 2.6 the CSS contract (exact)` — the `rc-editor-pane`
       (the `data-rich-editor-preview` element) is **not** re-shaped;
       it stays the same element with the same `.rc-body` / `.rc-image`
       CSS (RC frozen base). The **only** new CSS rule is
       `.rc-editor-pane[contenteditable="true"] { cursor: text;
       outline: 2px solid var(--bs-primary, #0d6efd); outline-offset:
       1px; }` (the a11y focus ring — the pane is keyboard-operable,
       WY·9). The textarea's CSS is **untouched** (it stays hidden in
       the DOM by the IE `rc-editor-source-hidden` class; the WY lane
       does not add a new CSS rule for the textarea).
     - `### 2.7 the pinned seam tests (exact names)` — file
       `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`:
       1. `WY10_RoundTrip_BoldHeadingListLinkImageCode`
       2. `WY3_Serializer_EmitsOnlyThePinnedSubset`
       3. `WY3_Serializer_SkipsBlankElements`
       4. `WY3_Serializer_RejectsUnsafeImageSrc`
       5. `WY3_Serializer_RejectsUnsafeLinkHref`
       6. `WY5_SavedBodyIsByteIdentical`
       7. `WY6_Sanitizer_StripsDisallowedElements`
       8. `WY6_Sanitizer_StripsDisallowedAttributes`
       9. `WY6_Sanitizer_StripsUnsafeHrefs`
       10. `WY7_CodeViewIsReadOnlyMirror`
       11. `WY8_TscOnly_NoEditorDependency`
       12. `WY9_PaneIsKeyboardOperable`
       13. `CompiledRichEditorJs_ContainsContentEditable`
       14. `CompiledRichEditorJs_ContainsToMarkdown`
       15. `CompiledRichEditorJs_ContainsSanitizer`
       16. `RichEditorTextarea_IsNotDisabled_OrRemoved` (RE/IE
           regression pin — **unchanged**)
       17. `RichEditorExports_AreIntact` (RE/IE regression pin — the
           6 RE exports + the new `toMarkdown` export are present)
     - `### 2.8 acceptance gate (U8 records)` — the three
       WY-style tests: **closed loop** (a resident opens a composer,
       types `**bold**` in the pane, saves; the saved body is
       `**bold**`; the read path renders it as `<strong>bold</strong>`;
       the round-trip holds), **handoff** (a resident types a heading
       + a list + a link + an image + a code block in the pane; the
       saved body is the exact Markdown a resident could have hand-
       typed; the read path renders it identically), **part-vs-whole**
       (the 17-test list is the whole; closed-loop + handoff are the
       parts; all must pass together).
     - `### 2.9 drift-guard (frozen once written)` — the 9-invariant
       table (U1), the 10 FACES (U1), the DOM contract (§2.2), the
       serializer contract (§2.3), the sanitizer contract (§2.4), the
       binder contract (§2.5), the CSS contract (§2.6), and the 17
       test names (§2.7) — all frozen pins; any mismatch is a
       `## U<m> — Drift pause` per unit-series rule §6.
  2. **`docs/adr/0033-wysiwyg-inline-editing.md`** — the ADR.
     **Status: Draft (lands in U9)**. **Amends** 0031 (the
     "hard non-negotiable" non-decision — **reversed** by user
     approval 2026-09-15) + 0032 (the rendered-by-default view —
     **kept**, the pane is still the default view; what changes is
     that it is now **editable**). Settles **D1** (the pane is the
     editing surface; the textarea is the read-only sink), **D2**
     (one new pure function: `toMarkdown`), **D3** (a sanitizer for
     paste + raw HTML). States the `tsc`-only constraint **stands
     unchanged**; what changes is the **composer surface only** (the
     pane is editable, the textarea is a read-only mirror, the
     serializer + sanitizer are new). **No** editor dependency in
     `package.json`; **no** `.csproj` change; **no** new route;
     **no** new server surface; **no** second renderer on the read
     path. Numbers after 0032.
- **Exit:** both files present; the design doc's DOM contract +
  serializer contract + sanitizer contract + binder contract + CSS
  contract + test names are stated **concretely** (a fresh agent can
  implement U3–U7 from the doc alone without re-deriving the RC subset).
  ADR 0033 is **present** but its **status is "Draft (lands in U9)"** —
  U9 is the unit that moves it to "Accepted" + does the ADR-index +
  `ARCHITECTURE.md` close. U2 only *authored* it. **No code, test, CSS,
  or `.csproj` change in this unit.** Handoff note: a `## U2 — design doc
  Part 2 + ADR 0033 drafted` section listing (a) the 9 invariants (by id),
  (b) the 10 FACES (WY1–WY10), (c) the 17 test names (by id), (d) the ADR
  number (0033) + its status (Draft), (e) the frozen base (RC R·1–R·7 +
  RE·1–RE·3 + IE·1 — **unchanged**).

### U3 — The serializer + its unit tests (the load-bearing artifact)

- **Goal:** implement `toMarkdown(html: string): string` +
  `sanitizeHtml(html: string): string` in `client/lib/dom-to-markdown.ts`
  (the two pure functions, co-located) + the **pure-function** unit tests
  (the WY3 / WY5 / WY6 / WY10 tests from §2.7 — the ones that do not
  need a DOM). **This is the load-bearing artifact** — the rest of the
  lane builds on it.
- **Entry reads:** `docs/design/wysiwyg-editor-design.md` §2.3 (the
  serializer contract — the **primary** source) + §2.4 (the sanitizer
  contract) + §2.7 (the pinned test names), `src/Kumunita.Web/client/lib/
  rich-editor.ts` (the `renderPreview` function — the inverse the
  serializer must be; the `htmlEscape` / `isSafeImageSrc` / `isSafeUrl`
  functions — the construction to reuse), `src/Kumunita.Web/Security/
  MarkdownRenderer.cs` (the C# renderer — the reference of record),
  `src/Kumunita.Web/Security/ContentImageIds.cs` (the `ImageIds` parse
  the serializer must stay byte-compatible with).
- **Deliverables (≤ 3 files):**
  - `src/Kumunita.Web/client/lib/dom-to-markdown.ts` — the two pure
    functions. `toMarkdown(html: string): string` — the serializer
    (§2.3). `sanitizeHtml(html: string): string` — the sanitizer
    (§2.4). Both are **pure** (no DOM, no side effects). The module
    self-wires at load (the `insert-image.ts` / `rich-editor.ts`
    pattern) **only if** it needs to (the two functions are exported;
    the binder imports them — the module does **not** self-wire a DOM
    handler; it is a **library**). The module exports **only** the two
    functions (`toMarkdown` + `sanitizeHtml`) — no other exports.
  - `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs` — the **pure-
    function** unit tests (the WY3 / WY5 / WY6 / WY10 tests from
    §2.7 — the ones that do not need a DOM). **7 tests:** `WY10_
    RoundTrip_BoldHeadingListLinkImageCode`, `WY3_Serializer_
    EmitsOnlyThePinnedSubset`, `WY3_Serializer_SkipsBlankElements`,
    `WY3_Serializer_RejectsUnsafeImageSrc`, `WY3_Serializer_
    RejectsUnsafeLinkHref`, `WY5_SavedBodyIsByteIdentical`, `WY6_
    Sanitizer_StripsDisallowedElements`, `WY6_Sanitizer_
    StripsDisallowedAttributes`, `WY6_Sanitizer_StripsUnsafeHrefs`.
    (The `WY7` / `WY8` / `WY9` / `Compiled*` / `RichEditor*` tests
    are **artifact-string pins** — they are authored in U4–U7, not
    here. U3 authors **only** the pure-function tests.)
- **Exit:** `dotnet build Kumunita.slnx -c Debug` + `npm run build`
  (in `src/Kumunita.Web`) green. The two pure functions are present in
  the compiled JS (`wwwroot/js/lib/dom-to-markdown.js` contains
  `toMarkdown` + `sanitizeHtml`). The 9 pure-function tests are
  **discovered** (the pass/red status is recorded for U4–U7 to consume).
  Handoff note: a `## U3 — serializer + tests` section — (a) the two
  function names + the file path, (b) the 9 test names (verbatim),
  (c) the 9 pass/red counts (for U4–U7 to consume), (d) any `tsc`
  warnings on the new module.

### U4 — The editing loop: the pane becomes `contenteditable` + the `input` sync

- **Goal:** extend `bindRichEditor` in `client/lib/rich-editor.ts` with
  the **WY block** (§2.5): the pane becomes `contenteditable`; the binder
  populates the pane from the textarea on load; the binder keeps the
  textarea in sync on every pane `input`. **The existing RE/IE wiring is
  untouched** — the WY block is **additive** (the `if (pane) { … }`
  guard).
- **Entry reads:** `docs/design/wysiwyg-editor-design.md` §2.2 (the
  DOM contract) + §2.5 (the binder contract — the **primary** source) +
  §2.6 (the CSS contract), `src/Kumunita.Web/client/lib/rich-editor.ts`
  (the full module — the `bindRichEditor` function + the IE toggle
  block + the `renderPreview` function — the wiring to extend),
  `src/Kumunita.Web/client/lib/dom-to-markdown.ts` (U3's serializer +
  sanitizer — the two pure functions to import), `src/Kumunita.Web/
  Views/Announcement/New.cshtml` (one of the 10 composer surfaces —
  the markup the binder must work with: the `.rc-editor` wrapper, the
  toolbar, the textarea, the pane, the `data-ie-toggle` button).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Web/client/lib/rich-editor.ts` — the **WY block**
    (§2.5) in `bindRichEditor`. The existing RE/IE wiring (the 6 pure
    functions + the `renderPreview` function + the IE toggle block +
    the image upload lane) is **untouched** — the WY block is
    **additive** (the `if (pane) { … }` guard). The WY block: (a)
    imports `toMarkdown` + `sanitizeHtml` from `./dom-to-markdown.js`;
    (b) sets `pane.contentEditable = 'true'`; (c) sets
    `pane.innerHTML = renderPreview(textarea.value)` (the initial
    population); (d) installs the `input` handler
    (`textarea.value = toMarkdown(pane.innerHTML)`); (e) installs
    the `paste` handler (the sanitizer + the insert — **U6** owns
    the paste handler's internals; U4 installs the **stub** that
    calls `sanitizeHtml` + inserts the result); (f) **does not**
    rework the toolbar's `click` handlers (that is **U5**); (g)
    **does not** rework the `data-ie-toggle` button's click handler
    (that is **U7**).
  - `src/Kumunita.Web/wwwroot/css/site.css` — the **one** new CSS
    rule (§2.6): `.rc-editor-pane[contenteditable="true"] { cursor:
    text; outline: 2px solid var(--bs-primary, #0d6efd); outline-
    offset: 1px; }`. The existing `.rc-editor-pane` rule is
    **untouched** (the a11y focus ring is **added**, not re-shaped).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` + `npm run build`
  green. The compiled JS (`wwwroot/js/lib/rich-editor.js`) contains
  `contentEditable` + the `input` handler + the `paste` handler stub.
  The 10 composer surfaces are **unchanged** in shape (the pane gains
  `contenteditable="true"` at runtime, **not** in the Razor). Handoff
  note: a `## U4 — editing loop` section — (a) the WY block's 6
  sub-steps (a)–(f) (the paste handler is a **stub** in U4; U6 owns
  the internals), (b) the one new CSS rule (the a11y focus ring),
  (c) the 10 composer surfaces (unchanged in shape), (d) any `tsc`
  warnings.

### U5 — The toolbar rework (splice DOM, not Markdown)

- **Goal:** rework the toolbar's `click` handlers in `bindRichEditor`
  (§2.5) to **splice DOM** (the Selection / Range API on the pane)
  instead of splicing Markdown into the textarea. The same buttons
  (B, I, C, H1, H2, H3, •, 1., Link, Image) stay; their click handlers
  change. The image button reuses the **RC upload lane** (`POST
  /content-image` via `apiFetch`, the `insert-image.ts` convention —
  **no** new route, **no** second upload).
- **Entry reads:** `docs/design/wysiwyg-editor-design.md` §2.5 (the
  binder contract — the toolbar's `click` handlers) + §2.2 (the DOM
  contract — the Selection / Range API), `src/Kumunita.Web/client/lib/
  rich-editor.ts` (the full module — the 6 pure functions + the
  toolbar's `click` handlers — the wiring to rework),
  `src/Kumunita.Web/client/lib/insert-image.ts` (the RC upload lane —
  the `POST /content-image` + the `apiFetch` convention the image
  button reuses), `src/Kumunita.Web/client/lib/dom-to-markdown.ts`
  (U3's serializer — the `toMarkdown` function the toolbar calls
  after each splice to keep the textarea in sync).
- **Deliverables (≤ 1 file):**
  - `src/Kumunita.Web/client/lib/rich-editor.ts` — the toolbar's
    `click` handlers (§2.5). The **existing** 6 pure functions
    (`applyToggle` / `applyBlock` / `applyLink` / `imageLink` /
    `isSafeImageSrc` + `renderPreview`) are **untouched** — the
    toolbar's `click` handlers change from "splice Markdown into the
    textarea" to "splice DOM into the pane, then call
    `textarea.value = toMarkdown(pane.innerHTML)`". The **per-button
    mapping** (the WY·4 invariant): **B** (bold) → wrap the
    selection in `<strong>` (the Selection / Range API — the
    `range.surroundContents(new Element('strong'))` idiom, with a
    fallback for a selection that crosses element boundaries — the
    `range.extractContents()` + `strong.appendChild(extracted)` +
    `range.insertNode(strong)` construction); **I** (italic) → wrap
    the selection in `<em>` (the same construction); **C** (code) →
    wrap the selection in `<code>` (the same construction); **H1** /
    **H2** / **H3** → change the current block's tag to `<h1>` /
    `<h2>` / `<h3>` (the `range.startContainer` + the
    `parentNode.replaceChild(new Element('h1'), currentBlock)`
    construction — the caret is placed inside the new heading); **•**
    (ul) → wrap the current block's text in a `<ul><li>` (the
    `ul.appendChild(li); li.appendChild(text)` construction); **1.**
    (ol) → wrap the current block's text in a `<ol><li>` (the same
    construction); **Link** → `window.prompt('URL:')` + wrap the
    selection in `<a href="…">` (the same construction as B/I/C,
    with the `isSafeUrl` check — a rejected url splices the label
    as plain text); **Image** → the **RC upload lane** (U4's stub
    is replaced with the **real** upload: `POST /content-image` via
    `apiFetch` + the `fileInput.click()` idiom from `insert-image.ts`;
    on success, splice an `<img src="/content-image/{id}" alt="…">`
    into the pane at the caret; the `isSafeImageSrc` check is
    reused). **After every splice**, the toolbar calls
    `textarea.value = toMarkdown(pane.innerHTML)` (the binder keeps
    the textarea in sync — the WY·2 invariant).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` + `npm run build`
  green. The compiled JS (`wwwroot/js/lib/rich-editor.js`) contains
  the 10 toolbar buttons' `click` handlers (the Selection / Range
  API splices). The 6 pure functions are **untouched** (the
  `RichEditorExports_AreIntact` test still passes). The image
  button reuses the **RC upload lane** (no new route, no second
  upload). Handoff note: a `## U5 — toolbar rework` section — (a)
  the 10 per-button mappings (B/I/C → `<strong>`/`<em>`/`<code>`;
  H1/H2/H3 → `<h1>`/`<h2>`/`<h3>`; •/1. → `<ul><li>`/`<ol><li>`;
  Link → `<a href="…">`; Image → the RC upload lane + `<img>`),
  (b) the 6 pure functions (untouched), (c) the "after every
  splice" line (`textarea.value = toMarkdown(pane.innerHTML)`),
  (d) any `tsc` warnings.

### U6 — The sanitizer + the `paste` handler (WY·6)

- **Goal:** replace U4's `paste` handler **stub** with the **real**
  sanitizer + insert (the WY·6 invariant: paste / raw HTML is
  sanitized to the WY·3 subset before insertion). The sanitizer
  (`sanitizeHtml`) is already present (U3); U6 wires it into the
  `paste` handler + installs the XSS-protection (the escape-first /
  IsSafeImageSrc / IsSafeUrl semantics already in `rich-editor.ts`
  are reused).
- **Entry reads:** `docs/design/wysiwyg-editor-design.md` §2.4 (the
  sanitizer contract — the **primary** source) + §2.5 (the binder
  contract — the `paste` handler), `src/Kumunita.Web/client/lib/dom-
  to-markdown.ts` (U3's sanitizer — the `sanitizeHtml` function to
  wire in), `src/Kumunita.Web/client/lib/rich-editor.ts` (U4's
  `paste` handler stub — the wiring to replace), `src/Kumunita.Web/
  client/lib/rich-editor.ts` (the `htmlEscape` / `isSafeImageSrc` /
  `isSafeUrl` functions — the construction to reuse).
- **Deliverables (≤ 1 file):**
  - `src/Kumunita.Web/client/lib/rich-editor.ts` — the `paste`
    handler (the WY·6 invariant). The **stub** from U4 is replaced
    with the **real** handler: (a) intercepts the `paste` event on
    the pane (`pane.addEventListener('paste', (e) => { … })`);
    (b) reads the clipboard HTML
    (`e.clipboardData.getData('text/html')`);
    (c) **sanitizes** it (`sanitizeHtml(html)` — the WY·3 subset
    only);
    (d) **inserts** the sanitized HTML into the pane at the
    selection (the `range.insertNode(fragment)` idiom — the
    sanitized HTML is parsed into a `DocumentFragment` via
    `DOMParser` + the fragment's children are inserted);
    (e) **prevents** the default paste
    (`e.preventDefault()` — the browser's native paste is
    suppressed; the sanitized insert is the only path);
    (f) **keeps** the textarea in sync
    (`textarea.value = toMarkdown(pane.innerHTML)`).
    **The XSS protection** (the WY·6 invariant): the sanitizer
    strips every `on*` event-handler attribute, every `style`
    attribute, every `class` attribute (except `language-{lang}` on
    `<code>`), every `id` attribute, every `href` that is not a
    safe url (the `isSafeUrl` reject), every `src` that is not a
    safe image src (the `isSafeImageSrc` reject), and every element
    outside the WY·3 subset (`<script>`, `<iframe>`, `<object>`,
    `<embed>`, `<form>`, `<input>`, `<button>`, `<table>`, `<tr>`,
    `<td>`, `<div>`, `<span style=…>`). The **construction** is a
    **single pass** over the HTML (a regex-based strip, the same
    construction as `htmlEscape` / `isSafeImageSrc` / `isSafeUrl`
    already in `rich-editor.ts`).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` + `npm run build`
  green. The compiled JS (`wwwroot/js/lib/rich-editor.js`) contains
  the `paste` handler (the `sanitizeHtml` call + the `range.
  insertNode` insert + the `e.preventDefault()`). The 3 `WY6_
  Sanitizer_*` tests (U3's pure-function tests) still pass
  (the sanitizer is unchanged — U6 only wires it into the `paste`
  handler). Handoff note: a `## U6 — sanitizer + paste handler`
  section — (a) the 6 sub-steps (a)–(f) (the `paste` handler's
  internals), (b) the XSS protection (the reject list — the
  elements/attributes the sanitizer strips), (c) the 3 `WY6_
  Sanitizer_*` tests (still passing), (d) any `tsc` warnings.

### U7 — The code view rework (the `</>` toggle reveals the read-only mirror)

- **Goal:** rework the `data-ie-toggle` button's click handler
  (§2.5) so the code view is a **read-only mirror** of the pane
  (the WY·7 invariant: the textarea is revealed as a read-only
  mirror; the pane stays editable + visible; the label swap is
  kept).
- **Entry reads:** `docs/design/wysiwyg-editor-design.md` §2.5 (the
  binder contract — the `data-ie-toggle` button's click handler) +
  §2.2 (the DOM contract — the textarea is the read-only sink),
  `src/Kumunita.Web/client/lib/rich-editor.ts` (the full module —
  the IE toggle block — the wiring to rework),
  `src/Kumunita.Web/Views/Shared/_RichEditorToggle.cshtml` (the
  toggle button's markup — the `data-ie-toggle` attribute + the
  `data-ie-label-source` / `data-ie-label-preview` data attributes
  + the `<kw-l>` element — the label swap is kept).
- **Deliverables (≤ 1 file):**
  - `src/Kumunita.Web/client/lib/rich-editor.ts` — the
    `data-ie-toggle` button's click handler (the WY·7 invariant).
    The **existing** IE toggle block (the `setView` function + the
    `rc-editor-source-hidden` class + the `rc-editor-pane-active`
    marker + the label swap) is **reworked**: the `setView`
    function's semantics change from "toggle the source's
    visibility" to "toggle the code view's visibility". The
    **two states** (the WY·7 invariant): (1) **pane only** (the
    default — the resident sees the editable pane; the textarea
    is hidden by the `rc-editor-source-hidden` class — **unchanged**
    from IE); (2) **pane + code view** (the resident sees the
    editable pane + the **read-only** textarea — the textarea is
    revealed by removing the `rc-editor-source-hidden` class; the
    textarea is **read-only** — the `textarea.readOnly = true`
    property is set by the binder (the textarea is **not**
    editable — the WY·2 invariant: the textarea is the read-only
    sink, not the editing surface)). The **label swap** (the
    `rc.editor.source` / `rc.editor.showPreview` keys) is **kept**
    (the `_RichEditorToggle` partial resolves them server-side; the
    binder swaps the `<kw-l>`'s `textContent` — **unchanged** from
    IE). The **pane stays editable + visible** in **both** states
    (the WY·1 invariant — the pane is the editing surface; the code
    view is a **mirror**, not a **mode switch**).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` + `npm run build`
  green. The compiled JS (`wwwroot/js/lib/rich-editor.js`) contains
  the `data-ie-toggle` button's click handler (the `setView`
  function's reworked semantics). The `_RichEditorToggle.cshtml`
  partial is **unchanged** (the label swap is kept). The textarea
  is **read-only** (`textarea.readOnly = true` — the WY·2 invariant).
  Handoff note: a `## U7 — code view rework` section — (a) the
  `setView` function's reworked semantics (the two states — pane
  only / pane + code view), (b) the label swap (kept — the
  `_RichEditorToggle` partial is unchanged), (c) the textarea's
  `readOnly` property (set by the binder — the WY·2 invariant),
  (d) any `tsc` warnings.

### U8 — Run + record the WY acceptance gate

- **Goal:** execute and **record** the three-test acceptance gate
  (closed-loop / handoff / part-vs-whole) from the design doc §2.8,
  *using* U3–U7's 17 tests as the part-vs-whole evidence. The
  **closed-loop** + **handoff** tests are **manual** (the resident
  opens a composer, types in the pane, saves; the saved body is
  verified + the read path is verified) — they are recorded in the
  design doc, not automated (the repo has no JS test runner; the
  pure-function tests are the automated floor). **If the manual
  tests cannot be run** (the dev server is not up, the DB is not
  seeded), the gap is recorded in the design doc + the handoff note
  + the next unit who lands the runtime records the pass count.
- **Entry reads:** `docs/design/wysiwyg-editor-design.md` §2.8 (the
  acceptance gate — the **primary** source) + §2.7 (the 17 pinned
  test names), `docs/plans-milestones/in-progress/wysiwyg-handoff-
  notes.md` (U3–U7's sections — the 17-test results that the gate
  *references*), `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs`
  (U3's 9 pure-function tests + U4–U7's 8 artifact-string pins —
  the part-vs-whole evidence).
- **Deliverables (1 file, modify):** `docs/design/wysiwyg-editor-
  design.md` — append `### Run result (WY acceptance gate — <date>)`:
  the three test names, their pass/red status, the 17-test count
  (from U3–U7), the manual-test status (closed-loop + handoff), and
  one line per any `## U<m> — Drift pause` section in the handoff
  note (each resolved or still open). **No code, no build.**
- **Exit:** the gate section is present and consistent with U3–U7's
  results. Handoff note: a `## U8 — gate recorded` section — the
  three test names + pass counts + the date + any still-open
  drift.

### U9 — Close: ADR 0033 → Accepted + ADR index + ARCHITECTURE.md flip + handoff Summary

- **Goal:** flip the `WYSIWYG inline editing` status line in
  `ARCHITECTURE.md` from "WY — not yet created" to **WY ✓ live**
  with the gate summary; move **ADR 0033** from "Draft (lands in
  U9)" to **Accepted**; append the ADR row to `docs/adr/README.md`;
  append the `## Summary` section to the handoff note (the
  shipped units U0–U8, with their one-liner goal + test count +
  any deviations + the named deferrals list). **No code, no build.**
- **Entry reads:** `docs/ARCHITECTURE.md` §2 (the `Posts/` /
  `Moderation/` / `Events/` / `Projects/` lines — the `WYSIWYG
  inline editing` line to flip; the `Events/` / `Projects/` lines
  **untouched**), `docs/adr/0033-wysiwyg-inline-editing.md` (U2's
  draft — the ADR to move to Accepted), `docs/adr/README.md` (the
  ADR index — the row to append), `docs/plans-milestones/in-progress/
  wysiwyg-handoff-notes.md` (U0–U8's sections — the shipped units
  to summarize).
- **Deliverables (≤ 4 files, modify):**
  - `docs/adr/0033-wysiwyg-inline-editing.md` — flip `Status:
    Draft (lands in U9)` → `Status: Accepted`.
  - `docs/adr/README.md` — append the row `| 0033 | WYSIWYG
    inline editing: the rendered pane is the editable surface
    (`contenteditable`); the Markdown source is a read-only mirror;
    the serializer is the inverse of `renderPreview`; the sanitizer
    strips paste to the pinned subset; `tsc`-only, no editor
    dependency, `Body` as Markdown, one renderer on the read path
    (Amends 0031 — the "hard non-negotiable" reversed by user
    approval 2026-09-15; Amends 0032 — the rendered-by-default view
    kept, now editable) | Accepted |`.
  - `docs/ARCHITECTURE.md` — flip the `WYSIWYG inline editing` line
    from "WY — not yet created" to **WY ✓ live** (the gate summary
    from U8). The `Events/` / `Projects/` lines are **untouched**
    (M4/M5/M6 stay Events / Projects / Portability).
  - `docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md` —
    append `## Summary` — a table of the shipped units (U0–U8),
    with their one-liner goal + test count + any deviations + the
    named deferrals list (each item named, each with a one-line
    WY-2 candidate or "resolved by U<m>").
- **Exit:** no build. `ARCHITECTURE.md`'s `WYSIWYG inline editing`
  line is flipped; ADR 0033 is **Accepted**; the ADR index has the
  0033 row; the handoff note's `## Summary` section is present.
  **The last handoff note U9 writes is for the WY-2 agent** (not for
  a U9 — there is none). Move the register + the 10 unit plans +
  the handoff note `in-progress/` → `done/` (move **last**).
  `git status` clean except the 4 modified files + the folder moves.
