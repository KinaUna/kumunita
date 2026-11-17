---
id: r13-prompt
title: "Prompt: IMPROVE-UX R13 — the Web surface for the ADR 0301 grace period"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r12-prompt.md
  - docs/adr/0301-self-service-deletion-grace-period.md
  - docs/adr/0142-delete-account.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R13. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R13 (the Web surface that turns R11's scheduling seams into a
> resident-facing "schedule my deletion / cancel by signing in" flow).

# Fresh-context prompt: IMPROVE-UX R13

You are continuing the Kumunita repository on `development`. R11, R12 and
the ADR 0301 D1 prerequisite are already landed:

- **R11** (`ce7ad89d`) — `DeletionRequest` + `IDeletionScheduler` /
  `DeletionScheduler` in `src/Kumunita.Core/Identity/`:
  `ScheduleDeletionAsync(subjectId)` (validates → writes the pending row + one
  audit row; up-front refusals: last-GlobalAdmin → `InvalidOperationException`,
  active `GuardianLink` child rule → `UnauthorizedAccessException`),
  `CancelDeletionAsync(subjectId)` (idempotent; `Pending` → `Cancelled`, no
  audit row — the `CancelledAt` timestamp is the record), `ListDueAsync`,
  `MarkExecutedAsync`. The grace length is `IDeletionScheduler.GracePeriodDays`
  (= 14, proposed — R14's SEC review may adjust).
- **R12** (`269693e7`) — the `DeletionDueTick` / `DeletionDueHandler` that runs
  due rows through the existing `DeleteAccountAsync` lane.
- **ADR 0301 D1 prereq** (`396ed2a9`) — **removed the 0142 D5 GlobalAdmin gate
  from the self-serve branch of `IIdentityService.DeleteAccountAsync`** (a
  non-GlobalAdmin may now self-delete; the surviving refusal is the 0142 D3
  last-GlobalAdmin pin, inside `DeleteCoreAsync`).

**Implement IMPROVE-UX unit R13 only**: the Web surface for the ADR 0301 grace
period. The plan card (in `plan-improve-ux.md`, the R13 row):

> R13 — `Account/Delete` for every resident — schedule, show date, cancel;
> signing in during the grace period shows a notice with Cancel; export link
> offered first; keys (replace `account.delete_refused`). Done when: **flow
> works signed in; refused text gone.**

**The core change: the self-serve delete-account surface becomes a
schedule/cancel flow, not an immediate-delete flow.** The two existing actions
(`GET /account/delete` + `POST /account/delete`, `AccountController.cs` ~167–320)
are the surface to change. **The admin-initiated branch is unchanged** (a
GlobalAdmin removing another resident stays immediate — ADR 0301 D4).

Concretely:

- **`GET /account/delete`** — render for **every resident** (the D5
  `SelfDeletionRefused` notice for non-GlobalAdmins is removed — ADR 0301 D1).
  Keep the password + acknowledgment-checkbox fields. **If the resident already
  has a pending `DeletionRequest` row, show the "deletion scheduled for {date}"
  notice + a Cancel button instead of the schedule form** (ADR 0301 D2).
  **Offer the export link first** (the existing `/account/portability` surface).
- **`POST /account/delete`** — keep the `model.Confirmed` +
  `CheckPasswordAsync` guards. On success, call
  **`IDeletionScheduler.ScheduleDeletionAsync(subjectId)`** (NOT
  `identity.DeleteAccountAsync` — R13 *schedules*; R12's due-job *executes*
  later). `catch (InvalidOperationException)` stays (last-GA pin / missing
  account). **Rewrite the `catch (UnauthorizedAccessException)` block
  (`AccountController.cs:295-300`)** — it currently says "only available to a
  GlobalAdmin" (the removed D5 gate); it now fires only for the **child rule**
  (ADR 0301 D3) — surface the **distinct** "contact a parent/guardian" notice.
- **`POST /account/delete/cancel`** (new action) — the Cancel button. Calls
  `IDeletionScheduler.CancelDeletionAsync(subjectId)` (idempotent), redirects
  back to `GET /account/delete` (the schedule form shows again).
- **The "deletion scheduled" notice** (ADR 0301 D2: "the Web surface renders
  the 'deletion scheduled for {date} — sign in to cancel' notice") — placement
  is a design call: the account menu (`Views/Shared/_AccountNav.cshtml`) or the
  notice strip (ADR 0174). Show the `DueAt` date (the resident's timezone) + a
  link to `/account/delete`.
- **Translation keys** (en/de/fr/da; de/fr/da machine-written, flagged for the
  parked native review) — add the schedule/cancel/child-refused/last-GA states;
  **replace** `account.delete_refused` and **remove** `validation.delete_admin_only`
  (the D5 gate text — no longer applies). The translation parity test must pass.
- **`DeleteAccountViewModel`** — remove/repurpose `SelfDeletionRefused` (the
  D5 gate); add what the view needs for the scheduled state (`IsScheduled` /
  `ScheduledFor`) and the child rule (`IsChildRefused`).

That is the whole unit: **the `AccountController` `Delete` GET+POST rewritten,
one new `DeleteCancel` POST, `Views/Account/Delete.cshtml`,
`DeleteAccountViewModels.cs`, the translation keys (en/de/fr/da), and the
Web.Tests.** No new ADR (ADR 0301 already decided it). **No change to
`IDeletionScheduler` / `DeletionScheduler`** (R11's seams stand — they are
registered in DI, `src/Kumunita.Core/Identity/DependencyInjection.cs:38`; add
`IDeletionScheduler scheduler` to the controller ctor, the `identity` param's
shape). **No change to `IIdentityService.DeleteAccountAsync`** (the D1 prereq
already removed the gate; the lane stands). **No change to the R12 handler.**
Do not start `R14` or any other unit.

**The one design call to make (record it as a `// TODO ❓` at the controller
site, and in your report):** the ADR 0301 D3 child rule fires at
**scheduling** time (the R11 seam enforces it). The R12 handler's
`catch (UnauthorizedAccessException)` is *defensive* (at HEAD the self-serve
branch does not throw it, so it is unreachable for the child rule at execution).
R13 only needs to know the child rule is a **scheduling-time** refusal, and to
rewrite the controller's catch for the "contact a parent/guardian" notice. The
open question (flag, don't silently pick) is whether the child rule should also
be *re-checked* at execution time (R14's SEC-review surface). R13 does **not**
touch the handler.

Before editing, read [AGENTS.md](../../../../AGENTS.md),
[testing-and-ci.md](../../../dev/testing-and-ci.md), ADR
[0301](../../../adr/0301-self-service-deletion-grace-period.md), ADR
[0142](../../../adr/0142-delete-account.md), the R13 card in
[plan-improve-ux.md](plan-improve-ux.md), and check `git status` + the branch;
preserve all existing work (R11 `ce7ad89d`, R12 `269693e7`, the D1 prereq
`396ed2a9` must remain intact).

Read first (the copy-paste shapes):

- **`src/Kumunita.Web/Controllers/AccountController.cs`** (`Delete()` GET +
  `Delete(model)` POST, ~167–320) — the surface to rewrite. The guards stay;
  the D5 gate logic (`roles.Contains(Roles.GlobalAdmin)` + `SelfDeletionRefused`
  + the "only available to a GlobalAdmin" catch) is removed/rewritten. The
  `identity.DeleteAccountAsync(subject, adminSubjectId: subject)` call is
  **replaced** with `scheduler.ScheduleDeletionAsync(subject)`. Add the
  `scheduler` ctor param (the `identity` param's shape — see the constructor at
  the top of the file).
- **`src/Kumunita.Web/Views/Account/Delete.cshtml`** — the form to update:
  remove the `SelfDeletionRefused` notice; add the scheduled-state notice +
  Cancel button, the export link (see `Views/Account/Portability/Index.cshtml`
  for the link shape), and the child-rule "contact a parent/guardian" notice.
- **`tests/Kumunita.Web.Tests/AdminControllerDeleteAccountTests.cs`** — the
  NSubstitute harness to copy (`BuildAccount`, `Build`,
  `Substitute.For<IIdentityService>()` + `Substitute.For<UserManager<User>>()`).
  **Rewrite** `Account_Delete_Post_Valid_Calls_Core_SelfBranch` (it asserts
  `identity.Received(1).DeleteAccountAsync(subject, subject)` → now assert
  `scheduler.Received(1).ScheduleDeletionAsync(subject)`); add the
  `DeleteCancel` + child-rule-notice tests.
- **`src/Kumunita.Core/Identity/IDeletionScheduler.cs` + `DeletionRequest.cs`** —
  the seams you call (`ScheduleDeletionAsync`, `CancelDeletionAsync`,
  `GracePeriodDays`, the `Pending`/`DueAt`/`Status` shape).
- The 4 `src/Kumunita.Core/Localization/Keys/*` files — search for
  `account.delete_refused` + `validation.delete_admin_only` to find the exact
  file + line; add the new keys (en/de/fr/da), remove the D5-gate one.

Keep it to the controller + view + view model + translation keys + tests
(roughly five files). Follow the repo's comment rule (what/when/throws + the
ADR numbers — `ADR 0301` / `ADR 0142`) and the code-check ratchet
(`tools/code/code-check.ps1` — a new file must not raise the baseline; check the
`cs_file` 600-line ceiling). The code-check gate is a **known-red** at HEAD
(`improve-sec-known-reds.md`) — confirm your files add **no new** violations,
not that the gate goes green. Run the narrowest tests via the in-process runner
(see testing-and-ci.md for the `-class` invocation): your new/updated
`AdminControllerDeleteAccountTests` + `DeletionSchedulerTests` (R11 intact) +
`IdentityServiceDeleteAccountTests` (D1 prereq intact) + the translation parity
test. Then report changed files, verification results, the commit hash, and the
one design call you made (the child-rule-at-execution question — flag it, don't
silently pick one). Make one commit for R13; do not commit unrelated work.
