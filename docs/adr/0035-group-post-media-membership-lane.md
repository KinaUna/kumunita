---
id: adr-0035
title: "Group-post media: the membership lane is the serve decision"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/attachment-inline-preview-design.md]
supersedes: [adr-0025]
superseded_by: []
updated: 2026-11-02
---

## Context

Two shipped truths collide at the serve route:

- **Group posts are gated by membership, not by audience.** ADR [0013](0013-group-posts-membership-lane.md)'s
  G·1/G·2/G·8 pin this: a post with non-empty `Post.GroupId` is a
  group-lane post; its visibility is *exactly* the group's current
  membership (`CanSeeGroupAsync`, the membership lane); the audience lane
  is **never** evaluated; the post's `Audience` is written non-null
  **empty**. The post's own detail read gate
  (`PostService.GetGroupPostAsync` → `CanSeeGroupAsync`) is the membership
  lane.
- **The media serve routes gated on the audience lane.** Both
  `ContentImageController.Serve` and `AttachmentController.Serve` — and the
  reply-parent branch of each — resolved the owning post and called
  `IAuthorizationService.CanAsync(…Read…, PostToAuditableResource(post))`.
  `PostToAuditableResource` projects `Audience = post.Audience` (the
  empty one), and the empty-audience-denies invariant (ADR [0006](0006-module-boundary-contracts.md)-C1) makes
  `EvaluateAudience` return `false` for an empty audience. The only
  branches that could fire were **owner** (the author) and **break-glass**
  (a consumed `AdminOverride`).

**Net effect before this ADR:** a *regular group member* — the exact class
of user who *can* see the group post via the membership lane — got a **404**
on that post's inline content images and attachment downloads. The post's
own read gate (`CanSeeGroupAsync`) and its media's gate
(`CanAsync` on the empty audience) were two different lanes, and the
audience lane is structurally closed for group posts.

The fix is small and surgical, but it settles a *design* question — *which
lane is the "single `Read` decision" for a group post's media?* — that
previously went unrecorded (both serve routes were authored on the
component-post assumption that the audience lane is the decision). ADR [0025](0025-rich-content-markdown-and-content-images.md)'s
D4 and ADR [0034](0034-file-attachments-posts-replies-announcements.md)'s D4 both say "the post's one `Read` decision"; for a group
post, the post's one `Read` decision *is* the membership lane (ADR [0013](0013-group-posts-membership-lane.md)
G·1). This ADR records that decision and the shared seam that enforces it.

## Decision

- **The lane choice is driven by the post's own shape.** A post with
  non-empty `Post.GroupId` (ADR [0013](0013-group-posts-membership-lane.md) G·2) routes to the **membership** lane
  (`IAuthorizationService.CanSeeGroupAsync(actorId, post.GroupId,
  post.Id)`); a post with empty `Post.GroupId` routes to the **audience**
  lane (`IAuthorizationService.CanAsync(actorId, AccessAction.Read,
  new PostToAuditableResource(post))`). One decision call, one audit row
  (Allow *or* Deny — the `IAuthorizationService` seam emits it, not the
  route). This is the same routing the post's *own* detail read gate uses
  (`GetGroupPostAsync` vs `GetPostAsync`), so a member who can open the
  post can open its media, and a non-member gets a 404 on both.
- **The routing is concentrated in one `public static` Web seam —
  `Kumunita.Web.Security.PostReadDecision.ResolveAsync(post, actorId,
  authz)`.** Both serve routes and the reply-parent branch of each delegate
  to it. The seam is the *single* place the lane choice is written; the
  controllers no longer carry the branch inline. The seam is
  **testable in `Kumunita.Web.Tests`** (NSubstitute `IAuthorizationService`
  — no store, no Postgres) because it is a pure function of the post's
  shape and the authorization seam it is handed.
