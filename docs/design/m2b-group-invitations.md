# Design Doc — M2b: owner-invited group membership (invite → accept/decline)

> **Single-part unit doc** (the m3b two-part split is unnecessary at m2b's
> size). Part A pins **scope**, **invariants** (`C-M2b·1..3` + carried M2/M3
> pins), the **FACES rows (F16–F20)**, and the **exact seam shapes** the
> tests in this unit anchor to. Part B records the **seam-test names**, the
> **handoff / drift notes** (the two M2 U9/U10 view-model pin updates), and
> the **acceptance gate** (recorded in `## M2b — Closed (recorded)`).
>
> **Lane, not a re-open:** m2b is a *new design unit* on the M2 groups
> surface (precedent: m3b on the M3 posts surface). The M2 pins — C-M2·1..3,
> F1–F15, the U9/U10 view-model shape pins — **stay authoritative**; m2b's
> pins reference them. The immediate add/remove member surface (U10/F7 pin)
> is **kept side-by-side**, not replaced.

## Context

M2 shipped the groups surface: list / create / detail / **immediate**
add-remove (`GroupsController`, `IUserInfoService` M2 lane,
`Groups/{Index,Detail,Create}.cshtml`; the nav link was the missing wire,
added pre-m2b). "Immediate add" is a flat admin-style write: the resident
cannot answer it, cannot see it coming, and a removed resident has no
record of *why*.

M2b adds the invitation lane on top of that, per the
user-approved decisions in the plan
(`docs/plans-milestones/done/plan-m2b-owner-invited-group-membership-(invite-acceptdecline).md`):

- **Direction:** the **owner invites**, the **invitee resolves** (accept /
  decline). No join-request flow — that would open the reader to
  owner∪member-less state and break the owner∪member privacy model.
- **Coexistence:** the immediate `AddGroupMemberAsync`/`RemoveGroupMemberAsync`
  pair is untouched and remains the U10/F7 pin's surface; the invitation is a
  second add-path with the invitee's consent in the audit lane.
- **No TTL / expiry:** pending invitations persist until resolved or
  cancelled; they are re-invitable after resolution (C-M2b·3). Cancellation
  is the owner/admin's only other lane.
- **Visibility:** the invitee sees their invitations on the `/groups` list
  (Index) — they **cannot** reach the group detail (owner∪member gate) until
  they accept.

## Scope

