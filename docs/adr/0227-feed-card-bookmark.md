# ADR 0227 — Bookmark toggle in the feed card footer

Status: Accepted

## Context
ADR 0226 left the bookmark button of the feed card footer as a TODO because the card needs the viewer's bookmark
state. Bookmarks already exist (ADR 0118): `IBookmarkService`, the `/bookmarks/toggle` and
`/bookmarks/{kind}/{id}/remove` form POSTs, and `_BookmarkButton` on detail pages (resolving one card at a time).

## Decision
- **State:** one batched personal read per page, `IBookmarkService.ListBookmarkedIdsAsync(ownerId, kind, ids)`,
  returning the set of bookmarked target ids. Same gates as `IsBookmarkedAsync` (no owner / unknown kind / no ids -> empty
  set, no query), no target read, no `AccessAudit` row.
- **Where it is called:** in the two host views (`Posts/Index`, `Groups/Detail`), via an injected `IBookmarkService`, not in
  the controllers. This avoids changing the `PostsController`/`GroupsController` constructors (many controller tests
  construct them directly) and keeps `PostListItem` unchanged. A failed read degrades to "nothing bookmarked".
- **View:** `PostCardFootModel` gains `PostId`, `IsBookmarked`, `ReturnUrl`. `_PostCardFoot` renders, at the right end,
  an icon button in a plain form POST (no JS): `/bookmarks/toggle` (kind `post`) when not bookmarked,
  `/bookmarks/post/{id}/remove` when bookmarked. `returnUrl` is the current feed path and query, so the viewer lands back
  on the same page with the usual flash toast. `aria-pressed`, a visually hidden label and a `title` reuse
  `bm.button.bookmark` / `bm.list.unbookmark` (no new keys). The toggle runs the existing write-lane visibility check.
- **CSS:** `.kmb-post-bookmark-btn` in `palettes.css` (accent when on or hovered; 44px target on coarse pointers).

## Not changed / open
- Group posts use the same `post` kind as `Groups/PostDetail` does.
- Razor and C# are not compiled here; `BookmarkBatchReadTests` needs a Postgres run.
