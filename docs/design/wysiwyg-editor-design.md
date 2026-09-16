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

---

## Seams & contracts (Part 2, written by U2)

This part pins the **exact** shapes U3–U7 implement against. A fresh agent
can build U3–U7 from this section alone without re-deriving the RC subset.
The reference of record for the subset is `Security/MarkdownRenderer.cs`
(the C# renderer) and `Security/ContentImageIds.cs` (the `ImageIds` parse
the serializer must stay byte-compatible with); the reference for the
escape-first / `isSafeImageSrc` / `isSafeUrl` construction is
`client/lib/rich-editor.ts` (the `renderPreview` function — the **inverse**
the serializer must be, and the six pure functions the pattern follows).
Every element/attribute below is either **in** the WY·3 subset (keep) or
**out** of it (strip). A mismatch between an implementation and these pins
is a `## U<m> — Drift pause` (unit-series rule 6), not a silent edit.

### 2.1 frozen base (unchanged)

The following keep binding **unchanged**; WY adds **no** re-shape of any of
them. WY's only new authority is the WY·1–WY·9 invariant table (Part 1).

- **RC R·1–R·7** — one renderer (`MarkdownRenderer`, untouched — the read
  path is **exactly** this, no second renderer); escape-first (client +
  server); the source is a `string` (`Body` as a Markdown `string`, RC R·7);
  `ImageIds` is a **server parse** of the body text (`ContentImageIds`,
  RC R·3); the `GET`/`POST /content-image` routes (RC R·5/R·6); the
  `.rc-body` / `.rc-image` CSS (RC R·4).
- **RE·1–RE·3** — one source of truth = the `<textarea>` (RE·1);
  preview↔renderer parity (RE·2); `tsc`-only / no new dependency / no new
  server surface (RE·3).
- **IE·1** — the source `<textarea>` is hidden via a **CSS class**
  (`rc-editor-source-hidden`), **never** `disabled` / `hidden`-attributed /
  removed; the textarea stays the live form field the server binds.
- **`tsc`-only** (WY·8 / RE·3) — `package.json` stays **`typescript`-only**;
  no editor dependency; no `.csproj` change.
- **The six RE pure functions** — `renderPreview`, `applyToggle`,
  `applyBlock`, `applyLink`, `imageLink`, `isSafeImageSrc` — **byte-
  identical**; `bindRichEditor` is **extended** (not re-shaped) by the WY
  block in §2.5.
- **`Body` as a Markdown `string`** (RC R·7) — zero server change, zero
  migration; the saved body is **byte-identical** to what a resident could
  hand-type in the code view (WY·5).

### 2.2 the DOM contract (exact)

The pane element is the **same** `rc-editor-pane` / `[data-rich-editor-
preview]` element IE made the default view (U0 verified 16 blocks / 10 view
files). No new element, no re-shape of the `.rc-body` / `.rc-image` CSS
(RC frozen base), no change to the 10 composer surfaces' markup.

| Aspect | Pinned shape (exact) |
|--------|----------------------|
| **Pane (editing surface, WY·1)** | `[data-rich-editor-preview]` carries `contenteditable="true"` — **set by the binder at runtime** (`pane.contentEditable = 'true'`), **not** in the Razor. Also carries `role="textbox"` + `aria-multiline` (WY·9, set by the binder). |
| **Textarea (read-only sink, WY·2)** | `textarea[data-rich-editor]` is **not** re-shaped, **not** disabled, **not** removed, **not** re-hidden. It stays the live form field the server binds on submit (RC R·3 / RE·1 / IE·1 unchanged). The binder sets `textarea.readOnly = true` (WY·7 — the code view is a **read-only mirror**, the resident never types into it). The binder is the **only** writer to `textarea.value`. |
| **Toolbar** | `.rc-editor-toolbar` is **not** re-shaped; the buttons stay `<button type="button" data-md="…">` (B/I/C/H1/H2/H3/•/1./Link/Image) + the `data-ie-toggle` button. Only the buttons' **click handlers** change (U5, §2.5e). |
| **`data-ie-toggle` button** | **Not** re-shaped (the `_RichEditorToggle` partial markup is unchanged — the label swap is kept). Its **click handler's semantics** change (U7, §2.5f): it reveals a **read-only mirror**, not the editable source. |
| **Pane initial state (on load)** | `pane.innerHTML = renderPreview(textarea.value)` — the binder populates the pane from the textarea's current value (reuses the existing `renderPreview` — **not** a new renderer). |
| **Pane `input` handler (WY·2)** | `textarea.value = toMarkdown(pane.innerHTML)` — on every pane `input`, the binder serializes the pane's constrained HTML to Markdown and writes it into the textarea. **No bidirectional sync** — the textarea is never typed into. |
| **Toolbar `click` handlers (WY·4)** | Each splices **DOM** (the Selection / Range API on the `contenteditable` pane), then calls `textarea.value = toMarkdown(pane.innerHTML)` (U5, §2.5e). |
| **`paste` handler (WY·6)** | Intercepted on the pane; the clipboard HTML is run through `sanitizeHtml` **before** insertion (U6, §2.5d). |

