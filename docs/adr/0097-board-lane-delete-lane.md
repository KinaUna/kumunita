# ADR 0097 — Board lane-delete lane (creator ∪ GlobalAdmin)

Status: Accepted
Date: 2026-09-26
Extends the **M5 board-lane surface** (ADR 0067 the standing matrix + the
frozen `IProjectService` convention; ADR 0068 the lane ⋮ action menu + the
`projects.board.lane.*` `kw-l` key family; ADR 0069 the lane/card renumber +
park-then-settle convention whose `(BoardId, Order)` unique index this lane's
renumber respects; ADR 0070 the board-update seam + the `CanEdit` standing
preview) and the **ADR 0093 group-delete** precedent (a hard-delete write lane
in one session, a `data-confirm`-gated menu item, a `*_delete` audit row, and a
redirect off the surface). This ADR adds the one write lane the lane surface
was missing: **deleting a single lane** from a board.

## Context

The board detail page's lane ⋮ menu (ADR 0068) gained, over the M5/PL lanes,
every lane-level affordance *except* one: set-title, set-limit, set-status,
move-left, move-right, and add-to-do were all present — but **there was no way
to delete a lane**. A board whose creator made a lane they no longer want has
no escape hatch: they can rename it, move it, or cap it, but they cannot
remove it. The only surface that deletes lanes at all is the whole-board
delete seam (`DeleteBoardAsync`), which is the wrong grain — a creator removing
one of five lanes would otherwise have to nuke the entire board.

The shape is already fixed by three precedents:

- **Lanes are the board's own rows, not documents.** A `KanbanLane` (like a
  `BoardItemPlacement`) has **no `IsDeleted` flag** — it is hard-deleted, not
  soft-deleted. The `DeleteBoardAsync` cascade (remove the lane + placement
  rows in one session, C3) is the exact precedent, applied to a single lane
  rather than the whole board. The ADR 0024 author-soft-delete idiom is
  *deliberately not* carried to lanes (the ADR 0093 group-delete precedent:
  hard delete is clean here because the row is the board's own, not a resident
  document).
- **Deleting a lane never touches the to-dos (C-M5·2).** A to-do placed on the
  deleted lane keeps its standalone form and any placements on *other* boards;
  only this board's `BoardItemPlacement` rows for the lane are removed. This
  is the same "a board-delete cascade is membership-only" wall as ADR 0093 —
  the lane is a board-local placement, not the to-do's owner.
- **The standing is the board's, not a per-lane one (C-M5·6).** Lanes have no
  independent standing matrix; the lane-write lanes (add/move, ADR 0068/0069)
  all resolve standing over the **board** via `CheckBoardStanding` — **creator
  ∪ GlobalAdmin**, the assignee branch not applying to a lane. Lane delete is
  the same wall.

## Decision

The board gains **one** write lane — delete a single lane — exposed as one POST
route, one Core seam, one `data-confirm`-gated menu item, and one audit row.
No schema change, no new index, no new bounded context, no new `kw-l` key
*family* (the key joins the existing `projects.board.lane.*` set), and the
`Milestones.cs` / README Roadmap / `MilestonesTests.cs` triple is **untouched**
(a named lane on the already-shipped M5 surface, not a milestone — the ADR
0086/0088/0089/0093 precedent).

**Core — `IProjectService` (additive frozen-surface, ADR 0084 additive-lane
precedent):** one method, `DeleteLaneAsync(laneId, actorId, actorRoles, ct)`,
placed after `MoveLaneToPositionAsync`. In one session (C3):

- **404-first, then standing** (the C3 split): a missing `laneId` or a
  missing / soft-deleted board is `KeyNotFoundException` (404); a denied actor
  (not creator, not GlobalAdmin) is `UnauthorizedAccessException` (403) — the
  standing re-check runs after the row loads, so the failure is a clean split,
  not a 500.
