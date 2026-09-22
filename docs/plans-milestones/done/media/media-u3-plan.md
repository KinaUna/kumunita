# U3 — Core tests: file store + media store (byte-store correctness)

> Self-contained: this file + the **entry reads** below is the whole context.
> The **exact** test names are `docs/design/media-file-storage-design.md` §2.5;
> the **gate shape** is §2.6. A prior handoff section (if any) is **U2**.

## Understanding

Lock the **byte-store correctness** the design doc §2.5 pins, on a **temp dir**
(no live volume required) for `LocalVolumeFileStore`, and against a `Marten`
over `MediaObject` (or an in-memory doc store if the existing `Kumunita.Core.Tests`
Postgres fixture is the model — check the existing harness first) for
`LocalVolumeMediaStore`. This is the "the gate is the product" unit for the
Core module — it proves C-MED·4 (dedup) and C-MED·7 (the `mt` doc) hold **before** any Web surface exists.

## Assumptions

- The **exact** test names are **fixed** by the design doc §2.5 (list below).
  A unit renaming a test name drifts the seam contract (C-MED·1).
- The existing `Kumunita.Core.Tests` Postgres/`IDocumentStore` fixture is the
  model to mirror — **check the existing test harness first** (the Postgres
  fixture may be the model; if so, the `MediaObject` doc store test can use
  the same `PostgresFixture`-backed `IDocumentStore`).
- `LocalVolumeFileStore` tests use a **temp dir** (no live volume) — the
  sharded-path + atomic-write + fail-closed-behaviour are all testable on a
  temp dir.

## Approach

Write a shared `MediaTestTempDir` fixture (temp dir + `MediaOptions` +
`LocalVolumeFileStore`) and a doc-store fixture (a `PostgresFixture`-backed
or in-memory `IDocumentStore` over `MediaObject`). Implement the §2.5 Core
tests.

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.5 (the **exact** Core test
  names) + §2.6 (the gate shape).
- `docs/plans-milestones/done/media-u1-plan.md` + `media-u2-plan.md`
  + the **U1** + **U2** handoff (the seams to test).
- the existing `tests/Kumunita.Core.Tests/` for the harness/fixture shape to
  mirror (the `PostgresFixture` / `IDocumentStore` back the `MediaObject`
  doc store test — **verify** the existing fixture before writing the
  `MediaObject` doc store test).

## Deliverables (≤ 4 new test files)

- `tests/Kumunita.Core.Tests/Media/LocalVolumeFileStoreTests.cs`
- `tests/Kumunita.Core.Tests/Media/LocalVolumeMediaStoreTests.cs`
- (optional shared `MediaTestTempDir` + doc-store fixture under
  `tests/Kumunita.Core.Tests/Media/`)

## Risks & open questions

- **The §2.5 Core test names** (fixed by the design doc — match verbatim):
  `LocalVolumeFileStore_Writes_Shards_Atomic_And_RoundTrips`,
  `LocalVolumeFileStore_OpenRead_Missing_FailsClosed`,
  `LocalVolumeFileStore_Delete_Missing_FailsClosed`,
  `LocalVolumeMediaStore_Put_Dedups_By_ContentHash`,
  `LocalVolumeMediaStore_Get_Missing_Throws`.
- **Doc store fixture:** if the existing `Kumunita.Core.Tests` harness
  `PostgresFixture` is Postgres-bound, the `MediaObject` doc store test can
  use the same fixture (verify the fixture before writing). If the harness
  is lighter (in-memory), adapt. The unit **must** note which fixture it
  used in the handoff (§2.6 gate depends on it).
- **Atomic-write test:** a crash-mid-write test is not feasible in a unit
  test; the §2.5 name
  `LocalVolumeFileStore_Writes_Shards_Atomic_And_RoundTrips` covers the
  "sharded + round-trip" halves (the atomicity is covered by the U1
  implementation's tmp-rename-fsync, which the handoff notes).

## Steps

1. Read the existing `tests/Kumunita.Core.Tests/` fixture shape (verify which
   back the `MediaObject` doc store test).
2. Write the shared `MediaTestTempDir` + doc-store fixture.
3. Write `LocalVolumeFileStoreTests.cs` (sharded path + atomic write +
   round-trip + fail-closed read/delete/exists).
4. Write `LocalVolumeMediaStoreTests.cs` (dedup-by-hash + missing-throw).
5. `run_build` → green; the §2.5 Core test names discover + pass via the
   §2.6 `dotnet exec Kumunita.Core.Tests.dll` path (AGENTS.md — **not**
   `dotnet test`).
6. Append **`## U3`** to the handoff notes: the temp-dir / doc-store fixture
   shape, the test file paths, the test names (verbatim), and the pass count
   (verified via `dotnet exec`, not assumed).
