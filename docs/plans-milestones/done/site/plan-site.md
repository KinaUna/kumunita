# Site content customization (`SITE`) — the `/home` + `/about` landing surfaces become admin-editable

> **Planned.** This is the **lane register** (secondary tier of the lane's
> three-tier contract) for a new named lane **`SITE`** — the platform's two
> *landing* surfaces (`/home` = `Views/Home/Index`, `/about` =
> `Views/StaticPages/About`) are currently **hard-coded**: the hero eyebrow,
> the hero lead text, and every secondary section ("What it is & how it
> works" button, the three feature cards, the scope band, the FIG-philosophy
> band, the code/docs links, and the "The plan" roadmap section) are all
> baked into the Razor views as `kw-l`-wrapped English strings. An admin
> who wants to change **what** those surfaces say — or **whether** a given
> section appears at all — has no path: they can edit the terms/help/
> privacy/conduct pages (the `SP` lane, ADR 0043) and the platform's
> language / timezone / date-format / sign-up / messaging settings (the
> `LocaleSettings` singleton, ADR 0005 B / 0019 / 0020 / 0050 / 0105),
> but the *landing story* is fixed. This lane adds a single
> **platform-level `SiteContent` singleton** (a new doc in a new
> `Kumunita.Core.SiteContent` context) with the per-section fields an
> admin actually needs: **editable text** for the two heroes' eyebrow +
> lead, and **show / hide** flags for every other section of both
> surfaces. The defaults are **byte-identical to the shipped `kw-l`
> text** — an admin who never touches the surface sees exactly what a
> fresh instance ships today (the `LS` "code-owned `en`" baseline
> pattern, ADR 0042 D1, applied to the landing surface).
>
> **What this is:** a *data + admin surface + conditional rendering* lane.
> **U00** verifies the surface and the ADR 0149 / ADR 0050 precedent
> shapes. **U01/U02** author the primary-tier design doc (invariants +
> FACES + the exact `SiteContent` field list + the pinned test names +
> the acceptance gate + the drift guard) and draft **ADR 0150** (the
> next free number after 0149). **U03** implements the Core
> (`SiteContent` doc + `ISiteContentService` + `SiteContentService` +
> `SiteContentDocTypes` + the DI registration). **U04** adds the
> `FirstBootSeeder` default (the exact shipped text, byte-identical) and
> the "seeded defaults match the shipped `kw-l` text" pin. **U05/U06**
> wire `HomeController` + `Views/Home/Index.cshtml` to the singleton
> (the hero text swap + the three section toggles). **U07** does the
> same for `/about` (the hero text swap + the six section toggles).
> **U08** ships the `/admin/site` surface (the GlobalAdmin's edit page)
> + the admin nav link, then runs the acceptance gate and flips the
> close (the `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
> `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip).
>
> **Sizing:** units are sized for a ~32K-context fresh agent one at a
> time, each with its own exit criteria, in the `LS` / `PG` / `SP`
> style. **No new `AccessAction`**, **no new `AccessVia`**, **no new
> authorization path** (the read is a public landing surface; the
> write is the ADR 0050 `GlobalAdmin`-gated single-write-lane shape,
> one `AccessAudit` row per save). **One new doc type**
> (`SiteContent`, a *singleton* — one row per instance, `Id =
> "singleton"` sentinel, the exact `LocaleSettings` shape, ADR 0005 B /
> ADR 0019 / ADR 0020 / ADR 0050). **No EF migration** (a new Marten
> doc type is additive per ADR 0004 §B.1; the delta is applied
> idempotently at boot). **No new route** for the read (the existing
> `/` and `/about` routes are unchanged — only what they render
> changes). **One new route** for the write: `/admin/site` (the
> GlobalAdmin's edit surface, mirroring `/admin/signup` /
> `/admin/timezone` / `/admin/dateformat`). **M4/M5/M6 stay Events /
> Projects / Portability** — no roadmap letter moves.

## Understanding (one paragraph)

Today a fresh instance serves two landing surfaces with fixed copy: `/`
(the hero band, the three feature cards, the "What's new" feed for
signed-in residents, the "The plan" roadmap section) and `/about` (the
hero, the three feature cards, the scope band, the FIG-philosophy band,
the code/docs links, the "What's new" changelog, the contact CTA band).
Every word of copy on both surfaces is a `kw-l`-wrapped English string
in `KnownTranslationKeys.cs` (the `home.*` / `about.*` / `platform.*`
key families); the *presence* of each section is hard-coded in the
Razor view — there is no way to hide a section, no way to change a
word, and no way to add a word, short of a code change. An admin who
wants to say "this is a home for one club, not a neighbourhood," or
wants to drop the FIG-philosophy band on a deployment where it feels
like marketing, or wants the hero to say "Welcome to Maplewood" instead
of the generic pitch, has no path. The `LocaleSettings` singleton
(ADR 0005 B) already carries the admin-settled instance values
(`DefaultLanguageCode` / `DefaultTimezone` / `DefaultDateFormat` /
`IsSignupOpen` / `NotifyAdminsOnSignup` / `AnnouncementCommentsEnabled`
/ `MessagingEnabled` / `QuietCheckMinutes` / …), and the
`AdminSignupController` (ADR 0050) / `AdminTimezoneController` (ADR
0019) / `AdminDateFormatController` (ADR 0020) surfaces already show
the GlobalAdmin-gated single-write-lane shape. This lane adds **one
more doc type in the same shape** (`SiteContent`, a singleton) and
**one more admin surface in the same shape** (`/admin/site`), and the
two landing views read the singleton and conditionally render their
sections. The defaults are byte-identical to the shipped `kw-l` text,
so a fresh instance that never touches the surface looks exactly the
same as it does today.

## The one thing every unit must respect

**Landing-surface semantics (locked in ADR 0150, U02):**

- **The read is a public landing surface.** `/` and `/about` are
  world-readable (the static-page contract — the seeded canonical pages
  are public, `Page.Audience = null`, the ADR 0043 standing matrix).
  The `SiteContent` singleton is read by the same two controllers
  (`HomeController.Index` / `StaticPagesController.About`) and is
  **not** an access decision, **not** a claim (the thin-token rule,
  ADR 0001-B), and is read fresh per request (a save is live on the
  very next render — the ADR 0050 `IsSignupOpen` strong-consistency
  shape). A missing singleton (a fresh boot before the seeder ran, or
  a test construction with no store) degrades to the **shipped
  defaults** (the byte-identical `kw-l` text + every section shown) —
  never a blank page, never an error.
- **The write is the ADR 0050 single-write-lane shape.** One
  `ISiteContentService.SaveAsync(content, actorBy)` lane that loads
  the singleton, applies the field set, and saves in one session
  (invariant C3); exactly one `AccessAudit` row per save
  (`Via = Admin`, action `site.save`, `TargetKind` "site" — the
  `signup.set-open` / `timezone.set-default` / `dateformat.set-default`
  singleton-toggle shape). The lane **never load-or-creates a new
  row on a save** — it upserts the singleton (the `LocaleSettings`
  "one row per instance" shape, ADR 0005 B). Strong consistency
  (invariant C4): the new value is live on the very next
  `GetAsync` / render.
- **The defaults are byte-identical to the shipped `kw-l` text.** The
  `SiteContent` default values (the `FirstBootSeeder` seed + the
  in-code fallback) are the **exact** `home.intro_eyebrow` /
  `home.intro_lead` / `about.eyebrow` / `about.lead` English strings
  from `KnownTranslationKeys.cs` (the `en` source text), and every
  section-toggle default is `true` (every section shown). An admin
  who never touches the surface sees exactly what a fresh instance
  ships today — the "keep it as is by default" requirement. The
  `LS_U04_SeederTests` / `PageServiceTests` "seeded defaults match
  the shipped text" pin carries this forward.
- **The `kw-l` keys stay in the registry (unchanged).** The
  `home.*` / `about.*` / `platform.*` keys in `KnownTranslationKeys.cs`
  are **not** removed, not re-shaped, not re-keyed — they remain the
  **fallback** the `SiteContent` default values are derived from
  (the seeder reads the registry at seed time; the in-code fallback
  is the same text, hard-coded in `SiteContent`'s field defaults so
  a missing singleton degrades to the shipped text). The registry
  parity tests (`KnownTranslationKeys_ParityTests` /
  `KwLRegistryConsistencyTests`) are **untouched** — the view's
  `kw-l` wrap is replaced by a server-resolved `SiteContent` value,
  but the registry entries stay (they are still the canonical `en`
  source text; a future translation lane for the landing surface is
  **out of scope** — the ADR 0005 §B "every other language is
  community-provided" clause is unchanged).
- **The ADR 0149 `Profile.HideHomeIntro` per-resident preference is
  untouched and composable.** The per-resident "hide the intro"
  checkbox (ADR 0149) and the platform-level "show the intro" flag
  (ADR 0150) are **independent**: the per-resident preference is a
  `Profile` field (ADR 0019 / ADR 0020 owner-scope shape, read in
  `HomeController.Index` and passed through `HomeViewModel.HideIntro`);
  the platform-level flag is a `SiteContent` field (read in the same
  `HomeController.Index` and passed through a new
  `HomeViewModel.ShowHomeIntro` field). The view's two intro sections
  render when **both** are true (the per-resident preference is the
  *resident's* choice to hide; the platform flag is the *admin's*
  choice to show — both must agree for the sections to appear). The
  "What's new" feed and the roadmap section are governed **only** by
  the platform flag (the per-resident preference never reaches them —
  the ADR 0149 pin is unchanged).
- **The `SiteContent` doc is a singleton (one row per instance).** The
  `Id` is the sentinel `"singleton"` (the exact `LocaleSettings`
  shape, ADR 0005 B). The doc is registered in a new
  `SiteContentDocTypes.Configure(opts)` surface (the
  `PageDocTypes` / `MediaDocTypes` parallel surface, ADR 0004 §B.1);
  the delta is applied idempotently at boot (the M1/M3/Media
  dev-only loop / versioned-boot path). **No EF migration** (a new
  Marten doc type is additive per ADR 0004 §B.1; the existing
  `LocaleSettings` doc is **not** modified — this lane adds a
  *new* doc, not a new field on the existing one, because the
  landing-surface content is a distinct bounded concern from the
  locale settings, and the ADR 0006 module-boundary contract keeps
  the two contexts independent).
- **The `/admin/site` surface is the ADR 0050 shape.**
  `[Route("admin/site")]` + `[Authorize(Roles = GlobalAdmin)]` on a
  new `AdminSiteController` (a **dedicated** controller — the
  `AdminController`'s constructor is pinned by two Web-layer test
  harnesses (`AdminControllerBlockTests` /
  `AdminControllerMandatoryTests`), so a new dependency there would
  break them; a separate `/admin/site` surface mirrors
  `/admin/signup` / `/admin/timezone` / `/admin/dateformat` — the
  codebase already puts the platform-settled instance values on
  their own controllers). The GET seeds the form with the current
  singleton; the POST saves it (one `AccessAudit` row, the
  `signup.set-open` shape). The `/admin/platform` page gains one
  list-group row linking to `/admin/site` (the
  `SP U03` / `ADR 0050` discoverability pattern).
- **The `Milestones.cs` / README / `MilestonesTests` trio is
  untouched until the lane *ships* (U08 owns the close flip).**
  The `MilestonesTests` pin that **M4** is the single `StatusNext`
  stays intact — a `SITE` row added as `StatusDone` (or a new
  `WhatsNew` entry, the required sixth member of the close flip)
  does not disturb it. The `WhatsNew.cs` registry gains one new
  entry (newest-first, the `0.42.0` row) naming the lane + the ADR
  0150 — the M27 "shipped with no entry until caught in review"
  lesson (AGENTS.md) is held.

## Assumptions

- **Scope = the two landing surfaces' hero text + section toggles.**
  In: the `SiteContent` doc (a new `Kumunita.Core.SiteContent`
  context), the `ISiteContentService` + `SiteContentService` (the
  ADR 0050 single-write-lane shape), the `SiteContentDocTypes`
  (the ADR 0004 §B.1 additive doc type), the `FirstBootSeeder`
  default (the exact shipped `kw-l` text, byte-identical), the
  `HomeController` / `StaticPagesController` read seam (the
  best-effort read, the in-code fallback), the two views'
  conditional rendering (the `@if` wraps + the text swap), the
  `/admin/site` surface (the `AdminSiteController` + the
  `AdminSite` view + the `/admin/platform` link), and the test
  pins. **Out (→ future lanes):** machine translation of the
  landing-surface copy (the ADR 0005 §B "every other language is
  community-provided" clause is unchanged — a future `SITE-2` lane,
  if one comes, would add a `SiteContentTranslation` row shape
  (the `PageTranslation` / `PostTranslation` precedent) + a
  `SiteContentTranslationDocTypes` surface + a `/admin/site`
  translation editor + the `KnownTranslationKeys` parity pin for
  the new keys), the **per-section** editable text (the current
  scope is only the two heroes' eyebrow + lead; the feature cards'
  titles / bodies, the scope band's body, the FIG-philosophy band's
  body, and the code/docs band's lead are **show / hide only** —
  a future `SITE-2` lane could add per-section editable text in the
  same `SiteContent` doc shape, the `SiteContent` field set is
  additive per ADR 0004 §B.1), the **per-section** custom CTA
  buttons (the home hero's "What it is & how it works" button is
  show / hide only; a future lane could add an editable URL +
  label), and the `WhatsNew.cs` / `Milestones.cs` close-flip
  entries until the lane *ships* (U08 owns them).
- **The `SiteContent` field set is exactly the admin's needs.**
  The fields are:
  - `HomeHeroEyebrow` (string, default = the `home.intro_eyebrow`
    English text, "A private home for one neighbourhood")
  - `HomeHeroLead` (string, default = the `home.intro_lead` English
    text, "One quiet place for everything your street does — the
    feed, the groups, and the notes that deserve better than a group
    chat. Private, plain-language, and yours.")
  - `HomeShowAboutButton` (bool, default `true` — the home hero's
    "What it is & how it works" button is shown)
  - `HomeShowFeatures` (bool, default `true` — the home's three
    feature cards are shown)
  - `HomeShowRoadmap` (bool, default `true` — the home's "The plan"
    roadmap section is shown)
  - `AboutHeroEyebrow` (string, default = the `about.eyebrow`
    English text, "Private by default")
  - `AboutHeroLead` (string, default = the `about.lead` English
    text, "One home for everything your neighborhood does — the
    feed, the groups, and the notes that deserve better than a group
    chat. Private, plain-language, and yours.")
  - `AboutShowFeatures` (bool, default `true`)
  - `AboutShowScope` (bool, default `true` — the about's scope band)
  - `AboutShowPhilosophy` (bool, default `true` — the about's
    FIG-philosophy band)
  - `AboutShowProject` (bool, default `true` — the about's
    code/docs band)
  - `AboutShowWhatsNew` (bool, default `true` — the about's "What's
    new" changelog)
  - `AboutShowContactCta` (bool, default `true` — the about's
    contact CTA band)
  The field set is the **complete** admin surface for the current
  scope — no hidden fields, no reserved fields. A future `SITE-2`
  lane adds fields (the `SiteContent` doc is additive per ADR 0004
  §B.1), it does not re-shape the existing ones.
- **The `kw-l` keys stay in the registry.** The view's `kw-l`
  `key=` attributes are replaced by `@Model.Site.HomeHeroEyebrow`
  (etc.), but the registry entries (`home.intro_eyebrow` /
  `home.intro_lead` / `about.eyebrow` / `about.lead` + the
  section-heading keys the views still use for the sections that
  are shown) stay in `KnownTranslationKeys.cs` — they are the
  canonical `en` source text the defaults are derived from, and a
  future translation lane (out of scope) would add a
  `SiteContentTranslation` row shape keyed on the same strings.
  The registry parity tests are untouched.
- **The ADR 0149 `Profile.HideHomeIntro` is composable, not
  replaced.** The two are independent: the per-resident preference
  is a `Profile` field (the ADR 0019 / ADR 0020 owner-scope shape,
  read in `HomeController.Index`); the platform flag is a
  `SiteContent` field (read in the same `HomeController.Index`).
  The view's two intro sections render when **both** are true
  (the per-resident preference is the *resident's* choice to hide;
  the platform flag is the *admin's* choice to show — both must
  agree). The "What's new" feed and the roadmap section are
  governed **only** by the platform flag (the per-resident
  preference never reaches them — the ADR 0149 pin is unchanged).
- **The `/admin/site` surface is a single page.** The
  `AdminSiteController` renders one `AdminSite` view with two
  form sections (Home + About), each with its own field group +
  save button (the `AdminSignupController`'s two-form shape —
  the `isOpen` form + the `notify` form, each with its own POST
  action). The GET seeds both sections from the current singleton;
  the POST for each section saves the field group (one
  `AccessAudit` row per save, the `signup.set-open` shape). The
  save is **partial** — saving the Home section does not touch the
  About section's fields, and vice versa (the `SiteContentService`
  loads the singleton, applies only the submitted field group,
  saves — the ADR 0050 "one field per save" shape, extended to
  "one section per save").
- **The test model is the `LS` / `SP` / `PG` shape.** The
  `FirstBootSeeder` default pin (the `LS_U04_SeederTests` /
  `PageServiceTests` "seeded defaults match the shipped text"
  pattern) asserts: (a) a fresh boot has exactly one
  `SiteContent` row (`Id = "singleton"`), (b) the `HomeHeroEyebrow`
  / `HomeHeroLead` / `AboutHeroEyebrow` / `AboutHeroLead` fields
  equal the exact `home.intro_eyebrow` / `home.intro_lead` /
  `about.eyebrow` / `about.lead` English strings from
  `KnownTranslationKeys.cs` (the `en` source text), (c) every
  section-toggle field is `true`, (d) a second boot is idempotent
  (no duplicate row, no field change). The Web-layer pins
  (the `HomeControllerTests` / `StaticPagesControllerTests`
  shape) assert: (a) a fresh instance (the in-code fallback)
  renders the two heroes with the shipped text + every section
  shown, (b) a saved `SiteContent` row (the `HomeShowAboutButton
  = false` case) hides the home hero's button, (c) the ADR 0149
  `HideHomeIntro = true` + `HomeShowFeatures = true` case hides
  the home's feature cards (the composability pin), (d) the
  `/admin/site` GET seeds the form with the current singleton,
  (e) the `/admin/site` POST saves the field group + writes one
  `AccessAudit` row, (f) the `/admin/site` POST is
  `GlobalAdmin`-gated (a non-`GlobalAdmin` is denied). The
  `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests`
  pins are **untouched** (the registry entries stay).
- **The PowerShell / terminal constraints in `AGENTS.md` and
  `copilot-instructions.md` bind** — no here-strings, no multi-line
  terminal commands, `$`-variables don't survive between commands,
  the `dotnet test` discovery bug on this machine (use the
  in-process `dotnet exec tests\…\bin\Debug\net10.0\*.dll` path).

## Approach

One track, **data + admin surface + conditional rendering**,
sequenced. **U0** verifies the surface (the two landing views, the
`kw-l` keys, the ADR 0149 / ADR 0050 precedent shapes) + authors the
handoff-note skeleton. **U1/U2** author the primary-tier design doc
(invariants + FACES + the exact `SiteContent` field set + the pinned
test names + the acceptance gate + the drift guard) and draft
**ADR 0150**. **U3** implements the Core (`SiteContent` doc +
`ISiteContentService` + `SiteContentService` + `SiteContentDocTypes`
+ the DI registration). **U4** adds the `FirstBootSeeder` default
(the exact shipped text, byte-identical) + the "seeded defaults
match the shipped text" pin. **U5/U06** wire `HomeController` +
`Views/Home/Index.cshtml` to the singleton (the hero text swap +
the three section toggles + the ADR 0149 composability). **U7**
does the same for `/about` (the hero text swap + the six section
toggles). **U8** ships the `/admin/site` surface (the
`AdminSiteController` + the `AdminSite` view + the `/admin/platform`
link) + runs the acceptance gate + flips the close (the
`Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
`MilestonesTests.cs` / `WhatsNew.cs` six-member close flip).

Every code unit ends with **build green** (`dotnet build
Kumunita.slnx -c Debug`). The last unit (U8) appends the final
handoff section so the lane is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U08
below), one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/site-content-design.md`,
  U01/U02 author) — pins the invariants (SITE·1–SITE·9), the FACES
  (SITE1–SITE10), the exact `SiteContent` field set, the read seam
  contract, the write lane contract, the ADR 0149 composability, the
  pinned test names, the acceptance gate, and the drift guard.
- **Secondary — this file** (`docs/plans-milestones/plan-site.md`) —
  the unit registry with each unit's deliverables and exit criteria.
  (The m27 flat-lane convention — the main plan sits at the top of
  `docs/plans-milestones/`, the unit plans sit in
  `docs/plans-milestones/in-progress/` as `site-u00.md` … `site-u08.md`,
  and move to `docs/plans-milestones/done/` as each unit completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/site-handoff-notes.md`) — one
  section per unit, appended (never rewritten). Each unit writes
  exactly one short section before it exits; the next unit reads
  only that section + its own entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the
minimal file list, 3–5 files < ~300 lines each, no full-repo scan;
the design-doc section cited is named); **Deliverables** (a closed
set of new/modified files, ≤ ~4 files / ~600 LOC, no misc cleanups);
**Exit** (`dotnet build Kumunita.slnx -c Debug` green for the
touched projects; handoff-note entry appended *before* any
follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its
own `Deliverables`; (2) never rewrites the design doc outside the
§drift-guard; (3) never introduces a test whose exact name is not in
the §pinned-test-names list; (4) never re-shapes the `SiteContent`
field set (the ADR 0150 D1 pin) outside the design doc; (5) never
removes a `kw-l` registry entry (the ADR 0150 D3 pin — the registry
stays); (6) never re-shapes the ADR 0149 `Profile.HideHomeIntro`
read (the ADR 0150 D5 pin — the per-resident preference is
unchanged); (7) if entry reads reveal the design doc is out of
date, the unit pauses and records `## U<m> — Drift pause` in the
handoff note.

---

## Units (9 total: U00–U08)

### U00 — Kickoff verification

- **Goal:** verify the surface (the two landing views, the `kw-l`
  keys, the ADR 0149 / ADR 0050 precedent shapes, the
  `LocaleSettings` singleton shape) and author the handoff-note
  skeleton (the "Lane open" section). **No code, no build.**
- **Entry reads:** `src/Kumunita.Web/Views/Home/Index.cshtml` (the
  `/home` landing view — the hero band, the three feature cards,
  the "What's new" feed, the "The plan" roadmap section);
  `src/Kumunita.Web/Views/StaticPages/About.cshtml` (the `/about`
  landing view — the hero, the three feature cards, the scope band,
  the FIG-philosophy band, the code/docs band, the "What's new"
  changelog, the contact CTA band);
  `src/Kumunita.Web/Controllers/HomeController.cs` (the `/home`
  controller — the ADR 0149 `HideHomeIntro` read, the `HomeViewModel`
  shape); `src/Kumunita.Web/Controllers/StaticPagesController.cs`
  (the `/about` controller — the `fallBackToProductStory` seam, the
  `HomeViewModel` shape);
  `src/Kumunita.Core/Localization/LanguageCatalog.cs` (the
  `LocaleSettings` singleton — the `Id = "singleton"` sentinel shape,
  the additive-field convention);
  `src/Kumunita.Web/Controllers/AdminSignupController.cs` (the
  ADR 0050 `/admin/signup` surface — the `GlobalAdmin`-gated
  single-write-lane shape, the `AccessAudit` row);
  `docs/adr/0149-home-intro-hide-preference.md` +
  `docs/adr/0050-admin-managed-signup-gate.md` (the two precedent
  ADRs this lane builds on).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/site-handoff-notes.md` — the
  **skeleton only** (the header + the "Lane open" section + the
  `<!-- U00 appends its section below this line. One ## section per
  unit, in order (U00, U01, … U08). Never rewrite a prior section.
  -->` marker). The skeleton mirrors the `wysiwyg-handoff-notes.md`
  / `system-pages-handoff-notes.md` shape (the "Lane open" section
  names the register, the design doc, the ADR, the scope, the
  out-of-scope deferrals, and the frozen base). **No** `## U<m> —`
  section yet (U00 appends its own section after this one).
- **Exit:** the handoff-note skeleton is present. The
  `## Lane open` section names (a) the two landing surfaces (the
  `/home` + `/about` routes, the two views), (b) the `kw-l` key
  families (the `home.*` / `about.*` / `platform.*` keys in
  `KnownTranslationKeys.cs`), (c) the frozen base (ADR 0005 B
  `LocaleSettings` singleton shape, ADR 0019 / ADR 0020
  singleton-toggle shape, ADR 0050 single-write-lane shape, ADR 0149
  `Profile.HideHomeIntro` pin — **unchanged**), (d) the new
  invariants (SITE·1–SITE·9), (e) the **ADR 0150** (the next free
  number after 0149 — the ADR index in `docs/adr/README.md`
  confirms 0149 is the current highest). Handoff note: a
  `## U00 — Kickoff verified` section with the view grep counts +
  the `kw-l` key list + the ADR number (0150) + the precedent ADR
  list (0149 / 0050 / 0019 / 0020 / 0005 B). Move this plan file
  `in-progress/` → `done/` (move **last**). `git status` clean
  except the one new handoff-note file.

### U01 — Design doc Part 1 (invariants + FACES)

- **Goal:** author `docs/design/site-content-design.md` Part 1 — the
  value chain, the **invariants (SITE·1–SITE·9)**, the **FACES
  (SITE1–SITE10)**, and the **assumptions** (the ADR 0150
  landing-surface semantics + the ADR 0149 composability). Mirrors
  the `wysiwyg-editor-design.md` / `system-pages` design doc shape.
  **No code, no build.**
- **Entry reads:** U00's handoff-note `## U00 — Kickoff verified`
  section (the view grep counts + the `kw-l` key list),
  `docs/design/wysiwyg-editor-design.md` (the WY design doc — the
  FACES/invariant template to emulate),
  `docs/design/pages-design.md` (the PG design doc — the
  bounded-context shape), `docs/philosophy/templates/design-doc.md`
  (the required section set),
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
  `home.*` / `about.*` / `platform.*` key families — the exact
  `en` source text the defaults are derived from),
  `src/Kumunita.Web/Views/Home/Index.cshtml` +
  `src/Kumunita.Web/Views/StaticPages/About.cshtml` (the two
  landing views — the exact section structure the design doc
  will pin).
- **Deliverables (1 file, new):**
  `docs/design/site-content-design.md` (~250 lines). Sections:
  - `## Value chain` — the `SP` lane shipped the four static pages
    (terms / help / privacy / conduct) as admin-editable `Page`
    docs; the `LocaleSettings` singleton ships the admin-settled
    instance values (language / timezone / date format / sign-up /
    messaging); this lane ships the **landing-surface content**
    (the two heroes' eyebrow + lead + every section's show/hide)
    as a new `SiteContent` singleton — the admin can now say
    "this is a home for one club," or drop the FIG-philosophy
    band, or change the hero text, without a code change.
  - `## Context` — the gap (the two landing surfaces are
    hard-coded; an admin who wants to change the hero text or
    hide a section has no path); the precedent shapes (ADR 0005 B
    `LocaleSettings` singleton, ADR 0019 / ADR 0020
    singleton-toggle, ADR 0050 single-write-lane, ADR 0149
    `Profile.HideHomeIntro` per-resident preference); the
    constraints that still bind (ADR 0004 §B.1 additive doc type,
    ADR 0006 module-boundary contract, ADR 0015 D1 `kw-l`
    provider-floor discipline, the `LS_U04` / `SP` / `PG` test
    model, the `Milestones.cs` / README / `MilestonesTests`
    close-flip trio).
  - `## Scope` — **In:** the `SiteContent` doc (the new
    `Kumunita.Core.SiteContent` context), the
    `ISiteContentService` + `SiteContentService` (the ADR 0050
    single-write-lane shape), the `SiteContentDocTypes` (the ADR
    0004 §B.1 additive doc type), the `FirstBootSeeder` default
    (the exact shipped `kw-l` text, byte-identical), the
    `HomeController` / `StaticPagesController` read seam (the
    best-effort read, the in-code fallback), the two views'
    conditional rendering (the `@if` wraps + the text swap), the
    `/admin/site` surface (the `AdminSiteController` + the
    `AdminSite` view + the `/admin/platform` link), and the test
    pins. **Out (named deferrals for a future SITE-2 lane, if
    one comes):** machine translation of the landing-surface
    copy (a `SiteContentTranslation` row shape, the
    `PageTranslation` / `PostTranslation` precedent), the
    per-section editable text (the feature cards' titles / bodies,
    the scope band's body, the FIG-philosophy band's body, the
    code/docs band's lead — the current scope is show / hide
    only), the per-section custom CTA buttons (an editable URL +
    label), and the `Milestones.cs` / README / `MilestonesTests`
    trio until the lane *ships* (U08 owns it).
  - `## Invariants (pinned for SITE)` — **SITE·1–SITE·9**,
    each with a one-line SITE note:
    - **SITE·1** — the read is a public landing surface
      (world-readable, not an access decision, not a claim —
      the ADR 0001-B thin-token rule; the ADR 0050
      `IsSignupOpen` strong-consistency shape).
    - **SITE·2** — the write is the ADR 0050 single-write-lane
      shape (one `ISiteContentService.SaveAsync` lane, one
      `AccessAudit` row per save, `Via = Admin`, action
      `site.save`, `TargetKind` "site" — the
      `signup.set-open` / `timezone.set-default` /
      `dateformat.set-default` singleton-toggle shape).
    - **SITE·3** — the defaults are byte-identical to the shipped
      `kw-l` text (the `FirstBootSeeder` seed + the in-code
      fallback; the `LS_U04_SeederTests` / `PageServiceTests`
      "seeded defaults match the shipped text" pin).
    - **SITE·4** — the `kw-l` registry entries stay (the
      `home.*` / `about.*` / `platform.*` keys in
      `KnownTranslationKeys.cs` are not removed, not re-shaped,
      not re-keyed — they are the canonical `en` source text the
      defaults are derived from; the registry parity tests are
      untouched).
    - **SITE·5** — the ADR 0149 `Profile.HideHomeIntro` is
      composable, not replaced (the per-resident preference is
      the *resident's* choice to hide; the platform flag is the
      *admin's* choice to show — both must agree for the two
      intro sections to appear; the "What's new" feed + the
      roadmap section are governed only by the platform flag).
    - **SITE·6** — the `SiteContent` doc is a singleton (one row
      per instance, `Id = "singleton"` sentinel — the exact
      `LocaleSettings` shape, ADR 0005 B; the delta is applied
      idempotently at boot, the M1/M3/Media dev-only loop /
      versioned-boot path; no EF migration).
    - **SITE·7** — the `/admin/site` surface is the ADR 0050
      shape (a dedicated `AdminSiteController`, `GlobalAdmin`-
      gated, one `AccessAudit` row per save; the
      `/admin/platform` page gains one list-group row).
    - **SITE·8** — the `Milestones.cs` / README / `MilestonesTests`
      trio is untouched until the lane *ships* (U08 owns the close
      flip; the `MilestonesTests` pin that M4 is the single
      `StatusNext` stays intact).
    - **SITE·9** — a11y: the two heroes' eyebrow + lead are
      still the first content in the DOM (the hero band is
      unchanged in shape — only the text source changes); the
      section toggles are **server-side** (a hidden section is
      not in the DOM at all, not `display: none` — a screen
      reader never sees it; the view's `aria-label` attributes
      are unchanged for the sections that are shown).
  - `## FACES (pinned, 10)` — **SITE1–SITE10**, each bound to
    an invariant:
    - **SITE1** — the read is a public landing surface (SITE·1,
      SITE·6)
    - **SITE2** — the write is the ADR 0050 single-write-lane
      shape (SITE·2)
    - **SITE3** — the defaults are byte-identical to the shipped
      `kw-l` text (SITE·3)
    - **SITE4** — the `kw-l` registry entries stay (SITE·4)
    - **SITE5** — the ADR 0149 composability (SITE·5)
    - **SITE6** — the `SiteContent` doc is a singleton (SITE·6)
    - **SITE7** — the `/admin/site` surface is the ADR 0050
      shape (SITE·7)
    - **SITE8** — the close-flip trio is untouched until
      *ships* (SITE·8)
    - **SITE9** — a11y (SITE·9)
    - **SITE10** — the `SiteContent` field set is the complete
      admin surface for the current scope (the 13 fields in the
      register's "one thing" section — no hidden fields, no
      reserved fields; a future SITE-2 lane adds fields, it
      does not re-shape the existing ones)
- **Exit:** the file exists with all sections. **No build.**
  Handoff note: a `## U01 — design doc Part 1` section listing
  the **9 invariants** (by id) and the **10 FACES** (SITE1–SITE10)
  so U02 can pin them by id.

### U02 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard) + ADR 0150

- **Goal:** append `## Seams & contracts (Part 2, written by U02)`
  to the design doc — the exact `SiteContent` field set, the
  read-seam contract, the write-lane contract, the **pinned
  seam-test names**, the **acceptance gate**, and the
  **drift-guard**. Plus **ADR 0150** (draft). **No code, no
  build.**
- **Entry reads:** U01's Part 1 (the invariant table is the
  primary source), `docs/design/wysiwyg-editor-design.md`
  §Pinned contract (the shape to emulate),
  `src/Kumunita.Core/Localization/LanguageCatalog.cs` (the
  `LocaleSettings` singleton — the `Id = "singleton"` sentinel
  shape, the additive-field convention),
  `src/Kumunita.Web/Controllers/AdminSignupController.cs` (the
  ADR 0050 `/admin/signup` surface — the `GlobalAdmin`-gated
  single-write-lane shape, the `AccessAudit` row),
  `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the seeder
  shape — the `SeedDefaultPagesAsync` / `SeedLanguages`
  create-if-missing, idempotent, never-overwrites patterns),
  `docs/adr/0050-admin-managed-signup-gate.md` +
  `docs/adr/0149-home-intro-hide-preference.md` (the two
  precedent ADRs — the ADR 0150 shape to emulate),
  `docs/adr/README.md` (the ADR index — 0149 is the current
  highest; 0150 is the next free number).
- **Deliverables (2 files, new):**
  1. **`docs/design/site-content-design.md`** (append Part 2).
     Sub-sections:
     - `### 2.1 frozen base (unchanged)` — ADR 0005 B
       `LocaleSettings` singleton shape + ADR 0019 / ADR 0020
       singleton-toggle shape + ADR 0050 single-write-lane shape +
       ADR 0149 `Profile.HideHomeIntro` pin + ADR 0004 §B.1
       additive doc type + ADR 0006 module-boundary contract +
       ADR 0015 D1 `kw-l` provider-floor discipline + the
       `LS_U04` / `SP` / `PG` test model — all **keep binding
       unchanged**.
     - `### 2.2 the `SiteContent` field set (exact)` — the 13
       fields (the 4 text fields + the 9 toggle fields), each
       with its exact default value (the `en` source text from
       `KnownTranslationKeys.cs` for the text fields; `true`
       for the toggle fields), its `kw-l` key (the
       `home.intro_eyebrow` / `home.intro_lead` /
       `about.eyebrow` / `about.lead` keys for the text fields;
       no key for the toggle fields — they are admin-settled
       instance values, not UI strings), and its a11y note
       (the text fields are the hero's eyebrow + lead; the
       toggle fields are server-side show/hide — a hidden
       section is not in the DOM at all).
     - `### 2.3 the read-seam contract (exact C#)` — the
       `ISiteContentService.GetAsync()` read (returns the
       singleton, or the in-code fallback if the store is
       missing / the row is absent — the ADR 0050
       `IsSignupOpenAsync` best-effort shape, the in-code
       fallback is the `SiteContent` field defaults,
       byte-identical to the shipped `kw-l` text). The
       `HomeController.Index` / `StaticPagesController.About`
       read is the `SiteContentService.GetAsync()` call
       (best-effort — a missing seam or a read failure degrades
       to the in-code fallback, and the page always renders —
       the ADR 0050 / ADR 0149 best-effort shape).
     - `### 2.4 the write-lane contract (exact C#)` — the
       `ISiteContentService.SaveAsync(SiteContent content,
       string actorBy)` write (loads the singleton, applies the
       field set, saves in one session — invariant C3; exactly
       one `AccessAudit` row per save, `Via = Admin`, action
       `site.save`, `TargetKind` "site" — the
       `signup.set-open` shape; the lane upserts the singleton
       (the `LocaleSettings` "one row per instance" shape, ADR
       0005 B); strong consistency — the new value is live on
       the very next `GetAsync` / render). The
       `AdminSiteController.SaveHome` / `SaveAbout` actions are
       the thin wrappers (the `AdminSignupController.Save` /
       `SaveNotify` shape — the `GlobalAdmin`-gated
       `[ValidateAntiForgeryToken]` POST, the `TempData["info"]`
       flash, the `RedirectToAction(nameof(Index))` redirect).
     - `### 2.5 the pinned seam-test names (exact)` — the
       `Core.Tests` pins (the `SiteContentServiceTests`
       class — the `GetAsync_MissingStore_ReturnsInCodeFallback` /
       `SaveAsync_WritesOneAccessAuditRow` /
       `SaveAsync_StrongConsistency_LiveOnNextGetAsync` /
       `SaveAsync_UpsertsSingleton_NoDuplicateRow` pins) + the
       `Core.Tests` seeder pins (the `SiteContentSeederTests`
       class — the `FreshBoot_HasExactlyOneSiteContentRow` /
       `FreshBoot_DefaultsMatchShippedKwLText` /
       `SecondBoot_IsIdempotent_NoDuplicateRow_NoFieldChange`
       pins) + the `Web.Tests` pins (the
       `HomeControllerSiteContentTests` class — the
       `FreshInstance_RendersShippedText_EverySectionShown` /
       `SavedRow_HomeShowAboutButtonFalse_HidesHomeHeroButton` /
       `ADR_0149_HideHomeIntroTrue_HomeShowFeaturesTrue_HidesHomeFeatureCards`
       / `ADR_0149_HideHomeIntroFalse_HomeShowFeaturesFalse_HidesHomeFeatureCards`
       pins) + the `Web.Tests` pins (the
       `AdminSiteControllerTests` class — the
       `GET_SeesCurrentSingleton` / `POST_SaveHome_SavesFieldGroup_WritesOneAccessAuditRow`
       / `POST_SaveAbout_SavesFieldGroup_WritesOneAccessAuditRow`
       / `POST_NonGlobalAdmin_IsDenied` pins).
     - `### 2.6 the acceptance gate (exact)` — the
       `dotnet build Kumunita.slnx -c Debug` green + the
       `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
       green + the `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
       green + the `KwLRegistryConsistencyTests` /
       `KnownTranslationKeys_ParityTests` pins green (the
       registry entries are untouched) + the
       `LS_U04_SeederTests` / `PageServiceTests` /
       `HomeControllerTests` / `StaticPagesControllerTests` /
       `AdminSignupControllerTests` / `AdminControllerTests` pins
       green (the precedent shapes are unchanged) + the
       `MilestonesTests` pin green (the M4 single-`StatusNext`
       pin is intact) + the `WhatsNewTests` pin green (the new
       `0.42.0` entry is present, newest-first).
     - `### 2.7 the drift guard (exact)` — the `SiteContent`
       field set is the **ceiling** (the 13 fields in the
       register's "one thing" section — no field outside the
       field set may appear in the doc, the ADR 0150 D1 pin);
       the `kw-l` registry entries are the **floor** (the
       `home.*` / `about.*` / `platform.*` keys stay, the ADR
       0150 D3 pin); the ADR 0149 `Profile.HideHomeIntro` read
       is the **frozen base** (the per-resident preference is
       unchanged, the ADR 0150 D5 pin); the `LocaleSettings`
       singleton is **untouched** (this lane adds a new doc, not
       a new field on the existing one, the ADR 0150 D6 pin).
  2. **`docs/adr/0150-site-content-customization.md`** (draft,
     Status: Accepted) — the ADR 0150 (the next free number
     after 0149 — the ADR index in `docs/adr/README.md`
     confirms 0149 is the current highest). Sections:
     `## Context` (the two landing surfaces are hard-coded; the
     `LocaleSettings` singleton ships the admin-settled instance
     values; this lane ships the landing-surface content as a
     new `SiteContent` singleton); `## Decision` (the 6
     decisions — the read is a public landing surface, the
     write is the ADR 0050 single-write-lane shape, the defaults
     are byte-identical to the shipped `kw-l` text, the `kw-l`
     registry entries stay, the ADR 0149 `Profile.HideHomeIntro`
     is composable, the `SiteContent` doc is a singleton);
     `## Consequences` (the two landing surfaces are now
     admin-editable; the `/admin/site` surface is the
     GlobalAdmin's edit page; the `kw-l` registry entries stay;
     the ADR 0149 `Profile.HideHomeIntro` is composable; the
     `LocaleSettings` singleton is untouched; the
     `Milestones.cs` / README / `MilestonesTests` trio is
     untouched until the lane *ships*; the `WhatsNew.cs`
     registry gains one new entry (the `0.42.0` row)).
- **Exit:** the design doc Part 2 is present with all
  sub-sections. The ADR 0150 is present (Status: Accepted).
  **No build.** Handoff note: a `## U02 — design doc Part 2 +
  ADR 0150` section listing the **13 `SiteContent` fields** (by
  name), the **pinned test names** (by class + method), the
  **acceptance gate** (the exact command list), and the **ADR
  0150** number (0150) + the **ADR index update** (the
  `docs/adr/README.md` table gains one row — the `0150 | Site
  content customization | Accepted` row).

### U03 — Core: `SiteContent` doc + `ISiteContentService` + `SiteContentService` + `SiteContentDocTypes` + DI

- **Goal:** implement the Core — the `SiteContent` doc (the 13
  fields, the exact defaults), the `ISiteContentService`
  interface (the `GetAsync` / `SaveAsync` shape), the
  `SiteContentService` implementation (the ADR 0050
  single-write-lane shape, the `AccessAudit` row), the
  `SiteContentDocTypes` (the ADR 0004 §B.1 additive doc type),
  and the DI registration. **No Web change, no view change, no
  admin surface.**
- **Entry reads:** U02's handoff-note `## U02 — design doc Part
  2 + ADR 0150` section (the 13 `SiteContent` fields + the
  read-seam contract + the write-lane contract),
  `src/Kumunita.Core/Localization/LanguageCatalog.cs` (the
  `LocaleSettings` singleton — the `Id = "singleton"` sentinel
  shape, the additive-field convention),
  `src/Kumunita.Core/M1DocTypes.cs` (the `LocaleSettings`
  registration — the `opts.Schema.For<LocaleSettings>()` shape),
  `src/Kumunita.Core/DependencyInjection.cs` (the DI
  registration — the `AddTransient<IPageService>(sp => new
  PageService(...))` shape),
  `src/Kumunita.Core/Identity/IdentityService.cs` (the
  `SetSignupOpenAsync` / `IsSignupOpenAsync` shape — the ADR
  0050 single-write-lane shape, the `AccessAudit` row).
- **Deliverables (5 files, new / modified):**
  1. **`src/Kumunita.Core/SiteContent/SiteContent.cs`** (new) —
     the `SiteContent` doc (the 13 fields, the exact defaults,
     the `Id = "singleton"` sentinel). The field set is the
     register's "one thing" section — the 4 text fields (the
     `HomeHeroEyebrow` / `HomeHeroLead` / `AboutHeroEyebrow` /
     `AboutHeroLead` string fields, the exact `en` source text
     from `KnownTranslationKeys.cs` as the defaults) + the 9
     toggle fields (the `HomeShowAboutButton` /
     `HomeShowFeatures` / `HomeShowRoadmap` / `AboutShowFeatures`
     / `AboutShowScope` / `AboutShowPhilosophy` /
     `AboutShowProject` / `AboutShowWhatsNew` /
     `AboutShowContactCta` bool fields, all default `true`).
  2. **`src/Kumunita.Core/SiteContent/ISiteContentService.cs`**
     (new) — the `ISiteContentService` interface (the
     `GetAsync()` read + the `SaveAsync(SiteContent content,
     string actorBy)` write).
  3. **`src/Kumunita.Core/SiteContent/SiteContentService.cs`**
     (new) — the `SiteContentService` implementation (the ADR
     0050 single-write-lane shape — the `GetAsync` best-effort
     read, the `SaveAsync` upsert + `AccessAudit` row).
  4. **`src/Kumunita.Core/SiteContent/SiteContentDocTypes.cs`**
     (new) — the `SiteContentDocTypes.Configure(opts)` surface
     (the ADR 0004 §B.1 additive doc type — the
     `opts.Schema.For<SiteContent>()` shape).
  5. **`src/Kumunita.Core/DependencyInjection.cs`** (modified) —
     the DI registration (the `AddTransient<ISiteContentService>(sp
     => new SiteContentService(sp.GetRequiredService<IDocumentStore>()))`
     shape) + the `SiteContentDocTypes.Configure(opts)` call in
     the schema bootstrap (the `M1DocTypes.Configure(opts)` /
     `PageDocTypes.Configure(opts)` shape).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green.
  Handoff note: a `## U03 — Core: SiteContent doc + service +
  doc types + DI` section listing the 5 files (by path) + the
  13 `SiteContent` fields (by name) + the `ISiteContentService`
  method signatures (the `GetAsync` / `SaveAsync` shape) + the
  `SiteContentDocTypes.Configure` call + the DI registration
  line. **No Web change, no view change, no admin surface.**

### U04 — Core: `FirstBootSeeder` default (the exact shipped text, byte-identical) + seeder pins

- **Goal:** add the `FirstBootSeeder` default (the `SiteContent`
  singleton, the 13 fields, the exact `en` source text from
  `KnownTranslationKeys.cs` as the defaults) + the
  `SiteContentSeederTests` pins (the `FreshBoot_HasExactlyOneSiteContentRow`
  / `FreshBoot_DefaultsMatchShippedKwLText` /
  `SecondBoot_IsIdempotent_NoDuplicateRow_NoFieldChange` pins).
  **No Web change, no view change, no admin surface.**
- **Entry reads:** U03's handoff-note `## U03 — Core: SiteContent
  doc + service + doc types + DI` section (the 13 `SiteContent`
  fields + the `ISiteContentService` method signatures),
  `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the seeder
  shape — the `SeedDefaultPagesAsync` / `SeedLanguages`
  create-if-missing, idempotent, never-overwrites patterns),
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the
  `home.intro_eyebrow` / `home.intro_lead` / `about.eyebrow` /
  `about.lead` keys — the exact `en` source text the defaults
  are derived from),
  `tests/Kumunita.Core.Tests/LS_U04_SeederTests.cs` (the
  `SeedPageTranslationsAsync` / `SeedDefaultPagesAsync` seeder
  pins — the shape to emulate),
  `tests/Kumunita.Core.Tests/PageServiceTests.cs` (the
  `PG5_Seeder_TermsAndHelp_UnderSystemRoot_NoDuplicates_AcrossTwoBoots`
  pin — the "two boots, no duplicates" shape).
- **Deliverables (2 files, new / modified):**
  1. **`src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`**
     (modified) — the `SeedSiteContentAsync` method (the
     create-if-missing, idempotent, never-overwrites shape — the
     `SeedDefaultPagesAsync` / `SeedLanguages` shape; the
     `SiteContent` singleton, the 13 fields, the exact `en`
     source text from `KnownTranslationKeys.cs` as the defaults;
     the `Id = "singleton"` sentinel).
  2. **`tests/Kumunita.Core.Tests/SiteContentSeederTests.cs`**
     (new) — the `SiteContentSeederTests` class (the
     `FreshBoot_HasExactlyOneSiteContentRow` /
     `FreshBoot_DefaultsMatchShippedKwLText` /
     `SecondBoot_IsIdempotent_NoDuplicateRow_NoFieldChange`
     pins).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green;
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green (the new `SiteContentSeederTests` pins pass; the
  existing `LS_U04_SeederTests` / `PageServiceTests` pins are
  unchanged). Handoff note: a `## U04 — Core: FirstBootSeeder
  default + seeder pins` section listing the 2 files (by path)
  + the `SeedSiteContentAsync` method signature + the 3 seeder
  pins (by class + method) + the "defaults match the shipped
  `kw-l` text" assertion (the exact `en` source text from
  `KnownTranslationKeys.cs`). **No Web change, no view change,
  no admin surface.**

### U05 — Web: `HomeController` reads `SiteContent` + `Views/Home/Index.cshtml` conditional rendering

- **Goal:** wire `HomeController.Index` to read the
  `SiteContent` singleton (the best-effort read, the in-code
  fallback) + pass it through a new `HomeViewModel.Site` field;
  wire `Views/Home/Index.cshtml` to render the hero text from
  `Model.Site.HomeHeroEyebrow` / `Model.Site.HomeHeroLead` (the
  `kw-l` wrap is replaced by the server-resolved `SiteContent`
  value) + conditionally render the three sections (the
  `@if (Model.Site.HomeShowAboutButton)` /
  `@if (Model.Site.HomeShowFeatures)` /
  `@if (Model.Site.HomeShowRoadmap)` wraps) + the ADR 0149
  composability (the two intro sections render when **both**
  `Model.HideIntro` is `false` and `Model.Site.HomeShowFeatures`
  is `true`). **No Core change, no `StaticPagesController`
  change, no `/admin/site` surface.**
- **Entry reads:** U04's handoff-note `## U04 — Core:
  FirstBootSeeder default + seeder pins` section (the 13
  `SiteContent` fields + the `SeedSiteContentAsync` method
  signature), `src/Kumunita.Web/Controllers/HomeController.cs`
  (the `/home` controller — the ADR 0149 `HideHomeIntro` read,
  the `HomeViewModel` shape),
  `src/Kumunita.Web/Models/HomeViewModel.cs` (the
  `HomeViewModel` record — the `CommunityName` / `SupportEmail` /
  `Feed` / `HideIntro` fields),
  `src/Kumunita.Web/Views/Home/Index.cshtml` (the `/home`
  landing view — the hero band, the three feature cards, the
  "What's new" feed, the "The plan" roadmap section),
  `src/Kumunita.Core/SiteContent/ISiteContentService.cs` (the
  `ISiteContentService` interface — the `GetAsync()` read).
- **Deliverables (3 files, new / modified):**
  1. **`src/Kumunita.Web/Models/HomeViewModel.cs`**
     (modified) — the `HomeViewModel` record gains a new
     `SiteContent? Site` field (the default `null` — the
     in-code fallback is the `SiteContent` field defaults,
     byte-identical to the shipped `kw-l` text).
  2. **`src/Kumunita.Web/Controllers/HomeController.cs`**
     (modified) — the `Index` action reads the
     `SiteContent` singleton (the best-effort read, the in-code
     fallback — the ADR 0050 / ADR 0149 best-effort shape) +
     passes it through the new `HomeViewModel.Site` field.
  3. **`src/Kumunita.Web/Views/Home/Index.cshtml`**
     (modified) — the hero text is `@Model.Site.HomeHeroEyebrow`
     / `@Model.Site.HomeHeroLead` (the `kw-l` wrap is replaced
     by the server-resolved `SiteContent` value); the three
     sections are conditionally rendered (the
     `@if (Model.Site.HomeShowAboutButton)` /
     `@if (Model.Site.HomeShowFeatures)` /
     `@if (Model.Site.HomeShowRoadmap)` wraps); the ADR 0149
     composability (the two intro sections render when **both**
     `!Model.HideIntro` and `Model.Site.HomeShowFeatures` are
     true).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green.
  Handoff note: a `## U05 — Web: HomeController + Home view
  conditional rendering` section listing the 3 files (by path)
  + the `HomeViewModel.Site` field + the `HomeController.Index`
  read seam + the `Views/Home/Index.cshtml` `@if` wraps + the
  ADR 0149 composability pin (the `!Model.HideIntro &&
  Model.Site.HomeShowFeatures` shape). **No Core change, no
  `StaticPagesController` change, no `/admin/site` surface.**

### U06 — Web: `StaticPagesController` reads `SiteContent` + `Views/StaticPages/About.cshtml` conditional rendering

- **Goal:** wire `StaticPagesController.About` to read the
  `SiteContent` singleton (the best-effort read, the in-code
  fallback) + pass it through a new `HomeViewModel.Site` field
  (the same field U05 added — the `HomeViewModel` is the shared
  home/about view model); wire `Views/StaticPages/About.cshtml`
  to render the hero text from `Model.Site.AboutHeroEyebrow` /
  `Model.Site.AboutHeroLead` (the `kw-l` wrap is replaced by
  the server-resolved `SiteContent` value) + conditionally
  render the six sections (the `@if (Model.Site.AboutShowFeatures)`
  / `@if (Model.Site.AboutShowScope)` /
  `@if (Model.Site.AboutShowPhilosophy)` /
  `@if (Model.Site.AboutShowProject)` /
  `@if (Model.Site.AboutShowWhatsNew)` /
  `@if (Model.Site.AboutShowContactCta)` wraps). **No Core
  change, no `HomeController` change, no `/admin/site` surface.**
- **Entry reads:** U05's handoff-note `## U05 — Web:
  HomeController + Home view conditional rendering` section
  (the `HomeViewModel.Site` field + the
  `HomeController.Index` read seam),
  `src/Kumunita.Web/Controllers/StaticPagesController.cs` (the
  `/about` controller — the `fallBackToProductStory` seam, the
  `HomeViewModel` shape),
  `src/Kumunita.Web/Views/StaticPages/About.cshtml` (the
  `/about` landing view — the hero, the three feature cards,
  the scope band, the FIG-philosophy band, the code/docs band,
  the "What's new" changelog, the contact CTA band),
  `src/Kumunita.Core/SiteContent/ISiteContentService.cs` (the
  `ISiteContentService` interface — the `GetAsync()` read).
- **Deliverables (2 files, modified):**
  1. **`src/Kumunita.Web/Controllers/StaticPagesController.cs`**
     (modified) — the `About` action reads the
     `SiteContent` singleton (the best-effort read, the in-code
     fallback — the ADR 0050 / ADR 0149 best-effort shape) +
     passes it through the `HomeViewModel.Site` field (the same
     field U05 added).
  2. **`src/Kumunita.Web/Views/StaticPages/About.cshtml`**
     (modified) — the hero text is
     `@Model.Site.AboutHeroEyebrow` /
     `@Model.Site.AboutHeroLead` (the `kw-l` wrap is replaced
     by the server-resolved `SiteContent` value); the six
     sections are conditionally rendered (the
     `@if (Model.Site.AboutShowFeatures)` /
     `@if (Model.Site.AboutShowScope)` /
     `@if (Model.Site.AboutShowPhilosophy)` /
     `@if (Model.Site.AboutShowProject)` /
     `@if (Model.Site.AboutShowWhatsNew)` /
     `@if (Model.Site.AboutShowContactCta)` wraps).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green.
  Handoff note: a `## U06 — Web: StaticPagesController + About
  view conditional rendering` section listing the 2 files (by
  path) + the `StaticPagesController.About` read seam + the
  `Views/StaticPages/About.cshtml` `@if` wraps + the 6 section
  toggles (by field name). **No Core change, no `HomeController`
  change, no `/admin/site` surface.**

### U07 — Web: `/admin/site` surface (the `AdminSiteController` + the `AdminSite` view + the `/admin/platform` link)

- **Goal:** implement the `/admin/site` surface — the
  `AdminSiteController` (the `GET` + the `POST /save-home` + the
  `POST /save-about` actions, the ADR 0050 single-write-lane
  shape, the `GlobalAdmin`-gated `[ValidateAntiForgeryToken]`
  POSTs, the `AccessAudit` row per save), the `AdminSite` view
  (the two form sections — Home + About, each with its field
  group + save button, the `AdminSignupController`'s two-form
  shape), and the `/admin/platform` link (the `AdminPlatform`
  view gains one list-group row linking to `/admin/site`).
  **No Core change, no `HomeController` change, no
  `StaticPagesController` change, no view conditional-rendering
  change.**
- **Entry reads:** U06's handoff-note `## U06 — Web:
  StaticPagesController + About view conditional rendering`
  section (the 13 `SiteContent` fields + the
  `StaticPagesController.About` read seam),
  `src/Kumunita.Web/Controllers/AdminSignupController.cs` (the
  ADR 0050 `/admin/signup` surface — the `GlobalAdmin`-gated
  single-write-lane shape, the `AccessAudit` row, the
  `TempData["info"]` flash, the `RedirectToAction` redirect),
  `src/Kumunita.Web/Views/Admin/Platform.cshtml` (the
  `/admin/platform` page — the `SP U03` / `ADR 0050`
  discoverability pattern, the list-group row shape),
  `src/Kumunita.Core/SiteContent/ISiteContentService.cs` (the
  `ISiteContentService` interface — the `GetAsync` / `SaveAsync`
  shape),
  `docs/adr/0050-admin-managed-signup-gate.md` (the ADR 0050
  shape to emulate).
- **Deliverables (3 files, new / modified):**
  1. **`src/Kumunita.Web/Controllers/AdminSiteController.cs`**
     (new) — the `AdminSiteController` (the `GET /admin/site`
     action, the `POST /admin/site/save-home` action, the
     `POST /admin/site/save-about` action; the ADR 0050
     single-write-lane shape; the `GlobalAdmin`-gated
     `[ValidateAntiForgeryToken]` POSTs; the `AccessAudit` row
     per save; the `TempData["info"]` flash; the
     `RedirectToAction(nameof(Index))` redirect).
  2. **`src/Kumunita.Web/Views/AdminSite/Index.cshtml`**
     (new) — the `AdminSite` view (the two form sections — Home
     + About, each with its field group + save button; the
     `AdminSignupController`'s two-form shape; the field labels
     are plain English — the admin view's local convention,
     the `Platform.cshtml` / `AdminSignup/Index.cshtml`
     shape).
  3. **`src/Kumunita.Web/Views/Admin/Platform.cshtml`**
     (modified) — the `/admin/platform` page gains one
     list-group row linking to `/admin/site` (the
     `SP U03` / `ADR 0050` discoverability pattern — the
     "Site content" row, the "Edit the landing surfaces' hero
     text + show/hide the sections" description).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green.
  Handoff note: a `## U07 — Web: /admin/site surface` section
  listing the 3 files (by path) + the `AdminSiteController`
  action signatures (the `GET` / `POST /save-home` /
  `POST /save-about` shape) + the `AdminSite` view's two form
  sections + the `/admin/platform` link row. **No Core change,
  no `HomeController` change, no `StaticPagesController`
  change, no view conditional-rendering change.**

### U08 — Tests + close flip (the `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip)

- **Goal:** run the acceptance gate (the `dotnet build
  Kumunita.slnx -c Debug` green + the
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green + the `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green) + flip the close (the `Milestones.cs` / README /
  `STATUS.md` / `ARCHITECTURE.md` / `MilestonesTests.cs` /
  `WhatsNew.cs` six-member close flip) + append the final
  handoff section so the lane is honest. **No new code
  beyond the test pins + the close-flip files.**
- **Entry reads:** U07's handoff-note `## U07 — Web:
  /admin/site surface` section (the 13 `SiteContent` fields +
  the `AdminSiteController` action signatures),
  `docs/design/site-content-design.md` §2.5 (the pinned seam-test
  names) + §2.6 (the acceptance gate) + §2.7 (the drift guard),
  `src/Kumunita.Web/Milestones.cs` (the `Milestones.cs`
  registry — the `M4` single-`StatusNext` pin, the
  `MilestonesTests` shape),
  `src/Kumunita.Web/WhatsNew.cs` (the `WhatsNew.cs` registry —
  the `0.41.0` / `0.40.0` / … entries, newest-first; the
  `WhatsNewTests` shape),
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the
  `MilestonesTests` pin — the M4 single-`StatusNext` pin),
  `README.md` (the Roadmap section — the `M4` / `M5` / `M6`
  rows, the `SITE` lane row to add),
  `docs/STATUS.md` (the status doc — the `SITE` lane row to
  add),
  `docs/ARCHITECTURE.md` (the architecture doc — the
  `SITE` bounded-context row to add, the `SiteContent` doc
  type row to add).
- **Deliverables (6 files, modified + 2 files, new):**
  1. **`tests/Kumunita.Core.Tests/SiteContentServiceTests.cs`**
     (new) — the `SiteContentServiceTests` class (the
     `GetAsync_MissingStore_ReturnsInCodeFallback` /
     `SaveAsync_WritesOneAccessAuditRow` /
     `SaveAsync_StrongConsistency_LiveOnNextGetAsync` /
     `SaveAsync_UpsertsSingleton_NoDuplicateRow` pins).
  2. **`tests/Kumunita.Web.Tests/HomeControllerSiteContentTests.cs`**
     (new) — the `HomeControllerSiteContentTests` class (the
     `FreshInstance_RendersShippedText_EverySectionShown` /
     `SavedRow_HomeShowAboutButtonFalse_HidesHomeHeroButton` /
     `ADR_0149_HideHomeIntroTrue_HomeShowFeaturesTrue_HidesHomeFeatureCards`
     / `ADR_0149_HideHomeIntroFalse_HomeShowFeaturesFalse_HidesHomeFeatureCards`
     pins) + the `AdminSiteControllerTests` class (the
     `GET_SeesCurrentSingleton` /
     `POST_SaveHome_SavesFieldGroup_WritesOneAccessAuditRow` /
     `POST_SaveAbout_SavesFieldGroup_WritesOneAccessAuditRow` /
     `POST_NonGlobalAdmin_IsDenied` pins).
  3. **`src/Kumunita.Web/Milestones.cs`** (modified) — the
     `Milestones.cs` registry gains one new entry (the `SITE`
     lane row, `StatusDone` — the "Site content
     customization — the landing surfaces' hero text is
     admin-editable + the sections are show/hide" row, the
     ADR 0150 reference).
  4. **`README.md`** (modified) — the Roadmap section gains
     one new row (the `SITE` lane row, the "Site content
     customization — the landing surfaces' hero text is
     admin-editable + the sections are show/hide" row, the
     ADR 0150 reference).
  5. **`docs/STATUS.md`** (modified) — the status doc gains
     one new row (the `SITE` lane row, the "Site content
     customization — the landing surfaces' hero text is
     admin-editable + the sections are show/hide" row, the
     ADR 0150 reference).
  6. **`docs/ARCHITECTURE.md`** (modified) — the architecture
     doc gains one new bounded-context row (the `SITE`
     bounded context, the `SiteContent` doc type, the
     `ISiteContentService` seam, the ADR 0150 reference).
  7. **`src/Kumunita.Web/WhatsNew.cs`** (modified) — the
     `WhatsNew.cs` registry gains one new entry (the `0.42.0`
     row, `2026-10-07` date, the "Site content customization —
     the landing surfaces' hero text is admin-editable + the
     sections are show/hide" change, the ADR 0150 reference).
  8. **`tests/Kumunita.Web.Tests/WhatsNewTests.cs`**
     (modified, if it exists) — the `WhatsNewTests` pin
     asserts the new `0.42.0` entry is present (newest-first).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green;
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green (the new `SiteContentServiceTests` pins pass; the
  existing `LS_U04_SeederTests` / `PageServiceTests` /
  `SiteContentSeederTests` pins are unchanged);
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  green (the new `HomeControllerSiteContentTests` +
  `AdminSiteControllerTests` pins pass; the existing
  `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests`
  / `HomeControllerTests` / `StaticPagesControllerTests` /
  `AdminSignupControllerTests` / `AdminControllerTests` /
  `MilestonesTests` / `WhatsNewTests` pins are unchanged).
  Handoff note: a `## U08 — Tests + close flip` section listing
  the 8 files (by path) + the `Milestones.cs` / README /
  `STATUS.md` / `ARCHITECTURE.md` / `WhatsNew.cs` close-flip
  rows (the exact row text + the ADR 0150 reference) + the
  `WhatsNew.cs` `0.42.0` entry (the exact change text + the
  ADR 0150 reference) + the acceptance gate result (the exact
  command output). **No new code beyond the test pins + the
  close-flip files.**
