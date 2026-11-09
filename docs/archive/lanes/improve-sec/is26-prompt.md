---
id: is26-prompt
title: "IS26 prompt — BreakGlassProbe (extract, don't widen the frozen seam)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS26 — `BreakGlassProbe` (extract, don't widen the frozen seam)

## What you are doing

You are the **twenty-sixth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS25 are committed.

This unit extracts the break-glass check out of `AuthorizationService` into
a small public static seam: **`BreakGlassProbe`**. ADR 0299 (IS25) is the
spec: the full instance export (IS28) must gate on an **active
`AdminOverride`** without touching the frozen `IAuthorizationService`
signature (ADR 0006) — so the check becomes a standalone static probe the
controller can call with the session it already has.

**Extract, don't widen:** move the *exact* SQL/behavior from the private
`HasBreakGlassAsync` method into `BreakGlassProbe.HasActiveAsync`, and make
the private method **delegate** to it. No behavior change, no new
`AccessVia`, no new `Decide()` branch. `IDocumentSession` already is an
`IQuerySession`, so existing call sites compile unchanged.

## The one thing to get right

**Do not change `IAuthorizationService`.** The seam stays private-method →
delegate. The public surface added is exactly one:
`public static Task<bool> BreakGlassProbe.HasActiveAsync(IQuerySession session, string userId, CancellationToken ct = default)`.
Anything wider (a new interface method, a new service, a new `AccessAction`)
is a drift from the ADR 0006 frozen signatures and must be paused and
recorded, not fixed in.

## Deliverables (≤ 3 files)

1. **`src/Kumunita.Core/Authorization/BreakGlassProbe.cs`** (new) —
   `public static async Task<bool> HasActiveAsync(IQuerySession session,
   string userId, …)` containing the exact query moved from the private
   `AuthorizationService.HasBreakGlassAsync` (active `AdminOverride` row
   for that user, not expired, not consumed — whatever the private method
   checks today; move it verbatim, do not re-derive).
2. **`src/Kumunita.Core/Authorization/AuthorizationService.cs`** — the
   private method body becomes a delegate call to `BreakGlassProbe`. No
   other change in the file.
3. **`tests/Kumunita.Core.Tests/BreakGlassProbeTests.cs`** (new) — the
   three tests below, reusing the existing break-glass test setup (find it
   with the entry read; the Marten boot for `AdminOverride` already exists
   there).

## Entry reads (only these — no whole-file reads)

- `grep -n "HasBreakGlassAsync" -A30 src/Kumunita.Core/Authorization/AuthorizationService.cs`
  (the private method, its SQL, its callers).
- The existing break-glass tests: `grep -ln "BreakGlass" tests/Kumunita.Core.Tests/*.cs`,
  then read the matching test's setup (≤ 60 lines) to copy its boot shape.
- `docs/adr/0299-instance-export-operator-gated.md` (the Constraints section
  names this seam; the gate IS28 will call it).
- `docs/plans-milestones/plan-improve-sec.md` — the IS26 row only.
- The SEC protocol: `docs/plans-milestones/plan-improve-sec.md` per-unit
  contract (D8) — read only the entry reads, edit only the deliverables,
  no PII, pause on drift, never edit around a failing test.
- Test-runner form (xunit.v3 in-process `-class`, **not**
  `dotnet test --filter`): `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3).

## Tests (3 new)

- `Probe_ActiveOverride_True` — a non-expired, unconsumed `AdminOverride`
  for the user → `true`.
- `Probe_Expired_False` — an `expiresAt` in the past → `false`.
- `Probe_NotConsumed_False` — a consumed `AdminOverride` → `false` (match
  the private method's exact semantics; if it has no "consumed" concept,
  name the test after what it actually checks and record the mapping in the
  handoff).

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `BreakGlassProbeTests` green (3/3) via the xunit.v3 runner form.
- [ ] The pre-existing `BreakGlass_*` tests still green (no behavior
      change — the delegate must be verbatim).
- [ ] `IAuthorizationService` untouched
      (`git diff` shows no interface change).
- [ ] No PII in code, tests or notes (`user-a`, `a@example.test` style only).
- [ ] One `## IS26` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`
      (files, the public name, tests + pass count, next-unit notes, anything
      noticed — 3–8 lines per the per-unit contract).
- [ ] One commit.

## Definition of done (IS26)

- [ ] `BreakGlassProbe.HasActiveAsync` public static, exact SQL moved,
      private method delegates.
- [ ] `IAuthorizationService` unchanged (ADR 0006).
- [ ] 3 new tests green + existing break-glass tests green.
- [ ] One commit, one handoff section, no PII.
- [ ] **Next unit: IS27** (`PortabilityExportScope` + the content-free
      filter — `PortabilityExportScope.cs` new; the export loop skips
      §2.6-marked types driven by `DataClassCatalog`; the
      `IPortabilityService` overload where the old `ExportAsync(actor, ct)`
      delegates to `ContentFree`; tests `ContentFree_HasNoConversationsOrMessages`,
      `ContentFree_HasNoRestrictedPosts_HasPublicOnes`,
      `ContentFree_BlanksProfileContactFields`, `Full_RoundTripsAsBefore`).
