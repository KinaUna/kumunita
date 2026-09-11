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
| U4 | Avatar lane: `Profile.AvatarId` + `SetProfileAvatarAsync` | **pending** |
| U5 | Core test: `SetProfileAvatarAsync` lane | **pending** |
| U6 | Web: `AvatarUpload` (self-only upload) | **pending** |
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
