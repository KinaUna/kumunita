# GA-AR U02 — Web switch + the 2 Web tests + the docs + close

> Unit 2 of 2 in the GA-AR lane (guardian assignment, second audit row).
> The **register** is `plan-guardian-assignment-second-audit-row.md` (the
> pinned contract, the primary source for what to do). U01 has already
> added the `AssignGuardianLinkAsync` seam + impl + the 1 Core test. This
> unit: switches the Web action to the new seam, edits the 2 Web tests,
> authors the docs (ADR 0038 §Amendment (2026-09-17, second) + the design
> doc updates), records the acceptance gate, and moves the lane's files to
> `done/`. **No Core code** (U01).

## Goal

Switch `GuardianController.Assign` to call `AssignGuardianLinkAsync` (the
**one** changed line), update + add the 2 Web tests, author the **docs**,
record the acceptance gate, and **move the lane's files to `done/`**. **No
Core code** (U01). The GU lane is byte-identical (S·2, S·3, S·4).

## Entry reads (6 files)

1. `docs/plans-milestones/in-progress/plan-guardian-assignment-second-audit-row.md`
   § Pinned contract § `GuardianController.Assign` (the one changed line)
   + § Pinned tests (#2 rename, #3 add) + § Acceptance gate + § Drift-guard
   (the **primary** source).
2. `src/Kumunita.Web/Controllers/GuardianController.cs` § `Assign` action
   (the one line to change — the `CreateGuardianLinkAsync` call; the
   `subject` is already in scope from the action's `SubjectId(User)` read;
   the `userInfo` field is already in the controller's ctor — the
   `AddChild` action uses it).
3. `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs` (the 2 tests to
   update/add — the `Assign_KnownEmail_CallsCreateGuardianLinkAsync`
   rename + the `Assign_KnownEmail_WritesAssignAuditRow` add; the
   `BuildAsync` harness + the `CountAuditAsync` / `LastAuditAsync`
   helpers).
4. `docs/adr/0038-guardian-assignment.md` (the §Amendment (2026-09-17)
   section to emulate — the dated-amendment shape + the §D legibility
   limitation this lane resolves + the §E "no second audit verb" deferral
   this lane implements).
5. `docs/design/guardian-assignment-design.md` (the pinned-test list + the
   drift-guard + the acceptance gate + the `Verification + reconciliation
   (2026-09-17)` section + the `GA — Closed (recorded)` section — the
   surfaces to update).
6. `docs/plans-milestones/in-progress/guardian-assignment-second-audit-row-
   handoff-notes.md` § U01 (the seam's exact signature + the impl's
   audit-row shape + the 1 Core test name + the pass/red counts — the
   U01 handoff note this unit reads).

## Deliverables (4 files modify + file moves)

### `src/Kumunita.Web/Controllers/GuardianController.cs`

The **one** changed line in the `Assign` action (the `CreateGuardianLinkAsync`
call → the new seam; the `subject` is the assigning guardian's id, already
in scope). Locate the existing line:

```csharp
            await userInfo.CreateGuardianLinkAsync(childId, assignedId);
```

and replace with:

```csharp
            await userInfo.AssignGuardianLinkAsync(childId, assignedId, subject);
```

The `TempData["info"]` line is **untouched** (the success message is the
same). Everything else in the action is **untouched** (the standing gate
G-A·1, the resolution G-A·2, the self-assignment refusal G-A·5, the
`try/catch` shape, the redirect — S·2's Web-side mirror: the GU lane's
`AddChild` call site is unchanged).

### `tests/Kumunita.Web.Tests/GuardianAssignmentTests.cs`

**Rename** `Assign_KnownEmail_CallsCreateGuardianLinkAsync` →
`Assign_KnownEmail_CallsAssignGuardianLinkAsync` + **update** its
assertions (the happy path now produces **two** audit rows — the
`guardian.create` row with `ActorId = the assigned guardian` + the
`guardian.assign` row with `ActorId = the assigning guardian`; both target
the same link id; both `Via = Guardian`, `Outcome = Allow`). The existing
assertions on the `GuardianLink` row (Active) and the redirect to `Detail`
are unchanged. Concretely, the updated test body:

```csharp
[Fact]
public async Task Assign_KnownEmail_CallsAssignGuardianLinkAsync()
{
    var (controller, identity, store) = await BuildAsync();
    var ct = TestContext.Current.CancellationToken;
    identity.FindSubjectByEmailAsync("new@example.com")
        .Returns(Task.FromResult<string?>(NewGuardian));

    var result = await controller.Assign(
        Child,
        new AssignGuardianForm { Email = "new@example.com" });

    // The happy path redirects to Detail (not a form re-render).
    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Detail", redirect.ActionName);

    // A new GuardianLink row for (newGuardian, child) with Active status.
    await using var session = store.QuerySession();
    var newLink = await session.Query<GuardianLink>()
        .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
        .FirstOrDefaultAsync(ct);
    Assert.NotNull(newLink);
    Assert.Equal(GuardianLinkStatus.Active, newLink!.Status);

    // Two audit rows target the new link (S·1 — one commit, two rows):
    // (1) guardian.create — ActorId = the ASSIGNED guardian (S·5).
    // (2) guardian.assign — ActorId = the ASSIGNING guardian (S·5).
    var createRow = await LastAuditAsync(store, "guardian.create", newLink.Id, ct);
    Assert.Equal(NewGuardian, createRow.ActorId);          // the assigned guardian
    Assert.Equal(AccessVia.Guardian, createRow.Via);

    var assignRow = await LastAuditAsync(store, "guardian.assign", newLink.Id, ct);
    Assert.Equal(Guardian, assignRow.ActorId);             // the assigning guardian (the test's actor)
    Assert.Equal(AccessVia.Guardian, assignRow.Via);

    Assert.Equal(newLink.Id, createRow.TargetId);
    Assert.Equal(newLink.Id, assignRow.TargetId);
    Assert.Equal("guardian-link", assignRow.TargetKind);
}
```

> **Note:** `Guardian` is the test class's existing constant for the
> **actor** (the assigning guardian — `actorSubjectId` in `BuildAsync`).
> `NewGuardian` is the **assigned** guardian (the email-resolved subject
> id). The `Assign` action passes `subject` (the actor = `Guardian`) as
> `assignedById` to the new seam, so the `guardian.assign` row's `ActorId`
> is `Guardian`.

**Add** `Assign_KnownEmail_WritesAssignAuditRow` (the register's § Pinned
tests #3 — the `guardian.assign` row's `ActorId` = the assigning guardian,
S·5). The test seeds a known non-self non-duplicate email, POSTs the
`Assign` action, and asserts **specifically** the `guardian.assign`
audit row's shape (the `ActorId` is the actor, **not** the assigned
guardian):

