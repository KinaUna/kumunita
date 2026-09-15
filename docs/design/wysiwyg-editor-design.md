# WYSIWYG inline editing (`WY`) — the rendered pane is the editable surface; the Markdown source becomes a read-only mirror

> **Three-tier contract.** This file is the **primary** tier of the WY lane:
> it pins the invariants (WY·1–WY·9), the FACES (WY1–WY10), the exact DOM /
> serializer / sanitizer / binder / CSS contracts, the pinned seam-test
> names, the acceptance gate, and the drift guard. The register
> (`docs/plans-milestones/plan-wysiwyg.md`) is the **secondary** tier
> (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md` is the
> **scratch** tier (one short section per unit, appended, never rewritten).
> When the three disagree, **this file wins for the pinned shapes**; the
> register wins for *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U1):** the value chain, the context (incl. the ADR
> 0033 reversal), the scope, the **nine invariants** (WY·1–WY·9), the **ten
> FACES** (WY1–WY10), and the frozen-base assumptions. **Part 2 (U2):** the
> seams & contracts (the exact TS shapes, the DOM contract, the serializer +
> sanitizer contracts, the pinned seam-test names, the acceptance gate, the
> drift guard).
>
> **The frozen base.** This lane is built **on top of** the inline-editor
> lane (`IE`, ADR 0032), the rich-editor lane (`RE`, ADR 0031), and the
> rich-content lane (`RC`, ADR 0025). RC's **R·1–R·7**, RE's **RE·1–RE·3**,
> and IE's **IE·1** all still bind **unchanged** (re-anchored in §Frozen base
> below). WY adds **no** re-shape of `MarkdownRenderer`, `ContentImageIds`,
> `IMediaStore`, the `ImageIds` fields, the `/content-image` routes, the
> `.rc-body` / `.rc-image` / `.rc-editor-*` CSS, `insert-image.ts`, or any of
> the 6 RE pure functions + `renderPreview`. It is **client-only and
> additive**: one new pure serializer (`toMarkdown`) + one new pure
> sanitizer (`sanitizeHtml`) in `client/lib/dom-to-markdown.ts`, one
> additive **WY block** in `bindRichEditor`, and one CSS focus-ring rule.
> **No** `.csproj` change, **no** `package.json` change (still
> `typescript`-only), **no** editor dependency, **no** new route, **no**
> second renderer on the read path (`MarkdownRenderer` is untouched). The
> saved body is **byte-identical** Markdown the RC read path already renders.

## Value chain

RC set out to deliver *"a resident can bold a heading, start a list, or show
a photo"* (RC Scope, R1). RC delivered the **read** half: `MarkdownRenderer`
renders Markdown on every read surface, and `Body` is stored as a Markdown
`string` (RC R·7, zero migrations). RE shipped the **write** half's
*visibility*: a split view with the toolbar row, the source `<textarea>`
row, and the rendered preview row (RE·1, RE·2). IE finished the arc toward
what a resident *sees*: the rendered pane became the **default view** and
the Markdown source was hidden behind a `</>` toggle (IE·1).

IE made the rendered pane the surface the resident's eyes land on — but the
pane is still **read-only**. The only way to *edit* is to click the `</>`
toggle, reveal the raw Markdown `<textarea>`, and hand-type `**bold**` /
`## Heading` / `![alt](/content-image/…)` there. That is the exact inverse
of what Gmail/Outlook do: a resident clicks the rendered text, puts the
caret in it, and types. The platform's promise — *"a resident writes what
they see"* — was met for *seeing*, not for *writing*.

This lane completes the arc from the resident's point of view:

- **The rendered pane becomes the editable surface** — a resident clicks the
  rendered text, puts the caret in it, and types; the formatting buttons
  splice *rendered* structure (WY·1, WY·4).
- **The Markdown source becomes a read-only mirror** — the `</>` toggle
  now reveals a **read-only** Markdown mirror of the pane (the "code view"),
  not the editable source (WY·7).
- **The textarea stays the single source of truth the server binds** — the
  binder keeps it in sync by serializing the pane on every `input`
  (WY·2, WY·5).

The value chain moves one arrow: from *"the resident sees what they will
publish"* (IE) to **"the resident writes what they see"** (WY) — the
Gmail/Outlook model. The saved body is **byte-identical** to what a
resident could have hand-typed in the code view — RC's server-side
`ContentImageIds` parse and `MarkdownRenderer` render are **unchanged**
(WY·5, RC R·3, RC R·1).

## Context

The gap this lane closes is the **editable** half of the split view. IE
shipped the rendered-by-default experience — a resident opens a composer
and sees *what they will publish* filling the surface. But the pane is
**read-only**: the only edit path is the `</>` toggle → raw Markdown →
hand-typed `**` / `#` markers. A resident who does not know Markdown cannot
bold a word, start a list, or insert a link *in the rendered text*; the
toolbar (RE D2) splices Markdown into the hidden textarea, and the resident
watches the pane update but never typed in the pane.

WY closes exactly that gap with **one architectural change** (the pane
becomes `contenteditable`) and **one new pure function** (a DOM→Markdown
serializer, the inverse of `renderPreview`). The Markdown source stays the
single source of truth the server binds (RC R·3 / RE·1, **unchanged**); the
pane becomes the editable surface; the serializer keeps the two in sync;
the code view (the `</>` toggle) becomes a **read-only mirror** of the pane.

### The ADR 0033 reversal (load-bearing)

This lane **deliberately reverses** the "hard non-negotiable" in **ADR 0031
D1 / ADR 0032** — *"true `contenteditable` / a third-party editor — hard
non-negotiable out."* The user has **explicitly approved** this reversal
(**2026-09-15**). **ADR 0033** (drafted in U2, Accepted in U9) records the
reversal and **what still binds**: `Body` stays a Markdown `string`
(RC R·7), the `tsc`-only constraint (RE·3 / WY·8), **no editor dependency**
(`package.json` still `typescript`-only), **one renderer on the read path**
(RC R·1 — `MarkdownRenderer` is untouched), and the saved body is
**byte-identical** Markdown (RC R·3). What changes is the **composer
surface only**: the pane is now `contenteditable`, the textarea is now a
read-only mirror, and there is a new hand-rolled, pure, unit-tested
**DOM→Markdown serializer** (the inverse of `renderPreview`) + a
**sanitizer** for paste.

**This is not a deferral; it is a new policy.** It is also *narrower* than
the old "hard non-negotiable" feared: there is still **no** editor
dependency, still **no** `.csproj` change, **no** new route, **no** new
server surface, and **no** second renderer on the read path. The reversal
unlocks `contenteditable` **in the composer** only; it does not touch the
read path, the storage model, or the server.

### The constraints that still bind

RC's **R·1–R·7**, RE's **RE·1–RE·3**, and IE's **IE·1** all keep binding
**unchanged** (re-anchored in §Frozen base below). On top of them:
`package.json` stays **`typescript`-only** (WY·8); **no** editor dependency;
**no** `.csproj` change; **one renderer on the read path**
(`MarkdownRenderer` untouched — RC R·1); **`Body` as a Markdown
`string`** (RC R·7). The serializer is the **exact inverse** of
`renderPreview` — the same RC-pinned subset, the same escape-first
construction, the same `isSafeImageSrc` / `isSafeUrl` semantics.

## Scope

**In (this lane):**

- The pane becomes `contenteditable` (the `rc-editor-pane` /
  `data-rich-editor-preview` element; set by the binder at runtime, **not**
  in the Razor) — WY·1.
- The **serializer** (`toMarkdown(html): string`) + its pure-function unit
  tests — the inverse of `renderPreview` — WY·3.
- The **editing loop** (the binder populates the pane on load and keeps the
  textarea in sync on every pane `input`) — WY·2, WY·5.
- The **toolbar rework** (the buttons splice **DOM**, not Markdown, into the
  pane via the Selection / Range API) — WY·4.
- The **sanitizer** (`sanitizeHtml(html): string`) + the **`paste` handler**
  (paste / raw HTML is sanitized to the WY·3 subset before insertion) —
  WY·6.
- The **code view rework** (the `data-ie-toggle` `</>` button reveals a
  **read-only** Markdown mirror of the pane; the label swap is kept) —
  WY·7.
- **ADR 0033** (the reversal + what still binds).
- **This design doc** (this file).
- The **artifact-string pins** (the compiled JS contains
  `contenteditable` + `toMarkdown` + the sanitizer; the textarea is **not**
  disabled/removed; the 6 RE exports + the new `toMarkdown` export are
  present).
- The **round-trip property** (WY·10) as a **pure-function** test
  (no browser needed — `toMarkdown` is pure).

**Out (named deferrals for a future WY-2 lane, if one comes):**

- **Nested lists** — the WY·3 subset is flat `<ul>`/`<ol>` + `<li>`; no
  `<li>`-in-`<li>`.
- **Blockquotes** — no `>` branch in `MarkdownRenderer`; not in the subset.
- **Tables** — no branch in `MarkdownRenderer`; not in the subset.
- **Footnotes** — no branch in `MarkdownRenderer`; not in the subset.
- **Strikethrough** — no branch in `MarkdownRenderer`; not in the subset.
- **`execCommand`-based undo/redo** — the browser's native
  `contenteditable` undo is the floor; a custom undo/redo is a **future
  lane**.
- **Mobile-specific editing UX** — thumb-reachable controls, touch
  selection, etc. A CSS + JS concern.
- **Caret-mapping between the pane and the code view** — when the resident
  toggles to the code view, the caret/selection could map to the
  corresponding Markdown position. A power feature, not a floor.
- **localStorage persistence of the editing preference** — remember the
  resident's view choice (pane vs. code view) across sessions.

Each is a **future** lane, named, not a WY re-open (the ADR 0011 / 0025 /
0031 precedent).

## Invariants (pinned for WY)

Nine invariants, **WY·1–WY·9**. Each is one idea, pinned so every unit and
every FACES row references a stable number. **RC R·1–R·7, RE·1–RE·3, and
IE·1 are not re-stated here** — they are re-anchored unchanged in §Frozen
base and keep binding in their own number spaces.

| # | Invariant (one-line WY note) |
|---|------------------------------|
| **WY·1** | **The pane is the editing surface** — the `rc-editor-pane` carries `contenteditable="true"` (set by the binder at runtime, not in the Razor). The resident clicks the rendered text, puts the caret in it, and types. |
| **WY·2** | **The textarea is the read-only sink** — RC R·3 / RE·1 unchanged: it is **never** removed, disabled, or re-shaped; it stays the live form field the server binds on submit; the binder only *writes* to it (`.value = toMarkdown(pane.innerHTML)`), the resident never *types* into it. |
| **WY·3** | **The serializer emits exactly the RC-pinned subset and nothing more** — the WY·3 subset (P, H1–H6, UL/OL, LI, STRONG, EM, CODE, A, IMG, PRE/CODE) is the **ceiling**; `toMarkdown` is the **inverse of `renderPreview`** (the same subset, the same escape-first construction, the same `isSafeImageSrc` / `isSafeUrl` semantics). |
| **WY·4** | **The toolbar splices DOM, not Markdown, into the pane** — the Selection / Range API on the `contenteditable` (the per-button mappings in §Part 2); after every splice the binder keeps the textarea in sync. |
| **WY·5** | **The saved body is byte-identical** to what a resident could have hand-typed in the code view — RC R·3 / RC R·1 unchanged; the server parse (`ContentImageIds`) and the read path (`MarkdownRenderer`) are untouched. |
| **WY·6** | **Paste / raw HTML is sanitized to the WY·3 subset before insertion** — the `paste` handler intercepts the clipboard, runs it through `sanitizeHtml`, and inserts only the constrained subset (the escape-first / `isSafeImageSrc` / `isSafeUrl` semantics already in `rich-editor.ts` are reused). |
| **WY·7** | **The code view is a read-only mirror of the pane** — the `data-ie-toggle` `</>` button reveals the textarea (the serialized Markdown); the textarea is **read-only** (`readOnly = true`, set by the binder); the pane stays editable + visible in both states; the label swap is kept. |
| **WY·8** | **The `tsc`-only constraint stands unchanged** — no editor dependency in `package.json` (still `typescript`-only); no `.csproj` change; the lane is client-only and additive. |
| **WY·9** | **a11y** — the pane is keyboard-operable, reachable in tab order, and carries `role="textbox"` + `aria-multiline`; the toolbar buttons stay `<button type="button">` + localized via `<kw-l>`. |

## FACES (pinned, 10)

Ten resident-facing scenarios, **WY1–WY10**, each exercising one or more
invariants.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| **WY1** | A resident opens a composer, clicks into the rendered pane, and types — the pane **is** the editing surface (`contenteditable`), reachable in tab order with `role="textbox"` + `aria-multiline`. | WY·1, WY·9 |
| **WY2** | As the resident types in the pane, the hidden textarea's `.value` is kept in sync (`textarea.value = toMarkdown(pane.innerHTML)`) — the sink the server binds stays authoritative without the resident ever touching it. | WY·2 |
| **WY3** | The resident selects a word and clicks **B** / **I** / **C** / **H** / list — the toolbar splices **DOM** (`<strong>` / `<em>` / `<code>` / `<h1>`–`<h3>` / `<ul><li>` / `<ol><li>`) into the pane via the Selection / Range API, then keeps the textarea in sync. | WY·4 |
| **WY4** | The resident clicks **Image** → the RC upload lane fires (`POST /content-image` via `apiFetch`, the `insert-image.ts` convention — no new route); on success an `<img src="/content-image/{id}" alt="…">` is spliced into the pane; `ImageIds` is populated **exactly as if hand-typed**. | WY·3, WY·4 |
| **WY5** | The resident saves → the saved body is **byte-identical** to what they could have hand-typed in the code view; the read path (`MarkdownRenderer`) renders it identically; the server parse (`ContentImageIds`) is untouched. | WY·5, RC R·3, RC R·1 |
| **WY6** | The resident pastes rich HTML (from Gmail, Word, etc.) into the pane → the `paste` handler sanitizes it to the WY·3 subset before insertion (every `on*` handler, `style`, disallowed `class`, disallowed element, unsafe `href`/`src` is stripped). | WY·6 |
| **WY7** | The resident clicks the `</>` toggle → the **read-only** Markdown mirror (the textarea) is revealed beneath the pane; the pane stays editable + visible; the label swaps (`</>` / `Preview`); clicking again hides the mirror. | WY·7 |
| **WY8** | On a surface where the image button is gated out (the RE `data-rich-editor-no-image` precedent), the **Image** button is absent — the pane, the toolbar's other buttons, and the code view are unchanged there. | (RE precedent) |
| **WY9** | A keyboard-only resident can tab to the pane, place the caret, type, and apply formatting; the toolbar buttons are `<button type="button">` localized via `<kw-l>`; the pane carries `role="textbox"` + `aria-multiline`. | WY·9 |
| **WY10** | **The round-trip property:** for any `md` in the RC-pinned subset, `toMarkdown(renderPreview(md)) === md` — the serializer is the **exact inverse** of `renderPreview` (the same subset, the same escape-first construction, the same `isSafeImageSrc` / `isSafeUrl` semantics). Pinned as a pure-function test. | WY·3, WY·5 |

## Assumptions

- **The reversal is approved and recorded.** The user explicitly approved
  reversing ADR 0031 D1 / ADR 0032's "hard non-negotiable" (2026-09-15).
  ADR 0033 records the reversal and what still binds. **This is not a
  deferral; it is a new policy.**
- **`Body` stays a Markdown `string`** (RC R·7 unchanged). The server still
  derives `ImageIds` by parsing the *body text* (RC R·3); the read path
  still runs `MarkdownRenderer` (RC R·1). **Zero server change.**
- **The textarea is never removed or disabled.** It stays the live form
  field the server binds (RC R·3 / RE·1). It is hidden in the DOM (a CSS
  class, as IE already does) but **present in the form** — the server reads
  `Body` from it on submit, exactly as today.
- **The pane is the same element IE made the default view.** The
  `rc-editor-pane` (the `data-rich-editor-preview` element) is the one that
  becomes `contenteditable`. No new element, no re-shape of the
  `.rc-body` / `.rc-image` CSS (RC frozen base).
- **The serializer is the load-bearing artifact.** It is pure, unit-tested,
  and round-trip-stable with `renderPreview`. It is the inverse of
  `renderPreview` — the same subset, the same escape-first construction,
  the same `isSafeImageSrc` / `isSafeUrl` semantics.
- **The sanitizer is a DOM-shape normalizer, not a renderer.** It strips
  pasted HTML to the WY·3 subset. It reuses the escape-first /
  `isSafeImageSrc` / `isSafeUrl` semantics already in `rich-editor.ts`. It
  does **not** add a second renderer or a new dependency.
- **The toolbar is reworked, not re-shaped.** The same buttons (B, I, C, H1,
  H2, H3, •, 1., Link, Image) stay; their click handlers change from "splice
  Markdown into the textarea" to "splice DOM into the pane." The image
  button reuses the **RC upload lane** (`POST /content-image` via `apiFetch`,
  the `insert-image.ts` convention — **no** new route, **no** second
  upload).
- **The code view (the `</>` toggle) becomes a read-only mirror.** The
  `data-ie-toggle` button (IE, ADR 0032) is kept; its semantics change from
  "reveal the editable source" to "reveal the **read-only** Markdown
  mirror." The label swap (`</>` / `Preview`) is kept.
- **The composer surfaces are unchanged in shape.** They keep the same
  `.rc-editor` wrapper, the same toolbar, the same textarea, the same pane
  (the **16 editor blocks / 10 view files** U0 verified). The only markup
  change is the pane gains `contenteditable="true"` (added by the binder at
  runtime, **not** in the Razor).
- **The test model is unchanged.** The artifact-string pins (the
  `InlineEditorTests` / `RichEditorTests` shape) are extended: the compiled
  JS contains `contenteditable` + `toMarkdown` + the sanitizer, the textarea
  is **not** disabled/removed/hidden-by-attribute, and the 6 RE exports +
  the new `toMarkdown` export are present. The round-trip property (WY10)
  is pinned as a **pure-function** test (no browser needed — `toMarkdown`
  is pure).

## Frozen base (re-anchored unchanged)

**RC R·1–R·7** (one renderer, escape-first, source-is-the-`string`,
`ImageIds` server parse, the `/content-image` routes, the `.rc-body` /
`.rc-image` CSS) + **RE·1–RE·3** (one source of truth = the textarea,
preview↔renderer parity, no new dependency / no new server surface) +
**IE·1** (the source is hidden via a **CSS class**, never
`disabled`/`hidden`-attributed/removed — the textarea stays the live form
field the server binds). WY adds **no** re-shape of any of them. The
`rich-editor.ts` 6 pure functions + `renderPreview` are byte-identical; the
10 view instances' existing toolbar / textarea / pane markup is
byte-identical; `package.json` is still `typescript`-only.

*— Part 1 authored by U1 (2026-09-15). Part 2 (seams & contracts) is
authored by U2.*
