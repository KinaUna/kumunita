# `PL` — Goals & Projects — the higher-level Projects page (sealed unit register)

> **In progress.** This is the **lane plan** (the secondary register tier) for the
> **`PL`** lane (short id **`PL`** = *Projects Lane*: the higher-level **goals**
> and **projects** page on top of the M5 to-do / board surface). The
> **primary reference tier** (the exact C# seams + the locked decisions) is the
> design doc `docs/design/pl-goals-projects-design.md` (authored **U00**); the
> **scratch tier** is `docs/plans-milestones/in-progress/pl/pl-handoff-notes.md`
> (one appended `## U#` section per unit, never rewritten).
>
> **What this is:** two **new documents** in the **existing**
> `Kumunita.Core.Projects` context (a `ProjectGoal` — title + description, the
> organizing container — and a `Project` — title + description + status +
> start/due dates + optional goal), **additive on the `M5DocTypes` surface**
> (zero new surface, zero migrations for existing docs), **additive
> `IProjectService` seams** (read / write for both docs + the
> project-association write lanes for to-dos and boards), **one new
> `ProjectId?` feed-filter field on `TodoItem` + `KanbanBoard`** (a *filter,
> never a gate* — C-M3·2), and **the Web**: the `/projects` landing page
> (goals + projects feed), the goal / project detail + composer + edit views,
> the **third nav tab** ("Projects") in `_ProjectsTabs.cshtml`, and the
> project-picker on the to-do / board edit forms + the project link on their
> detail views.
>
> **The one thing every unit must respect:** this lane is **additive and
> reusing.** It reuses the `Audience` doc (ADR 0001-B / 0036 — *reused*, not
> extended) on **both** `ProjectGoal` and `Project`, the frozen
> `IAuthorizationService.CanAsync(Read)` / `CanSeeAsync(Read)` decision path
> (ADR 0006) through **two new adapters**
> (`ProjectGoalToAuditableResource`, `ProjectToAuditableResource`), the
> standing matrix **creator ∪ GlobalAdmin** (the ADR 0070 board-edit precedent
> — there is *no* assignee collaborator on a goal or project), the
> `MarkdownRenderer` + `bindRichEditor` (ADR 0025 / 0031) for the optional
> description, the **`kw-dt`** TagHelper (ADR 0019 / 0020) for the dates, and
> the existing `AudienceEditorModel` / component-picker / language-picker
> composer trio (the M2/M3/M4/M5 pattern). **No new `AccessAction`**,
> **no new `AccessVia`**, **no new authorization branch** — `PL` adds two
> *adapters*, not a *branch* (the C-M5·11 precedent). **M7 stays
> `StatusNext` on the roadmap** — `PL` is a **named lane**, not a milestone
> (the ADR 0013 / 0015 / … precedent; the README gets one roadmap entry, no
> `Milestones.cs` / `MilestonesTests.cs` move).
>
> **Sizing:** units are sized for a **~32K-context fresh agent** one at a
> time, each with its own closed exit criteria, in the `M5` / `EV-DWM` / `GP`
> style. **U00 is the sign-off gate** — it authors the design doc and locks
> the **[PROPOSED]** decisions into **ADR 0086**; every later unit codes
> against the *locked* text. **Sequencing invariant:** the service read lanes
> (U02 goals, U03 projects) land before any Web view; the project-association
> lanes on the existing to-do / board surface (U04) land **before** any Web
> project-picker form (U08) posts against them; the delete lanes (U09) land
> **after** the detail views (U06 / U07) they extend; the close (U10) is last
> so the doc index + README + ARCHITECTURE.md are honest at ship time.

## Understanding (one paragraph)

M5 shipped **to-dos and Kanban boards** — the *work items* and the *boards
they sit on*. What it deliberately left out (the design doc D2's "a
per-project 'goals' rollup … M5 ships to-dos + boards, not a higher-level
goal doc") is the **higher level**: the *goals* a neighborhood wants to reach
and the *projects* it runs to get there. `PL` adds exactly that level, as two
new documents in the same `Projects` context: a **goal** (title +
description) is the organizing container a project can hang off; a **project**
(title + description + status + start date + due date) is the unit of
*managed* work, and it is what to-dos and boards associate **to** — the
existing work-item surface (M5) stays exactly as it is, and each to-do /
board gains one optional `ProjectId` link. The `/projects` landing page
becomes the *goals + projects* feed (the to-do / board feeds keep their
`/projects/todos` / `/projects/boards` routes, now one tab deeper), and the
nav tabs grow a **Projects** entry. Read is each doc's own `Audience`
decision through the two new adapters (the frozen `IAuthorizationService`
path); standing is **creator ∪ GlobalAdmin**; a goal's / project's
soft-delete (the ADR 0024 shape) **never deletes** the to-dos / boards
associated with it — the association field simply dangles and is not
rendered.

## Assumptions / decisions (each [PROPOSED], locked by ADR 0086 in U00)

- **D1 — two new docs, one existing context.** `ProjectGoal` + `Project`
  (ns `Kumunita.Core.Projects`, the M5 context — a *new* context would be
  overkill for two docs that share the same standing / audience / renderer
  surface). Additive on **`M5DocTypes`** (ADR 0004 §B.1 — the surface already
  delta-detects; zero new surface file, zero migration for existing docs).
- **D2 — `ProjectGoal` shape.** `Id`; `Title` (non-empty); `Description?`
  (Markdown — the ADR 0025 shape, optional); `ComponentId?` (a feed filter,
  never a gate — C-M3·2); `AuthorId` (the standing owner — C-M5·6);
  `Audience?` (the **exact** post `Audience` — ADR 0001-B / 0036,
  `null` = public); `IsDeleted` (the ADR 0024 soft-delete flag, `false`
  default); `LanguageCode` (ADR 0018 authored-in tag); `Created`;
  `Modified?`. **No** `IsDraft`, **no** dates (a goal is a direction, not a
  scheduled thing), **no** `ProjectId` (the association is *outward* — a
  project points at its goal, not the reverse).
- **D3 — `Project` shape.** `Id`; `Title` (non-empty); `Description?`
  (Markdown); `GoalId?` (the optional goal — `null` = standalone project;
  the **sole** association mechanism, the `TodoItem.ParentId` shape in
  intent); `Status?` (a **string** — the state label; `null` = none;
  **not** an enum — the C-M5·4 string-status pin carried over); `StartAt?` /
  `DueAt?` (both **nullable** `DateTimeOffset` — the ADR 0079 optional-date
  shape); `ComponentId?` (feed filter, never a gate); `AuthorId` (standing
  owner); `Audience?` (the exact post `Audience`); `IsDeleted` (ADR 0024
  flag); `LanguageCode` (ADR 0018); `Created`; `Modified?`. **No**
  `IsDraft`.
- **D4 — to-dos and boards gain one optional `ProjectId?` each.** A
  `ProjectId?` on **`TodoItem`** and **`KanbanBoard`** (a feed filter,
  never a gate — C-M3·2; the `ComponentId` shape, **not** a gate). The
  `ProjectId` is **additive** (ADR 0004 §B.1 — Marten delta-detects the new
  column; **zero migration for existing rows** — existing to-dos / boards
  simply have `ProjectId == null`). It is a **display + filter + standing
  context**, never an access boundary (a to-do's / board's `Read` decision
  stays its **own** `Audience` — C-M5·3; `ProjectId` changes nothing about
  who may *see* the to-do / board).
- **D5 — standing.** **Creator ∪ GlobalAdmin** over a goal / project (the
  ADR 0070 board-edit precedent; **no** assignee branch — a goal / project
  is not assignable the way a to-do is). The standing is **re-checked
  server-side** in every write lane (C-M5·6); the Web `[Authorize]` is a
  convenience pre-gate only.
- **D6 — delete cascade semantics (soft, non-destructive).** Deleting a
  **goal** sets `ProjectGoal.IsDeleted = true`; the `Project.GoalId` rows
  are **kept** (the association dangles; the project detail view's goal
  link is not rendered when the goal is soft-deleted — the read lane's
  filter, the ADR 0024 shape). Deleting a **project** sets
  `Project.IsDeleted = true`; the `TodoItem.ProjectId` /
  `KanbanBoard.ProjectId` rows are **kept** (the same dangling-association
  rule; the to-do / board detail view's project link is not rendered when
  the project is soft-deleted). **No hard delete anywhere.**
- **D7 — the association write lane.** One **`SetTodoProjectAsync(todoItemId,
  actorId, actorRoles, projectId?, ct)`** + one **
  `SetBoardProjectAsync(boardId, actorId, actorRoles, projectId?, ct)`** —
  each re-checks standing **over its own to-do / board** server-side —
  the to-do lane uses the to-do's own standing matrix
  (creator ∪ assignee ∪ GlobalAdmin, the C-M5·6 shape, matching
  `AssignTodoAsync`), the board lane uses the board's own
  (creator ∪ GlobalAdmin, the ADR 0070 shape, matching
  `UpdateBoardAsync`) — writes `ProjectId` (or clears it when `null`),
  stamps `Modified`, and stores one `AccessAudit` row
  (`TargetKind = "todo"` / `"board"`, the C-M3·3 aggregate shape). A
  non-null `projectId` pointing at a soft-deleted or unreadable project
  is **refused** (the C3 404-vs-403 split). **No new
  `AccessAction`**, **no new `AccessVia`** — the lanes reuse the frozen
  `Read` action and the existing per-resource standing matrix (the
  C-M5·11 precedent).
- **D8 — the `/projects` landing page.** `GET /projects` (new route on
  `ProjectsController`) renders the **goals + projects feed**: the goals
  (pinned list, each with its associated projects inline or linked) + the
  **standalone projects** (the `GoalId == null` projects) in one view. The
  `/projects/todos` + `/projects/boards` routes **keep their exact
  paths** (the M5 route surface is untouched — the nav tab is the only
  visible change). The `_ProjectsTabs.cshtml` partial grows a **Projects**
  tab (the first entry — the page the lane is named for) pointing at
  `/projects`, with the active-tab derivation extended to recognize the new
  action name.
- **D9 — the project link on the to-do / board detail views.** A to-do /
  board with a non-null `ProjectId` (and a non-deleted target project the
  actor may read) renders a **"Project: {title}"** link (the `kw-l` key
  `pl.todo.project_link` / `pl.board.project_link`) on its detail view,
  linking to `/projects/projects/{id}`. The to-do / board **edit** form
  gains a **project picker** (a `<select>` of the actor's readable,
  non-deleted projects, the `SeedComponentPickerAsync` shape, seeded in the
  controller — the picker is a *display* surface, never a gate).
- **D10 — the goal link on the project detail view.** A project with a
  non-null `GoalId` (and a non-deleted target goal the actor may read)
  renders a **"Goal: {title}"** link (the `kw-l` key
  `pl.project.goal_link`) linking to `/projects/goals/{id}`. The project
  **composer / edit** form gains a **goal picker** (a `<select>` of the
  actor's readable, non-deleted goals, the same picker shape as D9).
- **D11 — `kw-l` keys.** A `pl.*` key namespace (the `projects.*` shape the
  M5 lane used): `pl.goal.*` (title / description / new / edit / delete /
  projects / empty), `pl.project.*` (title / description / status /
  start_date / due_date / new / edit / delete / goal / goal_link /
  associated / empty), `pl.todo.project_link`, `pl.board.project_link`,
  `pl.projects_tab` (the nav-tab label). **× 4 languages** (the
  `KnownTranslationKeys.cs` `en` / `de` / `fr` / `da` floors — the M5 lane's
  18-key × 4-language precedent; the exact count is U00's to pin).
- **D12 — the ADR number.** **ADR 0086** (the next free number after ADR
  0085 — `docs/adr/README.md` confirms 0085 is the current highest).
- **Out of scope (→ follow-on lanes, own ADRs):** a per-project *progress
  rollup* (the to-do / board count is the display nicety — a follow-on
  lane), a **goal** rollup to a project's due date (a display nicety — a
  follow-on lane), drag-and-drop association (the explicit form POST is the
  M5 pin — ADR 0031's plain-GET/POST posture), a per-goal / per-project
  *member* surface (the standing matrix is creator ∪ GlobalAdmin; a
  collaborator lane is a follow-on ADR), and a **board-per-project**
  grouping view (the project detail page's associated-to-dos / boards list
  is the unit; a dedicated board-view is a follow-on lane).

## Unit register

| Unit | Title | Deliverable shape |
|------|-------|-------------------|
| **U00** | Design doc + **ADR 0086** | `docs/design/pl-goals-projects-design.md` (the D1–D12 above, locked, the exact C# seam shapes, the pinned test names, the three-test gate, the drift-guard) + `docs/adr/0086-goals-projects-lane.md` (Accepted) + `docs/adr/README.md` index row. **No code, no build.** |
| **U01** | `ProjectGoal` + `Project` docs + `M5DocTypes` surface + `ProjectId` on `TodoItem` / `KanbanBoard` | `src/Kumunita.Core/Projects/ProjectGoal.cs`, `Project.cs`, `ProjectGoalToAuditableResource.cs`, `ProjectToAuditableResource.cs`; `M5DocTypes.Configure` extended (the two new docs + the `TodoItem` / `KanbanBoard` `ProjectId` indexes); `dotnet build` clean; a `PL` unit test (the doc shapes compile + register). |
| **U02** | `IProjectService` goal read + write lanes | `IProjectService` (additive: `ListGoalsAsync` / `GetGoalAsync` / `CreateGoalAsync` / `UpdateGoalAsync` — the full-interface-first pattern, the M4 `IEventService` precedent); `ProjectService` implements all four (the frozen `IAuthorizationService` path, the two adapters, the standing re-check, the `AccessAudit` rows); `ProjectServiceTests` pinned tests (the design doc §pin list). |
| **U03** | `IProjectService` project read + write lanes | `IProjectService` (additive: `ListProjectsAsync` / `GetProjectAsync` / `CreateProjectAsync` / `UpdateProjectAsync` **+ `ListProjectsForGoalAsync`** — the goal detail's "projects in this goal" seam, the M5 `ListBoardsForTodoAsync` per-parent precedent); `ProjectService` implements (the `GoalId` guard — a `GoalId` pointing at a soft-deleted or unreadable goal is **refused** on create, the `KeyNotFoundException` 404 shape; the `Status` string + `StartAt` / `DueAt` optional-date shape, the ADR 0079 precedent); `ProjectServiceTests` pinned tests. |
| **U04** | `IProjectService` association lanes (`SetTodoProjectAsync` / `SetBoardProjectAsync`) + `ProjectId` on the existing read lanes | `IProjectService` (additive: `SetTodoProjectAsync` / `SetBoardProjectAsync` + the **`projectId?`** optional param on the existing `ListTodosAsync` / `ListBoardsAsync` feed seams — a feed filter, never a gate, C-M3·2); `ProjectService` implements (the standing re-check, the `AccessAudit` rows, the `ProjectId` filter); `ProjectServiceTests` pinned tests. |
| **U05** | Web: the `/projects` landing page (goals + projects feed) + the **Projects** nav tab | `ProjectsController` (additive: `GET /projects` — the `ProjectsIndex` action, the `IProjectService.ListGoalsAsync` / `ListProjectsAsync` read, the `KumunitaPrincipal` actor shape, the component-picker seed); `Models/ProjectViewModels.cs` (the `GoalCard` / `ProjectCard` / `ProjectsIndexViewModel` shapes — the card descriptions pre-rendered via `MarkdownRenderer`, **not** the WYSIWYG `rc-editor`); `Views/Projects/ProjectsIndex.cshtml` (the goals section + the standalone-projects section, the `kw-l` `pl.*` keys, the `kw-dt` dates, the "New goal" / "New project" buttons); `Views/Projects/_ProjectsTabs.cshtml` (the **Projects** tab — the first entry, the active-tab derivation extended); `dotnet build` + `npm run build` clean; the `ProjectsControllerTests` pinned tests (the feed renders + the 404 / 403 split on an unread goal / project). |
| **U06** | Web: the goal detail + composer + edit views | `ProjectsController` (additive: `GET /projects/goals/{id}`, `GET /projects/goals/new`, `POST /projects/goals`, `GET /projects/goals/{id}/edit`, `POST /projects/goals/{id}`); `Views/Projects/GoalDetail.cshtml` / `GoalNew.cshtml` / `GoalEdit.cshtml` (the `AudienceEditorModel` trio, the language / component pickers, the `kw-l` `pl.goal.*` keys, the **projects-in-this-goal** list on the detail view, the edit standing = creator ∪ GlobalAdmin); `ProjectService` (the `UpdateGoalAsync` **full-update** shape — the ADR 0070 precedent, blank `Description` → `null`); the `ProjectsControllerTests` pinned tests. |
| **U07** | Web: the project detail + composer + edit views | `ProjectsController` (additive: `GET /projects/projects/{id}`, `GET /projects/projects/new`, `POST /projects/projects`, `GET /projects/projects/{id}/edit`, `POST /projects/projects/{id}`); `Views/Projects/ProjectDetail.cshtml` / `ProjectNew.cshtml` / `ProjectEdit.cshtml` (the **goal picker** — D10, the `Status` string field, the `StartAt` / `DueAt` `datetime-local` fields, the **associated to-dos / boards** list on the detail view — the `IProjectService.ListTodosAsync` / `ListBoardsAsync` with the `projectId` filter, the `kw-l` `pl.project.*` keys, the edit standing = creator ∪ GlobalAdmin); the `ProjectsControllerTests` pinned tests. |
| **U08** | Web: the project-picker on the to-do / board edit forms + the project link on their detail views | `ProjectsController` (the to-do / board edit actions seed the **project picker** — the `SeedComponentPickerAsync` shape, the actor's readable non-deleted projects; the to-do / board detail actions resolve the associated project's display name, if any); `Views/Projects/TodoDetail.cshtml` / `BoardDetail.cshtml` (the **project link** — D9, the `kw-l` `pl.todo.project_link` / `pl.board.project_link` keys, the `kw-l` `pl.projects_tab` key on the nav-tab update); `Views/Projects/Edit.cshtml` (the to-do **edit** form — the **project picker** `<select>`, the `name="ProjectId"` field) / `Create.cshtml` (the to-do **new** form — the same picker, mirroring its existing `ParentId` select) + `BoardEdit.cshtml` / `BoardNew.cshtml` (the board-side equivalents); `ProjectService` (the `SetTodoProjectAsync` / `SetBoardProjectAsync` write, the standing re-check, the `AccessAudit` rows); the `ProjectsControllerTests` pinned tests (the picker seeds + the 404 / 403 split on an unread project, the `ProjectId` filter on the feed). |
| **U09** | The delete lanes (soft, non-destructive) + the delete UI | `ProjectService` (additive: `DeleteGoalAsync` / `DeleteProjectAsync` — the standing re-check, the `IsDeleted = true` flag, the **dangling-association** rule — D6, the `AccessAudit` rows (`TargetKind = "goal"` / `"project"`)); `ProjectsController` (additive: `POST /projects/goals/{id}/delete`, `POST /projects/projects/{id}/delete`); `Views/Projects/GoalDetail.cshtml` / `ProjectDetail.cshtml` (the **delete button** — the `confirm()` guard, the ADR 0058 precedent, the `kw-l` `pl.goal.delete` / `pl.project.delete` keys); the `ProjectsControllerTests` pinned tests (the soft-delete + the dangling-association rule + the standing re-check). |
| **U10** | Close: doc sync + README + handoff notes + the gate | `docs/adr/README.md` (the ADR 0086 row, the `Accepted` status); `docs/ARCHITECTURE.md` §3 + §5 (the `Projects/` tree node updated — the two new docs + the two adapters; the §5 `Project` sketch — the **canonical names** this lane settles: `ProjectGoal` / `Project` / the two adapters + the `ProjectId` on `TodoItem` / `KanbanBoard`); `README.md` Roadmap (the **`PL`** lane entry — the one-sentence shape, the ADR 0086 reference, the **Done** marker); `docs/design/pl-goals-projects-design.md` (the **gate** section — the three-test record, the handoff pointer); `pl-handoff-notes.md` (the U00–U10 sections appended, the **gate** section); the lane folder **moved** to `docs/plans-milestones/done/pl/` (the M5 / EV-DWM close-unit precedent). |

## Sequencing invariants (the one-paragraph pin)

- **U00 is the sign-off gate** — it authors the design doc + ADR 0086; every
  later unit codes against the *locked* text (the M5 / M4 / EV-DWM precedent).
- **The service read lanes (U02 goals, U03 projects) land before any Web
  view** — U05–U09 code against the *frozen* `IProjectService` surface
  (the full-interface-first pattern, the M4 `IEventService` precedent; a
  rename or re-scope of a seam after U00 is a drift event).
- **The association write lanes (U04) land before any Web project-picker
  form (U08) posts against them** — the `SetTodoProjectAsync` /
  `SetBoardProjectAsync` seams are frozen before the edit forms seed the
  picker.
- **The delete lanes (U09) land after the detail views (U06 / U07) they
  extend** — the delete button is the last UI addition on the detail page.
- **The close (U10) is last** — the doc index + README + ARCHITECTURE.md are
  honest at ship time (the AGENTS.md doc↔code parity rule).

## The three-test gate (U10 records)

1. **Closed loop:** a resident creates a goal, creates a project under it,
   associates a to-do with the project, and sees the goal → project → to-do
   chain rendered on the `/projects` landing page + the goal detail + the
   project detail + the to-do detail (the `kw-l` `pl.*` keys + the `kw-dt`
   dates + the `bindRichEditor`-rendered descriptions all resolve).
2. **Handoff:** the `IProjectService` surface (the `ListGoalsAsync` /
   `GetGoalAsync` / `CreateGoalAsync` / `UpdateGoalAsync` /
   `ListProjectsAsync` / `GetProjectAsync` / `CreateProjectAsync` /
   `UpdateProjectAsync` / `SetTodoProjectAsync` / `SetBoardProjectAsync`
   seams) is **frozen** — the Web controller (U05–U09) codes against it,
   never re-derives access (the ADR 0006-D pin); the standing matrix
   (creator ∪ GlobalAdmin) is **re-checked server-side** in every write lane
   (C-M5·6); the `AccessAudit` rows (`TargetKind = "goal"` / `"project"`)
   are stored in the caller's session (C3).
3. **Part-vs-whole:** a goal / project's soft-delete (U09) **never deletes**
   the to-dos / boards associated with it (the dangling-association rule —
   D6); a to-do / board's `ProjectId` (U04 / U08) is a **feed filter, never a
   gate** (C-M3·2) — the to-do / board's own `Audience` decision is the
   access boundary (C-M5·3); the `/projects/todos` + `/projects/boards`
   routes keep their **exact** M5 paths (the route surface is untouched).

## Drift-guard (the one-paragraph pin)

- **No new `AccessAction`**, **no new `AccessVia`**, **no new authorization
  branch** — `PL` adds two *adapters* (`ProjectGoalToAuditableResource`,
  `ProjectToAuditableResource`), not a *branch* (the C-M5·11 precedent).
- **The standing matrix is creator ∪ GlobalAdmin** (the ADR 0070 board-edit
  precedent; **no** assignee branch — a goal / project is not assignable the
  way a to-do is).
- **The `ProjectId` on `TodoItem` / `KanbanBoard` is a feed filter, never a
  gate** (C-M3·2) — the to-do / board's own `Audience` decision is the access
  boundary (C-M5·3).
- **The `Status` on `Project` is a string, not an enum** (the C-M5·4
  string-status pin carried over; the lane's vocabulary is whatever the
  project author names).
- **The `StartAt` / `DueAt` on `Project` are optional** (the ADR 0079
  optional-date shape; `null` = no date).
- **The delete cascade is soft + non-destructive** (D6; the dangling-
  association rule; **no** hard delete anywhere).
- **The `/projects/todos` + `/projects/boards` routes keep their exact M5
  paths** (the route surface is untouched; the nav tab is the only visible
  change).
- **`M7` stays `StatusNext` on the roadmap** (the `Milestones.cs` /
  `MilestonesTests.cs` are **untouched** — a named lane, not a milestone, the
  ADR 0013 / 0015 precedent).

## Entry reads (the shared list)

- `docs/design/m5-projects-design.md` (the **M5 design doc** — the lane
  template this one builds on; the `TodoItem` / `KanbanBoard` /
  `KanbanLane` / `BoardItemPlacement` shapes, the `IProjectService` surface,
  the `TodoItemToAuditableResource` / `KanbanBoardToAuditableResource`
  adapter shapes, the `M5DocTypes` surface, the standing matrix, the
  `kw-l` key-registry shape).
- `docs/adr/0067-m5-projects-todos-and-kanban.md` (the ADR this lane
  extends — the bounded context, the `IProjectService` surface, the two
  adapters, the `M5DocTypes` surface, the standing matrix; confirm what is
  frozen).
- `docs/adr/0070-board-edit-lane-title-and-description.md` (the
  **creator ∪ GlobalAdmin** standing precedent this lane reuses — the
  `UpdateBoardAsync` full-update shape, the ADR 0070 §pin).
- `docs/adr/0079-todo-optional-start-and-due-dates.md` (the **optional-date**
  shape this lane reuses for `Project.StartAt` / `Project.DueAt` — the
  `datetime-local` field shape, the blank → `null` rule).
- `docs/adr/0024-author-soft-delete-lane.md` (the `IsDeleted` shape this
  lane reuses for `ProjectGoal.IsDeleted` + `Project.IsDeleted` — the
  soft-delete flag, the dangling-association rule).
- `docs/adr/0006-module-boundary-contracts.md` (the frozen
  `IAuthorizationService` / `IAuditableResource` surface — the two new
  adapters plug into it, no signature change; C1–C6).
- `docs/adr/0001-b-audience-and-access.md` + `docs/adr/0036-*.md` (the
  `Audience` doc shape — the **exact** post `Audience`, `null` = public;
  *reused*, not extended).
- `src/Kumunita.Core/Projects/IProjectService.cs` (the **frozen** M5
  surface — the two new adapters + the `ProjectId` field + the association
  lanes are **additive** on it; the full-interface-first pattern the new
  seams mirror).
- `src/Kumunita.Core/Projects/ProjectService.cs` (the **frozen** M5
  implementation — the standing re-check, the `AccessAudit` rows, the
  `IAuthorizationService` path; the new lanes mirror it).
- `src/Kumunita.Core/Projects/TodoItem.cs` + `KanbanBoard.cs` (the
  **existing** doc shapes — the `ProjectId?` field is **additive** on them,
  the C-M3·2 feed-filter pin).
- `src/Kumunita.Core/M5DocTypes.cs` (the **existing** surface — the two new
  docs + the `TodoItem` / `KanbanBoard` `ProjectId` indexes are **additive**
  on it; the `UniqueIndex` / `Index` shape the new indexes mirror).
- `src/Kumunita.Web/Controllers/ProjectsController.cs` (the **existing**
  Web surface — the `/projects` landing page + the goal / project views +
  the nav-tab update are **additive** on it; the route surface is
  untouched).
- `src/Kumunita.Web/Views/Projects/_ProjectsTabs.cshtml` (the **existing**
  nav-tab partial — the **Projects** tab is **additive** on it, the
  active-tab derivation extended).
- `src/Kumunita.Web/Views/Projects/TodosIndex.cshtml` + `BoardDetail.cshtml`
  (the **existing** to-do / board views — the project link + the project
  picker are **additive** on them).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the **existing**
  `kw-l` key-registry — the `pl.*` keys are **additive** on it, the
  `en` / `de` / `fr` / `da` floors).
- `docs/adr/README.md` (the ADR-numbering convention — confirm the next free
  number is **0086**; the ADR-index row shape the close unit U10 mirrors).
- `src/Kumunita.Web/Milestones.cs` + `tests/Kumunita.Web.Tests/MilestonesTests.cs`
  (the **roadmap** shape — confirm `M7` is the single `StatusNext`; **no**
  roadmap move in `PL` — a named lane, not a milestone).
- `docs/philosophy/templates/design-doc.md` (required section set).
