# U6 — Reply route micro-fix (`POST /posts/{id}/replies`) — execution plan

> Sealed unit U6 of M3b (plan § U6). **Web-only micro-fix**: close the M3 404
> by wiring the *existing* M3 U6 seam `PostService.CreateReplyAsync` to a
> controller action. **No new Core seam, no new seam-test name** (§2.5 test-14
> anchors the delegation/absence pin). Deliberate deviation from the
> illustrative §2.2.4 pseudocode, reconciled to the real frozen seam + the
> file's established M3 U7 precedent.

## Understanding
M3 shipped a working detail + one-level-reply *read* (`GET /posts/{id}`) and a
compose route, but the reply *write* route `POST /posts/{id}/replies` was a
404 (M3 deferral item 5). The Core write lane already exists and is frozen
(`PostService.CreateReplyAsync`). U6's job is to add the thin Web
`[HttpPost]` action and verify the existing form wiring — mirroring M3's
`New()` write-lane precedent in the same file.

## Assumptions / reconciliation with §2.2.4
- **Post id is `string`, not `long`.** `CreateReplyAsync(string postId, ...)`
  and `PostReply.Id` (`Guid.NewGuid().ToString("N")`) are strings. §2.2.4's
  pseudocode `[FromRoute] long id` is illustrative and would 404 on a string
  id — I use `[FromRoute] string id`.
- **The service commits its own write.** `CreateReplyAsync` calls
  `session.SaveChangesAsync()` internally (C3 same-transaction lane). The §2.2.4
  pseudocode's extra `await session.SaveChangesAsync()` would be a redundant
  second save; I omit it (matches M3).
- **Body comes as `[FromForm]`, not `[FromBody] ReplyForm`.** The existing
  `Detail.cshtml` form is a real `method="post"` form posting a `body` field with
  `@Html.AntiForgeryToken()`. `[FromBody]` + a nonexistent `ReplyForm` model is
  illustrative; a `[FromForm] string? body` binds the existing form with no new
  model file.
- **Authz shape.** The plan ("authz check via the post's existing `Read`
  decision") is satisfied by reusing the parent's single `Read` decision via
  `PostService.GetPostAsync` (fail-closed 403 when missing/denied — same shape as
  the `Detail` GET) before opening the write session. This prevents writing a
  reply against a post the actor can't see (or that doesn't exist) and reuses
  `CreateReplyAsync` verbatim without reshaping it.
- **Two-file deliverable preserved** (controller + view) — the view change is a
  minimal, safe hardening (anti-forgery token on the form + optional redirect
  flash) rather than a new model file, staying within the pinned "2 files"
  deliverable.

## Key files
- `src/Kumunita.Web/Controllers/PostsController.cs` — add the `[HttpPost("/posts/{id}/replies")]` action.
- `src/Kumunita.Web/Views/Posts/Detail.cshtml` — keep the form action; add `@Html.AntiForgeryToken()` + a `TempData["info"]` flash on the action's redirect (read-only form already posts `/posts/{id}/replies`; no action-string change needed).

## Steps
1. Add the `Replies` `[HttpPost("/posts/{id}/replies")]` action in `PostsController.cs` (thin: `GetPostAsync` authz guard → `LightweightSession()` → `CreateReplyAsync(id, actor, body, session)` → `TempData` + `Redirect("/posts/{id}")`).
2. Confirm/fix the `Detail.cshtml` form (action string, anti-forgery token, body field) so it posts to the new route.
3. `run_build` green on `Kumunita.Web` (verify compilation + no route/shape errors).
4. Append the `## U6` handoff section to `docs/plans-milestones/m3b-handoff-notes.md`.
