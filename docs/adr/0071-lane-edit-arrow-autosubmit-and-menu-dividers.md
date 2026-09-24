# ADR 0071 — Kanban lane / card editing: modals, autosubmit status, menu dividers

Status: Accepted
Date: 2026-09-24
Amends: 0068, 0069

## Context

ADR 0068 gave the M5 Kanban board (ADR 0067) its lane action menu: the lane
head's ⋮ dropdown carried the Rename form, the Set limit form, the Set status
form, and the Move left / Move right reorder forms (each a POST form to the
lane-update or lane-reorder route), and the card's ⋮ dropdown carried Edit /
Unassign / Add subtask, Copy / Move-to another board, the reorder moves, and
Delete.

The board worked, but the menus read as flat lists, and the inline form
fields crowded the compact dropdown. Three concrete complaints (2026-09-24,
from using the surface) drove this ADR, and a fourth (same day) added the
modal shape:

- **The lane and card dropdown menus had no dividers** — which functions
  belong together (rename/limit/status vs. reorder vs. delete) was only
  inferable from the item order.
- **The inline Title / MaxItems inputs didn't fit the dropdown well** — the
  request: a single ⋮ menu whose **Rename** row opens a **modal** to edit
  the title, and whose **Add subtask** row opens a **modal** to add a to-do,
  in place of cramped inline inputs + arrow buttons.
- **The "Set status" button was a pointless round-trip** — the request:
  remove it and just **update the value when the status dropdown changes**.
  (The **Max items** row keeps its inline number input + a → arrow — a
  single number field fits the dropdown fine; only the title/limit text
  inputs were asked to move to modals.)

Two already-accepted decisions constrain this lane:

- **The lane-update route is fixed** (ADR 0068, the U08 shape): `POST
  /projects/boards/{id}/lanes/{laneId}` with the partial-update
  `LaneEditorModel` (a non-blank `Title`, optional `MaxItems` / `Status` /
  `Order`); the service's `UpdateLaneAsync` change-detection is the
  authority, and the `CheckBoardStanding` creator ∪ GlobalAdmin matrix gates
  it. This ADR adds **no new controller or service seam** — it changes how
  the existing route is *driven*.
- **The tsc-only / zero-dependency pin** (ADR 0031) holds: the autosubmit
  ships in the board's existing plain-TS module, reusing its existing
  `postAndRedirect` + `getAntiForgeryToken` shape.

## Decision

