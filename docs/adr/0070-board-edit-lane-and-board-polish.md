# ADR 0070 — Board edit lane (title + description) + board polish

Status: Accepted
Date: 2026-09-24
Amends: 0067

## Context

ADR 0067 shipped the M5 board: a `KanbanBoard` with its `KanbanLane`
columns, a `TodoItem`, and a `BoardItemPlacement` binding. The board detail
page (`Views/Projects/BoardDetail.cshtml`) supported **creating** a board and
writing into lanes and cards, but had **no lane to edit the board itself** —
a board's `Title` and `Description` were set once at creation and could not
be changed afterwards (only "Delete board" was reachable from the board head
menu).

Three more surface gaps, all cosmetic:

- **The board and lane ⋮ triggers were bordered buttons** (`btn-outline-
  secondary`) sitting next to the board / lane titles, which read as heavy
  secondary actions rather than quiet affordances.
- **The "add lane" form was vertically centered** in the placeholder lane
  (`.kanban-lane-new-body { justify-content: center }`), while every real
  lane's head sits at the **top** of its column — so the add-lane input
  visually disagreed with the lanes around it.

One already-accepted decision constrains this lane:

- **ADR 0067 §2.3 freezes the `IProjectService` surface.** "An ADD beyond
  this list is a **new ADR**." So the new `UpdateBoardAsync` seam this
  decision records is exactly what this ADR settles.
- **Board standing (C-M5·6)** is `creator ∪ GlobalAdmin` over the board
  (the `CheckBoardStanding` shape) — the same matrix every board-scoped
  write uses, so board edits reuse it rather than inventing a new one.

## Decision

- **One additive `IProjectService` seam: `UpdateBoardAsync`.**
  `UpdateBoardAsync(string boardId, string actorId, IReadOnlySet<string>
  actorRoles, UpdateBoardRequest, CancellationToken)` — a full update of the
  board's `Title` + `Description` only:
  - a missing board is `KeyNotFoundException` (404), standing failure is
    `UnauthorizedAccessException` (403), a blank `Title` is `ArgumentExcepti-
    on` (400) — the existing write-shape posture;
  - a blank `Description` normalizes to `null` (the create-path shape);
  - `KanbanBoard.Modified` is stamped **on a real change only** (the
    `UpdateLaneAsync` no-op shape);
  - one `AccessAudit` row: action `board.update`, the board id as target,
    `AccessVia.Owner` for the creator / `AccessVia.Admin` for the
    GlobalAdmin (the `BoardAuditViaFor` shape).
  - The board's `Audience`, community, and language are **not** editable —
    they are creation-time choices (fixed per ADR 0067's board-creation
    lane) and are not part of `UpdateBoardRequest`.

- **The board edit page**: `GET /projects/boards/{boardId}/edit` (title +
  description form, the description on the same `rc-editor` rich-text
  editor the board-creation form uses) and `POST /projects/boards/{boardId}`
  (redirect-after-POST; success → `TempData` notice; validation / 404 / 403
  → the form re-renders or the status page). The board head ⋮ menu gains an
  **Edit board** item (rendered when the viewer can edit — creator ∪
  GlobalAdmin) above the existing **Delete board** item. `kw-l` keys:
  `projects.board.edit`, `projects.board.edit_heading`,
  `projects.board.edit_lead`, `projects.board.save` × 4 languages.

- **Borderless ⋮ triggers.** A new `.kanban-glyph-btn` class (border: 0,
  transparent ground, a faint `rgba(0,0,0,.06)` hover/focus tint) replaces
  `btn-outline-secondary` on the board-head and lane-head ⋮ buttons — the
  same quiet affordance the card-head ⋮ already had.

- **The add-lane form sits at the top of its column.** `.kanban-lane-new-
  body` changes from `justify-content: center` to `justify-content:
  flex-start`, aligning the input + button with the lane heads around it.

## Consequences

- The `IProjectService` surface grows by exactly one seam (`UpdateBoardAsync`)
  — the frozen-surface rule (ADR 0067 §2.3) is honoured by recording it here
  rather than slipping it into the surface silently.
- A board's title and description are now a living record, matching the
  lane / card edit lanes; the `board.update` audit row keeps the
  audit-by-default posture.
- The board detail page reads as one consistent surface: quiet ⋮ affordances
  at every tier (board / lane / card), and the add-lane input aligned with
  the lanes it joins.
