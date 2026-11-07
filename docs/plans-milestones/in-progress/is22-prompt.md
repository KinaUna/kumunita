---
id: is22-prompt
title: "IS22 prompt — Erasure: identity-adjacent rows (S2/E2)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS22 — Erasure: identity-adjacent rows (S2/E2)

## What you are doing

You are the **twenty-second working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS21 are committed: the
`[DataClass]` attribute + all **81/81** type annotations (IS16 + IS17–IS20, with
IS21 closing the last two pauses), IS21's `ResidentThreadPseudonymizer` (the D3
rule for messaging threads), and the red→green `EveryRegisteredDocType_HasADataClass`
gate (now **81/81**).

This unit closes the next slice of the **E2 gap** (account deletion is
incomplete — of 65 principal-bearing doc types, 25 were not erased). IS21
handled the *messaging threads*. **IS22 handles the identity-adjacent rows** —
the per-resident state that F1's §2C "decision" rows left undecided:
`SessionDoc`, `IdentityToken`, `GuestAccess`, `DelegationGrant`, the **4
notification types**, and `OutboxEmail`. Each `[DataClass]` row for these types
was annotated in IS20b/IS20c and says exactly `Delete` — read them, they are
the spec. Do not invent new erasure rules.

**This is the one thing to get right:** a new eraser seam (one public method)
that deletes the resident's rows by owner field, wired in with **one call line**
in the composer (`DeleteCoreAsync`), committed by the composer's single
`SaveChangesAsync` (C3 — no commit in the eraser). The resident's **email** is
needed for the `OutboxEmail` match and is **passed in** by the composer — it is
**never logged** (the IS08 log-PII gate applies; see the gotcha below).

**Pause discipline (rules 1–9):** if a POCO field the plan names does not
exist, or `DeleteCoreAsync` does not already carry `target.Email` in scope,
**pause and record it** — do not guess a field. Never edit around a failing
test.

## Deliverables (≤ 3 files + the handoff append)

1. **`src/Kumunita.Core/Identity/ResidentAccountRowsEraser.cs`** (new, one
   public seam). Given the resident's subject id + the resident's email + the
   caller's `IDocumentSession` (no commit — C3, the composer commits), delete:
   - **`SessionDoc`** where `UserId == resident` (the session credential —
     IP + user agent, one per login).
   - **`IdentityToken`** where `UserId == resident` (verify/setup tokens).
   - **`GuestAccess`** where `SubjectId == resident`.
   - **`DelegationGrant`** where the resident is **either** side —
     `OwnerId == resident` **or** `DelegateId == resident` (both the grants
     they made and the grants they hold).
   - **The 4 notification types** where `RecipientId == resident`:
     `Notification`, `NotificationPreference`, `NotificationSubscription`,
     `NotificationQuietSchedule`.
   - **`OutboxEmail`** where `Recipient == residentEmail` (the recipient is the
     email string, not an id). The email is **passed in** as a parameter — the
     eraser never reads it from a doc and never logs it.

   Return the touched-row count (for the log summary, `{AccountRows}`) — an
   int, no PII.
2. **`src/Kumunita.Core/Identity/IdentityService.AccountOps.cs`** — add **one
   call line** in `DeleteCoreAsync` (anchor: right after the step-8c
   `ResidentContentEraser.EraseAsync` call, ≈ :381), passing
   `(session, targetSubjectId, target.Email, ct)`. `target` is the loaded
   `User` and already carries `Email` (verify — the plan says so). Do not
   restructure the method; the composer's single `SaveChangesAsync` commits it.
3. **`tests/Kumunita.Core.Tests/DeleteAccountRowsTests.cs`** (new, 4 tests —
   names below). Use the `IdentityServiceDeleteLaneGdprTests` fixture shape
   (`PostgresFixture`, `BootAsync`, `SeedAccountAsync`,
   `boot.Identity.DeleteAccountAsync`). The reference test's boot already
   registers `CoreDocTypes` (which covers `DelegationGrant`, `IdentityToken`,
   `GuestAccess`, `OutboxEmail`) — you only need to **add**
   `SessionDocTypes.Configure(opts)` (for `SessionDoc`) and
   `NotificationDocTypes.Configure(opts)` (for the 4 notification types).

   **⚠️ PII gotcha (the IS08 gate):** the test seeds a `SessionDoc` with an
   `Ip` and `UserAgent`. Those are PII, but they are *stored values in a test
   fixture*, not a log template — the IS08 gate scans **log calls** in
   `src/**/*.cs`, not test data. Still, keep them clearly synthetic
   (`10.0.0.42`, `test-agent`) and never log them.

