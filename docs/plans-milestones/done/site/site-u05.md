# SITE U05 — Web: `HomeController` reads `SiteContent` + `Views/Home/Index.cshtml` conditional rendering

## Context (self-contained)

`SITE` is the **site content customization** lane: the `/home` + `/about`
landing surfaces become admin-editable. U00–U04 locked the scope, the design
doc, ADR 0150, the Core, and the seeder default. **This unit wires
`/home`** — `HomeController.Index` reads the `SiteContent` singleton (the
best-effort read, the in-code fallback) and passes it through a new
`HomeViewModel.Site` field; `Views/Home/Index.cshtml` renders the hero text
from `Model.Site.HomeHeroEyebrow` / `Model.Site.HomeHeroLead` and
conditionally renders the three sections (the `@if (Model.Site.
HomeShowAboutButton)` / `@if (Model.Site.HomeShowFeatures)` /
`@if (Model.Site.HomeShowRoadmap)` wraps) with the ADR 0149 composability
(the two intro sections render when **both** `!Model.HideIntro` **and**
`Model.Site.HomeShowFeatures`). **No Core change, no
`StaticPagesController` change, no `/admin/site` surface.**

**The `SiteContent` field set relevant to `/home` (5 fields):**
`HomeHeroEyebrow` (string), `HomeHeroLead` (string), `HomeShowAboutButton`
(bool, default `true` — the home hero's "What it is & how it works"
button), `HomeShowFeatures` (bool, default `true` — the home's three
feature cards), `HomeShowRoadmap` (bool, default `true` — the home's
"The plan" roadmap section).

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The read is a **public landing surface** (world-readable, not an access
  decision, not a claim). A missing singleton degrades to the **shipped
  defaults** (byte-identical `kw-l` text + every section shown) — never a
  blank page. The in-code fallback is the `SiteContent` field defaults.
- The ADR 0149 `Profile.HideHomeIntro` is **composable, not replaced** —
  the two intro sections render when **both** `!Model.HideIntro` **and**
  `Model.Site.HomeShowFeatures`. The "What's new" feed + roadmap are
  governed **only** by the platform flag (`HomeShowRoadmap`), never by the
  per-resident preference.
- The `kw-l` registry entries **stay** (the view's `kw-l key=` for the
  hero eyebrow / lead is replaced by the server-resolved `SiteContent`
  value; the other `kw-l` keys the view uses for the sections that are
  shown stay).

## Goal

Wire `HomeController.Index` to read the `SiteContent` singleton (the
best-effort read, the in-code fallback) + pass it through a new
`HomeViewModel.Site` field; wire `Views/Home/Index.cshtml` to render the
hero text from `Model.Site.HomeHeroEyebrow` / `Model.Site.HomeHeroLead` +
conditionally render the three sections + the ADR 0149 composability.
**No Core change, no `StaticPagesController` change, no `/admin/site`
surface.**

## Entry reads (5 files)

1. `docs/design/site-content-design.md` §2.3 — the read-seam contract.
2. `src/Kumunita.Core/SiteContent/ISiteContentService.cs` (U03) — the
   `ISiteContentService` interface (the `GetAsync()` read).
3. `src/Kumunita.Web/Controllers/HomeController.cs` — the `/home`
   controller (the ADR 0149 `HideHomeIntro` read, the `HomeViewModel` shape,
   the `IOptions<CommunityOptions>` seam).
4. `src/Kumunita.Web/Models/HomeViewModel.cs` — the `HomeViewModel` record
   (the `CommunityName` / `SupportEmail` / `Feed` / `HideIntro` fields).
5. `src/Kumunita.Web/Views/Home/Index.cshtml` — the `/home` landing view
   (the hero band, the three feature cards, the "What's new" feed, the
   "The plan" roadmap section; the ADR 0149 `@if (!Model.HideIntro)` wrap).

## Deliverables (3 files, new/modify)

1. `src/Kumunita.Web/Models/HomeViewModel.cs` (modified) — the
   `HomeViewModel` record gains a new `SiteContent? Site` field (the default
   `null` — the in-code fallback is the `SiteContent` field defaults,
   byte-identical to the shipped `kw-l` text). Add `using
   Kumunita.Core.SiteContent;`.
2. `src/Kumunita.Web/Controllers/HomeController.cs` (modified) — the
   `Index` action reads the `SiteContent` singleton (the best-effort read,
   the in-code fallback — the ADR 0050 / ADR 0149 best-effort shape:
   inject `ISiteContentService?` as an optional constructor param, call
   `GetAsync()` in a `try/catch` that degrades to `null`, pass through the
   new `HomeViewModel.Site` field). The ADR 0149 `HideHomeIntro` read is
   **untouched**.
3. `src/Kumunita.Web/Views/Home/Index.cshtml` (modified). **The current
   markup has the hero band + the feature cards both inside one
   `@if (!Model.HideIntro) { … }` block** (the ADR 0149 wrap). Split it so
   each section is governed independently:
   - **The hero band** (`section.kmb-hero`) — stays inside `@if
     (!Model.HideIntro) { … }` (ADR 0149: a per-resident can hide the
     hero). Its two text lines are `@(Model.Site?.HomeHeroEyebrow ??
     <in-code default>)` / `@(Model.Site?.HomeHeroLead ?? <in-code
     default>)` (the `kw-l` wrap for those two is replaced by the
     server-resolved `SiteContent` value; the `??` fallback is the
     `SiteContent` field default, so a null `Site` degrades to the shipped
     text). The "What it is & how it works" button (`<a href="/about" …>`)
     is additionally wrapped in `@if (Model.Site?.HomeShowAboutButton ??
     true) { … }` (the hero itself is **not** hideable by the platform —
     only its button and its text are governed by `SiteContent`).
   - **The feature cards** (`section.kmb-features`) — move out of the
     combined block and wrap in `@if (!Model.HideIntro && (Model.Site?.
     HomeShowFeatures ?? true)) { … }` (the ADR 0149 composability: the
     cards render only when the per-resident preference is off **and** the
     platform flag is on).
   - **The "The plan" roadmap section** (`section.kmb-section-tint-a`) —
     wrap in `@if (Model.Site?.HomeShowRoadmap ?? true) { … }` (ADR 0149
     pin: the roadmap is **never** affected by the per-resident
     `HideIntro`; it is governed only by the platform flag).
   - **The "What's new" feed** section is **untouched** (governed only by
     `Model.Feed is { }` — the signed-in feed).
   The `<fallback>` default for each hero text line is the exact `en`
   source text from `KnownTranslationKeys.cs` (the `home.intro_eyebrow` /
   `home.intro_lead` values) — this is the in-code floor a null `Site`
   degrades to (SITE·3).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- Handoff note: a `## U05 — Web: HomeController + Home view conditional
  rendering` section listing the 3 files (by path) + the
  `HomeViewModel.Site` field + the `HomeController.Index` read seam + the
  `Views/Home/Index.cshtml` `@if` wraps + the ADR 0149 composability pin
  (the `!Model.HideIntro && (Model.Site?.HomeShowFeatures ?? true)` shape).
- **No Core change, no `StaticPagesController` change, no `/admin/site`
  surface.**
- Move this unit plan `in-progress/site-u05.md` → `done/site-u05.md` (move
  **last**).
