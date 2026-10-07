# Site content customization (`SITE`) — design

> **Three-tier contract.** This file is the **primary** tier of the SITE
> lane: it pins the invariants (SITE·1–SITE·9), the FACES (SITE1–SITE10),
> the exact `SiteContent` field set, the read-seam contract, the write-lane
> contract, the pinned seam-test names, the acceptance gate, and the drift
> guard. The register
> (`docs/plans-milestones/plan-site.md`) is the **secondary** tier
> (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/site-handoff-notes.md` is the
> **scratch** tier (one short section per unit, appended, never rewritten).
> When the three disagree, **this file wins for the pinned shapes**; the
> register wins for *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the value chain, the context, the scope, the
> **nine invariants** (SITE·1–SITE·9), the **ten FACES** (SITE1–SITE10), and
> the frozen-base assumptions. **Part 2 (U02):** the seams & contracts (the
> exact `SiteContent` field set, the read-seam contract, the write-lane
> contract, the pinned seam-test names, the acceptance gate, the drift
> guard).
>
> **The frozen base.** This lane is built **on top of** the `LocaleSettings`
> singleton (ADR 0005 B), the singleton-toggle admin surface (ADR 0019 /
> ADR 0020), the single-write-lane shape (ADR 0050), and the per-resident
> `Profile.HideHomeIntro` preference (ADR 0149). All of these still bind
> **unchanged** (re-anchored in §Frozen base below). SITE adds **no**
> re-shape of `LocaleSettings`, **no** re-shape of
> `Profile.HideHomeIntro`, **no** removal of a `kw-l` registry entry, and
> **no** new `AccessAction` / `AccessVia` / authorization path. It is
> **additive**: one new doc type (`SiteContent`, a singleton), one new
> bounded context (`Kumunita.Core.SiteContent`), one new service
> (`ISiteContentService`), one new admin surface (`/admin/site`), and
> conditional rendering in two existing views. **No EF migration** (a new
> Marten doc type is additive per ADR 0004 §B.1; the delta is applied
> idempotently at boot). **No new route for the read** (the existing `/` and
> `/about` routes are unchanged — only what they render changes).
>
> **The `SiteContent` field set is the ceiling** (the 13 fields in the
> register's "one thing" section — no field outside the field set may appear
> in the doc, the ADR 0150 D1 pin). **The `kw-l` registry entries are the
> floor** (the `home.*` / `about.*` / `platform.*` keys stay, the ADR 0150
> D3 pin). **The ADR 0149 `Profile.HideHomeIntro` read is the frozen base**
> (the per-resident preference is unchanged, the ADR 0150 D5 pin). **The
> `LocaleSettings` singleton is untouched** (this lane adds a new doc, not a
> new field on the existing one, the ADR 0150 D6 pin).

## Value chain

The `SP` lane (ADR 0043) shipped the four static pages (terms / help /
privacy / conduct) as admin-editable `Page` docs — an admin can now change
what those pages say without a code change. The `LocaleSettings` singleton
(ADR 0005 B) ships the admin-settled instance values (default language /
timezone / date format / sign-up gate / messaging) — an admin can now
change how the platform *behaves* without a code change. But the two
**landing surfaces** — `/` (the home) and `/about` (the about page) — are
still **hard-coded**: the hero eyebrow, the hero lead text, and the
*presence* of every secondary section (the feature cards, the scope band,
the FIG-philosophy band, the code/docs links, the "The plan" roadmap, the
"What's new" changelog, the contact CTA) are all baked into the Razor views
as `kw-l`-wrapped English strings. An admin who wants to say "this is a
home for one club, not a neighbourhood," or wants to drop the
FIG-philosophy band on a deployment where it feels like marketing, or wants
the hero to say "Welcome to Maplewood" instead of the generic pitch, has
**no path** — the only way to change what those surfaces say is a code
change to the view.

This lane completes the arc from the admin's point of view:

- **The two heroes' eyebrow + lead become editable text** — the admin
  writes what they want the hero to say; the view renders the admin's text
  instead of the `kw-l` string (SITE·3).
- **Every other section becomes show / hide** — the admin decides which
  sections appear at all; a hidden section is **not in the DOM** (not
  `display: none`), so a screen reader never sees it (SITE·9).
- **The defaults are byte-identical to the shipped `kw-l` text** — an admin
  who never touches the surface sees exactly what a fresh instance ships
  today (SITE·3, the "keep it as is by default" requirement).

The value chain moves one arrow: from *"the admin configures how the
platform behaves"* (`LocaleSettings`) and *"the admin edits the static
pages"* (`SP`) to **"the admin edits the landing surfaces' story"** (SITE)
— the same "admin-settled instance value" pattern, applied to the two
surfaces a visitor sees first.

## Context

The gap this lane closes is the **admin-editable** half of the landing
surfaces. The `SP` lane gave the admin a path to change what the terms /
help / privacy / conduct pages say. The `LocaleSettings` singleton gave the
admin a path to change the default language / timezone / date format /
sign-up gate / messaging. But the two **landing surfaces** — `/` and
`/about` — are the **first content a visitor sees**, and they are
**hard-coded**:

- `/home` = `Views/Home/Index.cshtml` (4 sections: hero, feature cards,
  "What's new" feed, roadmap). Sections 1+2 are wrapped in
  `@if (!Model.HideIntro)` (ADR 0149). Section 3 is wrapped in
  `@if (Model.Feed is { } feed)`. Section 4 is unconditional.
- `/about` = `Views/StaticPages/About.cshtml` (7 sections: hero, feature
  cards, scope, FIG philosophy, code/docs, "What's new" changelog, contact
  CTA). All unconditional (no `@if` wraps).

Every word of copy on both surfaces is a `kw-l`-wrapped English string in
`KnownTranslationKeys.cs` (the `home.*` / `about.*` / `platform.*` key
families). The *presence* of each section is hard-coded in the Razor view —
there is no way to hide a section, no way to change a word, and no way to
add a word, short of a code change.

### The precedent shapes (frozen, verified by U00)

- **ADR 0005 B** — the `LocaleSettings` singleton (`Id = "singleton"`
  sentinel, the additive-field convention, one row per instance). The
  `SiteContent` doc will be a **new** singleton in the same shape, **not** a
  new field on `LocaleSettings` (ADR 0006 module-boundary contract).
- **ADR 0019 / ADR 0020** — the singleton-toggle admin surface shape
  (`/admin/timezone` / `/admin/dateformat`, `GlobalAdmin`-gated, one
  `AccessAudit` row per save).
- **ADR 0050** — the single-write-lane shape (`AdminSignupController`, the
  `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write,
  the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
  `TempData["info"]` flash, the `RedirectToAction(nameof(Index))`
  redirect). The `AdminSiteController` will mirror this shape.
- **ADR 0149** — the `Profile.HideHomeIntro` **per-resident** hide-intro
  preference (composable, not replaced — the two intro sections render when
  **both** `!HideIntro` **and** the platform flag are true).

### The constraints that still bind

- **ADR 0004 §B.1** — additive doc type (delta applied idempotently at boot,
  no EF migration). The `SiteContent` doc is a **new** Marten doc type, not
  a new field on an existing one.
- **ADR 0006** — module-boundary contract. The `SiteContent` context is a
  **new** bounded context (`Kumunita.Core.SiteContent`), independent of
  `LocaleSettings` and the `Localization` context.
- **ADR 0015 D1** — `kw-l` provider-floor discipline. The `kw-l` registry
  entries stay (the `home.*` / `about.*` / `platform.*` keys are not
  removed, not re-shaped, not re-keyed).
