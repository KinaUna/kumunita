# M29 — Admin surface labels — sealed unit register

> **Planned.** This is the **lane register** (secondary tier of the milestone's
> three-tier contract) for **M29** — the README / `Milestones.cs` line, verbatim:
> "**Admin surface labels — a GlobalAdmin edits the display name of each
> top-navigation item and the related page header follows the choice, so a
> rename stays consistent across the surface (a label store on the SITE lane's
> shape — a per-item label, not a re-route).**" M29 is a **milestone**, not a
> named lane (it takes the `M29` letter; the roadmap order is unchanged —
> M30/M31/… stay as-is). It reuses the **SITE lane's shape** (ADR 0150)
> end-to-end — a **singleton doc** (`SurfaceLabels`), a **read + audited-write
> seam** (`ISurfaceLabelsService`), a **dedicated GlobalAdmin controller**
> (`/admin/labels`, the `AdminSiteController` shape), a **byte-identical
> seeder default** (all-null → the shipped `kw-l` text), and the **six-member
> close flip** — but it is a **per-surface label store** (13 nav items), not a
> per-section text/toggle store. The label is a **display override, never a
> re-route** (the routes are unchanged — only what they render changes), and it
> is shown in **both** the nav and the surface's page header, so a rename stays
> consistent across the surface.
>
> **U00** verifies the surface (the 13 nav items in `_Layout.cshtml`, the `kw-l`
> keys, the SITE precedent shapes, the ADR index — confirm **0152** is free) and
> authors the handoff-note skeleton. **U01/U02** author the primary-tier design
> doc (invariants + FACES + the closed 13-item label set + the pinned test
> names + the acceptance gate + the drift guard) and draft **ADR 0152** (the
> next free number after 0151). **U03** implements the Core (`SurfaceLabels`
> doc + `ISurfaceLabelsService` + `SurfaceLabelsService` +
> `SurfaceLabelsDocTypes` + the DI registration). **U04** adds the
> `FirstBootSeeder` default (all-null → the `kw-l` fallback) + the shared
> label-resolver seam + the "seeded defaults match the shipped `kw-l` text"
> pin. **U05** wires the 13 nav items (flat row + More dropdown + mobile rail)
> in `_Layout.cshtml` to the resolver. **U06/U07** wire the 13 surface page
> headers (`<h1>`) to the same resolver (the 4 flat-row surfaces, then the 9
> More-dropdown surfaces — Home's hero stays under the SITE lane). **U08** ships
> the `/admin/labels` surface (the GlobalAdmin's edit page + the `/admin/platform`
> link + the admin-surface `kw-l` keys). **U09** runs + records the acceptance
> gate. **U10** flips the close (the `Milestones.cs` / README / `STATUS.md` /
> `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs` six-member close
> flip).
>
> **Sizing:** units are sized for a **~32K-context fresh agent one at a time**,
> each with its own exit criteria, in the SITE / M28 style (≤ ~4 files / ~400
> LOC, 3–6 entry reads, one build + test run). **No new `AccessAction`**,
> **no new `AccessVia`**, **no new `Decide()` branch**, **no new
> `IAuthorizationService` surface** (the read is a public surface; the write is
> the ADR 0050/0150 `GlobalAdmin`-gated single-write-lane shape, one
> `AccessAudit` row per save). **One new doc type** (`SurfaceLabels`, a
> *singleton* — one row per instance, `Id = "singleton"` sentinel, the exact
> `SiteContent`/`LocaleSettings` shape, ADR 0005 B / ADR 0150). **One new
> bounded context** (`Kumunita.Core.SurfaceLabels`) + **one new registration
> surface** (`SurfaceLabelsDocTypes`). **No EF migration** (a new Marten doc
> type is additive per ADR 0004 §B.1; the delta is applied idempotently at
> boot). **Zero route change** for the read (the existing nav routes are
> unchanged — only what they render changes). **One new route** for the write:
> `/admin/labels` (the GlobalAdmin's edit surface, mirroring `/admin/site` /
> `/admin/signup` / `/admin/timezone`). **No roadmap letter moves** (M29 stays
> the milestone it is; M30–M34 are untouched).

## Understanding (one paragraph)

Today every top-navigation item (Home · Announcements · Community · Groups in
the flat row, plus Events · Projects · Inventory · Bookmarks · Documents ·
Pages · Tags · Directory · People in the "More" dropdown and the mobile rail)
and each of their page headers is a hard-coded `kw-l`-wrapped string (the
`nav.home` / `nav.announcements` / `nav.community` / `nav.groups` / `nav.events`
/ `nav.projects` / `inv.nav` / `bm.nav` / `documents.title` / `nav.pages` /
`nav.tags` / `nav.directory` / `nav.people` key families) in `KnownTranslationKeys.cs`
and rendered in `Views/Shared/_Layout.cshtml` + each surface's index view. A
GlobalAdmin who wants the platform to say "News" instead of "Announcements," or
"Board" instead of "Projects," or "Directory" instead of "People," has **no
path short of a code change** (and a rename that only touches the nav, leaving
the page header on the old word, would be *inconsistent* — the nav and the
header would disagree). The SITE lane (ADR 0150) already shipped the exact
machinery M29 needs — a **singleton doc** an admin edits on a dedicated
GlobalAdmin surface, a **best-effort read** that degrades to the shipped
default, a **single audited write lane**, and a **byte-identical seeder
default** — but it is scoped to the two landing surfaces' hero text + section
toggles. M29 adds **one more doc type in the same shape** (`SurfaceLabels`, a
singleton) + **one more admin surface in the same shape** (`/admin/labels`),
and the 13 nav items + 13 surface headers resolve the label from the singleton,
falling back to the `kw-l` key when the admin has not set one. The defaults are
**byte-identical to the shipped `kw-l` text** (all-null → every label falls back
to its key), so a fresh instance that never touches the surface looks exactly
the same as it does today.

## The one thing every unit must respect

**Admin-surface-label semantics (locked in ADR 0152, U00):**

- **The label is a display override, never a re-route.** The routes are
  unchanged (`/` , `/announcements`, `/community`, `/groups`, `/events`,
  `/projects/todos`, `/inventory`, `/bookmarks`, `/documents`, `/pages`,
  `/tags`, `/directory`, `/people`); only the **text** the nav item and the
  surface's page header show changes. A rename never moves a link, never changes
  a route, never touches an `asp-route-*` / `href`.
- **A rename stays consistent across the surface (M29·1).** The resolved label
  is the **same** in the nav (the flat row, the More dropdown, and the mobile
  rail) **and** in the surface's `<h1>` page header. One resolver, one value —
  a rename is never "nav says X, header says Y."
- **The resolution rule (M29·3).** For a surface, the resolved label = the
  admin-set label **if present and non-blank**, else the `kw-l` translation of
  that surface's nav key in the viewer's effective language (a blank /
  whitespace-only admin label falls back to the `kw-l` key — the "empty = use
  default" shape). The `kw-l` key is the **floor**; the admin label is an
  **optional override on top**.
- **The read is a public surface (M29·2).** The label resolution is
  world-readable, **not** an access decision, **not** a claim (the ADR 0001-B
  thin-token rule), and **never audited**. A missing `SurfaceLabels` row (a
  fresh boot before the seeder ran, or a test construction with no store)
  degrades to **all-null** → every label falls back to the `kw-l` key
  (byte-identical to today) — never a blank nav, never an error.
- **The write is the ADR 0150 single-write-lane shape (M29·5).** One
  `ISurfaceLabelsService.SaveAsync(labels, actorBy)` lane that loads the
  singleton, applies the full 13-field set, and saves in one session (invariant
  C3); exactly **one** `AccessAudit` row per save (`Via = Admin`, action
  `surface_labels.save`, `TargetKind` "surface-labels" — the `site.save` /
  `signup.set-open` / `timezone.set-default` singleton-toggle shape). The lane
  **upserts** the singleton (the `SiteContent` "one row per instance" shape,
  ADR 0150 D6); **strong consistency** — the new value is live on the very next
  `GetAsync` / render.
- **The defaults are byte-identical to the shipped `kw-l` text (M29·4).** The
  `SurfaceLabels` default is **all-null** (every label unset → every one falls
  back to its `kw-l` key); the `FirstBootSeeder` seed is **all-null**, and the
  in-code fallback is the **all-null** `SurfaceLabels` (the byte-identical
  `kw-l` text, via the fallback). An admin who never touches the surface sees
  exactly what a fresh instance ships today.
- **The `kw-l` keys stay in the registry (M29·4).** The `nav.*` / `inv.nav` /
  `bm.nav` / `documents.title` entries in `KnownTranslationKeys.cs` are **not**
  removed, not re-shaped, not re-keyed — they remain the **fallback** the
  in-code default resolves to. The registry parity tests
  (`KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests`) are
  **untouched**.
- **Single-string labels, not per-language (M29·8).** Each label is **one**
  admin-set string (a literal override), shown in **all** languages when set.
  The per-language label is a **named deferral** (a future `LBL-2` lane would
  add a `SurfaceLabelTranslation` row shape — the `PageTranslation` /
  `PostTranslation` precedent — + a `/admin/labels` translation editor) — the
  exact SITE lane D1 "single string, translation deferred" shape.
- **The `SurfaceLabels` doc is a singleton (M29·6).** The `Id` is the sentinel
  `"singleton"` (the exact `SiteContent`/`LocaleSettings` shape, ADR 0005 B).
  The doc is registered in a new `SurfaceLabelsDocTypes.Configure(opts)`
  surface (the `SiteContentDocTypes` parallel surface, ADR 0004 §B.1); the
  delta is applied idempotently at boot. **No EF migration.** The `SiteContent`
  and `LocaleSettings` docs are **untouched** — this lane adds a *new* doc in a
  *new* context, not a new field on an existing one (the ADR 0150 D6 pin, the
  ADR 0006 module-boundary contract).
- **The `/admin/labels` surface is the ADR 0150 shape (M29·7).**
  `[Route("admin/labels")]` + `[Authorize(Roles = GlobalAdmin)]` on a new
  `AdminSurfaceLabelsController` (a **dedicated** controller — the
  `AdminController`'s constructor is pinned by two Web-layer test harnesses, so
  a new dependency there would break them; a separate `/admin/labels` surface
  mirrors `/admin/site` / `/admin/signup` / `/admin/timezone`). The GET seeds
  the form with the current singleton; the POST saves it (one `AccessAudit`
  row). The `/admin/platform` page gains one list-group row linking to
  `/admin/labels`.
- **Home's hero stays under the SITE lane (M29·10).** The Home surface's hero
  text is already admin-editable via the SITE lane (ADR 0150 — the
  `HomeHeroEyebrow` / `HomeHeroLead` fields). M29 drives the **Home nav label
  only**; it does **not** touch the SITE hero. The Home surface's `<h1>` (if it
  is a distinct element from the SITE hero) may be driven by the M29 label; the
  SITE hero text is **never** re-shaped.
- **The `Milestones.cs` / README / `MilestonesTests` trio is untouched until
  the milestone *ships* (M29·10 — U10 owns the close flip).** The
  `WhatsNew.cs` registry gains one new entry (newest-first, the `0.45.0` row)
  naming M29 + ADR 0152 — the M27 "shipped with no entry until caught in
  review" lesson (AGENTS.md) is held.

## Assumptions

- **Scope = the 13 top-navigation items + their page headers.** In: the
  `SurfaceLabels` doc (a new `Kumunita.Core.SurfaceLabels` context), the
  `ISurfaceLabelsService` + `SurfaceLabelsService` (the ADR 0150
  single-write-lane shape), the `SurfaceLabelsDocTypes` (the ADR 0004 §B.1
  additive doc type), the `FirstBootSeeder` default (all-null → the `kw-l`
  fallback, byte-identical), the shared **label-resolver** seam (the
  `GetLabelAsync(surfaceKey)` read the views call — override → `kw-l`
  fallback), the 13 nav items in `_Layout.cshtml` (flat row + More dropdown +
  mobile rail), the 13 surface page headers (the `<h1>` of each surface's
  index view; Home's hero stays under SITE), the `/admin/labels` surface (the
  `AdminSurfaceLabelsController` + the `AdminSurfaceLabels` view + the
  `/admin/platform` link + the admin-surface `kw-l` keys), and the test pins.
  **Out (named deferrals for a future `LBL-2` lane, if one comes):**
  per-language labels (a `SurfaceLabelTranslation` row shape, the
  `PageTranslation` / `PostTranslation` precedent, + a `/admin/labels`
  translation editor + the `KnownTranslationKeys` parity pin for the new keys),
  per-label custom icons / glyphs (a label is text only — the nav icons stay as
  shipped), and the `Milestones.cs` / README / `MilestonesTests` trio until the
  milestone *ships* (U10 owns it).
- **The 13 surface keys are the closed set (the register's "one thing"
  section).** The `SurfaceLabels` doc carries exactly 13 optional `string?`
  label fields (one per surface key), **all defaulting to `null`**:
  - `Home` (fallback `nav.home`) — `/`
  - `Announcements` (fallback `nav.announcements`) — `/announcements`
  - `Community` (fallback `nav.community`) — `/community`
  - `Groups` (fallback `nav.groups`) — `/groups`
  - `Events` (fallback `nav.events`) — `/events`
  - `Projects` (fallback `nav.projects`) — `/projects/todos`
  - `Inventory` (fallback `inv.nav`) — `/inventory`
  - `Bookmarks` (fallback `bm.nav`) — `/bookmarks`
  - `Documents` (fallback `documents.title`) — `/documents`
  - `Pages` (fallback `nav.pages`) — `/pages`
  - `Tags` (fallback `nav.tags`) — `/tags`
  - `Directory` (fallback `nav.directory`) — `/directory`
  - `People` (fallback `nav.people`) — `/people`
  The field set is the **complete** admin surface for the current scope — no
  hidden fields, no reserved fields. A future `LBL-2` lane **adds** fields
  (additive per ADR 0004 §B.1), it does not re-shape the existing 13.
- **The `kw-l` keys stay in the registry.** The nav items' `kw-l` `key=`
  attributes are **replaced by the resolver** (a server-resolved label, the
  `kw-l` key as the fallback), but the registry entries (`nav.home` /
  `nav.announcements` / `nav.community` / `nav.groups` / `nav.events` /
  `nav.projects` / `inv.nav` / `bm.nav` / `documents.title` / `nav.pages` /
  `nav.tags` / `nav.directory` / `nav.people`) stay in
  `KnownTranslationKeys.cs` — they are the canonical `en` source text the
  fallback resolves to. The registry parity tests are untouched.
- **The label-resolver is a single seam the views call.** A thin read
  (`ISurfaceLabelsService.GetLabelAsync(string surfaceKey, string fallbackKey,
  string effectiveLanguage)`, or the equivalent in-code helper the views call)
  returns the admin-set label **if present and non-blank**, else the `kw-l`
  translation of `fallbackKey` in `effectiveLanguage`. The views (the nav in
  `_Layout.cshtml` + each surface's `<h1>`) call **one** helper, so the nav and
  the header resolve to the **same** value (M29·1). The resolver is **read-only**
  and **never audited** (M29·2).
- **The `/admin/labels` surface is a single page.** The
  `AdminSurfaceLabelsController` renders one `AdminSurfaceLabels` view with a
  13-row form (one text input per surface key, blank = use the default label) +
  a single Save button (the `AdminSiteController` two-section shape collapsed to
  one section — 13 fields is small enough for one save). The GET seeds the form
  with the current singleton; the POST saves the full 13-field set (one
  `AccessAudit` row, the `site.save` shape). A blank field **clears** that
  surface's label (falls back to the `kw-l` key).
- **The test model is the SITE / M28 shape.** The `FirstBootSeeder` default pin
  asserts: (a) a fresh boot has exactly one `SurfaceLabels` row
  (`Id = "singleton"`), (b) **all 13** label fields are `null`, (c) a second
  boot is idempotent (no duplicate row, no field change). The Web-layer pins
  assert: (a) a fresh instance (the in-code fallback) shows every nav item +
  header with the shipped `kw-l` text, (b) a saved `Announcements` label shows
  in the nav **and** the announcements header (the M29·1 consistency pin), (c)
  the label is shown in all languages (the single-string override pin), (d) a
  blank label falls back to the `kw-l` key, (e) the `/admin/labels` GET seeds
  the form with the current singleton, (f) the `/admin/labels` POST saves the
  label set + writes one `AccessAudit` row, (g) the `/admin/labels` POST is
  `GlobalAdmin`-gated (a non-`GlobalAdmin` is denied). The
  `KwLRegistryConsistencyTests` / `KnownTranslationKeys_ParityTests` pins are
  **untouched** (the registry entries stay).
- **The PowerShell / terminal constraints in `AGENTS.md` and
  `copilot-instructions.md` bind** — no here-strings, no multi-line terminal
  commands, `$`-variables don't survive between commands, the `dotnet test`
  discovery bug on this machine (use the in-process `dotnet exec
  tests\…\bin\Debug\net10.0\*.dll` path).

## Approach

One track, **data + resolver + nav/header wiring + admin surface**, sequenced.
**U00** verifies the surface (the 13 nav items, the `kw-l` keys, the SITE
precedent shapes, the ADR index) + authors the handoff-note skeleton. **U01/U02**
author the primary-tier design doc (invariants + FACES + the closed 13-item
label set + the pinned test names + the acceptance gate + the drift guard) and
draft **ADR 0152**. **U03** implements the Core (`SurfaceLabels` doc +
`ISurfaceLabelsService` + `SurfaceLabelsService` + `SurfaceLabelsDocTypes` +
the DI registration). **U04** adds the `FirstBootSeeder` default (all-null →
the `kw-l` fallback) + the shared label-resolver seam + the "seeded defaults
match the shipped text" pin. **U05** wires the 13 nav items in `_Layout.cshtml`
(flat row + More dropdown + mobile rail) to the resolver. **U06/U07** wire the
13 surface page headers to the same resolver (the 4 flat-row surfaces, then the
9 More-dropdown surfaces — Home's hero stays under the SITE lane). **U08** ships
the `/admin/labels` surface (the `AdminSurfaceLabelsController` + the
`AdminSurfaceLabels` view + the `/admin/platform` link + the admin-surface
`kw-l` keys). **U09** runs + records the acceptance gate. **U10** flips the
close (the six-member close flip).

Every code unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). The last unit (U10) appends the final handoff section so the milestone
is honest.

## Workflow — handoff protocol for fresh-context agents

This milestone is executed as a sequence of **sealed units** (U00–U10 below),
one unit per fresh agent with a ~32K context window.

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/m29-admin-surface-labels-design.md`,
  U01/U02 author) — pins the invariants (M29·1–M29·10), the FACES (M29-1–M29-10),
  the closed 13-item label set, the read-seam contract, the write-lane contract,
  the label-resolution rule, the pinned test names, the acceptance gate, and the
  drift guard.
- **Secondary — this file** (`docs/plans-milestones/plan-m29-admin-surface-labels.md`)
  — the unit registry with each unit's deliverables and exit criteria.
  (The SITE / M27 flat-lane convention — the main plan sits at the top of
  `docs/plans-milestones/`, the unit plans sit in
  `docs/plans-milestones/in-progress/` as `m29-u00.md` … `m29-u10.md`, and
  move to `docs/plans-milestones/done/m29/` as each unit completes.)
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/m29-handoff-notes.md`) — one section per
  unit, appended (never rewritten). Each unit writes exactly one short section
  before it exits; the next unit reads only that section + its own entry-reads
  list.

**Per-unit template** (each `U` below follows this): **Goal** (one
sentence, one or two related deliverables); **Entry reads** (the minimal file
list, 3–5 files < ~300 lines each, no full-repo scan; the design-doc section
cited is named); **Deliverables** (a closed set of new/modified files, ≤ ~4
files / ~600 LOC, no misc cleanups); **Exit** (`dotnet build Kumunita.slnx -c
Debug` green for the touched projects; handoff-note entry appended *before* any
follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard;
(3) never introduces a test whose exact name is not in the §pinned-test-names
list; (4) never re-shapes the `SurfaceLabels` field set (the ADR 0152 D1 pin —
the 13 optional `string?` fields) outside the design doc; (5) never removes a
`kw-l` registry entry (the ADR 0152 D3 pin — the registry stays); (6) never
re-shapes a route (the "label, not re-route" pin — the routes are unchanged);
(7) never touches the SITE `SiteContent` doc or the `LocaleSettings` doc (the
ADR 0150 D6 / ADR 0006 module-boundary pin — M29 adds a *new* doc in a *new*
context); (8) if entry reads reveal the design doc is out of date, the unit
pauses and records `## U<m> — Drift pause` in the handoff note.

---

## Units (11 total: U00–U10)

### U00 — Kickoff verification + handoff-note skeleton

- **Goal:** verify the surface (the 13 nav items in `_Layout.cshtml`, the `kw-l`
  keys, the SITE precedent shapes, the ADR index — confirm **0152** is free) and
  author the handoff-note skeleton (the "Milestone open" section). **No code, no
  build.**
- **Entry reads:** `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the top nav —
  the flat row [Home · Announcements · Community · Groups], the More dropdown
  [Events · Projects · Inventory · Bookmarks · Documents · Pages · Tags ·
  Directory · People], and the mobile rail — the 13 items to label);
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `nav.*` /
  `inv.nav` / `bm.nav` / `documents.title` key families — the exact `en` source
  text the fallback resolves to); `src/Kumunita.Core/SiteContent/SiteContent.cs`
  + `src/Kumunita.Core/SiteContent/SiteContentService.cs` (the SITE singleton
  + the best-effort read + the single audited write lane — the shape M29
  mirrors); `src/Kumunita.Web/Controllers/AdminSiteController.cs` (the ADR 0150
  `/admin/site` surface — the `GlobalAdmin`-gated dedicated controller + the
  `/admin/platform` link shape); `docs/adr/0150-site-content-customization.md`
  (the precedent ADR this milestone builds on); `docs/adr/README.md` (the ADR
  index — confirm **0152** is free after the 0151 row; 0151 = M28, 0150 = SITE).
- **Deliverables (1 file, new):**
  `docs/plans-milestones/in-progress/m29-handoff-notes.md` — the **skeleton only**
  (the header + the "Milestone open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit, in
  order (U00, U01, … U10). Never rewrite a prior section. -->` marker). The
  skeleton mirrors the `site-handoff-notes.md` / `m28-handoff-notes.md` shape
  (the "Milestone open" section names the register, the design doc, the ADR, the
  scope, the out-of-scope deferrals, and the frozen base). **No** `## U<m> —`
  section yet (U00 appends its own section after this one).
- **Exit:** the handoff-note skeleton is present. The `## Milestone open`
  section names (a) the 13 nav items (the flat row + the More dropdown + the
  mobile rail), (b) the `kw-l` key families (the `nav.*` / `inv.nav` / `bm.nav`
  / `documents.title` keys in `KnownTranslationKeys.cs`), (c) the frozen base
  (ADR 0005 B `LocaleSettings` singleton shape, ADR 0150 `SiteContent` shape,
  ADR 0050 single-write-lane shape — **unchanged**), (d) the new invariants
  (M29·1–M29·10), (e) the **ADR 0152** (the next free number after 0151 — the
  ADR index in `docs/adr/README.md` confirms 0151 is the current highest).
  Handoff note: a `## U00 — Kickoff verified` section with the 13 nav items
  (by surface key), the `kw-l` key list, the ADR number (0152) + the precedent
  ADR list (0150 / 0050 / 0005 B). Move this unit plan `in-progress/` → `done/`
  (move **last**). `git status` clean except the one new handoff-note file.

### U01 — Design doc Part 1 (invariants + FACES + closed label set)

- **Goal:** author `docs/design/m29-admin-surface-labels-design.md` Part 1 — the
  value chain, the **invariants (M29·1–M29·10)**, the **FACES
  (M29-1–M29-10)**, and the **assumptions** (the ADR 0152 label semantics + the
  SITE non-conflict). Mirrors the `site-content-design.md` /
  `m28-guardian-time-limits-design.md` shape. **No code, no build.**
- **Entry reads:** U00's handoff-note `## U00 — Kickoff verified` section (the
  13 nav items + the `kw-l` key list),
  `docs/design/site-content-design.md` (the SITE design doc — the
  FACES/invariant template to emulate), `docs/design/m28-guardian-time-limits-design.md`
  (the M28 design doc — the milestone-shape template),
  `docs/philosophy/templates/design-doc.md` (the required section set),
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `nav.*` /
  `inv.nav` / `bm.nav` / `documents.title` key families — the exact `en` source
  text the fallback resolves to), `src/Kumunita.Web/Views/Shared/_Layout.cshtml`
  (the 13 nav items + the mobile rail — the exact item structure the design doc
  will pin).
