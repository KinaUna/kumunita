# SITE U04 — Core: `FirstBootSeeder` default (byte-identical to the shipped `kw-l` text) + seeder pins

## Context (self-contained)

`SITE` is the **site content customization** lane: the `/home` + `/about`
landing surfaces become admin-editable. U00–U03 locked the scope, the design
doc, ADR 0150, and the Core (`SiteContent` doc + `ISiteContentService` +
`SiteContentService` + `SiteContentDocTypes` + DI). **This unit adds the
`FirstBootSeeder` default** (the `SiteContent` singleton, the 13 fields,
the exact `en` source text from `KnownTranslationKeys.cs` as the defaults)
**+ the `SiteContentSeederTests` pins**. **No Web change, no view change,
no admin surface.**

**The `SiteContent` field set (13 fields, pinned — the ADR 0150 D1
ceiling):**
4 text fields (the `en` source text from `KnownTranslationKeys.cs` as the
defaults) + 9 toggle fields (all default `true`):
`HomeHeroEyebrow`, `HomeHeroLead`, `HomeShowAboutButton`,
`HomeShowFeatures`, `HomeShowRoadmap`, `AboutHeroEyebrow`, `AboutHeroLead`,
`AboutShowFeatures`, `AboutShowScope`, `AboutShowPhilosophy`,
`AboutShowProject`, `AboutShowWhatsNew`, `AboutShowContactCta`.

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The defaults are **byte-identical to the shipped `kw-l` text** (the
  `FirstBootSeeder` seed + the in-code fallback; the `LS_U04_SeederTests` /
  `PageServiceTests` "seeded defaults match the shipped text" pin). The
  `en` source text is the **exact** `home.intro_eyebrow` /
  `home.intro_lead` / `about.eyebrow` / `about.lead` strings from
  `KnownTranslationKeys.cs`.
- The seeder is **create-if-missing, idempotent, never-overwrites** (the
  `SeedDefaultPagesAsync` / `SeedLanguages` shape — a second boot does not
  touch a resident-edit).
- The `kw-l` registry entries **stay** (not removed, not re-shaped).

## Goal

Add the `FirstBootSeeder` default (the `SiteContent` singleton, the 13
fields, the exact `en` source text from `KnownTranslationKeys.cs` as the
defaults) + the `SiteContentSeederTests` pins. **No Web change, no view
change, no admin surface.**

## Entry reads (5 files)

1. `docs/design/site-content-design.md` §2.2 — the exact `SiteContent`
   field set + the exact defaults.
2. `src/Kumunita.Core/SiteContent/SiteContent.cs` (U03) — the `SiteContent`
   doc (the 13 fields, the in-code defaults).
3. `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` — the seeder shape
   (the `SeedDefaultPagesAsync` / `SeedLanguages` create-if-missing,
   idempotent, never-overwrites patterns; the `EnDefaultPages()` /
   `DeDefaultPages()` shape).
4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
   `home.intro_eyebrow` / `home.intro_lead` / `about.eyebrow` /
   `about.lead` keys (the exact `en` source text the defaults are derived
   from).
5. `tests/Kumunita.Core.Tests/LS_U04_SeederTests.cs` +
   `tests/Kumunita.Core.Tests/PageServiceTests.cs` — the seeder pins (the
   `PG5_Seeder_TermsAndHelp_UnderSystemRoot_NoDuplicates_AcrossTwoBoots`
   "two boots, no duplicates" shape).

## Deliverables (2 files, new/modify)

1. `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (modified) — the
   `SeedSiteContentAsync` method (the create-if-missing, idempotent,
   never-overwrites shape — the `SeedDefaultPagesAsync` / `SeedLanguages`
   shape). The `SiteContent` singleton (`Id = "singleton"`), the 13 fields,
   the **exact `en` source text from `KnownTranslationKeys.cs`** as the
   defaults. Call it from the existing seeder entry point (the same place
   `SeedDefaultPagesAsync` / `SeedLanguages` are called).
2. `tests/Kumunita.Core.Tests/SiteContentSeederTests.cs` (new) — the
   `SiteContentSeederTests` class. Pins:
   - `FreshBoot_HasExactlyOneSiteContentRow` — a fresh boot has exactly one
     `SiteContent` row (`Id = "singleton"`).
   - `FreshBoot_DefaultsMatchShippedKwLText` — the `HomeHeroEyebrow` /
     `HomeHeroLead` / `AboutHeroEyebrow` / `AboutHeroLead` fields equal the
     **exact** `home.intro_eyebrow` / `home.intro_lead` / `about.eyebrow` /
     `about.lead` English strings from `KnownTranslationKeys.cs` (the `en`
     source text); every section-toggle field is `true`.
   - `SecondBoot_IsIdempotent_NoDuplicateRow_NoFieldChange` — a second boot
     does not add a duplicate row and does not change any field (the
     never-overwrites pin).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green (the new `SiteContentSeederTests` pins pass; the existing
  `LS_U04_SeederTests` / `PageServiceTests` pins are unchanged).
- Handoff note: a `## U04 — Core: FirstBootSeeder default + seeder pins`
  section listing the 2 files (by path) + the `SeedSiteContentAsync` method
  signature + the 3 seeder pins (by class + method) + the "defaults match
  the shipped `kw-l` text" assertion (the exact `en` source text from
  `KnownTranslationKeys.cs`).
- **No Web change, no view change, no admin surface.**
- Move this unit plan `in-progress/site-u04.md` → `done/site-u04.md` (move
  **last**).
