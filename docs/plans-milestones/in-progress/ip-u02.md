# IP · U02 — `StandingMatrix.AuditVia` + the five author-based mapper deletions

**You are the U02 agent.** This is a **Core** unit. It creates **one** new
static helper (`StandingMatrix`) in `Kumunita.Core.Authorization`, and then
**deletes** the five author-based standing→`AccessVia` mappers:
`AuditViaFor` (EventService), and `TodoAuditViaFor`, `BoardAuditViaFor`,
`GoalAuditViaFor`, `ProjectAuditViaFor` (ProjectService), replacing every
call site with the matrix. It does **not** touch
`InventoryService.AuditVia(IReadOnlySet<string>)` (a different pattern —
role-based, not author-based), does **not** touch the Web project, and does
**not** add any new authorization surface. **Exit: `dotnet build Kumunita.
slnx -c Debug` clean + `Kumunita.Core.Tests` green.**

**Read the register first** — D2, C-IP·1, C-IP·2, GATE-1, the §drift-guard.
**Read U01's handoff-notes section first** — U01 has already replaced every
`StoreAuditRow(session, …)` call with
`session.Store(AccessAuditFactory.SingleTarget(…))`. U02's call sites are the
*arguments* of those `SingleTarget` calls (the `via` parameter).

## Goal

Make the author-based standing→`AccessVia` mapping **one contract** instead
of five near-identical private methods. The matrix is a **pure static
helper** — it maps `actorId` vs. `ownerId` to `AccessVia.Owner` /
`AccessVia.Admin`; the caller passes the resource's owner id. **Zero new
authorization surface** (C-IP·2): no new `AccessVia` value, no new DI
registration, no new `AccessAction`, no new `TargetKind`.

## Entry reads (6)

