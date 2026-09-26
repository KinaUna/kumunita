# ADR 0093 — Group delete lane (owner ∪ GlobalAdmin)

Status: Accepted
Date: 2026-09-27
Amends: nothing — adds a new group write lane on the standing ADR 0007 prescribes

## Context

The group-management surface (M2 U10 + ADR 0007 / 0008 / 0009 / 0010 + the
m2b invitation lanes) gives an owner the full set of *member* lanes
(add, remove, invite, cancel), the group's own *field* lanes
(description, privacy), and the *translation* lanes — but **no lane to
remove the group itself**. A group a user created cannot be deleted by that
user: there is no route, no seam, and no button. The only deletion shape in
the platform is per-content (author soft-delete, ADR 0024) or a hard
platform-level delete (announcements, `AnnouncementService.DeleteAsync`) —
neither applies to a group.

The product want: **the owner of a group is able to delete the group.** A
group is a user-created organizing unit (ADR 0010 — "a membership/organizing
unit"); the people who can add members, flip its privacy, and rewrite its
description should also be able to remove it. The standing for this lane is
settled by ADR 0007's consequences clause: "Adding a new write lane to groups
in the future sits on `TryResolveOwnerSurface` by default." So the lane is
**owner ∪ GlobalAdmin**, not owner-only — consistent with every other group
write lane (remove/invite/privacy/description).

## Decision

A new owner ∪ GlobalAdmin write lane, `POST /groups/{id}/delete`
(`GroupsController.DeleteGroup`), on top of a new Core seam
`IUserInfoService.DeleteGroupAsync(string groupId, string deletedBy)`:

- **Standing = owner ∪ GlobalAdmin.** The route resolves its surface
  through `TryResolveOwnerSurface` (the ADR 0007 new-lane rule — identical
  to add/remove/invite/privacy/description): a plain member's POST 404s,
  the consistent write-lane failure shape (never a 200 + error text). The
  form carries **no** actor id — the `deletedBy` the seam receives is minted
  from `KumunitaPrincipal.SubjectId(User)`, never a form field (the U10
  add-member / ADR 0008 leave self-lane pin: SoD by structural identity).
- **Hard delete, one session, one `SaveChangesAsync` (invariant C3).** The
  seam loads the `Group` (throws `InvalidOperationException` if absent —
  the other group write lanes' exception style), then in the same
  transaction removes:
  - the `Group` document;
  - every `GroupMembership` row for the group;
  - every `GroupInvitation` row for the group.

  and appends exactly one `AccessAudit` row — action `group.delete`,
  `TargetKind` "group", `TargetId` = the group, `Via` derived exactly like
  every other group lane (`deletedBy == Group.OwnerId ⇒ Owner`, else
  `Admin`; the effective principal folds to the owner on the Owner lane).
  There is **no notification** emission (this is a deletion, not an
  invitation — the ADR 0083 `GroupAdded` emission shape does not apply).
- **No cascade into group-scoped content.** The group's `Post` (group posts,
  ADR 0013), `Event` (group events, ADR 0089), and `GroupTranslation`
  (ADR 0026) rows are **not** deleted; they remain in storage. They become
  **unreachable**, because the group lane's authorization is
  membership-only (ADR 0013: "membership is the sole decision") and the
  membership rows are gone — `ListGroupFeedAsync` /
  `GetGroupPostAsync` / `ListGroupEventsAsync` / `GetGroupEventAsync` all
  fail closed to a non-member, so an orphaned group-scoped row surfaces to
  no one. This is the repo's standing "no hard-delete cascade" posture
  (ADR 0024 / 0086 / 0087 leave soft-deleted content in place rather than
  cascading) applied to the group axis: the group's identity is removed,
  its content is retained-but-inert.
- **Redirect = `Index`, never `Detail` (the ADR 0008 `LeaveGroup` shape).**
  After the commit the actor is out of the owner ∪ member projection on the
  very next read (C4 strong consistency), and every former member loses
  access — so a redirect to the detail page would 404. The success redirect
  goes to the `/groups` list, with a `TempData["info"]` confirmation (the
  `LeaveGroup` / `RemoveMember` redirect + message shape).
- **The button is canManage-gated.** `Groups/Detail.cshtml` renders the
  delete form only in the Settings tab (`canManage` = `IsOwner ||
  IsGlobalAdmin`), so a plain member never sees it — what a user sees is
  exactly what the route would accept. The confirm is the declarative
  `data-confirm` contract (`client/lib/confirm.ts`, SECURITY.md §6 no-inline-
  script rule), with the message text server-rendered + localized
  (`groups.danger_*` `kw-l` keys × 4 languages, the ADR 0015 curated registry
  the Settings tab's existing keys sit in).

## Consequences

- A new `group.delete` action name enters the `AccessAudit` vocabulary — one
  audit row per deletion, the `group.*` lane family's shape
  (add-member / remove-member / update / invite / invite.accept / …).
- Group deletion is a **destructive, non-undoable** platform action with
  exactly one standing (owner ∪ GlobalAdmin). There is no soft-delete for
  groups: the `Group` document has no `DeletedAt` / `IsDeleted` field, and
  the membership-only authorization means a hard delete revokes access
  immediately and cleanly (C4), which a soft delete would have to emulate by
  filtering ~8 read lanes. The ADR 0024 soft-delete idiom is intentionally
  *not* carried to groups.
- Orphaned group-scoped `Post` / `Event` / `GroupTranslation` rows are
  retained in storage and unreachable — consistent with the repo's
  "no hard-delete cascade" posture, but unlike a soft-delete they are
  invisible *because* their membership gate is gone, not because of a
  `DeletedAt` filter. A future "restore a deleted group" lane (out of scope
  here) would re-add the membership rows and the content would reappear.
- `Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are **untouched**
  — this is a named lane on the already-shipped M2 group surface, not a
  milestone (the ADR 0013 / 0089 group-lane precedent).
