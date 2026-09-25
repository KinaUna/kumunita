# `TBD` — Todo dependency ("waiting on") — design (Part 1 + Part 2)

> **Named lane.** `TBD` (short id **`TBD`** = *Todo Blocked-By*) — the
> "waiting on" pointer on the M5 to-do surface: one optional
> `BlockedByTodoId` on `TodoItem`, a display chip, a feed filter, one write
> field + one picker read lane. **ADR 0087** is this lane's decision record —
> **authored in this unit (U00)**, **Accepted 2026-09-25**; every decision
> D1–D9 below is **locked** (the `[PROPOSED]` markers in the lane plan
> `plan-tbd-todo-dependency.md` were the pre-lock shape and are retired by
> this lock — the ADR 0086 precedent). The sealed-unit register is
> `docs/plans-milestones/in-progress/tbd/plan-tbd-todo-dependency.md`; the
> scratch log is
> `docs/plans-milestones/in-progress/tbd/tbd-handoff-notes.md` (one `## U#`
> section per unit, appended, never rewritten).
>
> **Status.** **Part 1 + Part 2 LOCKED.** The decisions D1–D9 are locked in
> **ADR 0087 (Accepted, 2026-09-25)**; **Part 2** (the exact C# shapes, the
> additive `IProjectService` seams, the request DTOs, the `M5DocTypes`
> additive shape, the pinned test names, the three-test acceptance gate, and
> the drift-guard) follows in the "Seams & contracts" section below.
>
> **Roadmap confirm (U00):** `M7` ("Pagination and filtering") is the single
> `StatusNext` on `Milestones.cs` (verified 2026-09-25); `TBD` is a
> **named lane, not a milestone** — `Milestones.cs` / `MilestonesTests.cs`
> are **untouched** (the ADR 0013 / 0015 / 0084 / 0085 / 0086 named-lane
> precedent). The README Roadmap gains one `TBD` lane entry at close (U04).
>
> **Scope of this file:** what this lane is; the existing surface it reuses
> — *verified against the actual files*; the design decisions (D1–D9); the
> invariants (C-TBD·1…6); the FACES (F1–F8); the parts affected; the
> feedback-loop shape; and the risks. **Part 2:** the exact C# shapes, the
> additive `IProjectService` seams, the request DTO fields, the
> `M5DocTypes` additive shape, the pinned test names, the three-test
> acceptance gate, and the drift-guard.

## 1. What this lane is

M5 (ADR 0067) shipped the **work items** — assignable, hierarchical to-dos +
statused boards — and deliberately kept `Status` a *free string* (C-M5·4)
with **no status vocabulary and no status machine**: the board is the state
machine the to-do flows through. PL (ADR 0086) added the higher level
(goals / projects) and the `ProjectId?` association. What neither surface
names is the **"waiting on"** relationship — *this to-do cannot proceed
until that other to-do is done*. Today it is only implicit: a board's lane
*ordering* can imply it, and a `Status` string can be *labelled* to imply
it — but nothing names the *other* to-do, nothing answers "what is waiting",
and a to-do blocked by one on a **different board** has no way to say so at
all.

`TBD` adds exactly that level, inside the **existing**
`Kumunita.Core.Projects` context:

- one **`BlockedByTodoId?`** field on the **existing** `TodoItem` document
  (a *hint, never a gate* — C-M5·3 / C-M3·2 discipline);
- a **"Blocked by: {title} — {status}"** chip on the to-do detail view + the
  board card (access-scoped: an unreadable blocker degrades to a generic
  "blocked" label with no title / link — the 404-vs-403 split idiom);
- a **blocker picker** on the to-do create + edit forms (`<select>` of the
  actor's readable, non-deleted to-dos, the ADR 0086 D9 project-picker /
  `SeedComponentPickerAsync` shape — a *display* surface, never a gate);
- a **`blockedOnly`** feed filter on `ListTodosAsync` (the
  `unassignedOnly` / `projectId` filter discipline — the "what's waiting"
  view a coordinator wants);
- **explicit clearing** via a `ClearBlockedBy` flag (a string status makes
  "unblocked" *undecidable* on the server side, so the human is the only
  one who may lift the wait — D5);
- **one cycle guard** (self + transitive refusal, the C-M5·7 `ParentId`
  cycle-guard shape) — the only server-side *refusal* this lane adds.

**The one thing every unit must respect:** this lane is **additive and
reusing.** It reuses the frozen `IAuthorizationService.CanAsync(Read)` path
(ADR 0006) over the **existing** `TodoItemToAuditableResource` (the chip is
a *read of another to-do*, not a new resource), the standing matrix
**creator ∪ assignee ∪ GlobalAdmin** (the C-M5·6 to-do matrix — the
`AssignTodoAsync` shape), the `kw-l` registry (ADR 0015) for the four new
keys, and the `M5DocTypes` surface (the ADR 0004 §B.1 delta-detect — one new
index, zero migration). **No new `AccessAction`**, **no new `AccessVia`**,
**no new authorization branch**, **no new adapter**, **no new document** —
`TBD` adds one *field + a chip*, not a *branch*. **No code in this unit —
no build, no tests.**

## 2. The existing surface this lane reuses (verified)

