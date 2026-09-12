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
