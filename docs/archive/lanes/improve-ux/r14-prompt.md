---
id: r14-prompt
title: "Prompt: IMPROVE-UX R14 — SEC review of the ADR 0301 grace period (R10–R13)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r13-prompt.md
  - docs/adr/0301-self-service-deletion-grace-period.md
  - docs/adr/0142-delete-account.md
  - docs/adr/0028-guardian-controls-account-scope-supervision.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent doing IMPROVE-UX R14,
> the SEC review of the ADR 0301 grace period (R10–R13). **Who is it for:**
> the next coding agent (fresh context). **When to read the rest:** when
> starting R14 (the review that ADR 0301's two `// TODO ❓` open questions
> defer to).

# Fresh-context prompt: IMPROVE-UX R14

You are continuing the Kumunita repository on `development`. R10–R13 are
already landed — the ADR 0301 grace period is **built**, not built-by-you:

- **R10** (`5218c9de`) — ADR [0301](../../../adr/0301-self-service-deletion-grace-period.md) (amends
  0142 D5): self-serve delete opens to every resident with a grace period +
  a child-under-guardian rule.
- **R11** (`ce7ad89d`) — `DeletionRequest` doc + `IDeletionScheduler` /
  `DeletionScheduler` in `src/Kumunita.Core/Identity/` (schedule/cancel;
  child rule → `UnauthorizedAccessException`, last-GA →
  `InvalidOperationException`, both **at scheduling**).
- **R12** (`269693e7`) — the `DeletionDueTick` / `DeletionDueHandler` that runs
  due rows through the existing `DeleteAccountAsync` lane.
- **R13** (`71827dc2`) — the Web surface: `/account/delete` schedule + cancel,
  the "scheduled for {date}" notice, the child-rule "contact a guardian"
  notice, the D5 `SelfDeletionRefused` / `account.delete_refused` text removed.

**Implement IMPROVE-UX unit R14 only** — the SEC review. This is a **review,
not an implementation**: the R10–R13 code is the subject under review. The
plan card (in `plan-improve-ux.md`, the R14 row):

> R14 — **SEC review** of R10–R13 (independent agent or the SEC lane):
> audit rows, re-auth before scheduling, child rule, guard ordering.
> Done when: **review note in the handoff; findings fixed or filed.**

