# ADR 0150 — Site content customization (the two landing surfaces' hero text is admin-editable + the sections are show/hide)

Status: Accepted
Date: 2026-10-07

## Context

The platform's two **landing surfaces** — `/` (the home,
`Views/Home/Index.cshtml`) and `/about` (the about page,
`Views/StaticPages/About.cshtml`) — are the **first content a visitor
sees**, and they are **hard-coded**: the hero eyebrow, the hero lead text,
and the *presence* of every secondary section (the feature cards, the
scope band, the FIG-philosophy band, the code/docs links, the "The plan"
roadmap, the "What's new" changelog, the contact CTA) are all baked into
the Razor views as `kw-l`-wrapped English strings (the `home.*` /
`about.*` / `platform.*` key families in `KnownTranslationKeys.cs`).

An admin who wants to say "this is a home for one club, not a
neighbourhood," or wants to drop the FIG-philosophy band on a deployment
where it feels like marketing, or wants the hero to say "Welcome to
Maplewood" instead of the generic pitch, has **no path** — the only way to
change what those surfaces say, or which sections appear at all, is a code
change to the view.

The `SP` lane (ADR 0043) shipped the four static pages (terms / help /
privacy / conduct) as admin-editable `Page` docs. The `LocaleSettings`
singleton (ADR 0005 B) carries the admin-settled instance values (default
language / timezone / date format / sign-up gate / messaging). But the two
**landing surfaces** are still fixed — the admin can configure how the
platform *behaves* and edit the static pages, but not the *landing
story*.

## Decision

- A new **platform-level singleton** `SiteContent` (a **new bounded
  context** `Kumunita.Core.SiteContent`, a new `SiteContentDocTypes`
  surface, a new `ISiteContentService` seam) carries **exactly 13
  fields**: the 4 text fields (`HomeHeroEyebrow` / `HomeHeroLead` /
  `AboutHeroEyebrow` / `AboutHeroLead`) and the 9 section-toggle fields
  (`HomeShowAboutButton` / `HomeShowFeatures` / `HomeShowRoadmap` /
  `AboutShowFeatures` / `AboutShowScope` / `AboutShowPhilosophy` /
  `AboutShowProject` / `AboutShowWhatsNew` / `AboutShowContactCta`). The
  `Id` is the sentinel `"singleton"` (the exact `LocaleSettings` shape,
  ADR 0005 B — one row per instance). The field set is the **ceiling**
  (D1): a future SITE-2 lane **adds** fields (additive per ADR 0004 §B.1),
  it does **not** re-shape the existing 13.
- **The read is a public landing surface** (D2). `/` and `/about` are
  world-readable (the static-page contract — the seeded canonical pages
  are public, `Page.Audience = null`, the ADR 0043 standing matrix). The
  `SiteContent` singleton is read by the same two controllers
  (`HomeController.Index` / `StaticPagesController.About`) and is **not**
  an access decision, **not** a claim (the thin-token rule, ADR 0001-B),
  and is read fresh per request (a save is live on the very next render —
  the ADR 0050 `IsSignupOpen` strong-consistency shape). A missing
  singleton (a fresh boot before the seeder ran, or a test construction
  with no store) degrades to the **shipped defaults** (the byte-identical
  `kw-l` text + every section shown) — never a blank page, never an error.
- **The write is the ADR 0050 single-write-lane shape** (D3). One
  `ISiteContentService.SaveAsync(content, actorBy)` lane that loads the
  singleton, applies the field set, and saves in one session (invariant
  C3); exactly **one** `AccessAudit` row per save (`Via = Admin`, action
  `site.save`, `TargetKind` "site" — the `signup.set-open` /
  `timezone.set-default` / `dateformat.set-default` singleton-toggle
  shape). The lane **upserts** the singleton — it never creates a second
  row (D6). Strong consistency (invariant C4): the new value is live on
  the very next `GetAsync` / render.
