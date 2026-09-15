# U0 — Kickoff verification

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U0 (of U0–U9)
- **Kind:** verification (doc-only — no code)

## Goal

Verify the surface (the 10 composer surfaces, the `rc-editor` blocks, the
pane element, the toolbar, the textarea, the `data-ie-toggle` button) and
the RC-pinned subset (the `renderPreview` mirror + the `MarkdownRenderer`
C#) are as the design doc will pin them. Author the handoff-note skeleton
(the "Lane open" section). **No code, no build.**

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/Views/` (grep `class="rc-editor"` +
   `data-rich-editor-preview` + `data-ie-toggle` to count the surfaces —
   the 10 composer surfaces RE/IE shipped).
2. `src/Kumunita.Web/client/lib/rich-editor.ts` — the full module (the
   `renderPreview` subset + the 6 pure functions + `bindRichEditor` +
   the IE toggle block).
3. `src/Kumunita.Web/Security/MarkdownRenderer.cs` — the C# renderer
   (the reference of record for the subset the serializer must emit).
4. `src/Kumunita.Web/Security/ContentImageIds.cs` — the `ImageIds`
   parse the serializer must stay byte-compatible with.
5. `docs/adr/0031-wysiwyg-editor-and-toolbar.md` +
   `docs/adr/0032-inline-editor-rendered-default-view.md` — the two
   ADRs this lane reverses / builds on.

## Deliverables (1 file, new)

- `docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md` — the
  **skeleton only** (the header + the "Lane open" section + the
  `<!-- U0 appends its section below this line. -->` marker). The
  skeleton mirrors the `inline-editor-handoff-notes.md` shape:
  the "Lane open" section names the register, the design doc, the
  ADR, the scope, the out-of-scope deferrals, and the frozen base.
  **No** `## U<m> —` section yet (U0 appends its own section after
  this one).

## Exit

- The handoff-note skeleton is present. The `## Lane open` section
  names (a) the composer surfaces (the grep count), (b) the
  RC-pinned subset (the `renderPreview` mirror list), (c) the
  frozen base (RC R·1–R·7 + RE·1–RE·3 + IE·1 — **unchanged**),
  (d) the new invariants (WY·1–WY·9), (e) the **ADR 0033
  reversal** (the "hard non-negotiable" in ADR 0031 D1 / ADR 0032
  is **reversed** by user approval 2026-09-15; the new ADR records
  the reversal + what still binds).
- Append a `## U0 — Kickoff verified` section to the handoff note
  with the grep counts + the RC subset list + the ADR number
  (0033) + the user-approval date (2026-09-15).
- Move this plan file `in-progress/` → `done/` (move **last**).
- `git status` clean except the one new handoff-note file.

## Notes / deviations

- The RC-pinned subset is the **ceiling** the serializer must emit
  — no element/attribute outside the WY·3 subset may appear in the
  pane (WY·3).
- The ADR 0033 filename is **`0033-wysiwyg-inline-editing.md`**
  (the register's close section names it); U9 verifies the name +
  number against the ADR index before accepting.
