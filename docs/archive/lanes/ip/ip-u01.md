# IP · U01 — `AccessAuditFactory.SingleTarget` + the three `StoreAuditRow` deletions

**You are the U01 agent.** This is a **Core** unit. It creates **one** new
static helper (`AccessAuditFactory`) in `Kumunita.Core.Authorization` (alongside
`AccessAudit` itself), and then **deletes** the three private `StoreAuditRow`
methods from `EventService`, `InventoryService`, and `ProjectService`,
replacing every call site with the factory. It does **not** touch the
`*AuditViaFor` mappers (U02), does **not** add a new `AccessAudit` document,
does **not** add a new `DocTypes` surface, does **not** add a DI registration,
and does **not** touch the Web project. **Exit: `dotnet build Kumunita.slnx
-c Debug` clean + `Kumunita.Core.Tests` green.**

**Read the register first** — D1, D2, C-IP·1, C-IP·2, GATE-1, the §drift-guard.
If the codebase's actual call-site count differs from the register's estimate,
**the codebase wins for mechanics** (the actual number of call sites); the
register wins for the **locked surface** (the factory is a pure static class,
the caller stores the row, the row shape is unchanged).

## Goal

Make the single-target `AccessAudit` row **one contract** instead of three
near-identical private methods. The factory is a **pure static helper** — it
returns a fully-populated `AccessAudit` object; the caller stores it in their
own `IDocumentSession` (the "caller's session" idiom — C3: the audit row
commits atomically with the domain write). **Zero new authorization surface**
(C-IP·2): no new `AccessAction`, no new `AccessVia` value, no new
`IAuthorizationService` method, no new `TargetKind`, no new `DocTypes`
surface, no new DI registration.

## Entry reads (7)

