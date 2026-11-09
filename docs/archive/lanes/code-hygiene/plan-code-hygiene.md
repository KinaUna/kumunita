---
id: plan-code-hygiene
title: "Plan: CODE-HYGIENE — build warnings, ratchet drift, route conflict"
tier: archive
kind: plan
status: archived
audiences: [developer, agent]
lane: code-hygiene
related:
  - docs/dev/testing-and-ci.md
  - docs/philosophy/in-code.md
  - tools/code/code-baseline.json
  - docs/archive/lanes/improve-sec/plan-improve-sec.md
updated: 2026-11-09


> **Abstract:** the CODE-HYGIENE lane resolves every open build warning
> (165), every ratchet-gate violation (51), and one route-conflict bug that
> shadows a real action. The 3 known-red tests in `Kumunita.Web.Tests`
> are the lane's *starting* state: one (`CodeCheck_Gate_Passes`) auto-clears
> when the ratchet is fixed; the other two (the M2 agreement reds) are
> **successor-scope** and are documented, not fixed. Four methods over their
> line/CC ceilings get split; the remaining violations are re-baselined
> with ADR annotations (the `improve-sec` precedent). 14 sealed units,
> sized for a 32K-token agent.
> **Who is it for:** whoever picks up a `CH`-unit, and the reviewer of the
> close. **When to read the rest:** before starting any `CH`-unit; units
> name their own entry reads.
>
> **First agent?** Start from the clean-slate brief
> [`in-progress/u00-clean-slate-brief.md`](in-progress/u00-clean-slate-brief.md)
> — it bootstraps a zero-context agent and hands it CH01.

# Plan: CODE-HYGIENE — build warnings, ratchet drift, route conflict

Measured on `development` @ **`f8423743`** (2026-11-09) with a full
`dotnet build --no-incremental`, `code-check.ps1`, and both test suites.
The starting state: **0 errors, 165 warnings, 51 ratchet violations,
3 known-red tests, 1 route-conflict bug**.

## Why now

The `improve-sec` lane (IS00–IS45) shipped on 2026-11-09 and left the
codebase in a **known-red** state: the ratchet gate is red, 165 build
warnings are visible, and one route is shadowed. None of these are
regressions — they are the *expected* drift after a lane that added
cross-context reads, new eraser methods, and new retention lambdas.
But the drift is now **blocking CI**: `code-check.ps1` exits 1, which
makes `CodeCheck_Gate_Passes` fail, which makes the CI red.

This lane does not introduce new features. It does not re-open any
`improve-sec` decision. It **clears the drift** so the next lane starts
from a green baseline.

## What this lane is (and is not)

- **Is:** fixing the 1 route-conflict bug; splitting 4 methods over their
  ceilings; re-baselining the legitimate boundary violations with ADR
  annotations; fixing 165 build warnings (155 nullability, 10 mechanical);
  adding 3 controllers to the Marten allowlist; re-baselining the
  `SampleDataSeeder.cs` 1-line drift and 2 stale method baselines.
- **Is not:** fixing the 2 M2 agreement reds (`EveryResidentExportType` /
  `EveryDeleteOrPseudonymizeType`) — those are **successor-scope** (the
  `improve-sec` plan names IS21–IS23 / IS31–IS32 as their owners). This
  lane documents them in the handoff note and does not touch them.
- **Is not:** re-opening any `improve-sec` decision (D1–D10). The
  boundary violations this lane re-baselines are the *consequences* of
  those decisions, not the decisions themselves.

## Decisions