- **The lane keeps its single ⋮ dropdown; the named buttons become → icons
  inside it.** The lane head's one ⋮ dropdown (the ADR 0068 shape, the
  ADR 0070 borderless `kanban-glyph-btn` trigger, `ms-auto` at the head's
  right edge) holds every lane action in one menu, now reshaped. The single
  field forms to the U08 lane-update route (`POST
  /projects/boards/{id}/lanes/{laneId}`, the `LaneEditorModel` shape — a
  partial update; the service's `UpdateLaneAsync` change-detection is the
  authority) are kept, but reshaped:
  - **Rename** — a **menu button opening a Bootstrap modal** (this ADR's
    first in-repo use of Bootstrap modals; the 5.x bundle is already loaded
    in `_Layout.cshtml`). The modal is a single `Title` input (required,
    `maxlength 120`, prefilled with the lane's current title) posting the
    lane-update route (MaxItems omitted → preserved), with a **Cancel**
    (`data-bs-dismiss`) / **Save** (`type="submit"`, `form="<id>"`) footer.
    The `Title` input + arrow that crowded the dropdown is gone.
  - **Set limit** — the `MaxItems` input, kept inline in the menu and
    submitted by a quiet **→ arrow icon button to the right of the input**
    (`.kanban-arrow-submit`, a compact `type="submit"` button with a 15px
    inline SVG). A single number field fits the dropdown; only the title
    was asked to move to a modal.
  - **Set status** — the `Status` select, with **no submit button at all**
    (see the autosubmit below); hidden `Title` likewise.
  The **Move left / Move right** reorder forms (the ADR 0068 reorder routes,
  the service's `MoveLaneAsync`) stay in the same menu, after a divider, and
  render **only when a neighbor exists** (`hasLeft` / `hasRight` from the
  lane's `Order`) — an edge lane's ⋮ menu is just the three edit rows. The
  standing + limit decisions are the service's (F7 / F8 / F6); the view
  renders what the service returns (C3).

- **Autosubmit on change (client-side, server still authoritative).** A
  delegated `change` listener in `client/lib/projects-board.ts` (channel
  `b2` of the module's documented channels): the `.kanban-lane-autosubmit`
  Status select auto-POSTs its own form's `action` (the lane-update route)
  with the `Status` value, the form's `Title` (required by the endpoint),
  and the view-rendered `__RequestVerificationToken` — via the module's
  existing CSRF-aware `postAndRedirect` (the `RequestVerificationToken`
  header + form-urlencoded body, following the redirect). A blank `Status`
  option (the `None` row) is an explicit update to none. The server stays
  authoritative (C3 / C-M5·9): no optimistic DOM update, the redirect
  re-renders the board. The Rename row (a modal) and the Set-limit row (a
  → arrow) have no discrete "change" event to hook, so they keep an
  explicit submit affordance.

- **The card menu's "Add subtask" row opens a Bootstrap modal.** The
  ADR 0068 card ⋮ menu's Add-subtask row — formerly a cramped inline `Title`
  input + a named submit button — is now a **menu button opening a modal**
  (the same first-use-of-modals as the lane Rename, above). The modal posts
  the to-do subtask route (`POST /projects/todos/{id}/subtasks`, the
  `AddSubtaskModel` shape) with a required `Title`, an optional free-text
  `Status` (C-M5·4), and an optional `AssigneeId` select (the standing
  options — display + standing, never a gate) seeded by an additive
  `SeedGrantPickerOptionsAsync()` call in the board detail's `GET` (the
  `Create` / `BoardNew` views' idiom). **Cancel** / **Add** footer. This
  is the repo's first Bootstrap modal (there was none before), so this ADR
  sets that convention: stock `data-bs-toggle="modal"` markup, the footer
  submit naming the form via the `form` attribute, and `common.*` keys for
  the shared footer labels.
  - **The same "Add subtask" modal is applied to the to-do list + detail
    pages** (the `TodosIndex` and `TodoDetail` views' ⋮ menus), which
    previously carried the cramped inline `Title` input + named submit
    button. Each "Add subtask" menu row is now a button opening a modal of
    the identical shape (required `Title`, optional `Status`, optional
    `AssigneeId` select) posting the same to-do subtask route; the two
    views' `GET` actions each gain the same **additive**
    `SeedGrantPickerOptionsAsync()` call so the Assignee picker has
    options. `TodosIndex` renders one modal per row (`#subtask-{row.Id}`);
    `TodoDetail` a single modal (`#subtask-modal`). No new controller
    action, service method, or route — the existing subtask lane is reused.

- **Menu dividers (`.kanban-divided-menu`).** `<hr class="dropdown-divider">`
  items now separate the menus' functional groups:
  - lane ⋮ menu — between **Rename | Max items | Status**, and between
    that group and **Move left / Move right**;
  - card menu — between **Edit / Unassign / Add subtask** | **Copy / Move to
    board** (rendered only when another board exists) | **Move up / down /
    left / right** | **Delete** — the destructive action always separated.
  The site.css rule gives those dividers full opacity, a `rgba(0,0,0,0.18)`
  hairline, and `0.25rem` vertical margin so the groupings read at a glance.

## Consequences

- Nearly zero new seams: the `BoardDetail`, `TodosIndex`, and `TodoDetail`
  `GET` actions each gain one **additive** `SeedGrantPickerOptionsAsync()`
  call (the `Create` / `BoardNew` views' idiom) so their Add-subtask
  modal's Assignee picker has options. No new controller action, no
  `IProjectService` addition (the frozen-surface rule of ADR 0067 §2.3 is
  untouched), no new route. The lane-update + lane-reorder lanes and the
  to-do subtask lane are reused as-is.
- The lane ⋮ menu is still one menu: **Rename** opens a modal, **Max items**
  submits via a quiet → icon, and **Status** commits the moment it changes —
  one fewer round-trip each, the server's validation / standing checks (and
  its `lane.update` audit rows) still decide the outcome, so the
  audit-by-default posture is unchanged. The card menu's **Add subtask**
  likewise opens a modal (one fewer cramped inline field).
- This ADR **sets the repo's first Bootstrap-modal convention**: stock
  `data-bs-toggle="modal"` markup, the footer submit naming the form via the
  `form` attribute, and shared footer labels on the existing `common.cancel`
  / `common.save` / `common.add` keys. The Rename button reuses the
  already-registered (previously unused) `projects.board.lane.rename` key,
  and the modal's title/status labels reuse `common.title` /
  `projects.todo.status` — the localization registry is otherwise untouched
  (the one-directional `KwLRegistryConsistencyTests` still passes).
- `site.css` keeps the `.kanban-arrow-submit` icon-button rule (now only the
  Set-limit row) and the `.kanban-divided-menu .dropdown-divider` hairline;
  the modals are styled by stock Bootstrap — no new CSS rules.
