---
id: design-rich-content-subsystem
title: "Rich content subsystem (RC + editor + inline)"
tier: reference
kind: design
status: current
audiences: [developer, agent]
related:
  - docs/adr/0025-rich-content-markdown-and-content-images.md
  - docs/adr/0031-wysiwyg-editor-and-toolbar.md
  - docs/adr/0033-wysiwyg-inline-editing.md
  - docs/adr/0032-inline-editor-rendered-default-view.md
  - docs/design/rich-content-design.md
  - docs/design/rich-editor-design.md
  - docs/design/wysiwyg-editor-design.md
  - docs/design/inline-editor-design.md
updated: 2026-11-02
---

> **Abstract:** the **one entry point into the rich-content subsystem** — the
> capability chain built on the frozen **RC** Markdown lane: the **rich editor
> (RE)** adds a WYSIWYG authoring surface + toolbar over RC, the **WYSIWYG
> inline editing (WY)** makes the rendered pane the editable surface (two-way
> sync), and the **inline editor (IE)** makes the rendered view the default
> editor (Markdown source hidden behind a toggle). **What this is:** the map
> from the base (RC) to the three authoring-surface extensions, with each
> extension's ADR, design doc, and archived lane. **Who is it for:** a
> developer touching *any* of the Markdown render, editor, or inline-editing
> surfaces; an agent that needs the frozen-base boundary. **When to read the
> rest:** before touching `renderMarkdown`, the editor surface, the toolbar,
> the two-way sync, or the source toggle.

# Rich content subsystem

The rich-content surface is a **capability chain**: RC is the base (the Markdown
render + the content-image lane, invariants R·1–R·7); RE, WY, and IE are
*authoring-surface extensions on top* — each one re-anchors the base as
frozen, unchanged. This hub is the single place that names the chain; each row
links the extension's own design doc (the current truth for that feature), its
ADR, and its archived lane.

| Layer | What it adds | Design doc (current truth) | ADR | Archived lane |
|---|---|---|---|---|
| **RC — base** | the Markdown render + the content-image lane (posts / replies / announcements / pages); invariants R·1–R·7 | [`rich-content-design.md`](rich-content-design.md) | [ADR 0025](../adr/0025-rich-content-markdown-and-content-images.md) | [`archive/lanes/rich-content/`](../archive/lanes/rich-content/plan-rich-content.md) |
| **RE — editor** | a **WYSIWYG authoring surface + toolbar** over RC: a `renderPreview` mirror, a closed toolbar marker set (the ceiling), the client `IsSafeImageSrc` mirror | [`rich-editor-design.md`](rich-editor-design.md) | [ADR 0031](../adr/0031-wysiwyg-editor-and-toolbar.md) | [`archive/lanes/rich-editor/`](../archive/lanes/rich-editor/plan-rich-editor.md) |
| **WY — inline** | the **rendered pane is the editable surface**; the Markdown source is an editable source view (two-way sync); invariants WY·1–WY·9 | [`wysiwyg-editor-design.md`](wysiwyg-editor-design.md) | [ADR 0033](../adr/0033-wysiwyg-inline-editing.md) | [`archive/lanes/wysiwyg/`](../archive/lanes/wysiwyg/plan-wysiwyg.md) |
| **IE — toggle** | the **rendered view is the default editor**; the Markdown source is hidden behind a toggle | [`inline-editor-design.md`](inline-editor-design.md) | [ADR 0032](../adr/0032-inline-editor-rendered-default-view.md) | [`archive/lanes/inline-editor/`](../archive/lanes/inline-editor/plan-inline-editor.md) |

**The one fact to keep straight:** each layer is built **on top of** the one
below it, and **re-anchors that base as frozen, unchanged** (RE re-anchors RC's
R·1–R·7; WY re-anchors RE + RC; IE re-anchors WY + RE + RC). So a change to
RC's render contract is a change to all four rows; a change to one extension's
editor contract is local to that extension.


---

## ADR 0033 — moved content (D11 trim, 2026-11-02)

## Not decided here (explicit non-decisions)

Each is a **future lane** (WY-2, if it comes), named — the ADR 0011 / 0025 /
0031 precedent holds:

- **Nested lists** — the WY·3 subset is flat `<ul>`/`<ol>` + `<li>`.
- **Blockquotes / tables / footnotes / strikethrough** — no branch in
  `MarkdownRenderer`; not in the subset (adding one is a lane that extends
  `MarkdownRenderer` **and** the client serializer in the same commit).
- **`execCommand`-based undo/redo** — the browser's native `contenteditable`
  undo is the floor; a custom undo/redo is a future lane.
- **Mobile-specific editing UX**, **caret-mapping between the pane and the
  code view**, **localStorage persistence of the editing preference** — see
  the design doc §Named deferrals.
- **A third-party editor** (ProseMirror/Quill/Tiptap) — **still out**
  (WY·8 / RE·3 `tsc`-only). This ADR uses `contenteditable` on a hand-rolled
  serializer, **not** an editor framework.


## Revisit when

- A resident asks for nested lists, tables, blockquotes, or a custom
  undo/redo — open the named WY-2 lane (a design doc + ADR), **not** an
  amendment to this ADR.
- The **`tsc`-only / no-editor-dependency** stance is challenged (e.g. a
  request to adopt ProseMirror/Quill/Tiptap) — that is the load-bearing
  constraint of ADR 0031 D1; relaxing it is itself a new ADR that would
  supersede this one.
- The **byte-identical body** guarantee (RC R·3 / WY·5) is challenged — that
  is RC's `ImageIds` source-parse; changing it is a new ADR, not a code
  change.