1. `docs/plans-milestones/plan-ip-integration-polish.md` — the register
   (D2, C-IP·1/2, the locked C# for `StandingMatrix`).
2. `docs/plans-milestones/in-progress/ip-integration-polish-handoff-notes.md`
   — the U00 section (D# lock) + the U01 section (the factory signature,
   the call-site replacement pattern).
3. `src/Kumunita.Core/Projects/ProjectService.cs` lines 905–963 — the **real**
   four mappers. Each body is the same two-line expression:
   `string.Equals(resource.AuthorId, actorId, StringComparison.Ordinal)
   ? AccessVia.Owner : AccessVia.Admin;` (Todo: `todo.AuthorId`; Board:
   `board.AuthorId`; Goal: `goal.AuthorId`; Project: `project.AuthorId`).
4. `src/Kumunita.Core/Events/EventService.cs` lines 473–477 — the **real**
   `AuditViaFor`: same two-line expression (`authorId` parameter vs.
   `actorId`). Called at line 861 (`event.update`, `existing.AuthorId`)
   and line 957 (`event.delete`, `existing.AuthorId`).
5. `src/Kumunita.Core/Inventory/InventoryService.cs` lines 494–496 — the
   **real** `AuditVia(IReadOnlySet<string>)`: `IsGlobalAdmin(actorRoles)
   ? AccessVia.Admin : AccessVia.Owner`. **Different pattern** (role-based,
   no owner id in the signature). **Do not touch** (see §drift-guard:
   in scope = author-based only).
6. `tests/Kumunita.Core.Tests/ProjectServiceTests.cs` line 3580 — the
   existing test that references `TodoAuditViaFor` by name in a doc-comment
   (confirm the reference is a comment, not a code dependency — the method
   is `private static`, so no code outside `ProjectService` can call it;
   the test reference is prose).

## Deliverables (3)

### 1. `src/Kumunita.Core/Authorization/StandingMatrix.cs` — new file

A **pure static class** in the `Kumunita.Core.Authorization` namespace.
One method:

```csharp
namespace Kumunita.Core.Authorization;

/// <summary>
/// The <b>single</b> author-based standing→<see cref="AccessVia"/> mapper
/// (invariant C-IP·1). A <b>pure static helper</b> — it maps the actor's
/// standing to the audit tag; it does not decide the standing (the
/// caller's standing check already did), does not query, does not store,
/// and does not decide.
/// <para>
/// The pattern is uniform across events, todos, boards, goals, and
/// projects: the actor is the <b>author/owner</b> of the resource →
/// <see cref="AccessVia.Owner"/>; otherwise the actor qualified under an
/// elevated role (GlobalAdmin — the ADR 0028 / ADR 0041 append precedent)
/// → <see cref="AccessVia.Admin"/> (the "least-distortion slot" rule —
/// C-M5·11 / C-PL·2 forbid a new <c>AccessVia</c> value).
/// </para>
/// <para>
/// Replaces the five private mappers that previously lived in
/// <c>EventService</c> (<c>AuditViaFor</c>) and <c>ProjectService</c>
/// (<c>TodoAuditViaFor</c> / <c>BoardAuditViaFor</c> / <c>GoalAuditViaFor</c>
/// / <c>ProjectAuditViaFor</c>) — each an independent copy of the same
/// two-line expression (the seam drift risk this matrix eliminates).
/// </para>
/// <para>
/// <b>Zero new authorization surface</b> (C-IP·2): no new
/// <see cref="AccessVia"/> value, no new <c>AccessAction</c>, no new
/// <c>IAuthorizationService</c> method, no new <c>TargetKind</c>, no DI
/// registration.
/// </para>
/// <para>
/// <b>Not covered:</b> the role-based <c>InventoryService.AuditVia</c>
/// (<c>IsGlobalAdmin(actorRoles) ? Admin : Owner</c>) — a different
/// pattern (no owner id in the signature). It stays in place.
/// </para>
/// </summary>
public static class StandingMatrix
{
    /// <summary>
    /// Maps the actor's standing to the <see cref="AccessVia"/> audit tag
    /// for an author-based resource (event / todo / board / goal /
    /// project): the author/owner → <see cref="AccessVia.Owner"/>; a
    /// GlobalAdmin (or other elevated role) → <see cref="AccessVia.Admin"/>.
    /// </summary>
    /// <param name="actorId">The acting account.</param>
    /// <param name="ownerId">The resource's author/owner id
    /// (<c>event.AuthorId</c> / <c>todo.AuthorId</c> / <c>board.AuthorId</c>
    /// / <c>goal.AuthorId</c> / <c>project.AuthorId</c>).</param>
    /// <returns><see cref="AccessVia.Owner"/> if the actor is the
    /// owner; <see cref="AccessVia.Admin"/> otherwise.</returns>
    public static AccessVia AuditVia(string actorId, string ownerId)
        => string.Equals(ownerId, actorId, StringComparison.Ordinal)
            ? AccessVia.Owner
            : AccessVia.Admin;
}
```

### 2. `src/Kumunita.Core/Events/EventService.cs` — delete `AuditViaFor`, replace call sites

**Delete** the private method `AuditViaFor` (lines 473–477, including its
doc-comment).

**Replace** the 2 call sites:

```csharp
// Before (line 861):
StoreAuditRow(session, actorId, "event.update", existing.Id, AuditViaFor(actorId, existing.AuthorId));
// After U01 (the call site is now):
session.Store(AccessAuditFactory.SingleTarget(
    actorId, "event.update", "event", existing.Id, AuditViaFor(actorId, existing.AuthorId)));
// After U02:
session.Store(AccessAuditFactory.SingleTarget(
    actorId, "event.update", "event", existing.Id,
    StandingMatrix.AuditVia(actorId, existing.AuthorId)));
```

The same pattern at line 957 (`event.delete`, `existing.AuthorId`).

### 3. `src/Kumunita.Core/Projects/ProjectService.cs` — delete four mappers, replace call sites

**Delete** the four private methods (each with its doc-comment):
- `TodoAuditViaFor` (line 905)
- `BoardAuditViaFor` (line 937)
- `GoalAuditViaFor` (line 950)
- `ProjectAuditViaFor` (line 963)

**Replace** every call site (U01 has already changed the surrounding call to
`session.Store(AccessAuditFactory.SingleTarget(…))`; U02 changes the `via`
argument). The pattern is uniform:

```csharp
// After U01 (a call site):
session.Store(AccessAuditFactory.SingleTarget(
    actorId, "todo.update", TargetKindTodo, todo.Id,
    TodoAuditViaFor(actorId, todo)));

// After U02:
session.Store(AccessAuditFactory.SingleTarget(
    actorId, "todo.update", TargetKindTodo, todo.Id,
    StandingMatrix.AuditVia(actorId, todo.AuthorId)));
```

The owner property per resource (read the actual property name in each
mapper's body to confirm — the five mappers all use `.AuthorId`):

| Mapper deleted | Owner property |
| --- | --- |
| `TodoAuditViaFor(actorId, todo)` | `todo.AuthorId` |
| `BoardAuditViaFor(actorId, board)` | `board.AuthorId` |
| `GoalAuditViaFor(actorId, goal)` | `goal.AuthorId` |
| `ProjectAuditViaFor(actorId, project)` | `project.AuthorId` |

There are **~20 call sites** in `ProjectService` (grep for `AuditViaFor`
in the file — the 4 method definitions + their doc-comment references +
the call sites). Replace each call-site reference; the doc-comment
references to the deleted methods in other methods' `<see cref>` tags must
be updated to point at `StandingMatrix.AuditVia` (or removed, if the
prose still makes sense without the cref).

### 4. Pin tests — `StandingMatrix` two-branch shape

In `tests/Kumunita.Core.Tests/`, add a test class (or extend the existing
`AccessAuditFactoryTests` file from U01):

```csharp
public class StandingMatrixTests
{
    [Fact]
    public void AuditVia_Owner_ReturnsOwner()
    {
        Assert.Equal(AccessVia.Owner,
            StandingMatrix.AuditVia(actorId: "alice", ownerId: "alice"));
    }

    [Fact]
    public void AuditVia_NonOwner_ReturnsAdmin()
    {
        Assert.Equal(AccessVia.Admin,
            StandingMatrix.AuditVia(actorId: "bob", ownerId: "alice"));
    }

    [Fact]
    public void AuditVia_IsOrdinalCaseSensitive()
    {
        Assert.Equal(AccessVia.Admin,
            StandingMatrix.AuditVia(actorId: "Alice", ownerId: "alice"));
    }
}
```

## Exit (build + Core test assembly)

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

Both must be green. Additionally:

- `grep -rn "AuditViaFor\|TodoAuditViaFor\|BoardAuditViaFor\|GoalAuditViaFor\|ProjectAuditViaFor" src/Kumunita.Core/`
  returns **zero** matches in `EventService.cs` and `ProjectService.cs`
  (the five mappers are deleted; no call-site references remain).
- `grep -rn "StandingMatrix.AuditVia" src/Kumunita.Core/` returns
  **~22** matches (2 EventService + ~20 ProjectService call sites).
- `InventoryService.AuditVia` is **unchanged** (grep still finds it).
- `StandingMatrixTests` pass (the three pin tests above).
- **Do not** move this unit's plan to `done/` until the handoff-notes
  section is appended (U00 created the handoff-notes file in its unit).
