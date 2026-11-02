---
id: adr-0031
title: "WYSIWYG editor + toolbar over the RC Markdown lane"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/attachment-inline-preview-design.md]
supersedes: [adr-0025]
superseded_by: [adr-0032, adr-0033, adr-0086, adr-0107, adr-0165]
updated: 2026-11-02
---

## Context

ADR 0025 (the rich-content lane, `RC`) delivered the **read** half of its
promise — *"a resident can bold a heading, start a list, or show a photo"* —
by extending `MarkdownRenderer` with image support and switching every UGC
body to render through it. ADR 0025's *"Not decided here"* section then
**explicitly deferred** the write half:

> **Any WYSIWYG / third-party editor** — the `tsc`-only constraint stands;
> the composer is a textarea with a Markdown hint.

That deferral named the *boring floor* (a `<textarea>` + a Markdown hint) but
not the *ceiling* (a rendered surface + formatting buttons). At the time of
this ADR the platform sits at that floor: every body authoring surface —
`Posts/{New,Edit}`, `Groups/{New,Edit}`, `Announcement/{New,Edit}`, the
static-page editor, and the reply composers — is a raw `<textarea>` bound to
`Body`, with **no rendered preview** and **no toolbar**. A resident who wants
bold hand-types `**text**` and watches the raw markers while composing.

The constraints the choice must honor (all pre-existing, carried from ADR
0025):

- **The `tsc`-only client convention** (ADR 0025 consequence: *"no editor
  dependency enters `package.json`"*).
- **One renderer** (RC R·1) — the read path is already `MarkdownRenderer`; a
  second renderer or an HTML → Markdown round-trip engine does not exist and
  must not be invented.
- **The source is a `string`** (RC R·3) — the server derives `ImageIds` by
  parsing the *body text*; a `contenteditable` DOM would fight that parse.
- **Escape-first** (RC R·2) — the preview must hold the same construction as
  the renderer.
- **Lean + Boring** (README principles, ADR 0002) — no new dependency, no new
  server surface.

## Decision

**D1 — WYSIWYG = a split-view live preview, not a `contenteditable`.** A
`contenteditable` surface (or a `ProseMirror` / `Quill` / `Tiptap`
dependency) would (a) break the `tsc`-only constraint, (b) introduce an
HTML → Markdown *round-trip* the codebase has no engine for, and (c) fight
RC's `ImageIds` parse (which reads the *source* text, not a DOM). The boring,
correct choice is a **split view**: the existing `<textarea>` (the *source*,
the form field the server binds) above a **read-only rendered pane** that
re-renders on every `input`. **No second renderer, no HTML round-trip, no
dependency.** This is the "live preview pane" ADR 0025's composer line
promised and never shipped. (Layout: the toolbar, the source textarea, and
the preview each sit on their own full-width row — toolbar on top, then
source, then preview — a change to the CSS arrangement only; the D1–D3
decisions above are unaffected by how the rows are laid out.)

**D2 — Toolbar = Markdown text splicing, in one shared `tsc`-only TS
module.** `client/lib/rich-editor.ts` renders a toolbar above any
`textarea[data-rich-editor]` and, per button, **splices the corresponding
Markdown markers** into the textarea's selection (or wraps the selection, or
prepends a block marker at the caret). Buttons are `<button type="button">`
(never `submit`), keyboard-accessible, and localized via the `<kw-l>`
TagHelper. The module exposes `bindRichEditor(root)` and self-wires at load
(the `insert-image.ts` pattern), so a Razor view only needs the module
include. The image button reuses RC's **existing** upload lane
(`POST /content-image` via `apiFetch`); it does not invent a second upload.