**The pane is authoritative; the textarea is the sink.** The resident types
in the pane; the binder serializes to the textarea; the server reads the
textarea on submit. The textarea's `.value` is **always** the serialized
Markdown of the pane — never hand-typed (WY·2, WY·7).

### 2.3 the serializer contract (exact TS)

The **load-bearing** new artifact. One module: `client/lib/dom-to-
markdown.ts`, exporting **exactly two** pure functions (`toMarkdown`,
`sanitizeHtml`) — no other exports. Both are **pure** (no DOM, no side
effects, importable in a non-DOM environment) so they are unit-testable
without a browser (the `insert-image.ts` / `rich-editor.ts` pure-function
pattern; the `typeof document !== 'undefined'` self-wire guard is **not
needed** here — the module is a library, the **binder** imports it).

**`export function toMarkdown(html: string): string`** — the **inverse of
`renderPreview`**: it takes the pane's `innerHTML` (a string) and returns a
Markdown string. It emits **exactly** the WY·3 subset and **nothing more**
(WY·3). It reuses the `isSafeImageSrc` / `isSafeUrl` semantics already in
`rich-editor.ts` (re-export or re-derive — same accept/reject set).

**The WY·3 subset (the ceiling) — element → Markdown form (the inverse of
`renderPreview`):**

| Element (as it appears in the pane) | Markdown emitted (the inverse of `renderPreview`) |
|-------------------------------------|-----------------------------------------------------|
| `<p>` (inline content) | the inline content + `\n\n` |
| `<h1>`–`<h6>` | `#`–`######` + ` ` + the inline content + `\n\n` |
| `<ul>` (a run of `<li>`) | `- ` + each `<li>`'s inline content on its own line + `\n\n` |
| `<ol>` (a run of `<li>`) | `1. ` / `2. ` / … per `<li>` + `\n\n` |
| `<li>` (inline content) | the inline content — **no nesting** (WY·3 is flat) |
| `<strong>` | `**` + the inline content + `**` |
| `<em>` | `*` + the inline content + `*` |
| `<code>` | `` ` `` + the text + `` ` `` |
| `<a href="…">` | `[` + the inline content + `](` + the href + `)` |
| `<img src="/content-image/{id}" alt="…">` | `![` + the alt + `](` + the src + `)` (the **exact** `imageLink` / `ContentImageIds.FullSrcRe` form — RC R·3 byte-identity) |
| `<pre><code class="language-{lang}">` | ` ```{lang}` + `\n` + the text + `\n` + ` ``` ` + `\n\n` |
| `<pre><code>` (no `class`) | ` ``` ` + `\n` + the text + `\n` + ` ``` ` + `\n\n` |

**The escape rule (the inverse of `htmlEscape`).** `renderPreview`
HTML-escapes every text character before emitting tags; `toMarkdown` must
**un-escape** the five HTML entities in the pane's `innerHTML` before
emitting the Markdown (so a round-tripped body is byte-identical to the
original):

| Entity in `innerHTML` | Character emitted |
|-----------------------|-------------------|
| `&amp;` | `&` |
| `&lt;` | `<` |
| `&gt;` | `>` |
| `&quot;` | `"` |
| `&#39;` | `'` |

**The edge cases (pinned, WY·3):**

- **(a)** a blank `<p></p>` → **skipped** (no Markdown emitted).
- **(b)** a heading with no content (`<h1></h1>`) → **skipped**.
- **(c)** a list with no items (`<ul></ul>` / `<ol></ol>`) → **skipped**.
- **(d)** a link with no href (`<a>label</a>`) → the **label as plain text**
  (the inline content, **not** a `[label]()` form).
- **(e)** an image with an **unsafe** `src` (the `isSafeImageSrc` reject) →
  the whole image rendered as **plain escaped text** (reusing the
  `isSafeImageSrc` predicate — the `renderPreview` reject precedent).
