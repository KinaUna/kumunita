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
| U7 | Web: `Avatar` (serving-lane contract) | **pending** |
| U8 | Web views: form + list + detail + preview | **pending** |
| U9 | Web seam tests: upload guard + FACES M1–M6 | **pending** |
| U10 | ADR 0011 + SECURITY/OPS/ARCHITECTURE/README + close | **pending** |

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
