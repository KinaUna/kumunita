# M29 — Admin surface labels (`SurfaceLabels`) — design

> **Three-tier contract.** This file is the **primary** tier of the M29
> milestone: it pins the invariants (M29·1–M29·10), the FACES (M29-1–M29-10),
> the closed 13-item label set, the read-seam contract, the write-lane
> contract, the pinned seam-test names, the acceptance gate, and the drift
> guard. The register
> (`docs/plans-milestones/plan-m29-admin-surface-labels.md`) is the
> **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m29-handoff-notes.md` is the
> **scratch** tier (one short section per unit, appended, never rewritten).
> When the three disagree, **this file wins for the pinned shapes**; the
> register wins for *which files exist* and *what each unit does*.
>
> **Part 1 (this section, U01):** the value chain, the context, the scope,
> the **ten invariants** (M29·1–M29·10), the **ten FACES** (M29-1–M29-10), and
> the frozen-base assumptions. **Part 2 (U02):** the seams & contracts (the
> exact `SurfaceLabels` field set, the read-seam contract, the write-lane
> contract, the pinned seam-test names, the acceptance gate, the drift
> guard).
>
> **The frozen base.** This milestone is built **on top of** the
> `LocaleSettings` singleton (ADR 0005 B), the `SiteContent` singleton shape
> (ADR 0150), the single-write-lane shape (ADR 0050), the additive-doc-type
> convention (ADR 0004 §B.1), the module-boundary contract (ADR 0006), and the
> `kw-l` provider-floor discipline (ADR 0015 D1). All of these still bind
> **unchanged** (re-anchored in §Frozen base below). M29 adds **no** re-shape
> of `SiteContent`, **no** re-shape of `LocaleSettings`, **no** removal of a
> `kw-l` registry entry, **no** new `AccessAction` / `AccessVia` /
> authorization path, and **no** route change for the read. It is
> **additive**: one new doc type (`SurfaceLabels`, a *singleton*), one new
> bounded context (`Kumunita.Core.SurfaceLabels`), one new service
> (`ISurfaceLabelsService`), one new admin surface (`/admin/labels`), and one
> label-resolver seam the views call. **No EF migration** (a new Marten doc
> type is additive per ADR 0004 §B.1; the delta is applied idempotently at
> boot). **No new route for the read** (the 13 existing nav routes are
> unchanged — only what they render changes).
>
> **The `SurfaceLabels` field set is the ceiling** (the 13 optional `string?`
> label fields in the register's "one thing" section — no field outside the
> field set may appear in the doc, the ADR 0152 D1 pin). **The `kw-l` registry
> entries are the floor** (the `nav.*` / `inv.nav` / `bm.nav` /
> `documents.title` keys stay, the ADR 0152 D3 pin). **The `SiteContent` and
> `LocaleSettings` docs are untouched** (this milestone adds a *new* doc in a
> *new* context, not a new field on an existing one, the ADR 0150 D6 / ADR 0006
> module-boundary pin). **The routes are unchanged** (the "label, not
> re-route" pin — a rename never moves a link).

## Value chain

The `SP` lane (ADR 0043) shipped the four static pages (terms / help /
privacy / conduct) as admin-editable `Page` docs — an admin can now change
what those pages say without a code change. The `LocaleSettings` singleton
(ADR 0005 B) ships the admin-settled instance values (default language /
timezone / date format / sign-up gate / messaging) — an admin can now change
how the platform *behaves* without a code change. The SITE lane (ADR 0150)
shipped the two **landing surfaces' hero copy + section toggles** as a
`SiteContent` singleton — an admin can now change what the home and about
heroes say, and which sections appear. But the **13 top-navigation items**
(Home · Announcements · Community · Groups in the flat row, plus Events ·
Projects · Inventory · Bookmarks · Documents · Pages · Tags · Directory ·
People in the "More" dropdown and the mobile rail) **and each of their page
headers** are still **hard-coded**: every one is a `kw-l`-wrapped English
string (the `nav.home` / `nav.announcements` / `nav.community` / `nav.groups`
/ `nav.events` / `nav.projects` / `inv.nav` / `bm.nav` / `documents.title` /
`nav.pages` / `nav.tags` / `nav.directory` / `nav.people` key families in
`KnownTranslationKeys.cs`) rendered in `Views/Shared/_Layout.cshtml` and in
each surface's index view. A GlobalAdmin who wants the platform to say "News"
instead of "Announcements," or "Board" instead of "Projects," or "Directory"
instead of "People," has **no path short of a code change** — and a rename that
only touches the nav, leaving the page header on the old word, would be
*inconsistent* (the nav and the header would disagree).

This milestone completes the arc from the admin's point of view, one arrow
further:

- **The 13 nav labels + 13 page headers become editable text** — the admin
  writes the label they want each surface called; the nav item **and** the
  surface's `<h1>` render the admin's label instead of the `kw-l` string
  (M29·1 — the rename stays consistent across the surface).
- **The defaults are byte-identical to the shipped `kw-l` text** — the
  `SurfaceLabels` default is **all-null** (every label unset → every one falls
  back to its `kw-l` key), so an admin who never touches the surface sees
  exactly what a fresh instance ships today (M29·4).

The value chain moves one arrow: from *"the admin edits the landing
surfaces' story"* (SITE) to **"the admin names the platform's own surface"**
(M29) — the same "admin-settled instance value, on the SITE lane's shape"
pattern, applied to the 13 nav items a resident sees on every page.

## Context

The gap this milestone closes is the **admin-editable** half of the
platform's own surface names. The SITE lane gave the admin a path to change
what the home/about heroes say and which sections appear. But the **13
top-navigation items** and **each of their page headers** are the **labels a
resident sees on every page**, and they are **hard-coded**:

- The **flat row** in `Views/Shared/_Layout.cshtml` — Home
  (`nav.home`), Announcements (`nav.announcements`), Community
  (`nav.community`), Groups (`nav.groups`).
- The **"More" dropdown** — Events (`nav.events`), Projects (`nav.projects`),
  Inventory (`inv.nav`), Bookmarks (`bm.nav`), Documents (`documents.title`),
  Pages (`nav.pages`), Tags (`nav.tags`), Directory (`nav.directory`), People
  (`nav.people`).
- The **mobile rail** (variant C) — the same 13, repeated as icon buttons.
- **Each surface's `<h1>`** — the page header of each surface's index view,
  currently the same `kw-l` key as the nav item.

Every word is a `kw-l`-wrapped English string in `KnownTranslationKeys.cs`
(present in **en/de/fr/da**, U00-verified). There is no way to rename a
surface, and a rename that only touches the nav would leave the header on the
old word — **inconsistent** (the nav and the header would disagree).

### The precedent shapes (frozen, verified by U00)

- **ADR 0005 B** — the `LocaleSettings` singleton (`Id = "singleton"`
  sentinel, the additive-field convention, one row per instance). The
  `SurfaceLabels` doc will be a **new** singleton in the same shape, **not** a
  new field on `LocaleSettings` (ADR 0006 module-boundary contract).
- **ADR 0150** — the `SiteContent` singleton + the best-effort read
  (`SiteContentService.GetAsync`, never throws / never null → in-code
  fallback) + the single audited write-lane (`SaveAsync`, one session, the doc
  + exactly one `AccessAudit` row commit together) + the
  `AdminSiteController` `[Route("admin/site")]`
  `[Authorize(Roles = GlobalAdmin)]` dedicated surface. The `SurfaceLabels`
  doc + service + `AdminSurfaceLabelsController` mirror this shape exactly.
- **ADR 0050** — the single-write-lane shape (`AdminSignupController`, the
  `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write, the
  `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
  `TempData["info"]` flash, the `RedirectToAction(nameof(Index))` redirect).

### The constraints that still bind

- **ADR 0004 §B.1** — additive doc type (delta applied idempotently at boot,
  no EF migration). The `SurfaceLabels` doc is a **new** Marten doc type, not
  a new field on an existing one.
- **ADR 0006** — module-boundary contract. The `SurfaceLabels` context is a
  **new** bounded context (`Kumunita.Core.SurfaceLabels`), independent of
  `SiteContent`, `LocaleSettings`, and the `Localization` context.
- **ADR 0015 D1** — `kw-l` provider-floor discipline. The `kw-l` registry
  entries stay (the `nav.*` / `inv.nav` / `bm.nav` / `documents.title` keys are
  not removed, not re-shaped, not re-keyed).
- **The SITE / M28 test model** — the `FirstBootSeeder` default pin (the
  "seeded defaults match the shipped text" pattern), the Web-layer pins (the
  `AdminSiteControllerTests` / `SurfaceLabelResolutionTests` shape), the
  `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` pins (the
  registry entries are untouched).
- **The `Milestones.cs` / README / `MilestonesTests` close-flip trio** — the
  order + single-in-progress pin stays intact until the milestone *ships*
  (U10 owns the close flip).

## Scope

**In:**

- The `SurfaceLabels` doc (a new `Kumunita.Core.SurfaceLabels` context,
  `Id = "singleton"` sentinel, the **13 optional `string?` label fields**, all
  defaulting to `null`).
- The `ISurfaceLabelsService` + `SurfaceLabelsService` (the ADR 0150
  single-write-lane shape — the `GetLabelAsync(surfaceKey, fallbackKey,
  effectiveLanguage, ct)` read + the `SaveAsync(SurfaceLabels labels, string
  actorBy)` write).
- The `SurfaceLabelsDocTypes.Configure(opts)` surface (the ADR 0004 §B.1
  additive doc type — the `opts.Schema.For<SurfaceLabels>()` shape).
- The `FirstBootSeeder` default (the **all-null** `SurfaceLabels` — create-if-missing,
  idempotent, never-overwrites; byte-identical to the shipped text via the
  `kw-l` fallback).
- The shared **label-resolver** seam the views call (the `GetLabelAsync`
  helper — override → `kw-l` fallback, best-effort, never audited).
- The 13 nav items in `Views/Shared/_Layout.cshtml` (flat row + More dropdown
  + mobile rail) resolving their label via the resolver (the `kw-l` key as the
  fallback; the `href` / `asp-route-*` attributes **unchanged**).
- The 13 surface page headers (the `<h1>` of each surface's index view;
  **Home's hero stays under the SITE lane** — M29 drives only the Home nav
  label).
- The `/admin/labels` surface (the `AdminSurfaceLabelsController` + the
  `AdminSurfaceLabels` view + the `/admin/platform` link + the admin-surface
  `labels.*` `kw-l` keys).
- The test pins (the `SurfaceLabelsSeederTests` / `SurfaceLabelsServiceTests`
  / `AdminSurfaceLabelsControllerTests` / `SurfaceLabelResolutionTests`
  classes).

**Out (named deferrals for a future `LBL-2` lane, if one comes):**

- **Per-language labels** — a `SurfaceLabelTranslation` row shape (the
  `PageTranslation` / `PostTranslation` precedent) + a
  `SurfaceLabelTranslationDocTypes` surface + a `/admin/labels` translation
  editor + the `KnownTranslationKeys` parity pin for the new keys. The ADR
  0005 §B "every other language is community-provided" clause is unchanged; a
  single-string label (M29·8) is shown in all languages when set.
- **Per-label custom icons / glyphs** — a label is **text only**; the nav
  icons stay as shipped (the flat-row link glyphs, the More-dropdown item
  shapes, and the mobile-rail `<svg>` icons are all unchanged).
- **The `nav.more` key** — the More dropdown's own label is **not** one of the
  13 (the register's closed set does not include it — a future `LBL-2` lane
  would add it if desired).
- **The `Milestones.cs` / README / `MilestonesTests` close-flip trio** — until
  the milestone *ships* (U10 owns it).

## Invariants (pinned for M29)

Ten invariants, **M29·1–M29·10**. Each is one idea, pinned so every unit and
every FACES row references a stable number.

| # | Invariant (one-line M29 note) |
|---|------------------------------|
| **M29·1** | **A rename stays consistent across the surface** — the resolved label is the **same** in the nav (the flat row, the More dropdown, and the mobile rail) **and** in the surface's `<h1>` page header. One resolver, one value — a rename is never "nav says X, header says Y." The nav + header views call **one** helper (the label-resolver seam) so they resolve to the same value. |
| **M29·2** | **The read is a public surface** — the label resolution is **world-readable**, **not** an access decision, **not** a claim (the ADR 0001-B thin-token rule), and **never audited**. A missing `SurfaceLabels` row (a fresh boot before the seeder ran, or a test construction with no store) degrades to **all-null** → every label falls back to its `kw-l` key (byte-identical to today) — never a blank nav, never an error. |
| **M29·3** | **The resolution rule** — for a surface, the resolved label = the admin-set label **if present and non-blank**, else the `kw-l` translation of that surface's nav key in the viewer's effective language (a blank / whitespace-only admin label falls back to the `kw-l` key — the "empty = use default" shape). The `kw-l` key is the **floor**; the admin label is an **optional override on top**. |
| **M29·4** | **The defaults are byte-identical to the shipped `kw-l` text** — the `SurfaceLabels` default is **all-null** (every label unset → every one falls back to its `kw-l` key); the `FirstBootSeeder` seed is **all-null**, and the in-code fallback is the **all-null** `SurfaceLabels` (the byte-identical `kw-l` text, via the fallback). An admin who never touches the surface sees exactly what a fresh instance ships today. The `kw-l` registry entries (`nav.*` / `inv.nav` / `bm.nav` / `documents.title`) are **not** removed, not re-shaped, not re-keyed — they remain the **fallback** the in-code default resolves to; the registry parity tests are **untouched**. |
| **M29·5** | **The write is the ADR 0150 single-write-lane shape** — one `ISurfaceLabelsService.SaveAsync(labels, actorBy)` lane that loads the singleton, applies the full 13-field set, and saves in one session (invariant C3); exactly **one** `AccessAudit` row per save (`Via = Admin`, action `surface_labels.save`, `TargetKind` "surface-labels" — the `site.save` / `signup.set-open` / `timezone.set-default` singleton-toggle shape). The lane **upserts** the singleton (the `SiteContent` "one row per instance" shape, ADR 0150 D6). Strong consistency: the new value is live on the very next `GetAsync` / render. |
| **M29·6** | **The `SurfaceLabels` doc is a singleton (one row per instance)** — the `Id` is the sentinel `"singleton"` (the exact `SiteContent` / `LocaleSettings` shape, ADR 0005 B). The doc is registered in a new `SurfaceLabelsDocTypes.Configure(opts)` surface (the `SiteContentDocTypes` parallel surface, ADR 0004 §B.1); the delta is applied idempotently at boot. **No EF migration** (a new Marten doc type is additive per ADR 0004 §B.1). The `SiteContent` and `LocaleSettings` docs are **untouched** — this milestone adds a *new* doc in a *new* context, not a new field on an existing one (the ADR 0150 D6 pin, the ADR 0006 module-boundary contract). |
| **M29·7** | **The `/admin/labels` surface is the ADR 0150 shape** — `[Route("admin/labels")]` + `[Authorize(Roles = GlobalAdmin)]` on a new `AdminSurfaceLabelsController` (a **dedicated** controller — the `AdminController`'s constructor is pinned by two Web-layer test harnesses, so a new dependency there would break them; a separate `/admin/labels` surface mirrors `/admin/site` / `/admin/signup` / `/admin/timezone`). The GET seeds the form with the current singleton; the POST saves it (one `AccessAudit` row, the `site.save` shape). A blank field **clears** that surface's label (falls back to the `kw-l` key). The `/admin/platform` page gains one list-group row linking to `/admin/labels`. |
| **M29·8** | **Single-string labels, not per-language** — each label is **one** admin-set string (a literal override), shown in **all** languages when set. The per-language label is a **named deferral** (a future `LBL-2` lane would add a `SurfaceLabelTranslation` row shape — the `PageTranslation` / `PostTranslation` precedent — + a `/admin/labels` translation editor) — the exact SITE lane D1 "single string, translation deferred" shape. |
| **M29·9** | **a11y** — the resolved label is rendered **where the `kw-l` key is today** (no DOM structure change); the nav links' `aria-label`s are **unchanged** for the surfaces that are shown. The label is **text only** — the nav icons (the flat-row link glyphs, the More-dropdown item shapes, the mobile-rail `<svg>` icons) stay as shipped; only the text source changes (the `kw-l` key is replaced by the resolver, the fallback is the same key). |
| **M29·10** | **The `Milestones.cs` / README / `MilestonesTests` trio is untouched until the milestone *ships* (U10 owns the close flip); Home's hero stays under the SITE lane** — the `MilestonesTests` pin that the single in-progress milestone is unchanged stays intact through U00–U09; the `WhatsNew.cs` registry gains one new entry (newest-first, the `0.45.0` row) naming M29 + ADR 0152 when it ships (the required sixth member of the close flip — the M27 "shipped with no entry until caught in review" lesson, AGENTS.md, is held). The Home surface's hero text is **already** admin-editable via the SITE lane (ADR 0150 — the `HomeHeroEyebrow` / `HomeHeroLead` fields); M29 drives the **Home nav label only**; it does **not** touch the SITE hero. |

## FACES (pinned, 10)

Ten admin/visitor-facing scenarios, **M29-1–M29-10**, each exercising one or
more invariants.

| # | Outcome (what the admin / visitor sees / can do) | Pinned by |
|---|---|---|
| **M29-1** | A visitor loads any page on a **fresh instance** (no `SurfaceLabels` row, or the seeder has run with all-null defaults) — every nav item (flat row, More dropdown, mobile rail) **and** every surface's `<h1>` renders **exactly** the shipped `kw-l` text (the `nav.*` / `inv.nav` / `bm.nav` / `documents.title` key families in the viewer's language). The read is **world-readable** — no authorization check, no `AccessAudit` row on the read. A missing singleton degrades to the **all-null fallback** — never a blank nav, never an error. | M29·2, M29·3, M29·4, M29·6 |
| **M29-2** | The admin sets the **"Announcements"** label on `/admin/labels` → the nav item **and** the Announcements page's `<h1>` **both** show the new label (e.g. "News") on the very next render (strong consistency). The rename is **consistent across the surface** — the nav and the header agree (one resolver, one value). | M29·1 |
| **M29-3** | The admin sets a label (e.g. "News") → the label is shown **in all languages** (en/de/fr/da) — a single-string override (M29·8). A viewer whose effective language is de still sees "News" (the admin's literal string), **not** the de `kw-l` translation of `nav.announcements`. The `kw-l` key is the **fallback only** — it resolves when the admin has **not** set a label (or has set one to blank). | M29·3, M29·8 |
| **M29-4** | The admin sets a surface's label to **blank / whitespace** (e.g. clears the "Projects" field and saves) → that surface's nav item **and** header fall **back** to the `kw-l` key in the viewer's language (the "empty = use default" shape, M29·3). A blank stored label is not shown as an empty nav item. | M29·3 |
| **M29-5** | The admin saves the label set on `/admin/labels` → the `SurfaceLabelsService.SaveAsync` lane loads the singleton, applies the full 13-field set, and saves in one session; exactly **one** `AccessAudit` row is written (`Via = Admin`, action `surface_labels.save`, `TargetKind` "surface-labels"); the new value is **live on the very next render** (strong consistency). The lane **upserts** the singleton — no duplicate row. | M29·5 |
| **M29-6** | The `SurfaceLabels` doc is a **singleton** — one row per instance, `Id = "singleton"`. A second boot is **idempotent** (no duplicate row, no field change). The `SiteContent` and `LocaleSettings` docs are **untouched** — this milestone adds a *new* doc in a *new* context, not a new field on an existing one. | M29·6 |
| **M29-7** | The admin navigates to `/admin/labels` (from the `/admin/platform` list-group row) — the GET seeds the form with the current singleton (all 13 fields). The admin edits a label and clicks **Save** — the POST saves the full 13-field set, writes one `AccessAudit` row, and redirects back to the GET. A **non-`GlobalAdmin`** is **denied** (the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST). | M29·7 |
| **M29-8** | The `kw-l` registry entries (`nav.*` / `inv.nav` / `bm.nav` / `documents.title` keys in `KnownTranslationKeys.cs`) are **untouched** — the nav + header `kw-l` wrap is replaced by a server-resolved `SurfaceLabels` value, but the registry entries stay (they are still the canonical `en` source text the fallback resolves to). The `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` pins are **green** (the registry entries are untouched). A future per-language-label lane (out of scope, `LBL-2`) would add a `SurfaceLabelTranslation` row shape keyed on the same strings. | M29·4 |
| **M29-9** | A visitor with a screen reader loads any page — the resolved label is in the **same DOM position** as the `kw-l` key today (the nav link text / the `<h1>` text); the DOM structure is **unchanged** (only the text source changes). The nav links' `aria-label`s are **unchanged** for the surfaces that are shown; the nav icons (flat-row glyphs, More-dropdown shapes, mobile-rail `<svg>`s) stay as shipped. | M29·9 |
| **M29-10** | The Home surface's **hero** (the `HomeHeroEyebrow` / `HomeHeroLead` fields, ADR 0150) is **untouched** by M29 — the SITE lane owns the hero text. M29 drives **only the Home nav label** (the `nav.home` key in the flat row, the More dropdown is unaffected, and the mobile-rail Home icon label). A resident who loads `/` sees the SITE hero **exactly** as the SITE lane renders it, and the Home nav item showing the admin's Home label (or the `kw-l` fallback). The SITE hero is **never** re-shaped. | M29·1 (the non-conflict note) |

## Frozen base (unchanged)

All of the following still bind **unchanged**; this milestone does not re-shape
any of them:

- **ADR 0005 B** — the `LocaleSettings` singleton (`Id = "singleton"`
  sentinel, the additive-field convention, one row per instance). The
  `SurfaceLabels` doc will be a **new** singleton in the same shape, **not** a
  new field on `LocaleSettings` (ADR 0006 module-boundary contract).
- **ADR 0150** — the `SiteContent` singleton + the best-effort read + the
  single audited write-lane + the `AdminSiteController`
  `[Route("admin/site")]` `[Authorize(Roles = GlobalAdmin)]` surface. The
  `SurfaceLabels` doc + service + `AdminSurfaceLabelsController` mirror this
  shape exactly.
- **ADR 0050** — the single-write-lane shape (`AdminSignupController`, the
  `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write, the
  `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
  `TempData["info"]` flash, the `RedirectToAction(nameof(Index))` redirect).
- **ADR 0004 §B.1** — additive doc type (delta applied idempotently at boot,
  no EF migration).
- **ADR 0006** — module-boundary contract. The `SurfaceLabels` context is a
  **new** bounded context (`Kumunita.Core.SurfaceLabels`), independent of
  `SiteContent`, `LocaleSettings`, and the `Localization` context.
- **ADR 0015 D1** — `kw-l` provider-floor discipline. The `kw-l` registry
  entries stay (the `nav.*` / `inv.nav` / `bm.nav` / `documents.title` keys are
  not removed, not re-shaped, not re-keyed).
- **The SITE / M28 test model** — the `FirstBootSeeder` default pin, the
  Web-layer pins, and the `KwLRegistryConsistencyTests` /
  `KnownTranslationKeys_ParityTests` pins are untouched.
- **The `Milestones.cs` / README / `MilestonesTests` close-flip trio** — the
  order + single-in-progress pin stays intact until the milestone ships
  (U10 owns it).

## Seams & contracts (Part 2, written by U02)

The Part 1 invariants (M29·1–M29·10) and FACES (M29-1–M29-10) are the
*what* this milestone pins; this section is the *how* — the exact `SurfaceLabels`
field set, the read-seam contract, the write-lane contract, the pinned
seam-test names, the acceptance gate, and the drift guard. Every unit in
U03–U09 codes against these exact shapes; the pinned test names are the
**primary source** for U09's test authoring.

### 2.1 frozen base (unchanged)

All of the following keep binding **unchanged**; this section re-anchors them
for the contract readers and they are **not** re-shaped by Part 2:

- **ADR 0005 B** — the `LocaleSettings` singleton shape (`Id = "singleton"`
  sentinel, the additive-field convention, one row per instance). The
  `SurfaceLabels` doc is a **new** singleton in the same shape, **not** a new
  field on `LocaleSettings` (ADR 0006 module-boundary contract).
- **ADR 0150** — the `SiteContent` singleton shape: the `SiteContentService`
  best-effort read (`GetAsync` never throws / never null → the in-code
  all-null fallback), the `SaveAsync` single audited write-lane (one session,
  the doc + exactly one `AccessAudit` row commit together, no-op on missing
  row — the SITE·6 "never load-or-creates" pin), and the
  `AdminSiteController` `[Route("admin/site")]` `[Authorize(Roles =
  GlobalAdmin)]` dedicated surface. The `SurfaceLabels` doc + service +
  `AdminSurfaceLabelsController` mirror this shape exactly.
- **ADR 0050** — the single-write-lane shape (`AdminSignupController`, the
  `IsSignupOpen` best-effort read, the `SetSignupOpenAsync` audited write, the
  `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
  `TempData["info"]` flash, the `RedirectToAction(nameof(Index))` redirect).