- **Deliverables (1 file, new):**
  `docs/design/m29-admin-surface-labels-design.md` (~250 lines). Sections:
  - `## Value chain` — the SITE lane shipped the two landing surfaces' content
    as a `SiteContent` singleton; M29 ships the **13 top-navigation items +
    their page headers** as a `SurfaceLabels` singleton — the admin can now say
    "News" instead of "Announcements" or "Board" instead of "Projects," and the
    rename stays consistent across the surface (the nav and the header agree).
  - `## Context` — the gap (the 13 nav items + 13 surface headers are hard-coded
    `kw-l` strings; no path to rename one, and a rename that only touches the
    nav would leave the header on the old word — inconsistent); the precedent
    shapes (ADR 0005 B `LocaleSettings` singleton, ADR 0150 `SiteContent` shape,
    ADR 0050 single-write-lane); the constraints that still bind (ADR 0004 §B.1
    additive doc type, ADR 0006 module-boundary contract, ADR 0015 D1 `kw-l`
    provider-floor discipline, the SITE / M28 test model, the `Milestones.cs` /
    README / `MilestonesTests` close-flip trio).
  - `## Scope` — **In:** the `SurfaceLabels` doc (the new
    `Kumunita.Core.SurfaceLabels` context), the `ISurfaceLabelsService` +
    `SurfaceLabelsService` (the ADR 0150 single-write-lane shape), the
    `SurfaceLabelsDocTypes` (the ADR 0004 §B.1 additive doc type), the
    `FirstBootSeeder` default (all-null → the `kw-l` fallback, byte-identical),
    the shared label-resolver seam (override → `kw-l` fallback), the 13 nav
    items in `_Layout.cshtml` (flat row + More dropdown + mobile rail), the 13
    surface page headers (the `<h1>` of each surface's index view; Home's hero
    stays under the SITE lane), the `/admin/labels` surface, and the test pins.
    **Out (named deferrals for a future `LBL-2` lane, if one comes):**
    per-language labels (a `SurfaceLabelTranslation` row shape, the
    `PageTranslation` / `PostTranslation` precedent), per-label custom icons /
    glyphs (a label is text only — the nav icons stay as shipped), and the
    `Milestones.cs` / README / `MilestonesTests` trio until the milestone
    *ships* (U10 owns it).
  - `## Invariants (pinned for M29)` — **M29·1–M29·10**, each with a one-line
    M29 note (the register's "one thing" section, restated):
    - **M29·1** — a rename stays consistent across the surface (the resolved
      label is the same in the nav and the surface's `<h1>`).
    - **M29·2** — the read is a public surface (world-readable, not an access
      decision, not a claim — ADR 0001-B thin-token; never audited).
    - **M29·3** — the resolution rule (the admin label if present and non-blank,
      else the `kw-l` key in the viewer's language; a blank label falls back to
      the key).
    - **M29·4** — the defaults are byte-identical to the shipped `kw-l` text
      (all-null → every label falls back to its key); the `kw-l` registry
      entries stay (the parity tests are untouched).
    - **M29·5** — the write is the ADR 0150 single-write-lane shape (one
      `SaveAsync` lane, one `AccessAudit` row, `Via = Admin`, action
      `surface_labels.save`, `TargetKind` "surface-labels"; strong
      consistency).
    - **M29·6** — the `SurfaceLabels` doc is a singleton (one row per instance,
      `Id = "singleton"` sentinel — the `SiteContent` shape; the delta is
      applied idempotently at boot; no EF migration; the `SiteContent` +
      `LocaleSettings` docs are untouched).
    - **M29·7** — the `/admin/labels` surface is the ADR 0150 shape (a dedicated
      `AdminSurfaceLabelsController`, `GlobalAdmin`-gated, one `AccessAudit`
      row per save; the `/admin/platform` page gains one list-group row).
    - **M29·8** — single-string labels, not per-language (a label is one
      admin-set string, shown in all languages when set; the per-language label
      is a named deferral — the SITE lane D1 shape).
    - **M29·9** — a11y: the resolved label is rendered where the `kw-l` key is
      today (no DOM structure change); the nav links' `aria-label`s are
      unchanged for the surfaces that are shown.
    - **M29·10** — the `Milestones.cs` / README / `MilestonesTests` trio is
      untouched until the milestone *ships* (U10 owns the close flip); Home's
      hero stays under the SITE lane (M29 drives only the Home nav label).
  - `## FACES (pinned, 10)` — **M29-1–M29-10**, each bound to an invariant:
    - **M29-1** — a fresh instance (no `SurfaceLabels` row) shows every nav
      item + header with the shipped `kw-l` text (M29·2, M29·3, M29·4, M29·6)
    - **M29-2** — an admin sets the "Announcements" label → the nav item **and**
      the announcements page header both show the new label (M29·1)
    - **M29-3** — the label is shown in all languages (single-string override);
      the `kw-l` key is the fallback only (M29·3, M29·8)
    - **M29-4** — a blank / whitespace label falls back to the `kw-l` key (M29·3)
    - **M29-5** — the write is the ADR 0150 single-write-lane shape (one
      `AccessAudit` row, strong consistency) (M29·5)
    - **M29-6** — the `SurfaceLabels` doc is a singleton (one row per instance)
      (M29·6)
    - **M29-7** — the `/admin/labels` surface is `GlobalAdmin`-gated (a
      non-`GlobalAdmin` is denied) (M29·7)
    - **M29-8** — the `kw-l` registry entries stay (the parity tests are
      untouched) (M29·4)
    - **M29-9** — a11y: the resolved label is in the same DOM position as the
      `kw-l` key today (M29·9)
    - **M29-10** — Home's hero stays under the SITE lane (M29 drives only the
      Home nav label, not the SITE hero) (M29·1, the non-conflict note)
- **Exit:** the file exists with all sections. **No build.**
  Handoff note: a `## U01 — design doc Part 1` section listing the **10
  invariants** (by id) and the **10 FACES** (M29-1–M29-10) so U02 can pin them
  by id. Move this unit plan `in-progress/` → `done/` (move **last**).

### U02 — Design doc Part 2 (seams, contracts, test list, gate, drift-guard) + ADR 0152

- **Goal:** append `## Seams & contracts (Part 2, written by U02)` to the design
  doc — the exact `SurfaceLabels` field set, the read-seam contract, the
  write-lane contract, the **pinned seam-test names**, the **acceptance gate**,
  and the **drift-guard**. Plus **ADR 0152** (draft) + one `docs/adr/README.md`
  index row. **No code, no build.**
- **Entry reads:** U01's Part 1 (the invariant table is the primary source),
  `docs/design/site-content-design.md` §Pinned contract (the shape to emulate),
  `src/Kumunita.Core/SiteContent/SiteContentService.cs` (the best-effort read +
  the single audited write lane — the shape M29 mirrors),
  `src/Kumunita.Web/Controllers/AdminSiteController.cs` (the ADR 0150
  `/admin/site` surface — the `GlobalAdmin`-gated dedicated controller + the
  `/admin/platform` link shape),
  `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the seeder shape — the
  `SeedSiteContentAsync` create-if-missing, idempotent, never-overwrites
  pattern),
  `docs/adr/0150-site-content-customization.md` + `docs/adr/README.md` (the ADR
  0152 shape to emulate + the ADR index — confirm 0152 is free).
- **Deliverables (3 files, 2 new + 1 modify):**
  1. **`docs/design/m29-admin-surface-labels-design.md`** (append Part 2).
     Sub-sections:
     - `### 2.1 frozen base (unchanged)` — ADR 0005 B `LocaleSettings` singleton
       shape + ADR 0150 `SiteContent` shape + ADR 0050 single-write-lane shape +
       ADR 0004 §B.1 additive doc type + ADR 0006 module-boundary contract +
       ADR 0015 D1 `kw-l` provider-floor discipline + the SITE / M28 test model
       — all **keep binding unchanged**.
     - `### 2.2 the `SurfaceLabels` field set (exact)` — the 13 optional
       `string?` label fields (one per surface key, all defaulting to `null`),
       each with its exact `kw-l` fallback key (the `nav.home` /
       `nav.announcements` / `nav.community` / `nav.groups` / `nav.events` /
       `nav.projects` / `inv.nav` / `bm.nav` / `documents.title` / `nav.pages`
       / `nav.tags` / `nav.directory` / `nav.people` keys), its surface route,
       and its a11y note (the label is text-only — the nav icons stay as
       shipped).
     - `### 2.3 the read-seam contract (exact C#)` — the
       `ISurfaceLabelsService.GetLabelAsync(string surfaceKey, string
       fallbackKey, string effectiveLanguage, CancellationToken ct)` read
       (returns the admin-set label **if present and non-blank**, else the
       `kw-l` translation of `fallbackKey` in `effectiveLanguage` — the ADR
       0050 `IsSignupOpenAsync` best-effort shape, the in-code fallback is the
       all-null `SurfaceLabels`). The nav + header views call **one** helper
       (the resolver) so they resolve to the **same** value (M29·1). The read
       is **never audited** (M29·2).
     - `### 2.4 the write-lane contract (exact C#)` — the
       `ISurfaceLabelsService.SaveAsync(SurfaceLabels labels, string actorBy)`
       write (loads the singleton, applies the full 13-field set, saves in one
       session — invariant C3; exactly one `AccessAudit` row per save,
       `Via = Admin`, action `surface_labels.save`, `TargetKind`
       "surface-labels" — the `site.save` shape; the lane upserts the singleton
       (the `SiteContent` "one row per instance" shape, ADR 0150 D6); strong
       consistency — the new value is live on the very next `GetAsync` /
       render). The `AdminSurfaceLabelsController.Save` action is the thin
       wrapper (the `AdminSiteController.SaveHome` shape — the
       `GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POST, the
       `TempData["info"]` flash, the `RedirectToAction(nameof(Index))`
       redirect).
     - `### 2.5 the pinned seam-test names (exact)` — the `Core.Tests` pins
       (the `SurfaceLabelsServiceTests` class — the
       `GetAsync_MissingStore_ReturnsAllNullFallback` /
       `GetAsync_MissingRow_ReturnsAllNullFallback` /
       `SaveAsync_WritesOneAccessAuditRow` /
       `SaveAsync_StrongConsistency_LiveOnNextGetAsync` /
       `SaveAsync_UpsertsSingleton_NoDuplicateRow` /
       `SaveAsync_BlankLabel_StoredBlank_FallsBackAtResolution` /
       `GetLabelAsync_ReturnsOverrideWhenSet` /
       `GetLabelAsync_ReturnsNullWhenNotSet` pins) + the `Core.Tests` seeder
       pins (the `SurfaceLabelsSeederTests` class — the
       `FreshBoot_HasExactlyOneSurfaceLabelsRow` /
       `FreshBoot_AllLabelsNull` /
       `SecondBoot_IsIdempotent_NoDuplicateRow` pins) + the `Web.Tests` pins
       (the `AdminSurfaceLabelsControllerTests` class — the
       `GET_SeesCurrentSingleton` /
       `POST_Save_SavesLabel_WritesOneAccessAuditRow` /
       `POST_NonGlobalAdmin_IsDenied` / `GET_FreshInstance_AllLabelsBlank`
       pins) + the `Web.Tests` pins (the `SurfaceLabelResolutionTests` class —
       the `FreshInstance_NavShowsKwLText` / `SavedLabel_NavShowsOverride` /
       `SavedLabel_HeaderShowsOverride` /
       `SavedLabel_SameLabelNavAndHeader` / `BlankLabel_FallsBackToKwL` pins).
     - `### 2.6 the acceptance gate (exact)` — the
       `dotnet build Kumunita.slnx -c Debug` green + the
       `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
       green + the `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
       green + the `KwLRegistryConsistencyTests` /
       `KnownTranslationKeys_ParityTests` pins green (the registry entries are
       untouched) + the `SiteContentServiceTests` /
       `AdminSiteControllerTests` pins green (the SITE precedent shapes are
       unchanged) + the `MilestonesTests` pin green (the order +
       single-in-progress pin is intact) + the `WhatsNewTests` pin green (the
       new `0.45.0` entry is present, newest-first).
     - `### 2.7 the drift guard (exact)` — the `SurfaceLabels` field set is the
       **ceiling** (the 13 optional `string?` fields — no field outside the
       field set may appear in the doc, the ADR 0152 D1 pin); the `kw-l`
       registry entries are the **floor** (the `nav.*` / `inv.nav` / `bm.nav` /
       `documents.title` keys stay, the ADR 0152 D3 pin); the `SiteContent` +
       `LocaleSettings` docs are **untouched** (the ADR 0150 D6 / ADR 0006
       module-boundary pin — M29 adds a *new* doc in a *new* context); the
       routes are **unchanged** (the "label, not re-route" pin — a rename never
       moves a link).
  2. **`docs/adr/0152-admin-surface-labels.md`** (new, Status: Accepted) — the
     ADR 0152 (the next free number after 0151 — the ADR index in
     `docs/adr/README.md` confirms 0151 is the current highest). Sections:
     `## Context` (the 13 nav items + 13 surface headers are hard-coded `kw-l`
     strings; the `SiteContent` singleton ships the landing-surface content;
     this milestone ships the 13 nav items + their page headers as a
     `SurfaceLabels` singleton); `## Decision` (the 6 decisions — the label is a
     display override, not a re-route, a rename stays consistent across the
     surface, the read is a public surface, the write is the ADR 0150
     single-write-lane shape, the defaults are byte-identical to the shipped
     `kw-l` text (all-null → the `kw-l` fallback), the `SurfaceLabels` doc is a
     singleton in a new context); `## Consequences` (the 13 nav items + 13
     surface headers are now admin-editable; the `/admin/labels` surface is the
     GlobalAdmin's edit page; the `kw-l` registry entries stay; the
     `SiteContent` + `LocaleSettings` docs are untouched; the
     `Milestones.cs` / README / `MilestonesTests` trio is untouched until the
     milestone *ships*; the `WhatsNew.cs` registry gains one new entry (the
     `0.45.0` row); the per-language label is a named deferral (a future
     `LBL-2` lane)).
  3. **`docs/adr/README.md`** (modify) — the ADR index gains one row:
     `| 0152 | Admin surface labels: a GlobalAdmin edits the display name of each top-navigation item and the related page header follows the choice (a `SurfaceLabels` singleton on the SITE lane's shape — a per-item label, not a re-route; additive on 0150 + 0004 §B.1 + 0005 B + 0006) | Accepted |`.
- **Exit:** the design doc Part 2 is present with all sub-sections. The ADR
  0152 is present (Status: Accepted). The ADR index has the 0152 row. **No
  build.** Handoff note: a `## U02 — design doc Part 2 + ADR 0152` section
  listing the **13 `SurfaceLabels` fields** (by surface key), the **pinned test
  names** (by class + method), the **acceptance gate** (the exact command
  list), and the **ADR 0152** number (0152) + the **ADR index update** (the
  `docs/adr/README.md` table gained the `0152` row). Move this unit plan
  `in-progress/` → `done/` (move **last**).

### U03 — Core: `SurfaceLabels` doc + `ISurfaceLabelsService` + `SurfaceLabelsService` + `SurfaceLabelsDocTypes` + DI

- **Goal:** implement the Core — the `SurfaceLabels` doc (the 13 optional
  `string?` fields, all defaulting to `null`), the `ISurfaceLabelsService`
  interface (the `GetLabelAsync` / `SaveAsync` shape), the `SurfaceLabelsService`
  implementation (the ADR 0150 single-write-lane shape, the `AccessAudit` row),
  the `SurfaceLabelsDocTypes` (the ADR 0004 §B.1 additive doc type), and the DI
  registration. **No Web change, no view change, no admin surface.**
- **Entry reads:** U02's handoff-note `## U02 — design doc Part 2 + ADR 0152`
  section (the 13 `SurfaceLabels` fields + the read-seam contract + the
  write-lane contract),
  `src/Kumunita.Core/SiteContent/SiteContent.cs` (the SITE singleton — the
  `Id = "singleton"` sentinel shape, the 13-field ceiling),
  `src/Kumunita.Core/SiteContent/SiteContentService.cs` (the best-effort read +
  the single audited write lane — the shape M29 mirrors),
  `src/Kumunita.Core/SiteContent/SiteContentDocTypes.cs` (the `opts.Schema.For`
  shape — the M29 `SurfaceLabelsDocTypes` mirrors this),
  `src/Kumunita.Core/DependencyInjection.cs` (the DI registration — the
  `AddTransient<ISiteContentService>` shape),
  `docs/design/m29-admin-surface-labels-design.md` §2.2–§2.4 (the exact
  `SurfaceLabels` shape + the read/write contracts).
- **Deliverables (5 files, new / modified):**
  1. **`src/Kumunita.Core/SurfaceLabels/SurfaceLabels.cs`** (new) — the
     `SurfaceLabels` doc (the 13 optional `string?` fields, all defaulting to
     `null`, the `Id = "singleton"` sentinel). The field set is the register's
     "one thing" section — `Home` / `Announcements` / `Community` / `Groups` /
     `Events` / `Projects` / `Inventory` / `Bookmarks` / `Documents` / `Pages` /
     `Tags` / `Directory` / `People`, each `public string? X { get; set; }`
     (default `null` = use the `kw-l` fallback). A `GetLabel(string surfaceKey)`
     helper (case-insensitive, returns the field's value or `null`).
  2. **`src/Kumunita.Core/SurfaceLabels/ISurfaceLabelsService.cs`** (new) — the
     `ISurfaceLabelsService` interface (the `GetLabelAsync(surfaceKey,
     fallbackKey, effectiveLanguage, ct)` read — override → `kw-l` fallback,
     best-effort, never audited — + the `SaveAsync(labels, actorBy, ct)` write
     — the ADR 0150 single audited write-lane shape).
  3. **`src/Kumunita.Core/SurfaceLabels/SurfaceLabelsService.cs`** (new) — the
     `SurfaceLabelsService` implementation. `GetLabelAsync`: load the singleton
     (best-effort — a missing row / read failure degrades to the all-null
     fallback), read the field by `surfaceKey`, return it **if present and
     non-blank**, else `await Translation.GetAsync(fallbackKey, effectiveLanguage)`.
     `SaveAsync`: the ADR 0150 single audited write-lane shape — one write
     session, the doc + exactly one `AccessAudit` row (`Via = Admin`, action
     `surface_labels.save`, `TargetKind` "surface-labels") commit together
     (invariant C3, strong consistency C4); the lane upserts the singleton —
     it never creates a second row (M29·6).
  4. **`src/Kumunita.Core/SurfaceLabels/SurfaceLabelsDocTypes.cs`** (new) —
     `public static class SurfaceLabelsDocTypes { public static void
     Configure(StoreOptions opts) { opts.Schema.For<SurfaceLabels>(); } }` —
     the `SiteContentDocTypes` shape, verbatim.
  5. **`src/Kumunita.Core/DependencyInjection.cs`** (modify) — add the
     `SurfaceLabelsService` registration:
     `services.AddTransient<ISurfaceLabelsService>(sp => new
     SurfaceLabelsService(sp.GetRequiredService<IDocumentStore>()));` — mirror
     the `ISiteContentService` registration shape. **Plus** the
     `SurfaceLabelsDocTypes.Configure(opts)` call in both boot paths (the
     dev-loop in `Program.cs` and the all-env `SchemaBootstrap`) — the
     `SiteContentDocTypes.Configure` neighbor.
- **Exit:** `run_build` on `Kumunita.Core` + `Kumunita.Web` green.
  `SurfaceLabels.cs` exists; the `SurfaceLabelsService` + `SurfaceLabelsDocTypes`
  compile. **No new test** (U04/U09's seam tests are the first M29 tests).
  Handoff note: a `## U03 — SurfaceLabels Core` section — (a) the 13 field
  names (by surface key), (b) the `SurfaceLabelsDocTypes` line (one
  `.Schema.For` call), (c) the two boot-path lines added (file + line numbers),
  (d) any compile warnings on the new types. Move this unit plan `in-progress/`
  → `done/` (move **last**).

### U04 — `FirstBootSeeder` default (all-null) + the label-resolver seam + the seeder pin

- **Goal:** add the `FirstBootSeeder` default (all-null → the `kw-l` fallback,
  byte-identical to the shipped text), the shared **label-resolver** seam the
  views call (the `GetLabelAsync` helper — override → `kw-l` fallback), and the
  "seeded defaults match the shipped `kw-l` text" pin. **No view change, no
  admin surface.**
- **Entry reads:** U03's handoff-note `## U03 — SurfaceLabels Core` section (the
  13 `SurfaceLabels` fields + the read-seam contract),
  `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` (the seeder shape — the
  `SeedSiteContentAsync` create-if-missing, idempotent, never-overwrites
  pattern), `src/Kumunita.Core/SurfaceLabels/SurfaceLabelsService.cs` (U03's
  `GetLabelAsync` — the resolver seam the views call),
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `nav.*` /
  `inv.nav` / `bm.nav` / `documents.title` keys — the exact `en` source text the
  fallback resolves to),
  `docs/design/m29-admin-surface-labels-design.md` §2.3 (the read-seam contract).
- **Deliverables (2 files, new / modified + 1 test):**
  1. **`src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs`** (modify) — add a
     `SeedSurfaceLabelsAsync` step (the `SeedSiteContentAsync` shape —
     create-if-missing, idempotent, never overwrites a resident edit): on a
     fresh boot, store exactly one `SurfaceLabels` row (`Id = "singleton"`,
     **all 13** label fields `null`). A warm boot is a no-op (the row exists,
     no field change).
  2. **`src/Kumunita.Core/SurfaceLabels/SurfaceLabelsService.cs`** (modify, if
     needed) — confirm the `GetLabelAsync` resolver seam is the **single** seam
     the views call (override → `kw-l` fallback, best-effort, never audited).
     If U03 left it as a raw field read, add the `GetLabelAsync(surfaceKey,
     fallbackKey, effectiveLanguage, ct)` wrapper here (the `Translation.GetAsync`
     fallback call).
  3. **`tests/Kumunita.Core.Tests/SurfaceLabelsSeederTests.cs`** (new) — the
     seeder pin (the `LS_U04_SeederTests` / `PageServiceTests` "seeded defaults
     match the shipped text" shape): (a) a fresh boot has exactly one
     `SurfaceLabels` row (`Id = "singleton"`), (b) **all 13** label fields are
     `null`, (c) a second boot is idempotent (no duplicate row, no field
     change).
- **Exit:** `run_build` on `Kumunita.Core` + `Kumunita.Web` green. The
  `SeedSurfaceLabelsAsync` step is present; the `SurfaceLabelsSeederTests`
  compile. Handoff note: a `## U04 — seeder default + resolver` section — (a)
  the `SeedSurfaceLabelsAsync` line count (one `.Store` call, all-null), (b)
  the `GetLabelAsync` signature (the resolver seam the views call), (c) the
  3 seeder test names, (d) any compile warnings. Move this unit plan
  `in-progress/` → `done/` (move **last**).

### U05 — Web: wire the 13 nav items (flat row + More dropdown + mobile rail) in `_Layout.cshtml`

- **Goal:** wire the 13 nav items in `Views/Shared/_Layout.cshtml` (the flat
  row [Home · Announcements · Community · Groups], the More dropdown [Events ·
  Projects · Inventory · Bookmarks · Documents · Pages · Tags · Directory ·
  People], and the mobile rail) to the label resolver (the `GetLabelAsync`
  helper — override → `kw-l` fallback). **No surface header change (U06/U07),
  no admin surface (U08).**
- **Entry reads:** U04's handoff-note `## U04 — seeder default + resolver`
  section (the `GetLabelAsync` signature),
  `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the 13 nav items — the flat
  row, the More dropdown, and the mobile rail — the exact `kw-l` key each item
  uses today),
  `src/Kumunita.Core/SurfaceLabels/SurfaceLabelsService.cs` (the `GetLabelAsync`
  resolver seam the views call),
  `src/Kumunita.Web/Views/Home/Index.cshtml` (the SITE lane's `kw-l` usage —
  the pattern to mirror for the nav item's label resolution),
  `docs/design/m29-admin-surface-labels-design.md` §2.3 (the read-seam contract
  + the M29·1 consistency rule).
- **Deliverables (1 file, modify):**
  1. **`src/Kumunita.Web/Views/Shared/_Layout.cshtml`** (modify) — the 13 nav
     items (the flat row, the More dropdown, and the mobile rail) resolve their
     label via the `GetLabelAsync` helper (the `kw-l` key as the fallback).
     The `href` / `asp-route-*` attributes are **unchanged** (the "label, not
     re-route" pin — a rename never moves a link). The `aria-label`s are
     unchanged for the surfaces that are shown (M29·9). The nav links' structure
     (the `<li>` / `<a>` markup, the `data-nav-fold` / `data-nav-more` hooks)
     is **unchanged** — only the text source changes (the `kw-l` key is replaced
     by the resolver, the fallback is the same key).
- **Exit:** `run_build` on `Kumunita.Web` green + `tsc` (the M2 TS build path —
  check `package.json`'s build script) green. The 13 nav items resolve their
  label via the `GetLabelAsync` helper. Handoff note: a `## U05 — nav items`
  section — (a) the 13 nav items (by surface key) + the flat row / More dropdown
  / mobile rail locations, (b) the `GetLabelAsync` call shape (the helper the
  views call), (c) the `aria-label`s (unchanged), (d) any `tsc` warnings. Move
  this unit plan `in-progress/` → `done/` (move **last**).

### U06 — Web: wire the 4 flat-row surface headers (Home, Announcements, Community, Groups)

- **Goal:** wire the 4 flat-row surface page headers (`<h1>`) — Home,
  Announcements, Community, Groups — to the same label resolver (the
  `GetLabelAsync` helper — override → `kw-l` fallback). **Home's hero stays
  under the SITE lane** (M29·10 — M29 drives only the Home nav label, not the
  SITE hero). **No More-dropdown surface header (U07), no admin surface (U08).**
- **Entry reads:** U05's handoff-note `## U05 — nav items` section (the
  `GetLabelAsync` call shape),
  `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the 4 flat-row nav items —
  the `kw-l` keys + the surface routes),
  `src/Kumunita.Web/Views/Home/Index.cshtml` + `src/Kumunita.Web/Views/Announcement/Index.cshtml`
  + `src/Kumunita.Web/Views/Posts/Index.cshtml` + `src/Kumunita.Web/Views/Groups/Index.cshtml`
  (the 4 flat-row surface headers — the exact `<h1>` each uses today; the view
  folders are singular (`Announcement`), and Community renders `Posts/Index`
  via `PostsController.AllSections` → `View("Index")`),
  `src/Kumunita.Core/SurfaceLabels/SurfaceLabelsService.cs` (the `GetLabelAsync`
  resolver seam the views call),
  `docs/design/m29-admin-surface-labels-design.md` §2.3 (the read-seam contract
  + the M29·1 consistency rule + the M29·10 Home non-conflict).
- **Deliverables (≤ 4 files, modify):**
  1. **`src/Kumunita.Web/Views/Home/Index.cshtml`** (modify) — the Home
     surface's `<h1>` (if it is a distinct element from the SITE hero) resolves
     its label via the `GetLabelAsync` helper (the `nav.home` key as the
     fallback). **The SITE hero (the `HomeHeroEyebrow` / `HomeHeroLead` fields)
     is untouched** (M29·10 — the SITE lane owns the hero text).
  2. **`src/Kumunita.Web/Views/Announcement/Index.cshtml`** (modify) — the
     Announcements surface's `<h1>` resolves its label via the `GetLabelAsync`
     helper (the `nav.announcements` key as the fallback).
  3. **`src/Kumunita.Web/Views/Posts/Index.cshtml`** (modify) — the Community
     surface's `<h1>` (rendered by `PostsController.AllSections` →
     `View("Index")`) resolves its label via the `GetLabelAsync` helper (the
     `nav.community` key as the fallback).
  4. **`src/Kumunita.Web/Views/Groups/Index.cshtml`** (modify) — the Groups
     surface's `<h1>` resolves its label via the `GetLabelAsync` helper (the
     `nav.groups` key as the fallback).
- **Exit:** `run_build` on `Kumunita.Web` green. The 4 flat-row surface headers
  resolve their label via the `GetLabelAsync` helper. Handoff note: a
  `## U06 — flat-row headers` section — (a) the 4 surface headers (by view
  path) + the `kw-l` key each uses, (b) the `GetLabelAsync` call shape, (c) the
  SITE hero (untouched — M29·10), (d) any compile warnings. Move this unit plan
  `in-progress/` → `done/` (move **last**).

### U07 — Web: wire the 9 More-dropdown surface headers (Events, Projects, Inventory, Bookmarks, Documents, Pages, Tags, Directory, People)

- **Goal:** wire the 9 More-dropdown surface page headers (`<h1>`) — Events,
  Projects, Inventory, Bookmarks, Documents, Pages, Tags, Directory, People —
  to the same label resolver (the `GetLabelAsync` helper — override → `kw-l`
  fallback). **No admin surface (U08).**
- **Entry reads:** U06's handoff-note `## U06 — flat-row headers` section (the
  `GetLabelAsync` call shape),
  `src/Kumunita.Web/Views/Shared/_Layout.cshtml` (the 9 More-dropdown nav items
  — the `kw-l` keys + the surface routes),
  `src/Kumunita.Web/Views/Event/Index.cshtml` + `src/Kumunita.Web/Views/Projects/TodosIndex.cshtml`
  + `src/Kumunita.Web/Views/Inventory/List.cshtml` + `src/Kumunita.Web/Views/Bookmarks/Index.cshtml`
  + `src/Kumunita.Web/Views/Document/Index.cshtml` + `src/Kumunita.Web/Views/Page/Index.cshtml`
  + `src/Kumunita.Web/Views/Tag/Index.cshtml` + `src/Kumunita.Web/Views/Directory/Index.cshtml`
  + `src/Kumunita.Web/Views/FindPeople/Index.cshtml` (the 9 More-dropdown
  surface headers — the exact `<h1>` each uses today; the view folders are
  singular (`Event` / `Document` / `Page` / `Tag`), and Inventory's list is
  `Inventory/List.cshtml`),
  `src/Kumunita.Core/SurfaceLabels/SurfaceLabelsService.cs` (the `GetLabelAsync`
  resolver seam the views call),
  `docs/design/m29-admin-surface-labels-design.md` §2.3 (the read-seam contract
  + the M29·1 consistency rule).