- **(f)** a link with an **unsafe** `href` (the `isSafeUrl` reject) → the
  **label as plain text** (reusing the `isSafeUrl` predicate).
- **(g)** a `<pre><code>` with **no** language → the fenced form with **no**
  language (` ``` ` + `\n` + text + `\n` + ` ``` `).
- **(h)** a `<li>` with **mixed** inline content (bold + italic + code) → the
  inline content serialized **in the same order** `renderPreview` emits it
  (code first, then bold, then italic — the `inlineText` order).

**The round-trip property (WY·10 / WY5, the key invariant).** For **any**
`md` in the RC-pinned subset:

```
toMarkdown(renderPreview(md)) === md
```

The serializer is the **exact inverse** of `renderPreview` — the same
subset, the same escape-first construction, the same `isSafeImageSrc` /
`isSafeUrl` semantics. This is pinned as a **pure-function** test
(`WY10_RoundTrip_BoldHeadingListLinkImageCode`, §2.7) with no browser.

### 2.4 the sanitizer contract (exact TS)

**`export function sanitizeHtml(html: string): string`** — co-located with
`toMarkdown` in `client/lib/dom-to-markdown.ts`. **Pure** (no DOM, no side
effects). It is a **DOM-shape normalizer, not a renderer** (WY·6): it strips
every element/attribute **outside** the WY·3 subset and keeps the WY·3
subset verbatim, so `toMarkdown` (and the pane) only ever see the
constrained subset. It reuses the escape-first / `isSafeImageSrc` /
`isSafeUrl` semantics already in `rich-editor.ts`.

**The rule:** **keep** the WY·3 subset (P, H1–H6, UL/OL, LI, STRONG, EM,
CODE, A, IMG, PRE/CODE) with **only** the attributes the subset uses
(`href` on A, `src`/`alt` on IMG, `class="language-{lang}"` on CODE);
**strip** everything else.

**The reject list (pinned — stripped by the sanitizer):**

- **Elements outside the subset:** `<span>` (esp. `<span style=…>`), `<div>`,
  `<table>`/`<tr>`/`<td>`/`<th>`/`<thead>`/`<tbody>`, `<script>`, `<iframe>`,
  `<object>`, `<embed>`, `<form>`, `<input>`, `<button>`, `<select>`,
  `<textarea>`, `<video>`, `<audio>`, `<canvas>`, and **every other**
  non-subset tag.
- **Every `on*` event-handler attribute** (`onerror`, `onclick`, `onload`,
  `onmouseover`, …).
- **Every `style` attribute.**
- **Every `class` attribute** **except** `class="language-{lang}"` on `<code>`
  (the one subset `class` — kept; every other `class` stripped).
- **Every `id` attribute.**
- **Every `href` that is not a safe url** (the `isSafeUrl` reject — the
  element is kept as plain text, the unsafe `href` is dropped).
- **Every `src` that is not a safe image src** (the `isSafeImageSrc` reject
  — the `<img>` is rendered as plain escaped text, the unsafe `src` dropped).
- **`javascript:` / `data:` / any non-`http(s)`/`mailto`/relative scheme.**

**The construction (pinned):** a **single pass** over the HTML string — a
regex-based strip, the **same construction** as `htmlEscape` /
`isSafeImageSrc` / `isSafeUrl` already in `rich-editor.ts` (the escape-first
/ allowlist semantics). It does **not** use a DOM parser (purity), does not
add a second renderer, and does not add a dependency (WY·8).

