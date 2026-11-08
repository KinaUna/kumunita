---
id: is39-prompt
title: "IS39 prompt — control-map rewrite (traceability): seam + pinning test per row"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS39 — Control-map rewrite (traceability)

## What you are doing

You are the **forty-first working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS38c are committed
(IS38c = `f04c16b0`).

**Finding E8 (severity M):** the control map at
`docs/security/control-map.md` promises that **"every threat must be covered"**,
but of its **24 rows, only ~9 link to code and 15 have no test naming their
seam**. The one row that *does* name a code seam points at the wrong one:
"security-stamp rotation" cites `SecurityStampValidatorOptions`, whose
`ValidationInterval` is set to **14 days so it never fires** (deliberately
inert — the real seams are `RevocationStampMiddleware` +
`PrivilegedStampMiddleware`). And the lane's new controls (data classes,
erasure, retention, operator-gated export, decision-free-read markers, log
redaction + Sentry floor, CSP discipline, admin-read audit) have **no row at
all**. This is the philosophy's *documentation as the control*: the promise
lives in a table, not in a gate.

This unit is the **doc half** of the traceability pair. IS39 rewrites the
control-map table so **every row links a resolving seam and every *code* row
names an existing pinning test**; **IS40** (next unit) turns that into a red
build. **This is a docs-only unit — one file, no code, no build.**

The plan's **IS39** section (`docs/plans-milestones/plan-improve-sec.md`,
search `#### IS39`) is the spec.

## The one thing to get right

**One file** (`docs/security/control-map.md`). The table's shape changes from
**3 columns → 5 columns**:

| old | new |
|---|---|
| `Control` | `Control` (unchanged) |
| `Answers` | `Answers` (unchanged) |
| `Decided by (code seam + ADR)` | split into **`Seam`** · **`Pinned by (test)`** · **`ADR`** |

- **`Seam`** — a **resolving** link: either a code link
  (`../../src/…` to the class/file that enforces it) **or** an `ops:` runbook
  link (`../ops/…`) for owner-ops steps. Every row has exactly one.
- **`Pinned by (test)`** — for a **code** row, the **existing** test that pins
  the seam (class + method; a short list of the most load-bearing ones is fine).
  For an **owner-ops** row (a flag, a daemon, a backup, a restore drill), write
  `— (owner-ops, no code seam)` — IS40 only requires *code* rows to name a real
  test.
- **`ADR`** — the ADR that decided it (`0298` / `0299` / `0003` / `0011` / …),
  or `—` where the decision is a `decisions-open-items.md` entry (link it).

Three concrete fixes the plan names, all required:
1. **The stamp row** moves its `Seam` from `SecurityStampValidatorOptions` to
   **`RevocationStampMiddleware` + `PrivilegedStampMiddleware`**, and records
   in the row that `SecurityStampValidatorOptions` is **deliberately inert at
   14 days** (validator `ValidationInterval` > cookie lifetime, so it cannot
   fire inside a live session).
2. **The 8 new rows** below get added (each with a seam, a pinning test, an ADR).
3. **Every existing code row** gets a `Pinned by (test)` named — no code row
   left blank.

## The contract (exact)

### The 5-column table

```
| Control | Answers | Seam | Pinned by (test) | ADR |
```

Update the table's intro sentence (currently: "the **Decided by** column names
the code seam … and the ADR") to describe the new `Seam` / `Pinned by` / `ADR`
columns and the code-row-vs-owner-ops-row rule. The front-matter `updated`
bumps to today's date; add the two new ADRs (`0298`, `0299`) to `related:`.

### The stamp row (fix, not add)

Current row: `Security-stamp rotation on privilege change | A3 (revoked grants)
| SecurityStampValidatorOptions in Program.cs …`. Rewrite so:
- **Seam:** [`RevocationStampMiddleware`](../../src/Kumunita.Web/Security/RevocationStampMiddleware.cs)
  + [`PrivilegedStampMiddleware`](../../src/Kumunita.Web/Security/PrivilegedStampMiddleware.cs).
