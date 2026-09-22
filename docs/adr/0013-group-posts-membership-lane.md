# ADR 0013 — Group posts: a membership-scoped group channel

Status: Accepted
Date: 2026-09-12

## Context

Two shipped truths collide:

- **Groups are collections of users, not channels.** M2/M2b gave a group
  membership, ownership, invitations, and privacy — but a group page has
  **no** post channel. ADR 0010 made a private group a pure
  membership/organizing unit that is **never** an audience option (both
  grant pickers seed only public groups), which closes the family case's
  privacy hole — yet M3 posts reach a group only through an **audience**
  grant, so in particular a private group has nowhere to *post*. For a
  group to *have* posts, the access unit has to be the thing private
  groups actually are: **membership**.
- **The platform already promises it.** `how-it-works.md` tells residents
  "you can post to a group — and when membership changes, all your past
  posts reach exactly the right people." The promise is only true if
  group-post visibility **is** current membership, evaluated per request —
  an audience grant evaluated once at publish time cannot deliver "reach
  exactly the right people" as membership moves.

Group posts therefore need a **new authorization lane beyond
audience**. A new lane on a frozen module surface is a deliberate,
reviewable decision
(ADR 0006-E), and the neighborhood trust model (single instance, resident
to resident, `how-it-works.md`) dictates its one hard edge up front: the
strictest privacy lane the platform has. This ADR records the decision;
`docs/design/group-posts-design.md` (Parts 1–2, authored by U1/U2) freezes
the invariants **G·1–G·8**, the 13 FACES, and the **exact C#** this ADR
points at — the design doc is the authority on shape; this ADR is the
authority on *why these shapes, and why nothing else*.

## Decision

- **The lane is membership — visibility and authoring alike.** A post with
  non-empty `Post.GroupId` is a group-channel post: who may see it is
  **exactly** the group's current members, resolved by a live
  `IUserInfoService.GetGroupIdsAsync` read per request (strong
  consistency, invariant C4) — a membership add or remove re-scopes the
  very next feed/detail (FACES G3/G4). The audience lane is **never
  evaluated** for a group post (G·1), and the post's audience grant list
  is written non-null **empty** (G·8) — nothing about it is grantable.

- **Only members may author.** The create gate **is** the group-lane
  decision (G·3, FACES G5/G6): Allow ⇒ the member may post; Deny ⇒ the
  service writes the group's **Deny audit row first, then** throws
  `UnauthorizedAccessException` so the row survives, and the Web renders
  **404** (the M2 "plain member's POST 404s" precedent — a non-member
  learns nothing about the channel's existence or contents). The owner may
  post **as** a member; the lane has no owner-skip.

- **One ADR 0006-E ADD lane on `IAuthorizationService` — nothing else
  moves on the frozen surface.** The four frozen `CanAsync` /
  `CanSeeAsync` signatures stay byte-identical; the group lane *adds*:
  the single-target pair `CanSeeGroupAsync(actorId, groupId, targetPostId
  [, IDocumentSession session])` and the feed pair
  `CanSeeGroupFeedAsync(actorId, groupId, candidateCount [, session])` —
  a **compatible** ADD on the owning module's surface per ADR 0006-E (the
  M2 `GetProfilesAsync` / M3b `Moderate` ADD precedent), with the exact
  frozen C# in design doc Part 2 §2.1 (including the `targetPostId` and
  feed-pair adjustments U2-A1/U2-A2 recorded there), plus
  `AccessVia.Group`, the **8th** value, appended **after** `Admin`
  exactly like the M1 `Admin` APPEND (a value-addition, never a
  renumbering; stored audit rows are unaffected).

- **The data is one additive, not a new doc.** `Post.GroupId` (`string`,
  default `string.Empty`) is the **single** change to the `Post` POCO —
  the ADR 0004 §B.1 additive lane, the M3b `Status` ADD precedent
  (delta-detected, idempotent, no re-seed, no `M3DocTypes` change). It is
  written **only** by `PostService.CreateGroupPostAsync`, which pins the
  write shape: `ComponentId = string.Empty` and `Audience = new
  Audience()` (G·2, G·8). `PostReply` is **unchanged** — replies are
  lane-neutral and **inherit** the parent's single group-lane decision
  (G·7, the C-M3·1 analog: no second evaluation, no reply-level row).
  `PostService`'s constructor is **unchanged** (ADR 0006-D: the lane owns
  every membership read; `PostService` never reads `GroupMembership` or
  `DelegationGrant` itself).

