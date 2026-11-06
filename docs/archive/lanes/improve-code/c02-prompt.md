---
id: c02-prompt
title: "C02 — Data out of code (D5)"
tier: archive
kind: prompt
status: archived
audiences: [developer, agent]
lane: improve-code
---
# C02 — Data out of code (D5)

## What you are doing

You are the third agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C02**, the third of 13. It is a **data-move** unit: you take data currently held as C# literals (translation keys, guide pages, the sample corpus) and move it into embedded resources, so the C# becomes thin loaders. You code against **ADR 0289** (the contract C01 already wrote).

**You are NOT** doing C00 (shipped — the gate and baseline exist) or C01 (shipped — ADRs 0286–0289). You are **NOT** doing C03–C13. You write **no new feature, no route or rendered-HTML change, no document-schema change.** You do not touch the `IUserInfoService` / `IProjectService` splits (that is C07/C08 against ADRs 0286/0287).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `da04e34b`.
- Commit style: `feat(improve-code): C02 — <short description>` (conventional commits, em dash).
- Gates you must keep green: `tools/code/code-check.ps1` (the ratchet) and `tools/docs/improve-check.ps1` (docs gates).
- The files you are shrinking are on the C00 baseline — moving their data off them **retires** those baseline entries. Retire them in the same commit you fix them.

## Entry reads

- **ADR 0289** (`docs/adr/0289-data-as-embedded-resources.md`) — the contract you build against.
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (structure: the four `*Values` dictionaries + `AllKeys` + the 12 U04 per-surface views) and `LocalizationService.cs`.
- `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the `*GuidePages` members) and `SampleDataSeeder.cs` (`BackfillSampleCorpusAsync` 893/CC 138, `SeedAsync` 732/CC 97).
- The plan's **C02** and **D5** sections, and ADR **0015** (the `KnownTranslationKeys` / provider-floor seam this must not break).

## Do

- **Translation keys:** generate `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/<surface>.json` (surface = the key prefix) from the current dictionaries via a one-off script kept under `tools/code/`; mark them embedded resources in the `.csproj`. `KnownTranslationKeys` becomes a ~60-line loader. **`EnValues` / `DeValues` / `FrValues` / `DaValues` / `AllKeys` keep their exact public surface** (the ADR 0015 `<kw-l>` provider floor and the M·12 completeness view must be unchanged). **Delete the 12 U04 views** and their test.
- **Parity tests (unit):** every `en` key exists in de/fr/da or is listed intentionally missing; no duplicate keys across surface files; byte-identical values against a snapshot taken before the move (one commit, then delete the snapshot).
- **Guide pages:** `src/Kumunita.Core/Bootstrap/GuidePages/{lang}/<slug>.md` embedded, read by slug. `FirstBootSeeder` keeps only the seeding logic (the `IsPristineAsync` gate, the upsert, the `en`-only first-boot rule).
- **Sample corpus:** `src/Kumunita.Core/Bootstrap/SampleCorpus/*.json` plus a table-driven applier. `BackfillSampleCorpusAsync` and `SeedAsync` become loops over records.

## Do NOT do

- Do NOT rename or remove any of `EnValues` / `AllKeys` / the `*Values` surface — loaders only.
- Do NOT change routes, rendered HTML, or document shapes.
- Do NOT touch the ADR 0286/0287 service splits, the baseline ceiling values, or any gate.
- Do NOT commit a new baseline `max` higher than today — the ratchet is shrink-only.

## Exit criteria (all must pass before you commit)

1. `KnownTranslationKeys.cs`, `FirstBootSeeder.cs`, and `SampleDataSeeder.cs` are **off** the `code-check` file list (−≈20 000 code lines); the 12 U04 views are gone.
2. A **fresh seed's document export is byte-identical** before vs. after the move (compare a clean seed, not a drifted dev DB).
3. `pwsh tools/code/code-check.ps1` → exit 0; the entries you fixed are retired (removed, not lowered).
4. `pwsh tools/docs/improve-check.ps1` → exit 0.
5. `dotnet build` clean and all localization tests pass unmodified **apart from the deleted U04 test**.

## Practical notes

- **The retirement rule is a hard stop.** The moment a file lands under its ceiling (or its data is gone), delete its baseline entry in the same commit — or the ratchet's stale-entry check (C00 rule 3) fails.
- Resources now fail at **run time**, not compile time — that is exactly what the parity tests are for (a missing key = test failure, not a compile error). Keep the `en` floor (ADR 0015 D1) a graceful lookup: a missing JSON file returns the key, never a null reference.
- Commit in per-surface steps (keys → guide pages → corpus) so a bad move is isolated.
- Commit with: `feat(improve-code): C02 — data out of code (D5): keys/guide pages/sample corpus to embedded resources`.
