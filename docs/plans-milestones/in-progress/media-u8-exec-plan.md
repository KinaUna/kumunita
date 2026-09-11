# U8 execution plan (working) — Web views: avatar form + list + detail + preview renders

> My (unit U8's) working plan for this pass. The **authoritative spec** is the
> U8 entry in `plan-media-file-storage.md` + the authored `media-u8-plan.md` +
> **design doc** `../../design/media-file-storage-design.md` **§2.4** (the
> `AvatarUpload` form target, lines 498–527) + **§2.3**/§2.2 (the `Avatar`
> (GET) serving endpoint the `<img>` `src` points at) + §2.1 FACES M1–M5 (the
> 404 rows the render surfaces must degrade to) + §2.7 rules 1/5. Prior
> handoff sections read: **U7** (latest — the serving-lane contract + the
> FACES M1–M6 mapping) and **U6** (the `AvatarUpload` action shape + the
> `enctype`/`[ValidateAntiForgeryToken]` boundary).

## Scope (in / out)

- **In (mine):** exactly the register's Deliverables set —
  **4 view files + 1 CSS file, no view-models, no controllers**:
  - `src/Kumunita.Web/Views/Profile/Edit.cshtml` — the avatar **form**
    (file input + current-avatar preview).
  - `src/Kumunita.Web/Views/Directory/Index.cshtml` — the avatar `<img>`
    per list row (subject = the row's `p.SubjectId`).
  - `src/Kumunita.Web/Views/Directory/Detail.cshtml` — the avatar `<img>`
    (subject = the route's `{subjectId}`, via `httpContextAccessor.HttpContext
   ?.Request.RouteValues["subjectId"]` — see resolution 2)
    resolution 2).
  - `src/Kumunita.Web/Views/Profile/Preview.cshtml` — the author's avatar
    `<img>` (the preview is the signed-in author's own profile — FACES M1).
  - `src/Kumunita.Web/wwwroot/css/site.css` — the `.avatar` /
    `.avatar-mono` presentation tokens (appended section; the `<img>` and
    the monogram fallback need a size/circle rule and a no-avatar glyph).
  - The standing per-unit additions: this exec-plan file, the `## U8`
    handoff section, and the Status-table U8 row flip.
- **Out:** `ProfileEditViewModel.cs` + `DirectoryViewModel.cs` (the register's
  *optional* view-model fields — **not needed**: the list row already carries
  `SubjectId`, the detail subject comes from the stable route, and the two
  self-avatars come from the signed-in principal. Touching the `Detail`
  record would also collide with its "exactly these fields" doc pin + force a
  `DirectoryController` edit — a file **outside** the Deliverables, §2.7
  rule 1). No controller edits (U6's `AvatarUpload` + U7's `Avatar` are
  frozen and already correct). U9's tests (I pin the FACES M1–M6 names + the
  U7a/b/c shapes for them, I do not write them). U10's docs. Drift-guard rule
  1: I modify **no** file outside the set above.

## Endpoint shapes I render against (pinned, verbatim from the shipped code)

```csharp
// U6 (ProfileController, frozen):  — the form's target
[HttpPost("/profile/avatar")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AvatarUpload([FromForm] IFormFile? file)
//   file is null/empty      -> 400  "Choose an image."
//   oversize (> 5 MiB)      -> 413
//   type not jpeg|png|webp|gif -> 415
//   success                 -> PutAsync(store-first) -> SetProfileAvatarAsync
//                              -> RedirectToAction("Edit")

// U7 (ProfileController, frozen):  — every <img> src
[HttpGet("/profile/avatar/{subjectId}")]
public async Task<IActionResult> Avatar([FromRoute] string subjectId)
//   M1 owner / M2 authorized -> 200 + stored Content-Type (+ nosniff)
//   M3 denied / M4 blocked / M5 unknown / no-avatar -> 404 (fail-closed)
```

So the form is: `method=post`, `action="/profile/avatar"` (literal — the
route is a path-attribute, so `Url.Action` is not the idiom here),
`enctype="multipart/form-data"` (the `IFormFile` boundary, C-MED·6), a
file input **named `file`** (binds `[FromForm] IFormFile? file`), and an
antiforgery token (`@Html.AntiForgeryToken()` — the action's
`[ValidateAntiForgeryToken]`). The `accept` hint mirrors the pinned C-MED·5
allowlist (`image/jpeg,image/png,image/webp,image/gif` — the
`MediaOptions.ResolvedAllowedTypes` default) + the `5 MiB` cap for the
`form-text`.

## Constraints I keep pinned while writing

- **Every `<img>` `src` is the U7 endpoint** `"/profile/avatar/" + subject` —
  **never** a static path (C-MED·3, §2.7 rule 5). A `<img>` at `/media/…`
  (or any static folder) defeats the whole serving feature (the gate + audit
  are bypassed).
- **The form's `action` is U6's `POST /profile/avatar`** — never a generic
  upload endpoint (C-MED·8, §2.7 rule 7). Self-only: the form carries **no
  subject field** — the target subject is the signed-in principal, minted
  server-side (the §2.4 U7a pin; a form-bound subject id would be a
  foreign-subject write surface).
- **The avatar subject is never a form field / view-model field**: on the
  render surfaces it is (a) the row's own `SubjectId` (list), (b) the route's
  `{subjectId}` (detail), or (c) the signed-in principal's subject (Edit +
  Preview — both are the author's own profile, FACES M1). The view-models
  keep their frozen shapes (C-MED·1 at the Web projection layer).
- **Fail-closed render contract (FACES M1–M5, U7's mapping):** for a
  resident whose avatar I may not see (M3 denied), who is blocked (M4), is
  unknown (M5), or who simply has none set, the U7 endpoint returns `404`
  with no body — so the `<img>` must **degrade**, not throw an error banner:
  `onerror` hides the `<img>` and reveals a pre-rendered monogram/
  placeholder sibling (Razor-rendered initial for named rows; an empty
  circle when no display-name channel exists — the Preview self-view).
  404 = "no avatar shown", never a broken-image icon.
- **Lean + boring (repo style):** no new JS file, no new framework markup —
  one `onerror` per `<img>` (two style assignments, no string interpolation
  in the JS), one appended CSS section using existing `--kmb-*` tokens
  (site.css's design-system vocabulary: `--kmb-tint-a` / `--kmb-border` /
  `--kmb-ink-soft`), and no view-model growth.

## Deliberate resolutions (recorded up front; all trace to a handoff, a pin, or a repo idiom)

1. **No view-model fields.** `media-u8-plan.md` offers `ProfileEditViewModel`
   / `DirectoryViewModel` additions as *optional* "if the views need them".
   The views don't: list rows already expose `VisibleProfile.SubjectId`; the
   detail's target subject is the stable route value; the Edit + Preview
   self-avatars are the signed-in principal. This also keeps the
   `DirectoryViewModel.Detail` "exactly these five fields" doc pin (the M2
   U8 register's pin at `DirectoryController.ProjectDetail`) untouched and
   avoids a `DirectoryController` edit (outside the Deliverables — §2.7 rule
   1). **Noted per §2.7 spirit** (a scoping resolution, not a shape change).
2. **`Directory/Detail.cshtml` reads the target subject from
   `httpContextAccessor.HttpContext?.Request.RouteValues["subjectId"] as
   string` via `@inject Microsoft.AspNetCore.Http.IHttpContextAccessor`** —
   the exact `[FromRoute]` parameter of `DirectoryController.Detail`
   (`[Route("directory")]` + `[HttpGet("{subjectId}")]`), a stable URL
   contract the list rows already link through
   (`Url.Action("Detail","Directory", new { subjectId = … })`). Alternatives
   were a new `Detail.SubjectId` view-model field (rejected — forces the
   un-deliverable controller edit + breaks the five-fields pin) or
   `User`-claim minting (wrong subject — this is the *target* resident, not
   the viewer). **Tried-and-rejected read channels (recorded for whoever
   re-derives this first):** `ViewContext.RouteData["subjectId"]` → no
   public indexer on the MVC `RouteData` surface (CS0021) and its `GetValue`
   is ambiguous (CS0411 against the `ConfigurationBinder` extension);
   `Url.RouteData["subjectId"]` → a Razor *View*'s `Url` is `IUrlHelper`,
   which has no `RouteData` member (CS1061); `Request.RouteValues…` / bare
   `HttpContext…` → not in scope in a Razor *View* (CS0103 / CS0120 — bare
   `HttpContext` resolves to the implicit `using`'s static class). The
   `IHttpContextAccessor` `@inject` is the standard in-view request channel.
   **Noted per §2.7 spirit** (a reading channel, not a seam).
3. **Self-avatar subject via `KumunitaPrincipal.SubjectId(User)` in the
   view** (Edit + Preview): the repo already mints the actor subject from
   `User` in Razor view code — `Groups/Detail.cshtml` L30
   (`KumunitaPrincipal.SubjectId(User)`) and `_PinnedAnnouncement` L9
   (`Kumunita.Web.Security.KumunitaPrincipal.SubjectId(User)`) — the same
   single claim-shaping helper the U6/U7 controller uses (U6 resolution 3).
   `ProfilePreviewViewModel` deliberately carries no raw subject id (the
   contact-peek pin), so view-level minting is the only channel that keeps
   the frozen shapes.
4. **Form `action="/profile/avatar"` literal** over `Url.Action(…)` — the
   action is path-attribute-routed (`[HttpPost("/profile/avatar")]`), the
   pinned design-doc §2.4 route string. **Also worth recording:** the
   `## U7` handoff's "Next" line names the upload target
   `POST /profile/avatar/upload` — that is a typo; the **shipped code** and
   the design doc §2.4 (line 503) both pin `POST /profile/avatar`. The code
   + doc win (the form `action` above is what I render).
5. **Monogram/placeholder fallback** (`<img> onerror` → hide → sibling
   `.avatar-mono` span) over `alt=` text or a JS image component: the U7
   404 is body-less and intentional (fail-closed, FACES M3–M5 / no-avatar),
   so the render contract is "no face shown, no error chrome"; the
   monogram keeps the directory grid visually consistent (`Lean + Boring` —
   one attribute handler, no new JS file). `alt=""` (decorative — the
   display name sits right next to it).
6. **Upload-failure feedback stays U6's pinned behaviour** (direct `400` /
   `413` / `415` responses from the action — the U9 `Upload_*` tests lock
   those codes). U8 does **not** add redirects / error views for them
   (that would be a U6/U9-shape change — §2.7 rule 1); it only adds the
   `accept` attribute (a browser pre-filter mirroring the server allowlist)
   + a one-line `form-text` (the format + size hint, so the browser's
   `required`/`accept` affordances do the first-line UX).

## Entry reads (done in this pass)

| Read | Why |
|------|-----|
| `plan-media-file-storage.md` §U8 + `media-u8-plan.md` | the unit scope / deliverables (`≤ 5 files`) / risks + the optional-view-model allowance |
| design doc §2.4 (L498–527, the `AvatarUpload` shape) + §2.2/§2.3 (the `Avatar` GET seam) + §2.5 (the FACES rows the `<img>` surfaces render against) + §2.7 rules 1/5 | the form target, the `<img>` endpoint, the 404 degrade contract, the no-static-path / drift-guard rules |
| handoff `## U7` (latest) + `## U6` | the serving-lane contract (M1–M6 mapping, the `404`-on-deny shape), the upload guards + the `enctype`/antiforgery boundary, the gate baselines (Web 60/60, Core 223/223) |
| `src/Kumunita.Web/Controllers/ProfileController.cs` (full) | the **exact** `[HttpPost("/profile/avatar")]` + `[FromForm] IFormFile? file` + `[ValidateAntiForgeryToken]` + `RedirectToAction("Edit")` (L357–384) and the `[HttpGet("/profile/avatar/{subjectId}")]` + fail-closed ordering (L415–431) — the two contracts my markup points at |
| `src/Kumunita.Web/Controllers/DirectoryController.cs` (full) | `Index`'s row projection (L73–79 — `VisibleProfile.SubjectId` is on the row) + `Detail([FromRoute] string subjectId)` (L108–126) + the `ProjectDetail` five-fields pin comment (L141) — the detail-subject channel (resolution 2) |
| `src/Kumunita.Web/Views/Profile/Edit.cshtml` + `Directory/Index.cshtml` + `Directory/Detail.cshtml` + `Profile/Preview.cshtml` | the existing markup shapes + idiom (Bootstrap 5 utilities, the `@{}`-block local-minting pattern, the `h5` section headers + `form-text` hints, the `partial`/`TempData` patterns) I extend |
| `src/Kumunita.Web/Models/DirectoryViewModel.cs` + `ProfileEditViewModel.cs` + `ProfilePreviewViewModel.cs` | the frozen view-model shapes (why **no** view-model field is needed — resolution 1) |
| `src/Kumunita.Web/Security/KumunitaPrincipal.cs` + `Groups/Detail.cshtml` L1–30 + `_AccountNav.cshtml` | the `KumunitaPrincipal.SubjectId` helper + the "Razor mints the actor subject from `User`" repo idiom (resolution 3) |
| `src/Kumunita.Core/Media/MediaOptions.cs` L17–29 | the pinned caps the form hint mirrors: `MaxBytes = 5 MiB`, `AllowsContentTypes` default `image/jpeg,image/png,image/webp,image/gif` (C-MED·5, Web reads the same constant) |
| `src/Kumunita.Web/wwwroot/css/site.css` (head + tail + tokens) | the design-system vocabulary (`--kmb-*` tokens, section-banner comment style) the new `.avatar` section writes in |
| `tests/Kumunita.Web.Tests/` (file list + grep `RenderView|ViewEngine|ProfileController|DirectoryController` → 0 view-rendering / 0 avatar-controller tests) | confirms my view edits cannot break an existing test; the gate is `run_build` (Razor compile-on-build) + the 60/60 Web.Tests baseline (view-model shape tests — untouched) |

## Render-surface mapping (the `<img>` targets)

| Surface | Avatar subject | Channel | Why |
|---------|----------------|---------|-----|
| `Profile/Edit.cshtml` (current avatar + form preview) | the actor's own | `KumunitaPrincipal.SubjectId(User)` | the editor is self-only (FACES M1 owner row; the form's write target is self-only per §2.4 U7a) |
| `Profile/Preview.cshtml` (the "how I appear" self-view) | the author's own (the signed-in actor) | `KumunitaPrincipal.SubjectId(User)` | `Preview` composes **the author's** saved profile as seen by the `?as=` viewer (U5's `PreviewAsAsync`) — the profile is always the author's, so its avatar is the author's (FACES M1); the view model carries no subject id by pin |
| `Directory/Index.cshtml` (list rows) | each resident (per row) | `p.SubjectId` (the frozen `VisibleProfile` row field) | the list is a pure catalog read (F1/F15); per-resident M3 404s degrade via the `onerror` fallback |
| `Directory/Detail.cshtml` (the single row) | the target resident | `httpContextAccessor.HttpContext?.Request.RouteValues["subjectId"]` (the `Detail` action's `[FromRoute]` param) | the `Detail` view-model record is pinned to exactly its contact-gate fields (resolution 1) — the stable route value is the honest channel |

## Steps (each leaves the tree in a buildable state)

1. **Exec plan** (this file) — `docs/plans-milestones/in-progress/media-u8-exec-plan.md` (done).
2. `Views/Profile/Edit.cshtml` — the avatar section (a `h5` header in the
   page's existing style + the current-avatar `<img>` + the **separate**
   upload `<form>`: `method=post`, `action="/profile/avatar"`,
   `enctype="multipart/form-data"`, `@Html.AntiForgeryToken()`,
   `<input type="file" name="file" accept="image/jpeg,image/png,image/webp,image/gif" required>`,
   a `Save avatar` submit, the one-line `form-text` hint; the `<img>` with
   the `onerror` → `.avatar-mono` fallback, the initial from
   `Model.DisplayName`).
3. `Views/Directory/Index.cshtml` — the per-row avatar (`<img>`
   `src="/profile/avatar/@p.SubjectId"` + monogram fallback, initial from
   `p.DisplayName`).
4. `Views/Directory/Detail.cshtml` — the detail avatar (subject from
   subject from `httpContextAccessor.HttpContext?.Request
   RouteValues["subjectId"]`, `avatar-lg` next to the heading,
   monogram fallback from `Model.DisplayName`).
5. `Views/Profile/Preview.cshtml` — the author-avatar block (`<img>`
   `src="/profile/avatar/{actor subject}"`, `avatar-lg`, placeholder-circle
   fallback — no display-name channel on this view model).
6. `wwwroot/css/site.css` — append the `--- Avatar (media feature, U8)`
   section (`.avatar`, `.avatar-mono`, the `-lg` sizes) in the file's
   existing token vocabulary.
7. **Gate (the U8 register Exit; AGENTS.md § "Running the tests"):**
   `run_build` (full `Kumunita.slnx`, Razor compile-on-build covers the
   view edits) → 0 errors; then the no-regression
   `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
   → expect **60/60** (U8 adds no tests — U9 does; the view-model shape
   tests `DirectoryDetailViewModelTests` / `DirectoryIndexViewModelTests` /
   `ProfileEditViewModelTests` are green by construction since I touch
   **no** view-model file). `Kumunita.Core.Tests` is **not** run — U8
   touches **no** Core file (U4/U5 pinned its 223/223 on the last Core
   change).
8. **Manual check (the register's Exit, second half):** a resident uploads
   an avatar (the new form) and sees it on the list + detail + preview
   surfaces (rendered through the U7 endpoint; a resident **without** an
   avatar shows the monogram, not a broken image).
9. Append **`## U8`** to `media-file-storage-handoff-notes.md`: the files
   touched (the 4 views + `site.css`; **no** view-models), the form's
   `enctype` / `action` / `name` shapes (all pointing at U6's
   `POST /profile/avatar`, never a static/generic endpoint), every `<img>`
   `src` = U7's endpoint (never a static path — C-MED·3), the 404-degrade /
   monogram-fallback contract, the resolutions 1–6 (noted per §2.7), the
   U7-handoff route typo note (resolution 4), the pass counts (verified,
   not assumed), `done`; "nothing staged or committed" (user reviewing);
   "Next: U9".

## Verification / risks

- **No view-rendering / no avatar-controller tests exist** in
  `Kumunita.Web.Tests` (grep-verified — the 60-test baseline is
  view-model + home/health/admin/announcement shape tests), so the view
  edits cannot silently break a pass; the `run_build` Razor
  compile-on-build is the compile gate for the `.cshtml` edits.
- **`onerror` fallback correctness:** the `onerror` handler mutates only
  `style.display` on the `<img>` + its immediate `nextElementSibling`
  (the monogram span, always present in the markup) — no global state, no
  new JS file; a failed `<img>` (the U7 404 rows) leaves no broken-image
  icon (the `alt=""` decorative + immediate hide).
- **Route stability (resolution 2):** the detail subject's channel is the
  `DirectoryController.Detail` `[FromRoute]` param name `subjectId`, which
  the list rows' `Url.Action(…, new { subjectId = … })` links already
  depend on — a rename there is a breaking URL change and would surface in
  `run_build` / the list links, not drift silently.
- **No new seams / no new write surface:** the form posts **only** the
  `file` field (+ the antiforgery token) to U6's self-only action; no
  subject field, no `ProfileEditViewModel` field, no view-model growth
  (C-MED·1 / rule 3 — the view-model shape tests stay green because the
  shapes are untouched).
- **`site.css` is a static asset** (C-MED·3's "never a static *media*
  path" is about *serving uploaded bytes* through a public route — a
  hand-authored stylesheet is the repo's existing presentation channel and
  carries no user data).
</content>
</invoke>
