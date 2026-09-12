# U7 execution plan (working) — group posts: Web controller surface (feed / detail / create / reply)

> My (unit U7's) working plan for this pass. The **authoritative spec** is
> `group-posts-u07-plan.md` (Goal / Entry reads / Deliverables / Exit) + the
> **master register** `plan-group-posts.md` (invariant G·1–G·8 pins) + the
> **design doc** `docs/design/group-posts-design.md` Part 2 **§2.2** (the three
> frozen `PostService` group-surface methods — U6 landed them) + **§2.3** (lane
> exclusivity + reply inheritance). Prior section read: **U6** (in
> `group-posts-handoff-notes.md`) — the service is ready; U7 composes the HTTP
> surface, never re-derives access (the lane owns its reads — ADR 0006-D).

## Scope (in / out)
- **In (mine):** 2 files — the closed Deliverables set (≤ 2 files, no cleanups):
  1. **`src/Kumunita.Web/Models/GroupViewModel.cs`** (append) — the three
     view-model types: `GroupFeedViewModel`, `GroupPostDetailViewModel`,
     `GroupPostComposeViewModel`. Reuse M3's `PostListItem` and `ReplyItem`
     records (same namespace `Kumunita.Web.Models`) — no new list-item types.
  2. **`src/Kumunita.Web/Controllers/GroupsController.cs`** (append) — the
     four group-post actions under the existing `/groups/{id}` route prefix:
     - `GET /groups/{id}/posts?page=N` → `ListGroupFeedAsync` → `View("Feed", …)`
     - `GET /groups/{id}/posts/{postId}` → `GetGroupPostAsync` → `View("PostDetail", …)`
     - `POST /groups/{id}/posts` → `CreateGroupPostAsync` → `Redirect` to detail
     - `POST /groups/{id}/posts/{postId}/replies` → `CreateReplyAsync` → `Redirect` to detail
     - Constructor gains `PostService posts` + `IDocumentStore store` (M3 pattern).

- **Out:** everything else — no Core change (U6 done), no Razor views (U8),
  no tests (U9), no ADR / design-doc edit, no `Report` action (deferred per
  design doc Scope Out), no moderation lane (G·4 — *unavailable*, not deferred).

## Constraints I keep pinned while I write
- **404 mapping (design doc §2.2 + master register):**
  - Detail: `Post = null` → **404** (not 403 — the design doc pins "Web 404 —
    G·3/G·4"; the master register "Non-member: 404"; the GroupsController
    "plain member's POST 404s" precedent).
  - Create: `UnauthorizedAccessException` → **404** (master register G·3:
    "Web renders 404, the GroupsController 'plain member's POST 404s'
    precedent").
  - Group missing: `GetGroupAsync(id)` returns null → **404** before any
    service call (the M2b `FindGroupAsync` pattern the unit plan names).
- **No re-derivation of access (ADR 0006-D):** the controller never calls
  `IAuthorizationService` directly — all access decisions live in the
  `PostService` group methods (U5/U6). The only extra read the controller
  makes is `GetGroupIdsAsync` for the feed's `CanPost` flag (a *display*
  convenience, not a gate — the POST gate is the authoritative deny).
- **Reply shape (G·7):** the reply route re-checks the parent's visibility
  via `GetGroupPostAsync` before calling `CreateReplyAsync` (the M3
  `Replies(id, body)` pattern — the parent's Allow is the pre-write gate).
- **No `Report` action** (design doc Scope Out; ADR 0013 deferral list).
- **No TypeScript / new frontend assets** (M3 plain-form pattern).
- **`[Authorize]`** is already at the class level (GroupsController) — the
  null-actor fallback in each action body is the fail-closed shape for a
  principal carrying no subject.

## Build gate
`dotnet build Kumunita.slnx -c Debug` — 0 errors, 0 warnings (the touched
project is `Kumunita.Web`).
