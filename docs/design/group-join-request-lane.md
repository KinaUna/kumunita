# Design Doc — Resident join-request lane for public groups (ADR 0094)

> **Single-part unit doc** (the m3b two-part split is unnecessary at this
> lane's size). This doc pins **scope**, the **invariants** (`JR·1..3` + the
> carried M2/m2b pins), the **FACES rows (F21–F26)**, and the **exact seam
> shapes** the tests anchor to. It also records the **seam-test names**, the
> **handoff / drift notes** (the two M2/m2b view-model pin updates), and the
> **acceptance gate**.
>
> **Lane, not a re-open:** this is a *new design unit* on the M2 groups
> surface (precedent: m2b on the M2 groups surface, ADR 0013 / 0089 on the M3
> posts surface). The M2 / m2b pins — C-M2·1..3, C-M2b·1..3, F1–F20, the
> U9/U10 view-model shape pins — **stay authoritative**; this lane's pins
> reference them. The m2b owner-invited invitation lane is **kept
> side-by-side**, not replaced.
>
> **Supersedes the m2b deferral:** the m2b design doc's "No join-request flow
> — that would open the reader to owner∪member-less state" deferral is now
> superseded *for public groups only*. The private-group owner∪member model it
> protected is untouched (ADR 0010): a join-request is only ever offered on a
> **public** group, and private groups stay owner-invite-only.

## Context

M2 shipped the groups surface (list / create / detail / immediate
add-remove). M2b added the **owner-invited** membership lane on top of it
(the `GroupInvitation` doc, the seven `IUserInfoService` seams, the F16–F20
FACES). M2b deliberately deferred the *resident-initiated* direction: a
resident who found a public group and wanted in had **no lane** — they could
only wait to be invited.

ADR 0094 adds that missing direction on top, as the **reverse of the m2b
lane**:

