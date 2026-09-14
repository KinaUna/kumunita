# GU U09 — Core seam tests (the 11 pinned names)

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Write the lane's **11 pinned tests** in one new test class
(`GuardianControlsTests`) that asserts U02–U06's seams: the `GuardianLink`
doc, `AccessVia.Guardian`, the formation/suspension/independence seams (U04),
the membership-curation guardian branch (U05), and the invitation gate +
approve (U06). The **load-bearing** test is `G1_GuardianCannotReadChildContent`
(invariant G·1: guardian standing **never** resolves a content read). These
tests are the **drift-guard** — they are named in the design doc's
**Pinned contract** (U01) and **must** carry exactly those names.

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`## Pinned contract` →
   `### Pinned seam tests (exact names)`** (U01 pinned all **11** names + a
   one-line intent per test) + **`### Acceptance gate (U10 records)`** — the
   *primary* source. Match the **names verbatim**.
2. `docs/adr/0028-...md` §C (the five actions + audit verbs) + §D (G·1–G·5,
   each maps to one or more of the 11) + §E (the audit-verb list) — the ADR
   authority each test asserts.
3. `tests/Kumunita.Core.Tests/UserInfoServiceGroupInvitationsM2bTests.cs` (top
   ~90 lines) — the **test-harness shape to mirror exactly**: the
   `class …Tests(PostgresFixture fixture) : IClassFixture<PostgresFixture>`
   class header; `var store = await BootStoreAsync();` + `var svc = new
   UserInfoService(store);` per test; the `[Fact] public async Task …`
   method shape; the `using` set (`Kumunita.Core; Kumunita.Core.Authorization;
   Kumunita.Core.UserInfo; Marten; Xunit;`). U09's tests follow this
   **exactly**.
4. `tests/Kumunita.Core.Tests/UserInfoServiceTests.cs` (the `BootStoreAsync`
   definition + any shared helpers — the scratch-DB `PostgresFixture`
   established by `UserInfoServiceGroupsU9Tests`). U09 reuses `BootStoreAsync`
   **as-is** (do not redefine it).
5. `src/Kumunita.Core/UserInfo/UserInfoService.cs` § the GU seams (U04/U05/
   U06) + `src/Kumunita.Core/Authorization/AuthorizationService.cs` (the
   `CanAsync` / `CanSeeAsync` content-decision surface U09's G·1 test
   **negates** against) — the seams the tests exercise.

## The 11 tests (pin this — the names are the contract)

Each is a `[Fact] public async Task <Name>()` mirroring the M2b harness. The
**intent** column is the design doc's one-liner (U01); the **asserts** column
is the minimum each must pin (more is fine — the names are the contract):

