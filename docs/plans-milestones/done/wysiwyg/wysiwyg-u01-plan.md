# U1 — Design doc Part 1 (invariants + FACES)

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U1 (of U0–U9)
- **Kind:** design (doc-only — no code)

## Goal

Author `docs/design/wysiwyg-editor-design.md` Part 1 — the value
chain, the **invariants (WY·1–WY·9)**, the **FACES (WY1–WY10)**, and
the **assumptions** (the ADR 0033 reversal + what still binds).
Mirrors the `inline-editor-design.md` / `rich-editor-design.md`
shape. **No code, no build.**

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md` —
   U0's `## U0 — Kickoff verified` section (the grep counts + the
   RC subset list).
2. `docs/design/inline-editor-design.md` — the IE design doc (the
   FACES/invariant template to emulate).
3. `docs/design/rich-editor-design.md` — the RE design doc (the
   `renderPreview` subset + the toolbar marker ceiling).
4. `docs/philosophy/templates/design-doc.md` — the required section
   set.
5. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — the C# renderer
   (the reference of record for the subset the serializer must emit)
   + `src/Kumunita.Web/client/lib/rich-editor.ts` (the `renderPreview`
   function — the inverse the serializer must be).

## Deliverables (1 file, new)

- `docs/design/wysiwyg-editor-design.md` (~250 lines). Sections:
  - `## Value chain` — IE shipped the rendered-by-default
    experience; WY ships the **editable** rendered experience. The
    arrow moves from *"the resident sees what they will publish"*
    to **"the resident writes what they see"** — the Gmail/Outlook
    model.
  - `## Context` — the gap (the pane is read-only; the only edit
    path is the `</>` toggle → raw Markdown; a resident who does not
    know Markdown cannot bold / list / link *in the rendered text*);
    the ADR 0033 reversal (the "hard non-negotiable" in ADR 0031 D1
    / ADR 0032 is reversed by user approval 2026-09-15; the new ADR
    records the reversal + what still binds); the constraints that
    still bind (RC R·1–R·7, RE·1–RE·3, IE·1, `tsc`-only, no editor
    dependency, one renderer on the read path, `Body` as a Markdown
    `string`).
  - `## Scope` — **In:** the pane becomes `contenteditable`; the
    serializer (`toMarkdown`) + its unit tests; the editing loop
    (the binder keeps the textarea in sync); the toolbar rework
    (splice DOM, not Markdown); the sanitizer + the `paste`
    handler; the code view rework (the `</>` toggle reveals the
    read-only mirror); ADR 0033; the design doc (this file); the
    artifact-string pins; the round-trip property (WY·10).
    **Out (named deferrals for a future WY-2 lane):** nested lists,
    blockquotes, tables, footnotes, strikethrough, `execCommand`-
    based undo/redo, mobile-specific editing UX, caret-mapping
    between the pane and the code view, localStorage persistence
    of the editing preference.
  - `## Invariants (pinned for WY)` — **WY·1–WY·9**, each with a
    one-line WY note:
    - **WY·1** — the pane is the editing surface
      (`contenteditable="true"`).
    - **WY·2** — the textarea is the read-only sink (RC R·3 /
      RE·1 unchanged; never removed, disabled, or re-shaped; the
      server binds it on submit).
    - **WY·3** — the serializer emits **exactly** the RC-pinned
      subset (the WY·3 subset: P, H1–H6, UL/OL, LI, STRONG, EM,
      CODE, A, IMG, PRE/CODE) and **nothing more** (the inverse of
      `renderPreview`).
    - **WY·4** — the toolbar splices **DOM** (not Markdown) into
      the pane (the Selection / Range API on the `contenteditable`).
    - **WY·5** — the saved body is **byte-identical** to what a
      resident could have hand-typed in the code view (RC R·3 /
      RC R·1 unchanged; the read path is untouched).
    - **WY·6** — paste / raw HTML is **sanitized** to the WY·3
      subset before insertion (the escape-first / IsSafeImageSrc /
      IsSafeUrl semantics already in `rich-editor.ts` are reused).
    - **WY·7** — the code view (the `</>` toggle) is a **read-only
      mirror** of the pane (the textarea's `.value` is the
      serialized Markdown; the label swap is kept).
    - **WY·8** — the `tsc`-only constraint stands unchanged (no
      editor dependency in `package.json`; no `.csproj` change).
    - **WY·9** — a11y: the pane is keyboard-operable, reachable in
      tab order, and carries `role="textbox"` + `aria-multiline`;
      the toolbar buttons stay `<button type="button">` + localized
      via `<kw-l>`.
  - `## FACES (pinned, 10)` — **WY1–WY10**, each bound to an
    invariant:
    - **WY1** — the pane is the editing surface (WY·1, WY·9)
    - **WY2** — typing in the pane keeps the textarea in sync
      (WY·2)
    - **WY3** — formatting via the toolbar splices DOM (WY·4)
    - **WY4** — image insert via the RC upload lane (WY·3, WY·4)
    - **WY5** — the saved body is byte-identical (WY·5, RC R·3,
      RC R·1)
    - **WY6** — paste is sanitized to the WY·3 subset (WY·6)
    - **WY7** — the code view is a read-only mirror (WY·7)
    - **WY8** — image-gating is unchanged (the RE
      `data-rich-editor-no-image` precedent)
    - **WY9** — a11y (WY·9)
    - **WY10** — the round-trip property:
      `toMarkdown(renderPreview(md)) === md` for any `md` in the
      RC-pinned subset (WY·3, WY·5)

## Exit

- The file exists with all sections. **No build.**
- Handoff note: a `## U1 — design doc Part 1` section listing the
  **9 invariants** (by id) and the **10 FACES** (WY1–WY10) so U2
  can pin them by id.
- Move this plan file `in-progress/` → `done/` (move **last**).

## Notes / deviations

- The WY·3 subset is the **ceiling** — no element/attribute
  outside the subset may appear in the pane. The serializer
  (`toMarkdown`) is the **exact** inverse of `renderPreview` —
  the same subset, the same escape-first construction, the same
  `isSafeImageSrc` / `isSafeUrl` semantics.
- The ADR 0033 reversal is the **load-bearing** decision — the
  "hard non-negotiable" in ADR 0031 D1 / ADR 0032 is reversed by
  user approval (2026-09-15). The new ADR records the reversal +
  what still binds (RC R·1–R·7, RE·1–RE·3, IE·1, `tsc`-only, no
  editor dependency, one renderer on the read path, `Body` as a
  Markdown `string`).
