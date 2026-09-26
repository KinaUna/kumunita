# M5 — Projects (to-dos + Kanban boards) — design (Part 1)

> **Milestone.** `M5` — the **outcome** arrow (the `M0`–`M6` roadmap letters
> stay fixed: M4 is shipped — Events, ADR 0054; M6 stays Portability).
> **ADR 0067** is this milestone's decision record — **not yet authored**;
> this unit (U00) authors Part 1 only, and every decision below is marked
> **[PROPOSED]** until **U01** locks it into ADR 0067.
>
> **Status.** **Part 1 (U00) [PROPOSED → LOCKED]; Part 2 (U01) Locked.** The
> decisions D1–D12 (+ D8a) are locked in **ADR 0067 (Accepted, 2026-09-23)**;
> **Part 2** (the exact C# shapes, the full `IProjectService` surface, the
> 16 + 8 pinned test names, the three-test acceptance gate, and the
> drift-guard) follows in the "Seams & contracts" section below. The
> sealed-unit register is `docs/plans-milestones/plan-m5-projects.md`; the
> scratch log is
> `docs/plans-milestones/done/projects/projects-handoff-notes.md`.
>
> **Scope of this file (Part 1):** what this milestone is; the existing
> surface it reuses — *verified against the actual files*; the design
> decisions (each **[PROPOSED]**); the 11 invariants (C-M5·1…11); the 10
> FACES (F1–F10); the parts affected; the feedback-loop shape; and the risks.
> **Not in this file (U01 owns Part 2):** the exact POCO field sets, the exact
> `IProjectService` signatures, the pinned test names, the gate, and the
> drift-guard.

## 1. What this milestone is

The next arrow — **coordination → outcome**: a decision becomes *owned work
with a shape*. M3 shipped **signal** (posts, replies, announcements, group
posts, moderation); M4 shipped **coordination** (events, RSVPs, reminders).
M5 delivers the neighborhood's **project management**: assignable,
hierarchical **to-dos** and **Kanban boards** with statused, limitable
lanes. It is a **new bounded context** `Kumunita.Core.Projects` (the `Projects/`
folder already sketched in `docs/ARCHITECTURE.md` §3, marked "not yet
created"), with **four documents**, **one new document surface**
(`M5DocTypes`), **one new service seam** (`IProjectService`), **two new
authorization adapters** (`TodoItemToAuditableResource`,
`KanbanBoardToAuditableResource`), **one new Web controller**
(`ProjectsController` + views + the nav entry), and **one new plain-TS
module** (`client/lib/projects-board.ts`) for the board's reorder / move /
copy / assign / delete actions.

The defining move of the lane: **a to-do is the work item; a placement is
where it sits on a board.** A `TodoItem` is a **standalone, assignable,
hierarchical** document (a title, an optional rich body, a status, an
assignee, subtasks, its own `Audience`) that is *also* **placeable** — on
zero, one, or many Kanban boards. Its position (which lane, and its order
within that lane) is a **separate `BoardItemPlacement` record**, not a field
on the to-do. A board is **laned** — each `KanbanLane` carries an optional
`MaxItems` limit and an optional `Status`; **moving a to-do into a statused
lane auto-updates the to-do's status** (the board is the state machine the
to-do flows through), and a lane at its limit **refuses** a further move /
copy into it. Standing over a to-do or board is **creator ∪ assignee ∪
GlobalAdmin**, re-checked server-side (the ADR 0014 / 0016 / 0017 precedent
extended with the assignee as a collaborator). Read is the to-do's / board's
**own** `Audience` decision, and a to-do rendered on a board is visible only
if **both** the board and the to-do are visible to the actor.

**The one thing every unit must respect:** this milestone is **additive and
reusing.** It reuses the `Audience` doc (ADR 0001-B / 0036 — *reused*, not
extended) on **both** `TodoItem` and `KanbanBoard`, the frozen
`IAuthorizationService.CanAsync(Read)` / `CanSeeAsync(Read)` decision path
(ADR 0006) through the two new adapters, the standing matrix (ADR 0014 /
0016 / 0017), the `MarkdownRenderer` + `bindRichEditor` (ADR 0025 / 0031) for
an optional to-do body, the `kw-dt` TagHelper (ADR 0019 / 0020) for
timestamps, and the existing profile-avatar surface for the assignee chip.
**No new `AccessAction`**, **no new `AccessVia`**, **no new authorization
path**, **no editor dependency**, **zero migrations for new fields** (ADR
0004 §B.1 additive — the four docs are new, the surface is additive). **M5 is
opened as the *next* lane** (it is already the single `StatusNext` on the
roadmap — U01 confirms, not moves); **M6 stays Portability.**

## 2. The existing surface this milestone reuses (verified)

Read directly from the actual files (not assumed), each a **frozen seam** M5
builds on without re-inventing:

1. **`Post` / `PostReply` + `PostToAuditableResource` + `PostService`**
   (`Kumunita.Core.Posts`, M3) — the `IAuditableResource` adapter shape M5
   mirrors verbatim: the same **6-member** surface (`Id` / `Name` /
   `OwnerId` / `Audience` / `ComponentId` / `TargetKind`, in
   `Authorization/AccessAction.cs`), the `Name = Title ?? Body[..60]`
   (57 + "…") truncation idiom, the audience written verbatim (ADR 0001-B).
   `Post.Audience` is the **`Audience` doc** shape both M5 documents reuse
   (`null` = public — the frozen `Decide()` branch 5); `Post` is the
   `ParentId`-free content-document precedent.
2. **`Announcement` + `AnnouncementService`** (M3b) — the
   **author-of-record ∪ GlobalAdmin** standing matrix (ADR 0017), the
   `CreateAsync` C3 pattern (standing re-checked server-side, the Web
   `[Authorize]` is a convenience pre-gate only), and the 404-vs-403 split
   (`KeyNotFoundException` / `UnauthorizedAccessException`) — the
   `IProjectService` write lanes mirror this.
3. **`Audience`** (`Authorization/Audience.cs`) — `Mode` + `Grants` + the
   ADR 0036 `Community` branch; **`null` = public**. The
   `AudienceEditorModel` + `BuildAudience()` single-source form surface (M2,
   reused by every composer) is the to-do / board composer's audience picker.
4. **`MarkdownRenderer` + `bindRichEditor`** (RC / RE, ADR 0025 / 0031 /
   0033) — the one renderer on the read path, the one editor on the write
   path. The optional to-do `Body` is rendered by the one `MarkdownRenderer`
   and edited by the one `bindRichEditor` — no new editor, no new TS editor
   module.
