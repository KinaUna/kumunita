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
