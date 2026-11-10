---
id: adr-0301
title: "Self-service account deletion for every resident: a grace-period cancellation window and the child-under-guardian refusal rule, amending ADR 0142 D5"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0142-delete-account.md
  - docs/adr/0028-guardian-controls-account-scope-supervision.md
  - docs/adr/0300-issue-resolved-notification.md
  - docs/ops/privacy-operations.md
  - docs/plans-milestones/in-progress/plan-improve-ux.md
supersedes: []
superseded_by: []
lane: improve-ux
updated: 2026-11-10
---

# ADR 0301 — Self-service account deletion for every resident: a grace-period cancellation window and the child-under-guardian refusal rule, amending ADR 0142 D5

Status: Accepted
Date: 2026-11-10
Amends: 0142 (D5 — the "the self-serve lane is a GlobalAdmin lane" gate is
removed: a non-GlobalAdmin resident may now self-delete through
`POST /account/delete`. Only the *who* and the *timing* of the self-serve
branch change — the D5 audit-row shape (`Via: Owner`, exactly one row) and
every other 0142 invariant stand.)
Builds on: 0142 (the delete-account lane this ADR opens up), 0028 (the
`GuardianLink` the child rule reads), 0300 (the most recent ADR in the
sequence — number-adjacent context, not a dependency)

> **Abstract:** this ADR amends ADR [0142](0142-delete-account.md) D5: the
> self-serve delete-account surface (`POST /account/delete`) opens from
> *GlobalAdmin-only* to **every signed-in resident**, with two carve-outs —
> the last-GlobalAdmin lockout pin (0142 D3, unchanged) and a new
> **child rule** (a child under an active `GuardianLink`, ADR
> [0028](0028-guardian-controls-account-scope-supervision.md), cannot self-delete).
> Deletion is no longer immediate on the self-serve branch: the resident
> **schedules** it, and it executes after a **grace period** (proposed
> 14 days) that they cancel simply by signing back in. Everything else in
> 0142 — the pseudonymization policy (D2), the last-GlobalAdmin guard (D3),
> the membership/profile/Identity deletion (D4), the single-audit-row shape
> (D5), the cross-store ordering (D6), the Web surface shape (D7), the
> translation keys (D8), and the *immediate* admin-removal branch — stands.
> **Who is it for:** the developer implementing IMPROVE-UX R11–R13 (the Core
> scheduling doc + service, the Wolverine due job, the Web surface) and the
> SEC reviewer at R14. **When to read the rest:** before touching the
> delete-account lane again, or when reviewing the grace-period code.

## Context

