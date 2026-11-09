---
id: is34b-prompt
title: "IS34b prompt — RetentionTickHandler + the Program.cs wiring (E5, D6)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS34b — `RetentionTickHandler` + the `Program.cs` seed (the Web-side recurring leg)

## What you are doing

You are the **thirty-fifth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS34a are committed
(IS34a = the `RetentionTick` record + the Core `RetentionService`, part-1 I/O —
see the handoff note's `## IS34a` section for the exact
`RetentionService.RunAsync` signature and the `RetentionSweepResult` shape).

**Finding E5 (severity M), the recurring leg:** IS34a built the *work* (the
`Kumunita.Core.Privacy.RetentionService` that deletes/nulls the part-1 types)
and the *message* (the 1-day `RetentionTick` record). Nothing fires them yet.
This unit adds the **Wolverine handler** that injects a live store + options
into `RetentionService` and re-yields the tick, plus the **`Program.cs` wiring**
that (a) binds `RetentionOptions` from config and (b) seeds the first tick so
the recurring schedule actually starts. Part 2 (notifications, error reports,
log files) is **IS35**. The spec is the plan's **IS34b** section
(`docs/plans-milestones/plan-improve-sec.md`).

## The one thing to get right

**The handler is a thin adapter — the work stays in `RetentionService`.**
`RetentionService` is a **`static`** class (the `UsagePurgeService` /
`MediaLifecycleService` shape), so there is **no DI registration to add** and
**no `DependencyInjection.cs` to create**. What IS34a's handoff called "the
deferred DI" is really just the **options binding** in `Program.cs`. Mirror
`MediaLifecycleHandler` / `AuditPurgeHandler` exactly:

