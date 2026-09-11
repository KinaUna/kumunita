# U7 execution plan (working) — Web: `ProfileController.Avatar` (the serving-lane contract)

> My (unit U7's) working plan for this pass. The **authoritative spec** is the
> U7 entry in `plan-media-file-storage.md` + the authored `media-u7-plan.md` +
> **design doc** `../../design/media-file-storage-design.md` **§2.3** (the exact
> pinned action shape, lines 463–496) + §2.2 (the `IMediaStore.GetAsync` /
> `OpenReadAsync` seams this action calls) + §2.1 (the `Avatar` (GET) seam +
> the **reused** `IAuthorizationService.CanAsync` row) + §2.5 lines 547–553
> (the FACES M1–M6 names U9 will lock) + §2.7 rules 4/5. Prior handoff section
> read: **U6** (latest) in `media-file-storage-handoff-notes.md` (plus U2's
> verbatim `IMediaStore` seam + U4's `Profile.AvatarId`/lane).

## Scope (in / out)

- **In (mine):** exactly one modified file (the register's Deliverables):
  - `src/Kumunita.Web/Controllers/ProfileController.cs` — add
    `IAuthorizationService authz` to the ctor and the `Avatar` (GET) serving
    action (design doc §2.3: mint `viewer`, resolve `profile`, the
    unknown/blocked/audience gate, then `CanAsync(…Read…)` → `OpenRead` →
    `nosniff` + stored `Content-Type`). The standing per-unit additions: this
    exec-plan file, the `## U7` handoff section, and the Status-table U7 row
    flip.
- **Out:** U8's views (the avatar `<img>` `src` pointing at this endpoint and
  the edit-form file input are U8's deliverable), U9's tests (I pin the FACES
  M1–M6 names for them, I do not write them), U10's docs. Drift-guard rule 1:
  I modify **no** file outside the Deliverables set.
- **DI already resolves `IAuthorizationService`:** `DependencyInjection.cs`
  L45 `services.AddTransient<IAuthorizationService, AuthorizationService>()`,
  the same provider that already supplies `IAuthorizationService` to
  `DirectoryService` (`_authz`) and `IUserInfoService` (L44). A new ctor param
  is a **reused frozen seam** (C-MED·1 — no new id); no DI change, no new
  registration.

## Constraints I keep pinned while writing

- **Fail-closed ordering (pinned from §2.3 lines 476–483), preserved
  verbatim order:**
  `profile is null → 404` (M5) → `profile.Blocked → 404` (M4, **before** the
  decision — blocked supersedes, no `CanAsync`, no audit row — the exact
  `DirectoryService.EvaluateContactGateAsync` early-return idiom in this
  repo) → `AvatarId is null/empty → 404` (no avatar set — fail-closed, not 500)
  → `CanAsync(…Read…)` → `!decision.Allowed → 404` (M3, **after** the seam has
  committed the audit row — C-MED·2). Deny is `404`, never `403` (the repo's
  fail-closed idiom).
- **Single decision path (C-MED·1 / §2.7 rule 4):** exactly one
  `IAuthorizationService.CanAsync(viewer, AccessAction.Read, …)` — the **frozen**
  `AccessAction.Read`; **no** new `AccessAction` / `AccessVia` id, **no**
  new seam. The owner branch is not short-circuited here (unlike `DirectoryService`):
  M1 (owner) is allowed *through* the same call's owner branch (the design doc
  §2.3 line 483 comment), the audit row still lands (C-MED·2, audit is
  Allow **and** Deny).
- **Audit-by-default (C-MED·2):** the audit row is committed by the `CanAsync`
  seam in its own commit — the action does **not** re-implement or emit any
  audit (a double-audit drift). The action's job is to **gate + stream**, not
  to audit.
- **Served only through the app endpoint (C-MED·3 / §2.7 rule 5):** the
  payload crosses the seam via `IMediaStore.OpenReadAsync` (the `mt` catalog is
  the reference, the bytes are the volume — the action bridges the two). **Never**
  a static folder / path.
- **`X-Content-Type-Options: nosniff` (C-MED·5 / §2.7 rule 5) + the stored
  `Content-Type`:** set `Response.Headers["X-Content-Type-Options"] =
  "nosniff"` before returning `File(stream, mediaObject.ContentType)`
  (§2.3 lines 488–489 — the doc writes `Response.TryAddHeader(…)`, which is not
  a runtime member; resolved against the runtime per §2.7, see resolution 7) —
  the stored `MediaObject.ContentType` (validated at the U6 upload boundary),
  not the request's `Accept`/content sniffing.
- **Doc-comment tone:** the controller's U11/long summary idiom — mine
  references §2.3 + the C-MED· pins, and documents the FACES M1–M6 mapping
  inline (the contract every follow-on lane copies).

## Deliberate resolutions (recorded up front; all trace to a handoff or a repo idiom)

1. **`profile.ToAuditableResource()` → `new ProfileToAuditableResource(profile)`:**
   the design doc §2.3, the register, and `media-u7-plan.md` all write the
   adapter as the shorthand method `Profile.ToAuditableResource()`. There is
   **no** such extension method in the codebase — the shipped idiom is the
   **adapter class** `Kumunita.Core.UserInfo.ProfileToAuditableResource`
   (used by `DirectoryService`, `PostService` → `new PostToAuditableResource(post)`,
   and covered by `ProfileToAuditableResourceTests`). I use the existing
   adapter (`new ProfileToAuditableResource(profile)`) and add the required
   `using Kumunita.Core.UserInfo;` (already present, L4) — I do **not** invent a
   new extension method (rule 1: no new seam; rule 2: reuse the additive shape).
   **Noted per §2.7** (a name→idiom mapping, not a shape change).
2. **`IAuthorizationService authz` ctor param (the design doc's `_authz`):**
   the controller's existing `Preview` action delegates the preview *decision*
   to `DirectoryService.PreviewAsAsync` (the composition-read idiom) rather than
   calling `CanAsync` directly; but §2.3 **pins** the serving action calling
   `IAuthorizationService.CanAsync` directly on the controller (the doc is the
   higher authority — §2.7, the doc wins — and the U6/precedent register names
   this seam as U7's). So I add `IAuthorizationService authz` to the primary
   ctor (after `DirectoryService directory`, before `media`, `mediaOpts`) and
   inject the frozen seam. `AddTransient` (DI L45) already resolves it for a
   transient MVC controller. **Noted per §2.7.**
3. **`var mediaObject = …` over the design doc's `var media = …` local:**
   the primary-ctor param is named `media` (`IMediaStore`); a `var media` local
   would shadow it (a self-reference compile error). I name the `MediaObject`
   local `mediaObject` (the same name U6's `AvatarUpload` already uses for its
   `PutAsync` result) — `media.GetAsync` / `media.OpenReadAsync` stay as the
   store calls. **Noted per §2.7** (a local rename, not a seam change).
4. **No action-level `[Authorize]`:** the `ProfileController` class is
   `[Authorize]`-gated (L67); the M6 "unsigned → Challenge" row is the defensive
   `viewer is null → Challenge()` inside the action (§2.3 line 474). Class-level
   is the stronger, already-pinned gate (the U6 resolution-2 precedent).
5. **`SubjectId(User)` helper over `User.FindFirstValue(ClaimTypes.NameIdentifier)`:**
   `KumunitaPrincipal.SubjectId` is the repo's single claim-shaping helper (the
   U6 resolution-3 precedent) — both mint the `NameIdentifier` claim; same
   observable behaviour.
6. **Resolve the `IAuthorizationService` name collision (CS0104).** The file
   already imports `Microsoft.AspNetCore.Authorization` (L7) for the class-level
   `[Authorize]` attribute, and `Kumunita.Core.Authorization` (L2) for the seam —
   so the bare identifier `IAuthorizationService` is ambiguous between the two.
   Since the Core seam is the one U7 calls, I fully-qualify it at its point of
   use: `Kumunita.Core.Authorization.IAuthorizationService` on the ctor parameter
   and in the action's doc-summary `<see cref>`. `AccessAction.Read` and
   `ProfileToAuditableResource` live only in `Kumunita.Core.Authorization`, so
   they stay bare (matching the repo's existing `DirectoryService` / `PostService`
   usage of the same seam). **Noted per §2.7** (a disambiguation, not a seam
   change).
7. **`Response.Headers[…] = …` instead of the design doc's `Response.TryAddHeader(…)`
   (CS1061):** `TryAddHeader` is not a member of
   `Microsoft.AspNetCore.Http.HttpResponse`. The nosniff header is single-valued,
   so the shipped idiom is `Response.Headers["X-Content-Type-Options"] = "nosniff";`
   (assign, not `Append`). C-MED·5 is still satisfied — the header value is
   exactly `nosniff`. **Noted per §2.7** (the doc doesn't compile; the runtime
   idiom is equivalent).

## Entry reads (done in this pass)

| Read | Why |
|------|-----|
| `plan-media-file-storage.md` §U7 + `media-u7-plan.md` + `media-u6-exec-plan.md` (format) | the unit scope / deliverables / risks + the exec-plan tier this file mirrors |
| design doc §2.3 (L463–496) + §2.1 (L148–164) + §2.2 (`IMediaStore` L319–418, `MediaObject` L168–188) + §2.5 (L547–553 FACES M1–M6) + §2.7 | the pinned action shape, the `GetAsync`/`OpenReadAsync` seam signatures, the "reused frozen seam" row, the U9 test names, the drift guards |
| handoff `## U6` (latest) + `## U2` (incl. verification pass) + `## U4` | the ctor param order (`userInfo`, `directory`, `media`, `mediaOpts`), the verbatim `IMediaStore` seam, `Profile.AvatarId`, the gate baselines |
| `src/Kumunita.Web/Controllers/ProfileController.cs` (full) | the primary-ctor shape (L68–72), the `SubjectId(User)` helper (L74–75), the `Preview` `CanAsync`/composition idiom (L258–328), the `AvatarUpload` action (L330–372), the section-heading style |
| `src/Kumunita.Core/Authorization/IAuthorizationService.cs` + `AccessAction.cs` | the `CanAsync(string, AccessAction, IAuditableResource)` seam (L36), `AccessAction.Read` (L9), the `Decision(Allowed, …)` record |
| `src/Kumunita.Core/UserInfo/ProfileToAuditableResource.cs` (full) + `Profile.cs` L28–100 | the adapter to present the profile (`new ProfileToAuditableResource(profile)`), `Profile.Blocked` (L55), `Profile.AvatarId` (L99), `Profile.Visibility` (the `Read` audience) |
| `src/Kumunita.Core/UserInfo/DirectoryService.cs` L175–212 | the fail-closed idiom to match (blocked→early-return no-audit; deny→`Allowed` false), the "one `CanAsync`, one audit row" read |
| `src/Kumunita.Core/DependencyInjection.cs` L44–45 | `IAuthorizationService` (L45) + `IUserInfoService` (L44) are `AddTransient` — the new ctor param resolves with no DI change |
| `tests/` (grep `ProfileController` → 0 matches) | confirms the ctor-change breaks no existing test |

## FACES M1–M6 row mapping (the serving contract)

| Row | Input | Action branch (order in code) | Result | Audit row? |
|-----|-------|-------------------------------|--------|-----------|
| M6 | unsigned viewer | `viewer is null` (before any load) | `Challenge()` | none (defensive; class `[Authorize]` already gates) |
| M5 | known viewer, unknown `subjectId` | `profile is null` | `404 NotFound` | none (seam never called) |
| M4 | known viewer, blocked profile | `profile.Blocked` (before the decision) | `404 NotFound` | none (blocked supersedes, like `DirectoryService`) |
| M3/M1/M2 | profile has `AvatarId`; viewer's `Read` decision | `CanAsync(viewer, Read, new ProfileToAuditableResource(profile))` → `!Allowed` | M3 → `404 NotFound` | **yes — Allow *and* Deny both commit** (C-MED·2) |
| M1 | owner (viewer == subject) | same call, owner branch → `Allowed` | bytes served | **yes (Allow)** |
| M2 | authorized other (in the audience) | same call, audience branch → `Allowed` | bytes served | **yes (Allow)** |
| — | allowed, but the `MediaObject` doc is missing | `GetAsync(AvatarId)` → `null` (or `OpenReadAsync` fail-closed) | `404 NotFound` | the decision's row already committed |
| — | allowed + doc present | `OpenReadAsync` → `File(stream, ContentType)` + `nosniff` | `200` + stored `Content-Type` | the decision's row already committed |

## Steps (each leaves the tree in a buildable state)

1. **Exec plan** (this file) — `docs/plans-milestones/in-progress/media-u7-exec-plan.md` (done).
2. `ProfileController.cs` —
   (a) the class doc-comment gains a **U7 addition** paragraph (the `Avatar`
   serving action + the `IAuthorizationService` ctor seam reuse, C-MED·1/2/3/5,
   the FACES M1–M6 note) after the U6 paragraph — doc↔code parity (the U6
   resolution-6 precedent);
   (b) the primary ctor gains `IAuthorizationService authz,` after
   `DirectoryService directory,` (before `media`, `mediaOpts` — the relative
   U6 ordering `userInfo → directory → … → media → mediaOpts` is preserved);
   (c) the `Avatar` (GET) action is added under a new
   `── Avatar (GET — the U7 serving-lane contract, design doc §2.3) ──` heading,
   placed immediately after `AvatarUpload` and before the `Private helpers`
   block (grouping the two avatar lanes together, mirroring the file's
   section-heading style).
3. **Gate (AGENTS.md § "Running the tests"; the U7 register Exit is
   `run_build` green on `Kumunita.Web`):** `run_build` (full
   `Kumunita.slnx`) → 0 errors; then the `dotnet exec` no-regression check
   `Kumunita.Web.Tests.dll` (U6's baseline 60/60 → expect **60/60**, U7 adds
   no tests). `Kumunita.Core.Tests` is not run — U7 touches no Core file
   (U4/U5 pinned its 223/223 on the last Core change).
4. Append **`## U7`** to `media-file-storage-handoff-notes.md`: the action
   signature (the pinned `[HttpGet("/profile/avatar/{subjectId}")]` + the
   `SubjectId(User) → Challenge` M6 row), the `CanAsync` + `OpenRead` call
   order (single decision path, C-MED·1), the
   `X-Content-Type-Options: nosniff` + stored `Content-Type` header set (C-MED·5,
   via `Response.Headers[…] = …`, resolution 7), and the
   **FACES M1–M6 row mapping** (which branch = which `Decision` outcome — the
   table above), the resolutions 1–7 (noted per §2.7), the pass counts
   (verified, not assumed);
   **done**; "nothing staged or committed" (user reviewing).

## Verification / risks

- **Ctor-change breakage:** `tests/` contains zero `ProfileController`
  references (grep, 0 matches), so the added `IAuthorizationService` param
  cannot break a direct construction; `AddTransient<IAuthorizationService, …>`
  (DI L45) already supplies it (verified — the same seam `DirectoryService`
  already injects as `_authz`).
- **`ToAuditableResource` vs adapter (resolution 1):** the doc's method-shorthand
  has no code counterpart; the shipped `ProfileToAuditableResource` adapter is
  what `DirectoryService`/`PostService`/`ProfileToAuditableResourceTests` use.
  Reusing it avoids a new seam/type (rule 1/2) and keeps the audit-lane
  `TargetKind = "directory"` shape identical to the shipped decisions.
- **`OpenReadAsync` fail-closed:** `LocalVolumeMediaStore.OpenReadAsync` throws
  `KeyNotFoundException` if the doc is missing (U2 handoff; C-MED·7). I guard
  the doc first with `GetAsync(...) is null → NotFound()`, so the normal path
  serves (no 500); a `FileNotFoundException` from the byte seam at serve-time
  is the fail-safe edge U9 may or may not pin.
- **`mediaObject.ContentType` could be `""`** only if stored without a type —
  but the U6 upload boundary (C-MED·5, `IsAllowed`) rejects a missing/unknown
  type, so a stored `MediaObject` always carries the validated `Content-Type`.
  The design doc §2.3 pins `File(stream, media.ContentType)` verbatim — kept.
- **nosniff header set before the `File` result (resolution 7):** the header is
  assigned via `Response.Headers["X-Content-Type-Options"] = "nosniff";` before
  returning the `File` body — the body isn't committed until the `File` result
  runs, so the header rides on the response (design doc §2.3 L488; the doc's
  `Response.TryAddHeader(…)` member doesn't exist in ASP.NET Core, resolved via
  `Response.Headers[…] = …` per §2.7, see resolution 7).
- **Working-directory hygiene:** nothing new written outside the volume (the
  store seam owns byte I/O — C-MED·3/6); no temp dirs touched by this action.
