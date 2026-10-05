# ADR 0142 — Delete account: a resident-leave / admin-removal lane that pseudonymizes the audit trail, removes memberships and the profile, and deletes the Identity account — with the last-GlobalAdmin lockout pin and the self-serve GlobalAdmin gate

Status: Accepted
Date: 2026-10-04
Builds on: 0004 (the two-stores persistence model — the Identity account in
the `identity` schema, the Profile / memberships / audit in the `mt` schema;
the cross-store window the class doc already acknowledges for the
register/verify lanes is the same one this lane's "Marten commit first, EF
delete second" ordering lives in); 0006 (the §A frozen `IIdentityService`
surface gains one *additive* method, `DeleteAccountAsync`); 0003 (the
audit-by-default rule — the pseudonymization is what keeps the audit log an
accountability mechanism even after the account it records is gone, per
`SECURITY.md` §3.1); 0028 (the `GuardianLink` dissolution precedent — the
"the service is the resolver" rule: this lane dissolves the target's
relationships by deleting their membership rows, the same strong-consistency
C4 shape the guardian-suspend / membership-removal lanes follow); 0138 (the
self-serve change-password surface — the *exact template* this lane mirrors:
a resident-facing form + a server-side `CheckPasswordAsync` guard + the
"surface-replaced-by-notice" shape for a resident who cannot use the
surface; the ADR 0138 `ChangePasswordAsync` self-serve/admin branch
distinction is the *exact shape* this lane's self-deletion/admin-initiated
branch distinction mirrors); 0001 (the "thin token, fat authorization" rule
— the lane does not encode "is this a deletion" into a claim; the effect is
the absence of the account, read from `mt` + `identity` on every request,
exactly as `Profile.Blocked` is read from the seam)
Amends: 0006 (its §A frozen `IIdentityService` surface gains one *additive*
method — `DeleteAccountAsync(targetSubjectId, adminSubjectId)` — so no
existing caller's contract changes); 0138 (its "the resident self-serve
change-password surface + the GlobalAdmin reset lane" two-branch shape is
*extended* with a parallel "the resident self-serve delete-account surface +
the GlobalAdmin removal lane" pair, on the same `AccountController` /
`AdminController` surfaces — no change to the ADR 0138 lane itself)

