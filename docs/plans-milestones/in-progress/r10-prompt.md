---
id: r10-prompt
title: "Prompt: IMPROVE-UX R10 — ADR 0301 amending 0142 D5 (self-service deletion + grace period)"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/adr/0142-delete-account.md
  - docs/adr/0300-issue-resolved-notification.md
  - docs/ops/privacy-operations.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R10. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R10 (the ADR that amends 0142 D5 before R11–R13 ship the
> code).

# Fresh-context prompt: IMPROVE-UX R10

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R10 only**: write **one new ADR** (number **0301**) that
amends ADR 0142's D5 gate. Today 0142 D5 says *"a non-GlobalAdmin resident
cannot self-delete through `POST /account/delete`"* — the E7 finding (the
"contact an administrator" dead end). R10 flips that restriction: **every
resident** may schedule their own deletion, with a **grace period** (a
cancellable window before the existing `DeleteAccountAsync` lane executes),
and a **child rule** (a child under a `GuardianLink` cannot self-delete).
R10 is **ADR only — no code, no Web surface, no Core, no test**. R11 (Core
doc + service), R12 (Wolverine scheduled job), and R13 (Web surface)
implement the decision; R14 (SEC review) is the follow-up gate. Do not
start R11 or any other unit.

Before editing, read [AGENTS.md](../../../AGENTS.md),
[DOCS-CONVENTIONS.md](../../DOCS-CONVENTIONS.md), and the R10 card in
[plan-improve-ux.md](plan-improve-ux.md). Check `git status` and the
current branch; preserve all existing work.

## What the ADR must decide

The ADR is an **amendment** to 0142 (it does not supersede it — the two
branches, the pseudonymization policy, the last-GlobalAdmin lockout pin,
the cross-store ordering, and the audit-row invariants all **remain**).
The only thing it changes is **who may self-delete** (D5) and **when the
deletion actually executes**. The decisions to capture:

