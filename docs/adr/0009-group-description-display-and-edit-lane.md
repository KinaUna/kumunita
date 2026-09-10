# ADR 0009 — Group description: resident-facing display + owner ∪ GlobalAdmin edit

Status: Accepted
Date: 2026-09-10
Supersedes: the M2-era pin that the group's `Description` was not
resident-facing (the `GroupDetailViewModel` shape test that excluded it)

## Context

The `Group` document carried a nullable `Description` field since M1, and the
create-group form captures it — but the field was dead surface:

- It was displayed **nowhere**: the group detail page's
  `GroupDetailViewModel` deliberately omitted it, and a shape-pinned test
  asserted the exclusion ("the M1 'admin surface' owns them" — a surface the
  shipped app never built a groups view on).
- It could not be changed after creation: `IUserInfoService` had no group
  update seam, so "fixing" a description meant deleting and recreating a
  group (orphaning the membership rows the owner had gathered).

The product want (2026-09-10): groups need descriptions — visible to the
people who can reach the group, and correctable by the group's owner.

## Decision

- The description is **resident-facing on the group detail surface**
  (`/groups/{id}`, already gated owner ∪ member for *reads*): rendered under
  the header when the group holds one, and shown identically to a member and
  to the owner (the detail's `IsOwner` badge/standing are presentation, not
  visibility). The `/groups` list row keeps its pinned 3-tuple
  (`{Id, Name, MemberCount}`); the detail page is where the field lives.
- The detail view model gains the one projected field
  (`GroupDetailViewModel.Description`); the shape-pin tests in
  `Kumunita.Web.Tests` grow correspondingly (8 → 9 fields, `Description`
  leaves the exclusion list, `Created` stays excluded).
- A new Core seam,
  `IUserInfoService.UpdateGroupDescriptionAsync(groupId, description,
  updatedBy)`, is the single write lane for the field. It follows the
  `AddGroupMemberAsync` lane's shape: one session, load the group, mutate the
  field, append one `AccessAudit` row (action **`group.update`**,
  `TargetKind` "group") in the same transaction (invariant C3), one
  `SaveChangesAsync`. Strong consistency (C4): the value is live on the next
  `GetGroupAsync` / `GetGroupsForUserAsync` call.
- The standing is **owner ∪ GlobalAdmin**, per ADR 0007's new-lane rule
  (which already sets the default standing for future group write lanes):
  the Web route
  (`POST /groups/{id}/update-description`) resolves through
  `TryResolveOwnerSurface` — a plain member's POST 404s, the consistent
  failure shape of the add/remove/invite lanes. The form carries only the
  description value; `updatedBy` is the actor's subject, minted from the
  signed-in principal (never a form field). The seam derives the audit
  `Via` exactly like every other group lane: `updatedBy == Group.OwnerId ⇒
  Owner`, else `Admin`; effective principal folds to the owner on the Owner
  lane.
- A blank form value **clears** the description (the create lane's
  whitespace-is-null mapping, U9): set and clear are the same route; `null`
  is the stored shape. `Created` and the group name stay outside this lane
  (renaming is not part of this decision).

## Consequences

- The description is finally a live feature: visible on the detail page,
  editable in-place, correctable without destroying the group.
- `group.update` enters the audit verb vocabulary (M1 froze it for the
  membership/invite lanes — this is the first non-lane-specific group verb,
  used here because the only currently mutable group field is the
  `Description`).
- The "M1 admin surface owns the description" story dies with this ADR
  (there was no admin groups view to own it) — the resident-facing detail
  surface is the description's home.
- Group names remain creation-time-only. Renaming, if wanted, is a separate
  lane + decision (and would need its own verb or a scoped `group.update`
  audit context).
