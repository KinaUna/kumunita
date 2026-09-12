# U8 — Web views: avatar form + list + detail + preview renders

> Self-contained: this file + the **entry reads** below is the whole context.
> The serving endpoint is `docs/design/media-file-storage-design.md` §2.2
> (the `Avatar` action, from **U7**); the form target is §2.2 (the
> `AvatarUpload` action, from **U6**). A prior handoff section (if any) is
> **U7**.

## Understanding

Add the **surface** — the profile-edit **form** (a file input, self-only
upload target at U6's `AvatarUpload`), and the **render** surfaces (Directory
list/detail + Profile preview) that show the avatar `<img>` (served via U7's
`Avatar` endpoint, **never** a static path — C-MED·3). This is the last Web
unit before the seam tests (U9) and the governance close (U10).

## Assumptions

- **Every `<img>` `src`** is U7's `Avatar` endpoint — **never** a static path
  (C-MED·3, §2.7 rule 5). A `<img>` `src` pointing at a static folder
  (e.g. `/media/...`) is a **fail-closed** design-doc violation.
- **The form's `action`** is U6's `AvatarUpload` — **never** a generic upload
  endpoint (C-MED·8, §2.7 rule 7 — the owner-scoped write lane).
- **The form's `enctype`** is `multipart/form-data` (the `IFormFile`
  boundary, C-MED·6 — Web-only).

## Approach

Add the profile-edit **form** (file input + a current-avatar preview) to the
edit view, and the avatar `<img>` to the Directory list/detail + Profile
preview. Both point at the U7 endpoint (serving) / U6 endpoint (upload). The
views are **thin** — the gate + audit are in the controller (C-MED·1/2), the
views render.

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.2 (the `Avatar` serving
  endpoint + the `AvatarUpload` form target) + §2.5 (the FACES M1–M6 rows,
  for the render contract) + §2.7 rule 5 (the **no-static-path** rule).
- `docs/plans-milestones/done/media-u6-plan.md` + `media-u7-plan.md`
  + the **U6** + **U7** handoff (the upload + serving endpoints to point at).
- `src/Kumunita.Web/Views/Profile/Edit.cshtml` — the existing form shape to
  extend (the file input + the current-avatar preview).
- `src/Kumunita.Web/Views/Directory/Index.cshtml` + `Detail.cshtml` +
  `src/Kumunita.Web/Views/Profile/Preview.cshtml` — the render surfaces.
- `src/Kumunita.Web/Models/ProfileEditViewModel.cs` + `DirectoryViewModel.cs`
  — the view-models to add the avatar fields to (if the views need them).

## Deliverables (≤ 5 files, modify/new)

- `src/Kumunita.Web/Views/Profile/Edit.cshtml` — the avatar form (file input
  + a current-avatar preview).
- `src/Kumunita.Web/Views/Directory/Index.cshtml` + `Detail.cshtml` — the
  avatar `<img>` (served via U7).
- `src/Kumunita.Web/Views/Profile/Preview.cshtml` — the avatar `<img>`.
- (optional) `src/Kumunita.Web/Models/ProfileEditViewModel.cs` +
  `DirectoryViewModel.cs` — the avatar fields (if the views need them).

## Risks & open questions

- **`<img>` `src` is U7's `Avatar` endpoint, never a static path** (C-MED·3,
  §2.7 rule 5). A `<img>` `src` at `/media/...` (or any static path) is a
  **fail-closed** design-doc violation — the **whole** serving feature is
  defeated (the gate + audit are bypassed).
- **The form's `action` is U6's `AvatarUpload`**, never a generic upload
  endpoint (C-MED·8, §2.7 rule 7). A generic-upload drift is a **fail-closed**
  design-doc violation.
- **The view-models** (`ProfileEditViewModel` / `DirectoryViewModel`): if the
  views need an `AvatarId` / `HasAvatar` / `AvatarUrl` field, add it — the
  view-model shape is **additive** (a C-MED·1 drift if reshaped). The unit
  chooses the minimal field (e.g. `HasAvatar` bool + `AvatarUrl` string, the
  URL is the U7 endpoint).

## Steps

1. Add the avatar form (file input + a current-avatar preview) to
   `Edit.cshtml`.
2. Add the avatar `<img>` to the Directory list/detail + Profile preview
   (every `src` is U7's `Avatar` endpoint, C-MED·3).
3. (optional) add the view-model fields if the views need them (additive
   only, C-MED·1).
4. `run_build` → green on `Kumunita.Web`; a resident can upload + see the
   avatar on the list/detail/preview surfaces (manual).
5. Append **`## U8`** to the handoff notes: the files touched, the form's
   `enctype` / `action` / `src` shapes (all pointing at U6/U7 endpoints,
   **never** a static path — C-MED·3), and the view-model fields added if
   any.
