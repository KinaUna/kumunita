# ADR 0094 — Resident join-request lane for public groups (self-initiated, the reverse of the m2b invitation lane)

Status: Accepted
Date: 2026-09-27
Amends: nothing — adds a new group write lane on the standing ADR 0007 / ADR 0008 prescribe. **Supersedes the m2b design doc's "No join-request flow" deferral** (docs/design/m2b-group-invitations.md, Context), which deferred a resident self-join as scope-out-of-lane; this ADR lands that deferred lane.

## Context

The m2b invitation lane (ADR 0007's standing, the `GroupInvitation` document, the
`group.invite.*` audit family) covers the *owner-initiated* direction of group
membership: the **owner invites**, the **invitee resolves** (accept / decline),
and the owner can **cancel**. Its design doc recorded the resident self-join
explicitly as **out-of-lane** ("No join-request flow — that would open the
reader to owner∪member-less state and break the owner∪member privacy model").

That deferral is now the product want: **a signed-in resident who is not yet a
member of a *public* group should be able to ask to join it**, and the group's
owner (or a GlobalAdmin) should be able to review and approve or decline those
asks on the group detail. This is the reverse direction of the m2b lane — the
*resident* starts it, the *owner* resolves it — and it only ever opens a
**public** group (ADR 0010), so it does not touch the private-group model the
m2b doc was protecting: a private group's membership still arrives only by
owner invitation, and no public list ever exposes the owner's private groups.

The standing for the resolve lane is already settled by ADR 0007's
consequences clause ("Adding a new write lane to groups in the future sits on
`TryResolveOwnerSurface` by default"): it is **owner ∪ GlobalAdmin**, not
owner-only — identical to add/remove/invite/privacy/description/delete. The
request + withdraw self-lane sits on the same standing as the m2b
accept/decline self-lane and ADR 0008's `LeaveGroup` self-lane: only the actor
may act on their **own** row, with the actor minted from the signed-in
principal, never a form field (SoD by structural identity).

## Decision

A new resident self-initiated lane, four Web routes on `GroupsController` over
four new Core seams on `IUserInfoService` (the ADR 0006-E lane — new named
methods, no signature change to any pinned M1/M2/m2b method):

**New Core types (`Kumunita.Core.UserInfo`):**

- `JoinRequestStatus` enum: `Pending, Approved, Declined, Withdrawn` — the
  state machine (below). `Withdrawn` is the self-lane terminal, the direct
  analogue of the m2b owner's `Cancelled`.
- `GroupJoinRequest` document (one row per (group, user)): `Id` (surrogate
  PK, `Guid-N`), `GroupId`, `UserId` (the requesting resident), `Status`,
  `RequestedAt`, `ResolvedAt?`, `ResolvedBy?`. Registered in
  `M1DocTypes.Configure` with `UniqueIndex(GroupId, UserId)` — the same
  one-row-per-(group, user) business-key convention as `GroupMembership` and
  `GroupInvitation`.

**The four seams (exact):**

```csharp
Task<GroupJoinRequest> RequestToJoinGroupAsync(string groupId, string actorId);                 // self-lane (requester)
Task ApproveJoinRequestAsync(string groupId, string userId, string resolvedBy);                 // owner ∪ GlobalAdmin
Task DeclineJoinRequestAsync(string groupId, string userId, string resolvedBy);                 // owner ∪ GlobalAdmin
Task WithdrawJoinRequestAsync(string groupId, string actorId);                                  // self-lane (requester)
// read lanes (candidate reads, C-M2·2):
Task<IReadOnlyList<GroupJoinRequest>> GetPendingJoinRequestsForUserAsync(string userId);        // Pending, RequestedAt desc
Task<IReadOnlyList<GroupJoinRequest>> GetPendingJoinRequestsForGroupAsync(string groupId);       // Pending, RequestedAt asc
```

- **Standing = owner ∪ GlobalAdmin on the resolve lane.** `ApproveJoinRequest`
  / `DeclineJoinRequest` resolve their surface through
  `TryResolveOwnerSurface` (the ADR 0007 new-lane rule — identical to
  add/remove/invite/privacy/description/delete): a plain member's POST 404s,
  the consistent write-lane failure shape. `ApproveJoinRequest` additionally
  enforces the **GU supervised-child wall** (ADR 0028 §C / G·2): a resident
  with an active `GuardianLink` is refused on the approve lane — their
  membership-landing lane is guardian-mediated, exactly as
  `AcceptGroupInvitationAsync`. The audit `Via` is derived in the seam
  (the single SoD source, ADR 0006-D): `resolvedBy == Group.OwnerId ⇒
  Owner`, else `Admin`; the self-lane (request/withdraw) uses
  `Via = Owner` with all three identities the requester.
- **State machine (one row per (group, user), the `UniqueIndex` pin).**
  `Pending → { Approved, Declined, Withdrawn }`; **re-request resets a
  resolved row back to `Pending`** (clearing the two resolve stamps); any
  other transition (double-resolution, resolve-after-resolution, or a
  self-lane act on a resolved row) throws `InvalidOperationException`. The
  Web maps that to `TempData["error"]` + a redirect — never a 500.
- **C3 — every write appends its `AccessAudit` row in the same session /
  one `SaveChangesAsync`:** `group.join.request` (self-lane, Via Owner),
  `group.join.approve` / `group.join.decline` (owner ⇒ Owner else Admin),
  `group.join.withdraw` (self-lane, Via Owner), all `TargetKind "group"`,
  `TargetId` the group, `Outcome Allow`.
- **C4 — approve makes the membership live on the very next
  `GetGroupIdsAsync` / `GetGroupsForUserAsync` call.** Approve upserts the
  `GroupMembership` row (the exact `AddGroupMemberAsync` shape, `AddedBy =
  resolvedBy`) in the same session. A **request** and a **decline** touch
  **no** membership row. The two read lanes return live rows (C4) and filter
  `Status == Pending` only.
- **C-M2·2 — the two read lanes append no `AccessAudit` row** (candidate
  projections, not access decisions).
- **`DeleteGroupAsync` cascade (ADR 0093).** Deleting a group now also removes
  its `GroupJoinRequest` rows — beside the `GroupMembership` and
  `GroupInvitation` cascade — so a resident is never left holding a request
  to a group that no longer exists.
- **The Web surface.** Two new projection records in
  `src/Kumunita.Web/Models/GroupViewModel.cs`: `JoinRequestViewModel(GroupId,
  GroupName)` (the `/groups` list's "Your join requests" card row) and
  `PendingJoinRequestViewModel(SubjectId, DisplayName)` (the
  `/groups/{id}` review-surface pending row, the `PendingInvitationViewModel`
  pin carried to the join-request axis). `GroupListViewModel` gains
  `PublicGroups` (the non-private groups the actor is *not* a member of — the
  resident-facing directory, each with a per-row member count) and
  `MyJoinRequests` (the actor's own pending requests). `GroupDetailViewModel`
  gains `PendingJoinRequests`. Four new routes (all `[ValidateAntiForgeryToken]`):
  `POST /groups/{id}/join/request`, `POST /groups/{id}/join/withdraw`,
  `POST /groups/{id}/join-requests/{subjectId}/approve`,
  `POST /groups/{id}/join-requests/{subjectId}/decline`. `RequestToJoin`
  asserts the public + not-already-a-member gate in the Web (404 on fail —
  "what is offered is what the route accepts"); `WithdrawJoinRequest` gates on
  the actor's own pending list (404 on fail). The "Other public groups"
  directory renders only public, non-member groups; the "Pending join
  requests" review surface renders only when `canManage` (owner ∪ GlobalAdmin)
  and there are pending rows. Nine new `kw-l` keys × 4 languages
  (the ADR 0015 curated registry, `KnownTranslationKeys` parity preserved).

## Consequences

- A new `group.join.*` action family enters the `AccessAudit` vocabulary —
  `group.join.request` / `group.join.approve` / `group.join.decline` /
  `group.join.withdraw`, one row per write, the `group.*` lane family's shape
  (add-member / remove-member / invite / invite.accept / … / delete).
- A **public group is now discoverable + joinable by any resident.** A signed-in
  resident sees the non-private groups they are not in, can request to join,
  see their own pending requests, and withdraw one. The owner ∪ GlobalAdmin
  reviews and approves (membership lands, C4) or declines. **Private groups are
  untouched** — they never appear in the directory, and a request to one 404s,
  so the ADR 0010 private-group model is preserved.
- **No notification emission.** This lane deliberately does not add a
  notification kind (ADR 0076 / 0083 / 0084): the requester's "Your join
  requests" card (with the "pending" state + withdraw) and the owner's
  "Pending join requests" review surface are the standing record; the
  `group.join.request` audit row is the durable fact. A "you have N join
  requests" inbox lane (ADR 0083 `GroupAdded` emission shape) is a future
  lane, out of scope here.
- **`Withdrawn` is a terminal state** (the m2b `Cancelled` analogue): it drops
  the row off both the requester's "Your join requests" card and the owner's
  review list, and it is re-requestable (a re-request resets it to
  `Pending`). It is **not** a fourth *resolution* outcome on the owner lane —
  the owner's two outcomes remain approve / decline.
- **The shape pins move in the same commit** (the AGENTS.md drift-guard):
  `GroupDetailViewModel` grows to 22 projected fields (`GroupsDetailViewModelTests`
  now pins `PendingJoinRequests`), and `GroupsViewModelTests` pins the two new
  records (`JoinRequestViewModel` = `{ GroupId, GroupName }`,
  `PendingJoinRequestViewModel` = `{ SubjectId, DisplayName }`).
- The new Core seam suite is `UserInfoServiceGroupJoinRequestTests`
  (mirroring `UserInfoServiceGroupInvitationsM2bTests`): the full lifecycle
  (request → approve / decline / withdraw), the SoD `Via` derivation, the
  self-lane foreign-actor wall, the state machine, the GU supervised-child
  wall on approve, C3 audit counts, the C-M2·2 no-audit read lanes, and the
  `DeleteGroupAsync` cascade.
- `Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are **untouched**
  — this is a named lane on the already-shipped M2 group surface (the
  ADR 0013 / 0089 group-lane precedent), not a new M-letter milestone.
