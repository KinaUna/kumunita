# Media & file storage — sealed unit register (M4-adjacent, in progress)

> **In progress.** This is the **plan** for the file-storage system, split into
> **sealed units** sized for a ~64K-context fresh agent one at a time, exactly
> like `plan-m3b-moderation.md`. The **primary** reference tier — the exact C#
> seams every unit codes against — is the design doc
> `docs/design/media-file-storage-design.md` (authored, not yet implemented).
> The **secondary** tier is this file (unit registry + deliverables + exit
> criteria). The **scratch** tier is
> `docs/plans-milestones/done/media-file-storage-handoff-notes.md`
> (one appended section per unit, never rewritten).
>
> **The user's chosen backend: content-addressed local files on a dedicated
> volume.** Chosen because it is the leanest *serving* path (no S3/MinIO SDK,
> no signed-URL machinery) and keeps within the privacy/audit model
> (raster-only, content-addressed, audit-gated). The honest cost is a
> **second restore surface** in `docs/OPS.md` (the byte payload is the one
> thing outside Postgres) — C-MED·7, closed in U10.

## Understanding

Add `Kumunita.Core.Media`, a bounded-context feature module (like
`Announcements` inside M3) that stores uploaded bytes content-addressed on a
local volume and catalogs each unique file as a `MediaObject` document in `mt`,
exposed behind an HTTP-free `IMediaStore` seam (ADR 0006-D). Then prove it
end-to-end with **one reference consumer — the profile avatar** (the serving
lane is the contract every future consumer copies): `Profile.AvatarId`,
`IUserInfoService.SetProfileAvatarAsync`, a self-only upload action, and an
audited serving action on `ProfileController` gated by `Profile.ToAuditableResource()`
via the **existing** `CanAsync(…Read…)` (C-MED·1/2/3).

This is **not** a new bounded context — it is a feature module (media is data,
not a business context like `Authorization`), registered on its own
`MediaDocTypes` surface (ADR 0004 §B.1) and served behind the existing
identity/authorization surface. Group logos, post attachments, and badge icons
are **out of scope** — they are follow-on lanes that reuse `IMediaStore` + the
U7 serving-lane idiom (design doc, Scope → Out-of-scope).

**The one thing every unit must respect:** the byte payload never becomes a
public/static path (C-MED·3). Serving is always through the app endpoint so
per-request authorization + audit run (C-MED·1/2). The volume is only ever
written via `IMediaStore` (C-MED·6) — never a helper that touches the disk
directly from a controller.

## Assumptions

- **Scope = the design doc, verbatim, no more.** In-scope: the media store
  primitive + the **profile-avatar** reference lane + ADR 0011 + doc sync.
  Out: group logos / post attachments / badge icons / video / arbitrary files
  (follow-on features, own design doc) — design doc `## Scope`.
- **Local volume backend, content-addressed.** `MediaObject.Id` **is** the
  lowercase-hex SHA-256 of the payload (C-MED·4). Dedup is "load by id → exists?
  return it : store". No content-addressing beyond identity.
- **Raster-only** (`image/jpeg png webp gif`); **SVG excluded** (C-MED·5). The
  allowlist lives in `MediaOptions` (Core-agnostic `CommunityOptions` twin) —
  the extension point for follow-on lanes.
- **`Kumunita.Core` stays HTTP-free** (ADR 0006-D). The `IMediaStore.Put` seam
  takes `byte[]` + `filename` + `contentType` + `actorId`, **not** `IFormFile` /
  `IFormFile`/`Stream` (C-MED·6). The Web layer reads the upload into bytes.
- **Serving gate reuses the frozen `IAuthorizationService.CanAsync(…Read…)`** —
  **no** new `AccessAction` / `AccessVia` id, **no** new authorization module
  (C-MED·1). Same `Profile.ToAuditableResource()` shape `Preview` already uses.
- **Audit-by-default** (C-MED·2): every serve (Allow or Deny) commits an
  `AccessAudit` row — inherited from `CanAsync`, not re-implemented.