- **The reply-parent branch uses the same seam.** A reply's media is
  served under its **parent post's** single `Read` decision (C-M3·1,
  ADR [0034](0034-file-attachments-posts-replies-announcements.md) D4). The parent's own shape decides the lane: a group-post
  parent ⇒ the membership lane (the parent's `GroupId`); a component-post
  parent ⇒ the audience lane (the parent's `Audience`). One decision
  call, one audit row, in either case.
- **The image lane's reply-404 drift pause is *not* lifted by this ADR**
  (ADR [0034](0034-file-attachments-posts-replies-announcements.md)'s "not decided here" record holds). The reply-parent *routing*
  is now group-aware (a group-post reply's attachment and image both route
  to the membership lane), but the image lane's reply branch still 404s
  unconditionally (the `PostReply.ImageIds` field is still unpopulated by
  the reply write lanes — the ADR [0025](0025-rich-content-markdown-and-content-images.md) lane's deliberate "create only, not
  edit" precedent, C-ATT·9). The attachment reply lane is the one that
  works for group posts.
- **No new `AccessAction`, no new `IAuthorizationService` method, no new
  authorization module** (ADR [0006](0006-module-boundary-contracts.md)-D). The two lanes this seam picks
  between — `CanSeeGroupAsync` and `CanAsync` — are both frozen. The seam
  only *chooses*; it does not *add*.
- **The audit row is the lane's, not the seam's.** On a group-lane Allow,
  the row is `TargetKind = "grouppost"`, `Via = Group` (or
  `Via = Delegation` for an in-scope delegate acting with the owner's
  membership); on a component-lane Allow, the row is `TargetKind = "post"`,
  `Via = Owner / Audience / Moderator / BreakGlass / Delegation` (the
  §4.4 order). The "who did this, by what right" query the audit log
  exists to answer (ADR [0006](0006-module-boundary-contracts.md)) is preserved by the *correct* lane being
  chosen — a second call on the wrong lane would emit a row with the
  wrong `Via` and corrupt the log.

## Consequences

- **Two serve routes change behavior for group posts.** A regular group
  member (not the author, not a break-glass GlobalAdmin) can now open a
  group post's inline content image and its attachment download. A
  non-member to a group post gets a **404** on the media (one `Deny` audit
  row on the membership lane — `Via = Group` / `Delegation`, `TargetKind =
  "grouppost"`), the same 404 posture as the post's own detail read gate
  (ADR [0013](0013-group-posts-membership-lane.md) G·3/G·4). The component-post behavior is **unchanged** (the
  audience lane was and is the decision; the seam routes it there).
- **One new file, one new test file, one ADR README row.**
  `src/Kumunita.Web/Security/PostReadDecision.cs` (the shared seam),
  `tests/Kumunita.Web.Tests/PostReadDecisionTests.cs` (5 tests — the
  routing pin: group post → membership lane, component post → audience
  lane, Deny passes through on both lanes, null post throws), and this
  ADR. The two controllers' post branches and the attachment controller's
  reply-parent branch each lose their inline 2-line branch and call the
  seam; the image controller's reply branch (the drift pause) is
  **untouched** (ADR [0034](0034-file-attachments-posts-replies-announcements.md)'s "not decided here" record holds).
- **The two serve routes now share a single routing decision.** Before
  this ADR, the lane choice was written inline in three places (the post
  branch of each controller + the reply-parent branch of the attachment
  controller). A routing bug in any one copy would have let a group-post
  member be served (or denied) inconsistently across the three surfaces.
  The seam makes the routing a *single* testable unit; the 5 tests in
  `PostReadDecisionTests` pin it at the seam level (no store, no Postgres).
- **The drift-paused Web serve FACES remain paused** (ADR [0034](0034-file-attachments-posts-replies-announcements.md)'s "known
  limitation" record holds). `AttachmentServingTests` /
  `ContentImageServingTests` still carry zero `[Fact]` methods — they need
  a drivable `PostService` reverse-lookup seam (or Testcontainers in
  `Kumunita.Web.Tests`), a separate infra decision. This ADR's 5 tests
  pin the *routing* (which the paused FACES depend on); a future lift of
  the paused FACES would build on this seam, not re-derive the routing.
