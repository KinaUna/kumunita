# Media & file storage — handoff notes

> **Scratch tier** of the three-tier contract (see
> `plan-media-file-storage.md`). One appended `## U#` section per unit,
> **appended, never rewritten** — exactly the M2/M3 convention
> (`m2-handoff-notes.md`, `m3b-handoff-notes.md`). A unit reads only the **most
> recent prior** `## U#` section + its own entry-read list before it exits; it
> then appends its own section at the bottom.
>
> **This feature has not been implemented yet** — the plan author has authored
> the **design doc** (`../design/media-file-storage-design.md`) and the
> **plan** (`plan-media-file-storage.md`) in this folder. Units U1…U10 are
> **pending**. The first unit appends `## U1` below.

## Status

| Unit | Title | Status |
|------|-------|--------|
| U1 | Core: `MediaOptions` + `IMediaFileStore` + `LocalVolumeFileStore` | **done** (2026-09-11) |
| U2 | Core: `MediaObject` + `IMediaStore` + `LocalVolumeMediaStore` + `MediaDocTypes` | **done** (2026-09-11) |
| U3 | Core tests: file store + media store | **done** (2026-09-11) |
| U4 | Avatar lane: `Profile.AvatarId` + `SetProfileAvatarAsync` | **done** (2026-09-11) |
| U5 | Core test: `SetProfileAvatarAsync` lane | **done** (2026-09-11) |
| U6 | Web: `AvatarUpload` (self-only upload) | **done** (2026-09-11) |
| U7 | Web: `Avatar` (serving-lane contract) | **done** (2026-09-11) |
| U8 | Web views: form + list + detail + preview | **done (2026-09-11)** |
| U9 | Web seam tests: upload guard + FACES M1–M6 | **done (2026-09-11)** |
| U10 | ADR 0011 + SECURITY/OPS/ARCHITECTURE/README + close | **done (2026-09-11)** |
| U11 | Follow-up fixes: U8 `onerror`, prod media volume, OPS §3; COOLIFY.md check | **done (2026-09-11)** |

## U0 — Plan authored (by the plan author, not a unit)

- **Chosen backend** (user): **content-addressed local files on a dedicated
  volume** — the leanest serving path, within the privacy/audit model
  (raster-only, content-addressed, audit-gated). The honest cost is a
  **second restore surface** in `OPS.md` (closed in U10).
- **Scope** (design doc `## Scope`): the media store primitive + the
  **profile-avatar** reference lane + ADR 0011 + four doc reconciliations.
  **Out:** group logos / post attachments / badge icons / video / arbitrary
  files — follow-on features with their own design doc.
- **Invariants pinned** (C-MED·1–8) — see the design doc `## Invariants`.
- **Gate shape** (design doc §2.6): build-green per unit; the full-suite gate
  runs via the `dotnet exec … .dll` path (AGENTS.md), **not** `dotnet test`.
- **Next:** U1 appends `## U1`.

## U1 — Core: `MediaOptions` + `IMediaFileStore` + `LocalVolumeFileStore` (done 2026-09-11)

- **Deliverables** (3 new files + 1 edit):
  - `src/Kumunita.Core/Media/MediaOptions.cs` — the `CommunityOptions` twin:
    `SectionName = "Media"`, `RootPath` default `{AppContext.BaseDirectory}/media`,
    `MaxBytes` = 5 MiB, `ResolvedAllowedTypes` + `IsAllowed`.
  - `src/Kumunita.Core/Media/IMediaFileStore.cs` — the HTTP-free byte seam
    (C-MED·6): `PutAsync`, `ExistsAsync`, `OpenReadAsync`, `DeleteFileAsync`,
    `RootPath`.
  - `src/Kumunita.Core/Media/LocalVolumeFileStore.cs` — sharded
    `{RootPath}/{id[0..2]}/{id}` (hex-only id) implementation.
  - edit `src/Kumunita.Core/DependencyInjection.cs` — the registrations below.
- **Exact registrations** (added at the tail of `AddKumunitaCore()`, after the
  `AuditPurgeOptions` line), plus `using Kumunita.Core.Media;`:
  ```csharp
  services.AddOptions<MediaOptions>();
  services.AddTransient<IMediaFileStore, LocalVolumeFileStore>();
  ```
- **`ResolvedAllowedTypes` default** (verbatim — C-MED·5, SVG excluded):
  ```csharp
  public IEnumerable<string> ResolvedAllowedTypes =>
      (AllowedContentTypes ?? "image/jpeg,image/png,image/webp,image/gif")
          .Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
  ```