- **The `LS` / `SP` / `PG` test model** — the `FirstBootSeeder` default pin
  (the `LS_U04_SeederTests` / `PageServiceTests` "seeded defaults match the
  shipped text" pattern), the Web-layer pins (the
  `HomeControllerTests` / `StaticPagesControllerTests` shape), the
  `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` pins
  (the registry entries are untouched).
- **The `Milestones.cs` / README / `MilestonesTests` close-flip trio** —
  the `MilestonesTests` pin that M4 is the single `StatusNext` stays intact
  until the lane *ships* (U08 owns the close flip).

## Scope

**In:**

- The `SiteContent` doc (a new `Kumunita.Core.SiteContent` context,
  `Id = "singleton"` sentinel, the 13 fields).
- The `ISiteContentService` + `SiteContentService` (the ADR 0050
  single-write-lane shape — the `GetAsync()` read + the
  `SaveAsync(SiteContent content, string actorBy)` write).
- The `SiteContentDocTypes.Configure(opts)` surface (the ADR 0004 §B.1
  additive doc type — the `opts.Schema.For<SiteContent>()` shape).
- The `FirstBootSeeder` default (the exact shipped `kw-l` text,
  byte-identical — the `SeedSiteContentAsync` method, create-if-missing,
  idempotent, never-overwrites).
- The `HomeController.Index` / `StaticPagesController.About` read seam (the
  best-effort read, the in-code fallback — a missing singleton degrades to
  the shipped defaults, never a blank page).
- The two views' conditional rendering (the `@if` wraps + the text swap —
  the hero text is `@Model.Site.HomeHeroEyebrow` /
  `@Model.Site.HomeHeroLead`; the sections are conditionally rendered).
- The `/admin/site` surface (the `AdminSiteController` + the `AdminSite`
  view + the `/admin/platform` link — the ADR 0050 shape).
- The test pins (the `SiteContentSeederTests` / `SiteContentServiceTests` /
  `HomeControllerSiteContentTests` / `AdminSiteControllerTests` classes).

**Out (named deferrals for a future SITE-2 lane, if one comes):**

- **Machine translation of the landing-surface copy** — a
  `SiteContentTranslation` row shape (the `PageTranslation` /
  `PostTranslation` precedent) + a `SiteContentTranslationDocTypes` surface
  + a `/admin/site` translation editor + the `KnownTranslationKeys` parity
  pin for the new keys. The ADR 0005 §B "every other language is
  community-provided" clause is unchanged.
- **Per-section editable text** — the feature cards' titles / bodies, the
  scope band's body, the FIG-philosophy band's body, and the code/docs
  band's lead are **show / hide only** in this lane. A future SITE-2 lane
  could add per-section editable text in the same `SiteContent` doc shape
  (the `SiteContent` field set is additive per ADR 0004 §B.1).
- **Per-section custom CTA buttons** — the home hero's "What it is & how it
  works" button is show / hide only; a future lane could add an editable
  URL + label.
- **The `Milestones.cs` / README / `MilestonesTests` close-flip entries** —
  until the lane *ships* (U08 owns them).

## Invariants (pinned for SITE)

Nine invariants, **SITE·1–SITE·9**. Each is one idea, pinned so every unit
and every FACES row references a stable number.

