# U9 — Group posts: the 19 pinned seam tests

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
Author the **19 pinned group-post seam tests** in **exactly the 19 names** frozen in the design doc §2.5, mirroring M3's test-file setup (`ComponentFeedSeamTests` / `PostDetailSeamTests` / `GroupServiceTests` scaffolding). **This unit writes tests only** — if a test exposes a production bug from U4–U8, fix it **only** if it is a one-line mirror-symmetry bug within the U4–U8 deliverables; otherwise leave the test failing and record it in the handoff so U10's gate catches it (and a `## U9 — Drift pause` note if it looks like the design doc is stale, unit-series rule 6).

## Entry reads
1. `docs/design/group-posts-design.md` §2.5 (the **exact** 19 names + the test file name) — the literal pin
2. `tests/Kumunita.Core.Tests/UserInfoServiceGroupsU9Tests.cs` (+ `UserInfoServiceGroupPrivacyTests.cs`) — the M2 group/membership scaffolding (`Group`/`GroupMembership`/`GetGroupIdsAsync`) to **reuse**; `ls tests/Kumunita.Core.Tests/` is authoritative if the name differs — the point is the member-scoping + group setup, not the file name
3. `tests/Kumunita.Core.Tests/PostServiceTests.cs` (the M3 lane + `Visible`/`HiddenCount` + aggregate-audit-row shapes to mirror) and `tests/Kumunita.Core.Tests/AuthorizationServiceTests.cs` (the mock `IAuthorizationService` + `IUserInfoService` seams + the `AccessVia` invocation records)
4. `src/Kumunita.Core/Posts/PostService.cs` — **grep** the U6 group-surface signatures (`ListGroupFeedAsync`, `GetGroupPostAsync`, `CreateGroupPostAsync`) + the `CreatePostAsync` gate (the G·3 exception type)
5. `src/Kumunita.Core/Authorization/AuthorizationService.cs` — **grep** `CanSeeGroupAsync` (U5's group-lane impl — the audit-row shape to assert); plus `tests/Kumunita.Core.Tests/` for the test-runner convention (xunit v3 style, and the in-process runner per `AGENTS.md`)

## Deliverables (1 file)
- **New:** `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs` — the 19 tests, **exact names** per §2.5, each asserting the FACES pin named in the master register's FACES table (G1–G13). The per-test assertions below are the **intent**; the §2.5 names are the **literal pin** and must match character-for-character.
  - **G1–G4 (membership = lane; C4 strong consistency):** `G1_MemberSeesGroupFeed` (Allow: `Visible` non-empty, the aggregate audit row present, `HiddenCount` per §2.1); `G2_NonMemberFeedEmptyWithDenyRow` (Deny: `Visible` empty **and** the deny audit row — G·5); `G3_MembershipAddReScopesNextFeed` (member added *after* a post sees it on the **next** feed — no projection lag); `G4_MembershipRemoveRevokesNextDetail` (member removed ⇒ the very **next** detail load is Deny/403).
  - **G5–G6 (members-only authoring; G·3 + G·8):** `G5_MemberCreatesGroupPostSeesIt` (create ⇒ stored ⇒ feed includes it); `G5_GroupPostAudienceWrittenEmpty` (stored `Post.Audience` is **non-null empty**); `G6_NonMemberCreateDenied` (the **exact** `UnauthorizedAccessException` + message U6 pinned, **and** a deny audit row).
  - **G7–G8 (G·4 — nobody peeks):** `G7_ModeratorNonMemberDenied` (a `read`-scoped non-member moderator → the group lane is membership-only; the moderator standing does **not** grant); `G8_BreakGlassDoesNotApplyToGroupPosts` (a non-member GlobalAdmin ⇒ Deny — break-glass off, the strictest privacy lane).
  - **G9–G10 (delegation, G·6):** `G9_DelegateWithReadInScopeSeesOwnerGroupPosts` (in-scope `read` ⇒ the delegate acts with the owner's standing; `Via` = `Delegation`); `G10_DelegateWithoutReadDenied` (out-of-scope delegate denies; the scope guard is real).
  - **G11 (replies inherit, G·7):** `G11_ReplyInheritsParentGroupLane` (a reply is reachable iff the parent group post is visible — single lane, no second evaluation); `G11_ReplyNotEvaluatedOnParentDeny` (if the parent is Deny, the reply is **not evaluated** — no second audience/lane pass; assert the `IAuthorizationService` call count or the audit-row count).
  - **G12–G13 (lane exclusivity, G·2):** `G12_GroupPostExcludedFromComponentFeed`, `G13_GroupPostExcludedFromAllFeed` (a `GroupId`-set post is **absent** from `PostService.ListFeedAsync` **and** `ListAllFeedAsync` `Visible`).
  - **Audit-row shapes (G·5):** `Feed_AggregateAuditRowShape_GroupPost` (TargetKind `"grouppost"`, TargetId null, `visibleCount`/`hiddenCount` present per §2.1), `Detail_DecisionAuditRowShape_ViaGroup` (detail row TargetId = post id, `Via` = `Group`), `Detail_DecisionAuditRowShape_ViaDelegation` (delegate branch: `Via` = `Delegation`).
  - **Lane-exception guard (G·4 enforcement):** `PostService_MakesNoModerateOrBreakGlassCallOnGroupPosts` (the `PostService` group surface makes **no** `AccessAction.Moderate` call and **no** break-glass path for group posts — assert on the `IAuthorizationService` mock / call record).

## Exit
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U9