- **Pinned by (test):** `RevocationStampFailClosedTests` + `PrivilegedStampMiddlewareTests`.
- Note in the row body: `SecurityStampValidatorOptions` is **deliberately inert
  at 14 days** (its `ValidationInterval` is set past the `kumunita.auth`
  cookie's own lifetime so the validator can never fire inside a live session;
  the per-request `Profile.Blocked` + stamp middlewares are the real seams).

### The 8 new rows (add)

Each must cite a seam that **actually exists** (verify with `grep`) and a
**pinning test that actually exists** (verify with `grep`). The mapping below
is the intended one — confirm the names before you write them (a wrong name is
exactly what IS40's gate is built to catch):

| Control (row text) | Seam | Pinned by (test) | ADR |
|---|---|---|---|
| **Data classes in code** — a registered doc type cannot exist without a `[DataClass]` policy (class / erasure / export / retention) | `Kumunita.Core/Privacy/DataClassAttribute.cs` + the catalog reader | `DataClassCoverageTests.EveryRegisteredDocType_HasADataClass` (Core.Tests, IS20h) | 0298 |
| **Account-deletion erasure** — every `Delete` / `Pseudonymize` type is handled by an eraser; 1:1 + group threads are pseudonymized (D3) | `Kumunita.Core/Identity/ResidentContentEraser.cs` + the thread pseudonymizer | `DeleteAccountThreadTests` + `DeleteAccountRowsTests` + `SecGate/DataClassCoverageTests.EveryDeleteOrPseudonymizeType_IsHandledByAnEraser` | 0298 |
| **Retention** — sessions / outbox / tokens / dead-letters / notifications / error reports / file logs all have a cutoff or a `Keep` reason (D6) | `Kumunita.Core/Privacy/RetentionService.cs` (driven by `RetentionTick`) | `RetentionServiceTests` (e.g. `Retention_DeletesOldRevokedSessions_KeepsLive`, `Retention_RetainsLogFilesDaily`) | 0298 |
| **Operator-gated instance export** — content-free by default; `full` requires an active `AdminOverride`, is audited `Via: BreakGlass`, and notifies residents (D1/D10) | `AdminPortabilityController` + `PortabilityService` + `BreakGlassProbe` | `AdminPortabilityScopeTests` (`Export_Default_IsContentFree`, `FullExport_WithoutOverride_Forbidden`, `FullExport_WithOverride_AuditedBreakGlass`, `FullExport_NotifiesEveryResident`) + `BreakGlassProbeTests` | 0299 |
| **Decision-free-read markers** — every read of an audience-bearing type is decided in-method or marked `// SEC-READ:` | the `SEC-READ` markers across `Kumunita.Core` | `SecGate/DecisionFreeReadGateTests.EveryAudienceBearingRead_IsDecidedOrMarked` + `SecReadMarker_NamesAReason` | 0298 |
| **Log redaction + Sentry floor** — no log template carries a PII placeholder; nothing below `Warning` reaches Sentry (B7) | `Kumunita.Core/Logging/LogRedact.cs` + the `UseSentry` block in `Program.cs` | `SecGate/LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder` + `SentryBreadcrumbPiiTests` (`InformationLog_IsNotABreadcrumb`, `ScrubberStillRedactsHeadersCookiesQuery`) | 0298 |
| **CSP discipline (no inline script in views)** — `script-src 'self'`, and no inline `on*` handler or executable `<script>` block in any Razor view (E9/E14) | the `app.Use(…)` CSP block in `Program.cs` (the enforcement) + the gate | `CspInlineDisciplineGateTests` (`NoView_HasAnInlineEventHandler`, `NoView_HasAnInlineScriptBlock`) | 0298 |
| **Admin-read audit** — the audit log, error reports, and guest lists are themselves audited when a GlobalAdmin reads them (D9) | `AdminController` / `ErrorReportAdminController` / `AdminGuestsController` | `AdminReadAuditTests` (`AuditLogView_IsItselfAudited`, `ErrorReportView_IsAudited`, `GuestListView_IsAudited`) | 0298 |

(Where a row's seam is genuinely a runbook rather than code — e.g. the
existing backup / restore / DB-role / seed-token rows — keep it as an owner-ops
row: `Seam` = the runbook link, `Pinned by` = `— (owner-ops, no code seam)`.)

## Constraints

- **One file** (`docs/security/control-map.md`). No code, no build, no new test.
- **Do not invent a test or a seam.** Every `Pinned by (test)` you write must be
  a class that exists and a method that exists (verify with `grep -rn`). Every
  `Seam` code link must resolve to a real file. A wrong name is the failure IS40
  is written to catch — do not ship one.
- **Do not delete any existing row** and do not change any existing `Control`
  or `Answers` text — this is a *column split + fixes + additions*, not a
  rewrite of the controls themselves.
- **Do not flip any value or decision.** The map describes controls that
  already ship; you are making each one *traceable*, not changing its posture.
- Keep the two `application/json` / prose links in the intro intact; the
  front-matter `tier: reference` / `kind: security` stay.

## Deliverables (1 file)

| # | File | Change |
|---|---|---|
| 1 | `docs/security/control-map.md` | 3→5 column split; stamp row seam fixed (+ 14-day inert note); 8 new rows; every existing code row names a pinning test; intro sentence + front-matter updated |

## Entry reads (only these — no whole-file reads)

- `docs/security/control-map.md` (whole — it is ~70 lines; the table + intro +
  front-matter).
- The plan's **IS39** section: `docs/plans-milestones/plan-improve-sec.md`
  (search `#### IS39`; also the **IS40** section for what the gate will check,
  and the **D8** row + **DoD-7/P-7** row for the shape).
- `ls tests/Kumunita.Web.Tests/SecGate` and the handoff headers
  (`grep -n "^## IS" docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`)
  — to know which gate/unit landed each new control.
- `docs/adr/0298-data-classes-in-code.md` (the header + title only) and
  `docs/adr/0299-instance-export-operator-gated.md` (the header + title only) —
  for the two new ADR links.
- To verify each test name you cite: `grep -rn "public.*<MethodName>" tests/`
  (the 8 rows above give the intended names; confirm them).

## Verification

- [ ] `docs/security/control-map.md` is the **only** file changed
      (`git status` shows just that file).
- [ ] The table has **exactly 5 columns**:
      `Control | Answers | Seam | Pinned by (test) | ADR`.
- [ ] **8 new rows** present (data classes, erasure, retention,
      operator-gated export, decision-free-read markers, log redaction + Sentry
      floor, CSP discipline, admin-read audit).
- [ ] **The stamp row** names `RevocationStampMiddleware` +
      `PrivilegedStampMiddleware` as the seam and records the 14-day inert
      `SecurityStampValidatorOptions`.
- [ ] **Every code row names a test that exists** — spot-check 5 of them with
      `grep -rn "public.*<Method>" tests/`; none return empty.
- [ ] **Every Seam code link resolves** — spot-check 5 links against
      `ls src/…` (no 404-style dangling `src/…` path).
- [ ] No existing row deleted; no `Control` / `Answers` text changed.

## Definition of done (IS39)

- [ ] One file changed; the table is 5 columns; every row has a resolving seam.
- [ ] Every *code* row names an existing pinning test; every *owner-ops* row
      links a runbook and is marked `— (owner-ops)`.
- [ ] The stamp row is fixed to the two stamp middlewares + the 14-day inert
      note.
- [ ] The 8 new rows are present with correct ADRs (0298 / 0299 / 0003 / …).
- [ ] One commit (the single doc + a `## IS39` handoff section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`); the
      prompt file is **not** part of the unit commit (lane convention).

## Next unit

**IS40 — the control-map traceability gate.** One new file:
`tests/Kumunita.Web.Tests/SecGate/ControlMapTraceabilityTests.cs` with
`EveryRow_HasASeamLink_ThatResolves`, `EveryCodeRow_NamesAPinningTestThatExists`,
`EveryOpsRow_LinksARunbookThatExists` — a red build if any row's seam is a dead
link or any code row names a test that does not exist (the M1 baseline: 9/24 →
all rows traced). This is why IS39's "verify every name with `grep`" step
matters: the gate IS40 writes is only as trustworthy as the names IS39 put in
the table.