The four review foci (the card's checklist) plus ADR 0301's two open
questions:

1. **Audit rows.** ADR 0301 D1 says the self-serve audit shape is unchanged
   (exactly one `AccessAudit` row, action `account.delete`, `Via: Owner`).
   But the lane is now **two phases**: schedule (R11 writes an
   `account.delete.scheduled` row) then execute (R12 runs
   `DeleteAccountAsync`, which writes the 0142 D5 `account.delete` row).
   Verify: the two rows are **distinct** (different `Action`), neither
   carries PII in the log/audit template (invariant P-3), and the
   "exactly one `account.delete`" shape holds per phase.
2. **Re-auth before scheduling / cancelling.** The self-serve schedule and
   cancel both touch a resident's own account. Verify the R13
   `CheckPasswordAsync` guards (R13's controller) are present and ordered
   **before** the `IDeletionScheduler` call — not after, and not bypassed by
   the scheduled-state branch. Confirm no path reaches
   `ScheduleDeletionAsync`/`CancelDeletionAsync` without the re-auth.
3. **Child rule.** ADR 0301 D3: a child with an **active** `GuardianLink`
   cannot self-delete; an inactive (`Dissolved`) or absent link does not
   block. Verify the R11 seam enforces this and the R13 surface renders the
   **distinct** "contact a guardian" notice (not the removed
   `SelfDeletionRefused` text).
4. **Guard ordering + the scheduling-vs-execution asymmetry.** R11 enforces
   **both** the last-GA pin and the child rule **at scheduling**; the R12
   handler re-checks the last-GA pin **at execution** but the child rule
   does **not** re-fire at execution. This asymmetry is the `// TODO ❓` R13
   left. Decide and record: is it acceptable that a `GuardianLink` formed
   mid-grace-window does not stop the already-scheduled deletion?
5. **ADR 0301 open questions** (the two `// TODO ❓` the ADR defers to R14):
   - **Grace-period length** — proposed **14 days** (`IDeletionScheduler.GracePeriodDays`).
     Confirm the number or file a change (a one-line constant, not an ADR edit,
     unless the decision changes the *mechanism*).
   - **Child rule on the admin-removal path** — ADR 0301 D3 states "yes
     (clarification)": a GlobalAdmin must dissolve the `GuardianLink` before
     removing a child. Confirm it (it is a `// TODO ❓` the ADR asks R14 to
     settle).

**The deliverable:** a **review note in the handoff**. Create
`docs/plans-milestones/in-progress/improve-ux-handoff-notes.md` (this lane's
running log — it does not exist yet; model the front-matter on
[IS01's prompt](../../../plans-milestones/is01-prompt.md) and
`docs/archive/lanes/improve-code/handoff-notes.md`: `tier: living`,
`kind: handoff`, `status: current`, `lane: improve-ux`, the standard abstract
block, and a `related:` list that includes ADR 0301 and the plan). Add a
**`## R14 — SEC review of the ADR 0301 grace period`** section: the four
focus areas each with its **verdict** (passes / needs fix / filed), the
asymmetry decision + reason, the two open questions resolved (grace-days
confirmed or changed, admin-removal child rule confirmed), any PII /
`Via` / audit-row findings, and a **Findings** sub-list — each finding:
what, where (file:line), severity, and **fixed** (in this unit) or **filed**
(name the next unit / ADR to follow it).

**Scope rules:**
- **Fix or file** — for each finding, either (a) fix it in this unit
  (a targeted change to an R10–R13 file, e.g. an ordering or a notice string)
  and record the fix, or (b) **file** it: name the follow-up unit or ADR that
  will resolve it. Do **not** silently leave a finding with no owner.
- **Preserve R10–R13** (`5218c9de`, `ce7ad89d`, `396ed2a9` (D1 prereq),
  `269693e7`, `71827dc2`) — the review confirms them, not rewrites them.
  A fix is a *targeted* change, not a redesign.
- **Do not start R15** (the kiosk-link unit) or any other unit.
- **No new ADR unless** a finding changes a decision ADR 0301 froze (e.g. the
  grace-days mechanism) — in that case amend ADR 0301 in the same commit.
- **Commits:** the review note is a `docs(improve-ux):` commit. Each **fix**
  lands as its own `feat(ux): … (IMPROVE-UX R14)` commit (the house rule is
  one commit per change; a review note + N fixes = 1 docs commit + N feat
  commits). Do not bundle unrelated work.

Before editing, read [AGENTS.md](../../../../AGENTS.md),
[testing-and-ci.md](../../../dev/testing-and-ci.md), ADR
[0301](../../../adr/0301-self-service-deletion-grace-period.md), ADR
[0142](../../../adr/0142-delete-account.md), ADR
[0028](../../../adr/0028-guardian-controls-account-scope-supervision.md), the R14
card in [plan-improve-ux.md](plan-improve-ux.md), and check `git status` +
the branch (R10–R13 commits must be intact).

Read first (the review's targets):

- **`src/Kumunita.Core/Identity/DeletionScheduler.cs`** — R11's schedule /
  cancel / `ListDueAsync` / `MarkExecutedAsync`; where the child rule and the
  last-GA pin fire (both **before any write** at scheduling), and the
  `account.delete.scheduled` audit row.
- **`src/Kumunita.Web/SideEffects/DeletionDueHandler.cs`** (the
  `DeletionDueTick` that drives it is in `src/Kumunita.Core/Identity/`) —
  R12's execution path; confirm the last-GA pin re-check at execution and
  that the child rule does **not** re-fire.
- **`src/Kumunita.Web/Controllers/AccountController.cs`** — R13's
  `Delete()` GET, `Delete(model)` POST, and the new `DeleteCancel(model)`
  POST; the `CheckPasswordAsync` guard ordering, the
  `catch (UnauthorizedAccessException)` (child rule) and
  `catch (InvalidOperationException)` (last-GA / missing) branches, and the
  `LoadPendingDeletionAsync` store read.
- **`src/Kumunita.Core/Identity/IDeletionScheduler.cs`** — the
  `GracePeriodDays` constant (the D2 open question) and the seam contract.
- The 4 `src/Kumunita.Core/Localization/Keys/*` files — confirm the
  removed keys are gone and the new `account.delete_*` keys carry no PII.

If any fix lands, run the narrowest tests via the in-process runner (see
testing-and-ci.md for the `-class` invocation): the fix's test class +
`DeletionSchedulerTests` (R11 intact) + `IdentityServiceDeleteAccountTests`
(D1 prereq intact) + the R13 tests in
`AdminControllerDeleteAccountTests` + the translation parity test, and
`dotnet build`. If **no** code changes (review only), do **not** run the
build (a scope slip). Then report: the findings list (fixed vs filed, with
owners), the two ADR 0301 open questions resolved, the commit hash(es), and
anything you left open. Make the R14 commit(s); do not commit unrelated work.
