---
id: is10-prompt
title: "IS10 prompt — fix the TodoDetail board-title leak (S4/E4, the one confirmed leak)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS10 — Fix the `TodoDetail` board-title leak

## What you are doing

You are the **tenth working agent** on the **IMPROVE-SEC** lane (plan:
`docs/plans-milestones/plan-improve-sec.md`). IS00–IS09 are committed. The
**S3/E3** work is fully done (IS05 floor, IS06 `LogRedact`, IS07/IS08
redaction + gate, IS09 docs). Your unit starts **Phase B — S4:
decision-free reads** (E4). E4 measured 52 methods that read an
audience-bearing type with no in-method decision; **one is a confirmed
leak** — `TodosController.TodoDetail` shows the **title of a board the
to-do sits on** when that board is audience-restricted to a different
audience, with no decision and no audit. Your unit fixes that leak by
routing the board-title lookup through the **already-existing,
`CanSeeAsync`-filtered** seam `IBoardService.ListBoardsForTodoAsync`, and
drops placements and lanes whose board is not in the visible set. **The
other 51 reads are IS11–IS14's triage; the gate that pins 52 → 0 is IS15's.**
You fix **only** this leak and add its tests.

**Deliverables (2 files + the handoff-notes append):**

1. `src/Kumunita.Web/Controllers/TodosController.cs` — the `TodoDetail`
   action body only (the F2 "to-do's board placements" block). Replace the
   raw `session.Query<KanbanBoard>()` title lookup with the injected
   `IBoardService.ListBoardsForTodoAsync(id, actorId, …)`, and drop
   placements and lanes whose `BoardId` is not in the visible set. **No
   constructor change.**
2. `tests/Kumunita.Web.Tests/TodoDetailBoardVisibilityTests.cs` (new, 3
   tests).
3. `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md` — the
   `## IS10` section (3–8 lines).

**The 3 tests (design §2.3, exact names):**

| Test | Asserts |
|---|---|
| `TodoDetail_RestrictedBoardTitle_NotShown` | a board the actor cannot see does **not** appear in `placementRows` (no `BoardTitle`, no `LaneTitle`) |
| `TodoDetail_VisibleBoardTitle_Shown` | a board the actor **can** see still renders its title + lane chip |
| `TodoDetail_RestrictedBoardPlacement_NoLaneChip` | the lane on a restricted board is dropped, so no lane chip renders for it |

## The fix (verify each anchor with the read, then edit)

The F2 block in `TodoDetail` (≈ `:257`–`:290`) currently does this, in order:

```
placements = session.Query<BoardItemPlacement>().Where(p => p.TodoItemId == id)…
boardIds   = placements.Select(p => p.BoardId).Distinct()…
boards     = session.Query<KanbanBoard>().Where(b => boardIds.Contains(b.Id))…   // ← THE LEAK: no decision
lanes      = session.Query<KanbanLane>().Where(l => laneIds.Contains(l.Id))…
boardTitle = boards.ToDictionary(b => b.Id, b => b.Title)
laneTitle  = lanes.ToDictionary(l => l.Id, l => l.Title)
placementRows = placements.Select(p => new TodoPlacementRow(… BoardTitle: boardTitle[…], LaneTitle: laneTitle[…], …))
```

Replace the **board** fetch with the decision-filtered seam and gate the
other two on the visible set. The injected service is already a
constructor dependency (`IBoardService boards`, line 40) — do **not** add
`IAuthorizationService` (ADR 0006 frozen signatures; the controller has no
authorization seam to widen):

