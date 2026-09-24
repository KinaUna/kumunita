# ADR 0067 — M5: Projects (to-dos + Kanban boards) — a new `Kumunita.Core.Projects` context reusing the frozen authorization / audience / renderer / editor / standing seams

Status: Accepted
Date: 2026-09-23
Amends: **0001-B** (the `Audience` doc — *reused*, not extended),
**0006** (the frozen `IAuthorizationService` / `IAuditableResource` surface
— the two new adapters plug into it, no signature change), **0036** (the
`Audience.Community` branch — the composer seeds it `true` by default,
verbatim the post convention), **0025** (the `MarkdownRenderer` +
content-image idiom — the to-do `Body` is rendered by the *one* renderer),
**0031** (the `bindRichEditor` + the tsc-only / no-dependency pin — the to-do
composer is the *one* editor; the board TS is plain-`client/lib` tsc-only),
**0037** (the `IsDraft` flag — **not carried onto M5**; a to-do is published
on creation; the draft lane is a follow-on ADR), **0024** (the `IsDeleted`
author soft-delete flag — carried onto `TodoItem.IsDeleted` +
`KanbanBoard.IsDeleted`), **0018** (the authored-in language tag — carried
onto `TodoItem.LanguageCode` + `KanbanBoard.LanguageCode`), **0044** (the
`TagIds` idiom — carried onto `TodoItem.TagIds`), **0019 / 0020** (the
`kw-dt` TagHelper — every M5 timestamp renders through it), and
**0014 / 0016 / 0017** (the author-only / author-of-record ∪ GlobalAdmin
standing precedent — the M5 standing matrix extends it with the **assignee**
as a collaborator), and **0013** (the group-post membership-lane precedent —
the M5 lane is **not** a membership lane; the standing matrix is the
**creator ∪ assignee ∪ GlobalAdmin** matrix, not a membership decision).

## Context

The roadmap arrows so far: **awareness** (M2 directory / groups), **signal**
(M3 posts / announcements / group posts / moderation), **coordination** (M4
events, RSVPs, reminders). M5 is the **outcome** arrow — a decision becomes
*owned work with a shape*: a resident writes a to-do, assigns it to a
neighbor, breaks it into subtasks, and arranges the neighborhood's shared
work on a Kanban board with lanes like "Planned / Doing / Done". It is the
platform's first **project-management** surface — assignable, hierarchical
**to-dos** and **Kanban boards** with statused, limitable lanes.

**Roadmap order (recorded here, per the sign-off gate):** M5 is **already**
the single `StatusNext` on the roadmap (M4 is shipped — Events, ADR 0054,
and the EV-CAL / EV-DWM / EV-MINE named lanes are `StatusDone`); M6 stays
`StatusPlanned`. **No roadmap letter moves in M5's open** — U01 confirms the
trio (M5 `StatusNext`, M6 `StatusPlanned`), and the **M5 close unit (U13)**
is the only roadmap move: M5 `StatusNext` → `StatusDone`, M6
`StatusPlanned` → `StatusNext` (in `Milestones.cs` + the README Roadmap +
`MilestonesTests.cs`).

