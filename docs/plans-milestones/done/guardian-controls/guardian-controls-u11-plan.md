# GU U11 — close: ARCHITECTURE.md flip + `## GU — Closed (recorded)` + move to done/

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

**Close the lane.** Three things, in this order: **(1)** flip the two
`docs/ARCHITECTURE.md` lines to carry the GU surface (the `UserInfo/` tree
line + the `DelegationGrant`/doc-map line — the ADR 0004 §B.1 layout
convention the other closed lanes set). **(2)** append `## GU — Closed
(recorded)` to the design doc (the M3/M3b/ML-UI/Media "Closed (recorded)"
precedent) — the lane's own **Close** marker. **(3)** `Move-Item` the register
+ the 11 unit plans + the handoff notes from
`docs/plans-milestones/in-progress/` → `docs/plans-milestones/done/` (the
"when a unit is done, its plan moves to `done/`" contract), and confirm
`in-progress/` is empty. **No C# changes, no build** (the gate was recorded
in U10).

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`### Run result (GU
   acceptance gate — <DATE>)`** (U10 appended it) — the gate's **verdict**.
   U11's Close section **references** this verdict (a GREEN gate is the
   precondition for closing; a RED gate means **stop** — a drift pause, not a
   close).
2. `docs/adr/0028-...md` (the whole thing) — the ADR the Close section
   names as the lane's decision record (the M3/M3b "see ADR 000X" voice).
3. `docs/ARCHITECTURE.md` § the **two** lines U11 flips (read them verbatim —
   they are the *primary* source for the edit, not this prose):
   - **The tree line** (~line 85): `│   │   ├── UserInfo/           #
     UserInfoModule (M1) + M2 directory/profile-editor/groups surface:
     DirectoryService (list/detail/preview), Profile, Group, DelegationGrant,
     Component, IUserInfoService` — U11 appends the GU surface to this
     comment (the way the `Posts/` line names "RC ✓ (ADR 0025)" + the design
     doc + the gate date).
   - **The doc-map line** (~line 329): `DelegationGrant  { id, ownerId,
     delegateId, scope: [action], from, to?, revokedBy? }` — U11 adds a
     `GuardianLink` line next to it (the way `DelegationGrant` is documented,
     one line, the field set).
4. `docs/plans-milestones/done/plan-m3-posts-components.md` (or
   `plan-translation-display.md`) — the **closed-lane** precedent for the
   `## <LANE> — Closed (recorded)` section's voice + the **move-to-done/**
   contract (the register + unit plans + handoff notes all live in `done/`).
5. `docs/plans-milestones/in-progress/` — the **current** contents (the
   register + `guardian-controls-u01…u11-plan.md` + the handoff notes) — the
   **exact** set U11 moves.

## Deliverables (3 files modify + the move)

### 1. `docs/ARCHITECTURE.md` (modify — two lines, the layout flip)

- **The `UserInfo/` tree line** (~line 85): append the GU surface to the
  existing comment, mirroring the `Posts/` line's "RC ✓ (ADR 0025) — … ; see
  design/… § … — Closed (recorded) (date)" voice. One edit, e.g. extend the
  trailing comment to add: `GU ✓ (ADR 0028) — GuardianLink (account-scope
  supervision: suspend / membership curation / invitation approval; **no
  content read**, G·1) + the `AccessVia.Guardian` standing (the 9th value) +
  the IUserInfoService guardian seams (formation / suspend / dissolve); see
  design/guardian-controls-design.md § GU — Closed (recorded) (<DATE>)`.
- **The doc-map line** (~line 329): add a new line immediately **after** the
  `DelegationGrant  { … }` line (the "one row per (guardian, child) pair"
  doc, the ADR 0028 §B relationship), in the same one-line-field-set voice:
  `  GuardianLink     { id, guardianId, childId, status: Active|Dissolved,
  createdAt, dissolvedAt?, dissolvedBy? }   // GU (ADR 0028): the account-scope
  supervision link (standing off an Active row, G·2); dissolve one-way (G·5)`.
- **Do not** touch the `M1DocTypes.cs` / `M3DocTypes.cs` / `MediaDocTypes.cs`
  lines (GU rides the **existing** `M1DocTypes` surface, ADR 0004 §B.1,
  additive — U02's one line; the tree line already names `M1DocTypes.cs`, so
  no new doc-types line is added). **Do not** touch the Events / Projects
  lines (M4/M5 stay planned).

### 2. `docs/design/guardian-controls-design.md` (modify — **append only**)

- Append the section (at the very end, after the U10 `### Run result`):