- **No new EF use.** `MediaObject` is a Marten doc in `mt` (C-MED·7: it
  restores with `pg_dump`; the byte payload is the *second*, volume-backed
  surface). `Profile.AvatarId` is an additive POCO field (ADR 0004 §B.1), like
  M3's `Post.Status`.
- **Ownership write** (C-MED·8) is **owner-scoped at the Web boundary**
  (self-only, the `subject` is the current user — never a path param) and
  funneled through the single `SetProfileAvatarAsync` lane. The
  `MediaObject.CreatedById` records who first stored each unique file.
- **Tests / acceptance model unchanged.** `xunit.v3`; build-green per unit
  (Exit); the full-suite gate runs via the `dotnet exec … .dll` path
  (AGENTS.md § Running the tests), **not** `dotnet test`.

## Approach

**Sequenced, self-contained units (U1–U10), one per fresh agent (~64K window).**
Each unit is **self-contained**: given *only* the design doc + its entry
read-list + one prior handoff-note section, it can be completed and verified
without re-reading the whole repo.

| Tier | Artifact | Owner |
|------|----------|-------|
| **Primary** (code) | `docs/design/media-file-storage-design.md` — §2.1 seam table, §2.2 exact C#, §2.5 test names, §2.6 gate, §2.7 drift-guard. **Authored now; units match verbatim.** | plan author (done) |
| **Secondary** (plan) | this file — unit registry, deliverables, exit criteria. | plan author (done) |
| **Scratch** (handoff) | `media-file-storage-handoff-notes.md` — one appended `## U#` section per unit. | each unit |
`media-u#-plan.md` (under `done/`)

**Track A (Core, U1–U5):** the `Kumunita.Core.Media` module (byte store +
seam options, then the store seam + `MediaObject` doc + `MediaDocTypes`), then
the avatar reference lane on the UserInfo side (`Profile.AvatarId`,
`SetProfileAvatarAsync`).

**Track B (Web, U6–U9):** `ProfileController.AvatarUpload` (U6) + the serving
action `Avatar` (U7) + the views (U8: form, list, detail, preview) + the seam
tests (U9).

**Track C (governance, U10):** ADR 0011 + `docs/adr/README.md` row + the
`docs/SECURITY.md` (e) data class + `docs/OPS.md` second restore surface +
`docs/ARCHITECTURE.md` module line + `README.md` feature bullet + the close
(`## Media — Closed (recorded)` in the design doc + `## Summary` in the
handoff notes).

