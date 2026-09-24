# ADR 0068 — Lane action menu: rename / limit / status / move + add-to-do foot

Status: Accepted
Date: 2026-09-23
Amends: 0067

## Context

ADR 0067 shipped the M5 board: a `KanbanBoard` with its `KanbanLane` columns,
a `TodoItem` (the movable card), and a `BoardItemPlacement` binding a to-do to a
lane at an `Order`. The board detail page
(`Views/Projects/BoardDetail.cshtml`) rendered each lane's edit form — the
`Title`, `Status`, and `MaxItems` inputs **plus** its Save button — always
visible in the lane head.

Three gaps:

- **No per-lane action menu.** Every lane permanently displayed its three
  inputs and a Save button, even when nothing was being edited. The board head
  already had a ⋮ dropdown ("Delete board"); a lane had no equivalent.
- **The three inputs were unlabeled** (no `<label>` / `for=` pairing), and the
  save button was not tied to any of them specifically.
- **No way to move a lane, and no way to add a to-do directly onto a lane.**
  A to-do could only be created on the to-do list and then dragged onto a
  lane; a lane's column position could only be changed by editing its raw
  `Order` value by hand.

Two already-accepted decisions constrain this lane:

- **ADR 0067 §2.3 freezes the `IProjectService` surface.** "An ADD beyond this
  list is a **new ADR**." So the two new seams this decision introduces —
  `MoveLaneAsync` and `AddTodoToLaneAsync` — are exactly what this ADR records.
- **Board/lane standing (C-M5·6)** is `creator ∪ GlobalAdmin` over the
  **board** (the `CheckBoardStanding` shape). A lane is not itself an auditable
  resource (C-M5·3), so both new seams resolve standing against the board and
  audit against the board's id.

## Decision

- **The lane head now carries a ⋮ "Actions" dropdown** (Bootstrap 5, the same
  markup shape as the board-head "Delete board" menu) with five items:
  **Rename**, **Set limit**, **Set status**, **Move left**, **Move right**.
  The three always-visible inputs and the standalone Save button are **removed**
  — the lane head is clean by default and each edit is a small, self-contained
  form inside the menu.

- **Rename / Set limit / Set status reuse the existing lane-update seam**
  (`ProjectService.UpdateLaneAsync`, the ADR 0067 `board.update_lane` lane). No
  new service or controller method for these three: each menu item is a form
  that POSTs to the existing
  `POST /projects/boards/{id}/lanes/{laneId}` route. **Set limit** and
  **Set status** submit a single named field (`MaxItems` / `Status`) and —
  because `LaneEditorModel.Title` is `[Required]` and `UpdateLaneAsync` rejects
  a blank title — a **hidden `<input name="Title">`** carrying the lane's
  current title, so the partial update goes through the existing change-
  detection path unchanged. **Rename** submits `Title` (labeled). Each input
  now has a real `<label for=>` pairing, and each form has its own submit.

- **`MoveLaneAsync(laneId, direction, actorId, actorRoles)`** is the new seam
  for re-ordering a lane to its adjacent position: `direction` is `"left"` or
  `"right"`. It resolves the adjacent lane (the nearest lower / higher `Order`
  in the same board) and **transposes** the two lanes' `Order` values — a
  swap, not a renumber — leaving every other lane and every card's
  `BoardItemPlacement` untouched. A lane at the board's edge in that direction
  has no adjacent lane and the call is a **no-op** (nothing written). Standing
  is `creator ∪ GlobalAdmin` over the board (`CheckBoardStanding`); a missing
  lane/board is `KeyNotFoundException` (404), a denied actor
  `UnauthorizedAccessException` (403). One `AccessAudit` row
  (`board.move_lane`, `TargetKind = "board"`) commits with the write.
  **The transposition is executed as a park-and-swap, not a two-row update:**
  `KanbanLane` carries a unique index on `(BoardId, Order)` (M5DocTypes), and
  Postgres enforces a non-deferrable unique index **row-by-row**, so a direct
  swap (moving lane B to C's slot while C still holds it — or vice-versa)
  transiently duplicates the pair and is refused (`23505`). The lanes are
  therefore routed through the board's next-free `Order` (`max + 1`,
  guaranteed unused) and settled one commit at a time, each intermediate state
  being a valid, all-unique board. (A single `UPDATE … CASE` over both rows
  hits the same per-row check and is not a shortcut.)

