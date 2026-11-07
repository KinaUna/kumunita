---
id: improve-sec-handoff-notes
title: "IMPROVE-SEC — Handoff Notes"
tier: living
kind: handoff
status: current
audiences: [developer, agent]
lane: improve-sec
related:
  - docs/plans-milestones/plan-improve-sec.md
  - docs/design/improve-sec-design.md
updated: 2026-11-07
---

> **Abstract:** the running handoff log for the IMPROVE-SEC lane — each unit
> appends a `## IS<n>` section (files, public names, tests, pass count,
> next-unit notes).

## IS01 — Design doc Part 1

- Files created: `docs/design/improve-sec-design.md` (≈ 180 lines; sections
  `## Context` → `## Invariants` → `## Findings register`, in that order) and
  this file.
- Invariants: P-1…P-8 verbatim from the plan, each with its closing finding
  and enforcing unit/gate.
- Findings register: **13 rows** (E1–E13), transcribed as written from the
  plan's Evidence table; *Where* refs are the plan's measurement at
  `main` `1970cfc4` (2026-11-06) — **IS02 re-verifies every one and
  corrects or marks n/a**.
- Nothing ambiguous in the plan rows; no code touched, no build run
  (IS01 is design-only).

## IS02 — Design doc Part 2a

- Appended to `docs/design/improve-sec-design.md`: `## Part 2a` with
  §2.1 (re-verified register), §2.2 (the seven fix-shape signatures), §2.3
  (test names for IS05–IS42), §2.4 (drift-guard anchors per unit).
- **13/13 register rows re-verified** against `development` @ `0936c327`:
  12 confirmed as written, **1 corrected** — E3's `IdentityService.cs:270`
  is `:271` (the plan's `:270` is the argument line; the `{Email}` template
  is `:271`). No row is "n/a (fixed)" — nothing in the tree pre-fixes any
  unit.
- Noticed: E11's "hashed at rest" claim actually lives in `WhatsNew.cs:94–120`
  (0.68.0) + `STATUS.md:62`, not `decisions-open-items.md` as the plan's
  *Where* pin says; recorded in §2.1 so IS09/IS41 land the correction in the
  right file (history stays untouched).
- `improve-check.ps1` ALL GATES PASS. No code touched, no build run
  (design-only unit).
- IS03 builds the §2.5 table on top of §2.2's `[DataClass]`/retention keys
  and §2.3's test-name contract; the signature text in §2.2 is the
  implementing contract for IS05–IS42.

## IS03 — Design doc Part 2b

- Appended to `docs/design/improve-sec-design.md`: `## Part 2b` with
  §2.5 (**81 rows**, one per `Schema.For<T>` registration across the 27
  `*DocTypes.cs` files — the plan's `81` is confirmed by count) and
  §2.6 (the `ContentFree` strip table over the 44 M11 types).
- Per-class breakdown of the 81: a **3** · b **41** · c **14** · d **3**
  · e **1** · operational **10** · platform **9**. Every principal-bearing
  type has a non-empty erasure value; 20 `Keep(reason)` rows are platform
  singletons / cascade sub-entities / `Group` (ADR 0142) / D3 thread
  coherence (`Conversation`, `GroupConversation`).
- F1 §2C "decision" rows all decided per D2/D3/D6: Notifications ×4,
  `DelegationGrant`, `GuestAccess`, `IdentityToken`, `SessionDoc` →
  `Delete`; `Report`, `ErrorReport`, `EscalationToken`, `UsageEvent` →
  `Pseudonymize`; resident-owned `Group` → `Keep` (ADR 0142).
- §2.6 leaves **8 `// TODO ❓` rows** (owner decision, IS27 defaults to
  skip): `DelegationGrant`, `GroupInvitation`, `GroupJoinRequest`,
  `GuardianLink`, `Report` (class c); `Notification`, `NotificationPreference`,
  `NotificationSubscription` (recipient inbox). 27 `PrivateContent` types
  omitted, 7 of those audience-bearing (public rows stay), `Profile` PII
  fields (`Email`/`Phone`/`Address`) blanked, 8 always-present
  (structural/public).
- Next: **IS04** builds the deny-matrix gate harness; **IS16–IS20**
  annotate the 81 types with `[DataClass]` **verbatim from §2.5** (the
  table is the single fact IS16's `DataClassAttribute` and IS24's
  agreement gate check against).
- `improve-check.ps1` ALL GATES PASS. No code touched, no build run
  (design-only unit).

## IS04 — Gate harness + deny-matrix gate

- Files created: `tests/Kumunita.Web.Tests/SecGate/SecGateSource.cs` (repo-root
  walk-up, `ReadSrc`, `AllSrcFiles`, `TestMethodExists`),
  `tests/Kumunita.Web.Tests/SecGate/AccessViaDenyMatrixTests.cs`,
  `tools/sec/sec-report.ps1`, this handoff append.
- The deny matrix maps all **13** `AccessVia` members (the U08 table's 11 plus
  `Anonymous` (ADR 0154) and `Escalation` (M31·9/ESC) — the enum has grown
  past U08's "all 11" line; the gate is written against the live enum, so the
  next append without a pin fails `EveryAccessVia_HasANamedDenyTest`). Each maps to a deny-side pin
  that exists in `tests/**` (e.g.
  `Escalation → ESC_5_AcceptInbound_InvalidToken_Returns_Error_NoRow`).
  The two E13 invariant tests (`EvaluateAudience_EmptyAudience_AllMode_Denies`,
  `C2_Delegate_OutOfScope_Denies_WithDelegationViaRecorded`) are pinned by
  `EmptyAudienceDenies_AndDelegationScope_ArePinned`.
- `dotnet build` 0 errors; the 3 `SecGate` tests pass, and the full
  `Kumunita.Web.Tests` suite (1296) is green (runner form: `dotnet exec
  tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`, the
  `docs/dev/testing-and-ci.md` in-process path — `dotnet test -- -class`
  reports "Zero tests ran / exit 5" on this machine).
- `pwsh tools/sec/sec-report.ps1` prints the six baselines (9/24 · 25/65 · 7 ·
  52 · 8 · 3).
- Noticed: `Directory.EnumerateFiles` does not take `**` glob patterns (it
  takes a filename pattern + `EnumerationOptions.RecurseSubdirectories`) —
  `SecGateSource.AllSrcFiles` is documented to that effect for IS15/IS24/IS38b/
  IS40.
- Next: **IS05** (Sentry breadcrumb floor) is the first Phase A fix;
  IS15/IS24/IS38b/IS40 reuse `SecGateSource`.
