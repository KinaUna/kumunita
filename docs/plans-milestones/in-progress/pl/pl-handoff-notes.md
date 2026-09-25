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

## U02 — the four goal seams + two goal DTOs + their implementations

**Three additive edits (all under `src/Kumunita.Core/Projects/`) + one test
addition** — the design doc §9.3 (seams) + §9.4 (DTOs) landed **verbatim**,
and the four `ProjectService` implementations mirror the frozen M5 board
lanes. **`IProjectService.cs`:** the four goal seams (`ListGoalsAsync` /
`GetGoalAsync` / `CreateGoalAsync` / `UpdateGoalAsync`) in a new
`// --- PL goal lanes (U02) ---` section, XML-doc prose verbatim from §9.3
(the `CanSeeAsync(Read)` feed gate, the C3 404-vs-403 split, the standing
re-check, the `AccessAudit` row shape). **`ProjectRequests.cs`:** the two
sealed records `CreateGoalRequest` (the §9.4 field set — `required string
Title`, `Description?` / `ComponentId?` / `Audience?` / `LanguageCode?`
creation-time choices) + `UpdateGoalRequest` (`required string Title` +
`Description?` only — `Audience` / `ComponentId` / `LanguageCode`
**not** editable, the ADR 0070 precedent). **`ProjectService.cs`:** the four
implementations — `ListGoalsAsync` mirrors `ListBoardsAsync` (`!IsDeleted`
+ optional `ComponentId` filter + `CanSeeAsync(Read)` survivor pass over
`ProjectGoalToAuditableResource` + `Created`-descending + paging);
`GetGoalAsync` mirrors `GetBoardAsync` (the `CanAsync(Read)` entry gate, the
404-on-absent / soft-deleted + 403-on-denied split); `CreateGoalAsync`
mirrors `CreateBoardAsync` (the author's choice verbatim, `AuthorId =
actorId`, the ADR 0018 language floor, one `goal.create` audit row,
`Via Owner`); `UpdateGoalAsync` mirrors `UpdateBoardAsync` (a **new**
public pure helper `CheckGoalStanding` — the **creator ∪ GlobalAdmin**
matrix, the assignee branch does **not** apply, C-PL·2; a full update of
`Title` + `Description`, a blank `Description` → `null`, `AuthorId` /
`Created` preserved, `Modified` stamped on a real change; one `goal.update`
audit row via a **new** `GoalAuditViaFor` — creator `Owner` / else `Admin`).
A **new** `private const string TargetKindGoal = "goal"` is added next to
`TargetKindTodo` / `TargetKindBoard`. **`ProjectServiceTests.cs`:** the
four §9.6 goal pins **as named** — `F1_GoalVisibleToAudienceMember_HiddenFromNonMember`
(the feed's both sides), `F2_GoalDetail_404OnAbsent_403OnDenied` (the C3
split), `F3_CreateGoal_AuthorIsStandingOwner_Audited` (the `goal.create`
row, the ADR 0018 `en` floor, the `Via Owner` tag), `F3_UpdateGoal_CreatorGlobalAdmin_StandingRechecked`
(the standing matrix — creator + GlobalAdmin succeed, the stranger is
refused 403 with nothing written, the blank `Description` clears to
`null`, `AuthorId` preserved, the `Modified` stamp, two `goal.update`
audit rows) — plus a new `GoalAuditRows` query helper (mirrors the existing
`BoardAuditRows` / `TodoAuditRows`).
**Build clean:** `dotnet build Kumunita.slnx -c Debug` — `Build succeeded`,
zero errors, zero **new** warnings (the one remaining `BoardDetail.cshtml`
CS8600 is pre-existing, the file is untouched by this unit). **Tests
pass:** `-class "Kumunita.Core.Tests.ProjectServiceTests"` — `Total: 46,
Errors: 0, Failed: 0` (the 41 pre-existing + the 5 new pins — the four
§9.6 names; the class is a `ClassFixture` so all tests run in one fresh
scratch Postgres). **No regressions:** full `Kumunita.Core.Tests` run
green — `Total: 806, Errors: 0, Failed: 0` (in-process xunit.v3,
Testcontainers). **No project / association / delete / Web / view / `kw-l`
change** — `git status` confirms exactly the four U02 files, all additions
(510 insertions, 0 deletions — the frozen M5 surface is untouched).
**Not committed / staged / moved** (U10's close does the move).

## U03 — the five project seams + two project DTOs + their implementations

**Four additive edits (all under `src/Kumunita.Core/Projects/`) + one test
addition** — the design doc §9.3 (seams) + §9.4 (DTOs) landed **verbatim**,
and the project lanes mirror the U02 goal lanes (which mirror the frozen M5
board lanes). **`IProjectService.cs`:** the four §9.3 project seams
(`ListProjectsAsync` / `GetProjectAsync` / `CreateProjectAsync` /
`UpdateProjectAsync`) in a new `// --- PL project lanes (U03) ---` section
**after** the U02 goal section (the goals → projects → association → delete
order is the lane plan's invariant), XML-doc prose verbatim from §9.3,
**plus** the U03-added `ListProjectsForGoalAsync(goalId, actorId, ct)`
per-parent seam (the goal detail view's "projects in this goal" seam — the
M5 `ListBoardsForTodoAsync` per-parent precedent: the goal itself is loaded
first + `CanAsync(Read)`-gated (the C3 split), then the goal's `!IsDeleted`
projects `GoalId == goalId` are `CanSeeAsync(Read)`-filtered,
`Created`-descending, **unpaged**). **`ProjectRequests.cs`:** the two
sealed records `CreateProjectRequest` (the §9.4 field set — `required
string Title`, `Description?` / `GoalId?` / `Status?` / `StartAt?` /
`DueAt?` / `ComponentId?` / `Audience?` / `LanguageCode?` creation-time
choices) + `UpdateProjectRequest` (the **partial** update — `Title?` /
`Description?` / `GoalId?` / `ClearGoal` / `Status?` / `StartAt?` /
`DueAt?`; `Audience` / `ComponentId` / `LanguageCode` **not** editable —
the ADR 0070 precedent). **`ProjectService.cs`:** the five implementations —
`ListProjectsAsync` mirrors `ListGoalsAsync` on the `Project` surface
(`!IsDeleted` + the `ComponentId` filter + the **`goalId` association
filter** — `goalId == null` selects the **standalone-projects** feed
(`GoalId == null` rows, the `/projects` landing's projects section — the
D8 pin), a value selects that goal's projects (`GoalId == goalId` rows); a
*filter, never a gate* — C-PL·3 — then the `CanSeeAsync(Read)` survivor
pass over `ProjectToAuditableResource`, `Created`-descending, paged);
`GetProjectAsync` mirrors `GetGoalAsync` (the `CanAsync(Read)` entry gate,
the 404-on-absent / soft-deleted + 403-on-denied split);
`CreateProjectAsync` mirrors `CreateGoalAsync` (the author's choice
verbatim, `AuthorId = actorId`, the ADR 0018 language floor, one
`project.create` audit row, `Via Owner`) — **with the `GoalId` guard**:
when `request.GoalId != null`, the goal is loaded (404 on absent /
soft-deleted) + `CanAsync(Read)` (403 on denied) **before** the project
write; `ListProjectsForGoalAsync` is the goal-guarded per-parent list
above; `UpdateProjectAsync` mirrors `UpdateGoalAsync` (a **new** public
pure helper `CheckProjectStanding` — the **creator ∪ GlobalAdmin** matrix,
the assignee branch does **not** apply, C-PL·2 — re-checked server-side
**first**; the **partial** update of `Title` / `Description` (a blank
`Description` → `null`, the ADR 0070 shape) / `GoalId` (a non-null value
re-associates **with the `GoalId` guard re-applied**; `ClearGoal = true` is
the explicit un-goal — sets `GoalId = null`, no guard) / `Status` (a
string, not an enum — C-PL·4; `null` clears) / `StartAt` / `DueAt` (ADR
0079 — non-null applied, `null` clears — C-PL·5); `AuthorId` / `Created`
preserved, `Modified` stamped on a real change; one `project.update` audit
row via a **new** `ProjectAuditViaFor` — creator `Owner` / else `Admin`).
A **new** `private const string TargetKindProject = "project"` is added
next to `TargetKindGoal`. **`ProjectServiceTests.cs`:** the four §9.6
project pins **as named** — `F5_ProjectFeed_GoalIdFilter_StandaloneVsUnderGoal`
(the feed's both sides — the standalone feed excludes under-goal projects
and vice versa), `F2_ProjectDetail_404OnAbsent_403OnDenied` (the C3
split), `F5_CreateProject_GoalIdGuard_RefusesDeletedOrUnreadableGoal`
(the `GoalId` guard — deleted goal → 404, unreadable goal → 403, both with
**nothing written**; a readable goal carries the association),
`F4_UpdateProject_ClearGoal_NullsGoalId` (the `ClearGoal` explicit un-goal
+ the ADR 0079 non-null-applied / null-clears date semantics, `AuthorId`
preserved, the `Modified` stamp, two `project.update` audit rows) — plus a
new `ProjectAuditRows` query helper (mirrors `GoalAuditRows` /
`BoardAuditRows`). **Build clean:** `dotnet build Kumunita.slnx -c Debug`
— `Build succeeded`, zero errors, zero **new** warnings (the one remaining
`BoardDetail.cshtml` CS8600 is pre-existing, the file is untouched by this
unit). **Tests pass:** `-class "Kumunita.Core.Tests.ProjectServiceTests"`
— `Total: 50, Errors: 0, Failed: 0` (the 46 from U02 + the 4 new §9.6
project pins; the class is a `ClassFixture` so all tests run in one fresh
scratch Postgres). **No regressions:** full `Kumunita.Core.Tests` run
green — `Total: 810, Errors: 0, Failed: 0` (in-process xunit.v3,
Testcontainers). **No association / delete / Web / view / `kw-l` change** —
`git status` confirms exactly the four U03 files, all additions (759
insertions, 0 deletions — the frozen M5 + U02 surface is untouched).
**Not committed / staged / moved** (U10's close does the move).
