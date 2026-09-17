# Guardian assignment, second audit row (`GA-AR`) — the conferral audit lane — sealed unit register

> **The primary tier** of the GA-AR lane's three-tier contract is the
> **pinned contract** below (the exact `AssignGuardianLinkAsync` seam, the
> `GuardianController.Assign` one-line switch, the invariants S·1–S·6, the
> FACES, the pinned test names, the acceptance gate, and the drift-guard).
> The **scratch** is
> `docs/plans-milestones/in-progress/guardian-assignment-second-audit-row-handoff-notes.md`
> (one `##` section per unit, appended in order). This register and the unit
> plans are **additive** — they do not touch any GU file or any GA file
> beyond the two named Web-test edits and the two doc appends.
>
> **ADR 0038 §Amendment (2026-09-17, second)** records this lane (U02 authors
> it). The GU lane (ADR 0028) is already shipped and closed; the GA lane
> (ADR 0038, §Amendment 2026-09-17) is closed and its named legibility
> limitation is **what this lane resolves**. This lane is **a named lane
> (`GA-AR`), not a milestone letter** — the same convention as `GA`, `GP`
> (ADR 0013), `ML` (ADR 0005), `GU` (ADR 0028). **M4/M5/M6 stay Events /
> Projects / Portability.** The roadmap trio (README / `Milestones.cs` /
> `MilestonesTests.cs`) is **untouched** — `GA-AR` is an audit-legibility
> refinement of a shipped lane, not a new roadmap item (the GU/GA lane
> discipline: the roadmap records shipped surfaces, not their internal
> refinement).

## Understanding

The GA lane (ADR 0038) shipped `GuardianController.Assign`, which calls the
GU byte-identical seam `CreateGuardianLinkAsync(childId, guardianId)`. That
seam writes **one** `guardian.create` audit row with `ActorId = guardianId`
— the **assigned** guardian (Carol). The **assigning** guardian (Alice) is
persisted **nowhere**: the `GuardianLink` POCO has no conferrer field (G-A·3
byte-identity), and the audit row records the standing-holder, not the
conferrer. ADR 0038 §D (as amended 2026-09-17) names this as a legibility
limitation: *"the assigning guardian's identity is not persisted on the
row — a legibility limitation accepted because G-A·3 forbids a new seam."*
ADR 0038 §E defers *"No second audit verb — `guardian.create` is the one
verb"* as a **named deferral** — *"each re-litigates as an ADR 0038
amendment when it earns its keep."*

**This lane is that amendment.** It adds a **second audit row**,
`guardian.assign`, written in the **same commit** as the link + the
`guardian.create` row, with `ActorId = the assigning guardian`. Together the
two rows answer both *"who holds standing over this child?"* (Carol, on the
`guardian.create` row + the `GuardianLink` row) and *"who conferred the
standing?"* (Alice, on the `guardian.assign` row).

The design decision is **made, not open** (re-litigated in the session that
authored this register — 2026-09-17): the conferrer is captured by an
**additive Core seam** (`AssignGuardianLinkAsync`), **not** by a
`GuardianLink` POCO field (which would re-open the GU lane's POCO, its DDL,
and its tests — the cost/benefit analysis favors the audit-trail over the
relationship-document). The seam is an **ADR 0006-E compatible ADD** — the
same pattern the GA lane used for `IIdentityService.FindSubjectByEmailAsync`.

The lane is **small by design** — one seam, one audit verb, one changed
line on the Web action, two tests, one ADR amendment section, one design-doc
section. The GU enforcement path, the `GuardianLink` POCO, the `M1DocTypes`
registration, the five GU actions, and the `AccessVia` enum are all
**byte-identical** (invariants S·2–S·4).

## Assumptions