| # | Invariant (one-line SITE note) |
|---|------------------------------|
| **SITE·1** | **The read is a public landing surface** — `/` and `/about` are world-readable (the static-page contract — the seeded canonical pages are public, `Page.Audience = null`, the ADR 0043 standing matrix). The `SiteContent` singleton is read by the same two controllers (`HomeController.Index` / `StaticPagesController.About`) and is **not** an access decision, **not** a claim (the thin-token rule, ADR 0001-B), and is read fresh per request (a save is live on the very next render — the ADR 0050 `IsSignupOpen` strong-consistency shape). A missing singleton (a fresh boot before the seeder ran, or a test construction with no store) degrades to the **shipped defaults** (the byte-identical `kw-l` text + every section shown) — never a blank page, never an error. |
| **SITE·2** | **The write is the ADR 0050 single-write-lane shape** — one `ISiteContentService.SaveAsync(content, actorBy)` lane that loads the singleton, applies the field set, and saves in one session (invariant C3); exactly one `AccessAudit` row per save (`Via = Admin`, action `site.save`, `TargetKind` "site" — the `signup.set-open` / `timezone.set-default` / `dateformat.set-default` singleton-toggle shape). The lane **never load-or-creates a new row on a save** — it upserts the singleton (the `LocaleSettings` "one row per instance" shape, ADR 0005 B). Strong consistency (invariant C4): the new value is live on the very next `GetAsync` / render. |
| **SITE·3** | **The defaults are byte-identical to the shipped `kw-l` text** — the `SiteContent` default values (the `FirstBootSeeder` seed + the in-code fallback) are the **exact** `home.intro_eyebrow` / `home.intro_lead` / `about.eyebrow` / `about.lead` English strings from `KnownTranslationKeys.cs` (the `en` source text), and every section-toggle default is `true` (every section shown). An admin who never touches the surface sees exactly what a fresh instance ships today — the "keep it as is by default" requirement. The `LS_U04_SeederTests` / `PageServiceTests` "seeded defaults match the shipped text" pin carries this forward. |
| **SITE·4** | **The `kw-l` registry entries stay** — the `home.*` / `about.*` / `platform.*` keys in `KnownTranslationKeys.cs` are **not** removed, not re-shaped, not re-keyed — they remain the **fallback** the `SiteContent` default values are derived from (the seeder reads the registry at seed time; the in-code fallback is the same text, hard-coded in `SiteContent`'s field defaults so a missing singleton degrades to the shipped text). The registry parity tests (`KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests`) are **untouched** — the view's `kw-l` wrap is replaced by a server-resolved `SiteContent` value, but the registry entries stay (they are still the canonical `en` source text; a future translation lane for the landing surface is **out of scope** — the ADR 0005 §B "every other language is community-provided" clause is unchanged). |
| **SITE·5** | **The ADR 0149 `Profile.HideHomeIntro` per-resident preference is composable, not replaced** — the per-resident "hide the intro" checkbox (ADR 0149) and the platform-level "show the intro" flag (ADR 0150) are **independent**: the per-resident preference is a `Profile` field (ADR 0019 / ADR 0020 owner-scope shape, read in `HomeController.Index` and passed through `HomeViewModel.HideIntro`); the platform-level flag is a `SiteContent` field (read in the same `HomeController.Index` and passed through a new `HomeViewModel.ShowHomeIntro` field). The view's two intro sections render when **both** are true (the per-resident preference is the *resident's* choice to hide; the platform flag is the *admin's* choice to show — both must agree for the sections to appear). The "What's new" feed and the roadmap section are governed **only** by the platform flag (the per-resident preference never reaches them — the ADR 0149 pin is unchanged). |
| **SITE·6** | **The `SiteContent` doc is a singleton (one row per instance)** — the `Id` is the sentinel `"singleton"` (the exact `LocaleSettings` shape, ADR 0005 B). The doc is registered in a new `SiteContentDocTypes.Configure(opts)` surface (the `PageDocTypes` / `MediaDocTypes` parallel surface, ADR 0004 §B.1); the delta is applied idempotently at boot (the M1/M3/Media dev-only loop / versioned-boot path). **No EF migration** (a new Marten doc type is additive per ADR 0004 §B.1; the existing `LocaleSettings` doc is **not** modified — this lane adds a *new* doc, not a new field on the existing one, because the landing-surface content is a distinct bounded concern from the locale settings, and the ADR 0006 module-boundary contract keeps the two contexts independent). |
| **SITE·7** | **The `/admin/site` surface is the ADR 0050 shape** — `[Route("admin/site")]` + `[Authorize(Roles = GlobalAdmin)]` on a new `AdminSiteController` (a **dedicated** controller — the `AdminController`'s constructor is pinned by two Web-layer test harnesses (`AdminControllerBlockTests` / `AdminControllerMandatoryTests`), so a new dependency there would break them; a separate `/admin/site` surface mirrors `/admin/signup` / `/admin/timezone` / `/admin/dateformat` — the codebase already puts the platform-settled instance values on their own controllers). The GET seeds the form with the current singleton; the POST saves it (one `AccessAudit` row, the `signup.set-open` shape). The `/admin/platform` page gains one list-group row linking to `/admin/site` (the `SP U03` / `ADR 0050` discoverability pattern). |
| **SITE·8** | **The `Milestones.cs` / README / `MilestonesTests` trio is untouched until the lane *ships* (U08 owns the close flip)** — the `MilestonesTests` pin that **M4** is the single `StatusNext` stays intact — a `SITE` row added as `StatusDone` (or a new `WhatsNew` entry, the required sixth member of the close flip) does not disturb it. The `WhatsNew.cs` registry gains one new entry (newest-first, the `0.42.0` row) naming the lane + the ADR 0150 — the M27 "shipped with no entry until caught in review" lesson (AGENTS.md) is held. |
| **SITE·9** | **a11y** — the two heroes' eyebrow + lead are still the first content in the DOM (the hero band is unchanged in shape — only the text source changes); the section toggles are **server-side** (a hidden section is **not in the DOM at all**, not `display: none` — a screen reader never sees it); the view's `aria-label` attributes are unchanged for the sections that are shown. |

## FACES (pinned, 10)

Ten admin/visitor-facing scenarios, **SITE1–SITE10**, each exercising one or
more invariants.

| # | Outcome (what the admin / visitor sees / can do) | Pinned by |
|---|---|---|
| **SITE1** | A visitor loads `/` or `/about` — the page renders **regardless** of whether the `SiteContent` singleton exists in the store. A missing singleton (a fresh boot before the seeder ran, or a test construction with no store) degrades to the **shipped defaults** (the byte-identical `kw-l` text + every section shown) — never a blank page, never an error. The read is **world-readable** — no authorization check, no `AccessAudit` row on the read. | SITE·1, SITE·6 |
| **SITE2** | The admin saves a field group on `/admin/site` → the `SiteContentService.SaveAsync` lane loads the singleton, applies the field set, and saves in one session; exactly **one** `AccessAudit` row is written (`Via = Admin`, action `site.save`, `TargetKind` "site"); the new value is **live on the very next render** (strong consistency). The lane **upserts** the singleton — no duplicate row. | SITE·2 |
| **SITE3** | A fresh instance (the seeder has run, the admin has never touched `/admin/site`) renders `/` and `/about` with **exactly** the shipped `kw-l` text — the hero eyebrow, the hero lead, and every section shown. The `SiteContentSeederTests` pins assert the defaults are byte-identical to the `home.intro_eyebrow` / `home.intro_lead` / `about.eyebrow` / `about.lead` English strings from `KnownTranslationKeys.cs`. | SITE·3 |
| **SITE4** | The `kw-l` registry entries (`home.*` / `about.*` / `platform.*` keys in `KnownTranslationKeys.cs`) are **untouched** — the view's `kw-l` wrap is replaced by a server-resolved `SiteContent` value, but the registry entries stay. The `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` pins are **green** (the registry entries are untouched). A future translation lane (out of scope) would add a `SiteContentTranslation` row shape keyed on the same strings. | SITE·4 |
| **SITE5** | A resident who has set `Profile.HideHomeIntro = true` (ADR 0149) loads `/` — the two intro sections (hero + feature cards) are **hidden** even if the admin has `HomeShowFeatures = true` (the per-resident preference is the *resident's* choice to hide; the platform flag is the *admin's* choice to show — **both** must agree). Conversely, a resident who has `HideHomeIntro = false` (the default) sees the two intro sections **only if** the admin has `HomeShowFeatures = true`. The "What's new" feed and the roadmap section are governed **only** by the platform flag (`HomeShowRoadmap`) — the per-resident preference never reaches them. | SITE·5 |
| **SITE6** | The `SiteContent` doc is a **singleton** — one row per instance, `Id = "singleton"`. A second boot is **idempotent** (no duplicate row, no field change). The `SiteContentSeederTests.SecondBoot_IsIdempotent_NoDuplicateRow_NoFieldChange` pin asserts this. The `LocaleSettings` doc is **untouched** — this lane adds a *new* doc, not a new field on the existing one. | SITE·6 |
| **SITE7** | The admin navigates to `/admin/site` (from the `/admin/platform` list-group row) — the GET seeds the form with the current singleton (both the Home and About sections). The admin edits the hero text or toggles a section and clicks **Save** — the POST saves the field group, writes one `AccessAudit` row, and redirects back to the GET. A non-`GlobalAdmin` is **denied** (the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST). The `AdminSiteControllerTests` pins assert all four scenarios. | SITE·7 |
| **SITE8** | The `Milestones.cs` / README / `MilestonesTests` trio is **untouched** until the lane *ships* (U08 owns the close flip). The `MilestonesTests` pin that M4 is the single `StatusNext` is **intact** through U01–U07. When U08 ships the close flip, the `Milestones.cs` registry gains one new entry (the `SITE` lane row, `StatusDone`), the README Roadmap gains one new row, and the `WhatsNew.cs` registry gains one new entry (the `0.42.0` row, newest-first). | SITE·8 |
| **SITE9** | A visitor with a screen reader loads `/` or `/about` — the two heroes' eyebrow + lead are the **first content in the DOM** (the hero band is unchanged in shape — only the text source changes). A section that the admin has toggled off is **not in the DOM at all** (not `display: none`) — a screen reader never sees it. The visible sections' `aria-label` attributes are **unchanged** (the `a11y.*` keys in `KnownTranslationKeys.cs` are untouched). | SITE·9 |
| **SITE10** | The `SiteContent` field set is the **complete** admin surface for the current scope — the 13 fields (the 4 text fields + the 9 toggle fields) are the **ceiling**: no field outside the field set may appear in the doc (the ADR 0150 D1 pin). A future SITE-2 lane **adds** fields (the `SiteContent` doc is additive per ADR 0004 §B.1); it does **not** re-shape the existing ones. The `/admin/site` view's two form sections (Home + About) expose **exactly** the 13 fields — no hidden fields, no reserved fields. | (register's "one thing" section) |

## Frozen base (unchanged)

All of the following still bind **unchanged**; this lane does not re-shape
any of them:

- **ADR 0005 B** — the `LocaleSettings` singleton (`Id = "singleton"`
  sentinel, the additive-field convention, one row per instance). The
  `SiteContent` doc will be a **new** singleton in the same shape, **not** a
  new field on `LocaleSettings` (ADR 0006 module-boundary contract).
- **ADR 0019 / ADR 0020** — the singleton-toggle admin surface shape
  (`/admin/timezone` / `/admin/dateformat`, `GlobalAdmin`-gated, one
  `AccessAudit` row per save).
- **ADR 0050** — the single-write-lane shape (`AdminSignupController`, the
  `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write,
  the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
  `TempData["info"]` flash, the `RedirectToAction(nameof(Index))`
  redirect).
- **ADR 0149** — the `Profile.HideHomeIntro` **per-resident** hide-intro
  preference (composable, not replaced — the two intro sections render when
  **both** `!HideIntro` **and** the platform flag are true).
- **ADR 0004 §B.1** — additive doc type (delta applied idempotently at boot,
  no EF migration).
- **ADR 0006** — module-boundary contract.
- **ADR 0015 D1** — `kw-l` provider-floor discipline.
- **The `LS` / `SP` / `PG` test model** — the `LS_U04_SeederTests` /
  `PageServiceTests` / `KwLRegistryConsistencyTests` /
  `KnownTranslationKeys_ParityTests` pins are untouched.
- **The `Milestones.cs` / README / `MilestonesTests` close-flip trio** —
  the M4 single-`StatusNext` pin stays intact until the lane ships (U08
  owns it).

## Seams & contracts (Part 2, written by U02)

This Part 2 is the **pinned-contract** half of the design doc. It names the
exact `SiteContent` field set, the read-seam contract, the write-lane
contract, the pinned seam-test names (by class + method), the acceptance
gate (the exact command list), and the drift guard. A later unit **never**
re-shapes any of these outside the drift-guard; a mismatch found by a later
unit is a `## U<m> — Drift pause` section in the handoff note (unit-series
rule 6), not a silent edit.

### 2.1 frozen base (unchanged)

The following still bind **unchanged** and this lane does not re-shape any
of them (re-anchored verbatim from Part 1's §Frozen base, for reference in
the Part-2 context):

- **ADR 0005 B** — the `LocaleSettings` singleton (`Id = "singleton"`
  sentinel, the additive-field convention, one row per instance). The
  `SiteContent` doc is a **new** singleton in the same shape, **not** a new
  field on `LocaleSettings`.
- **ADR 0019 / ADR 0020** — the singleton-toggle admin surface shape
  (`/admin/timezone` / `/admin/dateformat`, `GlobalAdmin`-gated, one
  `AccessAudit` row per save).
- **ADR 0050** — the single-write-lane shape (`AdminSignupController`, the
  `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write,
  the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
  `TempData["info"]` flash, the `RedirectToAction(nameof(Index))` redirect).
  The `AdminSiteController` mirrors this shape exactly.
- **ADR 0149** — the `Profile.HideHomeIntro` **per-resident** hide-intro
  preference (composable, not replaced — the two intro sections render when
  **both** `!HideIntro` **and** `HomeShowFeatures` are true; the feed +
  roadmap are governed only by the platform flag).
- **ADR 0004 §B.1** — additive doc type (delta applied idempotently at boot,
  no EF migration). The `SiteContent` doc is a **new** Marten doc type, not
  a new field on an existing one.
- **ADR 0006** — module-boundary contract. The `SiteContent` context is a
  **new** bounded context (`Kumunita.Core.SiteContent`), independent of
  `LocaleSettings` and the `Localization` context.
- **ADR 0015 D1** — `kw-l` provider-floor discipline. The `kw-l` registry
  entries stay (the `home.*` / `about.*` / `platform.*` keys are not
  removed, not re-shaped, not re-keyed).
- **The `LS` / `SP` / `PG` test model** — the `LS_U04_SeederTests` /
  `PageServiceTests` / `KwLRegistryConsistencyTests` /
  `KnownTranslationKeys_ParityTests` pins are untouched.
- **The `Milestones.cs` / README / `MilestonesTests` close-flip trio** —
  the M4 single-`StatusNext` pin stays intact until the lane ships (U08
  owns it).

### 2.2 the `SiteContent` field set (exact)

The `SiteContent` POCO (a new `Kumunita.Core.SiteContent` bounded context)
carries **exactly 13 fields** — the 4 text fields + the 9 toggle fields.
The field set is the **ceiling** (the ADR 0150 D1 pin): no field outside
this set may appear in the doc. A future SITE-2 lane **adds** fields (the
`SiteContent` doc is additive per ADR 0004 §B.1); it does **not** re-shape
the existing 13.

| # | Field | Type | Default (the `en` source text / `true`) | `kw-l` key (the canonical `en` source text the default is derived from) | a11y note |
|---|---|---|---|---|---|
| 1 | `HomeHeroEyebrow` | `string` | `"A private home for one neighbourhood"` | `home.intro_eyebrow` | the home hero's eyebrow — the first content in the DOM (SITE·9); the hero band is unchanged in shape, only the text source changes. |
| 2 | `HomeHeroLead` | `string` | `"One quiet place for everything your street does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours."` | `home.intro_lead` | the home hero's lead — the second content in the DOM (SITE·9); the hero band is unchanged in shape, only the text source changes. |
| 3 | `HomeShowAboutButton` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | the home hero's "What it is & how it works" CTA button; when `false` the button is **not in the DOM at all** (not `display: none`), so a screen reader never sees it (SITE·9). |
| 4 | `HomeShowFeatures` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | governs **both** of the home's intro sections (the hero band + the feature-cards band) together — the ADR 0149 "they hide and show as a pair" shape; when `false` **both** sections are **not in the DOM at all** (SITE·9), and the ADR 0149 `Profile.HideHomeIntro` per-resident preference is **additive** (a resident who set `HideHomeIntro = true` sees neither section regardless of this flag, SITE·5). |
| 5 | `HomeShowRoadmap` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | the home's "The plan" roadmap section; when `false` the section is **not in the DOM at all** (SITE·9); the ADR 0149 `Profile.HideHomeIntro` per-resident preference **never reaches** this section (SITE·5 — it is governed only by this platform flag). |
| 6 | `AboutHeroEyebrow` | `string` | `"Private by default"` | `about.eyebrow` | the about hero's eyebrow — the first content in the DOM of `/about` (SITE·9); the hero band is unchanged in shape, only the text source changes. |
| 7 | `AboutHeroLead` | `string` | `"One home for everything your neighborhood does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours."` | `about.lead` | the about hero's lead — the second content in the DOM of `/about` (SITE·9); the hero band is unchanged in shape, only the text source changes. |
| 8 | `AboutShowFeatures` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | the about's three feature cards; when `false` the section is **not in the DOM at all** (SITE·9). |
| 9 | `AboutShowScope` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | the about's scope band; when `false` the section is **not in the DOM at all** (SITE·9). |
| 10 | `AboutShowPhilosophy` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | the about's FIG-philosophy band; when `false` the section is **not in the DOM at all** (SITE·9). |
| 11 | `AboutShowProject` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | the about's code/docs band; when `false` the section is **not in the DOM at all** (SITE·9). |
| 12 | `AboutShowWhatsNew` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | the about's "What's new" changelog; when `false` the section is **not in the DOM at all** (SITE·9). |
| 13 | `AboutShowContactCta` | `bool` | `true` | (none — a server-side show/hide toggle, not a UI string) | the about's contact CTA band; when `false` the section is **not in the DOM at all** (SITE·9). |

**The `SiteContent` POCO (exact C#):**

```csharp
namespace Kumunita.Core.SiteContent;

/// <summary>
/// One row in the platform's landing-surface content (ADR 0150): the two
/// heroes' editable eyebrow + lead text and the show/hide toggle for every
/// other section of `/home` and `/about`. A **singleton** — one row per
/// instance, <see cref="Id"/> fixed to the sentinel <c>singleton</c> (the
/// exact <c>LocaleSettings</c> shape, ADR 0005 B), so re-resolving it is a
/// plain identity-keyed load. The 13 fields are the **ceiling** for this
/// lane's scope (the ADR 0150 D1 pin); a future SITE-2 lane **adds**
/// fields (additive per ADR 0004 §B.1), it does not re-shape the existing
/// 13. The 4 text-field defaults are the **exact** `en` source text from
/// <c>KnownTranslationKeys.cs</c> (the <c>home.intro_eyebrow</c> /
/// <c>home.intro_lead</c> / <c>about.eyebrow</c> / <c>about.lead</c> keys),
/// byte-identical (SITE·3); the 9 toggle-field defaults are <c>true</c>
/// (every section shown). The <c>LocaleSettings</c> doc is **untouched** —
/// this is a new doc, not a new field on the existing one (SITE·6, the ADR
/// 0150 D6 pin).
/// </summary>
public sealed class SiteContent
{
    public const string SingletonId = "singleton";

    public string Id { get; set; } = SingletonId;

    // ── /home hero (editable text — the exact en source text, SITE·3) ──

    /// <summary>The home hero's eyebrow (default = the <c>home.intro_eyebrow</c>
    /// English text, byte-identical to the shipped <c>kw-l</c> string).</summary>
    public string HomeHeroEyebrow { get; set; } = "A private home for one neighbourhood";

    /// <summary>The home hero's lead (default = the <c>home.intro_lead</c>
    /// English text, byte-identical to the shipped <c>kw-l</c> string).</summary>
    public string HomeHeroLead { get; set; } =
        "One quiet place for everything your street does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.";

    // ── /home section toggles (server-side show/hide, SITE·9) ──

    /// <summary>The home hero's "What it is &amp; how it works" CTA button is shown.</summary>
    public bool HomeShowAboutButton { get; set; } = true;

    /// <summary>The home's two intro sections (hero band + feature-cards band) are
    /// shown — the ADR 0149 "they hide and show as a pair" shape; the ADR 0149
    /// <c>Profile.HideHomeIntro</c> per-resident preference is additive (SITE·5).</summary>
    public bool HomeShowFeatures { get; set; } = true;

    /// <summary>The home's "The plan" roadmap section is shown — governed only by
    /// this platform flag; the ADR 0149 <c>Profile.HideHomeIntro</c> never reaches
    /// this section (SITE·5).</summary>
    public bool HomeShowRoadmap { get; set; } = true;

    // ── /about hero (editable text — the exact en source text, SITE·3) ──

    /// <summary>The about hero's eyebrow (default = the <c>about.eyebrow</c>
    /// English text, byte-identical to the shipped <c>kw-l</c> string).</summary>
    public string AboutHeroEyebrow { get; set; } = "Private by default";

    /// <summary>The about hero's lead (default = the <c>about.lead</c>
    /// English text, byte-identical to the shipped <c>kw-l</c> string).</summary>
    public string AboutHeroLead { get; set; } =
        "One home for everything your neighborhood does — the feed, the groups, and the notes that deserve better than a group chat. Private, plain-language, and yours.";

    // ── /about section toggles (server-side show/hide, SITE·9) ──

    /// <summary>The about's three feature cards are shown.</summary>
    public bool AboutShowFeatures { get; set; } = true;

    /// <summary>The about's scope band is shown.</summary>
    public bool AboutShowScope { get; set; } = true;

    /// <summary>The about's FIG-philosophy band is shown.</summary>
    public bool AboutShowPhilosophy { get; set; } = true;

    /// <summary>The about's code/docs band is shown.</summary>
    public bool AboutShowProject { get; set; } = true;

    /// <summary>The about's "What's new" changelog is shown.</summary>
    public bool AboutShowWhatsNew { get; set; } = true;

    /// <summary>The about's contact CTA band is shown.</summary>
    public bool AboutShowContactCta { get; set; } = true;
}
```

**The `ISiteContentService` seam (exact C#):**

```csharp
namespace Kumunita.Core.SiteContent;

/// <summary>
/// The read + write seams for the <c>SiteContent</c> singleton (ADR 0150).
/// The <c>GetAsync</c> read is the ADR 0050 <c>IsSignupOpenAsync</c>
/// best-effort shape (missing row / read failure degrades to the in-code
/// fallback — never throws, never returns null); the <c>SaveAsync</c> write
/// is the ADR 0050 <c>SetSignupOpenAsync</c> single audited write-lane
/// shape (one session, one <c>AccessAudit</c> row, strong consistency).
/// </summary>
public interface ISiteContentService
{
    /// <summary>
    /// Loads the <c>SiteContent</c> singleton. **Best-effort**: a missing
    /// store, a missing row (a fresh boot before the seeder ran), or a read
    /// failure degrades to the **in-code fallback** (a fresh
    /// <see cref="SiteContent"/> with every field at its shipped default —
    /// the byte-identical <c>kw-l</c> text + every section shown). The read
    /// is a **public landing surface** (SITE·1) — never an access decision,
    /// never a claim, never audited.
    /// </summary>
    Task<SiteContent> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Saves the <c>SiteContent</c> singleton — the ADR 0050
    /// <c>SetSignupOpenAsync</c> single audited write-lane shape. Loads the
    /// existing row (a missing row is a **no-op upsert** — the lane does not
    /// load-or-create; a fresh instance is already at the in-code fallback,
    /// and the seeder is the only writer that creates the row), applies the
    /// full 13-field set from <paramref name="content"/>, and saves in one
    /// session (invariant C3). Exactly **one** <c>AccessAudit</c> row per
    /// call (<c>Via = Admin</c>, action <c>site.save</c>, <c>TargetKind</c>
    /// "site" — the <c>signup.set-open</c> / <c>timezone.set-default</c> /
    /// <c>dateformat.set-default</c> singleton-toggle shape, SITE·2).
    /// **Strong consistency** (invariant C4): the new value is live on the
    /// very next <c>GetAsync</c> / render. The lane **upserts** the
    /// singleton — it never creates a second row (SITE·6).
    /// </summary>
    Task SaveAsync(SiteContent content, string actorBy, CancellationToken ct = default);
}
```

### 2.3 the read-seam contract (exact C#)

The read is the `SiteContentService.GetAsync()` call, made by the two
landing-surface controllers (`HomeController.Index` /
`StaticPagesController.About`). It is **best-effort** — a missing seam, a
missing row, or a read failure degrades to the in-code fallback (the
shipped defaults), and the page **always renders** (the ADR 0050
`IsSignupOpenAsync` / ADR 0149 `HideHomeIntro` best-effort shape, SITE·1).

```csharp
// Kumunita.Core.SiteContent.SiteContentService (the read half)
public sealed class SiteContentService : ISiteContentService
{
    private readonly IDocumentSession _session; // the ADR 0050 IIdentityService session-composition shape

    public SiteContentService(IDocumentSession session) => _session = session;

    public Task<SiteContent> GetAsync(CancellationToken ct = default)
    {
        // Best-effort: a missing row (a fresh boot before the seeder ran) or a
        // read failure degrades to the **in-code fallback** — a fresh SiteContent
        // with every field at its shipped default (the byte-identical kw-l text +
        // every section shown). The read never throws and never returns null;
        // the page always renders (SITE·1, the ADR 0050 IsSignupOpenAsync shape).
        // The read is world-readable — never an access decision, never a claim,
        // never audited (SITE·1, ADR 0001-B thin-token).
        var row = _session.Load<SiteContent>(SiteContent.SingletonId);
        return Task.FromResult(row ?? new SiteContent());
    }
    // SaveAsync in §2.4.
}

// Kumunita.Web.Controllers.HomeController (the read call-site)
public async Task<IActionResult> Index()
{
    var site = await _siteContent.GetAsync(); // best-effort; always a non-null SiteContent
    // ... build HomeViewModel, passing `site` through a new HomeViewModel.Site
    // field (the ADR 0149 HideIntro field is **untouched** — the two intro
    // sections render when **both** !HideIntro **and** site.HomeShowFeatures
    // are true; the feed + roadmap are governed only by site.HomeShowRoadmap).
    return View(model);
}

// Kumunita.Web.Controllers.StaticPagesController (the About read call-site)
public async Task<IActionResult> About()
{
    var site = await _siteContent.GetAsync(); // best-effort; always a non-null SiteContent
    // ... build the About view-model, passing `site` through; the hero text
    // is @Model.Site.AboutHeroEyebrow / @Model.Site.AboutHeroLead; the six
    // section toggles gate the six secondary sections (the hero is always
    // rendered — it is the landing surface's first content, SITE·9).
    return View(model);
}
```

### 2.4 the write-lane contract (exact C#)

The write is the `SiteContentService.SaveAsync(content, actorBy)` lane —
the ADR 0050 `SetSignupOpenAsync` single audited write-lane shape. It loads
the existing row (a missing row is a **no-op** — the lane never
load-or-creates; the seeder is the only writer that creates the row),
applies the 13-field set from the caller's `SiteContent`, saves in one
session (invariant C3), and writes **exactly one** `AccessAudit` row
(`Via = Admin`, action `site.save`, `TargetKind` "site" — the
`signup.set-open` shape). **Strong consistency** (invariant C4): the new
value is live on the very next `GetAsync` / render. The lane **upserts**
the singleton — it never creates a second row (SITE·6).

```csharp
// Kumunita.Core.SiteContent.SiteContentService (the write half)
public async Task SaveAsync(SiteContent content, string actorBy, CancellationToken ct = default)
{
    // ADR 0050 SetSignupOpenAsync shape: one session, one SaveChangesAsync,
    // exactly one AccessAudit row (Via = Admin, action "site.save", TargetKind
    // "site"). The lane upserts the singleton (it does not create a second row,
    // SITE·6) — the seeder is the only writer that creates the row on a fresh
    // boot; a save on a missing row is a no-op (a fresh instance is already at
    // the in-code fallback, and the admin has nothing to overwrite).
    var stored = _session.Load<SiteContent>(SiteContent.SingletonId);
    if (stored is null) return; // a missing row is a no-op — the lane never load-or-creates (SITE·6)

    stored.HomeHeroEyebrow      = content.HomeHeroEyebrow;
    stored.HomeHeroLead         = content.HomeHeroLead;
    stored.HomeShowAboutButton  = content.HomeShowAboutButton;
    stored.HomeShowFeatures     = content.HomeShowFeatures;
    stored.HomeShowRoadmap      = content.HomeShowRoadmap;
    stored.AboutHeroEyebrow     = content.AboutHeroEyebrow;
    stored.AboutHeroLead        = content.AboutHeroLead;
    stored.AboutShowFeatures    = content.AboutShowFeatures;
    stored.AboutShowScope       = content.AboutShowScope;
    stored.AboutShowPhilosophy  = content.AboutShowPhilosophy;
    stored.AboutShowProject     = content.AboutShowProject;
    stored.AboutShowWhatsNew    = content.AboutShowWhatsNew;
    stored.AboutShowContactCta  = content.AboutShowContactCta;

    _session.Store(stored);
    _session.Audit(new AccessAudit
    {
        ActorId   = actorBy,
        Via       = AccessVia.Admin,
        Action    = "site.save",
        TargetKind = "site",
        TargetId  = SiteContent.SingletonId,
        At        = DateTimeOffset.UtcNow,
    });
    await _session.SaveChangesAsync(ct); // invariant C3 — one commit, strong consistency (C4)
}

// Kumunita.Web.Controllers.AdminSiteController (the thin wrapper — the ADR 0050 AdminSignupController shape)
[Route("admin/site")]
[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]
public sealed class AdminSiteController(ISiteContentService site) : Controller
{
    private static string? ActorId(ClaimsPrincipal user) => KumunitaPrincipal.SubjectId(user);

    /// <summary>GET /admin/site — seeds both form sections (Home + About) with the
    /// current singleton. The read is best-effort — a missing row degrades to the
    /// in-code fallback (the shipped defaults), so the form always renders
    /// (SITE·1, the ADR 0050 IsSignupOpenAsync shape).</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var site = await site.GetAsync();
        return View(new AdminSiteViewModel
        {
            Home = new AdminSiteViewModel.HomeSection
            {
                HomeHeroEyebrow      = site.HomeHeroEyebrow,
                HomeHeroLead         = site.HomeHeroLead,
                HomeShowAboutButton  = site.HomeShowAboutButton,
                HomeShowFeatures     = site.HomeShowFeatures,
                HomeShowRoadmap      = site.HomeShowRoadmap,
            },
            About = new AdminSiteViewModel.AboutSection
            {
                AboutHeroEyebrow     = site.AboutHeroEyebrow,
                AboutHeroLead        = site.AboutHeroLead,
                AboutShowFeatures    = site.AboutShowFeatures,
                AboutShowScope       = site.AboutShowScope,
                AboutShowPhilosophy  = site.AboutShowPhilosophy,
                AboutShowProject     = site.AboutShowProject,
                AboutShowWhatsNew    = site.AboutShowWhatsNew,
                AboutShowContactCta  = site.AboutShowContactCta,
            },
        });
    }

    /// <summary>POST /admin/site/save-home — saves the Home section's 5 fields.
    /// The ADR 0050 AdminSignupController.Save shape: a GlobalAdmin-gated
    /// [ValidateAntiForgeryToken] POST, a TempData["info"] flash, a
    /// RedirectToAction(nameof(Index)) redirect. One AccessAudit row per call
    /// (action "site.save", TargetKind "site") — the signup.set-open shape
    /// (SITE·2).</summary>
    [HttpPost("save-home")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHome(AdminSiteViewModel.HomeSection home)
    {
        var actor = ActorId(User) ?? string.Empty;
        var current = await site.GetAsync();
        await site.SaveAsync(new SiteContent
        {
            // The Home section's 5 fields, verbatim from the form.
            HomeHeroEyebrow     = home.HomeHeroEyebrow,
            HomeHeroLead        = home.HomeHeroLead,
            HomeShowAboutButton = home.HomeShowAboutButton,
            HomeShowFeatures    = home.HomeShowFeatures,
            HomeShowRoadmap     = home.HomeShowRoadmap,
            // The About section's 8 fields, unchanged (the save is partial —
            // saving the Home section does not touch the About section's
            // fields, the ADR 0050 "one field per save" shape extended to
            // "one section per save").
            AboutHeroEyebrow    = current.AboutHeroEyebrow,
            AboutHeroLead       = current.AboutHeroLead,
            AboutShowFeatures   = current.AboutShowFeatures,
            AboutShowScope      = current.AboutShowScope,
            AboutShowPhilosophy = current.AboutShowPhilosophy,
            AboutShowProject    = current.AboutShowProject,
            AboutShowWhatsNew   = current.AboutShowWhatsNew,
            AboutShowContactCta = current.AboutShowContactCta,
        }, actor);
        TempData["info"] = "The home page's hero text and section toggles have been saved.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>POST /admin/site/save-about — saves the About section's 8
    /// fields. The ADR 0050 AdminSignupController.SaveNotify shape (a
    /// sibling POST action with its own [ValidateAntiForgeryToken] + flash +
    /// redirect). One AccessAudit row per call (SITE·2).</summary>
    [HttpPost("save-about")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAbout(AdminSiteViewModel.AboutSection about)
    {
        var actor = ActorId(User) ?? string.Empty;
        var current = await site.GetAsync();
        await site.SaveAsync(new SiteContent
        {
            // The About section's 8 fields, verbatim from the form.
            AboutHeroEyebrow    = about.AboutHeroEyebrow,
            AboutHeroLead       = about.AboutHeroLead,
            AboutShowFeatures   = about.AboutShowFeatures,
            AboutShowScope      = about.AboutShowScope,
            AboutShowPhilosophy = about.AboutShowPhilosophy,
            AboutShowProject    = about.AboutShowProject,
            AboutShowWhatsNew   = about.AboutShowWhatsNew,
            AboutShowContactCta = about.AboutShowContactCta,
            // The Home section's 5 fields, unchanged (the save is partial).
            HomeHeroEyebrow     = current.HomeHeroEyebrow,
            HomeHeroLead        = current.HomeHeroLead,
            HomeShowAboutButton = current.HomeShowAboutButton,
            HomeShowFeatures    = current.HomeShowFeatures,
            HomeShowRoadmap     = current.HomeShowRoadmap,
        }, actor);
        TempData["info"] = "The about page's hero text and section toggles have been saved.";
        return RedirectToAction(nameof(Index));
    }

    // ── View model (public nested type so the Razor view can bind to it) ──

    public sealed class AdminSiteViewModel
    {
        public HomeSection Home { get; init; } = new();
        public AboutSection About { get; init; } = new();

        public sealed class HomeSection
        {
            public string HomeHeroEyebrow { get; init; } = string.Empty;
            public string HomeHeroLead { get; init; } = string.Empty;
            public bool HomeShowAboutButton { get; init; } = true;
            public bool HomeShowFeatures { get; init; } = true;
            public bool HomeShowRoadmap { get; init; } = true;
        }

        public sealed class AboutSection
        {
            public string AboutHeroEyebrow { get; init; } = string.Empty;
            public string AboutHeroLead { get; init; } = string.Empty;
            public bool AboutShowFeatures { get; init; } = true;
            public bool AboutShowScope { get; init; } = true;
            public bool AboutShowPhilosophy { get; init; } = true;
            public bool AboutShowProject { get; init; } = true;
            public bool AboutShowWhatsNew { get; init; } = true;
            public bool AboutShowContactCta { get; init; } = true;
        }
    }
}
```

**The `/admin/site` surface is a dedicated `AdminSiteController`** (SITE·7)
— the `AdminController`'s constructor is pinned by two Web-layer test
harnesses (`AdminControllerBlockTests` / `AdminControllerMandatoryTests`),
so a new dependency there would break them; a separate `/admin/site`
surface mirrors `/admin/signup` / `/admin/timezone` / `/admin/dateformat`
(the codebase already puts the platform-settled instance values on their
own controllers). The `/admin/platform` page gains one list-group row
linking to `/admin/site` (the `SP U03` / `ADR 0050` discoverability
pattern).

### 2.5 the pinned seam-test names (exact)

**Frozen — a unit may never introduce a test whose exact name is not on
this list** (unit-series rule 3). The names below are the **only** test
names the SITE lane authors; a later unit that finds a mismatch is a
`## U<m> — Drift pause` section in the handoff note, not a silent edit.

**Core.Tests — `SiteContentServiceTests` (the ADR 0050 single-write-lane
shape, SITE·2 / SITE·6):**

1. `SiteContentServiceTests.GetAsync_MissingStore_ReturnsInCodeFallback` — a
   missing store (no row, a fresh boot before the seeder ran) degrades to
   the in-code fallback (a fresh `SiteContent` with every field at its
   shipped default — the byte-identical `kw-l` text + every section
   shown); the read never throws, never returns null (SITE·1, the ADR 0050
   `IsSignupOpenAsync` best-effort shape).
2. `SiteContentServiceTests.SaveAsync_WritesOneAccessAuditRow` — a save
   writes **exactly one** `AccessAudit` row (`Via = Admin`, action
   `site.save`, `TargetKind` "site" — the `signup.set-open` shape, SITE·2).
3. `SiteContentServiceTests.SaveAsync_StrongConsistency_LiveOnNextGetAsync` —
   a save is **live on the very next `GetAsync`** call (invariant C4 — the
   new value is read back without a restart, SITE·2).
4. `SiteContentServiceTests.SaveAsync_UpsertsSingleton_NoDuplicateRow` — a
   save **upserts** the singleton; a second save does not create a second
   row (the `LocaleSettings` "one row per instance" shape, SITE·6).

**Core.Tests — `SiteContentSeederTests` (the `LS_U04_SeederTests` /
`PageServiceTests` "seeded defaults match the shipped text" pattern,
SITE·3 / SITE·6):**

5. `SiteContentSeederTests.FreshBoot_HasExactlyOneSiteContentRow` — a fresh
   boot has **exactly one** `SiteContent` row (`Id = "singleton"` — the
   exact `LocaleSettings` shape, SITE·6).
6. `SiteContentSeederTests.FreshBoot_DefaultsMatchShippedKwLText` — the
   `HomeHeroEyebrow` / `HomeHeroLead` / `AboutHeroEyebrow` /
   `AboutHeroLead` fields equal the **exact** `home.intro_eyebrow` /
   `home.intro_lead` / `about.eyebrow` / `about.lead` English strings from
   `KnownTranslationKeys.cs` (the `en` source text, byte-identical, SITE·3);
   every section-toggle field is `true`.
7. `SiteContentSeederTests.SecondBoot_IsIdempotent_NoDuplicateRow_NoFieldChange` —
   a second boot is **idempotent** (no duplicate row, no field change — the
   create-if-missing / never-overwrites pattern, SITE·6).

**Web.Tests — `HomeControllerSiteContentTests` (the `/home` read seam + the
ADR 0149 composability, SITE·1 / SITE·5 / SITE·9):**

8. `HomeControllerSiteContentTests.FreshInstance_RendersShippedText_EverySectionShown` —
   a fresh instance (the in-code fallback) renders `/home` with **exactly**
   the shipped `kw-l` text (the hero eyebrow + lead) and **every** section
   shown (the hero band, the "What it is" CTA button, the feature-cards
   band, the roadmap; the "What's new" feed renders when the viewer is
   signed-in, the ADR 0149 `HideHomeIntro = false` default, SITE·1 /
   SITE·3).
9. `HomeControllerSiteContentTests.SavedRow_HomeShowAboutButtonFalse_HidesHomeHeroButton` —
   a saved `SiteContent` row with `HomeShowAboutButton = false` renders the
   home hero **without** the "What it is & how it works" CTA button (the
   button is **not in the DOM at all**, not `display: none`, SITE·9).
10. `HomeControllerSiteContentTests.ADR_0149_HideHomeIntroTrue_HomeShowFeaturesTrue_HidesHomeFeatureCards` —
    a resident who set `Profile.HideHomeIntro = true` (ADR 0149) sees
    **neither** of the two intro sections **even if** the admin has
    `HomeShowFeatures = true` (the per-resident preference is the
    *resident's* choice to hide; the platform flag is the *admin's* choice
    to show — **both** must agree for the sections to appear, SITE·5).
11. `HomeControllerSiteContentTests.ADR_0149_HideHomeIntroFalse_HomeShowFeaturesFalse_HidesHomeFeatureCards` —
    a resident who set `Profile.HideHomeIntro = false` (the default) sees
    **neither** of the two intro sections when the admin has
    `HomeShowFeatures = false` (the platform flag is the *admin's* choice
    to show; when it is `false` the sections are hidden regardless of the
    per-resident preference, SITE·5).

**Web.Tests — `AdminSiteControllerTests` (the `/admin/site` write lane,
SITE·2 / SITE·7):**

12. `AdminSiteControllerTests.GET_SeesCurrentSingleton` — the GET seeds
    both form sections (Home + About) with the **current** singleton (the
    `SiteContent` row in the store); a missing row degrades to the in-code
    fallback (the shipped defaults), so the form always renders (SITE·1,
    the ADR 0050 `IsSignupOpenAsync` best-effort shape).
13. `AdminSiteControllerTests.POST_SaveHome_SavesFieldGroup_WritesOneAccessAuditRow` —
    the `POST /admin/site/save-home` saves the Home section's 5 fields
    (the About section's 8 fields are **unchanged** — the save is partial),
    writes **exactly one** `AccessAudit` row (`Via = Admin`, action
    `site.save`, `TargetKind` "site"), and redirects back to the GET
    (SITE·2, the ADR 0050 `AdminSignupController.Save` shape).
14. `AdminSiteControllerTests.POST_SaveAbout_SavesFieldGroup_WritesOneAccessAuditRow` —
    the `POST /admin/site/save-about` saves the About section's 8 fields
    (the Home section's 5 fields are **unchanged** — the save is partial),
    writes **exactly one** `AccessAudit` row, and redirects back to the
    GET (SITE·2, the ADR 0050 `AdminSignupController.SaveNotify` shape).
15. `AdminSiteControllerTests.POST_NonGlobalAdmin_IsDenied` — a non-`GlobalAdmin`
    (a signed-in resident, or an anonymous visitor) is **denied** the
    `POST /admin/site/save-home` / `POST /admin/site/save-about` actions
    (the `GlobalAdmin`-gated `[Authorize(Roles = GlobalAdmin)]` +
    `[ValidateAntiForgeryToken]` POST, SITE·7).

### 2.6 the acceptance gate (exact)

The acceptance gate is the **exact** command list below. A later unit
(U03–U08) runs the gate after its own deliverable and records the result
in the handoff note. The gate is **green** when:

```powershell
# 1. The solution builds green (the touched projects — Kumunita.Core, Kumunita.Web, Kumunita.Core.Tests, Kumunita.Web.Tests).
dotnet build Kumunita.slnx -c Debug

# 2. The Core.Tests suite is green (the SiteContentServiceTests + SiteContentSeederTests pins, the ADR 0050 shape).
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll

# 3. The Web.Tests suite is green (the HomeControllerSiteContentTests + AdminSiteControllerTests pins, the ADR 0149 composability + the ADR 0050 write lane).
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

Plus the **regression pins** (the precedent shapes are unchanged — the
`kw-l` registry entries are untouched, the ADR 0050 / ADR 0149 / ADR 019 /
ADR 0020 shapes are unchanged, the close-flip trio is intact):

- `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` **green**
  (the `home.*` / `about.*` / `platform.*` registry entries are **untouched** —
  SITE·4, the ADR 0150 D3 pin).
- `LS_U04_SeederTests` + `PageServiceTests` **green** (the ADR 0005 B / ADR
  0043 "seeded defaults match the shipped text" pins are unchanged).
- `HomeControllerTests` + `StaticPagesControllerTests` **green** (the ADR
  0149 `Profile.HideHomeIntro` read path is unchanged — SITE·5, the ADR 0150
  D5 pin).
- `AdminSignupControllerTests` + `AdminControllerTests` **green** (the ADR
  0050 single-write-lane shape + the `AdminController` constructor pin are
  unchanged — SITE·7).
- `MilestonesTests` **green** (the M4 single-`StatusNext` pin is intact —
  SITE·8, the ADR 0150 D7 pin).
- `WhatsNewTests` **green** (the new `0.42.0` entry is present,
  newest-first — SITE·8, the ADR 0150 D7 pin).

**Runner (per AGENTS.md):** the `dotnet exec` path on the test assemblies,
**not** `dotnet test` / VS Test Explorer (the xunit.v3 discovery quirk on
this machine).

### 2.7 the drift guard (frozen once written)

The following are **frozen** once this Part 2 is written; a mismatch found
by a later unit is a `## U<m> — Drift pause` section in the handoff note
(unit-series rule 6), **not** a silent edit:

- **The 13-field `SiteContent` field set** (§2.2) — the **ceiling** (the
  ADR 0150 D1 pin): no field outside the field set may appear in the doc;
  a future SITE-2 lane **adds** fields (additive per ADR 0004 §B.1), it
  does **not** re-shape the existing 13.
- **The `kw-l` registry entries** — the **floor** (the ADR 0150 D3 pin):
  the `home.*` / `about.*` / `platform.*` keys in
  `KnownTranslationKeys.cs` are **not** removed, not re-shaped, not
  re-keyed; the registry parity tests (`KwLRegistryConsistencyTests` /
  `KnownTranslationKeys_ParityTests`) are **untouched**.
- **The ADR 0149 `Profile.HideHomeIntro` read** — the **frozen base** (the
  ADR 0150 D5 pin): the per-resident preference is **unchanged** (the
  `HomeController.Index` read of `Profile.HideHomeIntro` is untouched; the
  two intro sections render when **both** `!HideIntro` **and**
  `HomeShowFeatures` are true; the feed + roadmap are governed **only** by
  the platform flag).
- **The `LocaleSettings` singleton** — **untouched** (the ADR 0150 D6 pin):
  this lane adds a **new** doc, not a new field on the existing one; the
  `LocaleSettings` doc's shape (the `DefaultLanguageCode` /
  `DefaultTimezone` / `DefaultDateFormat` / `IsSignupOpen` /
  `NotifyAdminsOnSignup` / `AnnouncementCommentsEnabled` /
  `MessagingEnabled` / `QuietCheckMinutes` / … fields) is **unchanged**.
- **The read-seam contract** (§2.3) — `ISiteContentService.GetAsync()` is
  the ADR 0050 `IsSignupOpenAsync` best-effort shape (missing row / read
  failure degrades to the in-code fallback; never throws, never returns
  null; the page always renders).
- **The write-lane contract** (§2.4) — `ISiteContentService.SaveAsync` is
  the ADR 0050 `SetSignupOpenAsync` single audited write-lane shape (one
  session, one `AccessAudit` row, `Via = Admin`, action `site.save`,
  `TargetKind` "site"; strong consistency; upserts the singleton).
- **The 15 pinned seam-test names** (§2.5) — verbatim, a unit may never
  introduce a test outside this list.
- **The acceptance gate** (§2.6) — the exact command list + the regression
  pins; the gate is **green** only when every line passes.
- **The frozen base** (§2.1) — ADR 0005 B / ADR 0019 / ADR 0020 / ADR 0050
  / ADR 0149 / ADR 0004 §B.1 / ADR 0006 / ADR 0015 D1 / the `LS` / `SP` /
  `PG` test model / the `Milestones.cs` / README / `MilestonesTests`
  close-flip trio — **unchanged**.

*— Part 2 (seams & contracts) authored by U02 (2026-10-07). ADR 0150 is
drafted (Status: Accepted) in the same unit.*
