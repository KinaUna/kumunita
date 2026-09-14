# ADR 0024 — Author soft-delete lane (posts + replies, community + group)

Status: Accepted
Date: 2026-09-12

## Context

Two shipped truths collide:

- **The platform has a moderator hide/remove surface but no author "take it
  down."** M3b (C-M3b·3, F3/F4) gives a `Moderate`-gated write lane
  (`HidePostAsync` / `RemovePostAsync`) that sets `PostStatus.Hidden` or
  `PostStatus.Removed` — the *moderator's* lever. ADR 0014 / 0016 give an
  author-only *edit* lane. But the **author has no way to remove their own
  content**: a post that was a mistake, a reply that was out of turn, a
  comment that has become outdated. The only way to "delete" is a hard
  `session.Delete` (the announcement surface, `AnnouncementService.DeleteAsync`)
  — which destroys the record and orphans replies.
- **The neighborhood trust model demands soft delete.** A resident who
  deletes a post expects *their* replies and *other people's* replies to
  remain visible (the thread is still a record of the conversation). A hard
  delete orphans those replies (their `PostId` points at a vanished document)
  and destroys the audit trail. A soft delete keeps the record, hides the
  body from feeds, and renders a placeholder in the detail view — the same
  shape as the M3b `Hidden` state, but *author-initiated* and *reversible
  in principle* (a future admin restore lane could clear `DeletedAt`).

A new lane on a frozen module surface is a deliberate, reviewable decision
(ADR 0006-E). This ADR records the decision; the code is the authority on
shape.

## Decision

- **The lane is author-only — no moderator, no GlobalAdmin, no break-glass.**
  A direct sibling of the ADR 0014 (component) and ADR 0016 (group) edit
  lanes. The sole decision is `post.AuthorId == actorId` (ordinal
  comparison); a non-author is denied (`UnauthorizedAccessException`) even at
  GlobalAdmin. The Web layer maps the exception to its lane's failure shape
  (403 component, 404 group — the non-leaky pin, ADR 0014 / 0016).

- **The write is a single field: `DeletedAt` (nullable `DateTimeOffset`).**
  Additive (ADR 0004 §B.1 — delta-detected by Marten, idempotent, no re-seed,
  no schema-file change). Set on delete; `null` while live. Distinct from
  `PostStatus` (the M3b *moderator* surface): an author delete does not touch
  `Status` at all, so a post can be `Active` (from the moderator's view) and
  `DeletedAt != null` (from the author's view) simultaneously — the two
  concerns are orthogonal.

- **Lane-neutral: one `DeletePostAsync` + one `DeleteReplyAsync` in
  `PostService`.** Unlike `UpdatePostAsync` / `UpdateGroupPostAsync` (which
  differ in editable surface), the delete is the same field write regardless
  of lane — the Web layer's 403/404 failure shape is decided there, not in
  Core.

- **Feeds exclude deleted posts; detail lanes still return them.** The three
  feed queries (`ListFeedAsync`, `ListAllFeedAsync`, `ListGroupFeedAsync`)
  add `&& p.DeletedAt == null` to their candidate filter. The detail lanes
  (`GetPostAsync`, `GetGroupPostAsync`) do **not** filter — the post is
  returned as-is so the view can render the placeholder, and the replies
  (their own documents) remain visible.

- **Replies are their own documents.** A reply soft-deleted under a live post
  stays visible (placeholder in place of the body, still counts toward the
  parent's reply count). When the parent post is soft-deleted, the whole
  thread leaves the feeds, but the replies are not hard-deleted — their
  `DeletedAt` is the *author's own* action on the reply (or `null` if they
  didn't delete it).

- **No `AccessAudit` row.** The author is acting on their own content (the
  ADR 0014 / 0016 edit-lane precedent: content changes by the author are not
  audited). The audit lane is for *decisions about others'* content, not for
  an author managing their own.

- **Idempotent.** A second delete just moves `DeletedAt` forward. No
  "already deleted" error.

- **Web surface: four new POST routes.**
  - `POST /posts/{id}/delete` — component post (403 shape)
  - `POST /posts/{id}/replies/{replyId}/delete` — component reply (403 shape)
  - `POST /groups/{id}/posts/{postId}/delete` — group post (404 shape)
  - `POST /groups/{id}/posts/{postId}/replies/{replyId}/delete` — group reply (404 shape)
  All `[ValidateAntiForgeryToken]`. The detail views show a Delete button
  (author-only, alongside the existing Edit button) with a `confirm()` guard,
  and render a placeholder when `DeletedAt != null`.

## Consequences

- **Additive schema (ADR 0004 §B.1).** `Post.DeletedAt` and
  `PostReply.DeletedAt` are nullable `DateTimeOffset` fields. Marten's
  schema builder picks them up on the existing doc-type surface
  (`M3DocTypes.Configure` already registers `For<Post>()` /
  `For<PostReply>()`). No seed reset, no schema-file change. Existing rows
  read `DeletedAt = null` (live) — zero migration risk.

- **No collision with M3b moderation.** `PostStatus.Hidden` / `Removed` are
  the *moderator's* surface (written only by `HidePostAsync` /
  `RemovePostAsync`, gated on `AccessAction.Moderate`). `DeletedAt` is the
  *author's* surface (written only by `DeletePostAsync` /
  `DeleteReplyAsync`, gated on `AuthorId == actorId`). They are orthogonal:
  a post can be `Status == Active` and `DeletedAt != null`, or
  `Status == Hidden` and `DeletedAt == null`. A future admin-restore lane
  could clear `DeletedAt` without touching `Status`.

- **The reply count stays honest.** A soft-deleted reply still counts toward
  the parent's reply count (the record is kept; only the body is hidden).
  The detail view renders a placeholder in its place.

- **The detail view is the only place a deleted post is visible.** Feeds
  exclude it; the detail page shows the placeholder + the remaining replies.
  This is the "the thread is still a record" shape — a conversation where
  one participant took their words down is still visible as a conversation.

- **No new bounded context, no new doc-type surface, no new authorization
  seam.** The lane is a write seam on the existing `PostService` (the same
  surface ADR 0014 / 0016 / 0022 added their seams to). The authorization
  decision is the ordinal author check, not a call to `IAuthorizationService`
  (the author owns their own content — no cross-context call needed).