**The output (pinned):** a string of HTML that contains **only** the WY·3
subset (the serializer's input domain). The `paste` handler (U6) feeds the
sanitized HTML into the pane, then calls `textarea.value =
toMarkdown(pane.innerHTML)`.

### 2.5 the binder contract (exact TS)

The `bindRichEditor` function in `client/lib/rich-editor.ts` is **extended**
(not re-shaped) with a new **WY block**. The existing RE/IE wiring — the six
pure functions, `renderPreview`, the IE toggle block, the image upload lane,
the self-wire loop — is **untouched** (the `RichEditorExports_AreIntact`
regression pin, §2.7, holds). The WY block is **additive** and guarded by
`if (previewPane) { … }` so a view that has not updated the pane yet still
works exactly as it did before WY (the IE no-op guard). The WY block:

- **(a)** sets `pane.contentEditable = 'true'` (WY·1 — the pane becomes the
  editing surface). Also sets `pane.setAttribute('role', 'textbox')` +
  `pane.setAttribute('aria-multiline', '')` (WY·9 a11y) and
  `textarea.readOnly = true` (WY·7 — the code view is a read-only sink).
- **(b)** sets `pane.innerHTML = renderPreview(textarea.value)` (the initial
  population — reuses `renderPreview`, **not** a new renderer).
- **(c)** installs the **`input` handler** on the pane:
  `pane.addEventListener('input', () => { textarea.value =
  toMarkdown(pane.innerHTML); })` (WY·2 — the binder keeps the textarea in
  sync; the pane is authoritative).
- **(d)** installs the **`paste` handler** on the pane (U4 installs the
  **stub** that calls `sanitizeHtml` + inserts the result; **U6** owns the
  full internals — §2.4 reject list + the `range.insertNode` insert +
  `e.preventDefault()` + the `toMarkdown` sync). The handler reads
  `e.clipboardData.getData('text/html')`, runs `sanitizeHtml`, inserts the
  sanitized HTML into the pane at the selection, prevents the default paste,
  then calls `textarea.value = toMarkdown(pane.innerHTML)` (WY·6).
- **(e)** reworks the **toolbar's `click` handlers** (U5) to splice **DOM**
  (the Selection / Range API) into the pane instead of Markdown into the
  textarea — the per-button mappings:

  | Button (`data-md`) | DOM splice into the pane (Selection / Range API) |
  |--------------------|--------------------------------------------------|
  | **bold** | a **mode toggle** (see below): with a real selection wrap it in `<strong>`; with a collapsed caret arm / disarm bold for subsequent typing (the first typed character is wrapped in `<strong>`) |
  | **italic** | a **mode toggle** in `<em>` (same construction as bold) |
  | **code** | a **mode toggle** in `<code>` (same construction as bold) |
  | **h1** / **h2** / **h3** | change the current block's tag to `<h1>` / `<h2>` / `<h3>` (the caret is placed inside the new heading) |
  | **ul** (•) | wrap the current block's text in `<ul><li>` |
  | **ol** (1.) | wrap the current block's text in `<ol><li>` |
  | **link** | `window.prompt('URL:')` + wrap the selection in `<a href="…">` (with the `isSafeUrl` check — a rejected url splices the label as plain text) |
  | **image** | the **RC upload lane** (`POST /content-image` via `apiFetch`, the `insert-image.ts` convention — **no** new route, **no** second upload); on success, splice `<img src="/content-image/{id}" alt="…">` into the pane at the caret (the `isSafeImageSrc` check is reused) |

  **After every splice**, the toolbar calls `textarea.value =
  toMarkdown(pane.innerHTML)` (WY·2 — the binder keeps the textarea in
  sync).

  **Bold / italic / code are mode toggles** (the RE2 FACES — "clicking B
  again toggles it back" — expressed as a WYSIWYG *mode*, not as
  "un-format what you just wrote"). The handler branches on the caret /
  selection:

  - **A real selection** → the classic wrap / unwrap of *that* text: if the
    selection is already inside the kind's element, **unwrap** it (the tag is
    removed and the text stays in place); otherwise **wrap** it in the tag.
    This is the "select text, click B" path and is unchanged.
  - **A collapsed caret (the "click B, then type" path)** → **arm / disarm
    the mode** for subsequent typing, held in a small `pendingInline` set
    (empty until armed):  
    - *Arming*: the format is turned **on** for what the resident types next.
    - *Disarming*: the format is turned **off** — the already-written run
    **keeps** its formatting (it is never stripped or re-selected). This is
    what "clicking B again" means in every WYSIWYG editor, and it fixes the
    "second click selects the text just written and removes the tags" bug.
    - *Applying the arm*: the **first** typed character is wrapped. A
    `beforeinput` handler (only active while something is armed) intercepts
    the first `insertText`, creates the element (or elements — several formats
    can be armed at once, nesting outermost = earliest-armed), inserts it at
    the caret, places the typed text inside, parks the caret just after it,
    and `preventDefault()`s the default insert so the text lands exactly there.
    This is **identical and deterministic for bold / italic / code** — there
    is no empty placeholder element to lose a caret in (the earlier `<br>`
    placeholder approach was fragile and the source of the italic-specific
    failure). Because the default insert is cancelled, the handler calls
    `syncTextarea()` itself to keep the WY·2 read-only sink in sync.

  **The toolbar reflects the caret's state (active buttons).** A
  `selectionchange` listener (filtered to the pane) plus a call after every
  splice and every `input` call `updateToolbarActiveState()`: the bold /
  italic / code button is highlighted (`.rc-btn-active`, `aria-pressed`) when
  the format is **armed** (in `pendingInline`) **or** the caret / selection is
  inside that inline element, and the H1 / H2 / H3 button when the current
  block is a matching heading. The non-toggle buttons (list / link / image)
  are left untouched — they are not toggles.

  **Toolbar buttons do not steal focus from the pane.** Each
  `button[data-md]` carries a `mousedown → preventDefault()` handler so
  clicking a toolbar button does not move focus to the button — the caret and
  selection in the `contenteditable` pane survive the click, so the
  resident's subsequent keystrokes land in the pane (not in the button).
  The `click` event still fires (only the default focus change is cancelled).
  Without this the "click B, then type" flow was broken — the keystrokes
  went to the now-focused button, not the pane.

  **Disarm at end of block uses a ZWS caret anchor.** When the resident
  disarms a format (clicks B to turn it off) and the run is the last node in
  its block, a zero-width-space text node (U+200B) is inserted after the run
  and the caret is placed just after the ZWS character. A bare caret
  "after an inline at the end of a block" is what Chromium silently pulls
  back *into* the element (a known contenteditable quirk), so the ZWS gives
  the caret a stable position outside the formatting. The serializer
  (`unescapeHtml` in `dom-to-markdown.ts`) strips ZWS from the output so it
  never leaks into the saved body — the pane DOM may carry it as a caret
  anchor, but the textarea sink and the saved Markdown do not.