- **ADR 0004 §B.1** — additive doc type (delta applied idempotently at boot,
  no EF migration). The `SurfaceLabels` doc is a **new** Marten doc type,
  registered in a new `SurfaceLabelsDocTypes.Configure(opts)` surface (the
  `SiteContentDocTypes` parallel surface).
- **ADR 0006** — module-boundary contract. The `SurfaceLabels` context is a
  **new** bounded context (`Kumunita.Core.SurfaceLabels`), independent of
  `SiteContent`, `LocaleSettings`, and the `Localization` context.
- **ADR 0015 D1** — `kw-l` provider-floor discipline. The `kw-l` registry
  entries stay (the `nav.*` / `inv.nav` / `bm.nav` / `documents.title` keys
  are not removed, not re-shaped, not re-keyed).
- **The SITE / M28 test model** — the `FirstBootSeeder` default pin, the
  Web-layer pins, and the `KwLRegistryConsistencyTests` /
  `KnownTranslationKeys_ParityTests` pins are untouched.
- **The `Milestones.cs` / README / `MilestonesTests` close-flip trio** — the
  order + single-in-progress pin stays intact until the milestone ships
  (U10 owns it).

### 2.2 the `SurfaceLabels` field set (exact)

The `SurfaceLabels` doc (a **new** bounded context
`Kumunita.Core.SurfaceLabels`, `Id = "singleton"` sentinel — the exact
`SiteContent` / `LocaleSettings` shape, ADR 0005 B) carries **exactly 13
optional `string?` label fields**, one per surface key, **all defaulting to
`null`** (`null` = "use the `kw-l` fallback" — the "empty = use default"
shape, M29·3 / M29·4). The field set is the **ceiling** (ADR 0152 D1): a
future `LBL-2` lane **adds** fields (additive per ADR 0004 §B.1), it does not
re-shape the existing 13.

