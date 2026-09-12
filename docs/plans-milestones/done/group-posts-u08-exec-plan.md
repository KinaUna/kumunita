# U8 exec — Group posts: view layer (Feed / PostDetail / New + Detail entry link)

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md`
**Unit plan:** `group-posts-u08-plan.md` · **U7 handoff:** group-posts-handoff-notes.md §U7

## Entry reads (completed)
1. `src/Kumunita.Web/Views/Groups/Detail.cshtml` — M2 group profile view; owner ∪ member gate already in controller.
2. `src/Kumunita.Web/Views/Posts/Index.cshtml` — M3 feed; `FeedViewModel`, `PostListItem` rows, "Write a post" button, hidden-count hint.
3. `src/Kumunita.Web/Views/Posts/Detail.cshtml` — M3 detail; reply list + inline reply form, report form (deferred for group lane).
4. `src/Kumunita.Web/Views/Posts/New.cshtml` — M3 composer; component picker + audience editor (both absent in group lane).
5. `src/Kumunita.Web/Models/GroupViewModel.cs` — U7's three group-post view-model types: `GroupFeedViewModel`, `GroupPostDetailViewModel`, `GroupPostComposeViewModel`.
6. `src/Kumunita.Web/Controllers/GroupsController.cs` (group-post section) — U7's four actions; the three view names emitted: `Feed` / `PostDetail` / `New`; `View("New", model)` is the failure re-render path (no GET route for the composer).
7. `src/Kumunita.Web/Models/FeedViewModel.cs` + `PostDetailViewModel.cs` — `PostListItem` / `ReplyItem` record shapes reused by U7.

## Deliverables (4 files, closed set)

| File | Action | Notes |
|---|---|---|
| `Views/Groups/Feed.cshtml` | **create** | Bind `GroupFeedViewModel`. Inline composer at top when `CanPost`. Feed items list. No audience picker, no community pills, no "Manage members" / "Leave" — all M3-specific. Hidden-count hint. |
| `Views/Groups/PostDetail.cshtml` | **create** | Bind `GroupPostDetailViewModel`. Post card + reply list + inline reply form. No report form, no "Write a post" link, no audience text. Reply form POSTs to `/groups/{GroupId}/posts/{Post.Id}/replies`. |
| `Views/Groups/New.cshtml` | **create** | Bind `GroupPostComposeViewModel`. Standalone composer form (title + body only). POST action resolved via `ViewContext.RouteData.Values["id"]` (the `{id}` from `/groups/{id}/posts`). Used only by U7's failure re-render path. |
| `Views/Groups/Detail.cshtml` | **modify** | Add a single "Post to this group" link in the header area (after the Private badge), routing to `/groups/{Model.GroupId}/posts`. One link, not a new section. |

## Key design decisions

### Composer placement (Feed vs New)
U7's controller has **no** `GET /groups/{id}/posts/new` route. The only place `View("New")` is emitted is the failure re-render path (no actor, invalid body). The plan's "composer surface is U8's to place" instruction + U7's handoff option "render the composer inline on Feed when `CanPost`" are both honoured by:
- **Feed.cshtml**: inline composer form at the top when `CanPost` is true (title + body, `action="/groups/@Model.GroupId/posts"`). This is the primary user-facing composer.
- **New.cshtml**: standalone composer form, rendered only on failure re-render. `GroupId` recovered from `ViewContext.RouteData.Values["id"]` (the current route is `/groups/{id}/posts`, so the `{id}` is available).

This matches the M3 pattern (Feed has a "Write a post" CTA; the full composer is a separate page) adapted to the group lane where the composer is inline (simpler, no extra navigation).

### Detail view — no moderation, no report
Per the plan and the register's "Out of scope" list: no "Report this post" form, no "Hide post" / "Remove post" buttons, no moderation actions of any kind. The reply list + inline reply form are the only interactive elements beyond the post body itself.

### Reply form
Inline at the bottom of `PostDetail.cshtml` (exactly the M3 `Posts/Detail.cshtml` pattern). Single field: `body` (textarea, required). `action="/groups/@Model.GroupId/posts/@Model.Post.Id/replies"`. The helper text adapts M3's "reply-inherits" wording to the group-lane framing (G·7: reply inherits the parent's single group-lane decision; no per-reply audience).

### Hidden-count hint
`GroupFeedViewModel` has `Total` (the aggregate total) and `Items` (the visible count). When `Total > Items.Count`, render the same hint M3 uses: "N posts in this group; M shown to you."

## Exit
- `dotnet build Kumunita.slnx -c Debug` green (touched project: `Kumunita.Web`).
- Handoff note appended to `group-posts-handoff-notes.md`: `## U8 — view layer`.
- Unit plan file moved: `in-progress/group-posts-u08-plan.md` → `done/group-posts-u08-plan.md`.
