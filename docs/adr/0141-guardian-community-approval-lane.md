# ADR 0141 — GU community-approval lane: guardian approves / rejects a supervised child's community membership + group invitation

Status: Accepted
Date: 2026-10-04
Amends: ADR 0028 (GU — the standing is extended, not changed: the five
supervisory actions gain a **reject** on group invitations and an
**approve / reject** on community membership), ADR 0083 / 0095 (the group-invite
notification's *recipient* moves to the guardian when the invitee is a
supervised child; the child-facing lane is unchanged for unsupervised invitees
and for a guardian inviting their own child).

## Context

ADR 0028 (GU) gave a guardian account-scope supervision over a child they
created: suspend / un-suspend, messaging + community restriction, **approve a
group invitation sent to a child**, and dissolve. Two gaps in that surface
are now the product want:

1. **A guardian can approve a child's group invitation but has no *reject*
   lane.** The child's self-accept (`AcceptGroupInvitationAsync`) is
   GU-gated (the child may *always* decline, but may not *accept* — the
   guardian must approve), yet the only way a child's pending group
   invitation ever resolves to *no membership* is the child declining it
   themselves. The guardian has no path to say no on the child's behalf.

2. **A supervised child's *community* membership lands immediately when an
   admin adds them.** ADR 0012 / the community-add lanes
   (`AddCommunityMemberAsync` / `SetCommunityMembershipAsync`) upsert the
   live `ComponentMembership` row on the admin's write. For an unsupervised
   resident that is correct; for a *supervised child* it bypasses the
   guardian's supervision entirely — the membership is live on the very next
   `GetCommunityIdsAsync` (C4) before the guardian has seen or agreed to it.

Both want the same shape, and both already have a precedent in the codebase:
the **m2b owner-invited group lane** (ADR 0007, the `GroupInvitation`
document) and the **ADR 0094 join-request lane** — a *pending* row the
resolver approves or declines, where **approving is what writes the live
membership** and declining writes none. ADR 0141 carries that
pending-then-resolve shape to (a) the guardian's *reject* of a child's group
invitation, and (b) a supervised child's *community* membership — where the
**guardian is the resolver** (the `AccessVia.Guardian` standing, ADR 0028
G·2/G·3).

The notification lane (ADR 0076 / 0083 / 0095) is the standing delivery
mechanism: the `Notification` inbox row + best-effort email, with the
ADR 0095 `AcceptPath` / `DeclinePath` action links. ADR 0141 re-points the
group-invite notification's *recipient* to the child's guardian(s) when the
invitee is supervised, and adds a new `guardian.community_invite` kind for the
community lane — both deep-linking at the child's **manage-child page**
(`/me/children/{childId}`), which is where the approve / reject buttons live.

## Decision

### New Core types (`Kumunita.Core.UserInfo`)

- `CommunityMembershipRequestStatus` enum: `Pending, Approved, Declined` —
  the state machine (below). **No `Withdrawn`** — the child never sees the
  request (their own surface reads their *effective* membership, which already
  includes the mandatory implicit set), so there is no child self-lane to
  model a withdrawal on; only a guardian resolves it.
- `CommunityMembershipRequest` document (one row per (component, child)):
  `Id` (surrogate PK, `Guid-N`), `ComponentId`, `UserId` (the supervised
  child), `RequestedBy` (the admin / moderator who initiated the add),
  `Status`, `RequestedAt`, `ResolvedAt?`, `ResolvedBy?`. Registered in
  `M1DocTypes.Configure` with `UniqueIndex(ComponentId, UserId)` — the same
  one-row-per-(component, user) business-key convention as
  `ComponentMembership` and `GroupInvitation`.
- **No new group type.** The guardian's *reject* of a child's group
  invitation reuses the existing `GroupInvitation` row (its
  `InvitationStatus.Declined` terminal) — only the *resolver* and the audit
  verb are new (below). This keeps the group lane's single-row-per-(group,
  user) shape and the child's self-accept GU gate exactly as ADR 0028
  recorded them.

### The four new Core seams (exact)

```csharp
Task RejectGroupInvitationAsync(string groupId, string childId, string guardianId);   // guardian decline
Task ApproveCommunityMembershipRequestAsync(string componentId, string childId, string guardianId); // guardian approve
Task DeclineCommunityMembershipRequestAsync(string componentId, string childId, string guardianId);  // guardian decline
// read lane (candidate read, C-M2·2):
Task<IReadOnlyList<CommunityMembershipRequest>> GetPendingCommunityMembershipRequestsForChildAsync(string childId); // Pending, RequestedAt desc
```

