---
id: adr-0033
title: "WYSIWYG inline editing: the rendered pane is the editable surface"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/rich-content-subsystem.md]
supersedes: [adr-0031, adr-0032]
superseded_by: [adr-0165]
updated: 2026-11-02
---

## Context

ADR 0031 (the rich-editor lane, `RE`) and ADR 0032 (the inline-editor lane,
`IE`) together shipped the split-view authoring surface: a toolbar, a source
`<textarea>`, and a rendered preview pane on the 10 composer surfaces (16
editor blocks / 10 view files — U0 verified). IE made the rendered pane the
**default view** and the source **hidden** behind a `</>` toggle. That met
the platform's promise — *"a resident writes what they see"* — for
*seeing*, not for *writing*: the pane is **read-only**, and the only edit
path is to click `</>`, reveal the raw Markdown `<textarea>`, and hand-type
`**bold**` / `## Heading` / `![alt](/content-image/…)` there. That is the
exact inverse of what Gmail/Outlook do: a resident clicks the rendered text,
puts the caret in it, and types.

ADR 0031 D1 / ADR 0032 named the ceiling *"true `contenteditable` / a
third-party editor — hard non-negotiable out,"* on the grounds it would
(a) break the `tsc`-only constraint, (b) introduce an HTML→Markdown
round-trip engine, and (c) fight RC's `ImageIds` parse (which reads the
*source* text). The user has **explicitly approved** reversing that non-
decision (**2026-09-15**). This ADR records the reversal and — load-bearing
— **what still binds**:

- **`Body` stays a Markdown `string`** (RC R·7, unchanged). The server still
  derives `ImageIds` by parsing the *body text* (RC R·3); the read path still
  runs `MarkdownRenderer` (RC R·1). **Zero server change.**
- **The `tsc`-only constraint stands** (RE·3 / WY·8) — `package.json` stays
  **`typescript`-only**; **no** editor dependency; **no** `.csproj` change.
- **One renderer on the read path** (RC R·1) — `MarkdownRenderer` is
  **untouched**; the new serializer is a **client-side** DOM→Markdown
  function in the composer, **not** a second read-path renderer.
- **The saved body is byte-identical Markdown** (RC R·3 / WY·5) — identical
  to what a resident could have hand-typed in the code view.

**This is not a deferral; it is a new policy** — and *narrower* than the
reversed line feared: the reversal unlocks `contenteditable` **in the
composer only**. It does not touch the read path, the storage model, or the
server, and it adds **no** third-party editor (the serializer + sanitizer are
hand-rolled, pure, unit-tested, dependency-free).

## Decision

**D1 — The pane is the editing surface; the textarea is the sink.**
The `rc-editor-pane` (the `[data-rich-editor-preview]` element IE made the
default view) carries `contenteditable="true"` (**set by the binder at
runtime**, not in the Razor) and becomes the **editable** surface. The
`<textarea data-rich-editor>` is **not removed, not disabled, not re-shaped**
— it stays the live form field the server binds (RC R·3 / RE·1 / IE·1
unchanged); the binder keeps it in sync (on every pane `input`,
`textarea.value = toMarkdown(pane.innerHTML)`). The pane is
**authoritative**; the textarea is the **sink** the server reads on submit.
**Two-way sync** (WY·7): the textarea is also an **editable** surface —
typing in the code view re-renders the pane (`input` → `renderPane`), and
typing in the pane updates the code view (`syncTextarea`). The two remain
the same Markdown in different views: the pane is the rendered form,
the code view is the source form.

**D2 — One new pure function: `toMarkdown(html): string`.** A dependency-
free, pure, unit-tested **DOM→Markdown serializer** in
`client/lib/dom-to-markdown.ts` that is the **inverse of `renderPreview`** —
it emits **exactly** the RC-pinned subset (headings 1–6, paragraphs, `- `/`*
` lists, `1. ` lists, fenced code, `` `code` ``, `**bold**`/`*italic*`,
`[label](url)`, `![alt](/content-image/{hex})`) and **nothing more**. It is
the load-bearing new artifact; it is **pure** (no DOM, no side effects) so
it is unit-testable without a browser, and **round-trip-stable** with
`renderPreview` (the WY·10 invariant: `toMarkdown(renderPreview(md)) === md`).

**D3 — A sanitizer for paste + raw HTML.** The pane is `contenteditable`; a
resident can **paste** rich HTML (from Gmail, Word, etc.) that contains
elements/attributes outside the WY·3 subset (`<span style=…>`, `<div>`,
`<script>`, `onerror=`, …). A second pure function, `sanitizeHtml(html):
string` (co-located in `dom-to-markdown.ts`), strips pasted HTML down to the
WY·3 subset **before** insertion (the escape-first / `isSafeImageSrc` /
`isSafeUrl` semantics already in `rich-editor.ts` are reused). This is **not**
a second renderer; it is a **DOM-shape normalizer** so `toMarkdown` only ever
sees the constrained subset.

**The `tsc`-only constraint stands unchanged.** What this ADR changes is the
**composer surface only**: the pane is now `contenteditable`, the textarea is
now an editable Markdown source (two-way sync), and there is a new hand-rolled, pure, unit-tested
serializer + sanitizer. There is still **no editor dependency in
`package.json`**, still **no `.csproj` change**, **no new route**, **no new
server surface**, and **no second renderer on the read path**.

## Consequences

- **The resident edits the rendered text.** A resident clicks the rendered
  pane, puts the caret in it, and types; the toolbar splices **DOM**
  (the Selection / Range API), not Markdown markers. The promise *"a resident
  writes what they see"* is now met for *writing* as well as for *seeing*.
- **The Markdown source is preserved as the sink.** The `<textarea>` remains
  the single source of truth the server binds (RE·1): now **editable** (the
  code view, two-way sync — WY·7), still
  bound, still submitted, its value the serialized Markdown of the pane (or
  the resident's hand-typed Markdown, which the pane then re-renders from).
  `ImageIds` parse (`ContentImageIds`) and `MarkdownRenderer` render are
  **untouched** (RC R·3 / RC R·1).
- **Paste is safe by construction.** Every `on*` handler, `style`,
  non-subset `class`/`id`, non-subset element, and unsafe `href`/`src` is
  stripped before insertion (D3 / WY·6) — the same allowlist semantics
  `renderPreview` / `isSafeImageSrc` / `isSafeUrl` already use.
- **No regression surface.** The six RE pure functions, the IE toggle block,
  the image upload lane, and the 10 composer surfaces' markup are
  byte-identical; the only new authority is the WY block in `bindRichEditor`
  (additive, `if (previewPane) { … }`-guarded), the two pure functions in
  `dom-to-markdown.ts`, and one CSS focus-ring rule. The Web-test suite pins
  the artifact (17 tests — the 9 pure-function contract tests + the 8
  artifact-string / regression pins, §2.7 of the design doc).
- **The code view is an editable source.** The `</>` toggle (IE) still
  swaps the `<kw-l>` label; its semantics are to reveal the **editable**
  Markdown source (the textarea) with **two-way sync** — typing in the
  code view re-renders the pane, and typing in the pane updates the code
  view (the pane stays editable + visible in both states — WY·7).
