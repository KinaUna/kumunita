---
id: is23-prompt
title: "IS23 prompt — Erasure: guardian, community, error, escalation, usage (S2/E2)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS23 — Erasure: guardian, community, error, escalation, usage (S2/E2)

## What you are doing

You are the **twenty-third working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS22 are committed: the
`[DataClass]` attribute + all **81/81** annotations, IS21's
`ResidentThreadPseudonymizer` (D3 messaging threads), IS22's
`ResidentAccountRowsEraser` (the identity-adjacent delete set), and the
`EveryRegisteredDocType_HasADataClass` gate (green **81/81**).

This unit closes the next slice of the **E2 gap** (account deletion is
incomplete). **IS23 extends IS22's `ResidentAccountRowsEraser`** (do not create
a new file, do not add a new composer call line — IS22's one call line already
routes through it). It adds **6 more behaviours**, a *mix* of delete and
pseudonymize:

- **Delete (the resident is the child/subject, the row is gone):**
  `GuardianTimeLimitSchedule` (child → `ChildId == resident`),
  `GuardianEventRequest` (child → `ChildId == resident`),
  `CommunityMembershipRequest` (the supervised child → `UserId == resident`).
- **Pseudonymize (the row STAYS, the principal becomes the tombstone
  `$"deleted:{residentSubjectId}"`, the `PseudonymizeAuditRowsAsync` precedent
  at `IdentityService.AccountOps.cs:479`; never delete the row):**
  `ErrorReport` → `SubjectId` → tombstone **and** `ContactEmail` → `null`;
  `EscalationToken` → `CreatedBy` → tombstone;
  `UsageEvent` → `ActorId` → tombstone.

Each type's `[DataClass]` row (annotated IS20c) is the spec — `Delete` for the
three delete rows, `Pseudonymize` with the `principalField` named for the three
tombstone rows. **Do not invent new erasure rules.** The pseudonymize shape
mirrors `ResidentThreadPseudonymizer` (load the resident's rows, rewrite the
principal field to the tombstone, `session.Store(row)` — the row is kept).

