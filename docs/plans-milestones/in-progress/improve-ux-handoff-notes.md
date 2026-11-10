---
id: improve-ux-handoff-notes
title: "IMPROVE-UX — Handoff Notes"
tier: living
kind: handoff
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/adr/0302-shared-kiosk-link.md
  - docs/adr/0301-self-service-deletion-grace-period.md
  - docs/adr/0142-delete-account.md
  - docs/adr/0028-guardian-controls-account-scope-supervision.md
  - docs/adr/0143-guardian-delete-child.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** the running handoff log for the **IMPROVE-UX** lane — each
> unit appends its section here as it ships, so a successor agent (or the
> X02 cold read) gets the honest before → after, not the intended one. This
> note opens with **R15**, the ADR 0302 shared-kiosk-link Core seam
> (the `KioskLink` doc + the `IKioskLinkService` create/revoke/validate
> service + the P-1 gate count 82 → 83, the `// TODO ❓` open questions
> for R19, and the R16 notes), then **R14**, the SEC review of the ADR 0301
> grace period (R10–R13): the four review foci (audit rows, re-auth before
> scheduling, the child rule, guard ordering + the scheduling-vs-execution
> asymmetry) each with its verdict, the two ADR 0301 `// TODO ❓` open
> questions resolved, and the Findings list (each filed with an owner).
> **Who is it for:** the next agent in this lane and the X02 cold read.
> **When to read the rest:** before starting a unit, or when a DoD line
> "looks done" and you want to know if it actually is.

# IMPROVE-UX — handoff notes

## R15 — ADR 0302 + KioskLink doc + create/revoke/validate service

**Scope:** the Core seam + ADR for the shared kiosk link (IMPROVE-UX R15 —
the first kiosk unit, a new lane). The Web surface (R16–R18) and the SEC
review (R19) are separate later units and were **not** started.

**Outcome:** ADR 0302 + the `KioskLink` doc + the
`IKioskLinkService`/`KioskLinkService` + the DI registration + 8 new Core
tests, all in one `feat(ux):` commit (`8de9751a`). The personal kiosk
(0164/0285) is unchanged — R15 is purely additive.

### What landed

- **ADR 0302** — the shared-kiosk-link decision. Six decisions:
  - **D1** — the `KioskLink` doc: `Id`, `PlaylistId`, `OwnerSubjectId`,
    `TokenHash` (SHA-256 hex digest only — **never** the raw token),
    `CreatedAt`, `LastUsedAt?`, `RevokedAt?`. Registered on the existing
    `CoreDocTypes` surface (the ADR 0004 §B.1 additive precedent — no DDL,
    no new `*DocTypes`). Carries `[DataClass(Secret, Delete, Never)]`
    (the ADR 0298 SecGate rule).
  - **D2** — token shape: `kskl_` + 32 CSPRNG bytes base64url (256 bits,
    the 0159 `kesc_` shape), shown exactly once at creation, stored only
    as the SHA-256 hex digest (the 0159 ESC·3 / F2 hash-at-rest rule).
  - **D3** — `IKioskLinkService` (3-method seam, pure Core — the R16
    middleware can call it): `CreateAsync(playlistId, ownerSubjectId)`,
    `RevokeAsync(linkId, ownerSubjectId)`, `ValidateAsync(tokenHash,
    ownerSubjectId)`. Audit: one `kiosk.link.create` + one
    `kiosk.link.revoke` row each (`Via: Owner`, `TargetKind = "kiosk"`);
    `ValidateAsync` writes **zero** rows (the M35·5 "read = config load"
    floor) and stamps `LastUsedAt` on a match (the ESC·9 read-then-write).
  - **D4** — invariants the R16–R18 units build against: revocable,
    read-only, `/kiosk` + slides only, community-visible items only,
    audited under the owner as via-kiosk-link.
  - **D5** — the personal kiosk (0164/0285) and all its invariants stand
    unchanged.
  - **D6** — open questions (the R19 SEC review settles): (a) token length
    (proposed 256 bits), (b) expiry (none in this ADR), (c) max links per
    playlist (no ceiling in this ADR).
