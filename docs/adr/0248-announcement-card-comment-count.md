# ADR 0248 — Announcement cards show a comment count

Status: Accepted
Date: 2026-10-29

## Context
ADR 0226 put a reply count in the footer of post cards in the community and group feeds. The ui-refresh plan listed the same for other
card lists. Events have no comments or replies in the data model, so there is nothing to count there. Announcements do have comments
(ADR 0101), with two rules: only signed-in viewers can read them, and an admin can switch the whole feature off
(`AnnouncementCommentsEnabled`).

## Decision
- **Where:** the announcement list card footer (`Views/Announcement/Index.cshtml`) gets the same speech-bubble link as the post cards,
  to `/announcements/{id}#announcement-comments`: "No comments yet" / "1 comment" / "N comments". It sits first in the footer, before
  Edit and Delete. A card that has neither the link nor the admin actions shows no footer, as before.
- **When:** only for a signed-in viewer while the admin comments toggle is on, the same gate the detail's comment list uses. A visitor,
  or the toggle off, shows nothing and the controller makes no extra query.
- **How:** new `IAnnouncementService.CountAnnouncementCommentsAsync(ids)`, one batched query per page (the `CountRepliesAsync`
  precedent), soft-deleted comments counted like the detail list that shows them as placeholders. No authorization or audit in the
  seam: the ids are the page the list read already scope-gated, and the caller applies the signed-in and toggle gate.
  `AnnouncementIndexViewModel.CommentCounts` is `null` for "show nothing".
- Three new keys, `announcements.comment_count_none/_one/_other`, in en/de/fr/da (de/fr/da machine-written, native review welcome),
  with English floors in the view. The `kmb-post-replies` styles are reused; `#announcement-comments` gets a `scroll-margin-top`.
- Events cards are unchanged: no comments exist to count.

## Verified
Two Core tests (`AnnouncementCommentCountTests`: empty input; counts per announcement, soft-deleted included, unrequested and
comment-less ids absent) and three controller tests (`CommentCounts` filled for a signed-in viewer with the toggle on; null and no
query with the toggle off; null and no query for a visitor). Not built, not run and not looked at live: this environment has no
.NET SDK. Razor not compiled.

## Not covered
Other announcement surfaces (the pinned banner, the home page strip) show no count. The count does not distinguish deleted comments.
A to-do or project card count would need its own read; not requested.