- **Scope (per the register):** **In:** (1) the
  `IUserInfoService.AssignGuardianLinkAsync(childId, guardianId,
  assignedById)` ADD (the conferral seam — the link + both audit rows in
  one commit); (2) the `UserInfoService` implementation (the
  `CreateGuardianLinkAsync` shape + the second `guardian.assign` audit
  row); (3) the **one** changed line on `GuardianController.Assign` (the
  `CreateGuardianLinkAsync` call → the new seam); (4) the **one** new Core
  test (`AssignGuardianLink_WritesBothAuditRows`) + the **two** Web test
  edits (the `Assign_KnownEmail_CallsCreateGuardianLinkAsync` rename + the
  `Assign_KnownEmail_WritesAssignAuditRow` add); (5) ADR 0038 §Amendment
  (2026-09-17, second) + the design doc updates. **Out (named
  deferrals, unchanged):** no `AssignedBy` field on `GuardianLink` (the
  POCO stays byte-identical — G-A·3); no remove path (ADR 0038 §E,
  unchanged); no acceptance/consent step (ADR 0038 §E, unchanged); no bulk
  assign (ADR 0038 §E, unchanged); no GU lane code change (the GU seam, the
  five GU actions, the GU tests, the GU design doc, the GU ADR — all
  byte-identical); no `IAuthorizationService` / `AccessVia` / GU
  enforcement path change (G-A·3's substance); no UI change (the
  `Detail.cshtml` appends, the `AssignGuardianForm`, the l10n keys — all
  unchanged; the user-facing behavior is identical, only the audit trail
  gains a row); no roadmap trio flip (the `GA-AR` lane is an
  audit-legibility refinement of a shipped lane, not a new roadmap item —
  the GU/GA lane discipline).
- **The seam is one ADD on `IUserInfoService`.** The UserInfoModule is the
  owner of the `GuardianLink` document and its audit rows; the Web cannot
  write to the `Authorization` module's `AccessAudit` document directly
  (ADR 0006 module boundary). `AssignGuardianLinkAsync` is a **write** (one
  `SaveChangesAsync`, three writes: the link + the two audit rows), returns
  the `GuardianLink` row (the `CreateGuardianLinkAsync` shape), and is
  **idempotent** for the `(guardianId, childId)` pair (a duplicate active
  row is a no-op — no second row, no second pair of audit rows — the G-A·4
  precedent, inherited).
- **The C3 atomicity is the load-bearing pin.** The `GuardianLink` row +
  the `guardian.create` audit row + the `guardian.assign` audit row are
  written in **one `SaveChangesAsync`** (S·1). A partial write (the link +
  the `guardian.create` row but no `guardian.assign` row, or vice-versa) is
  a **bug**, not a state. The Web action writes **no** audit row itself —
  the audit is the Core seam's responsibility (the `CreateGuardianLinkAsync`
  precedent). This is the architectural resolution of the C3 tension that
  a Web-layer second-row approach would create (the "second audit row"
  analysis, 2026-09-17).
- **The `GuardianController.Assign` action gains one changed line.** The
  standing gate (G-A·1), the resolution (G-A·2), the self-assignment
  refusal (G-A·5), the `try/catch` shape, the `TempData["info"]` line, and
  the redirect are all **untouched**. The one changed line is the seam
  call: `CreateGuardianLinkAsync(childId, assignedId)` →
  `AssignGuardianLinkAsync(childId, assignedId, subject)`. The
  `subject` (the assigning guardian's id) is the new third argument.
- **The audit shape is two complementary rows.** The `guardian.create` row
  (the GU seam's shape, byte-identical to what `CreateGuardianLinkAsync`
  writes): `ActorId = guardianId` (the assigned guardian — the
  standing-holder), `EffectivePrincipalId = guardianId`, `TargetKind =
  "guardian-link"`, `TargetId = the link's id`, `Via = Guardian`,
  `Outcome = Allow`. The `guardian.assign` row (the new conferral verb):
  `ActorId = assignedById` (the assigning guardian — the conferrer),
  `EffectivePrincipalId = assignedById`, `TargetKind = "guardian-link"`,
  `TargetId = the link's id`, `Via = Guardian`, `Outcome = Allow`.
  Together they answer *"who holds standing"* AND *"who conferred it."*
- **The GU lane is byte-identical.** `CreateGuardianLinkAsync(childId,
  guardianId)` is unchanged in signature, behavior, and audit shape (S·2).
  The GU lane's `AddChild` call site is unchanged (creation-based
  standing). The two seams are **distinct verbs for distinct standing
  bases**: `guardian.create` for formation (the GU lane), `guardian.assign`
  for conferral (this lane).