- **Standing = the active `GuardianLink` for (guardian, child).** Every
  resolve lane runs `GuardActiveLinkAsync(session, guardianId, childId)`
  first (the ADR 0028 G·2 live / G·3 deny-by-default gate, the
  `ApproveGroupInvitationAsync` precedent): no active row for the exact pair
  ⇒ `UnauthorizedAccessException` (the Web surfaces a 404 — a non-guardian
  learns nothing). The audit `Via` on every resolve row is
  `AccessVia.Guardian` with all three identities the guardian.
- **The supervised-child branch of the add/invite lanes.** When the target
  holds an **active** `GuardianLink`, the two admin-facing community lanes
  (`AddCommunityMemberAsync` / `SetCommunityMembershipAsync`) do **not**
  upsert the `ComponentMembership` row. Instead they upsert a
  `Pending` `CommunityMembershipRequest` row (a resolved or absent row resets
  to `Pending`, the m2b C-M2b·3 re-request shape — the two resolve stamps
  cleared), append a `community.membership.request` audit row, and emit the
  `guardian.community_invite` notification to each active guardian — all in
  one session / one `SaveChangesAsync` (C3). The `GroupInvitation` lane
  (`InviteGroupMemberAsync`) is *unchanged* (the invitation row already
  lands for a supervised child); only its **notification recipient** moves
  (below).
- **The guardian's own curation lane is exempt (ADR 0028 preserved).** If the
  *actor* of a community add is one of the child's **active** guardians, the
  `ComponentMembership` row lands **immediately** with `Via: Guardian` — the
  pre-existing ADR 0028 "the guardian curates their child's membership"
  direct write (the `AddGroupMemberAsync` / `RemoveGroupMemberAsync`
  precedent), with **no** pending request and **no** self-notification (the
  actor is a recipient; a self-notification would be noise). Likewise a
  *guardian inviting their own child* to a group falls through to the
  child-facing `group.invite` nudge (not the guardian fan-out), because the
  child's self-accept GU gate means the guardian is the one who must resolve
  it anyway.
- **Approve makes the community membership live on the very next
  `GetCommunityIdsAsync` call (C4).** `ApproveCommunityMembershipRequestAsync`
  upserts the `ComponentMembership` row (the exact `SetCommunityMembershipAsync`
  shape, `AddedBy = guardianId`) in the same session. **Approve / decline
  touch no group membership on the community lane; the group *reject* lane
  writes no `GroupMembership` row** (the child simply never becomes a member,
  the `DeclineGroupInvitationAsync` shape) — the only difference is the
  child-keyed row, `ResolvedBy = guardianId`, and the audit verb.
- **State machine (one row per (component, child), the `UniqueIndex` pin).**
  `Pending → { Approved, Declined }`; **a re-add after any resolution resets
  the row back to `Pending`** (clearing the two resolve stamps); any other
  transition (double-resolution, resolve after resolution) throws
  `InvalidOperationException`. The group-invitation reject reuses
  `GroupInvitation`'s existing machine (`Pending → Declined`, a re-invite
  resets to `Pending`) — no new machine on that axis.
- **C3 — every write appends its `AccessAudit` row in the same session / one
  `SaveChangesAsync`.** New action family: `community.membership.request`
  (the admin-initiate, `Via` the admin's standing — `Admin` or `Guardian`
  per the existing `GateGuardianStandingAsync` / gate derivation),
  `community.membership.approve` / `community.membership.decline` (guardian,
  `Via: Guardian`), and `group.invite.reject` (guardian, `Via: Guardian`) —
  all `TargetKind` `"component"` (the community family) or `"group"` (the
  reject family), `Outcome Allow`.
- **The notification re-point + the two new kinds.** When a supervised child
  is invited to a group, the ADR 0083 / 0095 emission's *recipient* becomes
  the child's **active guardian(s)** (one `Notification` row per active
  guardian, idempotency key suffixed with the guardian id so two guardians
  coexist) under the new kind `guardian.group_invite`, with `LinkPath` /
  `AcceptPath` / `DeclinePath` all pointing at
  `/me/children/{childId}`. An unsupervised invitee keeps the ADR 0095
  child-facing shape unchanged (kind `group.invite`, the self-lane GET links).
  The community lane emits a new kind `guardian.community_invite`, same
  per-guardian shape. Both kinds are **opt-OUT** (not in `OptInKinds`) — the
  resident-facing posture, matching `group.invite` / `group.added`. The body
  is the group/community name + the child's display name (a curation fact the
  parent already sees in their `/me/children` list — the `ChildAccountItem`
  precedent, **not** a G·1-hiding content read).
