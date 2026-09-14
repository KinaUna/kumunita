# Guardian controls (`GU`) — rolling handoff notes

> **The scratch tier** of the GU lane's three-tier contract (the design doc is
> primary, the register is secondary, this file is scratch). One section per
> unit, **appended, never rewritten**. Each unit writes exactly one short
> section before it exits; the next unit reads only that section + its own
> entry-reads list. A `## U<m> — Drift pause` section is a **blocker**: the
> next unit reads it first and either resolves it (recording the resolution in
> its own section) or carries it forward (naming it in its exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##` section
> from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-14
- **Register:** `docs/plans-milestones/in-progress/plan-guardian-controls.md` (U01–U11)
- **Design doc (primary):** `docs/design/guardian-controls-design.md` (U01 finalizes its `## Pinned contract`)
- **ADR:** `docs/adr/0028-guardian-controls-account-scope-supervision.md` (already accepted; Amends 0006, 0003/0012, m2b)
- **Scope:** a parent adds an account for a child (the usual confirm-email
  process) and supervises it at the **account level**: suspend/lock, curate the
  child's community & group memberships, approve a group invitation sent to the
  child, and hand the account over to independence when the child comes of age.
  Standing is a 9th `AccessVia` value + a `GuardianLink` doc on the existing
  `M1DocTypes` surface; the five supervisory seams are ADDs on `IUserInfoService`
  (ADR 0006-E lane); the content path (`CanAsync` / `CanSeeAsync`) is
  **untouched** (G·1 — load-bearing).
- **Out of scope (the named deferrals, ADR 0028 §E):** no content read (G·1),
  no reading who contacted the child, no delegation of authorship, no blanket
  "block all communities" toggle, no stored age. Each re-litigates as an
  ADR 0028 amendment.
- **Test model:** Core seam tests in `Kumunita.Core.Tests` against
  `PostgresFixture` (U09 — the **eleven** pinned seam tests, incl. the
  load-bearing `G1_GuardianCannotReadChildContent` and `G3_ContentReadIsNeverGuardian`);
  Web VM data-shape tests in `Kumunita.Web.Tests` (U10 — the four pure VM
  projection tests). The lane's **acceptance gate** (U01 pins it in the design
  doc `### Acceptance gate`; U10 records the run result) = the eleven core
  tests + the four Web tests + the build line. Runner quirk (AGENTS.md) applies:
  run via `dotnet exec tests\…\.dll`, not `dotnet test`.

<!-- U01 appends its section below this line. One `##` section per unit, in
     order (U01, U02, … U11). Never rewrite a prior section. -->

## U01 — pinned contract

- **Date:** 2026-09-14. Appended `## Pinned contract (U01 — finalizes for U02–U11)` to `docs/design/guardian-controls-design.md` (between `## Seams & contracts (mandatory)` and `## Feedback loops`). **Docs-only: no code, no build.**
- **(a) Five seam names (verbatim):** new methods — `CreateGuardianLinkAsync(childId, guardianId)`, `SuspendChildAsync(childId, guardianId)` / `UnsuspendChildAsync(childId, guardianId)`, `ApproveGroupInvitationAsync(groupId, childId, guardianId)`, `DissolveGuardianLinkAsync(linkId, actorId, viaAdmin)`; the two **branches** (signatures unchanged, a `Via: Guardian` branch added to each standing gate) — `AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` and `AddGroupMemberAsync` / `RemoveGroupMemberAsync`; the **gate** on `AcceptGroupInvitationAsync` (self-accept refused for a supervised child); `DeclineGroupInvitationAsync` stays open.
- **(b) `AccessVia.Guardian`:** the **9th** value, appended after `Group` in `src/Kumunita.Core/Authorization/Decision.cs` (value-addition, no renumber; M1 `Admin` 7th / ADR 0013 `Group` 8th precedent).
- **(c) 11 pinned test names** (`tests/Kumunita.Core.Tests/GuardianControlsTests.cs`): `G1_GuardianCannotReadChildContent`, `G2_SuspendIsLiveAndBlocksStanding`, `G2_DissolveRestoresSelfLanesOnNextRead`, `G3_NonChildTargetIsRefused`, `G3_ContentReadIsNeverGuardian`, `G4_FormationCommitsAccountLinkAndAuditTogether`, `G5_GlobalAdminDissolvesAndUnSuspends`, `Invitation_GatedForSupervisedChild`, `Invitation_GuardianApproveLandsMembership_ViaGuardian`, `Membership_AddRemoveChild_ViaGuardian`, `SuspendSetsProfileBlocked_EnforcementIdentical`.
- **(d) G·1 pin:** `AccessVia.Guardian` appears on **no** `CanAsync` / `CanSeeAsync` content decision — the lane's load-bearing honesty; a unit that puts it on the content path is a drift pause, not a deviation (unit-series rule §5).
- **(e) Drift pause:** none — the design doc's prose was consistent with ADR 0028 and the frozen `IUserInfoService` surface; nothing to resolve or carry forward.