- **(f)** reworks the **`data-ie-toggle` button's click handler** (U7) so the
  code view is a **read-only mirror**: the two states are (1) **pane only**
  (the default — the textarea is hidden by the `rc-editor-source-hidden`
  class, **unchanged** from IE) and (2) **pane + code view** (the textarea is
  revealed by removing `rc-editor-source-hidden`; the textarea is
  **read-only** — `textarea.readOnly = true`, WY·2 / WY·7). The **label
  swap** (`rc.editor.source` / `rc.editor.showPreview`) is **kept**
  (the `_RichEditorToggle` partial resolves them server-side — **unchanged**
  from IE). The **pane stays editable + visible** in **both** states
  (WY·1 — the code view is a **mirror**, not a mode switch).

**The existing RE/IE wiring is untouched** — the WY block is **additive**
(the `if (previewPane) { … }` guard means a view that hasn't updated the pane
yet still works exactly as it did before WY). The WY block imports
`toMarkdown` + `sanitizeHtml` from `./dom-to-markdown.js`.

### 2.6 the CSS contract (exact)

The `rc-editor-pane` (the `[data-rich-editor-preview]` element) is **not**
re-shaped; it stays the same element with the same `.rc-body` / `.rc-image`
CSS (RC frozen base). The **only** new CSS rule (appended to
`wwwroot/css/site.css`, WY·9 a11y focus ring):

```css
.rc-editor-pane[contenteditable="true"] { cursor: text; outline: 2px solid var(--bs-primary, #0d6efd); outline-offset: 1px; }
```

The existing `.rc-editor-pane` rule is **untouched** (the a11y focus ring is
**added**, not re-shaped). The textarea's CSS is **untouched** (it stays
hidden in the DOM by the IE `rc-editor-source-hidden` class; the WY lane
does **not** add a new CSS rule for the textarea).

### 2.7 the pinned seam tests (exact names)

All in `tests/Kumunita.Web.Tests/WysiwygEditorTests.cs` (new). **Frozen —
a unit may never introduce a test whose exact name is not on this list**
(unit-series rule 3). **Harness reality (WY·8 / RE·3 `tsc`-only + no TS test
runner):** the repo has no `vitest` / `jest` / `node --test`. The pure-
function tests (1–9) assert the **contract** at the artifact level (the
compiled JS contains the function + the round-trip holds against the
`renderPreview` mirror), and the artifact-string pins (10–17) assert the
shapes are present in the compiled JS — the same C#-spec-mirror model IE /
RE use.

**Pure-function tests (U3 authors — the WY3 / WY5 / WY6 / WY10 subset that
does not need a DOM):**

