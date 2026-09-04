## U1 — design doc Part 1
- Wrote `docs/design/m3-posts-design.md` Part 1: Context, Scope (in-scope
  list + explicit **M3b deferral** block as M3's M1-style close
  ["Out of scope — M3b deferral (this section is M3's M1-style close)"]),
  Invariants (table, 11 rows), FACES (table, 10 rows), Drift-guard & change
  policy, and an explicit **"## M3 — Closed (recorded)"** placeholder at the
  end (empty by design — U10 / U11 / U12 will append).
- **Invariants pinned in Part 1 (11, by id):**
  - M3-owned: **C-M3·1** (reply inherits the parent's single `Read`
    decision; no separate `Audience` on `PostReply`; no second authz call),
    **C-M3·2** (`Component` is a candidate filter / feed organizer — never an
    access decision; no own `AccessAudit` row), **C-M3·3** (audit shape:
    feed = one aggregate `AccessAudit` row with `targetKind "post"`; detail
    = one decision row; reply-visibility is not its own row; same-transaction
    commit via the `IDocumentSession` overloads).
  - ADR 0006 (frozen, M3 is a caller): **C1** (empty audience denies — with
    the owner-branch exception; `Post.Audience` required non-null), **C2** (
    delegation action-scoped), **C3** (audit always on — Allow *and* Deny),
    **C4** (strong-consistency membership resolution; live documents),
    **C5** (moderator default-OFF — M3 adds **no** `Moderate`-on-post
    branch; `Via = Report` stays dormant; tests assert the **absence** at
    F3/F8), **C6** (one matching pass — `CanAsync` and `CanSeeAsync` share
    `MatchGroups`).
  - Companion ADRs (not re-pinned, but the code must not violate):
    **ADR 0001-B** (author's choice is absolute; the composer writes the
    chosen audience verbatim), **ADR 0003 §Separation of duties** (M3's
    post/reply write surface is author-scoped; delegate posts with
    `Via = Delegation`; no `/admin` change in M3), **ADR 0006-D** (
    dependency direction; `PostService` never reads `GroupMembership` /
    `DelegationGrant` for its own access decisions; `Kumunita.Web` is a
    thin controller), **ADR 0006-E** (compatible-lane ADD = the single M3
    seam `IUserInfoService.GetComponentsAsync(bool enabledOnly)`).
- **FACES pinned in Part 1 (10, F1–F10):** F1 (feed shows only what I may
  read; aggregate hidden count), F2 (non-member sees no trace of a hidden
  post), F3 (empty-audience post denies from **all** incl. moderators),
  F4 (empty-audience post allows the author — owner branch), F5 (group
  member added after the post sees it on the next request), F6 (delegate
  with `Read` in scope inherits owner standing), F7 (delegate without `Read`
  in scope sees nothing), F8 (`Component` is a feed organizer, never a
  gate — no moderator peek), F9 (candidate-filter query emits **no**
  `AccessAudit` row), F10 (reply visible iff parent visible; parent
  denies ⇒ reply *not evaluated*, no own row).
- **Single M3 ADD on a frozen interface (ADR 0006-E lane, mirrors M2's
  `GetProfilesAsync`):**
  `Task<IReadOnlyList<Component>> IUserInfoService.GetComponentsAsync(bool
  enabledOnly)` — the composer's *component picker*, the `/community/{id}`
  *grouping*, and the feed's *candidate filter*. Doc-comment (U2 will pin
  verbatim in §2.1): **returns a *candidate* set; C-M3·2 says never a
  visible set; C-M3·2 says no own `AccessAudit` row; C-M3·3 says the row
  shape is fixed to feed aggregate + detail decision.**