| # | Name (verbatim) | Intent | Minimum asserts |
|---|---|---|---|
| 1 | `G1_GuardianCannotReadChildContent` | **Load-bearing (G·1):** guardian standing never resolves a content read | A guardian (active link to the child) **cannot** read the child's posts / private profile via `AuthorizationService.CanAsync`/`CanSeeAsync` (the content decision stays `Deny`); the child's own post is `Invisible`/`Deny` to the guardian even though the guardian **can** curate memberships |
| 2 | `G2_SuspendIsLiveAndBlocksStanding` | G·2: suspend is live standing off `Profile.Blocked` | After `SuspendChildAsync`, the child's `Profile.Blocked` is `true`; a read of the child's standing (the self-lane) reflects the block on the **next** call (C4 strong consistency) |
| 3 | `G2_DissolveRestoresSelfLanesOnNextRead` | G·2: dissolve restores the child's self-lanes | After `DissolveGuardianLinkAsync`, the child's **self-accept** lane (`AcceptGroupInvitationAsync`) is **open again** on the next call (the U06 gate is off — no active link) — the "come-of-age" handoff. (The `Blocked` flag is orthogonal to dissolve — see test 11; a dissolve writes nothing to membership, design doc §D.) |
| 4 | `G3_NonChildTargetIsRefused` | G·3: deny-by-default — the guardian cannot act for a non-child | `ApproveGroupInvitationAsync` / `SuspendChildAsync` / `UnsuspendChildAsync` / membership curation **throw `UnauthorizedAccessException`** (the no-standing gate, U04/U06) when the actor is **not** the `GuardianId` of an active link for the `ChildId` (a foreign pair is refused — the Web surfaces this as a 404) |
| 5 | `G3_ContentReadIsNeverGuardian` | G·3: action-scoped — no content-read lane records `Via: Guardian` | The content-decision surface (`CanAsync`/`CanSeeAsync`) **never** returns an `AccessVia.Guardian` decision — the 9th value exists but is **not** reachable on a read (the "never on a content decision" pin) |
| 6 | `G4_FormationCommitsAccountLinkAndAuditTogether` | G·4: formation is one commit (link row + audit row, C3) | `CreateGuardianLinkAsync` commits **both** the `GuardianLink` (Active) **and** the `guardian.create` audit row (`Via: Guardian`) in one session — a read of the link + a read of the audit log both see them; a duplicate active link is refused (idempotency guard) |
| 7 | `G5_GlobalAdminDissolvesAndUnSuspends` | G·5: the safety valve — a GlobalAdmin dissolves **and** un-suspends | `DissolveGuardianLinkAsync(linkId, adminId, viaAdmin: true)` succeeds for a **GlobalAdmin** (not the `GuardianId`); the link is `Dissolved`; **a separate** un-suspend act (the M1 GlobalAdmin `UnblockAsync`, `Via: Admin`) sets the suspended child's `Profile.Blocked` to `false`; both acts are audited `Via: Admin` (dissolve `guardian.dissolve`, un-suspend the M1 verb) — the design doc §D: "the admin un-suspends a suspended child," both audited `Via: Admin` |
| 8 | `Invitation_GatedForSupervisedChild` | G·2: a supervised child cannot self-accept | A child with an **active** link → `AcceptGroupInvitationAsync` **throws** (the gate); the same invitee **without** a link → it succeeds (the gate is conditional on the active link) |
| 9 | `Invitation_GuardianApproveLandsMembership_ViaGuardian` | G·2: the guardian approves, the membership lands | `ApproveGroupInvitationAsync` → the `GroupInvitation` is `Accepted` with `ResolvedBy = guardianId`; the `GroupMembership` is live on the next `GetGroupIdsAsync`; the audit row is `group.invite.approve` `Via: Guardian` |
| 10 | `Membership_AddRemoveChild_ViaGuardian` | G·3: membership curation records `Via: Guardian` | `AddGroupMemberAsync` / `RemoveGroupMemberAsync` (the child as `userId`, the guardian as `addedBy`/`removedBy`) → the audit rows are `Via: Guardian` (the narrower standing, U05); the add/remove is live on the next read |
| 11 | `SuspendSetsProfileBlocked_EnforcementIdentical` | G·2: suspend's enforcement is identical to the existing block lane | `SuspendChildAsync` sets `Profile.Blocked = true` — **byte-identical** to the M1 `BlockAsync` effect on the standing (the child loses standing on the next read); the only difference is the audit verb (`guardian.suspend` `Via: Guardian` vs `block` `Via: Admin`) |

## Deliverables (1 file, new)

### `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` (new)

- `class GuardianControlsTests(PostgresFixture fixture) :
  IClassFixture<PostgresFixture>` (the M2b header shape).
- The 11 `[Fact] public async Task <Name>()` methods, **each** starting with
  `var store = await BootStoreAsync(); var svc = new UserInfoService(store);`
  (or the `AuthorizationService` the G·1/G·3 content-read tests need — mirror
  the existing `…AuthorizationService` test's construction if one exists in
  this assembly).
- Each test asserts the **minimum** in the table above (more is fine — the
  **names** are the contract, the design doc's Pinned contract is the
  authority).
- The file's top-level doc-comment: a `/// <summary>` pinning that these are
  the lane's 11 pinned tests (G·1 load-bearing first) + a reference to ADR
  0028 §C–§E + the design doc's **Pinned contract** section (the M2b
  doc-comment voice).
- **No** new test **beyond** the 11 (drift-guard: a 12th test is a drift
  pause, not a silent add).

## Exit

`dotnet build Kumunita.slnx -c Debug` green. `GuardianControlsTests.cs`
compiles + the 11 pinned names are discoverable. **Run** the assembly via the
reliable path (AGENTS.md — `dotnet test` is broken on this machine):

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

Record the **pass / red** result (which of the 11 pass, which red) in the
handoff note — **U10 records the final gate**, so U09's note is the *pre-gate*
snapshot. If any are red, **do not** weaken the assertion to make it pass —
the red is a **drift signal** (the seam impl in U02–U06 is wrong, not the
test); note it and stop (a drift pause), letting the next agent reconcile.
Clean up any leftover Postgres containers (`docker container prune`).

Handoff note (append): 6–8 lines starting `## U09 — 11 pinned seam tests` —
(a) the 11 names (one line each, with pass/red), (b) the **load-bearing** G·1
test's exact assertion (one line), (c) any **red** test + the suspected seam
it implicates (U04/U05/U06), (d) a confirmation **no 12th test** was added
(drift-guard), (e) the reliable-path command + the container-prune note,
(f) any compile warnings.