- **Test model (unchanged).** Core seam test in `Kumunita.Core.Tests`
  against the `PostgresFixture` fresh-scratch-DB shape (U01: the
  `AssignGuardianLink_WritesBothAuditRows` seam test). Web controller
  integration tests in `Kumunita.Web.Tests` (U02: the rename + the add).
  The lane's acceptance gate (closed-loop / handoff / part-vs-whole /
  audit-legibility) is recorded in the design doc (U02). The runner quirk
  (AGENTS.md) still applies: run via `dotnet exec tests\…\…\.dll`, not
  `dotnet test`.

## Approach

Two sealed units, sequenced. **U01 (Core):** the
`AssignGuardianLinkAsync` seam + its implementation + the **one** Core
seam test. **U02 (Web + docs):** the **one** changed line on
`GuardianController.Assign` + the **two** Web test edits + ADR 0038
§Amendment (2026-09-17, second) + the design doc updates + the file moves
to `done/`.

Every unit ends with **build green** (`dotnet build Kumunita.slnx -c
Debug`). U02 appends the final `## GA-AR — Closed (recorded)` section and
moves the lane's files to `done/guardian-assignment-second-audit-row/`.

## Pinned contract (finalized by this register for U01–U02)

### IUserInfoService.AssignGuardianLinkAsync (exact C#)

The **one new ADD** on the frozen `IUserInfoService` surface (the ADR
0006-E compatible lane; the `CreateGuardianLinkAsync` shape, extended with
the conferrer):

```csharp
/// <summary>
/// GA-AR (ADR 0038 amendment): assign a second guardian to a child — the
/// conferral-based standing basis (vs. <see cref="CreateGuardianLinkAsync"/>'s
/// creation-based basis). Writes the <see cref="GuardianLink"/> row + TWO
/// audit rows in ONE commit (C3): (1) <c>guardian.create</c> with
/// <c>ActorId = guardianId</c> (the standing-holder, the GU seam's shape —
/// byte-identical to what <see cref="CreateGuardianLinkAsync"/> writes);
/// (2) <c>guardian.assign</c> with <c>ActorId = assignedById</c> (the
/// conferrer). Together they answer "who holds standing" AND "who
/// conferred it." The <see cref="GuardianLink"/> POCO is unchanged (S·3).
/// Idempotent for the (guardianId, childId) pair (S·6 — the G-A·4
/// precedent, inherited): a duplicate active row is a no-op — no second
/// row, no second pair of audit rows.
/// </summary>
/// <param name="childId">The supervised child's subject id.</param>
/// <param name="guardianId">The assigned guardian's subject id (the
/// standing-holder; the <c>GuardianLink.GuardianId</c> value; the
/// <c>guardian.create</c> audit row's <c>ActorId</c>).</param>
/// <param name="assignedById">The assigning guardian's subject id (the
/// conferrer; the <c>guardian.assign</c> audit row's
/// <c>ActorId</c>/<c>EffectivePrincipalId</c>).</param>
Task<GuardianLink> AssignGuardianLinkAsync(
    string childId, string guardianId, string assignedById);
```

Doc-comment anchors S·1 (C3), S·3 (POCO byte-identity), S·6 (idempotency),
and the ADR 0006-E lane.

### GuardianController.Assign (the one changed line)

The **one changed line** on the existing `GuardianController.Assign`
action (the `CreateGuardianLinkAsync` call → the new seam; the `subject`
is the assigning guardian's id, already in scope from the action's
`SubjectId(User)` read):

```csharp
// Before:
//   await userInfo.CreateGuardianLinkAsync(childId, assignedId);
// After:
await userInfo.AssignGuardianLinkAsync(childId, assignedId, subject);
```