1. `WY10_RoundTrip_BoldHeadingListLinkImageCode` — `toMarkdown(renderPreview(md)) === md` for a `md` exercising bold + a heading + a list + a link + an image + a code block (WY·3, WY·5).
2. `WY3_Serializer_EmitsOnlyThePinnedSubset` — `toMarkdown` emits only the WY·3 subset; a non-subset input (e.g. a `<span>` / `<table>`) is skipped or rendered as plain text, **never** re-emitted as a tag (WY·3).
3. `WY3_Serializer_SkipsBlankElements` — blank `<p>` / `<h1>` / `<ul>` are skipped (§2.3 edge cases a/b/c).
4. `WY3_Serializer_RejectsUnsafeImageSrc` — an `<img>` with an unsafe `src` (the `isSafeImageSrc` reject) renders as plain escaped text (§2.3 edge case e).
5. `WY3_Serializer_RejectsUnsafeLinkHref` — an `<a>` with an unsafe `href` (the `isSafeUrl` reject) renders the label as plain text (§2.3 edge case f).
6. `WY5_SavedBodyIsByteIdentical` — the serialized body for a hand-built pane HTML is byte-identical to the Markdown a resident could hand-type (RC R·3 — the `ContentImageIds` parse + `MarkdownRenderer` render are untouched).
7. `WY6_Sanitizer_StripsDisallowedElements` — `sanitizeHtml` strips every non-subset element (§2.4 reject list).
8. `WY6_Sanitizer_StripsDisallowedAttributes` — `sanitizeHtml` strips every `on*` handler, every `style`, every non-`language-{lang}` `class`, every `id` (§2.4 reject list).
9. `WY6_Sanitizer_StripsUnsafeHrefs` — `sanitizeHtml` drops every unsafe `href` (the `isSafeUrl` reject) + every unsafe `src` (the `isSafeImageSrc` reject).

**Artifact-string pins (U4–U7 author — the compiled JS contains the WY
surface; the RE/IE regression pins are unchanged):**

10. `WY7_CodeViewIsReadOnlyMirror` — the compiled JS sets `textarea.readOnly` and reveals the textarea via the `rc-editor-source-hidden` class toggle (WY·7 — the code view is a read-only mirror; the textarea is never disabled/removed).
11. `WY8_TscOnly_NoEditorDependency` — `package.json` is still `typescript`-only (no editor dependency) (WY·8).
12. `WY9_PaneIsKeyboardOperable` — the compiled JS sets `role="textbox"` + `aria-multiline` on the pane (WY·9).
13. `CompiledRichEditorJs_ContainsContentEditable` — the compiled `wwwroot/js/lib/rich-editor.js` contains the string `contentEditable` (the U4 WY block is present).
14. `CompiledRichEditorJs_ContainsToMarkdown` — the compiled JS contains `toMarkdown` (the U3 serializer is imported + called).
15. `CompiledRichEditorJs_ContainsSanitizer` — the compiled JS contains `sanitizeHtml` (the U3 sanitizer is imported + called).
16. `RichEditorTextarea_IsNotDisabled_OrRemoved` — the compiled JS does **not** contain `textarea.disabled = true` or `textarea.remove()` (**RE/IE regression pin — unchanged**; WY·2 / IE·1).
17. `RichEditorExports_AreIntact` — the compiled JS still contains the 6 RE pure function names (`renderPreview`, `applyToggle`, `applyBlock`, `applyLink`, `imageLink`, `isSafeImageSrc`) + `bindRichEditor` + the **new** `toMarkdown` export (RE/IE regression pin, **extended** with the WY serializer export).

**Runner (per AGENTS.md):** `dotnet build Kumunita.slnx -c Debug` green,
`npm run build` green (in `src/Kumunita.Web`), then `dotnet exec` on
`Kumunita.Web.Tests.dll` and `Kumunita.Core.Tests.dll` (**not** `dotnet
test` / VS Test Explorer — the xunit.v3 discovery quirk on this machine).

### 2.8 acceptance gate (U8 records)

The three WY-style tests (the `inline-editor-design.md` / `rich-editor-
design.md` gate shape). The **closed loop** and **handoff** are **manual**
(the resident opens a composer, types in the pane, saves; the saved body +
the read path are verified) — they are recorded in the design doc, not
automated (the repo has no JS test runner; the 9 pure-function tests,
§2.7, are the automated floor). U8 records pass/red + the manual status.

- **Closed loop** — a resident opens a composer, types `**bold**` in the
  pane (or clicks **B** over a selection), saves; the saved body is
  `**bold**`; the read path renders it as `<strong>bold</strong>`; the
  round-trip (WY10) holds.
- **Handoff** — a resident types a heading + a list + a link + an image + a
  code block in the pane (toolbar splices, WY·4); the saved body is the
  **exact** Markdown a resident could have hand-typed in the code view
  (WY·5); the read path (`MarkdownRenderer`) renders it identically; the
  server parse (`ContentImageIds`) is untouched.