```
// C6/C3 — route the board-title lookup through the CanSeeAsync-filtered seam
// (IS10, E4): ListBoardsForTodoAsync decides per-request whether the actor
// may read each board; placements + lanes are display data, kept only for
// boards in the visible set.
var visibleBoards = await boards.ListBoardsForTodoAsync(id, actorId, HttpContext.RequestAborted);
var visibleBoardIds = new HashSet<string>(visibleBoards.Select(b => b.Id), StringComparer.Ordinal);

IReadOnlyList<BoardItemPlacement> placements = [];
IReadOnlyList<KanbanLane> lanes = [];
await using (var session = store.QuerySession())
{
    var allPlacements = await session.Query<BoardItemPlacement>()
        .Where(p => p.TodoItemId == id).OrderBy(p => p.Order)
        .ToListAsync(HttpContext.RequestAborted);
    placements = allPlacements.Where(p => visibleBoardIds.Contains(p.BoardId)).ToList();
    var laneIds = placements.Select(p => p.LaneId).Distinct(StringComparer.Ordinal).ToList();
    lanes = laneIds.Count > 0
        ? await session.Query<KanbanLane>()
            .Where(l => laneIds.Contains(l.Id) && visibleBoardIds.Contains(l.BoardId))
            .ToListAsync(HttpContext.RequestAborted)
        : [];
}
var boardTitle = visibleBoards.ToDictionary(b => b.Id, b => b.Title);
var laneTitle  = lanes.ToDictionary(l => l.Id, l => l.Title);
// placementRows: unchanged shape; placements is now already filtered to the visible set
```

**Known gotcha (name it in the handoff if you hit it):** the injected
service is named `boards` and the **old** local was also named `boards` —
after the fix the local `IReadOnlyList<KanbanBoard> boards` is gone and
`boards` resolves to the injected service, so the call
`boards.ListBoardsForTodoAsync(…)` is unambiguous. If you keep any local
named `boards`, rename it (e.g. `visibleBoards`) so the service call is not
shadowed.

**Do not** change the F2 comment's intent (the board title is still "a
display lookup, never a gate") — now it is a *decided* display lookup. Do
not touch the ADR 0086 project-link block below it, or any other action.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`.
- **IS09 is the current HEAD**
  (`docs(improve-sec): IS09 — trust-boundary + log-hygiene docs`). IS04's
  gate harness exists (`tests/Kumunita.Web.Tests/SecGate/` — `SecGateSource.cs`,
  `AccessViaDenyMatrixTests.cs`, `LogTemplatePiiGateTests.cs`) and is your
  dependency ("Needs: IS04").
- The leak is present at HEAD: `grep -n "session.Query<KanbanBoard>()"
  src/Kumunita.Web/Controllers/TodosController.cs` → **1 hit** (≈ :275, the
  unfixed title lookup). After your fix this must be **0 hits**.
- The decision path you reuse already exists and is tested:
  `IBoardService.ListBoardsForTodoAsync(string todoItemId, string actorId,
  CancellationToken ct = default)` (`src/Kumunita.Core/Projects/IBoardService.cs:77`)
  → `BoardService.ListBoardsForTodoAsync` (≈ :214–250) resolves the to-do's
  board placements and filters them through
  `_authorization.CanSeeAsync(actorId, AccessAction.Read, …)` — i.e. it
  returns **only the boards the actor may read**, exactly the decision the
  leak lacks.
- `TodosController` already injects `IBoardService boards` (line 40) — no
  constructor change.
- The existing controller-construction pattern for tests is in
  `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` (≈ :2425,
  `new TodosController(projects, …, boards: (IBoardService)projects,
  todos: (ITodoService)projects)`).
- Commit style (code + test unit, like IS05–IS08):
  `test(improve-sec): IS10 — fix the TodoDetail board-title leak`.

## Reading order

1. `AGENTS.md` and `.github/copilot-instructions.md`.
2. The **IS10** unit spec under "Phase B — S4: decision-free reads" of
   `docs/plans-milestones/plan-improve-sec.md` — your contract (2 files +
   handoff, no constructor change, 3 named tests).
3. `docs/design/improve-sec-design.md` — the **IS10** row of **§2.3** (the 3
   test names) and **§2.4** (anchor: `TodosController.cs` — the
   `ListBoardsForTodoAsync` call in the `TodoDetail` action body;
   `grep -n "session.Query<KanbanBoard>()" src/Kumunita.Web/Controllers/TodosController.cs`
   → **0 hits** after).
4. `src/Kumunita.Web/Controllers/TodosController.cs` — the `TodoDetail`
   action (≈ :222) and the F2 block (≈ :257–:290); the constructor (≈ :35).
   **Read ranges only** (`grep -n` then `sed -n 'A,Bp'`); the file is large —
   never read it whole.
5. `src/Kumunita.Core/Projects/IBoardService.cs` ≈ :77 (the seam) and
   `src/Kumunita.Core/Projects/BoardService.cs` ≈ :214–:250 (the
   `CanSeeAsync` filter) — to confirm the shape before you call it.
6. `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` — the `new
   TodosController(…)` setup (≈ :2425, ≤ 60 lines) and how it fakes
   `IBoardService`/`IProjectService`, to mirror in your new test file.

## The work

1. **The fix** — in `TodoDetail`'s F2 block, replace the raw
   `session.Query<KanbanBoard>()` lookup with
   `boards.ListBoardsForTodoAsync(id, actorId, HttpContext.RequestAborted)`;
   keep the raw placements + lanes queries but **filter both to the visible
   board set** (`visibleBoardIds`); rebuild `boardTitle` from the visible
   boards. No constructor change, no other action touched.
2. **The tests** — `tests/Kumunita.Web.Tests/TodoDetailBoardVisibilityTests.cs`
   with the 3 named tests. Seed: a to-do with two placements — one board the
   actor may read, one restricted board the actor may not (fake
   `IBoardService.ListBoardsForTodoAsync` to return only the visible board,
   and/or fake `IProjectService.GetTodoAsync` to return the to-do + subtasks).
   Assert `placementRows` / the returned view model: restricted board absent
   (no title, no lane chip), visible board present.
3. **Handoff** — append a `## IS10 — Fix the TodoDetail board-title leak`
   section (3–8 lines): files, the F2-block change (raw board query →
   `ListBoardsForTodoAsync` + placements/lanes filtered to the visible set),
   the 3 tests + pass count, the `boards`-shadowing note if you renamed a
   local, next-unit notes (IS11 triage batch 1), and anything noticed.

