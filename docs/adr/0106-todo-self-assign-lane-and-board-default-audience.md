# ADR 0106 — To-do self-assign lane ("Assign to me") and new-board "Everyone" default

Status: Accepted
Date: 2026-09-13
Amends: **0073** (the to-do claim lane — "Assign to me" generalizes the
claim: the same pick-up, but keyed off the to-do's single **Read** decision
rather than a group/community-membership probe), **0067** (the M5 to-do
surface + standing matrix — "Assign to me" is a *fifth* write path into the
to-do's standing, not a redefinition of the matrix), **0074** (the kanban
card "Assign to…" — the card now also exposes "Assign to me" and a "Details"
expander), **0036** (the community-visible audience flag — the new-board
default now seeds it `true`, so a fresh board is "Everyone in this
community"), **0006** (the frozen `IAuthorizationService` — the lane reuses
the existing `CanAsync` Read decision; **no new** `AccessVia`, no new
`Decide()` branch).

## Context

Three requests, one theme: **let a resident pick work up for themselves, and
give a board's default audience the "everyone" meaning the UI already
promises.**

1. *"The new board page audience setting should default to 'Everyone in this
   community' set to enabled."* The board new-page (`BoardNew.cshtml`)
   already renders the "everyone" checkbox from
   `Model.Audience?.CommunityVisible ?? true` and carries copy that says the
   board is visible to everyone by default. But `BoardCreateGet` seeded
   `CommunityVisible = false` with an empty `Grants` list. Under
   `EvaluateAudience`, a **non-null but empty** audience **always denies**
   (the `audience.IsEmpty` guard), so a freshly created board was in fact
   **author-only** — the exact opposite of the page's own "default is
   everyone" text. The rendered state and the submitted state disagreed.

2. *"If a todo is not assigned to the current user, the todo should have the
   option to assign the todos to self (current user). This should be in the
   todo list todo dropdown, the todo details dropdown menu, and in the Kanban
   board todo item dropdown. When using the 'Assign to...' menu option, the
   current user should also be on the list of people that can be assigned
   to."* The standing matrix (C-M5·6) is creator ∪ assignee ∪ GlobalAdmin —
   a *stranger* who can merely *see* an unassigned to-do has no sanctioned
   way to take it onto themselves except the claim lane (ADR 0073), which
   requires group/community membership standing, not just visibility. And the
   "Assign to…" people list deliberately **excluded** the actor themself (the
   grant-picker convention: "addressed to me" is meaningless), so self-assign
   was invisible even where the standing would have allowed it.

3. *"Kanban boards: each todo item on a board should have an expander, which
   should reveal the body, start date (if set), due date (if set), waiting on
   (if not set to 'no blocker'), and community."* A board card shows only the
   title + a metadata line; the full picture (body, dates, blocker, community)
   required navigating to the to-do detail.

## Decision

### 1. The new-board default audience is "Everyone in this community" (enabled)

`ProjectsController.BoardCreateGet` seeds the board's audience editor with
`CommunityVisible = true` (the ADR 0036 community-visible flag), `Mode =
"Any"`, and an empty `Grants` list — matching the Posts precedent
(`PostsController` seeds `CommunityVisible = true`). With a `null`/empty
`ComponentId`, the ADR 0036 branch is the whole decision: **any signed-in
resident sees the board** (the resident-only standing), and the "granular"
audience panel (`_GrantPickers`) hides while the flag is on. The rendered
checkbox (already `?? true`) and the submitted value now agree, and a fresh
board is visible to everyone by default — the view's own promise, kept.
Turning the flag off (and picking grants) is still the path to *narrow* the
audience. **No schema, service, or `Decide()` change** — this is a seed
correction in one controller action.

### 2. The "Assign to me" lane — a Read-gated self pick-up of an *unassigned* to-do

`ProjectService.AssignTodoAsync` gains a **self-assign allowance**, evaluated
*before* the standing matrix:

- If `assigneeId == actorId` (self-assign) **and** the to-do is
  **unassigned** (`AssigneeId` empty) — a *pick-up*, not a *take-over* — the
  gate is the to-do's **single Read decision**:
  `IAuthorizationService.CanAsync(actorId, AccessAction.Read,
  new TodoItemToAuditableResource(todo))`. A resident who can **see** the
  to-do may take it onto themselves; a stranger denied Read is refused.
