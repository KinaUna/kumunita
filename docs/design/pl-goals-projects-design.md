# `PL` — Goals & Projects — design (Part 1 + Part 2)

> **Named lane.** `PL` (short id **`PL`** = *Projects Lane*) — the higher-level
> **goals** and **projects** page on top of the M5 to-do / board surface.
> **ADR 0086** is this lane's decision record — **authored in this unit (U00)**,
> **Accepted 2026-09-25**; every decision D1–D12 below is **locked** (the
> `[PROPOSED]` markers in the lane plan `plan-pl-goals-projects.md` were the
> pre-lock shape). The sealed-unit register is
> `docs/plans-milestones/in-progress/pl/plan-pl-goals-projects.md`; the
> scratch log is
> `docs/plans-milestones/in-progress/pl/pl-handoff-notes.md` (one `## U#`
> section per unit, appended, never rewritten).
>
> **Status.** **Part 1 (U00) LOCKED; Part 2 (U00) LOCKED.** The decisions
> D1–D12 are locked in **ADR 0086 (Accepted, 2026-09-25)**; **Part 2** (the
> exact C# shapes, the additive `IProjectService` seams, the request DTOs,
> the `M5DocTypes` additive shape, the pinned test names, the three-test
> acceptance gate, and the drift-guard) follows in the "Seams & contracts"
> section below. **U00 authors both parts** (the M5 two-unit U00/U01 split is
> not required — this lane is smaller).
>
> **Roadmap confirm (U00):** `M7` ("Pagination and filtering") is the single
> `StatusNext` on `Milestones.cs` (verified 2026-09-25); `PL` is a **named
> lane, not a milestone** — `Milestones.cs` / `MilestonesTests.cs` are
> **untouched** (the ADR 0013 / 0015 / 0084 / 0085 named-lane precedent). The
> README Roadmap gains one `PL` lane entry at close (U10).
>
> **Scope of this file:** what this lane is; the existing surface it reuses
> — *verified against the actual files*; the design decisions (locked); the
> invariants (C-PL·1…8); the FACES (F1–F10); the parts affected; the
> feedback-loop shape; and the risks. **Part 2:** the exact POCO field sets,
> the two adapters, the additive `IProjectService` seams, the request DTOs,
> the `M5DocTypes` additive shape, the pinned test names, the gate, and the
> drift-guard.

## 1. What this lane is

M5 (ADR 0067) shipped the **work items** — assignable, hierarchical
**to-dos** and **Kanban boards** — and deliberately left out the *higher
level* (the M5 design doc D2's "a per-project 'goals' rollup … M5 ships
to-dos + boards, not a higher-level goal doc"): the **goals** a neighborhood
wants to reach and the **projects** it runs to get there. `PL` adds exactly
that level, inside the **existing** `Kumunita.Core.Projects` context:

- a **`ProjectGoal`** (title + optional Markdown description) — the
  organizing container a project can hang off;
- a **`Project`** (title + optional description + optional `Status` string +
  optional start/due dates + an optional `GoalId`) — the unit of *managed*
  work, and what to-dos and boards associate **to**;
- one **`ProjectId?`** feed-filter field on the **existing** `TodoItem` and
  `KanbanBoard` documents (a *filter, never a gate* — C-M3·2);
- the **`/projects` landing page** (goals + projects feed) and the **third
  nav tab** ("Projects") in `_ProjectsTabs.cshtml`;
- a **project picker** on the to-do / board create + edit forms and a
  **project link** on their detail views, and a **goal picker** on the
  project composer / edit + a **goal link** on the project detail view.

The `/projects/todos` and `/projects/boards` routes **keep their exact M5
paths** — the to-do / board feeds keep their current routes, now one tab
deeper. The M5 work-item surface (the `TodoItem` / `KanbanBoard` /
`KanbanLane` / `BoardItemPlacement` documents, the read / write / placement
lanes) is **untouched in behavior** — the `ProjectId` field is *additive* on
the two existing documents (ADR 0004 §B.1 delta-detect; zero migration for
existing rows), and the existing `IProjectService` seams gain one additive
optional `projectId` filter parameter each on the two feed seams (the
frozen-surface rule honoured by this ADR).

**The one thing every unit must respect:** this lane is **additive and
reusing.** It reuses the `Audience` doc (ADR 0001-B / 0036 — *reused*, not
extended) on **both** `ProjectGoal` and `Project`, the frozen
`IAuthorizationService.CanAsync(Read)` / `CanSeeAsync(Read)` decision path
(ADR 0006) through **two new adapters** (`ProjectGoalToAuditableResource`,
`ProjectToAuditableResource`), the standing matrix **creator ∪ GlobalAdmin**
(the ADR 0070 board-edit precedent — there is *no* assignee collaborator on
a goal or project), the `MarkdownRenderer` + `bindRichEditor` (ADR 0025 /
0031) for the optional description, the `kw-dt` TagHelper (ADR 0019 / 0020)
for the dates, and the existing `AudienceEditorModel` / component-picker /
language-picker composer trio (the M2/M3/M4/M5 pattern). **No new
`AccessAction`**, **no new `AccessVia`**, **no new authorization branch** —
`PL` adds two *adapters*, not a *branch* (the C-M5·11 precedent). **No code
in this unit — no build, no tests.**

## 2. The existing surface this lane reuses (verified)

Read directly from the actual files (not assumed), each a **frozen seam** PL
builds on without re-inventing:

1. **`TodoItem` / `KanbanBoard` + `TodoItemToAuditableResource` /
   `KanbanBoardToAuditableResource` + `ProjectService`**
   (`Kumunita.Core.Projects`, M5 / ADR 0067) — the **immediate template**:
   the field-by-field provenance shape (§9.1 mirrors it), the two 6-member
   adapters (§9.2 mirrors them verbatim), the standing re-check helpers
   (`CheckBoardStanding` creator ∪ GlobalAdmin — ADR 0070), the
   `AccessAudit` row shape (C3), and the `M5DocTypes` surface (§9.5
   extends it additively).
2. **`Audience`** (`Kumunita.Core.Authorization`) — the exact post
   `Audience` doc (ADR 0001-B / 0036; `null` = public, the frozen
   `Decide()` branch 5). Both new documents carry it verbatim; the
   `AudienceEditorModel` + `BuildAudience()` single-source form surface
   (M2, reused by every composer) is the goal / project composer's audience
   picker. **Reused, not extended.**
3. **`IAuthorizationService`** (the frozen 4-method Read surface, ADR 0006)
   — `CanSeeAsync(Read)` for the feeds (one shared matching pass; the single
   aggregate `AccessAudit` row), `CanAsync(Read)` for the details (the
   404-vs-403 split). The two new adapters plug into it; **no signature
   change, no new `AccessAction`, no new `AccessVia`, no new `Decide()`
   branch** (C-PL·1).
4. **`ProjectService.UpdateBoardAsync`** (ADR 0070) — the **creator ∪
   GlobalAdmin** standing precedent this lane reuses for goal / project
   write standing (there is *no* assignee collaborator on a goal or
   project), and the **full-update** shape (`Title` + `Description`, blank
   description → `null`, `Modified` stamped on a real change only).
5. **`TodoItem.StartAt` / `TodoItem.DueAt`** (ADR 0079) — the **optional
   date** shape: two nullable `DateTimeOffset?` fields, stored UTC, rendered
   by the one `<kw-dt>` TagHelper in the viewer's effective timezone (ADR
   0019), bound from `type="datetime-local"` inputs; `null` = no date.
   `Project.StartAt` / `Project.DueAt` are exactly this, verbatim.
