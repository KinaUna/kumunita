# ADR 0152 — Admin surface labels (a GlobalAdmin edits the display name of each top-navigation item and the related page header follows the choice)

Status: Accepted
Date: 2026-10-08

## Context

The platform's **13 top-navigation items** — Home · Announcements ·
Community · Groups in the flat row, plus Events · Projects · Inventory ·
Bookmarks · Documents · Pages · Tags · Directory · People in the "More"
dropdown and the mobile rail — **and each of their page headers** are the
**labels a resident sees on every page**, and they are **hard-coded**: every
one is a `kw-l`-wrapped English string (the `nav.home` /
`nav.announcements` / `nav.community` / `nav.groups` / `nav.events` /
`nav.projects` / `inv.nav` / `bm.nav` / `documents.title` / `nav.pages` /
`nav.tags` / `nav.directory` / `nav.people` key families in
`KnownTranslationKeys.cs`) rendered in `Views/Shared/_Layout.cshtml` and in
each surface's index view.

An admin who wants the platform to say "News" instead of
"Announcements," or "Board" instead of "Projects," or "Directory" instead
of "People," has **no path short of a code change** — and a rename that only
touches the nav, leaving the page header on the old word, would be
*inconsistent* (the nav and the header would disagree).

The SITE lane (ADR 0150) shipped the two **landing surfaces' hero copy +
section toggles** as a `SiteContent` singleton — an admin can now change
what the home and about heroes say, and which sections appear — but the
**13 nav items + 13 surface headers** are still fixed. This milestone
completes the arc from the admin's point of view, one arrow further: the
admin can now **name the platform's own surface**.

## Decision

- A new **platform-level singleton** `SurfaceLabels` (a **new bounded
  context** `Kumunita.Core.SurfaceLabels`, a new `SurfaceLabelsDocTypes`
  surface, a new `ISurfaceLabelsService` seam) carries **exactly 13
  optional `string?` label fields** (one per surface key — `Home` /
  `Announcements` / `Community` / `Groups` / `Events` / `Projects` /
  `Inventory` / `Bookmarks` / `Documents` / `Pages` / `Tags` / `Directory` /
  `People`), **all defaulting to `null`** (`null` = "use the `kw-l`
  fallback" — the "empty = use default" shape). The `Id` is the sentinel
  `"singleton"` (the exact `SiteContent` / `LocaleSettings` shape, ADR
  0005 B — one row per instance). The field set is the **ceiling** (D1): a
  future `LBL-2` lane **adds** fields (additive per ADR 0004 §B.1), it does
  **not** re-shape the existing 13.
- **The label is a display override, never a re-route** (D2). The 13 nav
  routes (`/`, `/announcements`, `/community`, `/groups`, `/events`,
  `/projects/todos`, `/inventory`, `/bookmarks`, `/documents`, `/pages`,
  `/tags`, `/directory`, `/people`) are **unchanged** — a rename never moves
  a link, never changes a route, never touches an `asp-route-*` / `href`.
  Only the **text** the nav item and the surface's `<h1>` show changes.
  **A rename stays consistent across the surface**: the resolved label is
  the **same** in the nav (the flat row, the More dropdown, and the mobile
  rail) **and** in the surface's `<h1>` page header (one resolver, one
  value).
- **The read is a public surface** (D3). The label resolution is
  **world-readable**, **not** an access decision, **not** a claim (the ADR
  0001-B thin-token rule), and **never audited**. A missing `SurfaceLabels`
  row (a fresh boot before the seeder ran, or a test construction with no
  store) degrades to **all-null** → every label falls back to its `kw-l` key
  (byte-identical to today) — never a blank nav, never an error. The nav +
  header views call **one** helper (the label-resolver seam) so they resolve
  to the **same** value.
- **The write is the ADR 0150 single-write-lane shape** (D4). One
  `ISurfaceLabelsService.SaveAsync(labels, actorBy)` lane that loads the
  singleton, applies the full 13-field set, and saves in one session
  (invariant C3); exactly **one** `AccessAudit` row per save (`Via = Admin`,
  action `surface_labels.save`, `TargetKind` "surface-labels" — the
  `site.save` / `signup.set-open` / `timezone.set-default` singleton-toggle
  shape). The lane **upserts** the singleton — it never creates a second row
  (D6). Strong consistency (invariant C4): the new value is live on the very
  next `GetLabelAsync` / render.
- **The defaults are byte-identical to the shipped `kw-l` text** (D5). The
  `SurfaceLabels` default is **all-null** (every label unset → every one
  falls back to its `kw-l` key); the `FirstBootSeeder` seed is **all-null**,
  and the in-code fallback is the **all-null** `SurfaceLabels` (the
  byte-identical `kw-l` text, via the fallback). An admin who never touches
  the surface sees exactly what a fresh instance ships today. The `kw-l`
  registry entries (`nav.*` / `inv.nav` / `bm.nav` / `documents.title`) are
  **not** removed, not re-shaped, not re-keyed — they remain the **fallback**
  the in-code default resolves to; the registry parity tests
  (`KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests`) are
  **untouched**.