- **Hard-delete the lane's own rows:** the `KanbanLane` row and every
  `BoardItemPlacement` row for that `LaneId` are `session.Delete`d in the same
  session (the `DeleteBoardAsync` cascade shape). The **to-dos are untouched**
  (C-M5·2) — no `TodoItem` row is loaded for mutation, let alone deleted.
- **Renumber the survivors:** the board's **remaining** lanes re-settle to a
  clean `0..n-1` `Order` sequence. Because the `(BoardId, Order)` unique index
  is enforced row-by-row, the ADR 0069 park-then-settle discipline applies —
  but the deleted lane's slot is now free, so parking the survivors into a
  band above their current max is a **no-op** for the `Order` values and the
  two commits collapse into the settle commit (each survivor is written once,
  to its final 0-based index). Survivors whose `Order` is unchanged are not
  re-stamped.
- **One audit row** `board.delete_lane` (`TargetKind = "board"`, the
  **board's** id as the target — the lane is not an auditable resource of its
  own; `Via` = Owner for the creator, Admin for a GlobalAdmin) commits
  atomically with the write (C3).

**Web — `ProjectsController`:** one action, `LaneDeletePost`, on
`POST /projects/boards/{id}/lanes/{laneId}/delete` (`[ValidateAntiForgeryToken]`).
It forwards to `DeleteLaneAsync`, maps `KeyNotFoundException` → `NotFound()`
and `UnauthorizedAccessException` → `ForbidResult` (the C3 split), sets
`TempData["info"] = "Lane deleted."`, and redirects back to
`/projects/boards/{id}` (the actor keeps standing on the board — unlike a
group delete, the creator is not out of the projection, so the redirect stays
on the board, the ADR 0068/0069 move-lane redirect shape).

**View — `BoardDetail.cshtml`:** the lane ⋮ menu (ADR 0068) gains a **Delete
lane** row, gated on `Model.CanEdit` (the same creator ∪ GlobalAdmin standing
preview that gates the board's own delete), set off by a `dropdown-divider`,
styled `text-danger`, wired to a `data-confirm` (the SECURITY.md §6
no-inline-script contract — "Delete this lane? Its cards come off this board —
the to-dos themselves are kept.") and the new `projects.board.lane.delete`
`kw-l` key (ADR 0015 registry; **en/de/fr/da** parity preserved in
`KnownTranslationKeys`).

**Tests:** Core pins (`ProjectServiceTests` F17 — delete middle lane deletes
the lane + its placements, keeps the to-do, renumbers survivors, writes the
`board.delete_lane` audit row with `Via Owner`; delete last lane leaves the
board with zero lanes + the to-do kept; a stranger is refused 403 with nothing
written; a missing lane / a lane on a soft-deleted board 404) and a Web pin
(`ProjectsControllerTests` `Board_DeleteLane` — the redirect + `TempData` +
seam-forwarding happy path, the 404 split, the 403 split).

## Consequences

- A creator (or a GlobalAdmin) can now remove a single lane without touching
  the rest of the board or any to-do; the lane surface's write affordances are
  complete (add / rename / cap / status / move / **delete**).
- Deleting the **last** lane is legal and leaves a zero-lane board — the detail
  view renders its existing `projects.board.no_lanes` empty state, no special
  case needed.
- The renumber reuses the ADR 0069 settle shape; the freed `Order` slot means
  the two-commit park-then-settle collapses to one commit here, so a lane
  delete is strictly cheaper than a lane move.
- Because the lane is hard-deleted (no soft-delete flag), a lane delete is
  **irreversible** — hence the `data-confirm` gate is mandatory, and there is
  no "undo" to promise.
- The audit row targets the **board**, not the lane: a lane-delete is logged
  against the board's `AccessAudit` history, consistent with the add/move
  lane's `board.add_lane` / `board.move_lane` rows.
- The `CanEdit` gate on the menu item is the same matrix the service enforces
  server-side — the UI hides the row for a non-creator; the service still
  re-checks (C3 single-source), so a hidden row POSTed directly still 403s.
