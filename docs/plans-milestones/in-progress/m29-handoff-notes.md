# M29 — Admin surface labels — rolling handoff note

> **Milestone open (U00).** This is the **scratch tier** (rolling handoff
> note) of M29's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U10). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> - **Register** — `docs/plans-milestones/plan-m29-admin-surface-labels.md`
> - **Design doc (primary)** — `docs/design/m29-admin-surface-labels-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0152** (the next free number after 0151 — the ADR index in
>   `docs/adr/README.md` confirms 0151 is the current highest; U00 verifies
>   0152 is free)
> - **Scope** — the 13 top-navigation items (the flat row [Home · Announcements
>   · Community · Groups] + the More dropdown [Events · Projects · Inventory ·
>   Bookmarks · Documents · Pages · Tags · Directory · People]) + their page
>   headers become admin-editable via a `SurfaceLabels` singleton (the SITE
>   lane's shape, ADR 0150) + the `/admin/labels` GlobalAdmin surface. The
>   label is a **display override, never a re-route**; a rename stays
>   consistent across the surface (the nav and the header agree).
> - **Out of scope (→ a future `LBL-2` lane)** — per-language labels (a
>   `SurfaceLabelTranslation` row shape), per-label custom icons / glyphs, and
>   the `Milestones.cs` / README / `MilestonesTests` trio until the milestone
>   *ships* (U10 owns it).
> - **Frozen base (unchanged)** — ADR 0005 B `LocaleSettings` singleton shape,
>   ADR 0150 `SiteContent` shape, ADR 0050 single-write-lane shape, ADR 0004
>   §B.1 additive doc type, ADR 0006 module-boundary contract, ADR 0015 D1
>   `kw-l` provider-floor discipline, the SITE / M28 test model.

<!-- U00 appends its section below this line. One ## section per unit, in order
     (U00, U01, … U10). Never rewrite a prior section. -->

## U00 — Kickoff verified

- **13 nav items (surface key → route):** Home (``/``) · Announcements (``/announcements``) · Community (``/community`` via `Posts/AllSections`) · Groups (``/groups``) — flat row; Events (``/events``) · Projects (``/projects/todos``) · Inventory (``/inventory``) · Bookmarks (``/bookmarks``) · Documents (``/documents``) · Pages (``/pages``) · Tags (``/tags``) · Directory (``/directory``) · People (``/people``) — More dropdown **+** mobile rail (all 13 repeated).
- **`kw-l` key list (verbatim, in `KnownTranslationKeys.cs`, present in en/de/fr/da):** `nav.home`, `nav.announcements`, `nav.community`, `nav.groups`, `nav.events`, `nav.projects`, `inv.nav`, `bm.nav`, `documents.title`, `nav.pages`, `nav.tags`, `nav.directory`, `nav.people`.
- **ADR number:** **0152** — free. `docs/adr/README.md` line 163 ends at `| 0151 | Guardian time limits … |`; the `docs/adr/` directory's highest file is `0151-guardian-time-limits.md`; no `0152-*.md` on disk.
- **Precedent ADRs (all present, all Accepted):** **0150** `SiteContent` singleton shape (the exact `SiteContent` doc + `SiteContentService` best-effort read + `site.save`/`Via = Admin` single audited write-lane + `AdminSiteController` `[Route("admin/site")]` `[Authorize(Roles = GlobalAdmin)]` surface) · **0050** `IsSignupOpenAsync` best-effort shape + `SetSignupOpenAsync` single-write-lane shape · **0005 B** `LocaleSettings` singleton (the `Id = "singleton"` sentinel M29 mirrors).
- **SITE shapes verified against source:** `SiteContent.cs` (singleton sentinel `SingletonId = "singleton"`, 13-field ceiling, byte-identical `en` defaults) · `SiteContentService.cs` (`GetAsync` never throws / never null → in-code fallback; `SaveAsync` one session, doc + exactly one `AccessAudit` row commit together, no-op on missing row — the SITE·6 "never load-or-creates" pin) · `AdminSiteController.cs` (`[Route("admin/site")]` + `[Authorize(Roles = GlobalAdmin)]`, `site.save` action, one `AccessAudit` per save).
- **Drift check:** none. The 13 nav items in `_Layout.cshtml` (lines 198–249 flat row + More dropdown; lines 285–361 mobile rail) match the register's closed set exactly. The `kw-l` keys are the canonical `en` source text in `KnownTranslationKeys.cs` (en lines 144/164/196/351–357/2194/2224/2243; `nav.events`+`nav.projects` also present in de/fr/da). The `nav.more` key (the More dropdown's own label, line 223) is **not** one of the 13 (the register's closed set does not include it — a future LBL-2 lane would add it if desired).

## U01 — design doc Part 1

- **Design doc** — `docs/design/m29-admin-surface-labels-design.md` Part 1 authored (Value chain / Context / Scope / Invariants / FACES / Frozen base). ~340 lines. No code, no build.
- **10 invariants (by id):** M29·1 rename consistent across the surface (nav + `<h1>` agree) · M29·2 read is a public surface (world-readable, never audited) · M29·3 resolution rule (admin label if non-blank, else `kw-l` fallback) · M29·4 defaults byte-identical to shipped `kw-l` text (all-null → `kw-l` fallback; registry entries stay) · M29·5 write is the ADR 0150 single-write-lane shape (one `SaveAsync`, one `AccessAudit` row, `surface_labels.save`, `TargetKind` "surface-labels") · M29·6 `SurfaceLabels` doc is a singleton (`Id = "singleton"`, additive, no EF migration; `SiteContent`/`LocaleSettings` untouched) · M29·7 `/admin/labels` surface is the ADR 0150 shape (dedicated `AdminSurfaceLabelsController`, `GlobalAdmin`-gated, `/admin/platform` link) · M29·8 single-string labels, not per-language (per-language is a named deferral → `LBL-2`) · M29·9 a11y: resolved label in the same DOM position as the `kw-l` key; `aria-label`s unchanged; icons stay as shipped · M29·10 `Milestones.cs`/README/`MilestonesTests` trio untouched until ship (U10 owns the close flip); Home's hero stays under the SITE lane (M29 drives only the Home nav label).
- **10 FACES (by id):** M29-1 fresh instance shows every nav item + header with shipped `kw-l` text · M29-2 admin sets "Announcements" label → nav item **and** header both show the new label · M29-3 label shown in all languages (single-string override); `kw-l` key is the fallback only · M29-4 blank/whitespace label falls back to the `kw-l` key · M29-5 write is the ADR 0150 single-write-lane shape (one `AccessAudit` row, strong consistency) · M29-6 `SurfaceLabels` doc is a singleton (one row per instance) · M29-7 `/admin/labels` is `GlobalAdmin`-gated (non-`GlobalAdmin` denied) · M29-8 `kw-l` registry entries stay (parity tests untouched) · M29-9 a11y: resolved label in the same DOM position as the `kw-l` key today · M29-10 Home's hero stays under the SITE lane (M29 drives only the Home nav label).