```csharp
[Fact]
public async Task Assign_KnownEmail_WritesAssignAuditRow()
{
    var (controller, identity, store) = await BuildAsync();
    var ct = TestContext.Current.CancellationToken;
    identity.FindSubjectByEmailAsync("new@example.com")
        .Returns(Task.FromResult<string?>(NewGuardian));

    var result = await controller.Assign(
        Child,
        new AssignGuardianForm { Email = "new@example.com" });

    var redirect = Assert.IsType<RedirectToActionResult>(result);
    Assert.Equal("Detail", redirect.ActionName);

    // The new link row exists (the seam's write, S·1).
    await using var session = store.QuerySession();
    var newLink = await session.Query<GuardianLink>()
        .Where(l => l.GuardianId == NewGuardian && l.ChildId == Child)
        .FirstOrDefaultAsync(ct);
    Assert.NotNull(newLink);

    // S·5 — the guardian.assign row's ActorId is the ASSIGNING guardian
    // (the actor, the test's Guardian constant), NOT the assigned
    // guardian (NewGuardian). This is the conferral legibility the
    // GA-AR lane adds (ADR 0038 §D's named limitation, resolved).
    var assignRow = await LastAuditAsync(store, "guardian.assign", newLink!.Id, ct);
    Assert.Equal(Guardian, assignRow.ActorId);             // the assigning guardian (the actor)
    Assert.Equal(Guardian, assignRow.EffectivePrincipalId);
    Assert.NotEqual(NewGuardian, assignRow.ActorId);       // NOT the assigned guardian
    Assert.Equal("guardian-link", assignRow.TargetKind);
    Assert.Equal(newLink.Id, assignRow.TargetId);
    Assert.Equal(AccessVia.Guardian, assignRow.Via);
    Assert.Equal(AccessOutcome.Allow, assignRow.Outcome);
}
```