Everything else in the `Assign` action is **untouched** (the standing gate
G-A·1, the resolution G-A·2, the self-assignment refusal G-A·5, the
`try/catch` shape, the `TempData["info"]` line, the redirect).

### Invariants (pinned for GA-AR)

- **S·1 — C3 atomicity.** The `GuardianLink` row + the
  `guardian.create` row + the `guardian.assign` row are written in **one
  `SaveChangesAsync`**. A partial write (the link + the
  `guardian.create` row but no `guardian.assign` row, or vice-versa) is a
  **bug**, not a state. (GA-AR-owned; the load-bearing pin.)
- **S·2 — GU seam byte-identical.** `CreateGuardianLinkAsync(childId,
  guardianId)` is unchanged in signature, behavior, and audit shape. The
  GU lane's `AddChild` call site is unchanged. The two seams are distinct
  verbs for distinct standing bases: `guardian.create` for formation (GU),
  `guardian.assign` for conferral (GA-AR). (GA-AR-owned; inherits G-A·3's
  substance.)
- **S·3 — POCO byte-identical.** `GuardianLink` has no new field, no new
  enum value. `M1DocTypes` is unchanged. (GA-AR-owned; inherits G-A·3.)
- **S·4 — Standing unchanged.** The assigned guardian's standing is
  identical in kind to the creator's (G-A·3's substance, inherited). No
  new GU action, no new content read, no new "assigned" tier. The
  `IAuthorizationService` frozen surface is unchanged. (GA-AR-owned;
  inherits G-A·3.)
