# SITE U01 — Design doc Part 1 (invariants + FACES)

## Context (self-contained)

`SITE` is the **site content customization** lane: the `/home` + `/about`
landing surfaces become admin-editable (the two heroes' eyebrow + lead are
editable text; every other section is show / hide). The defaults are
byte-identical to the shipped `kw-l` text. The precedent shapes are ADR
0005 B (`LocaleSettings` singleton), ADR 0019 / ADR 0020 (singleton-toggle
admin surface), ADR 0050 (single-write-lane), and ADR 0149 (the
per-resident `HideHomeIntro` preference — composable, not replaced).

**What this unit does:** author `docs/design/site-content-design.md` Part 1
— the value chain, the **invariants (SITE·1–SITE·9)**, the **FACES
(SITE1–SITE10)**, and the **assumptions**. Mirrors the
`wysiwyg-editor-design.md` / `pages-design.md` shape. **No code, no build.**

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The read is a **public landing surface** (world-readable, not an access
  decision, not a claim). A missing singleton degrades to the shipped
  defaults (byte-identical `kw-l` text + every section shown) — never a
  blank page.
- The write is the **ADR 0050 single-write-lane shape** (one
  `ISiteContentService.SaveAsync`, one `AccessAudit` row per save,
  `Via = Admin`, action `site.save`, `TargetKind` "site").
- The defaults are **byte-identical to the shipped `kw-l` text**; the
  `kw-l` registry entries **stay**.
- ADR 0149 `Profile.HideHomeIntro` is **composable, not replaced** — the
  two intro sections render when **both** `!HideIntro` **and**
  `HomeShowFeatures`; the feed + roadmap are governed only by the platform
  flag.
- The `SiteContent` doc is a **singleton** (one row per instance), additive
  per ADR 0004 §B.1, **no EF migration**.

## Goal

Author `docs/design/site-content-design.md` Part 1 — the value chain, the
**invariants (SITE·1–SITE·9)**, the **FACES (SITE1–SITE10)**, and the
**assumptions** (the ADR 0150 landing-surface semantics + the ADR 0149
composability). **No code, no build.**

## Entry reads (5 files)

1. `docs/plans-milestones/in-progress/site-handoff-notes.md` — U00's
   `## U00 — Kickoff verified` section (the `kw-l` key list + the 13
   `SiteContent` fields + the ADR number).
2. `docs/design/wysiwyg-editor-design.md` — the WY design doc (the
   FACES/invariant template to emulate).
3. `docs/design/pages-design.md` — the PG design doc (the bounded-context
   shape).
4. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the
   `home.intro_eyebrow` / `home.intro_lead` / `about.eyebrow` /
   `about.lead` keys (the exact `en` source text the defaults are derived
   from) + the section-heading keys the views still use.
5. `docs/philosophy/templates/design-doc.md` — the required section set.

## Deliverables (1 file, new)

`docs/design/site-content-design.md` (~250 lines). Sections:

- `## Value chain` — the `SP` lane shipped the four static pages as
  admin-editable `Page` docs; the `LocaleSettings` singleton ships the
  admin-settled instance values; this lane ships the **landing-surface
  content** (the two heroes' eyebrow + lead + every section's show/hide)
  as a new `SiteContent` singleton.
- `## Context` — the gap (the two landing surfaces are hard-coded; no
  path to change the hero text or hide a section); the precedent shapes
  (ADR 0005 B / 0019 / 0020 / 0050 / 0149); the constraints that still bind
  (ADR 0004 §B.1 additive doc type, ADR 0006 module boundary, ADR 0015 D1
  `kw-l` provider-floor, the `LS` / `SP` / `PG` test model, the
  `Milestones.cs` / README / `MilestonesTests` close-flip trio).
