# M1 Step 4 — UserInfoModule implementation

## Context

Steps 1–3 of M1 are complete (per `docs/plans-milestones/plan-m1-implement-identity,-groups,-delegation,-authorization.md`):
- ADR 0006 interfaces materialized (`IUserInfoService` fully sketched at `src/Kumunita.Core/UserInfo/IUserInfoService.cs`, 89 lines).
- M1 document POCOs land next to `Kumunita.Core/UserInfo/` and `Kumunita.Core/Authorization/` (`Profile`, `Group`, `GroupMembership`, `DelegationGrant`, `AccessAudit`, `AccessAction`, `IAuditableResource`, `Audience`); `M1DocTypes.cs` pins the non-default Marten conventions (e.g. `Profile`'s identity is `SubjectId`).
- M0's `KumunitaFeature` + Marten 9 `FeatureSchemaBase` pattern already in use; M1's `mt` tables are versioned through the same path.
- `tests/Kumunita.Core.Tests/PostgresFixture.cs` hands each test a fresh scratch Postgres DB.

Step 4 is the concrete `UserInfoService` behind `IUserInfoService`. It is the strong-consistency storage that Step 5 (Authorization `MatchGroups`) reads directly on every decision (invariant C4), that Step 6 (Identity lifecycle) calls from signup/seed-admin/profile-bootstrap (invariant C1), and that Step 8 (`/admin`) calls from the roles/scope surface (invariant C5).

## Files

- **new** `docs/plans-milestones/plan-m1-step-4-userinfomodule.md` — this plan document, saved at the outset for the record (user request; matches the existing milestone-plan naming in that folder).
- **new** `src/Kumunita.Core/UserInfo/UserInfoService.cs` — concrete `IUserInfoService`; 12 methods; one Marten session per mutating call, ending in a single `SaveChangesAsync` so the domain write and any accompanying `AccessAudit` row commit atomically.
- **create-or-extend** `src/Kumunita.Core/DependencyInjection.cs` — `AddKumunitaCore(this IServiceCollection)` (or similar) registering `IUserInfoService`. If step 3 already introduced a DI extension, add the registration there.
- **extend** `src/Kumunita.Web/Program.cs` — call the extension at startup so Web (DI composition root) can resolve the service (ADR 0006-D: Core has no HTTP types, so Web registers the service).
- **new** `tests/Kumunita.Core.Tests/UserInfoServiceTests.cs` — one handler test per mutating method plus the read-path tests; uses `PostgresFixture` and bootstraps Marten against a fresh scratch DB per test (mirrors M0's `KumunitaFeatureDdlTests` pattern).

## Approach

A single `sealed class UserInfoService : IUserInfoService` with an `IDocumentStore` constructor. Each method is:

- **Read** — direct `IDocumentStore` query against the *live* rows (no projection, no cache — invariant C4).
- **Mutate** — load-or-create in one session, apply the change, append the `AccessAudit` row (where the design doc's admin-action lane requires it), end with a single `SaveChangesAsync`.

The admin-action audit lane (from the design's "Seams & contracts" — "admin actions (role change, scope change, break-glass consumption) append `Via`-tagged audit rows"): `CreateGroupAsync`, `AddGroupMemberAsync`, `RemoveGroupMemberAsync`, `GrantDelegationAsync`, `RevokeDelegationAsync`, `SetComponentModeratorAccessAsync` all append an `AccessAudit` row. Idempotent bootstrap (`SeedComponentsAsync`) and the profile upsert (`UpsertProfileAsync`) do not — they are not access decisions.

## `AccessVia` derivation (needs an explicit call out)

The frozen signatures don't carry a `via` parameter, but the interface doc comments name `AccessVia.Admin` or `AccessVia.Owner` depending on "whoever grants / who added." The service must derive:

- **`AddGroupMemberAsync` / `RemoveGroupMemberAsync`** — the service loads the group's `OwnerId` in the same session. If the named actor (`addedBy` / `removedBy`) equals `OwnerId` → `Via = Owner`; else → `Via = Admin`.
- **`GrantDelegationAsync` / `RevokeDelegationAsync`** — interface doc already names only `ownerId` and `revokedBy`. Interpret the grantor's identity as: if `ownerId` (in grant; `revokedBy` in revoke) is the *grantee's* standing-bearer, `Via = Owner`; otherwise `Via = Admin`. In step 4 the only two callers we can actually predict are the grant-owner page (Owner) or a GlobalAdmin surface (Admin), so the service uses a simple rule: the *actor* for `GrantDelegationAsync` is recorded as `ownerId`; for `RevokeDelegationAsync` the actor is `revokedBy`. Record `Via = Owner` when the actor equals the grant's `OwnerId`, else `Via = Admin` (which requires one extra session load — acceptable for a rare admin action).
- **`SetComponentModeratorAccessAsync`** — interface doc pins `Via = Admin` (only a GlobalAdmin reaches it); record `Via = Admin` unambiguously.

If step 6 or step 8 surfaces a case where the derivation is wrong (e.g., a GlobalAdmin delegates on the owner's behalf), we add an *overload* that takes explicit `AccessVia` (ADR 0006-E compatible lane — non-breaking) rather than changing the frozen signature.

## `SeedComponentsAsync` upsert-by-key note

Interface doc: "upsert by `key` / `Component.Id`." Marten's default identity convention picks the `Id` property (or whatever `M1DocTypes` pins). If the `Component` POCO's `Id` is a guid, the upsert must run *read-then-decide* on the `Key` field (stable per component: Safety, Maintenance, Social, Governance), not on `Id`. Implementation detail is left to this step.

## Invariants this step is accountable for

- **C4 — strong consistency.** `GetGroupIdsAsync` and `GetActiveGrantAsync` read the live rows at call time; a change is live on the very next call. Pinned by the "membership-change-next-request" handler tests.
- **C5 — `ModeratorAccess` off by default.** `SeedComponentsAsync` creates all four components with `ModeratorAccess = false`; idempotent re-run leaves it `false`; only `SetComponentModeratorAccessAsync` flips it.
- **C3 — audit same-transaction (admin-action lane).** Every mutating method above appends its `AccessAudit` row in the *same* Marten session (single `SaveChangesAsync`) as the domain write. A failed save rolls back both — proven by the "both rows present after save" asserts in each handler test.

## Method-by-method sketch

| Method | Session shape | Audit row? |
|---|---|---|
| `GetProfileAsync` | `Document<Profile>(subjectId)` | — |
| `GetGroupIdsAsync` | `Query<GroupMembership>().Where(m => m.UserId == userId).Select(m => m.GroupId)` → `HashSet<string>` | — |
| `GetActiveGrantAsync` | `Query<DelegationGrant>().Where(g => g.DelegateId == delegateId)` → first where `IsActiveAt(delegateId, now)`; null if none | — |
| `CreateGroupAsync` | new `Group` (guid) + `GroupMembership(owner→owner)`, one `SaveChangesAsync` | optional — the design says "admin actions append audit"; group creation is arguably an admin action but the doc doesn't demand it for this one specifically; keep to the six above |
| `AddGroupMemberAsync` | upsert `GroupMembership` by (Group,User); append `AccessAudit` (action "group.add-member", `Via` per rule, `TargetType = "group"`); one `SaveChangesAsync` | yes |
| `RemoveGroupMemberAsync` | delete `GroupMembership` by (Group,User); append `AccessAudit` (action "group.remove-member", `Via` per rule, `TargetType = "group"`, `Actor = removedBy`); one `SaveChangesAsync` | yes |
| `GrantDelegationAsync` | new `DelegationGrant`; append `AccessAudit` (action "delegation.grant", `Via` per rule, `TargetType = "delegation_grant"`); one `SaveChangesAsync` | yes |
| `RevokeDelegationAsync` | load by `grantId`, set `RevokedBy = revokedBy`, keep row (history); append `AccessAudit` (action "delegation.revoke", `Via` per rule); one `SaveChangesAsync` | yes |
| `SeedComponentsAsync` | upsert 4 known components by key, `ModeratorAccess = false` (C5), idempotent | no |
| `SetComponentModeratorAccessAsync` | flip flag; append `AccessAudit` (action "moderator-access", `Via = Admin`, `TargetType = "component"`); one `SaveChangesAsync` | yes |
| `GetAssignmentsAsync` | `Query<ModeratorAssignment>().Where(a => a.UserId == userId)` | — |
| `UpsertProfileAsync` | apply non-null patch fields to existing (or new) `Profile`; one `SaveChangesAsync` | no (called from step 6 Identity lifecycle and step 8 bootstrap surface — not an access decision) |

## Tests (`tests/Kumunita.Core.Tests/UserInfoServiceTests.cs`)

Each test: `PostgresFixture.NewDatabaseAsync()` → fresh scratch DB → bootstrap Marten with `KumunitaFeature` + M1 feature + `M1DocTypes` conventions → construct `UserInfoService` → run → assert → `await store.DisposeAsync()`.

**Read path (3 tests):**
1. `GetProfileAsync_RoundTrip_PreservesSubjectIdAndVisibility` — upsert via the service's own `UpsertProfileAsync`, read back; assert `SubjectId`, `DisplayName`, `Verified`, `Visibility.Mode/Grants`.
2. `GetGroupIdsAsync_LiveMembership_C4_StrongConsistency` — create groups A and B with owner O; add O to A (`AddGroupMemberAsync`); the *very next* `GetGroupIdsAsync(O)` includes A and only A; remove O from A; the *very next* call drops A. No cache, no polling, no projection — the live row is the truth.
3. `GetActiveGrantAsync_ReturnsOnlyTheActiveGrant` — three grants for the same delegate: one within `[From, To]`, one expired (past `To`), one revoked. The call returns only the within-window one; for a delegate with only expired+revoked grants, returns null.

**Mutating (one handler test each, 7 tests):**
4. `CreateGroupAsync_OwnerIsMemberInTheSameTransaction` — after `SaveChangesAsync` both `Group` and the owner's `GroupMembership` row exist; `GetGroupIdsAsync(owner)` contains the group id.
5. `AddGroupMemberAsync_LiveOnNextCall_AndAuditRowPresent` — add member to a group with an owner; next `GetGroupIdsAsync(member)` contains it; an `AccessAudit` row exists with `Action = "group.add-member"`, `Outcome = Allowed`, `Via = Owner` (when actor == owner) or `Via = Admin` (when actor != owner), `TargetType = "group"`, both committed together with the membership row.
6. `RemoveGroupMemberAsync_LiveOnNextCall_AndAuditRowPresent` — symmetric to 5; next `GetGroupIdsAsync(member)` drops the group; `AccessAudit` row present.
7. `GrantDelegationAsync_PersistsWindowAndScope_AccessAuditPresent` — assert `From`/`To`/`Scope` persist; `IsActiveAt(delegateId, From)` is true; `IsActiveAt(delegateId, now after To)` is false (when `To` is set); `AccessAudit` row present with the via per the rule.
8. `RevokeDelegationAsync_ClosesGrant_HistoryPreservedAuditPresent` — granted → `IsActiveAt` true; revoke → `IsActiveAt` false for every `now`; the previous grant row is kept (no delete); two audit rows (grant + revoke).
9. `SeedComponentsAsync_CreatesFour_AllModeratorAccessFalse_IdempotentReRunDoesNotFlipFlag` — first call: 4 rows, all `ModeratorAccess = false`; second call: still 4 rows, still `false` (C5 invariant pinned).
10. `SetComponentModeratorAccessAsync_FlipsFlag_AuditViaAdminTargetComponent` — flip to `true` (and back); assert the flag, and the audit row with `Via = Admin`, `Action = "moderator-access"`, `TargetType = "component"`, `Actor = actorId`.

**Lifecycle (1 test):**
11. `UpsertProfileAsync_CreatesWhenAbsent_LeavesNullPatchFieldsUntouched` — create a profile; upsert with a patch that sets `DisplayName` and leaves `Email` null; assert `DisplayName` changed, `Email` unchanged; create-when-absent case: upsert a new `SubjectId` produces a fresh row.

Total: 11 tests (one per mutating method + the C5 idempotent re-run as its own test since that's the design doc's acceptance gate for this flag).

## Acceptance (end-of-step)

- `dotnet build` at `Kumunita.slnx` → 0 errors, 0 new warnings.
- All 11 tests in `UserInfoServiceTests` pass against fresh scratch DBs (no test clobbers another).
- **C4 proof** — the "membership flips on the very next call" tests (2, 5, 6) show live reads.
- **C5 proof** — test 9 pins `ModeratorAccess = false` through an idempotent re-run.
- **C3 proof** — each mutating test asserts both the domain row and the `AccessAudit` row are present *after* the service returns; the two rows are written in the same session/`SaveChangesAsync`.
- Update `docs/plans-milestones/plan-m1-implement-identity,-groups,-delegation,-authorization.md` step-4 line (checkmark / status) once green.

## Risks

- **`Via` derivation is a service-level interpretation.** If step 6 (identity) or step 8 (`/admin`) surfaces a case where the owner-vs-admin rule misbehaves (e.g., a GlobalAdmin grants a delegation on the owner's behalf), we add an *overload* with an explicit `Via` parameter (ADR 0006-E compatible lane, non-breaking) rather than changing the frozen signature.
- **`UpsertProfileAsync` dual-argument semantics.** The signature takes both a full `Profile` and a `ProfileUpdate` patch. Interpretation here: `patch` fields (non-null) take priority; `profile` fills gaps and is the identity/context. If the intent was "patch is the update, profile is the pre-update snapshot," no observable difference as long as `profile.SubjectId` and the document's identity agree.
- **`SeedComponentsAsync` upsert-by-key.** If `Component`'s `Id` is a guid, "upsert by key" means the service must *read-then-decide* on `Key`, not `Id`. This is an implementation detail this step owns; the invariant C5 default pins the observable behavior.
- **Test contamination.** `PostgresFixture` gives a fresh DB per test; each handler test must create its own `IDocumentStore`, apply the M0+M1 features, and dispose. Follow the M0 `KumunitaFeatureDdlTests` template.

## Implementation Steps

1. **Save the plan document** — create `docs/plans-milestones/plan-m1-step-4-userinfomodule.md` containing this plan verbatim (title, context, approach, sketches, tests, acceptance, risks, steps) — user-requested record; matches the folder's existing `plan-m1-*` naming.
2. **Scaffold the service + DI.** Create `src/Kumunita.Core/UserInfo/UserInfoService.cs` as a `sealed class UserInfoService : IUserInfoService` taking `IDocumentStore`. Add-or-extend the DI extension that registers `IUserInfoService` (new `src/Kumunita.Core/DependencyInjection.cs` if step 3 didn't introduce one). Wire it in `src/Kumunita.Web/Program.cs`.
3. **Implement the three read paths** (`GetProfileAsync`, `GetGroupIdsAsync`, `GetActiveGrantAsync`) and add tests 1–3 above. Build → test → green.
4. **Implement the group lifecycle** (`CreateGroupAsync`, `AddGroupMemberAsync`, `RemoveGroupMemberAsync`), each in one Marten session ending in a single `SaveChangesAsync`; add/remove append their `AccessAudit` row with `Via` per the derivation rule. Add tests 4–6. Build → test → green.
5. **Implement delegation** (`GrantDelegationAsync`, `RevokeDelegationAsync`), one session each; both append `AccessAudit` rows with `Via` per the rule. Add tests 7–8. Build → test → green.
6. **Implement components + profile upsert** — `SeedComponentsAsync` (read-then-decide upsert by key, `ModeratorAccess = false` per C5), `SetComponentModeratorAccessAsync` (audit via Admin, Target "component"), `GetAssignmentsAsync`, `UpsertProfileAsync` (non-null patch takes priority). Add tests 9–11. Build → test → green.
7. **Final acceptance.** Run the full `tests/Kumunita.Core.Tests` project via the repo's `.vscode/tasks.json` test task; confirm no warning/error in `dotnet build`; update the status of step 4 in `docs/plans-milestones/plan-m1-implement-identity,-groups,-delegation,-authorization.md` (checkmark / "complete — 11/11 handler tests green"); note the `AccessVia` derivation in the step-4 doc entry so step 6/7 authors can confirm or flag it (ADR 0006-E lane if a change is needed).
