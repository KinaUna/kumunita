# U8 — Group posts: view layer (feed / detail / new / reply + the entry link)

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
The **server-rendered view surface** — the group-post feed/detail/new/reply views in the Groups view area, mirroring M3's `Views/Posts/*` (plain form posts, no new components), **plus** the single entry-point link on `Groups/Detail` ("Post to this group") so the channel is reachable — M2's `Groups/Detail` is the only existing "group page surface." **No new frontend assets** (the plain-form + plain-list pattern from M3).

## Entry reads
1. `src/Kumunita.Web/Views/Groups/Detail.cshtml` (the **entry-point** view — the group's profile page; add the "Post to this group" link/button here; study the M2b invitation block + the "leave group" action placement so the new link slots consistently)
2. `src/Kumunita.Web/Views/Posts/Index.cshtml` (the M3 feed view, `@model FeedViewModel` — mirrors to `Views/Groups/Feed.cshtml`)
3. `src/Kumunita.Web/Views/Posts/Detail.cshtml` (the M3 detail view — the reply list **and the reply form block** both live inline on this one page; **note:** the view-area `Groups` folder already has a `Detail.cshtml` (the group's profile), so the group-post detail gets a distinct name (e.g. `Views/Groups/PostDetail.cshtml`) to avoid the collision — pin the actual name and hand off)
4. `src/Kumunita.Web/Views/Posts/New.cshtml` (the M3 composer, `@model PostComposeViewModel` — mirrors to `Views/Groups/New.cshtml`; **there is no separate M3 "replies" view** — the reply form is the one `<form>` block at the bottom of `Posts/Detail.cshtml`, so the group reply form is likewise a block inside `Views/Groups/PostDetail.cshtml`)
5. `src/Kumunita.Web/Models/GroupViewModel.cs` (the file U7 added its group-post view-model types to — the exact property names the views bind)

## Deliverables (≤ 5 files)
- `src/Kumunita.Web/Views/Groups/Feed.cshtml` (mirrors `Posts/Index.cshtml`; binds `GroupFeedViewModel`; the entry link target)
- `src/Kumunita.Web/Views/Groups/PostDetail.cshtml` (mirrors `Posts/Detail.cshtml` — the reply list **plus the inline reply `<form>` block** both live on this one page, exactly as M3; binds `GroupPostDetailViewModel`; **no** moderation actions, no "report" link — the deferred list from `docs/design/group-posts-design.md`)
- `src/Kumunita.Web/Views/Groups/New.cshtml` (mirrors `Posts/New.cshtml`; binds `GroupPostComposeViewModel`; **no** audience selector — the group's membership is the lane, not an audience choice)
- **no** `Views/Groups/Reply.cshtml` — there is no M3 equivalent; the reply form is the block inside `PostDetail.cshtml` (as M3 keeps it inside `Posts/Detail.cshtml`).
- `src/Kumunita.Web/Views/Groups/Detail.cshtml` — **small** addition: a "Post to this group" link/button near the top of the group-profile page (next to "leave group" / the member list), routing to `/groups/{id}/posts`. One link, not a new section.

## Exit
`run_build` green on `Kumunita.Web`. Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U8 — view layer` — 4 lines: (a) the 3 new view file paths + the one-line `Groups/Detail.cshtml` link + the inline reply-form block, (b) the view-model → view binding confirmation (which view-model type each view binds), (c) the **absent** moderation/report links (explicit, for U11's closeout), (d) the exact view file name chosen for the group-post detail (to avoid the `Detail.cshtml` collision). Then move this unit plan file `in-progress/group-posts-u08-plan.md` → `done/`.