- **Deliverables (≤ 4 files, modify — the 9 surface headers, one-line-per-view):**
  1. **`src/Kumunita.Web/Views/Event/Index.cshtml`** (modify) — the Events
     surface's `<h1>` resolves its label via the `GetLabelAsync` helper (the
     `nav.events` key as the fallback).
  2. **`src/Kumunita.Web/Views/Projects/TodosIndex.cshtml`** (modify) — the
     Projects surface's `<h1>` resolves its label via the `GetLabelAsync`
     helper (the `nav.projects` key as the fallback).
  3. **`src/Kumunita.Web/Views/Inventory/List.cshtml`** (modify) — the
     Inventory surface's `<h1>` resolves its label via the `GetLabelAsync`
     helper (the `inv.nav` key as the fallback).
  4. **`src/Kumunita.Web/Views/Bookmarks/Index.cshtml`** (modify) — the
     Bookmarks surface's `<h1>` resolves its label via the `GetLabelAsync`
     helper (the `bm.nav` key as the fallback).
  5. **`src/Kumunita.Web/Views/Document/Index.cshtml`** (modify) — the
     Documents surface's `<h1>` resolves its label via the `GetLabelAsync`
     helper (the `documents.title` key as the fallback).
  6. **`src/Kumunita.Web/Views/Page/Index.cshtml`** (modify) — the Pages
     surface's `<h1>` resolves its label via the `GetLabelAsync` helper (the
     `nav.pages` key as the fallback).
  7. **`src/Kumunita.Web/Views/Tag/Index.cshtml`** (modify) — the Tags
     surface's `<h1>` resolves its label via the `GetLabelAsync` helper (the
     `nav.tags` key as the fallback).
  8. **`src/Kumunita.Web/Views/Directory/Index.cshtml`** (modify) — the
     Directory surface's `<h1>` resolves its label via the `GetLabelAsync`
     helper (the `nav.directory` key as the fallback).
  9. **`src/Kumunita.Web/Views/FindPeople/Index.cshtml`** (modify) — the
     People surface's `<h1>` resolves its label via the `GetLabelAsync` helper
     (the `nav.people` key as the fallback).