- **The Web surface.** `GuardianController` gains three
  `[ValidateAntiForgeryToken]` POSTs: `POST /me/children/{childId}/invitations/{groupId}/reject`,
  `POST /me/children/{childId}/communities/{communityId}/approve`,
  `POST /me/children/{childId}/communities/{communityId}/reject`. Each gates
  on `ActiveLinkAsync` (a non-guardian → 404) and passes the cookie principal
  as the `guardianId`. `Detail` gains the child's pending
  `CommunityMembershipRequest` list (new projection
  `PendingCommunityRequestItem(CommunityId, CommunityName, RequestedAt)`);
  the pending-group-invitation row gains a **Reject** button beside the
  existing **Approve**. `MembershipEditorModel` grows to a 6-field record
  (`PendingCommunityRequests`). The notification's action links land on the
  existing standing-gated `Detail` GET (link-clickable, a non-guardian 404s)
  — the approve / reject themselves remain POSTs, so no new GET resolve
  action is added (the ADR 0095 `AcceptInvitationLink` precedent is
  deliberately *not* mirrored: the guardian's action is a write, and the
  deep-link is the read).

## Consequences

- A new `community.membership.*` action family enters the `AccessAudit`
  vocabulary — `community.membership.request` / `community.membership.approve`
  / `community.membership.decline` (the `community.add-member` /
  `community.remove-member` family gains the pending-then-resolve axis) — and
  `group.invite.reject` joins the existing `group.invite.*` family.
- **A supervised child's community membership is now guardian-gated.** An
  admin adding the child to a community produces a *pending request*, not a
  live membership; the membership lands only when the guardian approves (C4)
  and is never created if the guardian rejects. Unsupervised residents are
  **untouched** — the admin add still lands the membership immediately.
- **A guardian now has a full approve / reject pair** over a child's group
  invitation (previously approve-only), and an approve / reject pair over a
  child's community membership. The child's own *decline* self-lane on groups
  is preserved (ADR 0028: the child may always say no); the child still has
  **no self-accept** (the GU gate is unchanged) and **no self-lane at all** on
  community requests (there is no child-facing surface for them).
- **Two new resident notification kinds** enter the closed
  `NotificationKinds.Known` set (`guardian.group_invite` /
  `guardian.community_invite`) — the settings toggle count moves from 17 to 19
  (`NotificationsControllerTests` pin updated), and four new `kw-l` keys
  (the two `notifications.kind.*`, two `notifications.preference.*`, plus
  `guardian.pending_community_requests` / `guardian.no_community_requests` /
  `guardian.reject`) are registered × 4 languages in `KnownTranslationKeys`
  (the ADR 0015 / ADR 0005 registry parity preserved; `en` is the provider
  floor, so a fresh instance renders the English immediately).
- **The `MembershipEditorModel` pin moves in the same commit** (the AGENTS.md
  drift-guard): `GuardianViewModelsTests` now pins a **six**-field record
  (adding `PendingCommunityRequests`) and gains a
  `PendingCommunityRequestItem_Is_Exact_Three_Field_Projection` pin
  (`{ CommunityId, CommunityName, RequestedAt }`).
- **The pre-existing GU test that asserted the old immediate-add behavior is
  the one that changes semantics** — `GuardianControlsTests
  .Membership_AddRemoveChild_ViaGuardian` (a guardian adding *their own* child
  to a community) still passes because the guardian's own curation lane is
  exempt (immediate, `Via: Guardian`); it is the *admin*-initiated add of a
  supervised child whose observable behavior now changes (pending, not live).
- The new Core seam suite is `GuardianApprovalLaneTests` (15 tests: the
  supervised branch of invite / community-add, the guardian fan-out, the
  two-guardian case, approve / decline / reject write paths, the
  non-guardian deny-by-default walls, the child self-accept GU gate, the
  unsupervised-unchanged lanes, and the re-request reset).
- **`Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are
  untouched** — this is a named GU extension on the already-shipped
  guardian-controls surface (the ADR 0028 / 0038 precedent), not a new
  M-letter milestone. M4 / M5 / M6 stay Events / Projects / Portability.
