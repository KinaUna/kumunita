# ADR 0073 — To-do group / community assignment and claim lane (self-claim of unassigned work)

Status: Accepted
Date: 2026-09-25
Amends: **0067** (the M5 to-do surface + standing matrix — the claim is a
*fourth* write lane added to the to-do's standing, not a redefinition of the
matrix), **0013** (the group membership-lane precedent — the claim standing
is a *membership* decision, the M5 mutation matrix is not), **0036** (the
community branch — the claim keys off the to-do's `ComponentId` community),
**0012** (mandatory community membership — the community-membership seam the
claim reads), **0006** (the frozen `IAuthorizationService` — the claim reuses
the *existing* `AccessVia.Group` / `AccessVia.Community` values; **no new**
`AccessVia`, no new `Decide()` branch).

## Context

ADR 0067 shipped the M5 to-do with a single targeting dimension: an
**individual assignee** (`TodoItem.AssigneeId`). The standing matrix
(C-M5·6, enforced server-side in `ProjectService`) is
**creator ∪ assignee ∪ GlobalAdmin** for to-do mutations. That is the right
model for "work assigned to a named neighbor", but it leaves a gap in the
neighborhood's coordination loop:

- There is no way to say "this task is **addressed to a group** (or a
  community)" and let *any* member pick it up.
- A group that posts "we need someone to fix the shared fence" has to name a
  specific resident, or else the task sits unassigned with no sanctioned way
  for a willing member to take it on.

The request: *"It should be possible to assign to-dos to a group or a
community, so users in those can see what tasks are unassigned and pick
something to assign to themselves."*

