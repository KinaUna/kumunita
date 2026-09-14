# GU U06 — Core: invitation gate + `ApproveGroupInvitationAsync`

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

Two changes (ADR 0028 §C, invariant G·2): **(1)** a **gate** on
`AcceptGroupInvitationAsync` — a **supervised** child (one with an active
`GuardianLink`) may not self-accept; the approval must come from their
guardian. **(2)** a new seam `ApproveGroupInvitationAsync(groupId, childId,
guardianId)` — the guardian approves the child's pending invitation, reusing
the accept write path but recording `ResolvedBy = guardianId` + audit
`group.invite.approve` `Via: Guardian`. `DeclineGroupInvitationAsync` **stays
open** (a child may always decline). **No Web surface, no tests.**

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`## Pinned contract`** → the
   invitation row (U01 pinned the new seam's signature + the gate + the
   `ResolvedBy` / audit details) — the *primary* source.
2. `docs/adr/0028-...md` §C (invitation approval is one of the five actions;
   the approve reuses the accept write, records the **guardian** as
   `ResolvedBy` + `Via: Guardian`; the child can still **decline**) + §D G·2
   (supervision is live standing — the gate reads the active link each time).
3. `src/Kumunita.Core/UserInfo/UserInfoService.cs` § `AcceptGroupInvitationAsync`
   (~line 643) — the **write path to reuse**. Read it fully. Key shape: loads
   the `GroupInvitation` row for `(groupId, actorId)`, verifies it is the
   actor's own **and** `Pending`, moves it `Pending → Accepted` with
   `ResolvedAt`/`ResolvedBy = actorId`, **upserts the `GroupMembership` row**
   (the "exact `AddGroupMemberAsync` upsert shape, C4"), stores a `group.invite
   .accept` `Via: Owner` audit row, one `SaveChangesAsync`. U06's approve is
   this **same** body except: the row is the **child's** (`row.UserId ==
   childId`), `ResolvedBy = guardianId`, and the audit is
   `group.invite.approve` `Via: Guardian`.
4. `src/Kumunita.Core/UserInfo/GuardianLink.cs` (U02) — the gate + the
   approve's precondition query: an **active** link with `ChildId == childId`
   **and** `GuardianId == guardianId`.
5. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` § `AcceptGroupInvitationAsync`
   (the existing seam, ~line 677) + `DeclineGroupInvitationAsync` (~line 687)
   — where U06 appends the new seam (next to the accept) + adds the gate
   doc-comment.

## The gate (pin this)

`AcceptGroupInvitationAsync(groupId, actorId)` — `actorId` is the **self**
(the child, who is accepting their own invite). **At the top of the method
body**, before the existing row load, add:

> If **any** active `GuardianLink` has `ChildId == actorId`, this lane is
> closed for that child (G·2): throw `InvalidOperationException` with a message
> that names the gate (e.g. "Account {actorId} is supervised; a group
> invitation must be approved by their guardian (see ApproveGroupInvitationAsync).").

The gate is a **read-only** query (the `GetPendingInvitationsForUserAsync`
read-session shape) — it must not mutate. `DeclineGroupInvitationAsync` gets
**no** gate (a supervised child still declines their own invite).

## Deliverables (≤2 files, modify)

### 1. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (modify)

- **Add** the new seam, placed immediately after `AcceptGroupInvitationAsync`:

```
    /// <summary>
    /// GU (ADR 0028): a **guardian** approves a **supervised child's** pending
    /// group invitation — the accept write path (the membership lands) with the
    /// **guardian** recorded as <c>ResolvedBy</c> and the audit row
    /// <c>group.invite.approve</c> <c>Via: Guardian</c> (G·2). Precondition: an
    /// **active** <see cref="GuardianLink"/> for (guardian, child) and a
    /// <b>Pending</b> invitation on the child; a child with no active link is
    /// refused (they use the self-lane
    /// <see cref="AcceptGroupInvitationAsync"/> instead).
    /// </summary>
    Task<GroupInvitation> ApproveGroupInvitationAsync(string groupId, string childId, string guardianId);
```

- **Doc-comment** on `AcceptGroupInvitationAsync`: add one sentence noting the
  GU gate (a supervised child — one with an active `GuardianLink` — is refused
  here; their guardian calls `ApproveGroupInvitationAsync`).

### 2. `src/Kumunita.Core/UserInfo/UserInfoService.cs` (modify)

- **`AcceptGroupInvitationAsync`**: insert the gate (above) at the top of the
  body. Everything below it (the row load, the Pending check, the accept
  mutation, the membership upsert, the `group.invite.accept` audit) is
  **unchanged**.
- **`ApproveGroupInvitationAsync`**: `/// <inheritdoc />` method. Body:
  1. **Standing gate (no link → 404)**: query for an **active**
     `GuardianLink` with `ChildId == childId` **and** `GuardianId == guardianId`
     — if none, throw **`UnauthorizedAccessException`** (a non-child /
     non-guardian pair is refused — G·3 deny-by-default; the Web surfaces this
     as a 404; this is the `G3_NonChildTargetIsRefused` precondition).
  2. **Precondition (pending on the child)**: load the `GroupInvitation` for
     `(groupId, childId)` — if none, or not `Pending`, throw
     `InvalidOperationException` (the existing accept's row-load + Pending-check
     shape, but keyed on `childId` — a *missing / bad-state row*, distinct from
     the no-standing gate in step 1).
  3. **Mutation** (the accept write path, **reused verbatim**): move the row
     `Pending → Accepted`, `ResolvedAt = now`, `ResolvedBy = guardianId`
     (the guardian, not the child), upsert the `GroupMembership` row for
     `(groupId, childId)` (the "exact `AddGroupMemberAsync` upsert shape").
  4. **Audit** (in the same session): `group.invite.approve`, `TargetKind =
     "group"`, `TargetId = groupId`, `ActorId = guardianId`,
     `EffectivePrincipalId = guardianId`, `Via: Guardian`, `Outcome: Allow`.
  5. **One** `SaveChangesAsync`. Return the `GroupInvitation`.

## Exit

`dotnet build Kumunita.slnx -c Debug` green. `AcceptGroupInvitationAsync` now
gates on the active link; `ApproveGroupInvitationAsync` is declared +
implemented (guardian precondition → pending-on-child → accept write →
`group.invite.approve` `Via: Guardian`); `DeclineGroupInvitationAsync` is
**unchanged**. **No new test** (U09's `Invitation_GatedForSupervisedChild` +
`Invitation_GuardianApproveLandsMembership_ViaGuardian` pin this — U06 is the
*impl* they assert). Handoff note (append): 6–8 lines starting
`## U06 — invitation gate + ApproveGroupInvitationAsync` — (a) the exact gate
throw (one line), (b) the approve's two preconditions (guardian-link +
pending-on-child), (c) `ResolvedBy = guardianId` (verbatim), (d) the audit
verb + `Via`, (e) a confirmation `DeclineGroupInvitationAsync` is **unchanged**,
(f) any compile warnings.