- **Lanes are exclusive.** One post is either a component feed entry
  **or** a group-channel post — never both (G·2): a group post
  (`GroupId` non-empty) has an empty `ComponentId` and is **structurally
  absent** from `ListFeedAsync` / `ListAllFeedAsync` (both already filter
  on `ComponentId`; no filter is added for the exclusion), and a component
  post never carries a `GroupId`. No cross-posting between the two lanes
  in this milestone.

- **Delegation, action-scoped, as always (C2 / G·6).** An in-scope
  `read` grant lets the delegate act with **the owner's** standing —
  they see the group's posts iff the **owner** is a member — and the
  decision row audits `Via = Delegation`; an out-of-scope grant still
  denies (FACES G9/G10). This is the only non-`Group` `Via` value on the
  lane, by design.

- **Audit — per lane, always on, in-transaction (C3 / G·5).** A feed visit
  writes **one aggregate** `AccessAudit` row (TargetKind `"grouppost"`,
  TargetId null, `visibleCount`/`hiddenCount`); a detail view and the
  create gate each write **one decision** row (TargetKind `"grouppost"`,
  TargetId = the post id / the group id); **Allow and Deny** are both
  audited; the `IDocumentSession` overloads commit the row **inside the
  caller's transaction** (same lane as `CanAsync(..., session)`), and the
  create gate's Deny row is committed **before** the throw so it survives
  the rejection — "no silent, unaudited access" (C1–C3) holds unchanged
  for this lane's rows.

- **No break-glass, no moderator peek — a standing rule, not a deferral
  (G·4).** A non-member moderator and a non-member GlobalAdmin are
  **denied** group posts; there is no `Via = BreakGlass` branch, no
  `ModeratorAssignment` / `Component.ModeratorAccess` path, and no
  `AdminOverride` read on this lane — C5's "moderators are off by
  default" is carried to its strictest reading for the platform's most
  privacy-sensitive lane (ADR 0003; `how-it-works.md`'s trust pitch).
  This is **recorded here as a deliberate "not available"** and is
  deliberately **not** placed in the deferral list in Consequences:
  re-litigating it later requires an ADR 0013 amendment, per the design
  doc's drift-guard.

## Consequences

- **The family case lands (closing ADR 0010's want).** A family (or a
  small circle) creates a private group, invites the household, and keeps
  its running discussions *inside* that group — a real private channel the
  rest of the neighborhood can neither see (FACES G1/G2) nor peek into (G7
  / G8), and the group's membership is the only key to it, at all times.

- **The promise in `how-it-works.md` is now checkable, not aspirational.**
  Membership changes taking effect on the next request (C4) is what makes
  "when membership changes, past posts reach exactly the right people"
  true, and the seam tests pin it on both sides (G3: an added member
  sees; G4: a removed member loses).

- **The frozen surface grows by exactly one lane.** The 0006-E
  "named here (…)" list gains, **at this milestone's close (U11/U12)**:
  the four group-lane methods on `IAuthorizationService`, the
  `AccessVia.Group` value, the `Post.GroupId` additive, and the
  `GroupPostDraft` record (a type, not a seam) — each an ADD, each
  recorded here so the surface stays auditable, per 0006-E's "add few,
  add stable."

- **The lane is checkable at its seams.** 19 named seam tests (design doc
  Part 2 §2.5, `Kumunita.Core.Tests/GroupPostServiceTests.cs`) plus the
  three-test acceptance gate recorded by U10 cover every FACES row
  G1–G13, including the *absences* (no break-glass, no moderator branch,
  structurally-excluded feeds, replies-inherit). The invariant and FACES
  numbers (G·1–G·8, G1–G13) call on ADR 0006's **C1–C6** where they
  overlap, so the lane is checkable at its seams the same way M3's
  C-M3·1–·3 set is.

- **Named deferrals (carried forward; each deliberately out of scope):**
  - **Moderation / report flow on group posts** — M3b's component-scoped
    moderator standing does not reach a membership lane; no report queue,
    no resolve UI for group posts.
  - **Notifications on new group posts** (an M6-scope item).
  - **Search over group posts** (an M6-scope item).
  - **Cross-posting a group post into a component** — the two lanes stay
    exclusive (G·2); a standing rule, in practice not a deferral.
  - **Pagination UI beyond the existing feed paging** (no new paging
    controls in `Views/Groups/`).

- **Not deferrals — the "explicitly not available" standing rules of this
  ADR (G·4):** break-glass on group posts; moderator peek on group posts;
  non-member authoring on group posts. These are privacy-first answers
  (**no**, on purpose), not tickets to work off in a later milestone —
  changing any of them requires an ADR 0013 amendment, not a product
  decision.
