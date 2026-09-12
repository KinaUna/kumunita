# U7 — Group posts: Web controller surface (feed / detail / new / reply)

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
The **Web channel surface** — the group-post feed/detail/composer/reply actions and view-models, mirroring `PostsController`'s M3 pattern (plain POST forms, `View` over `RedirectToRoute` where it simplifies, null-actor → `ChallengeAsync`, the moderator-off `404`/`403` handling). **No new frontend assets, no TypeScript** — the plain form POST/GET pattern from M3.

## Entry reads
1. `docs/design/group-posts-design.md` §2.3 (lane exclusivity + reply-inherits) + §2.2 (the `PostService` group surface to call)
2. `src/Kumunita.Web/Controllers/PostsController.cs` (the M3 pattern to mirror — the `Index(componentId)`/`Detail(id)`/`New()`/`Replies(id, body)`/`Report` action shapes, the null-actor handling, the `FeedViewModel`/`PostDetailViewModel`/`PostComposeViewModel` bindings; note the reply form is a plain `POST` to `{post}/replies` rendered inside `Posts/Detail.cshtml`, not a dedicated view)
3. `src/Kumunita.Web/Controllers/GroupsController.cs` (the existing `/groups/{id}` actions — **the group-post actions slot under the same route prefix**; study its null-group → 404 pattern + the M2b invitation action shapes, so the group-post actions read like siblings)
4. `src/Kumunita.Web/Models/FeedViewModel.cs` + `PostDetailViewModel.cs` + `PostComposeViewModel.cs` (the real M3 view types to mirror — `ls src/Kumunita.Web/Models/` is authoritative for names) and `src/Kumunita.Web/Models/GroupViewModel.cs` (**the existing groups view-model file — `GroupDetailViewModel`/`GroupListViewModel`/`GroupCreateModel` live here; the new group-post view-model types slot in this file**)
5. `src/Kumunita.Core/Posts/PostService.cs` — **only** the U6 group-surface method signatures (grep `ListGroupFeedAsync` / `GetGroupPostAsync` / `CreateGroupPostAsync` / `CreateReplyAsync`)

## Deliverables (≤ 3 files)
- **Into the existing `src/Kumunita.Web/Models/GroupViewModel.cs`** (it already holds `GroupDetailViewModel`/`GroupListViewModel`/`GroupCreateModel` — slot the new types in there; the point of this unit is the 3 new view-model types, not file count): `GroupFeedViewModel` (mirrors `FeedViewModel` from `Models/FeedViewModel.cs` — `Total`, `Items`, the component-name slot becomes `GroupName`), `GroupPostDetailViewModel` (mirrors `PostDetailViewModel` from `Models/PostDetailViewModel.cs` — post body + `Replies` list + reply-form target slot; **no** dedicated reply view-model, exactly as M3 renders its reply form inline on the detail page), `GroupPostComposeViewModel` (mirrors `PostComposeViewModel` from `Models/PostComposeViewModel.cs` **minus** the `Audience`/audience-picker slot — the group's membership is the audience proxy).
- `src/Kumunita.Web/Controllers/GroupsController.cs` — append the 4 group-post actions **under the existing `/groups/{id}` route** (mirroring `PostsController`'s pattern, not inventing a new verb-style):
  - `GET /groups/{id}/posts?page=N` → `ListGroupFeedAsync` → `View("Feed", GroupFeedViewModel)` (`Views/Groups/Feed.cshtml`)
  - `GET /groups/{id}/posts/{postId}` → `GetGroupPostAsync` → `View("PostDetail", GroupPostDetailViewModel)` (`Views/Groups/PostDetail.cshtml`; 403 on Deny — the M3 `Detail` deny shape)
  - `POST /groups/{id}/posts` (the composer, `GroupPostComposeViewModel`) → `CreateGroupPostAsync` → `RedirectToRoute` to the new post's detail (the M3 `New` POST shape)
  - `POST /groups/{id}/posts/{postId}/replies` → **reuse** `PostService.CreateReplyAsync(parentId, actor, body, session)`, exactly M3's `Replies(id, body)` one-field form shape (G·7 lane-inherits — the parent-group decision is already in `GetGroupPostAsync`; the reply gets `PostReply.PostId` = parent post, no group field)
  - **no** moderation/report action on group posts (deferred list, in `docs/design/group-posts-design.md` Scope Out) — do **not** add `Report` here.
  - null group ⇒ 404 **before** the service call (the M2b `FindGroupAsync` pattern — read the controller, mirror exactly).

## Exit
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U7
