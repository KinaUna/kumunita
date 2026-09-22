# Guardian controls (`GU`) — rolling handoff notes

> **The scratch tier** of the GU lane's three-tier contract (the design doc is
> primary, the register is secondary, this file is scratch). One section per
> unit, **appended, never rewritten**. Each unit writes exactly one short
> section before it exits; the next unit reads only that section + its own
> entry-reads list. A `## U<m> — Drift pause` section is a **blocker**: the
> next unit reads it first and either resolves it (recording the resolution in
> its own section) or carries it forward (naming it in its exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##` section
> from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-14
- **Register:** `docs/plans-milestones/in-progress/plan-guardian-controls.md` (U01–U11)
- **Design doc (primary):** `docs/design/guardian-controls-design.md` (U01 finalizes its `## Pinned contract`)
- **ADR:** `docs/adr/0028-guardian-controls-account-scope-supervision.md` (already accepted; Amends 0006, 0003/0012, m2b)
- **Scope:** a parent adds an account for a child (the usual confirm-email
  process) and supervises it at the **account level**: suspend/lock, curate the
  child's community & group memberships, approve a group invitation sent to the
  child, and hand the account over to independence when the child comes of age.
  Standing is a 9th `AccessVia` value + a `GuardianLink` doc on the existing
  `M1DocTypes` surface; the five supervisory seams are ADDs on `IUserInfoService`
  (ADR 0006-E lane); the content path (`CanAsync` / `CanSeeAsync`) is
  **untouched** (G·1 — load-bearing).
- **Out of scope (the named deferrals, ADR 0028 §E):** no content read (G·1),
  no reading who contacted the child, no delegation of authorship, no blanket
  "block all communities" toggle, no stored age. Each re-litigates as an
  ADR 0028 amendment.
- **Test model:** Core seam tests in `Kumunita.Core.Tests` against
  `PostgresFixture` (U09 — the **eleven** pinned seam tests, incl. the
  load-bearing `G1_GuardianCannotReadChildContent` and `G3_ContentReadIsNeverGuardian`);
  Web VM data-shape tests in `Kumunita.Web.Tests` (U10 — the four pure VM
  projection tests). The lane's **acceptance gate** (U01 pins it in the design
  doc `### Acceptance gate`; U10 records the run result) = the eleven core
  tests + the four Web tests + the build line. Runner quirk (AGENTS.md) applies:
  run via `dotnet exec tests\…\.dll`, not `dotnet test`.

<!-- U01 appends its section below this line. One `##` section per unit, in
     order (U01, U02, … U11). Never rewrite a prior section. -->

## U01 — pinned contract

- **Date:** 2026-09-14. Appended `## Pinned contract (U01 — finalizes for U02–U11)` to `docs/design/guardian-controls-design.md` (between `## Seams & contracts (mandatory)` and `## Feedback loops`). **Docs-only: no code, no build.**
- **(a) Five seam names (verbatim):** new methods — `CreateGuardianLinkAsync(childId, guardianId)`, `SuspendChildAsync(childId, guardianId)` / `UnsuspendChildAsync(childId, guardianId)`, `ApproveGroupInvitationAsync(groupId, childId, guardianId)`, `DissolveGuardianLinkAsync(linkId, actorId, viaAdmin)`; the two **branches** (signatures unchanged, a `Via: Guardian` branch added to each standing gate) — `AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` and `AddGroupMemberAsync` / `RemoveGroupMemberAsync`; the **gate** on `AcceptGroupInvitationAsync` (self-accept refused for a supervised child); `DeclineGroupInvitationAsync` stays open.
- **(b) `AccessVia.Guardian`:** the **9th** value, appended after `Group` in `src/Kumunita.Core/Authorization/Decision.cs` (value-addition, no renumber; M1 `Admin` 7th / ADR 0013 `Group` 8th precedent).
- **(c) 11 pinned test names** (`tests/Kumunita.Core.Tests/GuardianControlsTests.cs`): `G1_GuardianCannotReadChildContent`, `G2_SuspendIsLiveAndBlocksStanding`, `G2_DissolveRestoresSelfLanesOnNextRead`, `G3_NonChildTargetIsRefused`, `G3_ContentReadIsNeverGuardian`, `G4_FormationCommitsAccountLinkAndAuditTogether`, `G5_GlobalAdminDissolvesAndUnSuspends`, `Invitation_GatedForSupervisedChild`, `Invitation_GuardianApproveLandsMembership_ViaGuardian`, `Membership_AddRemoveChild_ViaGuardian`, `SuspendSetsProfileBlocked_EnforcementIdentical`.
- **(d) G·1 pin:** `AccessVia.Guardian` appears on **no** `CanAsync` / `CanSeeAsync` content decision — the lane's load-bearing honesty; a unit that puts it on the content path is a drift pause, not a deviation (unit-series rule §5).
- **(e) Drift pause:** none — the design doc's prose was consistent with ADR 0028 and the frozen `IUserInfoService` surface; nothing to resolve or carry forward.

