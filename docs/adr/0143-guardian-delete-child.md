# ADR 0143 — Guardian deletes a child account: the 6th GU supervisory action that reuses the ADR 0142 deletion core, gated by an active `GuardianLink` and audited `Via: Guardian`

Status: Accepted
Date: 2026-10-05
Builds on: 0028 (the GU standing — the `GuardianLink` document, the
`AccessVia.Guardian` value, and the `GuardActiveLinkAsync` standing gate
this lane reuses; the "the service is the resolver" rule: the standing is
checked off the active row at decision time, live, with no projection);
0142 (the deletion core — `IdentityService.DeleteCoreAsync`: the
last-GlobalAdmin guard, the pseudonymization of the target's audit rows
to a `deleted:{subjectId}` tombstone, the membership + profile removal,
the single `account.delete` summary audit row, and the Identity-account
deletion. This lane reuses that core verbatim; it does not re-implement
the cascade); 0006 (the §A frozen `IIdentityService` surface gains one
*additive* method, `DeleteChildAccountAsync`; the §E compatible-ADD
precedent — the ADR 0013 / M3b `Moderate` ADD shape); 0003 (the
audit-by-default rule — the deletion is one `account.delete` row with the
narrower `Via: Guardian` standing, the ADR 0028 rule for every GU
action)

## Context

ADR 0028 froze the GU surface to **five** supervisory actions
(suspend, community curation, group curation, invitation approval,
dissolve) and — deliberately — chose *suspension* (reversible) over
*deletion*. ADR 0142 then added the full deletion lane, but its two
branches are **self-serve** (a GlobalAdmin deleting *themselves*) and
**admin-initiated** (a GlobalAdmin removing *another* resident). Neither
branch admits the guardian standing: a guardian is neither the target nor
a GlobalAdmin, so `DeleteAccountAsync` refuses a guardian by construction
(the fail-closed pin throws `UnauthorizedAccessException` on both branches).

The product want (2026-10-05), in the words it was given: **a guardian
should be able to delete a child account** — the end-state of a
supervisory relationship gone wrong (an abandoned account, an account the
child no longer needs, a mistake in the add-a-child lane). A *suspend* is
the "pause the account" control; a *delete* is the "remove the account"
control. The two already exist side by side in the admin surface
(Block vs. Delete); this ADR brings the pair to the guardian surface.

This is a **6th supervisory action**, not a new lane. It reuses the ADR
0142 core exactly (the same cascade, the same pseudonymization, the same
audit summary row) and differs in only two things: **who may invoke it**
(an active `GuardianLink`, not a `GlobalAdmin`) and **the `Via` tag**
(`Guardian`, not `Owner`/`Admin`). Everything else — the last-GlobalAdmin
guard, the pseudonymization, the membership removal, the single audit
row, the Identity-account deletion, the "not a silent no-op on a second
call" pin — is inherited from ADR 0142 unchanged.

## Decision

### A. The seam: `IIdentityService.DeleteChildAccountAsync`

One **additive** method on the frozen `IIdentityService` surface (ADR
0006 §A/§E):

```csharp
Task DeleteChildAccountAsync(string childId, string guardianId);
```

- **Standing gate (G·2/G·3, live):** before any write, an **active**
  `GuardianLink` with `GuardianId == guardianId` and `ChildId == childId`
  must exist, else `UnauthorizedAccessException` (the Web's 404; the
  exact `GuardActiveLinkAsync` shape every other GU seam runs). No
  projection, no cache — a dissolve is live on the next read.
- **Delegates to `DeleteCoreAsync`** (the ADR 0142 shared body) with
  `actorId: guardianId` and `via: Authorization.AccessVia.Guardian`.
  The last-GlobalAdmin guard still runs inside the core (a child is
  never a GlobalAdmin, so it is a no-op here — a child cannot be the
  last admin). The "not a silent no-op" property holds: a *second* call
  for the same child (after the first succeeded) is refused — the
  standing gate runs first and the successful delete dissolved the link
  (C·5), so the gate fails with the same 404; a repeat attempt cannot
  re-delete, and it errors rather than succeeding.
