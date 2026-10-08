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
