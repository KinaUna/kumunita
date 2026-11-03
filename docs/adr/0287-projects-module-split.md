---
id: adr-0287
title: "Projects module split: IProjectService (projects + goals), IBoardService (boards, lanes, placements), ITodoService (todos, comments, claim/assign)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0067-m5-projects-todos-and-kanban.md
  - docs/adr/0088-projects-todos-boards-translations.md
  - docs/plans-milestones/plan-improve-code.md
supersedes: []
superseded_by: []
updated: 2026-11-03
---

# ADR [0287](0287-projects-module-split.md) — Projects module split

Status: Accepted
Date: 2026-11-03
Amends: ADR [0067](0067-m5-projects-todos-and-kanban.md) §2.3 (the `IProjectService` surface freeze)

> **Abstract:** `IProjectService` has 58 methods spanning three aggregates.
> This ADR splits it into `IProjectService` (projects + goals),
> `IBoardService` (boards, lanes, placements), and `ITodoService` (todos,
> comments, claim/assign). Web gets `BoardsController`, `TodosController`,
> `ProjectsController` with routes unchanged. **For** a developer touching
> `Kumunita.Core.Projects`. **When to read the rest:** before adding a
> method or migrating a caller.

## Context

ADR [0067](0067-m5-projects-todos-and-kanban.md) §2.3 froze the `IProjectService` surface. Since then, ADR
0068 / 0069 / 0070 / 0071 / [0073](0073-todo-group-community-claim-lane.md) / [0074](0074-kanban-card-assign-to-person-group-community.md) / [0086](0086-goals-projects-lane.md) /
0087 / [0088](0088-projects-todos-boards-translations.md) / [0090](0090-pagination-and-filtering.md) / [0097](0097-board-lane-delete-lane.md) / [0098](0098-board-audience-edit-lane.md) / [0099](0099-todo-inherits-board-audience.md) /
[0100](0100-todo-comments-lane.md) / [0115](0115-integration-of-events-and-projects.md) have added 34 methods. The interface
now has 58 methods; `ProjectService` is 5 016 code lines. The IMPROVE-CODE
plan (C08) requires no Projects file over the file ceiling.

## Decision

- **Three interfaces in the Projects module**, each satisfying the
  part-vs-whole test (≥ 2 callers or owns a route family / aggregate):
  **`IProjectService`** (projects + goals — CRUD, association, translations),
  **`IBoardService`** (boards, lanes, placements — CRUD, placement moves,
  board ↔ project / event association, translations), and **`ITodoService`**
  (todos, comments, claim / assign — CRUD, subtasks, claim / assign / status,
  todo ↔ project / event association, translations).
- **Web: three controllers.** `ProjectsController` (`/projects`,
  `/projects/goals`), `BoardsController` (`/projects/boards`),
  `TodosController` (`/projects/todos`). **Route attributes move verbatim**;
  the route table is unchanged (C08 proves it by diffing
  `EndpointDataSource`).
- **Seam relocation.** ADR citations that name
  `IProjectService.<method>` keep resolving: the method's new home is the
  sub-interface that owns it. The ADR 0067 §2.3 freeze now applies **per
  sub-interface** (each is still "frozen"). The "frozen seams" cited by
  ADR 0067 / 0073 / 0086 / 0087 / 0088 / 0090 / 0097 / 0098 / 0099 / 0100 /
  [0115](0115-integration-of-events-and-projects.md) / [0163](0163-home-upcoming-events-and-todos.md) / [0173](0173-report-targets-lane.md) resolve to the
  specific sub-interface. The kiosk (ADR [0164](0164-kiosk-mode.md) / [0285](0285-kiosk-playlist-any-item.md)) reads
  through the frozen `IBoardService.GetBoardAsync` /
  `ITodoService.ListTodosAsync` / `IProjectService.GetProjectAsync` seams.
- **Translation lanes.** The 12 translation methods (ADR [0088](0088-projects-todos-boards-translations.md) D2) move
  to their owning sub-interface; C06 migrates them to the shared
  `TranslationLane` (ADR [0288](0288-one-translation-variant-lane.md)).

## Consequences

- **Positive:** each sub-interface has a clear caller set and can be
  unit-tested in isolation; `ProjectsController` splits into three
  controllers, each under the method ceiling.
- **Negative:** three more DI registrations; `ProjectService` splits into
  three implementation classes (C08); shared standing checks go to the C04
  policies. `IBoardService` (~24 methods) exceeds the D2 interface ceiling
  (20); C08 baselines it and shrinks it in subsequent commits.