- **Exit:** `run_build` on `Kumunita.Web` green. The 9 More-dropdown surface
  headers resolve their label via the `GetLabelAsync` helper. Handoff note: a
  `## U07 — More-dropdown headers` section — (a) the 9 surface headers (by view
  path) + the `kw-l` key each uses, (b) the `GetLabelAsync` call shape, (c) any
  compile warnings. Move this unit plan `in-progress/` → `done/` (move
  **last**).

### U08 — Web: the `/admin/labels` GlobalAdmin surface + the `/admin/platform` link + the admin-surface `kw-l` keys

- **Goal:** ship the `/admin/labels` surface (the GlobalAdmin's edit page — the
  13-row form, one text input per surface key, blank = use the default label;
  the `AdminSurfaceLabelsController` + the `AdminSurfaceLabels` view + the
  `/admin/platform` link + the admin-surface `kw-l` keys). **No test (U09).**
- **Entry reads:** U07's handoff-note `## U07 — More-dropdown headers` section
  (the `GetLabelAsync` call shape),
  `src/Kumunita.Web/Controllers/AdminSiteController.cs` (the ADR 0150
  `/admin/site` surface — the `GlobalAdmin`-gated dedicated controller + the
  `AdminSiteViewModel` + the `/admin/platform` link shape),
  `src/Kumunita.Core/SurfaceLabels/ISurfaceLabelsService.cs` (the
  `ISurfaceLabelsService` seam — the `GetLabelAsync` / `SaveAsync` shape the
  controller wraps),
  `src/Kumunita.Web/Views/AdminSite/Index.cshtml` (the ADR 0150 `/admin/site`
  view — the form shape to mirror),
  `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the admin-surface
  `kw-l` key families — the `admin.*` / `site.*` keys to mirror for the
  `labels.*` keys),
  `docs/design/m29-admin-surface-labels-design.md` §2.4 (the write-lane
  contract + the `/admin/labels` surface shape).
- **Deliverables (≤ 4 files, new / modified):**
  1. **`src/Kumunita.Web/Controllers/AdminSurfaceLabelsController.cs`** (new) —
     the `/admin/labels` surface. `[Route("admin/labels")]` +
     `[Authorize(Roles = Kumunita.Core.Identity.Roles.GlobalAdmin)]` on a new
     `AdminSurfaceLabelsController(ISurfaceLabelsService labels) : Controller`.
     The GET seeds the form with the current singleton (the
     `ISurfaceLabelsService.GetAsync` shape — best-effort, the all-null
     fallback); the POST saves the full 13-field set (one `AccessAudit` row,
     the `site.save` shape — the `AdminSiteController.SaveHome` shape). A blank
     field **clears** that surface's label (falls back to the `kw-l` key).
     The `AdminSurfaceLabelsViewModel` (the 13 `string?` fields, the
     `AdminSiteViewModel` shape).
  2. **`src/Kumunita.Web/Views/AdminSurfaceLabels/Index.cshtml`** (new) — the
     `/admin/labels` view (the `AdminSiteController` → `Views/AdminSite/Index`
     folder-per-controller convention). A 13-row form (one text input per
     surface key, blank = use the default label) + a single Save button (the
     `AdminSite/Index` view's form shape). The `@model` directive matches the
     `AdminSurfaceLabelsViewModel` exactly.
  3. **`src/Kumunita.Web/Views/Admin/Platform.cshtml`** (modify, if the
     `/admin/platform` page is a separate view) — add one list-group row
     linking to `/admin/labels` (the `SP U03` / `ADR 0050` discoverability
     pattern — the "Surface labels" row).
  4. **`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`** (modify) —
     add the admin-surface `kw-l` keys (the `labels.*` key family — the
     `labels.title` / `labels.save` / `labels.reset` keys + the 13 surface-key
     labels) in **en/de/fr/da** (the parity pins move together — the
     `KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests` pins are
     updated to include the new keys).
- **Exit:** `run_build` on `Kumunita.Web` green. The `/admin/labels` surface
  is present; the `/admin/platform` link is added; the admin-surface `kw-l`
  keys are in the registry. Handoff note: a `## U08 — /admin/labels surface`
  section — (a) the controller + view paths, (b) the `/admin/platform` link
  location, (c) the 4 admin-surface `kw-l` keys (× 4 languages), (d) any
  compile warnings. Move this unit plan `in-progress/` → `done/` (move
  **last**).

