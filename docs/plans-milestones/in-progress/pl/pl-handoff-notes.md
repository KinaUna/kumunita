# `PL` — Goals & Projects — rolling handoff notes

One `## U#` section per unit, appended (never rewritten). Each unit writes
exactly one short section before it exits; the next unit reads only that
section + its own entry-read list. Moves with the lane folder at close (U10).

## U00 — design doc (Part 1 + Part 2) + ADR 0086 + roadmap confirm

`docs/design/pl-goals-projects-design.md` authored in one pass (both parts —
Part 1: 8 sections — what it is / the reused surface (verified) / the locked
decisions / invariants / FACES / parts affected / feedback loops / risks;
Part 2: `## Seams & contracts`, §9.1–§9.8 — the exact C# of the two new docs
+ the two adapters, the additive `IProjectService` seams, the four request
DTOs, the `M5DocTypes` additive shape, the ~20 pinned test names, the
three-test gate, the drift-guard); **ADR 0086 authored and Accepted**
(`docs/adr/0086-goals-projects-lane.md`, 2026-09-25, Amends
0067 / 0070 / 0079 / 0024 / 0006 / 0001-B / 0036 / 0025 / 0031 / 0018 /
0019-0020); the **ADR 0086** index row added to `docs/adr/README.md` (0085
confirmed the current highest); **roadmap confirmed** — `M7`
("Pagination and filtering") is the single `StatusNext` on
`Milestones.cs` (verified 2026-09-25); `PL` is a **named lane, not a
milestone** — `Milestones.cs` / `MilestonesTests.cs` untouched; the README
Roadmap gains its `PL` entry at close (U10). **Decisions LOCKED:** D1–D12
(the lane plan's `[PROPOSED]` markers retired). **Invariants (8):**
C-PL·1, C-PL·2, C-PL·3, C-PL·4, C-PL·5, C-PL·6, C-PL·7, C-PL·8. **FACES
(10):** F1–F10. **Additive `IProjectService` seams (frozen verbatim for
U02–U04 / U09):** goal read — `ListGoalsAsync`, `GetGoalAsync`; goal write —
`CreateGoalAsync`, `UpdateGoalAsync`; project read — `ListProjectsAsync`,
`GetProjectAsync`; project write — `CreateProjectAsync`,
`UpdateProjectAsync`; association — `SetTodoProjectAsync`,
`SetBoardProjectAsync` + the additive `projectId` optional filter param on
`ListTodosAsync` / `ListBoardsAsync`; delete — `DeleteGoalAsync`,
`DeleteProjectAsync`. **Pinned test names (§9.6, ~20):** 11 Core
(`ProjectServiceTests.cs` appends) + 10 Web
(`ProjectsControllerTests.cs` appends). **No code, no build, no tests**
(U00 exit criteria). U01 (the docs + `M5DocTypes` + `ProjectId` fields)
reads only this section + its own entry-read list.

## U01 — the four new Core types + the `M5DocTypes` extension + the `ProjectId` fields

**Four new files** (all **verbatim** from the design doc §9.1 / §9.2, ADR
0086 D2 / D3, the `TodoItem` / `KanbanBoard` provenance-table XML-doc style,
ADR 0086 references in place): `src/Kumunita.Core/Projects/ProjectGoal.cs`
(the D2 shape — **no** `IsDraft` / **no** dates / **no** `ProjectId`),
`src/Kumunita.Core/Projects/Project.cs` (the D3 shape — `GoalId?` sole
association, `Status?` **string-not-enum** C-PL·4, `StartAt?` / `DueAt?`
optional-date C-PL·5 / ADR 0079, **no** `IsDraft`),
`src/Kumunita.Core/Projects/ProjectGoalToAuditableResource.cs` (the frozen
6-member `IAuditableResource` surface — confirmed against
`src/Kumunita.Core/Authorization/AccessAction.cs` — `TargetKind =>
"goal"` **exact**), `src/Kumunita.Core/Projects/ProjectToAuditableResource.cs`
(`TargetKind => "project"` **exact**). **Two additive edits:**
`src/Kumunita.Core/Projects/TodoItem.cs` + `KanbanBoard.cs` each gain
exactly one `string? ProjectId { get; set; }` field (the C-M3·2 feed-filter
pin, the `ComponentId` line style, ADR 0086 D4); **nothing else moves.**
**`M5DocTypes.cs` extension** (the design doc §9.5 shape): two new
registrations after the `BoardItemPlacement` block —
`opts.Schema.For<ProjectGoal>().Index(g => new { g.ComponentId,
g.Created })` +
`opts.Schema.For<Project>().Index(p => new { p.ComponentId, p.Created })
.Index(p => p.GoalId)`; the existing `TodoItem` / `KanbanBoard` blocks each
gain `.Index(t => t.ProjectId)` / `.Index(b => b.ProjectId)` (unnamed — the
Marten 9.31.2 / Weasel 9.29.0 no-`Name`-on-`ComputedIndex` constraint
already noted in the file); the `Configure` XML doc gains the PL additive
note (ADR 0086 / §9.5 / C-PL·3 / C-PL·8 named, the zero-migration pin).
**No** `IProjectService` / `ProjectService` / Web / view / `kw-l` change
(`git status` confirms: 4 new + 3 edited files, all under
`src/Kumunita.Core/` + the one new test). **Test** (new):
`tests/Kumunita.Core.Tests/ProjectDocShapeTests.cs` — one test,
`ProjectGoal_Project_ResolveAndRegister` (boots a fresh scratch Postgres
through the `M5DocTypes` surface, round-trips one of each of the four docs
exercising the two new `ProjectId` columns, asserts the two adapters'
6-member projection + the **exact** `"goal"` / `"project"` `TargetKind`
strings, and the two additive fields exist + are `string?`); **pass**
(`Total: 1, Errors: 0, Failed: 0`, in-process xunit.v3). **Build clean:**
`dotnet build Kumunita.slnx -c Debug` — `Build succeeded`, zero errors,
zero **new** warnings (the one remaining `BoardDetail.cshtml` CS8600 is
pre-existing, the file is untouched by this unit). **No regressions:**
full `Kumunita.Core.Tests` run green — `Total: 802, Errors: 0, Failed: 0`
(in-process xunit.v3, Testcontainers). **Not committed / staged / moved**
(U10's close does the move).