The other 4 Web tests (`Assign_NonGuardian_Returns404`,
`Assign_UnknownEmail_ReturnsValidationError`,
`Assign_SelfAssignment_ReturnsValidationError`,
`Assign_DuplicateAssignment_IsIdempotentNoOp`) are **unchanged**.

> **Note:** the `BuildAsync` harness's `IIdentityService` substitute
> (NSubstitute) already stubs `FindSubjectByEmailAsync`. The `UserInfoService`
> in `BuildAsync` is the **real** one (the seam runs against the real
> store). The new seam `AssignGuardianLinkAsync` is on the same
> `UserInfoService`, so no harness change is needed — the action's call to
> the new seam resolves against the same `userInfo` instance.

### `docs/adr/0038-guardian-assignment.md`

Append a new `## Amendment (2026-09-17, second)` section **after** the
existing `## Amendment (2026-09-17)` section (the two amendments are
sequential). The new section:

```markdown
## Amendment (2026-09-17, second)

The named legibility limitation in §D — the assigning guardian's identity
is not persisted on the row — is **resolved** by the GA-AR lane (the
"guardian assignment, second audit row" lane, this ADR's §E "no second
audit verb" deferral, now implemented). The GU lane (ADR 0028) and the
GA lane's shipped surface (the `GuardianLink` POCO, the
`CreateGuardianLinkAsync` seam, the five GU actions, the `AccessVia`
enum, the GU enforcement path) are all **byte-identical** (invariant
S·2/S·3/S·4, inherited from G-A·3).

1. **New seam (additive, ADR 0006-E compatible):**
   `IUserInfoService.AssignGuardianLinkAsync(string childId, string
   guardianId, string assignedById)` — the conferral-based standing basis
   (vs. `CreateGuardianLinkAsync`'s creation-based basis). Writes the
   `GuardianLink` row + **two** audit rows in **one** `SaveChangesAsync`
   (C3 atomicity, invariant S·1): (a) `guardian.create` with `ActorId =
   guardianId` (the standing-holder, the GU seam's shape — S·5); (b)
   `guardian.assign` with `ActorId = assignedById` (the conferrer — S·5).
   Idempotent for the `(guardianId, childId)` pair (S·6, the G-A·4
   precedent). The GU lane's `CreateGuardianLinkAsync` is **unchanged**
   (S·2) — its `AddChild` call site still calls it (creation-based).

2. **`guardian.assign` verb (the §E "no second audit verb" deferral is now
   implemented):** the `guardian.assign` row's `ActorId` /
   `EffectivePrincipalId` is the **assigning** guardian (the conferrer).
   The `guardian.create` row's `ActorId` / `EffectivePrincipalId` is the
   **assigned** guardian (the standing-holder). Both `TargetId`s = the
   link's id, both `Via = Guardian`, `Outcome = Allow`, `TargetKind =
   "guardian-link"` (S·5). Together they answer *"who holds standing over
   this child?"* AND *"who conferred the standing?"* — the §D named
   limitation is resolved.

3. **C3 atomicity (S·1):** the link + the two audit rows are in **one**
   commit. A partial write (the link + the `guardian.create` row but no
   `guardian.assign` row, or vice-versa) is a **bug**, not a state. The
   Web action writes **no** audit row itself — the audit is the Core
   seam's responsibility (the `CreateGuardianLinkAsync` precedent).

4. **The Web action's one changed line:** `GuardianController.Assign`
   calls `AssignGuardianLinkAsync(childId, assignedId, subject)` instead
   of `CreateGuardianLinkAsync(childId, assignedId)`. The standing gate
   (G-A·1), the resolution (G-A·2), the self-assignment refusal (G-A·5),
   and the redirect are **untouched**. The GU lane's `AddChild` call
   site is **untouched** (S·2).

5. **Tests:** the Core lane adds
   `AssignGuardianLink_WritesBothAuditRows` (the seam's both-audit-rows +
   idempotency shape). The Web lane renames
   `Assign_KnownEmail_CallsCreateGuardianLinkAsync` →
   `Assign_KnownEmail_CallsAssignGuardianLinkAsync` (the action now
   calls the new seam) + adds `Assign_KnownEmail_WritesAssignAuditRow`
   (the `guardian.assign` row's `ActorId` = the assigning guardian). The
   other 9 pinned tests (the GA lane's 3 Core `FindSubjectByEmail_*` + 1
   Core `Handoff_*` + 4 Web `Assign_*`) are **unchanged**. The acceptance
   gate is now 10 tests (4 Core + 6 Web), all PASS.

6. **Invariants S·1–S·6** (the design doc's § Pinned contract, verbatim) +
   **FACES F1–F4** (the design doc's § Pinned contract, verbatim).

7. **Roadmap trio:** **untouched** — the GA-AR lane is an
   audit-legibility refinement of a shipped lane, not a new roadmap item
   (the GU/GA lane discipline: the roadmap records shipped surfaces, not
   their internal refinement). M4/M5/M6 stay Events / Projects /
   Portability.

Each of §E's other non-decisions (no remove path; no acceptance/consent
step; no bulk assign; no self-assignment; no email notification) is
**unchanged** — still *deferred*, not *denied*; each re-litigates as a
future ADR 0038 amendment when it earns its keep.
```