### U09 — Tests + run + record the acceptance gate

- **Goal:** author the seam tests (the `SurfaceLabelsServiceTests` /
  `SurfaceLabelsSeederTests` / `AdminSurfaceLabelsControllerTests` /
  `SurfaceLabelResolutionTests` classes — the 20 pinned test names from the
  design doc §2.5) + run + record the acceptance gate (the
  `dotnet build Kumunita.slnx -c Debug` green + the two in-process test runs
  green + the registry parity pins green + the SITE precedent pins green + the
  `MilestonesTests` / `WhatsNewTests` pins green) + record it as the
  `### Run result (M29 acceptance gate — <date>)` section appended to the
  design doc. **No close flip (U10).**
- **Entry reads:** U08's handoff-note `## U08 — /admin/labels surface` section
  (the controller + view paths + the admin-surface `kw-l` keys),
  `docs/design/m29-admin-surface-labels-design.md` §2.5 (the 20 pinned test
  names, exact — the *primary* source for this unit) + §2.6 (the acceptance
  gate), `tests/Kumunita.Core.Tests/SurfaceLabelsSeederTests.cs` (U04's seeder
  pin — the harness shape to mirror), `tests/Kumunita.Core.Tests/SiteContentServiceTests.cs`
  (the SITE precedent seam tests — the shape to mirror),
  `tests/Kumunita.Web.Tests/AdminSiteControllerTests.cs` (the SITE precedent
  Web tests — the shape to mirror).
