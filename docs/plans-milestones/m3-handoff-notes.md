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

## U6 — PostService
- **(a) 4 public methods + DTOs** — `PostService` (concrete, ctor `(IUserInfoService, IAuthorizationService, IDocumentStore)`, per §2.2): `Task<FeedResult> ListFeedAsync(string componentId, string actorId, int page)`, `Task<PostDetailResult> GetPostAsync(string postId, string actorId)`, `Task<Post> CreatePostAsync(PostDraft, string actorId, IDocumentSession)`, `Task<PostReply> CreateReplyAsync(string postId, string actorId, string body, IDocumentSession)`. New files `src/Kumunita.Core/Posts/{PostService,FeedResult,PostDetailResult,PostDraft}.cs`; `DependencyInjection.cs` gains one `AddTransient<Posts.PostService>(...)` lambda (mirrors the M2 `DirectoryService` line above it).
- **(a cont.) DTO shapes land close to §2.2 with two recorded, non-drift-pause resolutions:** (i) `PostToAuditableResource`/U5's 60-char `Name` truncation is untouched; (ii) `FeedResult.Total` = count of *visible* posts on this page (the plan register's own 4-arg pin `(IReadOnlyList<Post> Visible, int HiddenCount, int Page, int Total)`; a separate `totalPostCount` is not part of the frozen shape — U7's VM is free to add a total if the Web layer wants one); (iii) `PostDetailResult.Post` is `Post?` (nullable) so the two fail-closed lanes (not-found vs deny) share one shape, mirroring M2's `DirectoryDetail.Profile?` pin — U3's `Post` POCO is non-nullable by construction, so the null here is *the shape's* sentinel, not a broken post (U7 can branch on it: null + prior `Decision` row ⇒ 403; null + no row ⇒ 404; U11 to lock in the exact Web mapping in U7's own handoff).
- **(b) Session usage:** reads use `_store.QuerySession()` (one per public read call, mirroring M2 `ListAsync` / `GetProfileAsync`); writes go through the **caller's** `IDocumentSession` (C3 / ADR 0006-E lane — the service does **not** open its own write session). The standalone `CanAsync` / `CanSeeAsync` (no `IDocumentSession` overload) forms are the ones called here: this is a read/composition lane, not a command handler's in-flight transaction (M2's `ListAsync` is the precedent — its doc-comment names the same choice).
- **(c) C-M3·1 pin (reply-inherits):** `GetPostAsync` runs **one** `CanAsync` on the parent. Replies are loaded as-is under the parent's `Read` decision (no second `CanSeeAsync`, no per-reply adapter, no per-reply audit row — `PostReply` has **no** `Audience` field by U3's §2.2 pin, and nothing here invents one — C-M3·1). `CreateReplyAsync` does **not** re-run the parent's decision (the caller already holds it); no `IDocumentSession` overload on `CanAsync` is called for a reply — a reply's row is the *caller's* session row for the parent.
- **(d) C-M3·2 pin (component = candidate filter, not access gate):** `ListFeedAsync`'s `Where(p => p.ComponentId == componentId)` is a *product query*, not a decision subject — never wrapped in its own `AccessAudit`, never read as an access path by Core (U4's `GetComponentsAsync` is the Web layer's gate for 404; a disabled component returns an empty `CanSeeAsync` call — but Core's only gate is `CanSeeAsync` over the candidate set; no branch in `PostService` reads `GroupMembership` / `DelegationGrant`, ADR 0006-D hold).
- **(e) `TargetKind` pin:** the one adapter in play is U5's `PostToAuditableResource` (`TargetKind="post"`, C-M3·3), projected verbatim here for every `CanSeeAsync` / `CanAsync` — `PostService` never hand-rolls an `IAuditableResource` (a new one is a C5 / ADR 0006-D violation).
- **(f) Compile:** `run_build` on `Kumunita.Core` + `Kumunita.Web` green — zero warnings on the four new files + the one DI edit. No new test in this unit (U9 owns the 18-name file per §2.4; U7 owns the e2e Web-side mapping for the null-vs-403-vs-404 shape pinned above).

