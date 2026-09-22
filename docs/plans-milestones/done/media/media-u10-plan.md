# U10 — Governance + close: ADR 0011 + `SECURITY.md` (e) + `OPS.md` + `ARCHITECTURE.md` + `README.md` + `## Closed`

> Self-contained: this file + the **entry reads** below is the whole context
> + the **full handoff notes** (U1–U9 sections, for the `## Summary`). A
> prior handoff section (if any) is **U9**.

## Understanding

The **loop-closing** unit (M2 U15 / M3 U12 analog): every doc the feature
must reconcile, plus the ADR that sets the governance shape (ADR 0011 — the
content-addressed, local-volume, content-type-gated, audit-gated backend
choice — C-MED·1/2/3/4/5/6/7/8), plus the design doc `## Media — Closed
(recorded)` + handoff `## Summary`. This is the **last** unit — after it,
the feature is closed.

## Assumptions

- **No code changes** in this unit (ADR/OPSDOC only) — the U10 Exit is
  `run_build` green (no code touched) + the §2.6 `dotnet exec` full gate
  recorded in `## Media — Closed (recorded)`.
- The **second restore surface** (the volume snapshot) is the **honest cost**
  of the local-volume backend (C-MED·7) — it **must** be documented in
  `OPS.md` (a C-MED·7 drift if missing).
- The **`Media__*`** config keys (`RootPath`, `MaxBytes`,
  `AllowedContentTypes`) are the **production** surface — they **must** be
  documented in `OPS.md` (a config drift if missing).

## Approach

Write ADR 0011 (the content-addressed, local-volume, content-type-gated,
audit-gated backend choice — C-MED·1/2/3/4/5/6/7/8) + the `SECURITY.md` (e)
data class (C-MED·1/2/3/5 controls: nosniff, audit-by-default, owner-scoped
write, content-type boundary) + the `OPS.md` second restore surface + the
`Media__*` config keys + the `ARCHITECTURE.md` module row + the `README.md`
feature bullet + the follow-on-lane note (group logos / post attachments /
badge icons — same seam, own design doc). Then append `## Media — Closed
(recorded)` to the design doc (the U3/U5/U9 pass counts, the §2.6 gate
result) + the `## Summary` to the handoff notes.

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — Context/Scope/Invariants +
  §2.2 (the exact C# seams, for the ADR + the SEC/ARCH/README reconciliations)
  + §2.5 (the test names, for the `## Closed` recorded pass counts) + §2.6
  (the gate shape to record) + §2.7 (the rules for the `## Closed` note).
- **the full handoff notes** (`media-file-storage-handoff-notes.md`) — the
  U1–U9 sections (for the `## Summary`).
- `docs/adr/README.md` — the ADR shape + the next-index (0011).
- `docs/adr/0004-data-persistence.md` — the "two schemas" + "documents"
  precedent (for the ADR).
- `docs/SECURITY.md` — the (a)–(d) data-class model + the (e) media class.
- `docs/OPS.md` — the restore-procedure shape + the `Community__*` config
  keys (the `Media__*` keys to add).
- `docs/ARCHITECTURE.md` — the bounded-contexts + module table.
- `README.md` — the Roadmap + Features surfaces.

## Deliverables (≤ 8 files, modify/new)

- `docs/adr/0011-media-and-file-storage.md` (new) + the `docs/adr/README.md`
  row (0011).
- `docs/SECURITY.md` — the (e) media / uploaded bytes data class + the
  C-MED·1/2/3/5 controls.
- `docs/OPS.md` — the second restore surface (the volume snapshot) + the
  `Media__*` config keys.
- `docs/ARCHITECTURE.md` — the `Kumunita.Core.Media` module row + the
  `MediaObject` doc + the `MediaDocTypes` surface + the "one database + one
  volume" note.
- `README.md` — the feature bullet + the follow-on-lane note.
- `docs/design/media-file-storage-design.md` — append `## Media — Closed
  (recorded)` (the U3/U5/U9 pass counts, the gate result).
- `docs/plans-milestones/done/media-file-storage-handoff-notes.md`
  — the `## Summary` section.

## Risks & open questions

- **No code touched in U10** — a **fail-closed** unit-series drift if the
  unit "fixes" a code bug in code while closing (the M2 U15 / M3 U12
  "no-code" close shape). If a code bug is found in the close, the unit
  **must** open a follow-up (a new unit, not a U10 edit).
- **The `## Closed` recorded pass counts** — the U10 unit **must** run the
  §2.6 `dotnet exec` gate (Core + Web test DLLs, AGENTS.md) **and** record the
  pass count verbatim in the `## Closed` section. A `## Closed` section
  without a recorded pass count is a **fail-closed** close shape (the
  recorded pass count **is** the close — a "not recorded" drift).
- **The `Media__*` config keys** (`RootPath`, `MaxBytes`,
  `AllowedContentTypes`) — the U10 **must** document the **production**
  surface (a config drift if missing — the `OPS.md` § "the one database +
  the one volume" + the `Media__*` keys).

## Steps

1. Write ADR 0011 (the content-addressed, local-volume, content-type-gated,
   audit-gated backend choice — C-MED·1/2/3/4/5/6/7/8).
2. Add the `SECURITY.md` (e) data class + the C-MED·1/2/3/5 controls.
3. Add the `OPS.md` second restore surface + the `Media__*` config keys +
   the "the one database + the one volume" note.
4. Add the `ARCHITECTURE.md` module row + the `MediaObject` doc + the
   `MediaDocTypes` surface + the "one database + one volume" note.
5. Add the `README.md` feature bullet + the follow-on-lane note (group logos
   / post attachments / badge icons — same seam, own design doc).
6. Run the §2.6 `dotnet exec` full gate (Core + Web test DLLs, AGENTS.md) —
   record the pass count.
7. Append `## Media — Closed (recorded)` to the design doc (the U3/U5/U9 pass
   counts, the gate result).
8. Append `## Summary` to the handoff notes (shipped units U1–U10, deviations,
   deferred follow-on lanes).
9. `run_build` → green (no code touched in this unit — ADR/OPSDOC only).