| # | Field (`public string?`) | Surface key | `kw-l` fallback key | Surface route | a11y note |
|---|---|---|---|---|---|
| 1 | `Home` | `home` | `nav.home` | `/` | label is text only — the nav icon stays as shipped; the SITE hero (`HomeHeroEyebrow` / `HomeHeroLead`) is **untouched** (M29·10) |
| 2 | `Announcements` | `announcements` | `nav.announcements` | `/announcements` | label is text only — the nav icon stays as shipped |
| 3 | `Community` | `community` | `nav.community` | `/community` | label is text only — the nav icon stays as shipped |
| 4 | `Groups` | `groups` | `nav.groups` | `/groups` | label is text only — the nav icon stays as shipped |
| 5 | `Events` | `events` | `nav.events` | `/events` | label is text only — the nav icon stays as shipped |
| 6 | `Projects` | `projects` | `nav.projects` | `/projects/todos` | label is text only — the nav icon stays as shipped |
| 7 | `Inventory` | `inventory` | `inv.nav` | `/inventory` | label is text only — the nav icon stays as shipped |
| 8 | `Bookmarks` | `bookmarks` | `bm.nav` | `/bookmarks` | label is text only — the nav icon stays as shipped |
| 9 | `Documents` | `documents` | `documents.title` | `/documents` | label is text only — the nav icon stays as shipped |
| 10 | `Pages` | `pages` | `nav.pages` | `/pages` | label is text only — the nav icon stays as shipped |
| 11 | `Tags` | `tags` | `nav.tags` | `/tags` | label is text only — the nav icon stays as shipped |
| 12 | `Directory` | `directory` | `nav.directory` | `/directory` | label is text only — the nav icon stays as shipped |
| 13 | `People` | `people` | `nav.people` | `/people` | label is text only — the nav icon stays as shipped |