6. **`TodoItem.IsDeleted` / `KanbanBoard.IsDeleted`** (ADR 0024) — the
   **soft-delete flag** (`bool`, `false` default) + the **dangling-
   association rule** (the referencing row is *kept*; the read lane's
   `!IsDeleted` filter is what hides it; the link is not rendered when the
   target is soft-deleted). `ProjectGoal.IsDeleted` / `Project.IsDeleted`
   are exactly this.
7. **`MarkdownRenderer` + `bindRichEditor`** (RC / RE, ADR 0025 / 0031 /
   0033) — the one renderer on the read path, the one editor on the write
   path. The optional goal / project `Description` is rendered by the one
   `MarkdownRenderer` and edited by the one `bindRichEditor` — no new
   editor, no new TS editor module. (The card / feed descriptions are
   pre-rendered via `MarkdownRenderer`, **not** the WYSIWYG `rc-editor`.)
8. **`kw-dt` TagHelper** (ADR 0019 / 0020) — the one timestamp renderer:
   per-request resolver (resident override → platform default → `UTC`
   floor) + format (resident override → platform default → Long floor).
   Every PL timestamp (`ProjectGoal.Created` / `Modified?`,
   `Project.StartAt` / `DueAt` / `Created` / `Modified?`) renders through it.
9. **`ProjectsController`** (`Kumunita.Web/Controllers/`) — the **existing**
   M5 controller: `TodosIndex` / `TodoDetail` / `CreateGet` / `CreatePost` /
   `UpdatePost` / `BoardsIndex` / `BoardDetail` / `BoardCreateGet` /
   `BoardCreatePost` / `BoardEditGet` / `BoardEditPost` (+ the lane /
   placement / reorder actions). The PL routes (`GET /projects`,
   `GET|POST /projects/goals/…`, `GET|POST /projects/projects/…`) are
   **additive** on it; the existing route surface is untouched.
10. **`Views/Projects/_ProjectsTabs.cshtml`** — the existing two-tab
    sub-nav (To-dos, Boards; active-tab derived from the action name, the
    `_AdminNav` idiom). The **Projects** tab is additive (the first entry);
    the active-tab derivation extends to the new action names.
11. **`KnownTranslationKeys.cs`** (`Kumunita.Core.Localization`) — the
    `kw-l` registry (ADR 0015) with the four seeded languages (en/de/fr/da,
    the ADR 0042 / 0045 set) + the ADR 0052 warm-boot baseline backfill that
    covers new keys' `de`/`fr`/`da` rows automatically. The `pl.*` keys
    (D11) are additive on it.
12. **`M5DocTypes`** (`Kumunita.Core`) — the existing surface the two new
    docs + the two `ProjectId` indexes register on (the ADR 0004 §B.1
    delta-detect shape; the unnamed-computed-index constraint noted there
    applies to the new `(ComponentId, Created)` indexes too).
13. **`PostgresFixture`** (`tests/Kumunita.Core.Tests`, Testcontainers
    `postgres:18`) — the harness the Core pinned tests run over;
    `Kumunita.Web.Tests` uses NSubstitute (no Postgres) for the Web pins.

## 3. The design decisions (locked)

Locked by **ADR 0086 (Accepted, 2026-09-25)**; the `[PROPOSED]` markers in
the lane plan are retired by this lock.

### 3.1 Two new documents, one existing context (D1)
`ProjectGoal` + `Project` (ns `Kumunita.Core.Projects`, the M5 context — a
*new* context would be overkill for two docs that share the same standing /
audience / renderer surface). Additive on **`M5DocTypes`** (ADR 0004 §B.1 —
the surface already delta-detects; zero new surface file, zero migration for
existing docs). *(C-PL·8.)*

### 3.2 `ProjectGoal` shape (D2)
`Id`; `Title` (non-empty); `Description?` (Markdown — the ADR 0025 shape,
optional); `ComponentId?` (a feed filter, never a gate — C-M3·2); `AuthorId`
(the standing owner — C-M5·6); `Audience?` (the **exact** post `Audience` —
ADR 0001-B / 0036, `null` = public); `IsDeleted` (the ADR 0024 soft-delete
flag, `false` default); `LanguageCode` (ADR 0018 authored-in tag);
`Created`; `Modified?`. **No** `IsDraft`, **no** dates (a goal is a
direction, not a scheduled thing), **no** `ProjectId` (the association is
*outward* — a project points at its goal, not the reverse).

### 3.3 `Project` shape (D3)
`Id`; `Title` (non-empty); `Description?` (Markdown); `GoalId?` (the
optional goal — `null` = standalone project; the **sole** association
mechanism, the `TodoItem.ParentId` shape in intent); `Status?` (a **string**
— the state label; `null` = none; **not** an enum — the C-M5·4
string-status pin carried over); `StartAt?` / `DueAt?` (both **nullable**
`DateTimeOffset` — the ADR 0079 optional-date shape); `ComponentId?` (feed
filter, never a gate); `AuthorId` (standing owner); `Audience?` (the exact
post `Audience`); `IsDeleted` (ADR 0024 flag); `LanguageCode` (ADR 0018);
`Created`; `Modified?`. **No** `IsDraft`.

### 3.4 To-dos and boards gain one optional `ProjectId?` each (D4)
A `ProjectId?` on **`TodoItem`** and **`KanbanBoard`** (a feed filter,
never a gate — C-M3·2; the `ComponentId` shape, **not** a gate). The
`ProjectId` is **additive** (ADR 0004 §B.1 — Marten delta-detects the new
column; **zero migration for existing rows** — existing to-dos / boards
simply have `ProjectId == null`). It is a **display + filter + standing
context**, never an access boundary (a to-do's / board's `Read` decision
stays its **own** `Audience` — C-M5·3; `ProjectId` changes nothing about
who may *see* the to-do / board).

### 3.5 Standing (D5)
**Creator ∪ GlobalAdmin** over a goal / project (the ADR 0070 board-edit
precedent; **no** assignee branch — a goal / project is not assignable the
way a to-do is). The standing is **re-checked server-side** in every write
lane (C-M5·6); the Web `[Authorize]` is a convenience pre-gate only.