Read directly from the actual files (not assumed), each a **frozen seam** TBD
builds on without re-inventing:

1. **`TodoItem`** (`Kumunita.Core/Projects/TodoItem.cs`, M5 / ADR 0067) —
   the **immediate template**: the `ParentId?` single-optional-FK shape
   (D1 mirrors it in intent), the `Status?` free-string field (C-M5·4 — the
   reason "unblocked" is undecidable), the `IsDeleted` soft-delete flag
   (ADR 0024 — the chip-degradation rule), and the exact position the new
   field lands: next to `ParentId` / `ProjectId`, with the same
   "a feed filter, never a gate" comment discipline.
2. **`IProjectService` + `ProjectService`** (`Kumunita.Core/Projects/`) —
   the **frozen surface** this lane amends additively (the ADR 0084 / 0085
   frozen-surface-amendment precedent): `ListTodosAsync` (gains
   `blockedOnly`, the `unassignedOnly` / `projectId` param discipline),
   `GetTodoAsync` → `TodoDetailResult` (gains `Blocker?`),
   `UpdateTodoAsync` (the `ClearParent` idiom the `ClearBlockedBy` flag
   mirrors, the C-M5·7 cycle-guard refusal, the C-M5·6 standing matrix, the
   C3 404-vs-403 split), `CreateTodoAsync` (the verbatim-write shape), and
   the `ListBoardsForTodoAsync` per-parent list (the shape
   `ListPickerTodosAsync` generalizes — one readable, non-deleted, paged
   to-do list over the `TodoItemToAuditableResource`).
3. **`KanbanStatuses`** (`Kumunita.Core/Projects/KanbanStatuses.cs`,
   ADR 0069) — the **closed display vocabulary** (`not-started` /
   `in-progress` / `done` / `cancelled`) that defines the chip's status
   rendering and the board's `<select>`; the stored `Status` stays a plain
   string (a `null` or legacy free-text value still renders, the
   "unknown" glyph fallback) — the chip's status text is **the blocker's
   `Status` string, rendered verbatim**, never re-derived.
4. **`TodoItemToAuditableResource`** (the M5 6-member adapter,
   `TargetKind = "todo"`) — the chip's blocker read routes over it: the
   blocker is *another to-do*, so its `CanAsync(Read)` uses the **existing**
   adapter; **no new adapter** (C-M5·11 — TBD adds no branch).