- **Core files** (all new or modified in `src/Kumunita.Core/`):
  - `Kiosk/KioskLink.cs` — the `[DataClass]` doc.
  - `Kiosk/IKioskLinkService.cs` — the 3-method seam.
  - `Kiosk/KioskLinkService.cs` — the impl (CSPRNG token, SHA-256 hash at
    rest via `IdentityService.Sha256Hex`, audit rows, `ValidateAsync`
    zero-audit + `LastUsedAt` stamp, owner-scoped `RevokeAsync`).
  - `CoreDocTypes.cs` — `opts.Schema.For<Kiosk.KioskLink>()` added (after
    the `DeletionRequest` registration).
  - `Kiosk/DependencyInjection.cs` — `IKioskLinkService` →
    `KioskLinkService` registered.
- **Tests** — `tests/Kumunita.Core.Tests/Kiosk/KioskLinkServiceTests.cs`
  (8 tests, `KioskLinkServiceTests` class):
  1. `ADR0302_Create_ReturnsRawTokenOnce_AndStoresRow`
  2. `ADR0302_Store_HoldsHashNotRawToken` — the "hash only at rest" pin
  3. `ADR0302_Validate_AnotherOwnersLink_IsRefused` — owner-scope pin
  4. `ADR0302_Validate_UnknownToken_IsRefused`
  5. `ADR0302_Validate_RevokedToken_IsRefused`
  6. `ADR0302_Validate_MatchesOnlyValidOwnLink` — the full matrix
  7. `ADR0302_Revoke_OwnerScoped_Idempotent_Audited` — audit-row pin
  8. `ADR0302_Validate_WritesNoAuditRow_StampedLastUsed` — zero-audit +
     `LastUsedAt` pin
- **P-1 gate update** — `DataClassCoverageTests` (Core.Tests): registered-
  type count raised 82 → 83 for the new `KioskLink` row.

### Test results

- `KioskLinkServiceTests` (8) — **all green**
- `KioskServiceTests` (6) — **all green** (the existing `KioskPlaylist`
  seam is intact)
- `DataClassCoverageTests` (1) — **green** (83/83 after the count update)
- `dotnet build` — **0 errors**
- Translation parity — **unaffected** (R15 adds no localized keys)

### `// TODO ❓` open questions flagged for R19's SEC review

1. **Token length** — D2 proposes 256 bits (32 CSPRNG bytes). The R19
   SEC review may adjust.
2. **Expiry** — this ADR ships no expiry (a link lives until revoked).
   Whether a long-lived shared credential needs a max age is a SEC call.
3. **Max links per playlist** — this ADR ships no ceiling. The R18
   settings surface can list and revoke them; whether a hard limit is
   needed is a SEC call.

### Notes for R16

- The `ValidateAsync` seam takes the **hash** (not the raw token). The
  R16 middleware must hash the presented token with the same
  `IdentityService.Sha256Hex` algorithm before calling it (the
  `FindTokenAsync` local-constant pattern — the digest is a query
  constant, never inside a `.Where(...)` expression).
- The `KioskLinkService` does **not** carry `IAuthorizationService` —
  the owner-scope check is data-scoped (the `OwnerSubjectId` field), not
  a `Decide()` branch. The R16 restricted principal (owner + `kiosk-link`
  claim) is a Web-side concern.
- `LastUsedAt` is stamped on every successful validate — the R18 surface
  reads this field for the "last used" display.

## R14 — SEC review of the ADR 0301 grace period (R10–R13)

**Scope reviewed:** the ADR [0301](../../adr/0301-self-service-deletion-grace-period.md)
grace-period lane — R10 (`5218c9de`, the ADR), R11 (`ce7ad89d`,
`DeletionRequest` doc + `IDeletionScheduler`/`DeletionScheduler`), R12
(`269693e7`, the `DeletionDueTick`/`DeletionDueHandler` due-job) and R13
(`71827dc2`, the `/account/delete` Web surface). R14 is a **review**, not an
implementation: R10–R13 are preserved, not rewritten.

