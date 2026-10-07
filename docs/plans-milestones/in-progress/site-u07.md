# SITE U07 — Web: `/admin/site` surface (the `AdminSiteController` + the `AdminSite` view + the `/admin/platform` link)

## Context (self-contained)

`SITE` is the **site content customization** lane: the `/home` + `/about`
landing surfaces become admin-editable. U00–U06 locked the scope, the design
doc, ADR 0150, the Core, the seeder default, and both landing views. **This
unit ships the `/admin/site` surface** — the GlobalAdmin's edit page:
`AdminSiteController` (the `GET` + the `POST /save-home` + the `POST
/save-about` actions, the ADR 0050 single-write-lane shape, the
`GlobalAdmin`-gated `[ValidateAntiForgeryToken]` POSTs, the `AccessAudit`
row per save), the `AdminSite` view (two form sections — Home + About, each
with its field group + save button, the `AdminSignupController`'s two-form
shape), and the `/admin/platform` link (the `AdminPlatform` view gains one
list-group row linking to `/admin/site`). **No Core change, no
`HomeController` change, no `StaticPagesController` change, no view
conditional-rendering change.**

**The `SiteContent` field set (13 fields, pinned — the ADR 0150 D1 ceiling):**
4 text fields (`HomeHeroEyebrow` / `HomeHeroLead` / `AboutHeroEyebrow` /
`AboutHeroLead`) + 9 toggle fields (`HomeShowAboutButton` /
`HomeShowFeatures` / `HomeShowRoadmap` / `AboutShowFeatures` /
`AboutShowScope` / `AboutShowPhilosophy` / `AboutShowProject` /
`AboutShowWhatsNew` / `AboutShowContactCta`).

**The one thing every unit must respect (SITE lane, locked in ADR 0150):**
- The write is the **ADR 0050 single-write-lane shape** (one
  `ISiteContentService.SaveAsync`, one `AccessAudit` row per save,
  `Via = Admin`, action `site.save`, `TargetKind` "site" — the
  `signup.set-open` / `timezone.set-default` shape).
- The `/admin/site` surface is a **dedicated `AdminSiteController`**
  (`[Route("admin/site")]` + `[Authorize(Roles = GlobalAdmin)]`) — the
  `AdminController`'s constructor is pinned by two Web-layer test harnesses
  (`AdminControllerBlockTests` / `AdminControllerMandatoryTests`), so a new
  dependency there would break them; a separate `/admin/site` surface
  mirrors `/admin/signup` / `/admin/timezone` / `/admin/dateformat`.
- The save is **partial** — saving the Home section does not touch the
  About section's fields, and vice versa (the `SiteContentService.SaveAsync`
  loads the singleton, applies only the submitted field group, saves — the
  ADR 0050 "one field per save" shape, extended to "one section per save").
- The field labels are **plain English** (the admin view's local convention
  — the `Platform.cshtml` / `AdminSignup/Index.cshtml` shape; an
  interpolated `kw-l` key would break the `KwLRegistryConsistencyTests`
  static key scan).

## Goal

Implement the `/admin/site` surface — the `AdminSiteController` (the `GET` +
the `POST /save-home` + the `POST /save-about` actions), the `AdminSite`
view (two form sections — Home + About), and the `/admin/platform` link
(one list-group row). **No Core change, no `HomeController` change, no
`StaticPagesController` change, no view conditional-rendering change.**

## Entry reads (5 files)

1. `docs/design/site-content-design.md` §2.4 / §2.5 — the write-lane
   contract + the pinned test names.
2. `src/Kumunita.Web/Controllers/AdminSignupController.cs` — the ADR 0050
   `/admin/signup` surface (the `GlobalAdmin`-gated single-write-lane shape,
   the `AccessAudit` row, the `TempData["info"]` flash, the
   `RedirectToAction` redirect, the two-form shape).
3. `src/Kumunita.Web/Views/AdminSignup/Index.cshtml` — the `/admin/signup`
   view (the form shape, the plain-English label convention).
