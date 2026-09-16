# U9 (execution) — author the 19 pinned group-post seam tests

**Register:** `docs/plans-milestones/plan-group-posts.md` · **Spec:** `group-posts-u09-plan.md`
· **Pin source:** `docs/design/group-posts-design.md` §2.5 (19 names, verbatim) + §2.4 (gate shape,
U10 consumes) · **Handoff state:** U5's lane is live (`CanSeeGroupAsync` pair +
`CanSeeGroupFeedAsync` pair, `AccessVia.Group`), U6's service surface is live
(`ListGroupFeedAsync` / `GetGroupPostAsync` / `CreateGroupPostAsync`), U7/U8 Web surface
presentation-only.

## Entry reads (done)

1. Design doc §2.5 — the 19 exact names + file name `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs`.
2. `tests/Kumunita.Core.Tests/PostServiceTests.cs` — the M3 scaffolding to mirror:
   `PostgresFixture` + `BootStoreAsync` (fresh scratch DB per test, `mt` schema,
   `KumunitaFeature` + `AuthorizationFeature` + `M1DocTypes` + `M3DocTypes`),
   the real-service `Services(store)` trio, `Plant` / `RunInSession` helpers,
   `PostAudits` audit-row query shape.
3. `tests/Kumunita.Core.Tests/UserInfoServiceGroupsU9Tests.cs` — group setup via
   `UserInfoService.CreateGroupAsync` (owner ∪ member live), `AddGroupMemberAsync` /
   `RemoveGroupMemberAsync` (C4 live lanes used by G3/G4).
4. `src/Kumunita.Core/Posts/PostService.cs` — U6 group-surface behavior (feed standalone
   aggregate call; detail standalone decision call, Deny ⇒ `Post = null`; create gate
   `IDocumentSession` overload, Deny ⇒ row committed then `UnauthorizedAccessException`;
   Allow ⇒ `ComponentId = string.Empty`, `GroupId` set, `Audience = new Audience()`).
5. `src/Kumunita.Core/Authorization/` — `AuthorizationService.DecideGroupAsync` (in-scope
   `read` grant ⇒ owner's standing, `Via = Delegation`; out-of-scope ⇒ themself,
   `Via = Delegation` still), the two row builders (decision shape: `TargetId =
   targetPostId ?? groupId`, counts null; aggregate shape: `TargetId = null`, counts),
   G·4 absence (no `Moderate`, no `AdminOverride`, no `ModeratorAssignment` on the lane).
6. Fixtures: `ModeratorAssignment` (M1 doc, `Plant`-able), `DelegationGrant` (M1 doc,
   `Plant`-able), `AdminOverride` (hand-rolled `mt` table — raw-SQL seed, the
   `AuthorizationServiceTests.SeedAdminOverrideAsync` precedent; **consumed + unexpired**
   is the usable break-glass shape).

## Deliverables (closed set — 1 new file)

