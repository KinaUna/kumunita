# ADR 0087 — Todo dependency lane (`TBD`): one `BlockedByTodoId?` on `TodoItem` — the "waiting on" pointer (a display chip + a feed filter + one write lane; **never a gate**, never a graph)

Status: Accepted
Date: 2026-09-25 (sign-off U00, 2026-09-25)
Amends: **0067** (the M5 `Projects` context — this ADR adds **one optional field**
to `TodoItem` (`BlockedByTodoId`, additive on the `M5DocTypes` surface, the
ADR 0086 `ProjectId`-on-`TodoItem` precedent) and **additive seams** on the
frozen `IProjectService` — `ListTodosAsync` / `GetTodoAsync` gain the
optional `blockedOnly` / `Blocker` surface, `UpdateTodoRequest` /
`CreateTodoRequest` gain the optional fields, and one new `ListPickerTodosAsync`
read lane — the same frozen-surface-amendment precedent ADR 0084 set for its
`EmitAsync` overload and ADR 0085 for its `linkPath` param: the existing
signatures' required params and return shapes stay compatible and every
existing call site compiles and behaves unchanged); **0073** (the
`unassignedOnly` optional-filter precedent this lane's `blockedOnly`
mirrors); **0004** (§B.1 additive delta-detect — zero migration for existing
rows); **0006** (the frozen `IAuthorizationService` / `IAuditableResource`
path — the blocker chip reuses `CanAsync(Read)` over the **existing**
`TodoItemToAuditableResource`; no new adapter, no new action, no new via, no
new `Decide()` branch); **0015** (the `kw-l` registry — four new keys × 4
languages); and **0024** (the soft-delete / dangling-association rule this
lane reuses for the blocker's soft-delete → the chip degrades).
`Milestones.cs` / the README Roadmap / `MilestonesTests.cs` **untouched** —
a named lane within the M5/PL surface, not a milestone (the ADR 0086 named-
lane precedent; `M7` stays the single `StatusNext`).

## Context

M5 (ADR 0067) shipped the **work items** — assignable, hierarchical to-dos
(`ParentId`) + statused boards, and PL (ADR 0086) added the higher level
(goals / projects) and the `ProjectId?` association. But the surface has no
first-class way to say **"this to-do cannot proceed until that other to-do
is done"** — the "waiting on" relationship. Today it is only implicit: board
lane *sequencing* can imply it, and `TodoItem.Status` (a free string, C-M5·4)
can be labelled to imply it — but nothing names the *other* to-do, nothing
surfaces "what is waiting" for a blocked item, and a to-do that depends on
one on a *different board* has no way to say so at all.

Three already-accepted decisions constrain the shape of the fix:

- **ADR 0067 freezes the `IProjectService` surface.** An ADD beyond it is a
  **new ADR** (the ADR 0084 / 0085 frozen-surface-amendment precedent) — so
  the `blockedOnly` filter, the `Blocker` read surface, the
  `BlockedByTodoId` / `ClearBlockedBy` write fields, and
  `ListPickerTodosAsync` this decision records are exactly what this ADR
  settles.
- **The to-do's standing matrix is creator ∪ assignee ∪ GlobalAdmin**
  (C-M5·6, the `AssignTodoAsync` shape) — the write lane reuses it; a
  blocker is an *association* on the to-do, not a new resource with its own
  matrix.
- **`KanbanStatuses` is a closed *display* vocabulary** (ADR 0069 — the
  stored value stays a plain string) — which is what makes "unblocked"
  **undecidable** on the server side (there is no canonical "done" state a
  to-do is *in*), and therefore why a blocker can only ever be a *hint*,
  never an enforced precondition.

**Rejected alternatives** (recorded for the drift-guard): a
`BlockedBy` **list** (a new `TodoDependency` doc + a DAG + topological
ordering = project-management software, not a neighborhood platform); a
**`Blocked` boolean** (it encodes the claim but not the *target* — and a
boolean "unblocked" is exactly the server-decidable state the string-status
design refuses); **enforced gating** (refuse moves while blocked — makes a
coordination hint into a lockout, and "unblocked" is not server-decidable
anyway — D5).

## Decision

- **D1 — one optional pointer, additive, zero migration.** One
  `string? BlockedByTodoId` on **`TodoItem`** (the ADR 0004 §B.1 / ADR 0086
  `ProjectId` additive shape; existing rows simply have `null`); registered
  on `M5DocTypes` with one `Index` on `(BlockedByTodoId)` (the
  unnamed-computed-index constraint from ADR 0004 §B.1 noted there applies).
  **The singular pointer is the design** — `ParentId?` / `GoalId?` /
  `ProjectId?` are all single-optional-FK idioms; a to-do waiting on *three*
  things uses the `ParentId` subtask structure or three to-dos, not a list.
- **D2 — a display chip + a feed filter, never a gate.** `BlockedByTodoId`
  changes **nothing** about who may see the to-do (its own `Audience`
  decision stays the boundary — C-M5·3), **nothing** about what may be
  written (moving a to-do into a `Done`-statused lane, editing it, or
  deleting it **never** clears or blocks on `BlockedByTodoId` — the lane
  limit is M5's one deliberate refusal and stays the only one), and **never
  a hard-delete cascade target**. It is a *hint*, rendered and filterable.
- **D3 — the cycle guard.** Setting `BlockedByTodoId = X` is **refused**
  when `X == todoItemId` (self) or when `X` **transitively** reaches
  `todoItemId` through the `BlockedByTodoId` chain (visited-set walk; the
  C-M5·7 `ParentId` descendant-cycle guard shape, the
  `InvalidOperationException` refusal). Clearing to `null` is always
  allowed.
- **D4 — the chip (read, access-scoped).** `GetTodoAsync`'s result gains a
  `Blocker?`: the blocker's **title + status** — resolved only when the
  blocker exists, is **not soft-deleted**, and passes its **own**
  `CanAsync(Read)` (the frozen path over the existing
  `TodoItemToAuditableResource`); otherwise the chip renders the generic
  "blocked" label with **no link and no title** (the 404-vs-403 split
  idiom; an unreadable blocker's title/status are not leaked). The board
  card + the to-do detail view render the same chip. **No new adapter, no
  new `AccessAction`, no new `AccessVia`.**
- **D5 — the write lane (standing + guard + audit).** The create form gains
  an optional blocker picker; the edit lane carries `BlockedByTodoId?`
  (a non-null value sets it) + `ClearBlockedBy` (an explicit un-block — the
  `ClearParent` idiom, since the field is *always in the form* the way
  `ComponentId` / `Status` are). **Standing: creator ∪ assignee ∪
  GlobalAdmin over the to-do** (the C-M5·6 matrix, the `AssignTodoAsync`
  shape, re-checked server-side); a non-null target that is
  absent/soft-deleted or unreadable is **refused** (the C3 404-vs-403
  split); `Modified` stamped on a real change; one `AccessAudit` row
  (`todo.update`, `TargetKind = "todo"`).
- **D6 — the `blockedOnly` feed filter.** `ListTodosAsync` gains an
  optional `blockedOnly` (default `false`) — the `unassignedOnly` (ADR
  0073) / `projectId` (ADR 0086) filter discipline: when `true`, only to-dos
  with `BlockedByTodoId != null` are returned; it **narrows the candidate
  set, never the audience decision** (a to-do still only appears if the
  actor passes the `CanSeeAsync(Read)` pass). **`KanbanBoard` gains no
  `BlockedBy`** (a board is a container, not a work item).
- **D7 — the picker.** The to-do create + edit forms seed a `<select>` of
  the actor's readable, non-deleted to-dos (one `ListPickerTodosAsync` read
  lane over the same feed discipline) with a leading "None" option — the
  ADR 0086 D9 project-picker / `SeedComponentPickerAsync` shape; the picker
  is a **display** surface, never a gate (it does not pre-check cycles — the
  service does, D3).
- **D8 — `kw-l` keys.** Four new keys, **× 4 languages** (the `KnownTranslationKeys`
  registry, the ADR 0052 warm-boot baseline backfill covers the non-`en`
  rows): `todo.blocked_by` (the chip / form label),
  `todo.blocked_by_none` (the picker's "None" option),
  `todo.blocked_generic` (the chip fallback when the blocker is
  absent / soft-deleted / unreadable), and `todo.blocked_filter` (the feed
  toggle label).
- **D9 — additive on the frozen M5 surface.** No new `AccessAction`, no new
  `AccessVia`, no new `Decide()` branch, no new adapter, no new bounded
  context, no new document (the `TodoItem` field is the whole schema
  change); every existing `IProjectService` call site compiles and behaves
  **unchanged** (the ADR 0084 / 0085 frozen-surface-amendment precedent).

**Out of scope (→ follow-on lanes, own ADRs):** a `KanbanBoard.BlockedBy`
field (boards are containers); a **multi-blocker** list / `TodoDependency`
doc (D1's rejected alternative); **auto-clear** of `BlockedByTodoId` when
the blocker moves to a `Done`-statused lane (the string-status design makes
"done" a *label*, not a state — clearing is a human decision, explicit via
`ClearBlockedBy`); a **"who is waiting on this" reverse list** (a
`ListTodosBlockedByAsync` read lane — a legitimate follow-on once the chip
is in use); a **notification / reply** on the blocker when it is completed
(a Wolverine side-effect, the standing convention — only once the field is
actually being used); cross-project / cross-goal dependency edges
(`Project.DependsOnProjectId?` — revisit only if the to-do-level field proves
its weight); and any **enforced** dependency (D2).

## Consequences

- The `M5DocTypes` surface gains **one index** (`TodoItem.BlockedByTodoId`)
  — delta-detected at boot (ADR 0004 §B.1), **zero migration** for existing
  rows.
- The frozen `IAuthorizationService` path is **untouched** (the chip reuses
  `CanAsync(Read)` over the existing `TodoItemToAuditableResource`) — no
  adapter, no branch (C-M5·11).
- The `IProjectService` surface grows **additively**: `ListTodosAsync` gains
  the `blockedOnly` optional param, `GetTodoAsync`'s `TodoDetailResult`
  gains `Blocker?`, `CreateTodoRequest` / `UpdateTodoRequest` gain the
  `BlockedByTodoId` / `ClearBlockedBy` fields, and `ListPickerTodosAsync`
  is added — all over the **creator ∪ assignee ∪ GlobalAdmin** matrix and
  the C3 404-vs-403 split; every existing M5 / PL call site + test compiles
  and behaves **unchanged**.
- `ProjectsController` + the to-do `Create` / `Edit` / `Detail` views gain
  the picker + chip + feed toggle; the board `Detail` view's card gains the
  chip; the M5 route surface is **untouched**.
- `KnownTranslationKeys.cs` gains 4 keys × 4 languages (the ADR 0015
  registry; the ADR 0052 warm-boot backfill covers the non-`en` rows).
- The lane is **additive and reusing** — no new bounded context, no new
  `AccessAction` / `AccessVia` / `Decide()` branch, no new document, no new
  editor module, no `Milestones.cs` move.
