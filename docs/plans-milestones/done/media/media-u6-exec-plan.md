# U6 execution plan (working) — Web: `ProfileController.AvatarUpload` (self-only upload)

> My (unit U6's) working plan for this pass. The **authoritative spec** is
> the U6 entry in `plan-media-file-storage.md` + the authored
> `media-u6-plan.md` + **design doc**
> `../../design/media-file-storage-design.md` **§2.4** (the pinned action
> shape, lines 498–527) + §2.2 (the `IMediaStore.PutAsync` / `MediaOptions`
> / `SetProfileAvatarAsync` seams this action calls) + §2.5 lines 555–559
> (the U7a/b/c names U9 will lock) + §2.7 rules 4/5/6 (the C-MED·5 type
> guard, C-MED·6 HTTP-free Core, C-MED·8 self-only owner). Prior handoff
> section read: **U5** (latest) in `media-file-storage-handoff-notes.md`
> (plus U2's verbatim `IMediaStore` seam + U4's verbatim lane signature).

## Scope (in / out)

- **In (mine):** exactly one modified file (the register's Deliverables):
  - `src/Kumunita.Web/Controllers/ProfileController.cs` — add `IMediaStore`
    + `IOptions<MediaOptions>` to the ctor and the `AvatarUpload` action
    (design doc §2.4: guards → `PutAsync` → `SetProfileAvatarAsync` →
    redirect `Edit`). The standing per-unit additions: this exec-plan file,
    the `## U6` handoff section, and the Status-table U6 row flip.
- **Out:** U7's `Avatar` serving action (the C-MED·1/2/3 gate is U7's
  contract — I do **not** add a serving seam), the U8 views (the form is
  U8's deliverable; U6 only guarantees the `POST /profile/avatar` lane +
  the redirect-to-`Edit` landing), U9's tests (I pin the §2.5 names for
  them, I do not write them), U10's docs. Drift-guard rule 1: I modify
  **no** file outside the Deliverables set.
- **No DI change needed:** U1/U2 already register
  `IOptions<MediaOptions>` (`AddOptions<MediaOptions>()` + U2's
  `Configure<MediaOptions>(… "Media")`) and `IMediaStore` (transient) in
  `AddKumunitaCore()`; the controller is MVC-registered and resolves both
  from the same provider (verified against the U1/U2 handoff lines).

## Constraints I keep pinned while writing

- **Self-only (C-MED·8):** `subject` = the current user
  (`SubjectId(User)`), **never** a path or form param. The action's first
  statement after subject minting is the defensive
  `subject is null → Unauthorized()` (the §2.4 U7a defensive row); a
  subject-param drift is a fail-closed design-doc violation.
- **Guard order (pinned from §2.4 lines 509–514):** file
  `null || Length == 0` → `400 "Choose an image."` (U7c empty) →
  `MaxBytes > 0 && file.Length > MaxBytes` → `413` (U7b) →
  `!IsAllowed(file.ContentType)` → `415` (U7c type, incl. SVG — C-MED·5;
  `IsAllowed` is the `MediaOptions` seam — the Web reads `IFormFile`, the
  decision stays Core). **All three guards run before any `Put`** — a
  `Put` with a disallowed type or oversize payload would violate C-MED·5 /
  `MaxBytes` (a `Put` also writes a volume file — orphan risk, C-MED·7).
- **Two-Core-lane call order (pinned from §2.4 lines 518–520):**
  `IMediaStore.PutAsync(bytes, filename, contentType, actorId)` **first**
  (the `MediaObject` id comes from the store — dedup, C-MED·4), then
  `IUserInfoService.SetProfileAvatarAsync(subject, media.Id, subject)`
  (the single write lane, C-MED·8 — the lane itself is load-throw-set-save,
  U4/U5). The `IFormFile` never crosses into Core (C-MED·6: copy to a
  `MemoryStream`, then `ToArray()`).
- **Route:** `[HttpPost("/profile/avatar")]` verbatim (§2.4 line 503) —
  the explicit-template idiom `PostsController`/`AnnouncementController`
  already use (`[HttpPost("/posts/new")]` etc.); U8's form and U7's
  sibling route `[HttpGet("/profile/avatar/{subjectId}")]` both key off
  this string (the U8 form's `action` is U8's problem, but the route
  string must be stable — it is pinned here, not free-form).
- **Landing:** `RedirectToAction("Edit")` verbatim (§2.4 line 521) — the
  editor page (U8 puts the file input there).