## U02 — GuardianLink + M1DocTypes

- **Date:** 2026-09-14. Created `src/Kumunita.Core/UserInfo/GuardianLink.cs` (POCO + `GuardianLinkStatus` enum, matching the §Pinned contract verbatim) and added one additive line to `src/Kumunita.Core/M1DocTypes.cs`. **`dotnet build Kumunita.slnx -c Debug` green.** No new test (U09 pins the lane's tests).
- **(a) POCO fields (verbatim):** `string Id` (surrogate PK), `string GuardianId` (the creator — G·4), `string ChildId` (the target), `GuardianLinkStatus Status` (the two-state machine), `DateTimeOffset CreatedAt`, `DateTimeOffset? DissolvedAt`, `string? DissolvedBy`. Enum: `GuardianLinkStatus { Active, Dissolved }` (file-scoped, like `InvitationStatus`).
- **(b) `M1DocTypes` line:** `opts.Schema.For<GuardianLink>().UniqueIndex(g => g.GuardianId, g => g.ChildId);` — placed **immediately after** `opts.Schema.For<DelegationGrant>();`, with the one-line comment `// GU (ADR 0028): one row per (guardian, child); the pair is the business key (GroupInvitation convention)`. No other line in `Configure` changed.
- **(c) Unique-index fields:** `(GuardianId, ChildId)` (the business-key pair, per the `GroupInvitation` convention; the surrogate `Id` is the Marten identity).
- **(d) Confirmed:** **no** doc-side `IsActive` boolean and **no** `Scope` field (G·2 — the service is the resolver; the guardian does not act *as* the child).
- **(e) Compile warnings:** none.

## U03 — AccessVia.Guardian

- **Date:** 2026-09-14. Appended `Guardian` as the **9th** value on `AccessVia` (after `Group`) in `src/Kumunita.Core/Authorization/Decision.cs`. **`dotnet build Kumunita.slnx -c Debug` green.** No new test (U09's tests exercise it indirectly via the audit rows).
- **(a) Enum now has 9 values:** `Owner, Audience, Delegation, Moderator, Report, BreakGlass, Admin, Group, Guardian`.
- **(b) `Guardian` is the 9th / last** (index 8), appended after `Group` (trailing comma added to `Group`).
- **(c) G·1 doc-comment line (verbatim):** "exercised only on the IUserInfoService management lanes — **never** on a CanAsync / CanSeeAsync content decision (G·1)".
- **(d) No renumber:** no existing value's position or name changed — `Owner`..`Group` (indices 0–7) are byte-identical; `Guardian` is purely additive.
- **(e) Compile warnings:** none.

## U04 — formation + suspension + independence seams

- **Date:** 2026-09-14. Declared the four GU account-level seams on `IUserInfoService` (a new `// GU guardian lanes (ADR 0028)` `#region` appended at the end of the interface) and implemented them in `UserInfoService.cs` (a new `// GU guardian lanes` section before the final brace, plus a shared `GuardActiveLinkAsync` standing gate). **`dotnet build Kumunita.slnx -c Debug` green** (4 projects, no warnings). No new test (U09 pins the 11 GU tests).
- **(a) Four seam signatures (verbatim):** `Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId);` · `Task SuspendChildAsync(string childId, string guardianId);` · `Task UnsuspendChildAsync(string childId, string guardianId);` · `Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin);`.
- **(b) Audit verb + `Via` for each:** `guardian.create` / `Via: Guardian` (TargetKind "guardian-link", TargetId = link.Id); `guardian.suspend` / `Via: Guardian` (TargetKind "profile", TargetId = childId); `guardian.unsuspend` / `Via: Guardian` (TargetKind "profile", TargetId = childId); `guardian.dissolve` / `Via: (viaAdmin ? Admin : Guardian)` (TargetKind "guardian-link", TargetId = linkId). All `Outcome: Allow`, in-transaction (C3).
- **(c) Exact throw precondition for each:** `CreateGuardianLinkAsync` — `ArgumentException` on blank childId/guardianId, **no other throw** (duplicate pair = idempotent no-op); `SuspendChildAsync` / `UnsuspendChildAsync` — `UnauthorizedAccessException` when no **active** link for (guardian, child) [the standing gate], then `InvalidOperationException` when the child's `Profile` (by `SubjectId`) is missing; `DissolveGuardianLinkAsync` — `ArgumentException` on blank linkId/actorId, `InvalidOperationException` when the `GuardianLink` row is missing, `UnauthorizedAccessException` when `!viaAdmin` and `actorId != GuardianId`.
- **(d) Dissolve does NOT set `Profile.Blocked`:** the impl writes nothing to membership and never touches `Profile` — the design doc §D "writes nothing to membership; the self-lanes restore on the very next read (C4)". Un-suspend is the **separate** `UnblockAsync` (G·5 valve) / `UnsuspendChildAsync` act, not a dissolve side-effect.
- **(e) Duplicate formation is an idempotent no-op:** `CreateGuardianLinkAsync` returns the existing `Active` row as-is (no mutation, no audit row) rather than throwing — the contract, not an error.
- **(f) `DependencyInjection.cs` NOT touched:** the four impls run on the existing `store`-owned `UserInfoService(IDocumentStore store)` ctor (no new parameter), so the registration is unchanged.
- **(g) GlobalAdmin `BlockAsync`/`UnblockAsync` unchanged** (they live on `IIdentityService`, untouched); **compile warnings:** none.

## U05 — membership curation admits guardian standing

- **Date:** 2026-09-14. Added a `GateGuardianStandingAsync` helper (next to `GateCommunityStanding`) and wired a `Via: Guardian` branch into the standing derivation of the four existing membership lanes. **`dotnet build Kumunita.slnx -c Debug` green** (4 projects, no warnings). No new test (U09's `Membership_AddRemoveChild_ViaGuardian` pins this).
- **(a) Helper + return contract:** `private async Task<Authorization.AccessVia?> GateGuardianStandingAsync(string actorId, string childId)` — a read-only `store.QuerySession()` of the **active** `GuardianLink` where `GuardianId == actorId` && `ChildId == childId` (mirrors U04's `GuardActiveLinkAsync` `Where` clause exactly); returns `Authorization.AccessVia.Guardian` when such a row exists, else `null` (blank actor/child ⇒ `null`, no throw). The branch **satisfies standing — it never throws on its own.**
- **(b) Each lane's `Via` expression (the `??` short-circuit, one per lane):** `AddGroupMemberAsync` → `via = await GateGuardianStandingAsync(addedBy, userId) ?? (addedBy == group.OwnerId ? Owner : Admin)`; `RemoveGroupMemberAsync` → `via = await GateGuardianStandingAsync(removedBy, userId) ?? (removedBy == group.OwnerId ? Owner : Admin)`; `AddCommunityMemberAsync` → `via = await GateGuardianStandingAsync(actorId, userId) ?? GateCommunityStanding(componentId, actorId, actorRoles)`; `RemoveCommunityMemberAsync` → `via = await GateGuardianStandingAsync(actorId, userId) ?? GateCommunityStanding(componentId, actorId, actorRoles)`. For the community lanes the `??` **bypasses** `GateCommunityStanding` (and its `UnauthorizedAccessException`) when the guardian base fires — a guardian holds neither GlobalAdmin nor a moderator scope by definition.
- **(c) Exceptions preserved:** the `RemoveCommunityMemberAsync` mandatory-community `InvalidOperationException` (still fires after the `via` resolution — unchanged order), the ADR 0008 group-owner-row behavior, and the `AddCommunityMemberAsync` idempotent upsert are all **unchanged** — the branch adds a path, never removes one.
- **(d) Interface signatures unchanged:** `IUserInfoService.cs` changed **doc-comment only** on the four lanes (each now names the GU `Via: Guardian` narrower-standing branch); no parameter added, no signature moved. `AccessVia.Guardian` stays **off** the `CanAsync`/`CanSeeAsync` path (G·1 — this unit touches neither).
- **(e) Compile warnings:** none. (First build was red only from a mangled splice in the two community-lane edits; both were rewritten clean and the rebuild is green.)

## U06 — invitation gate + ApproveGroupInvitationAsync

- **Date:** 2026-09-14. Added the GU gate to `AcceptGroupInvitationAsync` (a read-only active-link check at the top of the body, before the existing row load — everything below unchanged) and a new `ApproveGroupInvitationAsync(groupId, childId, guardianId)` seam in `IUserInfoService.cs` + `UserInfoService.cs` (impl reuses the accept write path verbatim, keyed on `childId`). **`dotnet build Kumunita.slnx -c Debug` green** (4 projects, no warnings). No new test (U09's `Invitation_GatedForSupervisedChild` + `Invitation_GuardianApproveLandsMembership_ViaGuardian` pin this).
- **(a) Gate throw (verbatim):** `throw new InvalidOperationException($"Account {actorId} is supervised; a group invitation must be approved by their guardian (see ApproveGroupInvitationAsync).")` — fired when **any** active `GuardianLink` has `ChildId == actorId` (the `?? ` idiom: `Query<GuardianLink>().Where(l => l.ChildId == actorId && l.Status == Active).FirstOrDefaultAsync()` is non-null).
- **(b) Approve's two preconditions:** (1) standing gate via U04's `GuardActiveLinkAsync(session, guardianId, childId)` → **`UnauthorizedAccessException`** on no active link (G·3 deny-by-default; `G3_NonChildTargetIsRefused`); (2) the child's `GroupInvitation` for `(groupId, childId)` must exist **and** be `Pending` → else **`InvalidOperationException`** (distinct from the no-standing gate).
- **(c) `ResolvedBy = guardianId`** (verbatim) — the guardian, not the child; `row.ResolvedAt = now`; membership upsert is the exact `AcceptGroupInvitationAsync` shape (same `GroupMembership` `(groupId, childId)` key, `AddedBy = guardianId`), one `SaveChangesAsync`.
- **(d) Audit verb + `Via`:** `Action = "group.invite.approve"`, `TargetKind = "group"`, `TargetId = groupId`, `ActorId`/`EffectivePrincipalId = guardianId`, `Via = Authorization.AccessVia.Guardian`, `Outcome = Allow`.
- **(e) `DeclineGroupInvitationAsync` unchanged** (impl byte-identical; doc-comment gained one line noting "a supervised child may always say no — no GU gate"). The self-lane's gate is **only** on the accept path.
- **(f) G·1 pin held:** `AccessVia.Guardian` appears **only** on the `group.invite.approve` audit row — **never** on a `CanAsync`/`CanSeeAsync` content decision. **Compile warnings:** none.

## U07 — GuardianController + view models

- **Date:** 2026-09-14. Created `src/Kumunita.Web/Controllers/GuardianController.cs` (the thin GU surface, `[Authorize]` on all actions, `SubjectId` from the cookie principal only) + `src/Kumunita.Web/Models/GuardianViewModels.cs` (the 4 pinned VMs). **`dotnet build Kumunita.slnx -c Debug` green** (4 projects, no warnings). No views (U08), no tests (U10).
- **(a) Route table (method + path + Core seam):** `GET me/children` → `Index` (reads the active `GuardianLink` rows + each child's `Profile.DisplayName`/`Blocked`, returns the `ChildAccountItem[]` list) · `GET me/children/{childId}` → `Detail` (`GetGroupIdsAsync` + `GetCommunityIdsAsync` + `GetPendingInvitationsForUserAsync`, gated on an active link → 404) · `POST …/{childId}/suspend` → `SuspendChildAsync` · `POST …/{childId}/unsuspend` → `UnsuspendChildAsync` · `POST …/{childId}/memberships/group` → `AddGroupMemberAsync`/`RemoveGroupMemberAsync` (`[FromForm] groupId, add`) · `POST …/{childId}/memberships/community` → `AddCommunityMemberAsync`/`RemoveCommunityMemberAsync` (`[FromForm] communityId, add`, passes `KumunitaPrincipal.RoleSet(User)` as `actorRoles`) · `POST …/{childId}/invitations/{groupId}/approve` → `ApproveGroupInvitationAsync` · `POST …/{childId}/dissolve` → `DissolveGuardianLinkAsync(link.Id, subject, viaAdmin: false)` · `POST me/children` → `AddChild` (`RegisterAsync` → `CreateGuardianLinkAsync`).
- **(b) One-commit formation (G·4):** the add-a-child POST does `identity.RegisterAsync(displayName, email, password)` then `userInfo.CreateGuardianLinkAsync(child.SubjectId, subject)` in the **same request** — a created-but-unlinked account can never exist; the verification email is M1's (staged by `RegisterAsync`), the form does not bypass it.
- **(c) G·1 held in the Web:** no route reads or links a child's content — reads are the `GuardianLink` row + the child's membership rows + pending group invitations (ids/names only); the child's posts / profile body never reach the controller or a VM.
- **(d) 4 VMs + exact field sets:** `ChildAccountItem(ChildId, DisplayName, Blocked)` · `PendingInvitationItem(GroupId, GroupName, InvitedAt)` · `MembershipEditorModel(ChildId, GroupIds, CommunityIds, PendingInvitations)` · `AddChildForm { DisplayName, Email, Password }` (a `class` with `DataAnnotations`, not a record).
- **(e) `IIdentityService` needed:** yes — for the add-a-child `POST me/children` only (`RegisterAsync`); all other routes use `IUserInfoService` alone. `IDocumentStore` is also injected (read-only `GuardianLink` queries for the child-list + the dissolve id resolution — a read, not a standing re-gate).
- **(f) Failure shape (mirrors the existing thin controllers):** `UnauthorizedAccessException` from a Core seam → `NotFound()` (404, a non-guardian learns nothing); `InvalidOperationException` → `TempData["error"]` + redirect (never a 500). **Compile warnings:** none. **Deviation note:** the user's condensed route list named `memberships/group` only; the plan (which wins) + design doc "community & group membership editor" scope includes the community lanes, so a `memberships/community` route was added alongside — the community `actorRoles` argument is `KumunitaPrincipal.RoleSet(User)` (the `CommunityController` pattern).

## U08 — Views/Guardian + nav entry

- **Date:** 2026-09-14. Created `src/Kumunita.Web/Views/Guardian/Index.cshtml` + `Detail.cshtml` + `AddChild.cshtml`; added **one** nav item to `Views/Shared/_AccountNav.cshtml`; registered the GU `kw-l` keys in `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`. **`dotnet build Kumunita.slnx -c Debug` green** (4 projects, no warnings).
- **(a) Views → bound VM + routes:** `Index.cshtml` (`@model IReadOnlyList<ChildAccountItem>`) — the child list (each row: `DisplayName` link → `Detail`; the `Blocked` badge + the state-picked `Suspend`/`Unsuspend` form, the m2b "state picks the card" shape) + the **inline** add-a-child form posting to `AddChild` + the empty card. `Detail.cshtml` (`@model MembershipEditorModel`) — `GroupIds` rows (remove → `CurateGroup` `add=false`) + an add form (`add=true`), the **same** shape for `CommunityIds` (→ `CurateCommunity`), `PendingInvitations` rows (→ `ApproveInvitation`), and the `Dissolve` form. `AddChild.cshtml` (`@model AddChildForm`) — the form re-rendered on `AddChild`'s validation/signup failure paths (U07's `return View(form)` resolves to a view named **`AddChild`**).
- **(b) Nav item:** one new `<li class="nav-item">` after `Profile` (the `Profile` item's exact shape) → `asp-controller="Guardian" asp-action="Index"`, `<kw-l key="nav.children">Children</kw-l>`. **Keys registered (catalog is a file):** `nav.children`, `guardian.title`, `guardian.lead`, `guardian.empty`, `guardian.add`, `guardian.manage_title` appended to `KnownTranslationKeys.EnValues` (the `nav.profile` catalog pattern) — required because `TranslationProvider`'s floor resolves an unregistered key to the **raw key**, so an unwrapped-then-unregistered `kw-l` would render its key, not its English. This touched one file beyond the plan's ≤4 (recorded here).
- **(c) G·1 confirmed:** **no view links to, renders, or even names a child's content** — the absence is the point: no post links, no "view their feed", no profile body; `Detail` shows only membership ids/names + the pending-invitation group names, and the `Index`/`AddChild` surfaces carry only `ChildAccountItem`/`AddChildForm`. A non-guardian's `Index` is the empty card (U07's `ActiveChildrenAsync` returns `[]`) — no error, no content.
- **(d) Confirm-guard:** the two destructive POSTs (`Suspend`, `Dissolve`) carry `onsubmit="return confirm('…')"` — a UX nicety, not a security gate (the server gate is U07's route + the Core seam's standing check). `wwwroot/js/site.js` **does not exist** in this repo (the layout uses `~/js/lib/*.js` modules); the codebase's `kw-l` exclusion of `confirm()` dialogs endorses inline `confirm()`, so no JS subsystem was created.
- **(e) Reconciliations vs the unit plan (both bound to the controller — ground truth):** (1) **Add-child is inline on `Index` and POST-only** — U07 exposed only `POST me/children → AddChild` (no `GET AddChild` / no `New` action; the controller doc-comment says the form renders inline on `Index`), so `Index.cshtml` renders it inline and `AddChild.cshtml` is the failure re-render — the plan's separate-`New.cshtml` + `Url.Action("AddChild")` link shape is **stale** vs U07. (2) **Communities ARE curatable** — U07 exposed `POST …/memberships/community → CurateCommunity`, so `Detail.cshtml` renders the add/remove form (not the plan's "if no community variant → read-only" fallback).
- **(f) Compile warnings:** none. **Deviation note (b):** the `KnownTranslationKeys.cs` registration is the one file beyond the plan's ≤4 deliverables; the nav deliverable itself directs it ("add the key the same way `nav.profile` is catalogued").

## U09 — 11 pinned seam tests

- **Date:** 2026-09-14. Created `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` — the lane's **11 pinned tests** (names verbatim from the design doc §Pinned contract → Pinned seam tests), the M2b `BootStoreAsync` harness shape, `class GuardianControlsTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>`. **`dotnet build Kumunita.slnx -c Debug` green** (0 warnings, 0 errors).
- **(a) The 11 names + result (all PASS):** `G1_GuardianCannotReadChildContent` ✅ · `G2_SuspendIsLiveAndBlocksStanding` ✅ · `G2_DissolveRestoresSelfLanesOnNextRead` ✅ · `G3_NonChildTargetIsRefused` ✅ · `G3_ContentReadIsNeverGuardian` ✅ · `G4_FormationCommitsAccountLinkAndAuditTogether` ✅ · `G5_GlobalAdminDissolvesAndUnSuspends` ✅ · `Invitation_GatedForSupervisedChild` ✅ · `Invitation_GuardianApproveLandsMembership_ViaGuardian` ✅ · `Membership_AddRemoveChild_ViaGuardian` ✅ · `SuspendSetsProfileBlocked_EnforcementIdentical` ✅.
- **(b) G·1 (load-bearing) assertion:** with an active `GuardianLink` over the child, `AuthorizationService.CanAsync(guardian, Read, childPost)` returns `Allowed == false` and `Via != AccessVia.Guardian`, whereas the child's own read of that same post is `Allowed == true` / `Via == Owner` — the denial is specifically the guardian standing (no `Via: Guardian` branch on the content path).
- **(c) Red tests:** **none** (11/11 green — no drift signal; no U04/U05/U06 seam implicated).
- **(d) Drift-guard:** **no 12th test** was added — exactly the 11 pinned names, load-bearing G·1 first.
- **(e) Reliable path:** `dotnet build Kumunita.slnx -c Debug` then `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.GuardianControlsTests"` → `Total: 11, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`. `docker container prune -f` ran clean (the fixture container self-stopped/deleted on exit; `Total reclaimed space: 0B`).
- **(f) Compile warnings:** none.
- **(g) G·5 reconciliation (dissolve ≠ un-suspend), honored in-test:** `G5_GlobalAdminDissolvesAndUnSuspends` asserts the **two separate acts** the plan names — (1) `DissolveGuardianLinkAsync(linkId, admin, viaAdmin: true)` on a link whose `GuardianId` is *not* the admin → the row goes `Dissolved` with `DissolvedBy = admin`, audit `guardian.dissolve` `Via: Admin`; (2) the M1 `UnblockAsync` (the G·5 valve) is a **separate** `IIdentityService` act — the GU dissolve seam **does not** itself flip `Profile.Blocked`, which the test asserts is still `true` after the dissolve (the suspension's un-set is the M1 `UnblockAsync` lane's, byte-identical per U04, kept out of the Core seam test). The Core-test assembly does not pull Identity/EF infra in (the `DirectoryServiceTests.cs:76` precedent), so test 7 pins the GU dissolve seam + the "dissolve leaves Blocked alone" reconciliation, not the M1 valve body.
- **(h) G·4 idempotency (as pinned):** `G4_FormationCommitsAccountLinkAndAuditTogether` asserts a duplicate `(guardian, child)` is an **idempotent no-op** (the existing `Active` row is returned as-is; the `guardian.create` audit-row count is unchanged on the second call — not a throw), the first call having committed link row + audit row in one session (C3).
- **(i) Exception types (the U04/U06 reconciled rules, as asserted):** no-standing → `UnauthorizedAccessException` (test 4, across `SuspendChildAsync` / `UnsuspendChildAsync` / `ApproveGroupInvitationAsync` / the community-curation branch); the U06 gate (test 8) → self-accept `AcceptGroupInvitationAsync` **throws** `InvalidOperationException` for a supervised child, self-decline **does not**.

## U10 — acceptance gate + Web tests

- **Date:** 2026-09-14. Created `tests/Kumunita.Web.Tests/GuardianViewModelsTests.cs` — the **4 GU Web VM projection pins** (the U07 pin; ADR 0028 §C), mirroring the `GroupsViewModelTests` idiom (assert the **public instance property name set**, sorted — the "drift-guard: no fields beyond the pin" discipline; name set, not types). **`dotnet build Kumunita.slnx -c Debug` green** (0 warnings, 0 errors).
- **(a) 11 Core seam tests (re-run as the gate) — all PASS:** `G1_GuardianCannotReadChildContent` ✅ · `G2_SuspendIsLiveAndBlocksStanding` ✅ · `G2_DissolveRestoresSelfLanesOnNextRead` ✅ · `G3_NonChildTargetIsRefused` ✅ · `G3_ContentReadIsNeverGuardian` ✅ · `G4_FormationCommitsAccountLinkAndAuditTogether` ✅ · `G5_GlobalAdminDissolvesAndUnSuspends` ✅ · `Invitation_GatedForSupervisedChild` ✅ · `Invitation_GuardianApproveLandsMembership_ViaGuardian` ✅ · `Membership_AddRemoveChild_ViaGuardian` ✅ · `SuspendSetsProfileBlocked_EnforcementIdentical` ✅. `Total: 11, Errors: 0, Failed: 0`.
- **(b) 4 Web VM tests — all PASS:** `ChildAccountItem_Is_Exact_Three_Field_Projection` ✅ · `MembershipEditorModel_Is_Exact_Four_Field_Projection` ✅ · `PendingInvitationItem_Is_Exact_Three_Field_Projection` ✅ · `AddChildForm_Is_Form_Model_With_Three_Required_Fields` ✅. `Total: 4, Errors: 0, Failed: 0`.
- **(c) Build line:** `dotnet build Kumunita.slnx -c Debug` — **green** (0 warnings, 0 errors; all four projects succeeded).
- **(d) Gate verdict:** **GREEN — GU is accepted.** The 11 Core seam tests (the part-vs-whole *whole*) + the 4 Web VM pins (the *parts*) are green together; G·1 (no `Via: Guardian` on the content path) holds as a test, not a promise. **No drift pause; no seam implicated** (had any Core test been red, the gate would have been RED against the U04/U05/U06 seam it names — none were).
- **(e) Design-doc append:** `docs/design/guardian-controls-design.md` gained `### Run result (GU acceptance gate — 2026-09-14)` (appended **inside the Pinned contract**, after `### Drift-guard` and before `## Feedback loops`; append-only — nothing above it rewritten).
- **(f) Docker cleanup:** `docker container prune -f` ran clean (`Total reclaimed space: 0B` — the Testcontainers postgres self-stopped/deleted on exit, as in U09).
- **(g) Compile warnings:** none (0 warnings on the full-solution build).

## U11 — close

- **Date:** 2026-09-14. **Docs + file-move only — no C# changed, no build run** (the gate was U10's; this unit closes the lane, it does not re-run it). **Drift pause: none** — the U10 gate is GREEN, so the close proceeds; no red to reconcile against a U04/U05/U06 seam.
- **(a) Gate verdict (U10's `### Run result`, read first as the precondition):** **GREEN — GU is accepted.** 11/11 Core seam tests PASS + 4/4 Web VM tests PASS + `dotnet build Kumunita.slnx -c Debug` green (0 warnings, 0 errors); G·1 (no `Via: Guardian` on the content path) holds as a test, not a promise; no drift pause, no seam implicated.
- **(b) `docs/ARCHITECTURE.md` — the two GU lines (the layout flip):**
  - **Tree line (~L85):** `UserInfo/` comment extended — `…; GU ✓ (ADR 0028) — GuardianLink (account-scope supervision: suspend / membership curation / invitation approval; **no content read**, G·1) + the AccessVia.Guardian standing (the 9th value) + the IUserInfoService guardian seams (formation / suspend / dissolve); see design/guardian-controls-design.md § GU — Closed (recorded) (2026-09-14)`.
  - **Doc-map line (after `DelegationGrant`):** `GuardianLink     { id, guardianId, childId, status: Active|Dissolved, createdAt, dissolvedAt?, dissolvedBy? }   // GU (ADR 0028): the account-scope supervision link (standing off an Active row, G·2); dissolve one-way (G·5)`.
  - **Untouched (as directed):** the `M1DocTypes.cs` / `M3DocTypes.cs` / `MediaDocTypes.cs` lines (GU rides the **existing** `M1DocTypes` surface, ADR 0004 §B.1, additive — U02's one line) and the `Events/` / `Projects/` lines (M4/M5 stay planned).
- **(c) Design-doc Close section:** `## GU — Closed (recorded) (2026-09-14)` appended at the **very end** of `docs/design/guardian-controls-design.md` (after the `## Three tests (run before "ready")` block) — the ADR 0028 decision-record line, the gate reference (→ U10's `### Run result`), the `M1DocTypes` layout note, the GlobalAdmin `viaAdmin: true` admin-shell carried-forward non-decision (ADR 0028 §C, §D G·5), and "M4/M5/M6 untouched". **Append-only held** — nothing above the U10 `### Run result` rewritten.
- **(d) The move to `done/`:** 13 GU files moved from `docs/plans-milestones/in-progress/` → `done/` (the register `plan-guardian-controls.md` + `guardian-controls-u01…u11-plan.md` + this handoff-notes file), one self-contained `Move-Item` (no `$vars`, no here-strings). **No stray non-GU file** was in `in-progress/` (confirmed this session) — all 13 are GU-lane files. **`in-progress/` is now empty.**
- **(e) No C# changed + no build:** confirmed — this unit touched exactly 3 files (`docs/ARCHITECTURE.md`, `docs/design/guardian-controls-design.md`, this notes file) and moved 13; no `src/` or `tests/` file was opened, no `dotnet build` was run (U10's GREEN gate stands).
- **(f) Anomalies:** none. The one cross-check worth noting for the next lane: the Close section's "Gate:" bullet names the **4 Web VM tests** (U10's `GuardianViewModelsTests.cs`) alongside the **11** Core tests — the gate is 11 + 4, not 11 alone; a future lane re-reading the design doc should not mistake the 11-pin for the whole gate.

## Summary

**The GU lane is closed (recorded 2026-09-14).** Shipped units (U01–U11), one-liner goal + test count + deviations:

| Unit | One-liner goal | Tests (this unit) | Deviations |
| --- | --- | --- | --- |
| **U01** | Finalize the pinned contract in the design doc (the `GuardianLink` POCO, the 5 `IUserInfoService` seams + 2 membership branches, `AccessVia.Guardian`, the 11 pinned test names, the acceptance gate + drift-guard) | none (docs) | none |
| **U02** | `GuardianLink` POCO + `M1DocTypes` registration (additive line, no re-seed) | none (U09 pins) | none |
| **U03** | `AccessVia.Guardian` — the 9th value (append after `Group`, no renumber) | none (U09 pins) | none |
| **U04** | Core seams: `CreateGuardianLinkAsync` / `SuspendChildAsync` / `UnsuspendChildAsync` / `DissolveGuardianLinkAsync` (standing live off the active link, deny-by-default) | none (U09 pins) | none |
| **U05** | Membership curation admits the guardian standing (a `Via: Guardian` branch on the 4 existing lanes; "record the narrower standing") | none (U09 pins) | one internal helper (`GateGuardianStandingAsync`) added; signatures unchanged |
| **U06** | The invitation gate (self-accept refused for a supervised child) + `ApproveGroupInvitationAsync` (self-decline stays open) | none (U09 pins) | none |
| **U07** | Web: `GuardianController` + the 4 VMs (the thin surface; one-commit formation; no route reads a child's content) | none (U10 pins) | **1 route added beyond the condensed plan** — `memberships/community` alongside `memberships/group` (the plan/design scope includes the community editor; the `actorRoles` arg is `KumunitaPrincipal.RoleSet(User)`) |
| **U08** | Web: `Views/Guardian/{Index,Detail,AddChild}` + the one nav entry + the `kw-l` key registrations | none (U10 pins) | **1 file beyond the ≤4 cap** — `KnownTranslationKeys.cs` (the nav deliverable directs it; an unregistered `kw-l` would render its raw key); **2 reconciliations vs the plan** — add-child is inline on `Index` (U07 exposed `POST me/children` only, no separate `New` action) + communities ARE curatable (U07 exposed the `memberships/community` route) |
| **U09** | The **11 pinned Core seam tests** (the load-bearing `G1_GuardianCannotReadChildContent` first) | 11/11 PASS | **2 reconciliations honored in-test** — G·5 (dissolve ≠ un-suspend: the M1 `UnblockAsync` valve is a separate `IIdentityService` act, kept out of the Core seam test) + G·4 (duplicate formation is an idempotent no-op, not a throw) |
| **U10** | Record the GU acceptance gate (GREEN) + the **4 Web VM projection pins** | 11/11 + 4/4 PASS | none (the gate's GREEN verdict is the deliverable) |
| **U11** | Close: flip the 2 `ARCHITECTURE.md` lines + append `## GU — Closed (recorded)` + move the 13 GU files to `done/` | none (docs + move) | **1 cross-check noted** — the Close section's "Gate" names the 4 Web tests alongside the 11 Core (the gate is 11 + 4, not 11 alone) |

**Gate (the lane's acceptance, recorded in U10):** **GREEN — GU is accepted** (2026-09-14). 11 Core seam tests + 4 Web VM tests, all PASS; `dotnet build` green. G·1 (the lane's whole honesty — no `Via: Guardian` on the content path) holds as a test, not a promise.

**ADR 0028 §E deferrals (each named; each re-litigates as an ADR 0028 amendment when it earns its keep, never a toggle):** (1) **no content read** (G·1 — the load-bearing one); (2) **no reading who contacted the child**; (3) **no delegation of authorship** (the guardian does not post *as* the child); (4) **no blanket "block all communities" toggle** (participation control is per-member); (5) **no stored age** ("minor" is carried by the link, not a stored field — `SECURITY.md`'s "what we don't store, we can't leak" is kept). **Plus the carried-forward admin-shell non-decision (U11):** the GlobalAdmin `viaAdmin: true` dissolve shell is a separate admin-shell lane, not a GU one (ADR 0028 §C, §D G·5).

**M4/M5/M6 untouched** — Events / Projects / Portability stay planned; GU is a named lane, not a renumber (the `GP` / `ML` / `RC` / `M4-adjacent` precedent). This `## Summary` is the lane's sole GU→next handoff artifact.