- **`AddTodoToLaneAsync(boardId, laneId, title, actorId, actorRoles)`** is the
  new seam for adding a to-do **directly onto a lane**: it creates a new
  `TodoItem` (the actor is its `AuthorId`, `IsDeleted = false`, `Audience =
  null`, `LanguageCode` materialized through the ADR 0018 resolver) **and** a
  `BoardItemPlacement` placing it on the given lane at the **end** (`max Order
  + 1`, the `MoveTodoToAdjacentLaneAsync` end-of-lane shape). **C-M5·4:** if
  the lane's `Status` is non-null it is imparted onto the new to-do in the same
  transaction. **C-M5·5:** if the lane is already at its `MaxItems` limit the
  create is **refused** (`InvalidOperationException` naming the lane's title;
  nothing written). Standing is `creator ∪ GlobalAdmin` over the board; a
  blank title is `ArgumentException` (400), a missing lane/board
  `KeyNotFoundException` (404), a denied actor `UnauthorizedAccessException`
  (403). One `AccessAudit` row (`board.add_todo`, `TargetKind = "board"`).

- **The lane foot carries an "Add to-do" form** — a single `Title` input +
  submit posting to `POST /projects/boards/{id}/lanes/{laneId}/todos` (the
  `AddTodoToLaneAsync` lane) — **rendered only while the lane has spare
  capacity** (`MaxItems is null` or `Cards.Count < MaxItems`). A lane at its
  limit shows no add form (the refusal is the C-M5·5 lane, and the foot form
  simply does not appear).

- **The six new strings are registered through `kw-l`** (the
  `KnownTranslationKeys` en/de/fr/da blocks, the ADR 0015 hard-gate test
  `KwLRegistryConsistencyTests`): `projects.board.lane.rename`,
  `projects.board.lane.set_limit`, `projects.board.lane.set_status`,
  `projects.board.lane.move_left`, `projects.board.lane.move_right`, and
  `projects.board.lane.add_todo`.

## Consequences

- A board detail page is **quiet by default** — a lane's head shows its title
  (and status/limit badge, per ADR 0067) and a ⋮ menu; no orphaned inputs or
  save button when nothing is being edited. Each edit (rename, limit, status,
  move) is a one-field form, which is what keeps a lane a *column* rather than a
  form.
- Two new verbs enter the `board.*` audit vocabulary (`board.move_lane`,
  `board.add_todo`) alongside the ADR 0067 ones; both audit against the
  **board** (the lane and the new to-do's placement are not themselves
  auditable resources — C-M5·3).
- `MoveLaneAsync`'s park-and-swap is the one M5 write lane that commits more
  than once (three ordered `SaveChangesAsync` commits + the audit commit).
  That is the price of the `(BoardId, Order)` unique index under Postgres'
  per-row enforcement; each commit is individually consistent and the lane is
  only ever in a valid position, so a failure mid-swap leaves the board in a
  sensible (partially moved) state rather than a corrupted one.
- **No new document, no new schema, no migration:** the lane and the
  placement shapes are unchanged from ADR 0067; only the write seam and the
  view surface change. The M5DocTypes `(BoardId, Order)` unique index is
  what forces the park-and-swap and is untouched.
- `Move left` / `Move right` are **only rendered** for a lane that actually
  has a neighbor in that direction (`hasLeft` / `hasRight` computed in the
  view from the board's lane `Order`s), so the menu never offers a no-op move.
  (The service still treats the edge as a no-op as a defense-in-depth pin.)
- Test pins: `ProjectServiceTests` gains `F11_MoveLane_SwapsOrderWithAdjacent`,
  `F11_MoveLane_AtEdge_NoOp`, `F11_MoveLane_NonCreatorRefused`,
  `F12_AddTodoToLane_PlacesAtEnd_AndImpartsStatus`,
  `F12_AddTodoToLane_AtMax_Refused`, and
  `F12_AddTodoToLane_NonCreatorRefused`; the Web suite's
  `KwLRegistryConsistencyTests` and `ProjectsControllerTests` pin the new
  `kw-l` keys and the unchanged `BoardDetailViewModel` shape.
