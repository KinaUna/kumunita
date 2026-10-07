# SITE U02 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard) + ADR 0150

## Context (self-contained)

`SITE` is the **site content customization** lane: the `/home` + `/about`
landing surfaces become admin-editable (the two heroes' eyebrow + lead are
editable text; every other section is show / hide). The defaults are
byte-identical to the shipped `kw-l` text. The precedent shapes are ADR
0005 B (`LocaleSettings` singleton), ADR 0019 / ADR 0020 (singleton-toggle
admin surface), ADR 0050 (single-write-lane), and ADR 0149 (the per-resident
`HideHomeIntro` preference — composable, not replaced).

**The `SiteContent` field set (13 fields, pinned — the ADR 0150 D1 ceiling):**
4 text fields (the `en` source text from `KnownTranslationKeys.cs` as the
defaults) + 9 toggle fields (all default `true`):

| # | Field | Type | Default (the `en` source text / `true`) |
|---|---|---|---|
| 1 | `HomeHeroEyebrow` | string | the `home.intro_eyebrow` text ("A private home for one neighbourhood") |
| 2 | `HomeHeroLead` | string | the `home.intro_lead` text ("One quiet place for everything your street does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.") |
| 3 | `HomeShowAboutButton` | bool | `true` (the home hero's "What it is & how it works" button) |
| 4 | `HomeShowFeatures` | bool | `true` (the home's three feature cards) |
| 5 | `HomeShowRoadmap` | bool | `true` (the home's "The plan" roadmap section) |
| 6 | `AboutHeroEyebrow` | string | the `about.eyebrow` text ("Private by default") |
| 7 | `AboutHeroLead` | string | the `about.lead` text ("One home for everything your neighborhood does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.") |
| 8 | `AboutShowFeatures` | bool | `true` |
| 9 | `AboutShowScope` | bool | `true` (the about's scope band) |
| 10 | `AboutShowPhilosophy` | bool | `true` (the about's FIG-philosophy band) |
| 11 | `AboutShowProject` | bool | `true` (the about's code/docs band) |
| 12 | `AboutShowWhatsNew` | bool | `true` (the about's "What's new" changelog) |
| 13 | `AboutShowContactCta` | bool | `true` (the about's contact CTA band) |

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The read is a **public landing surface** (world-readable, not an access
  decision, not a claim). A missing singleton degrades to the shipped
  defaults (byte-identical `kw-l` text + every section shown) — never a
  blank page.
- The write is the **ADR 0050 single-write-lane shape** (one
  `ISiteContentService.SaveAsync`, one `AccessAudit` row per save,
  `Via = Admin`, action `site.save`, `TargetKind` "site").
- The defaults are **byte-identical to the shipped `kw-l` text**; the
  `kw-l` registry entries **stay**.
- ADR 0149 `Profile.HideHomeIntro` is **composable, not replaced** — the
  two intro sections render when **both** `!HideIntro` **and**
  `HomeShowFeatures`; the feed + roadmap are governed only by the platform
  flag.
- The `SiteContent` doc is a **singleton** (one row per instance), additive
  per ADR 0004 §B.1, **no EF migration**.

## Goal

Append `## Seams & contracts (Part 2, written by U02)` to
`docs/design/site-content-design.md` — the exact `SiteContent` field set,
the read-seam contract, the write-lane contract, the **pinned seam-test
names**, the **acceptance gate**, and the **drift-guard**. Plus **ADR 0150**
(draft, Accepted) + the ADR index row. **No code, no build.**

## Entry reads (7 files)

1. `docs/design/site-content-design.md` — U01's Part 1 (the invariant table
   is the primary source).
2. `docs/design/wysiwyg-editor-design.md` §Pinned contract — the shape to
   emulate.
3. `src/Kumunita.Core/Localization/LanguageCatalog.cs` — the
   `LocaleSettings` singleton (the `Id = "singleton"` sentinel shape, the
   additive-field convention: `IsSignupOpen` / `NotifyAdminsOnSignup` / …).
4. `src/Kumunita.Web/Controllers/AdminSignupController.cs` — the ADR 0050
   `/admin/signup` surface (the `GlobalAdmin`-gated single-write-lane shape,
   the `AccessAudit` row).
5. `src/Kumunita.Core/Identity/IdentityService.cs` — the
   `SetSignupOpenAsync` / `IsSignupOpenAsync` shape (the ADR 0050
   single-write-lane shape, the `AccessAudit` row).
6. `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` — the seeder shape
   (the `SeedDefaultPagesAsync` / `SeedLanguages` create-if-missing,
   idempotent, never-overwrites patterns).
7. `docs/adr/README.md` — the ADR index. **Confirm `0150` is free** (the
   index runs 0001–0149, so `0150` is next). Read
   `docs/adr/0050-admin-managed-signup-gate.md` +
   `docs/adr/0149-home-intro-hide-preference.md` for the precedent shape.

## Deliverables (3 files, new/modify)

1. `docs/design/site-content-design.md` (append Part 2). Sub-sections:
   - `### 2.1 frozen base (unchanged)` — ADR 0005 B `LocaleSettings`
     singleton shape + ADR 0019 / ADR 0020 singleton-toggle + ADR 0050
     single-write-lane + ADR 0149 `Profile.HideHomeIntro` pin + ADR 0004
     §B.1 additive doc type + ADR 0006 module boundary + ADR 0015 D1 `kw-l`
     provider-floor + the `LS` / `SP` / `PG` test model — all **keep
     binding unchanged**.
   - `### 2.2 the SiteContent field set (exact)` — the 13 fields above, each
     with its exact default, its `kw-l` key (for the 4 text fields:
     `home.intro_eyebrow` / `home.intro_lead` / `about.eyebrow` /
     `about.lead`; no key for the 9 toggle fields — they are admin-settled
     instance values, not UI strings), and its a11y note.
   - `### 2.3 the read-seam contract (exact C#)` — `ISiteContentService.
     GetAsync()` (returns the singleton, or the in-code fallback if the
     store is missing / the row is absent — the ADR 0050 `IsSignupOpenAsync`
     best-effort shape; the in-code fallback is the `SiteContent` field
     defaults, byte-identical to the shipped `kw-l` text). The
     `HomeController.Index` / `StaticPagesController.About` read is the
     `SiteContentService.GetAsync()` call (best-effort — a missing seam or
     a read failure degrades to the in-code fallback, and the page always
     renders).
   - `### 2.4 the write-lane contract (exact C#)` —
     `ISiteContentService.SaveAsync(SiteContent content, string actorBy)`
     (loads the singleton, applies the field set, saves in one session —
     invariant C3; exactly one `AccessAudit` row per save, `Via = Admin`,
     action `site.save`, `TargetKind` "site" — the `signup.set-open` shape;
     upserts the singleton, the `LocaleSettings` "one row per instance"
     shape; strong consistency — the new value is live on the very next
     `GetAsync` / render). The `AdminSiteController.SaveHome` / `SaveAbout`
     actions are the thin wrappers (the `AdminSignupController.Save` /
     `SaveNotify` shape — the `GlobalAdmin`-gated
     `[ValidateAntiForgeryToken]` POST, the `TempData["info"]` flash, the
     `RedirectToAction(nameof(Index))` redirect).
   - `### 2.5 the pinned seam-test names (exact)` — the `Core.Tests` pins
     (the `SiteContentServiceTests` class — the
     `GetAsync_MissingStore_ReturnsInCodeFallback` /
     `SaveAsync_WritesOneAccessAuditRow` /
     `SaveAsync_StrongConsistency_LiveOnNextGetAsync` /
     `SaveAsync_UpsertsSingleton_NoDuplicateRow` pins) + the `Core.Tests`
     seeder pins (the `SiteContentSeederTests` class — the
     `FreshBoot_HasExactlyOneSiteContentRow` /
     `FreshBoot_DefaultsMatchShippedKwLText` /
     `SecondBoot_IsIdempotent_NoDuplicateRow_NoFieldChange` pins) + the
     `Web.Tests` pins (the `HomeControllerSiteContentTests` class — the
     `FreshInstance_RendersShippedText_EverySectionShown` /
     `SavedRow_HomeShowAboutButtonFalse_HidesHomeHeroButton` /
     `ADR_0149_HideHomeIntroTrue_HomeShowFeaturesTrue_HidesHomeFeatureCards`
     / `ADR_0149_HideHomeIntroFalse_HomeShowFeaturesFalse_HidesHomeFeatureCards`
     pins) + the `Web.Tests` pins (the `AdminSiteControllerTests` class —
     the `GET_SeesCurrentSingleton` /
     `POST_SaveHome_SavesFieldGroup_WritesOneAccessAuditRow` /
     `POST_SaveAbout_SavesFieldGroup_WritesOneAccessAuditRow` /
     `POST_NonGlobalAdmin_IsDenied` pins).
   - `### 2.6 the acceptance gate (exact)` — the
     `dotnet build Kumunita.slnx -c Debug` green + the
     `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
     green + the `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
     green + the `KwLRegistryConsistencyTests` /
     `KnownTranslationKeys_ParityTests` pins green (the registry entries
     are untouched) + the `LS_U04_SeederTests` / `PageServiceTests` /
     `HomeControllerTests` / `StaticPagesControllerTests` /
     `AdminSignupControllerTests` / `AdminControllerTests` pins green (the
     precedent shapes are unchanged) + the `MilestonesTests` pin green (the
     M4 single-`StatusNext` pin is intact) + the `WhatsNewTests` pin green
     (the new `0.42.0` entry is present, newest-first).
   - `### 2.7 the drift guard (exact)` — the `SiteContent` field set is the
     **ceiling** (the 13 fields — no field outside the field set may appear
     in the doc, the ADR 0150 D1 pin); the `kw-l` registry entries are the
     **floor** (the `home.*` / `about.*` / `platform.*` keys stay, the ADR
     0150 D3 pin); the ADR 0149 `Profile.HideHomeIntro` read is the
     **frozen base** (the per-resident preference is unchanged, the ADR 0150
     D5 pin); the `LocaleSettings` singleton is **untouched** (this lane
     adds a new doc, not a new field on the existing one, the ADR 0150 D6
     pin).
2. `docs/adr/0150-site-content-customization.md` (new, Status: Accepted) —
   `## Context` (the two landing surfaces are hard-coded; the
   `LocaleSettings` singleton ships the admin-settled instance values; this
   lane ships the landing-surface content as a new `SiteContent` singleton),
   `## Decision` (the 6 decisions — the read is a public landing surface,
   the write is the ADR 0050 single-write-lane shape, the defaults are
   byte-identical to the shipped `kw-l` text, the `kw-l` registry entries
   stay, the ADR 0149 `Profile.HideHomeIntro` is composable, the
   `SiteContent` doc is a singleton), `## Consequences` (the two landing
   surfaces are now admin-editable; the `/admin/site` surface is the
   GlobalAdmin's edit page; the `kw-l` registry entries stay; the ADR 0149
   `Profile.HideHomeIntro` is composable; the `LocaleSettings` singleton is
   untouched; the `Milestones.cs` / README / `MilestonesTests` trio is
   untouched until the lane *ships*; the `WhatsNew.cs` registry gains one
   new entry — the `0.42.0` row).
3. `docs/adr/README.md` (modified) — one index row after the 0149 row:
   `| 0150 | Site content customization (the two landing surfaces' hero text is
   admin-editable + the sections are show/hide; a new `SiteContent`
   singleton, additive on ADR 0005 B / ADR 0019 / ADR 0020 / ADR 0050;
   composable with ADR 0149; additive on ADR 0004 §B.1) | Accepted |`.

## Exit

- The design doc Part 2 is present with all sub-sections.
- The ADR 0150 is present (Status: Accepted) + the README row is added.
- **No code, no build.**
- Handoff note: a `## U02 — design doc Part 2 + ADR 0150` section listing
  the **13 `SiteContent` fields** (by name), the **pinned test names** (by
  class + method), the **acceptance gate** (the exact command list), and the
  **ADR 0150** number (0150) + the **ADR index update** (the
  `docs/adr/README.md` row).
- Move this unit plan `in-progress/site-u02.md` → `done/site-u02.md` (move
  **last**).
