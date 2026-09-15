# Rich editor (`RE`) — rolling handoff notes

> **The scratch tier** of the RE lane's three-tier contract (the design
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

- **Date:** 2026-09-15
- **Register:** `docs/plans-milestones/plan-rich-editor.md` (U0–U8)
- **Design doc (primary):** `docs/design/rich-editor-design.md` (U01 authors)
- **ADR:** `docs/adr/0031-wysiwyg-editor-and-toolbar.md` (U08 authors; Amends
  0025)
- **Scope:** a **WYSIWYG authoring surface** over the RC Markdown lane — a
  **split-view live preview** (`renderPreview` mirroring the RC pinned
  subset) + a **toolbar** (`bold`/`italic`/`code`/`h1`–`h3`/`ul`/`ol`/
  `quote`/`link`/`image`) in one shared `tsc`-only TS module
  (`client/lib/rich-editor.ts`) + the `.rc-editor-*` CSS + the toolbar/preview
  markup on the post / group-post / announcement / static-page composers
  **and the reply composers** (`Posts/Detail`, `Groups/PostDetail`,
  `Announcement/Detail`). The **text toolbar + live preview are on every
  surface** (all those bodies already render via the one `MarkdownRenderer`).
  The **image button is on only where RC's image lane is complete** — Post
  New (U04), Group New (U05), static page (U06); **gated off** (via U03's
  `data-rich-editor-no-image`) on Post Edit (U04), Group Edit + both
  Announcement composers (U05), and the reply composers (U06) — each a
  pre-existing RC drift pause (see the register's image-button matrix).
  **Client-only and additive:** no Core field, no route, no `AccessAction`,
  no `IMediaStore` method, no second server renderer, **no editor dependency
  in `package.json`**. `Body` stays a Markdown `string`; RC's
  `ContentImageIds` parse is byte-identical to the toolbar's image link.
- **Out of scope (named deferrals for a future RE-2 lane, if one comes):**
  undo/redo history stack, spellcheck/dictionary, drag-drop image reordering
  (RC stance), **contenteditable / third-party editor** (hard non-negotiable,
  D1), tables/footnotes in the toolbar or preview (RC out-of-scope set), and
  the **gated image lanes** (un-gating the image button on Post Edit / Group
  Edit / Announcement / replies — a **RC** follow-up per surface: the missing
  RC write-lane `ImageIds` seam [drift pause (a)/(c) analogs] and/or the
  missing RC serve-branch adapter [RC U03 inert-404]; until then those
  surfaces' image button stays gated via `data-rich-editor-no-image`, and
  un-gating is a one-attribute removal, no RE code).
- **Frozen base (RC, unchanged):** `MarkdownRenderer` · `ContentImageIds` ·
  `IMediaStore` · `MediaObject` · the four `ImageIds` fields · the
  `GET`/`POST /content-image` routes · the `.rc-body`/`.rc-image` CSS ·
  `insert-image.ts` · RC R·1–R·7 invariants.

<!-- U0 appends its section below this line. One `##` section per unit, in
     order (U0, U1, … U8). Never rewrite a prior section. -->
