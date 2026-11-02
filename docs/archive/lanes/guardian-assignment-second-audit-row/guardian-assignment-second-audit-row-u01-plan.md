# GA-AR U01 — `AssignGuardianLinkAsync` seam + impl + the 1 Core test

> Unit 1 of 2 in the GA-AR lane (guardian assignment, second audit row).
> The **register** is `plan-guardian-assignment-second-audit-row.md` (the
> pinned contract, the primary source for what to do). The **design doc** is
> `docs/design/guardian-assignment-design.md` (the GA lane's invariants,
> G-A·1–G-A·6 — the S·2/S·3/S·4 byte-identity pins inherit from G-A·3). The
> **handoff notes** are
> `guardian-assignment-second-audit-row-handoff-notes.md` (this unit writes
> one `##` section before it exits).

## Goal

Add the **one** new `IUserInfoService` ADD (`AssignGuardianLinkAsync`) + its
implementation (the `CreateGuardianLinkAsync` shape + the second
`guardian.assign` audit row, **one** `SaveChangesAsync` — S·1) + the **one**
Core seam test (`AssignGuardianLink_WritesBothAuditRows`). **No Web, no
docs.** The GU lane is byte-identical (S·2, S·3, S·4).

## Entry reads (5 files)

1. `docs/plans-milestones/in-progress/plan-guardian-assignment-second-audit-row.md`
   § Pinned contract § `IUserInfoService.AssignGuardianLinkAsync` (the
   **primary** source — the exact seam + doc-comment, the S·1–S·6
   invariants, the pinned test name #1).
2. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` §
   `CreateGuardianLinkAsync` (the seam the new one mirrors — the idempotent
   upsert + the `guardian.create` audit; the `GU guardian lanes (ADR 0028)`
   section header where the new seam is appended after).
3. `src/Kumunita.Core/UserInfo/UserInfoService.cs` §
   `CreateGuardianLinkAsync` (the impl to mirror — the session + the
   `GuardianLink` write + the `AccessAudit` write; the
   `SuspendChildAsync` impl's `AccessAudit` shape for the second row).
4. `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs` (the harness —
   `BootIdentityAsync` / `SeedChildProfileAsync` / `LastAuditAsync` — the
   test's existing shape to extend).
5. `tests/Kumunita.Core.Tests/GuardianControlsTests.cs` (the GU lane's
   audit-row assertion shape — `LastAuditAsync`, `CountAuditAsync` — the
   assertion style to mirror).

## Deliverables (3 files, 3 modify)

### `src/Kumunita.Core/UserInfo/IUserInfoService.cs`

Append, **after** `CreateGuardianLinkAsync` (the `guardian.assign` verb is
the GA-AR counterpart of the GU lane's `guardian.create` verb — the two are
paired), the **exact** seam from the register's pinned contract:

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

**No** other `IUserInfoService` member changed (S·2).

### `src/Kumunita.Core/UserInfo/UserInfoService.cs`

Implement `AssignGuardianLinkAsync` (the `CreateGuardianLinkAsync` shape +
the second audit row, **one** `SaveChangesAsync` — S·1). The implementation
mirrors `CreateGuardianLinkAsync` line-for-line (the `ArgumentException`
guards, the session, the idempotency check, the `GuardianLink` write, the
`guardian.create` audit) with **one addition**: a second `AccessAudit` row
(the `guardian.assign` verb, `ActorId = assignedById`). Concretely:

```csharp
/// <inheritdoc />
public async Task<GuardianLink> AssignGuardianLinkAsync(
    string childId, string guardianId, string assignedById)
{
    if (string.IsNullOrWhiteSpace(childId))
        throw new ArgumentException("Child id is required.", nameof(childId));
    if (string.IsNullOrWhiteSpace(guardianId))
        throw new ArgumentException("Guardian id is required.", nameof(guardianId));
    if (string.IsNullOrWhiteSpace(assignedById))
        throw new ArgumentException("Assigning guardian id is required.", nameof(assignedById));

    var now = DateTimeOffset.UtcNow;

    await using var session = store.OpenSession(new SessionOptions());

    // S·6 — idempotent formation: a duplicate (GuardianId, ChildId) Active row
    // is a no-op — the row is left as-is and returned (not a throw), no
    // second pair of audit rows (the G-A·4 precedent, inherited).
    var existing = await session.Query<GuardianLink>()
        .Where(l => l.GuardianId == guardianId && l.ChildId == childId)
        .FirstOrDefaultAsync()
        .ConfigureAwait(false);

    if (existing is not null)
    {
        // No mutation, no audit (a no-op is a no-op — the contract, not an
        // error). Return the existing row as-is.
        return existing;
    }

    var link = new GuardianLink
    {
        Id = Guid.NewGuid().ToString("N"),
        GuardianId = guardianId,
        ChildId = childId,
        Status = GuardianLinkStatus.Active,
        CreatedAt = now
    };
    session.Store(link);

    // S·5 — the two complementary audit rows, written in the SAME session
    // (S·1 — one SaveChangesAsync, no partial write):
    //
    // (1) guardian.create — the GU seam's shape, byte-identical to what
    //     CreateGuardianLinkAsync writes (S·2): ActorId = the ASSIGNED
    //     guardian (the standing-holder).
    session.Store(new Authorization.AccessAudit
    {
        Id = Guid.NewGuid().ToString("N"),
        At = now,
        ActorId = guardianId,
        EffectivePrincipalId = guardianId,
        Action = "guardian.create",
        TargetKind = "guardian-link",
        TargetId = link.Id,
        Via = Authorization.AccessVia.Guardian,
        Outcome = Authorization.AccessOutcome.Allow
    });

    // (2) guardian.assign — the GA-AR conferral verb: ActorId = the
    //     ASSIGNING guardian (the conferrer, S·5).
    session.Store(new Authorization.AccessAudit
    {
        Id = Guid.NewGuid().ToString("N"),
        At = now,
        ActorId = assignedById,
        EffectivePrincipalId = assignedById,
        Action = "guardian.assign",
        TargetKind = "guardian-link",
        TargetId = link.Id,
        Via = Authorization.AccessVia.Guardian,
        Outcome = Authorization.AccessOutcome.Allow
    });

    await session.SaveChangesAsync().ConfigureAwait(false);
    return link;
}
```

Place it **after** `CreateGuardianLinkAsync` (the two are paired). **No**
change to `CreateGuardianLinkAsync` (S·2). **No** change to the
`GuardianLink` POCO (S·3). **No** new `using` (the `Authorization`
namespace + `SessionOptions` are already imported in the file — the
`CreateGuardianLinkAsync` impl uses both).

### `tests/Kumunita.Core.Tests/GuardianAssignmentTests.cs`

Add the **one** pinned test (the register's § Pinned tests #1, exact name
`AssignGuardianLink_WritesBothAuditRows`). Place it **after** the existing
`Handoff_AssignedGuardian_CanSuspendAndUnsuspendChild` test (the Core
tests are ordered: the 3 `FindSubjectByEmail_*` seam tests, then the
handoff test, then this new seam test). The test uses the existing
harness (`BootIdentityAsync` — the two-store shape; `SeedChildProfileAsync`
— the minimal child profile; `LastAuditAsync` — the audit-row read):

```csharp
// ── 10 (GA-AR) — the conferral seam writes BOTH audit rows in one commit ──
// S·1 (C3 atomicity) + S·5 (audit shape) + S·6 (idempotency). The
// guardian.create row records the ASSIGNED guardian (the standing-holder,
// the GU seam's shape); the guardian.assign row records the ASSIGNING
// guardian (the conferrer, the GA-AR verb). Together they answer "who
// holds standing" AND "who conferred it."

[Fact]
public async Task AssignGuardianLink_WritesBothAuditRows()
{
    var (_, userInfo, store) = await BootIdentityAsync();
    var ct = TestContext.Current.CancellationToken;

    const string assigningGuardian = "ga-ar-assigning";
    const string assignedGuardian = "ga-ar-assigned";
    const string child = "ga-ar-child";

    await SeedChildProfileAsync(store, child, ct);

    // Call the seam — one commit, two audit rows (S·1).
    var link = await userInfo.AssignGuardianLinkAsync(
        child, assignedGuardian, assigningGuardian);
    Assert.Equal(GuardianLinkStatus.Active, link.Status);

    // (a) one GuardianLink row for the pair, Active.
    await using var q1 = store.QuerySession();
    var linkCount = await Marten.QueryableExtensions.CountAsync(
        q1.Query<GuardianLink>()
            .Where(l => l.GuardianId == assignedGuardian && l.ChildId == child),
        ct);
    Assert.Equal(1, linkCount);

    // (b) one guardian.create row, ActorId = the ASSIGNED guardian (S·5).
    var createRow = await LastAuditAsync(store, "guardian.create", link.Id, ct);
    Assert.Equal(assignedGuardian, createRow.ActorId);
    Assert.Equal(AccessVia.Guardian, createRow.Via);
    Assert.Equal(AccessOutcome.Allow, createRow.Outcome);

    // (c) one guardian.assign row, ActorId = the ASSIGNING guardian (S·5).
    var assignRow = await LastAuditAsync(store, "guardian.assign", link.Id, ct);
    Assert.Equal(assigningGuardian, assignRow.ActorId);
    Assert.Equal(assigningGuardian, assignRow.EffectivePrincipalId);
    Assert.Equal(AccessVia.Guardian, assignRow.Via);
    Assert.Equal(AccessOutcome.Allow, assignRow.Outcome);

    // (d) both rows target the same link id, TargetKind = "guardian-link".
    Assert.Equal(link.Id, createRow.TargetId);
    Assert.Equal(link.Id, assignRow.TargetId);
    Assert.Equal("guardian-link", createRow.TargetKind);
    Assert.Equal("guardian-link", assignRow.TargetKind);

    // (e) S·6 — idempotency: a second call with the same pair is a no-op —
    //     the row count stays 1, the audit-row counts stay 1 each.
    await userInfo.AssignGuardianLinkAsync(
        child, assignedGuardian, assigningGuardian);

    await using var q2 = store.QuerySession();
    var linkCount2 = await Marten.QueryableExtensions.CountAsync(
        q2.Query<GuardianLink>()
            .Where(l => l.GuardianId == assignedGuardian && l.ChildId == child),
        ct);
    Assert.Equal(1, linkCount2);

    var createCount2 = await CountAuditAsync(store, "guardian.create", link.Id, ct);
    var assignCount2 = await CountAuditAsync(store, "guardian.assign", link.Id, ct);
    Assert.Equal(1, createCount2);
    Assert.Equal(1, assignCount2);
}
```

The test also needs the **one** helper `CountAuditAsync` (the GU lane's
shape, already in `GuardianControlsTests.cs` — the `LastAuditAsync`
counterpart). Add it **after** the existing `LastAuditAsync` helper in the
file (the `Handoff-leg helpers (2026-09-17)` block):

```csharp
/// <summary>The count of <see cref="AccessAudit"/> rows for an action +
/// target (the GU lane's <c>CountAuditAsync</c> shape) — the idempotency
/// sub-assertion reads the audit-row counts.</summary>
private static async Task<int> CountAuditAsync(
    IDocumentStore store, string action, string targetId, CancellationToken ct)
{
    await using var session = store.QuerySession();
    return await Marten.QueryableExtensions.CountAsync(
        session.Query<AccessAudit>()
            .Where(a => a.Action == action && a.TargetId == targetId),
        ct);
}
```

**No** other test changed (the 4 existing Core tests are byte-identical).

## Exit

`dotnet build Kumunita.slnx -c Debug` green (0 warnings, 0 errors). Run
via the **reliable path** (AGENTS.md): `dotnet exec tests\Kumunita.Core.Tests\
bin\Debug\net10.0\Kumunita.Core.Tests.dll -class
"Kumunita.Core.Tests.GuardianAssignmentTests"` → reports **5 tests
discovered, 5 executed** (the 4 existing + the 1 new). Record the
pass/red status of each (for U02's gate). **No docs** (U02). **Clean up
Docker:** if the process was killed, `docker container prune`.

**Handoff note** — 5–7 lines starting `## U01 — AssignGuardianLinkAsync
seam + test`:
- (a) the seam's exact signature (verbatim);
- (b) the impl's audit-row shape (both rows, the `Action` verbs + the
  `ActorId` values, verbatim);
- (c) the 1 new test name (verbatim);
- (d) the pass/red counts (5/5 expected);
- (e) a confirmation `CreateGuardianLinkAsync` + the `GuardianLink` POCO
  are **untouched** (S·2, S·3 — "byte-identical, confirmed by reading the
  file");
- (f) any `## U01 — Drift pause`.
