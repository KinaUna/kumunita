# ADR 0086 — Goals & Projects lane (`PL`): the higher-level goals + projects page on top of M5

Status: Accepted
Date: 2026-09-25
Amends: 0067 / 0070 / 0079 / 0024 / 0006 / 0001-B / 0036 / 0025 / 0031 / 0018 / 0019 / 0020

## Context

ADR 0067 shipped the M5 **work-item surface** — `TodoItem`, `KanbanBoard`,
`KanbanLane`, `BoardItemPlacement` in the `Kumunita.Core.Projects` context —
and deliberately left out the *higher level* (the M5 design doc D2's "a
per-project 'goals' rollup … M5 ships to-dos + boards, not a higher-level
goal doc"): the **goals** a neighborhood wants to reach and the **projects**
it runs to get there. There was also a standing gap on the M5 surface: a
to-do or a board had **no first-class link to the work it was done for** —
the closest field was the ad-hoc `TodoItem.Status` string.

One already-accepted decision constrains this lane:

- **ADR 0067 §2.3 freezes the `IProjectService` surface.** "An ADD beyond
  this list is a **new ADR**." So the new goal / project read / write lanes,
  the association lanes, the delete lanes, and the `projectId` feed-filter
  parameter this decision records are exactly what this ADR settles.
- **Board standing (C-M5·6)** is `creator ∪ GlobalAdmin` over the board
  (the `CheckBoardStanding` shape, ADR 0070) — the same matrix every
  board-scoped write uses, so goal / project write standing reuses it
  rather than inventing a new one (a goal / project has **no** assignee
  collaborator).
- **The `ProjectId` on `TodoItem` / `KanbanBoard` is additive** (ADR 0004
  §B.1 delta-detect; zero migration for existing rows) and a **feed filter,
  never a gate** (C-M3·2, the `ComponentId` shape) — the to-do's / board's
  own `Audience` decision stays the access boundary (C-M5·3).

## Decision

- **Two new documents, one existing context (D1).** `ProjectGoal` +
  `Project` (ns `Kumunita.Core.Projects` — the M5 context; a *new* context
  would be overkill for two docs that share the same standing / audience /
  renderer surface), additive on **`M5DocTypes`** (the surface already
  delta-detects; zero new surface file, zero migration for existing docs).
- **`ProjectGoal` (D2).** `Id`; `Title` (non-empty); `Description?`
  (Markdown — the ADR 0025 shape, optional); `ComponentId?` (a feed filter,
  never a gate); `AuthorId` (the standing owner); `Audience?` (the **exact**
  post `Audience` — ADR 0001-B / 0036, `null` = public); `IsDeleted` (the
  ADR 0024 soft-delete flag, `false` default); `LanguageCode` (ADR 0018);
  `Created`; `Modified?`. **No** `IsDraft`, **no** dates (a goal is a
  direction, not a scheduled thing), **no** `ProjectId` (the association is
  *outward* — a project points at its goal, not the reverse).
- **`Project` (D3).** `Id`; `Title` (non-empty); `Description?` (Markdown);
  `GoalId?` (the optional goal — `null` = standalone project; the **sole**
  association mechanism); `Status?` (a **string** — the state label; `null`
  = none; **not** an enum — the C-M5·4 string-status pin carried over);
  `StartAt?` / `DueAt?` (both **nullable** `DateTimeOffset` — the ADR 0079
  optional-date shape); `ComponentId?`; `AuthorId`; `Audience?` (the exact
  post `Audience`); `IsDeleted` (ADR 0024 flag); `LanguageCode` (ADR 0018);
  `Created`; `Modified?`. **No** `IsDraft`.
- **One `ProjectId?` on `TodoItem` + `KanbanBoard` (D4).** A feed filter,
  never a gate (C-M3·2; the `ComponentId` shape, **not** a gate); additive
  (ADR 0004 §B.1 — zero migration for existing rows, which simply have
  `ProjectId == null`); a display + filter + standing context, never an
  access boundary (a to-do's / board's `Read` decision stays its **own**
  `Audience` — C-M5·3).
- **Standing (D5).** **Creator ∪ GlobalAdmin** over a goal / project (the
  ADR 0070 board-edit precedent; **no** assignee branch); re-checked
  server-side in every write lane (C-M5·6); the Web `[Authorize]` is a
  convenience pre-gate only.
- **Delete cascade semantics (D6 — soft, non-destructive).** Deleting a
  **goal** sets `ProjectGoal.IsDeleted = true`; the `Project.GoalId` rows
  are **kept** (the association dangles; the link is not rendered when the
  goal is soft-deleted). Deleting a **project** sets
  `Project.IsDeleted = true`; the `TodoItem.ProjectId` /
  `KanbanBoard.ProjectId` rows are **kept** (the same rule). **No hard
  delete anywhere.**
- **Two association write lanes (D7).**
  `SetTodoProjectAsync(todoItemId, actorId, actorRoles, projectId?, ct)`
  (standing = the to-do's own matrix — creator ∪ assignee ∪ GlobalAdmin,
  matching `AssignTodoAsync`) +
  `SetBoardProjectAsync(boardId, actorId, actorRoles, projectId?, ct)`
  (standing = the board's own matrix — creator ∪ GlobalAdmin, matching
  `UpdateBoardAsync`); each re-checks standing server-side, writes
  `ProjectId` (or clears it when `null`), stamps `Modified`, stores one
  `AccessAudit` row (`TargetKind = "todo"` / `"board"`); a non-null
  `projectId` pointing at a soft-deleted or unreadable project is
  **refused** (the C3 404-vs-403 split). **No new `AccessAction`, no new
  `AccessVia`** — the lanes reuse the frozen `Read` action and the existing
  per-resource standing matrix (the C-M5·11 precedent).
- **The `/projects` landing page (D8).** `GET /projects` (new route on
  `ProjectsController`) renders the **goals + standalone-projects feed**;
  the `/projects/todos` + `/projects/boards` routes **keep their exact
  paths**; `_ProjectsTabs.cshtml` gains the **Projects** tab (the first
  entry) + the active-tab derivation extends.
- **The project link + picker on the to-do / board surface (D9).** A to-do
  / board with a non-null `ProjectId` (target readable + non-deleted)
  renders a **"Project: {title}"** link on its detail view (`kw-l`
  `pl.todo.project_link` / `pl.board.project_link`); its create / edit form
  gains a **project picker** (`<select>` of the actor's readable,
  non-deleted projects, the `SeedComponentPickerAsync` shape — a *display*
  surface, never a gate).
- **The goal link + picker on the project surface (D10).** A project with a
  non-null `GoalId` (target readable + non-deleted) renders a **"Goal:
  {title}"** link on its detail view (`kw-l` `pl.project.goal_link`); its
  composer / edit form gains a **goal picker** (the same shape).
- **`kw-l` keys (D11).** A `pl.*` key namespace
  (`pl.goal.*` / `pl.project.*` / `pl.todo.project_link` /
  `pl.board.project_link` / `pl.projects_tab`) in
  `KnownTranslationKeys.cs`, **× 4 languages** (en/de/fr/da — the ADR 0015
  registry, the ADR 0052 warm-boot baseline backfill covering the non-`en`
  rows).
- **Two new adapters, not a branch.**
  `ProjectGoalToAuditableResource` (`TargetKind = "goal"`) +
  `ProjectToAuditableResource` (`TargetKind = "project"`) implement the
  frozen 6-member `IAuditableResource` surface; the read path is the frozen
  `IAuthorizationService.CanAsync(Read)` / `CanSeeAsync(Read)` (ADR 0006)
  — no signature change, no new action, no new via, no new `Decide()`
  branch.
- **`Milestones.cs` / README Roadmap / `MilestonesTests.cs` untouched** —
  `PL` is a **named lane, not a milestone** (the ADR 0013 / 0015 / 0084 /
  0085 precedent): `M7` stays the single `StatusNext`; the README Roadmap
  gains one `PL` lane entry at close (U10).

## Consequences

- The `M5DocTypes` surface grows by two documents + four indexes (the
  `(ComponentId, Created)` feed indexes on `ProjectGoal` + `Project`, the
  `GoalId` index on `Project`, the `ProjectId` indexes on `TodoItem` +
  `KanbanBoard`) — delta-detected at boot (ADR 0004 §B.1), zero migration
  for existing rows.
- The frozen `IAuthorizationService` path gains two adapters
  (`ProjectGoalToAuditableResource` / `ProjectToAuditableResource`) — no
  branch, no new action, no new via (C-M5·11).
- The `IProjectService` surface grows additively: the goal read / write
  lanes, the project read / write lanes, the `projectId` optional filter
  parameter on `ListTodosAsync` / `ListBoardsAsync` (every existing call
  site + the existing M5 tests compile and behave unchanged), the two
  association lanes, and the two delete lanes — all over the **creator ∪
  GlobalAdmin** standing matrix (the association lanes use each resource's
  own matrix) and the C3 404-vs-403 split, each write storing one
  `AccessAudit` row in the caller's session.
- `ProjectsController` gains the `GET /projects` route + the goal /
  project detail / composer / edit routes + the two delete POSTs; the M5
  route surface (`/projects/todos` / `/projects/boards` + the existing
  detail / edit / lane / placement actions) is untouched.
- `_ProjectsTabs.cshtml` grows the **Projects** tab; the to-do / board
  detail views grow the project link; the to-do / board create + edit forms
  + the project composer / edit grow the pickers.
- `KnownTranslationKeys.cs` grows the `pl.*` key block × 4 languages (the
  ADR 0015 registry; the ADR 0052 warm-boot baseline backfill covers the
  non-`en` rows).
- The lane is **additive and reusing** — no new bounded context, no new
  `AccessAction` / `AccessVia` / `Decide()` branch, no new editor module,
  no hard delete anywhere, no `Milestones.cs` move.
