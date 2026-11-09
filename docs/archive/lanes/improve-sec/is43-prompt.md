---
id: is43-prompt
title: "IS43 prompt — Run and record: the six baselines, before → after, against the IS04 baseline"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS43 — Run and record

## What you are doing

You are the **forty-third working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS42 are committed
(IS42 = `d15367fa`).

This is the **lane's close unit (of three)**: `IS43 → IS44 → IS45`. IS43
does **one thing**: prove, with the build and the suites, that the six
baselines the lane set out to retire are now **green by their own gates**,
and record the before → after numbers in the design doc's `### Run result`
section. It **adds no code, no new test, no new ADR, and closes no open item**.
It is the **measurement** the `SecGate` gates already enforce — the plan's
"the gates follow the IMPROVE-CODE precedent: start from the measured baseline
and **retire** it to zero; never freeze it."

The one thing to get right, in the philosophy's words: **the gate is the
control; this unit is the record.** Do not edit a gate to pass it, do not edit
a test to skip it, do not add an allowlist row to make a number go down. The
number goes down because the code changed in IS05–IS42 — not because of
anything this unit does.

## The one thing to get right

**Record the six numbers, and the known-reds, in one `### Run result` section.**
The six baselines (IS04's starting state → this run's state):

| # | Baseline (IS04) | Gate that retires it | Expected now |
|---|---|---|---|
| M1 | control-map rows with a pinned seam **9/24** | `ControlMapTraceabilityTests` (IS40) | **all rows pinned** |
| M2 | principal-bearing types with no erasure **25/65** | `DataClassCoverageTests.EveryDeleteOrPseudonymizeType_IsHandledByAnEraser` + `EveryResidentExportType_IsInUserScopeInventory` (IS24) | **0 unhandled** (see known-reds below) |
| M4 | PII log placeholders **7** | `LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder` (IS08) | **0** |
| M7 | unmarked decision-free reads **52** | `DecisionFreeReadGateTests.EveryAudienceBearingRead_IsDecidedOrMarked` (IS15) | **0** |
| — | inline `on*` handlers in views **8** | `CspInlineDisciplineGateTests.NoView_HasAnInlineEventHandler` + `NoView_HasAnInlineScriptBlock` (IS38 / IS38c) | **0** |
| — | unaudited admin reads of class (c)/PII **3** | the three IS36 tests (`AuditLogView_IsItselfAudited`, `ErrorReportView_IsAudited`, `GuestListView_IsAudited`) | **0** (the reads are now audited) |

**The known-reds (pre-existing, recorded by IS24 / IS31 / IS32 / IS38 / IS40,
all in `Kumunita.Web.Tests`):**

- `SecGate.DataClassCoverageTests.EveryResidentExportType_IsInUserScopeInventory`
- `SecGate.DataClassCoverageTests.EveryDeleteOrPseudonymizeType_IsHandledByAnEraser`
- `ImproveHarnessTests.CodeCheck_Gate_Passes`