**The 4 tests (design §2.3, exact names):**

| Test | Asserts |
|---|---|
| `DeleteAccount_RemovesSessions` | the resident's `SessionDoc` row is gone (fresh-session `LoadAsync` returns null) |
| `DeleteAccount_RemovesTokensAndGuestAccess` | the resident's `IdentityToken` + `GuestAccess` rows are gone |
| `DeleteAccount_RemovesDelegations_BothSides` | a `DelegationGrant` where the resident is `OwnerId` **and** one where they are `DelegateId` are both gone; a grant between two *other* residents survives |
| `DeleteAccount_RemovesNotificationsAndOutbox` | all 4 notification types for the resident are gone; the `OutboxEmail` with `Recipient == resident's email` is gone; an `OutboxEmail` to a *different* recipient survives |

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS21 is the latest lane commit** (`## IS21` handoff section + the
  `is21-prompt.md`). `grep -rc "\[DataClass(" src --include="*.cs"` → **81**.
  `DataClassCoverageTests.EveryRegisteredDocType_HasADataClass` → **green
  81/81**. `ResidentThreadPseudonymizer.cs` exists; `ResidentContentEraser.cs`
  routes the messaging set through it.
- **There is no `ResidentAccountRowsEraser.cs` yet** — that is your new file.
- Field names (verify before you query, the IS18 rule for `Report`):
  `SessionDoc.UserId` · `IdentityToken.UserId` · `GuestAccess.SubjectId` ·
  `DelegationGrant.OwnerId`/`.DelegateId` (in `UserInfo/Group.cs` ≈ :87) ·
  the 4 notification types all use `RecipientId` · `OutboxEmail.Recipient`
  (the **email**, not an id — it is a string the composer passes in).

## Verification (all green before you finish)

- [ ] `dotnet build` (solution) → **0 errors**.
- [ ] `DeleteAccountRowsTests` — all **4** tests green (the xunit.v3 runner
      form is in `/memories/repo/xunit-v3-inprocess-runner-syntax.md` —
      `dotnet exec …Kumunita.Core.Tests.dll -class "…DeleteAccountRowsTests"`).
- [ ] `IdentityServiceDeleteLaneGdprTests` + `IdentityServiceDeleteAccountTests`
      still green (no drift from the new call line — they must pass exactly as
      before; the eraser only *adds* deletes, it does not remove any).
- [ ] `grep -rc "\[DataClass(" src --include="*.cs"` → still **81** (this unit
      adds no annotations; it only adds the eraser).
- [ ] No PII in code/tests/notes (`user-a`, `a@example.test`); the new log line
      (if any) carries only `{AccountRows}` (an int) and `{Resident}` (the
      subject id — an id, not PII, per the IS08 rule).
- [ ] One `## IS22` handoff section (files, the new seam, the 4 tests + pass
      count, the anchor line in `DeleteCoreAsync`, `grep -c` count still 81,
      next-unit notes). One commit (≤ 3 deliverable files).

## Definition of done (IS22)

- [ ] `ResidentAccountRowsEraser` lands (one seam deleting the 6 identity-adjacent
      type sets by owner field; `OutboxEmail` matched on the passed-in email);
      one call line in `DeleteCoreAsync` after the step-8c eraser call.
- [ ] The 4 `DeleteAccountRowsTests` green; `IdentityServiceDeleteLaneGdprTests`
      + `IdentityServiceDeleteAccountTests` still green; `dotnet build` → 0 errors.
- [ ] No PII in code/tests/notes; no behaviour beyond the 6 deletes + the call
      line; the `## IS22` handoff section landed.
- [ ] **Next unit: IS23** (Erasure: guardian, community, error, escalation,
      usage — `GuardianTimeLimitSchedule` + `GuardianEventRequest` where the
      resident is the child, `CommunityMembershipRequest`, `ErrorReport`
      pseudonymized, `EscalationToken.CreatedBy` tombstoned, `UsageEvent.ActorId`
      tombstoned; extend `ResidentAccountRowsEraser.cs`).