- **Direction:** the **resident requests**, the **owner ∪ GlobalAdmin
  resolves** (approve / decline). The resident can **withdraw** a pending
  request (self-lane terminal, the m2b owner's `Cancelled` analogue).
- **Public groups only.** The request button / card is rendered **only** for
  a group that is public *and* not already joined. A private group is never
  exposed to the join-request surface — the m2b owner∪member model it guarded
  is unchanged (ADR 0010).
- **Coexistence:** the m2b invitation lane is untouched and stays the
  owner-initiated surface; the join-request lane is a second, resident-
  initiated add-path. They share the same standing, audit, and state-machine
  conventions but are separate docs and separate routes.
- **Consent model:** the *request* is the resident's self-initiated action
  (the audit row is `Via Owner`, the requester's own standing). Approval is
  the owner's / admin's consent decision — the `group.join.approve` row carries
  the resolver's standing (`Owner` if the owner, else `Admin`).

## Scope

**In-scope (the join-request lane):**

1. One new Core document, `GroupJoinRequest` (+ the `JoinRequestStatus` enum
   `Pending/Approved/Declined/Withdrawn`) — the request row with its state
   machine.
2. The `M1DocTypes` registration, `UniqueIndex(GroupId, UserId)` (the
   one-row-per-(group, user) business-key convention of `GroupMembership` /
   `GroupInvitation`).
3. Six new named seams on `IUserInfoService` (ADR 0006-E compatible lane — no
   signature change to any pinned M1/M2/m2b method): four writes
   (`RequestToJoinGroupAsync`, `ApproveJoinRequestAsync`,
   `DeclineJoinRequestAsync`, `WithdrawJoinRequestAsync`) + two reads
   (`GetPendingJoinRequestsForUserAsync`,
   `GetPendingJoinRequestsForGroupAsync`).
4. The Web surface: two projection records on
   `src/Kumunita.Web/Models/GroupViewModel.cs`
   (`JoinRequestViewModel`, `PendingJoinRequestViewModel`), the
   `GroupListViewModel.PublicGroups` + `GroupListViewModel.MyJoinRequests`
   lanes, the `GroupDetailViewModel.PendingJoinRequests` lane (the 22nd field
   — the drift-guard update recorded in § Handoff below), four new routes on
   `GroupsController`, and the two view additions (Index's "Other public
   groups" directory + "Your join requests" card; Detail's "Pending join
   requests" review surface).
5. **`DeleteGroupAsync` cascade extension** (ADR 0093): deleting a group now
   also removes its `GroupJoinRequest` rows, in the same session as the
   `Group` / `GroupMembership` / `GroupInvitation` removal.
6. Nine new `kw-l` keys × 4 languages (ADR 0015 registry; the En/De/Fr/Da
   parity is preserved — see `KnownTranslationKeys`):
   `groups.join_requests`, `groups.join_requests_pending`,
   `groups.join_requests_note`, `groups.join_withdraw`,
   `groups.other_public`, `groups.request_join`,
   `groups.join_requests_pending_heading`, `groups.join_approve`,
   `groups.join_decline`.
7. The tests: the new Core seam suite
   (`UserInfoServiceGroupJoinRequestTests`) + the same-commit pin updates to
   the two M2/m2b drift-guard files.

**Out-of-scope (the join-request lane's M1-style close):**

- **Notification delivery** on request/approve (M1's single durable email
  handler is the notification lane; this lane is UI-only — the pending request
  row *is* the notification, the m2b "a pending invitation *is* the
  notification" shape). A "you have N requests" owner inbox lane is a future
  lane (the ADR 0083 inbox surface is not re-opened here).
- The **`Community`** axis, `Directory`, or any `Group` surface other than the
  public `Groups` list + public group detail.
- **Private groups** (ADR 0010) — the join-request surface is never rendered
  for them; they remain owner-invite-only.
- The M2 / m2b pins — **unchanged**; this lane's close is its own
  `## Closed (recorded)` section in *this* doc.
- **Delegation** on the join-request lane (ADR 0003 default-OFF is carried; a
  delegate may not stand in for the requester in the self-lane — `JR·2`, the
  m2b `C-M2b·2` analogue).

## Invariants (pinned for the join-request lane)

This lane is a *caller* of the ADR 0006 invariants (not an owner). It owns
**three** new invariants (`JR·1..3`) — the three behavioral rules this lane is
the first unit to need in the resident-initiated direction — plus the
**carried** M2/m2b pins that its reads / writes must keep holding.

| # | Lane pin | How the lane uses it |
|---|---|---|
| **JR·1** (lane-owned) | **SoD lane (the m2b `C-M2b·1` mirror).** Resolve (`ApproveJoinRequestAsync` / `DeclineJoinRequestAsync`) a join request ⇒ **owner ∪ GlobalAdmin only**. Web gate = the detail's `TryResolveOwnerSurface` (a plain member's POST 404s — the consistent write-lane failure shape). Audited `Via` is derived exactly like the invitation resolve lane: `resolvedBy == Group.OwnerId ⇒ Owner`, else `Admin` (the seam's single SoD source — the Web layer carries no form-bound owner id). | The approve/decline actions (F23/F24) are this lane's owner∪GlobalAdmin write surface. `Approve_Via_Owner_When_Resolver_Is_GroupOwner` + `Approve_Via_Admin_When_Resolver_Is_Not_The_Owner` + `Decline_AuditRow_Use_The_Resolver_Standing` pin the audit `Via` derivation. |
| **JR·2** (lane-owned) | **Self-lane.** Request (`RequestToJoinGroupAsync`) / withdraw (`WithdrawJoinRequestAsync`) ⇒ the **requester only** — the withdraw actor must equal the row's `UserId`, **verified in Core** (not only by the Web gate). Web gate = "in my pending list, else 404" (read lane #1). A foreign caller throws `InvalidOperationException`; the Web maps it to an error, never a 500. | The request/withdraw actions (F22/F25) are this lane's *self*-SoD. `WithdrawBy_A_Foreign_Account_Throws_JR_2` + `Withdraw_Unknown_Group_Or_Pair_Throws` pin the Core wall; `RequestAndWithdraw_AuditRows_Use_The_Requesters_Own_Standing` pins the `Via=Owner` self-audit shape. |
| **JR·3** (lane-owned) | **State machine (the m2b `C-M2b·3` mirror).** One row per (group, user) (the `UniqueIndex` business key); `Pending → {Approved, Declined, Withdrawn}`; **re-request resets to `Pending`**; an invalid transition (double-resolution, or resolve-after-resolution) ⇒ `InvalidOperationException`. `Withdrawn` is the self-lane terminal (the m2b owner's `Cancelled` analogue) — it drops the row off both pending lists and stays re-requestable. The two resolve stamps (`ResolvedAt`/`ResolvedBy`) clear on a re-request reset. | `Resolved_Row_Cannot_Approve_Decline_Or_Withdraw_Again_JR_3` + `ReRequest_After_Decline_Resets_To_Pending_JR_3` + `Requesting_The_Same_Pair_Twice_Keeps_One_Row_JR_3` pin each wall; the Web maps the `InvalidOperationException` to `TempData["error"]`. |
| **C3** (carried — ADR 0006 + M1) | Every lane mutation writes its `AccessAudit` row in the **same transaction** (invariant C3): the `group.join.request` / `group.join.approve` / `group.join.decline` / `group.join.withdraw` rows, `TargetKind "group"`, `Outcome Allow`. | `Every_JoinRequest_Write_Appends_Its_AuditRow_C3` pins the four action ids on the `group` target-kind. |
| **C4** (carried — M1) | **Approve ⇒ live membership** on the very next `GetGroupIdsAsync` / `GetGroupsForUserAsync` call. A **request** / **decline** / **withdraw** itself never touches membership (C4's lane is approve-only). | `Request_Then_Approve_Membership_LiveOnNextCall_C4` pins both the positive (approve does, live-on-next-call) and the negative (request does not). |
| **C-M2·2** (carried — M2) | The **two new reads** (`GetPendingJoinRequestsForUserAsync`, `GetPendingJoinRequestsForGroupAsync`) append **no** `AccessAudit` row — they are candidate projections, not access decisions (C-M2·2 + C1). | `The_Two_JoinRequest_Read_Lanes_Append_No_AuditRow_CM2_2` pins the no-audit shape on both lanes. |
| **ADR 0028 §C / G·2** (carried) | **GU supervised-child gate on approve.** A supervised child (an active `GuardianLink`) is refused on the approve lane, exactly as `AcceptGroupInvitationAsync` refuses one — the consent-decision wall, not the request wall. | `Approve_GatedForSupervisedChild` pins the wall. |
| **ADR 0093** (carried) | **`DeleteGroupAsync` cascade.** Deleting a group removes its `GroupJoinRequest` rows in the same session as the `Group` / `GroupMembership` / `GroupInvitation` rows. | `DeleteGroup_Cascades_Its_JoinRequest_Rows` pins the cascade. |
| **ADR 0006-E** (carried — ADR 0006) | All seam additions are **new named methods** on `IUserInfoService` — no signature change to any pinned M1/M2/m2b method. | The `IUserInfoService` "join-request additions" block is the single addition; no existing method is touched. |

**ADRs the lane must keep holding** (pinned in the invariant rows above): ADR
0006-A/B/C (the read/write/audit split — exercised through the
`IUserInfoService` lane only, never the `IDocumentSession` overloads), ADR
0006-D (Web shapes HTTP, Core decides), ADR 0003 (SoD default-OFF — the `JR·1`
/ `JR·2` pins are its join-request face), ADR 0010 (private groups stay
owner-invite-only — the lane never surfaces them), ADR 0028 (the GU wall on
consent lanes), ADR 0093 (the group-delete cascade).

## FACES (join-request-added user-visible outcomes → invariant pins)

Continuing the FACES numbering (F1–F20 are unchanged, pinned by M2/m2b).
**FACES count: 6 new rows (F21–F26).** Each row is a resident-visible outcome
the lane seam tests must cover; the pin in the right column is the single
authority.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| F21 | On `/groups`, a signed-in resident sees a **"Other public groups"** directory — the public groups they are **not** yet a member of — each with a **"Request to join"** button; groups they already belong to are not re-listed here (private groups never appear in it). | JR·1/·2 (the public + not-a-member gate) + ADR 0010 (private untouched) |
| F22 | A resident who clicks **"Request to join"** on a public group they're not in creates a `Pending` request (self-lane, `Via Owner`); requesting the **same** public group again keeps a single row (JR·3 business key); the request does **not** make them a member. | JR·2 (self-lane) + JR·3 (one row per pair) + C4 (request never adds membership) |
| F23 | On `/groups/{id}`, the **owner ∪ GlobalAdmin** sees a **"Pending join requests"** review surface (responder display name + approve / decline); a **plain member** sees neither (the `TryResolveOwnerSurface` gate 404s a member's approve/decline POST). | JR·1 (SoD) + C-M2·2 (the review reads never audit) |
| F24 | The owner/admin **approves** a pending request — the `GroupMembership` lands **live on the very next** `GetGroupIdsAsync` / `GetGroupsForUserAsync` call (C4) and the responder's audit row carries the resolver standing (owner ⇒ `Owner`, admin ⇒ `Admin`); a **supervised child** is refused on approve (ADR 0028 §C). | C4 + JR·1 (Via) + ADR 0028 §C (GU wall) |
| F25 | On `/groups`, the requester sees a **"Your join requests"** card (the groups they requested, with a **Withdraw** button); **withdraw** drops the row off both lists (JR·3 `Withdrawn` terminal) and it stays re-requestable; a **foreign** resident cannot withdraw another's request (404 / error). | JR·2 (self-lane) + JR·3 (terminal + re-request) |
| F26 | Every lane write (request / approve / decline / withdraw) commits an `AccessAudit` row in the **same transaction** as the domain write (C3) — and **delete-group** cascades the group's join-request rows (ADR 0093). | C3 + ADR 0093 |

**FACES count (join-request): 6.** This count (and the invariant-pin per row)
is the input the close needs to name the seam-test list and the acceptance
gate without re-deriving them (the M2 §2.7 drift-guard).

## Seams & contracts (exact shapes)

> Every C# fragment below is **exact**: parameter lists, return types, and
> namespaces are the contract the tests anchor to. The `IUserInfoService`
> "join-request additions" block is the *only* change to the frozen M1/M2/m2b
> lane (ADR 0006-E). No existing method's signature is touched.

### New Core types (`Kumunita.Core.UserInfo`)

```csharp
// src/Kumunita.Core/UserInfo/GroupJoinRequest.cs
enum JoinRequestStatus
{
    Pending,
    Approved,
    Declined,
    Withdrawn
}

sealed class GroupJoinRequest
{
    public string Id { get; set; }                     // surrogate (Guid-N)
    public string GroupId { get; set; }                // business key (1/2)
    public string UserId { get; set; }                 // requester; business key (2/2)
    public JoinRequestStatus Status { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }    // null while Pending
    public string? ResolvedBy { get; set; }            // null while Pending
}
```

`M1DocTypes.Configure` registers the row:

```csharp
opts.Schema.For<GroupJoinRequest>()
    .UniqueIndex(r => r.GroupId, r => r.UserId);
```

The `UniqueIndex` is the **JR·3 business-key pin** (one row per (group, user);
re-request *resets* the existing row, it does not insert a second one — the
index is the enforcement wall).

### The six new `IUserInfoService` seams (exact)

```csharp
Task<GroupJoinRequest> RequestToJoinGroupAsync(string groupId, string actorId);
Task ApproveJoinRequestAsync(string groupId, string userId, string resolvedBy);
Task DeclineJoinRequestAsync(string groupId, string userId, string resolvedBy);
Task WithdrawJoinRequestAsync(string groupId, string actorId);

Task<IReadOnlyList<GroupJoinRequest>> GetPendingJoinRequestsForUserAsync(string userId);
Task<IReadOnlyList<GroupJoinRequest>> GetPendingJoinRequestsForGroupAsync(string groupId);
```

**Write semantics (exact, per `JR·1..3` + C3 + C4 + ADR 0028 §C carried):**

| Method | Precondition (else `InvalidOperationException`) | Post-state (same session) | `AccessAudit` row (same tx) |
|---|---|---|---|
| `RequestToJoinGroupAsync` | Group exists (re-request if a row exists in any status — JR·3) | Row = `Pending`, `RequestedAt=now`, stamps cleared; **no** `GroupMembership` lane | `group.join.request`, `TargetId=groupId`, `Via=Owner` (the requester's own standing) |
| `ApproveJoinRequestAsync` | **GU gate first** (active `GuardianLink` on `userId` ⇒ refuse — ADR 0028 §C); group exists; row exists for `(groupId, userId)`; row is `Pending` (JR·3) | Row = `Approved` (stamps set); `GroupMembership` upserted with `AddedBy=resolvedBy` (C4 live-on-next-call) | `group.join.approve`, `TargetId=groupId`, `Via=Owner` iff `resolvedBy==Group.OwnerId` else `Admin` |
| `DeclineJoinRequestAsync` | Group exists; row exists for `(groupId, userId)`; row is `Pending` (JR·3) | Row = `Declined` (stamps set); **no** `GroupMembership` lane | `group.join.decline`, `TargetId=groupId`, `Via=Owner` iff `resolvedBy==Group.OwnerId` else `Admin` |
| `WithdrawJoinRequestAsync` | Group exists; row exists for `(groupId, actorId)`; **actor == row.UserId** (JR·2 self-lane); row is `Pending` (JR·3) | Row = `Withdrawn` (stamps set — the requester); **no** `GroupMembership` lane | `group.join.withdraw`, `TargetId=groupId`, `Via=Owner` (the requester's own standing) |

**Read semantics (exact, per C-M2·2 carried):** both return live rows (no
projection, no cache — C4), filter `Status == Pending`, and append **no**
`AccessAudit` row. `GetPendingJoinRequestsForUserAsync` sorts `RequestedAt`
**desc** (newest first); `GetPendingJoinRequestsForGroupAsync` sorts
`RequestedAt` **asc** (oldest first — the owner's review queue). Both return
`Array.Empty<GroupJoinRequest>()` for an empty / null id (fail-safe, no
throw).

### Web surface (exact, per F21–F26)

`src/Kumunita.Web/Models/GroupViewModel.cs` — the two projection records (the
strict shape pins the drift-guard tests anchor to; see § Handoff):

```csharp
record JoinRequestViewModel(string GroupId, string GroupName);
record PendingJoinRequestViewModel(string SubjectId, string DisplayName);
```

- `GroupListViewModel` gains
  `IReadOnlyList<JoinRequestViewModel> MyJoinRequests` (default
  `Array.Empty<JoinRequestViewModel>()`) — F25's card — and a
  `PublicGroups` directory of public groups the actor is not yet a member of
  — F21's directory.
- `GroupDetailViewModel` gains a **22nd** field,
  `IReadOnlyList<PendingJoinRequestViewModel> PendingJoinRequests` (default
  `[]`) — F23's review surface. **This is the drift-guard pin update**
  (recorded in § Handoff).

`GroupsController` — four new routes (all `[ValidateAntiForgeryToken]`):

| Route | Action | Gate (404 on fail) |
|---|---|---|
| `POST /groups/{id}/join/request` | `RequestToJoin(id)` → `RequestToJoinGroupAsync(id, actor)` | group exists **and** `!IsPrivate` **and** actor not already a member (`GetGroupsForUserAsync`); Core `InvalidOperationException` → `TempData["error"]` + redirect `Index` |
| `POST /groups/{id}/join/withdraw` | `WithdrawJoinRequest(id)` → `WithdrawJoinRequestAsync(id, actor)` | actor is in **their own** `GetPendingJoinRequestsForUserAsync(actor)` for `id` (JR·2 Web gate) |
| `POST /groups/{id}/join-requests/{subjectId}/approve` | `ApproveJoinRequest(id, subjectId)` → `ApproveJoinRequestAsync(id, subjectId, resolvedBy: actor)` | `TryResolveOwnerSurface` (owner ∪ GlobalAdmin) |
| `POST /groups/{id}/join-requests/{subjectId}/decline` | `DeclineJoinRequest(id, subjectId)` → `DeclineJoinRequestAsync(id, subjectId, resolvedBy: actor)` | `TryResolveOwnerSurface` (owner ∪ GlobalAdmin) |

The `InvalidOperationException` from the Core state machine (JR·3) or
self-lane (JR·2) is caught **per-action** (not swallowed by a global 500) and
mapped to `TempData["error"]` + redirect — F22/F25's error rows. The audit
`Via` derivation is the seam's single SoD source (JR·1); the Web layer never
re-derives it.

`GroupsController.Index` loads the actor's own pending requests via
`GetPendingJoinRequestsForUserAsync(subject)` and projects them to
`JoinRequestViewModel` (the group name via `GetGroupAsync`), and the
`PublicGroups` directory (the public groups the actor is not a member of) —
the F21/F25 cards. `GroupsController.Detail` loads the group's pending
requests via `GetPendingJoinRequestsForGroupAsync(group.Id)` and projects them
to `PendingJoinRequestViewModel` (the requester's display name via
`GetProfileAsync`) — the F23 review surface. Neither the controller nor the
view has a channel to an owner-id form-bound field (ADR 0003 SoD by structural
identity, carried from the m2b invite lane).

## Seam-test names (landed, `Kumunita.Core.Tests`)

The join-request lane's seam tests, in
`UserInfoServiceGroupJoinRequestTests` (fresh scratch Postgres per test, the
`PostgresFixture` boot pattern in this assembly):

| Test name | Pin |
|---|---|
| `Request_Then_Approve_Membership_LiveOnNextCall_C4` | C4 carried + F24's "approve is live" row |
| `Request_Then_Decline_NoMembership_RowResolved` | JR·3 (state) + C3 (audit) + F24's "no membership on decline" |
| `Request_Then_Withdraw_NoMembership_RowResolved` | JR·2 (self-lane withdraw) + C3 + F25's "withdraw" |
| `Approve_Via_Owner_When_Resolver_Is_GroupOwner` | JR·1 (owner⇒Owner audit) |
| `Approve_Via_Admin_When_Resolver_Is_Not_The_Owner` | JR·1 (else⇒Admin audit) |
| `Decline_AuditRow_Use_The_Resolver_Standing` | JR·1 (decline `Via` derivation) |
| `RequestAndWithdraw_AuditRows_Use_The_Requesters_Own_Standing` | JR·2 (self-lane audit shape, `Via=Owner`) |
| `WithdrawBy_A_Foreign_Account_Throws_JR_2` | JR·2 (self-lane wall) + F25's "foreign 404" |
| `Withdraw_Unknown_Group_Or_Pair_Throws` | JR·2 (self-lane, unknown-pair lane) |
| `ApproveOrDecline_No_RequestRow_Throws` | JR·3 (resolve a non-existent / absent row wall) |
| `ReRequest_After_Decline_Resets_To_Pending_JR_3` | JR·3 (re-request reset) + F25's "re-request" |
| `Requesting_The_Same_Pair_Twice_Keeps_One_Row_JR_3` | JR·3 (business-key, one row per pair) + F22 |
| `Resolved_Row_Cannot_Approve_Decline_Or_Withdraw_Again_JR_3` | JR·3 (state machine, all resolve-after-resolution walls) |
| `Approve_GatedForSupervisedChild` | ADR 0028 §C (the GU wall on approve) |
| `Every_JoinRequest_Write_Appends_Its_AuditRow_C3` | C3 (same-tx audit on all four writes) + F26 |
| `The_Two_JoinRequest_Read_Lanes_Append_No_AuditRow_CM2_2` | C-M2·2 (read lanes never audit) + F23/F25's "card reads never audit" |
| `The_Pending_Read_Lanes_Filter_And_Order_LiveRows_C4` | C-M2·2 (filter) + C4 (live-on-next-call on the read lanes) |
| `DeleteGroup_Cascades_Its_JoinRequest_Rows` | ADR 0093 (the delete-group cascade) + F26 |

**Seam-test count (join-request): 18.** This count (and the pin per test) is
the input the acceptance gate needs (the M2 §2.7 drift-guard).

## Handoff / drift notes (the two M2/m2b view-model pin updates)

Per the M2 §2.7 drift-guard ("a shape that a later lane legitimately extends
is updated in the **same commit** with a drift note"), the two M2/m2b
drift-guard files are updated by this lane in the commit that lands
`GroupDetailViewModel`'s 22nd field:

- **`GroupsDetailViewModelTests.GroupDetailViewModel_Has_Exactly_21_Projected_Fields`**
  is renamed to `..._Exactly_22_Projected_Fields` and the expected field set
  is extended from the m2b 21 to the join-request 22 (adding
  `PendingJoinRequests`). The drift note: the 22nd field is the join-request
  lane's projection (F23's review surface) — not a source-field addition (the
  source `GroupJoinRequest` row's `Status`/`RequestedAt`/`Resolved*` still
  never reach the UI — the "when resolved, by whom" fact is on the
  `AccessAudit` lane, not the member-list-shaped UI). The m2b 21-field pin
  **stays authoritative** for the m2b surface; the 22nd is this lane's
  additive pin.
- **`GroupsViewModelTests`** gains the two new join-request shape-pin tests
  (`JoinRequestViewModel_Has_Exactly_Two_Projected_Fields`,
  `PendingJoinRequestViewModel_Has_Exactly_Two_Projected_Fields`) with the
  source-field exclusion guards (the `GroupJoinRequest` row's
  `Id`/`UserId`/`Status`/`RequestedAt`/`Resolved*` never appear on the
  projection) — the same "a projection is a small named set, not a document
  dump" pin the M2/m2b files carried. The existing M2/m2b pins are unchanged.

No **other** M2 / m2b / M3 pin is touched by this lane. If a later unit
(post-join-request) needs to extend these lanes further, that unit re-opens
the drift-guard in the same M2 §2.7 shape (same commit, drift note, this
file's FACES count → F27+).

## Acceptance Gate (recorded)

The lane's close is the checklist's "Full build + both suites green" +
"Commit":

- `dotnet build Kumunita.slnx -c Debug` → **0 errors.**
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → all pass (includes the updated `GroupsDetailViewModelTests` +
  `GroupsViewModelTests`).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  → all pass (includes the 18 new `UserInfoServiceGroupJoinRequestTests`
  lane + the unchanged M1/M2/m2b lanes).

## Closed (recorded)

Recorded at the commit that flips the checklist: the in-scope items 1–7
(scope section above) are landed, the **in-scope / out-of-scope split** is
honored (nothing beyond the six seams + two projection records + four routes +
two view lanes + the delete-group cascade + nine `kw-l` keys is touched on the
frozen M1/M2/m2b surface), the `JR·1..3` + carried invariants are pinned by
the 18 seam tests, and both suites are green.