```
## GU — Closed (recorded) (<DATE>)

The GU lane (guardian controls, ADR 0028) is **shipped**. The five actions
(suspend/unsuspend, community + group membership curation, invitation
approval) + formation (add-a-child, the usual verify-email flow) + the
independence handover (dissolve, the GlobalAdmin safety valve) are live.
Standing is **account-scope only** — invariant **G·1** held: the guardian
standing (`AccessVia.Guardian`, the 9th value) **never** resolves a content
read; it is exercised only on the `IUserInfoService` management lanes.

- **Decision record:** ADR 0028 (account-scope supervision; amends 0006,
  0003, 0012, m2b).
- **Gate:** the 11 pinned seam tests + the 4 Web VM tests — see the
  `### Run result (GU acceptance gate — <DATE>)` section above (U10).
- **Layout:** `GuardianLink` rides the existing `M1DocTypes` surface (ADR
  0004 §B.1, additive); `docs/ARCHITECTURE.md`'s `UserInfo/` line + doc-map
  carry the surface (U11).
- **Non-decision (carried forward):** the GlobalAdmin `viaAdmin: true`
  dissolve shell (the admin surface) is **out of scope** here — it is a
  separate admin-shell lane, not a GU one (ADR 0028 §C, the §D G·5 valve).
- **M4/M5/M6 untouched** (Events / Projects / Portability — the named-lane
  discipline: GU is not a renumber).
```

  `<DATE>` is the close date. **Do not** rewrite anything above this line
  (the append-only drift-guard).

### 3. The move to `done/` (12 files, one self-contained command)

Move the register + the 11 unit plans + the handoff notes (13 files) from
`docs/plans-milestones/in-progress/` → `docs/plans-milestones/done/`, **in
one** self-contained PowerShell command (the AGENTS.md rule: one command, no
`$vars` surviving between calls, no here-strings):

```powershell
Move-Item -Path 'docs\plans-milestones\in-progress\plan-guardian-controls.md','docs\plans-milestones\in-progress\guardian-controls-u01-plan.md','docs\plans-milestones\in-progress\guardian-controls-u02-plan.md','docs\plans-milestones\in-progress\guardian-controls-u03-plan.md','docs\plans-milestones\in-progress\guardian-controls-u04-plan.md','docs\plans-milestones\in-progress\guardian-controls-u05-plan.md','docs\plans-milestones\in-progress\guardian-controls-u06-plan.md','docs\plans-milestones\in-progress\guardian-controls-u07-plan.md','docs\plans-milestones\in-progress\guardian-controls-u08-plan.md','docs\plans-milestones\in-progress\guardian-controls-u09-plan.md','docs\plans-milestones\in-progress\guardian-controls-u10-plan.md','docs\plans-milestones\in-progress\guardian-controls-u11-plan.md','docs\plans-milestones\in-progress\guardian-controls-handoff-notes.md' -Destination 'docs\plans-milestones\done'
```

  (Adjust the file list to the **exact** current contents of
  `in-progress/` — read it first. **Do not** move any file that is **not**
  a GU-lane file (a stray non-GU file in `in-progress/` is **not** U11's to
  move — note it and leave it).)

## Precondition (the gate)

**Read the U10 `### Run result` verdict first.** If it is **GREEN**, proceed.
If it is **RED**, **stop** — a drift pause: the lane is **not** closed, the
files are **not** moved, the handoff note records the red + the implicated
seam (U02–U06), and the next agent reconciles. **Never close over a red gate.**

## Exit

`docs/ARCHITECTURE.md` has the two GU lines (the tree line + the doc-map line)
— the layout flipped, Events/Projects untouched. The design doc has `## GU —
Closed (recorded) (<DATE>)` appended (append-only held). `in-progress/` is
**empty** of GU files (the register + 11 unit plans + handoff notes are in
`done/`); any **non-GU** stray is noted + left. Handoff note (append, in the
  **moved** `done/guardian-controls-handoff-notes.md`): a `## Summary` section
  (the lane's sole GU→next handoff artifact) — the shipped units (U01–U11) with
  their one-liner goal + test count + any deviations + the ADR 0028 §E deferral
  list (each named), preceded by a `## U11 — close` section carrying: (a) the
  gate verdict (GREEN/RED, the U10 line), (b) the two
`ARCHITECTURE.md` lines (the tree line + the doc-map line, one line each),
(c) the design-doc Close section (the name + the date), (d) the moved-file
count + a confirmation `in-progress/` has **no** GU files left (the stray, if
any), (e) a confirmation **no C# changed** + **no build** (the gate was
U10's), (f) any anomalies.