- **Deliverables (4 files, 3 new + 1 modify):**
  1. **`tests/Kumunita.Core.Tests/SurfaceLabelsServiceTests.cs`** (new) — the
     8 Core seam pins (the `GetAsync_MissingStore_ReturnsAllNullFallback` /
     `GetAsync_MissingRow_ReturnsAllNullFallback` /
     `SaveAsync_WritesOneAccessAuditRow` /
     `SaveAsync_StrongConsistency_LiveOnNextGetAsync` /
     `SaveAsync_UpsertsSingleton_NoDuplicateRow` /
     `SaveAsync_BlankLabel_StoredBlank_FallsBackAtResolution` /
     `GetLabelAsync_ReturnsOverrideWhenSet` /
     `GetLabelAsync_ReturnsNullWhenNotSet` pins).
  2. **`tests/Kumunita.Web.Tests/AdminSurfaceLabelsControllerTests.cs`** (new) —
     the 4 Web controller pins (the `GET_SeesCurrentSingleton` /
     `POST_Save_SavesLabel_WritesOneAccessAuditRow` /
     `POST_NonGlobalAdmin_IsDenied` / `GET_FreshInstance_AllLabelsBlank` pins).
  3. **`tests/Kumunita.Web.Tests/SurfaceLabelResolutionTests.cs`** (new) — the
     5 Web resolution pins (the `FreshInstance_NavShowsKwLText` /
     `SavedLabel_NavShowsOverride` / `SavedLabel_HeaderShowsOverride` /
     `SavedLabel_SameLabelNavAndHeader` / `BlankLabel_FallsBackToKwL` pins).
  4. **`docs/design/m29-admin-surface-labels-design.md`** (modify) — append
     `### Run result (M29 acceptance gate — <date>)`: the three-test gate
     (closed-loop / handoff / part-vs-whole) + the 20-test count (from U09) +
     the registry parity pin status + the SITE precedent pin status + the
     `MilestonesTests` / `WhatsNewTests` pin status.
