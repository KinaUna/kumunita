# M3b U8 — "Report this" resident-facing action (execution plan)

## Understanding

Wire the M3b **`FileReportAsync`** intake lane (Core, U4) into the Web surface
so a resident can file a report against a post they can see. This is the plan's
`### U8 — "Report this" resident-facing action` (plan-m3b-moderation.md line
260–271). It is M3b deferral item 1's **resident-facing surface** — the read
branch (F2), the `/moderation` queue (U7), and the seam tests (U9) are other
units' scope; U8 is only the *filing* action + the form that posts to it.

## Entry reads (confirmed)

- `src/Kumunita.Web/Views/Posts/Detail.cshtml` — the existing detail surface.
  Has the reply form (posts `body` to `/posts/{id}/replies`), the `TempData`
  info/error flash block (added by U6), and `postId` scoped local.
- `src/Kumunita.Web/Controllers/PostsController.cs` — the M3 thin-controller
  (ADR 0006-D). Key precedents:
  - `Replies` `[HttpPost("/posts/{id}/replies")]` (U6) — the closest
    analog: guard order (empty id → `NotFound()`; no subject → `Forbid()`),
    re-run the parent's `Read` decision via `posts.GetPostAsync(id, actor)`
    before writing (`Post = null` → `Forbid()`), then open
    `store.LightweightSession()` and delegate to the Core write lane.
  - `New()` `[HttpPost("/posts/new")]` — `[ValidateAntiForgeryToken]`,
    the temp-data-then-redirect write-lane shape.
- `docs/design/m3b-moderation.md` Part 1 C-M3b·1 + Part 2 (U2/U4 pins):
  `FileReportAsync` is a **resident-facing intake** action — the Core lane
  **makes no `IAuthorizationService.CanAsync` call** (it is a write seam, not
  an access decision, analogous to M1 `UpsertProfileAsync`). The filing
  `AccessAudit` row carries the pinned filing tag `AccessVia.Admin` (NOT
  `Via = Report` [reserved for the read branch, C-M3b·2], NOT `Via = Owner`
  [C1 owner-branch]); `Report.Status = "filed"`.
- `src/Kumunita.Core/Moderation/ModerationService.cs` (U4) — the frozen
  signature: `Task<int> FileReportAsync(string postId, string actorId,
  string? reason, IDocumentSession session)`. The service owns its own
  `SaveChangesAsync` (C3 same-transaction; the `Report` row + `AccessAudit`
  row commit atomically).

## Assumptions

- **Who may file:** per C-M3b·1, a resident who can currently *see* the post
  may file a report. U8 enforces that precondition **at the Web layer** by
  re-running the post's `Read` decision via `PostService.GetPostAsync(id,
  actor)` (the exact U6 reply-lane precedent) — a `Post = null` result
  ("missing" or "denied", Core doesn't distinguish) maps to `Forbid()`. This
  keeps the Core lane's "no `CanAsync` call" pin intact: the *intake lane*
  makes no decision; the *Web layer* gates on the existing read decision.
- **Route:** `POST /posts/{id}/report` — distinct from `/posts/{id}/replies`
  (U6) and `/posts/new` (M3). Action name `Report`.
- **Reason:** optional (`string?`). `FileReportAsync` accepts `null`; the
  form provides an optional textarea but submission is valid without it
  (unlike the reply lane, which requires `body`).
- **DI:** `ModerationService` is already registered (transient,
  `src/Kumunita.Core/DependencyInjection.cs:75`); inject it into
  `PostsController`'s primary-constructor parameter list.
- **No Core seam / seam-test change:** U8 adds a Web action + a view form
  only. No new Core method, no new seam-test name (U9 owns the tests; the
  §2.5 list pins the file/assign/unlock/resolve + Via=Report + SoD lanes, not
  a Web-route test — consistent with U6, which added 0 tests).

## Approach

Two files (the pinned U8 "2 files" deliverable):

1. **`src/Kumunita.Web/Controllers/PostsController.cs`** (modify):
   - Add `ModerationService moderation` to the controller's primary
     constructor parameters (after `IDocumentStore store`).
   - Add a thin `[HttpPost("/posts/{id}/report")]` `Report` action mirroring
     the `Replies` action's guard order, the pre-write `GetPostAsync` read
     decision, the C3 session ownership, and the redirect:
     - empty id → `NotFound()`; no subject → `Forbid()`
     - `posts.GetPostAsync(id, actor)` → `Post = null` → `Forbid()`
     - `await using var session = store.LightweightSession();`
       `await moderation.FileReportAsync(id, actor, reason, session);`
     - `TempData["info"] = "Report submitted. A moderator may review it.";`
       `Return Redirect($"/posts/{id}");`
   - `[ValidateAntiForgeryToken]` on the action (the `New()` precedent; the
     form renders `@Html.AntiForgeryToken()`).

2. **`src/Kumunita.Web/Views/Posts/Detail.cshtml`** (modify):
   - Add a small "Report this post" form below the post card (above the
     Replies section): optional `reason` textarea + submit button, posting to
     `/posts/{id}/report`, with `@Html.AntiForgeryToken()`. Keep it minimal —
     no new model file, `[FromForm] string? reason` binds the field (same
     pattern as the existing reply form's `[FromForm] string? body`).

## Steps

1. Create `docs/plans-milestones/m3b-u8-plan.md` (this file) — done.
2. Modify `PostsController.cs` — inject `ModerationService`, add the `Report`
   `[HttpPost("/posts/{id}/report")]` action.
3. Modify `Views/Posts/Detail.cshtml` — add the "Report this post" form.
4. `run_build` on `Kumunita.Web` — confirm green (Exit criterion).
5. Append the `## U8` section to `docs/plans-milestones/m3b-handoff-notes.md`.

## Risks / open questions

- `FileReportAsync` throws `KeyNotFoundException` if the post is missing from
  the session. U8 pre-guards with `GetPostAsync` (`Post = null` → `Forbid()`
  before any session), so the only live case at the lane is a genuinely
  existing-but-unseen post, which is already denied. No new error surface to
  map (a resident can only report a post they can see).
- No route-conflict: `/posts/{id}/report` vs. `/posts/{id}/replies` vs.
  `/posts/new` are distinct literal shapes.
