# Group posts — rolling handoff notes (appended, never rewritten)

> One section per unit, **appended** (never rewritten). Each unit writes exactly
> one short section before it exits; the next unit reads only *that* section +
> its own entry-read list. Created by **U1**.

## U1 — design doc Part 1

- Authored `docs/design/group-posts-design.md` **Part 1**: **Context**,
  **Scope** (in / named deferral list / privacy-first "not available"),
  **Invariants G·1–G·8** (8), **FACES G1–G13** (13), and the drift-guard (Part
  1). No code, no build. The Part 2 seams / 19 test names / gate are **U2**.
- Invariants pinned (each verbatim from the master register, each with a
  pin-note): **G·1** membership-only = the single access decision (audience never
  evaluated) · **G·2** lane exclusivity (`GroupId ≠ ""` ⇒ `ComponentId` empty,
  excluded from `ListFeedAsync`/`ListAllFeedAsync`) · **G·3** members-only
  authoring (create gate **is** the group-lane decision) · **G·4** nobody peeks
  (no moderator, no break-glass) · **G·5** audit per lane (feed = aggregate row,
  detail = decision row, in-transaction) · **G·6** delegation action-scoped ·
  **G·7** replies inherit the parent's single group-lane decision · **G·8**
  group-post `Audience` written non-null and **empty**.
- **Break-glass is not a deferral** — it is an explicit "not available" rule
  (G·4). **U3 (ADR 0013) / U5 (the lane) do NOT re-litigate it**: the answer is
  *no by design* (how-it-works.md trust pitch, ADR 0003 default-OFF, invariant
  C5). Do not put break-glass in any deferral list.
- Handing forward to **U2**: **FACES count = 13** (G1–G13). U2 pins the **19**
  seam-test names (Part 2 §2.5) and the three-test gate (§2.4) against these
  invariants; the invariant/FACES numbers are frozen for the rest of the
  milestone.
- `Post.GroupId` is the **single additive** on the `Post` POCO (ADR 0004 §B.1,
  the M3b `Status` ADD precedent) — **no `M3DocTypes` change**, **no** second
  doc surface; `PostReply` is unchanged. `AccessVia.Group` (8th value) + the two
  `CanSeeGroupAsync` overloads are the group lane's ADDs on
  `IAuthorizationService` (frozen signatures untouched — ADR 0006-E). The
  "## Group posts — Closed (recorded)" section is still an empty **placeholder**.

## U2 — design doc Part 2 (seams, 19 test names, gate, drift guard)

- Appended **`## Seams & contracts (Part 2, written by U2)`** to
  `docs/design/group-posts-design.md`: §2.1 the frozen seam list (the exact
  four-method group-lane ADD + the frozen decision shape + the two row
  shapes), §2.2 the new/changed Core types (`AccessVia.Group` 8th value,
  `Post.GroupId` single additive, `GroupPostDraft`, three `PostService`
  group methods — ctor unchanged, no new `PostService` dependency),
  §2.3 the two rule tables (lane split + reply inheritance), §2.4 the
  three-test gate (closed loop / handoff / part-vs-whole) with the reliable
  runner path pinned (AGENTS.md quirk), §2.5 the **19** test names **verbatim**
  (master verified 1:1 — no renames, count 19 ✓) in
  `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs`, §2.6 the
  module-boundary impact notes (UserInfo **unchanged**; Web calls
  `PostService` only), §2.7 the drift guard + the U4–U12 ADD set + a
  freeze-at-a-glance count table. **No code, no build.**
