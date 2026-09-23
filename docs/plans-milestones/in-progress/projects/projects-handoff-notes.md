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

## U04 — service seam + read lanes

**Four new files, one edit:** (a) `src/Kumunita.Core/Projects/IProjectService.cs`
(the **full** frozen seam — read + write + placement, verbatim design doc §2.3:
4 read, 8 write, 4 placement); (b) `src/Kumunita.Core/Projects/ProjectService.cs`
(`sealed class`, constructor `(IDocumentStore, IAuthorizationService,
IUserInfoService)`, `PageSize = 30`); (c) `src/Kumunita.Core/Projects/
ProjectRequests.cs` (the 8 DTO records, verbatim §2.3); (d) **`IProjectService`
DI registration** in `src/Kumunita.Core/DependencyInjection.cs` (the
`AddTransient<Projects.IProjectService>` shape, placed immediately after the
`IEventService` registration). **Read lanes landed (4):**
`ListTodosAsync` (feed: `!IsDeleted` + optional `ComponentId` / `AssigneeId`
filter, `Created` descending, paged, one standalone `CanSeeAsync(Read)` over
`TodoItemToAuditableResource`); `GetTodoAsync` (detail: load → `IsDeleted`
404 → `CanAsync(Read)` 403 split → subtasks each individually
`CanAsync(Read)`-gated, ordered `Created` ascending, returned in a
`TodoDetailResult`); `ListBoardsAsync` (feed: `!IsDeleted` + optional
`ComponentId`, `Created` descending, paged, one `CanSeeAsync(Read)` over
`KanbanBoardToAuditableResource`); `GetBoardAsync` (detail: load → `IsDeleted`
404 → board `CanAsync(Read)` 403 split → lanes ordered `Order` ascending →
each lane's placements resolved to their `TodoItem` → each card individually
`CanAsync(Read)`-gated, soft-deleted cards excluded, denied cards **not
returned** — C-M5·3 two-level pin, returned in `BoardDetailResult` with
`LaneDetail` per lane). **Stubs landed (12):** all 8 write methods (U05) +
all 4 placement / reorder methods (U06) throw
`NotImplementedException` with the unit they land in named in the message
(the M4 U01 interface-first pin). **`IAuthorizationService` used with
**no** signature change, **no** new `AccessAction`, **no** new `AccessVia`,
**no** new branch in `Decide()` (C-M5·11). **`dotnet build Kumunita.slnx -c
Debug` is green** (4.8 s, 0 errors, 0 warnings).

## U05 — write lanes

**Eight write-lane stubs replaced in `src/Kumunita.Core/Projects/ProjectService.cs`:** (a) to-do write lanes — `CreateTodoAsync`, `UpdateTodoAsync`, `AssignTodoAsync`, `AddSubtaskAsync`, `DeleteTodoAsync`; (b) board write lanes — `CreateBoardAsync`, `UpdateLaneAsync`, `DeleteBoardAsync`. **Standing matrix (C-M5·6, enforced server-side via two new pure helpers):** to-do mutations — **creator ∪ assignee ∪ GlobalAdmin** (`CheckTodoStanding`, the ADR 0014/0016/0017 precedent + the ADR 0067 assignee collaborator branch); board / lane mutations — **creator ∪ GlobalAdmin** (`CheckBoardStanding`, the assignee branch does not apply to a board or a lane). **Cycle guard** (C-M5·7, `F9_ReparentToDescendant_Refused`): `UpdateTodoAsync` walks the descendant subtree via `ParentId` before applying a reparent; a would-be parent that is the to-do itself or one of its descendants throws `InvalidOperationException` and writes nothing. **Cascade** (C-M5·7): `DeleteTodoAsync` soft-deletes the full descendant subtree (BFS via `ParentId`); `DeleteBoardAsync` hard-deletes the board's `KanbanLane` + `BoardItemPlacement` rows, leaving the to-dos untouched (C-M5·2). **Audit rows:** `todo.create` / `todo.update` / `todo.assign` / `todo.add_subtask` / `todo.delete` / `board.create` / `board.update_lane` / `board.delete`, `TargetKind` `"todo"` / `"board"`, `Via` `Owner` (creator) or `Admin` (assignee / GlobalAdmin), `Outcome` `Allow` — stored atomically with each write (C3). **The 4 placement stubs are untouched** (U06). **`dotnet build Kumunita.slnx -c Debug` is green** (5.4 s, 0 errors, 0 warnings); **381/381 Web.Tests pass** (no regressions).