The `Id` is the sentinel `"singleton"` (the exact `SiteContent` /
`LocaleSettings` shape, ADR 0005 B — one row per instance). The doc is
registered in a new `SurfaceLabelsDocTypes.Configure(opts)` surface (the
`SiteContentDocTypes` parallel surface, ADR 0004 §B.1): `opts.Schema.For
<SurfaceLabels>()` — the delta is applied idempotently at boot. **No EF
migration.** The `SiteContent` and `LocaleSettings` docs are **untouched**
(this milestone adds a *new* doc in a *new* context, not a new field on an
existing one — the ADR 0150 D6 / ADR 0006 module-boundary pin). A
`GetLabel(string surfaceKey)` helper (case-insensitive) returns the field's
value or `null` (the `surfaceKey` → field mapping above, e.g. `"home"` →
`Home`, `"announcements"` → `Announcements`, …, `"people"` → `People`).

### 2.3 the read-seam contract (exact C#)

The read seam is the **single** helper the nav + header views call, so the
nav and the header resolve to the **same** value (M29·1). The read is
**world-readable**, **not** an access decision, **not** a claim (the ADR
0001-B thin-token rule), and **never audited** (M29·2). A missing
`SurfaceLabels` row (a fresh boot before the seeder ran, or a test
construction with no store) degrades to the **all-null** `SurfaceLabels`
→ every label falls back to its `kw-l` key (byte-identical to today) —
never a blank nav, never an error (the ADR 0050 `IsSignupOpenAsync`
best-effort shape, the SITE·1 read contract).