These three are **expected red** — they are the recorded drift the lane has
carried (the M2 gate lists types the lane's IS31/IS32 scope did not fully
close; `CodeCheck_Gate_Passes` is a pre-existing harness gate that fails at
IS43's HEAD too). They are **not new failures introduced by IS43**. Do not
"fix" them (that is a different unit's scope); do not let them block the
record — but **list them verbatim** in the `### Run result` section with their
owner units (IS24 / IS31 / IS32 / IS38 / IS40). **If any other test fails,
stop and list it** (per the plan: "If a gate is red, stop and list it.").

## Constraints

- **No code, no new test, no new ADR, no allowlist row, no config value.**
  The only file edits are the **`### Run result` section** in
  `docs/design/improve-sec-design.md` (new section, appended) and the
  **`## IS43` handoff section** in
  `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` (appended).
- **`sec-report.ps1` is the IS04 baseline** — it prints **9/24 · 25/65 · 7 ·
  52 · 8 · 3** (the *before* numbers). It does **not** measure the current
  state. Run it to confirm the baseline is intact; the *after* numbers come
  from the six green `SecGate` tests above.
- **`sec-measure.py`** — the plan says IS43 deletes it. Verify with
  `git ls-files tools/sec/sec-measure.py` and `ls tools/sec/` first. If it is
  **absent** (likely — IS00's "delivered as `improve-sec-00-plan.patch`" never
  landed as a tracked file), **record that** in the handoff; if it is
  **present**, `git rm` it (its checks now live in `SecGate`).
- **xunit.v3 runner form** (SEC U3 — `docs/dev/testing-and-ci.md`): `dotnet
  exec tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
  (full suite) and `dotnet exec
  tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll`
  (full suite). `dotnet test --filter` reports "Zero tests ran / exit 5" on
  this machine — do not use it.
- **The two `DataClassCoverageTests` classes are in different projects** —
  `tests/Kumunita.Core.Tests/DataClassCoverageTests.cs` (the IS20c
  `EveryRegisteredDocType_HasADataClass` gate — expected **green**, 81/81) and
  `tests/Kumunita.Web.Tests/SecGate/DataClassCoverageTests.cs` (the IS24
  agreement gate — the two known-reds above). Run both suites; the reds are
  all in Web.Tests.

## Deliverables

| # | File | Change |
|---|---|---|
| 1 | `docs/design/improve-sec-design.md` | **new `### Run result` section** (appended after the existing `§2.6` tail): the six before → after numbers (the table above), the full-suite totals for both projects (pass/fail/skip), and the three known-reds listed verbatim with their owner units. **If any gate other than the three known-reds is red, the section records the failure and the run stops — no close.** |
| 2 | `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` | **new `## IS43` section** (appended): the build result, the two-suite totals, the `sec-report.ps1` output (the IS04 baseline), the `sec-measure.py` absence record (or the `git rm`), the six after-numbers, the three known-reds verbatim, and the "next unit: IS44" pointer. |

## Entry reads (only these — no whole-file reads)

- `docs/plans-milestones/plan-improve-sec.md` — the **IS43** section + the
  **D7** decision row (gate form) + the **D8** decision row (unit shape) + the
  **DoD** (the 13 lines this run must satisfy).
- `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
  `## IS04` section (the baseline: 9/24 · 25/65 · 7 · 52 · 8 · 3), the `## IS24`
  section (the M2 gate's "recorded red" status), the `## IS31` + `## IS32`
  sections (the M2 drift they carry), the `## IS38` + `## IS38c` sections (the
  inline-handler gate's "8 → 0" + "2 → 0"), the `## IS36` section (the 3
  admin-read tests), the `## IS40` section (the control-map gate's "all rows
  traced"), and the `## IS42` section (the last committed unit).
- `tools/sec/sec-report.ps1` (whole, ~20 lines) — the baseline printer.
- `tests/Kumunita.Web.Tests/SecGate/` — the 6 gate test files (the names of
  the tests that retire the 6 baselines).
- `docs/design/improve-sec-design.md` — the tail (after `§2.6`), to place
  the `### Run result` section.
- `docs/dev/testing-and-ci.md` — the xunit.v3 runner form (the in-process
  `-class` / full-suite path; the `dotnet test --filter` quirk).

## Verification

- [ ] `dotnet build` → **0 errors**.
- [ ] Full `Kumunita.Web.Tests` suite → the **only** failures are the 3
      known-reds (the 2 `DataClassCoverageTests` + `ImproveHarnessTests.CodeCheck_Gate_Passes`);
      everything else green. **Any other red = stop and list it** (the `###
      Run result` section records the failure and the run stops — no close).
- [ ] Full `Kumunita.Core.Tests` suite → **0 failures** (the IS20c
      `EveryRegisteredDocType_HasADataClass` gate is green, 81/81).
- [ ] `pwsh tools/sec/sec-report.ps1` → prints the **IS04 baseline** (9/24 ·
      25/65 · 7 · 52 · 8 · 3) — confirm the baseline is intact.
- [ ] `git ls-files tools/sec/sec-measure.py` → **empty** (absent — record
      that); or the file is present and `git rm`'d in this commit.
- [ ] `### Run result` section in `docs/design/improve-sec-design.md` —
      present, with the six before → after numbers, the two-suite totals, and
      the three known-reds listed verbatim.
- [ ] `## IS43` section in the handoff notes — present, with the same content.
- [ ] One commit: the design-doc section + the handoff section (and the
      `git rm sec-measure.py` if it was present). No `.cs`, no `.ts`, no ADR.

## Definition of done (IS43)

- [ ] The six baselines are **retired to zero** by their own gates (all green);
      the 3 known-reds are **recorded** (not fixed, not skipped).
- [ ] `### Run result` in the design doc + `## IS43` in the handoff — both
      present, both consistent.
- [ ] `sec-measure.py` absent (recorded) or deleted (the plan's "delete it").
- [ ] One commit; no code, no ADR, no allowlist row, no config value flipped.
- [ ] **Next unit: IS44** — Independent verification (cold read). A **fresh
      agent with no lane context** gets only the DoD, the repo, and three
      probes (throwaway doc type → `DataClassCoverageTests` must fail;
      `logger.LogInformation("{Email}", e)` → the log gate must fail;
      `admin/portability/export?scope=full` as a GlobalAdmin with no override
      → 403 + a content-free archive by default). It reports each DoD line
      PASS/FAIL with evidence and reverts the probes; its report is appended
      verbatim to the handoff note. Then **IS45** (close flip + archive).