- **The defaults are byte-identical to the shipped `kw-l` text** (D4). The
  `SiteContent` default values (the `FirstBootSeeder` seed + the in-code
  fallback) are the **exact** `home.intro_eyebrow` / `home.intro_lead` /
  `about.eyebrow` / `about.lead` English strings from
  `KnownTranslationKeys.cs` (the `en` source text), and every
  section-toggle default is `true` (every section shown). An admin who
  never touches the surface sees exactly what a fresh instance ships
  today — the "keep it as is by default" requirement.
- **The `kw-l` registry entries stay** (D5). The `home.*` / `about.*` /
  `platform.*` keys in `KnownTranslationKeys.cs` are **not** removed, not
  re-shaped, not re-keyed — they remain the **fallback** the
  `SiteContent` default values are derived from (the seeder reads the
  registry at seed time; the in-code fallback is the same text, hard-coded
  in `SiteContent`'s field defaults so a missing singleton degrades to
  the shipped text). The registry parity tests
  (`KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests`)
  are **untouched**. A future SITE-2 translation lane (out of scope) would
  add a `SiteContentTranslation` row shape keyed on the same strings; the
  ADR 0005 §B "every other language is community-provided" clause is
  unchanged.
- **The ADR 0149 `Profile.HideHomeIntro` per-resident preference is
  composable, not replaced** (D6). The per-resident "hide the intro"
  checkbox (ADR 0149) and the platform-level "show the intro" flag
  (this ADR) are **independent**: the per-resident preference is a
  `Profile` field (ADR 0019 / ADR 0020 owner-scope shape, read in
  `HomeController.Index` and passed through `HomeViewModel.HideIntro`);
  the platform-level flag is a `SiteContent` field (read in the same
  `HomeController.Index` and passed through a new
  `HomeViewModel.ShowHomeIntro` field). The view's two intro sections
  render when **both** are true (the per-resident preference is the
  *resident's* choice to hide; the platform flag is the *admin's* choice
  to show — both must agree for the sections to appear). The "What's new"
  feed and the roadmap section are governed **only** by the platform flag
  (the per-resident preference never reaches them — the ADR 0149 pin is
  unchanged).
- **The `SiteContent` doc is a singleton (one row per instance)** (D7).
  The `Id` is the sentinel `"singleton"` (the exact `LocaleSettings`
  shape, ADR 0005 B). The doc is registered in a new
  `SiteContentDocTypes.Configure(opts)` surface (the `PageDocTypes` /
  `MediaDocTypes` parallel surface, ADR 0004 §B.1); the delta is applied
  idempotently at boot (the M1/M3/Media dev-only loop / versioned-boot
  path). **No EF migration** (a new Marten doc type is additive per ADR
  0004 §B.1; the existing `LocaleSettings` doc is **not** modified — this
  lane adds a *new* doc, not a new field on the existing one, because the
  landing-surface content is a distinct bounded concern from the locale
  settings, and the ADR 0006 module-boundary contract keeps the two
  contexts independent).
- **The `/admin/site` surface is the ADR 0050 shape** (D8). A
  **dedicated** `AdminSiteController` (the `AdminController`'s
  constructor is pinned by two Web-layer test harnesses
  (`AdminControllerBlockTests` / `AdminControllerMandatoryTests`), so a
  new dependency there would break them; a separate `/admin/site` surface
  mirrors `/admin/signup` / `/admin/timezone` / `/admin/dateformat` — the
  codebase already puts the platform-settled instance values on their own
  controllers). `[Route("admin/site")]` + `[Authorize(Roles = GlobalAdmin)]`
  + two form sections (Home + About), each with its own field group + save
  button (the `AdminSignupController`'s two-form shape — the `isOpen`
  form + the `notify` form, each with its own POST action). The GET seeds
  the form with the current singleton (the ADR 0050
  `IsSignupOpenAsync` best-effort shape); the POST saves it (one
  `AccessAudit` row per save, the `signup.set-open` shape, the
  `TempData["info"]` flash, the `RedirectToAction(nameof(Index))`
  redirect). The `/admin/platform` page gains one list-group row linking
  to `/admin/site` (the `SP U03` / `ADR 0050` discoverability pattern).