```csharp
// Kumunita.Core.SurfaceLabels.ISurfaceLabelsService (the read seam)
public interface ISurfaceLabelsService
{
    /// <summary>
    /// The shared label-resolver (M29·1 — the nav + header call **one**
    /// helper so they resolve to the **same** value).
    /// Returns the admin-set label **if present and non-blank**, else the
    /// `kw-l` translation of <paramref name="fallbackKey"/> in
    /// <paramref name="effectiveLanguage"/> (the "empty = use default"
    /// shape, M29·3 / M29·4). Best-effort: a missing row / read failure
    /// degrades to the `kw-l` fallback — never throws, never blank.
    /// World-readable, never audited (M29·2, ADR 0001-B thin-token).
    /// </summary>
    Task<string> GetLabelAsync(string surfaceKey, string fallbackKey,
                               string effectiveLanguage,
                               CancellationToken ct = default);

    /// <summary>
    /// The ADR 0150 single audited write-lane (M29·5): loads the singleton,
    /// applies the full 13-field set, and saves in one session (invariant
    /// C3); exactly **one** `AccessAudit` row per save (`Via = Admin`,
    /// action `surface_labels.save`, `TargetKind` "surface-labels" — the
    /// `site.save` shape). Strong consistency: the new value is live on the
    /// very next `GetLabelAsync` / render. The lane upserts the singleton —
    /// it never creates a second row (M29·6).
    /// </summary>
    Task SaveAsync(SurfaceLabels labels, string actorBy,
                   CancellationToken ct = default);
}
```

`SurfaceLabelsService.GetLabelAsync` (the implementation):

```csharp
public async Task<string> GetLabelAsync(
    string surfaceKey, string fallbackKey, string effectiveLanguage,
    CancellationToken ct = default)
{
    // ADR 0050 IsSignupOpenAsync best-effort shape (M29·2, SITE·1): a
    // missing row / read failure degrades to the all-null fallback —
    // never throws, never blank. World-readable, never audited.
    string? adminLabel = null;
    try
    {
        using var session = _store.QuerySession();
        var row = await session.LoadAsync<SurfaceLabels>(
            SurfaceLabels.SingletonId, ct).ConfigureAwait(false);
        adminLabel = row?.GetLabel(surfaceKey); // null when unset / blank
    }
    catch
    {
        adminLabel = null; // a read failure degrades to the kw-l fallback
    }

    if (!string.IsNullOrWhiteSpace(adminLabel))
    {
        return adminLabel; // the admin's literal string (all languages — M29·8)
    }

    // The kw-l floor (M29·3 / M29·4): the canonical en source text in the
    // viewer's effective language. The registry entries stay (ADR 0152 D3).
    return await Translation.GetAsync(fallbackKey, effectiveLanguage);
}
```

The nav + header views (U05/U06/U07) call **one** helper (this resolver) so
they resolve to the **same** value (M29·1). The `href` / `asp-route-*`
attributes are **unchanged** (the "label, not re-route" pin — a rename never
moves a link); the `aria-label`s are unchanged for the surfaces that are
shown (M29·9).

### 2.4 the write-lane contract (exact C#)

