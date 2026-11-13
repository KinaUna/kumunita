---
id: r11-prompt
title: "Prompt: IMPROVE-UX R11 — DeletionRequest doc + the schedule/cancel service (the ADR 0301 grace period in Core)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r10-prompt.md
  - docs/adr/0301-self-service-deletion-grace-period.md
  - docs/adr/0142-delete-account.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R11. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R11 (the Core `DeletionRequest` doc + the schedule/cancel
> service that turn ADR 0301's grace period into code).

# Fresh-context prompt: IMPROVE-UX R11

You are continuing the Kumunita repository on `development`. Implement
**IMPROVE-UX unit R11 only**: the **Core** half of the self-service deletion
grace period that [ADR 0301](../../../adr/0301-self-service-deletion-grace-period.md) decided.
Today `IIdentityService.DeleteAccountAsync` deletes **immediately** (the
0142 seam; the self-serve branch still carries the old GlobalAdmin-only D5
gate in code). R11 adds, **above** that seam, the thing ADR 0301 describes:
a resident **schedules** a deletion (a `DeletionRequest` document) and the
request is **cancellable** by signing back in. Concretely — **one new
document + one new service with two methods + their tests**:

- A **`DeletionRequest`** document (the pending self-serve request): the
  resident's `SubjectId`, when it was scheduled, its **due date** (the grace
  period, the ADR's proposed 14 days — read the constant from one place so
  R14's SEC review can change it in a single edit), and a small status
  (`Pending` → `Cancelled` / `Executed`). One row per resident — a resident
  has **at most one pending** request (a re-schedule resets the row; the
  `GroupInvitation` / `GroupJoinRequest` "reset the existing row" business-key
  convention in [`CoreDocTypes.cs`](../../../../src/Kumunita.Core/CoreDocTypes.cs)).
- **`ScheduleDeletionAsync(subjectId, ct)`** — validates, then writes the
  pending request:
  - **last-GlobalAdmin guard, refused up front** (ADR 0301 D2 / 0142 D3): a
    resident who is the *last* `GlobalAdmin` on the instance cannot self-delete
    — refuse with `InvalidOperationException` **before any write**, reusing the
    exact guard already in `DeleteCoreAsync` (the "Promote a second GlobalAdmin"
    message). (The same guard also fires again at execution time via the
    existing lane — ADR 0301 D2 — so a resident demoted to last-GA mid-window
    is still refused when the job runs.)
  - **child rule, refused up front** (ADR 0301 D3): if the subject has an
    **active** `GuardianLink` (any guardian — a child has standing iff *any*
    active row exists), refuse with `UnauthorizedAccessException` before any
    write ("contact a parent/guardian"). A `Dissolved`/absent link does **not**
    block. This is the new `UnauthorizedAccessException` case R13 renders with
    a distinct notice.
  - On success: write the pending `DeletionRequest` + one audit row (the
    "one summary" `AccessAudit` shape — action `account.delete.scheduled`,
    `Via: Owner`; keep the verb distinct from the execution `account.delete`).
- **`CancelDeletionAsync(subjectId, ct)`** — the ADR's "cancel = signing in":
  move the pending request to `Cancelled` (the resident signs back in, R13
  calls this). A second cancel with no pending row is a **no-op** (not an
  error — the resident returning is idempotent).
- **The execution seam (leave for R12):** the method the Wolverine due-job
  will call to run a due request is the **existing** `IIdentityService
  .DeleteAccountAsync(subjectId, adminSubjectId: subjectId)` lane — R11 does
  **not** re-implement deletion. Expose a small read seam R12 can use to find
  due `Pending` requests (or note that R12 queries the doc directly); do **not**
  write the scheduled job (that is R12).

That is the whole unit: **one new doc, one new service (two methods + a
due-query seam), the `CoreDocTypes` registration, the `[DataClass]` attribute,
and unit tests.** No Web surface, no controller, no route, no translation key,
no ADR (ADR 0301 already made the decision), and **no change to
`IIdentityService`** (its `DeleteAccountAsync` contract is unchanged — the
0142 invariants and the existing last-GlobalAdmin guard in `DeleteCoreAsync`
all stand). Do not start `R12`, `R13`, or any other unit.

Before editing, read [AGENTS.md](../../../../AGENTS.md),
[testing-and-ci.md](../../../dev/testing-and-ci.md), ADR
[0301](../../../adr/0301-self-service-deletion-grace-period.md), the R11 card in
[plan-improve-ux.md](plan-improve-ux.md), and check `git status` + the branch;
preserve all existing work.

Read first (the copy-paste shapes):

- **`src/Kumunita.Core/Identity/IdentityService.AccountOps.cs`** —
  `DeleteAccountAsync` (the two-branch seam + the D5 gate R11 is *replacing at
  the surface*, but NOT changing here) and **`DeleteCoreAsync`** (the
  last-GlobalAdmin guard block you reuse — the `GetUsersInRoleAsync(Roles
  .GlobalAdmin)` "otherGAs.Count == 0 → throw" shape) and **`DeleteChildAccountAsync`**
  (the `GuardianLink` standing-gate query: `.Where(l => l.ChildId == childId &&
  l.Status == GuardianLinkStatus.Active)` — the exact shape your child-rule
  check needs, minus the `GuardianId == guardianId` filter, since a child has
  standing iff *any* active row exists).
- **`src/Kumunita.Core/UserInfo/GuardianLink.cs`** — the `Status` enum
  (`Active` / `Pending` / `Dissolved` / `Declined`) + the `[DataClass(...)]`
  attribute on the doc (your `DeletionRequest` doc must carry `[DataClass]`
  too — ADR [0298](../../../adr/0298-data-classes-in-code.md); the `SecGate` test in
  [`tests/Kumunita.Web.Tests/SecGate/`](../../../../tests/Kumunita.Web.Tests/SecGate/)
  fails a newly-registered doc that lacks it. Pick the `DataClassKind` /
  `Erasure` / `ExportScope` values for a *self-serve request row* — the
  `GuardianLink` row (`AuditModeration` / `Delete` / `Never`) is the closest
  neighbor; a pending self-delete request is **never** exportable and is
  erased with the account, so it is the same shape).
- **`src/Kumunita.Core/CoreDocTypes.cs`** — where to register the doc
  (`opts.Schema.For<DeletionRequest>().Identity(r => r.SubjectId)` — one row
  per resident, the `GuestAccess` / `Profile` "SubjectId is the identity"
  precedent; **no** `UniqueIndex` pair needed — identity is the business key).
- **A `*DocTypes` / service pair for the DI shape** — follow how
  `IdentityService` gets its dependencies (the `DocumentStore`,
  `IUserManager<User>`, `ILogger`) and how the service is registered in
  `DependencyInjection.cs` for whichever context owns it (put the service in
  the `Identity` bounded context, next to `IdentityService`; the ADR names the
  `IIdentityService` seam as the execution lane, so the scheduler is a sibling,
  not a change to that interface).
- **`tests/Kumunita.Core.Tests/IdentityServiceDeleteAccountTests.cs`** — the
  `PostgresFixture` harness + `BootAsync` / `SeedAccountAsync(boot, email,
  globalAdmin:)` / `AuditRows(boot.Store, action:)` helpers are your
  copy-paste shape for the new `DeletionRequestTests` (seed a resident, call
  `ScheduleDeletionAsync`, assert the doc row + the `account.delete.scheduled`
  audit row; seed a child with an active `GuardianLink` and assert
  `UnauthorizedAccessException` + **no** doc row; seed the last-GA and assert
  `InvalidOperationException` + no row; then `CancelDeletionAsync` and assert
  the row is `Cancelled` and a second cancel is a no-op).
- **`src/Kumunita.Core/UserInfo/GuardianService.cs`** — `GuardActiveLinkAsync`
  (the "service is the resolver, the doc is not" rule — G·2) if you want to
  read the child's standing through a seam rather than a raw `GuardianLink`
  query.

Do **not**: change `IIdentityService` or `DeleteAccountAsync` /
`DeleteCoreAsync` (their contract and the 0142 invariants stand — ADR 0301 D4);
write the Wolverine scheduled job (R12); add any Web route, view, controller, or
translation key (R13); open or amend any ADR (0301 already decided it — the
grace length and the child-rule-admin-path are its `// TODO ❓` set, flagged
for R14, **not** for you to re-litigate); delete the existing D5 gate code from
`DeleteAccountAsync` (that surface change is R13's, gated by R14's SEC review);
or register the doc on a *new* `*DocTypes` surface (ADR 0004 §B.1: register on
the **existing** `CoreDocTypes` — additive delta, idempotent, no re-seed).

Keep it to the new doc + service + `CoreDocTypes` line + `DependencyInjection`
registration + tests (roughly five files). Follow the repo's comment rule
(what/when/throws + the ADR number — `ADR 0301`) and the code-check ratchet
(`tools/code/code-check.ps1` — a new file must not raise the baseline; check
the `cs_file` 600-line ceiling before the split). Run the narrowest tests via
the in-process runner (see testing-and-ci.md for the exact `-class` invocation):
your new `DeletionRequest*Tests` + `IdentityServiceDeleteAccountTests` (confirm
the existing lane is untouched) + the `SecGate` `[DataClass]` gate. Then report
changed files, verification results, the commit hash, and any held questions
(especially the one design call you made: the service name + where the
grace-days constant lives, so R14 can adjust it in one place). Make one commit
for R11; do not commit unrelated work.
