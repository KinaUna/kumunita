---
id: is34a-prompt
title: "IS34a prompt — RetentionTick record + Core RetentionService, part 1 (E5, D6)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS34a — `RetentionTick` record + `RetentionService`, part 1 (sessions, tokens, outbox, dead letters)

## What you are doing

You are the **thirty-fourth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS33 are committed
(IS33 = `RetentionOptions` + `RetentionPolicy`, the pure D6/E5 foundation — see
the handoff note's `## IS33` section for the exact `CutoffFor` signature).

**Finding E5 (severity M), the I/O leg:** IS33 made the retention *decision*
pure (`RetentionOptions` holds the D6 days-per-key, `RetentionPolicy.CutoffFor`
decides the cutoff, `0` = keep). This unit builds the **I/O** that consumes
them: the `RetentionTick` recurring-message record, the `RetentionService`
that deletes/nulls the part-1 types, and the unit test. Part 1 covers
**`SessionDoc`, `IdentityToken`/`EscalationToken`, `EmailDeadLetter`, and
`OutboxEmail`** (null body after 7 d, delete row after 90 d). The spec is the
plan's **IS34a** section (`docs/plans-milestones/plan-improve-sec.md`).

This is the **Core service** unit. The Wolverine handler and the `Program.cs`
seed are **IS34b**; part 2 (notifications, error reports, log files) is **IS35**.
Do **not** write the handler, do **not** touch `Program.cs`, and do **not** add
notifications/error-report/log-file logic here.

## The one thing to get right

**Consume IS33's seams — never re-derive the cutoff.** The service reads the
days from `RetentionOptions` and calls `RetentionPolicy.CutoffFor(options, key,
now)` for the cutoff; it must **not** inline `now.AddDays(-days)` or read the
key's days directly for the cutoff math. `0`/absent key → `CutoffFor` returns
`null` → **skip that type** (keep). This is the single-source-of-rule contract
the whole lane pins; the `UsagePurgeService.PurgeAsync` shape (I/O inlines the
SQL-translatable `field < cutoff`, but the cutoff value comes from the pure
`UsagePurgePolicy`) is the precedent — same split, applied to `RetentionService`.

**`RetentionTick` mirrors `MediaLifecycleTick`.** One `sealed record` in
`Kumunita.Core.Privacy`, a `Wolverine.TimeoutMessage` with the 1-day delay baked
in (the `UsagePurgeTick` / `MediaLifecycleTick` shape verbatim) — so every
re-publish carries the same schedule, no per-callsite `DelayedFor`. IS34b's
handler re-yields it after each run; you only define the record.

**The service returns counts only, no PII.** It queries + deletes (and nulls
`OutboxEmail.Body`) by cutoff and returns the affected counts. No email, no
body, no subject id in the return or in any log line (the IS08 `LogTemplatePii
GateTests` apply to anything you log — log the counts, never the rows).

## Constraint that keeps existing tests green

- **`RetentionOptions` + `RetentionPolicy` are IS33's and are frozen** — read
  them to call `CutoffFor` / `DaysFor`; do **not** change them. The `0` = keep
  semantics live there; trust them.
- **No `Program.cs`, no handler, no DI in `Program.cs`.** The handler + the
  `bus.PublishAsync` seed are IS34b. Register `RetentionService` in the `Privacy`
  module's `DependencyInjection.cs` **if one exists** (a 4th file — the plan's
  "a 4th file" note); otherwise defer the DI registration to IS34b (record it).
- **The service is I/O, so its tests use the store harness** — this is the
  opposite of IS33. Follow the `UsagePurgeServiceTests` / `MediaLifecycleHandler`
  pattern (the `PostgresFixture` / `DocumentStore` idiom the lane's other
  service tests use). The pure cutoff math is already covered by IS33's
  `RetentionPolicyTests` — do not re-test it here; test the **I/O behaviour**
  (old rows deleted, live rows kept, idempotency).
- **`PortabilityDocTypes` (M11) is untouched** (C-M11·5 closed-set pin) — this
  unit deletes rows, it adds no doc type and no doc-type surface.
- **PII rule:** the tests use synthetic fixture ids (`user-a`, `user-b`) and
  `a@example.test`-style addresses only; no real PII.

## Deliverables (≤ 3 files, + the optional DI line)