- **The `Milestones.cs` / README / `MilestonesTests` trio is untouched
  until the lane *ships*** (D9). The `MilestonesTests` pin that **M4** is
  the single `StatusNext` stays intact. When the lane ships (the close
  unit), the `Milestones.cs` registry gains one new entry (the `SITE`
  lane row, `StatusDone`), the README Roadmap gains one new row, and the
  `WhatsNew.cs` registry gains one new entry (newest-first, the `0.42.0`
  row) naming the lane + this ADR — the M27 "shipped with no entry until
  caught in review" lesson (AGENTS.md) is held.

## Consequences

- The two landing surfaces (`/` and `/about`) are now **admin-editable**
  — the admin can change the two heroes' eyebrow + lead text, and show or
  hide every other section, without a code change. A fresh instance that
  never touches the surface looks **exactly** the same as it does today
  (the defaults are byte-identical to the shipped `kw-l` text, every
  section shown).
- The `/admin/site` surface is the GlobalAdmin's edit page — a dedicated
  `AdminSiteController` in the ADR 0050 shape, with one `AccessAudit` row
  per save (the `signup.set-open` / `timezone.set-default` /
  `dateformat.set-default` singleton-toggle shape). The `/admin/platform`
  page gains one list-group row linking to it.
- The `kw-l` registry entries (`home.*` / `about.*` / `platform.*` keys
  in `KnownTranslationKeys.cs`) are **untouched** — they remain the
  canonical `en` source text the defaults are derived from, and a future
  SITE-2 translation lane (out of scope) would add a
  `SiteContentTranslation` row shape keyed on the same strings.
- The ADR 0149 `Profile.HideHomeIntro` per-resident preference is
  **composable, not replaced** — the two intro sections render when
  **both** the per-resident preference and the platform-level flag
  agree; the "What's new" feed and the roadmap section are governed
  **only** by the platform flag (the per-resident preference never
  reaches them).
- The `LocaleSettings` singleton is **untouched** — this lane adds a
  *new* doc, not a new field on the existing one (the ADR 0006
  module-boundary contract keeps the two contexts independent).
- The `SiteContent` field set is **additive** per ADR 0004 §B.1 — a
  future SITE-2 lane (out of scope) could add per-section editable text
  (the feature cards' titles / bodies, the scope band's body, the
  FIG-philosophy band's body, the code/docs band's lead), a
  `SiteContentTranslation` row shape for machine translation of the
  landing-surface copy, and per-section custom CTA buttons (an editable
  URL + label) — without re-shaping the existing 13 fields.
- The `Milestones.cs` / README Roadmap / `MilestonesTests.cs` trio is
  **untouched** until the lane *ships* (the close unit owns the flip);
  the `WhatsNew.cs` registry gains one new entry (the `0.42.0` row,
  newest-first) naming the lane + this ADR.
- **No EF migration** (a new Marten doc type is additive per ADR 0004
  §B.1; the delta is applied idempotently at boot). **No new route for
  the read** (the existing `/` and `/about` routes are unchanged — only
  what they render changes). **One new route for the write**:
  `/admin/site` (the GlobalAdmin's edit surface, mirroring
  `/admin/signup` / `/admin/timezone` / `/admin/dateformat`). **No new
  `AccessAction` / `AccessVia` / authorization path** (the read is a
  public landing surface; the write is the ADR 0050 `GlobalAdmin`-gated
  single-write-lane shape, one `AccessAudit` row per save).
- **M4/M5/M6 stay Events / Projects / Portability** — no roadmap letter
  moves.