Every code unit ends with **build green** (that unit's Exit). The last unit
(U10) appends the final handoff section and closes the feature (the design doc
`## Media — Closed (recorded)`) — the same loop-closing shape as M2 U15 / M3 U12.

---

## Workflow — handoff protocol for fresh-context agents

**Per-unit template** (each `U#` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, 3–6
files <~400 lines each, no full-repo scan; the design-doc § cited is named);
**Deliverables** (a closed set of new/modified files, ≤ ~5 files / ~500 LOC);
**Exit** (`run_build` green for the touched projects; handoff-note entry
appended *before* any follow-up action).

**Unit-series rules** (mirror M3b, plus media-specific ones):
1. A unit never modifies a file not in its own `Deliverables`.
2. Never reshape `MediaObject` / `Profile` / the store seams beyond the
   additive shapes in the design doc §2.2 — the **doc wins** (§2.7 drift-guard).
3. Never introduces a test whose exact name is not in §2.5 (rename only with a
   note; no silent drift).
4. Never opens a new seam on `IAuthorizationService` / `IUserInfoService`
   beyond `SetProfileAvatarAsync`; never adds a new `AccessAction` / `AccessVia`
   id (C-MED·1).
5. Never serves media from a public/static folder, and never writes the volume
   outside the `IMediaStore` seam (C-MED·3/6).
6. Never puts domain/media data in EF (ADR 0004 §B). `MediaObject` is a Marten
   doc in `mt`.
7. **Build-green is the per-unit Exit.** Do not run the full `dotnet test`
   suite as the pass/fail signal (AGENTS.md discovery bug); the gate is the
   §2.6 `dotnet exec … .dll` path, run only in U10.

---

## Unit register

### U1 — Core module: `MediaOptions` + `IMediaFileStore` + `LocalVolumeFileStore`
- **Goal:** the byte-level seam (options + file I/O + local-volume impl) with no
  Marten dependency yet — the unit everything else composes.
- **Entry reads:** design doc §2.2 (the four new type shapes above
  `IMediaStore`) + §2.7 rules 5/6; `src/Kumunita.Core/CommunityOptions.cs`
  (options shape to mirror); `src/Kumunita.Core/DependencyInjection.cs`
  (where to register `IMediaFileStore` / `AddOptions<MediaOptions>`);
  `src/Kumunita.Core/M3DocTypes.cs` (module doc-type surface shape, for
  context); `README.md` (the "one database" + self-hosted principle).
- **Deliverables (3 new files + 1 edit):**
  - `src/Kumunita.Core/Media/MediaOptions.cs`
  - `src/Kumunita.Core/Media/IMediaFileStore.cs`
  - `src/Kumunita.Core/Media/LocalVolumeFileStore.cs`
  - `src/Kumunita.Core/DependencyInjection.cs` — add
    `services.AddOptions<MediaOptions>()` +
    `services.AddTransient<IMediaFileStore, LocalVolumeFileStore>()`.
- **Exit:** `run_build` green on `Kumunita.Core`. Handoff note: `## U1` — the
  exact registered registrations, confirmation `MediaOptions.ResolvedAllowedTypes`
  default is `jpeg|png|webp|gif`, no Marten used yet.

### U2 — Core module: `MediaObject` doc + `IMediaStore` + `LocalVolumeMediaStore` + `MediaDocTypes`
- **Goal:** the store seam over the byte seam + the `mt` catalog doc + its
  Marten registration surface.
- **Entry reads:** design doc §2.2 (the `MediaObject` / `IMediaStore` /
  `LocalVolumeMediaStore` / `MediaDocTypes` shapes) + §2.7 rules 5/6; U1
  handoff (the `IMediaFileStore` + `MediaOptions` registrations);
  `src/Kumunita.Core/M3DocTypes.cs` (the `MediaDocTypes.Configure(StoreOptions)`
  shape to mirror); `src/Kumunita.Core/DependencyInjection.cs` (where to add
  `IMediaStore`); `src/Kumunita.Web/Program.cs` (L52–93 — where
  `MediaDocTypes.Configure(opts)` is called, next to `M1DocTypes` / `M3DocTypes`).
- **Deliverables (3 new files + 2 edits):**
  - `src/Kumunita.Core/Media/MediaObject.cs`
  - `src/Kumunita.Core/Media/IMediaStore.cs`
  - `src/Kumunita.Core/Media/LocalVolumeMediaStore.cs`
  - `src/Kumunita.Core/MediaDocTypes.cs`
  - `src/Kumunita.Core/DependencyInjection.cs` —
    `services.AddTransient<IMediaStore, LocalVolumeMediaStore>()`.
  - `src/Kumunita.Web/Program.cs` — call `MediaDocTypes.Configure(opts)`
    next to `M1DocTypes.Configure(opts)` (and confirm
    `Services.Configure<MediaOptions>(…)` binds `Media__*`, mirroring
    `CommunityOptions`).
- **Exit:** `run_build` green on `Kumunita.Core` **and** `Kumunita.Web`
  (Program.cs references the new surface). Handoff note: `## U2` — the
  `MediaDocTypes` call-site line, the DI registrations, and the `Media__*`
  binding confirmed.

### U3 — Core tests: file store + media store (byte store correctness)
- **Goal:** lock the byte-store correctness the design doc §2.5 pins, on a
  temp dir (no live volume) + a `MediaObject` doc store over
  `Marten` (the existing `Kumunita.Core.Tests` `PostgresFixture` is the model —
  or a lightweight `opts` in-memory if that fixture is Postgres-bound; the
  design doc §2.5 pins the doc store as `Marten` over `MediaObject`).
- **Entry reads:** design doc §2.2 + §2.5 (the **exact** test names) + §2.6;
  U1/U2 handoff (the registered seams to inject); the existing
  `tests/Kumunita.Core.Tests/` for the harness/fixture shape to mirror.
- **Deliverables (up to 4 new test files):**
  - `tests/Kumunita.Core.Tests/Media/LocalVolumeFileStoreTests.cs`
  - `tests/Kumunita.Core.Tests/Media/LocalVolumeMediaStoreTests.cs`
  - (optional shared temp-dir + doc-store fixture under
    `tests/Kumunita.Core.Tests/Media/`)
- **Exit:** `run_build` green; the §2.5 Core test names discover and pass via
  the §2.6 `dotnet exec` path. Handoff note: `## U3` — the temp-dir /
  doc-store fixture shape, the test file paths, pass count (verified
  via `dotnet exec`, not assumed).

### U4 — Avatar reference lane (UserInfo side): `Profile.AvatarId` + `SetProfileAvatarAsync`
- **Goal:** the **additive** `Profile.AvatarId` field (ADR 0004 §B.1) + the
  **single** write lane on `IUserInfoService` / `UserInfoService`
  (C-MED·8: owner scope is enforced Web-side, this lane only writes the field).
- **Entry reads:** design doc §2.2 (the `Profile.AvatarId` +
  `SetProfileAvatarAsync` signatures); U1/U2 handoff (the `IMediaStore` +
  `MediaObject` to reference by id); the existing
  `UserInfoService` `UpsertProfileAsync` / `UpdateGroupDescriptionAsync` shapes
  (lane idiom to mirror); `Profile.ToAuditableResource` (already present, the
  serving lane reuses it in U7).
- **Deliverables (2 files, modify):**
  - `src/Kumunita.Core/UserInfo/Profile.cs` — add
    `public string? AvatarId { get; set; }` (additive; ADR 0004 §B.1).
  - `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (+
    `UserInfoService.cs` impl) — add
    `Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy);`
    in the existing class (load → set field → save; null `avatarId` clears).
- **Exit:** `run_build` green on `Kumunita.Core`. Handoff note: `## U4` — the
  `Profile.AvatarId` + lane signatures (verbatim), confirmation no existing
  `IUserInfoService` seam was reshaped.

