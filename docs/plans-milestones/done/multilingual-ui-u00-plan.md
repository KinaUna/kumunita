# U0 — Multilingual UI wiring: kickoff (verification + plan finalization)

**Milestone:** `ML-UI` (the UI-wiring completion of the `ML` / ADR 0005
promise) · **Read first (5 min):**
`docs/plans-milestones/in-progress/plan-multilingual-ui.md` (master register —
the four verified gaps, the pinned contract, invariants M·10/M·11, FACES
L1–L9, the 9-unit table, unit-series rules) + the **U0** section of
`docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md` (this
session's verification record). **No repo-wide scan.**

## Goal
Confirm the four `ML` gaps still hold (so the lane is not redundant), create
the scratch handoff note with this U0 section, and finalize this plan file's
per-unit entry-read lists. **Doc unit — no code, no build.**

## Entry reads (the minimal set — read in this order)
1. `docs/plans-milestones/in-progress/plan-multilingual-ui.md` — the whole
   file (it is this session's output; the gaps, the pinned contract, the
   invariants, the FACES, and the unit table are all here).
2. The `ML` close record — the "## Multilingual — Closed (recorded by U9)"
   section of `docs/design/multilingual-design.md` (the seam-freeze list + the
   explicit "recorded, not shipped" `/about` note that U7 takes).
3. `docs/plans-milestones/done/plan-multilingual.md` — the `ML` unit register
   (the convention this lane copies: three-tier contract, per-unit template,
   unit-series rules, the FACES / pinned-seam-test / acceptance-gate shapes).
4. `src/Kumunita.Core/Localization/` (the folder listing) — confirm the
   frozen seams are present: `ITranslationProvider.cs`,
   `TranslationProvider.cs`, `ILocalizationService.cs`,
   `LocalizationService.cs`, `LanguageCompleteness.cs`,
   `LanguageCatalog.cs`, `LocaleSettings` (in the `LanguageCatalog.cs` file),
   `LocalizedPage.cs`, `TranslationResource.cs`.
5. `src/Kumunita.Web/Controllers/{LanguagesController,LocaleController,
   StaticPagesController}.cs` (the `ML` Web surfaces — confirm they exist and
   are the ones U6/U7 build against).

## Verification (the four gaps — check each before declaring U0 done)
Run these three greps and record the counts in the handoff note's U0 section
(the expected result is **zero** for the first two and **one** for the third,
which is only the plan file):
1. `ITranslationProvider` in `src/Kumunita.Web/Views/**/*.cshtml` →
   expected **0** (Gap 1: no view resolves through the provider).
2. `kw-l|LocalizeTagHelper|KnownTranslationKeys` anywhere in `src/` →
   expected **0** (the TagHelper and registry do not exist yet — Gaps 1/3).
3. `Store<TranslationResource>|Store<LocalizedPage>` in
   `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` → expected **0**
   (Gap 2: the seeder stores no `en` string/page rows — only the catalog row
   + the `LocaleSettings` singleton).

If **any** of these counts is unexpected, the lane may already be partially
shipped (or the `ML` close record is stale) — **pause** and record
`## U0 — Drift pause` in the handoff note with the exact grep output before
proceeding.

## Deliverables (2 files)
1. **New:** `docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md`
   — the scratch tier, created with the **U0** section (this session's
   verification record: the four gaps with their grep evidence, the frozen-seam
   list, the only-allowed-ADDs, the roadmap state, D1/D2, the unit count, the
   drift-pause count = 0). One section per unit, appended — never rewritten.
2. **This file** — `docs/plans-milestones/in-progress/multilingual-ui-u00-plan.md`
   — moved to `docs/plans-milestones/done/` on exit (per the unit protocol,
   each unit plan file moves to `done/` when the unit exits).

**Out of this unit (unit-series rule 1):** no code in `src/Kumunita.Core` or
`src/Kumunita.Web`, no `KnownTranslationKeys`, no seeder step, no TagHelper,
no view edits, no tests, no README / ADR / `Milestones.cs` edits. U1 creates
`multilingual-ui-u01-plan.md` as it starts.

## Exit
- The four-gap verification above ran and the counts match the expected values
  (recorded in the handoff note's U0 section with the exact grep output).
- The handoff note exists at
  `docs/plans-milestones/in-progress/multilingual-ui-handoff-notes.md` with the
  **U0** section appended **before** this unit plan file's folder move.
- This unit plan file moved to
  `docs/plans-milestones/done/multilingual-ui-u00-plan.md`.
- **No build** (doc unit) — but confirm `git status` shows only the two new
  files above as untracked (no stray edits to `src/`).
