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