- **Return codes are the seam U9 asserts:** `400` / `413` / `415` are
  pinned by §2.5 (`Upload_Oversize_Returns413_NoFileWritten`,
  `Upload_WrongType_Returns415_NoFileWritten`, `Upload_Empty_Returns400`) —
  do not "improve" them into 400-family messages.
- **Doc-comment tone:** the controller's U11 idiom (long, cross-
  referencing summary on each action) — mine references §2.4 + C-MED·
  pins. No `//`-comment noise beyond what the repo already carries.

## Deliberate resolutions (recorded up front; all trace to a handoff or a repo idiom)

1. **`[ValidateAntiForgeryToken]`:** the §2.4 pinned snippet omits it,
   but **every** write-lane POST in this controller (and repo) carries it
   (`Edit`'s POST at `ProfileController` L205–206; the Groups/Posts/
   Announcements write actions). I add it — it is the repo's own
   POST-lane idiom, it does not change the pinned shape (signature/route/
   guards/lanes), and U9's direct-invocation tests bypass the antiforgery
   middleware so the attribute is transparent to them. **Noted per
   §2.7** (an addition beyond the snippet, not a shape change).
2. **Action-level `[Authorize]` (snippet line 502):** redundant — the
   `ProfileController` class is `[Authorize]`-gated (L59). I keep it off
   the action (class-level is the stronger, already-pinned gate) and note
   it here.
3. **`SubjectId(User)` helper over `User.FindFirstValue(ClaimTypes.NameIdentifier)`
   (snippet line 506):** `KumunitaPrincipal.SubjectId`
   (`src/Kumunita.Web/Security/KumunitaPrincipal.cs`) is the repo's single
   claim-shaping helper (every controller mints the subject through it —
   ADR 0006-D: the Web shapes HTTP, the Core seams take the subject id;
   the claim is `NameIdentifier` both ways). Same observable behaviour.
4. **Ctor params over `_media`/`_mediaOpts` fields (snippet lines 511–521
   + the `_mediaOpts` note):** the controller is a
   **primary-constructor** shape (`ProfileController(IUserInfoService
   userInfo, DirectoryService directory)` — the "U9/U10 ctor precedent"
   per the class doc-comment). Params `media` + `mediaOpts` replace the
   `_`-prefixed fields; the property accesses become `mediaOpts.Value.*`.
   Same DI shape (U2's `AddTransient<IMediaStore, LocalVolumeMediaStore>()`
   resolves for a transient MVC controller).