**In-scope (the m2b in-scope, verbatim from the plan's Decisions + Files):**

1. One new Core document, `GroupInvitation` (+ the
   `InvitationStatus` enum) — the invitation row with its state machine.
2. The `M1DocTypes` registration, `UniqueIndex(GroupId, UserId)` (the
   one-row-per-(group, user) business-key convention of
   `GroupMembership`).
3. Seven new named seams on `IUserInfoService` (ADR 0006-E compatible
   lane — no signature change to any pinned M1/M2 method): four writes
   (`InviteGroupMemberAsync`, `AcceptGroupInvitationAsync`,
   `DeclineGroupInvitationAsync`, `CancelGroupInvitationAsync`) + three
   reads (`GetGroupAsync`, `GetPendingInvitationsForUserAsync`,
   `GetPendingInvitationsForGroupAsync`).
4. The Web surface: two projection records on
   `src/Kumunita.Web/Models/GroupViewModel.cs`
   (`InvitationViewModel`, `PendingInvitationViewModel`), the
   `GroupListViewModel.Invitations` lane, the
   `GroupDetailViewModel.PendingInvitations` lane (the 7th field — the
   drift-guard update recorded in § Handoff below), four new routes on
   `GroupsController`, and the two view additions (Index's "Your
   invitations" card; Detail's "Pending invitations" + "Invite a resident"
   lanes).
5. The tests: the new Core seam suite
   (`UserInfoServiceGroupInvitationsM2bTests`) + the same-commit pin
   updates to the two M2 drift-guard files.

**Out-of-scope (m2b's M1-style close, mirroring the plan's out-of-lane list):**

- Email / notification delivery on invite (M1's single durable email
  handler is the notification lane; m2b is UI-only — a pending invitation
  *is* the notification).
- Invitations on the `Community` axis, on `Directory`, or on any other
  `Group` surface than `Groups` in M2.
- The M2 F1–F15 and C-M2·1..3 pins — **unchanged**; m2b's close is its own
  `## M2b — Closed (recorded)` section in *this* doc.
- Any re-opening of the immediate add/remove surface (U10/F7) — it stays
  exactly as M2 shipped it.
- Group self-join, invitation-by-invitee, and delegation on invitations
  (ADR 0003 default-OFF is carried; a delegate may not stand in for the
  invitee in the self-lane — C-M2b·2).

## Invariants (pinned for m2b)

m2b is a *caller* of the ADR 0006 invariants (not an owner). M2b owns
**three** new invariants (`C-M2b·1..3`) — the three behavioral rules m2b is
the first unit to need on the groups surface — plus the **carried** M2 pins
that m2b's reads / writes must keep holding.

| # | m2b pin | How m2b uses it |
|---|---|---|
| **C-M2b·1** (m2b-owned) | **SoD lane (C-M2·3 extension).** Create (`InviteGroupMemberAsync`) / cancel (`CancelGroupInvitationAsync`) an invitation ⇒ **owner ∪ GlobalAdmin only**. Web gate = U10's `TryResolveWriteSurface` (owner ∪ member reachability) **plus** the owner-or-GlobalAdmin standing check (a plain member reaches the surface — the U10/F7 add/remove pin — but its invite/cancel POSTs 404 at the controller's `TryResolveInviteSurface`). Audited `Via` is derived **exactly like add/remove**: `invitedBy / cancelledBy == Group.OwnerId ⇒ Owner`, else `Admin` (the seam's single SoD source — the Web layer carries no form-bound owner id). | The invite/cancel actions (F16/F18) are the first m2b write lanes that the owner∪GlobalAdmin gate constrains. `UserInfoServiceGroupInvitationsM2bTests.Invite_Via_*` pin the audit `Via` derivation; the Web gate is a controller concern (no Web seam test). |
| **C-M2b·2** (m2b-owned) | **Self-lane.** Accept (`AcceptGroupInvitationAsync`) / decline (`DeclineGroupInvitationAsync`) ⇒ the **invitee only** — the actor must equal the row's `UserId`, **verified in Core** (not only by the Web gate). Web gate = "in my pending list, else 404" (read lane #2). A foreign caller throws `InvalidOperationException`; the Web maps it to an error, never a 500. | The accept/decline actions (F17) are the first m2b write lanes with a *self*-SoD (not owner/SoD). `UserInfoServiceGroupInvitationsM2bTests.AcceptBy_A_Foreign_Account_Throws_CM2b_2` + `Accept_Unknown_Group_Or_Pair_Throws` pin the Core wall. |
| **C-M2b·3** (m2b-owned) | **State machine (C-M2·3 extension).** One row per (group, user) (the `UniqueIndex` business key); `Pending → {Accepted, Declined, Cancelled}`; `Pending → Cancelled`; **re-invite resets to `Pending`**; an invalid transition (double-resolution, or resolve-after-resolution) ⇒ `InvalidOperationException`. The two resolve stamps (`ResolvedAt`/`ResolvedBy`) clear on a reset. | The `Resolved_Row_Does_Not_Accept_Decline_Or_Cancel_Again_CM2b_3` + `ReInvite_After_Decline_Resets_To_Pending_CM2b_3` + `Inviting_The_Same_Pair_Twice_Keeps_One_Row_CM2b_3` tests pin each wall; the Web maps the `InvalidOperationException` to `TempData["error"]` (F17/F18's error rows). |
| **C3** (carried — ADR 0006 + M1) | Every m2b mutation writes its `AccessAudit` row in the **same transaction** (invariant C3): the `group.invite` / `group.invite.accept` / `group.invite.decline` / `group.invite.cancel` rows, `TargetKind "group"`, `Outcome Allow`. Allow-and-Deny are both recorded (Deny never occurs in m2b's writes by construction — the SoD gates are pre-write, not post-write). | `UserInfoServiceGroupInvitationsM2bTests.Every_M2b_Write_Appends_Its_AuditRow_C3` pins the four action ids on the `group` target-kind. |
| **C4** (carried — M1) | **Accept ⇒ live membership** on the very next `GetGroupIdsAsync` / `GetGroupsForUserAsync` call. An **invite** itself never touches membership (C4's lane is accept-only). | `Invite_Then_Accept_Membership_LiveOnNextCall_C4` pins both the negative (invite does not add membership) and the positive (accept does, live-on-next-call). |
| **C-M2·2** (carried — M2) | The **three new reads** (`GetGroupAsync`, `GetPendingInvitationsForUserAsync`, `GetPendingInvitationsForGroupAsync`) append **no** `AccessAudit` row — they are candidate projections, not access decisions (C-M2·2 + C1). | `The_Three_M2b_Read_Lanes_Append_No_AuditRow_CM2_2` pins the no-audit shape on all three lanes. |
| **ADR 0006-E** (carried — ADR 0006) | All seam additions are **new named methods** on `IUserInfoService` — no signature change to any pinned M1/M2 method. | The `IUserInfoService` "M2b additions" block is the single addition; no existing method is touched. |

**ADRs m2b must keep holding** (pinned in the invariant rows above, and not
to be re-derived by later units): ADR 0001-B (audience-grants stored
verbatim — not exercised in m2b), ADR 0006-A/B/C (the read/write/audit split
— exercised through the `IUserInfoService` lane only, never the
`IDocumentSession` overloads), ADR 0006-D (Web shapes HTTP, Core decides),
ADR 0003 (SoD default-OFF, the C-M2b·1/·2/·3 pins are its m2b face).

## FACES (m2b-added user-visible outcomes → invariant pins)

Continuing M2's FACES numbering (F1–F15 are unchanged, pinned by M2).
**FACES count: 5 new rows (F16–F20).** Each row is a resident-visible
outcome the m2b seam tests must cover; the pin in the right column is the
single authority.

| # | Outcome (what a resident sees / can do) | Pinned by |
|---|---|---|
| F16 | The group **owner** (or a GlobalAdmin) on `/groups/{id}` sees an **"Invite a resident"** form and a **"Pending invitations"** list (with cancel links); a **plain member** sees neither (the add-member form remains; the **remove** form is owner ∪ GlobalAdmin by C-M2·3 — the route's `TryResolveOwnerSurface` gate 404s a member's remove POST). | C-M2b·1 (SoD) + C-M2·3 (carried) |
| F17 | The invitee on `/groups` (Index) sees a **"Your invitations"** card (group name + inviter + accept / decline buttons); a **foreign resident** (not the invitee) cannot resolve another's invitation (404 / error). Accept makes the membership **live on the very next** `GetGroupIdsAsync` / `GetGroupsForUserAsync` call (C4) and — because the group now grants — the detail page is reachable. | C-M2b·2 (self-lane) + C4 + C-M2·2 (the card's reads never audit) |
| F18 | The owner/admin on detail can **cancel** a pending invitation (a resolved row cannot be re-cancelled — C-M2b·3's state machine — the Web maps it to `TempData["error"]`); the cancel row carries the C-M2b·1 `Via` derivation (owner ⇒ `Owner`, admin ⇒ `Admin`). | C-M2b·1 + C-M2b·3 (state machine) |
| F19 | A resident who **declines** never becomes a member (no `GroupMembership` row, no audit row beyond `group.invite.decline`); the owner may **re-invite** afterward (C-M2b·3's reset). | C-M2b·3 (re-invite) + C3 (audit) |
| F20 | Every m2b write (invite / accept / decline / cancel) commits an `AccessAudit` row in the **same transaction** as the domain write — Allow and Deny are both recorded (Deny is unreachable in m2b's writes by construction; the SoD gates are pre-write). | C3 (carried) |

**FACES count (m2b): 5.** This count (and the invariant-pin per row) is the
input the close (U-`close`) needs to name the seam-test list and the
acceptance gate without re-deriving them (the M2 §2.7 drift-guard).

## Seams & contracts (exact shapes)

> Every C# fragment below is **exact**: parameter lists, return types, and
> namespaces are the contract the tests anchor to. The `IUserInfoService`
> "M2b additions" block is the *only* change to the frozen M1/M2 lane
> (ADR 0006-E). No existing method's signature is touched.

### New Core types (`Kumunita.Core.UserInfo`)

```csharp
// src/Kumunita.Core/UserInfo/GroupInvitation.cs
enum InvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Cancelled
}

sealed class GroupInvitation
{
    public string Id { get; set; }                     // surrogate (Guid-N)
    public string GroupId { get; set; }                // business key (1/2)
    public string UserId { get; set; }                 // invitee; business key (2/2)
    public string InvitedBy { get; set; }              // owner / admin actor
    public InvitationStatus Status { get; set; }
    public DateTimeOffset InvitedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }    // null while Pending
    public string? ResolvedBy { get; set; }            // null while Pending
}
```

`M1DocTypes.Configure` registers the row:

```csharp
opts.Schema.For<GroupInvitation>()
    .UniqueIndex(i => i.GroupId, i => i.UserId);
```

The `UniqueIndex` is the **C-M2b·3 business-key pin** (one row per
(group, user); re-invite *resets* the existing row, it does not insert a
second one — the index is the enforcement wall).

### The seven new `IUserInfoService` seams (exact)

```csharp
Task<Group?> GetGroupAsync(string groupId);

Task<GroupInvitation> InviteGroupMemberAsync(string groupId, string userId, string invitedBy);
Task AcceptGroupInvitationAsync(string groupId, string actorId);
Task DeclineGroupInvitationAsync(string groupId, string actorId);
Task CancelGroupInvitationAsync(string groupId, string userId, string cancelledBy);

Task<IReadOnlyList<GroupInvitation>> GetPendingInvitationsForUserAsync(string userId);
Task<IReadOnlyList<GroupInvitation>> GetPendingInvitationsForGroupAsync(string groupId);
```

**Write semantics (exact, per C-M2b·1..3 + C3 + C4 carried):**

| Method | Precondition (else `InvalidOperationException`) | Post-state (same session) | `AccessAudit` row (same tx) |
|---|---|---|---|
| `InviteGroupMemberAsync` | Group exists; (re-invite if row exists in any status — C-M2b·3) | `GroupInvitation` row = `Pending`, `InvitedBy=invitedBy`, `InvitedAt=now`; **no** `GroupMembership` lane | `group.invite`, `TargetId=groupId`, `Via=Owner` iff `invitedBy==Group.OwnerId` else `Admin` |
| `AcceptGroupInvitationAsync` | Group exists; row exists for `(groupId, actorId)`; row is `Pending` (C-M2b·2 self-lane + C-M2b·3) | Row = `Accepted` (stamps set); `GroupMembership` upserted with `AddedBy=actorId` (C4 live-on-next-call) | `group.invite.accept`, `Via=Owner` (the invitee's own standing) |
| `DeclineGroupInvitationAsync` | Group exists; row exists for `(groupId, actorId)`; row is `Pending` (C-M2b·2 self-lane + C-M2b·3) | Row = `Declined` (stamps set); **no** `GroupMembership` lane | `group.invite.decline`, `Via=Owner` (the invitee's own standing) |
| `CancelGroupInvitationAsync` | Group exists; row exists for `(groupId, userId)`; row is `Pending` (C-M2b·3) | Row = `Cancelled` (stamps set); **no** `GroupMembership` lane | `group.invite.cancel`, `TargetId=groupId`, `Via=Owner` iff `cancelledBy==Group.OwnerId` else `Admin` |

**Read semantics (exact, per C-M2·2 carried):** all three return live rows
(no projection, no cache — C4), filter `Status == Pending`, and append **no**
`AccessAudit` row. `GetPendingInvitationsForUserAsync` sorts `InvitedAt`
desc; `GetPendingInvitationsForGroupAsync` sorts `InvitedAt` asc. Both
return `Array.Empty<GroupInvitation>()` for an empty / null id (fail-safe,
no throw). `GetGroupAsync` returns `null` for an unknown id.

### Web surface (exact, per F16–F20)

`src/Kumunita.Web/Models/GroupViewModel.cs` — the two projection records
(the strict shape pins the drift-guard tests anchor to; see § Handoff):

```csharp
record InvitationViewModel(string GroupId, string GroupName, string InvitedByDisplayName);
record PendingInvitationViewModel(string SubjectId, string DisplayName);
```

- `GroupListViewModel` gains `IReadOnlyList<InvitationViewModel>
  Invitations` (default `Array.Empty<InvitationViewModel>()`) — F17's card.
- `GroupDetailViewModel` gains a 7th field,
  `IReadOnlyList<PendingInvitationViewModel> PendingInvitations` (default
  `Array.Empty<PendingInvitationViewModel>()`) — F16's pending list. **This
  is the drift-guard pin update** (recorded in § Handoff).

`GroupsController` — four new routes (all `[ValidateAntiForgeryToken]`):

| Route | Action | Gate (404 on fail) |
|---|---|---|
| `POST /groups/{id}/invite` | `InviteMember(id, [FromForm] subjectId)` → `InviteGroupMemberAsync(id, subjectId, invitedBy: actor)` | `TryResolveInviteSurface` (owner ∪ GlobalAdmin) + `subjectId` non-whitespace |
| `POST /groups/{id}/invitations/accept` | `AcceptInvitation(id)` → `AcceptGroupInvitationAsync(id, actor)` | actor is in **their own** `GetPendingInvitationsForUserAsync(actor)` for `id` (C-M2b·2 Web gate) |
| `POST /groups/{id}/invitations/decline` | `DeclineInvitation(id)` → `DeclineGroupInvitationAsync(id, actor)` | as accept |
| `POST /groups/{id}/invitations/{subjectId}/cancel` | `CancelInvitation(id, subjectId)` → `CancelGroupInvitationAsync(id, subjectId, cancelledBy: actor)` | `TryResolveInviteSurface` + `subjectId` non-whitespace |

The `InvalidOperationException` from the Core state machine (C-M2b·3) or
self-lane (C-M2b·2) is caught **per-action** (not swallowed by a global
500) and mapped to `TempData["error"] = "That invitation is no longer
pending."` + redirect — F17/F18's error rows. The audit `Via` derivation is
the seam's single SoD source (C-M2b·1); the Web layer never re-derives it.

`GroupsController.Index` loads the actor's own pending invitations via
`GetPendingInvitationsForUserAsync(subject)` and projects them to
`InvitationViewModel` (the group name via `GetGroupAsync`, the inviter's
display name via `GetProfileAsync`) — the F17 card. `GroupsController.Detail`
loads the group's pending invitations via
`GetPendingInvitationsForGroupAsync(group.Id)` and projects them to
`PendingInvitationViewModel` (the invitee's display name via
`GetProfileAsync`) — the F16 pending list. Neither the controller nor the
view has a channel to an owner-id form-bound field (ADR 0003 SoD by
structural identity, carried from U10/F7).

## Seam-test names (landed, `Kumunita.Core.Tests`)

The m2b lane's seam tests, in `UserInfoServiceGroupInvitationsM2bTests`
(fresh scratch Postgres per test, the U9/U5/U6 boot pattern in this
assembly):

| Test name | Pin |
|---|---|
| `Invite_Then_Accept_Membership_LiveOnNextCall_C4` | C4 carried + F17's "accept is live" row |
| `Invite_Then_Decline_NoMembership_RowResolved` | C-M2b·3 (state) + C3 (audit) + F19's "no membership" |
| `Invite_Then_Cancel_NoMembership_RowResolved` | C-M2b·1 (owner-cancel) + C3 + F18's "cancel" |
| `Invite_Via_Owner_When_Inviter_Is_GroupOwner` | C-M2b·1 (owner⇒Owner audit) |
| `Invite_Via_Admin_When_Inviter_Is_Not_The_Owner` | C-M2b·1 (else⇒Admin audit) |
| `AcceptAndDecline_AuditRows_Use_The_Invitees_Own_Standing` | C-M2b·2 (self-lane audit shape, `Via=Owner`) |
| `AcceptBy_A_Foreign_Account_Throws_CM2b_2` | C-M2b·2 (self-lane wall) + F17's "foreign 404" |
| `Accept_Unknown_Group_Or_Pair_Throws` | C-M2b·2 (self-lane, unknown-pair lane) |
| `ReInvite_After_Decline_Resets_To_Pending_CM2b_3` | C-M2b·3 (re-invite reset) + F19's "re-invite" |
| `Inviting_The_Same_Pair_Twice_Keeps_One_Row_CM2b_3` | C-M2b·3 (business-key, one row per pair) |
| `Resolved_Row_Does_Not_Accept_Decline_Or_Cancel_Again_CM2b_3` | C-M2b·3 (state machine, all five invalid walls) |
| `Every_M2b_Write_Appends_Its_AuditRow_C3` | C3 (same-tx audit on all four writes) + F20 |
| `The_Three_M2b_Read_Lanes_Append_No_AuditRow_CM2_2` | C-M2·2 (read lanes never audit) + F17's "card reads never audit" |
| `The_Pending_Read_Lanes_Filter_And_Order_LiveRows_C4` | C-M2·2 (filter) + C4 (live-on-next-call on the read lanes) |

**Seam-test count (m2b): 14.** This count (and the pin per test) is the
input the acceptance gate needs (the M2 §2.7 drift-guard).

## Handoff / drift notes (the two M2 U10/U9 pin updates)

Per the M2 §2.7 drift-guard ("a shape that a later lane legitimately
extends is updated in the **same commit** with a drift note"), the two M2
drift-guard files are updated by m2b in the commit that lands
`GroupDetailViewModel`'s 7th field:

- **`GroupsDetailViewModelTests.GroupDetailViewModel_Has_Exactly_6_Projected_Fields`**
  is renamed to `..._Exactly_7_Projected_Fields` and the expected field set
  is extended from the M2 U10 six to the m2b seven (adding
  `PendingInvitations`). The drift note: the 7th field is the m2b invite
  lane's projection (F16's pending list) — not a source-field addition
  (the source `GroupInvitation` row's `Status`/`InvitedBy`/`InvitedAt`
  still never reach the UI — the "who invited, when" fact is on the
  `AccessAudit` lane, not the member-list-shaped UI). The M2 U10 six-field
  pin **stays authoritative** for the M2 surface; the 7th is m2b's
  additive pin.
- **`GroupsViewModelTests`** gains the two new m2b shape-pin tests
  (`InvitationViewModel_Has_Exactly_Three_Projected_Fields`,
  `PendingInvitationViewModel_Has_Exactly_Two_Projected_Fields`) with the
  source-field exclusion guards (the `GroupInvitation` row's
  `Id`/`UserId`/`InvitedBy`/`Status`/`InvitedAt`/`Resolved*` never appear
  on the projection) — the same "a projection is a small named set, not a
  document dump" pin the M2 U9/U10 files carried. The existing M2 pins
  (`GroupViewModel_Has_Exactly_Three_Projected_Fields`,
  `GroupCreateModel_Exposes_OnlyNameAndDescription`) are unchanged.

No **other** M2 / M3 pin is touched by m2b. If a later unit (post-m2b)
needs to extend these lanes further, that unit re-opens the drift-guard in
the same M2 §2.7 shape (same commit, drift note, this file's FACES count →
F21+).

## Acceptance Gate (recorded)

The m2b close (recorded in `## M2b — Closed (recorded)`) is the plan's
checklist's "Full build + both suites green" + "Commit":

- `dotnet build Kumunita.slnx -c Debug` → **0 errors.**
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
  → all pass (includes the updated `GroupsDetailViewModelTests` +
  `GroupsViewModelTests`).
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  → all pass (includes the 14 new `UserInfoServiceGroupInvitationsM2bTests`
  lane + the unchanged M1/M2/M3 lanes).

## M2b — Closed (recorded)

Recorded at the commit that flips the plan's checklist:
the in-scope items 1–5 (scope section above) are landed, the
**in-scope / out-of-scope split** is honored (nothing beyond the seven
seams + two projection records + four routes + two view lanes is touched on
the M2 groups surface), the **14 seam tests** are green, the
**2 drift-guard pin updates** (the Handoff section above) are landed in
the same commit, the **5 FACES rows** (F16–F20) are pinned to their
invariants, and the **M2 F1–F15 + C-M2·1..3 pins are unchanged** (the
immediate add/remove surface — U10/F7 — is alive and side-by-side).
m2b's close is this section; m2b does not re-open the M2/M3 surface, and
the next unit (post-m2b) re-opens the M2 §2.7 drift-guard only through the
M2 §2.7 shape (same commit, drift note, this doc's FACES count → F21+).