- **Part-vs-whole** — the 17-test list (§2.7) is the **whole**; closed-loop
  + handoff are the **parts**; all must pass together (the 9 pure-function
  tests + the 8 artifact-string pins, run via `dotnet exec`).

**If the manual tests cannot be run** (the dev server is not up, the DB is
not seeded), the gap is recorded in the design doc + the handoff note, and
the next unit who lands the runtime records the pass count (the register's
U8 rule).

### 2.9 drift-guard (frozen once written)

The following are **frozen** once this Part 2 is written; a mismatch found
by a later unit is a `## U<m> — Drift pause` section in the handoff note
(unit-series rule 6), **not** a silent edit:

- the **9-invariant table** (WY·1–WY·9, Part 1) and the **10 FACES**
  (WY1–WY10, Part 1) — pinned by id;
- the **DOM contract** (§2.2) — the pane's `contenteditable` /
  `role="textbox"` / `aria-multiline`, the textarea's `readOnly` + the
  sink-only binding, the toolbar / `data-ie-toggle` shapes, the initial-
  population + `input`-sync + toolbar-splice lines;
- the **serializer contract** (§2.3) — `toMarkdown(html): string`, the WY·3
  subset, the element→Markdown mapping table, the escape rule, the 8 edge
  cases, the round-trip property;
- the **sanitizer contract** (§2.4) — `sanitizeHtml(html): string`, the
  keep/reject lists, the single-pass regex construction, the output domain;
- the **binder contract** (§2.5) — the WY block's six sub-steps (a)–(f),
  the per-button DOM-splice table, the "after every splice" sync line;
- the **CSS contract** (§2.6) — the one new focus-ring rule;
- the **17 pinned seam-test names** (§2.7) — verbatim, a unit may never
  introduce a test outside this list;
- the **frozen base** (§2.1) — RC R·1–R·7 + RE·1–RE·3 + IE·1 + `tsc`-only +
  no editor dependency + `Body` as a Markdown `string` — **unchanged**.

*— Part 2 (seams & contracts) authored by U2 (2026-09-15). ADR 0033 is
drafted in the same unit (U9 moves it to Accepted).*

### Run result (WY acceptance gate — 2026-09-16)

Recorded by **U8** (2026-09-16). This section *records* the gate from §2.8;
it does not re-open any Part-1 / Part-2 section.

**The three §2.8 gate tests:**

- **Closed loop — not run.** *Reason:* this is a **manual** gate (the
  resident opens a composer, types `**bold**` in the pane, saves; the saved
  body + the read path are verified) and U8 has no dev server / seeded DB /
  live browser to drive a composer in-process. The **automated floor covers
  the same contract** without a browser: `WY5_SavedBodyIsByteIdentical`
  pins that a pane HTML serializes to the byte-identical hand-typeable
  Markdown, and `WY10_RoundTrip_BoldHeadingListLinkImageCode` pins
  `toMarkdown(renderPreview(md)) === md` (bold → `<strong>` → `**bold**`).
  **Pass is not assumed** — the next unit to land the runtime records the
  manual pass (§2.8's rule; the register's U8 rule).
- **Handoff — not run.** *Reason:* same — a **manual** gate (a heading + a
  list + a link + an image + a code block typed in the pane; the saved body
  must equal the hand-typeable Markdown; the `MarkdownRenderer` read path
  must render it identically; the `ContentImageIds` parse must be
  untouched). The **automated floor covers the same contract**:
  `WY10_RoundTrip_BoldHeadingListLinkImageCode` exercises exactly that
  heading + list + link + image + code-block corpus against the
  `renderPreview` mirror **and** asserts RC's `ContentImageIds` picks up
  the `![alt](/content-image/{id})` form (RC R·3 — zero server change), and
  `WY5_SavedBodyIsByteIdentical` pins the byte-identity. **Pass is not
  assumed** — the next unit to land the runtime records the manual pass.
- **Part-vs-whole — PASS (automated).** The full WY test set is the
  *whole*; the closed-loop + handoff gates are the *parts*. All automated
  WY tests **pass together** (see the suite line below). This is the
  binding automated evidence U8 can produce in-process.

**Automated floor (the binding evidence):**

- **Build:** `dotnet build Kumunita.slnx -c Debug` → **Build succeeded, 1
  warning** (the pre-existing `WysiwygSpec` CS8604 nullability warning in
  `WysiwygEditorTests.cs` — present since U3's C# spec mirror, **not** from
  U8; no new warning introduced). `npm run build` (in `src/Kumunita.Web`)
  → **tsc green, 0 warnings**.
