# M11 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the register header). Each
entry: files written/touched, decisions locked or refined (with the
design-doc / ADR section it maps to), anything that drifted from the plan's
text, and the next unit's entry reads.

The register is `docs/plans-milestones/plan-m11-portability.md`. Each unit
ships its own self-contained plan in `in-progress/portability-uNN.md`; when
a unit is done its plan file moves to `done/`. The **next unit's** agent
reads its own unit plan + this file's most recent `## U#` section + its
entry reads — it does not re-derive the register.

## Kickoff — M11 is the in-progress milestone; register + unit plans authored

- **What this is.** M11 (Portability — import/export) is already the single
  `StatusNext` milestone in `Milestones.cs` (the M10 U07 close moved it
  there; no M11 work has started — `in-progress/` is empty and there is no
  `src/Kumunita.Core` `Portability/` context, no export/import code, no
  `manifest.json` shape, no archive reader/writer anywhere in the tree:
  grep-confirmed greenfield). This kickoff records the register's authoring,
  not a milestone-status change (M11 is already `StatusNext` — the flip
  happens at **U07 close**, when M11 → `StatusDone` and M12 → `StatusNext`).
- **Files touched (this kickoff: the plan tier only):**
  - `docs/plans-milestones/plan-m11-portability.md` — the unit register
    (U00–U07), [PROPOSED] D1–D10 + C-M11·1–8 + F1–F5, per-unit Goal /
    Entry reads / Deliverables / Exit, + the Deferred lanes.
  - `docs/plans-milestones/in-progress/portability-u00.md` …
    `portability-u06.md` — the six self-contained unit plans (U00–U06;
    U07's close is defined in the register's `## U07` section directly —
    the close unit reads it there, no separate in-progress file).
  - `docs/plans-milestones/m11-portability-handoff-notes.md` — this file.
- **Open veto window:** D1–D10 in the register are the [PROPOSED] set the
  user can still change cheaply **before U00 runs**. After U00 they are
  locked by `docs/design/m11-portability-design.md` + ADR 0108.
- **ADR number for M11:** **0108** (the index ran 0001–0107; `0108` is free
  — verified against `docs/adr/README.md`; `0107` is the M10 PWA lane, the
  current highest). U00 will author
  `docs/adr/0108-portability-import-export.md` + the `docs/adr/README.md`
  row.
- **Grounding facts verified at kickoff (U00's entry reads should re-verify,
  not trust this):** the closed document inventory spans nine `*DocTypes`
  surfaces (`M1DocTypes` / `M3DocTypes` / `M4DocTypes` / `M5DocTypes` /
  `M6DocTypes` / `M9DocTypes` / `MediaDocTypes` / `TagDocTypes` /
  `PageDocTypes`); the media volume is content-addressed
  (`MediaObject.Id` = the content hash, bytes at `{root}/{Id[0..2]}/{Id}`,
  `IMediaFileStore` / `IMediaStore`); the operator-surface house pattern is
  `[Authorize(Roles = GlobalAdmin)]` + one `AccessAudit` row `Via = Admin`
  (ADR 0101/0105/0106, `AdminMessagingController` the shape to mirror,
  `AttachmentController` the `Content-Disposition: attachment` streaming
  shape the `export` action reuses); `System.IO.Compression.ZipArchive` is
  in the .NET 10 BCL (no new package — the lean-stack + tsc-only discipline
  holds); the `LocaleSettings` + language-catalog state lives in
  `Localization/LanguageCatalog.cs` (the `config.json` snapshot source);
  `Milestones.cs` M11 = `StatusNext`, M12 iCal = `StatusPlanned` (so the
  U07 flip is M11 → `StatusDone`, M12 → `StatusNext`).
- **Not touched (historical records, per the "don't edit done records"
  discipline):** the ARCHITECTURE.md value-chain row ("outcome + world
  seams — the loop closes into the residents' lives") + line 784 deferral
  ("Cross-neighborhood migration: versioned JSON export/import service (not
  built yet)") + the README §Deferred "Cross-neighborhood federation"
  deferral — those are the *source* of M11's scope (whole-instance now,
  per-slice / cross-instance deferred); M11's design doc + ADR 0108
  *supersede* the deferral by owning the whole instance and re-naming the
  follow-on lanes (per-slice / cross-instance, import-merge,
  backup-automation) in the register's Deferred lanes list.
- **Next:** U00 (see `in-progress/portability-u00.md`).