1. `docs/plans-milestones/plan-ip-integration-polish.md` — the register
   (D1, D2, C-IP·1/2, the locked C# for `AccessAuditFactory`).
2. `src/Kumunita.Core/Authorization/AccessAudit.cs` — the **real** `AccessAudit`
   POCO: the 9 fields (`Id`, `At`, `ActorId`, `EffectivePrincipalId`,
   `Action`, `TargetKind`, `TargetId`, `VisibleCount`, `HiddenCount`, `Via`,
   `Outcome`), the "two shapes" doc-comment (single-target vs. aggregate),
   and the `AuditPurgeSummary` / `AuditPurgeJob` siblings.
3. `src/Kumunita.Core/Events/EventService.cs` lines 1693–1720 — the **real**
   `StoreAuditRow` (signature: `session, actorId, action, targetId, via`;
   hardcodes `TargetKind = "event"`, `Outcome = AccessOutcome.Allow`,
   `EffectivePrincipalId = actorId`), and the 8 call sites that invoke it.
4. `src/Kumunita.Core/Inventory/InventoryService.cs` lines 490–540 — the
   **real** `StoreAuditRow` (signature: `session, actorId, action, targetId,
   via, outcome`; hardcodes `TargetKind = "inventory"`,
   `EffectivePrincipalId = actorId`), the `AuditVia` helper (line 495 —
   **different** pattern, role-based, **not** covered by this unit), and the
   9 call sites.
5. `src/Kumunita.Core/Projects/ProjectService.cs` lines 3490–3530 — the
   **real** `StoreAuditRow` (signature: `session, actorId, action, targetId,
   targetKind, via`; hardcodes `Outcome = AccessOutcome.Allow`,
   `EffectivePrincipalId = actorId`), and the ~45 call sites (the `TargetKind`
   is a parameter, not a constant).
6. `tests/Kumunita.Core.Tests/` (the audit-related test files) — the test
   harness shape so the factory's pin tests follow the existing pattern.
7. `src/Kumunita.Core/DependencyInjection.cs` — confirm that `AccessAudit`
   is **not** registered via DI (it's a POCO stored directly; the factory
   follows the same shape — a static class, no DI).

## Deliverables (4)

### 1. `src/Kumunita.Core/Authorization/AccessAuditFactory.cs` — new file

A **pure static class** in the `Kumunita.Core.Authorization` namespace
(alongside `AccessAudit` itself). One method:

```csharp
namespace Kumunita.Core.Authorization;

/// <summary>
/// The <b>single</b> factory for single-target <see cref="AccessAudit"/> rows
/// (invariant C-IP·1). A <b>pure static helper</b> — it returns a
/// fully-populated <see cref="AccessAudit"/>; the <b>caller</b> stores it in
/// their own <c>IDocumentSession</c> (the "caller's session" idiom — C3: the
/// audit row commits atomically with the domain write, in the caller's
/// transaction).
/// <para>
/// Replaces the three private <c>StoreAuditRow</c> copies that previously
/// lived in <c>EventService</c>, <c>InventoryService</c>, and
/// <c>ProjectService</c> (each independently re-decided
/// <c>EffectivePrincipalId</c>, <c>At</c>, and <c>Id</c> — the seam
/// drift risk this factory eliminates).
/// </para>
/// <para>
/// <b>Zero new authorization surface</b> (C-IP·2): no new
/// <c>AccessAction</c>, no new <c>AccessVia</c> value, no new
/// <c>IAuthorizationService</c> method, no new <c>TargetKind</c>, no new
/// <c>DocTypes</c> surface, no DI registration.
/// </para>
/// </summary>
public static class AccessAuditFactory
{
    /// <summary>
    /// Creates a single-target <see cref="AccessAudit"/> row (C3 —
    /// "always-on, in-transaction"). The row is <b>not</b> stored here; the
    /// caller stores it in their own session.
    /// <para>
    /// <c>EffectivePrincipalId</c> is set to <paramref name="actorId"/> —
    /// the actor acts as themself (no delegation in write lanes; the
    /// break-glass-attached shape is the aggregate-row path, not this one).
    /// </para>
    /// </summary>
    /// <param name="actorId">The acting account.</param>
    /// <param name="action">The action string (e.g. "event.create",
    /// "inventory.checkout", "todo.update").</param>
    /// <param name="targetKind">The resource kind (e.g. "event",
    /// "inventory", "todo", "board", "goal", "project").</param>
    /// <param name="targetId">The resource id (the single-target shape).</param>
    /// <param name="via">The <see cref="AccessVia"/> tag (Owner / Admin /
    /// etc.) — the standing the actor qualified under.</param>
    /// <param name="outcome">The <see cref="AccessOutcome"/> (Allow / Deny).
    /// Defaults to <see cref="AccessOutcome.Allow"/> (the three previous
    /// copies each defaulted to Allow in different ways; the factory
    /// unifies them).</param>
    /// <returns>A fully-populated <see cref="AccessAudit"/> ready for the
    /// caller to store.</returns>
    public static AccessAudit SingleTarget(
        string actorId,
        string action,
        string targetKind,
        string targetId,
        AccessVia via,
        AccessOutcome outcome = AccessOutcome.Allow)
        => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            At = DateTimeOffset.UtcNow,
            ActorId = actorId,
            EffectivePrincipalId = actorId,
            Action = action,
            TargetKind = targetKind,
            TargetId = targetId,
            VisibleCount = null,
            HiddenCount = null,
            Via = via,
            Outcome = outcome,
        };
}
```

> **Match the existing `AccessAudit` field names exactly** (read
> `AccessAudit.cs` before writing — the fields are `Id`, `At`, `ActorId`,
> `EffectivePrincipalId`, `Action`, `TargetKind`, `TargetId`,
> `VisibleCount`, `HiddenCount`, `Via`, `Outcome`). The factory sets all
> of them; `VisibleCount` and `HiddenCount` are explicitly `null` (the
> single-target shape — the aggregate shape is a different row type, not
> covered by this factory).

### 2. `src/Kumunita.Core/Events/EventService.cs` — delete `StoreAuditRow`, replace call sites

**Delete** the private method `StoreAuditRow` (lines 1697–1715, the 5-parameter
version: `session, actorId, action, targetId, via`).

**Replace** every call site. The EventService `StoreAuditRow` hardcoded
`TargetKind = "event"` and `Outcome = Allow`. The factory takes `targetKind`
and `outcome` as parameters. The replacement pattern is:

```csharp
// Before:
StoreAuditRow(session, actorId, "event.create", @event.Id, AccessVia.Owner);

// After:
session.Store(AccessAuditFactory.SingleTarget(
    actorId, "event.create", "event", @event.Id, AccessVia.Owner));
```

There are **8 call sites** in `EventService` (grep for
`StoreAuditRow(session` in the file). Each one:
- Drops the `session` parameter (the factory doesn't take it; the caller
  calls `session.Store(...)` directly).
- Adds the `targetKind` argument: `"event"` (hardcoded, the same as the
  old `StoreAuditRow`).
- Keeps `outcome` as the default `AccessOutcome.Allow` (all 8 EventService
  call sites are Allow).

**Add** `using Kumunita.Core.Authorization;` to the file's `using` block
if it's not already present (it is — `AccessVia` and `AccessOutcome` are
already used; confirm the `using` is there and don't duplicate it).

### 3. `src/Kumunita.Core/Inventory/InventoryService.cs` — delete `StoreAuditRow`, replace call sites

**Delete** the private method `StoreAuditRow` (lines 507–525, the 6-parameter
version: `session, actorId, action, targetId, via, outcome`).

**Replace** every call site. The InventoryService `StoreAuditRow` hardcoded
`TargetKind = "inventory"`. The replacement pattern is:

```csharp
// Before:
StoreAuditRow(session, actorId, "inventory.checkout", item.Id, AuditVia(actorRoles), AccessOutcome.Deny);

// After:
session.Store(AccessAuditFactory.SingleTarget(
    actorId, "inventory.checkout", "inventory", item.Id,
    AuditVia(actorRoles), AccessOutcome.Deny));
```

There are **9 call sites** in `InventoryService` (grep for
`StoreAuditRow(session` in the file). Each one:
- Drops the `session` parameter.
- Adds the `targetKind` argument: `"inventory"` (hardcoded).
- Keeps the `outcome` argument (some are `AccessOutcome.Deny`, some
  `AccessOutcome.Allow` — the factory's `outcome` parameter handles both).

> **Note:** the `InventoryService.AuditVia(IReadOnlySet<string> actorRoles)`
> helper (line 495) is a **different** pattern (role-based, not
> author-based). It is **not** covered by this unit or by U02's
> `StandingMatrix`. Leave it in place.

### 4. `src/Kumunita.Core/Projects/ProjectService.cs` — delete `StoreAuditRow`, replace call sites

**Delete** the private method `StoreAuditRow` (lines 3498–3520, the 6-parameter
version: `session, actorId, action, targetId, targetKind, via`).

**Replace** every call site. The ProjectService `StoreAuditRow` took
`targetKind` as a parameter (the `TargetKindTodo`, `TargetKindBoard`,
`TargetKindGoal`, `TargetKindProject` constants) and hardcoded
`Outcome = Allow`. The replacement pattern is:

```csharp
// Before:
StoreAuditRow(session, actorId, "todo.update", todo.Id, TargetKindTodo, TodoAuditViaFor(actorId, todo));

// After:
session.Store(AccessAuditFactory.SingleTarget(
    actorId, "todo.update", TargetKindTodo, todo.Id,
    TodoAuditViaFor(actorId, todo)));
```

There are **~45 call sites** in `ProjectService` (grep for
`StoreAuditRow(session` in the file). Each one:
- Drops the `session` parameter.
- Keeps the `targetKind` argument (the `TargetKind*` constants — these
  stay; the factory takes `targetKind` as a parameter).
- Keeps `outcome` as the default `AccessOutcome.Allow` (all ProjectService
  call sites are Allow).

> **Note:** the four `*AuditViaFor` methods (`TodoAuditViaFor`,
> `BoardAuditViaFor`, `GoalAuditViaFor`, `ProjectAuditViaFor`) are
> **not** deleted in this unit — they are U02's concern. In U01, the call
> sites still reference them: `TodoAuditViaFor(actorId, todo)` stays as-is
> (the factory's `via` parameter accepts whatever the caller computes).

### 5. Pin tests — `AccessAuditFactory` field mapping

In the existing Core test project (find the audit-related test file, or add
a new test class in `tests/Kumunita.Core.Tests/`), add a test class:

```csharp
public class AccessAuditFactoryTests
{
    [Fact]
    public void SingleTarget_SetsAllFields()
    {
        var row = AccessAuditFactory.SingleTarget(
            actorId: "actor-1",
            action: "event.create",
            targetKind: "event",
            targetId: "evt-1",
            via: AccessVia.Owner);

        Assert.Equal("actor-1", row.ActorId);
        Assert.Equal("actor-1", row.EffectivePrincipalId);
        Assert.Equal("event.create", row.Action);
        Assert.Equal("event", row.TargetKind);
        Assert.Equal("evt-1", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);  // default
        Assert.NotNull(row.Id);
        Assert.NotEmpty(row.Id);
        Assert.True(row.At > DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Null(row.VisibleCount);
        Assert.Null(row.HiddenCount);
    }

    [Fact]
    public void SingleTarget_ExplicitDeny_SetsOutcome()
    {
        var row = AccessAuditFactory.SingleTarget(
            "actor-2", "inventory.checkout", "inventory", "item-1",
            AccessVia.Admin, AccessOutcome.Deny);

        Assert.Equal(AccessOutcome.Deny, row.Outcome);
    }
}
```

## Exit (build + Core test assembly)

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

Both must be green. Additionally:

- `grep -r "private static void StoreAuditRow" src/Kumunita.Core/` returns
  **zero** matches (the three copies are deleted).
- `grep -r "AccessAuditFactory.SingleTarget" src/Kumunita.Core/` returns
  **~62** matches (8 + 9 + ~45 call sites).
- `AccessAuditFactoryTests` pass (the two pin tests above).
