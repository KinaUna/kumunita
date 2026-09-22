# Group posts — membership-scoped group channel — sealed unit register

## Understanding
M1–M3b (plus the media milestone) shipped: identity, directory/profiles/groups, component posts + moderation, file storage. Groups are still *just a collection of users* (their M2 design): membership, ownership, invitations, privacy — a group page has **no** post channel. Posts (M3) let an author *address* a public group via an audience grant (ADR 0010: a private group can never be an audience). `how-it-works.md` promises "You can post to a group — and when membership changes, all your past posts reach exactly the right people": for a group to *have* posts, **membership must be the access unit**.

**Scope decisions (user, 2026-09-12):**
- Group posts are **membership-scoped**: visible to the group's *current* members; works for private groups (a family group becomes a real private channel). This is a **new authorization lane beyond audience** — a larger change that needs its own ADR (0013).
- **Members only** may post to a group (owner included; non-members can't create, can't see, can't reply).
- Named **group posts** — no milestone letter (media precedent; roadmap M4/M5/M6 stay Events/Projects/Portability).

## Assumptions (pinned; the design doc Part 2 makes them exact)
- **Membership, not audience (G·1).** A group post's visibility = `Post.GroupId` membership. The audience lane is **not evaluated** for group posts (single decision; group posts carry a non-null, **empty** `Audience` — G·8). One new lane on the frozen `IAuthorizationService` (ADR 0006-E compatible lane — *added* methods, frozen signatures untouched, like M2's `GetProfilesAsync` and M3b's `Moderate` ADD) plus an 8th `AccessVia` value `Group` (the M1 `Admin` enum-ADD precedent: least-distortion slot for the lane's standing).
- **Data is one additive `Post` field (G·2).** `Post.GroupId` (`string`, default empty) marks a group post — ADR 0004 §B.1 additive, exactly M3b's `PostStatus` ADD lane: delta-detected, idempotent, no re-seed, **no** `M3DocTypes` change (Marten's `Schema.For<Post>()` already registered; the delta picks the field up). `PostReply` is **unchanged** (lane-neutral). **Lane exclusivity:** `GroupId ≠ ""` ⇒ the post is a group-channel post, `ComponentId` is empty, and component feeds (`ListFeedAsync`/`ListAllFeedAsync`) never surface it (G·2).
- **Members-only authoring (G·3).** The create gate *is* the group-lane decision (Allow ⇒ may post, Deny ⇒ `UnauthorizedAccessException` — the SoD wall; Web renders 404, the GroupsController "plain member's POST 404s" precedent).
- **Moderator-off / breakglass-off (G·4 — `how-it-works.md`'s privacy pitch, ADR 0003, C5).** Non-member moderators, non-member GlobalAdmins — denied. **No break-glass on group posts** (the strictest privacy lane): group posts are visible to members only, full stop.
- **Strong consistency (G·1/C4).** A membership add/remove takes effect on the **very next** request through the live `IUserInfoService.GetGroupIdsAsync` read (no projection lag) — exactly today's group-lane behavior.
- **Delegation (G·6, C2).** An in-scope (`read`) delegate acts with the owner's standing for group posts (visible iff the owner is a member; `Via` = Delegation); out-of-scope denies.
- **Audit (G·5, C3).** Feed = **one aggregate** `AccessAudit` row (TargetKind `"grouppost"`, TargetId null, `visibleCount`/`hiddenCount`); detail = **one decision row** (TargetKind `"grouppost"`, TargetId = post id); create gate = one group-lane decision row. Always (Allow *and* Deny), in-transaction via the `IDocumentSession` overloads — "no silent, unaudited access".
- **Replies (G·7).** `PostReply` + `CreateReplyAsync` are lane-neutral and **reused**: a reply on a group post inherits the parent's single group-lane decision (C-M3·1 analog — no second evaluation, no second row, no reply-level audience).
- **Web surface (B track).** The group page grows a channel: feed at `/groups/{id}/posts` (members see a composer), detail + reply at `/groups/{id}/posts/{postId}`. Thin `GroupsController` actions (the M2 group lanes), plain form posts (M3's reply form pattern — **no** new TS). Non-member: 404.
- **Out of scope (carried forward, named):** moderation/report flow on group posts (M3b's component-scoped moderator standing does not reach a group lane), break-glass *allow* (explicitly **not** a deferral — privacy-first says no), notifications on new group posts (M6), search over group posts (M6), cross-posting a group post into a component (the lanes stay exclusive), pagination UI beyond the existing feed paging.
- **Test model (unchanged).** xunit.v3: on this machine the discovery path (VS Test Explorer / `dotnet test`) is a known-broken bug — the reliable runner is `dotnet build` + `dotnet exec <test-assembly>.dll` (AGENTS.md). Three-test acceptance gate (closed loop / handoff / part-vs-whole) + invariant-anchored seam list (19 tests, pinned in design doc Part 2).

## Approach
Three tracks, sequenced — exactly like M3. **Track A (Core + ADR):** design doc (U1/U2), **ADR 0013** (U3), the `Post.GroupId` additive (U4), the authorization group lane (U5), the `PostService` group surface (U6). **Track B (Web):** `GroupsController` actions + view models (U7), Razor under `Views/Groups/` + the group-detail entry point (U8). **Track C (Tests + close):** the 19 pinned seam tests (U9), run + record the gate (U10), docs close + folder moves (U11), final consistency (U12).

Each **code** unit ends **build green**. Each **test** unit verifies with the `dotnet exec` assemblies runner (not `dotnet test`). Doc units (U1–U3, U10–U12) never build.

## Workflow — handoff protocol for fresh-context agents

The milestone is executed as a sequence of **sealed units** (U1–U12), one unit per fresh agent with a ~64K context window. Each unit has its own plan file:

- While in flight: `docs/plans-milestones/in-progress/group-posts-uNN-plan.md`
- **When a unit is done, its plan file is moved to `docs/plans-milestones/done/`** (this move is part of that unit's Exit).

**Shared state (three-tier contract):**
- **Primary — the design doc** (`docs/design/group-posts-design.md`, authored by U1/U2) — the exact C# of every seam U4–U9 must match, the 19 pinned test names, the gate shape, the drift-guard.
- **Secondary — this file** (`docs/plans-milestones/plan-group-posts.md`) — the unit registry with each unit's deliverables and exit criteria (and pointers to the per-unit plan files).
- **Scratch — the rolling handoff note** (`docs/plans-milestones/in-progress/group-posts-handoff-notes.md`, **created by U1**). One section per unit, appended (never rewritten); each unit writes exactly one short section before it exits; the next unit reads only that section + its own entry-read list.

**Per-unit template** (each unit plan file follows this): **Goal**; **Entry reads** (the minimal file list, 3–5 files <~300 lines each — no full-repo scan; the design-doc section cited is named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files / ~500 LOC, no misc cleanups); **Exit** (build green for touched code projects; the handoff-note entry appended *before* the folder move; the unit plan file moved to `done/`).

**Unit-series rules:** (1) a unit never modifies a file not in its own `Deliverables`; (2) never rewrites the design doc outside the §2.7 drift-guard; (3) never introduces a test whose exact name is not in the design doc §2.5 seam list; (4) never opens a *new* seam beyond the U2 pin (the two `CanSeeGroupAsync` overloads, `AccessVia.Group`, the three `PostService` group methods, `Post.GroupId`) — any other ADD is a `## U<m> — Drift pause`; (5) never re-shapes `Post`/`PostReply` outside the §2.2 pin; (6) if entry reads reveal the design doc is out of date, the unit pauses and records `## U<m> — Drift pause` in the handoff note.

## Pinned contract (directional — U2 freezes the exact C#; any mismatch is a drift pause)

```
Post.GroupId : string, default string.Empty        // non-empty ⇒ group-post lane (G·2)
AccessVia.Group                                    // 8th value (M1-Admin precedent)

// IAuthorizationService ADDs (frozen signatures untouched — ADR 0006-E lane):
Task<Decision> CanSeeGroupAsync(string actorId, string groupId);
Task<Decision> CanSeeGroupAsync(string actorId, string groupId, IDocumentSession session);
// Decision: Allow ⇒ Via Group (or Delegation), member via live GetGroupIdsAsync;
//           owner branch = owner is a member; NO Moderate, NO BreakGlass (G·4).

// PostService group surface (PostService ctor unchanged):
Task<FeedResult>        ListGroupFeedAsync(string groupId, string actorId, int page);
Task<PostDetailResult>  GetGroupPostAsync(string groupId, string postId, string actorId);
Task<Post>              CreateGroupPostAsync(GroupPostDraft draft, string actorId, IDocumentSession session);
public sealed record GroupPostDraft(string GroupId, string? Title, string Body);  // Audience written empty (G·8)
// replies: reuse existing CreateReplyAsync(postId, actorId, body, session) (G·7)
```

## Invariants (pinned for group posts — U1 writes them into the design doc §1)
- **G·1** — group post visibility = current membership (strong consistency C4); the membership lane is the *only* access decision; the audience lane is never evaluated (G·8).
- **G·2** — lane exclusivity: a post is either a group-channel post **or** a component feed entry; `GroupId` non-empty ⇒ `ComponentId` empty, excluded from `ListFeedAsync`/`ListAllFeedAsync`.
- **G·3** — members-only authoring: the create gate is the group-lane decision; non-member create denies (`UnauthorizedAccessException`; Web 404).
- **G·4** — nobody peeks: no moderator lane, **no break-glass**; a moderator/GlobalAdmin sees group posts iff they are a member (C5, ADR 0003, `how-it-works.md`).
- **G·5** — audit per lane: feed = one aggregate row; detail = one decision row; Allow **and** Deny audited; in-transaction via the session overload (C3).
- **G·6** — delegation is action-scoped (C2): in-scope `read` delegate acts with the owner's standing; out-of-scope denies.
- **G·7** — replies inherit the parent's single group-lane decision (no second evaluation, no second row — C-M3·1 analog).
- **G·8** — a group post's `Audience` is written non-null and **empty**; audience grants never apply to group posts.

## FACES (pinned for group posts — U1 writes them into the design doc §1)
| F | Statement | Invariants |
|---|---|---|
| G1 | a member sees the group feed (aggregate row) | G·1, G·5 |
| G2 | a non-member gets the empty feed + a Deny row | G·1, G·5 |
| G3 | a member added after a post sees it on the next feed (C4) | G·1, C4 |
| G4 | a member removed after a post loses it on the next detail (C4) | G·1, C4 |
| G5 | a member creates a group post and sees it; audience written empty | G·3, G·8 |
| G6 | a non-member's create is denied (service exception + deny row) | G·3 |
| G7 | a non-member moderator cannot see group posts | G·4 |
| G8 | a non-member GlobalAdmin cannot see group posts (break-glass N/A) | G·4 |
| G9 | a delegate with `read` in scope sees the owner's group posts (via Delegation) | G·6, C2 |
| G10 | a delegate without `read` in scope sees nothing | G·6, C2 |
| G11 | a reply is visible iff the parent group post is visible | G·7, C-M3·1 |
| G12 | a group post never appears in the component feed | G·2 |
| G13 | a group post never appears in `ListAllFeedAsync` ("all sections") | G·2 |

## Pinned seam tests (19 — the design doc §2.5 names are authoritative; U2 freezes, U9 implements)
`tests/Kumunita.Core.Tests/GroupPostServiceTests.cs`:
1. `G1_MemberSeesGroupFeed`
2. `G2_NonMemberFeedEmptyWithDenyRow`
3. `G3_MembershipAddReScopesNextFeed`
4. `G4_MembershipRemoveRevokesNextDetail`
5. `G5_MemberCreatesGroupPostSeesIt`
6. `G5_GroupPostAudienceWrittenEmpty`
7. `G6_NonMemberCreateDenied`
8. `G7_ModeratorNonMemberDenied`
9. `G8_BreakGlassDoesNotApplyToGroupPosts`
10. `G9_DelegateWithReadInScopeSeesOwnerGroupPosts`
11. `G10_DelegateWithoutReadDenied`
12. `G11_ReplyInheritsParentGroupLane`
13. `G11_ReplyNotEvaluatedOnParentDeny`
14. `G12_GroupPostExcludedFromComponentFeed`
15. `G13_GroupPostExcludedFromAllFeed`
16. `Feed_AggregateAuditRowShape_GroupPost`
17. `Detail_DecisionAuditRowShape_ViaGroup`
18. `Detail_DecisionAuditRowShape_ViaDelegation`
19. `PostService_MakesNoModerateOrBreakGlassCallOnGroupPosts`

Acceptance gate (U10 records, the M3 §2.5 shape): **closed loop** (a member creates a group post → it lands in their group feed; aggregate row `visibleCount ≥ 1`); **handoff** (a member added *after* the post sees it on the next feed — strong consistency; the delegate-with-`read` branch is the handoff case); **part-vs-whole** (the 19 seam tests pass together with `Kumunita.Core.Tests`).

## Units (12 total — one plan file each)

| U | Goal (one line) | Plan file (in-progress → done on exit) |
|---|---|---|
| U1 | Design doc Part 1 — context, scope, 8 invariants (G·1–G·8), 13 FACES (G1–G13); + create the handoff note | `group-posts-u01-plan.md` |
| U2 | Design doc Part 2 — the exact C# seams, 19 test names, gate shape, drift-guard | `group-posts-u02-plan.md` |
| U3 | ADR 0013 — group posts are a membership lane | `group-posts-u03-plan.md` |
| U4 | `Post.GroupId` additive field (ADR 0004 §B.1) | `group-posts-u04-plan.md` |
| U5 | Authorization group lane — `AccessVia.Group` + `CanSeeGroupAsync` ×2 + impl | `group-posts-u05-plan.md` |
| U6 | `PostService` group surface — feed / detail / create (+ `GroupPostDraft`) | `group-posts-u06-plan.md` |
| U7 | Web — `GroupsController` group-post actions + view models | `group-posts-u07-plan.md` |
| U8 | Web — `Views/Groups/` channel views + group-detail entry point | `group-posts-u08-plan.md` |
| U9 | Seam tests — the 19 pinned names, one file | `group-posts-u09-plan.md` |
| U10 | Run + record the group-posts acceptance gate | `group-posts-u10-plan.md` |
| U11 | Close — ARCHITECTURE.md / Milestones.cs / README sync + folder moves | `group-posts-u11-plan.md` |
| U12 | Final consistency check + `## Group posts — Closed (recorded)` | `group-posts-u12-plan.md` |

**Execution order is strict** U1 → U12; a unit may run against whatever the previous state is (every exit is self-verified: doc units exit by section presence; code units by a green build; test units by the recorded run).