### 3.6 Delete cascade semantics (D6)
Deleting a **goal** sets `ProjectGoal.IsDeleted = true`; the
`Project.GoalId` rows are **kept** (the association dangles; the project
detail view's goal link is not rendered when the goal is soft-deleted — the
read lane's filter, the ADR 0024 shape). Deleting a **project** sets
`Project.IsDeleted = true`; the `TodoItem.ProjectId` /
`KanbanBoard.ProjectId` rows are **kept** (the same dangling-association
rule; the to-do / board detail view's project link is not rendered when the
project is soft-deleted). **No hard delete anywhere.**

### 3.7 The association write lane (D7)
One **`SetTodoProjectAsync`** + one **`SetBoardProjectAsync`** — each
re-checks standing **over its own to-do / board** server-side — the to-do
lane uses the to-do's own standing matrix (creator ∪ assignee ∪ GlobalAdmin,
the C-M5·6 shape, matching `AssignTodoAsync`), the board lane uses the
board's own (creator ∪ GlobalAdmin, the ADR 0070 shape, matching
`UpdateBoardAsync`) — writes `ProjectId` (or clears it when `null`), stamps
`Modified`, and stores one `AccessAudit` row (`TargetKind = "todo"` /
`"board"`). A non-null `projectId` pointing at a soft-deleted or unreadable
project is **refused** (the C3 404-vs-403 split). **No new
`AccessAction`**, **no new `AccessVia`** — the lanes reuse the frozen
`Read` action and the existing per-resource standing matrix (the C-M5·11
precedent).

### 3.8 The `/projects` landing page (D8)
`GET /projects` (new route on `ProjectsController`) renders the **goals +
projects feed**: the goals (pinned list, each with its associated projects
inline or linked) + the **standalone projects** (the `GoalId == null`
projects) in one view. The `/projects/todos` + `/projects/boards` routes
**keep their exact paths** (the M5 route surface is untouched — the nav tab
is the only visible change). The `_ProjectsTabs.cshtml` partial grows a
**Projects** tab (the first entry — the page the lane is named for) pointing
at `/projects`, with the active-tab derivation extended to recognize the new
action names.

### 3.9 The project link on the to-do / board detail views (D9)
A to-do / board with a non-null `ProjectId` (and a non-deleted target project
the actor may read) renders a **"Project: {title}"** link (the `kw-l` key
`pl.todo.project_link` / `pl.board.project_link`) on its detail view,
linking to `/projects/projects/{id}`. The to-do / board **edit** form gains
a **project picker** (a `<select>` of the actor's readable, non-deleted
projects, the `SeedComponentPickerAsync` shape, seeded in the controller —
the picker is a *display* surface, never a gate).