- **The `GuardianLink` rows for the deleted child are dissolved
  in-band.** `DeleteCoreAsync` removes the child's memberships and
  profile but does not touch `GuardianLink` rows (a child can have one
  *or two* guardians, ADR 0028 §B, and a co-guardian's row would dangle
  pointing at a deleted account). This seam dissolves **every**
  `GuardianLink` row where `ChildId == childId` (any status) to
  `Dissolved` in the same session — the ADR 0142 core takes an optional
  in-band callback (the ADR 0142 self/admin branches pass no callback;
  this lane supplies one) so no row references the deleted account,
  and the co-guardian's `Index` list no longer shows the child on the
  next read (C4 strong-consistency). Each dissolved row is stamped
  `DissolvedBy == guardianId` (the actor who deleted is the one who
  ended the standing). This is the
  `DelegationGrant.RevokedBy` / `GroupInvitation.ResolvedBy` precedent:
  the standing-holding row carries who ended it.
- **Ordering: standing gate, then existence check.** The standing gate
  runs *first* (the house convention every other GU seam follows — gate
  before existence; also keeps the response uniform for an existing vs.
  a non-existing target — both a 404, no account-existence oracle, the
  ADR 0012/0013 "a non-guardian learns nothing" shape), then the
  existence check (the `SuspendChildAsync` "load the profile, missing →
  `InvalidOperationException`" shape). A *second* call for an
  already-deleted child is refused by the standing gate (the successful
  delete dissolved the link, C·5) with the same 404 — the lane is **not
  a silent no-op**: a repeat attempt cannot re-delete, and it errors
  rather than succeeding.

### B. The Web surface: `GuardianController.DeleteChild`

- `GET /me/children/{childId}/delete` — the danger-zone confirm page.
  Standing-gated like `Detail` (an active link, else 404). Renders the
  consequences lede + the explicit acknowledgment checkbox (the ADR
  0142 `DeleteAccountViewModel.Confirmed` precedent, the ADR 0138
  "dangerous action guard" shape).
- `POST /me/children/{childId}/delete` — the write. The actor is the
  signed-in principal (never a form field). The checkbox must be
  checked (a bare `[Required]` on a bool accepts `false`, so the check
  is explicit — the ADR 0142 precedent). A non-guardian's crafted POST
  is refused server-side (`UnauthorizedAccessException` → the 404
  shape, the fail-closed pin). On success: redirect to `Index` with a
  confirmation `TempData["info"]` (the `Dissolve` action's redirect
  precedent — the child is no longer in the guardian's list).

### C. The invariants (owned by the seam tests)

- **C·1 — Standing is the active link, checked live (G·2).** Every
  `DeleteChildAccountAsync` call resolves the child's active
  `GuardianLink` for this exact pair in the same read before any write;
  a dissolve is live on the very next read (no projection, no cache).
- **C·2 — Deny-by-default (G·3).** A guardian with no active link over
  the child (or a non-guardian) gets an `UnauthorizedAccessException` —
  the Web's 404; they learn nothing. The lane is reachable **only**
  through an active `GuardianLink`; there is no self-serve "delete any
  account" lane and no GlobalAdmin-free path.
- **C·3 — One commit, one summary row (invariant C3).** The
  `GuardianLink` dissolution, the pseudonymization, the membership
  removal, the profile removal, the Identity-account deletion, and the
  single `account.delete` audit row all land in one Marten session
  (`Via: Guardian`); the EF Identity deletion follows (the accepted
  cross-store window ADR 0142 already documents).
- **C·4 — Pseudonymization, not deletion (ADR 0142 §A, the
  `SECURITY.md` §3.1 rule).** The child's audit rows remain (the
  community is still promised "prove what happened") but their
  `ActorId` / `EffectivePrincipalId` are rewritten to
  `deleted:{childId}`. The identity is gone, the fact remains.
- **C·5 — No dangling standing (ADR 0028 §B, C4).** After a
  successful delete, **no** `GuardianLink` row with
  `ChildId == childId` remains active or pending — every one is
  `Dissolved` — so a co-guardian's `Index` list and the child's
  standing gates all see the child as gone on the next read.

### D. Audit — the ADR 0142 summary row, `Via: Guardian`

Exactly one `AccessAudit` row: action `"account.delete"`,
`TargetKind` `"account"`, `TargetId` the child's subject,
`ActorId` the guardian, `Via: Guardian`. The `GuardianLink`
dissolution does **not** write its own `guardian.dissolve` row — it is
subsumed by the single `account.delete` summary row (the ADR 0142
"one summary, many rows" shape). The `Via: Guardian` tag is the
distinction from ADR 0142's two branches: the same action verb, the
narrower standing, the ADR 0028 rule that every GU action records the
narrower `Via`.

## Consequences

- **A guardian can now fully remove a child's account**, in addition to
  suspending it. The two controls (pause vs. remove) are now a pair on
  the guardian surface, exactly as they are on the admin surface.
- **A co-guardian loses their standing** over a deleted child: their
  `GuardianLink` row is dissolved by the deleting guardian's act. This
  is deliberate — the child is gone, so no standing over them remains;
  a re-assignment over the *same* (deleted) account is impossible (the
  account no longer exists), which is the correct outcome.
- **The child's content is pseudonymized, not deleted** (ADR 0142 §A,
  the `SECURITY.md` §3.1 policy). The user chose full deletion: the
  account, profile, and memberships are removed; the audit trail
  survives with the identity replaced by a tombstone.