4. `src/Kumunita.Web/Views/Admin/Platform.cshtml` — the `/admin/platform`
   page (the `SP U03` / `ADR 0050` discoverability pattern, the list-group
   row shape).
5. `src/Kumunita.Core/SiteContent/ISiteContentService.cs` (U03) — the
   `ISiteContentService` interface (the `GetAsync` / `SaveAsync` shape).

## Deliverables (3 files, new/modify)

1. `src/Kumunita.Web/Controllers/AdminSiteController.cs` (new) — the
   `AdminSiteController` (`[Route("admin/site")]` + `[Authorize(Roles =
   GlobalAdmin)]`, injecting `ISiteContentService`):
   - `GET /admin/site` (`Index`) — seeds the form with the current
     singleton (`ISiteContentService.GetAsync()`) + returns the `AdminSite`
     view (a view model with the 13 fields, grouped into Home + About).
   - `POST /admin/site/save-home` (`SaveHome`) — `[ValidateAntiForgeryToken]`
     — takes the 5 Home fields (`HomeHeroEyebrow` / `HomeHeroLead` /
     `HomeShowAboutButton` / `HomeShowFeatures` / `HomeShowRoadmap`), calls
     `ISiteContentService.SaveAsync` with **only** the Home field group
     applied (the About fields are loaded from the current singleton,
     unchanged), sets `TempData["info"]`, redirects to `Index`.
   - `POST /admin/site/save-about` (`SaveAbout`) — `[ValidateAntiForgeryToken]`
     — takes the 8 About fields (`AboutHeroEyebrow` / `AboutHeroLead` /
     `AboutShowFeatures` / `AboutShowScope` / `AboutShowPhilosophy` /
     `AboutShowProject` / `AboutShowWhatsNew` / `AboutShowContactCta`),
     calls `ISiteContentService.SaveAsync` with **only** the About field
     group applied, sets `TempData["info"]`, redirects to `Index`.
   - A public nested `AdminSiteViewModel` (the 13 fields, so the Razor view
     can bind to it — the `AdminSignupController.SignupAdminViewModel`
     shape).
2. `src/Kumunita.Web/Views/AdminSite/Index.cshtml` (new) — the `AdminSite`
   view. Two `<section>` blocks: **Home** (a `<form method="post"
   action="/admin/site/save-home">` with a text input for
   `HomeHeroEyebrow`, a `<textarea>` for `HomeHeroLead`, and three
   checkboxes for `HomeShowAboutButton` / `HomeShowFeatures` /
   `HomeShowRoadmap` + a save button) and **About** (a `<form method="post"
   action="/admin/site/save-about">` with a text input for
   `AboutHeroEyebrow`, a `<textarea>` for `AboutHeroLead`, and six
   checkboxes for the six About toggles + a save button). Each section has
   a `@if (TempData["info"])` flash. The field labels are plain English
   (the admin view's local convention). The two forms are independent (each
   POSTs only its own field group).
3. `src/Kumunita.Web/Views/Admin/Platform.cshtml` (modified) — the
   `/admin/platform` page gains one list-group row linking to `/admin/site`
   (the `SP U03` / `ADR 0050` discoverability pattern): `<a
   href="/admin/site" class="list-group-item list-group-item-action">` with
   `<div class="fw-semibold">Site content</div>` + `<small
   class="text-muted">Edit the landing surfaces' hero text and choose which
   sections are shown on the home and about pages.</small>`.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- Handoff note: a `## U07 — Web: /admin/site surface` section listing the
  3 files (by path) + the `AdminSiteController` action signatures (the
  `GET` / `POST /save-home` / `POST /save-about` shape) + the
  `AdminSiteViewModel` field list (the 13 fields) + the `AdminSite` view's
  two form sections + the `/admin/platform` link row.
- **No Core change, no `HomeController` change, no `StaticPagesController`
  change, no view conditional-rendering change.**
- Move this unit plan `in-progress/site-u07.md` → `done/site-u07.md` (move
  **last**).
