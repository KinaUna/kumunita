# ATT U12 — Close: ADR 0034 + docs sync + design-doc close + handoff summary + file moves

> **Sealed unit.** One fresh agent, ~32K context window. Self-contained. The
> register (`docs/plans-milestones/plan-file-attachments.md`) is the
> cross-reference; when the two disagree, **this file wins for what to do**.
> **Precondition:** U1–U11 all shipped (the design doc, the Core seams, the
> write lanes, the Core + Web tests, the option + upload, the serve route, and
> the editor are all in place and green). This is the **close** unit — no new
> production code; it settles the ADR, syncs the docs, and moves the files.

## Understanding

The lane is **functionally complete** after U11. This unit does what the
"keeping the docs in sync with the code" section of `AGENTS.md` requires of a
new capability that **settles a design question**: it **authorizes** the ADR
(0034 — the next number; the index tops out at 0033), records it in the ADR
index, syncs the four other doc surfaces, closes the design doc, writes the
handoff `## Summary`, and moves the plan files from `in-progress/` to `done/`
(the feature's final home). **No production code changes** — if you find a
production bug that blocks the close gate (the full test run), **stop and
report BLOCKED** rather than fixing production code in a close unit (a
production fix is its own unit; record the blocker and hand back).

## Entry reads (do NOT scan the repo — read exactly these)

1. `docs/plans-milestones/plan-file-attachments.md` — the register: the 10
   invariants, the 9 FACES, the 12-unit register (confirm U1–U11 are all
   checked off in the handoff log), and the **close-gate** section.
2. `docs/plans-milestones/file-attachments-handoff-notes.md` — the **full
   log** (U1–U11). Read top-to-bottom — this is the record of what was
   actually built, including any drift. Your `## Summary` section synthesizes
   it.
3. `docs/design/file-attachments-design.md` — the **complete** design doc
   (Part 1 + Part 2). You are appending a `## File attachments — Closed
   (recorded)` section at the end (the multilingual / rich-content design docs
   carry a "Closed (recorded)" marker — mirror that shape).
4. `docs/adr/README.md` — the ADR index (read the whole table; the last row is
   0033). You are adding the 0034 row.
5. `docs/adr/0025-rich-content-markdown-and-content-images.md` — the ADR you
   are **Amending** (read the "Not decided here" / deferred-lane section — the
   "arbitrary downloads" line this lane resolves). Your 0034 cites it.
6. `docs/adr/0011-media-and-file-storage.md` — the ADR you are **Amending**
   (the allowlist extension point). Your 0034 cites it.
7. `docs/adr/0031-wysiwyg-editor-and-toolbar.md` — a recent ADR that **Amends**
   another (0025). **The shape to mirror** for a multi-Amends ADR (the
   "Amends" header convention + how the amended ADR's status line reads).
8. `docs/SECURITY.md` — find the media / content-image access-control section
   (grep `content-image` or `media`). You are adding the attachment-lane
   access rules (the 404 posture, the one-`Deny`-row rule, the download
   semantics).
9. `docs/OPS.md` — find the `Media__*` config section (grep `Media__`). You are
   adding the `Media__AttachmentAllowedContentTypes` key + its default + the
   restore note (pg_dump + volume, C-MED·7).
10. `docs/ARCHITECTURE.md` — find the persistence / media section (grep
    `MediaObject` or `IMediaStore` or `content-image`). You are adding the
    attachment-lane note (the separate route, the `AttachmentIds` field, the
    serve semantics).
11. `README.md` — the **Roadmap** section (grep `Roadmap` or `Milestone`).
    You are adding the attachment lane as a **named lane** (the `GP` / `ML`
    precedent — a short ID, **not** a renumber; the M4/M5/M6 letters stay
    Events/Projects/Portability). **Also** `src/Kumunita.Web/Milestones.cs` —
    the home page renders it and its doc-comment names the README as the
    source of truth. **Keep them in sync** (AGENTS.md's explicit contract).
12. `AGENTS.md` — the "keeping the docs in sync" + "new capability that
    settles a design question gets an ADR" rules (the close checklist).

## Deliverables (10 file operations)

### 1. `docs/adr/0034-file-attachments-posts-replies-announcements.md` (new)
Author the ADR. (Filename matches the register's canonical name exactly.) **Mirror the shape of ADR 0031** (a recent multi-Amends ADR):
- **Header:** `# ADR 0034 — File attachments lane (downloads on post / reply / announcement)`
- **Status:** `Accepted`
- **Date:** (the close date — use the same date convention as ADR 0033's
  "user approval" line; if the repo uses a fixed date format, match it).
- **Amends:** `0025` (resolves its deferred "arbitrary downloads" follow-on
  for post / reply / announcement) + `0011` (uses its reserved allowlist
  extension point for the attachment lane). **Both** — the design doc's
  Amends line names both; the ADR must too.
- **Context:** the status quo (the store + image lane are live; files could
  not be attached anywhere; ADR 0025 deferred "arbitrary downloads" as a
  follow-on).
- **Decision:** the lane is a **separate** lane from images (separate route
  `/attachment/{id}`, separate allowlist
  `Media:AttachmentAllowedContentTypes`, `Content-Disposition: attachment`)
  while **reusing** the same content-addressed `IMediaStore` (one store, one
  volume, one catalog — C-ATT·1/3). The 10 invariants (C-ATT·1–10) are the
  decision's enforceable core — reference them (the design doc is the
  primary tier for the full text; the ADR states the decision + the key
  invariants).
- **Consequences:** the `AttachmentIds` additive field (zero migrations,
  ADR 0004 §B.1); the two new Web routes; the `MediaOptions` attachment
  allowlist; the editor "Attach file" button (incl. reply composers); the
  serve-route reply parent-resolution (C-ATT·8 — the deliberate difference
  from the image lane's reply-404 drift pause); the audit posture (one
  `Deny` row on UGC deny, zero on every other 404). The **5 drift-paused
  serve tests** (U11) are recorded as a known limitation (the sealed
  `PostService` seam gap) — a future unit that adds a substitutable seam or
  Testcontainers-in-Web.Tests can lift them.
- **Reference:** the design doc
  (`docs/design/file-attachments-design.md`) as the primary tier.

### 2. `docs/adr/README.md` — the index row
Add **after** the 0033 row:
`| 0034 | File attachments lane: downloads on post / reply / announcement (separate route + allowlist, `Content-Disposition: attachment`; reuses the ADR 0011 store; Amends 0025 + 0011) | Accepted |`
(match the table's column alignment — the title column is wide; keep the
`|` pipes aligned).

### 3. `docs/SECURITY.md` — the attachment access rules
In the media / content-image access section, add a sibling subsection for the
attachment lane: the 404-before-decision posture (C-ATT·7), the one-`Deny`-
row-on-UGC-deny / zero-rows-elsewhere audit rule (C-ATT·10), the download
semantics (`Content-Disposition: attachment` — a file is a download, not an
inline render — C-ATT·2), and the allowlist gate (C-ATT·6, SVG excluded).
**Do not** rewrite the image-lane section — add the attachment section
alongside.

### 4. `docs/OPS.md` — the config key
In the `Media__*` config section, add:
- `Media__AttachmentAllowedContentTypes` — comma-separated allowed
  Content-Types for the attachment lane (case-insensitive). **Default:** the
  11 types pinned in the design doc's `MediaOptions` attachment-allowlist
  section (grep the doc for `AttachmentAllowedContentTypes` to find the
  canonical default list) — pdf, msword, docx, ms-excel, xlsx, text/plain,
  text/csv, zip, + the 4 raster (jpeg, png, webp, gif). SVG excluded.
  **Distinct from**
  `Media__AllowedContentTypes` (the image lane).
- A one-line restore note: the attachment bytes live on the **same** volume as
  the image bytes (C-ATT·1) — the existing pg_dump + volume restore (C-MED·7)
  covers both; no new restore surface.

### 5. `docs/ARCHITECTURE.md` — the persistence note
In the persistence / media section, add: the attachment lane is a **separate
lane** from the image lane (separate route, separate allowlist, download
semantics) over the **same** `IMediaStore` (one store, one volume, one
catalog). The `AttachmentIds` additive POCO field (zero migrations, ADR 0004
§B.1) on `Post` / `PostReply` / `Announcement` is **separate** from
`ImageIds`. The serve route's reply branch **resolves the parent post**
(C-ATT·8) — the deliberate difference from the image lane's reply-404 drift
pause.

### 6. `README.md` — the Roadmap entry
In the **Roadmap** section, add the attachment lane as a **named lane** with a
short ID (the `GP` / `ML` precedent — **not** a renumber; M4/M5/M6 stay
Events/Projects/Portability). A one-line entry: "File attachments (lane
`ATT`) — attach files (PDF/Office/text/zip + raster) to posts, replies, and
announcements; downloads (not inline renders); separate allowlist; reuses the
ADR 0011 store. Shipped (ADR 0034)."

### 7. `src/Kumunita.Web/Milestones.cs` — the home-page roadmap
**Keep in sync with the README** (AGENTS.md's explicit contract — the
`Milestones.cs` doc-comment names the README as the source of truth). Add the
matching `ATT` lane entry in the same position the README has it. **If the
`Milestones.cs` shape is a list of milestone records**, add the `ATT` entry in
the same shape (read the file to see the exact record/enum shape — do not
invent a new shape). **Note:** the `MilestonesTests` pin the **exact order +
the single-in-progress milestone** — if adding a `done`-status `ATT` entry
would break that pin, **do not** add it to `Milestones.cs` and **record the
drift** in the handoff note (the Roadmap entry in the README is still added;
the `Milestones.cs` sync is the one place a test pin may forbid the change —
check `tests/Kumunita.Web.Tests/MilestonesTests.cs` before editing).

### 8. `docs/design/file-attachments-design.md` — the close marker
Append a `## File attachments — Closed (recorded)` section at the end:
the close date, the ADR number (0034), and a one-line pointer to the handoff
`## Summary`. (Mirror the multilingual / rich-content design docs' "Closed
(recorded)" marker shape.)

### 9. `docs/plans-milestones/file-attachments-handoff-notes.md` — the `## Summary`
Append a `## Summary` section at the end: a **synthesis** of the U1–U11 log
(the feature is complete; the 10 invariants are enforced; the 9 FACES are
pinned — 5 by the executable Web tests + the Core tests, 5 by the
drift-paused `AttachmentServingTests` record; the ADR is 0034; the docs are
synced). Note the **one known limitation** (the 5 drift-paused serve tests —
the sealed-`PostService` seam gap) as the single follow-on a future unit
could lift. This is the last section before the file moves.

### 10. The file moves (the feature's final home)
The user's explicit convention: **when a unit is done, its plan moves from
`in-progress/` to `done/`**. At close, **all 12** unit plans (U1–U12) are
done. Move them (and the master register + the handoff notes) into a
**`done/file-attachments/`** folder (the feature-folder convention — the
`done/media/` + `done/m3/` + `done/rich-content/` + `done/rich-editor/`
shape):

- `docs/plans-milestones/in-progress/file-attachments-u01-plan.md` → `docs/plans-milestones/done/file-attachments/file-attachments-u01-plan.md`
- `…u02-plan.md` → `done/file-attachments/file-attachments-u02-plan.md`
- `…u03-plan.md` → `done/file-attachments/file-attachments-u03-plan.md`
- `…u04-plan.md` → `done/file-attachments/file-attachments-u04-plan.md`
- `…u05-plan.md` → `done/file-attachments/file-attachments-u05-plan.md`
- `…u06-plan.md` → `done/file-attachments/file-attachments-u06-plan.md`
- `…u07-plan.md` → `done/file-attachments/file-attachments-u07-plan.md`
- `…u08-plan.md` → `done/file-attachments/file-attachments-u08-plan.md`
- `…u09-plan.md` → `done/file-attachments/file-attachments-u09-plan.md`
- `…u10-plan.md` → `done/file-attachments/file-attachments-u10-plan.md`
- `…u11-plan.md` → `done/file-attachments/file-attachments-u11-plan.md`
- `…u12-plan.md` → `done/file-attachments/file-attachments-u12-plan.md`
- `docs/plans-milestones/plan-file-attachments.md` → `docs/plans-milestones/done/file-attachments/plan-file-attachments.md`
- `docs/plans-milestones/file-attachments-handoff-notes.md` → `docs/plans-milestones/done/file-attachments/file-attachments-handoff-notes.md`

**Use `git mv` (via the terminal, one command per file, `;`-chained) for the
moves** — `git mv` preserves the rename in the index (a plain `move` would
show as delete+add). **Do not use a here-string** for the move (the
PowerShell trap, AGENTS.md) — `git mv` is a single-line command per file,
which is safe. **Chain them** with `;` in **one** terminal command (the
`$variable`-across-commands trap means each `git mv` is self-contained — they
are, so chaining is safe). **Verify** after the moves: `git status` shows the
14 renames (R entries), not 14 deletes + 14 adds.

## Close gate (must be green before the moves)

```
dotnet build Kumunita.slnx -c Debug
```
Green on Core + Web. **Then** the **full** test run (the runner quirk —
**never** `dotnet test`, AGENTS.md):

```
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```
Green (the Web suite, <1 s — the 7 writable ATT tests + all pre-existing).
**Then**:

```
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```
Green (the Core suite, ~20 s — the 10 ATT Core tests + all pre-existing; it
spins Testcontainers `postgres:18`). **If either suite has a pre-existing
failure** (not an ATT test), record it in the handoff `## Summary` as a
pre-existing failure and **do not** fix it in this close unit (a production
fix is its own unit). **If an ATT test fails**, that is a **blocker** — stop
and report BLOCKED (a close unit does not fix production code).

## Risks & open questions

- **The `Milestones.cs` test pin is the top risk (deliverable 7).** The
  `MilestonesTests` pin the **exact order + the single-in-progress
  milestone**. If adding a `done`-status `ATT` entry breaks that pin, **do
  not** add it — record the drift (the README Roadmap entry is still added;
  the `Milestones.cs` sync is the one place a test pin may forbid the change).
  **Check `tests/Kumunita.Web.Tests/MilestonesTests.cs` before editing**
  `Milestones.cs`.
- **The ADR number is 0034 — do not use 0035.** The index tops out at 0033;
  the next is 0034. If you see a 0034 already present (a concurrent lane
  claimed it), **stop and report BLOCKED** (a number collision is not a
  close-unit problem to resolve).
- **The Amends line must name both 0025 and 0011** — the design doc's Amends
  line names both; the ADR must too. If you Amend only 0025, you've dropped
  the 0011 allowlist extension point (C-ATT·6's home).
- **Do not fix production code in this unit.** A close unit settles the ADR +
  the docs + the file moves. If the close gate surfaces a production bug (an
  ATT test failing), **stop and report BLOCKED** — a production fix is its own
  unit. (A **pre-existing** non-ATT failure is recorded, not fixed.)
- **The file moves are `git mv`, not `move`.** A plain `move` shows as
  delete+add in the index (the rename is lost). `git mv` preserves the rename
  (an `R` entry in `git status`). **Verify** the 14 `R` entries after the
  moves.
- **The handoff `## Summary` is the last section before the moves.** Append it
  **before** the moves (the file is moved with its Summary intact).

## Steps

1. Read the 12 entry reads (register + handoff log first, then the design doc,
   then the ADR index + the two amended ADRs + the ADR 0031 shape, then the
   four doc surfaces, then the `Milestones.cs` + `MilestonesTests`).
2. **Check the close gate first** (build + the two test runs). If an ATT test
   fails → BLOCKED. If a pre-existing non-ATT test fails → record it, proceed.
3. Author `docs/adr/0034-file-attachments-posts-replies-announcements.md`
   (mirror ADR 0031's multi-Amends shape; Amends 0025 + 0011).
4. Add the 0034 row to `docs/adr/README.md`.
5. Add the attachment access section to `docs/SECURITY.md` (alongside the
   image section).
6. Add the `Media__AttachmentAllowedContentTypes` key to `docs/OPS.md`.
7. Add the attachment persistence note to `docs/ARCHITECTURE.md`.
8. Add the `ATT` lane to the `README.md` Roadmap (a named lane, not a
   renumber). **Then** check `MilestonesTests` — if the pin allows, add the
   matching entry to `src/Kumunita.Web/Milestones.cs`; if not, record the
   drift.
9. Append the `## File attachments — Closed (recorded)` section to the design
   doc.
10. Append the `## Summary` section to the handoff notes (synthesizing U1–U11
    + the known limitation).
11. **Re-run the close gate** (build + the two test runs) — confirm still
    green after the doc edits (the docs don't affect the build, but the
    `Milestones.cs` edit does — if you made it, the Web build + Web tests must
    still be green).
12. **The file moves** — `git mv` the 14 files (12 unit plans + the master
    register + the handoff notes) into `docs/plans-milestones/done/
    file-attachments/`. Chain the `git mv` commands with `;` in one terminal
    command. **Verify** `git status` shows 14 `R` (rename) entries.
13. **Final verification:** `ls docs/plans-milestones/done/file-attachments/`
    shows all 14 files; `ls docs/plans-milestones/in-progress/` shows the 12
    `file-attachments-*` plans are **gone**; the master register + handoff
    notes are in `done/file-attachments/`.
14. Done — the feature is closed.
