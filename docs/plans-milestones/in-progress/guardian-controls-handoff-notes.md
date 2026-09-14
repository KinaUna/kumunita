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

## U02 — GuardianLink + M1DocTypes

- **Date:** 2026-09-14. Created `src/Kumunita.Core/UserInfo/GuardianLink.cs` (POCO + `GuardianLinkStatus` enum, matching the §Pinned contract verbatim) and added one additive line to `src/Kumunita.Core/M1DocTypes.cs`. **`dotnet build Kumunita.slnx -c Debug` green.** No new test (U09 pins the lane's tests).
- **(a) POCO fields (verbatim):** `string Id` (surrogate PK), `string GuardianId` (the creator — G·4), `string ChildId` (the target), `GuardianLinkStatus Status` (the two-state machine), `DateTimeOffset CreatedAt`, `DateTimeOffset? DissolvedAt`, `string? DissolvedBy`. Enum: `GuardianLinkStatus { Active, Dissolved }` (file-scoped, like `InvitationStatus`).
- **(b) `M1DocTypes` line:** `opts.Schema.For<GuardianLink>().UniqueIndex(g => g.GuardianId, g => g.ChildId);` — placed **immediately after** `opts.Schema.For<DelegationGrant>();`, with the one-line comment `// GU (ADR 0028): one row per (guardian, child); the pair is the business key (GroupInvitation convention)`. No other line in `Configure` changed.
- **(c) Unique-index fields:** `(GuardianId, ChildId)` (the business-key pair, per the `GroupInvitation` convention; the surrogate `Id` is the Marten identity).
- **(d) Confirmed:** **no** doc-side `IsActive` boolean and **no** `Scope` field (G·2 — the service is the resolver; the guardian does not act *as* the child).
- **(e) Compile warnings:** none.

## U03 — AccessVia.Guardian

- **Date:** 2026-09-14. Appended `Guardian` as the **9th** value on `AccessVia` (after `Group`) in `src/Kumunita.Core/Authorization/Decision.cs`. **`dotnet build Kumunita.slnx -c Debug` green.** No new test (U09's tests exercise it indirectly via the audit rows).
- **(a) Enum now has 9 values:** `Owner, Audience, Delegation, Moderator, Report, BreakGlass, Admin, Group, Guardian`.
- **(b) `Guardian` is the 9th / last** (index 8), appended after `Group` (trailing comma added to `Group`).
- **(c) G·1 doc-comment line (verbatim):** "exercised only on the IUserInfoService management lanes — **never** on a CanAsync / CanSeeAsync content decision (G·1)".
- **(d) No renumber:** no existing value's position or name changed — `Owner`..`Group` (indices 0–7) are byte-identical; `Guardian` is purely additive.
- **(e) Compile warnings:** none.

## U04 — formation + suspension + independence seams

- **Date:** 2026-09-14. Declared the four GU account-level seams on `IUserInfoService` (a new `// GU guardian lanes (ADR 0028)` `#region` appended at the end of the interface) and implemented them in `UserInfoService.cs` (a new `// GU guardian lanes` section before the final brace, plus a shared `GuardActiveLinkAsync` standing gate). **`dotnet build Kumunita.slnx -c Debug` green** (4 projects, no warnings). No new test (U09 pins the 11 GU tests).
- **(a) Four seam signatures (verbatim):** `Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId);` · `Task SuspendChildAsync(string childId, string guardianId);` · `Task UnsuspendChildAsync(string childId, string guardianId);` · `Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin);`.
- **(b) Audit verb + `Via` for each:** `guardian.create` / `Via: Guardian` (TargetKind "guardian-link", TargetId = link.Id); `guardian.suspend` / `Via: Guardian` (TargetKind "profile", TargetId = childId); `guardian.unsuspend` / `Via: Guardian` (TargetKind "profile", TargetId = childId); `guardian.dissolve` / `Via: (viaAdmin ? Admin : Guardian)` (TargetKind "guardian-link", TargetId = linkId). All `Outcome: Allow`, in-transaction (C3).
- **(c) Exact throw precondition for each:** `CreateGuardianLinkAsync` — `ArgumentException` on blank childId/guardianId, **no other throw** (duplicate pair = idempotent no-op); `SuspendChildAsync` / `UnsuspendChildAsync` — `UnauthorizedAccessException` when no **active** link for (guardian, child) [the standing gate], then `InvalidOperationException` when the child's `Profile` (by `SubjectId`) is missing; `DissolveGuardianLinkAsync` — `ArgumentException` on blank linkId/actorId, `InvalidOperationException` when the `GuardianLink` row is missing, `UnauthorizedAccessException` when `!viaAdmin` and `actorId != GuardianId`.
- **(d) Dissolve does NOT set `Profile.Blocked`:** the impl writes nothing to membership and never touches `Profile` — the design doc §D "writes nothing to membership; the self-lanes restore on the very next read (C4)". Un-suspend is the **separate** `UnblockAsync` (G·5 valve) / `UnsuspendChildAsync` act, not a dissolve side-effect.
- **(e) Duplicate formation is an idempotent no-op:** `CreateGuardianLinkAsync` returns the existing `Active` row as-is (no mutation, no audit row) rather than throwing — the contract, not an error.
- **(f) `DependencyInjection.cs` NOT touched:** the four impls run on the existing `store`-owned `UserInfoService(IDocumentStore store)` ctor (no new parameter), so the registration is unchanged.
- **(g) GlobalAdmin `BlockAsync`/`UnblockAsync` unchanged** (they live on `IIdentityService`, untouched); **compile warnings:** none.

## U05 — membership curation admits guardian standing

- **Date:** 2026-09-14. Added a `GateGuardianStandingAsync` helper (next to `GateCommunityStanding`) and wired a `Via: Guardian` branch into the standing derivation of the four existing membership lanes. **`dotnet build Kumunita.slnx -c Debug` green** (4 projects, no warnings). No new test (U09's `Membership_AddRemoveChild_ViaGuardian` pins this).
- **(a) Helper + return contract:** `private async Task<Authorization.AccessVia?> GateGuardianStandingAsync(string actorId, string childId)` — a read-only `store.QuerySession()` of the **active** `GuardianLink` where `GuardianId == actorId` && `ChildId == childId` (mirrors U04's `GuardActiveLinkAsync` `Where` clause exactly); returns `Authorization.AccessVia.Guardian` when such a row exists, else `null` (blank actor/child ⇒ `null`, no throw). The branch **satisfies standing — it never throws on its own.**
- **(b) Each lane's `Via` expression (the `??` short-circuit, one per lane):** `AddGroupMemberAsync` → `via = await GateGuardianStandingAsync(addedBy, userId) ?? (addedBy == group.OwnerId ? Owner : Admin)`; `RemoveGroupMemberAsync` → `via = await GateGuardianStandingAsync(removedBy, userId) ?? (removedBy == group.OwnerId ? Owner : Admin)`; `AddCommunityMemberAsync` → `via = await GateGuardianStandingAsync(actorId, userId) ?? GateCommunityStanding(componentId, actorId, actorRoles)`; `RemoveCommunityMemberAsync` → `via = await GateGuardianStandingAsync(actorId, userId) ?? GateCommunityStanding(componentId, actorId, actorRoles)`. For the community lanes the `??` **bypasses** `GateCommunityStanding` (and its `UnauthorizedAccessException`) when the guardian base fires — a guardian holds neither GlobalAdmin nor a moderator scope by definition.
- **(c) Exceptions preserved:** the `RemoveCommunityMemberAsync` mandatory-community `InvalidOperationException` (still fires after the `via` resolution — unchanged order), the ADR 0008 group-owner-row behavior, and the `AddCommunityMemberAsync` idempotent upsert are all **unchanged** — the branch adds a path, never removes one.
- **(d) Interface signatures unchanged:** `IUserInfoService.cs` changed **doc-comment only** on the four lanes (each now names the GU `Via: Guardian` narrower-standing branch); no parameter added, no signature moved. `AccessVia.Guardian` stays **off** the `CanAsync`/`CanSeeAsync` path (G·1 — this unit touches neither).
- **(e) Compile warnings:** none. (First build was red only from a mangled splice in the two community-lane edits; both were rewritten clean and the rebuild is green.)

## U06 — invitation gate + ApproveGroupInvitationAsync

- **Date:** 2026-09-14. Added the GU gate to `AcceptGroupInvitationAsync` (a read-only active-link check at the top of the body, before the existing row load — everything below unchanged) and a new `ApproveGroupInvitationAsync(groupId, childId, guardianId)` seam in `IUserInfoService.cs` + `UserInfoService.cs` (impl reuses the accept write path verbatim, keyed on `childId`). **`dotnet build Kumunita.slnx -c Debug` green** (4 projects, no warnings). No new test (U09's `Invitation_GatedForSupervisedChild` + `Invitation_GuardianApproveLandsMembership_ViaGuardian` pin this).
- **(a) Gate throw (verbatim):** `throw new InvalidOperationException($"Account {actorId} is supervised; a group invitation must be approved by their guardian (see ApproveGroupInvitationAsync).")` — fired when **any** active `GuardianLink` has `ChildId == actorId` (the `?? ` idiom: `Query<GuardianLink>().Where(l => l.ChildId == actorId && l.Status == Active).FirstOrDefaultAsync()` is non-null).
- **(b) Approve's two preconditions:** (1) standing gate via U04's `GuardActiveLinkAsync(session, guardianId, childId)` → **`UnauthorizedAccessException`** on no active link (G·3 deny-by-default; `G3_NonChildTargetIsRefused`); (2) the child's `GroupInvitation` for `(groupId, childId)` must exist **and** be `Pending` → else **`InvalidOperationException`** (distinct from the no-standing gate).
- **(c) `ResolvedBy = guardianId`** (verbatim) — the guardian, not the child; `row.ResolvedAt = now`; membership upsert is the exact `AcceptGroupInvitationAsync` shape (same `GroupMembership` `(groupId, childId)` key, `AddedBy = guardianId`), one `SaveChangesAsync`.
- **(d) Audit verb + `Via`:** `Action = "group.invite.approve"`, `TargetKind = "group"`, `TargetId = groupId`, `ActorId`/`EffectivePrincipalId = guardianId`, `Via = Authorization.AccessVia.Guardian`, `Outcome = Allow`.
- **(e) `DeclineGroupInvitationAsync` unchanged** (impl byte-identical; doc-comment gained one line noting "a supervised child may always say no — no GU gate"). The self-lane's gate is **only** on the accept path.
- **(f) G·1 pin held:** `AccessVia.Guardian` appears **only** on the `group.invite.approve` audit row — **never** on a `CanAsync`/`CanSeeAsync` content decision. **Compile warnings:** none.
