# U2 execution plan (working) — group posts: design doc Part 2 (seams, 19 test names, gate, drift guard)

> My (unit U2's) working plan for this pass. The **authoritative spec** is
> `group-posts-u02-plan.md` (the created unit: Goal / Entry reads / Deliverables
> §2.1–§2.7 / Exit) + the **master register** `plan-group-posts.md` (the
> *directional* "Pinned contract" — the draft I either freeze verbatim or
> record a "## U2 — contract adjustment" for — and the 19-test list I
> cross-check) + the **templates**: `docs/design/m3-posts-design.md` §2.1 /
> §2.3–§2.6 (seam-freeze style, rule tables, test-name list, gate, drift
> guard) and `docs/design/group-posts-design.md` Part 1 (invariants G·1–G·8,
> FACES G1–G13 — the reference anchors for every frozen pin in Part 2).
> **No code, no build.**
> Prior section read: **U1** (in `group-posts-handoff-notes.md`) — 8 invariants,
> 13 FACES, the single-additive note, the break-glass flag.

## Scope (in / out)
- **In (mine):** 2 files.
  1. `docs/plans-milestones/in-progress/group-posts-u02-exec-plan.md` — this file.
  2. `docs/design/group-posts-design.md` — **append** (Part 2): §2.1 frozen seam
     list (exact C#), §2.2 new / changed Core types (exact C#), §2.3
     lane-exclusivity + reply-inheritance rule, §2.4 acceptance gate (3 tests),
     §2.5 the 19 pinned test names, §2.6 module-boundary impact notes, §2.7
     drift guard.
- **Out:** all code (U4–U8), ADR 0013 (U3), the 19 tests themselves (U9), the
  gate run + record (U10). I write no `.cs`, touch no project files, run no
  build. I do not rewrite Part 1 (the handoff note carries any deviation;
  Part 2's drift guard §2.7 is *mine* to write).

## Constraints I keep pinned while I write
- From **Part 1** (verbatim): G·1–G·8, G1–G13, "break-glass is not a deferral",
  the single-additive note (no `M3DocTypes` change, `PostReply` unchanged),
  "the '## Group posts — Closed (recorded)' section is a placeholder".
- From **the master register**: the 19 test names (authoritative count and
  list), `GroupPostDraft(string GroupId, string? Title, string Body)`,
  `Post.GroupId : string, default string.Empty`, `AccessVia.Group` (8th value),
  PostService's three group method signatures (ctor unchanged), the decision
  algorithm (in-scope `read` → owner's standing → live `GetGroupIdsAsync`;
  **no** Moderate, **no** BreakGlass).
- **The draft is directional** (the register says so) — wherever the draft's
  shape can't express a row shape Part 1's own FACES / a pinned test requires,
  I pin the shape and record `## U2 — contract adjustment` (in Part 2 + the
  handoff section) instead of silently conforming.
- Every C# fragment pinned against the **actual** source file (not a
  "common" pattern from memory): `IAuthorizationService.cs` (4 frozen
  signatures), `Decision.cs` (7-value `AccessVia`, `Decision`/`VisibleSet`),
  `AccessAudit.cs` (the two row shapes), `AuthorizationService.cs` (`Decide`
  / `ResolveActorAsync` / `DecideAsync` — the via-pin and delegate-branch
  precedent), `UserInfo/Group.cs` (`DelegationGrant.Scope` shape),
  `IUserInfoService.cs` (`GetGroupIdsAsync` / `GetActiveGrantAsync`),
  `Posts/Post.cs` + `Posts/PostReply.cs` (POCO shapes, the `Status` additive
  precedent), `Posts/PostService.cs` (ctor, the four methods I reuse,
  `FeedResult`/`PostDetailResult` shapes, the M3 feed queries),
  `M3DocTypes.cs` (where the `Post` surface is registered).

## Entry reads (done — all in `group-posts-u02-plan.md` / register lists)
- `docs/design/group-posts-design.md` (Part 1, the whole file)
- `docs/design/m3-posts-design.md` (Part 2 §2.1–§2.6 — the template)
- `src/Kumunita.Core/Authorization/IAuthorizationService.cs`, `Decision.cs`,
  `AccessAudit.cs`, `AuthorizationService.cs`
- `src/Kumunita.Core/UserInfo/IUserInfoService.cs`, `Group.cs`
- `src/Kumunita.Core/Posts/PostService.cs`, `Post.cs`, `PostReply.cs`,
  `FeedResult.cs`, `PostDetailResult.cs`, `PostDraft.cs`, `M3DocTypes.cs`

## Findings (the draft's two problems + one structural win)
1. **The 2-overload draft can't express the pinned row shapes.**
   `AccessAudit` has exactly **two** row shapes (`TargetId` set vs.
   `VisibleCount`/`HiddenCount` set — `AccessAudit.cs`), and the register's
   own pins require *both*: test #16 `Feed_AggregateAuditRowShape_GroupPost`
   (aggregate: `TargetId null`, counts set — G1 FACES "one aggregate row")
   and tests #17/#18 (decision: `TargetId` = **the post id**, counts null). A
   single-target seam `(actorId, groupId)` can only write one shape per call,
   and it has no parameter to carry the post id. → two adjustments (recorded
   in Part 2 §2.1 + the handoff section):
   **U2-A1** — a feed pair `CanSeeGroupFeedAsync(actorId, groupId, candidateCount[, session])`
   (the M1 single↔bulk pair precedent: `CanAsync` ↔ `CanSeeAsync`);
   **U2-A2** — the single-target pair gains `string? targetPostId`;
   row `TargetId = targetPostId ?? groupId` (detail ⇒ post id; gate ⇒ group id).
2. **G6's deny row must survive the throw.** Create lands in the caller's
   transaction (C3, the M3b write-lane shape). If the gate's Deny row sat in
   the caller's open transaction and the service threw before
   `SaveChangesAsync`, the row would roll back — contradicting G6 FACES
   ("… service exception **and** a Deny row"). → pinned in §2.2:
   `CreateGroupPostAsync` → on deny: `SaveChangesAsync()` **then** throw
   `UnauthorizedAccessException` (row persisted, no post); on allow: gate row
   + post in **one** `SaveChangesAsync` (atomic, C3).
3. **G12/G13 hold *structurally* — there is no filter to add.** Both M3 feed
   queries already filter on `ComponentId` (`p.ComponentId == componentId` /
   `componentIds.Contains(p.ComponentId)`), and group posts write
   `ComponentId = string.Empty` (G·2). → pinned in §2.3(a) as
   "structural exclusion; U6 adds no filter; the tests pin the outcome"
   (rather than implying a filter the code doesn't have).

Also noted (drives §2.2 wording, no adjustments): `CreatePostAsync`'s real
signature takes `actorRoles` (the GlobalAdmin/moderator skip) — the group
gate deliberately **has no** `actorRoles` parameter (G·4: a non-member
GlobalAdmin is denied) — the pin states the contrast so U6 doesn't copy the
component gate's shape; `FeedResult`/`PostDetailResult` are reused as-is (no
new result records — keeps U6's ADDs minimal); `AccessVia.Group` appends
**after** `Admin` (never inserted mid-enum — stored audit rows would
re-map); `PostService`'s ctor is already
`(IUserInfoService, IAuthorizationService, IDocumentStore)` — the group
surface adds no dependency (the lane owns all membership reads, ADR 0006-D).

## Steps (no build)
1. Write this exec plan file.
2. Append `## Seams & contracts (Part 2, written by U2)` to the design doc —
   §2.1 → §2.7 in order (matching the unit plan's Deliverables numbering,
   which differs deliberately from M3's) — including the adjustments'
   record.
3. Add the missing `## Group posts — Closed` placeholder heading at the file
   tail (Part 1's drift guard *references* it; it isn't in the file — the
   heading I add is the empty placeholder U10/U11/U12 fill at close; the
   handoff section notes this).
4. **Exit** (in this order, per the unit plan's Exit + workflow): append one
   section `## U2 — design doc Part 2 (…)` (with the "U2 — contract
   adjustment" record inside) to
   `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`; then move
   `group-posts-u02-plan.md` → `docs/plans-milestones/done/` (a plain file
   move — **not** staged, not committed; the user reviews everything first).

## Exit (per the unit plan's "Exit" + the master workflow template)
- The design doc's Part 2 contains §2.1–§2.7 exactly as the unit plan's
  Deliverables name them; every C# pin matches the real source (cross-checked
  against the entry reads); the 19 test names are verbatim from the master;
  the two contract adjustments are recorded (Part 2 + handoff section).
- Handoff section appended **before** the plan file moved to `done/` (done
  when the move has happened).
- **No build**: a doc unit — no `run_build`, no test run, no project change.

## Verification
- Open the design doc; confirm Part 2 headings §2.1–§2.7 are present and in
  the unit plan's order.
- Spot-check the frozen C# against source (enum order/appending, the 4 lane
  signatures, the 3 PostService signatures, the record shape) — one
  side-by-side each.
- Confirm all 19 test names verbatim (string-compare against the register's
  table; recount 19).
- Confirm the handoff file gained exactly one `## U2` section and U1's
  section is byte-untouched.
- `git status --short` (no `git add`): design doc (modified) + handoff notes
  (modified) + exec plan (new, untracked) + u02 plan file (moved to `done/`)
  — **no** `.cs` files, **nothing** staged.
