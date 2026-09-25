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