1. **Self-service is open to every resident — with two carve-outs.**
   D5's "a non-GlobalAdmin's invocation throws
   `UnauthorizedAccessException`" is removed. The
   `POST /account/delete` surface (0142 D7) is now available to any
   signed-in resident, not only a GlobalAdmin. The two residents who
   still **cannot** self-delete are: (a) the **last GlobalAdmin**
   (the 0142 D3 lockout pin, decision 2 below) and (b) a **child under
   an active `GuardianLink`** (the new child rule, decision 3 below).
   **Amendment language:** cite D5 by number, state the new rule
   (every resident may self-delete *except* those two), and note that
   the `Via: Owner` audit row + the single-audit-row invariant
   (0142 D5's audit shape) are **unchanged** — only the *who* and the
   *timing* change.

2. **Grace period (cancellable window).** The resident does not delete
   instantly; they **schedule** the deletion. The deletion executes
   **after N days** unless the resident signs in and cancels.
   - **Proposed N: 14 days.** Mark `// TODO ❓` on the final number —
     the SEC review (R14) may adjust it. The ADR states the proposal and
     the cancellation mechanism; it does **not** freeze the number as
     final.
   - **What "cancel" means:** signing in during the grace period
     (any authenticated session as that subject id) is the cancel.
     No extra button, no extra form — the act of returning invalidates
     the pending request. (The Web surface R13 will render a
     "deletion scheduled for {date} — sign out to confirm" notice; the
     ADR defines the rule, R13 renders it.)
   - **The last-GlobalAdmin lockout pin (0142 D3) is unchanged and
     still a hard refusal**: a resident who is the *last* GlobalAdmin
     cannot self-delete — the grace period does **not** relax the
     pin (0142 D3: "a lone GlobalAdmin cannot delete themselves; the
     guard fires *before any write*"). This is the one self-delete
     case that stays refused even after D5 opens the surface to
     every resident. Note for the implementation (R12's scheduled
     job): whether the guard is re-checked at *execution* time
     (to catch a second GlobalAdmin demoted mid-grace-window) is an
     implementation detail — the ADR states only that the pin
     stands and is not relaxed by the grace period.

3. **Child rule.** A child account that has an **active**
   `GuardianLink` (the ADR 0028 guardian-supervision lane) **cannot
   self-delete** — the request is refused at scheduling time with
   `UnauthorizedAccessException` (the resident sees a "contact a
   parent/guardian" notice, the same shape as 0142 D5's old
   `SelfDeletionRefused` but now with a different reason). Rationale:
   the guardian is the accountable party for a child's account;
   unilaterally deleting the child's account is a guardian decision,
   not the child's. An **inactive** or **absent** `GuardianLink` does
   not block self-delete.
   - The child rule applies to **both** the self-delete path *and*
     (clarification) the GlobalAdmin-admin-delete path (a GlobalAdmin
     cannot delete a child with an active `GuardianLink` either —
     dissolve the link first, the ADR 0028 dissolution precedent).

4. **What is NOT changed** (state explicitly in the ADR):
   - 0142 D2 (pseudonymization), D3 (last-GlobalAdmin guard), D4
     (membership + profile + Identity deletion), D5 audit-row shape,
     D6 (cross-store ordering), D7 (Web surface shape), D8
     (translation keys) all **stand**.
   - The GlobalAdmin **removal** surface (`POST /admin/delete`) is
     unchanged — a GlobalAdmin can still remove another resident
     immediately (no grace period on the admin path; the grace period
     applies only to the *self-service* path).
   - The `IIdentityService.DeleteAccountAsync(target, admin)` seam
     contract is unchanged (the grace-period logic lives *above* it —
     in a scheduling service that eventually calls the same
     `DeleteAccountAsync` lane; R11/R12 will define the seam shape).
   - The operator break-glass and the `OPS.md` §9 manual procedure
     remain as fallbacks.

## Files to read before writing

- **ADR 0142** in full —
  [0142-delete-account.md](../../adr/0142-delete-account.md)
  (especially D1–D8 and the Consequences section; the new ADR must
  reference D5 by number and state precisely which sentence is amended).
- **ADR 0028** (guardian supervision) —
  [0028-guardian-controls-account-scope-supervision.md](../../adr/0028-guardian-controls-account-scope-supervision.md)
  (the `GuardianLink` doc, the active/inactive shape, the dissolution
  precedent the child rule references).
- **ADR 0300** (the most recent ADR, R08's `issue.resolved`) —
  [0300-issue-resolved-notification.md](../../adr/0300-issue-resolved-notification.md)
  (use as a style reference: a short, single-decision ADR that amends an
  earlier one rather than superseding it).
- **`ops/privacy-operations.md`** —
  [privacy-operations.md](../../ops/privacy-operations.md)
  (the "Resident requests account deletion" section, §9 — the
  manual procedure the ADR is making obsolete for the common case;
  the ADR should note that this procedure becomes the *fallback*
  for the edge cases: a child with an active guardian link, a resident
  who cannot sign in to cancel, an operator-initiated deletion).
- **The R10 card** in
  [plan-improve-ux.md](plan-improve-ux.md) (the lane rules:
  "A unit that makes a decision writes or amends an ADR in the same
  commit" and the D3 decision table row).
- **DOCS-CONVENTIONS.md** —
  [DOCS-CONVENTIONS.md](../../DOCS-CONVENTIONS.md) (front-matter shape,
  the `related:` / `supersedes:` / `superseded_by:` fields, the
  `amends:` convention — R10 **amends** 0142 D5, does not supersede).

## What the ADR file should contain

Create **`docs/adr/0301-self-service-deletion-grace-period.md`**:

- **Front-matter:** `id: adr-0301`, `status: accepted`, `kind: adr`,
  `supersedes: []`, `superseded_by: []`, `related:` pointing at
  `adr-0142`, `adr-0028`, `adr-0300`, `lane: improve-ux`. The `updated`
  date is today (2026-11-10). The front-matter schema has **no `amends:`
  field** (see DOCS-CONVENTIONS.md) — the amendment link is the
  `related:` entry plus an **`Amends: 0142 (D5)`** line in the body's
  Status/Date block (the 0142 file's own shape: it lists
  "Amends: 0006; 0138" there, not in the front-matter).
- **Title:** something like
  *"Self-service account deletion for every resident: a grace-period
  cancellation window and the child-under-guardian refusal rule,
  amending ADR 0142 D5."*
- **Status / Date / Amends / Builds on** block: amends 0142 D5;
  builds on 0142 (the lane this extends), 0028 (the guardian lane
  the child rule references), 0300 (the most recent ADR in the
  sequence — not a real dependency, just the number-adjacent context).
- **Abstract:** one paragraph: who the ADR is for, what it changes
  (D5's who + the timing), what it does not change (the rest of 0142),
  and the two new rules (grace period + child refusal).
- **Context:** three sentences. (1) 0142 shipped the *mechanism*
  (pseudonymize, delete, guard) but deliberately restricted
  self-service to GlobalAdmins. (2) The E7 finding (the IMPROVE-UX
  evidence table) confirms this is a resident dead-end: the text says
  "contact an administrator" and no invitation or admin action
  exists. (3) The fix is to open the surface to every resident with
  a safety net (the grace period) and a guardian boundary (the child
  rule).
- **Decision:** the four numbered decisions above (self-service
  open, grace period + cancel rule, child rule, what is not
  changed). Use the `// TODO ❓` marker on the N-days proposal.
- **Consequences:** three **reversed** consequences (vs 0142):
  (a) a non-GlobalAdmin resident can now self-delete (was:
  `UnauthorizedAccessException` from the D5 gate); (b) the
  self-delete path is now *scheduled* (grace period) rather than
  immediate — execution timing changes for the self-serve branch
  only (the admin-removal branch stays immediate); (c) the
  OPS §9 manual procedure is now the *fallback* for edge cases
  (a child with an active guardian link, a locked-out account),
  not the primary path for "I want to leave." Plus two **new**
  consequences: (d) the D3 lockout pin is unchanged and still
  fires at execution time (a resident who is the **last**
  GlobalAdmin cannot self-delete, even with the grace period —
  the guard re-checks at the scheduled execution instant, not
  only at scheduling time); (e) the child rule adds one more
  `UnauthorizedAccessException` case (at scheduling time) the
  Web surface (R13) must render with a distinct "contact a
  guardian" notice.
- **The `// TODO ❓` block:** the grace-period length (proposal 14
  days, SEC review R14 may adjust); whether the child rule should
  also apply to the GlobalAdmin-removal path (the ADR states "yes,
  clarification" — flag it for SEC review R14 to confirm).

## Do **not**

- Do not change ADR 0142's file (the amendment is in the new file;
  0142 stays as-is with its `superseded_by: []` — the new ADR's
  `related:` pointer + its body `Amends: 0142 (D5)` line are the link).
- Do not write any code, Web surface, Core service, test, or
  translation key (those are R11–R13).
- Do not touch the GlobalAdmin-removal surface (`POST /admin/delete`)
  — it keeps its immediate, no-grace-period behaviour.
- Do not change the pseudonymization policy, the cross-store ordering,
  the last-GlobalAdmin guard, or the audit-row shape (all 0142
  invariants stand).
- Do not start R11, R12, R13, R14, or any other unit.

## Done when

- `docs/adr/0301-self-service-deletion-grace-period.md` exists, is
  well-formed (front-matter + abstract + context + decision +
  consequences + `// TODO ❓` block), and amends 0142 D5 without
  superseding it.
- The four decisions above are stated (who, grace period + cancel,
  child rule, what is not changed).
- `// TODO ❓` is on the grace-period length and the child-rule-also-
  applies-to-admin-path clarification.
- The front-matter `related:` references `adr-0142` (and the body's
  Status block carries the `Amends: 0142 (D5)` line) — the amendment
  link is real.
- One commit; the commit message follows the repo's conventional-
  commits style with `(ux)` scope and the lane reference
  `(IMPROVE-UX R10)`.
- No unrelated files are in the commit.
