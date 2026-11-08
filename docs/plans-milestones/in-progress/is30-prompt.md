---
id: is30-prompt
title: "IS30 prompt — Export docs (the OPS runbook + the M11 note)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS30 — Export docs

## What you are doing

You are the **thirtieth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS29 are committed.

IS25–IS29 landed the operator-gated instance export: content-free by
default, a `Full` scope only under an active `AdminOverride`, audited
`Via: BreakGlass`, and a D10 transparency notification to every non-guest
resident. **IS30 documents the operator's side of that** — the runbook step
the operator follows to run a full export, and the pointer that the M11 design
doc now needs. **This is a docs-only unit: no code, no build, no tests.**

The spec is **ADR 0299**
(`docs/adr/0299-instance-export-operator-gated.md`) — read its *Decision*
(d1–d4) and the supersession note (C-M11·6 "quiet surface" no longer holds for
the full scope). The handoff `## IS29` section and the `## IS28` / `## IS26`
sections in `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
are the recent ground truth.

## The one thing to get right

**The control-map row is NOT this unit.** The plan is explicit: *"The
control-map row is **IS39's** (it lands in the new format there)."* Do **not**
touch `docs/security/control-map.md`. IS30 delivers **only** the two docs
below (≤ 3 doc files).

**Keep the operator's steps faithful to the code, not to the prose.** A full
export is now a three-part break-glass act, and the runbook must say all three
or the operator will be missing one:
1. **Grant the override** — the existing DB-level `AdminOverride` path
   (`docs/ops/privacy-operations.md`, the "Break-glass elevation" bullet). No
   in-app endpoint creates one.
2. **Run the export** — the GlobalAdmin opens the portability surface and
   chooses the **full** scope (the second button; the content-free button is
   the default and needs no override).
3. **See the resident transparency entry** — the D10 notification (IS29) to
   every non-guest resident, kind `instance.full-export`, and the
   `portability.export.full` / `Via: BreakGlass` audit row. The operator
   should be able to *find* both after the fact.

## Deliverables (2 doc files)

1. **`docs/ops/privacy-operations.md`** — add a **"Full instance export"
   section** (a sibling to the existing "Break-glass elevation" bullet, or a
   new top-level bullet under the same "operator-at-the-DB" group). It must:
   - state **when** a full (not content-free) export is the right tool — a
     whole-neighborhood migration / a legal hold where private content must
     move, and that the content-free export is the everyday default;
   - give the **three steps above** in order (grant override → run the full
     scope → confirm the transparency entry + the `BreakGlass` audit row),
     each with the exact in-app surface / the audit-row `Action`
     (`portability.export.full`) so the operator can verify it landed;
   - link **ADR 0299** (the record) and **ADR 0298 / the D10 mechanism**
     (where the notification comes from);
   - keep the existing "Resident requests data export" bullet (that is the
     *resident's own* export, a different surface — do not conflate them).

2. **`docs/design/m11-portability-design.md`** — a short **note** at the
   "quiet surface" line (the `## Human cost` paragraph, ~line 163, where it
   says *"the surface is quiet (one audit row per action)")* and/or the
   §surface decision, that: **for the full scope, C-M11·6's "quiet surface"
   claim is superseded by [ADR 0299](../adr/0299-instance-export-operator-gated.md)**
   — a full export now (a) requires an active `AdminOverride`, (b) is audited
   `Via: BreakGlass`, and (c) emits the D10 resident transparency entry. One
   or two sentences + the link; do **not** rewrite the locked M11 decisions.

Both files must keep their existing front-matter / abstract intact and, where
a new `related:` entry is warranted, add ADR 0299 there (matching each file's
current `related:` style).

## Entry reads (only these — no whole-file reads)

- `docs/adr/0299-instance-export-operator-gated.md` (whole — it is short; the
  *Decision* + *Consequences* + the C-M11·6 supersession note are the spec).
- `docs/ops/privacy-operations.md` (whole — 73 lines; the "Break-glass
  elevation" bullet + the "Resident requests data export" bullet are the
  anchors to sit next to).
- `sed -n '150,166p' docs/design/m11-portability-design.md` (the `## Human
  cost` "quiet surface" line the note attaches to).
- The `## IS28` and `## IS29` sections of
  `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` (the exact
  public names: `portability.export.full`, `Via: BreakGlass`,
  `NotificationKinds.InstanceFullExport`, the two-button surface).
- Doc rules: `docs/DOCS-CONVENTIONS.md` (front-matter, abstract, ADR link
  style). The SEC protocol per-unit contract (D8) applies — the handoff note
  is still required for a docs unit.

## Verification

- [ ] `docs/ops/privacy-operations.md` has a full-export section with the
      three ordered steps (grant → run full scope → confirm transparency +
      audit), linking ADR 0299; the resident-export bullet is untouched.
- [ ] `docs/design/m11-portability-design.md` carries the C-M11·6 superseded
      note linking ADR 0299.
- [ ] **`docs/security/control-map.md` is NOT touched** (that is IS39's).
- [ ] No code changed; no build needed (docs only).
- [ ] No PII in the new text.
- [ ] One `## IS30` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files touched, the sections/lines added, and "no test, because docs-only").
- [ ] One commit.

## Definition of done (IS30)

- [ ] The operator can read one runbook section and execute a full export end
      to end (override → full scope → verify the D10 entry + the
      `BreakGlass` audit row) without opening the code.
- [ ] The M11 design doc no longer reads as though a full export is a quiet
      surface — it points at ADR 0299.
- [ ] The control map is left to IS39.
- [ ] One commit, one handoff section.
- [ ] **Next unit: IS31** (Resident export: notes, folders, page
      translations — E6) — `UserScopeInventory` rows for
      `PersonalNote`/`NoteFolder`/`BookmarkFolder` (by `OwnerId`) and
      `PageTranslation` (by `AuthorId`), the export type map, the
      `ImportOwnershipGuard` owner rule (verify the generic rule covers these
      or add it), and the tests
      `Export_IncludesOwnNotesAndFolders`, `Export_ExcludesOthersNotes`,
      `Import_ForeignNote_Rejected`.