### `docs/design/guardian-assignment-design.md`

Make the following **additive** edits (no existing section rewritten):

1. **§ Pinned seam tests** — the Core list (currently 3 names) gains one
   new name (`AssignGuardianLink_WritesBothAuditRows` — the GA-AR seam
   test). The Web list (currently 5 names) renames one
   (`Assign_KnownEmail_CallsCreateGuardianLinkAsync` →
   `Assign_KnownEmail_CallsAssignGuardianLinkAsync`) + adds one
   (`Assign_KnownEmail_WritesAssignAuditRow`). The total is now **10**
   (4 Core + 6 Web). Add a line after the list:
   > **GA-AR (2026-09-17):** the pinned-test list is extended from 9 to
   > **10** names (4 Core + 6 Web) by the GA-AR lane (the second-audit-row
   > amendment, ADR 0038 §Amendment (2026-09-17, second)). The GA lane's
   > 8 original names are unchanged (one renamed, one added); the GA-AR
   > lane adds `AssignGuardianLink_WritesBothAuditRows` (Core) +
   > `Assign_KnownEmail_WritesAssignAuditRow` (Web) and renames
   > `Assign_KnownEmail_CallsCreateGuardianLinkAsync` →
   > `Assign_KnownEmail_CallsAssignGuardianLinkAsync` (Web).

2. **§ Acceptance gate** — the "part-vs-whole" line updates from "the
   9-test list" to "the **10-test** list (4 Core + 6 Web, after the
   GA-AR rename + add)." Add a **fourth** leg after the existing three:
   > **audit legibility (GA-AR, 2026-09-17)** — the `guardian.assign`
   > row's `ActorId` is the **assigning** guardian (the conferrer) — the
   > §D named legibility limitation (the 2026-09-17 first amendment) is
   > **resolved**; proven by
   > `AssignGuardianLink_WritesBothAuditRows` (Core) +
   > `Assign_KnownEmail_WritesAssignAuditRow` (Web).

3. **§ Drift-guard** — add to the frozen-pin list: the
   `IUserInfoService.AssignGuardianLinkAsync` seam + its doc-comment, the
   `GuardianController.Assign` one changed line, the S·1–S·6 invariants,
   the FACES F1–F4, and the 3 new/renamed test names. (The GA-AR lane
   extends the GA lane's drift-guard; the GA lane's existing pins are
   unchanged.)

