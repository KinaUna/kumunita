---
id: r12-prompt
title: "Prompt: IMPROVE-UX R12 — the Wolverine due-job that runs the scheduled deletions"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/r11-prompt.md
  - docs/adr/0301-self-service-deletion-grace-period.md
  - docs/adr/0142-delete-account.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R12. **Who is it for:** the next coding agent. **When to read the rest:**
> when starting R12 (the scheduled job that turns R11's pending
> `DeletionRequest` rows into executed deletions).

# Fresh-context prompt: IMPROVE-UX R12

You are continuing the Kumunita repository on `development`. R11 is already
landed (the Core half of the ADR 0301 grace period): it added the
`DeletionRequest` doc + the `IDeletionScheduler` / `DeletionScheduler`
service (in `src/Kumunita.Core/Identity/`) with
`ListDueAsync(now)` (finds `Pending` rows past their `DueAt`) and
`MarkExecutedAsync(subjectId)` (a no-op when the row is already gone).
**Implement IMPROVE-UX unit R12 only**: the scheduled job that, on a cadence,
finds due `Pending` rows and runs each through the **existing**
`IIdentityService.DeleteAccountAsync(subjectId, adminSubjectId: subjectId)`
lane (the self-serve branch — ADR 0301 D4 keeps that lane's contract
unchanged; R12 does **not** re-implement deletion). Concretely — **one new
tick message + one new handler + one kickoff line + their tests**, in the
existing recurring-job shape:

- A **tick message** — a `Wolverine.TimeoutMessage` record with a baked-in
  cadence (the `MailAlarmsTick` 1-hour / `UsagePurgeTick` 1-day shape). Pick a
  sensible check cadence for a 14-day grace window (hourly is a fine
  default — the `ListDueAsync` query is cheap and a no-op when nothing is due);
  the cadence is a knob, not a decision the ADR freezes.
- A **handler** — the sanctioned recurring-job adapter
  (the `UsagePurgeHandler` / `AuditPurgeHandler` static `Handle(tick, …)`
  shape): resolve `IDeletionScheduler` + `IIdentityService`, call
  `ListDueAsync(DateTimeOffset.UtcNow)`, and for **each** due row run
  `IIdentityService.DeleteAccountAsync(row.SubjectId, adminSubjectId:
  row.SubjectId)`. After the lane, the `DeletionRequest` row is **already
  gone** (the `ResidentAccountRowsEraser` block 13 deletes it in the same
  commit as the account — the `GuardianLink` shape), so the next tick's
  `ListDueAsync` will not return it again: **that is the "executes once"
  invariant**, not `MarkExecutedAsync` (calling it is a harmless no-op — the
  row is already erased). Then re-publish the tick
  (`return new[] { new <Tick>() }`).
- A **kickoff line** in `Program.cs` (the `UsagePurgeTick` /
  `StorageMetricsCaptureTick` seed block, after `StartAsync`, in the startup
  scope): `await bus.PublishAsync(new <Tick>());`.

That is the whole unit: **one tick record, one handler in
`src/Kumunita.Web/SideEffects/`, one `Program.cs` publish line, and unit
tests.** No new ADR (ADR 0301 already made the decision), no Web surface /
view / controller / translation key (R13), **no change to
`IDeletionScheduler` / `DeletionScheduler` or `IIdentityService`** (R11's
seams stand), and **no change to the `DeleteAccountAsync` lane** (its 0142
invariants + the execution-time last-GlobalAdmin guard all stand). Do not
start `R13`, `R14`, or any other unit.