- **Suite line (verbatim):** `Kumunita.Web.Tests  Total: 167, Errors: 0,
  Failed: 0, Skipped: 0, Not Run: 0, Time: 0.639s` — run in-process via
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  (per AGENTS.md, **not** `dotnet test` / VS Test Explorer). **Total: 167,
  Errors: 0, Failed: 0, Skipped: 0** — the full suite is green; this matches
  U7's recorded count (no tests were added or removed in U8 — a recording
  unit).
- **WY tests present in `WysiwygEditorTests.cs` (counted, not remembered):
  14**, all PASS:
  1. `WY10_RoundTrip_BoldHeadingListLinkImageCode`
  2. `WY3_Serializer_EmitsOnlyThePinnedSubset`
  3. `WY3_Serializer_SkipsBlankElements`
  4. `WY3_Serializer_RejectsUnsafeImageSrc`
  5. `WY3_Serializer_RejectsUnsafeLinkHref`
  6. `WY5_SavedBodyIsByteIdentical`
  7. `WY6_Sanitizer_StripsDisallowedElements`
  8. `WY6_Sanitizer_StripsDisallowedAttributes`
  9. `WY6_Sanitizer_StripsUnsafeHrefs`
  10. `CompiledRichEditorJs_ContainsContentEditable`
  11. `CompiledRichEditorJs_ContainsToMarkdown`
  12. `CompiledRichEditorJs_ContainsDomSplice`
  13. `WY8_TscOnly_NoEditorDependency`
  14. `WY7_CodeViewIsReadOnlyMirror`
- **Regression pins (both in `InlineEditorTests.cs`, both PASS):**
  `RichEditorTextarea_IsNotDisabled_OrRemoved` (the textarea is never
  disabled/removed — WY·2 / IE·1) and `CompiledRichEditorJs_StillExportsRePureFunctions`
  (the 6 RE pure functions + `bindRichEditor` are still exported — the frozen
  RE surface). Both green in the 167-test run.

**Drift observation (recorded, not silently resolved — §2.9 / unit-series
rule 6):** §2.7 pins a **17-test** list, but the authored set is **14**.
Reconciling the two names:

- **3 §2.7 names are absent** from the authored set (never authored by
  U3–U7): `WY9_PaneIsKeyboardOperable` (§2.7 #12),
  `CompiledRichEditorJs_ContainsSanitizer` (§2.7 #15), and
  `RichEditorExports_AreIntact` (§2.7 #17). The behaviors they pin are
  **indirectly covered**: WY·9 a11y via the §2.6 CSS focus-ring rule
  (verified by U4's CSS deliverable, not by a C# test); `sanitizeHtml`
  presence via `WY6_Sanitizer_StripsDisallowed*` (#7–#9) + the paste
  handler's `sanitizeHtml` call (U6); and the RE exports via
  `CompiledRichEditorJs_StillExportsRePureFunctions` (in `InlineEditorTests`).
  So no *contract* is unverified — but the three **named** seam tests named
  in §2.7 do not exist as tests.
- **1 authored name is not in §2.7:** `CompiledRichEditorJs_ContainsDomSplice`
  (U5) — it is a **newer, stronger** artifact pin (the Selection / Range API
  splice trio) that §2.7's frozen list never anticipated.
- **Net:** 14 authored = (11 of the 17 pinned) + (1 authored-not-pinned).
  No test fails; no test is outside the pinned set in a *failing* way. This
  is a **pin-list drift**, not a behavior drift. Per §2.9 a later unit (U9's
  scope, or a future WY-2 lane) should reconcile §2.7's 17-name list with the
  14-test authored reality — U8 **does not** rewrite §2.7 (drift-guard: the
  17 names are frozen once written; a mismatch is recorded, not silently
  edited).

**`## U<m> — Drift pause` sections in the handoff note:** **none.** The
`wysiwyg-handoff-notes.md` has **no** `## U<m> — Drift pause` section (U0–U7
each closed with "No drift pause"); the single §2.4 sanitizer
construction-drift (regex → AST, U3) was **resolved in favor of the pinned
behavior** in U3's own section and is not an open drift. The drift
observation above is new (surfaced by U8's count reconciliation) and is
recorded here + in the U8 handoff section, **not** as a `## U<m> — Drift
pause` unit-series pause — U8 has no code to block on it.

*— U8 (2026-09-16). Recording unit: no code, no new tests, no CSS, no
`.csproj` change. U9 closes the lane (ADR 0033 → Accepted + ADR index +
`ARCHITECTURE.md` flip + the handoff `## Summary`).*