**Outcome:** **all four foci pass** (or are by-design decisions R14 records),
**both ADR 0301 open questions resolve to "confirm as written,"** and **four
observations are filed** with owners (three LOW, one MEDIUM — the
admin-removal path leaves a child's `GuardianLink` row dangling, a real gap
but a pre-existing ADR 0142 issue, not something R10–R13 introduced). None
required a code change in this unit, so per the R14 prompt the build was
**not** run — a scope slip to avoid. No finding is left without an owner.

### Focus 1 — Audit rows — **PASSES**

ADR 0301 D1 says the self-serve audit shape is "exactly one `AccessAudit`
row, action `account.delete`, `Via: Owner`." The lane is now **two phases**,
so the check is "one row **per phase**, distinct, PII-free" — verified:

- **Distinct verbs.** The schedule phase writes exactly one
  `account.delete.scheduled` row (`DeletionScheduler.ScheduleDeletionAsync`,
  the `IdentityService.AuditRow` store at `DeletionScheduler.cs:147`); the
  execution phase writes exactly one `account.delete` row
  (`IdentityService.AccountOps.cs` `DeleteCoreAsync` step 9, `AuditRow(…, "account.delete", …)`).
  The two facts (the request and the deletion) are separate rows with
  distinct `Action` values — the "audit-of-the-lane" shape `DeleteCoreAsync`
  already uses.