Before editing, read [AGENTS.md](../../../AGENTS.md),
[testing-and-ci.md](../../dev/testing-and-ci.md), ADR
[0301](../../adr/0301-self-service-deletion-grace-period.md), the R12 card in
[plan-improve-ux.md](plan-improve-ux.md), and check `git status` + the branch;
preserve all existing work (R11's commit `ce7ad89d` must remain intact).

Read first (the copy-paste shapes):

- **`src/Kumunita.Core/Notifications/MailAlarmsTick.cs`** — the tick-record
  shape: `public sealed record MailAlarmsTick() : Wolverine
  .TimeoutMessage(TimeSpan.FromHours(1));` — your `<DeletionDue>Tick` is the
  same shape (one class, one baked-in cadence; the delay lives in the type, so
  a re-publish carries the same schedule — no per-callsite `DelayedFor`).
- **`src/Kumunita.Web/SideEffects/UsagePurgeHandler.cs`** — the handler shape
  verbatim: a `public static class` with `public static async
  Task<IEnumerable<object>> Handle(<Tick> tick, <deps>)` that (a) runs the
  business logic (yours: `scheduler.ListDueAsync(UtcNow)` then, per row,
  `identity.DeleteAccountAsync(subjectId, adminSubjectId: subjectId)`) and (b)
  `return new[] { new <Tick>() };` to self-reschedule. `Task<IEnumerable
  <object>>` is the async-eligible cascade shape (an `IEnumerable<object>`
  iterator can't `await`).
- **`src/Kumunita.Web/Program.cs`** — the kickoff seed block (~line 1325–1360):
  the `UsagePurgeTick` / `StorageMetricsCaptureTick` / `MailAlarmsTick` publish
  lines, all `await bus.PublishAsync(new <Tick>())` inside the
  `startupScope` (after `StartAsync`, in a scope — `IMessageBus` is scoped and
  Wolverine asserts the host has started). Add your line alongside them.
- **`src/Kumunita.Core/Identity/IDeletionScheduler.cs` + `DeletionScheduler
  .cs`** — the R11 seams you call: `ListDueAsync(DateTimeOffset now, ct)` and
  `MarkExecutedAsync(subjectId, ct)`. Read `DeletionRequest.cs` for the
  `Pending` / `DueAt` / `Status` shape. Note the `GracePeriodDays` interface
  const (the 14-day proposal) — your job's cadence is independent of it.
- **`tests/Kumunita.Core.Tests/DeletionSchedulerTests.cs`** — the
  `PostgresFixture` harness + `SeedAccountAsync` / `PlantGuardianLinkAsync` /
  `DeletionRequestRowAsync` / `AuditRows` helpers R11 landed (your copy-paste
  shape): seed a resident, `ScheduleDeletionAsync`, force the row past its
  `DueAt` (the `ForceDueAsync` margin shape), then run the job's body and
  assert the account is gone + the `DeletionRequest` row is erased + the due
  list is now empty.

**The one design call to make (record it as a `// TODO ❓` at the handler
site, and in your report):** when `DeleteAccountAsync` **throws at execution
time** — specifically the last-GlobalAdmin guard (a resident promoted to
last-`GlobalAdmin` mid-grace-window, ADR 0301 D2 — "a resident demoted to
last-GlobalAdmin mid-window is still refused when the job runs") — the
`DeletionRequest` row is **not** erased (no write happened), so the next tick
re-returns it and the job retries the refusal forever. Decide the
no-infinite-retry behavior: the minimal correct one is to catch the per-row
`InvalidOperationException` / `UnauthorizedAccessException`, log it (no PII —
the IS08 log gate), and move the row **out of `Pending`** so it stops
retrying (the `DeletionRequestStatus` enum has `Pending` / `Cancelled` /
`Executed` — a refused-at-execution row is most honestly
`Cancelled`-with-a-note, or R12 may add a fourth state; that is **your** call,
but it must not loop). A `Cancelled`/already-erased row must **never** be
re-executed. This is the R14 SEC-review surface — flag it, don't silently
pick one.

Do **not**: re-implement deletion (the `DeleteAccountAsync` lane is the
execution path — ADR 0301 D4); change `IDeletionScheduler` /
`DeletionScheduler` / `DeletionRequest` (R11's seams stand); add any Web
route / view / controller / translation key (R13); open or amend any ADR (0301
already decided it — the grace length and the child-rule-admin-path are R14's
`// TODO ❓` set, not yours to re-litigate); or write a *new* scheduled-job
mechanism (use the existing `TimeoutMessage` + `SideEffects/*Handler` +
`Program.cs`-kickoff shape — do not invent a `BackgroundService` /
`IHostedService`).

Keep it to the tick record + the handler + the `Program.cs` line + tests
(roughly three files). Follow the repo's comment rule (what/when/throws + the
ADR number — `ADR 0301`) and the code-check ratchet
(`tools/code/code-check.ps1` — a new file must not raise the baseline; check
the `cs_file` 600-line ceiling). Note the code-check gate is a **known-red**
at HEAD (`improve-sec-known-reds.md`) — confirm your files add **no new**
violations, not that the gate goes green. Run the narrowest tests via the
in-process runner (see testing-and-ci.md for the exact `-class` invocation):
your new handler/tick tests + `DeletionSchedulerTests` (confirm R11 is
untouched) + `IdentityServiceDeleteAccountTests` (confirm the existing lane is
untouched). Then report changed files, verification results, the commit hash,
and the one design call you made (the refused-at-execution retry behavior).
Make one commit for R12; do not commit unrelated work.