### 3.10 The goal link on the project detail view (D10)
A project with a non-null `GoalId` (and a non-deleted target goal the actor
may read) renders a **"Goal: {title}"** link (the `kw-l` key
`pl.project.goal_link`) linking to `/projects/goals/{id}`. The project
**composer / edit** form gains a **goal picker** (a `<select>` of the
actor's readable, non-deleted goals, the same picker shape as D9).

### 3.11 `kw-l` keys (D11)
A `pl.*` key namespace (the `projects.*` shape the M5 lane used):
`pl.goal.*` (title / description / new / edit / delete / projects / empty),
`pl.project.*` (title / description / status / start_date / due_date / new /
edit / delete / goal / goal_link / associated / empty),
`pl.todo.project_link`, `pl.board.project_link`, `pl.projects_tab` (the
nav-tab label). **× 4 languages** (the `KnownTranslationKeys.cs`
`en` / `de` / `fr` / `da` floors — the M5 lane's 19-key × 4-language
precedent; the exact count is pinned in §9.6).

### 3.12 The ADR number (D12)
**ADR 0086** (the next free number after ADR 0085 — `docs/adr/README.md`
confirms 0085 is the current highest, verified 2026-09-25).

**Out of scope (→ follow-on lanes, own ADRs):** a per-project *progress
rollup* (the to-do / board count is the display nicety — a follow-on lane),
a **goal** rollup to a project's due date (a display nicety — a follow-on
lane), drag-and-drop association (the explicit form POST is the M5 pin —
ADR 0031's plain-GET/POST posture), a per-goal / per-project *member*
surface (the standing matrix is creator ∪ GlobalAdmin; a collaborator lane
is a follow-on ADR), and a **board-per-project** grouping view (the project
detail page's associated-to-dos / boards list is the unit; a dedicated
board-view is a follow-on lane).

## 4. Invariants (pinned for PL)

- **C-PL·1 — Reuse, don't reinvent the authorization surface.** **No new
  `AccessAction`**, **no new `AccessVia`**, **no new authorization branch**
  in `Decide()` — `PL` adds **two adapters**
  (`ProjectGoalToAuditableResource`, `ProjectToAuditableResource`) over the
  frozen `IAuthorizationService.CanAsync(Read)` / `CanSeeAsync(Read)` path
  (ADR 0006), not a branch (the C-M5·11 precedent).
- **C-PL·2 — Standing is creator ∪ GlobalAdmin, re-checked server-side.**
  Over a goal or a project (the ADR 0070 board-edit precedent; **no**
  assignee branch — a goal / project is not assignable the way a to-do is).
  The Web `[Authorize]` is a convenience pre-gate only (C-M5·6); the
  association lanes use each resource's **own** matrix — the to-do's
  (creator ∪ assignee ∪ GlobalAdmin) and the board's (creator ∪ GlobalAdmin).
- **C-PL·3 — `ProjectId` on `TodoItem` / `KanbanBoard` is a feed filter,
  never a gate.** The to-do's / board's own `Audience` decision is the
  access boundary (C-M5·3); `ProjectId` changes nothing about who may *see*
  the to-do / board — it is display + filter + standing context only
  (C-M3·2, the `ComponentId` shape).
- **C-PL·4 — `Project.Status` is a string, not an enum** (the C-M5·4
  string-status pin carried over; `null` = none; the lane's vocabulary is
  whatever the project author names; no registry, no enum, no server-side
  status machine — unlike a `KanbanLane`, which *imparts* status, a
  `Project.Status` is a self-labelled state).
- **C-PL·5 — `Project.StartAt` / `Project.DueAt` are optional** (the ADR
  0079 optional-date shape; both nullable `DateTimeOffset`; `null` = no
  date; stored UTC, rendered by the one `kw-dt` TagHelper, ADR 0019 / 0020;
  `type="datetime-local"` inputs bound model-side to nullable
  `DateTimeOffset?`; blank → `null`).
- **C-PL·6 — The delete cascade is soft + non-destructive.** A goal's /
  project's soft-delete (the ADR 0024 `IsDeleted` flag) **never deletes**
  the projects / to-dos / boards associated with it — the association field
  simply **dangles** and the read lane's `!IsDeleted` filter is what hides
  the target (the D6 rule; **no** hard delete anywhere).
- **C-PL·7 — The M5 route surface is untouched.** The
  `/projects/todos` + `/projects/boards` routes keep their **exact** M5
  paths; the `PL` routes (`/projects`, `/projects/goals/…`,
  `/projects/projects/…`) are additive; the nav tab is the only visible
  change on the M5 views.
- **C-PL·8 — Additive only.** Two new documents + two new adapters on the
  existing `Projects` context (D1); the `M5DocTypes` surface and the
  `IProjectService` seam grow **additively** (new docs registered, new
  optional feed-filter param, new lanes) — every existing M5 call site
  compiles and behaves **unchanged** (the ADR 0004 §B.1 + frozen-surface
  rule, the ADR 0070 / 0073 / 0074 / 0079 precedent); **zero migration for
  existing rows**.

## 5. FACES (pinned, 10)

- **F1** a goal is visible to its audience member and hidden from a
  non-member (the goal feed's `CanSeeAsync(Read)` filter) — C-PL·1
- **F2** a goal / project detail is a 404 when absent and a 403 when the
  actor is denied (the C3 split, one `CanAsync(Read)`) — C-PL·1
- **F3** a goal's / project's `Status` is a free string the author names
  (rendered verbatim; no server-side status machine) — C-PL·4
- **F4** a project's `StartAt` / `DueAt` are optional — a blank
  `datetime-local` field stores `null`, a value stores the parsed instant —
  C-PL·5
- **F5** a project may be standalone (`GoalId == null`) or under exactly
  one goal; a `GoalId` pointing at a soft-deleted or unreadable goal is
  **refused** on create and re-association (the C3 split) — C-PL·8
- **F6** a to-do / board may carry a `ProjectId`; it is a feed filter and a
  detail-view link, and it never changes the to-do's / board's own
  audience decision — C-PL·3
- **F7** setting a to-do / board's project re-checks standing over the
  **to-do** (creator ∪ assignee ∪ GlobalAdmin) / the **board** (creator ∪
  GlobalAdmin) server-side; a non-null `projectId` at a soft-deleted or
  unreadable project is refused — C-PL·2 / C-PL·1
- **F8** deleting a goal keeps its projects (the `GoalId` dangles); deleting
  a project keeps its to-dos / boards (the `ProjectId` dangles) — the
  dangling-association rule; the links are not rendered when the target is
  soft-deleted — C-PL·6
- **F9** the `/projects` landing renders the goals + the standalone projects
  (the `GoalId == null` feed) with the **Projects** nav tab active; the
  `/projects/todos` + `/projects/boards` routes are untouched — C-PL·7
- **F10** the to-do / board create + edit forms seed a project picker (the
  actor's readable, non-deleted projects — a display surface, never a gate)
  and the goal / project composer seeds a goal picker — C-PL·2 / C-PL·3

## 6. Parts affected

- **`Kumunita.Core.Projects`** — **two new documents**: `ProjectGoal`,
  `Project`; **two new adapters**: `ProjectGoalToAuditableResource`,
  `ProjectToAuditableResource`; **four new request DTOs**:
  `CreateGoalRequest`, `UpdateGoalRequest`, `CreateProjectRequest`,
  `UpdateProjectRequest` (in `IProjectService.cs` or a new
  `ProjectRequests.cs` — U01 settles; the shape is pinned in §9.4).
- **`TodoItem` / `KanbanBoard`** — one additive `ProjectId?` field each
  (U01; the C-PL·3 feed-filter pin).
- **`M5DocTypes`** — additive registration: the two new docs + the
  `(ComponentId, Created)` feed indexes on each + the `GoalId` index on
  `Project` + the `ProjectId` index on `TodoItem` / `KanbanBoard` (U01).
- **`IProjectService` + `ProjectService`** — additive seams (U02–U04, U09):
  the goal read / write lanes, the project read / write lanes, the
  `projectId` filter on the two feed seams, the association lanes, the
  delete lanes (the exact C# in §9.3).
- **`ProjectsController`** (`Kumunita.Web/Controllers/`) — additive routes:
  `GET /projects` (U05); the goal detail / composer / edit (U06); the
  project detail / composer / edit (U07); the project picker seeding +
  project link on the to-do / board forms + details (U08); the two delete
  POSTs (U09).
- **`Views/Projects/`** — new views: `ProjectsIndex.cshtml`,
  `GoalDetail.cshtml`, `GoalNew.cshtml`, `GoalEdit.cshtml`,
  `ProjectDetail.cshtml`, `ProjectNew.cshtml`, `ProjectEdit.cshtml`;
  additive updates: `_ProjectsTabs.cshtml` (the **Projects** tab),
  `TodoDetail.cshtml` / `BoardDetail.cshtml` (the project link), the to-do
  `Create.cshtml` / `Edit.cshtml` + `BoardNew.cshtml` / `BoardEdit.cshtml`
  (the project picker).
- **`Models/ProjectViewModels.cs`** (or the existing view-models file —
  U05 settles) — the `GoalCard` / `ProjectCard` / `ProjectsIndexViewModel`
  + goal / project editor model shapes (the card descriptions pre-rendered
  via `MarkdownRenderer`, **not** the WYSIWYG `rc-editor`).
- **`KnownTranslationKeys.cs`** — the `pl.*` key block × 4 languages (U05,
  the first view that needs the keys).
- **The test files** — `tests/Kumunita.Core.Tests/ProjectServiceTests.cs`
  (the Core pins appended — U02–U04, U09) +
  `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` (the Web pins
  appended — U05–U09).

## 7. Feedback loops

The **invariant table (§4) is the primary source**; the pinned test *names*
live in **§9.6**. The feedback shape the units implement:

- **The ~11 Core pins** (appended to
  `tests/Kumunita.Core.Tests/ProjectServiceTests.cs`, run over
  `PostgresFixture`) — the FACES exercised over the additive
  `IProjectService` goal / project / association / delete lanes (§9.6).
- **The ~10 Web pins** (appended to
  `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs`, NSubstitute, no
  Postgres) — the `ProjectsController`'s new actions over a substituted
  `IProjectService` (§9.6).
- **The three-test acceptance gate** (recorded by **U10**) — closed loop /
  handoff / part-vs-whole (§9.7, verbatim from the lane plan).
- **The handoff-note convention** — one `## U#` section per unit,
  **appended (never rewritten)** to
  `docs/plans-milestones/in-progress/pl/pl-handoff-notes.md` (the M5
  `projects-handoff-notes.md` header shape); the next unit reads only the
  previous unit's section + its own entry-read list.

**Runner note (AGENTS.md test-runner quirk, this machine):** the reliable
path is build then in-process execution — **not** `dotnet test` / VS Test
Explorer:

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

## 8. Risks

- **The `ProjectId` field on two existing documents** (C-PL·8) is the one
  additive-on-existing-doc risk in the lane: Marten delta-detects the new
  column at boot (ADR 0004 §B.1 — the M5 `StartAt` / `DueAt` additive
  precedent, ADR 0079), so there is no migration and no data change; the
  guard is that **no existing lane reads `ProjectId` as a gate** (C-PL·3) —
  the `ProjectServiceTests` feed pins (the `ProjectId` filter) prove the
  filter behavior, and the existing M5 pins prove nothing regressed.
- **The `IProjectService` surface growth** (C-PL·8) is the frozen-surface
  risk: the surface was frozen in U04 of M5 with the rule "an ADD beyond
  this list is a **new ADR**" — this ADR (0086) is that record, and the
  full-interface-first pattern (the M4 `IEventService` precedent: register
  the full additive surface in U01–U04 with stubs, implement per unit)
  keeps the Web coding against a *frozen* shape from U05 on.
- **The `kw-l` key count × 4 languages** (D11) is the localization-parity
  risk: the `KnownTranslationKeys.cs` registry is the closed set — a key
  registered in `en` but missing in `de` / `fr` / `da` fails the
  `KwLRegistryConsistencyTests` parity pin; the §9.6 list is the exact
  master list (a rename / renumber is a break).
- **The dangling-association reads** (C-PL·6) are the one new read-time
  rule: the goal link on a project detail and the project link on a to-do /
  board detail must both (a) resolve the target through the **frozen**
  `CanAsync(Read)` path and (b) drop the link when the target is
  soft-deleted or denied — a leak here would render a title the actor may
  not read (the F2 / F8 pair proves both directions).

---

# Seams & contracts (Part 2, written by U00)

> **Part 2 status.** **Locked** — ADR 0086 (Accepted, 2026-09-25) is the
> decisions source; this section is the **frozen reference tier** U01–U09
> code against. The two POCOs + two adapters (§9.1 / §9.2), the additive
> `IProjectService` seams (§9.3), the request DTOs (§9.4), the `M5DocTypes`
> additive shape (§9.5), the pinned test names (§9.6), the three-test
> acceptance gate (§9.7), and the drift-guard (§9.8) are **frozen pins** — a
> mismatch is a `## U<m> — Drift pause` per the lane plan's unit-series
> rule. U01–U09 code against these shapes **verbatim**.

## 9.1 The exact C# of the two new documents (verbatim)

```csharp
// Kumunita.Core.Projects — the two PL documents (ADR 0086 D2 / D3 — POCOs,
// conventional `string` `Id`, Marten-native; delta-detected + idempotent, no
// seeding, no EF; the surface is additive — ADR 0004 §B.1; the field
// provenance is the `TodoItem` / `KanbanBoard` shape, M5 §2.2).

public sealed class ProjectGoal
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;          // non-empty — the goal label + adapter `Name`
    public string? Description { get; set; }                   // optional Markdown — the ADR 0025 shape (the one MarkdownRenderer)

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2 — the Post.ComponentId shape)
    public string AuthorId { get; set; } = string.Empty;       // the standing owner (C-M5·6 — creator ∪ GlobalAdmin over the goal)

    public Authorization.Audience? Audience { get; set; }       // the exact post Audience (ADR 0001-B / 0036; `null` = public)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused (C-PL·6)
    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // **No** `IsDraft` (a goal is live on creation — the D8a precedent).
    // **No** dates (a goal is a direction, not a scheduled thing — D2).
    // **No** `ProjectId` (the association is *outward* — a project points at
    // its goal, not the reverse — D2).
}

public sealed class Project
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;          // non-empty — the project label + adapter `Name`
    public string? Description { get; set; }                   // optional Markdown — the ADR 0025 shape

    public string? GoalId { get; set; }                        // the optional goal; `null` = standalone (the `TodoItem.ParentId` shape in intent — the SOLE association mechanism)

    public string? Status { get; set; }                         // a STRING state label; `null` = none — NOT an enum (C-PL·4, the C-M5·4 pin carried over)
    public DateTimeOffset? StartAt { get; set; }                // optional start — `null` = no date (C-PL·5, the ADR 0079 shape)
    public DateTimeOffset? DueAt { get; set; }                  // optional due date — `null` = no date (C-PL·5)

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2)
    public string AuthorId { get; set; } = string.Empty;       // the standing owner (creator ∪ GlobalAdmin over the project — C-PL·2)

    public Authorization.Audience? Audience { get; set; }       // the exact post Audience (ADR 0001-B / 0036; `null` = public)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused (C-PL·6)
    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // **No** `IsDraft` (a project is live on creation — the D8a precedent).
    // **No** `ProjectId` (this IS the project — D3).
}
```

## 9.2 The two new adapters (exact C#, verbatim)

```csharp
// Kumunita.Core.Projects — the two PL adapters (the
// `TodoItemToAuditableResource` / `KanbanBoardToAuditableResource` 6-member
// shape verbatim; `sealed` keeps the surface closed — ADR 0006-D). The
// `Title` is non-empty by pin, so the `Name` projection is `Title`.

public sealed class ProjectGoalToAuditableResource : IAuditableResource
{
    public ProjectGoalToAuditableResource(ProjectGoal goal) => Goal = goal;

    public ProjectGoal Goal { get; }                            // the adapter does not own the goal
    public string Id => Goal.Id;
    public string Name => Goal.Title;
    public string? OwnerId => Goal.AuthorId;
    public Authorization.Audience? Audience => Goal.Audience;
    public string? ComponentId => Goal.ComponentId;
    public string TargetKind => "goal";                         // the EXACT string (C3 — the AccessAudit aggregate-row discriminator)
}

public sealed class ProjectToAuditableResource : IAuditableResource
{
    public ProjectToAuditableResource(Project project) => Project = project;

    public Project Project { get; }                             // the adapter does not own the project
    public string Id => Project.Id;
    public string Name => Project.Title;
    public string? OwnerId => Project.AuthorId;
    public Authorization.Audience? Audience => Project.Audience;
    public string? ComponentId => Project.ComponentId;
    public string TargetKind => "project";                      // the EXACT string (C3)
}
```

**No new `AccessAction`, no new `AccessVia`, no new `Decide()` branch**
(C-PL·1) — the adapters implement the frozen 6-member
`IAuditableResource` surface and nothing else.

## 9.3 The additive `IProjectService` seams (exact C#, verbatim)

Additive on the frozen M5 surface (ADR 0067 §2.3; the frozen-surface rule
honoured by ADR 0086 — this is the "new ADR" the rule names). Every existing
M5 seam keeps its **exact** signature **except** the two feed seams, which
gain one additive optional parameter each (noted below).

**Goal read lanes (U02):**

```csharp
/// The **goal list** (the feed): the candidates are <c>!IsDeleted</c>,
/// filtered by optional <paramref name="componentId"/> (a filter, never a
/// gate — C-M3·2); the survivors are <c>CanSeeAsync(Read)</c>-filtered
/// (C6 / C3) over the <see cref="ProjectGoalToAuditableResource"/>; ordered
/// by <c>Created</c> descending (the newest first — the post feed shape);
/// paged. The **aggregate** <c>AccessAudit</c> row (<c>TargetKind "goal"</c>,
/// <c>visibleCount</c> / <c>hiddenCount</c>) is the C-M3·3 shape.
Task<IReadOnlyList<ProjectGoal>> ListGoalsAsync(string? componentId, string actorId, int page, CancellationToken ct = default);

/// One goal; one <c>CanAsync(Read)</c>; <see cref="KeyNotFoundException"/>
/// (404) on absent, <see cref="UnauthorizedAccessException"/> (403) on
/// denied (the C3 404-vs-403 split). The goal's <c>GoalId</c>-linked
/// projects are **not** part of this seam (they are the
/// <see cref="ListProjectsAsync"/> feed with the <c>goalId</c> filter).
Task<ProjectGoal> GetGoalAsync(string goalId, string actorId, CancellationToken ct = default);
```

**Goal write lanes (U02):**

```csharp
/// Create a goal — the author becomes the standing owner (the
/// <c>AuthorId</c> branch); the goal is **live on creation** (no
/// <c>IsDraft</c> — the D8a precedent); the <c>AccessAudit</c> row
/// (<c>goal.create</c>, <c>TargetKind = "goal"</c>) is stored in the
/// caller's session (C3).
Task<ProjectGoal> CreateGoalAsync(string actorId, IReadOnlySet<string> actorRoles, CreateGoalRequest request, CancellationToken ct = default);

/// Edit a goal — **creator ∪ GlobalAdmin** (the ADR 0070 board-edit
/// precedent, enforced server-side per C-M5·6); a **full update** of
/// <c>Title</c> + <c>Description</c> (the ADR 0070 shape — the
/// <see cref="UpdateBoardAsync"/> shape: the edit page posts both; a blank
/// description clears it to <c>null</c>); <c>AuthorId</c> / <c>Created</c>
/// preserved untouched; <c>Modified</c> stamped on a real change; the
/// <c>AccessAudit</c> row (<c>goal.update</c>, <c>TargetKind = "goal"</c>)
/// is stored in the caller's session (C3). A missing goal is
/// <see cref="KeyNotFoundException"/> (404); a denied actor is <see
/// cref="UnauthorizedAccessException"/> (403); a blank <c>Title</c> is
/// <see cref="ArgumentException"/> (the write shape's 400).
Task<ProjectGoal> UpdateGoalAsync(string goalId, string actorId, IReadOnlySet<string> actorRoles, UpdateGoalRequest request, CancellationToken ct = default);
```

**Project read lanes (U03):**

```csharp
/// The **project list** (the feed): the candidates are <c>!IsDeleted</c>,
/// filtered by optional <paramref name="componentId"/> (a filter, never a
/// gate — C-M3·2) **and** optional <paramref name="goalId"/> (a filter,
/// never a gate — the <c>goalId == null</c> filter is the
/// **standalone-projects** feed, the <c>/projects</c> landing page's
/// projects section); the survivors are <c>CanSeeAsync(Read)</c>-filtered
/// (C6 / C3) over the <see cref="ProjectToAuditableResource"/>; ordered by
/// <c>Created</c> descending; paged. The **aggregate** <c>AccessAudit</c>
/// row (<c>TargetKind "project"</c>, <c>visibleCount</c> /
/// <c>hiddenCount</c>) is the C-M3·3 shape.
Task<IReadOnlyList<Project>> ListProjectsAsync(string? componentId, string? goalId, string actorId, int page, CancellationToken ct = default);

/// One project; one <c>CanAsync(Read)</c>; the 404-vs-403 split (C3).
Task<Project> GetProjectAsync(string projectId, string actorId, CancellationToken ct = default);
```

**Project write lanes (U03):**

```csharp
/// Create a project — the author becomes the standing owner; the project
/// is **live on creation** (no <c>IsDraft</c>); the **<c>GoalId</c>
/// guard**: a non-null <c>request.GoalId</c> pointing at a soft-deleted or
/// unreadable goal is **refused** (<see cref="KeyNotFoundException"/> 404
/// on absent, <see cref="UnauthorizedAccessException"/> 403 on denied — the
/// C3 split); the <c>AccessAudit</c> row (<c>project.create</c>,
/// <c>TargetKind = "project"</c>) is stored in the caller's session (C3).
Task<Project> CreateProjectAsync(string actorId, IReadOnlySet<string> actorRoles, CreateProjectRequest request, CancellationToken ct = default);

/// Edit a project — **creator ∪ GlobalAdmin** (the ADR 0070 precedent,
/// enforced server-side per C-M5·6); a **partial update** of
/// <c>Title</c> / <c>Description</c> / <c>GoalId</c> / <c>Status</c> /
/// <c>StartAt</c> / <c>DueAt</c> (the ADR 0079 optional-date shape — a
/// non-null value is applied, a <c>null</c> clears it, the edit form's
/// blank <c>datetime-local</c> field → <c>null</c>); the **<c>GoalId</c>
/// guard** on re-association: a non-null <c>request.GoalId</c> pointing at a
/// soft-deleted or unreadable goal is **refused** (the C3 split);
/// <c>ClearGoal = true</c> is an explicit un-goal (sets
/// <c>GoalId = null</c>); <c>AuthorId</c> / <c>Created</c> preserved
/// untouched; <c>Modified</c> stamped on a real change; the
/// <c>AccessAudit</c> row (<c>project.update</c>, <c>TargetKind =
/// "project"</c>) is stored in the caller's session (C3).
Task<Project> UpdateProjectAsync(string projectId, string actorId, IReadOnlySet<string> actorRoles, UpdateProjectRequest request, CancellationToken ct = default);
```

**Association lanes (U04):**

```csharp
// + the **existing** feed seams gain one additive optional parameter each
//   (appended, default `null` — every existing call site + the existing M5
//   Core tests compile and behave UNCHANGED — the C-PL·8 additive pin):
//
//   ListTodosAsync(string? componentId, string? assigneeId, string actorId,
//                  int page, bool unassignedOnly, string? projectId = null,
//                  CancellationToken ct = default);
//   ListBoardsAsync(string? componentId, string actorId, int page,
//                   string? projectId = null, CancellationToken ct = default);
//
//   When `projectId` is non-null, the candidate set is narrowed to the
//   to-dos / boards whose `ProjectId == projectId`; when `null` (the
//   default), no filter. A **feed filter, never a gate** (C-M3·2 / C-PL·3).

/// Sets <c>TodoItem.ProjectId</c> to <paramref name="projectId"/>
/// (<c>null</c> = unassociate). **Creator ∪ assignee ∪ GlobalAdmin** over
/// the **to-do** (the C-M5·6 standing matrix, re-checked server-side). A
/// non-null <c>projectId</c> pointing at a soft-deleted or unreadable
/// project is **refused** (the C3 split). <c>AuthorId</c> / <c>Created</c>
/// preserved untouched; <c>Modified</c> stamped; the <c>AccessAudit</c> row
/// (<c>todo.set_project</c>, <c>TargetKind = "todo"</c>) is stored in the
/// caller's session (C3).
Task<TodoItem> SetTodoProjectAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default);

/// Sets <c>KanbanBoard.ProjectId</c> to <paramref name="projectId"/>
/// (<c>null</c> = unassociate). **Creator ∪ GlobalAdmin** over the
/// **board** (the ADR 0070 precedent, re-checked server-side). A non-null
/// <c>projectId</c> pointing at a soft-deleted or unreadable project is
/// **refused** (the C3 split). <c>AuthorId</c> / <c>Created</c> preserved
/// untouched; <c>Modified</c> stamped; the <c>AccessAudit</c> row
/// (<c>board.set_project</c>, <c>TargetKind = "board"</c>) is stored in the
/// caller's session (C3).
Task<KanbanBoard> SetBoardProjectAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, string? projectId, CancellationToken ct = default);
```

**Delete lanes (U09):**

```csharp
/// Sets <c>ProjectGoal.IsDeleted = true</c> (the ADR 0024 shape); the
/// <c>Project.GoalId</c> rows are **kept** (the dangling-association rule
/// — D6 / C-PL·6); **creator ∪ GlobalAdmin** (re-checked server-side); the
/// <c>AccessAudit</c> row (<c>goal.delete</c>, <c>TargetKind = "goal"</c>)
/// is stored in the caller's session (C3).
Task DeleteGoalAsync(string goalId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

/// Sets <c>Project.IsDeleted = true</c> (the ADR 0024 shape); the
/// <c>TodoItem.ProjectId</c> / <c>KanbanBoard.ProjectId</c> rows are
/// **kept** (the dangling-association rule — D6 / C-PL·6); **creator ∪
/// GlobalAdmin** (re-checked server-side); the <c>AccessAudit</c> row
/// (<c>project.delete</c>, <c>TargetKind = "project"</c>) is stored in the
/// caller's session (C3).
Task DeleteProjectAsync(string projectId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);
```

## 9.4 The request DTOs (exact C#, verbatim)

```csharp
// Kumunita.Core.Projects — the four PL request DTOs (the M5
// `CreateTodoRequest` / `UpdateBoardRequest` shape, ADR 0067 / 0070).

public sealed record CreateGoalRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }                   // blank → `null` (the create-path normalization)
    public string? ComponentId { get; init; }                   // a feed filter, never a gate (C-M3·2)
    public Authorization.Audience? Audience { get; init; }      // `null` = public (the ADR 0001-B / 0036 shape)
    public string? LanguageCode { get; init; }                  // `null` → the ADR 0018 resolver's effective language
}

public sealed record UpdateGoalRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }                   // blank → `null` (the ADR 0070 full-update shape)
    // `Audience` / `ComponentId` / `LanguageCode` are creation-time choices
    // — NOT editable here (the ADR 0070 board-edit precedent).
}

public sealed record CreateProjectRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }                   // blank → `null`
    public string? GoalId { get; init; }                        // the optional goal — `null` = standalone (the C3 GoalId guard applies when non-null)
    public string? Status { get; init; }                        // a string, not an enum (C-PL·4)
    public DateTimeOffset? StartAt { get; init; }               // `null` = no date (C-PL·5, the ADR 0079 shape)
    public DateTimeOffset? DueAt { get; init; }                 // `null` = no date (C-PL·5)
    public string? ComponentId { get; init; }                   // a feed filter, never a gate (C-M3·2)
    public Authorization.Audience? Audience { get; init; }      // `null` = public
    public string? LanguageCode { get; init; }                  // `null` → the ADR 0018 resolver's effective language
}

public sealed record UpdateProjectRequest
{
    public string? Title { get; init; }                         // `null` = unchanged (the partial-update shape)
    public string? Description { get; init; }                   // blank → `null` (the ADR 0070 shape)
    public string? GoalId { get; init; }                        // a non-null value RE-ASSOCIATES the project to that goal (the C3 GoalId guard applies)
    public bool ClearGoal { get; init; }                        // `true` = explicit un-goal, sets `GoalId = null`
    public string? Status { get; init; }                        // `null` = clear (a string, not an enum — C-PL·4)
    public DateTimeOffset? StartAt { get; init; }               // ADR 0079 — non-null applied, `null` clears (C-PL·5)
    public DateTimeOffset? DueAt { get; init; }                 // ADR 0079 — non-null applied, `null` clears (C-PL·5)
    // `Audience` / `ComponentId` / `LanguageCode` are creation-time choices
    // — NOT editable here (the ADR 0070 board-edit precedent).
}
```

## 9.5 The `M5DocTypes` additive shape (exact C#)

Additive on the existing `M5DocTypes.Configure` (the four M5 registrations
stay byte-for-byte; the two new doc blocks + the two `ProjectId` indexes are
appended — the unnamed-computed-index constraint noted in the existing file
applies to the new `(ComponentId, Created)` indexes too):

```csharp
// Kumunita.Core/M5DocTypes.cs — the two additive PL registrations (ADR 0086 D1 / D2 / D3 / D4).

// ProjectGoal — conventional string Id (Marten's default); the
// (ComponentId, Created) **feed-ordering** index (the ListGoalsAsync feed
// orders survivors by `Created` descending — the same shape as the
// existing `TodoItem` / `KanbanBoard` feed indexes).
opts.Schema.For<ProjectGoal>()
       .Index(g => new { g.ComponentId, g.Created });

// Project — conventional string Id; the (ComponentId, Created)
// **feed-ordering** index (the ListProjectsAsync feed shape); the
// `GoalId` index (the "projects in this goal" read — the
// ListProjectsAsync `goalId` filter lookup).
opts.Schema.For<Project>()
       .Index(p => new { p.ComponentId, p.Created })
       .Index(p => p.GoalId);

// + one additive index on each of the two EXISTING documents — the
// `ProjectId` feed-filter lookup (the U04 `projectId` filter; a feed filter,
// never a gate — C-PL·3):
opts.Schema.For<TodoItem>().Index(t => t.ProjectId);
opts.Schema.For<KanbanBoard>().Index(b => b.ProjectId);
```

**No unique indexes** (a goal may be associated with many projects; a
project may be associated with many to-dos / boards; a project has **at
most one** goal — the `GoalId` is a **single** field, not a list — so no
business-key unique index is needed on any of the three). **Zero migration
for existing rows** (ADR 0004 §B.1 delta-detect; the C-PL·8 additive pin).

## 9.6 The pinned test names (the ~20 shape)

**Core pins** — appended to `tests/Kumunita.Core.Tests/ProjectServiceTests.cs`
(run over `PostgresFixture`), the FACES F1–F10 exercised over the additive
`IProjectService` goal / project / association / delete lanes. A rename or
re-scope of a name after this freeze is a `## U<m> — Drift pause`:

1. `F1_GoalVisibleToAudienceMember_HiddenFromNonMember` (the goal feed's
   `CanSeeAsync(Read)` filter — F1)
2. `F2_GoalDetail_404OnAbsent_403OnDenied` (the C3 split — F2)
3. `F3_CreateGoal_AuthorIsStandingOwner_Audited` (the `goal.create`
   `AccessAudit` row, `TargetKind = "goal"` — F1 / C3)
4. `F3_UpdateGoal_CreatorGlobalAdmin_StandingRechecked` (the C-PL·2
   standing re-check + `goal.update` row — F1 / C-PL·2)
5. `F5_ProjectFeed_GoalIdFilter_StandaloneVsUnderGoal` (the
   `ListProjectsAsync` `goalId` filter — standalone vs under-goal — F5 /
   C-PL·8)
6. `F2_ProjectDetail_404OnAbsent_403OnDenied` (the C3 split — F2)
7. `F5_CreateProject_GoalIdGuard_RefusesDeletedOrUnreadableGoal` (the
   `GoalId` guard, the C3 split — F5)
8. `F4_UpdateProject_ClearGoal_NullsGoalId` (the `ClearGoal` explicit
   un-goal + the partial-update date semantics — F4 / F5)
9. `F7_SetTodoProject_StandingRechecked_RefusesDeletedProject` (the to-do
   standing matrix + the C3 project guard — F7)
10. `F7_SetBoardProject_StandingRechecked_RefusesDeletedProject` (the
    board standing matrix + the C3 project guard — F7)
11. `F8_DeleteGoal_SoftDeletes_ProjectsKept_GoalLinkDangles` +
    `F8_DeleteProject_SoftDeletes_TodosAndBoardsKept_ProjectLinkDangles`
    (the dangling-association rule, the pair — F8 / C-PL·6)

**Web pins** — appended to
`tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` (NSubstitute, no
Postgres) — the `ProjectsController`'s new actions over a substituted
`IProjectService`:

1. `ProjectsIndex_GoalsPlusStandaloneProjects_Render` (the `/projects`
   landing's goals + standalone-projects feed — F9 / C-PL·7)
2. `GoalDetail_ProjectsInThisGoal_ListRendered` (the goal detail's
   projects-in-this-goal list — F5)
3. `ProjectDetail_AssociatedTodosAndBoards_ListRendered` (the project
   detail's associated to-dos / boards list — F6 / C-PL·3)
4. `ProjectsTabs_ProjectsTab_ActiveOnIndex` (the **Projects** nav tab
   active on `/projects` — F9)
5. `TodoEdit_ProjectPickerSeeded_ReadableNonDeleted` (the to-do edit form's
   project picker — F10 / C-PL·3)
6. `TodoDetail_ProjectLink_RendersWhenReadable` (the to-do detail's project
   link — F6 / F8)
7. `BoardEdit_ProjectPickerSeeded_ReadableNonDeleted` (the board edit
   form's project picker — F10 / C-PL·3)
8. `BoardDetail_ProjectLink_RendersWhenReadable` (the board detail's
   project link — F6 / F8)
9. `GoalDelete_SoftDeletes_DanglingProjectKept` (the goal delete lane + the
   dangling-association rule — F8 / C-PL·6)
10. `ProjectDelete_SoftDeletes_AssociatedTodosBoardsKept` (the project
    delete lane + the dangling-association rule — F8 / C-PL·6)

## 9.7 The three-test gate (U10 records)

The three-test gate, **verbatim** from the lane plan
(`plan-pl-goals-projects.md`, §The three-test gate):

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
   (creator ∪ GlobalAdmin) is **re-checked server-side** in every write
   lane (C-M5·6); the `AccessAudit` rows (`TargetKind = "goal"` /
   `"project"`) are stored in the caller's session (C3).
3. **Part-vs-whole:** a goal / project's soft-delete (U09) **never
   deletes** the to-dos / boards associated with it (the
   dangling-association rule — D6); a to-do / board's `ProjectId` (U04 /
   U08) is a **feed filter, never a gate** (C-M3·2) — the to-do / board's
   own `Audience` decision is the access boundary (C-M5·3); the
   `/projects/todos` + `/projects/boards` routes keep their **exact** M5
   paths (the route surface is untouched).

**Runner note (AGENTS.md test-runner quirk, this machine):** the reliable
path is build then in-process execution — **not** `dotnet test` / VS Test
Explorer:

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

## 9.8 The drift-guard (the lane plan's §Drift-guard, verbatim)

The following are **frozen pins**; any mismatch with the implementation is a
`## U<m> — Drift pause` (the lane plan's unit-series rule):

- **No new `AccessAction`**, **no new `AccessVia`**, **no new authorization
  branch** — `PL` adds two *adapters* (`ProjectGoalToAuditableResource`,
  `ProjectToAuditableResource`), not a *branch* (the C-M5·11 precedent).
- **The standing matrix is creator ∪ GlobalAdmin** (the ADR 0070 board-edit
  precedent; **no** assignee branch — a goal / project is not assignable the
  way a to-do is).
- **The `ProjectId` on `TodoItem` / `KanbanBoard` is a feed filter, never a
  gate** (C-M3·2) — the to-do / board's own `Audience` decision is the
  access boundary (C-M5·3).
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
  `MilestonesTests.cs` are **untouched** — a named lane, not a milestone,
  the ADR 0013 / 0015 precedent).

Plus this file's own frozen shapes: **the two POCOs** (§9.1) — the exact
field sets; a re-shape (renaming a field, adding a gate field, a `ProjectId`
on `ProjectGoal`, an `IsDraft`, a `Status` enum) is a **new ADR**, not a
silent addition. **The two adapters** (§9.2) — the 6-member projection,
`TargetKind = "goal"` / `"project"` (the **exact** strings). **The
additive `IProjectService` seams** (§9.3) — the signatures verbatim; an ADD
beyond this list is a **new ADR**; a re-scope of one is a drift event.
**The four request DTOs** (§9.4) — the exact field sets. **The
`M5DocTypes` additive shape** (§9.5) — the two new docs + the four new
indexes, no unique indexes. **The ~20 pinned test names** (§9.6) — the
master list; a rename / renumber is a break. **The three-test gate** (§9.7)
— closed loop / handoff / part-vs-whole.

---

*Part 1 (U00) — context, scope, the locked decisions D1–D12, the 8
invariants C-PL·1–8, and the 10 FACES F1–F10. **No code, no build, no
tests.** Part 2 (U00) — the exact C# shapes, the additive `IProjectService`
seams, the request DTOs, the `M5DocTypes` additive shape, the pinned test
names, the three-test gate, and the drift-guard, locked by **ADR 0086
(Accepted, 2026-09-25)**.*
