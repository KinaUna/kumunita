# ADR 0195 — Kanban board card edit modal: clicking a card (or choosing **Edit** in its ⋮ menu) opens a modal that edits the to-do in place instead of navigating to the detail page

Status: Accepted
Date: 2026-10-27
Amends: 0190 (the M5 Projects Kanban-boards surface this modal sits on — the board page,
its card markup, and the `board-modal.ts` re-home-to-`<body>` rule the new modal
joins). Reuses (does not restate) 0189 (the to-do detail / edit surface + the
`UpdateTodoAsync` update lane the modal posts to) and 0184/0185 (the
`UpdateTodoRequest` write shape the modal's fields are built to, and the
`TagSlugs.Parse` JSON-or-CSV intake the modal's plain-text tags field feeds).

A Kanban board (the M5 Projects `/projects/boards/{slug}` details page, ADR 0190) is a
**working surface**: its point is to triage to-dos across lanes at a glance.
Before this ADR, editing a card meant navigating away from the board to *reach*
the edit form — the card title was a link, and the ⋮ menu's **Edit** item was a
link, and both went to `/projects/todos/{id}`, so a quick change (re-assign,
fix a due date, tweak the status) cost a page round-trip and a re-orientation.
This ADR closes that seam: the card now opens an **edit modal on the board
itself**, prefilled with the card's current values — the form is *there*,
without leaving the board's spatial context. Saving posts to the same
`/projects/todos/{id}` route the dedicated Edit page uses and lands on the
to-do detail page (ADR 0189) — the *same* destination the Edit page already
uses, so the save behavior is unchanged; only the starting point (and the
distance to the form) moved.

The one thing the whole lane respects:

- **The modal is presentation over an existing write lane — it adds no write
  path.** The form posts to the *same* `POST /projects/todos/{id}` route the
  dedicated Edit page already posts to, with the *same* `UpdateTodoRequest`
  shape (ADR 0184), so it inherits the *same* authorization (the
  `Projects:Edit` grant, resolved by the `Authorization` service per request —
  thin token, fat authorization, ADR 0001). There is **no new `AccessAction`,
  no new `Decide()` branch, no new bounded context, doc, or migration**: the
  `UpdateTodoAsync` service lane (ADR 0184/0185) already does the write, the
  projection bounce, and the side-effect dispatch. The modal is a second
  *surface* onto that one lane, exactly as the dedicated Edit page is.

The shape:

- **Full edit form, minus audience.** The modal carries the same core fields
  the dedicated Edit page does (title, status, assignee, parent + clear,
  blocked-by + clear, start, due, tags, body, component, language). It **does
  not** carry the audience editor, because the `UpdateTodoRequest` write shape
  (ADR 0184) **does not include `Audience`** — the update lane never changes a
  to-do's audience, so a UI that edited it would be a lie. To satisfy
  `TodoEditorModel.IsValid` (which requires a well-formed `Audience` even on
  the update path — a validation-gate artifact), the form posts a hidden
  `Audience.Mode = Any`; the service ignores it. **Audience changes stay on
  the dedicated detail / edit pages, where the audience editor already lives.**
- **Prefilled from the card.** `ProjectsController.BoardDetail` seeds, per
  card, a `CardEditPrefill` (title, body, status, assignee, parent, blocked-by,
  start/due in the resolved zone, component, language, and the card's existing
  tag slugs — `SeedExistingTagSlugsAsync`, the same helper the Edit page uses)
  plus a board-level `CardEditPicker` (parent / blocker / component / language /
  audience options). These are serialized once into a
  `<script type="application/json" id="card-edit-prefills">` blob; the client
  reads it and fills the modal on open.
- **One client module, one shared modal instance.** `board-edit-modal.ts`
  self-wires on `.kanban-board` (the same convention as every other board
  module): it opens the single `#board-card-edit-modal` (a Bootstrap modal
  rendered by the `_BoardCardEditModal` partial), sets the form's `action` to
  `/projects/todos/{id}`, populates the fields from the card's prefill (a
  legacy free-text status — not in `KanbanStatuses.Known` — gets a temporary
  option appended), and shows it. The ⋮ menu's **Edit** item and the **card
  click** both fire the same `data-board-card-edit` trigger; a new **View
  details** link in the ⋮ menu keeps the full detail page (ADR 0189) reachable.
- **Re-homed to `<body>` like the existing card modal.** The modal is nested
  inside the board's transformed/stacking ancestors, so it would be covered by
  the body-level backdrop; `board-modal.ts`'s `show.bs.modal` handler already
  re-homes card modals to `<body>` (ADR 0190), and the new edit modal is added
  to that same `cardModals` collection so it inherits the fix.
- **Tags via plain CSV.** The modal's tags field is a plain text input fed to
  `TagSlugs.Parse` (JSON-array *or* raw-CSV, ADR 0185), so it can be prefilled
  with the card's existing slugs joined and edited without the tag-suggest
  chip UI; the dedicated Edit page keeps the richer `bindTagSuggest` surface.

Scope kept out of this ADR (deliberate, not accidental):

- **Audience editing** — by construction (the update lane drops it; ADR 0184).
- **Assignee as a separate lane** — the modal reuses the update-lane assignee
  field for convenience; the dedicated `AssignTodoAsync` lane (ADR 0184) is the
  canonical assign surface and is untouched.
- **A detail-viewing modal** — this is an *edit* modal; **View details** still
  navigates to the full ADR 0189 detail page, which carries the comment cards,
  history, and every other detail affordance.

Why a modal and not inline-on-the-card:

- A card is a ~280px-wide lane item; the full edit form (ten+ fields, two
  pickers, two clear-checks, a body) does not fit inline without collapsing the
  lane. A `modal-lg` gives the form room while keeping the board visible
  behind it, and the Bootstrap modal + the existing `board-modal.ts` re-home
  rule are already standing infrastructure in this codebase — reusing them is
  cheaper and more correct than inventing a bespoke inline editor.
