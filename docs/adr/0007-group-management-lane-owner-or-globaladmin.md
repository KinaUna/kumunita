# ADR 0007 — Group management lane: owner ∪ GlobalAdmin for add and remove

Status: Accepted
Date: 2026-09-10
Amends: C-M2·3 (docs/design/m2-directory-profiles-groups.md) — the *add* lane's standing

## Context

M2 (plan U10) intentionally gave the immediate **add-member** lane a broader
standing than the rest of group management: any member of the group could add
other residents (`AddMember` gated on the owner ∪ member reachability
projection), while **remove** and the m2b **invite/cancel** lane sit on the
stricter owner ∪ GlobalAdmin standing (C-M2·3).

In practice this means a group a user was added to can be used on their own
by a non-owner member to pull in anyone else — members see the "Add a
member" form, and their POSTs succeed. The product want: adding people to a
group is something the *owner* does; a non-owner should neither see the
section nor be able to submit it.

The Core seam is unchanged either way: `AddGroupMemberAsync(groupId, userId,
addedBy)` derives the audit `Via` from `addedBy == Group.OwnerId`, and its
SoD lane does not re-gate by actor standing (ADR 0006-D: Web shapes HTTP,
Core decides). The standing decision is a Web-controller concern, exactly as
for the existing remove/invite lanes.

## Decision

The immediate add-member lane adopts the C-M2·3 standing — **owner ∪
GlobalAdmin** — identical to the remove lane and the m2b invite/cancel
lane:

- `GroupsController.AddMember` resolves its surface through
  `TryResolveOwnerSurface` (owner ∪ GlobalAdmin on top of the U10
  owner ∪ member reachability projection) instead of
  `TryResolveWriteSurface`. A plain member's `POST /groups/{id}/add-member`
  now 404s, matching the consistent failure shape of the other write
  lanes (never a 200 + error text).
- `Groups/Detail.cshtml` offers the "Add a member" form only when
  `IsOwner || IsGlobalAdmin` (the same `canManage` expression that gates
  the per-member Remove forms and the invite lane) — so what a user sees is
  exactly what the route would accept.
- GlobalAdmin keeps the add lane, matching the remove/invite lanes (the
  admin's management reach is the M1 break-glass rule; ADR 0003).
- The owner ∪ member *read* projection (`GetGroupsForUserAsync`, F14) is
  untouched: plain members still see the group, its member list, and its
  pending invitations — they just see no mutation forms.
- The Core seam, its `Via` derivation (owner ⇒ Owner, else Admin), and the
  m2b invitation lane are unchanged.

## Consequences

- Membership adds and removes now have a single SoD story on the Web layer:
  the owner surface. One mental model for "who can change this group", not
  two.
- A plain member who previously added people loses that ability — intentional,
  per the product position above. Existing audit rows are unaffected (their
  `Via` derivation was always about the *seam's* actor-vs-ownerid compare).
- Adding a new write lane to groups in the future sits on
  `TryResolveOwnerSurface` by default; broadening any lane to "any member"
  again is a breaking change that belongs in an ADR.