| # | Decision | Chosen | Alternatives rejected |
|---|---|---|---|
| D1 | **Route conflict** — `Dissolve` in `GuardianController.cs:429` has the route `{childId}/eventrsvp/{eventId}/approve`, which is the same route as `ApproveEventRsvp` in `GuardianController.RsvpCurate.cs:38`. A POST to that URL is ambiguous — half the time it routes to `Dissolve` (which dissolves the guardian link) and half the time to `ApproveEventRsvp`. | **Fix `Dissolve`'s route** to `{childId}/dissolve`. The `Dissolve` action takes only `childId` (no `eventId`), so the `{eventId}` segment is wrong. The `RsvpCurate` partial has the correct `ApproveEventRsvp`. Update the test that pins the old route. | Suppress ASP0023 (hides a real ambiguity); rename `ApproveEventRsvp` (breaks the ADR 0028 lane's test names) |
| D2 | **4 methods over ceiling** — `ResidentAccountRowsEraser.EraseAsync` (142 lines, cc 21), `RetentionService.RunAsync` (118 lines, cc 38), `ImportOwnershipGuard.Check` (cc 32), `UserPortabilityService.ScopeRowHoldsSeat` (cc 33). All are over the 80-line / 15-cc ceiling. | **Split each method** into a ≤80-line public method + extracted private helpers. The public method keeps its name and signature (callers don't change); the helpers carry the per-type / per-scope logic. One unit per method. | Re-baseline the ceiling (the ratchet doctrine says the baseline only goes down; raising a number needs an ADR reason, and these are new methods, not grandfathered ones) |
| D3 | **155 nullability warnings** — CS8602/CS8767/CS8622/CS8625/CS8620/CS8604/CS8619/CS8601/CS8600. The bulk (52) are `IAuthenticationService` stubs in 2 test files where `scheme` and `properties` lack `?`. The rest are scattered across ~12 src files and ~8 test files. | **Fix in 4 batches** of ≤3 files each. Batch 1: the 2 stub files. Batch 2–4: src files grouped by context (Groups/Event/Boards, Posts/Portability/Todos, Projects/remaining). The fix is mechanical: add `?` to nullable params, add `!` to non-null assertions, add `null!` to required args. | Suppress with `#nullable disable` (hides real null bugs); fix all 155 in one unit (too large for 32K) |
| D4 | **10 mechanical warnings** — CS4014×4 (unawaited calls in tests), CS0105×1 (duplicate using), CS0219×1 (dead variable), CS0108×2 (hiding inherited member), CS8321×2 (unused local functions in a view), CS9113×2 (unread params in src). | **Fix in 2 units**: U3 (test files — CS4014/CS0105/CS0219/CS0108), U4 (src — CS8321/CS9113). | Fix all 10 in one unit (the files are in different projects) |
| D5 | **51 ratchet violations** — 1 file grew (SampleDataSeeder 2060→2061), 2 stale method baselines, 4 new methods over ceiling (D2), 3 boundary edges grew, ~38 new boundary violations, 3 controllers missing Marten allowlist entries. | **Re-baseline** the legitimate ones (all boundary violations are the consequence of `improve-sec` D2/D5/D6 — account deletion, retention, portability, logging all cross contexts by design). Run `CodeMetrics --tighten` to update the baseline, then annotate ADR 0298/0299 with the new boundary edges. Add the 3 controllers to the Marten allowlist. The 4 method splits (D2) come *before* the re-baseline so the tightened baseline reflects the split methods. | Refactor the boundary violations (they are legitimate — the contexts *must* cross for GDPR/retention); delete the gate (the ratchet is the point) |
| D6 | **3 known-red tests** — `EveryResidentExportType_IsInUserScopeInventory`, `EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`, `CodeCheck_Gate_Passes`. | `CodeCheck_Gate_Passes` **auto-clears** when the ratchet is fixed (U13). The other two are **successor-scope** — this lane documents them in the handoff note and does not fix them. | Fix all 3 (the M2 reds are a different lane's scope; fixing them here conflates two lanes' work) |

## Evidence / starting numbers (measured 2026-11-09, `development` `f8423743`)

| # | Type | Count | Where | Units |
|---|---|---|---|---|
| E1 | **Route conflict** (ASP0023) | 1 pair | `GuardianController.cs:429` (`Dissolve`) vs `GuardianController.RsvpCurate.cs:38` (`ApproveEventRsvp`) | U2 |
| E2 | **Mechanical warnings** (CS4014/0105/0219/0108/8321/9113) | 12 | 7 files (4 test, 3 src) | U3, U4 |
| E3 | **Nullability warnings** (CS8xxx) | 155 | 20 files (8 test, 12 src) | U5–U8 |
| E4 | **Methods over ceiling** | 4 | `ResidentAccountRowsEraser`, `RetentionService`, `ImportOwnershipGuard`, `UserPortabilityService` | U6–U9 |
| E5 | **Ratchet baseline drift** | 51 | `code-baseline.json` (1 file grew, 2 stale methods, 3 edges grew, ~38 new boundaries, 3 allowlist) | U13 |
| E6 | **Known-red tests** | 3 | `DataClassCoverageTests` ×2, `ImproveHarnessTests` ×1 | U14 (1 auto-clears, 2 successor) |

## Per-unit contract (32K window)

Each unit is sealed and self-contained. A fresh 32K-token agent reads the
unit prompt, the entry reads it names (3–5 files, ≤~300 lines each), makes
the edits, runs the build, and appends a handoff note *before* exiting.

**Rules (apply to every unit):**

1. A unit never modifies a file not in its own **Deliverables** list.
2. A unit never re-opens an `improve-sec` decision (D1–D10).
3. A unit never re-baselines a ceiling number upward (the ratchet doctrine:
   the baseline only goes down).
4. A unit never deletes a gate or a test to make a warning go away.
5. A unit never introduces a new public seam or a new bounded context.
6. If an entry read reveals the code has drifted from this plan, the unit
   pauses and records `## U<m> — Drift pause` in the handoff note.
7. The build must be **green** (0 errors, and the warnings this unit is
   responsible for must be gone) before the handoff note is written.
8. The handoff note is written *before* any follow-up action (commit, next
   unit, etc.).

**Test-runner form** (the xunit.v3 discovery quirk — see
[`docs/dev/testing-and-ci.md`](../dev/testing-and-ci.md)):

```bash
dotnet build Kumunita.slnx -c Debug
dotnet exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
```

Do NOT use `dotnet test` (it reports "Zero tests ran").

---

## Units (14 total)

### U1 — Design doc / triage

- **Goal:** author `docs/design/code-hygiene-design.md` — the triage table
  for every warning, every ratchet violation, and the 3 known-red tests.
  Each item gets a decision (fix / re-baseline / successor-scope) and the
  unit that owns it. This is the single source of truth U2–U14 reference.
  **No code, no build.**
- **Entry reads:** `docs/plans-milestones/plan-code-hygiene.md` (this file —
  the decisions table + evidence table), `/tmp/build-full.log` or re-run
  `dotnet build --no-incremental 2>&1 | grep -oE "warning [A-Z0-9]+:" | sort | uniq -c`
  (the warning code histogram), `/tmp/code-check.log` or re-run
  `pwsh -NoProfile -File tools/code/code-check.ps1 2>&1 | head -60` (the
  ratchet violations), `docs/dev/testing-and-ci.md` (the known-failure
  cheatsheet + the 2 by-design gates), `docs/archive/lanes/improve-sec/improve-sec-handoff-notes.md`
  (the IS43/IS44 sections — the 3 known-reds + the "successor-scope"
  annotation), `tools/code/code-baseline.json` (the `ceilings` block + the
  `files` / `methods` / `boundaries` sections that are violated).
- **Deliverables (1 file, new):** `docs/design/code-hygiene-design.md`
  (~200 lines). Sections:
  - `## Context` — the `improve-sec` lane shipped, leaving 165 warnings +
    51 ratchet violations + 1 route conflict. This lane clears the drift.
  - `## Scope` — **In:** the 1 route fix, the 4 method splits, the 165
    warning fixes, the re-baseline, the 3 allowlist entries. **Out
    (successor-scope):** the 2 M2 agreement reds (`EveryResidentExportType`,
    `EveryDeleteOrPseudonymizeType`).
  - `## Triage table` — one row per warning code × file, with the decision
    (fix in U<m> / re-baseline in U13 / successor) and a one-line rationale.
  - `## Ratchet re-baseline plan` — the list of items to `--tighten`, the
    ADR annotations to add (ADR 0298/0299 boundary edges), the 3 allowlist
    entries, the 2 stale method baselines to remove, the 1 file-growth
    re-baseline.
  - `## Known-reds (successor-scope)` — the 2 M2 agreement reds, named
    verbatim, with the `improve-sec` plan's IS21–IS23 / IS31–IS32 unit
    references. The `CodeCheck_Gate_Passes` red is auto-cleared by U13.
  - `## Unit map` — U1–U14, one line each, with the file list and the
    warning/violation count each unit clears.
- **Exit:** file exists with all sections. **No build.** Handoff note:
  5 lines starting `## U1 — design doc`, listing the triage table row count,
  the re-baseline item count, and the successor-scope test names.

### U2 — Route conflict fix (ASP0023)

- **Goal:** fix the `Dissolve` action's wrong route in
  `GuardianController.cs`. The action at line 429 has
  `[HttpPost("{childId}/eventrsvp/{eventId}/approve")]` but takes only
  `childId` — it dissolves the guardian link, it does not approve an RSVP.
  The correct route is `{childId}/dissolve`. Update the route + any test
  that pins the old URL.
- **Entry reads:** `sed -n '415,500p' src/Kumunita.Web/Controllers/GuardianController.cs`
  (the `Dissolve` action + its route + the ADR 0143 `DeleteChild` below it),
  `sed -n '30,80p' src/Kumunita.Web/Controllers/GuardianController.RsvpCurate.cs`
  (the `ApproveEventRsvp` action that has the correct route),
  `grep -rn "eventrsvp.*approve" tests/` (the tests that pin the route),
  `grep -rn "Dissolve\|dissolve" src/Kumunita.Web/Views/Guardian/` (the view
  that posts to the `Dissolve` route — update the `asp-action` / form action).
- **Deliverables (≤ 3 files):**
  - `src/Kumunita.Web/Controllers/GuardianController.cs` — change the route
    on `Dissolve` from `"{childId}/eventrsvp/{eventId}/approve"` to
    `"{childId}/dissolve"` (line 427). The `Dissolve` action signature
    (only `childId`) is correct; only the route is wrong.
  - The view that posts to `Dissolve` (likely
    `Views/Guardian/Detail.cshtml` or a partial) — update the form's
    `asp-action` / `action=` URL to the new route.
  - The test file that pins the old route (find via the grep above) — update
    the expected URL.
- **Exit:** `dotnet build` green. ASP0023 is gone (verify:
  `dotnet build --no-incremental 2>&1 | grep ASP0023` returns nothing).
  Handoff note: 3 lines starting `## U2 — route conflict` — the old route,
  the new route, the files touched, "ASP0023 gone".

### U3 — Test mechanical warnings (CS4014/CS0105/CS0219/CS0108)

- **Goal:** fix the 8 mechanical warnings in 4 test files. All are
  mechanical (add `await`, remove a duplicate `using`, remove a dead
  variable, add `new` keyword or rename).
- **Entry reads:** `sed -n '65,125p' tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs`
  (the 4 CS4014 unawaited calls), `head -10 tests/Kumunita.Web.Tests/HealthControllerTests.cs`
  (the CS0105 duplicate using), `sed -n '105,115p' tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs`
  (the CS0219 dead variable), `sed -n '45,65p' tests/Kumunita.Web.Tests/ProjectsBaseControllerTests.cs`
  (the 2 CS0108 hiding warnings).
- **Deliverables (4 files):**
  - `tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs` — add
    `await` to the 4 unawaited calls (lines 70, 89, 104, 119). If the
    method is not `async`, make it `async Task` and add `await`.
  - `tests/Kumunita.Web.Tests/HealthControllerTests.cs` — remove the
    duplicate `using System.Security.Claims;` (line 2).
  - `tests/Kumunita.Web.Tests/SecGate/CspInlineDisciplineGateTests.cs` —
    remove the unused `baseLine` variable (line 110) or prefix with `_`.
  - `tests/Kumunita.Web.Tests/ProjectsBaseControllerTests.cs` — add `new`
    keyword to `ProjectRow` (line 49) and `HasSortParam` (line 61) to
    silence the CS0108 hiding warnings, OR rename if the hiding is
    unintentional.
- **Exit:** `dotnet build` green. The 8 warnings are gone (verify:
  `dotnet build --no-incremental 2>&1 | grep -E "CS4014|CS0105|CS0219|CS0108"`
  returns nothing). Handoff note: 3 lines starting `## U3 — test mechanical`
  — the 8 warnings fixed, the 4 files touched.

### U4 — Src dead code + unread params (CS8321/CS9113)

- **Goal:** fix the 4 warnings in src: 2 unused local functions in a Razor
  view, 2 unread parameters in services/controllers.
- **Entry reads:** `sed -n '75,85p' src/Kumunita.Web/Views/Boards/BoardDetail.cshtml`
  (the 2 unused local functions `StatusGlyph` / `StatusLabelKey`),
  `sed -n '10,20p' src/Kumunita.Core/UserInfo/ProfileSettingsService.cs`
  (the CS9113 unread `services` param),
  `sed -n '44,55p' src/Kumunita.Web/Controllers/ReportTargetsController.cs`
  (the CS9113 unread `posts` param).
- **Deliverables (3 files):**
  - `src/Kumunita.Web/Views/Boards/BoardDetail.cshtml` — remove the 2
    unused local functions (`StatusGlyph` at line 78, `StatusLabelKey` at
    line 79) or confirm they are used elsewhere in the view (grep the file
    for the function names). If used, the warning is a false positive —
    record it in the handoff note.
  - `src/Kumunita.Core/UserInfo/ProfileSettingsService.cs` — prefix the
    unread `services` param with `_` (line 14) or remove it if the DI
    registration can be updated to match.
  - `src/Kumunita.Web/Controllers/ReportTargetsController.cs` — prefix the
    unread `posts` param with `_` (line 48) or remove it.
- **Exit:** `dotnet build` green. The 4 warnings are gone. Handoff note:
  3 lines starting `## U4 — src dead code` — the 4 warnings fixed, the
  3 files touched, whether the CS8321 was a false positive.

### U5 — IAuthenticationService stub nullability (CS8767/CS8622)

- **Goal:** fix the 52 nullability warnings in the 2 test stub files.
  The `StubAuthService` classes implement `IAuthenticationService` but
  declare `scheme` and `properties` as non-nullable where the interface
  allows null. Add `?` to match.
- **Entry reads:** `sed -n '225,250p' tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs`
  (the `StubAuthService` class with the CS8767 warnings),
  `sed -n '255,280p' tests/Kumunita.Web.Tests/RevocationStampFailClosedTests.cs`
  (the `StubAuthService` class),
  `grep -n "IAuthenticationService" src/Kumunita.Core/Identity/IAuthenticationService.cs`
  or `grep -rn "interface IAuthenticationService" src/` (the interface
  definition — confirm `scheme` is `string?` and `properties` is
  `AuthenticationProperties?`).
- **Deliverables (2 files):**
  - `tests/Kumunita.Web.Tests/SessionRevocationMiddlewareTests.cs` — in
    `StubAuthService`, change `string scheme` → `string? scheme` and
    `AuthenticationProperties properties` → `AuthenticationProperties?
    properties` on all 6 methods (Authenticate, Challenge, Forbid, SignIn,
    SignOut, and any others).
  - `tests/Kumunita.Web.Tests/RevocationStampFailClosedTests.cs` — same
    fix in its `StubAuthService`.
- **Exit:** `dotnet build` green. The 52 warnings are gone. Handoff note:
  3 lines starting `## U5 — stub nullability` — the 52 warnings fixed,
  the 2 files touched, the interface signature confirmed.

### U6 — Split `ResidentAccountRowsEraser.EraseAsync`

- **Goal:** split the 142-line / cc-21 method into a ≤80-line public method
  + extracted private helpers. The method erases rows for multiple doc
  types; extract the per-type erasure into private helpers.
- **Entry reads:** `src/Kumunita.Core/Identity/ResidentAccountRowsEraser.cs`
  (the full file — it should be <200 lines; the `EraseAsync` method is the
  target), `grep -n "EraseAsync" tests/Kumunita.Core.Tests/` (the callers
  / tests that pin the method's behavior), `tools/code/code-baseline.json`
  (the `ceilings` block — confirm 80 lines / 15 cc).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/Identity/ResidentAccountRowsEraser.cs` — extract
    the per-type erasure logic into private helpers (e.g.
    `EraseIdentityTokensAsync`, `EraseSessionsAsync`,
    `EraseNotificationsAsync`, etc.). The public `EraseAsync` method
    keeps its name and signature (callers don't change) and delegates to
    the helpers. Target: ≤80 lines, cc ≤15.
  - The test file(s) that test `EraseAsync` — if the split changes any
    internal behavior (it shouldn't — the split is pure refactoring),
    update the tests. If the tests only test the public behavior, no
    change needed.
- **Exit:** `dotnet build` green. The method is ≤80 lines / cc ≤15 (verify:
  count the lines in the method body). `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  green (0 new failures). Handoff note: 4 lines starting
  `## U6 — EraseAsync split` — the new line count, the cc, the helper
  names, "build green, tests green".

### U7 — Split `RetentionService.RunAsync`

- **Goal:** split the 118-line / cc-38 method into a ≤80-line public method
  + extracted private helpers. The method runs retention policies for
  multiple doc types; extract the per-type retention lambdas.
- **Entry reads:** `src/Kumunita.Core/Privacy/RetentionService.cs` (the full
  file — the `RunAsync` method is the target), `grep -n "RunAsync"
  tests/Kumunita.Core.Tests/` (the callers / tests), `src/Kumunita.Core/Privacy/RetentionOptions.cs`
  (the options shape — the per-type retention configs).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/Privacy/RetentionService.cs` — extract the per-type
    retention logic into private helpers (e.g.
    `RunSessionRetentionAsync`, `RunOutboxRetentionAsync`,
    `RunNotificationRetentionAsync`, etc.). The public `RunAsync` method
    keeps its name and signature. Target: ≤80 lines, cc ≤15.
  - The test file(s) that test `RunAsync` — update if the split changes
    internal behavior (it shouldn't).
- **Exit:** `dotnet build` green. The method is ≤80 lines / cc ≤15.
  `dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  green. Handoff note: 4 lines starting `## U7 — RunAsync split` — the
  new line count, the cc, the helper names, "build green, tests green".

### U8 — Split `ImportOwnershipGuard.Check`

- **Goal:** split the cc-32 method into a ≤15-cc public method + extracted
  private helpers. The method checks ownership of imported content; extract
  the per-type ownership checks.
- **Entry reads:** `src/Kumunita.Core/Portability/ImportOwnershipGuard.cs`
  (the full file — the `Check` method is the target),
  `grep -n "ImportOwnershipGuard" tests/Kumunita.Core.Tests/` (the callers
  / tests), `grep -n "ScopeRowHoldsSeat" src/Kumunita.Core/Portability/UserPortabilityService.cs`
  (the related method in U9 — confirm the split boundary).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/Portability/ImportOwnershipGuard.cs` — extract the
    per-type ownership logic into private helpers. The public `Check` method
    keeps its name and signature. Target: cc ≤15.
  - The test file(s) that test `Check` — update if needed.
- **Exit:** `dotnet build` green. The method is cc ≤15. Core tests green.
  Handoff note: 4 lines starting `## U8 — Check split` — the new cc, the
  helper names, "build green, tests green".

### U9 — Split `UserPortabilityService.ScopeRowHoldsSeat`

- **Goal:** split the cc-33 method into a ≤15-cc public method + extracted
  private helpers. The method checks whether a scope row holds a seat;
  extract the per-scope-type checks.
- **Entry reads:** `src/Kumunita.Core/Portability/UserPortabilityService.cs`
  (the `ScopeRowHoldsSeat` method — it's in a 806-line file; use
  `grep -n "ScopeRowHoldsSeat" -A60` to find the method),
  `grep -n "ScopeRowHoldsSeat" tests/Kumunita.Core.Tests/` (the callers /
  tests), `grep -n "ScopeRowHoldsSeat" src/Kumunita.Core/Portability/ImportOwnershipGuard.cs`
  (the related method in U8).
- **Deliverables (≤ 2 files):**
  - `src/Kumunita.Core/Portability/UserPortabilityService.cs` — extract the
    per-scope-type checks into private helpers. The public
    `ScopeRowHoldsSeat` method keeps its name and signature. Target: cc ≤15.
  - The test file(s) — update if needed.
- **Exit:** `dotnet build` green. The method is cc ≤15. Core tests green.
  Handoff note: 4 lines starting `## U9 — ScopeRowHoldsSeat split` — the
  new cc, the helper names, "build green, tests green".

### U10 — Nullability batch 1 (src: Groups/Event/Boards)

- **Goal:** fix the nullability warnings in 3 src files:
  `GroupsController.cs` (22 warnings), `Views/Groups/Detail.cshtml` (1),
  `Views/Event/Calendar.cshtml` (1).
- **Entry reads:** `dotnet build --no-incremental 2>&1 | grep -E
  "GroupsController|Groups/Detail|Event/Calendar"` (the exact warning
  lines), `sed -n '1,30p' src/Kumunita.Web/Views/Event/Calendar.cshtml`
  (the CS8602 at line 9), `sed -n '410,425p' src/Kumunita.Web/Views/Groups/Detail.cshtml`
  (the CS8604 at line 416).
- **Deliverables (3 files):** fix the nullability warnings in the 3 files
  (add `?` to nullable params, add `!` to non-null assertions, add `null!`
  to required args). The fixes are mechanical — match the warning's
  suggested fix.
- **Exit:** `dotnet build` green. The 24 warnings are gone. Handoff note:
  3 lines starting `## U10 — nullability batch 1` — the 24 warnings fixed,
  the 3 files touched.

### U11 — Nullability batch 2 (src: Posts/Portability/Todos)

- **Goal:** fix the nullability warnings in 3 src files:
  `PostService.cs` (8), `UserPortabilityService.cs` (8),
  `TodosController.cs` (10).
- **Entry reads:** `dotnet build --no-incremental 2>&1 | grep -E
  "PostService|UserPortabilityService|TodosController"` (the exact warning
  lines), the 3 files (read only the warning lines ± 5 lines).
- **Deliverables (3 files):** fix the nullability warnings (mechanical).
- **Exit:** `dotnet build` green. The 26 warnings are gone. Handoff note:
  3 lines starting `## U11 — nullability batch 2` — the 26 warnings fixed,
  the 3 files touched.

### U12 — Nullability batch 3 (src + tests: remaining)

- **Goal:** fix the remaining nullability warnings in 3 files:
  `ProjectsController.cs` (6), `EventController.cs` (6),
  `BoardsController.Translations.cs` (6). If there are remaining test-file
  nullability warnings (from `DocumentServiceTests`, `DocumentControllerTests`,
  `KioskItemCatalogTests`, `KioskControllerTests`, `CommunityControllerTranslationTests`,
  `BreakGlassProbeTests`, `HealthControllerTests`), fix the worst 2 in the
  same unit.
- **Entry reads:** `dotnet build --no-incremental 2>&1 | grep -E
  "ProjectsController|EventController|BoardsController.Translations|DocumentServiceTests|DocumentControllerTests"`
  (the exact warning lines), the files (read only the warning lines ± 5).
- **Deliverables (≤ 4 files):** fix the nullability warnings (mechanical).
- **Exit:** `dotnet build` green. The remaining nullability warnings are
  gone (or reduced to ≤5, documented in the handoff note). Handoff note:
  3 lines starting `## U12 — nullability batch 3` — the warning count
  before/after, the files touched.

### U13 — Ratchet re-baseline + allowlist

- **Goal:** run `CodeMetrics --tighten` to update the baseline after the
  4 method splits (U6–U9), remove the 2 stale method baselines, re-baseline
  the 3 boundary edges that grew, add the ~38 new boundary violations, and
  add the 3 controllers to the Marten allowlist. Annotate ADR 0298/0299
  with the new boundary edges.
- **Entry reads:** `tools/code/code-baseline.json` (the full file — 3547
  lines, but only the `files`, `methods`, `boundaries` sections are
  relevant), `docs/adr/0298-data-classes-in-code.md` (the ADR to annotate),
  `docs/adr/0299-instance-export-operator-gated.md` (the ADR to annotate),
  `grep -n "MartenAllowlist\|allowlist" src/Kumunita.Web/Controllers/AdminGuestsController.cs`
  (the pattern for the allowlist entry — or find the allowlist file via
  `grep -rn "allowlist" src/`), `grep -n "CodeMetrics" tools/code/code-check.ps1`
  (the `--tighten` invocation).
- **Deliverables (≤ 4 files):**
  - `tools/code/code-baseline.json` — run
    `dotnet run --project tools/code/CodeMetrics -- --tighten
    tools/code/code-baseline.json .` to update the baseline. Manually
    remove the 2 stale method entries (`ImportOwnershipGuard.Check` old
    signature, `UserPortabilityService.ScopeRowHoldsSeat` old signature).
    Manually add the 3 boundary edges that grew (or let `--tighten`
    handle them). Manually add the 3 controllers to the Marten allowlist
    section.
  - `docs/adr/0298-data-classes-in-code.md` — add one line: "boundary
    edges re-baselined to N on 2026-11-09 by ADR 0298 (CODE-HYGIENE U13)".
  - `docs/adr/0299-instance-export-operator-gated.md` — same annotation.
  - The Marten allowlist file (find via grep) — add the 3 controllers.
- **Exit:** `pwsh -NoProfile -File tools/code/code-check.ps1` exits 0
  (0 violations). Handoff note: 5 lines starting `## U13 — re-baseline`
  — the violation count before/after (51 → 0), the files touched, the
  ADR annotations added.

### U14 — Close: gate verification + handoff + archive

- **Goal:** run the full test suite, confirm `CodeCheck_Gate_Passes` is
  green (it should auto-clear after U13), confirm the 2 M2 agreement reds
  are the *only* remaining failures (successor-scope), append the final
  handoff note, and move the lane files to the archive.
- **Entry reads:** the full handoff note
  (`docs/plans-milestones/in-progress/code-hygiene-handoff-notes.md`),
  `docs/archive/README.md` (the archive conventions),
  `docs/plans-milestones/README.md` (the lane register layout).
- **Deliverables (≤ 3 files):**
  - `docs/plans-milestones/in-progress/code-hygiene-handoff-notes.md` —
    append the final section: `## U14 — close` with the test totals
    (Web: 1322 / Failed 2 — the 2 M2 reds only; Core: 1901 / Failed 0),
    the warning count (0), the ratchet violations (0), the route conflict
    (fixed), and the successor-scope annotation for the 2 M2 reds.
  - Move the lane files to `docs/archive/lanes/code-hygiene/`:
    `plan-code-hygiene.md`, `code-hygiene-handoff-notes.md`, all
    `U*-prompt.md` files. Update `docs/plans-milestones/README.md` to
    point at the new archive folder.
  - `docs/design/code-hygiene-design.md` — stays in `docs/design/`
    (it's a design doc, not a lane file).
- **Exit:** full test suite run (both assemblies), the 2 M2 reds are the
  only failures, `code-check.ps1` exits 0, the archive folder exists.
  Handoff note: 6 lines starting `## U14 — close` — the test totals,
  the warning count, the ratchet status, the archive path, "lane closed".

---

## Close protocol

The lane is **closed** when:

1. `dotnet build --no-incremental` reports **0 errors, 0 warnings** (or
   ≤5 warnings, all documented as false positives in the handoff note).
2. `pwsh -NoProfile -File tools/code/code-check.ps1` exits **0** (0
   violations).
3. `dotnet exec tests/Kumunita.Web.Tests/.../Kumunita.Web.Tests.dll`
   reports **Failed: 2** (exactly the 2 M2 agreement reds — successor-
   scope). `CodeCheck_Gate_Passes` is **green**.
4. `dotnet exec tests/Kumunita.Core.Tests/.../Kumunita.Core.Tests.dll`
   reports **Failed: 0**.
5. The e2e suite (`npm --prefix e2e run test`) reports **1 passed,
   1 skipped** (the ADR 0050 gate skip — by design).
6. The lane files are moved to `docs/archive/lanes/code-hygiene/`.