- **`Via` correct per phase.** Both rows are `Via: Owner` on the self-serve
  branch (actor == target == the resident's subject id) — the schedule row
  is the resident acting on their own account; the execution row is the
  same self-serve branch run by the due-job (`DeletionDueHandler` calls
  `DeleteAccountAsync(row.SubjectId, adminSubjectId: row.SubjectId)`).
- **No PII in any log/audit template (invariant P-3).** Both rows carry only
  id-shaped values (actor id, target id `account:{subjectId}`, a `Guid` row
  id). The only log templates in the lane use `{Resident}`
  (`DeletionScheduler.cs:155`) and `{ResidentId}` (`DeletionDueHandler.StopRefusedRowAsync`)
  — neither is a bare PII word, so the IS08
  `LogTemplatePiiGateTests.NoLogTemplate_CarriesAPiiPlaceholder` rule
  (PII iff the placeholder is exactly `Email`/`Ip`/`Phone`/`Address`/
  `Recipient`/`Body`) does not flag them.

### Focus 2 — Re-auth before scheduling / cancelling — **PASSES**

Both write lanes verify the resident's current password **before** the
`IDeletionScheduler` seam is called, and the scheduled-state branch is
read-only:

- **`POST /account/delete`** (`AccountController.Delete(model)`): the
  order is `model.Confirmed` check → `userManager.FindByIdAsync` +
  `CheckPasswordAsync` → `if (!ModelState.IsValid) return View(model)` →
  `schedulerRef.ScheduleDeletionAsync(subject)`. The password guard and the
  `[ValidateAntiForgeryToken]` guard both precede the seam; a wrong password or
  unchecked box re-renders before the seam is reached.
- **`POST /account/delete/cancel`** (`AccountController.DeleteCancel(model)`):
  the same order — `Confirmed` → `CheckPasswordAsync` →
  `if (!ModelState.IsValid) return` → `schedulerRef.CancelDeletionAsync`.
- **Not bypassed by the scheduled-state branch.** The "deletion scheduled
  for {date}" state is rendered only by the **GET** `Delete()` (no-arg),
  which is a read (the `LoadPendingDeletionAsync` `QuerySession` read); it
  performs **no write** and reaches no seam. Both write lanes are the
  `POST` handlers, each gated as above. There is **no path** that reaches
  `ScheduleDeletionAsync`/`CancelDeletionAsync` without the
  `CheckPasswordAsync` re-auth.

### Focus 3 — Child rule — **PASSES**

- **R11 enforces it at scheduling.** `DeletionScheduler.ScheduleDeletionAsync`
  step 3 queries `GuardianLink` for `ChildId == subjectId &&
  Status == GuardianLinkStatus.Active` and throws
  `UnauthorizedAccessException` if an active row exists; a `Dissolved` /
  absent link does **not** block (only `Active` rows confer standing,
  ADR 0028). The check fires **before any write** (after the existence
  check + the last-GlobalAdmin pin, before the `Store` of the
  `DeletionRequest` row).
- **R13 renders the distinct notice.** `AccountController.Delete(model)`
  `catch (UnauthorizedAccessException)` surfaces
  `account.delete_child_refused` + `DeleteAccountViewModel.IsChildRefused`
  (the "contact a parent/guardian" text) — a **distinct** notice from the
  removed 0142 D5 `SelfDeletionRefused` text. The R11 seam's English
  fallback is the floor; the localized key is what the resident sees.
- **The removed keys are gone.** `account.delete_refused` and
  `SelfDeletionRefused` appear in **no** code file and **no** locale file
  (only in plan/prompt prose, which is expected). The new `account.*`
  keys (`delete_child_refused`, `flash_scheduled`, `flash_cancelled`,
  `delete_scheduled_*`, `delete_cancel*`) carry no PII in all four locales
  (en/de/fr/da).

### Focus 4 — Guard ordering + the scheduling-vs-execution asymmetry — **DECIDED (acceptable at HEAD)**

R11 enforces **both** the last-GlobalAdmin pin and the child rule
**at scheduling**; the R12 due-job re-checks the last-GlobalAdmin pin
**at execution** (it runs the existing `DeleteAccountAsync` self-serve
branch → `DeleteCoreAsync`, whose first step is the pin), but the **child
rule does not re-fire at execution**. This asymmetry is the `// TODO ❓`
R13 left.

**R14 decision: the asymmetry is acceptable and by-design.** ADR 0301 D2
explicitly scopes the execution re-check to the last-GlobalAdmin pin ("the
scheduled-execution path re-checks the pin at the execution instant"); ADR
0301 D3 explicitly scopes the child rule to *scheduling* time ("refused at
scheduling time"). The reasoning:

1. **ADR 0028 G·4 makes `GuardianLink` formation creation-based** — "there
   is no self-serve 'claim guardianship over an existing account' lane."
   So a new `Active` link cannot appear over an already-existing account;
   the only way a link is over a scheduled-for-deletion account is that it
   existed *before* scheduling (which the R11 check would have refused) or
   was deliberately re-attached (a low-probability, intentional guardian
   act — ADR 0028 B: "re-attaching is a fresh `Active` row — a deliberate
   new act").
2. **The last-GlobalAdmin pin is re-checked because *role changes* are a
   supported mid-window action** (an admin can promote/demote a resident
   mid-grace-window), unlike a creation-based link. Re-checking the pin at
   execution is therefore the correct asymmetry, not an oversight.
3. **The R12 handler is already defensively covered** — its
   `catch (UnauthorizedAccessException)` (`DeletionDueHandler.Handle`)
   handles a future child-rule-at-execution refusal without infinite retry
   (moves the row out of `Pending` via `StopRefusedRowAsync`), so a later
   change that adds a child-rule gate to the execution path is already
   safe-netted.

**Filed (F4):** the forward-compat caveat — see Findings.

### ADR 0301 open questions — **RESOLVED**

- **`// TODO ❓` Grace-period length — CONFIRMED: 14 days.**
  `IDeletionScheduler.GracePeriodDays = 14` (the single `const`). 14 days
  is a sound cancellation window: long enough to catch a change of heart,
  short enough not to leave a "ghost account" indefinitely. **No change**
  (the number was not frozen by the ADR; R14 confirms it, so it now stands).

- **`// TODO ❓` Child rule on the admin-removal path — CONFIRMED as the
  ADR 0028 G·5 safety-valve workflow, with one gap filed (F2).** ADR 0301
  D3 states "a `GlobalAdmin` cannot delete a child with an active
  `GuardianLink` either — dissolve the link first." R14 reads that as
  **workflow guidance, not a code gate**: the GlobalAdmin-removal path
  (`IdentityService.AccountOps.cs` `DeleteAccountAsync` admin branch,
  `RequireGlobalAdminAsync` + `DeleteCoreAsync`) is the **ADR 0028 G·5
  safety valve** — "a `GlobalAdmin` may dissolve any active link … a
  mistreated child can always reach it" — and is deliberately **not**
  hard-gated on the child rule (the code has no active-link check on that
  branch). "Dissolve first" is the recommended safe workflow and is
  available (a GlobalAdmin can dissolve any active link via G·5, then
  delete). A hostile guardian **cannot** shield a child, because G·5 grants
  the GlobalAdmin the power to dissolve the link. **Confirmed as intended.**
  *Gap found while confirming:* the admin branch passes **no**
  `dissolveGuardianLinksForChildAsync` callback to `DeleteCoreAsync` (only
  the ADR [0143](../../adr/0143-guardian-delete-child.md) `DeleteChildAccountAsync`
  guardian-delete lane supplies one), so a GlobalAdmin removing a child
  **without** dissolving first leaves the child's `GuardianLink` row
  undissolved — a dangling-standing gap that ADR 0143 C·5 guards against for
  the guardian path but not the admin path. **Filed (F2)**.

### Findings

Each is **filed** (not fixed in this unit), with a named owner. Three are
LOW (model-honesty / wording / forward-compat); F2 is **MEDIUM** (a real
dangling-standing gap, but a pre-existing ADR 0142 admin-branch issue, not
something R10–R13 introduced). None blocks the lane; none requires a code
change in R14.

- **F1 — Execution-refusal state reuse (model honesty).**
  *Where:* `DeletionDueHandler.StopRefusedRowAsync` (R12) — moves a
  permanently-refused row out of `Pending` by calling
  `CancelDeletionAsync`, so "the resident signed in (the cancel)" and
  "the system refused at execution" both land on the `Cancelled` state.
  *Severity:* LOW (the `LogWarning` line distinguishes the reason —
  `"last-GlobalAdmin pin (0142 D3) or account no longer present"` vs.
  `"standing refusal (child rule, ADR 0301 D3)"` — so the trail is
  readable; the only cost is the imprecise state name).
  *R14 verdict:* **keep the reuse at HEAD** (the ADR 0301 D2 "no extra
  form" principle is honored; the log distinguishes the reason). The R12
  `// TODO ❓` explicitly defers the split decision to R14 — R14 decides:
  **do not split in this unit.**
  *Owner:* a follow-up IMPROVE-UX unit that adds a distinct
  `RefusedAtExecution` state to `DeletionRequestStatus`
  (R11's `DeletionRequest.cs`) + the R12 handler + a Web render branch.
  *(Touches R11's data model — beyond R14's "targeted change, not a
  redesign" scope.)*

- **F2 — ADR 0301 D3 wording + the admin-removal path leaves a child's
  `GuardianLink` row dangling.**
  *Where:* (a) ADR 0301 D3 — "a `GlobalAdmin` cannot delete a child with an
  active `GuardianLink` either — dissolve the link first" reads as a code
  gate but is workflow guidance (a future reader may expect a check on the
  GlobalAdmin-removal path that is **not** there — and correctly isn't, per
  ADR 0028 G·5). (b) `IdentityService.AccountOps.cs` `DeleteAccountAsync`
  admin branch — passes **no** `dissolveGuardianLinksForChildAsync` callback
  to `DeleteCoreAsync` (only the ADR 0143 `DeleteChildAccountAsync`
  guardian-delete lane supplies one). The eraser
  (`ResidentAccountRowsEraser.cs`) handles `GuardianTimeLimitSchedule`,
  `GuardianEventRequest`, and `CommunityMembershipRequest` for the deleted
  child, but **not** the `GuardianLink` row itself — so a GlobalAdmin
  removing a child *without* dissolving first leaves the child's
  `GuardianLink` row dangling with a dead `ChildId`. This is a real gap that
  ADR 0143 C·5 guards against for the guardian path ("no dangling standing:
  dissolve every `GuardianLink` row for the deleted child (any status) in
  the same session as the core's writes, so a co-guardian's row does not
  dangle pointing at a deleted account") but not for the admin path.
  *Severity:* **MEDIUM** (a real dangling-standing gap: the `GuardianLink`
  row dangles with a dead `ChildId`, the standing G·2/G·3 live-check reads
  an orphaned row. Not a privacy violation — the link row carries no PII
  beyond two id-shaped values — but a data-hygiene + standing-integrity gap
  that ADR 0143 C·5 explicitly guards against for the guardian path).
  *R14 verdict:* **confirmed as a gap, not a defect of R10–R13** (it is a
  pre-existing ADR 0142 admin-branch issue, not something R10–R13
  introduced). R14 does not fix it (a seam-contract change beyond R14's
  targeted scope) but files it.
  *Owner:* the SEC lane / a follow-up ADR amendment — (i) reword ADR 0301
  D3 to make explicit that the GlobalAdmin-removal path is the G·5 safety
  valve (dissolve-first is the safe workflow, not a hard gate); (ii) decide
  whether the admin-removal branch should pass the
  `dissolveGuardianLinksForChildAsync` callback (mirror ADR 0143 C·5) so a
  child's `GuardianLink` row does not dangle, or add an explicit
  `GuardianLink` dissolution step to the eraser for the admin-removal path.

- **F3 — Last-GlobalAdmin pin message is hard-coded English.**
  *Where:* `AccountController.Delete(model)` `catch (InvalidOperationException
  ex)` → `ModelState.AddModelError(string.Empty, ex.Message)` — shows the
  R11 seam's English message verbatim to a resident who is the last
  GlobalAdmin, unlike the child-rule notice (which is localized via
  `account.delete_child_refused`). The R13 comment defers this to R14
  ("a localized version is the R14 SEC-review surface").
  *Severity:* LOW (a rare case — only the last GlobalAdmin — and the
  English message is clear).
  *R14 verdict:* **file, do not fix in this unit.** The correct fix needs
  the R11 seam to throw a **distinct** last-GlobalAdmin exception type (so
  the controller maps it to a localized key); a naive "just localize the
  catch" is unsafe because that `catch (InvalidOperationException)` lumps
  the last-GA pin and the defensive existence check. Splitting the
  exception type is a seam-contract change beyond R14's targeted scope.
  *Owner:* a follow-up unit that (i) splits the last-GA
  `InvalidOperationException` into a distinct type in R11's
  `DeletionScheduler`/`IDeletionScheduler`, (ii) adds
  `account.delete_last_globaladmin` to en/de/fr/da, and (iii) maps the
  R13 catch. *(Adjacent to the UI-refresh lane's hard-coded-English
  ownership per the plan's "Not in this lane"; the seam split is an
  IMPROVE-UX follow-up.)*

- **F4 — Forward-compat: the child rule is not re-checked at execution.**
  *Where:* `DeletionDueHandler.Handle` runs the self-serve
  `DeleteAccountAsync` branch, which (per ADR 0301 D3) does **not**
  re-check the child rule at execution.
  *Severity:* LOW (no current path is affected — see Focus 4, where R14
  confirms the asymmetry is acceptable at HEAD).
  *R14 verdict:* **acceptable at HEAD** (ADR 0028 G·4 makes link formation
  creation-based; the R11 fail-closed check at scheduling is the primary
  guard; the R12 handler already has the defensive
  `catch (UnauthorizedAccessException)`).
  *Owner:* ADR 0028 amendment / the SEC lane — **if** a mid-window
  `GuardianLink`-formation lane is ever added (a "claim guardianship over
  an existing account" or a supported re-attach), the R12 execution path
  must re-check the child rule (or the R11 scheduling check must be
  duplicated at execution).

### DoD line 8 — honest before → after (R14)

- **Code defects found: 0.** All four foci pass (Focus 4 is a by-design
  decision R14 records, not a defect); both ADR 0301 open questions
  confirmed as written.
- **Code changed in R14: none** (review only — per the R14 prompt, the
  build was **not** run, to avoid a scope slip).
- **Findings filed (all with owners): 4** — F1 (LOW, execution-refusal
  state reuse), F2 (**MEDIUM**, ADR 0301 D3 wording + the admin-removal
  path leaves a child's `GuardianLink` row dangling — a pre-existing ADR
  0142 issue, not something R10–R13 introduced), F3 (LOW, last-GA pin
  English message), F4 (LOW, forward-compat child-rule re-check).
- **Nothing held silently** — every observation is in the Findings list
  above with a named owner.
