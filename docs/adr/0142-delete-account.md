---
id: adr-0142
title: "Delete account: a resident-leave / admin-removal lane that pseudonymizes the audit trail, removes memberships and the profile, and deletes the Identity account — with the last-GlobalAdmin lockout pin and the self-serve GlobalAdmin gate"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/archive/lanes/prod-hardening/hardening-handoff-notes.md]
supersedes: [adr-0006]
superseded_by: []
updated: 2026-11-02
---

# ADR 0142 — Delete account: a resident-leave / admin-removal lane that pseudonymizes the audit trail, removes memberships and the profile, and deletes the Identity account — with the last-GlobalAdmin lockout pin and the self-serve GlobalAdmin gate

Status: Accepted
Date: 2026-10-04
Builds on: 0004 (the two-stores persistence model), 0006 (the §A frozen
`IIdentityService` surface gains one *additive* method), 0003 (the
audit-by-default rule), 0028 (the `GuardianLink` dissolution precedent),
0138 (the self-serve change-password surface — the *exact template* this
lane mirrors), 0001 (the "thin token, fat authorization" rule).
Amends: 0006 (its §A frozen `IIdentityService` surface gains one
*additive* method — `DeleteAccountAsync(targetSubjectId, adminSubjectId)`
— so no existing caller's contract changes); 0138 (its two-branch shape is
*extended* with a parallel "resident self-serve delete-account surface +
the GlobalAdmin removal lane" pair).

> **Abstract:** a resident who wants to leave the platform, or a
> GlobalAdmin who must remove a resident, has no in-app surface today
> (the only paths are *Block* — reversible, preserves PII — and
> operator-level DB surgery). This ADR closes that seam with a single
> in-app lane — `IIdentityService.DeleteAccountAsync` — that both the
> resident self-serve surface (`POST /account/delete`) and the
> GlobalAdmin removal surface (`POST /admin/delete`) reach. The audit
> trail is **pseudonymized, not deleted** (the rows remain; the target's
> `ActorId` / `EffectivePrincipalId` are rewritten to a deterministic
> `deleted:{subjectId}` tombstone — the identity is gone, the fact
> remains). The **last-GlobalAdmin guard** is the lockout pin (a lone
> GlobalAdmin cannot self-delete; no admin may delete the last
> GlobalAdmin; the guard fires *before any write*). The **self-serve lane
> is a GlobalAdmin lane** (a non-GlobalAdmin resident cannot self-delete
> through `POST /account/delete` — a *deliberate* restriction, the
> `OPS.md` §9 operator procedure is not a resident self-serve one).
> **For** a developer touching `IIdentityService`, the
> `DeleteAccountAsync` seam, the `POST /account/delete` /
> `POST /admin/delete` routes, or the `tests/*` delete-account family. The
> seam detail (the two branches + both-branch invariants), the
> cross-store ordering, the Web surfaces, and the translation keys are in
> the [handoff
> note](../design/adr-handoffs.md#adr-0142--delete-account-moved-detail).

## Context

A resident who wants to leave the platform, or a GlobalAdmin who must
remove a resident (abuse, departure, a legal request), has no in-app
surface today. The only paths are: *Block* (the ADR 0006 admin suspension
lane — reversible, preserves the account and its PII; **not** a deletion)
and *operator-level DB surgery* (the `OPS.md` §9 "Resident requests
account deletion" procedure: disable in-app, then the operator hard-deletes
the personal data at the DB level — a procedure that leaves the platform's
own code as the wrong place to perform the deletion, and the
`AccessAudit` trail either deleted or left dangling). This ADR closes that
seam with a single in-app lane that both the resident self-serve surface
and the GlobalAdmin removal surface reach.

Three facts about the current surface shape the decision: **there was no
delete-account surface at all** (the `IIdentityService` seam had
`BlockAsync` / `UnblockAsync`, `ChangePasswordAsync`, and `SetRoleAsync`,
but no deletion); **the pseudonymization policy is already designed**
(`OPS.md` §9 and the data model both describe the "Deletion-of-account
interaction" — the policy is documented, the mechanism is not); **the
self-serve/admin two-branch shape already exists** (ADR 0138's
`ChangePasswordAsync(subjectId, newPassword, byAdmin)` is the exact shape).

## Decision

- **D1 — The seam: one write lane, two branches (the ADR 0138
  self-serve/admin distinction).**
  `IIdentityService.DeleteAccountAsync(string targetSubjectId, string
  adminSubjectId)`. Branch 1 — **Self-deletion**
  (`adminSubjectId == targetSubjectId`): the resident deletes their own
  account; **ADR 0142 D5 gate:** only a `GlobalAdmin` may self-delete (a
  non-GlobalAdmin's invocation throws `UnauthorizedAccessException`); the
  audit row is `Via: Owner`. Branch 2 — **Admin-initiated**
  (`adminSubjectId != targetSubjectId`): a `GlobalAdmin` removes another
  resident; the `RequireGlobalAdminAsync` gate applies; the audit row is
  `Via: Admin`. **Both branches** share the invariants below. See the
  handoff note for the full seam detail.
- **D2 — The audit trail is pseudonymized, not deleted** (the `OPS.md` §9
  / data-model policy, now implemented in-app): the rows the target wrote
  remain in the log (the community is still promised "prove what happened"),
  but the target's `ActorId` / `EffectivePrincipalId` are rewritten to a
  deterministic `deleted:{subjectId}` tombstone — the identity is gone, the
  fact remains. The tombstone is *deterministic* (derived from the subject
  id) so a re-deletion re-attaches to the same tombstone.
- **D3 — The last-GlobalAdmin guard is the lockout pin.** A lone
  GlobalAdmin cannot delete themselves, and no admin may delete the last
  GlobalAdmin on the instance. The guard fires *before any write* (the
  fail-closed pin). The recovery path is unchanged: promote a second
  GlobalAdmin first.
- **D4 — Membership removal + Profile deletion + Identity account
  deletion** (the strong-consistency C4 invariant): the target's
  `GroupMembership`, `ComponentMembership`, and `ModeratorAssignment` rows
  are deleted; the target's `Profile` row is removed (their PII is gone);
  the target's ASP.NET Identity account is deleted. The
  `PrivilegedStampMiddleware`'s existing "user was deleted while signed in"
  branch is the *consequence* path — this lane is the *cause* path.
- **D5 — Exactly one audit row + fail-closed / idempotency.** The lane
  writes a single `AccessAudit` row (action `"account.delete"`,
  `TargetKind` "account", `Via: Owner` or `Via: Admin` per the branch). A
  missing target throws `InvalidOperationException` before any write; a
  second call for the same subject throws the same (the Identity row is
  gone) — the lane is not a silent no-op.
- **D6 — The cross-store ordering.** The lane commits the Marten writes
  **first**, then deletes the Identity account (EF Core). The ordering
  inverts the register/verify lanes' ordering so that a failure *between*
  the two leaves the account still present (a recoverable state) rather
  than the account gone with the trail still carrying the identity (an
  unrecoverable privacy violation). See the handoff note for the full
  ordering rationale.
- **D7 — The Web surfaces.** `GET /account/delete` + `POST /account/delete`
  (the resident self-serve surface, on the `AccountController`; the form
  asks for the current password + an explicit acknowledgment checkbox; when
  the resident is not a `GlobalAdmin`, the view renders the
  `SelfDeletionRefused` notice). `POST /admin/delete` (the GlobalAdmin
  removal surface, on the `AdminController`; a `GlobalAdmin` deleting their
  own account through this surface is refused up-front). See the handoff
  note for the full Web surface detail.
- **D8 — One closed `account.delete.*` + `admin.delete_account.*` +
  `nav.delete_account` `kw-l` set × en/de/fr/da** (the ADR 0015
  closed-registry shape, the ADR 0138 `account.change_password.*`
  precedent). See the handoff note.

## Consequences

- A resident who is a `GlobalAdmin` can leave the platform in-app; a
  resident who is not a `GlobalAdmin` cannot self-delete (the ADR 0142 D5
  gate) — they ask a `GlobalAdmin`, or the operator performs the `OPS.md`
  §9 operator-level procedure. This is a *deliberate* restriction, not a
  bug.
- The `AccessAudit` trail is now pseudonymized-in-app for every deletion
  (the `OPS.md` §9 operator-level pseudonymization procedure is now a
  fallback, not the primary path).
- A lone `GlobalAdmin` cannot self-delete (the lockout pin) — the `OPS.md`
  §9 "Two `GlobalAdmin`s should be the standing state" procedure is now
  enforced in the seam, not just documented.
- The `PrivilegedStampMiddleware`'s "account-removed" branch (the
  consequence path) is now reachable from an in-app cause (this lane), not
  just from an operator-level DB delete.