- **Exit:** `run_build` green. `run_test(s)` on the 4 test classes reports
  **20 tests discovered, 20 executed** — the pass/red status of each is
  recorded, and the `### Run result (M29 acceptance gate — <date>)` section is
  appended to the design doc (from U09's own deliverable 4). **No close
  flip** (U10). Handoff note: a `## U09 — tests + gate` section — (a) the 3
  new test file paths, (b) the 20 test names (verbatim), (c) the 20 pass/red
  counts, (d) the gate status (green / red) + the registry parity pin status +
  the SITE precedent pin status. Move this unit plan `in-progress/` → `done/`
  (move **last**).

### U10 — Close: the six-member close flip + the final handoff section

- **Goal:** flip the close — the `Milestones.cs` / README / `STATUS.md` /
  `ARCHITECTURE.md` / `MilestonesTests.cs` / `WhatsNew.cs` six-member close
  flip (the AGENTS.md "required sixth member" = `WhatsNew.cs`) — and write the
  **final handoff section** (the `## Summary` + the `## M29 — Closed (recorded)`
  design-doc close). **No code, no build.**
- **Entry reads:** U09's handoff-note `## U09 — tests + gate` section (the 20
  test names + the gate status), `docs/design/m29-admin-surface-labels-design.md`
  §2.6 (the acceptance gate — the status to record), `src/Kumunita.Web/Milestones.cs`
  (the M29 row — `StatusPlanned` → `StatusDone`), `README.md` (the Roadmap
  section — the M29 row), `docs/STATUS.md` (the detailed status line),
  `docs/ARCHITECTURE.md` (the `SurfaceLabels/` bounded-context line),
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the order +
  single-in-progress pin), `src/Kumunita.Web/WhatsNew.cs` (the `0.45.0` entry).
