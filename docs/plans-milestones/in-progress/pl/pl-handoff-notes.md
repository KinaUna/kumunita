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

## U04

Association write seams — the two `Set*ProjectAsync` lanes (§9.3, verbatim
shapes) + the additive `string? projectId = null` feed-param on both
`ListTodosAsync` and `ListBoardsAsync`. Implementation in `ProjectService`:
each lane loads the to-do/board, checks the standing matrix (to-do:
creator ∪ assignee ∪ GlobalAdmin via `CheckTodoStanding`; board: creator ∪
GlobalAdmin via `CheckBoardStanding`), then — when `projectId` is non-null —
loads the project, refuses soft-deleted (404) / unreadable (403), writes
`ProjectId`, stamps `Modified`, and stores the audit row
(`todo.set_project` / `board.set_project`, `TargetKind` = `"todo"` /
`"board"`). `null` = unassociate (no project guard). The feed filter is
appended after the existing `unassignedOnly` / audience filters and is **a
filter, never a gate** (C-M3·2 / C-PL·3) — the audience decision stays the
access boundary. **New tests (7):** `F7_SetTodoProject_StandingRechecked_RefusesDeletedProject`,
`F7_SetBoardProject_StandingRechecked_RefusesDeletedProject` (the two §9.6
pins), `SetTodoProject_Null_Unassociates`, `SetTodoProject_ProjectDenied_Refused`,
`Todo_Feed_ProjectIdFilter_Narrows`, `Board_Feed_ProjectIdFilter_Narrows`,
`Todo_Feed_ProjectIdNull_DefaultUnchanged`. **Tests pass:**
`Total: 57, Errors: 0, Failed: 0` (the 50 from U03 + the 7 new).
**No regressions:** full `Kumunita.Core.Tests` run green — `Total: 817,
Errors: 0, Failed: 0`; `Kumunita.Web.Tests` run green — `Total: 431, Errors:
0, Failed: 0`. **Backward-compat ripple (only):** two `ProjectsController`
call-sites (`TodosIndex`, `BoardsIndex`) pass `null` for the new `projectId`
param + `ct:` named-arg; three NSubstitute stub/verification arities updated
in `ProjectsControllerTests.Todos_List_AudienceFiltered`. **No delete / Web
view / `kw-l` change; no U02/U03 seam changed.** Not committed / staged /
moved (U10's close does the move).

## U05

The **`/projects` landing surface** — `GET /projects` renders the two-section
feed (goals **then** standalone projects, `GoalId == null`), the `ProjectsIndex`
action, the view-models, `ProjectsIndex.cshtml`, the **Projects** tab as the
first entry of the tab trio, and the `pl.*` `kw-l` keys used by this page
(× 4 languages). **Deliverable shapes:**

- **Action** — `ProjectsController.ProjectsIndex(string? componentId, int
  page = 1)`, `[HttpGet("/projects")]` (placed at the **end** of the class,
  after the M5 move-to lane — no existing action reordered/renamed; no
  `lang` param, matching the existing `TodosIndex` / `BoardsIndex` shape
  — neither has one). Calls the **frozen** U02/U03 seams as-is:
  `ListGoalsAsync(componentId, actorId, page, ct:)` +
  `ListProjectsAsync(componentId, goalId: null, actorId, page, ct:)`
  (the `goalId == null` filter IS the standalone feed — the U03 pin);
  one `catch (UnauthorizedAccessException) → new ForbidResult()` (the C3
  403 split; the service raises it, the controller maps it — same as
  `TodosIndex`). No `KeyNotFoundException` (both are feed lanes). Actor
  minted via the existing `SubjectId(User)` helper; author + component
  display names resolved through the existing `ResolveDisplayNameAsync` /
  `ResolveComponentNamesAsync` helpers (read lookups, never gates);
  component picker seeded via the existing `SeedComponentPickerAsync`;
  returns `View("ProjectsIndex", vm)`. **No new `IProjectService` surface;
  no service file touched.**
- **View-models** — new file `src/Kumunita.Web/Models/ProjectViewModels.cs`
  (the M5 sibling-files convention: `ProjectTodoViewModels.cs` /
  `ProjectBoardViewModels.cs`): `GoalCard(Id, Title, DescriptionHtml,
  AuthorId, AuthorDisplayName, ComponentId, ComponentDisplayName, Created,
  Modified)` — **no** per-card project count (no count seam in the frozen
  surface; the "View projects →" link is the affordance, per the U05
  card pin); `ProjectCard(... + Status?, StartAt?, DueAt? ...)` (C-PL·4
  string-status + C-PL·5 optional dates); `ProjectsIndexViewModel(Goals,
  StandaloneProjects, ComponentPickerOptions, CurrentComponentId,
  CurrentPage)`. `DescriptionHtml` is the Markdown body pre-rendered
  through the one `MarkdownRenderer.RenderHtml` (ADR 0025 — **rendered**
  HTML, `null` for empty/whitespace so the view omits the block); the
  view renders it with `Html.Raw` inside `<div class="markdown">` (the
  `Page/Show.cshtml` / `Notifications/Index.cshtml` idiom — the card
  surface, not the WYSIWYG `rc-editor`).

## U06 — goal authoring surface (detail + composer + edit)

Shipped on top of U05 (the goal lane) and U03–U04 (the goal *service*). U06
adds the three goal surfaces the M5 lane never had, plus the `pl.goal.*` keys
and their tests. Nothing M5-route is touched (regression pins still green);
no commit/stage/move (U10's close does the move).

**The six actions** — `ProjectsController` (`/projects`), in this order after
`ProjectsIndex`:

- `GoalDetail` — `GET /projects/goals/{id:guid}`. `GetGoalForStanding` →
  `KeyNotFoundException` → `NotFound` (404) / `UnauthorizedAccessException` →
  `ForbidResult` (403) (the C3 split, mirrored from the board actions). Loads
  the goal's projects via `ListProjectsInGoal` and renders the author +
  project-author display names in one `ResolveDisplayNameAsync` batch.
  `CanEdit = Standing(User).Contains(goal.AuthorId) || User.IsInRole(GlobalAdmin)`
  is **display-only** (the service re-checks; the controller never re-derives
  access, ADR 0006).
- `GoalNew` — `GET /projects/goals/new`. Seeds component + language +
  grant-picker options; the audience defaults to public (`CommunityVisible =
  true`, `Mode = "Any"`, empty `Grants`).
- `GoalCreate` — `POST /projects/goals`. Binds the composer + a single-source
  `AudienceEditorModel`; `BuildAudience()` is the one deserialization site
  (reused verbatim from the board composer). `audience is null` → public. On
  success `RedirectToAction` to `GoalDetail`; on `UnauthorizedAccessException`
  → re-render `GoalNew` with `ViewData["formError"] = "forbidden"` (not a raw
  403, so the user sees why); on `KeyNotFoundException` → `NotFound` (404);
  on `ArgumentException` → re-render with `formError = "argument"`.
- `GoalEdit` — `GET /projects/goals/{id}/edit`. `GetGoalForStanding` (C3);
  sets `ViewData["goalId"]`; pre-fills **Title + Description only**.
- `GoalUpdate` — `POST /projects/goals/{id}`. `UpdateGoal` (full update of
  Title + Description only — audience/component/language are **not** editable,
  ADR 0070). C3 + same form-error re-render as `GoalCreate`. On success
  `RedirectToAction` to `GoalDetail`.

**The three view-models** — `Models/ProjectViewModels.cs`:

- `GoalDetailViewModel(GoalId, Title, DescriptionHtml, AuthorName,
  ComponentName?, AudiencePublic, Projects)` — `Projects` is
  `IReadOnlyList<GoalProjectCard>` (each `ProjectId, Title,
  AuthorName, Status`); `CanEdit` is a `[BindNever]` display flag (it needs a
  `using Microsoft.AspNetCore.Mvc.ModelBinding` — added to the file).
- `GoalComposerViewModel` — Title + Description + the picker lists + the
  `AudienceEditorModel Audience` (all `[BindNever]`/`[NotForBinding]` the
  server-side lists). `IsValid` = non-empty Title + `Audience.IsValid`. The
  component/language/grant lists are `[NotForBinding]` server-seeded.

**The three views** — `Views/Projects/`:

- `GoalDetail.cshtml` — header (back link → `/projects`, title, author ·
  Created/Modified via `kw-dt`, component badge, audience line
  public/restricted); Edit button (gated on `CanEdit`, → `/projects/goals/{id}/edit`);
  description card (`Html.Raw(DescriptionHtml)` in `<div class="markdown">`,
  else the `pl.goal.empty_description` placeholder); "Projects in this goal"
  section (cards → `/projects/projects/{id}`, empty state `pl.goal.projects_empty`);
  `_ProjectsTabs` partial (the `/projects` tab strip).
- `GoalNew.cshtml` — Title, Description (`rc-editor`), component picker
  (when `Components.Count > 0`), language picker, audience trio
  (`CommunityVisible` switch + `Any`/`All` radios + `_GrantPickers`), submit
  `pl.goal.create`, POST `/projects/goals`. Scripts: `rich-editor.js` +
  `_GrantPickerScripts`.
- `GoalEdit.cshtml` — mirrors `BoardEdit`: Title + Description only (ADR 0070),
  POST `/projects/goals/@(ViewData["goalId"])`, submit `pl.goal.save`, cancel
  back to the goal detail. Scripts: `rich-editor.js`.

**The `pl.goal.*` keys** (all four languages — en/de/fr/da): `new_heading`,
`new_lede`, `create`, `edit_heading`, `edit_lead`, `save`, `edit`, `title_hint`,
`description_hint`, `audience_heading`, `audience_public`, `audience_restricted`,
`projects_heading`, `projects_empty`, `empty_description`. The four-language
`KnownTranslationKeys` test (Core) pins all of them and passes. The detail
view reuses the shared `pl.index.*` / `projects.board.*` / `common.*` / `rc.*`
keys (already present), so no new keys were needed there.

**Tests** — `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs`:

- `Goal_Detail_ShowsGoalAndProjects` — the detail VM's goal + its projects.
- `Goal_Create_RedirectsToDetail` — create succeeds → `RedirectToAction(
  nameof(GoalDetail))`; `ViewBag["goalId"]` carries the new id.
- `Goal_Edit_RendersTitleAndDescription` — the edit VM pre-fills Title +
  Description from the standing-loaded goal.
- `Goal_Update_RedirectsOr403` — `UpdateGoal` returns the updated goal →
  redirect; `UpdateGoal` throws `UnauthorizedAccessException` → `ForbidResult`
  (the C3 split, asserted).

**Status** — `dotnet build Kumunita.slnx -c Debug` clean; `npm --prefix
src/Kumunita.Web run build` clean; Web suite **435/435** (431 + 4 new goal
pins); Core suite **817/817** (including the four-language kw-l pin). Not
committed / staged / moved — U10's close does the move.
- **View** — new `Views/Projects/ProjectsIndex.cshtml` (mirrors the
  `TodosIndex` / `BoardIndex` markup): header (`pl.index.title` /
  `pl.index.lede`) + the two composer buttons (`/projects/goals/new`,
  `/projects/projects/new` — **inert hrefs, U06/U07 ship the targets** —
  the deliberate register relaxation); `<partial name="_ProjectsTabs" />`;
  the component filter form (only when components exist; reuses the M5
  `projects.todo.all_communities` + `nav.community` + `common.filter`
  shared keys — the lane boundary is about *new* `pl.*` copy, not
  re-registering shared nav words); **goals section first** (`h2` +
  empty-state + `airy-ann-row` cards: title → `/projects/goals/{id}`,
  author · `kw-dt` created · component badge, `Html.Raw` description,
  "View projects →" link); **standalone-projects section second** (cards:
  title → `/projects/projects/{id}`, author · created · optional Start /
  Due via `kw-dt` (gated on non-null, the ADR 0079 shape) · verbatim
  status badge · component badge, `Html.Raw` description). `pl.*` keys
  only for this page's own copy.
- **Tab trio** — `_ProjectsTabs.cshtml`: the **Projects** tab is now the
  **first** entry — `("pl.tabs.projects", "Projects", "/projects")` —
  ahead of the untouched `("projects.todo.title", "To-dos",
  "/projects/todos")` + `("projects.board.title", "Boards",
  "/projects/boards")` (exact labels + routes + M5 keys kept — the
  frozen M5-route pin, C-PL·7). Active derivation extended:
  `"ProjectsIndex" => "Projects"`, `"BoardsIndex" => "Boards"`, `_ =>
  "To-dos"` — the `Goal*` / `Project*` actions (U06/U07) fall through to
  the default until they ship; note the comment was updated to name all
  three index actions.
- **`kw-l` keys registered (12, this unit only)** — `pl.tabs.projects`,
  `pl.index.title`, `pl.index.lede`, `pl.index.goals_heading`,
  `pl.index.projects_heading`, `pl.index.new_goal`,
  `pl.index.new_project`, `pl.index.view_projects`,
  `pl.index.goals_empty`, `pl.index.projects_empty`, `pl.index.start`,
  `pl.index.due` — each appended at the end of **all four**
  `KnownTranslationKeys` dictionaries (en/de/fr/da, the ADR 0015
  four-language parity; the `KwLRegistryConsistencyTests` pin passes —
  see the Core run below). The `pl.goal.*` / `pl.project.*` /
  `pl.todo.project_link` / `pl.board.project_link` keys are registered by
  **their** units (U06–U09).

**M5-route-untouched regression:** the existing M5 `ProjectsControllerTests`
pins (incl. `Todos_List_AudienceFiltered`) pass unmodified — no M5 action /
view / route / key changed. **Tests pass:** `Kumunita.Web.Tests` —
`Total: 431, Errors: 0, Failed: 0`; `Kumunita.Core.Tests` — `Total: 817,
Errors: 0, Failed: 0` (the four-language `kw-l` pin green). **Builds clean:**
`dotnet build Kumunita.slnx -c Debug` succeeds with only the pre-existing
`BoardDetail.cshtml` CS8600 warning (the allowed one); `npm --prefix
src/Kumunita.Web run build` (tsc) clean — no new TS this unit. **Guardrails
held:** no service changes, no detail views / pickers / delete (U06–U09),
the goal / project detail + `new` hrefs ship inert. Not committed / staged /
moved (U10's close does the move).
