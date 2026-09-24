# ADR 0069 — Kanban board: drag-and-drop moves, fixed statuses, add-lane

Status: Accepted
Date: 2026-09-23
Amends: 0067, 0068

## Context

ADR 0067 shipped the M5 board and ADR 0068 added the per-lane ⋮ menu (rename /
set limit / set status / move left / move right) plus an add-to-do foot. Three
things are still at odds with the board's reference model (KinaUna's kanban,
`KinaUnaAzure/Kanbans`) that the operator uses daily:

- **Moving is keyboard- and menu-only.** ADR 0067 D10 / C-M5·9 pins "no HTML5
  drag, no optimistic reordering"; the only channels are the arrow-key
  shortcuts on a focused card (`client/lib/projects-board.ts`) and the
  move-up/down/left/right menu items. A board you have to *read the menu* to
  rearrange does not feel like a board.
- **Statuses are free text.** `TodoItem.Status` and `KanbanLane.Status` are
  unconstrained strings ("Status is a free-text label — a string, not a fixed
  list", ADR 0067). There is no shared vocabulary, so one resident's
  "in progress" and another's "In Progress" are different values, and the lane
  head can only render whatever text it was given — no icon.
- **A board starts with its lanes frozen at creation.** `CreateBoardAsync`
  writes the initial lanes and there is no seam to add a lane afterwards; the
  only way to gain a column is to edit a raw `Order` by hand.
- **The lane head is quiet about capacity.** `MaxItems` is shown as a bare
  "Max items: N" badge with no count of what is actually on the lane, and the
  status is a plain text badge with no icon.

Two already-accepted decisions constrain this lane:

- **ADR 0067 §2.3 freezes the `IProjectService` surface.** "An ADD beyond this
  list is a **new ADR**." The three new seams below — `CreateLaneAsync`,
  `MoveLaneToPositionAsync`, `MoveTodoToLanePositionAsync` — are exactly what
  this ADR records, plus the fixed status vocabulary.
- **Board/lane standing (C-M5·6)** is `creator ∪ GlobalAdmin` over the
  **board** (the `CheckBoardStanding` shape); the placement-standing surface is
  the **to-do** (creator ∪ assignee ∪ GlobalAdmin, `CheckTodoStanding`). A lane
  and a placement are not themselves auditable resources (C-M5·3), so lane
  seams resolve standing against the board and audit against the board's id;
  card seams resolve standing against the to-do and audit against the to-do's
  id.

## Decision

### Drag-and-drop is a new interaction channel, not a new trust model

The mouse channel is added to the **same** server-authoritative contract as
the keyboard and menu channels. A drop is a `POST` (CSRF-tokenized, the
`getAntiForgeryToken` / `RequestVerificationToken` idiom) and the page
**re-renders from the server's response** — there is still **no optimistic
DOM reordering** (D10's spirit is preserved: the client never re-shapes a
board on its own, it only submits intent and follows the redirect). What
changes is that the *input* is a drag, not an arrow key or a menu click. The
keyboard channel (arrow keys on a focused card) and the menu channel
(move-up/down/left/right) are **kept as the accessible fallback** and continue
to work unchanged.

### Fixed status vocabulary (Core)

A new `KanbanStatuses` static class in `Kumunita.Core.Projects` defines the
closed status vocabulary as **string constants** (the stored value stays a
`string` — no schema change, no Marten migration, ADR 0004 untouched):

- `NotStarted = "not-started"`, `InProgress = "in-progress"`,
  `Done = "done"`, `Cancelled = "cancelled"`, and `Known = [NotStarted,
  InProgress, Done, Cancelled]`.

`KanbanLane.Status` and `TodoItem.Status` may still hold `null` (no status) or
any legacy free-text value — the vocabulary is a **documented, rendered** set,
not a database constraint. The lane's "Set status" menu changes from a free
`<input>` to a `<select>` over `null` (None) + the four constants, so new
writes pick from the shared set. The status **icon** is a Web-layer concern:
each code maps to one inline SVG glyph (the site's existing inline-SVG icon
convention — no icon font is shipped), matching KinaUna's
`getStatusIconForTodoItems` set (circle-dot → not-started, clock/arrow →
in-progress, check-circle → done, cancel → cancelled, help-circle → unknown).

### `CreateLaneAsync(boardId, title, actorId, actorRoles)`

The **add-lane** seam. Creates a new `KanbanLane` on the given board with the
given `Title` (`Status = null`, `MaxItems = null`, `Order` = the board's
`max Order + 1` — the end of the board, the `AddTodoToLaneAsync` end-of-lane
shape). Standing is `creator ∪ GlobalAdmin` over the board
(`CheckBoardStanding`); a blank title is `ArgumentException` (400), a missing
/ soft-deleted board is `KeyNotFoundException` (404), a denied actor
`UnauthorizedAccessException` (403). One `AccessAudit` row
(`board.add_lane`, `TargetKind = "board"`, the board's id) commits with the
write. A new lane is empty (no `BoardItemPlacement` rows).

### `MoveLaneToPositionAsync(laneId, index, actorId, actorRoles)`

The **lane drag** seam — a generalization of ADR 0068's adjacent
`MoveLaneAsync`. Moves a lane to the board's **0-based position `index`**
(clamped to `[0, laneCount-1]`) among the board's lanes ordered by `Order`.
This is a **reorder, not a renumber of cards**: the lane's own `Order`
changes, every other lane's `Order` is re-settled to a clean `0..n-1`
sequence, and every card's `BoardItemPlacement` (its `LaneId` + `Order`) is
untouched — a card keeps its lane and its slot. A no-op when the lane is
already at `index`. Standing, 404/403 split, and the `board.move_lane` audit
row are the same as `MoveLaneAsync` (audit target is the **board**).

**The renumber is executed park-then-settle, in two commits** (the ADR 0068
23505 rationale, generalized): `KanbanLane` carries a unique index on
`(BoardId, Order)` and Postgres enforces a non-deferrable unique index
**row-by-row**, so writing a lane's new `Order` while its destination still
holds the old value transiently duplicates the pair. The board's lanes are
therefore first parked to a guaranteed-free band (`maxOrder + 1 + position`,
all distinct, all above the current max — one commit), then settled to the
final `0..n-1` sequence (a second commit). Each commit leaves an all-unique
board. `MoveLaneAsync` (left/right) is **kept** as the keyboard/menu channel.

### `MoveTodoToLanePositionAsync(placementId, targetLaneId, index, actorId, actorRoles)`

The **card drag** seam. Moves a card's `BoardItemPlacement` to the given
lane at the lane's **0-based position `index`** (clamped to
`[0, laneCardCount-1]`). Semantics, all server-side (the F4/F5/F6 FACES):

- **C-M5·4 (status auto-update):** if the target lane's `Status` is non-null it
  is imparted onto the to-do in the same transaction; a null lane `Status`
  leaves the to-do's status unchanged.
- **C-M5·5 (lane-limit refusal):** if the card is moving **into a different
  lane** and that lane is already at its `MaxItems` limit, the move is
  **refused** (`InvalidOperationException` naming the lane's title; nothing
  written). A reorder **within** the same lane never trips the limit (the
  lane's placement count is unchanged).
- **The reorder is park-then-settle, in two commits** (the 23505 rationale
  generalized to `BoardItemPlacement`'s `(BoardId, LaneId, Order)` unique
  index): the target lane's placements (the moved card + its current cards,
  excluding the moved card when it came from another lane) are parked to a
  free band (commit), then settled to `0..n-1` (commit). The **source** lane
  is not renumbered — its remaining cards keep their relative order (a gap in
  `Order` is harmless; `Order` is only a sort key).

Standing is `creator ∪ assignee ∪ GlobalAdmin` over the **to-do**
(`CheckTodoStanding`); a missing placement / to-do / lane is
`KeyNotFoundException` (404), a denied actor `UnauthorizedAccessException`
(403). One `AccessAudit` row (`todo.move_to_lane`, `TargetKind = "todo"`, the
to-do's id — the same verb and target as ADR 0067's adjacent-lane move, so a
drag and a menu-move are indistinguishable in the audit trail). The adjacent
`MoveTodoWithinLaneAsync` / `MoveTodoToAdjacentLaneAsync` lanes are **kept** as
the keyboard/menu channel.

### The lane head, the add-lane affordance, and the count

- **The board head gains an "Add lane" ⋮/+ button** posting to a new
  `POST /projects/boards/{id}/lanes` route (the `CreateLaneAsync` lane) — a
  single `Title` input + submit in a dropdown, the board-head menu idiom.
- **The lane head shows a status icon + label** (not a free-text badge): the
  inline-SVG glyph for the lane's status code (or a muted "no status" state)
  followed by the localized label, per the fixed vocabulary above.
- **The lane head shows the card count and, when set, the limit** — `N` cards
  and, if `MaxItems` is set, `N / MaxItems` (a muted "at limit" state when
  `N == MaxItems`). This replaces the bare "Max items: N" badge.
- **The drop targets.** Each `.kanban-lane` (the card list is the drop zone)
  and each `.kanban-lane-head` (for lane reordering) is a drag-and-drop
  target. A `dragover` highlights the lane; a `drop` computes the target index
  from the pointer's position among the lane's cards and `POST`s to the new
  card-move or lane-move route.

### Routes (Web)

- `POST /projects/boards/{id}/lanes` → `CreateLaneAsync` (add lane).
- `POST /projects/boards/{id}/lanes/{laneId}/move` (`index` form field) →
  `MoveLaneToPositionAsync` (lane drag).
- `POST /projects/boards/{id}/lanes/{targetLaneId}/cards/{placementId}/move`
  (`index` form field) → `MoveTodoToLanePositionAsync` (card drag).

All three are `[ValidateAntiForgeryToken]`, redirect-after-POST to
`/projects/boards/{id}` (the ADR 0068 idiom); a lane-limit refusal or a blank
title is a `TempData["error"]` (the `AddTodoToLanePost` shape), a denied actor
a 403, a missing id a 404.

### Strings

New `kw-l` keys (the `KnownTranslationKeys` en/de/fr/da blocks, the ADR 0015
hard-gate): `projects.board.lane.add_lane`, and the status labels
`projects.board.status.none` / `.not_started` / `.in_progress` / `.done` /
`.cancelled`.

## Consequences

- A board can finally be **rearranged with the mouse**: a card or a lane is
  picked up, dropped on a lane, and the board re-renders server-authoritatively.
  The keyboard and menu channels remain the accessible fallback; nothing about
  the trust model changes (no optimistic reordering, no client-side board
  mutation, D10's spirit intact).
- The fixed status vocabulary makes the board **legible**: lanes and cards
  carry a shared, icon-able status instead of arbitrary text. It is additive
  and backward-compatible — legacy free-text statuses still render (falling
  back to the "unknown" glyph + the raw text).
- Two new verbs enter the `board.*` audit vocabulary
  (`board.add_lane`) and the card move reuses the existing
  `todo.move_to_lane` verb, so a drag and a menu-move are the same audit
  event. All three reordering seams commit more than once (park-then-settle)
  to stay inside Postgres's row-by-row unique-index check.
- `IProjectService` grows by three methods (`CreateLaneAsync`,
  `MoveLaneToPositionAsync`, `MoveTodoToLanePositionAsync`) — the ADDs this
  ADR authorizes under ADR 0067 §2.3's freeze.