### U5 — Core test: the `SetProfileAvatarAsync` lane
- **Goal:** lock the lane (set / clear / missing-profile) per §2.5.
- **Entry reads:** design doc §2.5 (the three lane test names) + §2.2 (the lane
  shape); U4 handoff (the `Profile.AvatarId` + lane signature); the
  existing `Kumunita.Core.Tests` UserInfo test harness.
- **Deliverables (≤ 1 test file, new or append):**
  `tests/Kumunita.Core.Tests/UserInfo/ProfileAvatarLaneTests.cs` (or fold into
  the existing `UserInfoService` test file — the unit chooses, notes it).
- **Exit:** `run_build` green; the three lane test names pass. Handoff note:
  `## U5` — the test file path + the names (verbatim) + pass count.

### U6 — Web: `ProfileController.AvatarUpload` (self-only, `IFormFile` → bytes → `PutAsync` → `SetProfileAvatarAsync`)
- **Goal:** the upload action — the `IFormFile` boundary (Web-only, C-MED·6),
  owner-scoped to the current user, size/type validated against `MediaOptions`,
  then the two-core-lane write.
- **Entry reads:** design doc §2.2 (the `AvatarUpload` shape) + §2.5 (U7a/b/c
  test names, for context) + §2.7 rules 4/5/6; U2/U4 handoff (the
  `IMediaStore` + `SetProfileAvatarAsync` + `MediaOptions` to inject);
  the existing `ProfileController` (the `IFormFile`-free upload idiom, and the
  owner-scoped self-only shape to mirror).
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Web/Controllers/ProfileController.cs` — add the
    `AvatarUpload` action (design doc §2.2 `IFormFile` → `PutAsync` →
    `SetProfileAvatarAsync`), add `IMediaStore` + `IOptions<MediaOptions>` to
    the ctor (the Web-only `IFormFile` boundary, C-MED·6).
- **Exit:** `run_build` green on `Kumunita.Web`. Handoff note: `## U6` — the
  action signature, the two-core-lane call order, confirmation `IFormFile` is
  Web-only (not in Core), and the §2.5 U7a/b/c test names the U9 test will
  target.

