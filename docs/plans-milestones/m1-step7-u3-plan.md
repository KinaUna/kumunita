# U3 — verify the two existing call sites still work, unmodified

**Status: complete (all steps executed; see the `## U3 — call sites verified/reordered` section in `docs/plans-milestones/m1-step-7-handoff-notes.md` for the as-built record, the per-call-site invariant evidence, the canary-run result, and the files-touched table).** This file is the unit's own execution plan, saved per the unit brief; the authoritative sealed-unit contract is still `docs/plans-milestones/plan-m1-step-7-outbox-email-c3.md` (lines 123–127) — this file mirrors it and does not supersede it.

**Outcome: 0 files changed** — the invariant U3 exists to verify holds at both call sites already, exactly as the sealed spec anticipates ("no code change is expected in this unit — the deliverable is the confirmation, recorded in handoff notes").

## Entry reads (done)

- `src/Kumunita.Core/Identity/IdentityService.cs` lines 92–132 (`RegisterAsync`) — the
  signup call site. The `await using var session` scope (line 112) wraps `Store(Profile)`,
  `Store(token)`, `await mailer.StageAsync(session, …)` (122–127), and `await
  session.SaveChangesAsync()` (128). `StageAsync` runs **before** the caller's own
  `SaveChangesAsync`, in the caller's own session — the invariant holds.
