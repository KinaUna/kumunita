# U6 — Group posts: `PostService` group surface (feed + detail + create)

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
The `PostService` **group surface** — the M3 `PostService` composition pattern applied to the group lane: three new public methods + `GroupPostDraft`, all routed through the frozen `IAuthorizationService` group lane (U5) + the existing session overloads (C3), reads/writes on the store the same way M3 does. Replies reuse the existing lane-neutral `CreateReplyAsync` (G·7) — **no** reply re-implementation.

## Entry reads
1. `docs/design/group-posts-design.md` §2.2 (frozen `PostService` group surface + `GroupPostDraft` + the method doc-comments) + §2.3 (the lane-exclusivity + reply-inherits tables)
2. `src/Kumunita.Core/Posts/PostService.cs` (the **whole** file — ~500 lines; study `ListFeedAsync`/`GetPostAsync`/`CreatePostAsync` verbatim — the group-surface methods must read like siblings: same null-actor guard, same session usage, same error style, same doc-comment density; the `PostDraft` + `CreatePostAsync` gate shape is the `CreateGroupPostAsync` template)
3. `src/Kumunita.Core/Posts/Post.cs` (U4's `GroupId` add, the `Status` field to filter — `Status == PostStatus.Removed` excluded from feeds, mirroring M3b's feed filter) and `PostDraft.cs` (the `GroupPostDraft` shape to mirror)
4. `src/Kumunita.Core/Posts/FeedResult.cs` + `PostDetailResult.cs` (reuse verbatim — group feeds/details return the **same** DTOs)
5. `src/Kumunita.Core/Authorization/IAuthorizationService.cs` (U5's group-lane overloads — the exact call shapes)

## Deliverables (≤ 2 files)
- `src/Kumunita.Core/Posts/GroupPostDraft.cs` — `public sealed record GroupPostDraft(string GroupId, string? Title, string Body);` (the audience is **not** in the draft — the service writes it empty, G·8).
- `src/Kumunita.Core/Posts/PostService.cs` — append the three group-surface methods, **exact** shapes per §2.2:
  - `Task<FeedResult> ListGroupFeedAsync(string groupId, string actorId, int page)` — one `CanSeeGroupAsync(actorId, groupId)` decision (G·1): Allow ⇒ filter posts where `GroupId == groupId` (+ the M3b `Status` filter the component feeds use), `Visible = posts`, `HiddenCount = 0`, aggregate audit row (G·5, TargetKind per §2.1); Deny ⇒ `Visible = []`, `HiddenCount` per §2.1 (the deny row is the audit evidence — G·1/G·5). **No** `CanSeeAsync` audience pass — the audience lane is never evaluated on a group post (G·1).
  - `Task<PostDetailResult> GetGroupPostAsync(string groupId, string postId, string actorId)` — one `CanAsync`-shaped group decision; Allow ⇒ load the post + replies (lane-neutral `GetPostAsync`-style load, **no** second audience check — G·7); Deny ⇒ throw/403-sentinel **exactly** like M3's `GetPostAsync` deny (the controller renders 403, not a blank page).
  - `Task<Post> CreateGroupPostAsync(GroupPostDraft draft, string actorId, IDocumentSession session)` — the group-lane decision **in the caller's session** (C3); Allow ⇒ store `Post { GroupId = draft.GroupId, ComponentId = string.Empty (G·2), Audience = new Audience(AudienceMode.Any, []) (G·8 — non-null empty), AuthorId = actorId, … }`; Deny ⇒ `UnauthorizedAccessException` (G·3) — the same exception type + message style as `CreatePostAsync`'s moderator gate.
  - **Do not** touch `CreateReplyAsync` (it's already lane-neutral — G·7) or any M3/M3b method (unit-series rule 1).

## Exit
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U6