ADR [0142](0142-delete-account.md) shipped the *mechanism* — pseudonymize the
audit trail, delete the memberships/profile/Identity account, hold the
last-GlobalAdmin pin — but deliberately restricted the self-serve branch to
GlobalAdmins (D5: "a non-GlobalAdmin's invocation throws
`UnauthorizedAccessException`"). IMPROVE-UX finding **E7** confirms that this
restriction is a resident dead-end in practice: the `account.delete_refused`
text says "contact an administrator", and no invitation or admin action
exists that a resident can take — "I want to leave" is a journey that cannot
complete in-app. The fix is to open the surface to every resident with two
safety nets: a **grace period** (a cancellable window before the existing
`DeleteAccountAsync` lane executes) and a **guardian boundary** (a child
cannot unilaterally delete an account their guardian is accountable for).

## Decision

- **D1 — Self-service is open to every resident, with two carve-outs (amends
  0142 D5).** The 0142 D5 sentence *"a non-GlobalAdmin's invocation throws
  `UnauthorizedAccessException`"* is removed. The `POST /account/delete`
  surface (0142 D7) is now available to **any signed-in resident**, not only
  a `GlobalAdmin`. The two residents who still **cannot** self-delete are:
  (a) the **last `GlobalAdmin`** — the 0142 D3 lockout pin, decision D2
  below, unchanged; and (b) a **child under an active `GuardianLink`** — the
  new child rule, decision D3 below. 0142 D5's audit shape is **unchanged**:
  exactly one `AccessAudit` row, action `account.delete`, `Via: Owner` on
  the self-serve branch — only the *who* and the *timing* change.

- **D2 — The grace period: the self-serve branch schedules, not executes.**
  A resident who requests deletion **schedules** it; the deletion executes
  **after N days** unless the resident signs in and cancels.
  `// TODO ❓` N is proposed as **14 days** — the SEC review (R14) may
  adjust it. This ADR states the proposal and the cancellation mechanism;
  it does **not** freeze the number as final.
  - **What "cancel" means:** signing in during the grace period (any
    authenticated session as that subject id) **is** the cancel — the act of
    returning invalidates the pending request. No extra button, no extra
    form. The Web surface (R13) renders the "deletion scheduled for
    {date} — sign in to cancel" notice; this ADR defines the rule.
  - **The last-GlobalAdmin lockout pin (0142 D3) stands and is not relaxed
    by the grace period.** A resident who is the *last* `GlobalAdmin` cannot
    self-delete — the guard fires *before any write*, as today. The
    scheduled-execution path (R12) re-checks the pin at the execution instant
    so that a resident demoted to last-GlobalAdmin mid-grace-window is
    refused at execution, not only at scheduling.

- **D3 — The child rule.** A child account that has an **active**
  `GuardianLink` (the ADR [0028](0028-guardian-controls-account-scope-supervision.md)
  guardian-supervision lane) **cannot self-delete** — the request is refused
  at scheduling time with `UnauthorizedAccessException`, and the resident
  sees a "contact a parent/guardian" notice (the same shape as 0142 D5's old
  `SelfDeletionRefused`, with a different reason). Rationale: the guardian
  is the accountable party for a child's account; unilaterally deleting the
  child's account is a guardian decision, not the child's. An **inactive**
  (`Dissolved`) or **absent** `GuardianLink` does **not** block self-delete.
  - **Also the GlobalAdmin-removal path (clarification):** a `GlobalAdmin`
    cannot delete a child with an active `GuardianLink` either — dissolve
    the link first (the ADR [0028](0028-guardian-controls-account-scope-supervision.md)
    dissolution precedent). `// TODO ❓` recorded as "yes (clarification)"
    here; the SEC review (R14) confirms it.

- **D4 — What is NOT changed.** ADR [0142](0142-delete-account.md) D2
  (pseudonymization), D3 (last-GlobalAdmin guard), D4 (membership + profile
  + Identity deletion), D5 (single-audit-row shape), D6 (cross-store
  ordering), D7 (Web surface shape), and D8 (translation keys) all
  **stand**. The GlobalAdmin **removal** surface (`POST /admin/delete`) is
  unchanged — a `GlobalAdmin` can still remove another resident
  **immediately**; the grace period applies only to the *self-serve*
  branch. The `IIdentityService.DeleteAccountAsync(target, admin)` seam
  contract is unchanged — the grace-period logic lives *above* it, in a
  scheduling service that eventually runs the same `DeleteAccountAsync`
  lane (R11/R12 define the seam shape). The operator break-glass and the
  [privacy-operations.md](../ops/privacy-operations.md) §9 manual procedure
  remain as fallbacks.

## Consequences

**Reversed (vs 0142):**

- A non-`GlobalAdmin` resident can now self-delete (was: the D5 gate threw
  `UnauthorizedAccessException`). The "contact an administrator" dead end
  (IMPROVE-UX E7) is closed.
- The self-delete path is now *scheduled* (grace period) rather than
  immediate — execution timing changes for the **self-serve branch only**;
  the admin-removal branch (`POST /admin/delete`) stays immediate.
- The [privacy-operations.md](../ops/privacy-operations.md) §9 manual
  procedure is now the *fallback* for edge cases (a child with an active
  guardian link, a resident who cannot sign in to cancel, an
  operator-initiated deletion), not the primary path for "I want to leave"
  (IMPROVE-UX O05 updates the runbook wording).

**New:**

- The D3 lockout pin is unchanged and still fires at execution time: a
  resident who is the **last** `GlobalAdmin` cannot self-delete, even with
  the grace period — the guard re-checks at the scheduled execution instant,
  not only at scheduling time.
- The child rule adds one more `UnauthorizedAccessException` case (at
  scheduling time) that the Web surface (R13) must render with a distinct
  "contact a guardian" notice, not the old `SelfDeletionRefused` text.

## Open questions (the `// TODO ❓` set)

- `// TODO ❓` **Grace-period length** — proposed **14 days** (D2). The SEC
  review (R14) may adjust; the number is not frozen by this ADR.
- `// TODO ❓` **Child rule on the admin-removal path** — this ADR states
  "yes, clarification" (D3): a `GlobalAdmin` must dissolve the
  `GuardianLink` before removing a child. The SEC review (R14) confirms.