- **U2 — contract adjustments** (both against the master's *directional*
  draft; both are **part of the freeze** under §2.7 — a later unit "fixing
  them back to the draft" is a drift pause, not a fix):
  - **U2-A1 — the feed pair.** The draft's two single-target
    `CanSeeGroupAsync` overloads cannot produce the **aggregate** row G1
    FACES + test #16 require (`AccessAudit` has exactly two row shapes —
    `TargetId` or counts — `AccessAudit.cs`). Frozen:
    `CanSeeGroupFeedAsync(actorId, groupId, candidateCount[, session])` —
    the M1 single↔bulk pair precedent (`CanAsync` ↔ `CanSeeAsync`).
  - **U2-A2 — `targetPostId`.** Test #17 requires the detail row's
    `TargetId =` **the post id**; the draft's two parameters carry no post
    id. Frozen: `string? targetPostId`; row `TargetId = targetPostId ??
    groupId` (detail ⇒ post id; gate ⇒ group id).
  - *(The deny-Via is **pinned, not adjusted**: plain non-member ⇒ `Via = Group`;
    a grant case ⇒ `Via = Delegation` — M1's `denyVia` pattern
    (`AuthorizationService.Decide`/`ResolveActorAsync`) with the group
    lane's own-standing value `Group`. And the gate's Deny row is persisted
    by a `SaveChangesAsync()` **before** the `UnauthorizedAccessException`
    throw — G6 FACES' "exception **and** a Deny row" — so it survives the
    caller's rejection.)*
- **G12/G13 are structural, not filter-based:** both M3 feed queries already
  filter on `ComponentId`, and group posts write it empty (G·2) — **U6 adds
  no filter**; the two tests pin the outcome (recorded in §2.3(a)).
- **U2 note (doc hygiene):** the `## Group posts — Closed` heading that
  Part 1's drift guard references **did not exist** in the file; I added the
  empty placeholder at the file tail for U10/U11/U12 to fill at close.
- **Handing to U3 (ADR 0013):** the lane's full contract is Part 2
  §2.1–§2.3 — the ADR points **at** it (don't re-derive), keeps break-glass
  **unavailable, not deferral** (Part 1's flag). ADR 0006-E's "named here"
  list grows by: **4 methods** (not the draft's 2) + the `AccessVia.Group`
  value + the `Post.GroupId` additive + the `GroupPostDraft` record (type,
  not seam).
- U2's plan file moves to `done/` immediately after this note (per the
  workflow) — a plain file move: **nothing is staged or committed; the user
  reviews first.**
## U8 — view layer (feed / detail / new + Detail entry link)

- **Deliverables (the closed set, 3 new + 1 modified):**
  - `src/Kumunita.Web/Views/Groups/Feed.cshtml` — the channel feed; binds
    `GroupFeedViewModel`. Inline composer at the top (offered when
    `CanPost`), the item list (rows link to
    `/groups/{GroupId}/posts/{Id}`), the hidden-count hint (`Total` >
    `Items.Count`). **No** component pills, no community management / leave
    buttons, no audience picker — all M3-only surfaces, absent by the
    group lane's shape (G·8).
  - `src/Kumunita.Web/Views/Groups/PostDetail.cshtml` — the group-post
    detail; binds `GroupPostDetailViewModel`. Post card + reply list + the
    **inline reply `<form>` block** at the bottom (exactly M3's
    `Posts/Detail.cshtml` shape — one `body` field POSTing to
    `/groups/{GroupId}/posts/{Post.Id}/replies`; helper text uses the
    group-lane "reply-inherits" framing, G·7).
  - `src/Kumunita.Web/Views/Groups/New.cshtml` — the standalone composer;
    binds `GroupPostComposeViewModel` (title + body, `IsValid` re-check is
    the controller's, not the view's). Rendered only by U7's failure
    re-render path (`View("New", model)`). **Drift pause (recorded, not a
    seam):** U7's controller has no `GET /groups/{id}/posts/new` route —
    the `New` view's `action` and back-link recover the group id from
    `ViewContext.RouteData.Values["id"]` (the current route is
    `/groups/{id}/posts`, the form posts to it — the `{id}` is always
    present). U11/U12's docs close should note the group lane has no GET
    composer route (the inline composer on `Feed` is the primary surface,
    the standalone `New` is the failure re-render only).
  - `src/Kumunita.Web/Views/Groups/Detail.cshtml` — **one** link added, in
    the header block right after the Private badge:
    `<a class="btn btn-outline-primary btn-sm"
       href="/groups/{Model.GroupId}/posts">Post to this group</a>`
    (routed via `Url.Action("GroupPosts", "Groups", …)`). Every viewer who
    reaches `Detail` is already a member (the owner ∪ member gate is the
    controller's), so the link is offered unconditionally — composer
    visibility on the feed itself is the `CanPost` flag's call. One link,
    not a new section (the unit plan's "one link, not a new section" pin).
- **Binding confirmation (view-model → view):** `Feed.cshtml` →
  `GroupFeedViewModel` (`GroupId` / `GroupName` / `Items: List<PostListItem>`
  / `Total` / `CanPost`) · `PostDetail.cshtml` → `GroupPostDetailViewModel`
  (`GroupId` / `Post` / `AuthorDisplayName` / `AuthorSubjectId` /
  `Replies: List<ReplyItem>` / `IsAuthor`) · `New.cshtml` →
  `GroupPostComposeViewModel` (`Title` / `Body`). The rows reuse U7's
  M3-reused `PostListItem` / `ReplyItem` records verbatim (no re-invention).
  The M3 component/audience slots on `PostListItem` (`ComponentName` /
  `ComponentId`) are rendered **neither linked nor shown** in
  `Feed.cshtml` — a group post's `ComponentId` is empty (G·2) and the feed
  is group-scoped, so there is nothing to say.
- **Absent (explicit, for U11's closeout):** `PostDetail.cshtml` has **no**
  "Report this post" form, **no** "Hide" / "Remove" / moderation buttons of
  any kind, and **no** "Write a post" CTA (the composer is the inline block
  on `Feed.cshtml`). `New.cshtml` has **no** component picker, **no**
  audience editor, **no** grant-picker partial — the M3 audience card and
  `@section Scripts { <partial name="_GrantPickerScripts" /> }` are both
  **absent** (G·8: the service writes `Audience` non-null and empty
  regardless of anything on the form). No new CSS / TS / npm assets — the
  plain-form + plain-list pattern from M3, exactly the unit plan's "no new
  frontend assets" pin.
- **The group-post detail's chosen view file name:** `PostDetail.cshtml`
  (not `Detail.cshtml`) — the existing `Views/Groups/Detail.cshtml` is the
  M2 group-profile page, so the group-post detail gets the distinct name to
  avoid the collision, exactly the unit plan's pin. `GroupPostDetail`
  (U7's controller action) emits `View("PostDetail", …)` → resolves to
  `Views/Groups/PostDetail.cshtml` under the default view-name resolution.
- **Verified:** `dotnet build Kumunita.slnx -c Debug` — **Build
  succeeded** in 8.3 s, all four projects clean (the touched project is
  `Kumunita.Web`); `dotnet exec Kumunita.Web.Tests.dll` → **Total: 90,
  Errors: 0, Failed: 0, Skipped: 0** — no M2/m2b/M3 regression (nothing in
  the test harnesses touches the new views directly, and the M3 routes are
  untouched).
- **Handing to U9 (seam tests):** the 19 pinned names live in
  `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs` (design doc Part 2
  §2.5). The U7/U8 Web surface is presentation-only (U7's handoff pin: the
  controller "should not re-implement membership checks"; every decision
  is inside the U6 service calls) — U9's tests run against `PostService`'s
  group surface (the U6 handoff's three signatures), not against the views.
  The 404 mapping (U7's `NotFound()` on both the detail Deny and the
  create gate's `UnauthorizedAccessException`) is the Web-side
  fail-closed shape; U9's #6/#7/#8 tests are Core-side (the service
  throws / returns `Post = null`) — the two are consistent, not
  duplicates.
- U8's plan file moves to `done/` immediately after this note (per the
  workflow) — a plain file move: **nothing is staged or committed; the user
  reviews first.**
## U3 — ADR 0013 (group posts are a membership lane)

- Authored `docs/adr/0013-group-posts-membership-lane.md` (**Accepted,
  2026-09-12**, the 0010 shape — Context / Decision / Consequences). The ADR
  **points at** the design doc Part 2 §2.1–§2.2 for the frozen C# instead of
  re-deriving it. `docs/adr/README.md` register grew exactly one 0013 line
  (next to 0012). **No code, no build, no `.cs` touched.**
- The ADR states the ADD set as **the compatible ADR 0006-E ADD**: the
  **4** group-lane methods (the `CanSeeGroupAsync` pair with `targetPostId`
  + the `CanSeeGroupFeedAsync` pair — per **U2-A1/U2-A2**, *not* the
  register's 2-overload draft) + `AccessVia.Group` (8th value, appended
  after `Admin`; frozen `CanAsync`/`CanSeeAsync` byte-identical).
  `Post.GroupId` stays the **single additive** (ADR 0004 §B.1, the M3b
  `Status` precedent — no `M3DocTypes` change, no new doc surface);
  `PostReply` / `PostService` ctor **unchanged** (ADR 0006-D: the lane owns
  its reads).
- **Break-glass, moderator peek, non-member authoring** are recorded in the
  ADR as **standing "not available" (G·4)** — deliberately **not** on the
  named deferral list (that list is the 5 design-doc Scope items:
  moderation/report, notifications, search, cross-posting, pagination UI).
  Re-litigating any of them requires an **ADR 0013 amendment** per Part 1's
  drift-guard.
- **Handing to U4 (`Post.GroupId` additive):** the ADR's Decision bullets
  "The data is one additive, not a new doc" and "Lanes are exclusive" are
  the standing rules U4's single-field change must satisfy — the field is
  written **only** by `CreateGroupPostAsync` (U6) with
  `ComponentId = string.Empty` + `Audience = new Audience()` (G·2/G·8), and
  **no filter** is added to the M3 feeds (G12/G13 hold structurally — design
  doc §2.3(a)).
- U3's plan file moves to `done/` immediately after this note (per the
  workflow) — a plain file move: **nothing is staged or committed; the user
  reviews first.**

## U4 — Post.GroupId additive (ADR 0004 §B.1)

- Added **exactly one member** to `src/Kumunita.Core/Posts/Post.cs`, after
  M3b's `Status`: `public string GroupId { get; set; } = string.Empty;` —
  non-nullable `string`, default `string.Empty`, member name `GroupId`: the
  **exact §2.2 pin**. All other POCO members + `PostStatus` byte-untouched;
  `PostReply` untouched. Mirror of the M3b ADD block style (marker line +
  pinned doc-comment). One file, one field — the entry plan's Deliverables.
- The doc-comment pins (from §2.2 + the entry plan): **G·2** lane
  exclusivity (`non-empty ⇒ group-lane post`, `ComponentId` **empty**,
  structurally absent from `ListFeedAsync` / `ListAllFeedAsync` — §2.3(a)),
  **G·1/G·8** (membership is the **sole** access decision; the audience lane
  is never evaluated, audience written non-null **empty**), **G·3/G·4**
  (only members may create or see it), empty ⇒ component post (M3/M3b)
  unchanged, **ADR 0013**, and the ADR 0004 §B.1 additive precedent (the
  M3b `Status` ADD).
- **No `M3DocTypes.cs` change** (verified read-only — the registration
  surface is `opts.Schema.For<Post>()` at `M3DocTypes.cs:26`; that file is
  the one to cite, not modify): Marten's delta picks the new property up —
  delta-detected, idempotent, **no re-seed**, exactly M3b's `PostStatus`
  ADD lane, no second doc surface.
- **Build green:** `Kumunita.Core` builds clean (the runner path AGENTS.md
  pins). No new doc-comment warnings.
- **Cref adaptation (recorded — not a drift pause):** the pinned
  §2.2 comment names the writer `PostService.CreateGroupPostAsync`, which
  does not exist yet (U6's frozen ADD — unit rule 4 forbids *me*
  introducing it). I wrote that one reference as a `<c>` literal instead of
  a `<see cref>` so the build stays green **now**; when U6 lands the
  member, upgrade it to a cref. Every other cref
  (`ListFeedAsync` / `ListAllFeedAsync` / `ComponentId`) resolves today.
- **Handing to U5 (the authorization group lane):** the lane's full C#
  contract is design doc Part 2 **§2.1** (the **4** group-lane methods —
  the `CanSeeGroupAsync` pair with `targetPostId` + the
  `CanSeeGroupFeedAsync` pair per U2-A1/A2; the frozen decision algorithm
  and row shapes) — U5 implements **exactly that**: `AccessVia.Group`
  appended **after** `Admin` (8th value, the M1 Admin-APPEND precedent; that
  enum lives in `Authorization/Decision.cs`, **not** `Post.cs` — U5 does
  not need `Post.cs` at all) + the 4 methods on
  `IAuthorizationService` (frozen signatures untouched). The lane decides
  over `Post.GroupId` (now in place) via the live
  `IUserInfoService.GetGroupIdsAsync` read (the lane owns its reads —
  ADR 0006-D); **no** moderator / break-glass branch (G·4 — standing "not
     available", not a deferral).
  - U4's plan file moves to `done/` immediately after this note (per the
     workflow) — a plain file move: **nothing is staged or committed; the user
     reviews first.**

  ## U5 — Authorization group lane (`AccessVia.Group` + `CanSeeGroupAsync`/`CanSeeGroupFeedAsync`)

  - Implemented the **entire** frozen group-lane ADD (design doc Part 2
    **§2.1**, per **U2-A1/A2** — the **four** methods, not the register's
    2-overload draft) in **three** files, all in
    `src/Kumunita.Core/Authorization/` (the touched project is
    `Kumunita.Core`):
    - **`Decision.cs`** — appended **`AccessVia.Group`** as the **8th** enum
      value, **after `Admin`** (the M1 `Admin`-value precedent: an additive
      enum value, the seven frozen values byte-untouched), with the pinned
      doc-comment (group-lane standing, ADR 0013, group posts milestone).
      `AccessOutcome`, `Decision`, `VisibleSet` untouched.
    - **`IAuthorizationService.cs`** — appended the **four exact** §2.1
      signatures, parameter names/positions verbatim: the
      `CanSeeGroupAsync(actorId, groupId, string? targetPostId)` pair (with the
      `IDocumentSession` session overload) + the
      `CanSeeGroupFeedAsync(actorId, groupId, int candidateCount)` pair (with
      the `IDocumentSession` session overload), each with a `<summary>`
      mirroring the frozen overloads' style (G·1/G·4/G·5, C4, C3). The **four
      frozen signatures are byte-untouched** (ADR 0006-E lane; `CanAsync`/
      `CanSeeAsync` ADDs unchanged).
    - **`AuthorizationService.cs`** — implemented all four plus a shared
      private core + two row builders; the frozen methods
      (`CanAsync`/`CanSeeAsync` public + session overloads, `Decide`,
      `DecideAsync`, `CanSeeInternalAsync`, `EvaluateAudience`,
      `ResolveActorAsync`, `HasBreakGlassAsync`, `ActorContext`) are
      byte-untouched.
  - **The core** (`DecideGroupAsync`) follows the §2.1 frozen algorithm
    exactly:
    - Delegation (G·6, C2): `GetActiveGrantAsync(actorId)`; an **in-scope
      `read`** grant (`grant.Scope.Contains(AccessAction.Read.Id)`, i.e.
      contains `"read"`) ⇒ **`principal = grant.OwnerId`**, `isDelegated =
      true`; **out-of-scope** (or no grant) ⇒ `principal = actorId`,
      `isDelegated = (grant is not null)`.
    - Membership (G·1, C4): **live**
      `GetGroupIdsAsync(principal)` — the **principal's** groups, not a
      projection (strong consistency). `allowed = groupIds.Contains(groupId)`.
    - `via = isDelegated ? AccessVia.Delegation : AccessVia.Group` (the Allow
      **and** Deny deny-Via pin — `Group` where the frozen M1 lanes use
      `Audience`); `return Decision(allowed, via, principal)`.
    - **Absent by contract (G·4/G·1, test #19):** the lane touches **no**
      `HasBreakGlassAsync` / `AdminOverride`, **no** `ModeratorAssignment` /
      `Component.ModeratorAccess`, **no** `EvaluateAudience`, and never takes
      `AccessAction.Moderate`. The action on **every** group-lane row is
      `"read"`.
  - **The rows** (C3; the only two shapes that exist — `AccessAudit.cs`):
    - **decision shape** (`CanSeeGroupAsync` pair): `TargetKind "grouppost"`,
      **`TargetId = targetPostId ?? groupId`** (detail ⇒ the post id;
      create-gate ⇒ the group id), counts **null**, `Via` Group / Delegation,
      `Outcome` per membership, `Action "read"`, `ActorId` the actor,
      `EffectivePrincipalId` the principal (owner when delegated),
      `Id Guid.NewGuid().ToString("N")`, `At` UtcNow.
    - **aggregate shape** (`CanSeeGroupFeedAsync` pair): `TargetKind
      "grouppost"`, **`TargetId = null`**, **`VisibleCount`/`HiddenCount`** =
      `(candidateCount, 0)` on Allow / `(0, candidateCount)` on Deny (G·5,
      the C-M3·3 analog) — the channel is all-or-nothing.
  - **Transaction shape (C3), exactly the frozen lane's two-form precedent:**
    the standalone (non-session) overloads `store.OpenSession` → decide →
    `Store` → `SaveChangesAsync` in one commit (the M1 `CanAsync` standalone
    lane); the `IDocumentSession` overloads `Store` the row into the
    **caller's** in-flight transaction and leave commit to the caller (the
    ADR 0006-E compatible lane). This is the lane U6's `CreateGroupPostAsync`
    G·3 create-gate uses to commit the Deny row **before** the
    `UnauthorizedAccessException` (so the row survives — G6 FACES) and the
    Allow row + post in one atomic `SaveChangesAsync`.
  - **Build green:** `dotnet build Kumunita.slnx -c Debug` — **Build
    succeeded, 0 Warning(s), 0 Error(s)**, `Kumunita.Core` clean. **No
    regression:** the reliable runner (`dotnet exec
    Kumunita.Core.Tests.dll`) → **Total: 235, Errors: 0, Failed: 0, Skipped:
    0, Not Run: 0** — every frozen M1/M3b/C2/C3/C4/C5/C6 lane still passes.
  - **Cref note (adaptation, **not** a drift pause):** the group-lane
    interface/impl doc-comments name **`PostService.CreateGroupPostAsync`**
    (U6's frozen ADD — unit rule 4 forbids **me** creating it) and
    **`ListGroupFeedAsync`** (U6) as **plain text**, never as
    `<see cref>`/`<c>`-cref, so the build is green **now** without forward
    references (the U4 case, avoided rather than introduced). Every other
    cref in the change (`AccessVia.Group`, `AccessAction.Read`,
    `IUserInfoService.GetGroupIdsAsync`/`GetActiveGrantAsync`,
    `AccessAudit`, `Decision`) resolves today.
  - **Handing to U6 (`PostService` group surface):** the lane is **live** —
    implement the **§2.2** three frozen methods against it. Key hand-offs:
    (1) the create gate **is** a group-lane call — use `CanSeeGroupAsync` with
    `targetPostId: null` (⇒ the row's `TargetId = groupId`, the channel as
    the gate's target) and take the **`IDocumentSession` overload** so the
    Deny row + throw and the Allow row + post commit **in the caller's
    transaction**; deny ⇒ throw `UnauthorizedAccessException` **after** the
    row is stored (G·3/G6). (2) feed/detail: `CanSeeGroupFeedAsync`
    (aggregate row) for the feed, `CanSeeGroupAsync` with **`targetPostId =`
    the post id** for the detail — the row's `TargetId` becomes the post id
    (tests #17/#18). (3) `GroupPostDraft` writes **`GroupId` non-empty**,
    **`ComponentId = string.Empty`**, **`Audience = new Audience()`** (G·2/G·8) —
    written **only** by `CreateGroupPostAsync`. (4) **No** moderator/break-glass
    branch exists on this lane to reach (G·4) — the group surface cannot
    grant access any other way.
  - U5's plan file moves to `done/` immediately after this note (per the
    workflow) — a plain file move: **nothing is staged or committed; the user
    reviews first.**

## U6 — `PostService` group surface (ADR 0013 `G-6` / FACES `G1`–`G13`: feed / detail / create)

### Deliverables (as stated in the unit plan)

`PostService` group surface — feed / detail / create (+ `GroupPostDraft`). Two
files only, exactly the two in the plan:

- **`src/Kumunita.Core/Posts/GroupPostDraft.cs`** (new) — the group draft record.
- **`src/Kumunita.Core/Posts/PostService.cs`** (appended) — the three group-lane
  methods, added after `RemovePostAsync`, in the order **feed → detail → create**
  (§2.1).

### What I read and relied on

The U6 unit plan (`group-posts-u06-plan.md`); design §2.1/§2.2/§2.3 (the literal
contract — it wins over the plan's own prose); U5's "U5 → U6" hand-off (the frozen
group-lane signatures + the create-gate `IDocumentSession`-overload instruction);
`PostService.cs` in full (the M3 idioms to mirror); `Post.cs` / `PostDraft.cs` /
`Audience.cs` / `FeedResult.cs` / `PostDetailResult.cs` (the shapes); and
`IAuthorizationService.cs` to confirm the exact U5-landed signatures I call.

### How I implemented it (the two new files, and the one call-shape each method uses)

1. **`GroupPostDraft`** — `public sealed record GroupPostDraft(string GroupId,
   string? Title, string Body);`. **No `Audience` member** (G·8): this draft is the
   group lane's *only* audience input, and it carries none. The doc-comment says the
   record "carries **no audience of any kind**" and cites G·8 / ADR 0013 `G-6`.
   (The exact §2.1 shape, minus the explanatory comment.)

2. **`ListGroupFeedAsync`** (G-6 / G7 / FACES G1) — mirror of `ListFeedAsync` with
   the membership test swapped for the group-lane aggregate gate:
   `CanSeeGroupFeedAsync(actorId, groupId, candidates.Count)`. Allow ⇒
   `new FeedResult(candidates, 0, HiddenCount)`; Deny ⇒ `new FeedResult([], 0,
   HiddenCount)` (empty list + the aggregate hidden count; **no**
   `UnauthorizedAccessException`). Zero candidate rows ⇒ the empty feed with **no**
   authorization row (the method returns before the lane call). Loads via
   `await using var session = _store.QuerySession();`.

3. **`GetGroupPostAsync`** (G-6 / FACES G3) — mirror of `GetPostAsync`, group-lane
   `CanSeeGroupAsync(actorId, groupId, post.Id, session)` (the post id is the lane's
   `targetPostId`). Load-by-id, then **fail-closed** on: `post is null`,
   `string.IsNullOrEmpty(post.GroupId)` (a plain post reached via the group surface),
   or `post.GroupId != groupId` (route mismatch) — each throws
   `KeyNotFoundException`. On Deny ⇒ `KeyNotFoundException` (**not**
   `UnauthorizedAccessException`, per §2.2). Replies returned **as-is** (G·7).

4. **`CreateGroupPostAsync`** (G-6 / G8 / FACES G11) — the group-lane create.
   Mirrors `CreatePostAsync`'s shape but routes the membership test through the
   **group-lane `IDocumentSession` overload** `CanSeeGroupAsync(actorId,
   draft.GroupId, null, session)` — so the group-lane audit row and the post
   **commit in the same transaction** (U5's "U5 → U6" instruction / G·6;
   `targetPostId: null` = the row's `TargetId` is the **channel**, the *write
   gate*). Deny ⇒ `await session.SaveChangesAsync(ct);` **then** `throw new
   UnauthorizedAccessException(...)` (the group-lane row is flushed **before** the
   throw — G·3 / G6). Allow ⇒ persist a `Post` with **`ComponentId = string.Empty`**,
   **`GroupId = draft.GroupId`**, **`Audience = new Audience()`** (the non-null,
   empty shape — `Kumunita.Core.Authorization.Audience`, the **only** audience write
   on this lane; G·2 / G·8), then `await session.SaveChangesAsync(ct);`. No audience
   resolution, no grant reads (U1's, absent by construction here); no moderator /
   break-glass branch (G·4 — `CanWriteToGroupChannelAsync` is the **M4** lane §2.4
   explicitly does not define).

### What I deliberately did NOT touch

- **Replies on group threads:** G·7 says "no extra gate" — I return the thread's
  `Replies` **as-is** (same as `GetPostAsync`). The lane call I *do* make (`post`
  visible ⇒ caller is a reader of the group) is the design's intent; I did **not**
  add a per-reply gate (there is no `CanSeeGroupReplyAsync` in the frozen surface).
  If U9's seam tests expect a **separately** per-reply gate, flag it — that belongs
  to a follow-up, not U6. (See "What I deliberately did not do", item below.)
- **The `Post.cs` §2.3 line:** `Post.cs` line 61 still reads
  `` `PostService.CreateGroupPostAsync` `` (plain backticks, **not** `<c>`). I did
  **not** upgrade it to a `<c>` cref — that's a `Post.cs` edit (a 3rd file) and is
  cosmetic; the member now exists and resolves regardless. Left for U12 (final
  consistency check).
- **M3 surface byte-untouched:** `GetPostAsync`, `CreatePostAsync`, the two
  `List*` feeds, `CreateReplyAsync`, `HidePostAsync`, `RemovePostAsync` are all
  **unchanged** (I only appended). This keeps `G12` / `G13` holding
  **structurally** — M3 feeds still filter on `ComponentId`; nothing here narrows.
- **No new dependencies** (ADR 0006-D): `PostService`'s constructor is
  **unchanged** (`IUserInfoService`, `IAuthorizationService`, `IDocumentStore`) —
  the group-lane membership read is owned by U5's `AuthorizationService.GroupLane`,
  which reads `IUserInfoService` **itself**; `PostService` never reads
  `IsMemberOf` / grants.
- **No doc-types change:** `Post` is already a registered document (U1, via
  `M1DocTypes.WithDocument<Post>()`); writing `Post` rows in the group lane needs
  no new doc type. I did not touch `M3DocTypes` / `M1DocTypes`.
- **No ADR / design-doc edits** (unit-series rule: only create/modify files in
  Deliverables); **no `GroupPostController`** (U7); **no `GroupPosts` page** (U8);
  **no seam tests** (U9).

### How I verified

- `dotnet build Kumunita.slnx -c Debug` — **0 Warning(s), 0 Error(s)** (no
  `<c>`→ref failures, no unused-import warnings; `GroupPostDraft` and the three
  methods compile clean).
- **All 235 `Kumunita.Core.Tests` pass** (0 Failures, 0 Errors, 0 Skipped) via the
  reliable in-process `dotnet exec …\Kumunita.Core.Tests.dll` runner (the `dotnet
  test` / Test Explorer "No tests found" is the known-bad discovery path on this
  machine, per AGENTS.md). The U5 suite stays green against the U6 surface — no M3 /
  directory seam test broke.

### Handing to U7 (`GroupPostController`)

The service is **ready** for a controller:
- `ListGroupFeedAsync(string groupId, int page, int pageSize, CancellationToken)` →
  `FeedResult` (feed; U7 binds `page` / `pageSize`).
- `GetGroupPostAsync(string groupId, string postId, CancellationToken)` →
  `PostDetailResult` (detail).
- `CreateGroupPostAsync(GroupPostDraft, CancellationToken)` → `Post` (create —
  U7 constructs the `GroupPostDraft` from the form).

**Shape the tests (#1–9) expect:** the **service** is where the authorization gates
live; the **controller** (U7) does HTTP mapping + form binding and is **expected to
call `PostService`** with exactly these signatures — no additional authorization
logic in the controller (the group-lane calls are already in the service, §2.2).
U7 should **not** re-implement membership checks. The `UnauthorizedAccessException`
from `CreateGroupPostAsync` (test #3: 403) and `KeyNotFoundException` from
`GetGroupPostAsync` (tests #6 / #7: 404) are the HTTP status codes U7 maps.

### U6's plan-file status

`group-posts-u06-plan.md` was already in `in-progress/` at this unit's start
(moved there by U5's Exit per the workflow). **This** unit's Exit moves it to
`done/` immediately after this note lands — a plain file move: **nothing is staged
or committed; the user is asked to review first.**

### What I deliberately did NOT do (out-of-scope per the unit plan)

1. Did **not** implement the `GroupPostController` (U7).
2. Did **not** implement the `GroupPosts` page (U8).
3. Did **not** write any seam tests (U9 owns #1–9 of the 19).
4. Did **not** modify `M3DocTypes` / `M1DocTypes` / `M5DocTypes` / `M6DocTypes`
   (U1 — already landed; the `Post` registration already lives there).
5. Did **not** implement the **moderator** path — the group lane has **no** moderator
   branch by design (G·4).
6. Did **not** add any new **dependencies** (no `IUserGroupService`, no new
   `IUserInfoService` call — the group lane's `IsMemberOf` / grants reads are U5's
   `GroupLane`'s, not the service's).

## U7 — Web controller surface (feed / detail / create / reply)

- **Two files, the closed Deliverables set** (the unit plan's "≤ 3 files", met with 2;
  the rows reuse the M3 list-item records, so no new row types were needed):
  - **`src/Kumunita.Web/Models/GroupViewModel.cs`** (appended) — the three new types,
    slotted into the existing groups view-model file per the unit plan's pin:
    `GroupFeedViewModel` (mirrors M3 `FeedViewModel`; the component-name slot is
    `GroupName`; the M3 community-specific slots — `IsMandatory` / `CanManageCommunity`
    /
    `CanLeaveCommunity` / `Communities` — do **not** exist here, the group lane has no
    community management surface), `GroupPostDetailViewModel` (mirrors M3
    `PostDetailViewModel` + a `GroupId` reply-form target slot; **no** dedicated reply
    view-model — the reply form is inline, exactly M3), `GroupPostComposeViewModel`
    (mirrors M3 `PostComposeViewModel` **minus** `ComponentId`/`Audience`/picker —
    title + body only; the group's identity is the route's `{id}`, never form-bound).
    The feed/detail rows reuse the M3 `PostListItem` / `ReplyItem` records verbatim
    (same namespace — "reuse, don't re-invent").
  - **`src/Kumunita.Web/Controllers/GroupsController.cs`** (appended) — the four
    actions under the existing `/groups/{id}` route prefix, plus the constructor gain
    `PostService posts` + `IDocumentStore store` (the M3 `PostsController` pattern;
    the class-level `[Authorize]` and the existing M2/m2b actions are untouched):
    `GET /groups/{id}/posts?page=N` → `View("Feed", GroupFeedViewModel)` ·
    `GET /groups/{id}/posts/{postId}` → `View("PostDetail", GroupPostDetailViewModel)` ·
    `POST /groups/{id}/posts` → `CreateGroupPostAsync` → `Redirect` to the new
    post's detail · `POST /groups/{id}/posts/{postId}/replies` → **reused**
    `CreateReplyAsync(postId, actor, body, session)` (lane-neutral, unchanged).
    **No** `Report` / moderation action (Scope Out; G·4 is *unavailable*, not
    deferred). **No TypeScript / new assets** (plain forms).
- **404 mapping (the register + design doc §2.2 pins — recorded, not a drift pause):**
  the unit plan file's detail-"403" line was superseded by the design doc
  (authoritative): U6's `GetGroupPostAsync` returns `Post = null` and its doc pins
  "**Web 404 — G·3/G·4**", and the master register pins "Non-member: 404" + "Web
  renders 404" for the create gate. Both the detail-Deny and the create
  `UnauthorizedAccessException` therefore map to **`NotFound()`** (consistent with
  this file's standing "a plain member's POST 404s" / "a non-visible group 404s"
  precedent) — **not** M3's `Forbid()` 403 shape, which belongs to the audience lane.
- **Access is never re-derived (ADR 0006-D):** no `IAuthorizationService` call in the
  controller; every decision is inside the U6 service calls. The feed's `CanPost`
  flag is a *display* convenience (a live `GetGroupIdsAsync` read — "a read, not a
  decision") driving the composer's visibility; the POST gate remains the
  authoritative deny (G·3). The reply route re-runs `GetGroupPostAsync` (parent Allow
  first) before the write — the M3 `Replies` pre-write gate, the C4 gap case (a
  member removed between render and POST is denied).
- **Fail-closed shapes:** group missing ⇒ 404 **before** the service call (the M2b
  `FindGroupAsync` pattern, read via `GetGroupAsync`); null actor ⇒ 404 (this file's
  `Detail` shape, `Unauthorized()` is the M2 read-lane variant but 404 keeps the
  group-post family consistent); blank reply body ⇒ `TempData["error"]` + redirect
  back to the detail (the M3 `Replies` shape).
- **Verified:** `dotnet build Kumunita.slnx -c Debug` — **Build succeeded**, all
  four projects clean (the touched project is `Kumunita.Web`); `dotnet exec
  Kumunita.Web.Tests.dll` → **Total: 90, Errors: 0, Failed: 0, Skipped: 0** — no
  M2/m2b/M3 regression (nothing in the test harnesses touches `GroupsController`
  directly, and the M3 routes are untouched).
- **Handing to U8 (the Razor views):** the three view names the actions emit are
  `View("Feed")` / `View("PostDetail")` / `View("New")` **under `Views/Groups/`**
  (the `New` view is the composer GET, rendered only on the POST's failed-shape
  re-render — U8 should add a thin `GET /groups/{id}/posts/new`-style entry or
  render the composer inline on `Feed` when `CanPost` — the plan names the views
  `Feed.cshtml` / `PostDetail.cshtml`; the composer surface is U8's to place, the
  model is `GroupPostComposeViewModel` (title + body, antiforgery, action
  `POST /groups/{id}/posts`). `FeedViewModel`-style `CanPost` drives the
  composer's visibility; the group name is on the model (`GroupName`) — no extra
  reads in the view. The reply form on `PostDetail` is a one-field `body` POST to
  `/groups/{GroupId}/posts/{Post.Id}/replies` (the `GroupId` slot exists for
  exactly this).
- **Drift pause (recorded, unit rule 6 — the entry reads revealed a stale line):**
  `docs/plans-milestones/in-progress/group-posts-u07-plan.md` (the unit's own
  spec file) still says "403 on Deny — the M3 `Detail` deny shape" for the detail
  action, but the **authoritative** design doc §2.2 (U6's landed seam doc: "Web
  404") + the master register ("Non-member: 404") + U6's handoff note ("tests
  #6/#7: 404") all pin **404**. I implemented **404**. The plan file was already
  moved to `done/` state in the in-progress folder at my entry (unlike U6's, its
  Exit move had not happened) — the line is left as-is for U11/U12's docs close to
  reconcile (it is a *plan*-file prose line, not a seam, so no design-doc drift).
- U7's plan file moves to `done/` immediately after this note (per the workflow) —
  a plain file move: **nothing is staged or committed; the user reviews first.**

## U9 — the 19 pinned group-post seam tests (`GroupPostServiceTests`)

- **The Deliverable (closed set = 1 new file):** `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs`
  — class `GroupPostServiceTests(PostgresFixture) : IClassFixture<PostgresFixture>`,
  all **19 `[Fact]`s with the exact §2.5 names, character-for-character** (no rename —
  §2.7 drift guard): `G1_MemberSeesGroupFeed`, `G2_NonMemberFeedEmptyWithDenyRow`,
  `G3_MembershipAddReScopesNextFeed`, `G4_MembershipRemoveRevokesNextDetail`,
  `G5_MemberCreatesGroupPostSeesIt`, `G5_GroupPostAudienceWrittenEmpty`,
  `G6_NonMemberCreateDenied`, `G7_ModeratorNonMemberDenied`,
  `G8_BreakGlassDoesNotApplyToGroupPosts`, `G9_DelegateWithReadInScopeSeesOwnerGroupPosts`,
  `G10_DelegateWithoutReadDenied`, `G11_ReplyInheritsParentGroupLane`,
  `G11_ReplyNotEvaluatedOnParentDeny`, `G12_GroupPostExcludedFromComponentFeed`,
  `G13_GroupPostExcludedFromAllFeed`, `Feed_AggregateAuditRowShape_GroupPost`,
  `Detail_DecisionAuditRowShape_ViaGroup`, `Detail_DecisionAuditRowShape_ViaDelegation`,
  `PostService_MakesNoModerateOrBreakGlassCallOnGroupPosts`. No production file
  touched — the U4–U8 surface is asserted, not amended.
- **Fixture shape (reuses the suite's existing harness, no new plumbing):** each test
  boots its own store via `PostgresFixture`'s connection + a fresh `IDocumentStore`
  (`BootStoreAsync` → `(store, conn)`), plants domain rows directly (a
  `GroupMembership` per test — the *sole* decision input, G·4), and builds the trio
  (`IUserInfoService`, `IAuthorizationService`, `PostService`) with
  `DependencyInjection`-style composition mirroring `AuthorizationServiceTests` /
  `PostServiceTests`. G8's consumed + unexpired `AdminOverride` and G7's
  `ModeratorAssignment` (for a *different* component) are planted via raw Npgsql,
  following the `AuthorizationServiceTests` precedent.
- **What the suite pins (per §2.5 intent, against the landed U6 seams):**
  - **Membership is the whole decision (G·4 "nobody peeks"):** G1 allow / G2 deny +
    empty feed with the deny row / G3 add re-scopes next feed / G4 remove revokes
    next detail / G6 create denied with the exact
    `UnauthorizedAccessException` message / **G7: a moderator of *another*
    component is still denied** / **G8: a consumed, unexpired `AdminOverride` is
    still denied** — both are fixture proofs that *some* standing exists and it is
    *ignored*. No test needs a "moderator sees it" positive case; that case does not
    exist in the lane.
  - **Create writes the group shape (C·shape):** G5 writes `ComponentId = ""`,
    `GroupId = draft.GroupId`, and **`Audience` stays empty**
    (`G5_GroupPostAudienceWrittenEmpty` — the audience lane is structurally unused
    here, not merely denied); G5's create-then-see round-trip proves the single
    `SaveChangesAsync` path returns the readable post.
  - **Delegation (G·6/C2):** G9 — an in-scope `read` grant ⇒ the delegate reads the
    *owner's* group post, audit row `Via = Delegation`

## U10 — gate run + record

- **Gate: ALL THREE PASS, 2026-09-12, this machine (Windows/PowerShell).**
  Runner per AGENTS.md: `dotnet build Kumunita.slnx -c Debug` (green) →
  `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → **90/90, 0 failed** → `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` →
  **254/254, 0 failed** (Testcontainers `postgres:18`, containers stopped and
  deleted by the run itself — no `docker container prune` needed).
- **Part-vs-whole evidence:** all 19 §2.5 pinned names are present
  character-for-character in `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs`
  (19 `[Fact]`s counted) and are **inside** the 254-passing run alongside the
  inherited M1/M2/M3/M3b/media suites — the same-run requirement of §2.4 row 3.
  **No drift:** U2's freeze held, so the §2.5 rename lane was not exercised.
- **Record:** appended `## Group posts — Gate (recorded by U10)` to
  `docs/design/group-posts-design.md`, immediately **above** the untouched
  `## Group posts — Closed` placeholder (which remains U12's close marker).
  Closed loop = `G5_MemberCreatesGroupPostSeesIt` +
  `Feed_AggregateAuditRowShape_GroupPost`; handoff =
  `G3_MembershipAddReScopesNextFeed` + `G9_DelegateWithReadInScopeSeesOwnerGroupPosts`;
  part-vs-whole = the 19-name line above.
- **Handing to U11 (docs close + folder moves):** U10 changed **no** code and
  added **no** tests — the only repo delta this unit is the design-doc section.
  U11 may treat the group-posts surface (U4–U9) as gate-verified when it syncs
  `ARCHITECTURE.md` / `Milestones.cs` / README and reconciles the folder moves
  (several plan files from U1–U9 are still in `in-progress/` — the register
  says each unit's Exit moves its own file, and the U7 note records one such
  line left as-is for U11 to reconcile). **Nothing is staged or committed —
  the user reviews first.**
    (`Detail_DecisionAuditRowShape_ViaDelegation`); G10 — a grant whose scope is
    `["write"]` (no `read`) ⇒ denied, delegate acts as themself (scope entries are
    action ids; `AccessAction` only defines `Read`/`Moderate`, so the out-of-scope
    id is a literal string, mirroring how `Grant.Scope` is written elsewhere).
  - **Replies inherit the lane (G·7):** G11-allow — a member replying to a visible
    parent lands a reply child with `GroupId` inherited from the parent and the
    detail still returns the post + replies as-is; G11-deny — parent Deny ⇒ the
    reply is never evaluated (no child row, parent detail is `Post = null`).
  - **Exclusion from the other feeds:** G12 — the component feed (a post's own
    `ComponentId` feed) excludes group posts; G13 — the all-feed excludes them.
    (Group posts have `ComponentId = ""`, so both are the additive's negative
    shape, not a new query path.)
  - **Audit row shapes (C3):** `Feed_AggregateAuditRowShape_GroupPost` —
    `TargetId = null`, `VisibleCount`/`HiddenCount` set, `TargetKind = "grouppost"`,
    `Action = "read"`; `Detail_DecisionAuditRowShape_ViaGroup` — decision shape
    `TargetId = postId`, counts null, `Via = Group`; delegation variant as above.
- **Test #19 — the "nobody peeks" invariant is enforced structurally, not just by
  fixtures:** a nested `RecordingAuthz(IAuthorizationService inner) : IAuthorizationService`
  spy wraps the real service; the test drives one feed + one detail + one create
  through `PostService` and asserts **`GroupLaneCalls >= 4`**,
  **`AudienceLaneCalls == 0`** (the lane never calls the frozen `CanAsync` /
  `CanSeeAsync` for group rows), **`ModerateCalls == 0`** (the lane never asks for
  `AccessAction.Moderate`). This is the machine-checkable half of G·4; G7/G8 are
  its fixture half.
- **Verified (the AGENTS.md reliable path):** `dotnet build Kumunita.slnx -c Debug`
  — **Build succeeded**, all four projects clean; `dotnet exec
  tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` →
  **Total: 254, Errors: 0, Failed: 0, Skipped: 0, Time: 27.5s** — that is U8's
  235 baseline + these 19, zero regressions in the suite.
- **One compile fix made while landing (no seam impact):** the first draft of G10
  used `AccessAction.Write.Id`, which does not exist (`AccessAction` defines only
  `Read` and `Moderate`). Corrected to the literal id `["write"]` — the intended
  semantic (an out-of-scope action, no `read`) is unchanged.
- **Drift pauses: none.** Every assertion matched the landed U4–U8 seams as
  documented in their notes; no production file needed a one-line mirror fix.
- U9's plan file moves to `done/` immediately after this note (per the workflow) —
  a plain file move: **nothing is staged or committed; the user reviews first.**

## U11 — docs close (README / Milestones.cs / ARCHITECTURE.md) + folder reconcile

- **Deliverables (3 files, all small additive edits — the closed set):**
  - **`README.md` — Features** (one new bullet, the **media precedent**: a
    shipped non-lettered lane lives in *Features*, not the M-named Roadmap —
    media has no Roadmap line and no `Milestones.cs` entry either):
    **Group posts** — the post channel *inside* a group
    (`/groups/{id}/posts`): membership-scoped feed / detail / composer /
    replies (ADR 0013); members only — a non-member (moderator or admin
    alike) neither sees nor posts; the audience lane is never evaluated;
    replies inherit the parent's single membership decision. The
    **M4/M5/M6 Roadmap lines are untouched** (still Events / Projects /
    Portability).
  - **`src/Kumunita.Web/Milestones.cs`** — one matching `Entry`, slotted
    **after M3** (shipping order), `StatusDone`:
    `new("GP", "Group posts — the membership-scoped post channel inside a
    group (ADR 0013)", StatusDone)`. The README↔`Milestones.cs` contract
    holds: both name the same lane, both say done; `M0–M6` byte-untouched.
    **Build green:** `dotnet build Kumunita.slnx -c Debug` — Build
    succeeded, 7.4 s, all four projects clean.
  - **`docs/ARCHITECTURE.md`** — the two additive spots (the unit plan's
    (a)/(b)), each with the ADR 0013 pointer:
    - **(b) §4.2 `IAuthorizationService` sketch** — one comment block after
      the `VisibleSet` line: the group lane — `CanSeeGroupAsync(actorId,
      groupId, targetPostId?)` (detail / create-gate, one decision row) +
      `CanSeeGroupFeedAsync(actorId, groupId, candidateCount)` (feed, one
      aggregate row); `Via = Group` or `Delegation` (in-scope `read` grant
      acts with the owner's membership); **no** moderator / **no**
      break-glass branch (G·4 "nobody peeks"); the audience lane is never
      evaluated.
    - **(a) §5 `Content` block** — one comment above `PostReply`: a group
      post = `Post.groupId` non-empty; `audience` written non-null **empty**
      (never evaluated); `componentId` empty (excluded from component +
      "all sections" feeds); `PostReply` **unchanged** (lane-neutral),
      replies inherit the parent's single group-lane decision.
    - **One line beyond the two (stale-fix, recorded):** §5 `AccessAudit`
      via-list — appended **`Group`** to `…|BreakGlass|Admin` (the 8th
      value landed by U5; the list was stale without it, and U12's
      consistency check greps it).
- **Folder reconcile (the Exit's "verify every prior unit's plan file is in
  `done/`"):**
  - `done/` — `group-posts-u01-plan.md` … `group-posts-u10-plan.md` **all
    present at entry** ✓ (no move needed).
  - **Moved (the debt U10 flagged — "several plan files from U1–U9 are still
    in `in-progress/`"):** the nine stray **`group-posts-u{01…09}-exec-plan.md`
    working files** (the media precedent archives its exec-plans in `done/`;
    U7's note records its own Exit move never happened).
  - `in-progress/` now holds exactly: `group-posts-handoff-notes.md`
    (**stays** — living scratch, never archived), `group-posts-u11-exec-plan.md`
    (my working note, the media exec-plan precedent), `group-posts-u11-plan.md`
    (moves to `done/` at this Exit), `group-posts-u12-plan.md` (**stays** —
    U12's spec).
  - **ADR index:** U3's 0013 line is **present** in `docs/adr/README.md`
    (verified — no edit needed, per the plan's "if U3's entry is missing").
- **Handing to U12 (final consistency + close record):** the three close
  docs now agree with each other and with the design doc's §2 seams;
  `AccessVia.Group` is in the §5 via-list; the 19-test gate line is U10's
  section above (ALL THREE PASS). **One open prose item for U12's
  checklist:** `done/group-posts-u07-plan.md` (archived spec) still reads
  "403 on Deny" for the detail action — the **landed behavior is 404**
  (U6/U7 both pinned it; the design doc is authoritative). It is a
  *plan*-file prose line in an archived file — **not** a seam and **not** a
  drift-pause header in this note; flag it as a known-stale archived line,
  do not count it as a `## U<m> — Drift pause`.
- **Drift pauses: none.** No `## U<m> — Drift pause` header was added by
  this unit.
- U11's plan file moves to `done/` immediately after this note (per the
  workflow) — a plain file move: **nothing is staged or committed; the user
  reviews first.**

## U12 — final consistency check + record `## Group posts — Closed (recorded)`

- **Milestone closes — all five checklist lines PASS, 2026-09-12, this machine
  (Windows/PowerShell).** This is the last unit (U1 → U12 strict order). No code
  changes, no new tests, no folder moves beyond this file's own exit.
- **Deliverable (1 file):** `docs/design/group-posts-design.md` — the `##
  Group posts — Closed` placeholder (U2's, referenced by Part 1's drift guard)
  is now **`## Group posts — Closed (recorded)`** with the date (2026-09-12) and
  the five-line consistency checklist, each line PASS:
  - **seams PASS** — `Post.GroupId` (`src/Kumunita.Core/Posts/Post.cs:63`),
    `AccessVia.Group` (`src/Kumunita.Core/Authorization/Decision.cs:31`, 8th
    value), the four group-lane methods on
    `IAuthorizationService`/`AuthorizationService` (the two `CanSeeGroupAsync`
    + the two `CanSeeGroupFeedAsync` — U2-A1/A2 freeze, lines 101/111/125/132),
    the three `PostService` group methods (`PostService.cs:405/460/520`, ctor
    unchanged), and `GroupPostDraft` (`GroupPostDraft.cs:20`). All present, each
    named with its file.
  - **tests PASS** — all 19 §2.5 names present in
    `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs` (lines 45–644, 19
    `[Fact]`s), 1:1 with §2.5, none missing/extra; the U10 gate line (Core
    254/254 + Web 90/90, 344/344) still PASS per the recorded gate section.
  - **close docs PASS** — `README.md:59` Features bullet,
    `Milestones.cs:27` the `GP`/`StatusDone` entry, `ARCHITECTURE.md` §4.2
    group-lane comment + §5 `Post.groupId`/via-list `…|Group` — all three carry
    the group-posts line and agree with §2 (U11's checklist).
  - **folders PASS** — `done/group-posts-u01-plan.md` … `group-posts-u11-plan.md`
    all present; the handoff note has one section each for U1–U11 (the U5 section
    is indented under U4's — content present, counted) and this one, appended at
    exit.
  - **drift PASS** — `## U<m> — Drift pause` **header** count in the handoff note
    = **0** (clean). Per U11's handoff: the "Drift pause" text mentions in the
    note are inline prose self-described *not* a drift pause, and
    `done/group-posts-u07-plan.md`'s "403 on Deny" prose (landed behavior is 404)
    is a known-stale archived plan-file line — not a seam, not a header, not
    counted. **No hard failure → the milestone is closed.**
- **Handing forward:** none — this is the last unit. The group-posts surface
  (U4–U9) is gate-verified (U10, ALL THREE PASS) and the close docs (U11) agree
  with the design doc's §2 seams. The handoff note **stays** in `in-progress/`
  (living scratch, never archived); this exec-plan and the spec
  (`group-posts-u12-plan.md`) move to `done/` at this Exit.
- **Drift pauses: none.** No `## U<m> — Drift pause` header was added by this
  unit; the milestone closes clean.
- U12's exec-plan and spec move to `done/` immediately after this note (per the
  workflow) — a plain file move: **nothing is staged or committed; the user
  reviews first.**