- `## Scope` — **In:** the `SiteContent` doc (new `Kumunita.Core.SiteContent`
  context), `ISiteContentService` + `SiteContentService` (the ADR 0050
  shape), `SiteContentDocTypes` (the ADR 0004 §B.1 additive doc type), the
  `FirstBootSeeder` default (byte-identical), the two controllers' read
  seam (best-effort, in-code fallback), the two views' conditional
  rendering, the `/admin/site` surface, and the test pins. **Out (named
  deferrals for a future SITE-2 lane):** machine translation of the
  landing-surface copy (a `SiteContentTranslation` row shape), the
  per-section editable text (the feature cards / scope / FIG / code bands
  are show/hide only in this lane), the per-section custom CTA buttons,
  and the `Milestones.cs` / README / `MilestonesTests` trio until the lane
  *ships* (U08 owns it).
- `## Invariants (pinned for SITE)` — **SITE·1–SITE·9**, each with a one-
  line SITE note:
  - **SITE·1** — the read is a public landing surface (world-readable, not
    an access decision, not a claim — ADR 0001-B thin-token; ADR 0050
    strong-consistency shape).
  - **SITE·2** — the write is the ADR 0050 single-write-lane shape (one
    `SaveAsync` lane, one `AccessAudit` row, `Via = Admin`, action
    `site.save`, `TargetKind` "site").
  - **SITE·3** — the defaults are byte-identical to the shipped `kw-l`
    text (the `FirstBootSeeder` seed + the in-code fallback; the
    "seeded defaults match the shipped text" pin).
  - **SITE·4** — the `kw-l` registry entries stay (the `home.*` / `about.*`
    / `platform.*` keys are not removed / re-shaped / re-keyed — they are
    the canonical `en` source text the defaults are derived from; the
    registry parity tests are untouched).
  - **SITE·5** — the ADR 0149 `Profile.HideHomeIntro` is composable, not
    replaced (the two intro sections render when **both** `!HideIntro`
    and `HomeShowFeatures`; the feed + roadmap are governed only by the
    platform flag).
  - **SITE·6** — the `SiteContent` doc is a singleton (one row per
    instance, `Id = "singleton"` sentinel — the `LocaleSettings` shape;
    the delta is applied idempotently at boot; no EF migration).
  - **SITE·7** — the `/admin/site` surface is the ADR 0050 shape (a
    dedicated `AdminSiteController`, `GlobalAdmin`-gated, one
    `AccessAudit` row per save; the `/admin/platform` page gains one
    list-group row).
  - **SITE·8** — the `Milestones.cs` / README / `MilestonesTests` trio is
    untouched until the lane *ships* (U08 owns the close flip; the
    `MilestonesTests` pin that M4 is the single `StatusNext` stays intact).
  - **SITE·9** — a11y: a hidden section is **not in the DOM at all** (not
    `display: none`) — a screen reader never sees it; the two heroes'
    eyebrow + lead stay the first content in the DOM; the visible
    sections' `aria-label` attributes are unchanged.
- `## FACES (pinned, 10)` — **SITE1–SITE10**, each bound to an invariant:
  - **SITE1** — the read is a public landing surface (SITE·1, SITE·6)
  - **SITE2** — the write is the ADR 0050 single-write-lane shape (SITE·2)
  - **SITE3** — the defaults are byte-identical to the shipped `kw-l`
    text (SITE·3)
  - **SITE4** — the `kw-l` registry entries stay (SITE·4)
  - **SITE5** — the ADR 0149 composability (SITE·5)
  - **SITE6** — the `SiteContent` doc is a singleton (SITE·6)
  - **SITE7** — the `/admin/site` surface is the ADR 0050 shape (SITE·7)
  - **SITE8** — the close-flip trio is untouched until *ships* (SITE·8)
  - **SITE9** — a11y (SITE·9)
  - **SITE10** — the `SiteContent` field set is the complete admin surface
    for the current scope (the 13 fields — no hidden fields, no reserved
    fields; a future SITE-2 lane adds fields, it does not re-shape the
    existing ones)

## Exit

- The file exists with all sections. **No build.**
- Handoff note: a `## U01 — design doc Part 1` section listing the **9
  invariants** (by id) and the **10 FACES** (SITE1–SITE10) so U02 can pin
  them by id.
- Move this unit plan `in-progress/site-u01.md` → `done/site-u01.md` (move
  **last**).