A resident who wants to leave the platform, or a GlobalAdmin who must remove
a resident (abuse, departure, a legal request), has no in-app surface today.
The only paths are: *Block* (the ADR 0006 admin suspension lane — reversible,
preserves the account and its PII; **not** a deletion) and *operator-level
DB surgery* (the `OPS.md` §9 "Resident requests account deletion" procedure:
disable in-app, then the operator hard-deletes the personal data at the DB
level — a procedure that leaves the platform's own code as the wrong place to
perform the deletion, and the `AccessAudit` trail either deleted (losing the
accountability the audit log exists to provide) or left dangling (retaining
the resident's identity past their departure). This ADR closes that seam with
a single in-app lane — `IIdentityService.DeleteAccountAsync` — that both the
resident self-serve surface (`POST /account/delete`) and the GlobalAdmin
removal surface (`POST /admin/delete`) reach.

The one thing the whole lane respects:

- **The audit trail is pseudonymized, not deleted** (the `OPS.md` §9 /
  `ARCHITECTURE.md` §5 "Deletion-of-account interaction" policy, now
  implemented in-app): the rows the target wrote remain in the log (the
  community is still promised "prove what happened"), but the target's
  `ActorId` / `EffectivePrincipalId` are rewritten to a deterministic
  `deleted:{subjectId}` tombstone — the identity is gone, the fact remains.
  This is the *exact* "keep the row, replace the identity" shape the
  `OPS.md` §9 procedure describes for the operator-level path; this ADR
  moves it into the app so it is audited, gated, and repeatable, not a
  manual DB procedure.
- **The last-GlobalAdmin guard is the lockout pin** (the `OPS.md` §9
  "Hand over admin" precedent, now a hard error in the seam): a lone
  GlobalAdmin cannot delete themselves, and no admin may delete the last
  GlobalAdmin on the instance. The guard fires *before any write* (the
  fail-closed pin, the `BlockAsync` / `UnblockAsync` precedent). The
  recovery path is unchanged: promote a second GlobalAdmin first.
- **The self-serve lane is a GlobalAdmin lane** (the ADR 0142 D5 gate, the
  ADR 0138 "locked" notice shape generalized): a non-GlobalAdmin resident
  cannot self-delete through `POST /account/delete` (a crafted POST is
  refused server-side, the `UnauthorizedAccessException` the `BlockAsync` /
  `UnblockAsync` seams throw). The Web surface renders the
  `ChangePasswordLockedViewModel`-shape notice (the `SelfDeletionRefused`
  flag) so the resident sees the gate before submitting. This is a
  *deliberate* restriction, not an oversight: the platform's
  `OPS.md` §9 "Resident requests account deletion" procedure is an
  *operator* procedure (the operator hard-deletes at the DB level), not a
  resident self-serve one — the in-app self-serve lane this ADR adds is the
  *GlobalAdmin* surface the `OPS.md` §9 "Suspend a resident" procedure
  already assumes exists, extended to the deletion the §9 "Resident
  requests account deletion" procedure otherwise leaves to the operator.

## Context

Three facts about the current surface shape the decision:

- **There was no delete-account surface at all** — the
  `IIdentityService` seam had `BlockAsync` / `UnblockAsync` (the reversible
  suspension lane, ADR 0006), `ChangePasswordAsync` (the ADR 0138
  self-serve + admin reset pair), and `SetRoleAsync` (the role
  promote/demote lane), but no deletion. The `OPS.md` §9 "Resident
  requests account deletion" procedure is the *only* documented path, and
  it is an *operator-level* DB procedure (disable in-app, then hard-delete
  the personal data at the DB level, pseudonymize the audit rows manually)
  — a procedure that leaves the platform's own code out of the loop, and
  the `AccessAudit` trail either deleted (losing the accountability) or
  left dangling (retaining the identity). The `PrivilegedStampMiddleware`
  already handles the *consequence* of a user being deleted while signed
  in (the "account-removed" login error code, the `SignOutAsync` + redirect
  to `/Account/Login?error=account-removed`) — the app knows how to react
  to a deletion, but nothing in-app *performs* one.
- **The pseudonymization policy is already designed** — `OPS.md` §9 and
  `ARCHITECTURE.md` §5 both describe the "Deletion-of-account
  interaction": the departing resident's `AccessAudit` / `Report` rows are
  **pseudonymized** (the actor id replaced by a tombstone), *not* deleted.
  The policy is documented; the mechanism is not. This ADR is the
  mechanism.
- **The self-serve/admin two-branch shape already exists** — ADR 0138's
  `ChangePasswordAsync(subjectId, newPassword, byAdmin)` is the exact
  shape: a resident-facing lane (the self-serve form, the
  `CheckPasswordAsync` guard) + a GlobalAdmin-facing lane (the admin reset,
  the `RequireGlobalAdminAsync` gate). The self-serve lane's
  `ChangePasswordLockedViewModel` "surface-replaced-by-notice" shape (a
  resident who cannot use the lane sees a static notice, not a form) is
  the *exact shape* this ADR's `SelfDeletionRefused` notice mirrors for a
  non-GlobalAdmin resident.

## Decision

### The seam (the single place the rule lives)

`IIdentityService.DeleteAccountAsync(string targetSubjectId, string
adminSubjectId)` is the single write lane. Two branches, one seam (the
ADR 0138 self-serve/admin distinction):

1. **Self-deletion** (`adminSubjectId == targetSubjectId`) — the resident
   deletes their own account (the Web `POST /account/delete` invokes this
   shape, after verifying their current password). **ADR 0142 D5 gate:**
   only a `GlobalAdmin` may self-delete (a non-GlobalAdmin's invocation
   throws `UnauthorizedAccessException` — the fail-closed pin). The audit
   row is `Via: Owner`.
2. **Admin-initiated** (`adminSubjectId != targetSubjectId`) — a
   `GlobalAdmin` removes another resident (the Web `POST /admin/delete`
   invokes this shape). The `RequireGlobalAdminAsync` gate applies (a
   non-GlobalAdmin's invocation throws `UnauthorizedAccessException`). The
   audit row is `Via: Admin`.

**Both branches:**

- **Last-GlobalAdmin guard** (the lockout pin): if the target holds the
  `GlobalAdmin` role and they are the *only* `GlobalAdmin` on the
  instance, the lane throws `InvalidOperationException` **before any
  write** (the `OPS.md` §9 "Hand over admin" precedent). A lone
  `GlobalAdmin` cannot self-delete; no admin may delete the last
  `GlobalAdmin`. The recovery path is to promote a second `GlobalAdmin`
  first. *Design consequence:* on the admin-initiated branch, the actor
  must be a `GlobalAdmin` to pass the admin gate, so when the target is
  also a `GlobalAdmin` there are at least two `GlobalAdmin`s (actor +
  target) and the guard never fires — the guard is only reachable on the
  self branch (a lone `GlobalAdmin` deleting themselves). The Core test
  `DeleteAccountAsync_AdminInitiated_TargetIsGlobalAdmin_GuardPasses_BecauseActorIsAlsoGA`
  pins that the admin branch *can* delete a `GlobalAdmin` target (a
  "second admin removes a departing `GlobalAdmin`" scenario), and
  `DeleteAccountAsync_SelfDeletion_LastGlobalAdmin_ThrowsAndDoesNotWrite`
  pins the guard on the self branch.
- **Pseudonymization** (the `OPS.md` §9 / `ARCHITECTURE.md` §5 policy):
  the target's `AccessAudit` rows *remain* (the accountability the
  community is promised), but their `ActorId` and `EffectivePrincipalId`
  (when equal to the target) are rewritten to the deterministic
  `deleted:{targetSubjectId}` tombstone. The tombstone is *deterministic*
  (derived from the subject id, not a fresh GUID) so a re-deletion of the
  same subject (a re-seeded account) re-attaches to the same tombstone —
  the audit trail is a single, stable, pseudonymized history, not a pile
  of disconnected tombstones.
- **Membership removal** (the strong-consistency C4 invariant): the
  target's `GroupMembership`, `ComponentMembership`, and
  `ModeratorAssignment` rows are deleted (they leave every group and
  community, lose their posting rights and their moderation scope). The
  next `GetGroupIdsAsync` / `GetCommunityIdsAsync` read misses them — the
  same "the loss of access is live on the next request" shape the
  `RemoveGroupMemberAsync` lane promises.
- **Profile deletion**: the target's `Profile` row is removed (their PII —
  name, email, phone, address, bio, preferences — is gone). The directory
  and every author-name resolution already falls back to the raw subject
  id when the profile is absent (the existing null-safe
  `profile?.DisplayName ?? authorId` idiom) — a deleted resident's posts
  still render, attributed to the subject id (the tombstone), not to a
  dangling "(unknown)" stub.
- **Identity account deletion**: the target's ASP.NET Identity account is
  deleted (their password hash, roles, and the `User` row are gone). The
  `PrivilegedStampMiddleware`'s existing "user was deleted while signed
  in" branch (the `SignOutAsync` + `/Account/Login?error=account-removed`
  redirect) is the *consequence* path — this lane is the *cause* path. A
  resident who just deleted their own account is signed out (their
  credential is gone) and lands on the login page with the "your account
  was deleted" confirmation.
