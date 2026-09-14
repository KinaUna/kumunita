# GU U01 — Finalize the pinned contract in the design doc

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Append a `## Pinned contract (U01 — finalizes for U02–U11)` section to the
**existing** `docs/design/guardian-controls-design.md`, turning its prose
(`## Seams & contracts (mandatory)`) into **machine-pinnable exact C#**: the
`GuardianLink` POCO, the `AccessVia.Guardian` value, the exact five
`IUserInfoService` seams + the two membership-lane branches + the invitation
gate, the **pinned seam-test names**, the **acceptance gate**, and the
**drift-guard**. The design doc and **ADR 0028 already exist** — U01 adds the
pinned contract; it does **not** author the ADR (that shipped in the design
commit). **No code, no build.**

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` — the **primary** tier. Read the
   **`## Goals / Non-goals`** (the five supervisory actions + formation +
   come-of-age, and the non-decisions), the **`## Seams & contracts
   (mandatory)`** (the prose U01 pins — the `AccessVia.Guardian` value, the
   `GuardianLink` doc, the seams "each with an owner", the audit verbs, and the
   invariants **G·1–G·5**), and the **`## Feedback loops`** (the test shapes
   the pinned names must satisfy). This is the source of truth for *what*; U01
   supplies the *exact C#*.
2. `docs/adr/0028-guardian-controls-account-scope-supervision.md` — §A (the
   standing), §B (the relationship), §C (the **five supervisory actions** —
   the exact names U01 pins), §D (G·1–G·5), §E (the **audit verbs**:
   `guardian.create`, `guardian.suspend`, `guardian.unsuspend`,
   `guardian.dissolve`, `group.invite.approve`, + the four existing membership
   verbs carrying `Via: Guardian`). The ADR is the *authority* U01 pins to.
