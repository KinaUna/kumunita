---
id: is27b-prompt
title: "IS27b prompt — point the round-trip tests at the Full scope"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS27b — point the round-trip tests at the `Full` scope

## What you are doing

You are the **twenty-seventh working agent (b)** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS27 are committed.

This is the **tests-only follow-up** IS27 deliberately deferred (plan row IS27,
"Known touches"). IS27 added the scope seam and made the **old**
`ExportAsync(actor, ct)` delegate to `PortabilityExportScope.ContentFree`.
That silently changed the default the `PortabilityRoundTripTests` class runs
on — so the tests that assert **private rows survive** now see them stripped
and go red. This unit fixes the tests: the round-trip class's whole purpose
is the **full** archive (the M11 D9a–D9c invariants — content graph + media +
roles preserved), so its call sites must say `Full` **explicitly**.

**Tests only, one file, one commit.** You do **not** touch product code, the
`ContentFreeFilter`, the `IPortabilityService` seam, or `Program.cs`. You do
**not** change what any test asserts — only *which scope* each export call uses.

## The one thing to get right

**`Full` is byte-identical to the pre-IS27 export** (all 44 types, no fields
blanked, no rows filtered). So the fix is purely additive at the call site:
`ExportAsync(actor, ct)` → `ExportAsync(actor, PortabilityExportScope.Full, ct)`.
Do **not** rewrite the assertions, re-plant data, or loosen any
`Assert.Equal`/`Assert.NotNull` to make the red tests pass — if a test still
fails after the scope fix, that is a **drift pause** (read the failure, record
it, stop), not a reason to edit the assertion (SEC rules 1–9).

## Deliverable (1 file, this commit)

- **`tests/Kumunita.Core.Tests/PortabilityRoundTripTests.cs`** — change every
  call site of the **two-arg** `ExportAsync(actor, ct)` in this class to pass
  `PortabilityExportScope.Full` explicitly. The class is the D9a–D9c full
  round-trip, so all of its exports are `Full` by intent. Add
  `using Kumunita.Core.Portability;` if it is not already imported (it is).
  Leave the three-arg `ImportAsync(...)` calls and the
  `PortabilityExportScopeTests` (a different class) untouched.

## Entry reads (only these — no whole-file reads)

- `grep -n "ExportAsync(" tests/Kumunita.Core.Tests/PortabilityRoundTripTests.cs`
  (the exact call sites; count them — the handoff notes said ~6, trust this grep).
- For each call site, the enclosing `[Fact]` method only (`grep -n` the method
  name, then ~40 lines) — confirm its intent is a **full** round-trip (it is,
  by the D9 invariants) before flipping the scope.
- `src/Kumunita.Core/Portability/PortabilityService.cs` (the overload
  signatures — `ExportAsync(string, CancellationToken)` and
  `ExportAsync(string, PortabilityExportScope, CancellationToken)`).
- `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
  **IS27** section (what IS27 changed + the "expected red" note).
- The SEC protocol: `docs/plans-milestones/plan-improve-sec.md` per-unit
  contract (D8) — no PII, pause on drift, **never edit around a failing test**.
- Test-runner form (xunit.v3 in-process `-class`, **not**
  `dotnet test --filter`): `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3).

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `PortabilityRoundTripTests` green (the full class, 13/13) via the xunit.v3
      runner form — the 6 reds IS27 flagged are now green.
- [ ] `PortabilityExportScopeTests` **still** green (4/4) — unchanged, but run
      it to prove no regression.
- [ ] `AdminPortabilityControllerTests` **still** green (it mocks the two-arg
      seam and already passes — do **not** change it).
- [ ] No PII in code, tests or notes (`user-a`, `a@example.test` style only).
- [ ] One `## IS27b` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (which call sites changed, tests + pass count, next-unit notes — 3–8 lines
      per the per-unit contract).
- [ ] One commit.

## Definition of done (IS27b)

- [ ] Every `ExportAsync(actor, ct)` call site in `PortabilityRoundTripTests`
      passes `PortabilityExportScope.Full` explicitly.
- [ ] `PortabilityRoundTripTests` 13/13 green; `PortabilityExportScopeTests`
      4/4 green; `AdminPortabilityControllerTests` green; no assertions edited.
- [ ] One commit, one handoff section, no PII, no product-code change.
- [ ] **Next unit: IS28** — Controller: scope, override gate, audit row
      (`GET export?scope=full` returns `Forbid` unless
      `BreakGlassProbe.HasActiveAsync`; the audit row records
      `Action = portability.export.{content-free|full}` +
      `Via = Admin|BreakGlass`; two view buttons; tests
      `Export_Default_IsContentFree`, `FullExport_WithoutOverride_Forbidden`,
      `FullExport_WithOverride_AuditedBreakGlass`).
