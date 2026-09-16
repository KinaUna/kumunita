# U9 — Close: ADR 0033 → Accepted + ADR index + ARCHITECTURE.md flip + handoff Summary

- **Lane:** WYSIWYG inline editing (`WY`)
- **Unit:** U9 (of U0–U9)
- **Kind:** close (doc-only — no code)

## Goal

Close the lane: move **ADR 0033** from "Draft (lands in U9)" to
**Accepted**; append the ADR row to `docs/adr/README.md`; flip
the `WYSIWYG inline editing` status line in `ARCHITECTURE.md`
from "WY — not yet created" to **WY ✓ live** with the gate
summary; append the `## Summary` section to the handoff note
(the shipped units U0–U8, with their one-liner goal + test count
+ any deviations + the named deferrals list). Move **all** the
unit plans + the register + the handoff note from
`in-progress/` → `done/`. **No code, no build.**

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/ARCHITECTURE.md` §2 (the `Posts/` / `Moderation/` /
   `Events/` / `Projects/` lines — the `WYSIWYG inline editing`
   line to flip; the `Events/` / `Projects/` lines **untouched**).
2. `docs/adr/0033-wysiwyg-inline-editing.md` (U2's draft — the
   ADR to move to Accepted).
3. `docs/adr/README.md` (the ADR index — the row to append).
4. `docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md`
   (U0–U8's sections — the shipped units to summarize).
5. `docs/plans-milestones/plan-wysiwyg.md` (the register — the
   10 units to move to `done/`).

## Deliverables (≤ 4 files, modify + move)

1. **`docs/adr/0033-wysiwyg-inline-editing.md`** — flip
   `Status: Draft (lands in U9)` → `Status: Accepted`.
2. **`docs/adr/README.md`** — append the row:
   `| 0033 | WYSIWYG inline editing: the rendered pane is the editable
   surface (`contenteditable`); the Markdown source is a read-only
   mirror; the serializer is the inverse of `renderPreview`; the
   sanitizer strips paste to the pinned subset; `tsc`-only, no
   editor dependency, `Body` as Markdown, one renderer on the read
   path (Amends 0031 — the "hard non-negotiable" reversed by user
   approval 2026-09-15; Amends 0032 — the rendered-by-default view
   kept, now editable) | Accepted |`
3. **`docs/ARCHITECTURE.md`** — flip the `WYSIWYG inline editing`
   line from "WY — not yet created" to **WY ✓ live** (the gate
   summary from U8). The `Events/` / `Projects/` lines are
   **untouched** (M4/M5/M6 stay Events / Projects / Portability).
4. **`docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md`**
   — append `## Summary` — a table of the shipped units
   (U0–U8), with their one-liner goal + test count + any
   deviations + the named deferrals list (each item named, each
   with a one-line WY-2 candidate or "resolved by U<m>").

**Folder moves** (the **last** action in this unit):
- `docs/plans-milestones/in-progress/plan-wysiwyg.md` →
  `docs/plans-milestones/done/plan-wysiwyg.md`
- `docs/plans-milestones/in-progress/wysiwyg-u00-plan.md` →
  `docs/plans-milestones/done/wysiwyg-u00-plan.md`
- … (U01–U08 the same)
- `docs/plans-milestones/in-progress/wysiwyg-handoff-notes.md` →
  `docs/plans-milestones/done/wysiwyg-handoff-notes.md`

## Exit

- **No build.**
- `ARCHITECTURE.md`'s `WYSIWYG inline editing` line is flipped
  to **WY ✓ live** (the gate summary from U8).
- ADR 0033 is **Accepted**.
- The ADR index has the 0033 row.
- The handoff note's `## Summary` section is present.
- **All** the unit plans (U0–U9) + the register + the handoff
  note are in `docs/plans-milestones/done/`.
- `git status` clean except the 4 modified files + the folder
  moves.

## Notes / deviations

- U9 is the **last** unit — the **last** handoff note it writes is
  the `## Summary` (the WY-2 agent, if one comes, reads the
  `## Summary` section as the **entry** to the lane).
- The folder moves are the **last** action (the user's explicit
  requirement: "When a unit is done, its plan should be moved to
  `docs\plans-milestones\done`" — U9 moves **all** the plans at
  once, since the lane is closed).
- The `## Summary` section is the **handoff** for the WY-2 lane
  (the named deferrals: nested lists, blockquotes, tables,
  footnotes, strikethrough, `execCommand`-based undo/redo,
  mobile-specific editing UX, caret-mapping, localStorage
  persistence — each a **future** lane, not a WY re-open).