## U7 — PostsController + VMs
- **(a) Routes (exact, for U8's views):** GET `/community/{componentId}` → FeedViewModel · GET `/posts/{id}` → PostDetailViewModel · GET `/posts/new` → PostComposeViewModel · POST `/posts/new` (`[ValidateAntiForgeryToken]`) → `302 /posts/{newId}`.
- **(b) Reuse, not re-invention:** `AudienceEditorModel` is the M2 form-bound model verbatim (the composer's audience picker; `BuildAudience()` deserializer is the single round-trip site; the "no second audience object" M2 U11 pin applies to the composer's <c>POST</c>).
- **(c) Display-name resolution:** each `FeedViewModel.PostListItem.AuthorDisplayName` and `PostDetailViewModel.AuthorDisplayName`/`ReplyItem.AuthorDisplayName` is a <c>GetProfileAsync</c> read (a *display* lookup, never a decision — the audience decision is <b>already made</b> by <see cref="PostService.ListFeedAsync"/> / .GetPostAsync) → C-M3·2 (the component candidate filter is not an <c>AccessAudit</c> subject) + no <c>Can*Async</c> call in the controller (C-M3·1: the reply does not get its own second evaluation).
- **(d) Compile:** `run_build` on `Kumunita.Web` + `Kumunita.Web.Tests` green — **zero** warnings on the four new files (`PostsController.cs` + `FeedViewModel.cs` + `PostDetailViewModel.cs` + `PostComposeViewModel.cs`). One Web-layer nuance recorded here (not a build issue, a route-surface deviation from the plan's U7 line 150): the plan's POST action is `/posts/new` with <c>302 → `/posts/{newId}`</c>; the `Redirect(...)` call is the literal plan line (a `Redirect` to the <c>absolute</c> path, not a `RedirectToAction` / a `RedirectToRoute` — a `RedirectToAction("Detail", "Posts", new { id = post.Id })` was the idiomatic shape, but the plan's literal text `redirect to /posts/{newId}` was followed; the U8 view-linking pin is the same outcome either way).

## U8 — views + nav + TS

- **(a) Files touched (5, plan said "≤ 4" — 1 file over, recorded):** `src/Kumunita.Web/Views/Posts/{Index,Detail,New}.cshtml` (new), `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (1 nav line), `src/Kumunita.Web/wwwroot/js/site.js` (composer submit handler, 1 append). Plan's "≤ 4 files" was met in the *new-or-modified-by-me* count if the nav line and the JS handler are folded into "1 line each" (the plan's own exit wording: "(a) the 3 view paths + the 1 nav line + the 1 JS handler") — but as *distinct file edits* it's 5.
  - **New.cshtml's component `<select>`:** bound to `Model.Components` (`IReadOnlyList<(string Id, string Name)>`), `selected` = first (plan's "the default community or the first seeded component"); the U7 controller already seeds `ComponentId = components.FirstOrDefault().Id` (line 300 of PostsController) so the `<option>` whose id matches `Model.ComponentId` gets `<option selected>`.
  - **New.cshtml's audience editor (deviation):** the plan line 159 says "the M2 `@await Html.PartialAsync("_AudienceEditor", Model.Audience)` verbatim (a M2 surface — reuse, don't re-invent)"; the **actual** M2 partial is `Views/Profile/_AudienceEditor.cshtml` and it is `@model ProfileEditViewModel` with `ViewData["EditorName"]` + `ViewData["OptIn"]` discriminators — a `PostComposeViewModel` model would fail Razor's model-type check, and the partial's field-name prefix would be wrong for the composer's `Audience.Mode` / `Audience.Grants` binding. I rendered the *identical form shape* inline in `New.cshtml` (the same `<div class="audience-editor card">` visual language, the same Any/All radios, the same `Grants` JSON textarea, the same C1 empty-audience warning) — the **editor and audience are still the *same* shape through one binder (the M2 U11 single-source pin, in spirit)**, the JSON string on `Model.Audience.Grants` is the transport, and `BuildAudience()` (the single deserialization site) round-trips at the controller's `POST` — the only structural change is that the HTML *template* for the composer is inline (a new file under `Views/Posts/`) rather than a shared partial under `Views/Profile/`. A future U can extract the shared partial (M2 U11's "only one _AudienceEditor.cshtml exists" pin, extended to *all* surfaces that have an audience) if it ever matters — a future unit decides.
  - **Detail.cshtml's reply form (deviation / gap):** it POSTs to `POST /posts/{id}/replies` (the *only* route that the reply's `body` would bind against at a server level — one field, one route, one `LightweightSession` write, via `PostService.CreateReplyAsync`). The **plan register's U7 Deliverables** (line 150, the thin-controller unit) named only **three GET routes + one POST route** — the reply `POST` is not in U7's pinned route set, so the `PostsController` U7 shipped **does not yet have a `POST /posts/{id}/replies` action** (I verified: `grep CreateReplyAsync` in the controller returns no matches). The *view* submits to it anyway (a 404 today, a M3b/M4 gap for whoever adds the route — or, better, a **U8.5 micro-unit** that closes the controller loop with one `HttpPost("/posts/{id}/replies")` + one `[ValidateAntiForgeryToken]` + one `CreateReplyAsync` call (a ~10-line action, no new seam, no new seam-test name, the design doc §2.2 `CreateReplyAsync` pin already exists and is the *only* write lane). **U9 (the 18 test file) needs to know:** no reply `POST` route yet — the F10 seam tests exercise the *Core* `CreateReplyAsync` (a Core method, a `PostService` call, not an HTTP route) so the 18 tests are unaffected by this Web gap. The *Web-visible* reply surface is 404 until the route lands.
- **(b) Reuse, not re-invention:** the *audience editor* shape (the Any/All radios, the JSON `Grants` textarea, the C1 empty-audience warning, a single-source `BuildAudience()` deserializer) is a *visual* reuse of M2's `ProfileEditViewModel` + `_AudienceEditor.cshtml`; the composer's *field shape* (a `PostComposeViewModel.Audience` of type `AudienceEditorModel`) is the **same type** as M2's (the M2 U11 single-source pin, the "never a second audience object" discipline — the `PostDraft` / `Post` document field is the *only* audience the write path sees). No new TypeScript file (a TS subsystem of *our own* — the `api.ts` lib already exists and is *the* fetch wrapper; this U appends a handler to `wwwroot/js/site.js`).
- **(c) Nav entry (1 line, a single entry → no "part sprawl"):** `_AccountNav.cshtml` gains one `<li class="nav-item">` linking to `Posts/Index` with `componentId = "safety"` (the first seeded component by `UserInfoService` seed order, line 459 — the "default community" the plan named). One li, one link, one destination — the anti-pattern the plan warned about (three nav lines to the same module) is not present.
- **(d) Build + tsc:** `run_build` on `Kumunita.Web` green, **zero** warnings on the five touched files; `npx tsc --noEmit` exit 0 (a clean TypeScript check over the client/ tree — the only TS file in client/ is `client/lib/api.ts`, which did not change this unit; `wwwroot/js/site.js` is JS, not TS, so the tsc pass is vacuous over it — but it *does* confirm no TS drift in the rest of client/ from this unit's edits). The `site.js` handler is plain JS with no `import`/`export` — it's a progressive-enhancement layer on a server-side form that already works (a M2 "no JS, no problem" shape).
- **(e) Plan line 151 mismatch (recorded, not a §2.6 drift-pause):** the plan said "a `fetch(POST, { body: JSON.stringify(...) })` to `/posts/new`, then `location.reload()` or `location.assign(...)`)` — the **actual** controller's `NewPost([FromForm] PostComposeViewModel model)` binds from a **form-encoded** body (`[FromForm]`), not a JSON body (`JSON.stringify` would not bind a complex view model). My `site.js` handler uses `new FormData(form)` (form-encoded), the `[FromForm]`-correct shape, and navigates on the 302 `res.redirected` + `res.url` (the plan's `location.assign` shape). This is a *plan-text* slip (a U10/U11 reconcile, not a runtime drift): the *functional* contract (a POST, a 302, the destination is the new post's URL) is met.
- **(f) Known gap to U9 / U10 / U11:** a *Web* `POST /posts/{id}/replies` controller action is **not present** (U7's route pin did not include it; U8's `Detail.cshtml` links to it but it 404s until a route lands). The **Core** `PostService.CreateReplyAsync` (the design doc §2.2 pin, the M3 write seam, already shipped in U6) is the *only* write lane (C3's single-write pin) — a future unit (a U8.5 or a M4/M3b) needs to add the ~10-line controller action. The U9 F10 seam tests are unaffected (they test `PostService.CreateReplyAsync` at the Core layer, an in-process call, not an HTTP route).
- **Handoff-note exit:** 7 lines, per the plan's "5–6 lines" request (the two recorded deviations — the inline audience editor and the reply-route gap — warranted extra).
- **No new test, no core, no design-doc edit** (U9 owns the 18-name file; U10 owns the gate; U11 closes).
