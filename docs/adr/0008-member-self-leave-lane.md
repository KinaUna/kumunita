# ADR 0008 — Member self-leave lane (owner excepted)

Status: Accepted
Date: 2026-09-10
Amends: C-M2·3 (docs/design/m2-directory-profiles-groups.md) — adds the self-leave lane as the standing's lone deviation

## Context

M2's group-management lanes (U10's remove, plus ADR 0007's add and the m2b
invite/cancel lanes) all sit on the owner ∪ GlobalAdmin standing. As a
result, a *plain member* of a group they didn't create can never leave it:
their `POST /groups/{id}/remove-member` 404s (the route
`TryResolveOwnerSurface` gate), and no other self-removal route exists.

The product want: **a member of a group is able to remove themselves from
it, except if they are the owner.** A group membership someone else granted
(or that the owner added) should not be a one-way ticket; but the owner is
different — the owner row is the group's anchor (M1's `CreateGroupAsync`
commits the owner's own `GroupMembership` in one session), and owner
departure is a *transfer* question this platform hasn't settled (there is no
owner-transfer lane), so an owner who self-removes is rejected.

ADR 0007's consequences section anticipates exactly this shape of change:
"Adding a new write lane to groups in the future sits on
`TryResolveOwnerSurface` by default; broadening any lane to 'any member'
again is a breaking change that belongs in an ADR." This ADR is that ADR —
and it is narrower than a broadening: the lane is
**any-member ∩ own-row ∩ ¬owner**, never any-member ∩ other-row.

## Decision

A new self-lane, `POST /groups/{id}/leave`
(`GroupsController.LeaveGroup`), shaped exactly like the m2b invitation
self-lane (`AcceptInvitation` / `DeclineInvitation` — the "only the actor
themselves, actor minted from the signed-in principal, no form field"
pattern):

- **Target = actor, structurally.** The route carries no `subjectId` at
  all — no form field, no route parameter. The
  `userId` and `removedBy` the Core seam receives are both the actor's
  subject, minted from `KumunitaPrincipal.SubjectId(User)`. There is no
  channel through which a member could remove *someone else* through this
  route — the self-lane is self by construction (ADR 0003 the same way:
  SoD by structural identity, not a re-gate).
- **Gate = the U10 reachability projection, not `TryResolveOwnerSurface`.**
  `LeaveGroup` resolves its surface through
  `TryResolveWriteSurface` (owner ∪ member) — the member must already be in
  the group, else 404 (the consistent write-lane failure shape; no 200 +
  error text).
- **Owner excepted.** On top of the projection, `actor == Group.OwnerId`
  → 404. The owner cannot leave their own group, and the detail view hides
  the Leave button on the owner's own row — the form and the route agree
  (the m2b pattern: what the user sees is exactly what the route would
  accept).
- **Same seam, same audit lane, same consistency.** The write is the frozen
  `RemoveGroupMemberAsync(groupId, userId, removedBy)` — `userId ==
  removedBy == actor`, C4 strong consistency (the actor is out of the
  owner ∪ member projection on the very next read — the detail page they
  just left now 404s for them, which is why the success redirect goes to
  `Index`, not `Detail`; the `DeclineInvitation` redirect shape). The
  `AccessVia` derivation is the M1 rule, unchanged: `actor == OwnerId ⇒
  Owner, else Admin`. A self-leaving member's audit row therefore reads
  `Action: group.remove-member, ActorId: <the member>, Via: Admin` —
  deliberate; the row's *actor* field is the self-leave fact, and we do
  not invent a third `Via` state or a new action verb for one lane (the
  seam's SoD derivation stays the single source, ADR 0006-D: Web shapes
  HTTP, Core decides).
- **Presentation.** `Groups/Detail.cshtml` member list per row:
  - the **owner's own row** carries no button (the Remove form is hidden
    there — the owner cannot self-remove through the existing lane either,
    which is what the C-M2·3 exception demands; the owner's Remove buttons
    on *other* members' rows are untouched);
  - a **non-owner member's own row** carries the **Leave** form (their only
    self-removal route — plain members see no Remove button on anyone's
    row, including their own, since that lane stayed owner ∪ GlobalAdmin);
  - an owner/GlobalAdmin **on someone else's row** still carries the
    Remove form, unchanged.
- **No Core change.** No new seam, no new document, no schema; the Core
  already supports "remove this (group, user) membership" and does not
  care whose standing performed it (its SoD lane is the `Via` derivation,
  not an actor-standing gate — ADR 0006-D).

## Consequences

- A non-owner member can exit any group they belong to; their membership
  (and with it: the group audience on their profile visibility grants,
  directory access through that group, and any scoped delegations granted
  to that group) is gone on the very next read (C4, unchanged).
- The owner ∪ GlobalAdmin remove lane keeps its standing against *other*
  members, including the GlobalAdmin's ability to remove the owner. The
  *owner's self-removal* through that lane is closed both ways: the detail
  view hides the button on the owner's own row, and the `RemoveMember`
  route itself 404s when the actor is the owner and the target is the
  owner's subject (a crafted `POST` with `subjectId = OwnerId` is rejected,
  consistent failure shape) — the product rule is enforced by the route,
  the view just never offers it.
- A member re-added after leaving is a fresh membership row (the M2 add /
  m2b invite lanes are untouched; re-adding someone who previously left is
  exactly the existing "adding someone already in is a no-op the form
  should not offer" path — they are no longer "in").
- ADR 0007's "new lanes default to `TryResolveOwnerSurface`" remains the
  default for *management* reach over others' rows; this is the recorded
  exception for the actor's own row, and it is the only one. Broadening
  *any* lane to remove *another* member is still a breaking change that
  belongs in a further ADR.