3. `src/Kumunita.Core/Authorization/Decision.cs` — the `AccessVia` enum. Note
   the **append precedent**: `Owner, Audience, Delegation, Moderator, Report,
   BreakGlass, Admin, Group` (8 values today); `Admin` and `Group` each carry
   a doc-comment explaining why they are an additive append (the "least-
   distortion slot", ADR 0006-E). `Guardian` will be the **9th**, appended
   after `Group`. Also note the `Decision` / `VisibleSet` / `AccessOutcome`
   shapes (untouched by this lane).
4. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — the **frozen surface**.
   The exact signatures U01 pins: `AddCommunityMemberAsync(string componentId,
   string userId, string actorId, IReadOnlySet<string> actorRoles)` (~line 474),
   `RemoveCommunityMemberAsync(…)` (~505), `AddGroupMemberAsync(string groupId,
   string userId, string addedBy)` (~51), `RemoveGroupMemberAsync(…)` (~55),
   `AcceptGroupInvitationAsync(string groupId, string actorId)` (~677),
   `DeclineGroupInvitationAsync(…)` (~687), `InviteGroupMemberAsync(…)` (~659).
   Also the doc-comment/exception conventions (`UnauthorizedAccessException`
   on no standing, `InvalidOperationException` on a missing row) the new seams'
   doc-comments must match.
5. `src/Kumunita.Core/UserInfo/GroupInvitation.cs` — the `InvitationStatus`
   enum (`Pending/Accepted/Declined/Cancelled`), the `ResolvedAt`/`ResolvedBy`
   stamps, and the **audit vocabulary** `group.invite` / `group.invite.accept`
   / `group.invite.decline` / `group.invite.cancel` (targetKind "group") that
   U01 reuses for `group.invite.approve`.
6. `src/Kumunita.Core/UserInfo/Group.cs` §`DelegationGrant` (~line 79) — the
   **shape precedent** for `GuardianLink`: `Id`, `OwnerId`, `DelegateId`,
   `Scope`, `From`, `To?`, `RevokedBy?` + the `IsActiveAt` helper's comment
   ("the service (not the document) is the resolver — this helper is the
   single truth for 'active'"). U01's `GuardianLink` mirrors this (the "service
   is the resolver" pin, not a doc-side boolean).
7. `src/Kumunita.Core/M1DocTypes.cs` §`Configure` (~line 48) — where
   `GuardianLink` will register (a neighbor of `opts.Schema.For<
   DelegationGrant>();` at ~line 61) and the **business-key convention** to
   mirror: `opts.Schema.For<GroupInvitation>().UniqueIndex(i => i.GroupId,
   i => i.UserId);` (surrogate `Id` is the Marten identity; the pair is the
   business key).

## Deliverables (1 file, modify)

### `docs/design/guardian-controls-design.md` — append `## Pinned contract (U01 — finalizes for U02–U11)`

Place it **after** the existing `## Seams & contracts (mandatory)` section (so
the prose and the pin read together) and **before** `## Feedback loops`. In
order:

- `### GuardianLink POCO (exact C#)` — pin `src/Kumunita.Core/UserInfo/
  GuardianLink.cs` (new, U02 authors): `public sealed class GuardianLink` with
  `public string Id { get; set; } = string.Empty;` (surrogate PK),
  `public string GuardianId { get; set; } = string.Empty;`, `public string
  ChildId { get; set; } = string.Empty;`, `public GuardianLinkStatus Status
  { get; set; }`, `public DateTimeOffset CreatedAt { get; set; }`, `public
  DateTimeOffset? DissolvedAt { get; set; }`, `public string? DissolvedBy
  { get; set; }`; plus `public enum GuardianLinkStatus { Active, Dissolved }`.
  A doc-comment anchors G·2 (standing is from the active link, resolved by the
  **service**) and G·4 (the standing basis is creation — `GuardianId` is the
  creator). **No** doc-side `IsActive` boolean (mirror the `DelegationGrant.
  IsActiveAt` "service is the resolver" rule).
- `### AccessVia.Guardian (exact C#)` — pin the **9th** value appended after
  `Group` in `src/Kumunita.Core/Authorization/Decision.cs` (U03 authors), with
  the doc-comment: "The GU standing (ADR 0028): action-scoped to the five
  supervisory actions (G·3); **never** on a `CanAsync` / `CanSeeAsync` content
  decision (G·1). The M1 `Admin` / ADR 0013 `Group` append precedent."
- `### IUserInfoService guardian seams (exact C#)` — pin the **five** (the
  three are new methods; the two are branches) exactly as ADR 0028 §C names
  them, each with its doc-comment (standing gate = active link; the audit verb
  + `Via`; the invariant; the exceptions):
  - `Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId);` — **formation** (U04). Upserts the `(GuardianId, ChildId)` `Active` row (idempotent no-op on re-attach); audit `guardian.create`, `Via: Guardian`, target the child account; G·4.
  - `Task SuspendChildAsync(string childId, string guardianId);` + `Task UnsuspendChildAsync(string childId, string guardianId);` — **suspension** (U04). Verify the active link (else `UnauthorizedAccessException`); set `Profile.Blocked` true/false (the **same flag** `BlockedAccountMiddleware` + the directory already read — enforcement parity); audit `guardian.suspend` / `guardian.unsuspend`, `Via: Guardian`; G·2 (live on the next read).
  - **Membership curation** — the **existing** `AddCommunityMemberAsync(componentId, userId, actorId, actorRoles)` / `RemoveCommunityMemberAsync(…)` (ADR 0012) **and** `AddGroupMemberAsync(groupId, userId, addedBy)` / `RemoveGroupMemberAsync(…)` **grow a `Via: Guardian` branch** (U05): when the actor has an **active link over the target `userId`**, the standing gate is satisfied and the audit row records the **narrower** standing `Via: Guardian` (the ADR 0012 "record the narrower standing" rule). **No new method; a branch.** The four **signatures are unchanged.**
  - `Task<GroupInvitation> ApproveGroupInvitationAsync(string groupId, string childId, string guardianId);` — **invitation approval** (U06). Resolves the child's `Pending` row as `Accepted` (the `AcceptGroupInvitationAsync` membership write + `ResolvedAt`/`ResolvedBy = guardianId`); audit `group.invite.approve`, `Via: Guardian`, targetKind "group".
  - `Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin);` — **independence** (U04). Moves the row `Active → Dissolved` (`DissolvedAt`/`DissolvedBy = actorId`); audit `guardian.dissolve`, `Via: Admin` when `viaAdmin` else `Via: Guardian` (G·5 safety valve). The child's memberships are **preserved**; self-lanes restore on the next read (G·2/C4).
  - **The invitation gate** — `AcceptGroupInvitationAsync(groupId, actorId)` (U06) **gates**: if the invitee has an active `GuardianLink`, the self-accept is refused (`InvalidOperationException` → the Web's error, never a 500); `DeclineGroupInvitationAsync` **stays open** (a child may always say no).
- `### Pinned seam tests (exact names)` — pin the file `tests/Kumunita.
  Core.Tests/GuardianControlsTests.cs` (U09 authors) with exactly these **11**
  (the load-bearing one first):
  1. `G1_GuardianCannotReadChildContent` — **the lane's honesty**: a guardian, given the child's id, is **denied** a post the child authored for a non-guardian audience; the `CanAsync` decision carries no `Via: Guardian` branch.
  2. `G2_SuspendIsLiveAndBlocksStanding` — suspend → the account is standing-less + directory-excluded (`Profile.Blocked`); un-suspend → standing restored on the next read.
  3. `G2_DissolveRestoresSelfLanesOnNextRead` — dissolve → the child's self-accept lane is live on the very next attempt (C4).
  4. `G3_NonChildTargetIsRefused` — a guardian acting on a *non-child* target is refused (`UnauthorizedAccessException`).
  5. `G3_ContentReadIsNeverGuardian` — a guardian attempting a content read is refused; no `Via: Guardian` on the path (the G·1 unit-level twin).
  6. `G4_FormationCommitsAccountLinkAndAuditTogether` — account + link + audit land in one commit; a duplicate `(guardian, child)` is an idempotent no-op.
  7. `G5_GlobalAdminDissolvesAndUnSuspends` — the safety valve: GlobalAdmin dissolves an active link (`Via: Admin`) and un-suspends; the child's self-lanes restore.
  8. `Invitation_GatedForSupervisedChild` — a child with an active link **cannot** self-accept a group invitation (refused) but **can** self-decline.
  9. `Invitation_GuardianApproveLandsMembership_ViaGuardian` — `ApproveGroupInvitationAsync` lands the membership, audit `group.invite.approve`, `Via: Guardian`.
  10. `Membership_AddRemoveChild_ViaGuardian` — the guardian adds/removes the child from a component **and** a group; the audit rows record `Via: Guardian`; the ADR 0012 posting/feed gate reflects it on the next read.
  11. `SuspendSetsProfileBlocked_EnforcementIdentical` — the flag the existing `BlockedAccountMiddleware` / directory already read is the one set (enforcement parity).
- `### Acceptance gate (U10 records)` — the three tests: **closed loop** (a parent forms a child account, suspends it, curates a membership, approves an invitation, then dissolves — each lands its audit row + effect on the next read); **handoff** (dissolve hands the account to the child — the child's self-lanes restore live, memberships preserved — the "come of age" handoff); **part-vs-whole** (the 11-test list is the whole; closed-loop + handoff are the parts; all must pass together).
- `### Drift-guard (frozen once written)` — the `GuardianLink` POCO, the `AccessVia.Guardian` value + its position (9th), the five seam signatures + the two membership-lane branches + the invitation gate, the 11 pinned test names, the G·1–G·5 invariants, and the acceptance gate — all frozen pins; any mismatch is a `## U<m> — Drift pause` (register unit-series rule §2/§8).

## Exit

The design doc has the `## Pinned contract` section with all sub-sections +
the **11 pinned test names**. **No code, no build.** Handoff note (append to
`docs/plans-milestones/in-progress/guardian-controls-handoff-notes.md`): 6–8
lines starting `## U01 — pinned contract` — (a) the five seam names (verbatim),
(b) the `AccessVia.Guardian` position (9th), (c) the **11 pinned test names**,
(d) the G·1 pin ("no `Via: Guardian` on the content path"), (e) any drift
pause.
