# ADR 0010 — Private groups: a membership/organizing unit, hidden from the audience pickers

Status: Accepted
Date: 2026-09-10

## Context

Two product wants collided with the group model as shipped after ADR 0009:

- **Cluttered access lists.** Every group on the platform appears in both
  grant/access pickers (the profile's contact-visibility editor and the post
  composer's audience editor both seed their group option list from
  `IUserInfoService.GetAllGroupsAsync`). In a single-neighborhood deployment the
  group list accumulates quickly — "mushroom hunters", "bike owners", "Building
  4", … — and each one adds a row to every resident's picker, even when the
  resident has nothing to do with most of them.
- **A family-organizing primitive with no privacy.** A family (or a circle of a
  handful of people) that wants to keep its own discussions, to-dos, and contact
  details in a place that is *not* a grantable audience for strangers on the
  platform had no representation before this ADR. The group was either a public
  reuse unit (picker-able by everyone) or nothing — there was no "organizing
  place that is not a candidate for grant."

The product want (2026-09-10): a group can optionally be **private**. A private
group is a pure membership/organizing unit — its members can see one another and
discuss among themselves — but it is **never** an audience option: it is hidden
from both grant/access pickers (for everyone, including a non-member), so the
pickers show only users and public groups.

## Decision

- **The flag is a plain property on the `Group` document.** `Group.IsPrivate`
  is a non-nullable `bool` (default `false` = public, matching the
  `Profile.Blocked` / `Component.Enabled` precedent for booleans on Marten
  docs). Under this stack's Marten 9 the document lives as a JSONB `data`
  column in the `mt.mt_doc_group` table, so no DDL change is required for the
  new property at all — a legacy row whose JSONB carries no `IsPrivate` key
  reads back on the C# default (`false` = public), and the
  `ApplyAllConfiguredChangesToDatabaseAsync` boot step is a no-op for it.
  `GroupIsPrivateUpgradePathTests` pins exactly this upgrade path against a
  real Postgres (strip the key from a populated row, reboot the new code,
  and verify the row reads public and the privacy lane still works).

- **`CreateGroupAsync` gains a defaulted parameter.**
  `CreateGroupAsync(ownerId, name, description, isPrivate = false)` — a
  source-compatible extension per ADR 0006-E: every existing 3-arg caller binds
  unchanged and produces a public group; the create form's new "Private group"
  checkbox drives the `true` value.

- **Two new named seams on `IUserInfoService`** (ADR 0006-E additions — the same
  shape ADR 0009 established for the description lane):
  - `SetGroupPrivacyAsync(groupId, isPrivate, updatedBy)` — the **write lane**
    (owner ∪ GlobalAdmin standing, ADR 0007's new-lane rule). One session: load
    the group, mutate `IsPrivate`, append one `AccessAudit` row (action
    **`group.update`**, `TargetKind` "group", `TargetId` = the group id) in the
    same transaction (invariant C3), one `SaveChangesAsync`. `Via` is derived
    exactly like every other group lane: `updatedBy == Group.OwnerId ⇒ Owner`,
    else `Admin`; the effective principal folds to the owner on the Owner lane.
    Strong consistency (C4): the flag is live on the next `GetGroupAsync` /
    `GetGroupsForUserAsync` / `GetPublicGroupsAsync` call.
  - `GetPublicGroupsAsync()` — the **public-only read lane**: the
    `GetAllGroupsAsync` shape (`Created` desc) filtered to `!IsPrivate`. Both
    grant/access picker seeders consume this.

- **The pickers consume `GetPublicGroupsAsync`.** The profile's
  contact-visibility editor
  (`ProfileController.SeedGrantPickerOptionsAsync`) and the post composer's
  audience editor (`PostsController.SeedGrantPickerOptionsAsync`) both seed
  their group options from `GetPublicGroupsAsync()` — so a private group, once
  it flips, is absent from the picker on the very next render (C4 on that lane).
  The pickers' group candidates = the platform's **public** groups; a private
  group's organizing power comes from **membership**, never from a grant.

- **The surfaces.**
  - **Create** — a new "Private group" checkbox on the create form; checked →
    the group is born hidden from the pickers (no owner action needed post-create).
  - **Detail** — the `/groups/{id}` header shows a "Private" badge when
    `IsPrivate`; an owner ∪ GlobalAdmin "Privacy" section (gated
    `TryResolveOwnerSurface` — the same gate ADR 0009's description edit uses)
    carries a single "Private group" checkbox posted to
    `POST /groups/{id}/update-privacy`. One route, one form, both directions:
    unchecked = public, checked = private (a blank form value binds `false` to
    the `bool isPrivate` parameter — the same shape ADR 0009's
    "blank clears" mapping uses for the description).

## Consequences

- **The family case lands.** A family (or a circle of a handful of people)
  creates one private group, adds the household, and keeps its discussions and
  organizing there — without that group being an audience option on any other
  resident's post composer or contact-visibility editor.

- **The pickers stay lean.** Every grant/access picker on the platform now shows
  only users and **public** groups — the "mushroom hunters" style of reuse unit —
  and no "family" or "my circle" group crowds them.

- **`group.update` gains a second field.** The audit verb ADR 0009 introduced for
  the description lane now covers two mutable group fields: the description and
  the privacy state. The `Action = group.update` + `TargetId = groupId` pair is
  the audit row's identity; the two fields write through two distinct Core seams
  (`UpdateGroupDescriptionAsync` / `SetGroupPrivacyAsync`), each producing its
  own audit row on the same verb.

- **ADR 0007's standing is consistent, not extended.** Every group write lane —
  add/remove/invite/leave (ADR 0007/0008), description (ADR 0009), privacy
  (this ADR) — sits on **owner ∪ GlobalAdmin**; the seam itself does not re-gate
  (ADR 0006-D), the Web's `TryResolveOwnerSurface` does. The privacy lane is a
  third consumer of the same standing.

- **Non-decisions:**
  - **The group's own read gate is unchanged:** the `/groups` list and the
    `/groups/{id}` detail are still owner ∪ member (via `GetGroupsForUserAsync`)
    for **both** public and private groups; a non-member 404s on both. Privacy's
    observable effect is on the *platform-wide audience pickers* (exclusion),
    not on the group's own surface (which is member-scoped either way).
  - **The `/groups` list-row pin stays at 3 fields** (`{Id, Name, MemberCount}`).
    A private group shows in "my groups" for its members exactly the way a
    public one does; the list row does not need a 4th `IsPrivate` field — the
    privacy flag's only member-visible surface is the detail's "Private" badge.
  - **Membership is unaffected by privacy.** A private group's members still
    resolve its id in `GetGroupIdsAsync` (their membership read), the invite
    flow still works, and the group's `Description` / membership management are
    unchanged from the public case. What changes is the *candidate set* of
    audience options a non-member stranger sees in a grant picker — and only
    that.

- **Backward-compat:** the `isPrivate` parameter defaults to `false`, so every
  pre-existing group in a running deployment is unchanged by this ADR until an
  owner actively flips it to private.
