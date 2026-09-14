# Rich content (`RC`) — rolling handoff notes

> **The scratch tier** of the RC lane's three-tier contract (the design
> doc is primary, the register is secondary, this file is scratch).
> One section per unit, **appended, never rewritten**. Each unit writes
> exactly one short section before it exits; the next unit reads only
> that section + its own entry-reads list. A `## U<m> — Drift pause`
> section is a **blocker**: the next unit reads it first and either
> resolves it (recording the resolution in its own section) or carries
> it forward (naming it in its exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##`
> section from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-14
- **Register:** `docs/plans-milestones/plan-rich-content.md` (U01–U08)
- **Design doc (primary):** `docs/design/rich-content-design.md` (U01 authors)
- **ADR:** `docs/adr/0025-rich-content-markdown-and-content-images.md` (U01 authors)
- **Scope:** `Body` stays a Markdown `string` on every document (zero
  migrations); the four `ImageIds` ADDs (`Post`, `PostReply`,
  `LocalizedPage`, **`Announcement`**); the `MarkdownRenderer` image extension; the
  `GET /content-image/{id}` serving route (decision-deferred, 404-orphan);
  the `POST /content-image` upload lane (ADR 0011's boundary, verbatim);
  the composer image control (post/announcement/group-post/static-page);
  the render switch (every UGC body → `MarkdownRenderer` in `.rc-body`;
  every preview → `PlainTextPreview`).
- **Out of scope (the named deferrals for a future RC-2 lane, if one
  comes):** video/audio, image transforms (crop/resize), multi-image
  reordering UI, any WYSIWYG/third-party editor, tables/footnotes in the
  renderer.

<!-- U01 appends its section below this line. One `##` section per unit, in
     order (U01, U02, … U08). Never rewrite a prior section. -->