- **Atomic write** (C-MED·4): `PutAsync` → tmp file → `Flush(); Flush(true)`
  (fsync) → `File.Move(tmp, final, overwrite: true)`. Idempotent by content id
  (dedup lives in U2's `IMediaStore.PutAsync`).
- **Fail-closed reads/deletes** (C-MED·7, orphan-safe): `ExistsAsync` returns
  false; `OpenReadAsync` and `DeleteFileAsync` both throw
  `FileNotFoundException` when the file is absent. I made `OpenReadAsync` check
  `File.Exists` and throw *explicitly* (the design doc §2.2 relied on `FileStream`
  `Open` to throw) — same exception type, made fail-closed explicit per U1's
  pinned intent "OpenRead/Delete/Exists all fail closed if the file is absent".
- **No Marten used yet.** U1 composes nothing; the four new types reference only
  BCL (`System.IO`, `System.Linq`) + `Microsoft.Extensions.Options` +
  `Microsoft.Extensions.DependencyInjection.Abstractions` — no `Marten`,
  no `System.Web`/HTTP types (ADR 0006-D holds). `MediaObject` / `IMediaStore` /
  `LocalVolumeMediaStore` / `MediaDocTypes` are U2's scope.
- **Core-agnostic ctor:** `LocalVolumeFileStore(IOptions<MediaOptions>)` — the
  only injected dependency is the Core-agnostic options; it is NOT a Web type.
- **Gate:** `dotnet build Kumunita.slnx -c Debug` → 0 errors (8 pre-existing
  xUnit warnings in test files, none from U1). `dotnet exec` (AGENTS.md path,
  not `dotnet test`) → `Kumunita.Web.Tests` 60/60, `Kumunita.Core.Tests` 213/213,
  no regressions from the DI registration.
- **Next:** U2 appends `## U2` (the `MediaObject` doc + `IMediaStore` +
  `LocalVolumeMediaStore` + `MediaDocTypes`, which composes
  `IMediaFileStore` + the host `IDocumentStore`).

## U2 — Core: `MediaObject` + `IMediaStore` + `LocalVolumeMediaStore` + `MediaDocTypes` (done 2026-09-11)

- **Deliverables** (4 new files + 2 edits):
  - `src/Kumunita.Core/Media/MediaObject.cs` — the `mt` catalog POCO
    (C-MED·7): `Id` (lowercase-hex SHA-256), `Filename?`, `ContentType`,
    `SizeBytes`, `Created`, `CreatedById?`.
  - `src/Kumunita.Core/Media/IMediaStore.cs` — the module seam (C-MED·6
    HTTP-free): `PutAsync` / `GetAsync` / `OpenReadAsync` / `RemoveAsync`.
  - `src/Kumunita.Core/Media/LocalVolumeMediaStore.cs` — composes U1's
    `IMediaFileStore` + the host `IDocumentStore`; the dedup lane (C-MED·4).
  - `src/Kumunita.Core/MediaDocTypes.cs` — the new ADR 0004 §B.1 doc surface
    (`opts.Schema.For<MediaObject>()`), parallel to `M1DocTypes`/`M3DocTypes`.
  - edit `src/Kumunita.Core/DependencyInjection.cs` — registration below.
  - edit `src/Kumunita.Web/Program.cs` — the doc-surface call-site + the
    `Media__*` binding (below).
- **DI registration** (tail of `AddKumunitaCore()`, right after U1's block):
  ```csharp
  services.AddTransient<IMediaStore, LocalVolumeMediaStore>();
  ```
  (plain `AddTransient` — DI resolves `IMediaFileStore` from U1 and the
  host-registered `IDocumentStore`; mirrors M3 U6's `PostService` shape.)
- **`MediaDocTypes` call-site** (in the `builder.Services.AddMarten(opts => …)`
  lambda, immediately after `M3DocTypes.Configure(opts);`):
  ```csharp
  MediaDocTypes.Configure(opts);
  ```
- **`Media__*` binding** (confirmed/added in `Program.cs`, mirroring
  `CommunityOptions`):
  ```csharp
  builder.Services.Configure<MediaOptions>(
      builder.Configuration.GetSection(MediaOptions.SectionName)); // "Media"
  ```
- **Dedup correctness (C-MED·4):** the `Put` path is exactly
  `Sha256Hex(content)` → `LoadAsync<MediaObject>(id, ct)` → **return the
  existing doc if present**, else `IMediaFileStore.PutAsync` (bytes first,
  orphan-safe C-MED·7) → `Store(doc)` → one `SaveChangesAsync`. No
  store-then-check order, so two concurrent identical writers cannot each
  write a second volume file.
- **Marten 9.31 idiom (the one thing I verified against the reference before
  trusting the design doc's `LightweightSession` shape):** **Marten 9 is
  async-only** (no sync `Load`/etc.). I used the codebase-proven
  `await using var session = _store.LightweightSession();` (proven in the
  Web `PostsController`/`AnnouncementController` against `IDocumentStore`)
  + `await session.LoadAsync<MediaObject>(id, ct)` + `session.Store`/
  `session.Delete` + `await session.SaveChangesAsync(ct)`. `SHA256.HashData` +
  `Convert.ToHexString(…).ToLowerInvariant()` give the lowercase-hex id.
- **`Sha256Hex`** is the single source of the content id — the doc `Id` and the
  volume path (via U1's `PathFor`) are both derived from it (C-MED·4/7
  integrity key).
- **No Web/HTTP types in the seam.** `IMediaStore.PutAsync` takes
  `byte[] / filename / contentType / actorId`, not `IFormFile`/`Stream`
  (C-MED·6); `OpenReadAsync` returns `Task<Stream>` (BCL only). Core stays
  HTTP-free (ADR 0006-D).
- **Gate:** `dotnet build Kumunita.slnx -c Debug` → **0 errors** (8
  pre-existing xUnit warnings in test files, none from U2; green on both
  `Kumunita.Core` and `Kumunita.Web`). `dotnet exec` (AGENTS.md path) →
  `Kumunita.Web.Tests` 60/60, `Kumunita.Core.Tests` 213/213, no regressions
  from the new doc surface + DI registration.
- **Next:** U3 appends `## U3` (the file-store + media-store Core tests on the
  pinned §2.5 seam-test names).

## U2 (verification pass — 2026-09-11; this unit, gate re-run)

- **Re-verified against the design doc §2.2 (doc wins, §2.7 rule):** the four
  new files (`MediaObject`, `IMediaStore`, `LocalVolumeMediaStore`,
  `MediaDocTypes`) match the pinned shapes verbatim; the sole intentional
  deviation is the **Marten 9.31.2 async-only session**
  (`await using var session = _store.LightweightSession();`) — the
  codebase-proven idiom (`PostsController` / `AnnouncementController`)
  replacing the design doc's pre-9 `using var` line. The dedup lane (C-MED·4)
  is exactly `Sha256Hex → LoadAsync → return-or-store`, bytes-first
  (C-MED·7).
- **Gate re-run (this pass):** `run_build` (full `Kumunita.slnx`) → **0
  errors** — green on both `Kumunita.Core` and `Kumunita.Web`; `dotnet exec`
  (AGENTS.md path, not `dotnet test`) → `Kumunita.Web.Tests` 60/60,
  `Kumunita.Core.Tests` 213/213 — matching the U1/U2 claims above.
- **Wiring confirmed at the pinned lines:** `IMediaStore` registration at
  `DependencyInjection.cs` L128 (right after U1's block);
  `MediaDocTypes.Configure(opts)` at `Program.cs` L87 (immediately after
  `M3DocTypes.Configure(opts);`); the `Media__*` binding at `Program.cs`
  L45–46 (`Configure<MediaOptions>(… GetSection(MediaOptions.SectionName))`,
  `SectionName = "Media"`).
- **Drift scan:** no Web/HTTP type (`IFormFile` etc.) in the four new files
  (C-MED·6 holds); no EF reference to `MediaObject`; U6/U7 seams are untouched
  (the Web layer only references the doc surface + a comment at
  `Program.cs` L83–86).
- **Working plan recorded:** `media-u2-exec-plan.md` (committed this pass;
  mirrors U1's exec-plan tier).
- **Next:** unchanged — U3 appends `## U3` (the §2.5 byte-store +
  media-store test names, on a temp dir + a `Marten` doc store).

## U3 — Core tests: file store + media store (byte-store correctness) (done 2026-09-11)

- **Deliverables** (2 new test files; the plan's optional shared fixture was
  **deliberately skipped** — each class is self-contained with a small shared
  helper, noted here per rule 1 "never modify a file not in its own
  Deliverables", and no third file is needed):
  - `tests/Kumunita.Core.Tests/Media/LocalVolumeFileStoreTests.cs` — the 4
    §2.5 file-store tests on a **temp dir** (`MediaOptions.RootPath` =
    `Path.GetTempPath()/kumunita-media-tests-{guid10}` via
    `Options.Create(new MediaOptions { RootPath = root })` — the
    `SmtpHealthCheckTests` `IOptions<>` idiom), no Docker.
  - `tests/Kumunita.Core.Tests/Media/LocalVolumeMediaStoreTests.cs` — the 3
    §2.5 media-store tests on the **existing `PostgresFixture`** (one shared
    `postgres:18` Testcontainers, `NewDatabaseAsync()` scratch DB per test,
    `DocumentStore.For(opts => { Connection; DatabaseSchemaName = "mt";
    MediaDocTypes.Configure(opts); })` +
    `ApplyAllConfiguredChangesToDatabaseAsync`, the
    `AnnouncementServiceTests.BootStoreAsync` shape) composited over a
    temp-dir `LocalVolumeFileStore`. **Schema carries only `MediaObject`**
    (no M1/M3 lanes needed here).
- **Pinned test names (design doc §2.5, verbatim — 7 names):**
  - `LocalVolumeFileStore_Put_open_read_roundtrips`
  - `LocalVolumeFileStore_Put_idempotent_same_id_no_second_writer`
  - `LocalVolumeFileStore_OpenRead_missing_throws_FileNotFound`
  - `LocalVolumeFileStore_Delete_missing_throws_FileNotFound`
  - `LocalVolumeMediaStore_Put_dedups_by_content_hash`
  - `LocalVolumeMediaStore_OpenRead_missing_doc_throws_KeyNotFound`
  - `LocalVolumeMediaStore_Put_sets_CreatedById_and_SizeBytes`
- **Stale-name note (drift-guard §2.7 rule 3, "rename only with a note"):**
  the authored `media-u3-plan.md` §Risks lists a *stale* 5-name draft
  (e.g. `LocalVolumeFileStore_Writes_Shards_Atomic_And_RoundTrips`). The
  **design doc §2.5 is the pinned source and won** (the plan itself says
  the exact names are fixed by §2.5); I used the §2.5 names verbatim.
- **One test-body corrected after the first full-suite run (my test, not
  the code) — per §2.7 rule 3 (name kept verbatim, no silent drift):**
  `LocalVolumeFileStore_Put_idempotent_same_id_no_second_writer` first
  asserted first-writer-wins for same-id-different-bytes. That contradicts
  the C-MED·4 contract — the content id *is* the SHA-256 of the payload
  (id ⇒ bytes), and U1's `PutAsync` is an idempotent atomic **overwrite**
  (`File.Move(tmp, final, overwrite: true)`), not first-writer-wins for
  a state that cannot exist under C-MED·4. Corrected the test to put the
  *same* bytes twice and assert one file, no `.tmp` litter, payload
  unchanged. Implementation (C-MED·4/7) was correct as U1/U2 shipped it.
- **Gate (verified — `dotnet exec` path per AGENTS.md §"Running the tests",
  not `dotnet test`):** `dotnet build Kumunita.slnx -c Debug` → **0
  errors** (21 pre-existing xUnit warnings, none from U3); `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` →
  **`Total: 220, Errors: 0, Failed: 0, Skipped: 0`** (= U2's 213 baseline +
  the 7 new §2.5 tests, all discovered and passing), 23 s (Testcontainers);
  filtered `-filterVSTest "FullyQualifiedName~Core.Tests.Media"` run → **7
  total, 0 failed** — the §2.5 test names discover + pass.
- **Working plan recorded:** `media-u3-exec-plan.md` (mirrors U1/U2's
  exec-plan tier) — includes the constraints pinned while writing (file-
  store idempotency semantics, dedup lane, fail-closed, cleanup discipline)
  and the risks (stale names, the first-run failure and its correction).
- **Nothing staged or committed** (user wants to review first) — the 2 test
  files + the exec plan + this note are uncommitted.
- **Next:** U4 appends `## U4` (the avatar reference lane —
  `Profile.AvatarId` + `SetProfileAvatarAsync`, the UserInfo side).

## U4 — Avatar reference lane (UserInfo side): `Profile.AvatarId` + `SetProfileAvatarAsync` (done 2026-09-11)

- **Deliverables** (3 files, all modify — exactly the U4 set, nothing else):
  - `src/Kumunita.Core/UserInfo/Profile.cs` — additive
    `public string? AvatarId { get; set; }` (ADR 0004 §B.1, like M3's
    `Post.Status`), appended after `Address`.
  - `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — the **single** write
    lane in a new "── Media additions (ADR 0011; C-MED·8) ──" block (the
    file's named ADR 0006-E compatible-addition idiom), before the M3 block.
  - `src/Kumunita.Core/UserInfo/UserInfoService.cs` — impl right after
    `UpsertProfileAsync` (one `OpenSession(new SessionOptions())` session,
    `LoadAsync<Profile>` → set field → `Store` → one `SaveChangesAsync`).
- **Verbatim seam shapes (design doc §2.2, doc wins):**
  - `Profile.cs` L95–100:
    ```csharp
    public string? AvatarId { get; set; }
    ```
  - `IUserInfoService.cs` (Media additions block):
    ```csharp
    Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy);
    ```
  - both doc-comments verbatim from §2.2 lines 442–457.
- **Behavior:** `avatarId == null` → clears the field (the lane still stores +
  saves, so the clear persists); a missing profile **throws
  `KeyNotFoundException("Profile not found: {subjectId}")`** (fail closed —
  the doc §2.2 pins that exception type; see deviation note below).
- **`IUserInfoService` seams untouched (C-MED·1):** **no existing seam was
  reshaped** — the block is purely additive (verified: no other implementer
  of `IUserInfoService` exists in src/ or tests/, so the additive interface
  method is compile-safe); no new `AccessAction` / `AccessVia` id introduced;
  `Profile.ToAuditableResource` untouched (U7's to reuse).
- **Deviations (both noted here per §2.7 rule 3 — "rename only with a note";
  neither is a shape change):**
  1. **Exception type on a missing profile:** the existing group lanes
     (`UpdateGroupDescriptionAsync` / `SetGroupPrivacyAsync`) throw
     `InvalidOperationException` on a missing group, and
     `media-u4-plan.md` §Risks says "match the existing lane idiom". The
     **design doc §2.2 pins `KeyNotFoundException`** for *this* lane — the
     doc is the higher authority (§2.7: "on a mismatch **the doc wins**"),
     and the fail-closed posture (throw, not create) is the same in both.
     Used the doc's type.
  2. **`actorBy` accepted but not persisted:** the sealed signature requires
     it (§2.2 line 457; also anticipated in `media-u4-plan.md` §Risks). The
     pinned impl line points at `UpsertProfileAsync`'s shape first, and that
     lane appends **no** `AccessAudit` row ("not an access decision") — so
     this lane writes the field only, no audit row. The avatar *serving*
     lane's audit row is owned by U7's existing `CanAsync(…Read…)` gate
     (C-MED·2). `actorBy` is recorded-by-signature only for now; U5's lane
     tests (the §2.5 names) can pin the observable behavior.
- **Session idiom** — the existing codebase-shape from `UpsertProfileAsync`
  in the same file (`store.OpenSession(new SessionOptions())` +
  `LoadAsync` + one `SaveChangesAsync`), not the design doc's prose;
  consistent with U2/U3's "Marten 9.31.2 async sessions" note.
- **Gate (verified, not assumed):** `run_build` (full `Kumunita.slnx`) →
  **0 errors** — green on `Kumunita.Core` (the U4 Exit) and `Kumunita.Web`;
  `dotnet exec` (AGENTS.md path, not `dotnet test`) → `Kumunita.Web.Tests`
  **60/60**, `Kumunita.Core.Tests` **220/220** — exactly the U3 baseline
  (additive field + additive interface method: no regressions).
- **Working plan recorded:** `media-u4-exec-plan.md` (mirrors U1–U3's
  exec-plan tier; records the two deviation resolutions + the constraint
  set pinned while writing).
- **Nothing staged or committed** (user wants to review first).
- **Next:** U5 appends `## U5` (the `SetProfileAvatarAsync` lane tests —
  the three §2.5 names, on the existing `PostgresFixture` model).

## U5 — Core test: the `SetProfileAvatarAsync` lane (done 2026-09-11)

- **Deliverables** (1 new test file — the register's Deliverables and the
  §2.5 pinned path; **new file, not folded** into an existing test file —
  the register's "new or append" clause, the choice noted here):
  - `tests/Kumunita.Core.Tests/UserInfo/ProfileAvatarLaneTests.cs` — the 3
    §2.5 lane tests (set / clear / missing-profile), on the
    **existing `PostgresFixture`** (one shared `postgres:18` Testcontainers,
    `NewDatabaseAsync()` scratch DB per test +
    `DocumentStore.For(opts => { Connection; DatabaseSchemaName = "mt";
    opts.Storage.Add<KumunitaFeature>(); opts.Storage.Add<AuthorizationFeature>();
    M1DocTypes.Configure(opts); })` +
    `ApplyAllConfiguredChangesToDatabaseAsync` — the
    `UserInfoServiceGroupDescriptionTests.BootStoreAsync` shape in this
    assembly, mirrored verbatim). `Profile` seeded through the service's own
    `UpsertProfileAsync(profile, new ProfileUpdate(null ×5))` (all-null
    patch ⇒ the record supplies every field — the `UserInfoServiceTests` L62
    idiom). The avatar id is a plain lowercase-hex `KnownHash` constant
    (the U3 idiom; C-MED·4) — the lane references the catalog by id only,
    so **no `MediaDocTypes` surface and no `MediaObject` doc** in the
    scratch schema.
- **Pinned test names (design doc §2.5 lines 542–545, verbatim — 3 names):**
  - `UserInfoService_SetProfileAvatar_sets_id`
  - `UserInfoService_SetProfileAvatar_null_clears`
  - `UserInfoService_SetProfileAvatar_missing_profile_throws_KeyNotFound`
- **Stale-name note (drift-guard §2.7 rule 3, "rename only with a note"):**
  the authored `media-u5-plan.md` §Assumptions lists a *single* stale draft
  name (`SetProfileAvatarAsync_Sets_And_Clears_And_FailsClosed_On_Missing`).
  The **design doc §2.5 is the pinned source and won** (three names) — the
  same doc-wins resolution U3 recorded for its own stale draft; recorded
  here and in `media-u5-exec-plan.md`.
- **Behaviour pinned per test (all trace to U4's shipped impl,
  `UserInfoService.cs` L872–891 — tests assert U4's pinned behaviour,
  nothing new):**
  - *set:* the new id is live on the very next `GetProfileAsync` (C4), and
    the fresh read proves the save landed; the other profile fields are
    untouched (DisplayName asserted equal).
  - *null clears:* after a set + a `null` call, a **fresh** `GetProfileAsync`
    still sees `AvatarId == null` (the clear **persists** — `media-u5-plan.md`
    §Risks: an in-memory-only assertion would not prove the save); the id
    is then re-settable (plain field write, no tombstone).
  - *missing profile:* **`KeyNotFoundException`** (U4's doc-wins note over
    the group lanes' `InvalidOperationException`) **and no profile created**
    — fail closed without side effects (the lane is load-throw-set-save,
    never load-or-create).
- **`actorBy` note (carried from U4):** the lane accepts it per the pinned
  signature but does not persist it; the tests pass it and pin zero
  observable effect beyond the field write (the serving-lane audit row is
  U7's `CanAsync` gate, C-MED·2).
- **Gate (verified — `dotnet exec` path per AGENTS.md §"Running the tests",
  not `dotnet test`):** `run_build` (full `Kumunita.slnx`) → **0
  errors**; `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\
  Kumunita.Core.Tests.dll -filterVSTest
  "FullyQualifiedName~ProfileAvatarLaneTests"` → **Total: 3, Errors: 0,
  Failed: 0** (the three §2.5 names discover + pass, 7.8 s Testcontainers);
  full `Kumunita.Core.Tests` → **Total: 223, Errors: 0, Failed: 0** (= U4's
  220 baseline + the 3 new lane tests, no regressions), 23.4 s;
  `Kumunita.Web.Tests` → **60/60** (unchanged, as expected — no Web surface
  touched yet).
- **Working plan recorded:** `media-u5-exec-plan.md` (mirrors U1–U4's
  exec-plan tier; records the stale-name resolution + the constraint set
  pinned while writing).
- **Nothing staged or committed** (user wants to review first) — the test
  file + the exec plan + this note are uncommitted.
- **Next:** U6 appends `## U6` (the Web `AvatarUpload` action — the
  `IFormFile` boundary, owner-scoped self-only, size/type validated against
  `MediaOptions`, then the `PutAsync` → `SetProfileAvatarAsync` write).

## U6 — Web: `ProfileController.AvatarUpload` (self-only upload) (done 2026-09-11)

- **Deliverables** (1 file, modify — exactly the register's Deliverables
  set, nothing else):
  - `src/Kumunita.Web/Controllers/ProfileController.cs` — the ctor gains
    `IMediaStore media, IOptions<MediaOptions> mediaOpts` (after the
    `IUserInfoService` / `DirectoryService` params — the primary-constructor
    shape the class already used, the "U9/U10 ctor precedent" per the
    class doc-comment) + the `AvatarUpload` action (design doc §2.4, below).
  - no DI change needed — U1/U2's registrations already supply both
    (`AddOptions<MediaOptions>()` + U2's `Configure<MediaOptions>(… "Media")`
    + U2's `AddTransient<IMediaStore, LocalVolumeMediaStore>()`), verified
    against the U1/U2 handoff lines.
- **Action signature (design doc §2.4, doc wins; the five deliberate
  resolutions are noted below per §2.7):**
  ```csharp
  [HttpPost("/profile/avatar")]
  [ValidateAntiForgeryToken]
  public async Task<IActionResult> AvatarUpload([FromForm] IFormFile? file)
  ```
  - `subject` minted via the repo's single claim-shaping helper
    `SubjectId(User)` = `KumunitaPrincipal.SubjectId` (the `NameIdentifier`
    claim the §2.4 snippet reads directly — same behaviour, the codebase
    idiom every other controller uses); `subject is null → Unauthorized()`
    (the §2.4 U7a defensive row, class `[Authorize]` already gates).
  - guards, **before** any `Put` (a `Put` with a disallowed type / oversize
    payload would write a volume file that must not exist — C-MED·5 /
    `MediaOptions.MaxBytes`): `file is null || file.Length == 0` →
    `BadRequest("Choose an image.")` (U7c empty, the pinned body);
    `MaxBytes > 0 && file.Length > MaxBytes` → `413` (U7b);
    `!IsAllowed(file.ContentType)` → `415` (U7c type, incl. SVG — C-MED·5;
    the decision stays on Core's `MediaOptions.IsAllowed`, the Web only
    reads the form).
  - `using var ms = new MemoryStream(); await file.CopyToAsync(ms);` then the
    **two-Core-lane call order (pinned §2.4 L518–520):**
    1. `IMediaStore.PutAsync(ms.ToArray(), file.FileName, file.ContentType, subject)`
       — **first** (the `MediaObject.Id` comes from the store's
       content-hash dedup, C-MED·4; bytes-first/orphan-safe, C-MED·7);
    2. `IUserInfoService.SetProfileAvatarAsync(subject, mediaObject.Id, subject)`
       — the C-MED·8 single write lane (U4/U5's `load-throw-set-save`;
       `actorBy` = this same self-subject, the lane's pinned third
       parameter);
    then `RedirectToAction("Edit")` (pinned §2.4 L521 — U8 renders the
    form there).
- **`IFormFile` is Web-only (C-MED·6 confirmed):** the action is the only
  place the form type is touched; it is copied to `byte[]` before crossing
  the seam — `IMediaStore.PutAsync` takes `byte[] / filename / contentType
  / actorId` (U2's verbatim seam), `Kumunita.Core` remains HTTP-free
  (ADR 0006-D). No `IFormFile` reference added to Core (drift-free — the
  U1/U2 scans plus this unit's change hold).
- **§2.5 U7a/b/c test names U9 will lock (design doc L555–559, verbatim):**
  - `Upload_OwnerValidRaster_SetsAvatarAndServes`
  - `Upload_Oversize_Returns413_NoFileWritten`
  - `Upload_WrongType_Returns415_NoFileWritten`
  - `Upload_Empty_Returns400`
  (the 400/413/415 codes + the empty-body string are what the action pins;
  the `NoFileWritten` halves are U9's assertion surface over the
  guards-before-`Put` order this unit implemented.)
- **Deliberate resolutions (none is a seam/shape change; noted per §2.7,
  "rename only with a note" — the U3/U5 precedent):**
  1. **`[ValidateAntiForgeryToken]`** — the §2.4 snippet omits it, but every
     write-lane POST in this controller/repo carries it (`Edit`'s POST at
     L205–206; the Groups/Posts/Announcements write actions); U9's
     direct-invocation tests bypass the antiforgery middleware, so the
     attribute is transparent to them.
  2. **No action-level `[Authorize]`** — the class is `[Authorize]`-gated
     (L61); the snippet's action-level attribute would be redundant.
  3. **`SubjectId(User)` helper** over `User.FindFirstValue(ClaimTypes.NameIdentifier)`
     (the `KumunitaPrincipal` idiom — same `NameIdentifier` claim).
  4. **Primary-ctor params** `media` / `mediaOpts` over the snippet's
     `_media` / `_mediaOpts` fields (the controller's existing constructor
     shape; `IOptions<T>.Value` is the read site).
  5. **`IFormFile` unqualified** — the Web SDK's implicit
     `using Microsoft.AspNetCore.Http;` supplies it; no new using added for
     it (`MemoryStream` / `StatusCodes` are the same story).
  6. **Class doc-comment** synced (the "U9/U10 ctor precedent" paragraph
     now names the U6 `IMediaStore` + `IOptions<MediaOptions>` addition
     with the C-MED·6 note) — doc↔code parity per the repo instructions.
- **Gate (verified, not assumed):** `run_build` /
  `dotnet build Kumunita.slnx -c Debug` → **0 errors, 0 warnings**
  (the register's Exit: green on `Kumunita.Web`); `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  (AGENTS.md path, not `dotnet test`) → **Total: 60, Errors: 0, Failed: 0**
  (= U5's 60/60 baseline — U6 adds no tests; no regressions from the ctor
  change). `Kumunita.Core.Tests` not run this pass — U6 touches **no**
  Core file (U4/U5 pinned its 223/223 on the last Core change).
- **Working plan recorded:** `media-u6-exec-plan.md` (mirrors U1–U5's
  exec-plan tier; records the six resolutions + the constraint set pinned
  while writing).
- **Nothing staged or committed** (user wants to review first) — the
  controller change + the exec plan + this note are uncommitted.
- **Next:** U7 appends `## U7` (the `Avatar` serving action — the
  `Profile.ToAuditableResource()` + `CanAsync(…Read…)` audit gate,
  `X-Content-Type-Options: nosniff` + the stored `Content-Type`, the FACES
  M1–M6 serving contract).

## U7 — Web: `ProfileController.Avatar` (the serving-lane contract) (done 2026-09-11)

- **Entry read:** `plan-media-file-storage.md` §U7; `media-u7-plan.md`
  (the authored self-contained unit); `media-u6-exec-plan.md` (the exec-plan
  format); design doc §2.3 (the pinned action shape) + §2.5 (FACES M1–M6) +
  §2.7 (the drift guards).
- **Prior section read:** `## U6` (latest — the ctor order + the `IMediaStore`
  store-first idiom it already uses) + `## U2` (the verbatim `IMediaStore` seam
  + U4's `Profile.AvatarId`).
- **What landed:** one modified file — `src/Kumunita.Web/Controllers/ProfileController.cs`.
- **The serving action (the contract every follow-on lane copies):**
  - Route: `[HttpGet("/profile/avatar/{subjectId}")]` on
    `public async Task<IActionResult> Avatar([FromRoute] string subjectId)`.
  - `var viewer = SubjectId(User); if (viewer is null) return Challenge();`
    — the M6 (unsigned) defensive guard; the class-level `[Authorize]` is the
    primary gate.
  - Fail-closed ordering (design doc §2.3, verbatim order preserved):
    `userInfo.GetProfileAsync(subjectId)` → `profile is null → NotFound()` (M5)
    → `profile.Blocked → NotFound()` (M4, **before** the decision — blocked
    supersedes, no `CanAsync`, no audit row — the exact `DirectoryService`
    early-return idiom) → `string.IsNullOrEmpty(profile.AvatarId) → NotFound()`
    (no avatar set — fail-closed, not 500) → **one**
    `authz.CanAsync(viewer, AccessAction.Read, new
    ProfileToAuditableResource(profile))` → `!decision.Allowed → NotFound()`
    (M3) — M1 (owner) + M2 (authorized other) auto-allow through the same call.
    The audit row is committed by the `CanAsync` seam in its own commit
    (C-MED·2, Allow **and** Deny) — the action never re-implements the audit.
  - `media.GetAsync(profile.AvatarId)` → `null → NotFound()` (fail-closed, so
    the normal path serves without a 500) → `media.OpenReadAsync(profile.AvatarId)`
    → `Response.Headers["X-Content-Type-Options"] = "nosniff";` (C-MED·5;
    resolution 7) → `return File(stream, mediaObject.ContentType)` (the stored,
    U6-validated `Content-Type`). No static path, no temp dir (C-MED·3/6).
- **DI:** the primary ctor gains `Kumunita.Core.Authorization.IAuthorizationService
  authz` (after `DirectoryService directory`, before `media`, `mediaOpts`) —
  the **frozen** seam reused from `DirectoryService`/`PostService`;
  `AddTransient<IAuthorizationService, AuthorizationService>` (DI L45) already
  resolves it. No new seam, no DI change, no new `AccessAction`/`AccessVia` id
  (C-MED·1).
- **FACES M1–M6 mapping (the serving contract U9 will lock; design doc §2.5):**
  M6 (unsigned) → `Challenge()`; M5 (unknown profile) → `404` (the seam is
  never called, no audit row); M4 (blocked profile) → `404` **before** the
  decision (no audit row, blocked supersedes — like `DirectoryService`); M3
  (denied audience) → `404` **after** `CanAsync` (the audit row already
  committed by the seam); M1 (owner) / M2 (authorized other) → `200` + stored
  `Content-Type` (the audit Allow row committed). Deny is `404`, never `403`
  (the repo's fail-closed idiom).
- **Deliberate resolutions (all noted per §2.7):**
  1. `Profile.ToAuditableResource()` → `new ProfileToAuditableResource(profile)`
     — the doc's method-shorthand has no code counterpart; the shipped idiom is
     the adapter class (used by `DirectoryService`/`PostService`, covered by
     `ProfileToAuditableResourceTests`). No new seam/type (rules 1/2).
  2. `IAuthorizationService authz` ctor param (the design doc's `_authz`) — the
     **frozen** seam injected at the controller; the design doc is the higher
     authority (§2.7), overriding the register's "reuse the composition-read
     idiom" pointer.
  3. `var mediaObject = …` over the design doc's `var media = …` local — the
     primary-ctor `IMediaStore` param is named `media`; a `var media` would
     shadow it (a self-reference compile error).
  4. No action-level `[Authorize]` — the class is already `[Authorize]`-gated;
     M6 is the defensive in-action `viewer is null → Challenge()` guard.
  5. `SubjectId(User)` over `User.FindFirstValue(ClaimTypes.NameIdentifier)` —
     `KumunitaPrincipal.SubjectId` is the repo's single claim-shaping helper
     (the U6 resolution-3 precedent).
  6. **`IAuthorizationService` name collision (CS0104):** the file imports both
     `Kumunita.Core.Authorization` (L2) and `Microsoft.AspNetCore.Authorization`
     (L7, for the class `[Authorize]`), so the bare name is ambiguous. The Core
     seam is fully-qualified at its point of use
     (`Kumunita.Core.Authorization.IAuthorizationService` on the ctor param +
     in the action's `<see cref>`); `AccessAction`/`ProfileToAuditableResource`
     are already unambiguous and stay bare.
  7. **`Response.Headers[…] = …` over the design doc's `Response.TryAddHeader(…)`
     (CS1061):** `TryAddHeader` is not a member of
     `Microsoft.AspNetCore.Http.HttpResponse`; the nosniff header is
     single-valued, so the shipped idiom is
     `Response.Headers["X-Content-Type-Options"] = "nosniff";` (assign, not
     `Append`). C-MED·5 still satisfied — the value is exactly `nosniff`.
- **Gate (verified, not assumed):** `run_build` on `Kumunita.Web` (and
  `dotnet build Kumunita.slnx -c Debug`) → **0 errors, 0 warnings** (the
  register's Exit: green on `Kumunita.Web`); `dotnet exec
  tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` (AGENTS.md
  path, not `dotnet test`) → **Total: 60, Errors: 0, Failed: 0** (= U5/U6's
  60/60 baseline — U7 adds no tests; no regressions from the ctor+action change).
  `Kumunita.Core.Tests` not run this pass — U7 touches **no** Core file (U4/U5
  pinned its 223/223 on the last Core change).
- **Working plan recorded:** `media-u7-exec-plan.md` (mirrors U1–U6's exec-plan
  tier; records the seven resolutions + the FACES M1–M6 row table + the
  constraint set pinned while writing).
- **Nothing staged or committed** (user wants to review first) — the
  controller change + the exec plan + this note are uncommitted.
- **Next:** U8 appends `## U8` (Web views + editor wiring — the avatar `<img>`
  `src` pointing at `GET /profile/avatar/{subjectId}`, the edit-form file input
  wired to U6's `POST /profile/avatar/upload`, the list/detail/preview views).

## U8

**What I did:** the Web-rendering unit — the register's **4 view files + 1
CSS file, no view-models, no controllers** (U6's upload action and U7's
serving action both stay **frozen**; U8 only points the render surfaces at
them):

- Four view files:
  - `src/Kumunita.Web/Views/Profile/Edit.cshtml` — the avatar **form**
    (file input + submit + hint) and the **current-avatar preview**.
  - `src/Kumunita.Web/Views/Directory/Index.cshtml` — the per-resident
    avatar `<img>` (subject = the row's `p.SubjectId`).
  - `src/Kumunita.Web/Views/Directory/Detail.cshtml` — the detail avatar
    `<img>` (subject = the route's `{subjectId}`).
  - `src/Kumunita.Web/Views/Profile/Preview.cshtml` — the author's own
    avatar `<img>` (the preview composes the signed-in **author's** saved
    profile as seen by the `?as=` viewer — so the profile, and its
    avatar, is always the author's: FACES M1's owner row).
- `src/Kumunita.Web/wwwroot/css/site.css` — the `.avatar` /
  `.avatar-mono` presentation tokens (appended section; written in the
  file's existing design-system vocabulary — `--kmb-tint-a` /
  `--kmb-border` / `--kmb-ink-soft`, the Gabarito heading face).

**Form shape (pinned — drift-guarded):** a **separate** `<form>`
(a nested `<form>` would be invalid HTML — the profile form keeps its
own action) with **`method="post"`**, **`action="/profile/avatar"`**
(literal — the pinned U6 route; the action is path-attribute-routed),
**`enctype="multipart/form-data"`** (the `IFormFile` boundary, C-MED·6),
**`@Html.AntiForgeryToken()`** (the action's `[ValidateAntiForgeryToken]`),
a file input **named `file`** (binds the `[FromForm] IFormFile? file`
param), `required` +
`accept="image/jpeg,image/png,image/webp,image/gif"` (a browser
pre-filter mirroring the C-MED·5 `MediaOptions` default allow-list) and
a one-line `form-text` hint (format + the `5 MiB` cap — the
`MediaOptions.MaxBytes` pinned `5L * 1024 * 1024` default). The form
carries **no subject field** — self-only by structural identity (the
§2.4 U7a pin; the target is the signed-in principal, minted
server-side).

**Render `<img>` contract:** every avatar `<img>` now carries
**`src="/profile/avatar/" + subject`** — U7's serving endpoint, **never a
static path** (C-MED·3, §2.7 rule 5; the audit-by-default gate + the
fail-closed `CanAsync` decision run on the endpoint, not on the `<img>`).
The endpoint's 404 fail-safe rows (FACES M3–M5 + "no avatar set") mean a
failed load is not an error to display — each `<img>` carries one
`onerror` (two `style.display` assignments, no JS file) that hides it and
reveals a **server-rendered sibling**: `.avatar-mono` (the resident's
initial, Razor-rendered from the display name where one exists; an empty
circle where the surface has no name channel — the Preview self-view).
404 = "no face shown, no error chrome" — the monogram keeps the directory
grid visually consistent (`Lean + Boring`). `alt=""` (decorative — the
display name sits next to it).

**The subject channels (per surface):**

| Surface | Subject | Channel |
|---------|---------|---------|
| `Profile/Edit.cshtml` | the actor's own (self-only editor — FACES M1) | `KumunitaPrincipal.SubjectId(User)` (the repo's single claim-shaping helper — the same one the controller mints with; the `@using Kumunita.Web.Security` idiom per `Groups/Detail.cshtml`) |
| `Profile/Preview.cshtml` | the author's own (the preview is **the author's** profile, by composition) | `KumunitaPrincipal.SubjectId(User)` — `ProfilePreviewViewModel` deliberately carries **no raw subject id** (the contact-peek pin), so the view mints it |
| `Directory/Index.cshtml` | the resident (per row) | `p.SubjectId` — the frozen `VisibleProfile` row already carries it (no view-model change needed) |
| `Directory/Detail.cshtml` | the target resident | `httpContextAccessor.HttpContext?.Request.RouteValues["subjectId"] as string` via `@inject Microsoft.AspNetCore.Http.IHttpContextAccessor` — the stable route value of `DirectoryController.Detail`'s `[HttpGet("{subjectId}")]` (the list rows link through the same value) |

**Key decisions (recorded):**

1. **No view-model fields** — the register's `ProfileEditViewModel` /
   `DirectoryViewModel` additions were **optional** ("if the views need
   them"); the views don't (the row already has `SubjectId`, the detail
   subject is the stable route value, and the two self-avatars are the
   signed-in principal). This also keeps `DirectoryViewModel.Detail`'s
   "exactly these fields" pin (the `DirectoryController.ProjectDetail`
   remark) and touches **no** controller file (outside the Deliverables —
   §2.7 rule 1). The view-model shape test files in
   `Kumunita.Web.Tests` (`DirectoryDetailViewModelTests` /
   `DirectoryIndexViewModelTests` / `ProfileEditViewModelTests`) stay
   green by construction.
2. **The detail-subject read channel** (resolution 2 in
   `media-u8-exec-plan.md`): the tried-and-rejected variants are recorded
   there — `ViewContext.RouteData["subjectId"]` (CS0021: no public
   indexer on the MVC `RouteData` surface; `GetValue` is CS0411-ambiguous
   against the `ConfigurationBinder` extension), `Url.RouteData[...]`
   (CS1061 — a Razor *View*'s `Url` is `IUrlHelper`), bare
   `Request…` / `HttpContext…` (CS0103 / CS0120 — bare `HttpContext`
   resolves to the implicit `using`'s **static**
   `Microsoft.AspNetCore.Http.HttpContext` class). The
   `IHttpContextAccessor` `@inject` is the working in-view channel.
3. **Route-typo note (recorded per §2.7):** the `## U7` handoff's "Next:"
   line names the upload target `POST /profile/avatar/upload` — that is a
   **typo**. The **shipped `AvatarUpload` action** and the design doc
   §2.4 (line 503) both pin **`POST /profile/avatar`**; the form `action`
   is the literal `/profile/avatar` (the code + doc win).
4. **Upload-failure presentation stays U6's pinned behaviour** — the
   action's direct `400` / `413` / `415` responses (the U9 `Upload_*`
   tests will lock those codes); U8 does **not** add redirects or error
   views for them (a shape change into U6/U9 territory); it contributes
   the `accept` / `required` / `form-text` first-line UX only.
5. **The `RZ1010` catch (recorded):** an `@{ … }` block **inside** the
   `@foreach` body is illegal (Razor is already in the C# context there) —
   the per-row monogram local in `Index.cshtml` is a bare `string
   avatarInitial = …` declaration in the loop body, not `@{ … }`.

**Gate:** `dotnet build Kumunita.slnx -c Debug` → **0 errors, 0
warnings** (a `CS8602` nullable deref on the `IHttpContextAccessor`
chain was caught at build and resolved with the `?.` chain).
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
→ **60/60 passing** (U8 adds **no tests** — U9 writes the FACES M1–M6 +
U7a/b/c suites; U8 touches **no Core file**, so the Core 223/223 baseline
from the last Core change (U4/U5) is unchanged and un-run by design).

- **Nothing staged or committed** (user wants to review first) — the
  four view files + the `site.css` section + the exec plan + this note
  are uncommitted.
- **Next:** U9 appends `## U9` — the FACES M1–M6 + U7a/b/c tests (the
  pinned names in design doc §2.5); the `## U7` "Next:" line's
  `/profile/avatar/upload` is the typo decision 3 above (the shipped
  target is `POST /profile/avatar`).

**Noted per media-u8-plan:** the register's "optional" view-model step
was exercised as **not needed** — the frozen `ProfileEditViewModel` /
`DirectoryViewModel` shapes are untouched (the U11 "exactly six form
fields" and the M2 "exactly these fields" pins remain intact). The
working detail + all resolutions live in
`docs/plans-milestones/in-progress/media-u8-exec-plan.md`.

## U9

**What I did:** the Web-seam-test unit — **two new test files, no
production file touched** (U6/U7's actions and U8's views stay frozen):

- `tests/Kumunita.Web.Tests/ProfileAvatarServingTests.cs` — the
  **FACES M1–M6** serving gate, design-doc §2.5 names verbatim:
  - `Serving_SignedAuthorizedOwner_Returns200_CorrectContentType` (M1)
  - `Serving_SignedAuthorizedOther_Returns200` (M2)
  - `Serving_SignedDeniedAudience_Returns404_And_Audits` (M3)
  - `Serving_BlockedProfile_Returns404` (M4)
  - `Serving_UnknownProfile_Returns404` (M5)
  - `Serving_Unsigned_Challenges_No200` (M6)
- `tests/Kumunita.Web.Tests/ProfileAvatarUploadTests.cs` — the
  **U7a/b/c** upload-guard suite, §2.5 names verbatim:
  - `Upload_OwnerValidRaster_SetsAvatarAndServes` (M1 roundtrip)
  - `Upload_Oversize_Returns413_NoFileWritten` (U7b)
  - `Upload_WrongType_Returns415_NoFileWritten` (U7c type)
  - `Upload_Empty_Returns400` (U7c empty / defensive U7a)
- `docs/plans-milestones/in-progress/media-u9-exec-plan.md` — the working
  detail + resolutions for this unit (the register's U9 "Entry reads"
  and "Deliverables" are fully covered).

**How each pin is proven (the assertion shape):**

| Test | Pin | Assertion surface |
|------|-----|-------------------|
| M1 / M2 | 200 + stored `Content-Type` + `nosniff` | `Assert.IsAssignableFrom<FileStreamResult>` + drain-copy byte equality + `file.ContentType == MediaObject.ContentType` + the `nosniff` header via `httpContext.Response.Headers` (the action sets it on `HttpContext`) |
| M3 | 404 **and** the `CanAsync` decision was made (audit committed by the seam) | `Assert.IsType<NotFoundResult>` + `authz.Received(1).CanAsync(Viewer, AccessAction.Read, Arg.Is<ProfileToAuditableResource>(r => r.Id == Other))` + `media.DidNotReceiveWithAnyArgs().GetAsync(...)` / `.OpenReadAsync(...)` (the "And_Audits" half — the seam's job, C-MED·2; the payload is never reached) |
| M4 | 404 **before** the decision | `Assert.IsType<NotFoundResult>` + `userInfo.Received(1).GetProfileAsync(Other)` (the profile is read — its `Blocked=true` short-circuits) + `authz.DidNotReceiveWithAnyArgs().CanAsync(...)` (the fail-closed row — no decision, no audit; `DirectoryService`'s idiom surfaces through the seam) |
| M5 | 404 — unknown profile | `Assert.IsType<NotFoundResult>` + `userInfo.Received(1).GetProfileAsync(Unknown)` + `authz.DidNotReceiveWithAnyArgs().CanAsync(...)` + `media.DidNotReceiveWithAnyArgs().GetAsync(...)` (a null profile is the pin; nothing downstream runs) |
| M6 | Challenge, no 200 | `Assert.IsType<ChallengeResult>` + `Assert.IsNotAssignableFrom<FileStreamResult>(result)` (M6's "No200" half, pinned) |
| 413 / 415 / 400 | status + **no file written** | `Assert.IsType<StatusCodeResult>` (413/415) / `Assert.IsType<BadRequestObjectResult>` (400 — see decision 4) + `media.DidNotReceiveWithAnyArgs().PutAsync(Arg.Any<byte[]>(), Arg.Any<...>(), ...)` (the full-arity `Arg.Any` including `CancellationToken`, xUnit1051-clean) + `userInfo.DidNotReceiveWithAnyArgs().SetProfileAvatarAsync(...)` |
| M1 roundtrip | the store-first / profile-second ordering (C-MED·7 orphan-safe) | `media.Received(1).PutAsync(Arg.Is<byte[]>(b => b.SequenceEqual(Png)), "pixel.png", "image/png", Owner, Arg.Any<CancellationToken>())` then `userInfo.Received(1).SetProfileAvatarAsync(Owner, MediaId, Owner)` + `Assert.IsType<RedirectToActionResult>` (the 302 `Edit`) |

**Key decisions (recorded):**

1. **No new route/authz fixture invented.** The repo's existing
   `Kumunita.Web.Tests` idiom (the sealed `DirectoryService` +
   `DefaultHttpContext` + `Kumunita` identity principal + NSubstitute)
   already covers the seam — `IUserInfoService` / `IAuthorizationService`
   / `IMediaStore` are substituted, `ProfileController` is the real
   SUT, and `Kumunita.Core.Identity.ClaimTypes.Subject` (the single
   claim the `KumunitaPrincipal` mints) is the `User` channel. No
   `IHttpContextAccessor` `@inject` needed (that's a Razor view thing;
   the controller reads `User` directly).
2. **The local `TestFormFile : IFormFile`** (not the ASP.NET `FormFile`):
   `FormFile.set_ContentType` throws NRE in this harness (a .NET 10
   `FormFile` quirk — the property's nullability / backing is off for a
   direct object-initializer call from the test process). The
   `IFormFile` surface the action under test reads — `Length` /
   `ContentType` / `FileName` / `CopyToAsync` / `CopyTo` / `Open` /
   `OpenReadStream` / `Name` / `Headers` / `ContentDisposition` /
   `Dispose` — is a small local class the byte-array `content` drives,
   so the action's `await file.CopyToAsync(ms)` and the `Png`-equals
   assertion both hold without a real multipart pipeline (the seam is
   `IFormFile`, not the pipeline — the `C-MED·6` pin).
3. **`Arg.Any<CancellationToken>()` on every `PutAsync` / `GetProfileAsync`
   / `CanAsync` NSub invocation** (both `Received` / `DidNotReceiveWithAnyArgs`
   and `Returns`): `IMediaStore` / `IUserInfoService` /
   `IAuthorizationService` all take an optional `CancellationToken`
   parameter (xUnit1051 — the "omitted trailing optional" rule). The
   `Arg.Is<byte[]>(b => b.SequenceEqual(Png))` (not `Arg.IsFunc` — that
   doesn't exist in NSub v5) is the byte-matching shape for the
   `PutAsync` payload.
4. **The 400 test's exact type pin**: the `Assert.IsType<StatusCodeResult>`
   that works for 413/415 **fails** for the 400 (empty-file) row —
   because the action does `return BadRequest("Choose an image.")`
   (U6/U7's frozen line), which produces a **`BadRequestObjectResult :
   ObjectResult`** — a *sibling* of `StatusCodeResult`, not a child. So the
   400 test's exact pin is `Assert.IsType<BadRequestObjectResult>` with
   a `StatusCodes.Status400BadRequest` on the `StatusCode` property
   (which the ObjectResult surface exposes via `ObjectResult.StatusCode`).
   **This pins U6's exact `BadRequest(string)` shape** — a drift in that
   line to `BadRequest()` (no arg) would flip the result to a
   `StatusCodeResult`-descendant and break this test, which is the
   intended guard.
5. **FACES M3's "And_Audits" is the *positive-received* pin on the seam call.**
   The audit row is committed inside the `CanAsync` seam (C-MED·2) — the
   action never writes it. The test asserts
   `authz.Received(1).CanAsync(Viewer, AccessAction.Read, Arg.Is<ProfileToAuditableResource>(r => r.Id == Other))`
   (the decision ran for the *right* profile + subject); the audit-row
   *shape* is the `M1` seam tests' pin (out of U9's scope — U9 does not
   re-pin the audit-row *bytes*, only that the decision was made and
   the 404 result surfaced). The `Arg.Is<ProfileToAuditableResource>`
   Id-matcher (not `Arg.Any`) is what keeps M4/M5's `DidNotReceive` pin
   from accidentally satisfying M3's `Received` pin in the *same* seam
   shape — the `Id` is the disambiguator.
6. **No §2.7 drift**: no new test name, no new seam, no
   `AccessAction` / `AccessVia` id, no volume write bypassing the
   `IMediaStore` seam (all four 4xx rows and the M4/M6 rows pin the
   `DidNotReceive` surface), no `MediaObject` / `Profile` shape change.

**Gate (§2.5 + §2.6):**
- `run_build` (whole solution, net10.0): **0 errors, 0 warnings**.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`:
  **70/70 passing** (U9 adds 10 — the two files above — to the 60
  baseline U8 saw; the 6 `ProfileAvatarServingTests` + 4
  `ProfileAvatarUploadTests` are all green in that pass).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`:
  **not re-run** — U9 touches **no** Core file, so the last
  Core-change baseline (U4/U5, 223/223; U6's Core-untouched `Profile`
  surface re-run unchanged by U6 itself) stands. Running it would leave
  a Testcontainers `postgres:18` behind (AGENTS.md's cleanup note)
  with no change to assert; U10's close (the full-suite `## Media —
  Closed` record) is the re-run.

- **Nothing staged or committed** (user wants to review first) — the
  two test files + the exec plan + this note are uncommitted.
- **Next:** U10 appends `## U10` (the governance + close —
  ADR 0011, the `SECURITY.md` (e) row, the `OPS.md` "second surface"
  row, the `ARCHITECTURE.md` context entry, the `README.md` Roadmap
  update, and the `## Media — Closed (recorded)` section in the design
  doc + the `## Summary` in this file). U10 *additionally* re-runs the
  Core test suite (223/223) to record the full-suite §2.6 gate.

## U10 — Governance + close (governance, no code)

**Scope as executed:** ADR 0011 + the four governance-doc reconciliations +
the design-doc close + this `## Summary`. No code file touched; the §2.6
full-suite gate is the (re-)run U9 deferred.

**§2.6 gate — recorded (2026-09-11, zero drift vs U9/U5 baselines):**
- `dotnet build Kumunita.slnx -c Debug` — **Build succeeded, 0 Warning(s), 0 Error(s)** (00:00:10.07)
- `dotnet exec Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` — **Total: 70, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (0.799 s)
- `dotnet exec Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` — **Total: 223, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0** (27.061 s)

**Deliverables landed (all in this review, uncommitted):**
- `docs/adr/0011-media-and-file-storage.md` — **created** (accepted
  2026-09-11; amends ADR 0004's "single `pg_dump`" story with the volume
  second restore surface). Names C-MED·1/2/3/5 in its decision text; the
  two-schema / Weasel-feature precedent (ADR 0004 §B) is the shape model.
- `docs/adr/README.md` — 0011 index row added.
- `docs/SECURITY.md` — §3 data-class row **(e) Media & uploaded bytes**;
  §5 two control rows (serving-endpoint audit, C-MED·1/2/3; upload
  allowlist + self-only lane, C-MED·5/8).
- `docs/OPS.md` — config rows `Media__RootPath` (Recommended; the default
  path lives **inside the image layer** — prod must attach a volume),
  `Media__MaxBytes` (Optional, default 5242880), `Media__AllowedContentTypes`
  (Optional, raster allowlist, SVG excluded); §4 a **second restore
  surface** block (snapshot the volume beside its `pg_dump`; verify
  round-trip at the quarterly test; the half-restore degrades safely —
  `MediaObject` is re-hydratable, orphaned files inert); §5 the volume
  paragraph after the DR list.
- `docs/ARCHITECTURE.md` — §2 tree `MediaDocTypes.cs` + `Media/` rows; §3
  feature-modules list now includes Media (byte-store module blurb); §5 the
  "one database + one volume" note + the `MediaObject` POC block; the stale
  ADR range corrected to 0001–0011 (it was 0001–0009 — pre-existing, it
  was already missing 0010).
- `README.md` — Status note (avatar landed, ADR 0011, second restore
  surface); Features bullet (avatars + the follow-on-lane note); ADR range
  0001–0011; the design-doc row.
- `docs/design/media-file-storage-design.md` — **`## Media — Closed
  (recorded)`** appended (gate verbatim, U1–U10 line, deviations, the
  4-item follow-on-lane list, the governance reconciliation list, sole
  handoff-artifact pointer).
- `media-u10-exec-plan.md` — this unit's exec plan + the actual gate block.
- This file — Status row now **done**; `## U10` + `## Summary`.

**Deviations from the design doc (all per the §2.7 process, all already
recorded at their unit):** U4 `KeyNotFoundException` doc-wins ·
U6 `[ValidateAntiForgeryToken]` · U7 `nosniff` header idiom
(`Response.Headers[…]='nosniff'`, not `TryAddHeader`) · U8
`IHttpContextAccessor` `@inject` for the subject id (Razor
`RouteData`-based lookups hit CS0021/CS1061) · U9 local
`TestFormFile : IFormFile` (.NET 10 `FormFile` setter NRE). The ADR (rule 6)
conformed to the doc; no drift-guard fired against it.

**Findings carried forward (open, not fixed — no-code unit; each needs a
named follow-up owner when the lane it belongs to is planned):**
1. **U8's inline `onerror`** on the avatar `<img>` contradicts
   `SECURITY.md §6`'s CSP discipline ("no inline `on*`"). U8 predates this
   U10 pass; needs a small follow-up unit (a `client/*.ts` monogram
   fallback) or a scoped SEC-6 decision. Flagged in `## Summary`.
2. **Production media mount is undefined infra.** The `Dockerfile` has no
   `VOLUME` for media and `docker-compose.yml` only names
   `kumunita_dpkeys`; a bare container would lose uploads on re-deploy.
   `OPS.md` now *requires* an attached volume at `Media__RootPath`, but the
   compose/Dockerfile wiring is a code change — out of this unit's
   deliverables. Owner: the first follow-on media lane, or a small infra unit.
3. **Stale ADR range "0001–0009"** in `README.md` + `ARCHITECTURE.md` was
   already missing 0010 before this feature; corrected to 0001–0011 in this
   pass as part of the legitimate touch-set (not drift on the media doc
   itself).
4. **`OPS.md §3` names an `mt.migrations` ledger** but ADR 0004 §B states
   the Weasel pattern has **no** `mt.migrations` ledger (delta detection).
   Pre-existing doc drift, not a media concern — flagged for the next
   OPS.md touch, left alone here.
5. **U7a's non-owner 403 half** has no dedicated test — the self-only shape
   is structural (subject is minted server-side, not a path param); the
   404 row in `ProfileAvatarServingTests` + the form's self-only shape pin the
   positive side. Noted in the design-doc close; no test to add retroactively.
6. **Route name:** the shipped route is `POST /profile/avatar` (U6 register
   + design doc §2.4); U7's handoff "Next:" line wrote it as
   `POST /profile/avatar/upload` — a typo, the shipped handler is
   `AvatarUpload`, the route is `/profile/avatar`.

**Nothing staged or committed by U10.** The U1–U9 code surface is already
committed by its units (the `Media(U#)` commits, U1..U9, in `git log`).
The uncommitted review change set from this unit's pass is exactly the
nine doc files: 7 modified (`README.md`, `docs/ARCHITECTURE.md`,
`docs/OPS.md`, `docs/SECURITY.md`, `docs/adr/README.md`,
`docs/design/media-file-storage-design.md`, this handoff file) + 2 created
(`docs/adr/0011-media-and-file-storage.md`, `media-u10-exec-plan.md`).

## Summary

**Feature: Media & file storage — closed 2026-09-11.** Ten units, one seam,
one doc surface, one volume. The reference lane (the profile avatar) is
live end-to-end; the primitive is seam-ready for the follow-on lanes below.

| Unit | Deliverable | Gate at unit | Status |
|------|-------------|--------------|--------|
| U1 | `MediaOptions` + `IMediaFileStore` + `LocalVolumeFileStore` | build 0/0 | done |
| U2 | `MediaObject` + `IMediaStore` + `LocalVolumeMediaStore` + `MediaDocTypes` | build 0/0 | done |
| U3 | File-store + media-store tests (dedup, roundtrip, missing-doc/file) | Core suite green | done |
| U4 | `Profile.AvatarId` + `IUserInfoService.SetProfileAvatarAsync` | build 0/0 | done |
| U5 | Lane tests (set / clear / missing) | Core 223/223 | done |
| U6 | `POST /profile/avatar` — self-only upload, 400/413/415 before write, `ValidateAntiForgeryToken` | build 0/0 | done |
| U7 | `GET /profile/avatar/{subjectId}` — FACES M1–M6, one frozen `CanAsync(Read)` → Allow *and* Deny audited, `nosniff` | build 0/0 | done |
| U8 | Avatar form + `<img src="/profile/avatar/{subject}">` + monogram fallback (inline `onerror` — see Finding 1) | build 0/0 | done |
| U9 | `ProfileAvatarServingTests` (M1–M6) + `ProfileAvatarUploadTests` (U7a/b/c); local `TestFormFile : IFormFile` | Web 70/70 | done |
| U10 | ADR 0011 + SECURITY/OPS/ARCH/README + design-doc close + this `## Summary` | **build 0/0 · Web 70/70 · Core 223/223** (2026-09-11, zero drift) | done |

**Deviations (all reconciled, all unit-recorded — see the design-doc
close for the full five):** U4 `KeyNotFoundException` doc-wins;
U6 `ValidateAntiForgeryToken`; U7 nosniff idiom; U8 `IHttpContextAccessor`;
U9 `TestFormFile : IFormFile`.

**Deferred follow-on lanes (each its own design doc + units —
`## Media — Closed (recorded)` in the design doc is the named list with the
next-owner cue):** **group logos** · **post / reply attachments** ·
**badge / icon catalog** · **video / office documents** (this one crosses
the raster-only allowlist + 5 MiB default — the §4 Revisit trigger).

**Open findings (carry into the next unit on the media surface —
none blocking, none fixed here):** Finding 1 (the U8 inline `onerror`
CSP-§6 conflict) · Finding 2 (the prod media volume is not yet wired in
`compose` / `Dockerfile`) · Finding 4 (the `OPS.md §3` `mt.migrations`
ledger claim contradicts ADR 0004 §B). Findings 3/5/6 are
record-and-done.

**Sole handoff artifact going forward:** this `## Summary` — read it first
when starting any follow-on lane's U1 (it holds the unit table, the
reconciled gate counts, the 4-item deferral list, the 3 live open
findings in one place).

**U11 addendum (this unit, 2026-09-11):** the three open findings above are
closed — Finding 1 (inline `onerror`) → the `client/lib/avatar.ts` module
(tsc-compiled to `wwwroot/js/lib/avatar.js`, loaded by the Razor layout as
`<script type="module" src="~/js/lib/avatar.js">`; the four U8 view
files now carry a hidden monogram sibling + `data-avatar-fallback`, and
the inline attribute is gone — no SECURITY.md §6 exception needed);
Finding 2 (prod media volume) → `Dockerfile` `VOLUME /data/media` +
`docker-compose.yml` `Media__RootPath: "/data/media"` + named volume
`kumunita_media`, mirroring the `kumunita_dpkeys` precedent (the
`U11` section below carries the live `docker compose` survival check);
Finding 4 (OPS §3 `mt.migrations`) → Confirmed-drift-and-corrected (the
code — Weasel storage features + delta detection, ADR 0004 §B — is
truth; OPS §3 now says so). `## U11` has the unit plan + dispositions +
gate counts.

## U11 — Follow-up fixes: the three open findings (done 2026-09-11)

**Plan:** `media-u11-plan.md` (in this folder; the `U11` sections of the
unit table below mirror its D1–D4 dispositions).

### D1 — inline `onerror` (SECURITY.md §6 CSP discipline) — **Fixed**

- `src/Kumunita.Web/client/lib/avatar.ts` (new) — the monogram fallback
  module, one `error` listener per `img[data-avatar-fallback]` (capture:
  `onerror` never fires for images that never load, and the listener
  survives a lazy-load deferral an inline attribute would not).
- `Views/Profile/Edit.cshtml` · `Preview.cshtml` · `Views/Directory/
  Index.cshtml` · `Detail.cshtml` — the inline `onerror` attribute
  removed; each avatar `<img>` gains `data-avatar-fallback`, each monogram
  sibling gains `class="avatar-mono"` (+ `avatar-lg` at lg size) and
  stays `style="display:none"` until the module flips it.
- `Views/Shared/_Layout.cshtml` — one `<script type="module"
  src="~/js/lib/avatar.js"></script>` after the bootstrap script (the
  ARCH §7 pattern; global because the fallback is a lib-level concern).
- Compile + ship (`wwwroot/js/` is .gitignored build output — no csproj
  change needed): `Microsoft.TypeScript.MSBuild` (7.0.1, already in
  `Kumunita.Web.csproj`) drives tsc from the project-root
  `tsconfig.json` (`client/**/*.ts` → `wwwroot/js`; `rootDir: client`) —
  `dotnet build` emits `wwwroot/js/lib/avatar.js`, and `dotnet publish`
  (the Docker build's path) was verified to carry it into the output
  alongside the U8 `site.js` lane. This tree has no npm build; the tsc
  lane is MSBuild-driven.
- `wwwroot/css/site.css` — the U8 comment block that named the `onerror`
  reworded to the module; the `.avatar-mono` monogram fallback class added
  (the four views' `<span>`s already carried its size classes — they were
  dead pending this).
- **No** SECURITY.md change: the inline attribute is gone, so no
  §6 exception was recorded.
- `Models/AvatarUpload.cshtml.cs` — unchanged: its `IsAvatar` doc-comment
  names U7's seam semantics (FACES M3–M5), not the inline fallback.

### D2 — production media volume — **Wired**

- `Dockerfile` — `VOLUME /data/media` floor, same comment style +
  floor-not-replacement rationale as `VOLUME /data/dataprotection-keys`.
- `docker-compose.yml` — `Media__RootPath: "/data/media"` on the app
  service (the exact path OPS.md's config row and ADR 0011 name for the
  prod mount), `- kumunita_media:/data/media` in the app `volumes:`, and
  `kumunita_media:` in the top-level `volumes:` block — the
  `kumunita_dpkeys` precedent mirrored line-for-line.
- Verified with the live stack (this box has Docker; the plan's
  "recorded as a manual step" contingency was not needed): a real
  signup → login → `POST /profile/avatar` (a 70 B PNG upload) against a
  fresh `docker compose up --build -d`, the served avatar recorded
  (`200 image/png`, SHA-256 `35227EEA…DAC7D`, stored content-addressed
  at `/data/media/35/35227…` on the `kumunita_media` volume); then a
  **whole-stack re-creation** (`docker compose up --force-recreate -d` —
  app + db + mailpit all rebuilt from disk) and the **byte-identical
  avatar served back** from the same named volume — the upload survives,
  exactly per the `kumunita_dpkeys` precedent.
### D3 — OPS.md §3 `mt.migrations` drift (Finding 3)

Dispo: **Confirmed-drift-and-corrected** (ADR 0004 §B wins; OPS.md fixed).

- **Code truth:** there is no `mt.migrations` ledger anywhere — not in
  `Program.cs` (boot is `Marten` feature-schema boot from the
  `M1DocTypes`/`M3DocTypes` registration surfaces), not in `M1DocTypes`
  (its comment is explicit: "Marten owns the schema — no hand-written
  `migrations/` files"), and not on the live stack: `pg_tables` on the
  compose `kumunita` DB shows the `identity` schema (7 AspNet* tables),
  the `mt` schema (31 feature/doc tables, **no** `mt.migrations`), and
  `public.__EFMigrationsHistory` (EF Core's ledger for *Identity*'s
  tables only — the EF Core `IdentityDbContext` half of ADR 0004 §B).
- **Correction:** OPS.md §3 now says the domain/feature schema surface
  follows ADR 0004 §B's **Weasel-feature pattern** (delta detection
  against the live schema, no recorded-applied ledger) and that the only
  migration ledger is `public.__EFMigrationsHistory` for Identity. The
  old "list migrations and check `mt` for the ledger" restore step was
  removed; §4/§5's second restore surface (media volume) is unchanged
  and now matches the wired `Dockerfile`/`docker-compose.yml` state.
- ADR 0004 §B was **not** touched — the code proves it right.

### D4 — `COOLIFY.md` production path (the check)

Dispo: **Fixed** — COOLIFY.md as it stood after U10 pointed its env
table at `Media__RootPath` but shipped **no volume step in the app
creation flow**, so a production deploy following §5 alone would have
lost uploads on every redeploy. U11 closed that, and the shipped state
now documents the second restore surface consistently with OPS.md:

- §5 env table: `Media__RootPath` row (required in production; omit =
  in-image default `/app/media` ⇒ uploads lost on redeploy).
- **§5.2A — Media volume persistence (required in production)**:
  a **Coolify-managed named volume** on the app container at
  `/data/media` (e.g. `coolify_kumunita_media`; a host-path bind is
  mentioned only as an optional fallback) + the `Media__RootPath` env
  pointing at that path — the exact two-step shape of §5.2 for the
  keyring, and the same container path as the `Dockerfile`'s
  `VOLUME /data/media` and the compose `kumunita_media` named volume.
  Backup/restore paragraph is aligned with OPS §4/§5 (`media-<date>.tgz`
  snapshot of `Media__RootPath` in the same backup set as the `pg_dump`,
  restored into `Media__RootPath` before app start).
- §5.2A verify step + `## Troubleshooting (quick)` row: avatars 404 for
  everyone after a redeploy (monograms shown) ⇒ volume not attached
  or `Media__RootPath` drifted; catalog row may survive (Postgres) while
  the *bytes* are gone (volume).

No OPS.md/COOLIFY.md contradiction: both name the container
`/data/media` path, the `Media__RootPath` env, and the media volume as
the *second restore surface* alongside Postgres. A deployment following
the updated COOLIFY.md does **not** lose uploads on redeploy — the
named volume outlives the container.

### Gate

Re-verified on `release` @ `8d849bf Media(U9)` + the uncommitted
U10/U11 working-tree change set (U10/U11 ship per the house rule — user
reviews first):

- `dotnet build Kumunita.slnx -c Debug` → **0 Warning(s), 0 Error(s)**.
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → **Total: 70, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0**
  (the recorded U10 baseline of 70, verbatim — U11 adds no tests).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  → **Total: 223, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0**
  (the recorded U10 baseline of 223, verbatim — U11 adds no tests).

**Both suites fully green, at the recorded baselines.** No new tests
were added this unit (the work was view/client-script + deployment/docs
corrections), so the counts are expected to hold at exactly 70 and 223.
