# Guardian controls (`GU`) — account-scope supervision of a child's account — sealed unit register

> **The secondary tier** of the GU lane's three-tier contract. The **primary**
> is `docs/design/guardian-controls-design.md` (U01 finalizes its *pinned
> contract*; U02–U11 match it verbatim); the **scratch** is
> `docs/plans-milestones/in-progress/guardian-controls-handoff-notes.md` (one
> `##` section per unit, appended in order). When this register and a unit plan
> disagree, **the unit plan wins for what to do**; this register wins for
> *which files exist and in what order*.
>
> The design doc (primary) and **ADR 0028** already exist — this is an
> **implementation** lane, not a design lane. U01's job is to turn the existing
> prose into the machine-pinnable contract (exact C# seam signatures, the
> `GuardianLink` POCO, the pinned seam-test names, the acceptance gate, and the
> drift-guard) that U02–U11 implement against.

## Understanding

The platform is invitation-only and one-neighborhood (`SECURITY.md` §1,
ADR 0002), and its population is not only adults — **there will be kids on the
platform** (`the-platform-as-integrator.md`). The GU lane (ADR 0028) lets a
**parent** add an account for a **child** (the usual confirm-email process) and
supervise it at the **account level**: suspend/lock, curate the child's
community & group memberships, and approve a group invitation sent to the child
— and, when the child is old enough, hand the account over to independence.

The lane's whole honesty is the thing it **refuses** to do: it gives a
guardian **no standing to read the child's private content**. That refusal is
invariant **G·1** — "a test, not a promise" — and is the reason the guardian
standing is deliberately kept **out of the frozen `IAuthorizationService`
content-decision path** (`CanAsync` / `CanSeeAsync`, ADR 0006-A stay
byte-identical) and is instead exercised only on the **management** lanes
(suspend, membership, invitation), which live on `IUserInfoService` — the exact
ADR 0013 "one additive lane, nothing else moves on the frozen surface"
discipline.

This is a **named lane** (`GU`), not a milestone letter — the media
(`M4-adjacent`, ADR 0011), group posts (`GP`, ADR 0013), multilingual (`ML`,
ADR 0005), and rich content (`RC`, ADR 0025) lanes are the precedent. **M4/M5/M6
stay Events / Projects / Portability.** No roadmap letter moves; the README /
`Milestones.cs` / `MilestonesTests.cs` trio already carries `GU` as the single
in-progress entry (that trio was flipped in the design commit).

## Assumptions

- **Scope (per user, ADR 0028 §C):** five supervisory actions + formation +
  come-of-age. **In:** (1) formation (`CreateGuardianLinkAsync`, paired with
  `IIdentityService.RegisterAsync` in one Web transaction); (2) suspend /
  un-suspend (`SuspendChildAsync` / `UnsuspendChildAsync`, sets the existing
  `Profile.Blocked` flag); (3) community membership curation (the ADR 0012
  `AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` lanes admit the
  guardian standing); (4) group membership curation (the group
  `AddGroupMemberAsync` / `RemoveGroupMemberAsync` lanes, same branch); (5)
  invitation approval (`ApproveGroupInvitationAsync` + the `AcceptGroupInvitationAsync`
  gate); (6) independence (`DissolveGuardianLinkAsync`). **Out (named
  deferrals, ADR 0028 §E "Non-decisions"):** no content read (G·1 — load-bearing),
  no reading who contacted the child, no delegation of authorship, no blanket
  "block all communities" toggle, no stored age. Each re-litigates as an
  ADR 0028 amendment, not a toggle.
- **Standing is a 9th `AccessVia` value + a `GuardianLink` doc.**
  `AccessVia.Guardian` is appended **after** `Group` (the M1 `Admin` (7th) and
  ADR 0013 `Group` (8th) appends — a value-addition, never a renumbering; no
  stored row's meaning changes). `GuardianLink` is **one row per (guardian,
  child) pair** — a child may have one **or two** guardians; standing exists iff
  **any** active row. `Status` is a two-state machine (`Active → Dissolved`).
- **`GuardianLink` rides the existing `M1DocTypes` surface** (ADR 0004 §B.1) —
  a neighbor of `Schema.For<DelegationGrant>()`, a `(GuardianId, ChildId)`
  unique index (the `GroupInvitation` business-key convention), delta-detected
  + idempotent, **no new `*DocTypes`, no re-seed**. Additive → re-deploying an
  older image over a forward-migrated DB is safe.
- **The content path is untouched (G·1).** The guardian standing never appears
  on a `CanAsync` / `CanSeeAsync` decision; the child's content is authorized
  exactly as a normal account's. The frozen `IAuthorizationService` 4-method
  surface and the ADR 0006 frozen `IUserInfoService` signatures are **byte-
  identical** — the five supervisory seams are **ADDs** on `IUserInfoService`
  (ADR 0006-E compatible lane) and the membership lanes **grow a branch**, not
  a new method.
- **Formation is creation-based (G·4).** The standing basis is that the
  guardian *created* the child's account (the link's `GuardianId` is the
  creator); there is **no** self-serve "claim guardianship over an existing
  account" lane. The account + link + audit commit in **one Web transaction**
  (C3) so a created-but-unlinked account can never exist.
- **The safety valve always exists (G·5).** A GlobalAdmin may dissolve any
  active link and un-suspend any account, audited `Via: Admin`.
