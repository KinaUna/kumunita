# U5 — Core test: the `SetProfileAvatarAsync` lane

> Self-contained: this file + the **entry reads** below is the whole context.
> The **exact** test names are `docs/design/media-file-storage-design.md` §2.5.
> A prior handoff section (if any) is **U4**.

## Understanding

Lock the **avatar lane** on the UserInfo side: set / clear / missing-profile
behaviour. This is the **one** seam a future group-logo / post-attachment /
badge lane will copy (C-MED·8) — uniting the Core media store (U2) with the
UserInfo profile (U4) before any Web surface exists.

## Assumptions

- The **exact** test names are **fixed** by the design doc §2.5:
  `SetProfileAvatarAsync_Sets_And_Clears_And_FailsClosed_On_Missing`.
  A unit renaming the test drifts the seam contract (C-MED·8).
- The lane's **missing-profile** behaviour is **fail closed** (U4 handoff
  notes confirm — either throw or no-op; the test must assert **that**
  behaviour, not a new one).

## Approach

Write the test: create a profile (or reference an existing test fixture's
profile), call `SetProfileAvatarAsync` with a real `MediaObject` id → assert
`Profile.AvatarId` is set; call with `null` → assert cleared; call with a
**missing** `subjectId` → assert the fail-closed behaviour U4 pinned
(throw or no-op — **match the U4 handoff**).

## Key files (entry reads — no more)

- `docs/design/media-file-storage-design.md` — §2.5 (the **exact** lane test
  name) + §2.2 (the `Profile.AvatarId` + `SetProfileAvatarAsync` shapes).
- `docs/plans-milestones/in-progress/media-u4-plan.md` + the **U4** handoff
  (the `Profile.AvatarId` + lane signature + the missing-profile behaviour).
- the existing `tests/Kumunita.Core.Tests` harness — mirror the existing
  `UserInfoService` test shape for the profile fixture (the
  `PostgresFixture`-backed profile store).

## Deliverables (1 test file, new or append)

- `tests/Kumunita.Core.Tests/UserInfo/ProfileAvatarLaneTests.cs`
  (or fold into the existing `Kumunita.Core.Tests` `UserInfoService` test
  file — the unit chooses, notes it in the handoff).

## Risks & open questions

- **Missing-profile behaviour:** the unit **must** assert the **same**
  behaviour U4 pinned (throw or no-op — **read the U4 handoff** before
  writing the test). A test that **expects** a different behaviour (e.g.
  expects a throw but U4 implemented a no-op) **fails** the build — align
  the test with U4's implementation, not a new expectation.
- **`null` `avatarId`:** the test **must** assert the clear persists (a
  fresh load of the profile still sees `AvatarId == null`). A test that
  only asserts the in-memory `Profile.AvatarId` is `null` (without a
  re-load) does not prove the clear was **saved**.

## Steps

1. Read the U4 handoff (the lane signature + the missing-profile behaviour).
2. Write the test (set / clear / missing-profile — the three halves of the
   §2.5 name).
3. `run_build` → green; the §2.5 lane test name discovers + passes via the
   §2.6 `dotnet exec Kumunita.Core.Tests.dll` path (AGENTS.md).
4. Append **`## U5`** to the handoff notes: the test file path, the test name
   (verbatim), the three assertions (set / clear / missing-profile), and the
   pass count.