- **Exactly one audit row** (the `AuditPurgeSummary` "one summary, many
  rows" shape): the lane writes a single `AccessAudit` row (action
  `"account.delete"`, `TargetKind` "account", `TargetId` the subject,
  `Via: Owner` or `Via: Admin` per the branch, `Outcome: Allow`) — the
  pseudonymization and the membership removal are the *detail* rows; this
  is the *summary* row. The audit log answers "who deleted whom, by what
  right" from this one row; the per-row pseudonymization is the
  "what happened to the trail" detail.
- **Fail-closed / idempotency**: a missing target account throws
  `InvalidOperationException` before any write (the lane is not a silent
  no-op). A second call for the same subject (after a successful first
  call) throws the same `InvalidOperationException` (the Identity row is
  gone) — the lane is not a silent no-op on re-invocation.

### The cross-store ordering

The lane commits the Marten writes (the pseudonymization + membership
removal + profile deletion + the summary audit row) **first**, then deletes
the Identity account (EF Core, the `identity` schema). The class doc's
"two stores, one Postgres" paragraph already acknowledges a "rare failure
between the two is the accepted cross-store window" for the register/verify
lanes; this lane inverts the ordering (the `mt` cleanup commits first, the
identity row is removed second) so that a failure *between* the two leaves
the account still present (a recoverable state — the resident can retry,
or an admin can complete the deletion) rather than the account gone with
the trail still carrying the identity (an unrecoverable privacy violation).
The ordering is deliberate, not incidental: the `mt` commit is the
*irreversible* step (the pseudonymization is a rewrite, not a delete — but
the profile deletion is a delete, and the memberships are deletes), so it
is the one that must not be left half-done with the identity still
resolvable; the identity deletion is the *reversible-by-re-seed* step (a
re-seeded account re-attaches to the same deterministic tombstone).

