# GU U08 — Web: `Views/Guardian/*` + nav entry

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

The **Razor views** for the U07 `GuardianController` (the child list, the
per-child curation view, the add-a-child form) + **one** nav-bar entry under
`_AccountNav.cshtml`. The views render **only** the U07 view models
(membership **ids/names** + the suspend/unsuspend/approve/dissolve action
forms). **G·1 in the UI: no view links to, renders, or even names a child's
private content** — no posts, no profile body, no "view their feed" link. An
optional client-side confirm-guard for the destructive `dissolve` + `suspend`
POSTs (a small `wwwroot/js/site.js` addition) is in-scope; it is a UX nicety,
not a security gate (the server gate is the U07 route).

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **Parts affected** → the Web
   views (U01 pinned which views exist + the G·1 "no child-content" rule) —
   the *primary* source.
2. `docs/adr/0028-...md` §D G·1 (the load-bearing non-decision: guardian
   standing **never** reads the child's content — the UI must make that
   visible by its *absence*) — the ADR authority.
3. `src/Kumunita.Web/Views/Groups/Index.cshtml` (top ~60 lines) — the **view
   conventions**: `@model Kumunita.Web.Models. …ViewModel`; `ViewData
   ["Title"]`; the Bootstrap `row/col-md-8 col-lg-7` shell; the `<kw-l
   key="…">…</kw-l>` localization wrapper (the ML-UI string lane — every
   user-facing string goes through it); the `TempData["info"]` /
   `TempData["error"]` alert blocks; the action-form shape (`<form method
   ="post" action="@Url.Action("…","Guardian", new { … })">@Html
   .AntiForgeryToken()<button …>…</button></form>`). U08's views mirror this
   **exactly**.
4. `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` — the **nav entry**
   insertion point: the signed-in block (`@if (isAuthenticated) { … }`) where
   the `Profile` / `Settings` / `Admin` items live. U08 adds **one** item
   (mirroring the `Profile` item's `nav-item`/`nav-link` shape) — no new
   `is…` flag (every signed-in resident can reach their own children page;
   the *list* is empty for a non-guardian, which the `Index` view already
   handles).
5. `src/Kumunita.Web/Models/GuardianViewModels.cs` (U07) — the **four** VMs
   the views bind to: `ChildAccountItem`, `MembershipEditorModel`,
   `PendingInvitationItem`, `AddChildForm`. **Bind only these** — no Core
   doc leaks into a view (the `GroupViewModel` "the document itself is not a
   view model" rule).

## Deliverables (≤4 files)

### 1. `src/Kumunita.Web/Views/Guardian/Index.cshtml` (new)

- `@model Kumunita.Web.Models.ChildAccountItem[]` (or the list the `Index()`
  action returns — match U07). `ViewData["Title"]` + `<h1>`/`<p class
  ="text-muted">` shell (the `Groups/Index` voice).
- The `TempData["info"]`/`["error"]` alert blocks.
- The child list: one `list-group-item` per `ChildAccountItem` — `DisplayName`
  + a `Blocked` badge + a link to `Detail` (`Url.Action("Detail","Guardian",
  new { childId = item.ChildId })`) + **two** action forms:
  `suspend` / `unsuspend` (the `Blocked` state picks which shows, mirroring
  the m2b accept/decline pair's "the state picks the card" shape).
- The **add-a-child** link/button → `Url.Action("AddChild","Guardian")`
  (the `Groups/Index` "Create a group" button shape).
- **Empty state**: no children → the `Groups/Index` "alert alert-info" empty
  card (a non-guardian's page). **No** child-content surface anywhere.

### 2. `src/Kumunita.Web/Views/Guardian/Detail.cshtml` (new)

- `@model Kumunita.Web.Models.MembershipEditorModel`. The same shell.
- Three sections, **each a list of ids/names** (G·1 — no body content):
  - **Groups** (`Model.GroupIds`): each row a `remove` form
    (`Url.Action("CurateGroup","Guardian", new { childId, groupId, add =
    false })`) + the page-top "add a group" mini-form (a `groupId` input +
    `add = true`).
  - **Communities** (`Model.CommunityIds`): the same shape (U07's
    `CurateGroup` route family — if U07 exposed a community variant, bind to
    it; if not, note the gap in the handoff and render the community list
    read-only with a "curate from the admin shell" note).
  - **Pending invitations** (`Model.PendingInvitations`): one
    `PendingInvitationItem` row each with an **`approve`** form
    (`Url.Action("ApproveInvitation","Guardian", new { childId, groupId =
    item.GroupId })`).
- A **dissolve** form (the destructive action) at the bottom:
  `Url.Action("Dissolve","Guardian", new { childId })`, `btn-danger`, with a
  visible confirm-guard (see the `site.js` deliverable) + a
  `form-confirm` data-attribute the guard hooks.
- **No** link to the child's posts / profile body / feed (G·1).

### 3. `src/Kumunita.Web/Views/Guardian/New.cshtml` (new)

- `@model Kumunita.Web.Models.AddChildForm`. The `Groups/Create` form shell
  (the `@Html.AntiForgeryToken()`, the `form-control` inputs, the
  `validation-summary` for `ModelState`).
- The three `AddChildForm` fields (`DisplayName`, `Email`, `Password`) as
  Bootstrap inputs; the submit button posts to `Url.Action("AddChild",
  "Guardian")`.
- A `TempData["info"]`/`["error"]` block; a one-line reassurance that the
  child verifies their own email (the usual flow, G·4) — rendered via
  `<kw-l>`.

### 4. `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (modify)

- Add **one** `nav-item` inside the `@if (isAuthenticated) { … }` block,
  after the `Profile` item (or after `Settings`):

```
        <li class="nav-item">
            <a class="nav-link text-dark" asp-controller="Guardian" asp-action="Index"><kw-l key="nav.children">Children</kw-l></a>
        </li>
```

  (the `Profile` item's exact shape; a new `<kw-l>` key `nav.children` — the
  ML-UI string lane, add the key the same way `nav.profile` is catalogued. If
  the localization catalog is a file, add the key there; if it's DB-seeded,
  note the seed in the handoff and keep the fallback text "Children".)

### 5. `src/Kumunita.Web/wwwroot/js/site.js` (modify — **optional**, UX only)

- A ~10-line confirm-guard: intercept `submit` on forms carrying
  `[data-confirm]`; if the browser `confirm()` is declined, `preventDefault`.
  Add `data-confirm="…"` to the `suspend` + `dissolve` forms in the views
  above. **Do not** let this gate the security path — it is a UX nicety; the
  server gate is the U07 route.

## Exit

`dotnet build Kumunita.slnx -c Debug` green (views compile at build time — a
`@model` / `Url.Action` typo is a compile error). The three `Views/Guardian/*`
files render **only** the U07 VMs (membership ids/names + action forms); the
`_AccountNav` has exactly **one** new item (the `Profile` shape); **no view
links to or renders a child's content** (G·1). **No tests** (U10 pins the
VM data-shape tests). Handoff note (append): 5–7 lines starting
`## U08 — Views/Guardian + nav entry` — (a) the three view files + the one
they bind to (one line each), (b) the one new nav item (the `kw-l` key + the
catalog/seed note if any), (c) a confirmation **no view links to child
content** (G·1 — point at the absence), (d) the optional `site.js` guard
(added or not + why), (e) any compile warnings.