- **Deliverables (6 files, modify + 1 design-doc close):**
  1. **`src/Kumunita.Web/Milestones.cs`** (modify) — the M29 row
     `StatusPlanned` → `StatusDone`.
  2. **`README.md`** (modify) — the Roadmap section's M29 row gets "**Done.**
     (ADR 0152)."
  3. **`docs/STATUS.md`** (modify) — the detailed status line (the M29 shipped
     line — the `SurfaceLabels` singleton + the 13 nav items + 13 surface
     headers + the `/admin/labels` surface).
  4. **`docs/ARCHITECTURE.md`** (modify) — the `SurfaceLabels/`
     bounded-context line flipped to **M29 ✓ live** with the gate summary.
  5. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** (modify) — the two
     status-pin methods updated: `Shipped_Milestones_Are_Marked_Done` adds
     `"M29"` to the done set, and
     `No_Milestone_Is_InProgress_And_Planned_Milestones_Are_Marked_Planned`
     drops `"M29"` from the planned set (the planned set becomes
     `{M30, M31, M32, M33, M34}`; the done set grows to include M29; the
     order test is unchanged — M29 stays in the same position).
  6. **`src/Kumunita.Web/WhatsNew.cs`** (modify) — the `0.45.0` entry
     (newest-first, the `0.44.0` neighbor): "Admin surface labels — a
     GlobalAdmin edits the display name of each top-navigation item and the
     related page header follows the choice, so a rename stays consistent
     across the surface (the `SurfaceLabels` singleton on the SITE lane's
     shape — a per-item label, not a re-route; ADR 0152)."
  7. **`docs/design/m29-admin-surface-labels-design.md`** (modify) — append
     `## M29 — Closed (recorded)` — the three tests (from U09's record), the
     `ARCHITECTURE.md` flip (from U10), the six-member close flip (from U10),
     and the `LBL-2` deferral list (each named).
  8. **`docs/plans-milestones/in-progress/m29-handoff-notes.md`** (modify) —
     append `## Summary` — a table of the shipped units (U00–U10), with their
     one-liner goal + test count + any deviations + the `LBL-2` deferral list
     (each item named).
- **Exit:** no build. `Milestones.cs`'s M29 row is `StatusDone`; the README
  Roadmap M29 row is "**Done.** (ADR 0152)."; `STATUS.md` has the M29 line;
  `ARCHITECTURE.md`'s `SurfaceLabels/` line is flipped; `MilestonesTests.cs`
  is consistent; `WhatsNew.cs` has the `0.45.0` entry. The design doc ends
  with `## M29 — Closed (recorded)`. The handoff note's `## Summary` section
  is present. **Move the 11 unit plans + the handoff note from
  `in-progress/` to `done/m29/`** (the close move — the milestone is shipped;
  the register stays at the top level, `docs/plans-milestones/`).