1. **`src/Kumunita.Core/Privacy/RetentionTick.cs`** (new) — `sealed record
   RetentionTick() : Wolverine.TimeoutMessage(TimeSpan.FromDays(1))`, the
   `MediaLifecycleTick` / `UsagePurgeTick` shape verbatim (the 1-day delay baked
   in; one class, one schedule, re-yielded by IS34b's handler).

2. **`src/Kumunita.Core/Privacy/RetentionService.cs`** (new) — the part-1 I/O:
   - delete `SessionDoc` revoked or expired beyond the `Sessions` cutoff;
   - delete consumed or expired `IdentityToken` / `EscalationToken` beyond the
     `Tokens` cutoff;
   - delete `EmailDeadLetter` beyond the `DeadLetters` cutoff;
   - for `OutboxEmail`, **null `Body`** after the `OutboxBodyNullDays` cutoff and
     **delete the row** after the `OutboxDays` cutoff.
   Every cutoff comes from `RetentionPolicy.CutoffFor(options, key, now)`; a
   `null` cutoff (key `0`/absent) skips that type. Returns the affected counts
   only. No PII in the return, in logs, or in the query parameters.

3. **`tests/Kumunita.Core.Tests/RetentionServiceTests.cs`** (new) — the four
   tests below (store harness per the precedent).

   **Optional 4th file:** the DI registration line in the `Privacy` module's
   `DependencyInjection.cs`, if one exists (the plan's "a 4th file"). If there
   is no such module file, **do not create one** — record in the handoff that
   the DI registration is deferred to IS34b.

## Entry reads (only these — no whole-file reads)

- `cat src/Kumunita.Core/Media/MediaLifecycleTick.cs` (the record shape —
  ~15 lines).
- `sed -n '1,60p' src/Kumunita.Web/SideEffects/MediaLifecycleHandler.cs` (the
  handler shape: Core does the work, Web hosts the handler — **awareness only**,
  you do not write the handler).
- `sed -n '40,90p' src/Kumunita.Core/Usage/UsagePurgeService.cs` (the
  delete-by-cutoff I/O idiom + the "cutoff value comes from the pure policy"
  split).
- `sed -n '1,60p' src/Kumunita.Core/Sessions/SessionDoc.cs` (field names —
  which field is the revoke/expiry time).
- `sed -n '1,80p' src/Kumunita.Core/Identity/EmailMessages.cs` (the
  `OutboxEmail` / `EmailDeadLetter` field names — `Body`, the delivery time,
  the recipient field).
- `grep -n "class IdentityToken\|class EscalationToken" -A25 -r src/Kumunita.Core`
  (the consumed/expired fields on the two token types).
- `sed -n '1,60p' src/Kumunita.Core/Privacy/RetentionOptions.cs` +
  `sed -n '1,80p' src/Kumunita.Core/Privacy/RetentionPolicy.cs` (IS33's seams —
  the `CutoffFor` / `DaysFor` / `Keep` signatures you consume; do not change).
- One store-harness service test for the idiom (e.g.
  `sed -n '1,60p' tests/Kumunita.Core.Tests/UsagePurgeServiceTests.cs` if
  present, else the closest `PostgresFixture` service test).
- Doc rules + the per-unit contract (D8): `docs/plans-milestones/
  plan-improve-sec.md` (the **IS34a** section + the **D6** decision row) +
  `docs/DOCS-CONVENTIONS.md`.
- Test-runner form: `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 —
  xunit.v3 in-process `-class`, **not** `dotnet test --filter`).

## Tests (4 new, in `RetentionServiceTests`)

- `Retention_DeletesOldRevokedSessions_KeepsLive` — a `SessionDoc` revoked or
  expired before the `Sessions` cutoff is deleted; a live one is kept.
- `Retention_NullsOutboxBodyAfter7d_DeletesAfter90d` — an `OutboxEmail` past the
  `OutboxBodyNullDays` cutoff has `Body` nulled (row kept until `OutboxDays`);
  one past the `OutboxDays` cutoff is deleted.
- `Retention_DeletesConsumedTokens` — consumed / expired `IdentityToken` /
  `EscalationToken` beyond the `Tokens` cutoff are deleted; live ones kept.
- `Retention_IsIdempotent` — a second run deletes nothing new (counts 0); a
  key set to `0` (keep) skips its type entirely.

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `RetentionServiceTests` green (4/4) via the xunit.v3 `-class` runner form.
- [ ] IS33's `RetentionPolicyTests` **still** green (you read them, you did not
      change them) — run both classes in one command.
- [ ] The service calls `RetentionPolicy.CutoffFor` for every cutoff; **no**
      inline `now.AddDays(-days)` and **no** direct `options.<Key>` read for the
      cutoff math (grep the service for `AddDays` → expect none).
- [ ] **No** `Program.cs` touch. **No** handler. DI registration in the module
      file only if it exists; otherwise deferred to IS34b (recorded).
- [ ] The service returns **counts only**; no PII in the return, logs, or query
      params.
- [ ] `PortabilityDocTypes` (M11) **untouched** (C-M11·5 closed-set pin).
- [ ] `RetentionTick` is a `sealed record : Wolverine.TimeoutMessage(1-day)`.
- [ ] No PII in code, tests or notes.
- [ ] One `## IS34a` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files, the `RetentionService` public surface, the four tests + pass
      count, the DI-registration state, next-unit notes).

## Definition of done (IS34a)

- [ ] `RetentionTick` (the 1-day `TimeoutMessage` record) + `RetentionService`
      (part 1: sessions, tokens, dead letters, outbox null+delete) are in
      `Kumunita.Core.Privacy`; the service returns counts only, no PII.
- [ ] Every cutoff comes from IS33's `RetentionPolicy.CutoffFor`; `0` = keep
      skips the type; the service never re-derives the cutoff.
- [ ] `RetentionServiceTests` green (4/4) via the xunit.v3 runner; IS33's
      `RetentionPolicyTests` still green.
- [ ] No handler, no `Program.cs`, no DI beyond the existing module file (or
      deferred to IS34b); `PortabilityDocTypes` unchanged; build green; no PII.
- [ ] One commit, one handoff section.
- [ ] **Next unit: IS34b** (`RetentionTickHandler` + the `Program.cs` seed —
      the Web-side Wolverine handler that calls this unit's `RetentionService`
      and re-yields `RetentionTick`).
