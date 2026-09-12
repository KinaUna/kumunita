# Plan — M2b: owner-invited group membership (invite → accept/decline)

## Context
- Groups surface already shipped in M2 (closed): list/create/detail/immediate add-remove (`GroupsController`, `IUserInfoService`, `Groups/{Index,Detail,Create}.cshtml`). It was only unreachable — the navbar had no link; **nav link added** (✅ _AccountNav.cshtml, built OK).
- New scope (user-approved): owner invites a resident → the invitee accepts or declines. Immediate add/remove is **kept** side-by-side (U10/F7 pin untouched).
- Lane: a new design unit **m2b** (precedent: m3b moderation), not an M2 re-open — M2 pins (F-rows, C-M2·1..3) stay authoritative; m2b pins reference them.

## Decisions (confirmed with user)
- Direction: owner-initiated invite, invitee resolves (no join-request flow; keeps the owner∪member privacy model).
- Both add paths coexist: immediate `AddMember` (existing) + invitation (new).
- No TTL/expiration; pending invites are cancellable by the owner/admin, re-invitable after resolution.
- Invitee sees their invitations on `/groups` (Index) — they *cannot* reach the group detail (owner∪member gate) until accepted.

## Invariants (pinned by m2b doc)
- **C-M2b·1 SoD lane**: create/cancel invitation ⇒ owner ∪ GlobalAdmin only (C-M2·3 extension; Web gate = U10 `TryResolveWriteSurface`; audited `Via` derived exactly like add/remove).
- **C-M2b·2 self-lane**: accept/decline ⇒ the invitee *only* (actor == row.UserId, verified in Core; Web gate = "in my pending list", else 404).
- **C-M2b·3 state machine**: one row per (group, user) (unique index); Pending → {Accepted, Declined}; Pending → Cancelled; re-invite resets to Pending; invalid transition ⇒ `InvalidOperationException` (Web maps to an error, never 500).
- **C3 (carried)**: every mutation writes its `AccessAudit` row in the same transaction (`group.invite`, `group.invite.accept`, `group.invite.decline`, `group.invite.cancel`; TargetKind "group").
- **C4 (carried)**: accept = live membership on the very next `GetGroupIdsAsync` / `GetGroupsForUserAsync` call.
- **C-M2·2 (carried)**: the three new reads append no audit row.
- **ADR 0006-E**: all seam additions are new named methods on `IUserInfoService` — no signature change to any pinned M1/M2 method.

## Files
Core:
- `src/Kumunita.Core/UserInfo/GroupInvitation.cs` — new document + `InvitationStatus`.
- `src/Kumunita.Core/M1DocTypes.cs` — register, UniqueIndex(GroupId, UserId).
- `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — "M2b additions" seam block.
- `src/Kumunita.Core/UserInfo/UserInfoService.cs` — implementation (mirror `AddGroupMemberAsync` session/audit shape).

Web:
- `src/Kumunita.Web/Models/GroupViewModel.cs` — `InvitationViewModel`, `PendingInvitationViewModel`; `GroupListViewModel.Invitations`; `GroupDetailViewModel.PendingInvitations` (drift-guard updates to the two shape-pin tests in the same commit, per M2 §2.7).
- `src/Kumunita.Web/Controllers/GroupsController.cs` — Index loads own pending invites; Detail loads group pending invites; 4 new routes (invite / accept / decline / cancel).
- `src/Kumunita.Web/Views/Groups/Index.cshtml` — "Your invitations" card (accept/decline).
- `src/Kumunita.Web/Views/Groups/Detail.cshtml` — pending list (cancel) + invite form.

Docs:
- `docs/design/m2b-group-invitations.md` — new unit doc (scope, invariants, FACES, handoff/drift notes for the two U10/U9 pin updates).

Tests:
- `tests/Kumunita.Core.Tests/UserInfoServiceGroupInvitationsM2bTests.cs` — full lifecycle + SoD + C3/C4/C-M2·2 pins.
- `tests/Kumunita.Web.Tests/GroupsDetailViewModelTests.cs` + `GroupsViewModelTests.cs` — pin updates (drift lane).

## Verification
`dotnet build Kumunita.slnx -c Debug` → `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` → `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` (xunit.v3 runner, per AGENTS.md — not `dotnet test`).

## Checklist
- [x] Nav link (_AccountNav) + build
- [x] m2b design doc (`docs/design/m2b-group-invitations.md`)
- [x] Core document + schema (`GroupInvitation` + `M1DocTypes` UniqueIndex)
- [x] Core seam + implementation (7 seams on `IUserInfoService`)
- [x] Web view models + controller + views (2 records, 4 routes, Index/Detail lanes)
- [x] Tests (14 new Core seam tests + same-commit pin updates)
- [x] Full build + both suites green (Web 57/57, Core 189/189)
- [x] Commit
