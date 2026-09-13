# ADR 0016 — Group-post + reply edit lane: author-only

Status: Accepted
Date: 2026-09-13

## Context

ADR 0014 shipped the **component-lane** post edit seam
(`PostService.UpdatePostAsync`): an author re-writes a post's
`Title` / `Body` / `Audience`; `ComponentId` / `AuthorId` / `Created` /
`Status` / `GroupId` are immutable on that lane; a non-author is denied even
at `GlobalAdmin` (the author's voice is exclusive). Its Consequences
deliberately deferred two adjacent lanes:

> Component re-targeting (moving a post between feed buckets) and **group-post
> editing remain out of scope** — each is a separate lane + decision if ever
> wanted.

The group-lane post (ADR 0013, G·1–G·8) had been **write-once** since the
group-posts milestone: a member who mistyped a group post had no way to
correct it, and a member who wanted to refine a reply had no way to do that
either. The M3 `PostServiceTests` pin (and ADR 0014's own decision text)
made the author-only standing the platform's single convention for post-text
editing, and the group lane's "explicitly not available" rules (G·4 — no
moderator peek, no break-glass) rule out a non-author edit standing on that
lane. The reply side had no edit lane at all — `PostReply` had no
`Modified` field to carry one.

The ADR 0014 pattern already encodes every decision needed here — author-only
standing, `Modified`-stamped / `Created`-untouched, no auditor vocabulary
change, the "form is a shape, service is the gate" Web split. The only
substance that differs is **which fields are editable** (the group lane has
no `Audience` slot and no `ComponentId`) and the Web **failure shape**
(group lane is 404, component lane is 403 — G·3/G·4's non-leaky pin).

## Decision

- **Group-post editing is author-only, with no moderator or admin branch.**
  The new seam `PostService.UpdateGroupPostAsync(postId, actorId, title,
  body, languageCode, session)` is the **single** write lane. Its gates are,
  in order:
  a missing id is a `KeyNotFoundException`; the post must be a **group**
  post (non-empty `GroupId`) — a component post (empty `GroupId`) is the
  ADR 0014 lane's shape and is fail-closed here with the same
  `KeyNotFoundException` (a cross-lane edit is never a re-lane — each seam
  owns exactly one post shape); and the actor must be the post's own
  author (`post.AuthorId == actorId`, ordinal) or
  `UnauthorizedAccessException`. There is deliberately **no** role check
  on this lane: a `GlobalAdmin` who is not the post's author is denied; a
  member who is *not* the author is denied; a member who *is* the author
  is allowed. This is G·4's "no moderator, no break-glass" standing applied
  to a **write** lane — the author's voice is exclusive, and the platform
  has no other lever over group-post text than the absence itself.

- **The editable surface on the group lane is `Title`, `Body`, and
  `LanguageCode` — and only those.** The group lane's identity fields are
  **immutable** on the edit lane: `GroupId` (the lane itself, G·2),
  `ComponentId` (must stay empty, G·2 lane exclusivity), `Audience`
  (non-null empty, G·8 — there is no audience to re-choose on this lane; the
  membership is the audience proxy), `AuthorId`, `Created`, and `Status` are
  all untouched. `Modified` is stamped forward (null until first edited).
  Strong consistency (C4): the new values are live on the **very next**
  `GetGroupPostAsync` / `ListGroupFeedAsync` render.
  **Amended 2026-09-13:** the authored-in `LanguageCode` (ADR 0018) is now
  also editable on this lane — a non-empty submitted code is written
  verbatim; a blank submission re-materializes through the shared resolver
  (instance default, `en` floor, never blanking a stored tag), mirroring
  the ADR 0017 announcement-edit precedent. The **reply** edit lane
  (body-only, below) is unchanged — replies keep their create-time tag.

- **Reply editing is author-only, body-only.** The new seam
  `PostService.UpdateReplyAsync(replyId, actorId, body, session)` is the
  **single** write lane. A missing reply id is a `KeyNotFoundException`;
  the actor must be the reply's own author
  (`reply.AuthorId == actorId`, ordinal) or
  `UnauthorizedAccessException`. The only editable field is
  `PostReply.Body`; `PostId`, `AuthorId`, and `Created` are immutable (a
  reply cannot be re-parented or re-attributed). `Modified` is stamped
  forward (null until first edited). Like `CreateReplyAsync`, the seam
  writes **no** `Authorization.AccessAudit` row of its own (C-M3·1 — the
  reply inherits the parent's single decision; an author editing their own
  reply is a content change, not an access decision).

- **`PostReply.Modified` is the single new POCO field on the reply lane**
  (ADR 0004 §B.1 additive — delta-detected, idempotent): it mirrors
  `Post.Modified` (which was ADR 0014's first live surface). A reply that
  has never been edited has `Modified == null`; after an edit it carries
  the last-edit time. The group-post lane reuses the **existing**
  `Post.Modified` field (already live per ADR 0014).

- **The failure shape is 404, not 403 — for both the group-post and
  group-reply edit routes.** A non-author, a non-group post, a missing
  group, a missing post, or a reply not under this post is a `NotFound()`
  **404** on both the `GET` and `POST` edit routes. A **403** would leak
  which ids are real; a re-render of the form would leak the post's content
  to a non-author. A 404 tells the viewer nothing about either. This is the
  group lane's "a non-visible group 404s" standing (G·3/G·4, the
  `GroupsController` create-lane precedent) carried to the edit lane — the
  *opposite* of ADR 0014's component-lane 403, which is the audience
  lane's standing. The two lanes are deliberately asymmetric in failure
  shape because they are asymmetric in their access unit (membership vs.
  audience): each lane's 404/403 is the non-leaky shape *for that lane*.

- **Defense-in-depth: the Web layer is a shape gate, the service is the
  decision.** The `GroupsController.EditGroupPost` GET action re-checks
  `post.AuthorId == actor` and the group-lane shape before rendering the
  form (so a non-author never sees the seeded Title/Body). The paired POST
  catches both `KeyNotFoundException` (missing id or non-group post) and
  `UnauthorizedAccessException` (non-author) and maps both to `NotFound()`.
  The reply-edit POST re-runs the parent's group-lane decision via
  `GetGroupPostAsync` (G·7 — the reply inherits it) and also checks the
  reply is under the named post before writing; both `KeyNotFoundException`
  and `UnauthorizedAccessException` are mapped to `NotFound()`. The service
  is still the authoritative gate — the controller's checks are the non-
  leaking *shape*, not the decision.

## Consequences

- A group post is finally a **live, correctable** artifact: a member can
  fix a typo, refine the body, adjust the title, or — as amended 2026-09-13
  (ADR 0018) — correct the authored-in language tag, after publishing,
  without deleting (and orphaning replies to) and re-posting. A reply
  likewise (body-only, its tag stays the create-time one).
- `PostReply.Modified` becomes a live field (it was `null`-only before this
  lane) — the first surface that reads it is the reply-edit round-trip; any
  future "edited" badge on a reply has a field to read. `Post.Modified` on
  a group post is now live on the group lane in addition to the component
  lane (ADR 0014).
- The **author-only standing** is deliberately narrower than ADR 0009's
  owner ∪ GlobalAdmin (group description edit) and consistent with
  ADR 0014's author-only standing (component post edit): post text — whether
  on the component or group lane — is the author's voice, so the author,
  and only the author, may edit it. A `GlobalAdmin` is not the author, and
  the group lane's "no moderator peek" standing (G·4) does not change to
  admit one here.
- The **moderator's lever over a group post is unchanged**: the group lane
  has no moderation surface at all (ADR 0013's "explicitly not available"
  standing — G·4, not a deferral). Editing a group post's text is *not* a
  moderation action and gains no moderator branch here.
- Two new Core seams (`PostService.UpdateGroupPostAsync`,
  `PostService.UpdateReplyAsync`) and a battery of `GroupPostServiceTests`
  (group-post: author-allows + `Modified`-stamped, title-nil-allows,
  immutable-fields-untouched, non-author Member / GlobalAdmin all denied,
  missing id, cross-lane fail-closed, and — as the editable surface was
  amended 2026-09-13 — author-changes-language-tag-persists-new-code and
  author-blank-language-resolves-instance-default; reply: author-allows +
  `Modified`-stamped, immutable-fields-untouched, non-author denied,
  missing id) pin the lane. No new verb enters the audit vocabulary: an
  author-edit on either lane is the author's own content change, not an
  access decision, so both seams write **no** `AccessAudit` row.
- The component-lane post edit (ADR 0014) is **unchanged**; the component
  reply-edit lane remains **out of scope** (a separate lane + decision if
  ever wanted — the same deferral ADR 0014 carried forward, now scoped to
  the *component* reply lane, with the *group* reply lane settled here).
- Component re-targeting (moving a post between feed buckets) remains
  **out of scope** (ADR 0014's own deferral, carried forward).
- `PostReply.Modified` is a **schema additive** (ADR 0004 §B.1):
  `Schema.For<PostReply>()` delta-detects the new nullable field; the
  `ApplyAllConfiguredChangesToDatabaseAsync` at boot adds the column
  idempotently; no re-seed, no data migration. Existing reply rows carry
  `Modified == null` (the "never edited" shape).
