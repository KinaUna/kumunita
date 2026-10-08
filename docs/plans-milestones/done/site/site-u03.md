# SITE U03 — Core: `SiteContent` doc + `ISiteContentService` + `SiteContentService` + `SiteContentDocTypes` + DI

## Context (self-contained)

`SITE` is the **site content customization** lane: the `/home` + `/about`
landing surfaces become admin-editable. U00–U02 locked the scope, the design
doc, and ADR 0150. **This unit implements the Core** — the `SiteContent`
doc (the 13 fields, the exact defaults), the `ISiteContentService`
interface (the `GetAsync` / `SaveAsync` shape), the `SiteContentService`
implementation (the ADR 0050 single-write-lane shape, the `AccessAudit`
row), the `SiteContentDocTypes` (the ADR 0004 §B.1 additive doc type), and
the DI registration. **No Web change, no view change, no admin surface.**

**The `SiteContent` field set (13 fields, pinned):**
4 text fields (the `en` source text from `KnownTranslationKeys.cs` as the
defaults) + 9 toggle fields (all default `true`):
`HomeHeroEyebrow`, `HomeHeroLead`, `HomeShowAboutButton`,
`HomeShowFeatures`, `HomeShowRoadmap`, `AboutHeroEyebrow`, `AboutHeroLead`,
`AboutShowFeatures`, `AboutShowScope`, `AboutShowPhilosophy`,
`AboutShowProject`, `AboutShowWhatsNew`, `AboutShowContactCta`.

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The write is the **ADR 0050 single-write-lane shape** (one
  `ISiteContentService.SaveAsync`, one `AccessAudit` row per save,
  `Via = Admin`, action `site.save`, `TargetKind` "site" — the
  `signup.set-open` / `timezone.set-default` shape).
- The defaults are **byte-identical to the shipped `kw-l` text** (the
  `SiteContent` field defaults are the exact `en` source text from
  `KnownTranslationKeys.cs`).
- The `SiteContent` doc is a **singleton** (one row per instance,
  `Id = "singleton"` sentinel — the `LocaleSettings` shape), additive per
  ADR 0004 §B.1, **no EF migration**.

## Goal

Implement the Core — the `SiteContent` doc (the 13 fields, the exact
defaults), the `ISiteContentService` interface, the `SiteContentService`
implementation (the ADR 0050 single-write-lane shape, the `AccessAudit`
row), the `SiteContentDocTypes` (the ADR 0004 §B.1 additive doc type), and
the DI registration. **No Web change, no view change, no admin surface.**

## Entry reads (5 files)

1. `docs/design/site-content-design.md` §2.2 / §2.3 / §2.4 — the exact
   `SiteContent` field set, the read-seam contract, the write-lane contract.
2. `src/Kumunita.Core/Localization/LanguageCatalog.cs` — the
   `LocaleSettings` singleton (the `Id = "singleton"` sentinel shape, the
   additive-field convention).
3. `src/Kumunita.Core/M1DocTypes.cs` — the `LocaleSettings` registration
   (the `opts.Schema.For<LocaleSettings>()` shape).
4. `src/Kumunita.Core/DependencyInjection.cs` — the DI registration (the
   `AddTransient<IPageService>(sp => new PageService(...))` shape) + the
   schema bootstrap (the `M1DocTypes.Configure(opts)` /
   `PageDocTypes.Configure(opts)` shape).
5. `src/Kumunita.Core/Identity/IdentityService.cs` — the
   `SetSignupOpenAsync` / `IsSignupOpenAsync` shape (the ADR 0050
   single-write-lane shape, the `AccessAudit` row).

## Deliverables (5 files, new/modify)

1. `src/Kumunita.Core/SiteContent/SiteContent.cs` (new) — the `SiteContent`
   doc. The `Id = "singleton"` sentinel (the `LocaleSettings` shape). The
   13 fields: 4 text fields (`HomeHeroEyebrow` / `HomeHeroLead` /
   `AboutHeroEyebrow` / `AboutHeroLead` — `string`, the exact `en` source
   text from `KnownTranslationKeys.cs` as the defaults) + 9 toggle fields
   (`HomeShowAboutButton` / `HomeShowFeatures` / `HomeShowRoadmap` /
   `AboutShowFeatures` / `AboutShowScope` / `AboutShowPhilosophy` /
   `AboutShowProject` / `AboutShowWhatsNew` / `AboutShowContactCta` —
   `bool`, all default `true`).
2. `src/Kumunita.Core/SiteContent/ISiteContentService.cs` (new) — the
   `ISiteContentService` interface (the `Task<SiteContent> GetAsync()` read
   + the `Task SaveAsync(SiteContent content, string actorBy)` write).
3. `src/Kumunita.Core/SiteContent/SiteContentService.cs` (new) — the
   `SiteContentService` implementation (the ADR 0050 single-write-lane
   shape — the `GetAsync` best-effort read, the `SaveAsync` upsert +
   `AccessAudit` row, `Via = Admin`, action `site.save`, `TargetKind`
   "site").
4. `src/Kumunita.Core/SiteContent/SiteContentDocTypes.cs` (new) — the
   `SiteContentDocTypes.Configure(opts)` surface (the ADR 0004 §B.1
   additive doc type — the `opts.Schema.For<SiteContent>()` shape).
5. `src/Kumunita.Core/DependencyInjection.cs` (modified) — the DI
   registration (the `AddTransient<ISiteContentService>(sp => new
   SiteContentService(sp.GetRequiredService<IDocumentStore>()))` shape) +
   the `SiteContentDocTypes.Configure(opts)` call in the schema bootstrap
   (the `M1DocTypes.Configure(opts)` / `PageDocTypes.Configure(opts)` shape).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- Handoff note: a `## U03 — Core: SiteContent doc + service + doc types +
  DI` section listing the 5 files (by path) + the 13 `SiteContent` fields
  (by name) + the `ISiteContentService` method signatures (the `GetAsync` /
  `SaveAsync` shape) + the `SiteContentDocTypes.Configure` call + the DI
  registration line.
- **No Web change, no view change, no admin surface.**
- Move this unit plan `in-progress/site-u03.md` → `done/site-u03.md` (move
  **last**).