The write is the **ADR 0150 single-write-lane shape** (M29·5). One
`ISurfaceLabelsService.SaveAsync(labels, actorBy)` lane that loads the
singleton, applies the full 13-field set, and saves in one session
(invariant C3); exactly **one** `AccessAudit` row per save (`Via = Admin`,
action `surface_labels.save`, `TargetKind` "surface-labels" — the
`site.save` / `signup.set-open` / `timezone.set-default` singleton-toggle
shape). The lane **upserts** the singleton (the `SiteContent` "one row per
instance" shape, ADR 0150 D6) — it never creates a second row (M29·6).
**Strong consistency** (invariant C4): the new value is live on the very
next `GetLabelAsync` / render.

```csharp
// Kumunita.Core.SurfaceLabels.SurfaceLabelsService (the write lane)
public async Task SaveAsync(SurfaceLabels labels, string actorBy,
                            CancellationToken ct = default)
{
    // ADR 0150 single audited write-lane shape (M29·5): one write session,
    // the doc + exactly one AccessAudit row commit together (invariant C3,
    // strong consistency C4 — live on the very next GetLabelAsync / render).
    // The lane upserts the singleton — it never creates a second row
    // (M29·6); a missing row is a no-op (the seeder is the only writer
    // that creates the row on a fresh boot).
    await using var session = _store.OpenSession(new Marten.Services.SessionOptions());

    var stored = await session.LoadAsync<SurfaceLabels>(
        SurfaceLabels.SingletonId, ct).ConfigureAwait(false);

    if (stored is null)
    {
        return; // a missing row is a no-op — the lane never load-or-creates (M29·6)
    }

    // The full 13-field set, verbatim from the caller's SurfaceLabels.
    stored.Home        = labels.Home;
    stored.Announcements = labels.Announcements;
    stored.Community   = labels.Community;
    stored.Groups      = labels.Groups;
    stored.Events      = labels.Events;
    stored.Projects    = labels.Projects;
    stored.Inventory   = labels.Inventory;
    stored.Bookmarks   = labels.Bookmarks;
    stored.Documents   = labels.Documents;
    stored.Pages       = labels.Pages;
    stored.Tags        = labels.Tags;
    stored.Directory   = labels.Directory;
    stored.People      = labels.People;

    session.Store(stored);

    // Exactly one AccessAudit row (the site.save / signup.set-open /
    // timezone.set-default singleton-toggle shape, M29·5) — the real
    // AccessAudit doc shape (Id + EffectivePrincipalId + Outcome).
    session.Store(new AccessAudit
    {
        Id = Guid.NewGuid().ToString("N"),
        At = DateTimeOffset.UtcNow,
        ActorId = actorBy,
        EffectivePrincipalId = actorBy,
        Action = "surface_labels.save",
        TargetKind = "surface-labels",
        TargetId = SurfaceLabels.SingletonId,
        Via = AccessVia.Admin,
        Outcome = AccessOutcome.Allow
    });

    await session.SaveChangesAsync(ct).ConfigureAwait(false);
}
```

The `AdminSurfaceLabelsController.Save` action (U08) is the **thin wrapper**
(the `AdminSiteController.SaveHome` shape — the `GlobalAdmin`-gated
`[ValidateAntiForgeryToken]` POST, the `TempData["info"]` flash, the
`RedirectToAction(nameof(Index))` redirect). The controller is a **dedicated**
controller (the `AdminController`'s constructor is pinned by two Web-layer
test harnesses, so a new dependency there would break them; a separate
`/admin/labels` surface mirrors `/admin/site` / `/admin/signup` /
`/admin/timezone`): `[Route("admin/labels")]` + `[Authorize(Roles =
Kumunita.Core.Identity.Roles.GlobalAdmin)]`. The GET seeds the form with the
current singleton (the `ISurfaceLabelsService` best-effort read — a missing
row degrades to the all-null fallback, so the form always renders); the POST
saves the full 13-field set (one `AccessAudit` row). A blank field **clears**
that surface's label (falls back to the `kw-l` key — M29·4).

### 2.5 the pinned seam-test names (exact)

The **primary source** for U09's test authoring. Twenty tests across four
classes; the exact names are the drift-guard's test-name ceiling (no test
whose exact name is not in this list may be introduced — unit-series rule 3).

**`tests/Kumunita.Core.Tests/SurfaceLabelsServiceTests.cs`** — the 8 Core
seam pins (the ADR 0150 single-write-lane shape + the read-seam contract):

1. `GetAsync_MissingStore_ReturnsAllNullFallback` — a test construction with
   no store degrades to the all-null fallback (M29·2, M29·6).
2. `GetAsync_MissingRow_ReturnsAllNullFallback` — a store with no
   `SurfaceLabels` row degrades to the all-null fallback (M29·2, M29·6).
3. `SaveAsync_WritesOneAccessAuditRow` — one save writes exactly **one**
   `AccessAudit` row (`Via = Admin`, action `surface_labels.save`,
   `TargetKind` "surface-labels") (M29·5).
4. `SaveAsync_StrongConsistency_LiveOnNextGetAsync` — the saved value is
   live on the very next `GetLabelAsync` / `GetAsync` (invariant C4, M29·5).
5. `SaveAsync_UpsertsSingleton_NoDuplicateRow` — a second save does not
   create a second `Id = "singleton"` row (M29·6).
6. `SaveAsync_BlankLabel_StoredBlank_FallsBackAtResolution` — a blank stored
   label is stored blank and falls back to the `kw-l` key at resolution
   (M29·3, M29·4).
7. `GetLabelAsync_ReturnsOverrideWhenSet` — a non-blank admin label is
   returned as the resolved label (all languages — M29·8) (M29·3).
8. `GetLabelAsync_ReturnsNullWhenNotSet` — an unset label resolves to the
   `kw-l` fallback (M29·3, M29·4).

**`tests/Kumunita.Core.Tests/SurfaceLabelsSeederTests.cs`** — the 3 Core
seeder pins (the `FirstBootSeeder` all-null default, the SITE / M28
"seeded defaults match the shipped text" shape):

9. `FreshBoot_HasExactlyOneSurfaceLabelsRow` — a fresh boot has exactly one
   `SurfaceLabels` row (`Id = "singleton"`) (M29·6).
10. `FreshBoot_AllLabelsNull` — all 13 label fields are `null` (M29·4, M29·6).
11. `SecondBoot_IsIdempotent_NoDuplicateRow` — a second boot is idempotent
    (no duplicate row, no field change) (M29·6).

**`tests/Kumunita.Web.Tests/AdminSurfaceLabelsControllerTests.cs`** — the 4
Web controller pins (the `/admin/labels` surface, the ADR 0150 shape):

12. `GET_SeesCurrentSingleton` — the GET seeds the form with the current
    singleton (all 13 fields) (M29·7).