5. **`IFormFile` fully-qualified in the snippet:** the Web SDK's implicit
   `using Microsoft.AspNetCore.Http;` already supplies it — I bind
   `[FromForm] IFormFile? file` without a new using (no `System.Web`-style
   polluting using; Core stays HTTP-free regardless — C-MED·6 is about
   *Core*, and U2's drift scan already verified that).

## Entry reads (done in this pass)

| Read | Why |
|------|-----|
| `plan-media-file-storage.md` §U6 + `media-u6-plan.md` | the unit scope / deliverables / risks |
| design doc §2.4 (L498–527) + §2.2 (L185–461) + §2.5 (L529–566) + §2.7 | the pinned action shape, the seam signatures it calls, the U9 test names, the drift guards |
| handoff `## U5` (latest) + `## U2` (incl. verification pass) + `## U1` | the verbatim `IMediaStore.PutAsync` signature, the `MediaOptions` API (`MaxBytes`/`IsAllowed`), the DI registrations U6 injects, the gate baselines |
| `src/Kumunita.Web/Controllers/ProfileController.cs` (full) | the ctor precedent (primary-constructor, L60–62), the `SubjectId(User)` helper (L64–65), the `Edit` POST idiom (`[ValidateAntiForgeryToken]`, `RedirectToAction("Edit")`, L205–246), the guard style (L209–214 "You must sign in…" / no-500 shapes) |
| `src/Kumunita.Core/Media/MediaOptions.cs` (full) | the actual options API I validate against (`MaxBytes` L17, `IsAllowed` L26) |
| `src/Kumunita.Core/UserInfo/IUserInfoService.cs` L119–121 + `UserInfoService.cs` L872–891 | the verbatim `SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy)` seam + its shipped behaviour (load-throw-set-save; `KeyNotFoundException` on missing profile) |
| `src/Kumunita.Web/Program.cs` L24, L341–344 | `AddControllersWithViews()` (the controller DI host) + `MapControllerRoute` default (the explicit template at L503 coexists with `PostsController`/`AnnouncementController`'s) |
| `src/Kumunita.Web/Security/KumunitaPrincipal.cs` L13–34 | `SubjectId(ClaimsPrincipal)` — the single subject-minting helper (resolution 3) |
| `tests/` (grep `ProfileController` → 0 matches) | confirms the ctor change breaks no existing test |
| `media-u5-exec-plan.md` (format) | the exec-plan tier this file mirrors |

## Steps (each leaves the tree in a buildable state)

1. **Exec plan** (this file) — `docs/plans-milestones/done/` (done).
2. `ProfileController.cs` — the pinned resolutions 1–5: two usings
   (`Kumunita.Core.Media`, `Microsoft.Extensions.Options`), the ctor gains
   `IMediaStore media, IOptions<MediaOptions> mediaOpts` **after** the
   existing two params (U4's interface addition changed no signature
   U6 calls; order: `userInfo`, `directory`, `media`, `mediaOpts`), then
   the `AvatarUpload` action in the existing section layout (after
   `Preview`, before `Private helpers` — it is a write lane like `Edit`,
   so the `Edit`/`Preview` pair stays unbroken and the new action gets
   its own `── Avatar (POST — the U6 write lane) ──` heading block to
   match the file's section style).
3. **Gate (AGENTS.md § "Running the tests"; the U6 register Exit is
   `run_build` green on `Kumunita.Web`):** `run_build` (full
   `Kumunita.slnx` — the repo's build entry) → 0 errors; then the
   `dotnet exec` no-regression check `Kumunita.Web.Tests.dll`
   (U5's baseline 60/60 → expect **60/60**, U6 adds no tests).
   `Kumunita.Core.Tests` is not run — U6 touches no Core file (U4/U5
   pinned its own 223/223 on the last Core change).
4. Append **`## U6`** to `media-file-storage-handoff-notes.md`: the action
   signature (the pinned route + params, verbatim per resolution 1–5),
   the two-Core-lane call order (`PutAsync` → `SetProfileAvatarAsync`),
   the `IFormFile`-is-Web-only confirmation (C-MED·6 — not in Core), the
   §2.5 U7a/b/c names U9 will target (**verbatim**):
   `Upload_OwnerValidRaster_SetsAvatarAndServes`,
   `Upload_Oversize_Returns413_NoFileWritten`,
   `Upload_WrongType_Returns415_NoFileWritten`,
   `Upload_Empty_Returns400`, the resolutions 1–5 (noted per §2.7), and
   the pass counts (verified, not assumed); flip the Status-table U6 row
   to **done**; "nothing staged or committed" (user reviewing).

## Verification / risks

- **Ctor-change breakage:** `tests/` contains zero `ProfileController`
  references (grep, 0 matches), so the added params cannot break a direct
  construction; MVC DI resolves both new params from U1/U2's
  registrations (verified against the U1/U2 handoff sections — `IMediaStore`
  at `DependencyInjection.cs` L128, `AddOptions<MediaOptions>()` + the
  `Configure<MediaOptions>` binding).
- **`IFormFile?` nullability:** §2.4 pins the nullable param + the
  `file is null || Length == 0` 400 guard (U7c empty row) — both kept; the
  `BadRequest("Choose an image.")` body is the pinned string (U9 may or may
  not assert the body — it is verbatim either way).
- **`file.Length` on a client that lies about Content-Length:** the
  `Length > MaxBytes` 413 check is the pinned guard (U7b); the body copy
  then happens for a *valid* type at ≤ `MaxBytes` — a body that overflows
  the declared `Length` is a Web-host concern beyond this unit's surface
  (noted, not implemented — §2.4 does not pin a post-copy re-check).
- **Redirect-landing vs `Edit` seed:** the `Edit` GET reads
  `GetProfileAsync(subject)`; after `SetProfileAvatarAsync` the profile
  row exists (bootstrap self-only shape per the `Edit` doc-comment), so
  the landing seeds fine. U8 then renders the avatar `<img>` over the U7
  endpoint.
- **`await file.CopyToAsync(ms)` + `ms.ToArray()`** per §2.4 L516–518 — the
  `IFormFile`→bytes seam (C-MED·6): Core never sees the form type.
- **Working-directory hygiene:** nothing new written outside the volume
  (the store seam owns that), no temp dirs; the `media` volume dir is
  created by U1's `LocalVolumeFileStore` ctor, not by this action.