- **The GlobalAdmin safety valve is unchanged (G·5).** A GlobalAdmin
  may still delete any account through the ADR 0142 admin-initiated
  branch; this ADR adds no new admin surface and removes none.
- **The resident-facing guide ships in the same lane (ADR 0057 D3).**
  The new sixth guardian action is documented in the `child-accounts`
  guide (a "To delete the account" step, en + de/fr/da) and in a new
  `deleting-an-account` guide (added to the `help` page's `## Guides`
  index and the ADR 0057 closed set, en + de/fr/da baselines, `docs/
  guides/CONVENTIONS.md` registry row). The guide update is the owed
  consistency step of this lane — a feature-without-guide would trip
  the ADR 0057 D4 drift pin.

## Trade-offs (FACES)

- **C**oherent (strengthened): the guardian surface now offers the full
  control pair the admin surface offers (suspend *and* delete), and the
  deletion reuses one core rather than branching into a third bespoke
  cascade — the "one seam, many branches" shape the ADR 0142 core
  already established.
- **A**daptive (strengthened): a mistreated child or an abandoned
  account now has a clean, audited, in-app removal path for the
  guardian — a closed response path, not a DB procedure.
- **S**table (consumed): the deletion is irreversible by design (the
  user's explicit choice, the ADR 0142 "not a silent no-op" pin). The
  cost is the lost account; the counterweight is the pseudonymized
  audit trail (the fact remains) and the GlobalAdmin surface (the G·5
  valve — an admin can still investigate, and can re-seed if a mistake
  is proven).

## Rollout & rollback

- **No schema change:** `GuardianLink` already exists (ADR 0028); this
  ADR only *dissolves* rows (a status write), it does not add columns.
  ADR 0004 §B additive-discipline holds (no DDL, no re-seed).
- **Additive interface:** `IIdentityService` gains one method (the
  ADR 0013 / M3b ADD precedent); no existing caller's contract changes.
- **Rollback:** remove the `GuardianController.DeleteChild` action +
  the `IIdentityService.DeleteChildAccountAsync` seam. No data
  migration (nothing new is stored).