- **No new outbound channel.** The only email is M1's verification email on the
  usual lane (the child clicks the link to activate); supervision rides the
  link, not a re-keyed password. The durable outbox / dead-letter / degraded
  path is unchanged.
- **Test model (unchanged).** Core seam tests in `Kumunita.Core.Tests` against
  the `PostgresFixture` fresh-scratch-DB shape; the lane's three-test acceptance
  gate (closed-loop / handoff / part-vs-whole) is recorded in the design doc.
  Web controller/VM data-shape tests in `Kumunita.Web.Tests` (the codebase
  unit-tests controller/VM shape, not Razor markup). The runner quirk (AGENTS.md)
  still applies: run via `dotnet exec tests\…\Kumunita.Core.Tests.dll` /
  `Kumunita.Web.Tests.dll`, not `dotnet test`.

## Approach

Three tracks, sequenced. **Track A (Core, U02–U06):** the `GuardianLink`
POCO + `M1DocTypes` line; the `AccessVia.Guardian` value; the five supervisory
seams on `IUserInfoService` (formation, suspend/un-suspend, membership
curation, invitation approval, independence) + the `AcceptGroupInvitationAsync`
gate; all standing resolved **live** off the active link (G·2) and
**deny-by-default** (G·3). **Track B (Web, U07–U08):** a thin
`GuardianController` + VMs + Razor (list children, suspend/un-suspend, the
community & group membership editor, the pending-invitation approval list, the
dissolve control, the add-a-child form driving `RegisterAsync` +
`CreateGuardianLinkAsync` in one commit) + one nav entry. **Track C (tests +
close, U09–U11):** the pinned seam tests, the acceptance gate recorded, and the
`ARCHITECTURE.md` / design-doc flip + the register/unit-plans/handoff-notes
moved to `done/`.

Every unit ends with **build green** (`dotnet build Kumunita.slnx -c Debug`).
U11 appends the final `## GU — Closed (recorded)` section and moves the lane's
files to `done/`.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U01–U11 below), one
unit per fresh agent with a **~32K context window** (smaller than M3's ~64K —
keep each unit ≤ ~4 files / ~500 LOC so a 32K window closes it in one pass).

**Shared state (three-tier contract):**
- **Primary —** `docs/design/guardian-controls-design.md` — the GU invariants
  (G·1–G·5), the FACES, and (after U01) the **pinned contract**: the exact
  `GuardianLink` POCO, the exact C# of the five `IUserInfoService` seams + the
  two membership-lane branches, the `AccessVia.Guardian` value, the pinned
  seam-test names, and the acceptance gate. U01 finalizes; U02–U11 match it
  verbatim.
- **Secondary — this file** (`docs/plans-milestones/in-progress/
  plan-guardian-controls.md`) — the unit registry with each unit's
  deliverables and exit criteria.
- **Scratch —** `docs/plans-milestones/in-progress/guardian-controls-handoff-
  notes.md`. One section per unit, appended (never rewritten). Each unit writes
  exactly one short section before it exits; the next unit reads only that
  section + its own entry-reads list.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, 3–5
files, no full-repo scan; the design-doc section cited is named); **Deliverables**
(a closed set of new/modified files, ≤ ~4 files / ~500 LOC, no misc cleanups);
**Exit** (`dotnet build` green for the touched projects + the named test file
discovers its pinned tests, and a handoff-note entry appended *before* any
follow-up action).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside a `## U<m> — Drift
pause`; (3) never reshapes `GuardianLink` (the §Pinned-contract pin) or any
other Core document; (4) never opens a seam on `IUserInfoService` /
`IAuthorizationService` / `IIdentityService` beyond what U01 pinned;
(5) **never puts `AccessVia.Guardian` on a `CanAsync` / `CanSeeAsync` decision
path** (G·1 — the load-bearing rule; a unit that does this is a drift pause,
not a deviation); (6) never renumbers `AccessVia` (append only); (7) never
introduces a test whose exact name is not in the design doc's pinned-test
section; (8) if entry reads reveal the design doc is out of date, the unit
pauses and records `## U<m> — Drift pause` in the handoff note.

---

## Units (11 total)

