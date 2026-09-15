# U8 — Close: ADR 0031 + roadmap + `rc.editor.*` keys + folder moves

- **Lane:** Rich editor (`RE`)
- **Unit:** U8 (of U0–U8 — the lane's close)
- **Kind:** doc / governance (the RE→future-lane handoff artifact)

## Goal

Close the RE lane: accept **ADR 0031** (settling D1 split-view / D2
toolbar-as-splice / D3 client-preview-parity, **amending** ADR 0025's
*"Any WYSIWYG / third-party editor — the `tsc`-only constraint stands"*
non-decision), land the **`RE` named lane** in `Milestones.cs` + README +
`MilestonesTests.cs` (a **named** lane, **not** a renumber — M4/M5/M6 stay
Events/Projects/Portability), register the **`rc.editor.*`** `<kw-l>` keys
(the U04–U06 views reference them; U08 lands the `en` floor), and move the
9 unit plan files `in-progress/` → `done/`.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/adr/0031-wysiwyg-editor-and-toolbar.md` (U01, draft) — the ADR to
   **accept** (flip Draft → Accepted; confirm it **Amends** 0025, not
   supersedes; confirm the `tsc`-only constraint is stated as **unchanged**).
2. `docs/adr/0030-role-independence-composable-elevated-roles.md` — the
   current highest ADR (0031 is the next; the shape/numbering to follow).
3. `docs/adr/0025-rich-content-markdown-and-content-images.md` — the ADR 0031
   **amends** (the *"Any WYSIWYG / third-party editor"* line — verify the
   Amend's scope is exactly that non-decision, not the whole ADR 0025).
4. `src/Kumunita.Web/Milestones.cs` + `tests/Kumunita.Web.Tests/MilestonesTests.cs`
   — the roadmap + its pin (the exact-order + single-in-progress milestone
   the AGENTS.md contract requires kept in step).
5. `README.md` — the **Roadmap** section (the `Milestones.cs` doc-comment
   names the README as the source of truth — keep them together).

## Deliverables (closed set — 6 files + 9 folder moves)

1. **`docs/adr/0031-wysiwyg-editor-and-toolbar.md`** — **Accepted** status;
   the "Amends" section explicitly names ADR 0025's *"Any WYSIWYG /
   third-party editor — the `tsc`-only constraint stands"* non-decision as
   the amended line, and states the `tsc`-only constraint **stands
   unchanged** (what changed is the composer *surface*: split-view +
   toolbar, still `tsc`-only, still no editor dependency).
7. **`docs/adr/README.md`** — the ADR index is a numbered table (currently
   ending at **0030**, verified). Add the **0031** row
   (`| 0031 | WYSIWYG editor + toolbar over the RC Markdown lane (split-view
   live preview + Markdown-splice toolbar, `tsc`-only; Amends 0025) |
   Accepted |`) after the 0030 row — the register's close names this.
2. **`src/Kumunita.Web/Milestones.cs`** — the `RE` named lane (the
   `GP`/`RC` precedent: a named lane with a short ID, **not** a renumber;
   M4/M5/M6 stay Events/Projects/Portability). The lane's state (shipped /
   in-progress) per the close's final status.
3. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** — the test that pins the
   exact order + the single-in-progress milestone; update it **in this same
   commit** as the `Milestones.cs` change (the AGENTS.md contract).
4. **`README.md`** — the **Roadmap** section, in step with `Milestones.cs`.
5. **`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`** — register
   the **`rc.editor.*`** `en` values the U04–U06 views reference via
   `<kw-l>`: `rc.editor.bold`, `.italic`, `.code`, `.h1`, `.h2`, `.h3`,
   `.list`, `.olist`, `.quote`, `.link`, `.image`, `.preview` (the
   ML-UI M·9 precedent — `en` floor only; **no** non-`en` rows). If U06's
   handoff section flagged the RC `rc.markdown_hint` debt, optionally add
   that one key here too (a one-line RC-debt fix, named in the handoff
   section) — **only if** U06's section explicitly asked; otherwise leave it
   (it's RC's, not RE's).
6. **Folder moves** — the 9 unit plan files
   `docs/plans-milestones/in-progress/rich-editor-u0{0..8}-plan.md` →
   `docs/plans-milestones/done/`. **Move the unit plan files *last*** (after
   the ADR/roadmap/keys are landed) — the `done/` folder is the "this is
   finished" signal. The `rich-editor-handoff-notes.md` stays in
   `in-progress/` (it's the scratch tier, not a unit plan — confirm the RC
   close's convention for where handoff notes live; if RC moved its to
   `done/`, move RE's too and record it).

## Exit

- ADR 0031 is **Accepted**; the Amend to 0025 is explicit + scoped to the
  WYSIWYG non-decision (not the whole ADR); the `tsc`-only constraint is
  stated unchanged.
- `Milestones.cs` + `README` Roadmap + `MilestonesTests.cs` are **in step**
  (the `RE` named lane present; the single-in-progress milestone is the
  correct one post-close; the test's pinned order matches the code).
- `dotnet build Kumunita.slnx -c Debug` **green** (the `KnownTranslationKeys`
  + `Milestones` changes compile) **and** `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  **all-green** (the `MilestonesTests` pin passes against the new
  `Milestones.cs`).
- The 9 unit plan files are in `done/`; the handoff note's `## Summary`
  (or `## Lane closed`) section is the **sole** RE→future-lane handoff
  artifact (names the **gated image lanes** — Post Edit / Group Edit /
  Announcement / replies — as RC follow-ups + the undo/redo /
  contenteditable / tables deferrals).
- `git status` clean except the 6 files (ADR + its index row +
  `Milestones.cs` + `MilestonesTests.cs` + `README` +
  `KnownTranslationKeys.cs`) + the 9 folder moves + the handoff append.

## Notes / deviations

- **This is the lane's close** — it is the only unit that touches
  `Milestones.cs` / `README` / `MilestonesTests.cs` / `KnownTranslationKeys`
  / ADR status. U01 *drafted* ADR 0031; U8 *accepts* it. U04–U06 *referenced*
  the `rc.editor.*` keys; U8 *registers* them. Keep that separation clean —
  a unit that "helpfully" lands another unit's deliverable is a
  `## U8 — Drift pause`.
- **The `RE` lane is a named lane, not a renumber** (the `GP`/`RC` precedent,
  the AGENTS.md contract). If `Milestones.cs`'s structure makes a named lane
  awkward (e.g. it's a strict ordered list), that's a **design question** —
  `## U8 — Drift pause` naming the `Milestones.cs` shape, **not** a silent
  renumber of M4/M5/M6.
- **The handoff note's final section** (the RE→future-lane handoff) must name
  the **gated image lanes** as the first RE-2 / RC follow-ups. The image
  button is **gated off** on four surfaces because RC's image lane is
  incomplete there, each a distinct RC seam RE·3 does not invent:
  - **Post Edit** — `UpdatePostAsync` doesn't set `post.ImageIds` (RC drift
    pause (c)).
  - **Group Edit** — `UpdateGroupPostAsync` doesn't set `post.ImageIds`
    (RC drift pause (c) analog).
  - **Announcement New + Edit** — `ContentImageController.Serve` has no
    announcement branch (RC U03 drift pause).
  - **Reply New/Edit (all three Detail views)** — `CreateReplyAsync` /
    `UpdateReplyAsync` don't populate `PostReply.ImageIds` (RC drift pause
    (a)) **and** the reply serve branch is inert-404 (RC U03).

  The text toolbar + preview are **fully on** at all of these (the bodies
  already render via the one `MarkdownRenderer`). A future lane that builds
  the missing RC seam on a surface can then **un-gate** that surface's image
  button (a one-attribute removal of `data-rich-editor-no-image` on that
  view) — note the per-surface RC seam + the gate attribute + the view path
  as the handoff.
- **No code re-shape in the close.** U8 touches only doc / roadmap / keys /
  ADR / folder moves — **no** view, **no** module, **no** controller, **no**
  CSS. If a U03–U06 artifact is wrong and needs a fix, that's a `## U8 —
  Drift pause` (a re-visit of the owning unit), not a close-time edit.