- **S·5 — Audit shape.** The `guardian.assign` row's `ActorId` /
  `EffectivePrincipalId` = the **assigning** guardian (the conferrer).
  The `guardian.create` row's `ActorId` / `EffectivePrincipalId` = the
  **assigned** guardian (the standing-holder, the GU seam's shape). Both
  `TargetId`s = the link's id. Both `Via = Guardian`, `Outcome = Allow`,
  `TargetKind = "guardian-link"`. (GA-AR-owned; the conferral legibility
  pin.)
- **S·6 — Idempotency (G-A·4, inherited).** A duplicate
  `(guardianId, childId)` active row is a no-op — the row is left as-is,
  no second row, no second pair of audit rows. (GA-AR-owned; inherited.)

### FACES (pinned, 4)

- **F1** a non-guardian cannot assign (404) — G-A·1 (unchanged, inherited)
- **F2** the `guardian.assign` row's `ActorId` is the assigning guardian
  — S·5
- **F3** a duplicate assignment is a no-op (no second pair of audit rows)
  — S·6
- **F4** the GU seam + POCO are byte-identical — S·2, S·3

### Pinned tests (exact names)

The file `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` (U01
authors) with **one new** test:

1. `AssignGuardianLink_WritesBothAuditRows` — call
   `AssignGuardianLinkAsync(childId, guardianId, assignedById)`; assert:
   (a) one `GuardianLink` row for the pair with `Status = Active`; (b) one
   `guardian.create` row with `ActorId = guardianId` (the standing-holder,
   S·5); (c) one `guardian.assign` row with `ActorId = assignedById` (the
   conferrer, S·5); (d) both rows target the same link id
   (`TargetKind = "guardian-link"`, `TargetId = the link's id`); (e) both
   `Via = Guardian`, both `Outcome = Allow`. The seam's idempotency
   (S·6) is asserted by a **second** call with the same pair: the row
   count stays 1, the audit-row counts stay 1 each (no second pair).

The file `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs` (U02
authors) with **one rename** + **one add**:

2. `Assign_KnownEmail_CallsCreateGuardianLinkAsync` → **renamed** to
   `Assign_KnownEmail_CallsAssignGuardianLinkAsync` — the action now calls
   the new seam; the assertions are updated to expect **two** audit rows
   (the `guardian.create` row with `ActorId = assignedId` + the
   `guardian.assign` row with `ActorId = the assigning guardian`). The
   existing assertions on the `GuardianLink` row (Active) and the redirect
   to `Detail` are unchanged.
3. `Assign_KnownEmail_WritesAssignAuditRow` — a guardian POSTs a known,
   non-self, non-duplicate email → the `guardian.assign` audit row's
   `ActorId` is the **assigning** guardian (the test's `actorSubjectId`),
   **not** the assigned guardian. The `guardian.create` row's `ActorId` is
   the **assigned** guardian (S·5). Both target the same link id.

The 9 pre-existing pinned tests (4 Core — 3 `FindSubjectByEmail_*` + 1
`Handoff_AssignedGuardian_CanSuspendAndUnsuspendChild`; 5 Web `Assign_*`) are
**unchanged** in name and assertion **except** the one rename above
(`Assign_KnownEmail_CallsCreateGuardianLinkAsync` →
`Assign_KnownEmail_CallsAssignGuardianLinkAsync`, whose assertions also gain
the second audit row).

A test whose exact name is not in this list is a `## U<m> — Drift pause`.

### Acceptance gate (U02 records)

- **closed loop** — the assigned guardian's standing is live on their next
  read (the `SuspendChildAsync` / `UnsuspendChildAsync` lanes resolve the
  new row) — proven by the existing
  `Handoff_AssignedGuardian_CanSuspendAndUnsuspendChild`.
- **handoff** — the assigned guardian can suspend / un-suspend over the
  child — proven by the same test.
- **part-vs-whole** — the 10-test list (4 Core + 6 Web, after the rename +
  add) is the whole; all must pass together.
- **audit legibility** (new, this lane) — the `guardian.assign` row's
  `ActorId` is the assigning guardian — proven by the two new tests
  (`AssignGuardianLink_WritesBothAuditRows` +
  `Assign_KnownEmail_WritesAssignAuditRow`).

### Drift-guard (frozen once written)

The `IUserInfoService.AssignGuardianLinkAsync` seam + its doc-comment, the
`GuardianController.Assign` one changed line, the S·1–S·6 invariants, the
3 pinned test names, the acceptance gate, and the S·2/S·3 byte-identity
pins — all frozen pins; any mismatch is a `## U<m> — Drift pause`.

---

## Units (2 total)

### U01 — `AssignGuardianLinkAsync` seam + impl + the 1 Core test

- **Goal:** add the **one** new `IUserInfoService` ADD (the pinned
  contract's exact seam) + its implementation (the
  `CreateGuardianLinkAsync` shape + the second `guardian.assign` audit
  row, **one** `SaveChangesAsync` — S·1) + the **one** Core seam test
  (`AssignGuardianLink_WritesBothAuditRows`). **No Web, no docs.**
- **Entry reads:** `src/Kumunita.Core/UserInfo/IUserInfoService.cs` §
  `CreateGuardianLinkAsync` (the seam the new one mirrors — the idempotent
  upsert + the `guardian.create` audit); `src/Kumunita.Core/UserInfo/
  UserInfoService.cs` § `CreateGuardianLinkAsync` (the impl to mirror —
  the session + the `GuardianLink` write + the `AccessAudit` write — the
  `SuspendChildAsync` impl's `AccessAudit` shape for the second row);
  `src/Kumunita.Core/UserInfo/GuardianLink.cs` (the POCO — **byte-identical,
  do not touch**); `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs`
  (the harness — `BootIdentityAsync` / `SeedChildProfileAsync` /
  `LastAuditAsync`); `tests/Kumunita.Core.Tests/GuardianControlsTests.cs`
  (the GU lane's audit-row assertion shape — `LastAuditAsync`,
  `CountAuditAsync`); this register's § Pinned contract (the **primary**
  source).
- **Deliverables (3 files, 3 modify):**
  - `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — append the **exact**
    `AssignGuardianLinkAsync(childId, guardianId, assignedById)` seam from
    the pinned contract, with its doc-comment. Place it **after**
    `CreateGuardianLinkAsync` (the `guardian.assign` verb is the GA-AR
    counterpart of the GU lane's `guardian.create` verb — the two are
    paired).
  - `src/Kumunita.Core/UserInfo/UserInfoService.cs` — implement
    `AssignGuardianLinkAsync`: the **same** session + `GuardianLink` write
    + `guardian.create` audit as `CreateGuardianLinkAsync` (byte-identical
    logic — S·2), **plus** a second `AccessAudit` row (the
    `guardian.assign` verb, `ActorId = assignedById` — S·5). **One**
    `SaveChangesAsync` (S·1). Idempotent for the `(guardianId, childId)`
    pair (S·6). **No** change to `CreateGuardianLinkAsync` (S·2).
  - `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` — add the
    **one** pinned test `AssignGuardianLink_WritesBothAuditRows` (the
    pinned contract's exact assertions, including the idempotency
    sub-assertion).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. Run via the
  **reliable path** (AGENTS.md): `dotnet exec tests\Kumunita.Core.Tests\
  bin\Debug\net10.0\Kumunita.Core.Tests.dll -class
  "Kumunita.Core.Tests.GuardianAssignmentTests"` → reports **5 tests
  discovered, 5 executed** (the 4 existing + the 1 new). Record the
  pass/red status of each (for U02's gate). **No docs** (U02). **Clean up
  Docker:** if the process was killed, `docker container prune`. Handoff
  note: 5–7 lines starting `## U01 — AssignGuardianLinkAsync seam + test`
  — (a) the seam's exact signature (verbatim), (b) the impl's audit-row
  shape (both rows, verbatim), (c) the 1 new test name (verbatim), (d) the
  pass/red counts, (e) a confirmation `CreateGuardianLinkAsync` + the
  `GuardianLink` POCO are **untouched** (S·2, S·3), (f) any drift pause.

### U02 — Web switch + the 2 Web tests + the docs + close

- **Goal:** switch `GuardianController.Assign` to call
  `AssignGuardianLinkAsync` (the **one** changed line), update + add the 2
  Web tests, author the **docs** (ADR 0038 §Amendment (2026-09-17,
  second) + the design doc updates), record the acceptance gate, and **move
  the lane's files to `done/`**. **No Core code** (U01).
- **Entry reads:** `src/Kumunita.Web/Controllers/GuardianController.cs` §
  `Assign` action (the one line to change — `CreateGuardianLinkAsync` →
  `AssignGuardianLinkAsync`; the `subject` is already in scope from the
  action's `SubjectId(User)` read); `tests/Kumunita.Web.Tests/
  GuardianAssignmentTests.cs` (the 2 tests to update/add — the
  `Assign_KnownEmail_CallsCreateGuardianLinkAsync` rename + the
  `Assign_KnownEmail_WritesAssignAuditRow` add); `docs/adr/0038-guardian-
  assignment.md` (the §Amendment (2026-09-17) section to emulate — the
  dated-amendment shape); `docs/design/guardian-assignment-design.md` (the
  pinned-test list + the drift-guard + the acceptance gate + the
  `Verification + reconciliation (2026-09-17)` section to update);
  this register's § Pinned contract (the **primary** source).
- **Deliverables (4 files modify + file moves):**
  - `src/Kumunita.Web/Controllers/GuardianController.cs` — the **one**
    changed line in the `Assign` action (the `CreateGuardianLinkAsync`
    call → `AssignGuardianLinkAsync(childId, assignedId, subject)`). The
    `TempData["info"]` line is **untouched** (the success message is the
    same). Everything else in the action is **untouched** (S·2's Web-side
    mirror: the GU lane's `AddChild` call site is unchanged).
  - `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs` — **rename**
    `Assign_KnownEmail_CallsCreateGuardianLinkAsync` →
    `Assign_KnownEmail_CallsAssignGuardianLinkAsync` + update its
    assertions (two audit rows — the `guardian.create` row with
    `ActorId = assignedId` + the `guardian.assign` row with `ActorId = the
    assigning guardian`); **add** `Assign_KnownEmail_WritesAssignAuditRow`
    (the `guardian.assign` row's `ActorId` = the assigning guardian, S·5).
  - `docs/adr/0038-guardian-assignment.md` — append a new `## Amendment
    (2026-09-17, second)` section: (a) the new seam (the signature,
    verbatim), (b) the `guardian.assign` verb (the §E "no second audit
    verb" deferral is now **implemented**, not deferred), (c) the C3
    atomicity (both rows in one commit — the partial-write risk is
    resolved), (d) the GU seam + POCO are byte-identical (S·2, S·3 — the
    new seam is additive), (e) the 2 new test names, (f) the §D named
    legibility limitation is **resolved** (the conferrer is now on the
    audit trail — the `guardian.assign` row's `ActorId`).
  - `docs/design/guardian-assignment-design.md` — update the pinned-test
    list (add the 2 new test names; the count is now **10**), the
    drift-guard (add the new seam + the 2 new test names + the S·1–S·6
    invariants), the acceptance gate (re-count to 10; add the "audit
    legibility" leg), the `Verification + reconciliation (2026-09-17)`
    section (the "Not done" line is now **superseded** — the named
    limitation is resolved by the GA-AR lane; add a line recording the
    second-audit-row implementation), and a new `## GA-AR — Second audit
    row (implemented) (2026-09-17)` section (the seam, the verb, the C3
    atomicity, the test names, the GU lane's byte-identity, the S·1–S·6
    invariants, the FACES F1–F4, the acceptance gate, the drift-guard).
  - **File moves (PowerShell `Move-Item`, one self-contained command):**
    `docs/plans-milestones/in-progress/
    plan-guardian-assignment-second-audit-row.md` → `done/
    guardian-assignment-second-audit-row/`; every `docs/plans-milestones/
    in-progress/guardian-assignment-second-audit-row-uNN-plan.md` → `done/
    guardian-assignment-second-audit-row/`; `docs/plans-milestones/
    in-progress/guardian-assignment-second-audit-row-handoff-notes.md` →
    `done/guardian-assignment-second-audit-row/`. Confirm `in-progress/`
    is empty afterward.
- **Exit:** `dotnet build Kumunita.slnx -c Debug` green. Run via the
  **reliable path** (AGENTS.md):
  - `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\
    Kumunita.Core.Tests.dll -class
    "Kumunita.Core.Tests.GuardianAssignmentTests"` → **5/5 PASS**.
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\
    Kumunita.Web.Tests.dll -class
    "Kumunita.Web.Tests.GuardianAssignmentTests"` → **6/6 PASS** (the 5
    existing — one renamed — + the 1 new = 6).
  - `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\
    Kumunita.Web.Tests.dll` → **201/201 PASS** (the full suite — the GU
    lane's tests are unchanged, the GA lane's Web tests go 5 → 6 (one renamed,
    one added), so the full-suite total goes 200 → **201**).
  - **Clean up Docker:** if the process was killed, `docker container
    prune`.
  - Handoff note: the `## Summary` section is present — a table of the
    shipped units (U01–U02) with their one-liner goal + test count + any
    deviations + the invariants S·1–S·6 (each named) + the FACES F1–F4
    (each named).

## Unit-series rules

1. A unit never modifies a file not in its own `Deliverables`.
2. Never reshapes `GuardianLink` (the GU lane's POCO) or adds a new field /
   enum value / renumbering to it (S·3).
3. Never modifies `CreateGuardianLinkAsync` or the GU lane's `AddChild`
   call site (S·2).
4. Never opens a seam on `IUserInfoService` / `IIdentityService` /
   `IAuthorizationService` beyond what this register pinned (the one
   `AssignGuardianLinkAsync` ADD).
5. Never touches the GU enforcement path (the GU seams, the
   `GuardActiveLinkAsync` helper, the `AccessVia.Guardian` value — all
   byte-identical — S·4).
6. Never introduces a test whose exact name is not in this register's
   pinned-test list.
7. If entry reads reveal the pinned contract is out of date, the unit
   pauses and records `## U<m> — Drift pause` in the handoff notes.
