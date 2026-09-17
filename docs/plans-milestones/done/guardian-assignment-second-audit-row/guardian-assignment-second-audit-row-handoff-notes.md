# Guardian assignment, second audit row (`GA-AR`) — rolling handoff notes

> **The scratch tier** of the GA-AR lane's three-tier contract (the
> register is primary, the unit plans are secondary, this file is scratch).
> One section per unit, **appended, never rewritten**. Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-reads list. A `## U<m> — Drift pause` section is
> a **blocker**: the next unit reads it first and either resolves it
> (recording the resolution in its own section) or carries it forward
> (naming it in its exit criteria).
>
> The skeleton below is the **only** pre-written content — every `##`
> section from here on is authored by a unit, in order.

## Lane open

- **Date:** 2026-09-17
- **Register:** `docs/plans-milestones/in-progress/
  plan-guardian-assignment-second-audit-row.md` (U01–U02)
- **Pinned contract (primary):** the register's § Pinned contract (the exact
  `AssignGuardianLinkAsync` seam + the `GuardianController.Assign` one
  changed line + the S·1–S·6 invariants + the FACES F1–F4 + the 3
  pinned/renamed test names + the acceptance gate + the drift-guard)
- **ADR:** `docs/adr/0038-guardian-assignment.md` §Amendment (2026-09-17,
  second) (U02 authors it fresh — the GA lane's 2026-09-17 amendment's
  shape to emulate; the §D named legibility limitation this lane resolves;
  the §E "no second audit verb" deferral this lane implements)
- **Scope:** the assigning guardian is now **on the audit trail** — a
  second `guardian.assign` audit row (`ActorId = the assigning guardian`)
  written in the **same commit** as the `GuardianLink` row + the
  `guardian.create` row (`ActorId = the assigned guardian`). One additive
  Core seam (`AssignGuardianLinkAsync`), one changed line on the Web
  action, one Core test, two Web test edits, one ADR amendment, one design-doc
  section. The GU lane is **byte-identical** (S·2, S·3, S·4).
- **Out of scope (the named deferrals, unchanged):** no `AssignedBy` field
  on `GuardianLink` (the POCO stays byte-identical — S·3, the conferrer is
  in the audit trail, not the relationship document); no remove path (ADR
  0038 §E, unchanged); no acceptance/consent step (ADR 0038 §E, unchanged);
  no bulk assign (ADR 0038 §E, unchanged); no GU lane code change (the GU
  seam, the five GU actions, the GU tests, the GU design doc, the GU ADR —
  all byte-identical — S·2/S·4); no `IAuthorizationService` / `AccessVia`
  / GU enforcement path change (G-A·3's substance — S·4); no UI change (the
  `Detail.cshtml` appends, the `AssignGuardianForm`, the l10n keys — all
  unchanged; the user-facing behavior is identical, only the audit trail
  gains a row); no roadmap trio flip (the GA-AR lane is an
  audit-legibility refinement of a shipped lane, not a new roadmap item).
- **Test model:** Core seam test in `Kumunita.Core.Tests` against
  `PostgresFixture` (U01 — the **1** pinned seam test:
  `AssignGuardianLink_WritesBothAuditRows` — both audit rows + idempotency).
  Web controller integration tests in `Kumunita.Web.Tests` (U02 — the
  **1** rename: `Assign_KnownEmail_CallsCreateGuardianLinkAsync` →
  `Assign_KnownEmail_CallsAssignGuardianLinkAsync`, + the **1** add:
  `Assign_KnownEmail_WritesAssignAuditRow`). The lane's **acceptance
  gate** (U01 pins it in the register's § Acceptance gate; U02 records the
  run result) = the 10-test list (4 Core + 6 Web) + the build line. The
  GA lane's 9 existing pinned tests are **unchanged** (one renamed, one
  added). Runner quirk (AGENTS.md) applies: run via `dotnet exec
  tests\…\…\.dll`, not `dotnet test`.

## U01 — AssignGuardianLinkAsync seam + test