**D3 — The client preview mirrors the RC pinned subset + `IsSafeImageSrc`
semantics.** The client cannot call C#, so the preview pane renders the body
with a small client-side function (`renderPreview`) that **mirrors
`MarkdownRenderer`'s supported set verbatim** (headings, paragraphs, lists,
fenced code, inline code / bold / italic, links, and the
`![alt](/content-image/{hex})` image under the `rc-image` class) and **only**
that set. The client image-src predicate mirrors
`MarkdownRenderer.IsSafeImageSrc` (the `/content-image/{1–128 hex}` route
shape + schemeless-relative; **every scheme rejected**). This is **not** a
second *server* renderer (RC R·1 still holds — the read path is still only
`MarkdownRenderer`); it is the **authoring-side preview** the RC scope
promised. The output is **escaped-first** (client-side RC R·2).

**The `tsc`-only constraint stands unchanged.** What this ADR changes is the
**composer surface only**: it is now a split-view + toolbar rather than "a
textarea with a Markdown hint." There is still **no editor dependency in
`package.json`**, still **no `.csproj` change**, still **no new route /
`AccessAction` / `IMediaStore` method**, and **no second renderer on the
server**. The saved body is **byte-identical** to what a resident could have
hand-typed — RC's server-side `ContentImageIds` parse and `MarkdownRenderer`
render are untouched.

## Consequences

- **One new artifact, client-only.** `client/lib/rich-editor.ts` (pure
  functions + `bindRichEditor`), a `.rc-editor-*` CSS block in
  `wwwroot/css/site.css`, the toolbar + preview markup on the composer views,
  and the `rc.editor.*` `<kw-l>` keys (U08). **No** `.csproj` or
  `package.json` change.
- **The toolbar's marker set is a *ceiling*, not a wishlist.** No button may
  emit a marker `renderPreview` does not render (RE·2). A button that emits a
  marker the frozen `MarkdownRenderer` cannot render — e.g. **blockquote
  (`> `)**, which the frozen renderer has no branch for — is **cut**, not
  shipped (recorded as a drift pause at U01; a future lane must extend
  `MarkdownRenderer` **and** the client preview in the same commit).
- **The image button is on only where RC's image lane is complete** (Post
  New, Group New, the static-page editor, **and Announcement New/Edit**) and
  **gated off** (via `data-rich-editor-no-image`) where RC's write-lane or
  serve-branch seam is still an open RC drift pause (Post Edit, Group Edit,
  replies). RE ships a working button only where RC can actually serve the
  image; it does not ship a button that produces 404s.
  <br>
  **Announcement New/Edit were un-gated (2026-09-15)** once the announcement
  serve branch shipped: `ContentImageController.Serve` previously returned a
  fail-closed 404 for announcement-owned images (the RC U03 drift pause — the
  announcement lane is flat-scope, not audience-restricted, so RC's
  auditable-resource + `CanAsync` + `AccessAudit` idiom does not map onto it),
  so the button was inert. The branch now serves through the announcement's
  own flat `Scope`/communities read gate
  (`IAnnouncementService.GetAsync` — public scope always, community scope when
  signed in, a targeted row to its GlobalAdmin / member / moderator; else 404,
  not 403 — no `CanAsync`, no `AccessAudit` row, matching how the
  announcement's own body is gated). With the
  toolbar image button live on every composer, the **redundant RC file-chooser
  (`rc-insert-image` block + `insert-image.ts`) was removed** — the toolbar
  button (the same `POST /content-image` upload + `![alt](/content-image/{id})`
  splice) is the single image path, so the second file-input affordance below
  the preview is dead UI. Replies stay gated: their write-lane (`PostReply`
  `ImageIds`) and serve-branch drift pauses are untouched by this change.
- **Audit-by-default holds** (SECURITY.md §3): the toolbar's only network
  action is the RC image upload, which is already CSRF-aware and
  audited-by-RC; every other button is a local DOM text edit (zero fetches,
  zero audit rows).
- **The read path is unchanged** — `MarkdownRenderer` still renders every
  body; `PlainTextPreview` still strips the same markers; `ImageIds` is still
  derived server-side from the body text. RE regresses nothing RC shipped.