5. **`KanbanBoard` + `BoardDetailResult` / `LaneDetail`** (M5) — the board
   card's chip is additive on the **existing** `LaneDetail.Cards` rendering
   (`Views/Projects/BoardDetail.cshtml` + the board's card partial) — the
   card already renders the to-do's `Status`; the chip sits beside it.
6. **The to-do create + edit views** (`Views/Projects/Create.cshtml` /
   `Edit.cshtml`) — the **existing** `ParentId` `<select>` (the
   "mirrors its existing `ParentId` select" PL U08 note) is the exact
   picker shape the blocker picker mirrors; the edit form is a
   **partial-update** surface (the `UpdateTodoRequest` shape — each
   non-`null` field applied, `null` = no change *except* the explicit
   `Clear*` flags).
7. **`ProjectsController`** (`Kumunita.Web/Controllers/`) — the **existing**
   M5 controller: the `TodosIndex` / `TodoDetail` / `CreateGet` /
   `CreatePost` / `UpdatePost` actions the picker seed + chip + feed toggle
   are additive on; **no new routes** (the filter is a `?blockedOnly=true`
   query param on the existing `TodosIndex` route — the `unassignedOnly`
   precedent).
8. **`M5DocTypes`** (`Kumunita.Core`) — the existing surface the new
   `TodoItem.BlockedByTodoId` **index** registers on (the ADR 0004 §B.1
   delta-detect shape; the unnamed-computed-index constraint noted there
   applies).
9. **`KnownTranslationKeys.cs`** (`Kumunita.Core.Localization`) — the
   `kw-l` registry (ADR 0015) with the four seeded languages
   (en/de/fr/da) + the ADR 0052 warm-boot baseline backfill that covers new
   keys' `de`/`fr`/`da` rows automatically. The four `todo.*` keys are
   additive on it.
10. **`PostgresFixture`** (`tests/Kumunita.Core.Tests`, Testcontainers
    `postgres:18`) — the harness the Core pinned tests run over;
    `Kumunita.Web.Tests` uses NSubstitute (no Postgres) for the Web pins.

## 3. The design decisions (locked)

Locked by **ADR 0087 (Accepted, 2026-09-25)**; the `[PROPOSED]` markers in
the lane plan are retired by this lock.

### 3.1 One optional pointer, additive, zero migration (D1)

One `string? BlockedByTodoId` on **`TodoItem`** (the ADR 0004 §B.1 / ADR
0086 `ProjectId` additive shape; existing rows simply have `null`);
registered on `M5DocTypes` with one `Index` on `(BlockedByTodoId)`.
**The singular pointer is the design** — `ParentId?` / `GoalId?` /
`ProjectId?` are all single-optional-FK idioms; a to-do waiting on *three*
things uses the `ParentId` subtask structure or three to-dos, not a list.
*(C-TBD·1.)*

### 3.2 A display chip + a feed filter, never a gate (D2)

`BlockedByTodoId` changes **nothing** about who may see the to-do (its own
`Audience` decision stays the boundary — C-M5·3), **nothing** about what
may be written (moving a to-do into a `Done`-statused lane, editing it, or
deleting it **never** clears or blocks on `BlockedByTodoId` — the lane
limit is M5's one deliberate refusal and stays the only one), and **never a
hard-delete cascade target** (the soft-delete of a to-do with `BlockedBy`
set behaves exactly like one without it — the D6 dangling rule). It is a
*hint*, rendered and filterable. *(C-TBD·2.)*

### 3.3 The cycle guard (D3)

Setting `BlockedByTodoId = X` is **refused** when `X == todoItemId` (self)
or when `X` **transitively** reaches `todoItemId` through the
`BlockedByTodoId` chain (visited-set walk; the C-M5·7 `ParentId`
descendant-cycle guard shape — the `InvalidOperationException` refusal,
**nothing written**). Clearing to `null` is always allowed. *(C-TBD·3.)*

### 3.4 The chip (read, access-scoped) (D4)

`GetTodoAsync`'s `TodoDetailResult` gains a `Blocker?`: the blocker's
**title + status** — resolved only when the blocker exists, is **not
soft-deleted**, and passes its **own** `CanAsync(Read)` (the frozen path
over the existing `TodoItemToAuditableResource`); otherwise the chip
renders the generic `todo.blocked_generic` label with **no link and no
title** (the 404-vs-403 split idiom — an unreadable blocker's title/status
are not leaked). The board card + the to-do detail view render the same
chip. **No new adapter, no new `AccessAction`, no new `AccessVia`.**
*(C-TBD·4.)*

### 3.5 The write lane (standing + guard + audit) (D5)

The create form gains an optional blocker picker; the edit lane carries
`BlockedByTodoId?` (a non-null value sets it) + `ClearBlockedBy` (an
explicit un-block — the `ClearParent` idiom, since the field is *always in
the form* the way `ComponentId` / `Status` are). **Standing: creator ∪
assignee ∪ GlobalAdmin over the to-do** (the C-M5·6 matrix, the
`AssignTodoAsync` shape, re-checked server-side); a non-null target that is
absent/soft-deleted or unreadable is **refused** (the C3 404-vs-403 split);
`Modified` stamped on a real change; one `AccessAudit` row
(`todo.update`, `TargetKind = "todo"`). *(C-TBD·5.)*

### 3.6 The `blockedOnly` feed filter (D6)

`ListTodosAsync` gains an optional `blockedOnly` (default `false`) — the
`unassignedOnly` (ADR 0073) / `projectId` (ADR 0086) filter discipline:
when `true`, only to-dos with `BlockedByTodoId != null` are returned; it
**narrows the candidate set, never the audience decision** (a to-do still
only appears if the actor passes the `CanSeeAsync(Read)` pass). **
`KanbanBoard` gains no `BlockedBy`** (a board is a container, not a work
item). *(C-TBD·2.)*

### 3.7 The picker (D7)

The to-do create + edit forms seed a `<select>` of the actor's readable,
non-deleted to-dos (one **`ListPickerTodosAsync`** read lane over the same
feed discipline — paged, `!IsDeleted`, `CanSeeAsync(Read)`-filtered over
the `TodoItemToAuditableResource`) with a leading "None" option — the ADR
0086 D9 project-picker / `SeedComponentPickerAsync` shape; the picker is a
**display** surface, never a gate (it does not pre-check cycles — the
service does, D3). *(C-TBD·4.)*

### 3.8 `kw-l` keys (D8)

Four new keys, **× 4 languages** (the `KnownTranslationKeys.cs` registry;
the ADR 0052 warm-boot baseline backfill covers the non-`en` rows):
`todo.blocked_by` (the chip / form label), `todo.blocked_by_none` (the
picker's "None" option), `todo.blocked_generic` (the chip fallback when the
blocker is absent / soft-deleted / unreadable), and `todo.blocked_filter`
(the feed toggle label).

### 3.9 Additive on the frozen M5 surface (D9)

No new `AccessAction`, no new `AccessVia`, no new `Decide()` branch, no new
adapter, no new bounded context, no new document (the `TodoItem` field is
the whole schema change); every existing `IProjectService` call site
compiles and behaves **unchanged** (the ADR 0084 / 0085
frozen-surface-amendment precedent). *(C-TBD·6.)*

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
(`Project.DependsOnProjectId?` — revisit only if the to-do-level field
proves its weight); and any **enforced** dependency (D2).

## 4. Invariants (pinned for TBD)

- **C-TBD·1 — One optional pointer, additive, zero migration.** One
  `string? BlockedByTodoId` on `TodoItem` (the `ParentId?` / `GoalId?` /
  `ProjectId?` single-optional-FK idiom); the `M5DocTypes` surface gains
  one `Index` on `(BlockedByTodoId)` (ADR 0004 §B.1 delta-detect; existing
  rows simply have `null`). **Not a list, not a new document.**
- **C-TBD·2 — A hint, never a gate.** `BlockedByTodoId` never changes the
  to-do's own `Audience` decision (C-M5·3), never enables or blocks a
  write, never trips a lane limit (C-M5·5), and never cascades a
  hard-delete (the ADR 0024 soft-delete shape). The lane limit is M5's one
  deliberate refusal and stays the only one.
- **C-TBD·3 — The cycle guard is the only refusal.** Setting
  `BlockedByTodoId` to a self- or transitively-reaching target is refused
  (`InvalidOperationException`, nothing written — the C-M5·7 cycle-guard
  shape); clearing to `null` is always allowed.
- **C-TBD·4 — The chip is access-scoped; the picker is a display surface.**
  The blocker's title / status are resolved only when the blocker passes
  its **own** `CanAsync(Read)` (the frozen path over the existing
  `TodoItemToAuditableResource` — no new adapter); an unreadable / absent /
  soft-deleted blocker degrades to the generic `todo.blocked_generic` label
  (no title, no link — the C3 404-vs-403 split idiom). The picker seeds the
  actor's readable to-dos (the ADR 0086 D9 shape) and **never** pre-checks
  cycles (the service does — C-TBD·3).
- **C-TBD·5 — Standing is the to-do's own matrix, re-checked server-side.**
  Creator ∪ assignee ∪ GlobalAdmin (the C-M5·6 matrix, the
  `AssignTodoAsync` shape); the Web `[Authorize]` is a convenience pre-gate
  only; one `AccessAudit` row (`todo.update`, `TargetKind = "todo"`) per
  write.
- **C-TBD·6 — Additive on the frozen M5 surface.** No new `AccessAction`,
  no new `AccessVia`, no new `Decide()` branch, no new adapter, no new
  bounded context, no new document; every existing `IProjectService` call
  site compiles and behaves **unchanged** (the ADR 0084 / 0085
  frozen-surface-amendment precedent; the ADR 0067 frozen-surface rule
  honoured by this ADR).

## 5. FACES (pinned, 8)

- **F1** a to-do with a readable, non-deleted `BlockedByTodoId` target
  renders the chip with the blocker's title + status (a link to the
  blocker's detail) on its detail view and its board card — C-TBD·4
- **F2** a to-do whose blocker is absent, soft-deleted, or unreadable by
  the actor renders the generic `todo.blocked_generic` label — no title,
  no link (the C3 404-vs-403 split idiom) — C-TBD·4
- **F3** setting `BlockedByTodoId` to the to-do's own id, or to a to-do
  that transitively reaches it, is refused (`InvalidOperationException`,
  nothing written) — C-TBD·3
- **F4** `blockedOnly = true` on the to-do feed returns **only** to-dos
  with `BlockedByTodoId != null`, still audience-filtered (a denied to-do
  is dropped, not the whole set) — C-TBD·2
- **F5** the to-do create + edit forms seed a blocker picker (the actor's
  readable, non-deleted to-dos + a "None" option); selecting a target
  writes it, selecting "None" clears it (the `ClearBlockedBy` flag) —
  C-TBD·5
- **F6** a denied actor's `UpdateTodoAsync` (a `BlockedByTodoId` set or
  clear) is refused (the C3 404-vs-403 split); a non-null target at a
  soft-deleted / unreadable to-do is refused (the C3 split) — C-TBD·5
- **F7** moving a to-do into a `Done`-statused lane, editing it, or
  soft-deleting it **never clears** `BlockedByTodoId` (the hint persists —
  the human lifts it explicitly) and **never blocks** the write — C-TBD·2
- **F8** the M5 route surface (`/projects/todos` / `/projects/boards` +
  the existing to-do / board actions) is untouched; the chip + picker +
  feed toggle are additive on the existing to-do views — C-TBD·6

## 6. Parts affected

- **`Kumunita.Core.Projects`** — one additive field: `TodoItem.BlockedByTodoId?`
  (U01); one additive index on `M5DocTypes` (U01).
- **`IProjectService` + `ProjectService`** — additive seams (U02):
  `ListTodosAsync` gains `blockedOnly`; `TodoDetailResult` gains `Blocker?`
  (+ `GetTodoAsync` resolves it); `CreateTodoRequest` / `UpdateTodoRequest`
  gain the `BlockedByTodoId` / `ClearBlockedBy` fields; `ListPickerTodosAsync`
  added (the exact C# in §7).
- **`ProjectsController`** (`Kumunita.Web/Controllers/`) — additive:
  `TodosIndex` reads `?blockedOnly=` (U03); `TodoDetail` / the board card
  render the chip (U03); `CreateGet` / `UpdatePost` seed the picker + read
  the fields (U03). **No new routes** (the ADR 0073 `unassignedOnly`
  precedent — a query param on the existing route).
- **`Views/Projects/`** — additive: `TodoDetail.cshtml` (the chip), the
  board's card partial (the chip), `Create.cshtml` / `Edit.cshtml` (the
  blocker picker), `TodosIndex.cshtml` (the feed toggle).
- **`Models/ProjectViewModels.cs`** (or the existing view-models file —
  U03 settles) — the `BlockerChip` view-model shape (title + status +
  link? or the generic fallback) + the picker seed shape.
- **`KnownTranslationKeys.cs`** — the 4 `todo.*` keys × 4 languages
  (U03, the first view that needs the keys).
- **The test files** — `tests/Kumunita.Core.Tests/ProjectServiceTests.cs`
  (the Core pins appended — U02) +
  `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs` (the Web pins
  appended — U03).

## 7. Part 2 — Seams & contracts (exact shapes)

### 7.1 The `TodoItem` field (additive, U01)

The field lands **next to `ParentId` / `ProjectId`** in
`Kumunita.Core/Projects/TodoItem.cs`, with the same comment discipline:

```csharp
public string? ParentId { get; set; }                       // the sole hierarchy mechanism; `null` = top-level (C-M5·7)
public string? BlockedByTodoId { get; set; }                // the "waiting on" pointer; a hint, never a gate (C-TBD·2 — ADR 0087)
```

The `M5DocTypes` `Configure` gains **one index** (the ADR 0004 §B.1
delta-detect shape; the unnamed-computed-index constraint noted there
applies):

```csharp
// the TBD "waiting on" index (ADR 0087 D1 — additive on M5DocTypes, zero
// migration for existing rows)
db.Schema.For<TodoItem>(x => x.BlockedByTodoId).CreateIndex();
```

(Exact registration syntax per the existing `M5DocTypes` file — the
`TodoItem.ProjectId` index is the immediate template; U01 settles the
exact call.)

### 7.2 The additive `IProjectService` seams (U02)

**`ListTodosAsync`** — gains one optional param (the `unassignedOnly` /
`projectId` param discipline; every existing call site compiles and
behaves unchanged):

```csharp
Task<IReadOnlyList<TodoItem>> ListTodosAsync(
    string? componentId, string? assigneeId, string actorId, int page,
    bool unassignedOnly = false, string? projectId = null,
    bool blockedOnly = false,                       // ADR 0087 D6 — a filter, never a gate (C-TBD·2)
    CancellationToken ct = default);
```

When `blockedOnly` is `true`, the candidate set is narrowed to
`BlockedByTodoId != null` **before** the `CanSeeAsync(Read)` pass (the
`unassignedOnly` shape — it narrows the candidates, it does not change the
audience decision).

**`TodoDetailResult`** — gains the `Blocker?` surface (the exact shape):

```csharp
public sealed record TodoDetailResult
{
    public required TodoItem Todo { get; init; }
    public IReadOnlyList<TodoItem> Subtasks { get; init; } = [];
    public BlockerChip? Blocker { get; init; }        // ADR 0087 D4 — the "waiting on" chip, access-scoped (C-TBD·4)
}

/// <summary>
/// The "waiting on" chip surface (ADR 0087 D4) — the blocker's title +
/// status + detail link, resolved only when the blocker exists, is
/// <c>!IsDeleted</c>, and passes its <c>own</c> <c>CanAsync(Read)</c>
/// (the frozen path over the existing <see cref="TodoItemToAuditableResource"/>
/// — no new adapter). When the blocker is absent / soft-deleted / unreadable,
/// <see cref="Generic"/> is <c>true</c> and <see cref="Title"/> /
/// <see cref="Status"/> / <see cref="LinkPath"/> are <c>null</c> (the C3
/// 404-vs-403 split idiom — an unreadable blocker's title / status are not
/// leaked). The chip is a <b>hint</b> — it is never an access boundary
/// (C-TBD·2 / C-TBD·4).
/// </summary>
public sealed record BlockerChip
{
    public required string TodoId { get; init; }       // the blocker's id (the link target, when !Generic)
    public string? Title { get; init; }                 // the blocker's Title (null when Generic)
    public string? Status { get; init; }                // the blocker's Status string, verbatim (C-M5·4; null = none)
    public string? LinkPath { get; init; }              // /projects/todos/{id} (null when Generic)
    public bool Generic { get; init; }                   // true = render the `todo.blocked_generic` label (no title, no link)
}
```

`GetTodoAsync` resolves `Blocker` **after** the to-do's own `CanAsync(Read)`
decision (the chip is part of the to-do's detail read, not a separate
audit event — the C3 single-aggregate-row shape is preserved; the blocker's
read is an *access decision* within the same session, the
`ListBoardsForTodoAsync` per-parent precedent).

**`CreateTodoRequest`** — gains one optional field (the verbatim-write
shape):

```csharp
public string? BlockedByTodoId { get; init; }           // ADR 0087 D5 — optional on create (the picker's "None" → null)
```

**`UpdateTodoRequest`** — gains two fields (the `ClearParent` idiom — the
field is *always in the form*, the way `ComponentId` / `Status` are):

```csharp
public string? BlockedByTodoId { get; init; }           // ADR 0087 D5 — a non-null value sets it (the C-TBD·3 cycle guard applies)
public bool ClearBlockedBy { get; init; }                // `true` = explicit un-block (sets `BlockedByTodoId = null`)
```

(`BlockedByTodoId == null && !ClearBlockedBy` is a no-op on the
association — the `ParentId` no-op rule.)

**`ListPickerTodosAsync`** — the new read lane (the ADR 0086 D9
project-picker shape; the `ListBoardsForTodoAsync` per-parent precedent
generalized to the full readable to-do set):

```csharp
/// <summary>
/// The **blocker picker** read lane (ADR 0087 D7) — the actor's readable,
/// non-deleted to-dos (the candidates are <c>!IsDeleted</c>; the survivors
/// are <c>CanSeeAsync(Read)</c>-filtered (C6 / C3) over the
/// <see cref="TodoItemToAuditableResource"/>; ordered by <c>Created</c>
/// descending; paged). The to-do's **own** id is **excluded** (the
/// picker's leading "None" option is the form's own, not a row). A
/// **display** surface, never a gate (C-TBD·4) — it does not pre-check
/// cycles (the service does — C-TBD·3).
/// </summary>
Task<IReadOnlyList<TodoItem>> ListPickerTodosAsync(string actorId, int page, CancellationToken ct = default);
```

**`ProjectService`** — the implementation shape (U02):

- `ListTodosAsync` — the `blockedOnly` branch is a candidate-set narrowing
  (`BlockedByTodoId != null`) applied **before** the `CanSeeAsync(Read)`
  pass (the `unassignedOnly` shape).
- `GetTodoAsync` — after the to-do's own `CanAsync(Read)`, resolve the
  blocker: load `BlockedByTodoId` → if `null`, `Blocker = null`; else load
  the blocker's `TodoItem` → if absent / `IsDeleted`, `Blocker = new
  BlockerChip { TodoId = …, Generic = true }`; else `CanAsync(Read)` over
  the blocker → on denial, `Generic = true`; on pass, `Title` / `Status` /
  `LinkPath` populated. **One session, one aggregate `AccessAudit` row**
  (the blocker's read is an *access decision* within the same session, the
  `ListBoardsForTodoAsync` per-parent precedent — not a separate audit
  event).
- `CreateTodoAsync` — the `BlockedByTodoId` is written verbatim (the
  ADR 0001-B shape); a non-null target at an absent / soft-deleted /
  unreadable to-do is **refused** (the C3 split); the C-TBD·3 cycle guard
  applies.
- `UpdateTodoAsync` — the `BlockedByTodoId` / `ClearBlockedBy` fields are
  applied **after** the existing partial-update logic (the `ClearParent`
  idiom); the C-TBD·3 cycle guard applies on a non-null set; the C3 split
  applies on the target; `Modified` stamped on a real change; one
  `AccessAudit` row (`todo.update`, `TargetKind = "todo"`).
- `ListPickerTodosAsync` — the candidate set is `!IsDeleted` **and**
  `Id != actorTodoId` (the to-do's own id excluded — the picker's
  leading "None" is the form's own, not a row); the survivors are
  `CanSeeAsync(Read)`-filtered; ordered by `Created` descending; paged.

### 7.3 The Web shapes (U03)

**`ProjectsController.TodosIndex`** — reads `?blockedOnly=` (the
`unassignedOnly` precedent — a query param on the existing route, **no new
route**):

```csharp
[HttpGet]
public IActionResult TodosIndex(string? componentId, string? assigneeId, bool unassignedOnly = false, string? projectId = null, bool blockedOnly = false, int page = 1)
{
    // … the existing read …
    var todos = await _projects.ListTodosAsync(componentId, assigneeId, actorId, page, unassignedOnly, projectId, blockedOnly);
    // …
}
```

**`ProjectsController.TodoDetail`** — resolves the chip (the `BlockerChip`
view-model is passed to the view):

```csharp
[HttpGet("projects/todos/{id}")]
public async Task<IActionResult> TodoDetail(string id)
{
    // … the existing read …
    var detail = await _projects.GetTodoAsync(id, actorId);
    var vm = new TodoDetailViewModel { Todo = detail.Todo, Subtasks = detail.Subtasks, Blocker = detail.Blocker };
    return View(vm);
}
```

**`ProjectsController.CreateGet` / `UpdatePost`** — seed the picker (the
`SeedComponentPickerAsync` shape):

```csharp
// the blocker picker seed (the ADR 0086 D9 project-picker shape — a display surface, never a gate)
var pickerTodos = await _projects.ListPickerTodosAsync(actorId, page: 1);
vm.BlockerOptions = pickerTodos.Select(t => new PickerOption(t.Id, t.Title));  // the leading "None" is the form's own
```

**`Views/Projects/TodoDetail.cshtml`** — the chip (additive, the `kw-l`
keys):

```html
@* the "waiting on" chip (ADR 0087 D4 — a hint, never a gate) *@
@if (Model.Blocker is not null)
{
    @if (Model.Blocker.Generic)
    {
        <span class="tbd-chip tbd-chip--generic"><kw-l k="todo.blocked_generic" /></span>
    }
    else
    {
        <span class="tbd-chip">
            <kw-l k="todo.blocked_by" />
            <a href="@Model.Blocker.LinkPath">@Model.Blocker.Title</a>
            @if (Model.Blocker.Status is not null) { <span class="tbd-status">@Model.Blocker.Status</span> }
        </span>
    }
}
```

**`Views/Projects/Create.cshtml` / `Edit.cshtml`** — the blocker picker
(the `ParentId` `<select>` mirror):

```html
@* the blocker picker (ADR 0087 D5 — the `ParentId` `<select>` mirror) *@
<div class="mb-3">
    <label for="BlockedByTodoId"><kw-l k="todo.blocked_by" /></label>
    <select name="BlockedByTodoId" id="BlockedByTodoId" class="form-select">
        <option value=""><kw-l k="todo.blocked_by_none" /></option>
        @foreach (var opt in Model.BlockerOptions)
        {
            <option value="@opt.Value" selected="@(opt.Value == Model.BlockedByTodoId)">@opt.Text</option>
        }
    </select>
</div>
@* the edit form's explicit clear (the `ClearParent` idiom — the field is always in the form) *@
<input type="hidden" name="ClearBlockedBy" value="@(Model.BlockedByTodoId is null ? "true" : "false")" />
```

**`Views/Projects/TodosIndex.cshtml`** — the feed toggle (the
`unassignedOnly` toggle mirror):

```html
@* the "blocked only" feed toggle (ADR 0087 D6 — a filter, never a gate) *@
<a class="btn @((bool)ViewData["BlockedOnly"] ? "btn-primary" : "btn-outline-primary")"
   href="/projects/todos?blockedOnly=@(! (bool)ViewData["BlockedOnly"])">
    <kw-l k="todo.blocked_filter" />
</a>
```

### 7.4 `M5DocTypes` additive shape (U01)

The `M5DocTypes.Configure` gains **one index** (the exact call per the
existing file — the `TodoItem.ProjectId` index is the immediate template):

```csharp
// the TBD "waiting on" index (ADR 0087 D1 — additive on M5DocTypes, zero
// migration for existing rows; the ADR 0004 §B.1 delta-detect shape)
db.Schema.For<TodoItem>(x => x.BlockedByTodoId).CreateIndex();
```

### 7.5 The pinned test names

**Core pins** (appended to `tests/Kumunita.Core.Tests/ProjectServiceTests.cs`,
run over `PostgresFixture`):

1. `ListTodos_BlockedOnly_ReturnsOnlyToDosWithBlockedByTodoId` — the
   `blockedOnly = true` candidate-set narrowing (F4).
2. `GetTodo_WithReadableBlocker_ResolvesBlockerChip` — the chip's title /
   status / link resolved (F1).
3. `GetTodo_WithUnreadableBlocker_ReturnsGenericChip` — the
   `Generic = true` fallback, no title / link leaked (F2).
4. `GetTodo_WithSoftDeletedBlocker_ReturnsGenericChip` — the
   `IsDeleted` blocker → `Generic = true` (F2).
5. `UpdateTodo_SetBlockedBy_SelfRefused` — the C-TBD·3 self-cycle guard
   (F3).
6. `UpdateTodo_SetBlockedBy_TransitiveRefused` — the C-TBD·3 transitive
   cycle guard (F3).
7. `UpdateTodo_ClearBlockedBy_Allowed` — the explicit un-block (F5).
8. `UpdateTodo_DeniedActor_Refused` — the C-TBD·5 standing re-check (F6).
9. `UpdateTodo_NonNullTargetAtSoftDeletedRefused` — the C3 404-vs-403
   split on the target (F6).
10. `MoveTodoToDoneLane_NeverClearsBlockedByTodoId` — the C-TBD·2
    "never clears" rule (F7).
11. `DeleteTodo_WithBlockedBy_SoftDeleteUnchanged` — the C-TBD·2
    "never cascades" rule (F7).

**Web pins** (appended to
`tests/Kumunita.Web.Tests/ProjectsControllerTests.cs`, NSubstitute, no
Postgres):

1. `TodosIndex_BlockedOnlyTrue_PassesFilterToService` — the query param
   read + the service call (F4).
2. `TodoDetail_RendersBlockerChip_WhenReadable` — the chip's title /
   status / link rendered (F1).
3. `TodoDetail_RendersGenericChip_WhenUnreadable` — the
   `todo.blocked_generic` label, no title / link (F2).
4. `CreateGet_SeedBlockerPicker` — the `ListPickerTodosAsync` seed + the
   "None" option (F5).
5. `UpdatePost_BlockedByTodoId_PassesFieldToService` — the write field
   read + the service call (F5).
6. `UpdatePost_ClearBlockedBy_PassesFlagToService` — the explicit
   un-block flag read + the service call (F5).

### 7.6 The three-test gate (U04 records)

1. **Closed loop:** a resident creates to-do A, creates to-do B with
   `BlockedByTodoId = A`, and sees the chip "Blocked by: {A's title} —
   {A's status}" on B's detail view + B's board card; the `?blockedOnly=true`
   feed returns B (and any other blocked to-do) but not A; selecting "None"
   on B's edit form clears the association (the `ClearBlockedBy` flag).
2. **Handoff:** the `IProjectService` surface (the `ListTodosAsync` /
   `GetTodoAsync` / `CreateTodoAsync` / `UpdateTodoAsync` /
   `ListPickerTodosAsync` seams) is **frozen** — the Web controller
   (U03) codes against it, never re-derives access (the ADR 0006-D pin);
   the standing matrix (creator ∪ assignee ∪ GlobalAdmin) is
   **re-checked server-side** in the write lane (C-TBD·5); the
   `AccessAudit` row (`todo.update`, `TargetKind = "todo"`) is stored in
   the caller's session (C3).
3. **Part-vs-whole:** a to-do's `BlockedByTodoId` (U02 / U03) is a
   **hint, never a gate** (C-TBD·2) — the to-do's own `Audience` decision
   is the access boundary (C-M5·3); moving / editing / soft-deleting the
   to-do **never clears** `BlockedByTodoId` (the C-TBD·2 "never clears"
   rule); the M5 route surface (`/projects/todos` / `/projects/boards`)
   is **untouched** (the C-TBD·6 pin); the chip is **access-scoped** — an
   unreadable blocker degrades to the generic label (the C-TBD·4 pin).

**Recorded at close (U04, 2026-09-25) — all three PASS**, confirmed by
reading the named handoff sections + the code (the full record is in
`docs/plans-milestones/done/tbd/tbd-handoff-notes.md`, `## U04`):

- **Closed loop — PASS.** U01 adds `TodoItem.BlockedByTodoId?` + the
  `M5DocTypes` index; U02 lands the seams + the cycle guard + the
  `BlockerChip` (11 Core pins); U03 wires the chip on `TodoDetail.cshtml`,
  the picker on `Create.cshtml` / `Edit.cshtml`, the `?blockedOnly=true`
  toggle on `TodosIndex.cshtml`, the `None`→`ClearBlockedBy` clear, and the
  four `todo.*` `kw-l` keys × 4 languages (6 Web pins).
- **Handoff — PASS.** The `IProjectService` surface is frozen (U03's
  `ProjectsController` codes against the U02 seams — `ListTodosAsync` /
  `GetTodoAsync` / `CreateTodoAsync` / `UpdateTodoAsync` /
  `ListPickerTodosAsync`; no access re-derivation, the ADR 0006-D pin); the
  standing matrix (creator ∪ assignee ∪ GlobalAdmin) is re-checked
  server-side in the `UpdateTodoAsync` / `CreateTodoAsync` write lane
  (C-TBD·5); the `AccessAudit` row (`todo.update`, `TargetKind = "todo"`)
  is stored in the caller's session (C3).
- **Part-vs-whole — PASS.** `BlockedByTodoId` is a hint, never a gate
  (C-TBD·2); the `CreateTodoRequest` / `UpdateTodoRequest` DTOs gain only
  the `BlockedByTodoId` field (+ the `ClearBlockedBy` flag) — no gate field;
  the M5 route surface (`/projects/todos` / `/projects/boards`) is untouched
  (C-TBD·6); the chip is access-scoped (C-TBD·4); `Milestones.cs` /
  `MilestonesTests.cs` are **untouched** (`M7` stays the single in-progress
  milestone).

**Gate run (AGENTS.md test-runner quirk — build then in-process, not
`dotnet test` / VS Test Explorer):**

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

Result: build clean (0 errors); `Kumunita.Web.Tests` — **Total: 453,
Failed: 0**; `Kumunita.Core.Tests` — **Total: 830, Failed: 0**
(in-process xunit.v3; Core via Testcontainers `postgres:18`).

## 8. Drift-guard

- **C-TBD·6 — additive on the frozen M5 surface.** No new `AccessAction`,
  no new `AccessVia`, no new `Decide()` branch, no new adapter, no new
  bounded context, no new document (the `TodoItem` field is the whole
  schema change); `TBD` adds one *field + a chip*, not a *branch* (the
  C-M5·11 precedent; the chip reuses the **existing**
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

## 9. Risks

- **The "waiting on" relationship is a *human* concept, not a *state*
  concept.** The string-status design (C-M5·4) makes "unblocked"
  undecidable on the server side — the lane's only mitigation is the
  explicit `ClearBlockedBy` flag (D5) + the chip's persistence (F7). A
  follow-on lane (the ADR 0087 "Out of scope" note) may add an
  auto-clear when the blocker moves to a `Done`-statused lane, but only
  if the field is actually being used — the string-status pin is
  deliberate (the D5 refusal).
- **The cycle guard is the only server-side *refusal* this lane adds.**
  It is the C-M5·7 `ParentId` guard shape (visited-set walk,
  `InvalidOperationException`, nothing written) — the only place the lane
  deviates from the "never a gate" discipline, and it is a *write*
  refusal, not an *access* or *state* gate (C-TBD·3).
- **The picker is a *display* surface, never a gate.** It seeds the
  actor's readable to-dos (the ADR 0086 D9 shape) and **never** pre-checks
  cycles (the service does — C-TBD·3); a to-do that *appears* in the
  picker may still be refused by the C-TBD·3 guard (a self- or
  transitive-reach target) — the form's error message is the
  `InvalidOperationException` → the controller's 400 shape (the M5
  write-refusal precedent).
- **The chip's `Blocker` resolution is an *access decision* within the
  same session, not a separate audit event.** The C3 single-aggregate-row
  shape is preserved (the blocker's read is within the to-do's detail
  read, the `ListBoardsForTodoAsync` per-parent precedent) — a separate
  `AccessAudit` row for the blocker would double-count the to-do's own
  read.
- **The `blockedOnly` filter is a *candidate-set* narrowing, not an
  *audience* change.** The `CanSeeAsync(Read)` pass still applies (the
  `unassignedOnly` / `projectId` discipline) — a to-do that is blocked
  *but denied* is dropped, not the whole set (F4).