### U01 — Finalize the pinned contract in the design doc
- **Goal:** append a `## Pinned contract (U01 — finalizes for U02–U11)` section to the **existing** `docs/design/guardian-controls-design.md` that turns the prose into machine-pinnable exact C# — the `GuardianLink` POCO, the exact five `IUserInfoService` seams + the two membership-lane branches, the `AccessVia.Guardian` value, the **pinned seam-test names**, and the **acceptance gate** + **drift-guard**. ADR 0028 already exists (no ADR to author). **No code, no build.**
- **Entry reads:** `docs/design/guardian-controls-design.md` (the **primary** — the G·1–G·5 invariants, the seams prose, the FACES, the three tests); `docs/adr/0028-guardian-controls-account-scope-supervision.md` (the §C five actions + §D invariants + §E audit verbs — the authority U01 pins); `src/Kumunita.Core/Authorization/Decision.cs` (the `AccessVia` enum — the `Admin`/`Group` append precedent U01 mirrors for `Guardian`); `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (the frozen surface + the exact `AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` / `AddGroupMemberAsync` / `RemoveGroupMemberAsync` / `AcceptGroupInvitationAsync` / `DeclineGroupInvitationAsync` signatures the guardian branches ride); `src/Kumunita.Core/UserInfo/GroupInvitation.cs` (the `InvitationStatus` + `ResolvedAt`/`ResolvedBy` + the `group.invite.*` audit vocabulary U01 reuses); `src/Kumunita.Core/UserInfo/Group.cs` §`DelegationGrant` (the shape precedent for `GuardianLink`); `src/Kumunita.Core/M1DocTypes.cs` (where `GuardianLink` will register + the `GroupInvitation` unique-index convention to mirror).
- **Deliverables (1 file, modify):** `docs/design/guardian-controls-design.md`. Append `## Pinned contract (U01 — finalizes for U02–U11)` with, in order:
  - `### GuardianLink POCO (exact C#)` — `src/Kumunita.Core/UserInfo/GuardianLink.cs`: `Id`, `GuardianId`, `ChildId`, `Status` (a new `GuardianLinkStatus { Active, Dissolved }` enum), `CreatedAt`, `DissolvedAt?`, `DissolvedBy?`; a `HasActiveLink(string childId)`-style helper is the **service**'s job, not the doc's (mirror `DelegationGrant.IsActiveAt`'s "the service is the resolver" comment). Doc-comment anchors G·2/G·4/G·5.
  - `### AccessVia.Guardian (exact C#)` — the 9th value appended after `Group` in `Decision.cs`, with the `Admin`/`Group`-style doc-comment (ADR 0028; G·3; the "never a content read" pin).
  - `### IUserInfoService guardian seams (exact C#)` — the **five ADDs** (each with its doc-comment: the standing gate = active link, the audit verb + `Via`, the invariant it enforces):
    - `Task<GuardianLink> CreateGuardianLinkAsync(string childId, string guardianId);` — formation (G·4; audit `guardian.create`, `Via: Guardian`, target the child account); upserts the `(GuardianId, ChildId)` `Active` row.
    - `Task SuspendChildAsync(string childId, string guardianId);` / `Task UnsuspendChildAsync(string childId, string guardianId);` — sets `Profile.Blocked` true/false (G·2 live; audit `guardian.suspend` / `guardian.unsuspend`, `Via: Guardian`); the GlobalAdmin `BlockAsync`/`UnblockAsync` are **unchanged**.
    - **Membership curation** — the ADR 0012 `AddCommunityMemberAsync(componentId, userId, actorId, actorRoles)` / `RemoveCommunityMemberAsync(…)` and the group `AddGroupMemberAsync(groupId, userId, addedBy)` / `RemoveGroupMemberAsync(…)` **grow a `Via: Guardian` branch**: when the actor has an **active link over `userId`** (the target child) the standing gate is satisfied; the audit row records the **narrower** standing `Via: Guardian` (the ADR 0012 "record the narrower standing" rule). **No new method** — a branch on the existing seams (the exact seam to grow is named).
    - `Task<GroupInvitation> ApproveGroupInvitationAsync(string groupId, string childId, string guardianId);` — resolves the child's `Pending` invitation as `Accepted` (the `AcceptGroupInvitationAsync` membership write + `ResolvedAt`/`ResolvedBy = guardianId`), audit `group.invite.approve` (`Via: Guardian`, targetKind "group").
    - `Task DissolveGuardianLinkAsync(string linkId, string actorId, bool viaAdmin);` — moves the row `Active → Dissolved` (`DissolvedAt`/`DissolvedBy = actorId`); audit `guardian.dissolve`, `Via: Guardian` (guardian) or `Via: Admin` (the G·5 safety valve, when `viaAdmin` is true). The child's memberships are **preserved**; the self-lanes restore on the next read (G·2/C4).
    - **The invitation gate** — `AcceptGroupInvitationAsync(groupId, actorId)` **gates**: if the invitee has an active `GuardianLink`, the self-accept is refused (`InvalidOperationException` → the Web's error, never a 500); `DeclineGroupInvitationAsync` **stays open** (a child may always say no).
  - `### Pinned seam tests (exact names)` — file `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` (the **load-bearing** tests first):
    1. `G1_GuardianCannotReadChildContent` — **the lane's honesty**: a guardian, given the child's id, is **denied** a post the child authored for a non-guardian audience; the `CanAsync` decision carries no `Via: Guardian` branch.
    2. `G2_SuspendIsLiveAndBlocksStanding` — suspend → the account is standing-less + directory-excluded (`Profile.Blocked`); un-suspend → standing restored on the next read.
    3. `G2_DissolveRestoresSelfLanesOnNextRead` — dissolve → the child's self-accept lane is live on the very next attempt (C4).
    4. `G3_NonChildTargetIsRefused` — a guardian acting on a *non-child* target is refused (`UnauthorizedAccessException`).
    5. `G3_ContentReadIsNeverGuardian` — a guardian attempting a content read is refused; no `Via: Guardian` on the path (the G·1 unit-level twin).
    6. `G4_FormationCommitsAccountLinkAndAuditTogether` — account + link + audit land in one commit; a duplicate `(guardian, child)` is an idempotent no-op.
    7. `G5_GlobalAdminDissolvesAndUnSuspends` — the safety valve: GlobalAdmin dissolves an active link (`Via: Admin`) and un-suspends; the child's self-lanes restore.
    8. `Invitation_GatedForSupervisedChild` — a child with an active link **cannot** self-accept a group invitation (refused) but **can** self-decline.
    9. `Invitation_GuardianApproveLandsMembership_ViaGuardian` — `ApproveGroupInvitationAsync` lands the membership, audit `group.invite.approve`, `Via: Guardian`.
    10. `Membership_AddRemoveChild_ViaGuardian` — the guardian adds/removes the child from a component **and** a group; the audit rows record `Via: Guardian`; the ADR 0012 posting/feed gate reflects it on the next read.
    11. `SuspendSetsProfileBlocked_EnforcementIdentical` — the flag the existing `BlockedAccountMiddleware` / directory already read is the one set (enforcement parity).
  - `### Acceptance gate (U10 records)` — the three tests: **closed loop** (a parent forms a child account, suspends it, curates a membership, approves an invitation, then dissolves — each lands its audit row + effect on the next read); **handoff** (dissolve hands the account to the child — the child's self-lanes restore live, memberships preserved — the "come of age" handoff); **part-vs-whole** (the 11-test list is the whole; closed-loop + handoff are the parts; all must pass together).
  - `### Drift-guard (frozen once written)` — the `GuardianLink` POCO, the `AccessVia.Guardian` value + its position (9th), the five seam signatures + the two membership-lane branches + the invitation gate, the 11 pinned test names, the G·1–G·5 invariants, and the acceptance gate — all frozen pins; any mismatch is a `## U<m> — Drift pause`.
- **Exit:** the design doc has the `## Pinned contract` section with all sub-sections + the **11 pinned test names**. **No build.** Handoff note: 6–8 lines starting `## U01 — pinned contract` — (a) the five seam names (verbatim), (b) the `AccessVia.Guardian` position (9th), (c) the **11 pinned test names**, (d) the G·1 pin ("no `Via: Guardian` on the content path"), (e) any drift pause.

### U02 — `GuardianLink` POCO + `M1DocTypes` registration
- **Goal:** create the `GuardianLink` document (the `DelegationGrant`/`GroupInvitation` shape) + the `GuardianLinkStatus` enum, and register it on the **existing** `M1DocTypes` surface (additive line — no new `*DocTypes`, no re-seed, ADR 0004 §B.1).
- **Entry reads:** `docs/design/guardian-controls-design.md` §Pinned contract (the exact POCO — the *primary* source); `src/Kumunita.Core/UserInfo/Group.cs` §`DelegationGrant` (the shape + the `IsActiveAt` "service is the resolver" comment to mirror); `src/Kumunita.Core/UserInfo/GroupInvitation.cs` (the `Status` enum + `ResolvedAt`/`ResolvedBy` + the business-key convention); `src/Kumunita.Core/M1DocTypes.cs` (where to add the `Schema.For<GuardianLink>()` line + the `GroupInvitation` `.UniqueIndex(i => i.GroupId, i => i.UserId)` convention to mirror as `(GuardianId, ChildId)`).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/UserInfo/GuardianLink.cs` (new) — the exact POCO per the pinned contract (`Id`, `GuardianId`, `ChildId`, `Status`, `CreatedAt`, `DissolvedAt?`, `DissolvedBy?`) + the `GuardianLinkStatus { Active, Dissolved }` enum (file-scoped or in the same file — match the `InvitationStatus` placement in `GroupInvitation.cs`).
  - `src/Kumunita.Core/M1DocTypes.cs` (modify) — add **one** line next to `opts.Schema.For<DelegationGrant>();`: `opts.Schema.For<GuardianLink>().UniqueIndex(g => g.GuardianId, g => g.ChildId);` (the business-key convention; the surrogate `Id` is the Marten identity).
- **Exit:** `dotnet build` green. `GuardianLink.cs` compiles; the `M1DocTypes` line is additive (no other line changed). **No new test.** Handoff note: 4–5 lines starting `## U02 — GuardianLink + M1DocTypes` — (a) the POCO's fields (verbatim), (b) the exact `M1DocTypes` line + its placement (after `DelegationGrant`), (c) the unique-index fields, (d) any compile warnings.

### U03 — `AccessVia.Guardian` (the 9th value)
- **Goal:** append `Guardian` as the **9th** value on `AccessVia` (after `Group`) — the M1 `Admin` / ADR 0013 `Group` append precedent. **No renumbering; no stored row's meaning changes.** No other file touched.
- **Entry reads:** `docs/design/guardian-controls-design.md` §Pinned contract (the exact value + doc-comment); `src/Kumunita.Core/Authorization/Decision.cs` (the `AccessVia` enum — the `Admin` + `Group` append doc-comments to mirror); `docs/adr/0028-guardian-controls-account-scope-supervision.md` §A (the standing's ADR authority).
- **Deliverables (1 file, modify):** `src/Kumunita.Core/Authorization/Decision.cs` — add `Guardian` after `Group` (with a trailing comma on `Group`), with the doc-comment: the GU standing (ADR 0028); action-scoped to the five supervisory actions (G·3); **never** on a `CanAsync` / `CanSeeAsync` content decision (G·1).
- **Exit:** `dotnet build` green. `AccessVia` has exactly **9** values, `Guardian` last. **No new test** (U09 pins it). Handoff note: 3–4 lines starting `## U03 — AccessVia.Guardian` — (a) the enum now has 9 values, (b) `Guardian` is the 9th / last, (c) the G·1 doc-comment line (verbatim), (d) a confirmation no existing value's position or name changed.

### U04 — Core: formation + suspension + independence seams
- **Goal:** implement the three `IUserInfoService` seams that are **new** methods (not branches): `CreateGuardianLinkAsync`, `SuspendChildAsync` / `UnsuspendChildAsync`, and `DissolveGuardianLinkAsync` — each resolving standing **live** off the active link (G·2) and **deny-by-default** (G·3).
- **Entry reads:** `docs/design/guardian-controls-design.md` §Pinned contract (the exact signatures + doc-comments + audit verbs) + § Invariants G·2/G·4/G·5; `src/Kumunita.Core/UserInfo/IUserInfoService.cs` (the surface to extend + the existing `SetCommunityMandatoryAsync` / `AddCommunityMemberAsync` doc-comment/exception conventions to mirror); `src/Kumunita.Core/UserInfo/UserInfoService.cs` (the impl shape to extend — the write-session + audit-row pattern, the standing-gate pattern); `src/Kumunita.Core/UserInfo/Profile.cs` (the `Blocked` field the suspension lane sets + the `SubjectId` the child row keys on); `src/Kumunita.Core/Identity/IIdentityService.cs` (the `BlockAsync` / `UnblockAsync` **unchanged** lanes the new lane mirrors; the `RegisterAsync` seam U08 pairs with); `src/Kumunita.Core/UserInfo/GuardianLink.cs` (U02's doc) + `src/Kumunita.Core/M1DocTypes.cs` (confirm the doc is registered).
- **Deliverables (≤ 3 files):**
  - `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — append the **exact** three (+ the suspend/un-suspend pair) seams from the pinned contract, each with its doc-comment (standing gate = active link; audit verb + `Via`; the invariant it enforces; the exceptions: `UnauthorizedAccessException` on no link, `InvalidOperationException` on the missing/dissolved row).
  - `src/Kumunita.Core/UserInfo/UserInfoService.cs` — implement them. `CreateGuardianLinkAsync` upserts the `(GuardianId, ChildId)` `Active` row (idempotent) + audits `guardian.create`. `SuspendChildAsync`/`UnsuspendChildAsync` verify the active link, set `Profile.Blocked`, audit `guardian.suspend`/`guardian.unsuspend`. `DissolveGuardianLinkAsync` moves the row to `Dissolved` (`DissolvedAt`/`DissolvedBy`), audits `guardian.dissolve` with `Via: Admin` when `viaAdmin` else `Via: Guardian`. All in one write session, audit in-transaction (C3).
  - `src/Kumunita.Core/DependencyInjection.cs` — **only if** `UserInfoService`'s ctor changes (it should not — these are method additions on the existing service); confirm the registration is unchanged.
- **Exit:** `dotnet build` green. The three seams resolve standing live; the GlobalAdmin `BlockAsync`/`UnblockAsync` are **unchanged**. **No new test** (U09 pins). Handoff note: 5–6 lines starting `## U04 — formation/suspension/independence seams` — (a) the seam names (verbatim) + the audit verb each emits, (b) the "standing = active link, resolved live" line (G·2), (c) a confirmation `BlockAsync`/`UnblockAsync` were untouched, (d) a confirmation the child's account + link commit path is one Web transaction's job (U08), (e) any compile warnings.

### U05 — Core: membership curation admits the guardian standing
- **Goal:** grow the **existing** ADR 0012 + group membership lanes with a `Via: Guardian` branch so a guardian can add/remove a **child** they have an active link over — the audit row records the **narrower** standing. **No new method; a branch on the existing seams (G·3 deny-by-default otherwise).**
- **Entry reads:** `docs/design/guardian-controls-design.md` §Pinned contract (the exact seam to grow + the "record the narrower standing" rule) + § Invariants G·2/G·3; `src/Kumunita.Core/UserInfo/IUserInfoService.cs` §`AddCommunityMemberAsync` / `RemoveCommunityMemberAsync` (the standing gate to extend — the `actorRoles`-based gate today) + §`AddGroupMemberAsync` / `RemoveGroupMemberAsync`; `src/Kumunita.Core/UserInfo/UserInfoService.cs` (the impl of those four lanes — where the standing gate is checked + the audit row built, so the `Via: Guardian` branch is added in the same place); `docs/adr/0012-community-membership-mandatory-and-moderator.md` (the "record the narrower standing" rule + the mandatory-community no-op the branch must still honor).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/UserInfo/UserInfoService.cs` — in each of the four lanes' standing-gate, add the **guardian branch first**: *if* the actor has an **active link over the target `userId`** ⇒ standing is **satisfied** (the gate does NOT throw for this actor) and the audit row records the **narrower** standing `Via: Guardian`; *else* the existing gate applies as today (`Owner`/`Moderator`/`Admin`, or `UnauthorizedAccessException` for a non-guardian with no role — the branch **adds** a path, it never **throws** on its own). The `RemoveCommunityMemberAsync` mandatory-community `InvalidOperationException` and the ADR 0008 group-owner-row exception are **preserved** (the branch adds a path, never removes one). The `AddCommunityMemberAsync` idempotent upsert is unchanged.
  - `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — update the **doc-comment** on the four lanes to name the new `Via: Guardian` branch (the ADR 0006-E "named here" list) — **signature unchanged** (still `(componentId, userId, actorId, actorRoles)` / `(groupId, userId, addedBy)`).
- **Exit:** `dotnet build` green. The four lanes admit a guardian over a child they supervise and record `Via: Guardian`; every other standing path is byte-identical. **No new test** (U09's `Membership_AddRemoveChild_ViaGuardian` pins it). Handoff note: 5–6 lines starting `## U05 — membership curation branch` — (a) the four lanes touched (by name), (b) the "record the narrower standing `Via: Guardian`" line, (c) a confirmation the mandatory-community + owner-row exceptions are preserved, (d) a confirmation the four **signatures** are unchanged, (e) any deviation from the pinned contract.

### U06 — Core: the invitation gate + `ApproveGroupInvitationAsync`
- **Goal:** gate the child's **self-accept** while a `GuardianLink` is active (the C-M2b·2 self-lane's one recorded exception), and add the guardian's `ApproveGroupInvitationAsync` that resolves the child's `Pending` invitation. The child's **self-decline stays open**.
- **Entry reads:** `docs/design/guardian-controls-design.md` §Pinned contract (the gate + the new seam's exact shape + the `group.invite.approve` audit verb) + § Invariants G·2/G·3; `src/Kumunita.Core/UserInfo/IUserInfoService.cs` §`AcceptGroupInvitationAsync` / `DeclineGroupInvitationAsync` / `InviteGroupMemberAsync` (the self-lane + the `group.invite.*` audit vocabulary + the `ResolvedAt`/`ResolvedBy` stamps to mirror); `src/Kumunita.Core/UserInfo/UserInfoService.cs` (the `AcceptGroupInvitationAsync` impl — where the gate + the new seam's membership write land); `src/Kumunita.Core/UserInfo/GroupInvitation.cs` (the `InvitationStatus` machine + the business key the new seam resolves).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — add **one** seam (the pinned contract's `ApproveGroupInvitationAsync(groupId, childId, guardianId)`) + update the `AcceptGroupInvitationAsync` doc-comment to name the gate (refused for a supervised child; the decline lane stays open — ADR 0008 owner-row-exception shape, carried to the supervised-child row).
  - `src/Kumunita.Core/UserInfo/UserInfoService.cs` — (a) in `AcceptGroupInvitationAsync`, before the membership write: if the invitee has an active `GuardianLink`, throw `InvalidOperationException` (the Web surfaces the error, never a 500). (b) `ApproveGroupInvitationAsync`: verify the guardian's active link over `childId`, resolve the child's `Pending` row as `Accepted` (the same membership upsert + `ResolvedAt`/`ResolvedBy = guardianId` as `AcceptGroupInvitationAsync`), audit `group.invite.approve` (`Via: Guardian`, targetKind "group").
- **Exit:** `dotnet build` green. A supervised child **cannot** self-accept but **can** self-decline; the guardian's approve lands the membership + audit row. **No new test** (U09's two `Invitation_*` tests pin). Handoff note: 5–6 lines starting `## U06 — invitation gate + approve` — (a) the gate's throw type (`InvalidOperationException`), (b) the new seam name + audit verb (`group.invite.approve`), (c) a confirmation `DeclineGroupInvitationAsync` is **unchanged** (self-decline stays open), (d) a confirmation the membership write reuses the `AcceptGroupInvitationAsync` path, (e) any deviation from the pinned contract.

### U07 — Web: `GuardianController` + view models
- **Goal:** the thin HTTP surface — `GuardianController` with the child-list, suspend/un-suspend, community & group membership editor, the pending-invitation approval list, the dissolve control, and the add-a-child form (which drives `RegisterAsync` + `CreateGuardianLinkAsync` in **one** commit). All standing comes from the Core seams; the controller is thin (route + authz + shape, never a re-derivation of access). Failure shape is the existing lane's (404 / error on no standing — a non-guardian learns nothing).
- **Entry reads:** `docs/design/guardian-controls-design.md` § Surfaces + § Pinned contract (the seam signatures the controller calls); `src/Kumunita.Web/Controllers/GroupsController.cs` (the thin-controller pattern to mirror — the route, the authz, how a Core seam is called, how a lane's `InvalidOperationException`/`UnauthorizedAccessException` is surfaced as a 404/error); `src/Kumunita.Web/Controllers/CommunityController.cs` (the ADR 0012 membership-lane Web pattern — the `actorRoles` the membership editor must pass through); `src/Kumunita.Web/Models/` (a representative VM — the record/class shape to mirror); `src/Kumunita.Core/Identity/IIdentityService.cs` §`RegisterAsync` (the add-a-child form's Core call) + `src/Kumunita.Core/Identity/IdentityService.cs` (how the verification email is staged — the form must NOT bypass it).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Controllers/GuardianController.cs` (new) — `[Authorize]` on all actions (see the U07 unit plan for the **exact** route table + the `SubjectId`-from-cookie-principal rule). Route family: `GET /me/children` → the parent's child list; `GET /me/children/{childId}` → the per-child curation view (group + community memberships **and** the pending invitations); `POST /me/children/{childId}/suspend` + `/unsuspend`; `POST /me/children/{childId}/memberships/group` (add/remove a group, calling the U05 branch lanes); `POST /me/children/{childId}/invitations/{groupId}/approve` (U06's seam); `POST /me/children/{childId}/dissolve`; the add-a-child form `GET /me/children` (form) + `POST /me/children` (`RegisterAsync` then `CreateGuardianLinkAsync` in **one** request/commit — a created-but-unlinked account can never exist). G·1: **no** route reads a child's content.
  - `src/Kumunita.Web/Models/GuardianViewModels.cs` (new) — `ChildAccountItem { ChildId, DisplayName, Blocked }`, `MembershipEditorModel { ChildId, GroupIds, CommunityIds, PendingInvitations }` (the three curation sets, **ids only** — G·1), `PendingInvitationItem { GroupId, GroupName, InvitedAt }`, `AddChildForm { DisplayName, Email, Password }` (the exact field sets are in the U07 unit plan; U10's Web tests pin them).
- **Exit:** `dotnet build` on `Kumunita.Web` green. The add-a-child form pairs `RegisterAsync` + `CreateGuardianLinkAsync` in one commit. **No new test** (U09 Core tests + U10 Web tests pin). Handoff note: 6–8 lines starting `## U07 — GuardianController + VMs` — (a) the routes (exact, for U08's views), (b) the one-commit formation note (file + the session usage), (c) the `actorRoles` the membership POST passes (from the existing `CommunityController` pattern), (d) the failure shape (404/error on no standing), (e) any compile warnings.

### U08 — Web: `Views/Guardian/*` + nav entry
- **Goal:** the Razor surface for U07's VMs — the child list (with the suspend/un-suspend toggle + the dissolve control), the membership editor, the pending-invitation approval list, and the add-a-child form — plus **one** nav entry. Reuse the existing audience/partial patterns; **no new TS subsystem** (plain inline handlers or the existing `site.js` style — a confirm-guard on the destructive actions).
- **Entry reads:** `src/Kumunita.Web/Views/Shared/_Layout.cshtml` + `_AccountNav.cshtml` (the nav to add the one entry to + the flash/toast + confirm-guard patterns); `src/Kumunita.Web/Views/Groups/*` (the Razor patterns to mirror — the list, the form, the confirm-guard on a destructive POST); `src/Kumunita.Web/Models/GuardianViewModels.cs` (U07's models — the `@model` directives must match exactly); `src/Kumunita.Web/wwwroot/js/site.js` (the existing JS style — add a confirm-guard handler if one is needed; no bundler).
- **Deliverables (≤ 4 files):**
  - `src/Kumunita.Web/Views/Guardian/Index.cshtml` (new) — the child list (each: display name + `Blocked` badge + suspend/un-suspend toggle) + the add-a-child link + the empty-state (a non-guardian's page).
  - `src/Kumunita.Web/Views/Guardian/Detail.cshtml` (new) — the per-child curation view: the community & group membership editor (add/remove) + the pending-invitation approval list + the dissolve control (confirm-guarded). The child's *content* is **never** shown here — G·1 in the UI: **no** link to a child's posts.
  - `src/Kumunita.Web/Views/Guardian/New.cshtml` (new) — the add-a-child form (display name / email / password → U07's POST).
  - `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (modify) — **one** nav entry (the anti-pattern "part sprawl": multiple links to the same module is sprawl). An optional confirm-guard in `site.js` for the suspend/dissolve POSTs (UX only, not a security gate).
- **Exit:** `dotnet build` green. The four surfaces render; the nav has **one** entry; the destructive actions are confirm-guarded. **No new test** (U10's Web data-shape tests pin the VMs). Handoff note: 5–6 lines starting `## U08 — views + nav` — (a) the 3 view paths + the 1 nav line, (b) the confirm-guard mechanism (inline `onsubmit` vs a `site.js` handler), (c) a confirmation the UI shows **no** link to a child's private content (G·1), (d) any markup the U10 Web tests must reflect.

### U09 — Core seam tests (the 11 pinned names)
- **Goal:** implement the **11** tests from the design doc §Pinned contract in `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` (the M2 `DirectoryServiceTests.cs` / M3 `PostServiceTests.cs` shape — the `PostgresFixture`, the seed-and-assert pattern). **The load-bearing test is `G1_GuardianCannotReadChildContent`** (G·1). **This unit does NOT run the acceptance gate (U10) and does NOT author Web tests (U10).**
- **Entry reads:** `docs/design/guardian-controls-design.md` §Pinned contract § Pinned seam tests (the **primary** source — the 11 names, exact); `tests/Kumunita.Core.Tests/DirectoryServiceTests.cs` or `PostServiceTests.cs` (the seam-test shape to mirror — the `PostgresFixture` usage, the seed-and-assert, the audit-row assertions); `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the harness); `src/Kumunita.Core/UserInfo/UserInfoService.cs` (the impls U04–U06 wrote — the exception types + audit verbs the tests assert).
- **Deliverables (1 file, new):** `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` — **11 tests**, one per pinned name (the exact list in the design doc §Pinned contract): `G1_GuardianCannotReadChildContent`, `G2_SuspendIsLiveAndBlocksStanding`, `G2_DissolveRestoresSelfLanesOnNextRead`, `G3_NonChildTargetIsRefused`, `G3_ContentReadIsNeverGuardian`, `G4_FormationCommitsAccountLinkAndAuditTogether`, `G5_GlobalAdminDissolvesAndUnSuspends`, `Invitation_GatedForSupervisedChild`, `Invitation_GuardianApproveLandsMembership_ViaGuardian`, `Membership_AddRemoveChild_ViaGuardian`, `SuspendSetsProfileBlocked_EnforcementIdentical`.
- **Exit:** `dotnet build` green. Run via the **reliable path** (AGENTS.md): `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` — reports **11 GU tests discovered** (the file's others may co-run). Record the pass/red status of each (for U10's gate). **No gate recorded** (U10). **Clean up Docker:** if the process was killed, `docker container prune`. Handoff note: 3 lines starting `## U09 — seam tests (11)` — (a) the file path, (b) the 11 names (verbatim), (c) the pass/red counts (for U10 to consume), (d) any `## U<m> — Drift pause`.

### U10 — run + record the GU acceptance gate + Web tests
- **Goal:** execute and **record** the three-test acceptance gate (closed-loop / handoff / part-vs-whole) from the design doc §Pinned contract, *using* U09's 11 tests as the part-vs-whole evidence; and author the **Web** controller/VM data-shape tests (the codebase unit-tests controller/VM shape, not Razor). Record both in the design doc.
- **Entry reads:** `docs/design/guardian-controls-design.md` §Pinned contract § Acceptance gate (the three test names + definitions); `docs/plans-milestones/in-progress/guardian-controls-handoff-notes.md` (U09's section — the 11-test results the gate references); `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` (the 11 tests); `src/Kumunita.Web/Models/GuardianViewModels.cs` + `src/Kumunita.Web/Controllers/GuardianController.cs` (the Web surface U10's Web tests target); `tests/Kumunita.Web.Tests/` (an existing Web data-shape test to mirror the harness).
- **Deliverables (2 files):**
  - `tests/Kumunita.Web.Tests/GuardianViewModelsTests.cs` (new) — the controller/VM data-shape tests: the child list reflects `Blocked` + active links; the membership editor model carries the child's communities/groups; the pending-invitation list shape; the add-a-child form binds (the `RegisterAsync` + `CreateGuardianLinkAsync` pairing is asserted at the VM level). Match the existing Web-test harness.
  - `docs/design/guardian-controls-design.md` (modify) — append `### Run result (GU acceptance gate — <date>)`: the three test names, their pass/red status, the 11 Core-test count (U09) + the Web-test count (this unit), and one line per any `## U<m> — Drift pause` in the handoff note (each resolved or still open).
- **Exit:** `dotnet build` green; both test assemblies run via the reliable path and the GU tests are green; the gate section is present and consistent. Handoff note: 4–5 lines starting `## U10 — gate recorded` — the three test names + pass counts + the date + the Core + Web test counts + any still-open drift.

### U11 — close: `ARCHITECTURE.md` flip + `## GU — Closed (recorded)` + move to `done/`
- **Goal:** flip the `UserInfo/` line in `docs/ARCHITECTURE.md` to record `GuardianLink` + the GU standing (M4/M5/M6 lines untouched), write the **`## GU — Closed (recorded)`** section in the design doc, and **move the lane's files to `done/`** (the register, this file's unit plans `guardian-controls-uNN-plan.md`, and the handoff notes) — the loop-closing step (the M2 U15 / M3 U12 analog).
- **Entry reads:** `docs/ARCHITECTURE.md` §2 (the `UserInfo/` line to extend + the `Posts/` line as the "✓ live" flip precedent; the `Events/`/`Projects/` lines **untouched**); `docs/design/guardian-controls-design.md` (full — the invariants, the FACES, the gate, the drift-guard the close summarizes); `docs/plans-milestones/in-progress/plan-guardian-controls.md` (this register, full); `docs/plans-milestones/in-progress/guardian-controls-handoff-notes.md` (all `##` sections U01–U10 wrote).
- **Deliverables (3 files modify + 12 file moves):**
  - `docs/ARCHITECTURE.md` — the `UserInfo/` line: add `GuardianLink` (the GU standing, ADR 0028) to the module's document list, marked ✓, with a one-line gate summary (the five supervisory actions, no content read). `Events/`/`Projects/` lines untouched.
  - `docs/design/guardian-controls-design.md` — append `## GU — Closed (recorded)`: the three tests (U10's record), the `ARCHITECTURE.md` flip (this unit), the ADR 0028 non-decisions (each named), and the "GU is closed; the three tests are recorded in §Pinned contract; the named deferrals are the ADR 0028 §E list" line.
  - **File moves (PowerShell `Move-Item`, one self-contained command):** `docs/plans-milestones/in-progress/plan-guardian-controls.md` → `done/`; every `docs/plans-milestones/in-progress/guardian-controls-uNN-plan.md` → `done/`; `docs/plans-milestones/in-progress/guardian-controls-handoff-notes.md` → `done/`. Confirm `in-progress/` is empty afterward.
- **Exit:** `ARCHITECTURE.md`'s `UserInfo/` line is flipped; the design doc ends with `## GU — Closed (recorded)`; the lane's files are in `done/` and `in-progress/` is empty. The handoff note's `## Summary` (this unit) is the sole GU→next handoff artifact. **No build** (docs + moves only). Handoff note: the `## Summary` section is present — a table of the shipped units (U01–U11) with their one-liner goal + test count + any deviations + the ADR 0028 §E deferral list (each named).
