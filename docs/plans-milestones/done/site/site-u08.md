# SITE U08 — Tests + close flip (the six-member close-flip contract)

## Context (self-contained)

`SITE` is the **site content customization** lane: the `/home` + `/about`
landing surfaces become admin-editable. U00–U07 locked the scope, the design
doc, ADR 0150, the Core, the seeder default, both landing views, and the
`/admin/site` surface. **This unit runs the acceptance gate + flips the
close** (the `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
`MilestonesTests.cs` / `WhatsNew.cs` six-member close-flip contract, the
AGENTS.md required-sixth-member rule) **+ appends the final handoff section**
so the lane is honest. **No new code beyond the test pins + the close-flip
files.**

**The `SiteContent` field set (13 fields, pinned — the ADR 0150 D1 ceiling):**
4 text fields + 9 toggle fields (the U00–U07 register names them).

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The **acceptance gate** is the design doc §2.6 (the exact command list):
  `dotnet build Kumunita.slnx -c Debug` green + the
  `Kumunita.Core.Tests.dll` in-process run green + the
  `Kumunita.Web.Tests.dll` in-process run green + the
  `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` pins
  green (the registry entries are untouched) + the `LS_U04_SeederTests` /
  `PageServiceTests` / `HomeControllerTests` / `StaticPagesControllerTests`
  / `AdminSignupControllerTests` / `AdminControllerTests` pins green (the
  precedent shapes are unchanged) + the `MilestonesTests` pin green (the
  M4 single-`StatusNext` pin is intact) + the `WhatsNewTests` pin green
  (the new `0.42.0` entry is present, newest-first).
- The **close flip** is the **required sixth member** (AGENTS.md — the
  M27 lesson: "shipped 2026-10-07 with no entry until caught in review —
  added as 0.40.0"): `Milestones.cs` + README + `STATUS.md` +
  `ARCHITECTURE.md` + `MilestonesTests.cs` + **`WhatsNew.cs`**. A `SITE`
  row added as `StatusDone` does not disturb the `MilestonesTests` pin that
  M4 is the single `StatusNext`.
- The **drift guard** (the design doc §2.7) is the frozen set — any
  mismatch at this point is a `## U08 — Drift pause`, not a silent fix.

## Goal

Run the acceptance gate (the exact command list from the design doc §2.6) +
flip the close (the `Milestones.cs` / README / `STATUS.md` /
`ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs` six-member
close-flip contract) + append the final handoff section so the lane is
honest. **No new code beyond the test pins + the close-flip files.**

## Entry reads (7 files)

1. `docs/design/site-content-design.md` §2.5 / §2.6 / §2.7 — the pinned
   seam-test names + the acceptance gate + the drift guard.
2. `docs/plans-milestones/in-progress/site-handoff-notes.md` — the rolling
   handoff note (the U00–U07 sections — read for the drift check).
3. `src/Kumunita.Web/Milestones.cs` — the `Milestones.cs` registry (the
   `M4` single-`StatusNext` pin, the `StatusDone` / `StatusNext` /
   `StatusPlanned` shape).
4. `src/Kumunita.Web/WhatsNew.cs` — the `WhatsNew.cs` registry (the
   `0.41.0` / `0.40.0` / … entries, newest-first; the `Release` record
   shape).
5. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the `MilestonesTests`
   pin (the M4 single-`StatusNext` pin).
6. `README.md` — the Roadmap section (the `M4` / `M5` / `M6` rows, the
   `SITE` lane row to add).
7. `docs/STATUS.md` + `docs/ARCHITECTURE.md` — the status + architecture
   docs (the `SITE` lane row to add; the `SiteContent` doc type row to
   add).

## Deliverables (8 files, new/modify)

1. `tests/Kumunita.Core.Tests/SiteContentServiceTests.cs` (new) — the
   `SiteContentServiceTests` class. Pins (the design doc §2.5 names):
   - `GetAsync_MissingStore_ReturnsInCodeFallback` — a missing store (a
     test construction with no `IDocumentStore`) degrades to the in-code
     fallback (the `SiteContent` field defaults, byte-identical to the
     shipped `kw-l` text).
   - `SaveAsync_WritesOneAccessAuditRow` — a save writes exactly one
     `AccessAudit` row (`Via = Admin`, action `site.save`, `TargetKind`
     "site").
   - `SaveAsync_StrongConsistency_LiveOnNextGetAsync` — the new value is
     live on the very next `GetAsync` call (the ADR 0050 strong-consistency
     shape).
   - `SaveAsync_UpsertsSingleton_NoDuplicateRow` — a save upserts the
     singleton (no duplicate row, the `LocaleSettings` "one row per
     instance" shape).
2. `tests/Kumunita.Web.Tests/HomeControllerSiteContentTests.cs` (new) —
   two test classes (the design doc §2.5 names):
   - `HomeControllerSiteContentTests` — the
     `FreshInstance_RendersShippedText_EverySectionShown` /
     `SavedRow_HomeShowAboutButtonFalse_HidesHomeHeroButton` /
     `ADR_0149_HideHomeIntroTrue_HomeShowFeaturesTrue_HidesHomeFeatureCards`
     / `ADR_0149_HideHomeIntroFalse_HomeShowFeaturesFalse_HidesHomeFeatureCards`
     pins (the ADR 0149 composability).
   - `AdminSiteControllerTests` — the `GET_SeesCurrentSingleton` /
     `POST_SaveHome_SavesFieldGroup_WritesOneAccessAuditRow` /
     `POST_SaveAbout_SavesFieldGroup_WritesOneAccessAuditRow` /
     `POST_NonGlobalAdmin_IsDenied` pins.
3. `src/Kumunita.Web/Milestones.cs` (modified) — the `Milestones.cs`
   registry gains one new entry (the `SITE` lane row, `StatusDone` — the
   "Site content customization — the landing surfaces' hero text is
   admin-editable + the sections are show/hide (ADR 0150)" row). Insert it
   in the right position (after the current last `StatusDone` row, before
   the single `StatusNext` row — the M4 row). The `MilestonesTests` pin
   that M4 is the single `StatusNext` stays intact.
4. `README.md` (modified) — the Roadmap section gains one new row (the
   `SITE` lane row, the "Site content customization — the landing
   surfaces' hero text is admin-editable + the sections are show/hide (ADR
   0150)" row). Keep the `Milestones.cs` and README in sync (the
   AGENTS.md contract).
5. `docs/STATUS.md` (modified) — the status doc gains one new row (the
   `SITE` lane row, the "Site content customization — the landing
   surfaces' hero text is admin-editable + the sections are show/hide (ADR
   0150)" row).
6. `docs/ARCHITECTURE.md` (modified) — the architecture doc gains one new
   bounded-context row (the `SITE` bounded context, the `SiteContent` doc
   type, the `ISiteContentService` seam, the ADR 0150 reference) + one new
   doc-type row (the `SiteContent` singleton, the `Kumunita.Core.SiteContent`
   context, the ADR 0150 reference).
7. `src/Kumunita.Web/WhatsNew.cs` (modified) — the `WhatsNew.cs` registry
   gains one new entry (the `0.42.0` row, `2026-10-07` date, the "Site
   content customization — the landing surfaces' hero text is
   admin-editable + the sections are show/hide (ADR 0150)" change). Insert
   it as the **newest-first** head of the `All` list.
8. `tests/Kumunita.Web.Tests/WhatsNewTests.cs` (modified, if it exists) —
   the `WhatsNewTests` pin asserts the new `0.42.0` entry is present
   (newest-first). If the test file does not exist, skip this deliverable
   (the `WhatsNewTests` pin is the AGENTS.md contract; the M27 lesson is
   that a missing entry is caught in review — the pin is the defense).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green (the new `SiteContentServiceTests` pins pass; the existing
  `LS_U04_SeederTests` / `PageServiceTests` / `SiteContentSeederTests` pins
  are unchanged).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green (the new `HomeControllerSiteContentTests` +
  `AdminSiteControllerTests` pins pass; the existing
  `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` /
  `HomeControllerTests` / `StaticPagesControllerTests` /
  `AdminSignupControllerTests` / `AdminControllerTests` / `MilestonesTests`
  / `WhatsNewTests` pins are unchanged).
- Handoff note: a `## U08 — Tests + close flip` section listing the 8 files
  (by path) + the `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md`
  / `WhatsNew.cs` close-flip rows (the exact row text + the ADR 0150
  reference) + the `WhatsNew.cs` `0.42.0` entry (the exact change text +
  the ADR 0150 reference) + the acceptance gate result (the exact command
  output) + the **drift-check** result (the design doc §2.7 frozen set is
  intact — no `## U08 — Drift pause` needed).
- **No new code beyond the test pins + the close-flip files.**
- **Lane close:** move all 9 unit plans (`site-u00.md` … `site-u08.md`)
  from `in-progress/` → `done/` (move **last**, after the handoff note is
  appended). The main plan (`docs/plans-milestones/plan-site.md`) **stays**
  at the top of `docs/plans-milestones/` (the m27 convention — the main
  plan is not moved; only the unit plans move to `done/`).
- `git status` clean except the close-flip files + the test pins + the
  handoff note.
