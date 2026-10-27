# ADR 0189 — To-dos: status chip, assignee, filter bar

Status: **Proposed** (scaffolding landed)
Date: 2026-10-27
Relates to: ADR 0188 (shared list rows), ADR 0184 (detail pages)

## Decision

- New partial `Views/Shared/_StatusChip.cshtml` (model: the raw status string) renders a soft chip
  with a coloured dot: `not-started` neutral, `in-progress` amber dot, `done` selected-chip colours with
  an accent dot, `cancelled` muted and struck through; any other (pre-ADR-0069 free-text) value is a
  neutral chip. The label stays the verbatim status text, so meaning never rests on the dot. Used by
  to-do list and detail (and subtasks), Projects landing, goal and project detail.
- To-do list rows: the assignee avatar moves into the meta line (it sat on a line of its own);
  a due date in the past on a to-do that is not done / cancelled is bold with `title="Overdue"`.
- `.kmb-filter-bar` (palettes.css) turns the to-dos, Projects and Boards filter forms into a quiet
  surface toolbar.
- To-do detail: description / translation cards are `.kmb-detail-card`; labels and tags are `.kmb-chip`;
  comments are `.kmb-ann-comment` cards; the composer is a detail card; the boards list is member-style rows.

## Open

- Status labels are not localised (verbatim, as before); no new translation keys were added, so the
  overdue marker is a plain-English `title`.
- The to-do detail header, the subtask / assign modals, and the Boards views (kanban) are not restyled.
- Switches in the filter bar keep Bootstrap's look.