- **Seam (verbatim):** `Task<GuardianLink> AssignGuardianLinkAsync(string childId, string guardianId, string assignedById);` — appended after `CreateGuardianLinkAsync` in `IUserInfoService.cs`, with the register's pinned doc-comment (S·1 C3, S·3 POCO byte-identity, S·6 idempotency, ADR 0006-E lane).
- **Audit-row shape (verbatim):** (1) `Action = "guardian.create"`, `ActorId = guardianId`, `EffectivePrincipalId = guardianId` (the standing-holder — the GU seam's shape, S·2/S·5); (2) `Action = "guardian.assign"`, `ActorId = assignedById`, `EffectivePrincipalId = assignedById` (the conferrer — the GA-AR verb, S·5). Both `TargetKind = "guardian-link"`, `TargetId = link.Id`, `Via = Guardian`, `Outcome = Allow`, written in **one** `SaveChangesAsync` (S·1).
- **New test (verbatim):** `AssignGuardianLink_WritesBothAuditRows` — asserts both rows (ActorId, Via, Outcome, TargetId, TargetKind) + S·6 idempotency (second call: link count stays 1, both audit counts stay 1).
- **Pass/red counts:** 5/5 PASS (4 existing + 1 new), 0 failed, 10.6s.
- **GU lane confirmation:** `CreateGuardianLinkAsync` and the `GuardianLink` POCO are **byte-identical, confirmed by reading the file** (S·2, S·3) — no field added, no signature changed, no DDL touched.
- **Drift pause:** none — the pinned contract matched the entry reads exactly.

## U02 — Web switch + 2 Web tests + docs + close

- **One changed line in `GuardianController.Assign` (verbatim):**
  - Before: `await userInfo.CreateGuardianLinkAsync(childId, assignedId);`
  - After: `await userInfo.AssignGuardianLinkAsync(childId, assignedId, subject);`
  The `TempData["info"]` line, the standing gate (G-A·1), the resolution (G-A·2), the self-assignment refusal (G-A·5), the `try/catch` shape, and the redirect are all **untouched**.
- **2 Web test names (verbatim):**
  - **Renamed:** `Assign_KnownEmail_CallsCreateGuardianLinkAsync` → `Assign_KnownEmail_CallsAssignGuardianLinkAsync` (assertions now expect **two** audit rows: `guardian.create` with `ActorId = NewGuardian` [the assigned guardian, S·5] + `guardian.assign` with `ActorId = Guardian` [the assigning guardian / conferrer, S·5], both `TargetId` = the link's id).
  - **Added:** `Assign_KnownEmail_WritesAssignAuditRow` — the `guardian.assign` row's `ActorId` = `Guardian` (the assigning guardian, the test's `actorSubjectId`), `EffectivePrincipalId` = `Guardian`, `NotEqual(NewGuardian, ActorId)`, `TargetKind = "guardian-link"`, `TargetId` = the link's id, `Via = Guardian`, `Outcome = Allow`.
- **Build / test counts:** `dotnet build Kumunita.slnx -c Debug` → **0 warnings, 0 errors**. Core `GuardianAssignmentTests` → **5/5 PASS** (4 GA + 1 GA-AR — re-verified after the Web switch, confirming the Core seam is intact). Web `GuardianAssignmentTests` → **6/6 PASS** (5 GA — one renamed — + 1 GA-AR). Full `Kumunita.Web.Tests` suite → **201/201 PASS** (the GA lane's 9 existing pinned tests are unchanged; the GA-AR lane adds 1 Web test and renames 1, so the total goes 200 → **201**). All run via the reliable `dotnet exec …dll` path (AGENTS.md), not `dotnet test`.
- **GU lane confirmation (S·2, S·3, S·4):** `CreateGuardianLinkAsync`, the `GuardianLink` POCO, `M1DocTypes`, the five GU actions, `AccessVia`, and the GU enforcement path are **byte-identical, confirmed by reading the file** — the new `AssignGuardianLinkAsync` seam is purely additive; the GU lane's `AddChild` call site still calls `CreateGuardianLinkAsync` (creation-based) and is untouched.
- **Docs:** ADR 0038 `## Amendment (2026-09-17, second)` appended (the new seam + C3 atomicity + the `guardian.assign` verb + the Web action's one changed line + the test list + S·1–S·6 + the roadmap-trio-untouched line + the §E other-non-decisions closing paragraph). The design doc's **5 additive edits** are done (§ Pinned seam tests gains the Core name + renames/adds the Web names + the GA-AR extension note; § Acceptance gate re-counts + adds the **fourth** "audit legibility" leg; § Drift-guard extends with the new seam + the one changed line + S·1–S·6 + FACES F1–F4 + the 3 new/renamed names; the `Verification + reconciliation (2026-09-17)` "Not done" line is **superseded**; new `## GA-AR — Second audit row (implemented) (2026-09-17)` section appended). The roadmap trio (README / `Milestones.cs` / `MilestonesTests.cs`) is **untouched**.
- **File moves:** all 4 lane files (register + U01 plan + U02 plan + handoff notes) moved from `in-progress/` to `done/guardian-assignment-second-audit-row/`; `in-progress/` is **empty** (confirmed).
- **Drift pause:** none — the pinned contract matched the entry reads exactly (U01's seam signature + audit-row shape + the 1 Core test name all present in `IUserInfoService.cs` / `UserInfoService.cs` / `Kumunita.Core.Tests`). **One reconciliation recorded (not a drift pause, a count correction):** the register's prose pinned the acceptance gate as "10 tests (4 Core + 6 Web)", but the measured run is **11 (5 Core + 6 Web)** — the register's 4-Core tally counts only the GA lane's 4 Core tests (`FindSubjectByEmail_*` ×3 + `Handoff_*`) and omits U01's new Core test `AssignGuardianLink_WritesBothAuditRows`. The test NAMES (11, per § Pinned seam tests) are authoritative; the ADR 0038 §Amendment (second) item 5 and the design doc's § Acceptance gate / § GA-AR section record the true 11-test count with the reconciliation note.

## Summary

| Unit | One-liner goal | Test count | Deviations |
|------|----------------|------------|------------|
| **U01** | Add the `AssignGuardianLinkAsync` seam + impl + the 1 Core test (the conferral seam, two audit rows, one commit) | 5/5 Core PASS (4 GA + 1 GA-AR) | none |
| **U02** | Web switch (one line) + 2 Web tests (rename + add) + ADR 0038 §Amendment (second) + 5 additive design-doc edits + file moves | 6/6 Web PASS (5 GA — one renamed — + 1 GA-AR); full suite 201/201 | **Count reconciliation:** register prose said "10 (4 Core + 6 Web)"; measured truth is **11 (5 Core + 6 Web)** (register's 4-Core tally omits U01's new Core test `AssignGuardianLink_WritesBothAuditRows`). Test NAMES authoritative; ADR 0038 + design doc record the true count. |

**Invariants S·1–S·6** (each named, held):
- **S·1 — C3 atomicity:** link + both audit rows in one `SaveChangesAsync`; a partial write is a bug, not a state.
- **S·2 — GU seam byte-identical:** `CreateGuardianLinkAsync` unchanged in signature, behavior, and audit shape; the GU lane's `AddChild` call site unchanged.
- **S·3 — POCO byte-identical:** `GuardianLink` has no new field / enum value; `M1DocTypes` unchanged.
- **S·4 — Standing unchanged:** the assigned guardian's standing is identical in kind to the creator's; no new GU action / content read / "assigned" tier; the `IAuthorizationService` frozen surface unchanged.
- **S·5 — Audit shape:** `guardian.assign`'s `ActorId`/`EffectivePrincipalId` = the **assigning** guardian (the conferrer); `guardian.create`'s = the **assigned** guardian (the standing-holder); both `TargetId`s = the link's id, both `Via = Guardian`, `Outcome = Allow`, `TargetKind = "guardian-link"`.
- **S·6 — Idempotency (G-A·4, inherited):** a duplicate `(guardianId, childId)` active row is a no-op — no second row, no second pair of audit rows.

**FACES F1–F4** (each named, proven):
- **F1** a non-guardian cannot assign (404) — G-A·1 (inherited) — proven by `Assign_NonGuardian_Returns404`.
- **F2** the `guardian.assign` row's `ActorId` is the assigning guardian — S·5 — proven by `AssignGuardianLink_WritesBothAuditRows` + `Assign_KnownEmail_WritesAssignAuditRow`.
- **F3** a duplicate assignment is a no-op (no second pair of audit rows) — S·6 — proven by `Assign_DuplicateAssignment_IsIdempotentNoOp`.
- **F4** the GU seam + POCO are byte-identical — S·2, S·3 — confirmed by reading the file (no GU file touched).

**ADR 0038 §E deferrals (each named, still deferred — not denied):** no remove path; no acceptance/consent step; no bulk assign; no self-assignment (refused, G-A·5, not a lane); no email notification to the assigned guardian. The **"no second audit verb"** deferral is now **implemented** (the `guardian.assign` verb) — that is the GA-AR lane's contribution. Each remaining deferral re-litigates as a future ADR 0038 amendment when it earns its keep.

<!-- Lane closed: the GA-AR lane's files are in
     done/guardian-assignment-second-audit-row/; in-progress/ is empty. -->