5. **`Event` + `EventRsvp` + `EventService` + `M4DocTypes`**
   (`Kumunita.Core.Events`, M4) — the **immediate template**: the
   field-by-field provenance table (each field reuses an existing idiom),
   the `EventRsvp` second-document shape with a **business-key unique index**
   (the model for `BoardItemPlacement`'s `(TodoItemId, BoardId)` identity),
   the standing matrix (author ∪ GlobalAdmin), the adapter, and the
   `M4DocTypes` surface — the pattern `M5DocTypes` mirrors verbatim.
6. **`kw-dt` TagHelper** (ADR 0019 / 0020) — the one timestamp renderer:
   per-request resolver (resident override → platform default → `UTC` floor)
   + format (resident override → platform default → Long floor). Every M5
   timestamp (`TodoItem.Created` / `Modified?`, `KanbanBoard.Created`)
   renders through it.
7. **The profile-avatar surface** (the M2 directory) — the existing
   avatar / profile lookup the assignee chip reuses for a to-do's
   `AssigneeId`.
8. **`client/lib/api.ts`** (the M2/M3 precedent) — the CSRF-aware `fetch`
   shape (the M3 group-posts / the M4 board interactions already rely on
   it); the new `client/lib/projects-board.ts` reuses it for the explicit
   POST round trips. **The `tsc`-only / no-editor-dependency constraint
   stands unchanged** (ADR 0031 § Context: "no editor dependency enters
   `package.json`"; `client/lib` is plain-TS, tsc-only — confirmed in
   `src/Kumunita.Web/package.json`: `build` = `tsc`, the only devDependency
   is `typescript`).
9. **The `kw-l` key-registry shape** (ADR 0015) — the `KnownTranslationKeys`
   host for the new UI strings; the four seeded languages (en/de/fr/da, the
   ADR 0042 / 0045 set).
10. **`Program.cs` + `SchemaBootstrap.cs`** (`Kumunita.Web`) — the **two
    boot paths** the `M5DocTypes` surface and the `IProjectService`
    registration are wired into (U02). The per-feature registration shape in
    `Kumunita.Core/DependencyInjection.cs` ("add transient with the store
    injected") is what the `IProjectService` registration follows.
11. **`Kumunita.Core.Tests/PostgresFixture.cs`** — the `PostgresFixture`
    test harness (Testcontainers `postgres:18`) that the ~16 Core pinned
    tests (U12) use; `Kumunita.Web.Tests` uses NSubstitute (no Postgres) for
    the ~8 Web pins.

## 3. The design decisions

Each is **[PROPOSED]** in this unit (U00); **U01** locks them into **ADR
0067** and gives them exact C# shapes in Part 2.

### 3.1 One bounded context, four documents, one surface, additive (D1)
**[PROPOSED]**

`Kumunita.Core.Projects` with four documents — `TodoItem`, `KanbanBoard`,
`KanbanLane`, `BoardItemPlacement` — and **one new document surface**
`M5DocTypes` (the `M4DocTypes` pattern verbatim: the `M3DocTypes` / `M4DocTypes`
precedent). The four are Marten-native POCOs with conventional `string` `Id`,
delta-detected + idempotent, no seeding, no EF. **Zero migrations for
existing surfaces** — the four docs are new, the surface is additive (ADR
0004 §B.1). *(C-M5·1.)*

### 3.2 The canonical names (D2)
**[PROPOSED]**

`docs/ARCHITECTURE.md` §5 sketches the context as `Project { id, title,
description, componentId?, ownerId, status, audience, created }`,
`ProjectTask { id, projectId, title, assigneeId?, done, order }`,
`ProjectMember { id, projectId, userId, role }`. This lane delivers the
*same intent* under the user's framing with these **canonical names**:
**`TodoItem`** (the to-do / work item), **`KanbanBoard`** (the board),
**`KanbanLane`** (a board's column), **`BoardItemPlacement`** (a to-do's
presence on a board / lane). **`Project` / `ProjectTask` /
`ProjectMember` do not exist** — the §5 sketch is the pre-decision shape;
**"project" is not a distinct document in M5**: a to-do is the unit, a board
is the container. **The M5 close unit (U13) rewrites the ARCHITECTURE.md §5
`Projects` block + the §3 tree to these names** (per the AGENTS.md
doc↔code parity rule), so no unit in U02–U12 touches that file. *(C-M5·1.)*

### 3.3 A to-do is standalone; placement is a separate record (D3)
**[PROPOSED]**

`TodoItem` carries **no** `BoardId` / `LaneId` / `Order` field. Its presence
on a board (and its position) is a **`BoardItemPlacement`** row
(`TodoItemId`, `BoardId`, `LaneId`, `Order`). A to-do may have **zero**
placements (a pure to-do-list item) or **many** (on several boards). The
actions map onto the placement: **move up / down** = reorder within a lane
(change `Order` among that lane's placements); **move left / right** = move
to the adjacent lane of the *same* board (change `LaneId`, reset `Order` to
the end); **move to (board)** = the placement relocates (removed from the
source board, added to the target board's first lane); **copy to (board)** =
a new `TodoItem` copy + a placement on the target board (the original is
untouched). *(C-M5·2.)*

### 3.4 Read is the to-do's own `Audience`; the board is a container (D4)
**[PROPOSED]**

`TodoItem` and `KanbanBoard` each carry the **exact** post `Audience` (ADR
0001-B / 0036; `null` = public) and each has its **own** adapter:
`TodoItemToAuditableResource` (`TargetKind "todo"`) and
`KanbanBoardToAuditableResource` (`TargetKind "board"`) — the
`PostToAuditableResource` 6-member shape verbatim. A **standalone** to-do
list / detail is gated by the to-do's own `Audience` decision (one
`CanSeeAsync(Read)` over the list — the C-M3·3 aggregate shape; one
`CanAsync(Read)` on the detail — the C3 404-vs-403 split). A **board** view
is gated by the board's `Audience` **first** (entry), then **each card** by
the to-do's own `Audience` (a to-do on the board is visible iff **both** the
board and the to-do are visible to the actor). A **`BoardItemPlacement` is
not itself an auditable resource** — it inherits the visibility of the to-do
it points at (no separate `BoardItemPlacementToAuditableResource`). *(C-M5·3.)*

### 3.5 Status is a string, not an enum (D5)
**[PROPOSED]**

`TodoItem.Status` and `KanbanLane.Status` are **nullable strings** (the state
label; `null` = no status / a lane that imparts no status). **No status
enum, no status registry, no seeded status vocabulary** — the lane's
vocabulary is whatever the board's lanes name (a board author names their
lanes "Planned" / "Doing" / "Done" and sets each lane's optional `Status`
text). This keeps the model data-driven and avoids a second
frozen-vocabulary surface. Moving a to-do into a lane whose `Status` is
non-null sets `TodoItem.Status` to that lane's status, **in the same
transaction** as the placement move (audited — C3); a lane with null `Status`
leaves the to-do's status unchanged. **The status icon is a client-side
*display* mapping** from the status string to an icon — never persisted
(C-M5·9). *(C-M5·4.)*

### 3.6 Lane limits are advisory backstops, refused not dropped (D6)
**[PROPOSED]**

`KanbanLane.MaxItems?` (nullable int; `null` = no limit) caps the number of
placements a lane may hold. A move / copy **into** a lane already at its
limit is **refused** (the actor sees a message; **nothing is written**). A
**reorder within** a lane, and a move *out of* a lane, never trip the limit
on the source. The limit counts **placements in that lane** (a to-do placed
on two boards is counted once per board's lane). *(C-M5·5.)*

### 3.7 Hierarchy is `ParentId` only; a subtask is a full to-do (D7)
**[PROPOSED]**

`TodoItem.ParentId?` is the **sole** hierarchy mechanism — a subtask is a
`TodoItem` with `ParentId` set, with its **own** status / assignee /
placements (a subtask can sit on a board just like a top-level to-do).
**Delete cascades:** soft-deleting a to-do also soft-deletes its
**descendant subtree** (the to-dos transitively reachable via `ParentId`).
**A to-do with children cannot be re-parented to create a cycle** — the
service refuses a `ParentId` that would make the target a descendant of the
to-do. Subtasks are listed under their parent in the to-do detail view and
may also appear as their own cards on a board (placement is orthogonal to
hierarchy). *(C-M5·7.)*

### 3.8 Standing is creator ∪ assignee ∪ GlobalAdmin, re-checked server-side (D8)
**[PROPOSED]**

The Web `[Authorize]` is a convenience pre-gate only, **never** the source of
truth; `IProjectService` enforces the matrix for **every mutating lane**
(edit title / status / assignee, add subtask, reorder, move, copy, delete,
and lane CRUD) — the ADR 0014 / 0016 / 0017 precedent, re-checked
server-side. The **creator** is `TodoItem.AuthorId` /
`KanbanBoard.AuthorId` (the standing owner); the **assignee** is
`TodoItem.AssigneeId` (a collaborator — assigning a to-do to a resident
gives that resident standing over it); **GlobalAdmin** is the override
branch. `TodoItem.AssigneeId` is a **`SubjectId`** (nullable — `null` =
unassigned); it is *display + standing*, **never an access gate** (read is
the `Audience` decision — D4). *(C-M5·6.)*

### 3.8a No draft lane (D8a)
**[PROPOSED]**

The ADR 0037 `IsDraft` pin is **out of scope for M5**. A to-do is
**published on creation** — the create write lane does not set a draft flag,
and `TodoItem` has **no `IsDraft` field**. The ADR 0037 author-only draft +
`/my/drafts` lane is a **follow-on lane** (own ADR), not part of M5.
`KanbanBoard` likewise has no `IsDraft` (a board is live on creation; the
`/my/drafts` lane for boards is out of scope). *(Part of C-M5·11 — no new
mechanism.)*

### 3.9 Copy-to duplicates; move-to relocates (D9)
**[PROPOSED]**

**"Copy to (board)"** = a new `TodoItem` copy (title / status / assignee) + a
placement on the target board's first lane; **the original is untouched**.
**"Move to (board)"** = the placement is removed from the source board and
added to the target board's first lane (the to-do is then on the target
board, not both). *(C-M5·8.)*

### 3.10 Plain TS, zero deps, explicit server round trips (D10)
**[PROPOSED]**

tsc-only (ADR 0031); **no UI library, no bundler**;
`client/lib/projects-board.ts` is the **one** new module. Every action
(reorder / move / copy / assign / delete) is an **explicit POST**,
CSRF-aware (the `client/lib/api.ts` shape), then a **full re-render** —
**no** optimistic local reordering, **no** HTML5 drag events (keyboard /
menu driven — the ADR 0031 "explicit, server-authoritative" posture). The
status icon + assignee avatar + dropdown menu are plain DOM / JS. *(C-M5·9.)*

### 3.11 Localization parity (D11)
**[PROPOSED]**

The new `kw-l` keys (the to-do / board / lane nouns, the status-icon
fallback, the dropdown-action labels — move up / down / left / right, copy
to, move to, assign, delete; the lane "limit" + "status" labels; the board
empty-state) are present in **all four** seeded languages (en/de/fr/da —
the ADR 0042 / 0045 set); English is the fallback in every view; the ADR
0015 key-registry shape is the host. *(C-M5·10.)*

### 3.12 Reuse, don't reinvent (D12)
**[PROPOSED]**

The `Audience` doc, the frozen `IAuthorizationService`, the standing matrix,
the `MarkdownRenderer` + `bindRichEditor`, the `kw-dt` TagHelper, and the
profile-avatar surface are all **frozen seams** — M5 adds **adapters + a
service + views + one TS module**, not a branch. **No new `AccessAction`**,
**no new `AccessVia`**, **no new authorization path**, **no editor
dependency**. *(C-M5·11.)*

## 4. Invariants (pinned for M5)

- **C-M5·1 — One bounded context, four documents, one surface, additive.**
  `Kumunita.Core.Projects` with `TodoItem`, `KanbanBoard`, `KanbanLane`,
  `BoardItemPlacement`; one new surface `M5DocTypes` (the `M4DocTypes`
  pattern verbatim); Marten-native POCOs, conventional `string` `Id`,
  delta-detected + idempotent, no seeding, no EF, zero migrations for
  existing surfaces (ADR 0004 §B.1).
- **C-M5·2 — A to-do is standalone; placement is a separate record.**
  `TodoItem` carries **no** `BoardId` / `LaneId` / `Order`; its presence on a
  board (and its position) is a `BoardItemPlacement` row. A to-do has zero or
  many placements (on several boards). Move up/down = reorder within a lane;
  move left/right = change lane (same board); move-to = relocate the
  placement; copy-to = duplicate the to-do + place the copy.
- **C-M5·3 — Read is the to-do's own `Audience`; the board is a container.**
  `TodoItem` and `KanbanBoard` each carry the **exact** post `Audience`
  (ADR 0001-B / 0036; `null` = public) and each has its own adapter
  (`TargetKind "todo"` / `"board"`). A standalone to-do list / detail is
  gated by the to-do's `Audience` (C-M3·3 aggregate / C3 404-vs-403). A board
  view is gated by the board's `Audience` (entry), then each card by the
  to-do's own `Audience` (visible iff **both**). A `BoardItemPlacement` is
  not itself an auditable resource (it inherits its to-do's decision).
- **C-M5·4 — Lane status auto-update is a write, audited, the single status
  mechanism.** Status is a **nullable string** on `TodoItem` and
  `KanbanLane` (no enum, no registry). Moving a to-do to a lane whose
  `Status` is non-null sets `TodoItem.Status` to that lane's status, in the
  same transaction as the placement move (C3). A lane with null `Status`
  leaves the to-do's status unchanged. The **status icon is a client-side
  display** mapping (C-M5·9), never persisted.
- **C-M5·5 — Lane limits are advisory backstops, refused not dropped.**
  `KanbanLane.MaxItems?` (`null` = no limit) caps the placements a lane
  holds; a move/copy **into** a lane at its limit is **refused** (a message;
  nothing written). Reorder within a lane and move *out* never trip the
  source limit. The limit counts placements in that lane.
- **C-M5·6 — Standing is creator ∪ assignee ∪ GlobalAdmin, re-checked
  server-side.** The Web `[Authorize]` is a convenience pre-gate only;
  `IProjectService` enforces the matrix for every mutating lane (the ADR
  0014 / 0016 / 0017 precedent). The creator is `AuthorId` (the standing
  owner); the assignee is `AssigneeId` (a collaborator — assignment grants
  standing); GlobalAdmin is the override branch. `AssigneeId` is display +
  standing, **never an access gate** (read is the `Audience` decision —
  C-M5·3).
- **C-M5·7 — Hierarchy is `ParentId` only; a subtask is a full to-do.**
  `TodoItem.ParentId?` is the sole hierarchy mechanism; a subtask has its own
  status / assignee / placements. **Delete cascades** to the descendant
  subtree. A `ParentId` that would create a cycle is refused. Subtasks list
  under their parent and may also appear as their own cards.
- **C-M5·8 — Copy-to duplicates; move-to relocates.** "Copy to (board)" = a
  new `TodoItem` copy (title / status / assignee) + a placement on the target
  board's first lane; the original is untouched. "Move to (board)" = the
  placement is removed from the source board and added to the target board's
  first lane (the to-do is then on the target board, not both).
- **C-M5·9 — Plain TS, zero deps, explicit server round trips.** tsc-only
  (ADR 0031); **no UI library, no bundler**; `client/lib/projects-board.ts`
  is the one new module. Every action (reorder / move / copy / assign /
  delete) is an **explicit POST**, CSRF-aware (the `client/lib/api.ts`
  shape), then a full re-render — **no** optimistic local reordering, **no**
  HTML5 drag events (keyboard / menu driven). The status icon + assignee
  avatar + dropdown are plain DOM / JS.
- **C-M5·10 — Localization parity.** The new `kw-l` keys (to-do / board /
  lane nouns, the action labels, the lane limit / status labels, the board
  empty-state) are present in **all four** seeded languages (en/de/fr/da);
  English is the fallback in every view; the ADR 0015 key-registry shape is
  the host.
- **C-M5·11 — Reuse, don't reinvent.** The `Audience` doc, the frozen
  `IAuthorizationService`, the standing matrix, the `MarkdownRenderer` +
  `bindRichEditor`, the `kw-dt` TagHelper, the profile-avatar surface are all
  **frozen seams** — M5 adds **adapters + a service + views + one TS
  module**, not a branch. **No new `AccessAction`**, **no new `AccessVia`**,
  **no new authorization path**, **no editor dependency**.

## 5. FACES (pinned, 10)

- **F1** a standalone to-do is visible to its audience member, hidden from a
  non-member — C-M5·3
- **F2** a to-do can be placed on several boards; it appears on each — C-M5·2
- **F3** a board is gated by its own `Audience`; a to-do on the board is
  visible iff **both** the board and the to-do are visible — C-M5·3
- **F4** moving a to-do into a lane with a `Status` sets the to-do's status
  (auto-update, audited) — C-M5·4
- **F5** moving a to-do into a lane with null `Status` leaves its status
  unchanged — C-M5·4
- **F6** a lane at its `MaxItems` limit **refuses** a further move/copy into
  it (below the limit it accepts) — C-M5·5
- **F7** the assignee has standing over an assigned to-do (creator ∪ assignee
  ∪ GlobalAdmin) — C-M5·6
- **F8** a non-creator, non-assignee, non-GlobalAdmin has **no** standing to
  mutate a to-do — C-M5·6
- **F9** a subtask is a full to-do (own status / assignee / placement) shown
  under its parent; deleting a to-do cascades to its subtree — C-M5·7
- **F10** copy-to-board duplicates (original untouched); move-to-board
  relocates the placement — C-M5·8

## 6. Parts affected

- **`Kumunita.Core.Projects`** — the new bounded context (the `Projects/`
  folder in the ARCHITECTURE.md §3 tree, flipped from "not yet created").
  Four POCOs: `TodoItem`, `KanbanBoard`, `KanbanLane`,
  `BoardItemPlacement`.
- **`M5DocTypes`** — the new document surface (a new file next to
  `M4DocTypes`; the `M3DocTypes` / `M4DocTypes` pattern verbatim).
- **The two adapters** — `TodoItemToAuditableResource` +
  `KanbanBoardToAuditableResource` (new files in `Projects/`).
- **`IProjectService` + `ProjectService`** — the new service seam (new files
  in `Projects/`; registered in `Kumunita.Core/DependencyInjection.cs`).
- **`ProjectsController`** — the new Web controller
  (`Kumunita.Web/Controllers/`) + the view models.
- **The views** — the new `Kumunita.Web/Views/Projects/` folder (to-do list
  / detail / compose; board detail + composer) + the nav entry.
- **The TS module** — the new `Kumunita.Web/client/lib/projects-board.ts`
  (reorder / move / copy / assign / delete, keyboard + menu, explicit
  POSTs, CSRF-aware).
- **`site.css`** — the new `projects-board*` rules.
- **`kw-l` keys × 4** — the new `projects.*` block in
  `KnownTranslationKeys.cs` (en/de/fr/da).
- **Boot wiring** — `Program.cs` + `SchemaBootstrap.cs` (the two boot paths
  the surface + service are wired into — U02).

## 7. Feedback loops

The **invariant table (§4) is the primary source**; the pinned test *names*
live in **Part 2 (U01)**. The feedback shape U01 pins and U12 implements:

- **The ~16 Core pins** (in `tests/Kumunita.Core.Tests/ProjectServiceTests.cs`,
  run over `PostgresFixture`) — the FACES F1–F10 exercised over the
  `IProjectService` read / write / placement lanes.
- **The ~8 Web pins** (appended to
  `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs`, NSubstitute, no
  Postgres) — the `ProjectsController` over a substituted `IProjectService`.
- **The three-test acceptance gate** (recorded by U13): **closed loop** (an
  author creates a to-do → it appears in their to-do list and, when placed,
  on the board with the right card + one aggregate audit row); **handoff**
  (a to-do assigned to a second resident — the assignee sees it on the board
  and has standing to edit it on the next render; the non-assignee's view /
  standing still exclude it); **part-vs-whole** (the ~24 pinned tests pass
  together with the full M4 `EventServiceTests` + the existing post /
  announcement / page pin suites still green — the lane is additive, nothing
  regressed).

**Runner note (AGENTS.md test-runner quirk, this machine):** the reliable
path is build then in-process execution — **not** `dotnet test` / VS Test
Explorer:

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

## 8. Risks

- **The lane-limit refusal is the one new error surface.** A move / copy
  into a `MaxItems`-capped lane is refused (a message, nothing written). The
  message shape is pinned in Part 2; the acceptance is the F6 pair
  (refuse-at-limit / accept-below).
- **The hierarchy cycle guard is the one new invariant to enforce
  server-side.** A `ParentId` that would make the target a descendant of the
  to-do is refused (F9). This is a new server-side check with no direct M4
  analog — the test name is pinned in Part 2.
- **The board-gated-card visibility is the one new two-level decision.** A
  to-do on a board is visible iff **both** the board's `Audience` and the
  to-do's own `Audience` permit it (F3). Getting the "entry then per-card"
  ordering wrong would either leak a card the board should hide or hide one
  the board should show — the test names are pinned in Part 2.
- **The placement row is the one new document that is not itself an
  auditable resource** (C-M5·3). It inherits its to-do's decision; a
  spurious `BoardItemPlacementToAuditableResource` would be a new
  authorization surface (forbidden — C-M5·11 / D12).
- **The lane-status auto-update is a write, not a display nicety.** It must
  land in the **same transaction** as the placement move and store an
  audited row (C-M5·4 / C3); a race or a missing audit row is a privacy /
  trust leak, not just a UI bug.

---

# Seams & contracts (Part 2, written by U1)

> **Part 2 status.** **Locked** — ADR 0067 (Accepted, 2026-09-23) is the
> decisions source; this section is the **frozen reference tier** U02–U12
> code against. The four POCOs + two adapters (§2.2), the **full
> `IProjectService` surface** (§2.3), the `M5DocTypes` surface (§2.4), the
> standing matrix (§2.5), the `kw-l` keys (§2.6), the **16 Core** +
> **8 Web** pinned test names (§2.7 / §2.8), the three-test acceptance gate
> (§2.9), and the drift-guard (§2.10) are **frozen pins** — a mismatch is a
> `## U<m> — Drift pause` per the plan's unit-series rule §9. U04 copies
> §2.3 **verbatim**.

## 2.1 Frozen seam list (exact C#)

The seams M5 builds on — each **frozen**, each **reused without a new
signature, a new `AccessAction`, or a new `AccessVia`** (C-M5·11 / D12):

```csharp
// Kumunita.Core.Authorization — frozen 4-method surface (ADR 0006 §A; the
// group lane's CanSeeGroupAsync additions (ADR 0013) are out of M5's scope —
// M5 plugs into the two frozen Read lanes, no new method).
public interface IAuthorizationService
{
    Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target);
    Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target, IDocumentSession session);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates, IDocumentSession session);
    // + the ADR 0013 group lane (CanSeeGroupAsync × 2) — untouched by M5.
}

// Kumunita.Core.Authorization — the exact Action surface (ADR 0006):
// `Read` = "read" (M5's only action), `Moderate` = "moderate" (reserved —
// M5 adds **no** action id).
public sealed record AccessAction(string Id)
{
    public static readonly AccessAction Read = new("read");
    public static readonly AccessAction Moderate = new("moderate");
}

// Kumunita.Core.Authorization — the exact 6-member resource surface M5's two
// adapters implement (the `PostToAuditableResource` /
// `EventToAuditableResource` projection, verbatim):
public interface IAuditableResource
{
    string Id { get; }
    string Name { get; }
    string? OwnerId { get; }
    Audience? Audience { get; }
    string? ComponentId { get; }
    string TargetKind { get; }
}

// Kumunita.Core.Authorization — the exact Audience shape M5's documents
// carry (ADR 0001-B / 0036 / 0041 — *reused*, not extended; `null` = public,
// the frozen Decide() branch 5):
public sealed class Audience
{
    public AudienceMode Mode { get; set; }                       // Any (union, default) | All (intersection)
    public List<AudienceGrant> Grants { get; set; }             // AudienceGrant(GrantKind Kind, string Id)
    public bool Community { get; set; }                          // ADR 0036 — the composer seeds it `true` by default (the post convention)
    public bool AllResidents { get; set; }                       // ADR 0041
    public bool IsEmpty => Grants.Count == 0;
}

// Kumunita.Core.UserInfo — the profile-read seam (ADR 0006 §A, frozen): the
// M5 display lookup for the assignee chip / assign picker (a `Profile` with
// the avatar + display-name fields). **No new seam** — M5 adds no method
// here.
public interface IUserInfoService
{
    Task<Profile?> GetProfileAsync(string subjectId);
    // + the remaining frozen methods — untouched by M5.
}
```

Also reused, not re-shapeable (no C# here — the seams are the *frozen*
existing surfaces): the `MarkdownRenderer` (ADR 0025) for the optional
`TodoItem.Body` / `KanbanBoard.Description`; the `bindRichEditor`
(ADR 0031) for the composers; the `kw-dt` TagHelper (ADR 0019 / 0020) for
every M5 timestamp; the `client/lib/api.ts` CSRF-aware fetch shape (the M2/M3
precedent) for `projects-board.ts`'s explicit POSTs; the `AudienceEditorModel`
+ `BuildAudience()` form surface (M2) for the to-do / board composers; and the
`PostgresFixture` (`tests/Kumunita.Core.Tests`, Testcontainers
`postgres:18`) harness the §2.7 Core pins run over (`Kumunita.Web.Tests`
pins use NSubstitute — no Postgres).

## 2.2 New M5-owned Core types (exact C#)

```csharp
// Kumunita.Core.Projects — the four M5 documents (ADR 0067 D1 — all POCOs,
// conventional `string` `Id`, Marten-native; delta-detected + idempotent, no
// seeding, no EF; the surface is additive — ADR 0004 §B.1).

public sealed class TodoItem
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;          // non-empty — the card label + adapter `Name`
    public string? Body { get; set; }                           // optional Markdown — the ADR 0025 shape (the one MarkdownRenderer)

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2 — the Post.ComponentId shape)
    public string AuthorId { get; set; } = string.Empty;       // the standing owner (C-M5·6)
    public string? AssigneeId { get; set; }                    // a SubjectId — display + standing, NEVER a gate (C-M5·3 / C-M5·6)
    public string? Status { get; set; }                         // nullable string state label; `null` = no status — NOT an enum (C-M5·4)
    public string? ParentId { get; set; }                       // the sole hierarchy mechanism; `null` = top-level (C-M5·7)

    public Authorization.Audience? Audience { get; set; }       // the exact post Audience (ADR 0001-B / 0036; `null` = public)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused
    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused
    public IReadOnlyList<string> TagIds { get; set; } = [];    // ADR 0044, reused
    public IReadOnlyList<string> ImageIds { get; set; } = [];  // ADR 0025 content-image ids, reused
    public IReadOnlyList<string> AttachmentIds { get; set; } = []; // ADR 0034 attachment ids, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }

    // **No** `BoardId` / `LaneId` / `Order` (C-M5·2 — placement is a separate
    // `BoardItemPlacement` record). **No** `IsDraft` (D8a — a to-do is
    // published on creation; the ADR 0037 draft lane is a follow-on lane).
}

public sealed class KanbanBoard
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string Title { get; set; } = string.Empty;          // non-empty — the board label
    public string? Description { get; set; }                   // optional Markdown — the ADR 0025 shape

    public string? ComponentId { get; set; }                   // a feed filter, never a gate (C-M3·2)
    public string AuthorId { get; set; } = string.Empty;       // the standing owner (C-M5·6)

    public Authorization.Audience? Audience { get; set; }       // the exact post Audience (ADR 0001-B / 0036; `null` = public)

    public bool IsDeleted { get; set; } = false;               // ADR 0024 soft-delete flag, reused
    public string LanguageCode { get; set; } = string.Empty;   // ADR 0018 authored-in tag, reused

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // **No** `IsDraft` (D8a — a board is live on creation).
}

public sealed class KanbanLane
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string BoardId { get; set; } = string.Empty;        // the parent board (the (BoardId, Order) business key)
    public string Title { get; set; } = string.Empty;          // non-empty — the lane label
    public string? Status { get; set; }                         // the status a lane IMPARTS on a to-do moved into it; `null` = imparts none (C-M5·4)
    public int? MaxItems { get; set; }                          // the lane's capacity; `null` = no limit — advisory, refused not dropped (C-M5·5)
    public int Order { get; set; }                              // the lane's position within the board, 0-based (the column order)

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // **No** `Audience` (C-M5·3 — a lane's visibility is the board's).
}

public sealed class BoardItemPlacement
{
    public string Id { get; set; } = string.Empty;             // surrogate (Marten default)

    public string TodoItemId { get; set; } = string.Empty;     // the to-do (the (TodoItemId, BoardId) business key)
    public string BoardId { get; set; } = string.Empty;        // the board
    public string LaneId { get; set; } = string.Empty;         // the lane (the (BoardId, LaneId, Order) business key)
    public int Order { get; set; }                              // the to-do's position within the lane, 0-based (the card order)

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    // **No** `Audience` (C-M5·3 — the placement's visibility is the to-do's +
    // the board's; it is **not** itself an auditable resource).
}

// Kumunita.Core.Projects — the two adapters (the `PostToAuditableResource` /
// `EventToAuditableResource` 6-member shape verbatim; `sealed` keeps the
// surface closed — ADR 0006-D). `TodoItem.Title` is non-empty by pin, so the
// `Name` projection is `Title` (no `Body` fallback needed).

public sealed class TodoItemToAuditableResource : IAuditableResource
{
    public TodoItemToAuditableResource(TodoItem todo) => TodoItem = todo;

    public TodoItem TodoItem { get; }                          // the adapter does not own the to-do
    public string Id => TodoItem.Id;
    public string Name => TodoItem.Title;
    public string? OwnerId => TodoItem.AuthorId;
    public Authorization.Audience? Audience => TodoItem.Audience;
    public string? ComponentId => TodoItem.ComponentId;
    public string TargetKind => "todo";                        // the EXACT string (C3 — the AccessAudit aggregate-row discriminator)
}

public sealed class KanbanBoardToAuditableResource : IAuditableResource
{
    public KanbanBoardToAuditableResource(KanbanBoard board) => Board = board;

    public KanbanBoard Board { get; }                          // the adapter does not own the board
    public string Id => Board.Id;
    public string Name => Board.Title;
    public string? OwnerId => Board.AuthorId;
    public Authorization.Audience? Audience => Board.Audience;
    public string? ComponentId => Board.ComponentId;
    public string TargetKind => "board";                       // the EXACT string (C3)
}
```

## 2.3 The full `IProjectService` surface (exact C#, verbatim)

> **U04 registers this interface + a skeleton `ProjectService` — the write
> (U05) and placement (U06) methods throw `NotImplementedException` first**
> (the M4 full-interface-first pin). U05 / U06 **implement** the stubbed
> methods; a rename or re-scope of a signature after U04 is a **drift event**
> (§2.10). Every method's standing + audit row is pinned here per lane.

```csharp
// Kumunita.Core.Projects
public interface IProjectService
{
    // --- Read lanes (U04) ---------------------------------------------------

    /// <summary>
    /// The standalone to-do list (the feed): candidates = `!IsDeleted`,
    /// filtered by the optional `componentId` (a filter, never a gate — C-M3·2)
    /// and the optional `assigneeId` (a filter, never a gate — C-M5·6); the
    /// survivors are `CanSeeAsync(Read)`-filtered (C6 / C3) over the
    /// `TodoItemToAuditableResource`; ordered by `Created` descending (the
    /// newest first — the post feed shape); paged. The **aggregate**
    /// `AccessAudit` row (`TargetKind = "todo"`, `visibleCount` /
    /// `hiddenCount`) is the C-M3·3 shape.
    /// </summary>
    Task<IReadOnlyList<TodoItem>> ListTodosAsync(string? componentId, string? assigneeId, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// One to-do + its **subtasks** (the `TodoItem` rows with
    /// `ParentId == todoItemId`, ordered by `Created` ascending). One
    /// `CanAsync(Read)` over the to-do; `KeyNotFoundException` (404) on
    /// absent, `UnauthorizedAccessException` (403) on denied — the C3
    /// 404-vs-403 split. Each subtask is **itself** `CanAsync(Read)`-gated
    /// (a subtask is a full to-do with its own `Audience` — C-M5·7); a denied
    /// subtask is not returned. The to-do's single decision is the
    /// `AccessAudit` row (C3).
    /// </summary>
    Task<TodoDetailResult> GetTodoAsync(string todoItemId, string actorId, CancellationToken ct = default);

    /// <summary>
    /// The board list (the feed): candidates = `!IsDeleted`, filtered by the
    /// optional `componentId` (a filter, never a gate — C-M3·2); the survivors
    /// are `CanSeeAsync(Read)`-filtered (C6 / C3) over the
    /// `KanbanBoardToAuditableResource`; ordered by `Created` descending;
    /// paged. The **aggregate** `AccessAudit` row (`TargetKind = "board"`,
    /// `visibleCount` / `hiddenCount`) is the C-M3·3 shape.
    /// </summary>
    Task<IReadOnlyList<KanbanBoard>> ListBoardsAsync(string? componentId, string actorId, int page, CancellationToken ct = default);

    /// <summary>
    /// One board + its **lanes** (the `KanbanLane` rows with
    /// `BoardId == boardId`, ordered by `Order` ascending) + each lane's
    /// **cards** (the `BoardItemPlacement` rows with `BoardId == boardId`
    /// `&& LaneId == laneId`, ordered by `Order` ascending, each resolved to
    /// its `TodoItem`). **The two-level decision (C-M5·3):** the board's
    /// `CanAsync(Read)` is the entry gate (`KeyNotFoundException` (404) on
    /// absent, `UnauthorizedAccessException` (403) on denied — the C3 split);
    /// each card's `TodoItem` is **itself** `CanAsync(Read)`-gated — a to-do
    /// on the board is visible iff **both** the board and the to-do are
    /// visible; a denied card is **not returned** in the result, not just
    /// hidden in the view. The **aggregate** `AccessAudit` row
    /// (`TargetKind = "board"`, `visibleCount` = the visible card count,
    /// `hiddenCount` = the denied card count) is the C-M3·3 shape.
    /// </summary>
    Task<BoardDetailResult> GetBoardAsync(string boardId, string actorId, CancellationToken ct = default);

    // --- Write lanes (U05) — standing re-checked server-side (C-M5·6, C3) ---

    /// <summary>
    /// Create a to-do — the author's choice is written verbatim (ADR 0001-B);
    /// the to-do is **published on creation** (no `IsDraft` — D8a). The author
    /// becomes the standing owner (the `AuthorId` branch). The `AccessAudit`
    /// row (`todo.create`, `TargetKind = "todo"`) is stored in the caller's
    /// session (C3).
    /// </summary>
    Task<TodoItem> CreateTodoAsync(string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Edit a to-do (title / body / status / component / language / tags /
    /// reparent) — **creator ∪ assignee ∪ GlobalAdmin** (the ADR 0014 / 0016 /
    /// 0017 precedent, enforced server-side per C-M5·6). `AuthorId` / `Created`
    /// preserved untouched; `Modified` stamped on a real change.
    /// `actorRoles` carries the principal's real role set (the Web layer
    /// passes `RoleSet(User)`). The **hierarchy cycle guard** (C-M5·7): a
    /// `request.ParentId` that would make the target a descendant of the
    /// to-do is refused (`InvalidOperationException`). The `AccessAudit` row
    /// (`todo.update`, `TargetKind = "todo"`) is stored in the caller's
    /// session (C3).
    /// </summary>
    Task<TodoItem> UpdateTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, UpdateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Assign a to-do — sets `AssigneeId` to `assigneeId` (`null` =
    /// unassign). **Creator ∪ assignee ∪ GlobalAdmin** (C-M5·6). The
    /// `AccessAudit` row (`todo.assign`, `TargetKind = "todo"`) is stored in
    /// the caller's session (C3).
    /// </summary>
    Task<TodoItem> AssignTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? assigneeId, CancellationToken ct = default);

    /// <summary>
    /// Add a subtask — a new `TodoItem` with `ParentId =
    /// parentTodoItemId` (the sole hierarchy mechanism — C-M5·7); the
    /// subtask is a full to-do (its own status / assignee / placements) and
    /// its author is the actor. **Creator ∪ assignee ∪ GlobalAdmin** over
    /// the **parent** (C-M5·6). The parent's `ParentId` is unchanged. The
    /// `AccessAudit` row (`todo.add_subtask`, `TargetKind = "todo"`) is
    /// stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> AddSubtaskAsync(string parentTodoItemId, string actorId, IReadOnlySet<string> actorRoles, CreateTodoRequest request, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** a to-do — sets `IsDeleted = true` (the ADR 0024
    /// author-lane shape) + **cascades** to the descendant subtree (the
    /// `TodoItem` rows transitively reachable via `ParentId` — C-M5·7); the
    /// to-do's `BoardItemPlacement` rows are **kept** (a placement's target
    /// becoming a soft-deleted to-do is the read lane's filter — a board card
    /// for a deleted to-do is not returned). **Creator ∪ assignee ∪
    /// GlobalAdmin** (C-M5·6). The `AccessAudit` row (`todo.delete`,
    /// `TargetKind = "todo"`) is stored in the caller's session (C3).
    /// </summary>
    Task DeleteTodoAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// Create a board — a new `KanbanBoard` + its initial `KanbanLane` rows
    /// (each lane's `Title` / `Status` / `MaxItems` / `Order` values come
    /// from `request.Lanes`). The board is **live on creation** (D8a). The
    /// author becomes the standing owner (the `AuthorId` branch). The
    /// `AccessAudit` row (`board.create`, `TargetKind = "board"`) is stored
    /// in the caller's session (C3).
    /// </summary>
    Task<KanbanBoard> CreateBoardAsync(string actorId, IReadOnlySet<string> actorRoles, CreateBoardRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update a lane — set the lane's `Title` / `Status` / `MaxItems` /
    /// `Order`. **Creator ∪ GlobalAdmin** over the **board** (the lane is
    /// not its own standing surface — the assignee branch does not apply to a
    /// lane). The `AccessAudit` row (`board.update_lane`,
    /// `TargetKind = "board"`) is stored in the caller's session (C3).
    /// </summary>
    Task<KanbanLane> UpdateLaneAsync(string laneId, string actorId, IReadOnlySet<string> actorRoles, UpdateLaneRequest request, CancellationToken ct = default);

    /// <summary>
    /// **Soft-delete** a board — sets `IsDeleted = true` (the ADR 0024
    /// author-lane shape) + **deletes** the board's `KanbanLane` rows +
    /// **deletes** the board's `BoardItemPlacement` rows (the placements are
    /// **not** cascaded to the to-dos — a to-do on a deleted board is still
    /// standalone, C-M5·2). **Creator ∪ GlobalAdmin** over the board. The
    /// `AccessAudit` row (`board.delete`, `TargetKind = "board"`) is stored
    /// in the caller's session (C3).
    /// </summary>
    Task DeleteBoardAsync(string boardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    // --- Placement + reorder lanes (U06) ------------------------------------

    /// <summary>
    /// **Reorder within the lane** — `direction` is `"up"` or `"down"` (a
    /// string, not an enum — the ADR 0031 plain-GET/POST posture): swap
    /// `Order` with the adjacent card (the `BoardItemPlacement` row with the
    /// next lower / higher `Order` in the same lane; a card at the edge is a
    /// no-op — already first / last). **Creator ∪ assignee ∪ GlobalAdmin**
    /// over the **to-do** (C-M5·6). The `AccessAudit` row
    /// (`todo.move_within_lane`, `TargetKind = "todo"`) is stored in the
    /// caller's session (C3).
    /// </summary>
    Task<BoardItemPlacement> MoveTodoWithinLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Change lane (same board)** — `direction` is `"left"` or `"right"`
    /// (a string, not an enum): the placement's `LaneId` is set to the
    /// adjacent lane's id (the `KanbanLane` row with the next lower / higher
    /// `Order` in the same board; a lane at the edge is a no-op); `Order` is
    /// reset to the **end** of the target lane (the max `Order` + 1).
    /// **The lane-status auto-update (C-M5·4):** if the target lane's
    /// `Status` is non-null, the to-do's `Status` is set to that lane's
    /// status **in the same transaction** (C3); a null `Status` leaves the
    /// to-do's status unchanged. **The lane-limit refusal (C-M5·5):** if the
    /// target lane's `MaxItems` is non-null and the target lane already has
    /// `MaxItems` placements, the move is **refused** (`InvalidOperationException`
    /// with the lane's `Title` in the message; **nothing is written**).
    /// **Creator ∪ assignee ∪ GlobalAdmin** over the to-do. The
    /// `AccessAudit` row (`todo.move_to_lane`, `TargetKind = "todo"`) is
    /// stored in the caller's session (C3).
    /// </summary>
    Task<BoardItemPlacement> MoveTodoToAdjacentLaneAsync(string placementId, string direction, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Relocate the placement (C-M5·8):** the placement rows on the
    /// **source** board (all of them) are **deleted**; a new
    /// `BoardItemPlacement` row is created on the target board's **first
    /// lane** (the `KanbanLane` row with the lowest `Order` in the target
    /// board); `Order` = the end of that lane (max `Order` + 1). **The
    /// lane-status auto-update (C-M5·4)** and **the lane-limit refusal
    /// (C-M5·5)** apply to the target's first lane (the same rules).
    /// **Creator ∪ assignee ∪ GlobalAdmin** over the to-do. The
    /// `AccessAudit` row (`todo.move_to_board`, `TargetKind = "todo"`) is
    /// stored in the caller's session (C3).
    /// </summary>
    Task<BoardItemPlacement> MoveTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);

    /// <summary>
    /// **Duplicate the to-do (C-M5·8):** a new `TodoItem` with the same
    /// `Title` / `Body` / `Status` / `AssigneeId` / `Audience` — the
    /// `AuthorId` is the **actor**, not the original's author (the copy is a
    /// new to-do, not a clone); **no** `ParentId` (a copy is always
    /// top-level — a subtask is not copied as a subtask); a new
    /// `BoardItemPlacement` row on the target board's **first lane** (the
    /// same shape as `MoveTodoToBoardAsync`). **The lane-status
    /// auto-update (C-M5·4)** and **the lane-limit refusal (C-M5·5)** apply
    /// to the target's first lane (the same rules). The original to-do is
    /// **untouched**. **Creator ∪ assignee ∪ GlobalAdmin** over the to-do.
    /// The `AccessAudit` row (`todo.copy_to_board`, `TargetKind = "todo"`)
    /// is stored in the caller's session (C3).
    /// </summary>
    Task<TodoItem> CopyTodoToBoardAsync(string todoItemId, string targetBoardId, string actorId, IReadOnlySet<string> actorRoles, CancellationToken ct = default);
}

// Kumunita.Core.Projects — the DTOs (the `EventRequests.cs` sealed-record
// shape, verbatim — one file `ProjectRequests.cs` next to the POCOs):

public sealed record CreateTodoRequest
{
    public required string Title { get; init; }
    public string? Body { get; init; }
    public string? ComponentId { get; init; }
    public string? AssigneeId { get; init; }
    public string? ParentId { get; init; }                    // a non-null value = created as a subtask of that parent (the AddSubtaskAsync shape)
    public Audience? Audience { get; init; }
    public string? LanguageCode { get; init; }
    public IReadOnlyList<string>? TagIds { get; init; }
}

public sealed record UpdateTodoRequest
{
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? ComponentId { get; init; }
    public string? Status { get; init; }
    public string? ParentId { get; init; }                    // a non-null value **reparents** the to-do to that parent
    public bool ClearParent { get; init; }                     // `true` = explicit unparent (sets `ParentId = null`)
    public string? LanguageCode { get; init; }
    public IReadOnlyList<string>? TagIds { get; init; }
    // `ParentId == null && !ClearParent` is a no-op on the hierarchy. The
    // **hierarchy cycle guard** (C-M5·7) is enforced server-side in
    // `UpdateTodoAsync` — the request carries the intent, the service
    // enforces the guard (F9).
}

public sealed record CreateBoardRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ComponentId { get; init; }
    public Audience? Audience { get; init; }
    public string? LanguageCode { get; init; }
    public IReadOnlyList<CreateLaneRequest> Lanes { get; init; } = [];
}

public sealed record CreateLaneRequest
{
    public required string Title { get; init; }
    public string? Status { get; init; }
    public int? MaxItems { get; init; }
    public int Order { get; init; }
}

public sealed record UpdateLaneRequest
{
    public string? Title { get; init; }
    public string? Status { get; init; }
    public int? MaxItems { get; init; }
    public int? Order { get; init; }
}

public sealed record TodoDetailResult
{
    public required TodoItem Todo { get; init; }
    public IReadOnlyList<TodoItem> Subtasks { get; init; } = [];
}

public sealed record BoardDetailResult
{
    public required KanbanBoard Board { get; init; }
    public IReadOnlyList<LaneDetail> Lanes { get; init; } = [];
}

public sealed record LaneDetail
{
    public required KanbanLane Lane { get; init; }
    public IReadOnlyList<TodoItem> Cards { get; init; } = [];
}
```

## 2.4 The `M5DocTypes` surface (exact C#)

```csharp
// Kumunita.Core (the M4DocTypes shape, verbatim — a new file `M5DocTypes.cs`
// next to `M4DocTypes.cs`, wired into **both boot paths** — `Program.cs`
// (next to the `M4DocTypes.Configure(opts)` call) + `SchemaBootstrap.cs` —
// U02).
using Kumunita.Core.Projects;
using Marten;

namespace Kumunita.Core;

public static class M5DocTypes
{
    public static void Configure(StoreOptions opts)
    {
        // TodoItem — conventional string Id (Marten's default); the
        // (ComponentId, Created) **feed-ordering** index (the ListTodosAsync
        // feed orders survivors by `Created` descending — the M4 `Event`
        // (ComponentId, Start) index shape); the `ParentId` index (the
        // subtask lookup — the GetTodoAsync subtask read).
        opts.Schema.For<TodoItem>()
               .Index(t => new { t.ComponentId, t.Created }, "idx_todocomp_created")
               .Index(t => t.ParentId, "idx_todo_parent");

        // KanbanBoard — conventional string Id; the (ComponentId, Created)
        // feed-ordering index (the ListBoardsAsync feed shape).
        opts.Schema.For<KanbanBoard>()
               .Index(b => new { b.ComponentId, b.Created }, "idx_boardcomp_created");

        // KanbanLane — conventional string Id; the (BoardId, Order) **unique**
        // index (a lane's position within its board is a business key — the
        // `EventRsvp` (EventId, UserId) unique-index shape, the
        // last-write-wins concurrency exception).
        opts.Schema.For<KanbanLane>()
               .UniqueIndex(l => l.BoardId, l => l.Order);

        // BoardItemPlacement — conventional string Id; the (BoardId, LaneId,
        // Order) **unique** index (a to-do's position within a lane is a
        // business key — the same EventRsvp shape); the (TodoItemId, BoardId)
        // **unique** index (a to-do appears on a board at most once — the M5
        // pin, the EventRsvp (EventId, UserId) shape carried to the
        // to-do / board pair).
        opts.Schema.For<BoardItemPlacement>()
               .UniqueIndex(p => p.BoardId, p => p.LaneId, p => p.Order)
               .UniqueIndex(p => p.TodoItemId, p => p.BoardId);
    }
}
```

**Zero migrations for existing surfaces** — the four docs are new, the
surface is additive (ADR 0004 §B.1; the `M4DocTypes` precedent).

## 2.5 The standing matrix (pinned)

Enforced **server-side** in the `ProjectService` (the
`AnnouncementService.CreateAsync` C3 pattern — `CheckCreateStanding` /
`CheckEditStanding`-shaped helpers, each throwing
`UnauthorizedAccessException` (403) / `KeyNotFoundException` (404) exactly
like `AnnouncementService`). The Web `[Authorize]` is a **convenience
pre-gate only, never the source of truth** (C-M5·6 / D8). Every mutating
method above carries the principal's real role set — the Web layer passes
`RoleSet(User)`; Core enforces the matrix (the ADR 0014 / 0016 / 0017
precedent, extended with the **assignee** as a collaborator — the ADR 0067
standing decision).

| Lane (method) | Standing | `AccessVia` | Audit row (C3) |
|---|---|---|---|
| `CreateTodoAsync` | any signed-in resident (becomes the author) | `Owner` | `todo.create` |
| `UpdateTodoAsync` | **creator ∪ assignee ∪ GlobalAdmin** | `Owner` / `Admin` | `todo.update` |
| `AssignTodoAsync` | **creator ∪ assignee ∪ GlobalAdmin** | `Owner` / `Admin` | `todo.assign` |
| `AddSubtaskAsync` (over the parent) | **creator ∪ assignee ∪ GlobalAdmin** | `Owner` / `Admin` | `todo.add_subtask` |
| `DeleteTodoAsync` | **creator ∪ assignee ∪ GlobalAdmin** | `Owner` / `Admin` | `todo.delete` |
| `MoveTodoWithinLaneAsync` / `MoveTodoToAdjacentLaneAsync` / `MoveTodoToBoardAsync` / `CopyTodoToBoardAsync` | **creator ∪ assignee ∪ GlobalAdmin** over the **to-do** | `Owner` / `Admin` | `todo.move_within_lane` / `todo.move_to_lane` / `todo.move_to_board` / `todo.copy_to_board` |
| `CreateBoardAsync` | any signed-in resident (becomes the author) | `Owner` | `board.create` |
| `UpdateLaneAsync` (over the board) | **creator ∪ GlobalAdmin** (the lane is not its own standing surface — the assignee branch does not apply) | `Owner` / `Admin` | `board.update_lane` |
| `DeleteBoardAsync` (over the board) | **creator ∪ GlobalAdmin** | `Owner` / `Admin` | `board.delete` |

- **The creator** is `TodoItem.AuthorId` / `KanbanBoard.AuthorId` (the
  standing owner); **the assignee** is `TodoItem.AssigneeId` (a collaborator
  — assignment grants standing); **GlobalAdmin** is the override branch
  (the ADR 0017 precedent, exercised via the `actorRoles` parameter).
- `AssigneeId` is **display + standing, never an access gate** — read is
  the `Audience` decision (C-M5·3); the matrix above governs *mutations*.
- **The placement lanes' standing is over the to-do** (not the board, not
  the lane) — C-M5·6.

## 2.6 The `kw-l` keys (pinned, the D11 set)

The new `projects.*` block in the `KnownTranslationKeys` registry (the ADR
0015 key-registry shape is the host), present in **all four** seeded
languages (en/de/fr/da — the ADR 0042 / 0045 set), English the fallback in
every view:

- `projects.todo.title`
- `projects.todo.body`
- `projects.todo.assignee`
- `projects.todo.status`
- `projects.todo.delete`
- `projects.todo.add_subtask`
- `projects.todo.copy_to`
- `projects.todo.move_to`
- `projects.todo.move_up`
- `projects.todo.move_down`
- `projects.todo.move_left`
- `projects.todo.move_right`
- `projects.board.title`
- `projects.board.description`
- `projects.board.lane.title`
- `projects.board.lane.status`
- `projects.board.lane.max_items`
- `projects.board.lane.empty`
- `projects.board.empty`

(19 keys; the ADR 0052 warm-boot baseline backfill lane covers the
`de`/`fr`/`da` rows automatically — no new seeder step in M5.)

## 2.7 Pinned seam tests — Core (exact names, 16)

`tests/Kumunita.Core.Tests/ProjectServiceTests.cs` (run over the
`PostgresFixture`), the FACES F1–F10 exercised over the `IProjectService`
read / write / placement lanes. A rename or re-scope of a name after this
freeze is a `## U<m> — Drift pause` (unit-series rule §3):

1. `F1_TodoVisibleToAudienceMember`
2. `F1_TodoHiddenFromNonMember`
3. `F2_TodoPlacedOnMultipleBoards_AppearsOnEach`
4. `F3_BoardGate_TodoOnBoard_BoardDenies_HidesCard`
5. `F3_BoardGate_TodoOnBoard_BoardAllows_ShowsCard`
6. `F4_MoveToStatusLane_SetsTodoStatus_Audited`
7. `F5_MoveToNullStatusLane_TodoStatusUnchanged`
8. `F6_LaneAtMaxRefusesMoveIn`
9. `F6_LaneBelowMaxAllowsMoveIn`
10. `F7_AssigneeHasStanding_UpdatesTodo`
11. `F8_NonStandaloneActorRefused`
12. `F9_SubtaskIsFullTodo_OwnStatusAssignee`
13. `F9_DeleteCascadesToSubtree`
14. `F9_ReparentToDescendant_Refused`
15. `F10_CopyToBoard_Duplicates_OriginalUntouched`
16. `F10_MoveToBoard_RelocatesPlacement`

## 2.8 Pinned seam tests — Web (exact names, 8)

Appended to `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs`
(NSubstitute, no Postgres) — the `ProjectsController` over a substituted
`IProjectService`:

1. `Todos_List_AudienceFiltered`
2. `Todo_Detail_SubtasksRendered`
3. `Board_Detail_LanesAndCards`
4. `Board_MoveLeftRight_TriggerStatusUpdate`
5. `Board_LaneAtMax_RefusesMoveIn`
6. `Board_CopyToBoard_Duplicates`
7. `Board_MoveToBoard_Relocates`
8. `Todo_AssignToUser_StandingGranted`

## 2.9 Acceptance gate (U13 records the run)

The three M4-style tests:

| # | Test | Shape (what it proves) |
|---|------|------------------------|
| 1 | **closed loop** | an author creates a to-do → it appears in their to-do list **and**, when placed, on the board with the right card + one aggregate `AccessAudit` row (`TargetKind = "todo"` / `"board"`, `Outcome = Allow`) |
| 2 | **handoff** | a to-do assigned to a second resident — the assignee sees it on the board and has standing to edit it on the **next** render (the `AssigneeId` branch of the §2.5 matrix, strong consistency); the non-assignee's view / standing still exclude it |
| 3 | **part-vs-whole** | the 16 Core (§2.7) + 8 Web (§2.8) pinned tests pass **together** in the same run as the full M4 `EventServiceTests` + the existing post / announcement / page pin suites — the lane is additive, nothing regressed |

**Runner note (AGENTS.md test-runner quirk, this machine):** the reliable
path is build then in-process execution — **not** `dotnet test` / VS Test
Explorer:

```
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
```

## 2.10 Drift-guard (frozen once written)

The following are **frozen pins**; any mismatch with the implementation is a
`## U<m> — Drift pause` (unit-series rule §9):

- **The 11 invariants** C-M5·1…C-M5·11 (Part 1 §4 — U00).
- **The 10 FACES** F1–F10 (Part 1 §5 — U00).
- **The four POCOs** — `TodoItem` / `KanbanBoard` / `KanbanLane` /
  `BoardItemPlacement` (this file §2.2) — the exact field sets above; a
  re-shape (renaming a field, adding a gate field, a `BoardId` / `LaneId` /
  `Order` on `TodoItem`, an `IsDraft`, a status enum) is a **new ADR**, not a
  silent addition.
- **The two adapters** — `TodoItemToAuditableResource` /
  `KanbanBoardToAuditableResource` (this file §2.2) — the 6-member
  projection, `TargetKind = "todo"` / `"board"` (the **exact** strings). No
  new `AccessAction` / `AccessVia` / `Decide()` branch.
- **The full `IProjectService` surface** (this file §2.3) — the read + write
  + placement signatures verbatim (frozen in U04). An ADD beyond this list
  is a **new ADR**; a re-scope of one is a drift event.
- **The DTOs** (this file §2.3) — `CreateTodoRequest` / `UpdateTodoRequest`
  / `CreateBoardRequest` / `CreateLaneRequest` / `UpdateLaneRequest` /
  `TodoDetailResult` / `BoardDetailResult` / `LaneDetail`.
- **The `M5DocTypes` surface** (this file §2.4) — the four documents + the
  business-key unique indexes.
- **The standing matrix** (this file §2.5) — creator ∪ assignee ∪ GlobalAdmin
  for to-do mutations; creator ∪ GlobalAdmin for board / lane mutations; the
  GlobalAdmin override branch.
- **The `kw-l` keys** (this file §2.6) — the 19-key `projects.*` block.
- **The 16 Core test names** (this file §2.7) — the master list; a
  rename / renumber is a break.
- **The 8 Web test names** (this file §2.8) — the master list; a rename /
  renumber is a break.
- **The three-test gate** (this file §2.9) — closed loop / handoff /
  part-vs-whole.

---

*Part 1 (U00) — context, scope, the [PROPOSED] decisions D1–D12 (+ D8a), the
11 invariants C-M5·1–11, and the 10 FACES F1–F10. **No code, no build, no
tests.** Part 2 (U01) — the exact C# shapes, the full `IProjectService`
surface, the 16 Core + 8 Web pinned test names, the three-test gate, and the
drift-guard, locked by **ADR 0067 (Accepted, 2026-09-23)**.*