### U7 — Web: the serving-lane contract — `ProfileController.Avatar` (audited, `Profile.ToAuditableResource`)
- **Goal:** the avatar serving action — the **contract** every follow-on
  lane copies: gated by `Profile.ToAuditableResource()` via the **frozen**
  `CanAsync(…Read…)`, audit-by-default (C-MED·1/2/3), `X-Content-Type-Options:
  nosniff` + the stored `Content-Type` (C-MED·5).
- **Entry reads:** design doc §2.2 (`Avatar` action shape, `X-Content-Type-Options`
  + Content-Type) + §2.3 (the serving-lane rule, C-MED·1/2/3/5) + §2.5 (the
  FACES M1–M6 test names); U2/U4/U6 handoff (the `IMediaStore` +
  `MediaObject` to read bytes back); the `Profile.ToAuditableResource`
  + `IAuthorizationService.CanAsync` idiom already in `ProfileController`;
  the `SECURITY.md` audit-by-default clause for the `nosniff` + audit notes.
- **Deliverables (1 file, modify):**
  - `src/Kumunita.Web/Controllers/ProfileController.cs` — add the `Avatar`
    action (design doc §2.2, the C-MED·1/2/3/5 shape), and the
    FACES-gate logic (allow/deny/owner/blocked/unknown) matching
    `DirectoryService.PreviewAsAsync`'s `CanAsync` idiom.
- **Exit:** `run_build` green on `Kumunita.Web`. Handoff note: `## U7` — the
    action signature, the `CanAsync` + `OpenRead` call order, the
    `X-Content-Type-Options: nosniff` + Content-Type header set, and the
    FACES M1–M6 row mapping (which row = which `Decision`/`Decision` branch).

### U8 — Web views: avatar form + list + detail + preview renders
- **Goal:** the surface — the profile-edit **form** (file input, self-only
  upload target), and the **render** surfaces (Directory list/detail + Profile
  preview) that show the avatar `<img>` (served via the U7 endpoint, never a
  static path — C-MED·3).