### The Web surfaces

- **`GET /account/delete`** + **`POST /account/delete`** (the resident
  self-serve surface, on the `AccountController` beside
  `ChangePassword`). The form asks for the resident's current password
  (verified server-side via `CheckPasswordAsync`, the ADR 0138
  `ChangePassword` POST precedent) and an explicit acknowledgment checkbox
  (the dangerous-action guard, enforced server-side — a bare `[Required]`
  on a `bool` would accept `false`, so the check is explicit). The
  `data-confirm` client dialog is a *second* layer (the repo's existing
  `client/lib/confirm.ts` idiom). **ADR 0142 D5 gate:** when the signed-in
  resident is not a `GlobalAdmin`, the view renders the
  `SelfDeletionRefused` notice (the ADR 0138 `ChangePasswordLockedViewModel`
  "surface-replaced-by-notice" shape, generalized) — the form is still
  rendered (a crafted POST is refused server-side with the same message),
  but the page makes the gate visible. On success the resident is signed
  out and redirected to the login page with a positive confirmation.
- **`POST /admin/delete`** (the GlobalAdmin removal surface, on the
  `AdminController` beside `Block` / `Unblock`). The self-delete guard
  (the `Block` self-guard precedent): a `GlobalAdmin` deleting their own
  account through this surface is refused up-front — they use
  `/account/delete` (the self-serve lane, `Via: Owner`) instead. The
  button is hidden on the admin's own row in both the `Accounts.cshtml`
  list and the `Manage.cshtml` detail (the same "the UI never offers the
  action it would refuse" shape as the `Block` button). On success the
  admin lands on the accounts page with a confirmation.

### The translation keys

One closed `account.delete.*` + `admin.delete_account.*` + `nav.delete_account`
`kw-l` set × en/de/fr/da (the ADR 0015 closed-registry shape, the ADR 0138
`account.change_password.*` precedent), registered in
`KnownTranslationKeys` and seeded by the `FirstBootSeeder`'s existing
`en`-floor + `de`/`fr`/`da` baselines (the `KwLRegistryConsistencyTests`
parity pin holds without change).

## Consequences

- A resident who is a `GlobalAdmin` can leave the platform in-app; a
  resident who is not a `GlobalAdmin` cannot self-delete (the ADR 0142 D5
  gate) — they ask a `GlobalAdmin`, or the operator performs the
  `OPS.md` §9 operator-level procedure. This is a *deliberate*
  restriction (see the "self-serve lane is a GlobalAdmin lane" pin above),
  not a bug.
- The `AccessAudit` trail is now pseudonymized-in-app for every deletion
  (the `OPS.md` §9 operator-level pseudonymization procedure is now a
  fallback for the operator-level path, not the primary path).
- A lone `GlobalAdmin` cannot self-delete (the lockout pin) — the
  `OPS.md` §9 "Two `GlobalAdmin`s should be the standing state" procedure
  is now enforced in the seam, not just documented.
- The `PrivilegedStampMiddleware`'s "account-removed" branch (the
  consequence path) is now reachable from an in-app cause (this lane),
  not just from an operator-level DB delete.
