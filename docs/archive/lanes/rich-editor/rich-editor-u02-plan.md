# U2 — (No code) the preview-mirror checklist: pin "what the preview must mirror"

- **Lane:** Rich editor (`RE`)
- **Unit:** U2 (of U0–U8)
- **Kind:** verification (read-only + one handoff section — no code)

## Goal

Before any client code exists, read the **frozen render baseline** and
produce, in the handoff note, a **one-paragraph mirror checklist** — the exact
list of markers `MarkdownRenderer` renders + the exact `src` allowlist
predicate + the exact CSS classes the preview output must carry. This keeps
U03's `renderPreview` from inventing a divergent marker set (RE·2) and makes
U03's entry reads small for a 32K window (U03 reads *this* section, not the
full renderer, to know the ceiling).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — **full read**. The
   supported subset (`Render`/`Inline`/`MatchHeading`/`ImageOrLinkPattern`/
   `IsSafeImageSrc`) is the mirror list + the toolbar ceiling.
2. `src/Kumunita.Web/wwwroot/css/site.css` — the `.rc-body` / `.rc-image`
   block (~lines 733–790) the preview output wraps in.
3. `src/Kumunita.Web/Security/ContentImageIds.cs` — the `ImageIds` parse +
   the `/content-image/{hex}` route shape the preview's `<img>` `src` must
   carry.
4. `tests/Kumunita.Web.Tests/MarkdownRendererTests.cs` — the **7 pinned RC
   tests** — the parity the preview must hold (the preview's test surface is
   this set + the RE·2 additions).
5. `docs/design/rich-content-design.md` — §Pinned contract (the `rc-body` /
   `rc-image` / `IsSafeImageSrc` contract the preview reuses).

## Deliverables (closed set — 1 handoff section)

1. **`docs/plans-milestones/in-progress/rich-editor-handoff-notes.md`** —
   append a `## U2 — Mirror checklist (pinned)` section containing, **exactly**:
   - **The marker set** `MarkdownRenderer` renders (headings 1–6, paragraph,
     `ul` (`-`/`*`), `ol` (`1.`), fenced code, `**bold**`, `*italic*`,
     `` `code` ``, `[label](url)`, `![alt](src)` — the **ceiling**; the
     toolbar + preview must stay inside it);
   - **The client `IsSafeImageSrc` mirror** — the exact accept predicate
     (accept `/content-image/{1–128 lowercase hex}` or a schemeless relative
     path; reject **every** URL scheme — `http:`/`https:`/`data:`/`blob:`/
     `javascript:`) — stated so U03's `renderPreview` can copy it verbatim;
   - **The CSS classes** the preview output must carry (a
     `<div class="rc-body rc-editor-pane">` wrapper; `<img class="rc-image">`)
     — so the preview looks like the read path (RE·1's one-source-of-truth
     stance made visual);
   - **The escape-first stance** — `renderPreview` escapes HTML entities
     **before** applying the marker → HTML map (client-side R·2).
   - A one-line note: **the preview is a *function of* the textarea value,
     re-rendered on every `input`; the textarea is the single source of
     truth (RE·1).**
   - **Nothing else.** No code, CSS, or test change in this unit.

## Exit

- The `## U2 — Mirror checklist (pinned)` section is present in the handoff
  note and every marker/predicate/class it names is verbatim-correct against
  the frozen renderer (a re-read confirms no drift).
- If any frozen seam is missing or differs from RC's close (e.g. the
  `MarkdownRenderer` subset changed), this unit **pauses** and appends
  `## U2 — Drift pause` naming the seam + the delta.
- No build / `npm run build` required (no code touched).
- Move this plan file `in-progress/` → `done/` (move **last**, after the
  handoff section is appended).
- `git status` clean except the handoff-note append + this file's move.

## Notes / deviations

- U2 is the RE lane's equivalent of RC U02 (the "the renderer is one class,
  read-only" no-code unit). It exists to make **RE·2** (preview ↔ renderer
  parity) checkable before U03 writes a single line of TS, and to give a
  32K-window fresh agent (U03) a **short** pin to implement against instead
  of the full 126-line renderer.
- U2 does **not** implement `renderPreview` (U03), does **not** register
  keys (U08), and does **not** touch any view.
