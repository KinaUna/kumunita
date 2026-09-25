# `TBD` — Todo dependency ("waiting on") — the `BlockedByTodoId?` lane (sealed unit register)

> **In progress.** This is the **lane plan** (the secondary register tier)
> for the **`TBD`** lane (short id **`TBD`** = *Todo Blocked-By*: the
> "waiting on" pointer on the M5 to-do surface — one optional
> `BlockedByTodoId` on `TodoItem`). The **primary reference tier** (the
> exact C# seams + the locked decisions) is the design doc
> `docs/design/tbd-todo-dependency-design.md` (authored **U00**); the
> **scratch tier** is
> `docs/plans-milestones/in-progress/tbd/tbd-handoff-notes.md` (one appended
> `## U#` section per unit, never rewritten).
>
> **What this is:** one **additive `string? BlockedByTodoId` field on
> `TodoItem`** (a *hint, never a gate* — C-M5·3 / C-M3·2 discipline),
> **additive on the `M5DocTypes` surface** (one `Index`, zero new surface,
> zero migrations for existing rows), **additive `IProjectService` seams**
> (the `blockedOnly` feed filter on `ListTodosAsync`, the `Blocker?` chip
> surface on `TodoDetailResult`, the `BlockedByTodoId` / `ClearBlockedBy`
> write fields on the to-do create / edit requests, and the one new
> `ListPickerTodosAsync` read lane), **one cycle guard** (self + transitive
> refusal — the C-M5·7 shape, the lane's only server-side refusal), and
> **the Web**: the "Blocked by" chip on the to-do detail view + the board
> card, the blocker picker on the to-do create / edit forms (the
> `SeedComponentPickerAsync` shape), and the `?blockedOnly=` feed toggle on
> the existing to-do feed. **No new routes, no new document, no new adapter,
> no new `AccessAction` / `AccessVia` / `Decide()` branch, no new bounded
> context.**
>
> **The one thing every unit must respect:** this lane is **additive and
> reusing.** It reuses the frozen
> `IAuthorizationService.CanAsync(Read)` / `CanSeeAsync(Read)` decision
> path (ADR 0006) over the **existing** `TodoItemToAuditableResource`
> (the chip is a *read of another to-do*, not a new resource), the standing
> matrix **creator ∪ assignee ∪ GlobalAdmin** (the C-M5·6 to-do matrix —
> the `AssignTodoAsync` shape), the `KanbanStatuses` closed display
> vocabulary (ADR 0069) for the chip's status text, the `kw-l` registry
> (ADR 0015) for the four new keys × 4 languages, and the `M5DocTypes`
> surface (the ADR 0004 §B.1 delta-detect — one new index, zero
> migration). **No code in this unit — no build, no tests.**
>
> **Sizing:** units are sized for a **~32K-context fresh agent** one at a
> time, each with its own closed exit criteria, in the `M5` / `PL` style.
> **U00 is the sign-off gate** — it authors the design doc and locks the
> **[PROPOSED]** decisions into **ADR 0087**; every later unit codes
> against the *locked* text. **Sequencing invariant:** the service seams
> (U02) land before any Web view; the close (U04) is last so the doc index
> + README + the ADR status are honest at ship time.

## Understanding (one paragraph)

M5 shipped **to-dos and Kanban boards** — the *work items* and the *boards
they sit on* — and deliberately kept `Status` a **free string** (C-M5·4)
with **no status vocabulary and no status machine**: the board is the state
machine the to-do flows through. What neither surface names is the
**"waiting on"** relationship — *this to-do cannot proceed until that other
to-do is done*. Today it is only implicit: a board's lane *ordering* can
imply it, and a `Status` string can be *labelled* to imply it — but nothing
names the *other* to-do, nothing answers "what is waiting", and a to-do
blocked by one on a **different board** has no way to say so at all. `TBD`
adds exactly that, as one optional `BlockedByTodoId` on `TodoItem`: a
**hint, never a gate** — it never changes the to-do's own `Audience`
decision, never enables or blocks a write, and never cascades a
hard-delete. It is rendered as a "Blocked by" chip on the to-do detail +
board card (access-scoped: an unreadable blocker degrades to a generic
"blocked" label), set / cleared from the to-do create / edit forms via a
blocker picker, and surfaced by a `?blockedOnly=` feed filter. Because
`Status` is a free string, "unblocked" is **not server-decidable** — the
human lifts the wait explicitly (the `ClearBlockedBy` flag). One cycle
guard (self + transitive refusal, the C-M5·7 shape) is the lane's only
server-side refusal.

## Assumptions / decisions (each [PROPOSED], locked by ADR 0087 in U00)

- **D1 — one optional pointer, additive, zero migration.** One
  `string? BlockedByTodoId` on **`TodoItem`** (the ADR 0004 §B.1 / ADR 0086
  `ProjectId` additive shape; existing rows simply have `null`); registered
  on `M5DocTypes` with one `Index` on `(BlockedByTodoId)`. **The singular
  pointer is the design** — `ParentId?` / `GoalId?` / `ProjectId?` are all
  single-optional-FK idioms; a to-do waiting on *three* things uses the
  `ParentId` subtask structure or three to-dos, not a list.
- **D2 — a display chip + a feed filter, never a gate.**
  `BlockedByTodoId` changes **nothing** about who may see the to-do (its
  own `Audience` decision stays the boundary — C-M5·3), **nothing** about
  what may be written (moving a to-do into a `Done`-statused lane, editing
  it, or deleting it **never** clears or blocks on `BlockedByTodoId` — the
  lane limit is M5's one deliberate refusal and stays the only one), and
  **never a hard-delete cascade target**. It is a *hint*, rendered and
  filterable.
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
  0073) / `projectId` (ADR 0086) filter discipline: when `true`, only
  to-dos with `BlockedByTodoId != null` are returned; it **narrows the
  candidate set, never the audience decision**. **`KanbanBoard` gains no
  `BlockedBy`** (a board is a container, not a work item).
- **D7 — the picker.** The to-do create + edit forms seed a `<select>` of
  the actor's readable, non-deleted to-dos (one `ListPickerTodosAsync` read
  lane over the same feed discipline) with a leading "None" option — the
  ADR 0086 D9 project-picker / `SeedComponentPickerAsync` shape; the picker
  is a **display** surface, never a gate (it does not pre-check cycles —
  the service does, D3).
- **D8 — `kw-l` keys.** Four new keys, **× 4 languages** (the
  `KnownTranslationKeys` registry, the ADR 0052 warm-boot baseline backfill
  covers the non-`en` rows): `todo.blocked_by`,
  `todo.blocked_by_none`, `todo.blocked_generic`, `todo.blocked_filter`.
- **D9 — additive on the frozen M5 surface.** No new `AccessAction`, no
  new `AccessVia`, no new `Decide()` branch, no new adapter, no new
  bounded context, no new document; every existing `IProjectService` call
  site compiles and behaves **unchanged** (the ADR 0084 / 0085
  frozen-surface-amendment precedent).
- **Out of scope (→ follow-on lanes, own ADRs):** a `KanbanBoard.BlockedBy`
  field (boards are containers); a **multi-blocker** list / `TodoDependency`
  doc; **auto-clear** of `BlockedByTodoId` when the blocker moves to a
  `Done`-statused lane (the string-status design makes "done" a *label*,
  not a state); a **"who is waiting on this" reverse list** (a
  `ListTodosBlockedByAsync` read lane); a **notification / reply** on the
  blocker when it is completed (a Wolverine side-effect — only once the
  field is actually being used); cross-project / cross-goal dependency
  edges; and any **enforced** dependency (D2).

## Unit register

| Unit | Title | Deliverable shape |
|------|-------|-------------------|
| **U00** | Design doc + **ADR 0087** (sign-off) | `docs/design/tbd-todo-dependency-design.md` (the D1–D9 above, the exact C# seam shapes, the pinned test names, the three-test gate, the drift-guard) + `docs/adr/0087-todo-dependency-blocked-by-lane.md` (the decisions **Accepted** — the status flips from `Proposed` on sign-off) + `docs/adr/README.md` index row (status **Proposed** until sign-off). **No code, no build.** |
| **U01** | `TodoItem.BlockedByTodoId` + `M5DocTypes` index | `src/Kumunita.Core/Projects/TodoItem.cs` (the one additive field, the C-TBD·1 comment discipline); `M5DocTypes.Configure` extended (the one `Index` on `(BlockedByTodoId)` — the ADR 0004 §B.1 delta-detect shape, zero migration); `dotnet build` clean; a `TBD` Core pin (the doc shape compiles + registers + existing M5 / PL pins behave unchanged). |
| **U02** | `IProjectService` additive seams (the write lane + the read surfaces) | `IProjectService` (additive: `ListTodosAsync` gains `blockedOnly`; `TodoDetailResult` gains `Blocker?`; `CreateTodoRequest` / `UpdateTodoRequest` gain the `BlockedByTodoId` / `ClearBlockedBy` fields; `ListPickerTodosAsync` added — the exact C# in the design doc §7.2); `ProjectService` implements (the `blockedOnly` candidate-set narrowing, the chip resolution over the **existing** `TodoItemToAuditableResource`, the C-TBD·3 cycle guard, the C-M5·6 standing re-check, the C3 404-vs-403 split, the `AccessAudit` rows); `ProjectServiceTests` pinned tests (the 11 Core pins, the design doc §7.5). |
| **U03** | Web: the chip + picker + feed toggle | `ProjectsController` (additive: `TodosIndex` reads `?blockedOnly=` — the ADR 0073 `unassignedOnly` precedent, **no new route**; `TodoDetail` resolves the chip; `CreateGet` / `UpdatePost` seed the picker + read the fields); `Models/ProjectViewModels.cs` (the `BlockerChip` view-model + the picker seed shape); `Views/Projects/TodoDetail.cshtml` (the chip — the `kw-l` `todo.*` keys), the board's card partial (the chip), `Create.cshtml` / `Edit.cshtml` (the blocker picker — the `ParentId` `<select>` mirror, the `ClearBlockedBy` hidden field), `TodosIndex.cshtml` (the feed toggle); `KnownTranslationKeys.cs` (the 4 `todo.*` keys × 4 languages); `dotnet build` clean; the `ProjectsControllerTests` pinned tests (the 6 Web pins, the design doc §7.5). |
| **U04** | Close: doc sync + README + handoff notes + the gate | `docs/adr/0087-todo-dependency-blocked-by-lane.md` (the status flips **Proposed → Accepted**; the Date confirmed); `docs/adr/README.md` (the ADR 0087 row, the `Accepted` status); `docs/ARCHITECTURE.md` §5 (the `Projects/` block — the `TodoItem` `BlockedByTodoId` field, the "waiting on" chip + picker + filter — the canonical surface this lane settles; the §3 tree untouched — no new file beyond the field); `README.md` Roadmap (the **`TBD`** lane entry — the one-sentence shape, the ADR 0087 reference, the **Done** marker); `docs/design/tbd-todo-dependency-design.md` (the **gate** section — the three-test record, the handoff pointer); `tbd-handoff-notes.md` (the U00–U04 sections appended, the **gate** section); the lane folder **moved** to `docs/plans-milestones/done/tbd/` (the M5 / PL close-unit precedent). |

## Sequencing invariants (the one-paragraph pin)

- **U00 is the sign-off gate** — it authors the design doc + ADR 0087; every
  later unit codes against the *locked* text (the M5 / PL precedent).
- **The service seams (U02) land before any Web view (U03)** — the Web
  codes against the *frozen* `IProjectService` surface (a rename or
  re-scope of a seam after U00 is a drift event).
- **The close (U04) is last** — the ADR status + doc index + README +
  ARCHITECTURE.md are honest at ship time (the AGENTS.md doc↔code parity
  rule).

## The three-test gate (U04 records)

1. **Closed loop:** a resident creates to-do A, creates to-do B with
   `BlockedByTodoId = A`, and sees the chip "Blocked by: {A's title} —
   {A's status}" on B's detail view + B's board card; the
   `?blockedOnly=true` feed returns B (and any other blocked to-do) but not
   A; selecting "None" on B's edit form clears the association (the
   `ClearBlockedBy` flag).
2. **Handoff:** the `IProjectService` surface (the `ListTodosAsync` /
   `GetTodoAsync` / `CreateTodoAsync` / `UpdateTodoAsync` /
   `ListPickerTodosAsync` seams) is **frozen** — the Web controller (U03)
   codes against it, never re-derives access (the ADR 0006-D pin); the
   standing matrix (creator ∪ assignee ∪ GlobalAdmin) is **re-checked
   server-side** in the write lane (C-TBD·5); the `AccessAudit` row
   (`todo.update`, `TargetKind = "todo"`) is stored in the caller's session
   (C3).
3. **Part-vs-whole:** a to-do's `BlockedByTodoId` (U02 / U03) is a
   **hint, never a gate** (C-TBD·2) — the to-do's own `Audience` decision
   is the access boundary (C-M5·3); moving / editing / soft-deleting the
   to-do **never clears** `BlockedByTodoId` (the C-TBD·2 "never clears"
   rule); the M5 route surface (`/projects/todos` / `/projects/boards`) is
   **untouched** (the C-TBD·6 pin); the chip is **access-scoped** — an
   unreadable blocker degrades to the generic label (the C-TBD·4 pin).

## Drift-guard (the one-paragraph pin)

- **No new `AccessAction`**, **no new `AccessVia`**, **no new `Decide()`
  branch**, **no new adapter** — `TBD` adds one *field + a chip*, not a
  *branch* (the C-M5·11 precedent; the chip reuses the **existing**
  `TodoItemToAuditableResource`).
- **The `BlockedByTodoId` is a hint, never a gate** (C-TBD·2) — it never
  changes the to-do's own `Audience` decision (C-M5·3), never enables or
  blocks a write, never trips a lane limit (C-M5·5), and never cascades a
  hard-delete.
- **The cycle guard is the only refusal** (C-TBD·3) — self + transitive,
  the C-M5·7 cycle-guard shape; clearing to `null` is always allowed.
- **The standing matrix is creator ∪ assignee ∪ GlobalAdmin** (the C-M5·6
  to-do matrix, the `AssignTodoAsync` shape) — re-checked server-side in
  the write lane.
- **The chip is access-scoped** (C-TBD·4) — the blocker's title / status
  are resolved only when the blocker passes its **own** `CanAsync(Read)`;
  an unreadable / absent / soft-deleted blocker degrades to the generic
  label (no title, no link — the C3 404-vs-403 split idiom).
- **`KanbanBoard` gains no `BlockedBy`** (a board is a container, not a
  work item — the D6 pin).
- **The M5 route surface is untouched** (C-TBD·6) — the chip + picker +
  feed toggle are additive on the existing to-do views; the filter is a
  query param on the existing `TodosIndex` route (the ADR 0073
  `unassignedOnly` precedent — **no new route**).
- **`M7` stays `StatusNext` on the roadmap** (the `Milestones.cs` /
  `MilestonesTests.cs` are **untouched** — a named lane, not a milestone,
  the ADR 0013 / 0015 / 0086 precedent).

## Entry reads (the shared list)

- `docs/design/m5-projects-design.md` (the **M5 design doc** — the lane
  template this one builds on; the `TodoItem` shape, the `IProjectService`
  surface, the `TodoItemToAuditableResource` adapter, the `M5DocTypes`
  surface, the standing matrix, the C-M5·4 string-status pin, the C-M5·6
  standing matrix, the C-M5·7 cycle-guard shape).
- `docs/design/pl-goals-projects-design.md` (the **PL design doc** — the
  immediate template: the `ProjectId?` additive-field shape (D4), the
  project-picker / `SeedComponentPickerAsync` shape (D9), the
  frozen-surface-amendment precedent, the unit-register shape, the
  three-test gate, the drift-guard).
- `docs/adr/0067-m5-projects-todos-and-kanban.md` (the ADR this lane
  extends — the bounded context, the `IProjectService` surface, the
  standing matrix; confirm what is frozen + the ADD-is-a-new-ADR rule).
- `docs/adr/0086-goals-projects-lane.md` (the **immediate precedent** — the
  `ProjectId?` additive-field shape this lane's `BlockedByTodoId` mirrors,
  the `ProjectId` index on `M5DocTypes`, the project-picker / feed-filter
  shape, the named-lane + ADR-index row shape).
- `docs/adr/0084-per-target-notification-subscriptions.md` +
  `0085-notification-item-link-and-reply-highlight.md` (the
  **frozen-surface-amendment** precedent this lane's `IProjectService`
  additive seams mirror — the optional-param / optional-field shape, the
  "every existing call site compiles and behaves unchanged" pin).
- `docs/adr/0073-*.md` (the `unassignedOnly` optional-filter precedent this
  lane's `blockedOnly` mirrors — the candidate-set-narrowing discipline,
  the "filter, never a gate" pin).
- `docs/adr/0024-author-soft-delete-lane.md` (the `IsDeleted` shape this
  lane reuses for the chip's degradation rule — the soft-delete flag, the
  dangling-association rule).
- `docs/adr/0006-module-boundary-contracts.md` (the frozen
  `IAuthorizationService` / `IAuditableResource` surface — the chip's
  blocker read plugs into it over the **existing** adapter, no signature
  change; C1–C6).
- `src/Kumunita.Core/Projects/TodoItem.cs` (the **existing** doc shape —
  the `BlockedByTodoId?` field is **additive** on it, next to
  `ParentId` / `ProjectId`, the C-TBD·1 comment discipline).
- `src/Kumunita.Core/Projects/IProjectService.cs` (the **frozen** M5 / PL
  surface — the `blockedOnly` param, the `Blocker?` surface, the
  `BlockedByTodoId` / `ClearBlockedBy` fields, the `ListPickerTodosAsync`
  lane are **additive** on it; the `unassignedOnly` / `projectId` param
  discipline + the `ClearParent` idiom are the immediate templates).
- `src/Kumunita.Core/Projects/ProjectService.cs` (the **frozen** M5
  implementation — the standing re-check, the `AccessAudit` rows, the
  `IAuthorizationService` path, the C-M5·7 cycle-guard shape; the new
  seams mirror it).
- `src/Kumunita.Core/Projects/KanbanStatuses.cs` (the **closed display
  vocabulary** — the chip's status text is the blocker's `Status` string
  rendered verbatim; the "unknown" glyph fallback for free-text values).
- `src/Kumunita.Core/M5DocTypes.cs` (the **existing** surface — the one
  new `TodoItem.BlockedByTodoId` index is **additive** on it; the
  `TodoItem.ProjectId` index is the immediate template).
- `src/Kumunita.Web/Controllers/ProjectsController.cs` (the **existing**
  Web surface — the `TodosIndex` / `TodoDetail` / `CreateGet` /
  `UpdatePost` actions the chip + picker + feed toggle are **additive** on;
  the route surface is untouched).
- `src/Kumunita.Web/Views/Projects/TodoDetail.cshtml` + `Create.cshtml` +
  `Edit.cshtml` + `TodosIndex.cshtml` + the board's card partial (the
  **existing** to-do views — the chip + picker + feed toggle are
  **additive** on them; the `ParentId` `<select>` is the immediate picker
  template).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the **existing**
  `kw-l` key-registry — the 4 `todo.*` keys are **additive** on it, the
  `en` / `de` / `fr` / `da` floors).
- `docs/adr/README.md` (the ADR-numbering convention — confirm the next
  free number is **0087**; the ADR-index row shape the close unit U04
  mirrors).