## U06 — placement + reorder lanes

**Four placement-lane stubs replaced in `src/Kumunita.Core/Projects/ProjectService.cs`:** `MoveTodoWithinLaneAsync` (`"up"`/`"down"` — swap `Order` with the adjacent card in the same lane; edge = no-op, nothing written, no audit row), `MoveTodoToAdjacentLaneAsync` (`"left"`/`"right"` — adjacent lane on the same board; `Order` = end of the target lane; edge = no-op), `MoveTodoToBoardAsync` (relocate, C-M5·8 — other boards' placements deleted, new placement on the target's first lane), `CopyTodoToBoardAsync` (duplicate, C-M5·8 — actor is the new `AuthorId`, **no** `ParentId`, original untouched). **Lane-status auto-update (C-M5·4):** a non-null target lane `Status` sets the to-do's `Status` in the same transaction (F4); a null lane `Status` leaves it unchanged (F5). **Lane-limit refusal (C-M5·5):** a move/copy into a lane at its `MaxItems` limit is refused — `InvalidOperationException` with the lane's `Title`, **nothing written** (F6); a shared private `RefuseIfLaneAtLimitAsync` gate serves the move-to/copy-to lanes (the within-lane reorder and the move-out never trip a limit). **Standing (C-M5·6) on all four:** creator ∪ assignee ∪ GlobalAdmin over the **to-do** via the existing `CheckTodoStanding` helper (the placement's board is not the standing surface); invalid `direction` = `ArgumentException`. **Audit rows:** `todo.move_within_lane` / `todo.move_to_lane` / `todo.move_to_board` / `todo.copy_to_board`, `TargetKind "todo"` (copy targets the copy's id — the `AddSubtaskAsync` precedent), `Via` per `TodoAuditViaFor`. **`dotnet build Kumunita.slnx -c Debug` is green** (4.7 s, 0 errors, 0 warnings); **381/381 Web.Tests pass** (no regressions).

## U07 — ProjectsController to-do routes + view models