- `public static async Task<IEnumerable<object>> Handle(RetentionTick tick,
  IDocumentStore store, IOptions<RetentionOptions> options)` — call
  `RetentionService.RunAsync(store, options.Value, DateTimeOffset.UtcNow)`,
  **log the counts (never the rows)** — the IS08 `LogTemplatePiiGateTests`
  apply to anything you log here — and `return new[] { new RetentionTick() }`
  (the tick's 1-day delay is baked into the type; no per-callsite `DelayedFor`).

**`Program.cs` gets exactly two retention touches** (this is the lane's last
`Program.cs` touch after IS05's Sentry block):

1. The **options binding**, next to the existing `Configure<MediaOptions>`
   line (`Program.cs` ≈ :92): `builder.Services.Configure<RetentionOptions>(
   builder.Configuration.GetSection(RetentionOptions.SectionName));` —
   `SectionName` is `"Retention"`, so keys bind as `Retention__*` (IS33's
   `RetentionOptions`).
2. The **first-boot seed**, right next to the existing
   `await bus.PublishAsync(new MediaLifecycleTick());` line (`Program.cs`
   ≈ :1342): `await bus.PublishAsync(new RetentionTick());` — with the same
   "without this line the schedule never fires" comment shape as the
   neighbours (F15 / M33 / M13 seed precedent).

## Constraints that keep existing tests green

- **`RetentionService` + `RetentionTick` are IS34a's and frozen** — read
  `RunAsync` / `RetentionSweepResult` to call them; do not change them.
- **Do not touch IS34a's two files** (`RetentionService.cs`, `RetentionTick.cs`)
  or `RetentionPolicyTests` / `RetentionServiceTests` (IS33 / IS34a).
- **`PortabilityDocTypes` (M11) is untouched** (C-M11·5 closed-set pin) — this
  unit fires a job; it adds no doc type and no doc-type surface.
- **PII rule:** the handler logs counts only; the test uses synthetic fixture
  ids (`user-a`) / `a@example.test`-style addresses only; no real PII anywhere.
- The `Program.cs` changes are the **options-binding line + the one seed line**
  only — nothing else in `Program.cs`.

## Deliverables (≤ 4 files)

1. **`src/Kumunita.Web/SideEffects/RetentionTickHandler.cs`** (new) — the
   `static` handler above (`using Microsoft.Extensions.Options;` for
   `IOptions<>`), mirroring `MediaLifecycleHandler` / `AuditPurgeHandler`.
2. **`src/Kumunita.Web/Program.cs`** — the two retention touches above
   (options binding near the `Configure<MediaOptions>` line; the seed near the
   `MediaLifecycleTick` seed).
3. **`tests/Kumunita.Web.Tests/RetentionTickHandlerTests.cs`** (new) — the test
   below (the Web.Tests `PostgresFixture` idiom, the `UsagePurgeServiceTests`
   boot shape).
4. **Optional 4th:** the handoff-note section (append `## IS34b`) — always done
   per the per-unit contract (D8).

## Entry reads (only these — no whole-file reads)

- `cat src/Kumunita.Web/SideEffects/MediaLifecycleHandler.cs` (the handler
  shape — the closest analog; ~43 lines).
- `sed -n '1,60p' src/Kumunita.Web/SideEffects/AuditPurgeHandler.cs` (the
  `IOptions<…>`-driven handler precedent — options param + the reschedule
  return).
- `grep -n "Configure<MediaOptions>\|SectionName" src/Kumunita.Web/Program.cs`
  (the options-binding precedent, ≈ :92) and
  `grep -n "MediaLifecycleTick" src/Kumunita.Web/Program.cs` (the seed line,
  ≈ :1342, ±12 for the comment shape).
- `sed -n '1,60p' src/Kumunita.Core/Privacy/RetentionService.cs` (the
  `RunAsync` + `RetentionSweepResult` surface you call — IS34a, frozen).
- `sed -n '1,40p' src/Kumunita.Core/Privacy/RetentionTick.cs` (the record —
  IS34a, frozen).
- `sed -n '1,60p' tests/Kumunita.Web.Tests/AdminPortabilityScopeTests.cs` (a
  Web.Tests test that boots a scratch `DocumentStore` over the
  `PostgresFixture` — the boot idiom to mirror in the handler test).
- `sed -n '40,70p' tests/Kumunita.Web.Tests/PostgresFixture.cs` (the
  `NewDatabaseAsync` seam — same shape as the Core.Tests fixture).
- Doc rules + the per-unit contract (D8): `docs/plans-milestones/
  plan-improve-sec.md` (the **IS34b** section + the **D6** decision row) +
  `docs/DOCS-CONVENTIONS.md`.
- Test-runner form: `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 —
  xunit.v3 in-process `-class`, **not** `dotnet test --filter`).

## Tests (in `RetentionTickHandlerTests`)

- `Handler_CallsService_AndReschedules` — drive `RetentionTickHandler.Handle`
  with a live store (a scratch `DocumentStore`, IS34a's boot shape) + a
  `RetentionOptions` (or `IOptions<>` wrapper); seed one old part-1 row
  (e.g. a revoked `SessionDoc`); assert the handler **deletes it** (the
  service ran) and **returns a fresh `RetentionTick`** (the re-schedule). A
  second drive is a no-op. Use synthetic ids only.

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `RetentionTickHandlerTests` green via the xunit.v3 `-class` runner form.
- [ ] IS34a's `RetentionServiceTests` **and** IS33's `RetentionPolicyTests`
      **still** green (you read them, did not change them) — run them in the
      same command (they live in `Kumunita.Core.Tests`).
- [ ] The handler calls `RetentionService.RunAsync` (the IS34a seam); **no**
      re-derived cutoff, **no** inline delete SQL in the handler.
- [ ] `Program.cs` touches are **only** the options-binding line + the one
      seed line (nothing else in `Program.cs`).
- [ ] No `DependencyInjection.cs` created, no `DI registration` of
      `RetentionService` (it is `static` — there is nothing to register).
- [ ] `RetentionService` / `RetentionTick` **unchanged**;
      `PortabilityDocTypes` (M11) unchanged.
- [ ] The handler logs counts only; no PII in code, tests, or notes.
- [ ] One `## IS34b` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files, the handler public surface, the `Program.cs` line count, the test
      + pass count, next-unit notes).

## Definition of done (IS34b)

- [ ] `RetentionTickHandler` (the `IOptions<RetentionOptions>`-driven `static`
      adapter) + the `Program.cs` options-binding line + the one seed line are
      in place; the recurring schedule now fires and self-reschedules.
- [ ] The handler calls IS34a's `RetentionService.RunAsync`; no re-derived
      cutoff; counts-only logging; no PII.
- [ ] `RetentionTickHandlerTests` green via the xunit.v3 runner; IS34a + IS33
      test classes still green; build green.
- [ ] No `DI`/module file created; `RetentionService`/`RetentionTick`
      unchanged; `PortabilityDocTypes` unchanged.
- [ ] One commit, one handoff section.
- [ ] **Next unit: IS35** (`RetentionService` part 2: notifications
      read-90d/unread-365d, resolved `ErrorReport` 180d, and the daily
      `RollingFileSink.Retain` — extending the same handler/service this unit
      wired).