- Otherwise (assigning to **someone else**, or a to-do that is **already
  assigned**) the existing `CheckTodoStanding` (creator ∪ assignee ∪
  GlobalAdmin) is unchanged — **no take-over, no standing change** for
  non-self writes.

This is the ADR 0073 claim **generalized**: the claim required group /
community *membership* standing (a probe); the self-assign lane is broader
but strictly safer — "if you can see it and it's unassigned, you may take it
onto yourself" is the natural neighborhood rule, and it reuses the *existing*
frozen `CanAsync` Read decision (no new `AccessVia`, no new `Decide()`
branch, ADR 0006). The `todo.assign` audit row and the self-assign
**no-notification** case (actor == assignee skips the recipient) already
exist; both are unchanged.

The UI exposes "Assign to me" in **all three** to-do dropdowns (the to-do
list, the to-do detail, and the kanban card menu), each gated on the to-do
being **unassigned** and the actor being **authenticated**, posting the
existing assign route with `assigneeId = KumunitaPrincipal.SubjectId(User)`
(the card variant also carries the same-site `returnUrl` back to the board).
The server-side Read + unassigned gate in `AssignTodoAsync` is the authority.

### 3. The "Assign to…" people list includes the actor (a separate `Assign_Users`)

The grant-picker convention excludes self (correct — an audience grant of
"addressed to me" is meaningless, and the single-source audience editor + the
subtask assignee picker keep reading the self-excluding `Audience_Users`).
But the **assign** pickers need the actor present, so `SeedGrantPickerOptionsAsync`
seeds a **second, self-including** list as `ViewData["Assign_Users"]` (the
same verified, non-blocked, label-ordered set, minus the self-exclusion
filter). The two "Assign to…" modals (the to-do list + the kanban card) read
`Assign_Users` for their People optgroup; everything else keeps `Audience_Users`.
**No new service or schema** — a view-data seeding change + a repoint of two
read sites.

### 4. The kanban card "Details" expander

`TodoCardRow` gains two optional, trailing, default-`null` fields: `Body` and
`CommunityDisplayName` (existing `StartAt` / `DueAt` / `Blocker` are already
there). `ProjectsController.BoardDetail` populates them: `Body` verbatim from
`card.Body` (a title-only to-do leaves it empty), and
`CommunityDisplayName` resolved from `card.ComponentId` through the
enabled-component name map (`null` when unscoped / unresolvable).
`BoardDetail.cshtml` adds a Bootstrap **collapse** ("Details" button +
content) per card revealing, each section gated on the field being set:

- the **body** — rendered as markdown (`MarkdownRenderer.RenderHtml`, the
  same idiom as the to-do detail), or the "no body — title-only" hint when
  empty;
- **Start** / **Due** — via the `<kw-dt>` effective-timezone render;
- **Waiting on** — the access-scoped `BlockerChip` (the same Generic /
  resolved-chip markup as the at-a-glance line);
- **Community** — the resolved community name.

**Display metadata only** — the card's visibility already ran the two-level
read decision (C-M5·3); the expander reveals nothing the actor may not see.

## Consequences

- A **new board is visible to everyone in the community by default**; to
  narrow it, turn the "everyone" flag off and pick grants. One controller
  seed, no service/schema change.
- A **resident who can see an unassigned to-do can take it onto themselves**
  from any of the three dropdowns, even without group/community standing or
  authorship — the claim (ADR 0073) subsumed. **Already-assigned** to-dos and
  **non-self** assignments still require standing (no take-over).
- The **"Assign to…" people list now includes the actor** (a separate
  `Assign_Users` seed), while the audience/subtask pickers keep excluding
  self.
- **Kanban cards reveal their full picture in place** (body, dates, blocker,
  community) without leaving the board.
- **No new document, migration, `AccessVia`, `Decide()` branch, or
  `AccessAction`** — the lane is a seed correction, a standing allowance in
  one service method, a view-data seed, and a view.
- **Translation**: three new keys — `projects.todo.assign_to_me`,
  `projects.todo.details`, `projects.todo.community` — added to **all four**
  dictionaries (en/de/fr/da) in `KnownTranslationKeys`, so the ADR 0042 /
  0044 parity + warm-add tests keep passing.
