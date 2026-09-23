# M5 — Projects (to-dos + Kanban boards) — rolling handoff notes

One `## U#` section per unit, appended (never rewritten). Each unit writes
exactly one short section before it exits; the next unit reads only that
section + its own entry-read list. Moves with the lane folder at close (U13).

## U00 — design doc Part 1

Part 1 of the M5 design doc authored: `docs/design/m5-projects-design.md`
(8 sections — what it is / the reused surface (verified) / decisions /
invariants / FACES / parts affected / feedback loops / risks) — **no code,
no build, no tests**. **Decisions [PROPOSED]:** D1, D2, D3, D4, D5, D6, D7,
D8, D8a, D9, D10, D11, D12 (D8a = the no-draft-lane non-decision).
**Invariants (11):** C-M5·1, C-M5·2, C-M5·3, C-M5·4, C-M5·5, C-M5·6,
C-M5·7, C-M5·8, C-M5·9, C-M5·10, C-M5·11. **FACES (10):** F1, F2, F3, F4,
F5, F6, F7, F8, F9, F10. **ADR 0067 not yet authored (U01 locks it); Part 2**
(exact C# shapes, the full `IProjectService` surface, the ~24 pinned test
names, the three-test gate, the drift-guard) **is U01**.

## U01 — design doc Part 2 + ADR 0067 + roadmap confirm

Part 2 (`## Seams & contracts (Part 2, written by U1)`, §2.1–§2.10) appended
to `docs/design/m5-projects-design.md` (1195 lines); **ADR 0067 authored and
Accepted** (`docs/adr/0067-m5-projects-todos-and-kanban.md`, 2026-09-23,
Amends the 0001-B/0006/0036/0025/0031/0037/0024/0018/0044/0019-0020/
0014-0016-0017/0013 set); **no code, no build, no tests**. **`IProjectService`
(§2.3, frozen verbatim for U04):** read — `ListTodosAsync`, `GetTodoAsync`,
`ListBoardsAsync`, `GetBoardAsync`; write — `CreateTodoAsync`,
`UpdateTodoAsync`, `AssignTodoAsync`, `AddSubtaskAsync`, `DeleteTodoAsync`,
`CreateBoardAsync`, `UpdateLaneAsync`, `DeleteBoardAsync`; placement —
`MoveTodoWithinLaneAsync`, `MoveTodoToAdjacentLaneAsync`,
`MoveTodoToBoardAsync`, `CopyTodoToBoardAsync`. **16 Core pins** (design doc
§2.7, file `ProjectServiceTests.cs`): F1 `F1_TodoVisibleToAudienceMember`,
`F1_TodoHiddenFromNonMember`; F2 `F2_TodoPlacedOnMultipleBoards_AppearsOnEach`;
F3 `F3_BoardGate_TodoOnBoard_BoardDenies_HidesCard`,
`F3_BoardGate_TodoOnBoard_BoardAllows_ShowsCard`; F4
`F4_MoveToStatusLane_SetsTodoStatus_Audited`; F5
`F5_MoveToNullStatusLane_TodoStatusUnchanged`; F6 `F6_LaneAtMaxRefusesMoveIn`,
`F6_LaneBelowMaxAllowsMoveIn`; F7 `F7_AssigneeHasStanding_UpdatesTodo`; F8
`F8_NonStandaloneActorRefused`; F9 `F9_SubtaskIsFullTodo_OwnStatusAssignee`,
`F9_DeleteCascadesToSubtree`, `F9_ReparentToDescendant_Refused`; F10
`F10_CopyToBoard_Duplicates_OriginalUntouched`,
`F10_MoveToBoard_RelocatesPlacement`. **8 Web pins** (§2.8, file
`ProjectsControllerTests.cs`): `Todos_List_AudienceFiltered`,
`Todo_Detail_SubtasksRendered`, `Board_Detail_LanesAndCards`,
`Board_MoveLeftRight_TriggerStatusUpdate`, `Board_LaneAtMax_RefusesMoveIn`,
`Board_CopyToBoard_Duplicates`, `Board_MoveToBoard_Relocates`,
`Todo_AssignToUser_StandingGranted`. **Three-test gate** (§2.9, U13 records):
closed loop / handoff / part-vs-whole. **Roadmap confirm:** M5 stays
`StatusNext`, M6 stays `StatusPlanned` — **no roadmap move, no file change**
(U13's close is the only move: M5 → `StatusDone`, M6 → `StatusNext`).

## U02 — model + surface + wiring

**Five new files:** (a) the four POCOs, `src/Kumunita.Core/Projects/` —
`TodoItem.cs`, `KanbanBoard.cs`, `KanbanLane.cs`, `BoardItemPlacement.cs`
(verbatim §2.2; no `BoardId`/`LaneId`/`Order` on `TodoItem`, no `IsDraft`,
no `Audience` on `KanbanLane`/`BoardItemPlacement`); (b) the registration
surface, `src/Kumunita.Core/M5DocTypes.cs`. **One edit site:**
`src/Kumunita.Web/Program.cs`, `M5DocTypes.Configure(opts);` added immediately
after `M4DocTypes.Configure(opts);` (both boot paths pick the surface up
automatically). **`dotnet build Kumunita.slnx -c Debug` is green.**
**One deviation (recorded per the unit-series drift rule):** design-doc §2.4
pins the feed/lookup indexes as named `.Index(expr, "idx_…")`, but Marten
9.31.2 / Weasel 9.29.0 expose no way to name a computed index (the only
overloads are `Index(expr)` and `Index(expr, Action<ComputedIndex>)`, and
`ComputedIndex` has no `Name` property — only `Casing`/`TenancyScope`).
`M5DocTypes.cs` therefore uses the unnamed `.Index(expr)` form (the exact
`M4DocTypes` precedent) for `TodoItem` (×2) and `KanbanBoard` (×1); all
unique indexes are unaffected.

## U03 — adapters

Two new files, `src/Kumunita.Core/Projects/` —
`TodoItemToAuditableResource.cs` (`TargetKind => "todo"`) and
`KanbanBoardToAuditableResource.cs` (`TargetKind => "board"`), each a
`sealed class` over the frozen `IAuditableResource` 6-member surface,
mirroring `EventToAuditableResource`. `Name` is the 60-char
title-then-body/description pin (57 + "..."), null-safe via a small
private `Truncate` helper (the body/description is optional — degenerate
case = empty `Name`). **`dotnet build Kumunita.slnx -c Debug` is green.**
