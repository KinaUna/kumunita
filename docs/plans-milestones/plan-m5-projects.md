# M5 — Projects (to-dos + Kanban boards) — sealed unit register

> **In progress.** This is the **lane plan** (the secondary register tier) for
> the **M5** milestone — the **coordination → outcome** arrow (the roadmap's
> "Projects — goals, tasks, contributors", here delivered as **project
> management**: assignable, hierarchical **to-do lists** and **Kanban boards**
> with statused, limitable lanes). The **primary reference tier** (the exact C#
> seams + the locked decisions) is the design doc
> `docs/design/m5-projects-design.md` (authored U00–U01); the **scratch tier**
> is `docs/plans-milestones/in-progress/projects/projects-handoff-notes.md`
> (one appended `## U#` section per unit, never rewritten).
>
> **What this is:** a **new bounded context** `Kumunita.Core.Projects` (the
> `TodoItem`, `KanbanBoard`, `KanbanLane`, `BoardItemPlacement` documents),
> **one new document surface** (`M5DocTypes`), **one new service seam**
> (`IProjectService`), **two new authorization adapters**
> (`TodoItemToAuditableResource`, `KanbanBoardToAuditableResource`), **one new
> Web controller** (`ProjectsController`) + views + nav, and **one new
> plain-TS module** (`client/lib/projects-board.ts`) for the board's
> reorder / dropdown actions. It is the **Projects/** context already sketched
> in `docs/ARCHITECTURE.md` §3 (marked "not yet created") and §5 (the
> `Project` / `ProjectTask` / `ProjectMember` sketch — this lane settles the
> canonical names below).
>
> **The one thing every unit must respect:** this milestone is **additive and
> reusing.** It reuses the `Audience` doc (ADR 0001-B / 0036 — *reused*, not
> extended) on **both** `TodoItem` and `KanbanBoard`, the frozen
> `IAuthorizationService.CanAsync(Read)` / `CanSeeAsync(Read)` decision path
> (ADR 0006) through the two new adapters, the standing matrix
> **creator ∪ assignee ∪ GlobalAdmin** (the ADR 0014 / 0016 / 0017 precedent,
> re-checked server-side), the **`MarkdownRenderer` + `bindRichEditor`**
> (ADR 0025 / 0031) for an optional to-do body, the **`kw-dt`** TagHelper
> (ADR 0019 / 0020) for timestamps, and the **existing profile-avatar surface**
> for the assignee chip. **No new `AccessAction`**, **no new `AccessVia`**,
> **no new authorization path**, **no editor dependency**, **zero migrations
> for new fields** (ADR 0004 §B.1 additive — the four docs are new, the surface
> is additive). **M5 is opened as the *next* lane** (it is already the single
> `StatusNext` on the roadmap — U01 confirms, not moves); **M6 stays
> Portability.**
>
> **Sizing:** units are sized for a **~32K-context fresh agent** one at a time,
> each with its own closed exit criteria, in the `M4` / `EV-DWM` / `GP` / `PG`
> style. **U00 is the sign-off gate** — it authors the design doc, locks the
> **[PROPOSED]** decisions into **ADR 0067**, and confirms the roadmap trio
> (U01); every later unit codes against the *locked* text. **Sequencing
> invariant:** the read lanes (U04) land before any write (U05) or
> placement/reorder (U06) lane; the Web controller (U07–U08) codes against the
> *frozen* `IProjectService` before any view (U09–U10); the plain-TS board
> module (U11) lands after the board view (U10) it wires; the pinned tests
> (U12) run before the gate (U13); the close (U13) is last so the doc index +
> README + ARCHITECTURE.md are honest at ship time.

## Understanding (one paragraph)

M3 shipped **signal** (posts, replies, announcements, group posts,
moderation); M4 shipped **coordination** (events, RSVPs, reminders). M5 is the
next arrow — **outcome**: a decision becomes *owned work with a shape* — a
resident writes a to-do, assigns it to a neighbor, breaks it into subtasks,
and arranges the neighborhood's shared work on a Kanban board with lanes like
"Planned / Doing / Done". Unlike the other content surfaces, a to-do is a
**standalone, assignable, hierarchical document** that is *also* placeable — on
zero, one, or many Kanban boards — and its position (a lane + an order within
that lane) is a **separate placement record**, not a field on the to-do. That
one split is the lane's defining move: the to-do *is* the work item (status,
assignee, subtasks, its own audience); the placement *is* where that item sits
on a particular board (which lane, and its order there). Moving a to-do into a
lane that carries a status **auto-updates the to-do's status** (the board is a
state machine the to-do flows through); a lane with a `MaxItems` limit
**refuses** to accept more. Standing over a to-do is **creator ∪ assignee ∪
GlobalAdmin** (the ADR 0014/0016/0017 precedent extended with the assignee as
a collaborator); read is the to-do's **own** `Audience` decision, and a to-do
rendered on a board is visible only if *both* the board and the to-do are
visible to the actor. The authorization path is the **frozen**
`IAuthorizationService` through the two new adapters; the audience is the
**exact** post `Audience`; an optional to-do body is rendered by the **one**
`MarkdownRenderer` and edited by the **one** `bindRichEditor`; every
timestamp renders through the **one** `kw-dt`; the assignee chip reuses the
**existing** profile-avatar surface; the board's reorder / dropdown actions are
**plain `client/lib` TS, tsc-only, zero dependencies** (the ADR 0031 pin).

## Assumptions

- **Scope (per user, settled by ADR 0067):** **to-dos** (create / list /
  detail; assignable to a user; a status; a standalone *and* placeable on one
  or more boards; hierarchical — subtasks; a status icon, an assignee avatar,
  the title, and a per-card dropdown menu: move up / down, move left / right,
  copy to (board), move to (board), assign to (user), remove / delete) and
  **Kanban boards** (create / list / detail; lanes; each lane an optional
  `MaxItems` limit + an optional `Status`; auto status-update on
  move-to-lane). **In:** the four documents, `M5DocTypes`, the
  `IProjectService` read + write + placement surface, the two
  `ToAuditableResource` adapters, `ProjectsController` + views + nav, the
  `client/lib/projects-board.ts` module + `site.css` + `kw-l` keys × 4
  languages, the pinned tests, the acceptance gate. **Out (→ M6 or a follow-on
  lane):** notifications on assignment (M6 owns notification surfaces), iCal /
  export (M6 Portability), due dates / reminders (a follow-on lane — M5 has
  *status*, not *time*), a per-project "goals" rollup (a follow-on lane — M5
  ships to-dos + boards, not a higher-level goal doc), subtask *progress
  rollup* to a parent (a display nicety — a follow-on lane), drag-and-drop
  *autosave* (M5's reorder / move / copy / assign are explicit server round
  trips — the ADR 0031 plain-GET/POST posture; no optimistic local
  reordering), and a resident-facing *board-per-project* grouping (a board is
  the unit; "project" is not a distinct doc in M5 — the design doc D2 settles
  this).
- **The canonical names (settled in ADR 0067, the M4 field-naming-decision
  precedent).** `docs/ARCHITECTURE.md` §5 sketches `Project { id, title,
  description, componentId?, ownerId, status, audience, created }`,
  `ProjectTask { id, projectId, title, assigneeId?, done, order }`,
  `ProjectMember { id, projectId, userId, role }`. This lane delivers the
  *same intent* under the user's framing, with these **canonical names**: the
  context is **`Kumunita.Core.Projects`** (the §3 tree's `Projects/` folder);
  the to-do doc is **`TodoItem`**; the board is **`KanbanBoard`**; a board's
  column is **`KanbanLane`**; the placement of a to-do on a board/lane is
  **`BoardItemPlacement`**. **`Project` / `ProjectTask` / `ProjectMember` do
  not exist** — the §5 sketch is the pre-decision shape; **the M5 close unit
  (U13) rewrites the ARCHITECTURE.md §5 `Projects` block to these names** and
  flips the §3 tree's `Projects/ (M5 — not yet created)` to the created
  context (per the AGENTS.md doc↔code parity rule), so no unit in U02–U12
  touches that file.
- **A to-do is standalone; placement is a separate record (C-M5·2 — the
  lane's defining pin).** `TodoItem` carries **no** `BoardId` / `LaneId` /
  `Order` field. Its presence on a board (and its position there) is a
  **`BoardItemPlacement`** row (`TodoItemId`, `BoardId`, `LaneId`, `Order`).
  A to-do may have **zero** placements (a pure to-do list item) or **many**
  (on several boards). **Move up/down** = reorder within a lane (change
  `Order` among that lane's placements); **move left/right** = move to the
  adjacent lane of the *same* board (change `LaneId`, reset `Order` to the
  end); **move to (board)** = the placement relocates (removed from the source
  board, added to the target board's first lane); **copy to (board)** = a new
  `TodoItem` copy + a placement on the target board (the original is
  untouched).
- **Status is a string, not an enum (the M4 `Location` / `Color`
  display-metadata shape).** `TodoItem.Status` is a **nullable string** (the
  state label; `null` = no status) and `KanbanLane.Status` is a **nullable
  string** (the status a lane *imparts* on a to-do moved into it). **No
  status enum, no status registry, no seeded status vocabulary** — the lane's
  vocabulary is whatever the board's lanes name (a board author names their
  lanes "Planned" / "Doing" / "Done" and sets each lane's optional `Status`
  text; the icon is a client-side *display* mapping from the status string to
  an icon, never persisted). This keeps the model data-driven and avoids a
  second frozen-vocabulary surface. **The status icon is a client-side
  display concern (C-M5·9).**
- **Lane limits are advisory backstops, refused not dropped (C-M5·5).**
  `KanbanLane.MaxItems?` (nullable int; `null` = no limit) caps the number of
  placements a lane may hold. A move/copy **into** a lane already at its
  limit is **refused** (the actor sees a message; nothing is written). A
  **reorder within** a lane, and a move *out of* a lane, never trip the limit
  on the source. The limit counts **placements in that lane** (a to-do placed
  on two boards is counted once per board's lane).
- **Hierarchy is `ParentId` only; a subtask is a full to-do (C-M5·7).**
  `TodoItem.ParentId?` is the **sole** hierarchy mechanism — a subtask is a
  `TodoItem` with `ParentId` set, with its **own** status / assignee /
  placements (a subtask can sit on a board just like a top-level to-do).
  **Delete cascades:** soft-deleting a to-do also soft-deletes its **descendant
  subtree** (the to-dos transitively reachable via `ParentId`). **A to-do with
  children cannot be re-parented to create a cycle** (the service refuses a
  `ParentId` that would make the target a descendant of the to-do). **Subtasks
  are listed under their parent** in the to-do detail view and may also appear
  as their own cards on a board (placement is orthogonal to hierarchy).
- **Standing is creator ∪ assignee ∪ GlobalAdmin, re-checked server-side
  (C-M5·6, the ADR 0014/0016/0017 precedent).** The Web `[Authorize]` is a
  convenience pre-gate only, **never** the source of truth; the
  `IProjectService` enforces the matrix for every mutating lane (edit title /
  status / assignee, add subtask, reorder, move, copy, delete, and lane CRUD).
  The **creator** is `TodoItem.AuthorId` / `KanbanBoard.AuthorId` (the standing
  owner); the **assignee** is `TodoItem.AssigneeId` (a collaborator — assigning
  a to-do to a resident gives that resident standing over it); **GlobalAdmin**
  is the override branch. `TodoItem.AssigneeId` is a **`SubjectId`** (nullable
  — `null` = unassigned); it is *display + standing*, never an access gate
  (read is the `Audience` decision — C-M5·3).
- **Read access is the to-do's own `Audience`; the board is a container with
  its own `Audience` (C-M5·3).** Both `TodoItem` and `KanbanBoard` carry the
  **exact** post `Audience` (ADR 0001-B / 0036; `null` = public) and each has
  its **own** adapter (`TodoItemToAuditableResource` → `TargetKind "todo"`,
  `KanbanBoardToAuditableResource` → `TargetKind "board"`). A **standalone**
  to-do list / detail is gated by the to-do's own `Audience` decision (one
  `CanSeeAsync(Read)` over the list — the C-M3·3 aggregate shape; one
  `CanAsync(Read)` on the detail — the C3 404-vs-403 split). A **board** view
  is gated by the board's `Audience` first (entry), then each card by the
  to-do's own `Audience` (a to-do on the board is visible iff **both** are
  visible). A placement row is **not** itself an auditable resource — it
  inherits the visibility of the to-do it points at (no separate
  `BoardItemPlacementToAuditableResource`).
- **The to-do body is rich content, optional (ADR 0025 / 0031).** `TodoItem`
  has a **`Title`** (required, the card label + the adapter `Name`) and an
  optional **`Body`** (Markdown; when present it is rendered by the **one**
  `MarkdownRenderer` and edited by the **one** `bindRichEditor` — the
  post / announcement / event composer surface verbatim). A to-do is usable
  with **title only** (the common case — "Buy milk"); the body is optional.
  **No new renderer, no new editor, no new TS editor module.**
- **Timestamps are `kw-dt` (ADR 0019 / 0020)** — `TodoItem.Created` /
  `Modified?` and `KanbanBoard.Created` render through the **one** `kw-dt`
  TagHelper. **No new timezone or format mechanism.**
- **Localization parity (C-M5·10).** The new `kw-l` keys (the to-do / board /
  lane nouns, the status-icon fallback, the dropdown-action labels
  — move up / down / left / right, copy to, move to, assign, delete; the lane
  "limit" + "status" labels; the board empty-state) are present in **all four**
  seeded languages (en/de/fr/da — the ADR 0042 / 0045 set), English fallback in
  every view. The key registry shape (ADR 0015) is the host.
- **Plain `client/lib` TS, tsc-only, zero dependencies (C-M5·9, the ADR 0031
  pin).** The board's reorder (move up / down / left / right), the
  copy-to / move-to pickers, the assign picker, the status icon, and the
  dropdown menu are **one new plain-TS module** (`client/lib/projects-board.ts`)
  — no bundler, no UI library (the `package.json` `tsc`-only pin). Every
  action is an **explicit server round trip** (a small POST, CSRF-aware — the
  M2/M3 `client/lib/api.ts` fetch shape), then a full re-render; **no**
  optimistic local reordering, **no** HTML5 drag events (keyboard / menu
  driven — the ADR 0031 "explicit, server-authoritative" posture).
- **Test model (unchanged).** xunit.v3, run via `dotnet exec …dll` per
  AGENTS.md (**not** `dotnet test` / VS Test Explorer on this machine);
  `Kumunita.Core.Tests` = `PostgresFixture`; `Kumunita.Web.Tests` = NSubstitute
  (no Postgres). The pinned seam tests (Core: the FACES over the service) + the
  Web controller pins are named in the design doc Part 2 (U01); the
  three-test acceptance gate (closed-loop / handoff / part-vs-whole) is
  recorded by U13.

## Approach

Four tracks, sequenced — exactly like M4. **Track A (Core model, U02–U03):**
the four POCOs in `Kumunita.Core.Projects`, the `M5DocTypes` registration +
boot wiring (U02), and the two `ToAuditableResource` adapters (U03).
**Track B (Core service, U04–U06):** `IProjectService` — U04 declares the
**full** interface (read + write + placement signatures, verbatim from the
design doc) + the **read-lane** impl (standalone to-do list / detail with
subtasks, board list / detail with lanes + placements), with the write +
placement methods as `NotImplementedException` stubs (the M4 U01
interface-registered-first pin); U05 fills the **write lanes** (create / edit /
assign / add-subtask / delete on to-dos; board create / lane CRUD / delete);
U06 fills the **placement + reorder lanes** (move up / down, move left / right
with the lane-status auto-update, move-to-board, copy-to-board, the
lane-limit refusal, the hierarchy cycle guard). **Track C (Web, U07–U11):**
`ProjectsController` — U07 the to-do routes (list / detail / compose / the
to-do action endpoints) + the to-do view models; U08 the board routes (board
list / detail / lane CRUD / the placement endpoints) + the board view model;
U09 the to-do Razor views + the nav entry; U10 the board view (lanes as
columns, cards with the status icon + assignee avatar + title + the dropdown
menu, the lane toolbar); U11 the `client/lib/projects-board.ts` module + the
`site.css` rules + the `kw-l` keys × 4 languages. **Track D (Tests + gate,
U12–U13):** U12 the pinned seam tests (Core + Web); U13 the acceptance gate +
the close (roadmap trio, ARCHITECTURE.md §3+§5 sync, README, handoff summary,
and moving this lane's unit files from `in-progress/projects/` →
`done/projects/`).

Every unit ends with **build green** (and `tsc` green from U11). The last
unit (U13) appends the final handoff section + moves the lane's unit files
into `done/projects/` so the roadmap / ADR / ARCHITECTURE.md pin is honest at
ship time.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U00–U13 below), one
unit per fresh agent with a **~32K context window**.

**Shared state (three-tier contract):**

- **Primary — the design doc** (`docs/design/m5-projects-design.md`, authored
  U00–U01): pins the exact C# shapes of every document / adapter / service
  method every unit codes against, the invariant table, the pinned test names,
  and the acceptance-gate shape. U00–U01 are the **sign-off gate** — **ADR
  0067** (Accepted) is the decisions source; the design doc turns it into exact
  shapes.
- **Secondary — this file** (`docs/plans-milestones/plan-m5-projects.md`) —
  the lane register: understanding, assumptions, invariants, FACES, and the
  unit index (each pointing at its unit file).
- **Unit files — `docs/plans-milestones/in-progress/projects/U0#.md`** — one
  file per unit with the full **Goal / Entry reads / Deliverables / Exit**.
  **On a unit's completion its file is moved
  `in-progress/projects/U0#.md` → `done/projects/U0#.md`** (the per-lane
  subfolder follows the existing `done/m4/` / `done/ev-dwm/` convention and
  avoids cross-lane `U0#` collisions). **The lane register stays at
  `docs/plans-milestones/plan-m5-projects.md`** (it does not move with the
  units — the lane folder it registers is `in-progress/projects/`, which
  becomes `done/projects/` when the lane closes). When the lane closes (U13)
  the whole `in-progress/projects/` unit-file set moves to
  `done/projects/`.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/projects/projects-handoff-notes.md`).
  One `## U#` section per unit, appended (never rewritten). Each unit writes
  exactly one short section before it exits; the next unit reads only that
  section + its own entry-read list. Moves with the lane folder at close.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, ≤ 5
files <~300 lines each, no full-repo scan; the design-doc section cited is
named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files /
~600 LOC, no misc cleanups); **Exit** (build green for the touched projects +
`tsc` green from U11; handoff-note entry appended *before* any follow-up
action; the unit file moved `in-progress/projects/` → `done/projects/`).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard
unit (U13); (3) never introduces a test whose exact name is not in the design
doc's pinned list (U12); (4) **the `IProjectService` interface is frozen in
U04** (all read + write + placement signatures, verbatim from the design doc)
— U05 / U06 **implement** the stubbed methods, they do **not** re-shape the
signatures; (5) never re-shapes a document (`TodoItem`, `KanbanBoard`,
`KanbanLane`, `BoardItemPlacement`) outside the design-doc §2 pin; (6) never
adds a seam on `IAuthorizationService` / `IUserInfoService` / `IIdentityService`
beyond the **reuse** of the frozen `CanAsync(Read)` / `CanSeeAsync(Read)` + the
frozen profile-read seams for display lookups (there is **no** ADD in this
lane); (7) never introduces a UI library or a bundler (plain `client/lib` TS,
tsc-only — the ADR 0031 pin); (8) never re-derives an access decision in the
Web (the `IProjectService` is the single authorization path — ADR 0006-D); (9)
if entry reads reveal the design doc is out of date, the unit pauses and
records `## U<m> — Drift pause` in the handoff note.

---

## The invariants (pinned for `M5`)

- **C-M5·1 — One bounded context, four documents, one surface, additive.**
  `Kumunita.Core.Projects` with `TodoItem`, `KanbanBoard`, `KanbanLane`,
  `BoardItemPlacement`; one new surface `M5DocTypes` (the `M4DocTypes` pattern
  verbatim); Marten-native POCOs, conventional `string` `Id`, delta-detected +
  idempotent, no seeding, no EF, zero migrations for existing surfaces (ADR
  0004 §B.1). *(ADR 0067 D1.)*
- **C-M5·2 — A to-do is standalone; placement is a separate record.**
  `TodoItem` carries **no** `BoardId` / `LaneId` / `Order`; its presence on a
  board (and its position) is a `BoardItemPlacement` row. A to-do has zero or
  many placements (on several boards). Move up/down = reorder within a lane;
  move left/right = change lane (same board); move-to = relocate the placement;
  copy-to = duplicate the to-do + place the copy. *(D3.)*
- **C-M5·3 — Read is the to-do's own `Audience`; the board is a container.**
  `TodoItem` and `KanbanBoard` each carry the **exact** post `Audience` (ADR
  0001-B / 0036; `null` = public) and each has its own adapter
  (`TargetKind "todo"` / `"board"`). A standalone to-do list / detail is gated
  by the to-do's `Audience` (C-M3·3 aggregate / C3 404-vs-403). A board view is
  gated by the board's `Audience` (entry), then each card by the to-do's own
  `Audience` (visible iff **both**). A `BoardItemPlacement` is not itself an
  auditable resource (it inherits its to-do's decision). *(D4.)*
- **C-M5·4 — Lane status auto-update is a write, audited, the single status
  mechanism.** Status is a **nullable string** on `TodoItem` and
  `KanbanLane` (no enum, no registry). Moving a to-do to a lane whose
  `Status` is non-null sets `TodoItem.Status` to that lane's status, in the
  same transaction as the placement move (C3). A lane with null `Status`
  leaves the to-do's status unchanged. The **status icon is a client-side
  display** mapping (C-M5·9), never persisted. *(D5.)*
- **C-M5·5 — Lane limits are advisory backstops, refused not dropped.**
  `KanbanLane.MaxItems?` (`null` = no limit) caps the placements a lane holds;
  a move/copy **into** a lane at its limit is **refused** (a message; nothing
  written). Reorder within a lane and move *out* never trip the source limit.
  The limit counts placements in that lane. *(D6.)*
- **C-M5·6 — Standing is creator ∪ assignee ∪ GlobalAdmin, re-checked
  server-side.** The Web `[Authorize]` is a convenience pre-gate only;
  `IProjectService` enforces the matrix for every mutating lane (the ADR
  0014 / 0016 / 0017 precedent). The creator is `AuthorId` (the standing
  owner); the assignee is `AssigneeId` (a collaborator — assignment grants
  standing); GlobalAdmin is the override branch. `AssigneeId` is display +
  standing, **never an access gate** (read is the `Audience` decision —
  C-M5·3). *(D7.)*
- **C-M5·7 — Hierarchy is `ParentId` only; a subtask is a full to-do.**
  `TodoItem.ParentId?` is the sole hierarchy mechanism; a subtask has its own
  status / assignee / placements. **Delete cascades** to the descendant
  subtree. A `ParentId` that would create a cycle is refused. Subtasks list
  under their parent and may also appear as their own cards. *(D8.)*
- **C-M5·8 — Copy-to duplicates; move-to relocates.** "Copy to (board)" = a
  new `TodoItem` copy (title / status / assignee) + a placement on the target
  board's first lane; the original is untouched. "Move to (board)" = the
  placement is removed from the source board and added to the target board's
  first lane (the to-do is then on the target board, not both). *(D9.)*
- **C-M5·9 — Plain TS, zero deps, explicit server round trips.** tsc-only
  (ADR 0031); **no UI library, no bundler**; `client/lib/projects-board.ts` is
  the one new module. Every action (reorder / move / copy / assign / delete)
  is an **explicit POST**, CSRF-aware (the `client/lib/api.ts` shape), then a
  full re-render — **no** optimistic local reordering, **no** HTML5 drag
  events (keyboard / menu driven). The status icon + assignee avatar +
  dropdown are plain DOM / JS. *(D10.)*
- **C-M5·10 — Localization parity.** The new `kw-l` keys (to-do / board / lane
  nouns, the action labels, the lane limit / status labels, the board
  empty-state) are present in **all four** seeded languages (en/de/fr/da);
  English is the fallback in every view; the ADR 0015 key-registry shape is
  the host. *(D11.)*
- **C-M5·11 — Reuse, don't reinvent.** The `Audience` doc, the frozen
  `IAuthorizationService`, the standing matrix, the `MarkdownRenderer` +
  `bindRichEditor`, the `kw-dt` TagHelper, the profile-avatar surface are all
  **frozen seams** — M5 adds **adapters + a service + views + one TS module**,
  not a branch. **No new `AccessAction`**, **no new `AccessVia`**, **no new
  authorization path**, **no editor dependency**. *(D12.)*

## FACES (pinned, 10)

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

## The pinned tests (pinned in detail by U01)

**~16 Core pins** (`tests/Kumunita.Core.Tests/ProjectServiceTests.cs`):
`F1_TodoVisibleToAudienceMember`, `F1_TodoHiddenFromNonMember`,
`F2_TodoPlacedOnMultipleBoards_AppearsOnEach`,
`F3_BoardGate_TodoOnBoard_BoardDenies_HidesCard`,
`F3_BoardGate_TodoOnBoard_BoardAllows_ShowsCard`,
`F4_MoveToStatusLane_SetsTodoStatus_Audited`,
`F5_MoveToNullStatusLane_TodoStatusUnchanged`,
`F6_LaneAtMaxRefusesMoveIn`, `F6_LaneBelowMaxAllowsMoveIn`,
`F7_AssigneeHasStanding_UpdatesTodo`,
`F8_NonStandaloneActorRefused`, `F9_SubtaskIsFullTodo_OwnStatusAssignee`,
`F9_DeleteCascadesToSubtree`, `F9_ReparentToDescendant_Refused`,
`F10_CopyToBoard_Duplicates_OriginalUntouched`,
`F10_MoveToBoard_RelocatesPlacement`.

**~8 Web pins** (appended to `tests/Kumunita.Web.Tests/ProjectsControllerTests.cs`):
`Todos_List_AudienceFiltered`, `Todo_Detail_SubtasksRendered`,
`Board_Detail_LanesAndCards`, `Board_MoveLeftRight_TriggerStatusUpdate`,
`Board_LaneAtMax_RefusesMoveIn`, `Board_CopyToBoard_Duplicates`,
`Board_MoveToBoard_Relocates`, `Todo_AssignToUser_StandingGranted`.

**The three-test acceptance gate** (recorded by U13): **closed loop** (an
author creates a to-do → it appears in their to-do list and, when placed, on
the board with the right card + one aggregate row), **handoff** (a to-do
assigned to a second resident — the assignee sees it on the board and has
standing to edit it on the next render — the non-assignee's view / standing
still exclude it), **part-vs-whole** (the ~24 pinned tests pass together with
the full M4 `EventServiceTests` + the existing post / announcement / page pin
suites still green — the lane is additive, nothing regressed).

---

## Units (14 total)

> Each unit's full **Goal / Entry reads / Deliverables / Exit** lives in its
> own file: `docs/plans-milestones/in-progress/projects/U0#.md`. On completion
> the file moves to `docs/plans-milestones/done/projects/U0#.md` (see the
> handoff protocol above).

| Unit | Goal (one line) | File |
|------|-----------------|------|
| **U00** | Design doc Part 1 — context, scope, decisions D1–D12, invariants C-M5·1…11, FACES F1–F10. No code. | `U00.md` |
| **U01** | Design doc Part 2 (exact C# shapes, the full `IProjectService` surface, the ~24 pinned tests, gate, drift-guard) + **ADR 0067** + **roadmap confirm** (M5 is already the single `StatusNext`; U1 stays `StatusNext`, M6 stays `StatusPlanned`). | `U01.md` |
| **U02** | `TodoItem`, `KanbanBoard`, `KanbanLane`, `BoardItemPlacement` POCOs + `M5DocTypes` registration + boot wiring (both boot paths). | `U02.md` |
| **U03** | `TodoItemToAuditableResource` + `KanbanBoardToAuditableResource` adapters (the two `TargetKind`s: `"todo"` / `"board"`). | `U03.md` |
| **U04** | `IProjectService` — the **full** interface (read + write + placement signatures, verbatim) + the **read-lane** impl (standalone to-do list / detail + subtasks; board list / detail + lanes + placements); write + placement as `NotImplementedException` stubs. | `U04.md` |
| **U05** | The **write lanes** — to-do create / edit (incl. reparent + the hierarchy cycle guard) / assign / add-subtask / delete (cascading); board create (with lanes) / lane update (title / `Status` / `MaxItems` / order) / board delete (cascades lanes + placements). Standing re-checked server-side. | `U05.md` |
| **U06** | The **placement + reorder lanes** — move up/down (reorder), move left/right (change lane + the lane-status auto-update), move-to-board, copy-to-board, the lane-limit refusal. | `U06.md` |
| **U07** | `ProjectsController` — the to-do routes (`/projects/todos`, `/projects/todos/new`, `/projects/todos/{id}` + the to-do action endpoints) + the to-do view models. | `U07.md` |
| **U08** | `ProjectsController` — the board routes (`/projects/boards`, `/projects/boards/new`, `/projects/boards/{id}` + lane CRUD + the placement endpoints) + the board view model. | `U08.md` |
| **U09** | The to-do Razor views (list / detail / compose) + the nav entry + the to-do dropdown (assign / add-subtask / delete). | `U09.md` |
| **U10** | The board view (lanes as columns; cards with the status icon + assignee avatar + title + the full dropdown menu; the lane toolbar for limit / status) + the board composer. | `U10.md` |
| **U11** | `client/lib/projects-board.ts` (reorder / move / copy / assign / delete, keyboard + menu, explicit POSTs, CSRF-aware) + the `site.css` board rules + the `kw-l` keys × 4 languages. | `U11.md` |
| **U12** | The **~24 pinned tests** — ~16 Core pins + ~8 Web pins. No gate. | `U12.md` |
| **U13** | **Acceptance gate + close** — run + record the three-test gate; roadmap trio close (M5→`StatusDone`, M6→`StatusNext`); ARCHITECTURE.md §3 tree + §5 `Projects` block sync; README; handoff summary; move the unit files `in-progress/projects/` → `done/projects/`. | `U13.md` |