- **Storage registration (M3DocTypes step):**
  `M3DocTypes.Configure(StoreOptions)` — new parallel document surface
  analogous to `M1DocTypes`.
  (the four components are already seeded in M1's `FirstBootSeeder`).
  **No hand-rolled `FeatureSchemaBase` in M3** (the carve-out is for
  operator-written tables like `AdminOverride`); U1 records the veto that
  U2 may flip before §2.2 is pinned.
- **Document register (pinned by Part 1, shapes pinned by Part 2 §2.2):**
  `Post` (ns `Kumunita.Core.Posts`), `PostReply`, `Report` (registered for
  forward compat; **dormant — no surface, no workflow, no tests in M3**;
  the *flow* is M3b's). `PostToAuditableResource.TargetKind = "post"`.
- **M3b deferral list (this section is M3's "out of scope" close, mirroring
  M2 §1):**
  - Report workflow: **file / assign / unlock / resolve** (the C5
    carve-out that M3's "absence" tests assert at F3/F8).
  - The **`Via = Report` read branch** on a post (moderator sees a
    previously-invisible post *through* a filed report).
  - **Moderator surfaces** — the queue, the resolve UI, the "assign to a
    moderator" form. `/admin` (M1's admin surface) is **unchanged** in M3.
  - The post **`Status`** field (hidden / removed) and the M3b removal
    path. M3's post has no `Status` column.
- **Plan-count note (recorded, U1):** the plan § U1 headline says "12
  invariants" but the body's bullet list enumerates **11**. This Part 1
  pins the 11 from the body (the body is authoritative). U2 owns the final
  count in the seam-test list (§2.5) and the acceptance gate (§2.6) — if
  the intended invariant count was 12 (e.g., a C-M3·4 that did not make it
  into U1's body bullets), U2 will surface it in §2.5 / §2.6. **Not a
  pinned-invariant drift** — a plan-documentation slip recorded here for the
  U2 review.
- **No code. No build.** — design-only unit, per the plan.
- **U2, your job:** append `## Seams & contracts (Part 2, written by U2)`
  at the end of `docs/design/m3-posts-design.md` (above the
  "## M3 — Closed (recorded)" placeholder — keep that section last):
  §2.1 frozen seam list (exact C# — verbatim quotes of
  `IAuthorizationService`, `IAuditableResource`, `Audience` / `AudienceGrant`
  / `AudienceMode`, `Decision` / `VisibleSet`, the M1 frozen `IUserInfoService`
  surface **plus** the single M3 ADD pinned above), §2.2 new M3-owned Core
  types (exact C# for `Post`, `PostReply`, `Report`,
  `PostToAuditableResource`, `PostService` ctor + public methods,
  `M3DocTypes.Configure` signature)
  (unauth ⇒ empty set; verified ⇒ `GetComponentsAsync(true)` candidate set;
  unverified ⇒ own component only or empty (U2 decision); moderator ⇒ same
  as verified; missing component ⇒ empty), §2.4 reply-inherits rule (C-M3
  ·1) — 4-shape table (parent Allow ⇒ replies rendered; parent Deny ⇒
  replies not evaluated/no own row; empty-audience parent ⇒ owner branch
  only; explicit `All`+empty ⇒ deny), §2.5 **seam-test names** (pinned test
  method names in `Kumunita.Core.Tests/PostServiceTests.cs`, each carrying
  its FACES row and invariant), §2.6 **acceptance gate** (three-test shape:
  closed-loop / handoff / part-vs-whole, named), §2.7 **drift-guard**
  (the test list, the ADD signature, the 4 reply-inherits shapes, the 5
  candidate-filter states, FACES count, and the invariant ids are frozen
  pins).
- **Drift-guard from Part 1 (repeats into U2's §2.7):** the invariant
  *numbers* (C-M3·1/2/3, ADR 0006 C1..C6, ADR 0001-B, ADR 0004 §B.1) are
  stable for the rest of M3; rename / renumber is a breaking change.
  Adding a new FACES row (F11+) is only by the unit that ships the outcome
  it pins, in the same commit as the feature; FACES count is a handoff
  field (U1 → U2, and forward).

## U2 — design doc Part 2
- Appended `## Seams & contracts (Part 2, written by U2)` to
  `docs/design/m3-posts-design.md` **above** the `## M3 — Closed (recorded)`
  placeholder (that section stays last), with §2.0 preamble through §2.6
  drift-guard — mirroring M2's §2.1–§2.7 structure. **No code. No build.**
- **(a) The single M3 ADD (sealed, §2.1):**
  `Task<IReadOnlyList<Component>> IUserInfoService.GetComponentsAsync(bool enabledOnly)`
  — the composer's *component picker* + `/community/{id}` *grouping* +
  feed's *candidate filter*. Doc-comment pins: candidate set (never a
  visible set — C-M3·2), no `AccessAudit` row (C-M3·2), strong-consistency
  live rows (C4). ADR 0006-E compatible lane; precedent: M2's
  `GetProfilesAsync(bool)` (U3). **FROZEN** in §2.6 once U4 lands them.
- **(b) 18 pinned seam-test names (§2.4, `tests/Kumunita.Core.Tests/PostServiceTests.cs`):**
  `F1_FeedVisibleToAudienceMember` · `F2_FeedHiddenFromNonMember` ·
  `F3_FeedDeniesModeratorOnAudiencePost` · `F4_EmptyAudiencePostAuthorSeesOwnDraft` ·
  `F4_EmptyAudiencePostDeniesNonAuthor` · `F5_MembershipChangeReScopesNextRequest` ·
  `F6_DelegateWithReadInScopeSeesAuthorPost` · `F7_DelegateWithoutReadDenies` ·
  `F8_ComponentIsFilterNotAccessGate` · `F9_CandidateFilterEmitsNoAuditRow` ·
  `F10_ReplyVisibleIffParentVisible` · `F10_ReplyNotEvaluatedOnParentDeny` ·
  `Feed_AggregateAuditRowShape` · `Detail_DecisionAuditRowShape_ViaOwner` ·
  `Detail_DecisionAuditRowShape_ViaAudience` · `Detail_DecisionAuditRowShape_ViaDelegation` ·
  `AuthorAudienceWrittenVerbatim` · `PostService_MakesNoModerateCall`.
- **(c) Three-test acceptance gate (§2.5, U10 records):** (1) **closed
  loop** (author's post appears in their own feed next request; aggregate
  `AccessAudit` row `VisibleCount ≥ 1` + `TargetKind = "post"`), (2)
  **handoff** (a group member added after the post sees it on the next
  request — C4 strong consistency; the *delegate* branch is the "handoff to
  a delegate" case — C2), (3) **part-vs-whole** (the 18-test list is the
  whole, closed-loop + handoff are the parts; all must pass together,
  plus the M1-inherited + M2-inherited anchors re-run unchanged).
- **(d) `Report` table-in-M3 / flow-in-M3b flag (§2.2 + §2.6):** `Report`
  is registered in M3 (`M3DocTypes.Configure(StoreOptions)`, U3 lands
  this) as a *dormant* table — 7 fields
  `(Id, PostId, ReporterId, ComponentId?, Reason?, Status?, At)`,
  `Status` **nullable** (M3b's write lane sets it), **no** surface /
  workflow / tests in M3. The *flow* (file / assign / unlock /
  resolve), the *`Via = Report`* read branch, and the *moderator
  surfaces* are **M3b's** (the Part 1 "Out of scope — M3b deferral" block
  is M3's M1-style close; U11 flips `ARCHITECTURE.md` §2 and writes the
  handoff-note close).
- **(e) Count reconciliation (12 vs 11):** U2 confirms **11 invariants**
  (the plan's "12" headline is a documentation slip — U1's handoff records
  it; the body list is authoritative). U2's §2.4 pins **18 test names**
  (not a smaller/larger set). Part 2's *frozen counts* are: **11**
  invariants + **10** FACES + **18** test names + **2 tables** (5-row
  candidate-filter + 4-shape reply-inherits) + **3 records**
  (`FeedResult` / `PostDetailResult` / `PostDraft`) + **4** `PostService`
  public methods + **6-member** adapter + **3 POCOs**
  (`Post` / `PostReply` / `Report`). All frozen in §2.6; any mismatch is a
  `## U<m> — Drift pause`.
- **(f) Unit-owner references in Part 2 follow the plan register:**
  U3 = `Post/PostReply/Report` + `M3DocTypes`; U4 = `GetComponentsAsync`
  + `F9` unit-test; U5 = `PostToAuditableResource`; U6 = `PostService` +
  records + the 4 methods; U7 = `PostsController` + VMs; U8 = Razor views;
  U9 = the 18-test file; U10 = the gate record; U11/U12 = the close.
  If a later unit discovers the plan register and Part 2 mismatch, the
  Part 2 text wins (the drift-guard lane, §2.6).
- **No code, no build** (design-only unit, per the plan). U3 is next.

## U3 — documents + M3DocTypes + boot
- **(a) `M3DocTypes.cs` line count:** 3 `.Schema.For` calls — `Post`, `PostReply`, `Report` (all conventional `string Id`; no `Identity(...)`, no `UniqueIndex(...)` — Marten defaults apply; the `(PostId, Status)` business-key index is deferred to M3b when it owns the report write lane).
- **(b) Boot-path lines added:** 1 line in one file — `src/Kumunita.Web/Program.cs` **line 72** (`M3DocTypes.Configure(opts);`, immediately after `M1DocTypes.Configure(opts)` at line 67, inside the `AddMarten(opts => { … })` lambda). **Deviation from the plan register:** plan U3 Deliverables listed two boot-path insertions — `SchemaBootstrap.cs` + `Program.cs` — "the dev loop in Program.cs and the all-env SchemaBootstrap". Actual M1 precedent (verified in the codebase): `M1DocTypes.Configure(opts)` is called **exactly once**, in `Program.cs`'s `AddMarten` lambda. `SchemaBootstrap.ApplyAsync` does **not** call `M1DocTypes.Configure` — it only calls `store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync()`, which *consumes* every stored `Schema.For<T>`/`Storage.Add<T>` registration (including `M1DocTypes`'s) in all environments. The design doc §2.2 prose "Wired by U3 into both boot paths: SchemaBootstrap.cs (called from ApplyAllConfiguredChangesToDatabaseAsync) and Program.cs" conflates the *registration* call (which belongs in the `AddMarten` lambda) with the *apply* call (which is environment-agnostic and already in `SchemaBootstrap`). M3 follows the same single-registration shape as M1 — the all-env apply path picks it up automatically, so no edit to `SchemaBootstrap.cs` is needed. Recorded here, not a drift-pause (M1's precedent + the actual code are authoritative over the plan-register misdescription; the frozen §2.6 pins are the `M3DocTypes` type and its `Configure(StoreOptions)` signature — both are preserved verbatim).
- **(c) `Report` registered, `Status` nullable, no index, no seed:** table-in-M3 / flow-in-M3b (design doc §2.2 + §2.6 flag). No write lane, no surface, no tests in M3 — U3's POCO is the *registration artifact only*; the M3b u
- **(d) Compile warnings on new POCOs:** none. Both `Kumunita.Core` and `Kumunita.Web` build green with zero warnings on the four new/modified files (`src/Kumunita.Core/Posts/Post.cs`, `PostReply.cs`, `Report.cs`, `src/Kumunita.Core/M3DocTypes.cs`, `src/Kumunita.Web/Program.cs`).

## U4 — GetComponentsAsync + F9

- **(a) `IUserInfoService` public-method count: 16 actual; plan-register says 15, flag raised.** Physical count from `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (post-U4): 12 M1 (GetProfileAsync, GetGroupIdsAsync, GetActiveGrantAsync, CreateGroupAsync, AddGroupMemberAsync, RemoveGroupMemberAsync, GrantDelegationAsync, RevokeDelegationAsync, UpsertProfileAsync, SeedComponentsAsync, SetComponentModeratorAccessAsync, GetAssignmentsAsync — 8 core + 4 lifecycle) + 3 M2 (GetProfilesAsync, GetGroupsForUserAsync, GetGroupMembersAsync) + 1 M3 (GetComponentsAsync) = **16** total. The plan register's headline "M1's 11 + M2's 3 + M3's 1 = 15" undercounts M1 by 1 (same slip as M2's U3, which recorded "11 M1 methods + 1 M2 ADD = 12" where M1 was actually 12). U1's convention (flag plan-documentation slips in the handoff note; body list is authoritative; not a drift-pause per §2.6) applies: **16 is the accurate count**, 15 is a plan-documentation slip for U10/U11 to reconcile. The frozen §2.1 / §2.6 pin is the *signature* `Task<IReadOnlyList<Component>> GetComponentsAsync(bool enabledOnly)` — only the count prose is misstated.

- **(b) F9 test name + audit-row assertion:** `GetComponentsAsync_CandidateFilterEmitsNoAuditRow` (exact F9 seam name from the §2.4 registry, `tests/Kumunita.Core.Tests/UserInfoServiceTests.cs`). Asserts: (i) `enabledOnly: true` returns exactly 2 rows — the two enabled components `c-u4-a`, `c-u4-b`; (ii) `enabledOnly: false` returns all 3 rows including `c-u4-c` (disabled); (iii) **zero** `AccessAudit` rows in the scratch database after both calls — the C-M3·2 unit-level pin, mirroring M2's U3 `GetProfilesAsync_VerifiedOnly_Filters` assertion that the candidate-filter is *not* an access decision (C3/C-M2·2 at the unit level). Components are planted via a write session (`SeedComponentsAsync` would upsert only the four defaults `safety/maintenance/social/governance`, which would not give the 2 enabled + 1 disabled shape the §2.3 test needs).

- **(c) Marten Linq ternary deviation:** **None.** `GetComponentsAsync(bool enabledOnly)` in `UserInfoService.cs` mirrors M2's `GetProfilesAsync(bool verifiedOnly)` exactly — captured-bool ternary on the outside, `Where(c => c.Enabled)` / no-filter on the inside, one `store.QuerySession()` (read session, not a write session, mirroring M1's `GetProfileAsync` + M2's `GetProfilesAsync` shape). No deviation to record — the shape compiles and is consistent with M2's precedent in the same file.

DI: no registration change — `IUserInfoService → UserInfoService` already rides the transient registration in `src/Kumunita.Core/DependencyInjection.cs` (M2 U3 precedent); the new interface method does not alter the type identity, so the existing registration binds it.

## U5 — PostToAuditableResource
- **(a) New file:** `src/Kumunita.Core/Posts/PostToAuditableResource.cs` — `public sealed class PostToAuditableResource : IAuditableResource` (M3's single `Post` → `IAuthorizationService` projection; mirrors M2's `ProfileToAuditableResource`, no new seam; replies get **no** adapter — C-M3·1 pins that the parent's single `Read` decision owns reply visibility, so `PostReply` has no own `CanSeeAsync` call / no own audit row).
- **(b) 6-member mapping (per design doc §2.2 + plan-register U5 pin):** `Id => Post.Id`; `Name => Post.Title ?? (Post.Body.Length < 60 ? Post.Body : Post.Body[..57] + "...")` — title-or-body-truncated shape exactly as the plan register pins it (plan U5 spec is authoritative over the §2.2 design-doc snippet, which showed a 60-char no-ellipsis form; recording the resolution here per U4's drift-flag convention, not a §2.6 drift-pause since the *frozen* pin is the 6-member count + `TargetKind` string); `OwnerId => Post.AuthorId`; `Audience => Post.Audience` (projected verbatim, ADR 0001-B; non-null by construction, C1); `ComponentId => Post.ComponentId` (C-M3·2 — a scoping key for M3b's moderator surfaces, never itself a decision input); `TargetKind => "post"` (**exact-string pin**, C-M3·3: the aggregate-feed row's `AccessAudit.TargetKind` discriminator).
- **(c) Build:** `run_build` on `Kumunita.Core` green — zero compile warnings on the new file.