Two design options were on the table. **Option A (chosen)**: reuse the
to-do's existing `Audience` as the "addressed to" dimension — an audience
group grant means "addressed to that group", and the to-do's `ComponentId`
means "addressed to that community" — and add a **claim** lane: any member of
an addressed group (or a resident of the to-do's community) may self-assign an
**unassigned** to-do to themselves. **Option B (rejected)**: a new
`TodoAssignment`-shaped document (group/community rows) + a new standing
dimension. Option B is heavier (new doc, new migration, new standing
branch, new audit `AccessVia`) for a capability the existing `Audience`
already models as "who this to-do is about."

## Decision

### 1. "Addressed to" reuses the existing `Audience` + `ComponentId` — no new doc, no new migration

A to-do is **addressed to a group** when one of its `Audience.Grants` is a
`GrantKind.Group` grant to that group (the ADR 0013 group lane), and is
**addressed to a community** when its `ComponentId` is set (the ADR 0036
community branch; a resident's community membership per ADR 0012). The
existing audience editor in the to-do composer is therefore *already* the
"addressed to" control — **no composer change is required**; the group grants
and the community picker do the addressing. This keeps the single-source
`Audience` (ADR 0001-B) authoritative and adds **zero** new document fields
(`M5DocTypes` is untouched).

The addressed-to projection is a **read** convenience: `ProjectsController`
resolves the addressed groups' display names (`IUserInfoService.
GetPublicGroupsAsync`, never a gate) into the row's `GroupNames`, and renders
an "Addressed to: …" badge on the feed + detail. It is the same
display-vs-standing separation ADR 0013/0036 already enforce.

### 2. The claim is a new write lane — membership standing, not a `Decide()` branch

`IProjectService.ClaimTodoAsync(todoItemId, actorId, actorRoles, ct)` is the
self-claim. Its standing (`ProjectService.ClaimStandingAsync`) is:

1. The to-do is **unassigned** (`AssigneeId` empty) — a claim is a *pick-up*,
   not a *take-over*; an already-assigned to-do is a 403 (the assignee /
   creator / GlobalAdmin govern it via the C-M5·6 mutation lanes, incl.
   unassigning to free it up).
2. The actor is a member of **at least one** addressed group
   (`Audience.Grants` Group ids ∩ the actor's live group memberships from the
   frozen `IUserInfoService.GetGroupIdsAsync`) **or** a resident of the
   to-do's **community** (`ComponentId` ∈ the actor's live community
   memberships from `IUserInfoService.GetCommunityIdsAsync`).
3. Otherwise a 403; a missing / deleted to-do is a 404 (the C3 split).

On success the claim sets `AssigneeId = actorId`, stamps `Modified`, and
writes the audit row **`todo.claim`** with `AccessVia` = `Group` when the
standing came from a group grant, else `Community` when it came from the
community. It deliberately **reuses** the existing `AccessVia` values —
**no new `AccessVia`, no new `AccessAction`, and no new `IAuthorizationService.Decide()`
branch** (C-M5·11: the M5 lane stays an *adapter* over the frozen seams, not
a new decision dimension). A claim is structurally distinct from the
creator ∪ assignee ∪ GlobalAdmin mutation matrix: it is the *one* to-do write
that is a **membership** decision (the ADR 0013/0036 shape), and it is
gated to the *unassigned* state so it never collides with the assignee.

### 3. The unassigned pool is a feed *filter*, never a gate

`ListTodosAsync(…, bool unassignedOnly = false, …)` narrows the candidate set
to `AssigneeId == null` when set — the "what's available to claim" view. The
feed's existing `CanSeeAsync(Read)` audience gate is untouched and remains
the sole reader; `unassignedOnly` only narrows what the *already-visible*
set shows. The `CanClaim` badge on each row is a **client-rendered mirror**
of the service's claim standing (computed server-side in `ProjectsController`
from the same live-membership seams) — an affordance, **never** an
authorization decision. The authoritative gate is always the service's
`ClaimTodoAsync`, which re-checks membership at write time.

### 4. The surface

- **Feed** (`TodosIndex`): an "Unassigned only" switch on the filter form;
  an "Addressed to: …" badge where the to-do has group grants; a **Claim**
  item in the row's action dropdown (CSRF-tokenized POST to
  `/projects/todos/{id}/claim`, redirect-after-POST) shown only when
  `row.CanClaim`.
- **Detail** (`TodoDetail`): an "Addressed to: …" badge in the top badge
  row, an "Unassigned" else-branch in the meta line, and the **Claim** item
  in the action dropdown gated on `Model.Todo.CanClaim`.
- **Route** (`ClaimPost`): mirrors the `AssignPost` controller idiom — 404
  `NotFound`, 403 `ForbidResult`, success `TempData` + redirect to the
  detail.

### 5. The copy

Three keys are added to `KnownTranslationKeys` (all four language
dictionaries, preserving the parity pins): `projects.todo.claim`,
`projects.todo.addressed_to`, `projects.todo.filter_unassigned`.

## Consequences

- **No schema change.** Reusing `Audience` + `ComponentId` means `M5DocTypes`
  and the migration surface are untouched — the capability is pure standing +
  projection, the ADR 0004 B-shape of "documents + projections, no event
  sourcing" stays intact.
- **No authorization-surface change.** `IAuthorizationService` / `Decide()`
  are frozen (ADR 0006 / C-M5·11); the claim reuses `AccessVia.Group` /
  `AccessVia.Community`. The audit trail gains a new *action* string
  (`todo.claim`) on a known `AccessVia`, which the audit log already
  records verbatim.
- **The standing matrix is extended, not rewritten.** C-M5·6 (creator ∪
  assignee ∪ GlobalAdmin) still governs edit / assign / add-subtask / delete;
  the claim is the additional, membership-gated, unassigned-only pick-up lane.
  A resident can claim onto *themselves* only; they cannot re-assign someone
  else's to-do (that remains the assignee / creator / GlobalAdmin's lane).
- **The feed stays non-leaky.** `unassignedOnly` narrows the candidate set
  after the audience gate, and `CanClaim` is a mirror, never a gate — a
  denied row is never rendered in a state that implies the actor may act on
  it.
- **Tests.** `ProjectServiceTests` gains the claim pins (unassigned-group
  member succeeds + `todo.claim` audit row with `AccessVia.Group`;
  unassigned-community member succeeds with `AccessVia.Community`;
  already-assigned refused; non-member refused; `ListTodosAsync`
  `unassignedOnly` filter) and `ProjectsControllerTests` gains the
  `ClaimPost` pins (404 / 403 / redirect + `TempData`).
- **Roadmap.** This is a **named lane under M5** (like the GP / media
  precedents) — M5 stays `StatusDone`; no milestone letter moves and no
  renumber (the ADR 0013 "group posts: no milestone letter" precedent).