- **The `SurfaceLabels` doc is a singleton (one row per instance)** (D6).
  The `Id` is the sentinel `"singleton"` (the exact `SiteContent` /
  `LocaleSettings` shape, ADR 0005 B). The doc is registered in a new
  `SurfaceLabelsDocTypes.Configure(opts)` surface (the `SiteContentDocTypes`
  parallel surface, ADR 0004 §B.1); the delta is applied idempotently at
  boot. **No EF migration** (a new Marten doc type is additive per ADR 0004
  §B.1; the existing `SiteContent` + `LocaleSettings` docs are **not**
  modified — this lane adds a *new* doc in a *new* context, because the
  surface-label store is a distinct bounded concern, and the ADR 0006
  module-boundary contract keeps the contexts independent).
- **The `/admin/labels` surface is the ADR 0150 shape** (D7). A **dedicated**
  `AdminSurfaceLabelsController` (the `AdminController`'s constructor is
  pinned by two Web-layer test harnesses, so a new dependency there would
  break them; a separate `/admin/labels` surface mirrors `/admin/site` /
  `/admin/signup` / `/admin/timezone`). `[Route("admin/labels")]` +
  `[Authorize(Roles = GlobalAdmin)]` + one form section (the 13 fields —
  small enough for one save, one Save button). The GET seeds the form with
  the current singleton (the `ISurfaceLabelsService` best-effort read); the
  POST saves the full 13-field set (one `AccessAudit` row, the `site.save`
  shape, the `TempData["info"]` flash, the `RedirectToAction(nameof(Index))`
  redirect). A blank field **clears** that surface's label (falls back to
  the `kw-l` key). The `/admin/platform` page gains one list-group row
  linking to `/admin/labels` (the `SP U03` / `ADR 0050` discoverability
  pattern).
- **Single-string labels, not per-language** (D8). Each label is **one**
  admin-set string (a literal override), shown in **all** languages when
  set. The per-language label is a **named deferral** (a future `LBL-2`
  lane would add a `SurfaceLabelTranslation` row shape — the
  `PageTranslation` / `PostTranslation` precedent — + a `/admin/labels`
  translation editor) — the exact SITE lane D1 "single string, translation
  deferred" shape.
- **The `Milestones.cs` / README / `MilestonesTests` trio is untouched
  until the milestone *ships*** (D9). The `MilestonesTests` pin that the
  single in-progress milestone is unchanged stays intact through U00–U09.
  When the milestone ships (the close unit, U10), the `Milestones.cs`
  registry flips the M29 row to `StatusDone`, the README Roadmap flips the
  M29 row to "**Done.** (ADR 0152).", `STATUS.md` / `ARCHITECTURE.md` flip
  the M29 line, `MilestonesTests.cs` adds M29 to the done set and drops it
  from the planned set, and the `WhatsNew.cs` registry gains one new entry
  (newest-first, the `0.45.0` row) naming M29 + this ADR — the M27 "shipped
  with no entry until caught in review" lesson (AGENTS.md) is held.

## Consequences

- The **13 nav items + 13 surface headers** are now **admin-editable** —
  the admin can rename any surface (e.g. "Announcements" → "News"), and the
  rename stays **consistent across the surface** (the nav and the header
  agree). A fresh instance that never touches the surface looks **exactly**
  the same as it does today (the defaults are byte-identical to the shipped
  `kw-l` text, all-null).
- The `/admin/labels` surface is the GlobalAdmin's edit page — a dedicated
  `AdminSurfaceLabelsController` in the ADR 0150 shape, with one
  `AccessAudit` row per save (the `site.save` / `signup.set-open` /
  `timezone.set-default` singleton-toggle shape). The `/admin/platform`
  page gains one list-group row linking to it.
- The `kw-l` registry entries (`nav.*` / `inv.nav` / `bm.nav` /
  `documents.title` keys in `KnownTranslationKeys.cs`) are **untouched** —
  they remain the canonical `en` source text the fallback resolves to, and a
  future `LBL-2` translation lane (out of scope) would add a
  `SurfaceLabelTranslation` row shape keyed on the same strings.
- The `SiteContent` + `LocaleSettings` docs are **untouched** — this lane
  adds a *new* doc in a *new* context, not a new field on an existing one
  (the ADR 0006 module-boundary contract keeps the contexts independent).
- The `Milestones.cs` / README / `MilestonesTests` trio is **untouched**
  until the milestone *ships* (the close unit, U10, owns the flip); the
  `WhatsNew.cs` registry gains one new entry (the `0.45.0` row,
  newest-first) naming M29 + this ADR.
- **No EF migration** (a new Marten doc type is additive per ADR 0004 §B.1;
  the delta is applied idempotently at boot). **No route change for the
  read** (the 13 existing nav routes are unchanged — only what they render
  changes). **One new route for the write**: `/admin/labels` (the
  GlobalAdmin's edit surface, mirroring `/admin/site` / `/admin/signup` /
  `/admin/timezone`). **No new `AccessAction` / `AccessVia` / authorization
  path** (the read is a public surface; the write is the ADR 0150
  `GlobalAdmin`-gated single-write-lane shape, one `AccessAudit` row per
  save).
- **M30–M34 stay as-is** — no roadmap letter moves; M29 stays the milestone
  it is.
