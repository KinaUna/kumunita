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