**This is the one thing to get right:** `ResidentAccountRowsEraser.EraseAsync`
gains the 3 delete sets + the 3 tombstone rewrites, still in the caller's
session (C3 — no commit; the composer's single `SaveChangesAsync` commits it),
still returning the touched-row count (an int, no PII). `ErrorReport.ContactEmail`
is the PII being *nulled* — it is **never logged** (the IS08 gate; the eraser's
log line keeps only `{Resident}` = subject id and `{AccountRows}` = int).

**Pause discipline (rules 1–9):** if a plan-named field does not exist, or the
type is on a surface the test boot does not register, **pause and record it** —
do not guess a field. Never edit around a failing test.

## Deliverables (≤ 3 files + the handoff append)

1. **`src/Kumunita.Core/Identity/ResidentAccountRowsEraser.cs`** (modify the
   existing seam). Inside `EraseAsync`, add:
   - `GuardianTimeLimitSchedule` where `ChildId == resident` → delete.
   - `GuardianEventRequest` where `ChildId == resident` → delete.
   - `CommunityMembershipRequest` where `UserId == resident` → delete.
   - `ErrorReport` where `SubjectId == resident` → **tombstone**
     (`SubjectId = $"deleted:{resident}"`) **and** `ContactEmail = null`,
     `session.Store(row)` (the row stays).
   - `EscalationToken` where `CreatedBy == resident` → **tombstone**
     (`CreatedBy = $"deleted:{resident}"`), `session.Store(row)`.
   - `UsageEvent` where `ActorId == resident` → **tombstone**
     (`ActorId = $"deleted:{resident}"`), `session.Store(row)`.

   Keep the one-seam shape and the existing log line (add the new touched rows
   to the `{AccountRows}` count; do not add a second log call).
2. **`tests/Kumunita.Core.Tests/DeleteAccountRowsTests.cs`** (append — IS22's
   4 tests stay). The IS22 boot already registers `CoreDocTypes` (which covers
   `GuardianTimeLimitSchedule` + `CommunityMembershipRequest`) — you must
   **add** `EventDocTypes.Configure(opts)` (for `GuardianEventRequest`),
   `ErrorReportDocTypes.Configure(opts)` (for `ErrorReport`),
   `EscalationDocTypes.Configure(opts)` (for `EscalationToken`), and
   `UsageDocTypes.Configure(opts)` (for `UsageEvent`).

   **⚠️ PII gotcha (the IS08 gate):** the test seeds an `ErrorReport` with a
   `ContactEmail`. That is *stored fixture data*, not a log template — the IS08
   gate scans log calls in `src/**/*.cs`, not test data. Keep it clearly
   synthetic (`a@example.test` is fine as a *stored value*; it is the
   "never log" rule that matters) and never log it.

**The 3 tests (plan §IS23, exact names — all 6 behaviours covered across them):**

| Test | Asserts |
|---|---|
| `DeleteAccount_ChildGuardianRows_Removed` | the **3 child-scoped deletes** are **gone** (fresh `LoadAsync` → null): `GuardianTimeLimitSchedule` + `GuardianEventRequest` (where the resident is the child) and `CommunityMembershipRequest` (where the resident is the supervised child); a row where the resident is the *other* party survives |
| `DeleteAccount_ErrorReport_Pseudonymized_ContactEmailCleared` | the resident's `ErrorReport` row **stays** with `SubjectId == "deleted:{resident}"` **and** `ContactEmail == null`; an `ErrorReport` for a different `SubjectId` is untouched |
| `DeleteAccount_UsageEvents_Pseudonymized` | the **two principal-tombstone** rows **stay** with the tombstone applied: `UsageEvent` with `ActorId == "deleted:{resident}"` and `EscalationToken` with `CreatedBy == "deleted:{resident}"`; a `UsageEvent` for a different `ActorId` is untouched |

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS22 is the latest improve-sec lane commit** (`## IS22` handoff section +
  `is22-prompt.md`). `ResidentAccountRowsEraser.cs` exists (the IS22 delete
  seam, 6 sets); `DeleteAccountRowsTests.cs` exists (IS22's 4 tests, boot
  registers `CoreDocTypes` + `MessagingDocTypes` + `GroupMessagingDocTypes` +
  `MediaDocTypes` + `SessionDocTypes` + `NotificationDocTypes`).
- `grep -rc "\[DataClass(" src --include="*.cs"` → **81**.
- **Field names (verify before you query):** `GuardianTimeLimitSchedule.ChildId`
  · `GuardianEventRequest.ChildId` · `CommunityMembershipRequest.UserId` (the
  supervised child) · `ErrorReport.SubjectId` + `.ContactEmail` ·
  `EscalationToken.CreatedBy` · `UsageEvent.ActorId`.
- **Pinned-identity note:** `GuardianTimeLimitSchedule` pins its identity to
  `ChildId` (no `Id` field) in `CoreDocTypes` — it is already registered by the
  IS22 boot, so no new registration gotcha; the other 5 types have conventional
  `Id` fields. (The IS22 harness gotcha — see the `## IS22` handoff — does not
  recur here because no new pinned-identity type is queried.)

## Verification (all green before you finish)

- [ ] `dotnet build` (solution) → **0 errors**.
- [ ] `DeleteAccountRowsTests` — all **7** green (IS22's 4 + the 3 new) via the
      xunit.v3 in-process runner (`/memories/repo/xunit-v3-inprocess-runner-syntax.md` —
      `dotnet exec …Kumunita.Core.Tests.dll -class "…DeleteAccountRowsTests"`).
- [ ] The 5 delete-composer regression classes still green
      (`DeleteAccountThreadTests`, `IdentityServiceDeleteLaneGdprTests`,
      `IdentityServiceDeleteAccountTests`, `IdentityServiceDeleteChildAccountTests`,
      and `DeleteAccountRowsTests`) — no drift.
- [ ] `grep -rc "\[DataClass(" src --include="*.cs"` → still **81** (no new
      annotations; IS23 only extends the eraser).
- [ ] No PII in code/tests/notes; the eraser's log line carries only
      `{Resident}` (subject id) + `{AccountRows}` (int); `ContactEmail` is
      nulled, never logged.
- [ ] One `## IS23` handoff section (files, the 3 deletes + 3 tombstones, the
      3 tests + pass count, the 4 surfaces added to the boot, `grep -c` count
      still 81, next-unit notes). One commit (≤ 3 deliverable files).

## Definition of done (IS23)

- [ ] `ResidentAccountRowsEraser.EraseAsync` covers all 6: the 3 child-scoped
      deletes (`GuardianTimeLimitSchedule`/`GuardianEventRequest`/
      `CommunityMembershipRequest`) + the 3 tombstones
      (`ErrorReport.SubjectId` + `ContactEmail` nulled, `EscalationToken.CreatedBy`,
      `UsageEvent.ActorId`); the tombstone rows **stay**, the delete rows are gone.
- [ ] The 3 new `DeleteAccountRowsTests` green; IS22's 4 + the 5 regression
      classes still green; `dotnet build` → 0 errors; `[DataClass]` count 81.
- [ ] No PII in code/tests/notes; no new composer call line, no new file
      (extend the IS22 seam); the `## IS23` handoff section landed.
- [ ] **Next unit: IS24** (Erasure and export agree with the attributes — a
      `public static IReadOnlySet<Type> HandledTypes` on
      `ResidentContentEraser`, `ResidentThreadPseudonymizer` and
      `ResidentAccountRowsEraser`, plus the `DataClassCoverageTests`
      `EveryDeleteOrPseudonymizeType_IsHandledByAnEraser` /
      `EveryResidentExportType_IsInUserScopeInventory` / `NoHandledType_IsMarkedKeep`
      gates — IS22 + IS23's seams are exactly what that gate will list).