**You are NOT** doing IS11–IS14 (the other 51 decision-free reads / `SEC-READ`
markers), IS15 (the 52 → 0 gate), or any doc. You touch **only**
`TodosController.cs` (one action), the new test file, and the handoff notes.

## Verification (all green before you finish)

- [ ] `grep -n "session.Query<KanbanBoard>()"
      src/Kumunita.Web/Controllers/TodosController.cs` → **0 hits** (the raw
      title lookup is gone).
- [ ] `grep -n "ListBoardsForTodoAsync"
      src/Kumunita.Web/Controllers/TodosController.cs` → **1 hit** in the
      `TodoDetail` action (the decision seam is now called).
- [ ] `dotnet build` 0 errors.
- [ ] **3/3** `TodoDetailBoardVisibilityTests` pass — run the class with the
      xunit.v3 runner form (the repo's recorded form, not
      `dotnet test --filter`; see SEC U3 in
      `docs/archive/lanes/sec/sec-handoff-notes.md`).
- [ ] No regression: the pre-existing `TodosController` / board tests that
      touch `TodoDetail` still pass (e.g. any `ProjectsControllerTests`
      to-do-detail case) — if one asserted the *old* (leaky) behaviour,
      **pause** (rule 9) and say which, rather than editing around it.
- [ ] `git status` shows **only** `TodosController.cs`, the new test file, and
      the handoff-notes append. No doc, no control-map, no `src/**` beyond
      the one action.

## Definition of done (IS10)

- [ ] `TodoDetail` no longer discloses the title (or a lane chip) of a board
      the actor cannot read — the lookup goes through the
      `CanSeeAsync`-filtered `ListBoardsForTodoAsync`.
- [ ] The 3 named tests pass; the raw `session.Query<KanbanBoard>()` is gone
      from the controller.
- [ ] No constructor change; no `IAuthorizationService` added; no other
      action touched.
- [ ] `git status` shows only the controller, the new test file, and the
      handoff append.
- [ ] One commit: `test(improve-sec): IS10 — fix the TodoDetail board-title leak`.

**If a DoD line fails, stop and say which one and why — do not fix it
silently.** A failed check is a recoverable step, not a stopping point: read
it, adjust within the unit's scope, continue. If the *same* check failed
twice, change approach — don't retry the identical edit a third time.
