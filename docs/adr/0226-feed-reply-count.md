# ADR 0226 — Feed card footer with a reply count

Status: Accepted

## Context
ADR 0175 left the feed card footer (`.airy-post-foot`) as a TODO: it needs a reply count, and `PostListItem` did not
carry one. This is a data change, not only styling.

## Decision
- **Source:** a batched read per feed page, `PostService.CountRepliesAsync(IReadOnlyCollection<string> postIds)`
  returning `postId -> count` (posts with no replies are absent). One query per page, owns its `QuerySession`, no audit
  row (a reply inherits the parent post's `Read` decision already made by the feed). No schema or write-path change,
  no denormalised counter to keep in sync. Soft-deleted replies still count (ADR 0024), matching the detail page's
  "Replies (N)" heading.
- **Model:** `PostListItem` gains a trailing `int ReplyCount = 0`. Populated by the community feed, the all-sections
  feed and the group feed; `MyDraftsController` leaves it at 0.
- **View:** shared partial `Views/Shared/_PostCardFoot.cshtml` (model `PostCardFootModel(RepliesHref, ReplyCount)`)
  renders the footer with a speech-bubble icon and a link to `…#replies`. `Posts/Detail` gets `id="replies"` on the
  Replies heading. Labels use new keys `posts.reply_count_none/_one/_other` (en/de/fr/da; the plural pattern of
  `groups.member_count_*`, English literals as the floor).
- **CSS:** link inherits the muted footer colour, accent on hover/focus, 44px minimum height on coarse pointers
  (`palettes.css`).

## Not changed / open
- The bookmark button stays a TODO: it needs per-viewer bookmark state.
- de/fr/da strings are machine-written and need native review.
- Razor and C# are not compiled here; `PostReplyCountTests` needs a Postgres run.
