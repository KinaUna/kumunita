# SITE U06 — Web: `StaticPagesController` reads `SiteContent` + `Views/StaticPages/About.cshtml` conditional rendering

## Context (self-contained)

`SITE` is the **site content customization** lane: the `/home` + `/about`
landing surfaces become admin-editable. U00–U05 locked the scope, the design
doc, ADR 0150, the Core, the seeder default, and the `/home` surface. **This
unit wires `/about`** — `StaticPagesController.About` reads the
`SiteContent` singleton (the best-effort read, the in-code fallback) and
passes it through the same `HomeViewModel.Site` field U05 added;
`Views/StaticPages/About.cshtml` renders the hero text from
`Model.Site.AboutHeroEyebrow` / `Model.Site.AboutHeroLead` and
conditionally renders the six non-hero sections (the feature cards, the
scope band, the FIG-philosophy band, the code/docs band, the "What's new"
changelog, and the contact CTA band). **No Core change, no `HomeController`
change, no `/admin/site` surface.**

**The `SiteContent` field set relevant to `/about` (8 fields):**
`AboutHeroEyebrow` (string, default = the `about.eyebrow` text),
`AboutHeroLead` (string, default = the `about.lead` text),
`AboutShowFeatures` (bool, default `true` — the three feature cards),
`AboutShowScope` (bool, default `true` — the scope band),
`AboutShowPhilosophy` (bool, default `true` — the FIG-philosophy band),
`AboutShowProject` (bool, default `true` — the code/docs band),
`AboutShowWhatsNew` (bool, default `true` — the "What's new" changelog),
`AboutShowContactCta` (bool, default `true` — the contact CTA band).

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The read is a **public landing surface** (world-readable, not an access
  decision, not a claim). A missing singleton degrades to the **shipped
  defaults** (byte-identical `kw-l` text + every section shown) — never a
  blank page. The in-code fallback is the `SiteContent` field defaults.
- The About hero (eyebrow + lead) is **always shown** (the hero is the page
  identity — it is not one of the six toggle-able sections; the two CTAs in
  the hero band stay). Only the **other** six sections are show/hide-able
  (the user's scope: "each of the other elements should have options to
  show or hide them").
- The `kw-l` registry entries **stay** (the view's `kw-l key=` for the
  hero eyebrow / lead is replaced by the server-resolved `SiteContent`
  value; the other `kw-l` keys the view uses for the sections that are
  shown stay).

## Goal

Wire `StaticPagesController.About` to read the `SiteContent` singleton
(the best-effort read, the in-code fallback) + pass it through the
`HomeViewModel.Site` field (the same field U05 added — the `HomeViewModel`
is the shared home/about view model); wire `Views/StaticPages/About.cshtml`
to render the hero text from `Model.Site.AboutHeroEyebrow` /
`Model.Site.AboutHeroLead` + conditionally render the six non-hero
sections. **No Core change, no `HomeController` change, no `/admin/site`
surface.**

## Entry reads (5 files)

1. `docs/design/site-content-design.md` §2.3 — the read-seam contract.
2. `src/Kumunita.Core/SiteContent/ISiteContentService.cs` (U03) — the
   `ISiteContentService` interface (the `GetAsync()` read).
3. `src/Kumunita.Web/Controllers/StaticPagesController.cs` — the `/about`
   controller (the `fallBackToProductStory` seam, the `HomeViewModel` shape,
   the `IPageService` / `ITranslationProvider` / `ILocalizationService` /
   `IHttpContextAccessor` / `IOptions<CommunityOptions>` seams).
4. `src/Kumunita.Web/Models/HomeViewModel.cs` (U05) — the `HomeViewModel`
   record (the `Site` field U05 added).
5. `src/Kumunita.Web/Views/StaticPages/About.cshtml` — the `/about` landing
   view (the hero, the three feature cards, the scope band, the
   FIG-philosophy band, the code/docs band, the "What's new" changelog, the
   contact CTA band).

## Deliverables (2 files, modified)

1. `src/Kumunita.Web/Controllers/StaticPagesController.cs` (modified) —
   inject `ISiteContentService?` as an optional constructor param (the
   `IOptions<CommunityOptions>` shape — the constructor is **not** pinned
   by a Web-layer test harness, unlike `AdminController`); the `About`
   action reads the `SiteContent` singleton (the best-effort read, the
   in-code fallback — the ADR 0050 / ADR 0149 best-effort shape:
   `try { site = await siteContent.GetAsync(); } catch { site = null; }`)
   and the `FallThrough` path passes it through the new
   `HomeViewModel.Site` field when it falls back to the product-story view.
   The `Page(slug, …)` path (an admin-created `about` `Page` wins) is
   **untouched** — only the product-story fallback view is wired to
   `SiteContent`.
2. `src/Kumunita.Web/Views/StaticPages/About.cshtml` (modified). The
   current markup has **seven** `<section>` blocks in order: (1) hero,
   (2) feature cards, (3) scope band, (4) FIG-philosophy band, (5) code/docs
   band, (6) "What's new" changelog, (7) contact CTA band. Refactor so:
   - **The hero** (`section.kmb-hero`) — **always shown** (not one of the
     six toggle-able sections; the two CTAs stay). Its two text lines are
     `@(Model.Site?.AboutHeroEyebrow ?? <in-code default>)` / `@(Model.
     Site?.AboutHeroLead ?? <in-code default>)` (the `kw-l` wrap for those
     two is replaced by the server-resolved `SiteContent` value; the `??`
     fallback is the `SiteContent` field default, so a null `Site` degrades
     to the shipped text).
   - **The feature cards** (`section.kmb-features`) — wrap in `@if
     (Model.Site?.AboutShowFeatures ?? true) { … }`.
   - **The scope band** (`section.kmb-section` with the `platform.scope_*`
     keys) — wrap in `@if (Model.Site?.AboutShowScope ?? true) { … }`.
   - **The FIG-philosophy band** (`section.kmb-section-tint-a` with the
     `platform.fig_*` keys) — wrap in `@if (Model.Site?.
     AboutShowPhilosophy ?? true) { … }`.
   - **The code/docs band** (`section.kmb-section` with the
     `about.project.*` keys + the `RepositoryInfo.Links` loop) — wrap in
     `@if (Model.Site?.AboutShowProject ?? true) { … }`.
   - **The "What's new" changelog** (`section#whats-new` with the
     `whatsnew.*` keys) — wrap in `@if (Model.Site?.AboutShowWhatsNew ??
     true) { … }`.
   - **The contact CTA band** (`section.kmb-cta-band`) — wrap in `@if
     (Model.Site?.AboutShowContactCta ?? true) { … }`.
   The `<fallback>` default for each hero text line is the exact `en`
   source text from `KnownTranslationKeys.cs` (the `about.eyebrow` /
   `about.lead` values) — this is the in-code floor a null `Site` degrades
   to (SITE·3).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- Handoff note: a `## U06 — Web: StaticPagesController + About view
  conditional rendering` section listing the 2 files (by path) + the
  `StaticPagesController.About` read seam + the `Views/StaticPages/
  About.cshtml` `@if` wraps + the 8 `About` fields (by name) + the
  "About hero is always shown" pin.
- **No Core change, no `HomeController` change, no `/admin/site` surface.**
- Move this unit plan `in-progress/site-u06.md` → `done/site-u06.md` (move
  **last**).