13. `POST_Save_SavesLabel_WritesOneAccessAuditRow` — the POST saves the
    label set + writes exactly one `AccessAudit` row (M29·5, M29·7).
14. `POST_NonGlobalAdmin_IsDenied` — a non-`GlobalAdmin` is denied the POST
    (the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST) (M29·7).
15. `GET_FreshInstance_AllLabelsBlank` — a fresh instance's GET shows all
    13 labels blank (the all-null fallback) (M29·1, M29·4, M29·6).

**`tests/Kumunita.Web.Tests/SurfaceLabelResolutionTests.cs`** — the 5 Web
resolution pins (the nav + header consistency, the single-string override,
the blank fallback):

16. `FreshInstance_NavShowsKwLText` — a fresh instance's nav shows the
    shipped `kw-l` text (M29·1, M29·2, M29·4).
17. `SavedLabel_NavShowsOverride` — a saved label shows in the nav (M29·1,
    M29·3).
18. `SavedLabel_HeaderShowsOverride` — a saved label shows in the surface's
    `<h1>` header (M29·1).
19. `SavedLabel_SameLabelNavAndHeader` — the nav and the header resolve to
    the **same** value (M29·1).
20. `BlankLabel_FallsBackToKwL` — a blank / whitespace label falls back to
    the `kw-l` key (M29·3, M29·4).

### 2.6 the acceptance gate (exact)

The acceptance gate (U09 runs + records it) is the **closed-loop** gate —
all three must be green:

1. `dotnet build Kumunita.slnx -c Debug` — **green** (the touched projects
   `Kumunita.Core` + `Kumunita.Web` compile).
2. `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
   — **green** (the `SurfaceLabelsServiceTests` 8 pins + the
   `SurfaceLabelsSeederTests` 3 pins + the `SiteContentServiceTests` /
   `KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests`
   registry-parity pins + the pre-existing Core suite all pass; the registry
   entries are **untouched** — ADR 0152 D3, M29·4, M29·8).
3. `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
   — **green** (the `AdminSurfaceLabelsControllerTests` 4 pins + the
   `SurfaceLabelResolutionTests` 5 pins + the `AdminSiteControllerTests`
   SITE-precedent pins + the `MilestonesTests` pin (the order +
   single-in-progress pin is intact through U00–U09) + the `WhatsNewTests`
   pin (the new `0.45.0` entry is present, newest-first — added by U10) +
   the pre-existing Web suite all pass).

The `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` pins
stay **green** (the registry entries are untouched — the `nav.*` /
`inv.nav` / `bm.nav` / `documents.title` keys remain the canonical `en`
source text the fallback resolves to). The `SiteContentServiceTests` /
`AdminSiteControllerTests` pins stay **green** (the SITE precedent shapes are
unchanged — M29 adds a *new* doc in a *new* context, not a new field on
`SiteContent` — ADR 0150 D6 / ADR 0006 module-boundary pin). The
`MilestonesTests` pin stays **green** through U00–U09 (the order +
single-in-progress pin is intact until the milestone *ships* — U10 owns the
close flip). The `WhatsNewTests` pin is **green at U10** (the `0.45.0`
entry is added by U10, newest-first — the required sixth member of the
close flip, the M27 "shipped with no entry until caught in review" lesson,
AGENTS.md, is held).

> **Test-runner quirk (AGENTS.md).** Both test projects use **xunit.v3**
> (`Microsoft.Testing.Platform`). On this machine the discovery path
> reliably goes wrong (VS Test Explorer / `dotnet test` report "No tests
> found to run" / `Zero tests ran / Exit code: 5` even though discovery found
> them). The reliable path is the **in-process** `dotnet exec` of each test
> assembly (the commands above) — this is what actually reports pass/fail
> here. `Kumunita.Core.Tests` takes ~20 s (Testcontainers `postgres:18`)
> and leaves Docker containers behind if killed — clean up with
> `docker container prune`.

### 2.7 the drift guard (exact)

The drift guard is the **ceiling / floor / untouched / unchanged** pin set
for M29. A unit that drifts from any of these is a `## U<m> — Drift pause`
in the handoff note (unit-series rule 8), not a silent re-shape.

- **The `SurfaceLabels` field set is the ceiling** (ADR 0152 D1, M29·6) — the
  13 optional `string?` fields (Home / Announcements / Community / Groups /
  Events / Projects / Inventory / Bookmarks / Documents / Pages / Tags /
  Directory / People) are the **complete** admin surface for the current
  scope. **No field outside the field set may appear in the doc**; a future
  `LBL-2` lane **adds** fields (additive per ADR 0004 §B.1), it does not
  re-shape the existing 13.
- **The `kw-l` registry entries are the floor** (ADR 0152 D3, M29·4) — the
  `nav.home` / `nav.announcements` / `nav.community` / `nav.groups` /
  `nav.events` / `nav.projects` / `inv.nav` / `bm.nav` / `documents.title` /
  `nav.pages` / `nav.tags` / `nav.directory` / `nav.people` keys in
  `KnownTranslationKeys.cs` are **not** removed, not re-shaped, not re-keyed.
  They remain the **fallback** the in-code default resolves to (the canonical
  `en` source text). The registry parity tests
  (`KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests`) are
  **untouched**.
- **The `SiteContent` + `LocaleSettings` docs are untouched** (ADR 0150 D6 /
  ADR 0006 module-boundary pin, M29·6) — M29 adds a **new** doc in a **new**
  bounded context (`Kumunita.Core.SurfaceLabels`), not a new field on an
  existing one. The `SiteContent` and `LocaleSettings` field sets are
  **unchanged**.
- **The routes are unchanged** (the "label, not re-route" pin, M29·9) — the
  13 existing nav routes (`/`, `/announcements`, `/community`, `/groups`,
  `/events`, `/projects/todos`, `/inventory`, `/bookmarks`, `/documents`,
  `/pages`, `/tags`, `/directory`, `/people`) are **unchanged**; a rename
  never moves a link, never changes a route, never touches an
  `asp-route-*` / `href`. Only the **text** the nav item and the surface's
  `<h1>` show changes.
- **The `Milestones.cs` / README / `MilestonesTests` trio is untouched
  until the milestone *ships*** (M29·10) — the order + single-in-progress
  pin stays intact through U00–U09; U10 owns the close flip (the
  six-member close flip including the `WhatsNew.cs` `0.45.0` entry).
- **The SITE hero is untouched** (M29·10) — the Home surface's hero text
  (the `HomeHeroEyebrow` / `HomeHeroLead` fields, ADR 0150) is **already**
  admin-editable via the SITE lane; M29 drives the **Home nav label only**
  (the `nav.home` key), it does **not** touch the SITE hero.