4. **`## Verification + reconciliation (2026-09-17)`** — the "Not done"
   line (the "named limitation" the first amendment recorded) is now
   **superseded**. Append a line:
   > **Superseded (GA-AR, 2026-09-17):** the named legibility limitation
   > (the assigning guardian not persisted) is **resolved** by the GA-AR
   > lane — the `guardian.assign` audit row's `ActorId` is the assigning
   > guardian (ADR 0038 §Amendment (2026-09-17, second)). The
   > `GuardianLink` POCO is still byte-identical (S·3) — the conferrer is
   > in the **audit trail**, not the relationship document.

5. **New section `## GA-AR — Second audit row (implemented) (2026-09-17)`**
   — the lane's record (the register's § Pinned contract, the invariants
   S·1–S·6, the FACES F1–F4, the acceptance gate (10 tests, all PASS),
   the drift-guard, the GU lane's byte-identity confirmation, the
   roadmap-trio-untouched line). This is the loop-closing section (the
   GA lane's `## GA — Closed (recorded)` analog, for the GA-AR lane).

### File moves (PowerShell `Move-Item`, one self-contained command)

```powershell
$dest = "docs\plans-milestones\done\guardian-assignment-second-audit-row"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Move-Item "docs\plans-milestones\in-progress\plan-guardian-assignment-second-audit-row.md" -Destination $dest
Move-Item "docs\plans-milestones\in-progress\guardian-assignment-second-audit-row-u01-plan.md" -Destination $dest
Move-Item "docs\plans-milestones\in-progress\guardian-assignment-second-audit-row-u02-plan.md" -Destination $dest
Move-Item "docs\plans-milestones\in-progress\guardian-assignment-second-audit-row-handoff-notes.md" -Destination $dest
Write-Host "in-progress/ now contains:"; Get-ChildItem "docs\plans-milestones\in-progress" -ErrorAction SilentlyContinue | Select-Object Name
```

Confirm `in-progress/` is **empty** afterward (the GA-AR lane's files are
in `done/guardian-assignment-second-audit-row/`).

## Exit

`dotnet build Kumunita.slnx -c Debug` green (0 warnings, 0 errors). Run
via the **reliable path** (AGENTS.md):

- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.GuardianAssignmentTests"` → **5/5 PASS** (4 GA + 1 GA-AR).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll -class "Kumunita.Web.Tests.GuardianAssignmentTests"` → **6/6 PASS** (5 GA + 1 GA-AR, after the 1 rename).
- `dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` → **201/201 PASS** (the full suite — the GU lane's tests are unchanged, the GA lane's Web tests go 5 → 6, the full-suite total goes 200 → 201).

**Clean up Docker:** if the process was killed, `docker container prune`.

**Handoff note** — the `## Summary` section is present — a table of the
shipped units (U01–U02) with their one-liner goal + test count + any
deviations + the invariants S·1–S·6 (each named) + the FACES F1–F4 (each
named) + the ADR 0038 §E deferral list (each named — the other
non-decisions, unchanged).

## Unit-series rules (this unit)

1. This unit never modifies a file not in its own `Deliverables`.
2. This unit never touches `CreateGuardianLinkAsync` or the GU lane's
   `AddChild` call site (S·2).
3. This unit never reshapes `GuardianLink` (the GU lane's POCO) or adds a
   new field / enum value / renumbering to it (S·3).
4. This unit never opens a seam on `IUserInfoService` /
   `IIdentityService` / `IAuthorizationService` beyond what the register
   pinned (the one `AssignGuardianLinkAsync` ADD — U01's).
5. This unit never touches the GU enforcement path (the GU seams, the
   `GuardActiveLinkAsync` helper, the `AccessVia.Guardian` value — all
   byte-identical — S·4).
6. This unit never introduces a test whose exact name is not in the
   register's pinned-test list.
7. If entry reads reveal the pinned contract is out of date, this unit
   pauses and records `## U02 — Drift pause` in the handoff notes.
