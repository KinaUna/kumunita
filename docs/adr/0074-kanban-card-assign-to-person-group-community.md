# ADR 0074 — Kanban card "Assign to…": person, group, or community

Status: Accepted
Date: 2026-09-24
Amends: 0071

## Context

ADR 0071 gave the Kanban card its ⋮ menu (Edit / Unassign / Add subtask,
Copy / Move to board, the reorder moves, Delete) and established the
Bootstrap-modal convention the card's "Add subtask" row uses. The assign
*lane* itself already exists (ADR 0067, the U07 route `POST
/projects/todos/{id}/assign`, the service's `AssignTodoAsync`: sets /
clears `AssigneeId`, standing = creator ∪ assignee ∪ GlobalAdmin, the
`todo.assign` audit row) — but on the board the only assign affordance is
the **Unassign** form, which renders *only when* a card already has an
assignee. There is no way to assign, reassign, or point a card at a
**group** or a **community** from the board at all (the detail page's
edit form assigns a person only).

ADR 0073 settled the read side of the group / community semantics: a to-do
addressed to a group / community is *claimable* by its members. This ADR
is the write-side counterpart — the assignee of a to-do may be a **person**
(a `Profile` subject id), a **group** (a `Group` id), or a **community**
(a `Component` id); the standing grant (F7) applies unchanged, and C-M5·3
holds: `AssigneeId` is *display + standing*, never a gate.

Two already-accepted decisions constrain this lane:

- **The assign route is fixed** (ADR 0067): `POST /projects/todos/{id}/assign`
  with the form field `assigneeId` (empty = unassign). This ADR adds **no
  new controller or service seam** — it adds a UI affordance that drives the
  existing route, plus one optional `returnUrl` form field on that route so
  a caller arriving from a board can go back to the board.
- **The modal convention is fixed** (ADR 0071): the affordance is a menu
  button opening a Bootstrap 5 modal, the footer submit naming the form via
  the `form` attribute.

## Decision

- **The card ⋮ menu gains an "Assign to…" row, always first.** A menu button
  (localized `projects.todo.assign_to`) opens a per-card Bootstrap modal
  (`#assign-{todoId}`, the ADR 0071 shape: modal inside the card div,
  fixed-position, `display:none` until opened). The modal body is:
  - a "currently assigned" line (the card's `AssigneeDisplayName`, when
    set);
  - a **single** `name="assigneeId"` `<select>` with three `<optgroup>`s —
    **People** (the seeded `Audience_Users`: verified, non-blocked, non-self
    profiles), **Groups** (the seeded `Audience_Groups`: public groups —
    a private group is an organizing unit, never a grant target, ADR 0010),
    and **Communities** (the seeded `Assign_Communities`: the instance's
    enabled components) — led by an **Unassigned** option (an empty value;
    submitting it clears the assignee, the lane's existing semantics).
  - footer **Cancel** (`data-bs-dismiss`) / **Assign** (`type="submit"`,
    `form="<id>"`). The current assignee is preselected when it matches an
    option (the id spaces are disjoint on a real instance).
- **The modal posts the existing assign route** with a same-site
  `returnUrl` field (`/projects/boards/{boardId}`), so the redirect — and
  the "To-do assigned." / "Assignee removed." flash — lands on the board
  the caller came from, not on the to-do's detail. The `Unassign` menu form
  is unchanged (its default target is still the to-do's detail).
- **The standing gate is the service's, unchanged.** The modal's option
  lists are a *display convenience, never a gate*: any `assigneeId` that
  survives the service's `AssignTodoAsync` standing check (creator ∪
  assignee ∪ GlobalAdmin) is accepted; a group / community id is a valid
  assignee value with the same standing semantics as a person id (the
  C-M5·3 pin, the ADR 0073 read-side semantics mirrored on the write side).
  No new `IProjectService` seam, no `Decide()` branch, no schema change
  (`M5DocTypes` untouched — `AssigneeId` is already a single subject-id
  string).
- **The controller gains two small additive pieces** (thin HTTP layer,
  ADR 0006-D):
  - `AssignPost` accepts an optional `[FromForm] string? returnUrl =
    null`; a **same-site** `returnUrl` (checked with
    `Url.IsLocalUrl`) is the redirect target, otherwise the lane's
    original target (the to-do's detail). An external URL is never
    followed (no open redirect).
  - `BoardDetail` (GET) seeds the modal's option lists:
    `SeedGrantPickerOptionsAsync()` already seeds `Audience_Users` +
    `Audience_Groups` (the ADR 0071 "Add subtask" modal's source); this ADR
    adds `Assign_Communities` from `IUserInfoService.GetComponentsAsync
    (enabledOnly: true)` (the M2/M3/M4 component-picker seam).
  - The card's `AssigneeDisplayName` is now resolved **across** profiles,
    groups, and components (the first lookup that hits wins) so a
    group / community assignee renders its name on the card instead of its
    raw id (a read surface, never an access decision).
- **Localization.** Five new keys, all four languages (the parity pins —
  `KnownTranslationKeys_ParityTests` / `KwLRegistryConsistencyTests` — hold):
  `projects.todo.assign_to` ("Assign to…"),
  `projects.todo.assign_people` / `projects.todo.assign_groups` /
  `projects.todo.assign_communities` (the optgroup labels, resolved the
  ADR 0072 attribute-exception way), and the modal's lead line reuses the
  existing `projects.todo.assignee` key.

## Consequences

- The board now has a symmetric assign / unassign affordance: "Assign to…"
  is always present; the old "Unassign" row stays as the quick clear.
- A to-do on the board can be pointed at a group or a community — the
  natural write-side counterpart of the ADR 0073 claim lane — with the
  standing matrix and audit semantics unchanged.
- One additive form field on an existing route (`returnUrl`); one additive
  `ViewData` bag (`Assign_Communities`); no new service seam, no schema
  change, no new dependency.
- The ADR 0067 frozen `IProjectService` surface is untouched (the
  frozen-surface rule, as in 0068 / 0069 / 0070 / 0071 / 0073).
