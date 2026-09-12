# U3 execution plan (working) — Core tests: file store + media store (byte-store correctness)

> My (unit U3's) working plan for this pass. The **authoritative spec** is
> `media-u3-plan.md` (the authored unit) + **design doc**
> `../../design/media-file-storage-design.md` **§2.5** (the pinned test names) +
> §2.6 (the gate) + §2.7 (drift-guard). Prior handoff section read: **U2**
> (incl. its verification pass) in `media-file-storage-handoff-notes.md`.

## Scope (in / out)

- **In (mine):** exactly the §2.5 Core test files:
  - `tests/Kumunita.Core.Tests/Media/LocalVolumeFileStoreTests.cs` — 4 tests
  - `tests/Kumunita.Core.Tests/Media/LocalVolumeMediaStoreTests.cs` — 3 tests
- **Out:** U4+ (`Profile.AvatarId` / `SetProfileAvatarAsync`), U5's lane tests,
  the U9 Web seam tests, any src/ change (U1/U2 code is already done and
  build-green). Drift-guard §2.7 rule 1: I only touch the two new test files
  (no shared fixture file needed — see Steps; the plan's "optional" fixture is
  skipped deliberately, noted in the handoff).
- **No test beyond the §2.5 names** (§2.7 rule 3). A note on names: the
  authored `media-u3-plan.md` §"Risks" lists a *stale* 5-name draft; the
  **design doc §2.5 is the pinned source** and wins (the plan itself says so).
  I use the §2.5 names verbatim:
  - `LocalVolumeFileStore_Put_open_read_roundtrips`
  - `LocalVolumeFileStore_Put_idempotent_same_id_no_second_writer`
  - `LocalVolumeFileStore_OpenRead_missing_throws_FileNotFound`
  - `LocalVolumeFileStore_Delete_missing_throws_FileNotFound`
  - `LocalVolumeMediaStore_Put_dedups_by_content_hash`
  - `LocalVolumeMediaStore_OpenRead_missing_doc_throws_KeyNotFound`
  - `LocalVolumeMediaStore_Put_sets_CreatedById_and_SizeBytes`

## Constraints I keep pinned while writing

- **§2.5 verbatim names + file paths.** File paths are also pinned by §2.5
  (`tests/Kumunita.Core.Tests/Media/…`).
- **File-store tests = temp dir only** (no live volume, no Docker): `LocalVolumeFileStore`
  via `Options.Create(new MediaOptions { RootPath = <tempdir> })` — the exact
  idiom `SmtpHealthCheckTests` already uses for `IOptions<>` in this project.
- **Media-store tests = the existing `PostgresFixture`.** Verified: it is a
  one `postgres:18` Testcontainers shared per class + `NewDatabaseAsync()` →
  fresh scratch DB + `DocumentStore.For(opts => …)` +
  `ApplyAllConfiguredChangesToDatabaseAsync` (the `AnnouncementServiceTests.BootStoreAsync`
  shape). `MediaObject` needs only `MediaDocTypes.Configure(opts)` in the schema.
  Marten **9.31.2 → async-only sessions** (`await using var s = store.QuerySession();`)
  per U2's handoff.
- **Dedup assertion (C-MED·4):** second `PutAsync` of identical bytes returns
  the **first** doc (same `Id`, same `CreatedById` — the first actor wins) —
  the doc is *returned*, never overwritten. **(Media store lane.)**
- **File-store idempotency (C-MED·4, `U1` `PutAsync` shape — verified against
  the implemented `LocalVolumeFileStore`):** the content id *is* the
  SHA-256 of the payload, so "same id" ⇒ **same bytes**. `PutAsync` is an
  idempotent atomic **overwrite** (`File.Move(tmp, final, overwrite: true)`),
  NOT first-writer-wins for same-hash-different-bytes (that state cannot
  exist under C-MED·4). The idempotency test asserts exactly one file, no
  `.tmp` litter, and the payload unchanged.
- **Fail-closed (C-MED·7):** `OpenReadAsync`(file store) and `DeleteFileAsync`
  throw `FileNotFoundException`; `LocalVolumeMediaStore.OpenReadAsync` throws
  `KeyNotFoundException` on a missing doc.
- **No `.tmp` litter** after `PutAsync` (atomic replace; the idempotency test
  asserts no `*.tmp` remains in the shard dir).
- **Cleanup discipline:** every temp dir I create is deleted in `try/finally`
  (recursive) after closing any open streams, so a failing test can't wedge
  Windows path deletion.
- **xunit.v3:** `[Fact]`, `Assert.*` (v3 keeps `Assert.Equal/Contains/ThrowsAsync`),
  `TestContext.Current.CancellationToken` where existing tests use it.
- **Gate (§2.6, AGENTS.md):** `run_build` green on `Kumunita.Core` +
  `Kumunita.Core.Tests`; then the suite runs via
  `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  (the reliable in-process path — **not** `dotnet test`, not `run_tests`).
  I record the real pass count and that the 7 §2.5 names are discovered.
  (The Core suite spins Testcontainers — ~20 s expected; `docker container prune`
  afterwards if the process is killed.)

## Entry reads (done in this pass)

| Read | Why |
|------|-----|
| plan `plan-media-file-storage.md` U3 + `media-u3-plan.md` | the unit scope/deliverables |
| design doc §2.2 / §2.5 / §2.6 / §2.7 | exact seams tested + pinned names + gate |
| handoff `## U1` + `## U2` (+ verification pass) | the implemented seams; Marten 9 async-session idiom |
| `src/Kumunita.Core/Media/{MediaOptions,MediaObject,LocalVolumeFileStore,LocalVolumeMediaStore}.cs` | the exact behavior under test (incl. U1's explicit fail-closed `OpenReadAsync`) |
| `tests/Kumunita.Core.Tests/PostgresFixture.cs` | the doc-store fixture to mirror |
| `tests/Kumunita.Core.Tests/AnnouncementServiceTests.cs` (tail) | `BootStoreAsync` / `Plant` harness shape |
| `tests/Kumunita.Core.Tests/SmtpHealthCheckTests.cs`, `CommunityOptionsTests.cs`, `Kumunita.Core.Tests.csproj` | `Options.Create` idiom + project refs (Testcontainers/xunit.v3 present) |

## Steps (each leaves the tree in a buildable state)

1. `tests/Kumunita.Core.Tests/Media/LocalVolumeFileStoreTests.cs` — the 4
   §2.5 tests on a per-test temp dir (`Path.GetTempPath() + Guid`), the 7
   `KumunitaFeature`-free shape: plain class, no fixture.
2. `tests/Kumunita.Core.Tests/Media/LocalVolumeMediaStoreTests.cs` — the 3
   §2.5 tests on `PostgresFixture` (fresh scratch DB per test +
   `MediaDocTypes.Configure(opts)`), composing `LocalVolumeFileStore`
   (temp dir) + the booted `IDocumentStore`.
3. **Exit:** `run_build` green; `dotnet exec Kumunita.Core.Tests.dll`
   green — the 7 §2.5 names discovered + passing (record the pass count).
4. Append **`## U3`** to `media-file-storage-handoff-notes.md`: the fixture
   shape chosen (temp dir / `PostgresFixture`), the test file paths, the
   pin-names used, the pass count (**verified via `dotnet exec`**, not
   assumed), the stale-name note. Flip the Status-table U3 row to done.

## Verification / risks

- **Stale names in the authored U3 plan §Risks** — resolved by doc-wins
  (§2.5), noted in the handoff per §2.7 rule 3 ("rename only with a note").
- **First full-suite run: 1 failure — my test, not the code:**
  `LocalVolumeFileStore_Put_idempotent_same_id_no_second_writer` asserted
  first-writer-wins for same-id-different-bytes, contradicting the C-MED·4
  contract (id = SHA-256 ⇒ bytes) and U1's `overwrite: true` shape. Test
  corrected to the implemented (correct) contract; name kept verbatim. See
  the new Constraints bullet above.
- **Marten schema:** only `MediaObject` is needed; I add
  `MediaDocTypes.Configure(opts)` and nothing else to the scratch-DB boot.
- **Windows temp-dir deletion** while a `FileStream` is still open — close
  streams before `Directory.Delete` (the roundtrip test disposes the read
  stream first).
- **Docker left behind:** if I kill `dotnet exec` early, prune containers.