**The canonical-names decision (recorded here, the M4 field-naming-decision
precedent):** `docs/ARCHITECTURE.md` §5 sketches the context as
`Project { id, title, description, componentId?, ownerId, status, audience,
created }`, `ProjectTask { id, projectId, title, assigneeId?, done, order }`,
`ProjectMember { id, projectId, userId, role }`. This ADR settles the
canonical names as **`TodoItem`** (the to-do / work item), **`KanbanBoard`**
(the board), **`KanbanLane`** (a board's column), and
**`BoardItemPlacement`** (a to-do's presence on a board / lane). **`Project`
/ `ProjectTask` / `ProjectMember` do not exist** — the §5 sketch is the
pre-decision shape; "project" is not a distinct document in M5 (a to-do is
the unit, a board is the container). **The M5 close unit (U13) rewrites the
ARCHITECTURE.md §5 `Projects` block + the §3 tree's `Projects/` line to these
names** (per the AGENTS.md doc↔code parity rule), so no unit in U02–U12
touches that file.

**The one constraint:** the milestone is **additive and reusing.** The
`Audience` doc (ADR 0001-B / 0036), the
`IAuthorizationService.CanAsync(Read)` / `CanSeeAsync(Read)` decision path
(ADR 0006), the standing matrix (ADR 0014 / 0016 / 0017), the
`MarkdownRenderer` + `bindRichEditor` (ADR 0025 / 0031), the `kw-dt`
TagHelper (ADR 0019 / 0020), and the profile-avatar surface are all
**frozen seams** — M5 adds **adapters + a service + views + one TS module**,
not a branch. **No new `AccessAction`**, **no new `AccessVia`**, **no new
authorization path**, **no editor dependency**, **zero migrations for
existing surfaces** (ADR 0004 §B.1 — the four docs are new, the surface is
additive).

## Decision

- **A new bounded context `Kumunita.Core.Projects`** with four documents:
  - **`TodoItem`** — `Id`; `Title` (non-empty — the card label + adapter
    `Name`); **`Body?`** (optional Markdown — the one `MarkdownRenderer`,
    ADR 0025; a to-do is usable with title only); `ComponentId?` (a feed
    filter, *never* a gate — C-M3·2); **`AuthorId`** (the standing owner);
    **`AssigneeId?`** (a `SubjectId` — display + standing, **never** an
    access gate); **`Status?`** (a **nullable string** — the state label;
    `null` = none; **no** status enum / registry / seeded vocabulary);
    **`ParentId?`** (the **sole** hierarchy mechanism — a subtask is a full
    to-do with its own status / assignee / placements); **`Audience?`** (the
    **exact** post `Audience`, ADR 0001-B / 0036 — `null` = public, the
    frozen `Decide()` branch 5); `IsDeleted` (bool, `false` default — ADR
    0024); `LanguageCode` (ADR 0018); `TagIds` (ADR 0044); `ImageIds`
    (ADR 0025); `AttachmentIds` (ADR 0034); `Created` / `Modified?`.
    **No** `BoardId` / `LaneId` / `Order` (a placement is a separate
    `BoardItemPlacement` record) and **no** `IsDraft` (a to-do is published
    on creation — the ADR 0037 draft lane is a follow-on ADR).
  - **`KanbanBoard`** — `Id`; `Title` (non-empty); **`Description?`**
    (optional Markdown — the ADR 0025 shape); `ComponentId?` (a feed
    filter, never a gate — C-M3·2); **`AuthorId`** (the standing owner);
    **`Audience?`** (the **exact** post `Audience`, ADR 0001-B / 0036 —
    `null` = public); `IsDeleted` (bool, `false` default — ADR 0024);
    `LanguageCode` (ADR 0018); `Created` / `Modified?`. **No** `IsDraft`
    (a board is live on creation).
  - **`KanbanLane`** — `Id`; `BoardId` (the parent board); `Title`
    (non-empty — the lane label); **`Status?`** (a nullable string — the
    status a lane **imparts** on a to-do moved into it; `null` = imparts
    none); **`MaxItems?`** (a nullable int — the lane's capacity; `null` =
    no limit; **advisory**, refused not dropped); `Order` (an int — the
    lane's position within the board, 0-based, the column order);
    `Created` / `Modified?`. **No** `Audience` (a lane's visibility is the
    board's).
  - **`BoardItemPlacement`** — `Id`; `TodoItemId`; `BoardId`; `LaneId`;
    `Order` (an int — the to-do's position within the lane, 0-based, the
    card order); `Created` / `Modified?`. **No** `Audience` — the
    placement's visibility is the to-do's **and** the board's; it is **not**
    itself an auditable resource (no
    `BoardItemPlacementToAuditableResource`).
  - **`M5DocTypes`** — a new document surface (the `M4DocTypes` pattern
    verbatim): `TodoItem` (conventional `Id` + a `(ComponentId, Created)`
    feed-ordering index + a `ParentId` index); `KanbanBoard` (conventional
    `Id` + a `(ComponentId, Created)` feed-ordering index); `KanbanLane`
    (conventional `Id` + a **unique** `(BoardId, Order)` index — the
    business-key convention, the `EventRsvp` shape); `BoardItemPlacement`
    (conventional `Id` + a **unique** `(BoardId, LaneId, Order)` index + a
    **unique** `(TodoItemId, BoardId)` index — a to-do appears on a board at
    most once). **Zero migrations for existing surfaces** — the four docs
    are new, the surface is additive (ADR 0004 §B.1).
- **The audience is the post `Audience`, reused verbatim.** **No new
  `Audience` field, no new `AudienceMode`, no new `AccessVia` value.** The
  frozen `Decide()` algorithm already covers every standing a to-do / board
  needs. The composer seeds it **community-visible by default** (ADR 0036)
  via the `AudienceEditorModel` form surface verbatim — the same partial the
  post / announcement / event composers already use.
- **The decision path is the frozen `IAuthorizationService`** through two
  new adapters — `TodoItemToAuditableResource` (`TargetKind = "todo"`) and
  `KanbanBoardToAuditableResource` (`TargetKind = "board"`), the
  `PostToAuditableResource` / `EventToAuditableResource` 6-member shapes
  verbatim: `Id` = the document id; `Name` = the `Title` (non-empty by
  pin); `OwnerId` = `AuthorId`; `Audience` = the document's `Audience`
  (null allowed); `ComponentId` = the document's `ComponentId`;
  `TargetKind` = the **exact** string (the `AccessAudit` aggregate-row
  discriminator). **No new `AccessAction`** (the existing `Read` is enough),
  **no new authorization branch**, **no new `AccessVia`** — the adapters are
  the **only** new authorization surface in this milestone. A **standalone**
  to-do list / detail is gated by the to-do's own `Audience` decision
  (`CanSeeAsync(Read)` aggregate over the list — the C-M3·3 shape;
  `CanAsync(Read)` on the detail — the C3 404-vs-403 split). A **board**
  view is gated by the board's `Audience` first (entry), then **each card**
  by the to-do's own `Audience` — a to-do on the board is visible iff
  **both** the board and the to-do are visible (a denied card is not
  returned).
- **The standing matrix** (enforced server-side in the `ProjectService` —
  the `AnnouncementService.CreateAsync` C3 pattern; the Web `[Authorize]`
  is a convenience pre-gate only, never the source of truth):

  | Action | Standing | `AccessVia` |
  |---|---|---|
  | **Create** (to-do / board) | any signed-in resident (becomes the author) | `Owner` |
  | **To-do mutations** (edit / assign / add-subtask / delete / reorder / move / copy) | **creator ∪ assignee ∪ GlobalAdmin** | `Owner` / `Admin` |
  | **Board / lane mutations** (lane CRUD, board delete) | **creator ∪ GlobalAdmin** (the lane is not its own standing surface) | `Owner` / `Admin` |

  `CheckCreateStanding` / `CheckEditStanding`-shaped helpers throw
  `UnauthorizedAccessException` (403) / `KeyNotFoundException` (404)
  exactly like `AnnouncementService`. The `actorRoles` parameter carries the
  principal's real role set (the Web layer passes `RoleSet(User)`); Core
  enforces the matrix. **`AssigneeId` is display + standing, never an
  access gate** — read is the `Audience` decision. Every audited write lane
  stores its `AccessAudit` row in the caller's session (C3),
  `TargetKind = "todo"` / `"board"`, `Action` `todo.create` / `todo.update`
  / `todo.assign` / `todo.add_subtask` / `todo.delete` /
  `todo.move_within_lane` / `todo.move_to_lane` / `todo.move_to_board` /
  `todo.copy_to_board` / `board.create` / `board.update_lane` /
  `board.delete`.
- **The `IProjectService` seam** (the design doc §2.3 is the authoritative
  pin — the full read + write + placement surface, verbatim): read lanes
  (`ListTodosAsync` / `GetTodoAsync` + subtasks / `ListBoardsAsync` /
  `GetBoardAsync` + lanes + per-lane cards); write lanes (`CreateTodoAsync`
  / `UpdateTodoAsync` / `AssignTodoAsync` / `AddSubtaskAsync` /
  `DeleteTodoAsync` / `CreateBoardAsync` / `UpdateLaneAsync` /
  `DeleteBoardAsync`); placement + reorder lanes
  (`MoveTodoWithinLaneAsync` / `MoveTodoToAdjacentLaneAsync` /
  `MoveTodoToBoardAsync` / `CopyTodoToBoardAsync`). **Copy-to duplicates**
  (a new `TodoItem` — the copy's `AuthorId` is the actor — + a placement on
  the target board's first lane; the original is untouched); **move-to
  relocates** (the placement is removed from the source board and added to
  the target board's first lane). **No translation lane in M5's own scope**
  — a to-do / board is authored-in-language; user-added translations for
  M5 documents is a follow-on lane with its own ADR (the ADR 0022 / 0029 /
  0048 / 0059 shape), so it is additive to this ADR, not part of it.
- **The board is the state machine the to-do flows through:**
  - **Lane-status auto-update** — moving a to-do into a lane whose `Status`
    is non-null sets `TodoItem.Status` to that lane's status **in the same
    transaction** as the placement move (audited — C3); a lane with null
    `Status` leaves the to-do's status unchanged. The status icon is a
    **client-side display** mapping, never persisted.
  - **Lane limits are advisory backstops, refused not dropped** — a move /
    copy **into** a lane at its `MaxItems` limit is **refused** (an
    `InvalidOperationException` with the lane's `Title`; **nothing is
    written**); a reorder within a lane and a move *out* of a lane never
    trip the source limit.
  - **Hierarchy is `ParentId` only** — a subtask is a full to-do;
    **delete cascades** to the descendant subtree; a `ParentId` that would
    create a cycle is refused (the server-side hierarchy cycle guard).
- **The Web surface** — `ProjectsController` (`Kumunita.Web.Controllers`):
  the to-do routes (`/projects/todos` feed — `CanSeeAsync(Read)`-filtered so
  a restricted to-do's *existence* does not leak; `/projects/todos/new`
  compose — title, the optional body in the **one** `bindRichEditor`, the
  `AudienceEditorModel` verbatim, the language picker, the assignee picker;
  `/projects/todos/{id}` detail — title + rendered body, the subtasks under
  the parent, 404 on absent / 403 on denied) + the to-do action endpoints
  (edit / assign / add-subtask / delete); the board routes
  (`/projects/boards` feed; `/projects/boards/new` compose — the lane list;
  `/projects/boards/{id}` detail — lanes as columns, cards with the status
  icon + assignee avatar + title + the dropdown menu) + the lane CRUD +
  placement endpoints (reorder / move left-right / move-to / copy-to). The
  **nav entry** is one line (the `Community` nav pattern). The
  `Post` / `Announcement` / `Page` / `Event` surfaces stay **untouched**.
- **The board module is plain `client/lib` TS, tsc-only, zero
  dependencies** — `client/lib/projects-board.ts` (the one new module):
  every action (reorder / move / copy / assign / delete) is an **explicit
  POST**, CSRF-aware (the `client/lib/api.ts` shape), then a **full
  re-render** — **no** optimistic local reordering, **no** HTML5 drag
  events (keyboard / menu driven — the ADR 0031 "explicit,
  server-authoritative" posture); the status icon + assignee avatar +
  dropdown menu are plain DOM / JS.
- **The draft / language / tag / media / delete lanes are reused, not
  reinvented:** the `LanguageCode` (ADR 0018); the `TagIds` (ADR 0044);
  the `ImageIds` + `AttachmentIds` (ADR 0025 / 0034 — the server-side body
  parse populates them; the client never sends them); the `IsDeleted` flag
  + read-lane filter (ADR 0024). **The ADR 0037 draft lane is NOT carried
  onto M5** — a to-do / board is live on creation; the draft lane for M5
  documents is a follow-on ADR.
- **The localization parity** — the new `projects.*` `kw-l` key block
  (design doc §2.6 — 19 keys: the to-do / board / lane nouns, the
  dropdown-action labels — move up / down / left / right, copy to, move to,
  the lane limit / status labels, the board empty-state) is present in
  **all four** seeded languages (en/de/fr/da — the ADR 0042 / 0045 set);
  English is the fallback in every view; the ADR 0015 key-registry shape is
  the host (the ADR 0052 warm-boot baseline backfill lane covers the
  `de`/`fr`/`da` rows).
- **The 24 pinned seam test names** (the design doc §2.7 / §2.8 is the
  master list, locked here) — 16 in
  `tests/Kumunita.Core.Tests/ProjectServiceTests.cs` (F1–F10 over the
  `IProjectService` read / write / placement lanes, run over
  `PostgresFixture`) + 8 appended to
  `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` (NSubstitute, no
  Postgres).
- **The three-test acceptance gate** (the design doc §2.9, recorded by the
  gate unit U13): **closed loop** (an author creates a to-do → it appears
  in their to-do list and, when placed, on the board with the right card +
  one aggregate row), **handoff** (a to-do assigned to a second resident —
  the assignee sees it on the board and has standing to edit it on the next
  render — the non-assignee's view / standing still exclude it),
  **part-vs-whole** (the 24 names are the whole; all pass together in the
  same run as the inherited M1–M4 anchors + the EV lanes — the lane is
  additive, nothing regressed).
- **The drift-guard** (the design doc §2.10): the four POCOs' field sets
  (including the no-`BoardId`/`LaneId`/`Order` / no-`IsDraft` pins and the
  string-status / nullable-`MaxItems` shape), the two adapters
  (`TargetKind = "todo"` / `"board"` — the **exact** strings), the
  `IProjectService` full surface (the read + write + placement signatures
  verbatim — frozen in U04), the DTOs, the `M5DocTypes` surface, the
  standing matrix, the `kw-l` keys, the 16 Core + 8 Web test names, and the
  three-test gate — all frozen pins; any mismatch is a `## U<m> — Drift
  pause` per the lane's unit-series rule §9.
