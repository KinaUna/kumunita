# ADR 0192 — Documents

Status: **Proposed** (scaffolding landed)
Date: 2026-10-27
Relates to: ADR 0188 (list rows), ADR 0184 (detail pages), ADR 0182 (forms)

## Decision

- New partial `Views/Shared/_FileTile.cshtml` (model: the content type) renders a rounded 48x56 tile with the
  file kind as text (PDF, DOC, XLS, PPT, IMG, TXT, ZIP, else FILE), tinted per kind with `color-mix` over the
  surface so it works in all four palette/mode combinations. It is `aria-hidden`; the content type is still
  printed as text on the detail page, and the tile never carries the kind by colour alone.
- List rows (`Document/Index.cshtml`): tile + title / summary / meta (size, date); Download is an outline
  button. The rows are the shared row cards (ADR 0188). The size helper is the detail page's `FormatSize`.
- Detail: the tile sits beside the title; tag and folder labels are `.kmb-chip`; the file facts are a
  `.kmb-detail-card` with muted labels (`.kmb-meta-label`).
- Upload and Edit forms already use `form > .card` and pick up the form look (ADR 0182).

## Open

- The list shows no folder grouping (folders remain "a pure organizational aid"); the folder chip appears on
  the detail page only.
- Per-kind hues are fixed values, not palette tokens.
- Not previewed live (no documents in the dev data); verify with a PDF, an image and an unknown type.