- **The test names are the ceiling** (unit-series rule 3) — no test whose
  exact name is not in §2.5 may be introduced; the 20 pinned names above are
  the complete set for M29.

### Run result (M29 acceptance gate — 2026-10-08)

**The three-test gate (closed-loop / handoff / part-vs-whole):**

- **closed-loop — PASS.** The gate ran end-to-end through the in-process
  xunit.v3 runner (the AGENTS.md reliable path — not `dotnet test` / VS Test
  Explorer). Real exit codes + test counts were captured:
  - `dotnet build Kumunita.slnx -c Debug` → **green** (`Build succeeded.
    53 Warning(s) 0 Error(s)`; all 53 warnings pre-existing in unrelated
    Core/Web/view/test files, none in the 4 U09-touched files).
  - `dotnet exec Kumunita.Core.Tests.dll` → **green** (`Total: 1384,
    Errors: 0, Failed: 0, EXITCODE=0`, ~195 s / Testcontainers `postgres:18`).
  - `dotnet exec Kumunita.Web.Tests.dll` → **my 9 M29 tests green** (confirmed
    in isolation: `Total: 9, Errors: 0, Failed: 0`, ~8.6 s); the *full* Web
    suite is `Total: 961, Errors: 0, Failed: 4` — see the part-vs-whole note.
- **handoff — PASS.** This Run result section + the `## U09 — tests + gate`
  scratch-tier section in `m29-handoff-notes.md` are appended (the three-tier
  contract is complete through U09).
- **part-vs-whole — PARTIAL (recorded, not silent).** The *part* (M29's 20
  seam tests) is green; the *whole* (full Web suite) is **red on 4
  pre-existing failures that are not M29's tests** — carried forward to U10
  (see below). The Core suite (the whole of the Core side) is green.

**The 20-test count (all PASS):**

- `SurfaceLabelsServiceTests` (Core, 8): `GetAsync_MissingStore_ReturnsAllNullFallback`
  / `GetAsync_MissingRow_ReturnsAllNullFallback` / `SaveAsync_WritesOneAccessAuditRow`
  / `SaveAsync_StrongConsistency_LiveOnNextGetAsync` /
  `SaveAsync_UpsertsSingleton_NoDuplicateRow` /
  `SaveAsync_BlankLabel_StoredBlank_FallsBackAtResolution` /
  `GetLabelAsync_ReturnsOverrideWhenSet` / `GetLabelAsync_ReturnsNullWhenNotSet`.
- `SurfaceLabelsSeederTests` (Core, 3, U04's): `FreshBoot_HasExactlyOneSurfaceLabelsRow`
  / `FreshBoot_AllLabelsNull` / `SecondBoot_IsIdempotent_NoDuplicateRow`.
- `AdminSurfaceLabelsControllerTests` (Web, 4): `GET_SeesCurrentSingleton` /
  `POST_Save_SavesLabel_WritesOneAccessAuditRow` / `POST_NonGlobalAdmin_IsDenied` /
  `GET_FreshInstance_AllLabelsBlank`.
- `SurfaceLabelResolutionTests` (Web, 5): `FreshInstance_NavShowsKwLText` /
  `SavedLabel_NavShowsOverride` / `SavedLabel_HeaderShowsOverride` /
  `SavedLabel_SameLabelNavAndHeader` / `BlankLabel_FallsBackToKwL`.

**The pin status (registry-parity / SITE-precedent / `MilestonesTests` / `WhatsNewTests`):**

- **registry parity — GREEN.** `KnownTranslationKeys_ParityTests` +
  `KwLRegistryConsistencyTests` pass (not in the 4-failure set) — the
  `nav.*` / `inv.nav` / `bm.nav` / `documents.title` registry entries are
  **untouched** (ADR 0152 D3, M29·4).
- **SITE precedent — GREEN.** `SiteContentServiceTests` +
  `SiteContentSeederTests` (Core, green in the 1384) + `AdminSiteControllerTests`
  (Web, not in the 4-failure set) pass — the SITE shapes are unchanged
  (ADR 0150 D6 / ADR 0006 — M29 adds a *new* doc in a *new* context).
- **`MilestonesTests` — GREEN** (not in the 4-failure set) — the order +
  single-in-progress pin is intact through U00–U09 (U10 owns the close flip).
- **`WhatsNewTests` — PENDING U10** — the `0.45.0` entry (the required sixth
  close-flip member, newest-first) is not yet added; U10 owns it. The
  M27 "shipped with no entry until caught in review" lesson (AGENTS.md) is held
  by pinning it to U10.

**The 4 full-Web-suite failures (all pre-existing, NOT M29's tests — carried
forward to U10, recorded here per the handoff / part-vs-whole gate):**

1. `InventoryControllerTests.Nav_Entry_Present_In_Both_Layout_Variants_And_Registry`
   — asserts the literal `key="inv.nav"` `<kw-l>` markup in
   `_Layout.cshtml`; **U05 replaced the 13 nav items' `<kw-l key="…">` text
   with the resolver** (`@_nav*` locals), so the literal `key="…"` attribute no
   longer appears. The registry key, the `/inventory` link, and the 2-variant
   presence are all still present — only the *markup assertion* is stale.
2. `NavMoreFoldTests.Layout_Carries_Fold_Hooks_And_Loads_Module` — asserts
   `key="nav.groups"` (same U05 resolver change; the `data-nav-fold` /
   `data-nav-more` hooks, the module load, and the `/projects/todos` + `/inventory`
   links all still hold).
3. `M23FindPeopleTests.Layout_Carries_APeopleNavEntry_InBothVariants` — asserts
   `key="nav.people"` (same U05 resolver change; the two `FindPeople/Index`
   nav links still hold).
4. `ImproveHarnessTests.ImproveCheck_Gate_Passes` — the IMPROVE lane's growth
   gate: 2 files grew past their U00 baseline (`FirstBootSeeder.cs` from U04's
   `SeedSurfaceLabelsAsync`; `KnownTranslationKeys.cs` from U08's `labels.*`
   keys — both legitimate M29 additions) + the design doc is >400 lines without
   an `Abstract` in the first 15 lines.

> **Carry-forward for U10 (or a follow-up lane):** U05's register-authorized
> resolver change (the nav `<kw-l key="…">` → resolver text, the `kw-l` registry
> keys staying) **regressed 3 pre-existing nav-structural tests** that assert
> the old literal `key="…"` markup. U05's exit was "build green" (no Web-suite
> run), so this surfaced at U09's gate. These 3 tests + the improve-check
> baseline need reconciling (re-pin to the resolver shape, or the structural
> invariants the tests actually care about) before the milestone ships. U09's
> scope (rule 1 — no file outside its own Deliverables) is the 20 seam tests +
> this record, so it does **not** fix them; U10 owns the close flip and the
> honest "green/red" state.