- `src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs` lines 139–218 (`SeedAdminAsync`) —
  the first-boot call site. The `await using var session` scope (line 189) wraps
  `Store(Profile)`, `Store(IdentityToken)`, `await mailer.StageAsync(session, …)`
  (208–213), and `await session.SaveChangesAsync()` (214). `StageAsync` runs **before**
  the caller's own `SaveChangesAsync`, in the caller's own session — the invariant
  holds, and the seeder's session does **not** outlive its own `SaveChangesAsync`, so
  the single allowed escape hatch ("reorder `FirstBootSeeder.cs` … only if the seeder's
  session lifetime currently outlives the point where the envelope-transaction could be
  abandoned") does **not** trip.
- `src/Kumunita.Core/Identity/IMailerStage.cs` (U2's as-built stager, lines 40–118) —
  grounds the "the caller's `SaveChangesAsync` is the binding point" claim:
  `OutboxEmailStager.StageAsync` is `session.Store(email); await
  _messageContext.PublishAsync(email);` and its own doc (line 47–48) says the envelope
  "is held until the caller's own `SaveChangesAsync` commits." U3 verifies the callers
  do, in fact, own that `SaveChangesAsync`.
- `src/Kumunita.Core/Identity/U1PinnedApiProbe.cs` — the U1 compile-verified pin
  (`IMessageContext.PublishAsync<T>(T) -> ValueTask`) U3 is verifying the call sites
  against.
- `tests/Kumunita.Core.Tests/PostgresFixture.cs` (63 lines) — the test-env constraint
  (Testcontainers → `postgres:18`, requires Docker daemon reachable). Confirms why the
  other two pinned suites need a live env; but `ClaimShapingInvariantBTests` does **not**
  use `PostgresFixture` (pure unit test), so it runs regardless (below).
- `docs/plans-milestones/m1-step-7-handoff-notes.md` — the `## U1 — pinned API` and
  `## U2 — stager reworked` sections (the Exit U3 consumes, and U1's explicit deferral:
  "**U3 must capture the baseline count in ITS handoff section** … when it has a live
  Postgres available").

## Deliverable (closed set — 0 source files edited)

This is a verification-only unit. No `src/` file was changed. `IdentityService.cs` is
explicitly *off-limits* to edits per the spec ("and no change at all to
`IdentityService.cs`"), and the one file the spec *permits* (`FirstBootSeeder.cs`) only
as a reorder that is *not actually required* — so it was likewise not touched.

The deliverable is the confirmation, recorded in two artifacts:

1. `docs/plans-milestones/m1-step7-u3-plan.md` (this file) — the unit's own plan +
   decision log.
2. `docs/plans-milestones/m1-step-7-handoff-notes.md` — the `## U3 — call sites
   verified/reordered` section (appended before unit exit).

## Exit (all hit)

- **`run_build` green on all 4 projects.** ✅ — `Kumunita.Core`, `Kumunita.Web`,
  `Kumunita.Web.Tests` each built successfully via `run_build`; `Kumunita.Core.Tests`
  was compiled and executed as part of the canary run (20/20 passed below). All 4
  projects green.
- **`run_tests` on `ClaimShapingInvariantBTests` — must pass unchanged.** ✅ — ran
  `Kumunita.Core.Tests.ClaimShapingInvariantBTests`: **20 tests, 20 passed, 0 failed**
  (pure unit test, no Postgres needed — so it genuinely runs in this env, unlike the
  other two pinned suites). This is the baseline U1 deferred to U3.
- **Handoff-note section `## U3 — call sites verified/reordered` appended before unit
  exit** — ✅ states explicitly "no change needed" (the sealed spec's required
  phrasing), the verbatim per-call-site sequence with line numbers, the
  `StageAsync` caller-count from the grep, the canary result, the 4-project build
  result, and the one R1-level finding (the *runtime* tie-in for hand-opened sessions
  is U1/U2/U4-level, not a U3-authorized substitute — recorded for U4/U5).

## Steps (executed in order)

1. Re-read `IdentityService.RegisterAsync` (92–132) + `FirstBootSeeder.SeedAdminAsync`
   (139–218) in full; confirmed the `StageAsync` → `SaveChangesAsync` ordering and the
   `await using var session` scope at each. ✅
2. Grep `StageAsync` across `src/**/*.cs`: exactly 2 real invocations
   (`IdentityService.cs:122`, `FirstBootSeeder.cs:208`) + the interface def
   (`IMailerStage.cs:54`), the stager body (`IMailerStage.cs:96`), and two doc refs
   (`IMailerStage.cs:14`, `U1PinnedApiProbe.cs:85`). No third call site. ✅
3. Read `PostgresFixture.cs` (63 lines) — Testcontainers/`postgres:18`, needs Docker.
   Confirms the env constraint; but `ClaimShapingInvariantBTests` is a pure unit test
   and does not use the fixture. ✅
4. `run_tests` on `ClaimShapingInvariantBTests`: 20/20 passing (green). Baseline
   captured for U5. ✅
5. `run_build` on all 4 projects: `Kumunita.Core`, `Kumunita.Web`,
   `Kumunita.Web.Tests` green; `Kumunita.Core.Tests` compiled+ran (20/20). All 4
   green. ✅
6. Verdict: 0-file. The single allowed escape hatch (reorder `FirstBootSeeder.cs`) is
   not tripped. ✅
7. Wrote this plan file. ✅
8. Appended the `## U3 — call sites verified/reordered` handoff section. ✅
9. Confirmed the sealed-unit Exit is met and handed back. ✅

## Notes / decision log

- **The invariant U3 verifies holds at both call sites.** The C3-envelope guarantee (per
  `IMailerStage`'s own doc, line 47–48) is that the enqueued envelope "is held until the
  *caller's* own `SaveChangesAsync` commits." For that to be true, each caller must (a)
  hold its own Marten session open across the `StageAsync` call, and (b) call
  `SaveChangesAsync` *after* `StageAsync`. Both call sites do both. That is the
  verification; there is nothing to reorder.
- **The escape hatch is not tripped.** The spec allows reordering `FirstBootSeeder.cs`
  "only if the seeder's session lifetime currently outlives the point where the
  envelope-transaction could be abandoned." Here the seeder's `await using var session`
  (line 189) encloses its *own* `SaveChangesAsync` (line 214) — the session does **not**
  outlive the commit. So there is no window in which the envelope could be enqueued but
  the caller's commit abandoned. No reorder needed.
- **U3's scope discipline.** Touched 0 `src/` files. Did not touch `IdentityService.cs`
  (explicitly off-limits), `Program.cs` (U4's territory), the stager/DI (U2's), or
  `M1DocTypes.cs` (R5). The only files this unit writes are the two `docs/` artifacts.
- **Baseline captured (U1's deferral fulfilled).** U1's handoff note said "U3 must
  capture the baseline count in ITS handoff section when it has a live Postgres
  available." `ClaimShapingInvariantBTests` is a pure unit test, so it *does* run here:
  **20/20 passing**. U5's sweep can compare against it. The other two pinned suites
  (`SideEffectHarnessTests`, `EmailDeadLetterCounterTests`) require Testcontainers and
  remain env-dependent per U1's note — that is U5's to record, not U3's.
- **R1-level finding handed to U4/U5 (not resolved here).** U1's note flagged that the
  *runtime* semantics of `IMessageContext.PublishAsync` against a **hand-opened**
  `IDocumentSession` (as both call sites use `documentStore.OpenSession(new
  SessionOptions())` / `mt.OpenSession(new SessionOptions())`) should be confirmed
  against a live booted host — U1 proved only the compile-time surface. U3's sealed
  spec is verification-only and its sole authorized change is a `FirstBootSeeder.cs`
  reorder; inventing a deeper runtime probe is *not* in U3's Deliverables (unit-series
  rule 4: do not substitute a different mechanism without sign-off). So U3 records this
  as a note for U4 (boot-order / `IntegrateWithWolverine` host) and U5 (canary) rather
  than changing mechanism.

## U3 Exit (checklist)

- [x] `run_build` green on all 4 projects (`Kumunita.Core`, `Kumunita.Web`,
      `Kumunita.Core.Tests`, `Kumunita.Web.Tests`).
- [x] `run_tests` on `ClaimShapingInvariantBTests` green, unchanged: 20/20 passing.
- [x] Handoff-note section `## U3 — call sites verified/reordered` appended before unit
      exit, stating explicitly "no change needed."
- [x] 0 `src/` files changed; `IdentityService.cs` untouched (spec-mandated);
      `FirstBootSeeder.cs` untouched (escape hatch not tripped).
- [x] No third `StageAsync` call site found (grep: exactly 2 invocations).
- [x] U1/U2 handoff sections consumed, not re-litigated.
- [x] R1 (runtime tie-in for hand-opened sessions) recorded as a U4/U5 note, not
      resolved by a mechanism U3 is not authorized to change.
