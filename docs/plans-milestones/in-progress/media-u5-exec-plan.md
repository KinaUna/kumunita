# U5 execution plan (working) — Core test: the `SetProfileAvatarAsync` lane

> My (unit U5's) working plan for this pass. The **authoritative spec** is
> the U5 entry in `plan-media-file-storage.md` + the authored
> `media-u5-plan.md` + **design doc**
> `../../design/media-file-storage-design.md` **§2.5** (the pinned lane test
> names, lines 542–545) + §2.2 (the lane shape, lines 440–461) + §2.7
> (drift-guard — **the doc wins** on a shape/name mismatch). Prior handoff
> section read: **U4** (latest) in `media-file-storage-handoff-notes.md`
> (plus U2/U3 for the harness shapes this file mirrors).

## Scope (in / out)

- **In (mine):** exactly one new test file (the register's Deliverables and
  the §2.5 pinned path — no folding into an existing file needed):
  - `tests/Kumunita.Core.Tests/UserInfo/ProfileAvatarLaneTests.cs` — the
    three §2.5 lane names (set / clear / missing-profile), on the
    existing `PostgresFixture` (scratch DB per test) + a scratch `mt`
    schema over `M1DocTypes` (the `Profile` doc; the lane writes only the
    `string?` `AvatarId` — **no `MediaObject` doc is stored or loaded**, and
    the store schema needs no `MediaDocTypes` surface).
- **Out:** U4's implementation (done — read-only), U6–U9's Web surface,
  U10's ADR/docs close. Drift-guard rule 1: I modify **no** file outside
  the Deliverables set (+ the exec-plan tier + the handoff section + the
  handoff Status-table row, the standing per-unit convention).

## Constraints I keep pinned while writing

- **Test names verbatim — the DESIGN DOC §2.5 is the pin source** (doc wins,
  §2.7 rule 3; the same resolution U3 recorded for its stale draft):
  - `UserInfoService_SetProfileAvatar_sets_id`
  - `UserInfoService_SetProfileAvatar_null_clears`
  - `UserInfoService_SetProfileAvatar_missing_profile_throws_KeyNotFound`
  - **Stale-name note:** `media-u5-plan.md` §Assumptions lists a *single*
    drafted name (`SetProfileAvatarAsync_Sets_And_Clears_And_FailsClosed_On_Missing`);
    the design doc §2.5 (the pinned source, three names) **wins**, exactly
    per §2.7 rule 3 "rename only with a note" — recorded here and in the
    handoff (U3's stale-name note is the precedent).
- **Assert U4's pinned behaviour, nothing new (read U4 handoff before
  writing — done):**
  - set: fresh `GetProfileAsync` read returns `AvatarId == <hash>` (C4 —
    live on the very next read; the read itself is the persistence proof).
  - null: call with `null` **clears** and the clear **persists** — a fresh
    `GetProfileAsync` read sees `AvatarId == null` (per
    `media-u5-plan.md` §Risks: an in-memory-only assertion would not prove
    the save landed).
  - missing profile: **`KeyNotFoundException`** (U4 pinned the design doc's
    exception type over the group lanes' `InvalidOperationException`) —
    assert the throw **and** that the lane did not *create* a profile
    (`GetProfileAsync` → null — fail closed, not create).
- **Avatar id is a plain string** (a content hash, C-MED·4); reuse the
  `KnownHash` constant idiom from U3's `LocalVolumeMediaStoreTests` — no
  hashing, no volume, no `IMediaStore` (the lane references the catalog by
  id only).
- **Harness shape (mirror the assembly idiom, not the design doc's prose):**
  `UserInfoServiceGroupDescriptionTests` — `PostgresFixture`
  (`IClassFixture`) + `NewDatabaseAsync` scratch DB per test +
  `DocumentStore.For(opts => { Connection; DatabaseSchemaName = "mt";
  opts.Storage.Add<KumunitaFeature>(); opts.Storage.Add<AuthorizationFeature>();
  M1DocTypes.Configure(opts); })` + one
  `ApplyAllConfiguredChangesToDatabaseAsync`. Profile seeded through the
  service's own `UpsertProfileAsync(profile, new ProfileUpdate(5×null))` —
  the exact bootstrap idiom at `UserInfoServiceTests` L62 (an all-null patch
  ⇒ the profile record supplies every field).
- **Namespace/file shape (mirror U3's Media subfolder):** file under
  `tests/Kumunita.Core.Tests/UserInfo/` (the §2.5 pinned path), `namespace
  Kumunita.Core.Tests.UserInfo;`, `public sealed class` primary-ctor +
  `IClassFixture<PostgresFixture>`.
- **Gate (AGENTS.md § "Running the tests"; §2.6 path, NOT `dotnet test`):**
  `dotnet build Kumunita.slnx -c Debug` → 0 errors; then `dotnet exec` on
  both assemblies (U4's regression baseline: Web 60/60, Core 220/220 →
  expecting **Core 223/223** = 220 + the 3 lane tests), plus a filtered
  `-filterVSTest` run on the three lane names to prove they discover (the
  U3 precedent for the "names discover and pass" Exit clause).

## Entry reads (done in this pass)

| Read | Why |
|------|-----|
| plan `plan-media-file-storage.md` §U5 + `media-u5-plan.md` | the unit scope / deliverables / risks |
| design doc §2.5 lines 542–545 + §2.2 lines 440–461 | the three pinned names (verbatim) + the lane shape |
| handoff `## U4` (+ U2 verification, U3) | U4's pinned behaviour (`KeyNotFoundException`, `actorBy` accepted-not-persisted, null-clear persists) + harness precedent |
| `tests/Kumunita.Core.Tests/UserInfoServiceGroupDescriptionTests.cs` (full) | the closest UserInfo-lane test shape (`BootStoreAsync` on `PostgresFixture`) |
| `tests/Kumunita.Core.Tests/UserInfoServiceTests.cs` (L40–120) | the `UpsertProfileAsync` + all-null `ProfileUpdate` seeding idiom |
| `tests/Kumunita.Core.Tests/Media/LocalVolumeMediaStoreTests.cs` (L1–60) | the `KnownHash` constant + namespace-sealed-class shape |
| `src/Kumunita.Core/UserInfo/Profile.cs` (full) | POCO defaults (`Visibility` defaults to `new Audience()`), `ProfileUpdate` 5-positional form |
| `src/Kumunita.Core/UserInfo/UserInfoService.cs` L872–891 | the shipped lane impl (load / throw / set / store / save) — the tests pin exactly this |
| `src/Kumunita.Core/M1DocTypes.cs` (full) | `Profile` identity mapping (`Identity(p => p.SubjectId)`) — confirms the scratch schema needs `M1DocTypes` |

## Steps (each leaves the tree in a buildable state)

1. **Exec plan** (this file) — `docs/plans-milestones/in-progress/` (done).
2. `ProfileAvatarLaneTests.cs` — the three §2.5 names (verbatim) + the
   shared `BootStoreAsync` / `SeedProfileAsync` helpers, per the Constraints
   above.
3. **Gate:** `run_build` (full `Kumunita.slnx`) → 0 errors (the register's
   Exit); then the AGENTS.md `dotnet exec` path on both test assemblies +
   the filtered lane-names run; expect Core 223/223, Web 60/60.
4. Append **`## U5`** to `media-file-storage-handoff-notes.md`: the test
   file path, the three names (verbatim), the stale-name note, the
   behaviour pinned per test, the pass counts (verified, not assumed), and
   "nothing staged or committed" (user reviewing). Flip the Status-table
   U5 row to done (the U4 precedent).

## Verification / risks

- **Stale single name in `media-u5-plan.md` §Assumptions** — resolved by
  doc-wins (§2.7 rule 3; U3 precedent), noted in the handoff per rule 3.
- **Asserting new vs pinned behaviour:** every assertion traces to U4's
  shipped impl (`UserInfoService.cs` L872–891) — throw type `KeyNotFound`,
  set/clear both `Store` + `SaveChangesAsync` — so the tests pin existing
  behaviour, not an expectation; if one fails, the fix is the test side
  (unless it exposes a real U4 defect — then escalate, don't silently
  paper over).
- **`avatarId` never validated against the catalog** — by design (the lane
  "writes `Profile.AvatarId` only"; C-MED·8 — existence is the store seam's
  concern, U6/U7); the tests use a well-formed lowercase-hex id and assert
  nothing about the `MediaObject` catalog.
- **Marten 9 async-only sessions** — the lane and the test harness use the
  assembly-proven `await using` shape (U2/U3/U4 notes); no sync `Load`.
- **Working-directory test hygiene:** no temp dirs this unit (pure `mt`
  doc lane over scratch Postgres DBs, the fixture's cleanup covers them).
