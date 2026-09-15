# U1 — The design doc + ADR 0031 (settles D1/D2/D3)

- **Lane:** Rich editor (`RE`)
- **Unit:** U1 (of U0–U8)
- **Kind:** design (doc-only — no code)

## Goal

Author the RE lane's **primary-tier design doc**
(`docs/design/rich-editor-design.md`) that pins the RE invariants
(**RE·1–RE·3**), the FACES (**RE1–RE5**), the **exact toolbar marker set**,
the **`renderPreview` subset** + the **client `IsSafeImageSrc` mirror
predicate**, the **pinned seam-test names**, the **acceptance gate**, and the
**drift guard** — and draft **ADR 0031** (settling D1 split-view / D2
toolbar-as-splice / D3 client-preview-parity) that **amends** ADR 0025's
*"Any WYSIWYG / third-party editor — the `tsc`-only constraint stands"*
non-decision.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/adr/0025-rich-content-markdown-and-content-images.md` — the ADR
   shape + the exact *"Any WYSIWYG / third-party editor"* line this ADR
   amends.
2. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — the **exact** supported
   subset (the preview's mirror list + the toolbar's marker ceiling).
3. `src/Kumunita.Web/Security/ContentImageIds.cs` — the `ImageIds` parse the
   image button must stay byte-compatible with.
4. `docs/design/rich-content-design.md` — the RC §Pinned contract (the
   `.rc-body`/`.rc-image` + `IsSafeImageSrc` + `PlainTextPreview` step list
   the preview mirrors).
5. `docs/adr/0030-role-independence-composable-elevated-roles.md` — the
   **current highest** ADR (0031 is the next).

## Deliverables (closed set — 2 files)

1. **`docs/design/rich-editor-design.md`** — the primary tier. Required
   sections (mirror the `rich-content-design.md` / `multilingual-design.md`
   shape): the value chain; **Invariants RE·1–RE·3**; **FACES RE1–RE5**;
   the **Pinned contract** (the `rich-editor.ts` exports + the
   `.rc-editor-*` CSS + the toolbar/preview markup pattern); the **exact
   marker set** the toolbar emits (the *ceiling* — no button may emit a
   marker `renderPreview` doesn't render); the **`renderPreview` subset**
   (bold/italic/code, `h1`–`h3`, `ul`/`ol`, `> `, link,
   `![alt](/content-image/{hex})` image under `rc-image`, paragraph, fenced
   code — and **only** that); the **client `IsSafeImageSrc` mirror** (accept
   `/content-image/{1–128 hex}` + schemeless relative; reject every scheme);
   the **pinned seam-test names** (the 10 in the register §Pinned seam
   tests); the **acceptance gate** (the register §Acceptance gate); the
   **drift guard**; the **named deferrals** (undo/redo, contenteditable,
   tables/footnotes). It re-anchors **RC R·1–R·7** as the frozen base
   (unchanged) this lane builds on.
2. **`docs/adr/0031-wysiwyg-editor-and-toolbar.md`** — the ADR. Settles
   **D1** (split-view live preview, **not** contenteditable), **D2**
   (toolbar-as-Markdown-splice in one shared `tsc`-only TS module), **D3**
   (client preview mirrors the RC pinned subset + `IsSafeImageSrc`
   semantics). States the `tsc`-only constraint **stands unchanged**; what
   changes is that the composer surface is now split-view + toolbar (still
   `tsc`-only, still no editor dependency). **Amends** ADR 0025 (not
   supersedes). Numbers after 0030.

## Exit

- Both files present; the design doc's marker set + `renderPreview` subset
  + client allowlist predicate are stated **concretely** (a fresh agent can
  implement U03 from the doc alone without re-deriving the RC subset).
- ADR 0031 is **present** but its **status is "Draft (lands in U08)"** —
  U08 is the unit that moves it to "Accepted" + does the folder/roadmap
  close. U01 only *authored* it. (If the lane wants the ADR live earlier,
  U08 still owns the ADR-accept + ADR-index update; U01 must not do the
  close's governance steps.)
- No code, test, CSS, or `.csproj` change in this unit.
- Append a `## U1 — Design doc + ADR 0031 drafted` section to the handoff
  note (the marker set + the client predicate as the two load-bearing
  pins) **before** the folder move.
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the two new doc files + the handoff append.

## Notes / deviations

- **The marker set is a ceiling, not a wishlist.** If the design doc is
  tempted to add a button (e.g. *strikethrough*, *table*) whose marker
  `MarkdownRenderer` doesn't render, **cut the button** (RE·2) — or record a
  `## U1 — Drift pause` naming the marker as a **future lane**. Do **not**
  add a button that the frozen renderer can't render.
- U01 does **not** implement the module (U03), does **not** register the
  `rc.editor.*` keys (U08), and does **not** touch `Milestones.cs` /
  `README` / `MilestonesTests.cs` (U08).
- The ADR 0031 filename is **`0031-wysiwyg-editor-and-toolbar.md`** (the
  register's close section names it); U08 verifies the name + number against
  the ADR index before accepting.