**Two new files, zero edits to frozen surfaces:** (a) `src/Kumunita.Web/Models/ProjectTodoViewModels.cs` — `TodoRow`, `TodoIndexViewModel`, `TodoPlacementRow`, `TodoDetailViewModel` (records) + `TodoEditorModel` (class, `[Required] Title`, the `AudienceEditorModel Audience` sole access boundary, `ParentId`/`ClearParent` for the reparent partial shape, `[BindNever]` `Languages`/`Components`/`ParentOptions`) + `AddSubtaskModel`; (b) `src/Kumunita.Web/Controllers/ProjectsController.cs` — `[Authorize] sealed class`, the **8 to-do routes** verbatim from the U07 spec: `GET /projects/todos` (feed → `TodoIndexViewModel`), `GET /projects/todos/{id}` (detail → `TodoDetailViewModel` with `Subtasks` + F2 board placements), `GET /projects/todos/new` + `POST /projects/todos` (compose/create → `CreateTodoRequest`), `POST /projects/todos/{id}` (update → `UpdateTodoRequest` with `ParentId`/`ClearParent`), `POST /projects/todos/{id}/assign`, `POST /projects/todos/{id}/subtasks` (→ `CreateTodoRequest` with `ParentId = parentTodoItemId`), `POST /projects/todos/{id}/delete` (soft-delete, cascade is the service's). **Thin-HTTP per ADR 0006-D (M4 `EventController` precedent, mirrored verbatim):** `actorId`/`actorRoles` minted claim-based from `KumunitaPrincipal`, every access decision is the service's; the controller's **only** authz concern is the **C3 split** — `catch (KeyNotFoundException) → NotFound()`, `catch (UnauthorizedAccessException) → ForbidResult`; display-name / component-title resolution is a *read* (`IUserInfoService`), never a decision. **Seed-trio reused (not reinvented):** `SeedGrantPickerOptionsAsync` / `SeedLanguagePickerAsync` / `SeedComponentPickerAsync`. **F1/F7/F8/F9 are the service's** — the controller never re-derives standing or audience; the board-placement read (F2) runs only after the to-do's own Read decision. **`dotnet build Kumunita.slnx -c Debug` is green** (4.0 s, 0 errors, 0 warnings); **381/381 Web.Tests pass** (no regressions).

## U08 — ProjectsController board routes + board view models

**Two deliverables, zero edits to frozen surfaces:** (a) `src/Kumunita.Web/Models/ProjectBoardViewModels.cs` — `BoardRow`, `BoardIndexViewModel`, `LaneDetailRow`, `TodoCardRow` (records) + `BoardDetailViewModel` (record) + `BoardEditorModel` (class, `[Required] Title`, `AudienceEditorModel Audience` sole access boundary, `List<LaneEditorModel> Lanes`, `[BindNever]` `Languages`/`Components`) + `LaneEditorModel` (class, `Title`/`Status`/`MaxItems`/`Order`); (b) `src/Kumunita.Web/Controllers/ProjectsController.cs` — the **12 board routes** verbatim from the U08 spec: `GET /projects/boards` (feed → `BoardIndexViewModel`), `GET /projects/boards/{id}` (detail → `BoardDetailViewModel` with lanes + cards), `GET/POST /projects/boards` (composer/create → `CreateBoardRequest` + lanes), `POST /projects/boards/{id}/lanes/{laneId}` (→ `UpdateLaneRequest`), `POST /projects/boards/{id}/delete` (cascade is the service's), the 4 reorder endpoints (`move-up`/`move-down` → `MoveTodoWithinLaneAsync`; `move-left`/`move-right` → `MoveTodoToAdjacentLaneAsync`), `POST /projects/todos/{id}/copy-to` (→ `CopyTodoToBoardAsync`), `POST /projects/todos/{id}/move-to` (→ `MoveTodoToBoardAsync`). **Thin-HTTP per ADR 0006-D (M4 `EventController` precedent, mirrored verbatim):** `actorId`/`actorRoles` minted claim-based from `KumunitaPrincipal`, every access decision is the service's; the controller's **only** authz concern is the **C3 split** — `catch (KeyNotFoundException) → NotFound()`, `catch (UnauthorizedAccessException) → ForbidResult`; lane-limit refusals (`InvalidOperationException`, F6) redirect back with `TempData["error"]` (the M4 "a form is a shape" precedent); display-name resolution is a *read* (`IUserInfoService`), never a decision. **Board-placement read (F2):** `BoardDetail` resolves the card `BoardItemPlacement` rows via a read-only `IDocumentStore` query (the U07 `TodoDetail` board-placement idiom — a display convenience, never a gate; the two-level decision already ran in `GetBoardAsync`). **Seed-trio reused (not reinvented):** `SeedGrantPickerOptionsAsync` / `SeedLanguagePickerAsync` / `SeedComponentPickerAsync`. **F2/F3/F4/F5/F6/F10 are the service's** — the controller never re-derives standing or audience. **Deviation:** `TodoCardRow.PlacementId` / `Order` are not in the frozen `GetBoardAsync` return (the `LaneDetail.Cards` is `IReadOnlyList<TodoItem>`) — resolved via the U07 read-only `IDocumentStore` placement idiom (a display lookup, never a gate), not a service change (frozen-surface pin). **`dotnet build Kumunita.slnx -c Debug` is green** (7.4 s, 0 errors, 0 warnings); **381/381 Web.Tests pass** (no regressions).


## U09 — To-do Razor views + nav entry + action dropdowns

**Four new views under `src/Kumunita.Web/Views/Projects/` + the `nav.projects` entry in `_Layout.cshtml`:** `TodosIndex.cshtml` (feed, component filter, per-row action dropdown — Unassign → `POST /projects/todos/{id}/assign`, Add subtask → `POST /projects/todos/{id}/subtasks`, Delete → `POST /projects/todos/{id}/delete`, all thin POST forms; F1/F7/F8/F9 remain the service's), `TodoDetail.cshtml` (rich body, F9 subtasks + parent link, F2 board placements, same action dropdown + an Edit link), `Create.cshtml` (compose — `form method="post" action="/projects/todos"`, `common.title` label, AssigneeId select from `ViewData["Audience_Users"]`, ParentId select from `Model.ParentOptions`, rich-editor body, audience switch + `_GrantPickers`), `Edit.cshtml` (re-render — form action + cancel link use the request path since `TodoEditorModel` has no `Id`, `projects.todo.edit_heading` h1, `ClearParent` checkbox, `projects.todo.reparent_hint`). **Deviations (unit-series drift):** (a) **view filenames** — the spec's `TodoIndex`/`TodoNew`/`TodoEdit` names would 500 at runtime because Razor named-view resolution is by filename against the frozen controller's `View("Create")`/`View("Edit")` calls and `View(vm)` by action name (`TodosIndex`); renamed to match the frozen `ProjectsController`, not the other way around; (b) **kw-l keys registered now, all four languages** — 36 keys (`nav.projects` + 35 `projects.todo.*`) added to `KnownTranslationKeys.cs` in en/de/fr/da, pulled forward from U11: `KwLRegistryConsistencyTests` fails on any unregistered static key, and `TranslationProvider.GetAsync` falls back to the **raw key** (not English), so the spec's "English fallback until U11" premise is contradicted by `TranslationProvider.cs`; registering en-only would break `KnownTranslationKeys_ParityTests` (4-language set-equality); U11 still owns `projects.board.*` + the board TS/CSS; (c) **Detail "Edit" link gap** — the link targets `/projects/todos/{id}/edit`, which has **no GET route** in the frozen `ProjectsController` (only `POST /projects/todos/{id}` renders `Edit.cshtml`; M4's `EventController` precedent has the GET route) — a later unit must add the GET route or rework the link; `Edit.cshtml` posts to the request path, which works as the failed-POST re-render idiom. **`dotnet build Kumunita.slnx -c Debug` is green** (14.3 s, 0 errors, 0 warnings); **381/381 Web.Tests pass** (no regressions); **720/720 Core.Tests pass** (parity intact).

## U10 — board Razor views + card action dropdown


**Three new views under `src/Kumunita.Web/Views/Projects/`:** `BoardIndex.cshtml` (feed — component filter, per-row delete-board dropdown → `POST /projects/boards/{id}/delete`), `BoardDetail.cshtml` (board head + description via `MarkdownRenderer.RenderHtml` (the M4 `Event/Detail` idiom, not the spec's inline rich-editor on read), kanban lanes as columns — each with the lane-status + `MaxItems` badges, an inline lane-update toolbar posting to `POST /projects/boards/{id}/lanes/{laneId}` (the `LaneEditorModel` shape — Title / Status / MaxItems / Order), and per-card action dropdowns: Edit link, Unassign, Add subtask, Copy-to / Move-to (each with a `targetBoardId` select — the frozen controller binds `[FromForm] string? targetBoardId`), Move up / down / left / right (thin POSTs to the U08 reorder routes, `card.PlacementId` as the service's first arg), Delete; the board delete dropdown), `BoardNew.cshtml` (composer — title / rich description / component / language / lanes editor (title + status + max items + order per lane; a single `Lanes[0].Title` fallback lane when the list is empty so `CreateBoardRequest.Lanes` is never empty) / audience switch + `_GrantPickers`, posting to `POST /projects/boards`). **Deviations (unit-series drift):** (a) **kw-l keys registered now, all four languages** — 29 new keys (6 `projects.todo.move_*/copy_to/move_to` + 23 `projects.board.*`) added to `KnownTranslationKeys.cs` en/de/fr/da, pulled forward from U11 (same U09 pattern — `KwLRegistryConsistencyTests` scans every view for unregistered static keys and `TranslationProvider` falls back to the **raw key**, so the spec's "English fallback until U11" premise does not hold); U11 still owns the `projects-board.ts` + `site.css` kanban rules (`kanban-board` / `kanban-lane` / `kanban-lane-head` / `kanban-lane-toolbar` / `kanban-cards` / `kanban-card` / `kanban-lane-empty` / `kanban-board-empty` classes are used by the views and unstyled until then); (b) **`otherBoards` read in the view** — the frozen `BoardDetailViewModel` carries no target-board option list, so `BoardDetail` queries non-deleted boards via `IDocumentStore` (a display convenience, never a gate — the M4 / U07 placement-lookup idiom; `IDocumentStore` is injected by the frozen U02 Program.cs boot wiring); (c) **lane update is an inline toolbar** — the spec's `/lanes/{id}/edit` link has no GET route in the frozen controller (only `POST /lanes/{laneId}`), so the lane editor renders inline on the detail view; (d) **copy-to / move-to / unassign / subtask / delete reuse the U09 `projects.todo.*` action routes** — the frozen controller has no board-side equivalents of those to-do actions, and the spec's `assign` form field does not match the frozen `targetBoardId` binding. **`dotnet build Kumunita.slnx -c Debug` is green** (12.9 s, 0 errors, 0 warnings); **381/381 Web.Tests pass** (incl. `KwLRegistryConsistencyTests`); Core.Tests unchanged (381 at U09, registry-only edit in this unit). **`## U11`** — the board TS module (`projects-board.ts`, keyboard / menu, explicit POSTs, CSRF via `client/lib/api.ts`), the `site.css` kanban rules, and any residual `kw-l` keys are the next unit (the key set is now complete for the views — U11 only adds keys if the TS module introduces new labels).

