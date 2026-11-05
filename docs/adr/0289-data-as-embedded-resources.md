---
id: adr-0289
title: "Data as embedded resources: translation keys, guide pages, and the sample corpus are embedded resources, not C# literals"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0015-ui-view-localization-mechanics.md
  - docs/archive/lanes/improve-code/plan-improve-code.md
supersedes: []
superseded_by: []
updated: 2026-11-03
---

# ADR [0289](0289-data-as-embedded-resources.md) — Data as embedded resources

Status: Accepted
Date: 2026-11-03

> **Abstract:** three C# files hold data as literals:
> `KnownTranslationKeys.cs` (12 963 lines, the en/de/fr/da key
> dictionaries), `FirstBootSeeder.cs` (5 688 lines, the guide-page
> Markdown), and `SampleDataSeeder.cs` (1 632 lines, the sample corpus).
> This ADR moves the data to embedded resources (per-language JSON,
> per-slug Markdown) and makes the C# files loaders. **For** a developer
> touching the localization or bootstrap code. **When to read the rest:**
> before adding a translation key, guide page, or sample record.

## Context

ADR [0015](0015-ui-view-localization-mechanics.md) D2 defines `KnownTranslationKeys` as "a curated
bounded registry that defines and seeds the `en` key set" — a C# dictionary
of key → en source text. The first-boot seeder materializes it `en`-only.
The guide pages (ADR [0057](0057-user-guides-in-help-subtree-and-consistency-loop.md)) and sample corpus (ADR [0129](0129-sample-data-embedded-json-document.md))
are C# string literals in `FirstBootSeeder` and `SampleDataSeeder`.
Translators and content editors edit C# code, not data. The `KnownTranslationKeys`
file grew 69 % since the U00 baseline (8 948 → 15 157 physical lines)
because every new surface adds a key block in C#. The IMPROVE-CODE plan
(C02) moves the data to embedded resources.

## Decision

- **Translation keys.** `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/<surface>.json`
  — per-language, per-surface JSON files, marked as embedded resources in
  the `.csproj`. `KnownTranslationKeys` becomes a loader of ~60 lines.
  `EnValues`, `DeValues`, `FrValues`, `DaValues`, and `AllKeys` keep their
  exact public surface (the ADR 0015 D1 TagHelper floor and the M·12
  completeness view are unchanged).
- **Guide pages.** `src/Kumunita.Core/Bootstrap/GuidePages/{lang}/<slug>.md`
  — embedded Markdown, read by slug. `FirstBootSeeder` keeps the seeding
  logic only (the `IsPristineAsync` gate, the upsert, the `en`-only
  first-boot rule).
- **Sample corpus.** `src/Kumunita.Core/Bootstrap/SampleCorpus/*.json`
  plus a table-driven applier. `BackfillSampleCorpusAsync` (893 / CC 138)
  and `SeedAsync` (732 / CC 97) become loops over records.
- **Parity tests (unit).** Every `en` key exists in de/fr/da, or is listed
  as intentionally missing; no duplicate keys across surface files;
  byte-identical values against a snapshot taken before the move (one
  commit, then the snapshot is deleted).
- **C02 builds against this ADR.** The move is one commit per surface; the
  seeded DB is identical (compare a fresh seed's document export, before vs
  after).

## Consequences

- **Positive:** translators and content editors edit JSON / Markdown, not
  C#.
- **Positive:** the three files drop off the file list (−≈20 000 code
  lines); `KnownTranslationKeys`, `FirstBootSeeder`, and
  `SampleDataSeeder` are off the baseline.
- **Positive:** a new translation key is a JSON entry, not a C# dictionary
  entry — the 69 % growth rate stops.
- **Negative:** resources fail at run time rather than at compile time;
  the parity tests compensate (a missing key is a test failure, not a
  compile error).
- **Negative:** the `en` floor (ADR 0015 D1) is now a runtime lookup, not
  a C# literal; the provider's `en` fallback path must handle a missing
  JSON file gracefully (return the key, not a null reference).
