# ADR 0017 — Announcement edit lane: author-of-record ∪ GlobalAdmin (flat lane)

Status: Accepted
Date: 2026-09-13

## Context

The M3b platform-announcement lane (the M3b "platform announcements" bounded
context, `Kumunita.Core.Announcements`) lets a platform-wide or community
announcement be **created** by two classes of actor:

- **Public scope** (`AnnouncementScope.Public`): a GlobalAdmin.
- **Community scope** (`AnnouncementScope.Community`): a GlobalAdmin *or* a
  Moderator — for the **flat** all-residents shape (no `CommunityId`), or for
  a **targeted** shape (a `CommunityId`), where the Moderator must hold that
  community's `moderator:{id}` claim.

That create standing was always correct. The **edit** standing was not. The
edit lane (`AnnouncementService.UpdateAsync`, the `Edit` action's shape gate,
the `Index` row affordance) reused the create standing, so the flat
all-residents lane (the most sensitive — it broadcasts to every resident
regardless of community) was editable by **any** Moderator, not just the
author or an admin. A community moderator who did not write the announcement
could re-title or rewrite it.

The flat all-residents lane is the one lane where the create standing
deliberately broadens (a Moderator may originate a platform-wide message)
while the *ownership* of the message remains a single author-of-record
(`AuthorId` on the `Announcement` document — never re-assigned, so the author
is immutable on the edit lane). The edit lane should therefore be **narrower**
than the create lane: broadening who may *start* the message does not imply
broadening who may *re-write* it.

The targeted lane is different and was already correct: its edit standing is
that community's moderator, which is meaningful (the moderator governs that
community's channel) and is exactly who should be able to correct a message
addressed to it. The ADR 0014/0016 "author-only, no moderator/admin branch"
convention is the *post* lane's rule; the announcement lane is deliberately
**not** author-only — it is author **∪** GlobalAdmin — because an admin
legitimately curates platform announcements even when another admin or a
moderator originated them.

## Decision

- **The edit standing is scope-specific and, on the flat lane, narrower than
  the create standing.** The single write lane is
  `AnnouncementService.UpdateAsync`; it loads the stored row first (so the
  missing-id → `KeyNotFoundException` / 404 contract is preserved and the
  stored `AuthorId` is in hand), then applies the edit gate before any write:
  - **Public** (`AnnouncementScope.Public`): **GlobalAdmin only.** A
    non-admin is denied, regardless of authorship.
  - **Flat Community** (`AnnouncementScope.Community`, `CommunityId` is
    null): **the author-of-record (`AuthorId == actorId`) or a GlobalAdmin.**
    A non-authoring Moderator — even one holding the base `Moderator` role
    that still lets them *create* a flat all-residents announcement — is
    **denied** the edit. Nothing is written (the gate runs before the write).
  - **Targeted Community** (`AnnouncementScope.Community`, `CommunityId` set):
    **a GlobalAdmin or that community's moderator** (`moderator:{CommunityId}`
    claim). A non-authoring moderator of the target community may edit it; the
    flat-lane author rule does not apply here.
- **`AuthorId` is immutable on the edit lane.** `UpdateAsync` preserves the
  stored `AuthorId`; it never re-assigns authorship. `Created` is preserved;
  `Modified` is stamped (the same Modified-stamped / Created-untouched shape
  as ADR 0014/0016).
- **The create lane is unchanged.** `EnsureCreatePermissionAsync` keeps the
  existing standing (Public → GlobalAdmin; flat Community → GlobalAdmin or
  Moderator; targeted → GlobalAdmin or that community's moderator). Only the
  **edit** gate narrows.
- **Web split preserved** ("form is a shape, service is the gate"): the `Edit`
  action's shape gate and the `Index` row affordance both call the same
  scope-specific rule (`CanEditAnnouncement`), mirroring the service gate, so
  a form a user cannot submit is not rendered. The service re-check at POST
  (`UpdateAsync`) is the sole real gate. The denied shape is `ForbidResult`
  (403), the missing-id shape is `NotFoundResult` (404).
- **`CanEditAnnouncement` is the single Web affordance rule.** It takes the
  stored `AuthorId` and the actor's role set, and returns true only when the
  actor satisfies the scope-specific edit standing above. The `Detail` and
  `Index` surfaces both derive their edit affordance from it, so the
  "who may click Edit" rule and the "who may submit Edit" rule can never
  drift.

## Consequences

- A community moderator can no longer silently re-title or rewrite a flat
  all-residents announcement they did not author. The fix is in one place —
  `EnsureEditPermissionAsync` in `AnnouncementService` — and is mirrored in
  the Web affordance/shape gate, so it holds on every surface (Index row,
  Detail, GET/POST Edit).
- The create lane is untouched: a Moderator may still originate a flat
  all-residents announcement; they simply cannot edit one they did not write.
- The targeted-lane edit standing is unchanged and remains correct (the
  target community's moderator may correct its own community's message).
- Tests pin all three lanes: the Core `AnnouncementServiceTests` deny pin
  (`Update_Community_Flat_AsNonAuthorModerator_Denied_NotPersisted`) plus the
  author and GlobalAdmin positive pins on the flat lane; the Web
  `AnnouncementControllerTests` GET-edit shape pins (flat non-author
  moderator → 403, flat author → 200). The public-lane deny pin is updated to
  assert `ForbidResult` directly (the denied branch no longer writes
  `TempData`).
- No schema change, no new document type, no new bounded context. `AuthorId`
  already exists on `Announcement` and is the immutable author-of-record; the
  edit gate simply starts reading it. The `M3DocTypes` registration surface is
  unchanged.
- This lane is intentionally **not** author-only (contrast ADR 0014/0016): the
  GlobalAdmin branch is retained for platform curation. If a future
  requirement wants to remove the admin branch from the flat lane, it is a
  separate decision — this ADR's flat-lane rule is `author ∪ GlobalAdmin`, not
  author-only.