- **New:** `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs`
  - The **19** `[Fact]`s, **exact names** per §2.5 (character-for-character; a rename is a
    drift event, §2.7).
  - Mirror M3's `PostServiceTests` scaffolding: same `PostgresFixture`, same
    `BootStoreAsync` (adding `M1DocTypes` + `M3DocTypes` + the two storage features),
    real `UserInfoService` + `AuthorizationService` + `PostService` trio, `Plant` /
    `RunInSession` helpers, `GroupPostAudits` query (TargetKind `"grouppost"`).
  - **G·4 fixtures:** #8 plants a `ModeratorAssignment` (denial must hold); #9 plants a
    consumed+unexpired `AdminOverride` via raw-SQL (denial must hold — break-glass is
    *not read* on the lane, so seeding it proves its absence at the decision level).
  - **#19 spy:** a private `RecordingAuthz` decorator over the **real**
    `AuthorizationService` — records `CanAsync` / `CanSeeAsync` action ids and the four
    group-lane calls; drives feed + detail + create-allow + create-deny and asserts:
    ≥ 1 group-lane call, zero audience-lane calls, zero `Moderate` anywhere.
  - **No production code change.** (The unit rule 1 closed set: this file only. If a test
    exposes a U4–U8 one-line mirror-symmetry bug, fix only that; otherwise record in the
    handoff for U10's gate.)

## Per-test intent (the §2.5 anchors)

| # | name | setup → assert |
|---|------|----------------|
| 1 | `G1_MemberSeesGroupFeed` | member (added) reads feed ⇒ `Visible` contains the post, aggregate row: `TargetKind "grouppost"`, `TargetId` null, `VisibleCount = 1`, `HiddenCount = 0`, `Action "read"`, `Via Group`, `Allow` |
| 2 | `G2_NonMemberFeedEmptyWithDenyRow` | non-member reads ⇒ `Visible` empty, `HiddenCount = 1`; row `Outcome Deny`, `Via Group`, `TargetId` null |
| 3 | `G3_MembershipAddReScopesNextFeed` | post created **before** `AddGroupMemberAsync`; the **next** feed Allow (C4 live) |
| 4 | `G4_MembershipRemoveRevokesNextDetail` | owner author; `RemoveGroupMemberAsync`; the **next** detail ⇒ `Post = null` (no owner-skip), Deny row `TargetId = postId` |
| 5 | `G5_MemberCreatesGroupPostSeesIt` | `CreateGroupPostAsync` (session) ⇒ returned post; feed contains it |
| 6 | `G5_GroupPostAudienceWrittenEmpty` | created post: `Audience` non-null, `Grants` empty, `ComponentId == ""`, `GroupId` set |
| 7 | `G6_NonMemberCreateDenied` | `Assert.ThrowsAsync<UnauthorizedAccessException>`; message contains "not a member of the group"; gate row `TargetId = groupId`, `Deny`, `Via Group` **persisted** |
| 8 | `G7_ModeratorNonMemberDenied` | `ModeratorAssignment` planted (different component) + `Roles.Moderator` claim shape ⇒ feed/detail still Deny |
| 9 | `G8_BreakGlassDoesNotApplyToGroupPosts` | consumed+unexpired `AdminOverride` row seeded (raw SQL) ⇒ feed/detail still Deny |
| 10 | `G9_DelegateWithReadInScopeSeesOwnerGroupPosts` | `DelegationGrant` (scope `["read"]`, owner = group member) ⇒ delegate feed Allow; aggregate row `Via Delegation`, `EffectivePrincipalId = owner` |
| 11 | `G10_DelegateWithoutReadDenied` | grant scope `["write"]`, delegate not a member ⇒ Deny; row `Via Delegation`, `EffectivePrincipalId = delegate` |
| 12 | `G11_ReplyInheritsParentGroupLane` | reply planted; member detail ⇒ `Replies` present; **no** reply-target row (audit rows are all `TargetId = postId` or null) |
| 13 | `G11_ReplyNotEvaluatedOnParentDeny` | reply planted; non-member detail ⇒ `Post = null`, `Replies` empty; exactly **one** row (the parent's Deny) |
| 14 | `G12_GroupPostExcludedFromComponentFeed` | group post + component post, same actor audience ⇒ component feed `Visible` has the component post **only**; no `"grouppost"` rows |
| 15 | `G13_GroupPostExcludedFromAllFeed` | same, via `ListAllFeedAsync` |
| 16 | `Feed_AggregateAuditRowShape_GroupPost` | full aggregate-row shape on a member's feed visit |
| 17 | `Detail_DecisionAuditRowShape_ViaGroup` | full decision-row shape on a member's detail visit |
| 18 | `Detail_DecisionAuditRowShape_ViaDelegation` | delegate (in-scope `read`) detail ⇒ `Via Delegation`, `EffectivePrincipalId = owner` |
| 19 | `PostService_MakesNoModerateOrBreakGlassCallOnGroupPosts` | `RecordingAuthz` spy: group surface calls **only** the group-lane methods; zero `CanAsync` / `CanSeeAsync` calls; zero `AccessAction.Moderate` |

## Exit

1. `dotnet build Kumunita.slnx -c Debug` — green.
2. `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` —
   record the run (expect the 19 new names among the prior 235; any failure is recorded
   for U10's gate, not silently fixed beyond the one-line-mirror rule).
3. Handoff note appended to `group-posts-handoff-notes.md` (before the folder move).
4. This plan file (and the spec file `group-posts-u09-plan.md`, still in `in-progress/`)
   moved to `done/` — plain file move; **nothing staged or committed**.