- **Entry reads:** U7 handoff (the serving endpoint shape, for the `<img>`
  `src`); the existing `ProfileController` **view** + `Edit` view (the
  form shape to mirror); the `Directory` list/detail + `Profile` preview
  views; the design doc Scope note ("render surfaces — avatar `<img>` served
  via the U7 endpoint, never a static path").
- **Deliverables (≤ 5 files, modify/new):** the profile-edit form (file
  input + a current-avatar preview), and the avatar `<img>` in the Directory
  list/detail + Profile preview, all pointing at the U7 endpoint.
- **Exit:** `run_build` green on `Kumunita.Web`; a resident can upload + see
  the avatar on the list/detail/preview surfaces (manual). Handoff note:
  `## U8` — the files touched, the form's `enctype`/`action` shape, and that
  every `<img>` `src` is the U7 endpoint (no static path — C-MED·3).

### U9 — Web seam tests: the upload guard (U7a/b/c) + the FACES M1–M6 serving gate
- **Goal:** lock the Web-side behavior the design doc §2.5 pins: the
  **upload guard** (self-only / size / type) + the **serving gate**
  (FACES M1–M6). These are the M3-style "the gate is the product" tests.
- **Entry reads:** design doc §2.5 (the **exact** Web test names:
  `Upload_Owner_Roundtrip`, `Upload_NonOwner_Rejected`, `Upload_Oversize_Rejected`,
  `Upload_WrongType_Rejected` + `Serving_M1..M6`) + §2.6 (the gate shape);
  U6/U7/U8 handoff (the actions + views to exercise); the existing
  `Kumunita.Web.Tests` controller-test harness (the `DirectoryService` test
  shape to mirror for the audited-serve seam).
- **Deliverables (1 test file, new):**
  `tests/Kumunita.Web.Tests/ProfileAvatarTests.cs` (or fold into an
  existing `Kumunita.Web.Tests` profile/controller test file — the unit
  chooses, notes it).
- **Exit:** `run_build` green; the §2.5 Web test names discover and pass via
  the §2.6 `dotnet exec` path. Handoff note: `## U9` — the test file path,
  the test names (verbatim), and the pass count.

### U10 — Governance + close: ADR 0011 + `SECURITY.md` (e) + `OPS.md` second surface + `ARCHITECTURE.md` + `README.md` + `## Closed`
- **Goal:** the loop-closing unit (M2 U15 / M3 U12 analog) — every doc the
  feature must reconcile, plus the ADR that sets the governance shape, plus
  the design doc `## Media — Closed (recorded)` + handoff `## Summary`.
- **Entry reads:** the full design doc (Context/Scope/Invariants + §2.6 gate
  + §2.7 rules); the full handoff notes (U1–U9 sections);
  `docs/adr/README.md` (the ADR shape + the next-index — 0011);
  `docs/adr/0004-data-persistence.md` (the "two schemas" + "documents"
  precedent, for the ADR); `docs/SECURITY.md` (the (a)–(d) data-class model +
  the (e) media class); `docs/OPS.md` (the restore-procedure shape);
  `docs/ARCHITECTURE.md` (the bounded-contexts + module table); the
  `README.md` "Roadmap" + "Features" surfaces.
- **Deliverables (≤ 6 files, modify/new):**
  - `docs/adr/0011-media-and-file-storage.md` (new) + the
    `docs/adr/README.md` row (0011).
  - `docs/adr/README.md` — the 0011 row (new ADR, status, date, summary).
  - `docs/SECURITY.md` — the **(e) media / uploaded bytes** data class + the
    C-MED·1/2/3/5 controls (nosniff, audit-by-default, owner-scoped write,
    content-type boundary).
  - `docs/OPS.md` — the **second restore surface** (the volume snapshot) + the
    `Media__*` config keys (`RootPath`, `MaxBytes`, `AllowedContentTypes`).
  - `docs/ARCHITECTURE.md` — the `Kumunita.Core.Media` module row + the
    `MediaObject` doc + the `MediaDocTypes` surface + the "one database +
    one volume" note.
  - `README.md` — the feature bullet + the follow-on-lane note (group logos
    / post attachments / badge icons — same seam, own design doc).
  - `docs/design/media-file-storage-design.md` — append
    `## Media — Closed (recorded)` (the U3/U5/U9 pass counts, the gate result
    from §2.6).
  - `docs/plans-milestones/done/media-file-storage-handoff-notes.md` —
    the `## Summary` section (shipped units, deviations, deferred follow-on
    lanes).
- **Exit:** `run_build` green (no code touched in this unit — ADR/OPSDOC only);
  a §2.6 `dotnet exec` full gate run is recorded in `## Media — Closed
  (recorded)`. Handoff note's `## Summary` is the sole media→next-feature
  handoff artifact. `ARCHITECTURE.md` + `SECURITY.md` + `OPS.md` + `README.md`
  + the design doc all reconciled; ADR 0011 authored.

---

*The feature opens with the design doc (authored) as its first read and
closes the same way M2/M3 did: one design-doc `## Media — Closed (recorded)`
section + one handoff-note `## Summary` section + the ADR 0011 + the four
doc reconciliations. Nothing else.*
