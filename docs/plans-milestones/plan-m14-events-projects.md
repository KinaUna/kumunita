# Plan: M14 — Integration of Events and Projects

> **In progress** (M14 is the single in-progress milestone — `Milestones.cs`
> already says `StatusNext`; this register is its unit plan). Unit register
> (secondary tier). Living handoff note:
> `docs/plans-milestones/in-progress/m14-events-projects-handoff-notes.md`
> (scratch tier — one `## U#` section per unit, appended, never rewritten).
> The authoritative design (primary tier) — `docs/design/m14-events-projects-design.md`
> — is authored by **U00** and locked before any code unit runs; the decision
> record is **ADR 0115** (the next free number — 0112 = iCal, 0113 =
> nav-row overflow, 0114 = logging/analytics; **confirm it is free against
> `docs/adr/README.md` before writing**).
>
> **Unit plans (this convention):** each unit ships its own self-contained
> plan file in `docs/plans-milestones/in-progress/`
> (`m14-u00.md` … `m14-u07.md`). When a unit is done, its plan file moves to
> `docs/plans-milestones/done/`. A unit agent reads **its own plan file +
> its entry reads** — it does not need to re-derive this register, which is
> why each unit plan restates the context it needs.
>
> **U00 is the sign-off gate** for the design decisions and the invariant
> contract; the decisions below are the [PROPOSED] set U00 locks (or the
> user vetoes before U00 runs — this register is the last cheap place to
> change them).
>
> **Atomicity contract.** This register is sized for **~32K-context
> agents**: every unit is one coherent step — a short entry-reads list
> (4–8 files), a tight deliverables list (≤ 9 small files), and an Exit
> check that fits in one build + test run. A unit's full context (its
> unit-plan file + its entry reads + its deliverables) fits in one 32K
> window with headroom.
> **Unit-series rule: never touch files outside your own Deliverables;
> never rewrite the design doc outside the drift-guard note; no tests
> beyond the pinned list; **no new authorization surface**
> (`AccessAction`/`AccessVia`/`Decide()` branch — C-M14·4); **no `RRULE` /
> recurrence of any kind anywhere in M14** (C-M14·6 / D6 — that is M18); the
> `EventId` link is **a filter/association, never a gate** (C-M14·1); the
> `VTODO` file **never carries a decision** (C-M14·5, the ADR 0028 posture
> applied to the file form, the C-M12·2 pin carried to the to-do surface).**

> **M14 is an interlock on two already-shipped surfaces.** There is **no
> `EventId` on `TodoItem`, no to-dos-in-a-calendar surface, and no cross-
> surface display link** anywhere in the tree (grep-confirmed before U00;
> U00 re-confirms against the live tree). What M14 builds on: the **frozen
> `IEventService` read seams** (`ListUpcomingAsync` / `GetAsync` /
> `ListMineAsync` in `Kumunita.Core/Events/IEventService.cs` — the
> event→side reads M14 composes against), the **frozen `IProjectService`
> read + write lanes** (`ListTodosAsync` / `GetTodoAsync` /
> `ListBoardsForTodoAsync` / `CreateTodoAsync` / `UpdateTodoAsync` in
> `Kumunita.Core/Projects/IProjectService.cs` — the `ListTodosForEventAsync`
> reverse seam + the `set-event` lane copy the `set-project` /
> `ListBoardsForTodoAsync` shapes verbatim), the **`ProjectId` field on
> `TodoItem`** (`Kumunita.Core/Projects/TodoItem.cs` — the *association,
> never a gate* shape the new `EventId?` copies), the **`ProjectLink` /
> `BlockerChip` dangling-safe display idiom**
> (`Kumunita.Core/Projects/ProjectRequests.cs` — the to-do→event and
> event→to-do links copy its access-scoped / omitted-when-unreadable rule),
> the **`M5DocTypes` registration surface**
> (`Kumunita.Core/M5DocTypes.cs` — the additive `(EventId)` feed index joins
> the existing `(ProjectId)` / `(ComponentId, Created)` indexes), the
> **`IcsWriter` BCL-only closed-subset emitter**
> (`Kumunita.Core/Events/IcsWriter.cs` — the `VTODO` emitter copies its
> CRLF / fold / escape / stable-UID / no-decisions discipline), and the
> **two M12 iCal routes on `EventController`** (`GET /events/{id}.ics` +
> `GET /events.ics` at `EventController.cs` ≈ L822 / L898 — the `VTODO`
> routes copy the `text/calendar` serve idiom + the ADR 0034/0108 header
> shape). M14 is the **linkage** the README's "Integration over features"
> principle names: it adds one field, one reverse read seam, one write
> lane, both-direction display links, and — if D4 stands — a `VTODO`
> calendar surface. It adds **no new bounded context, no new authorization
> surface, no recurrence concept.**

---

## Understanding

The platform now has two **time-bearing coordination surfaces** that a real
neighborhood uses as one thing: **Events** (M4 — "the cleanup day is
Saturday, who's going?") and **Projects** (M5 — "who's bringing what, and
has it happened yet?"). Today they are **two separate surfaces that never
point at each other**: a resident who plans an event cannot link the
prep to-dos to it, and the to-dos (which already carry an optional
`DueAt` / `StartAt` — ADR 0079) **do not appear in any calendar** even
though the events do (M12 iCal, `IcsWriter`). ARCHITECTURE.md's value-chain
row names M14 exactly: "**M14 integration of Events and Projects |
coordination — the two coordination surfaces interlock**." And the README
principles are explicit that this interlock **is the product**, not a
feature: "**Integration over features.** … Kumunita's value is the
*linkage* that turns those parts into a whole, not the count of parts."

M14 makes that linkage concrete, in two faces:

1. **The data interlock** — a to-do can be **associated with an event**
   ("prep for the cleanup" is a to-do *for* "cleanup day"). This is one
   additive `EventId?` field on `TodoItem` (the `ProjectId` shape: an
   association and a feed-filter, **never an access gate**), one reverse
   read seam (`ListTodosForEventAsync` — "the to-dos linked to this
   event"), one dedicated write lane (`set-event`, the `set-project` shape),
   and **both-direction display links** on the detail pages (the event
   detail shows its linked to-dos; the to-do detail shows its linked event)
   — each **access-scoped and dangling-safe** (an unreadable / absent /
   soft-deleted target is omitted entirely, never a 404/403, the
   `ProjectLink` / `BlockerChip` idiom). This is the *linkage the
   principle names*: two surfaces that now refer to each other.
2. **The calendar interlock (D4, [PROPOSED])** — the to-dos' existing
   optional dates (`DueAt` / `StartAt`) become a **standard iCal `VTODO`
   surface** the resident's calendar app can import / subscribe to:
   `GET /projects/todos.ics` (the caller's visible dated to-dos) +
   `GET /projects/todos/{id}.ics` (one), emitted by a **`TodoIcsWriter`**
   that copies the `IcsWriter` BCL-only closed-subset discipline (CRLF,
   75-octet fold, full escaping, stable `UID`, **no decision in the file**).
   This is the *other* half of the interlock: the two **time-bearing**
   surfaces now meet in the calendar's native format. ADR 0112 deferred
   `VTODO` as "a different calendar surface, own lane" — M14 is the natural
   home for exactly that deferral, because the calendar **is** the Events
   surface's format, so "to-dos in the calendar" is literally
   "Events ∩ Projects."

That is the whole surface. What M14 is **not** (deferred, named in ADR 0115's
Consequences, own lanes / own milestones):

- **Recurring events / `RRULE` — M18's home, NOT M14's** (D6).
  `Milestones.cs` (the source of truth, pinned by `MilestonesTests.cs`)
  assigns **M18 = "Recurring events."** ADR 0112 + the `IcsWriter.cs`
  doc-comment reference `RRULE` as "M14's home" — those are **stale
  forward-refs** (written before the roadmap settled M18); U00 records the
  clarification in the design doc + ADR 0115 and M14 emits **no** `RRULE`
  and adds **no** recurrence field to `Event` (C-M14·6).
- **`RRULE` on the `VTODO` surface** — the `TodoIcsWriter` (D4) emits a
  *single* `VTODO` per to-do; recurrence of a to-do is a follow-on lane
  (it would ride M18's recurrence concept, if that ever extends past
  events).
- **ICS import** (round-tripping a calendar file *into* Kumunita — ADR 0112's
  "own lane" deferral, unchanged).
- **Per-component calendar feeds** (a `?component=` scoping of the `VTODO`
  feed — a scope generalization, own lane; the M14 feed is the actor's
  visible set, the M12 feed shape).
- **Many-to-many event↔to-do** — if the user vetoes D1's 1:1 field, the
  many-to-many `EventTodoLink` doc is the alternative (own design decision
  U00 records); the 1:1 field is the default (the `ProjectId` precedent).

## Assumptions / decisions — [PROPOSED, lockable by ADR 0115 in U00]

> **Open veto.** These are the decisions the user can still change cheaply
> — **before U00 runs**. After U00 they are locked by
> `docs/design/m14-events-projects-design.md` + ADR 0115 and changeable
> only via the drift guard.

- **D1 · The interlock is a field, not a link doc.** One additive
  `public string? EventId { get; set; }` on `TodoItem` (the `ProjectId`
  shape — "a feed filter / association, **never a gate**", C-M14·1;
  `TodoItem.cs` already carries `ProjectId` / `ComponentId` in exactly this
  shape). **1:1** — one event per to-do, "a to-do is *for* one event" (the
  `ProjectId` "a to-do is *in* one project" precedent). An optional
  `(EventId)` feed index joins `M5DocTypes` (the `(ProjectId)` index
  precedent — additive, delta-detected, zero migration for existing rows,
  C-M14·7). **[Veto: a separate many-to-many `EventTodoLink` doc — then
  `ListTodosForEventAsync` reads the link table instead of the field; the
  rest of the plan is unchanged.]**
- **D2 · Both display directions are Web-layer composition over the two
  frozen read seams — no Core cross-context dependency.** The **to-do →
  event** link resolves the event through the **frozen**
  `IEventService.GetAsync(eventId, actorId)` (the 404-vs-403 split is the
  seam's; the Web layer treats a `KeyNotFoundException` /
  `UnauthorizedAccessException` / `IsDeleted` as "omit the chip", the
  `ProjectLink` idiom). The **event → to-dos** link resolves through a
  **new reverse read seam** `IProjectService.ListTodosForEventAsync(string
  eventId, string actorId, int page, CancellationToken ct)` — the
  `ListTodosAsync` shape (candidate `!IsDeleted && EventId == eventId` →
  `CanSeeAsync(Read)` over the existing `TodoItemToAuditableResource` →
  `Created` descending → `TodoPage`/`HasMore` + one aggregate
  `AccessAudit` row `TargetKind="todo"`), **the `ListBoardsForTodoAsync`
  "list X for a Y" precedent**. The Web layer (the controller) is the
  composition point — the same place the M6 notification card composes
  `ListBoardsForTodoAsync`. **No `Kumunita.Core.Events` → `Kumunita.Core.
  Projects` dependency is introduced** (the "a bounded context per concern"
  rule, ADR 0004 §B.1 — the composition stays in `Kumunita.Web`).
- **D3 · The write lane is a dedicated `set-event` lane (the `set-project`
  shape), NOT a create/edit form field.** `IProjectService.
  SetTodoEventAsync(string todoId, string actorId, IReadOnlySet<string>
  actorRoles, string? eventId, CancellationToken ct)` — standing
  **creator ∪ assignee ∪ GlobalAdmin** (C-M5·6, the ADR 0014/0016/0017
  precedent, re-checked server-side); the to-do must exist and be
  `!IsDeleted` (`KeyNotFoundException` 404); a non-existent /
  soft-deleted / unreadable `eventId` is refused (`KeyNotFoundException` —
  you can only link to an event you can *see*; the standing decision is
  the frozen `IAuthorizationService.CanAsync(Read)` path); a `null`
  `eventId` **clears** the link. One `AccessAudit` row
  (`todo.set_event`, `TargetKind = "todo"`), committed atomically (C3).
  Web: `POST /projects/todos/{id}/set-event` on `ProjectsController` (the
  `POST /projects/todos/{id}/set-project` lane at ≈ L1303 — the
  `[ValidateAntiForgeryToken]` + `eventId` form field + the actor's visible
  event picker shape, `null` posts as the clear affordance). **`EventId` is
  not on `CreateTodoRequest` / `UpdateTodoRequest`** (the `ProjectId`
  precedent — association is a dedicated lane, not the main form).
- **D4 · [VETO-ABLE] The `VTODO` calendar surface is in M14.** A
  **`TodoIcsWriter`** (a new static class in `Kumunita.Core/Events/`,
  beside `IcsWriter`) — **BCL-only, pure** (the D1/`IcsWriter` discipline):
  `BuildTodos(IReadOnlyList<TodoItem> todos, DateTimeOffset nowUtc)` → the
  entire `VCALENDAR` with one `VTODO` per **dated** to-do
  (`DueAt != null || StartAt != null`; a to-do with neither is skipped — a
  `VTODO` with neither `DUE` nor `DTSTART` is a valid-but-pointless
  component, the "only include what's meaningful" `IcsWriter` idiom). The
  closed `VTODO` subset: `UID` (`kw-todo-{Id}@kumunita`, stable) /
  `DTSTAMP` (caller's `nowUtc`) / `DUE` (`DueAt`, only when set) /
  `DTSTART` (`StartAt`, only when set) / `SUMMARY` (`Title`) /
  `DESCRIPTION` (`Body`, only when non-empty) / `STATUS` (only when
  `IsDeleted` — the RFC update path, an emitter *capability* never reached
  through the frozen read seams). CRLF / 75-octet fold / full escaping /
  stable `UID` — copied from `IcsWriter`. **Never emitted (the
  "no decision in the file" pin, C-M14·5 / ADR 0028):** `ORGANIZER` /
  `CREATED-BY` / `ATTENDEE` (no author or assignee identity in a file that
  travels), `RRULE` / `RECURRENCE-ID` (C-M14·6 / D6), `Audience` / grant /
  membership internals, and any `X-…` vendor property. Web: **two
  `[Authorize]` routes on `ProjectsController`** — `GET
  /projects/todos.ics` (the frozen `ListTodosAsync` caller's visible set,
  filtered to dated to-dos, always `200` — the M12 feed-lane shape) +
  `GET /projects/todos/{id}.ics` (the frozen `GetTodoAsync`'s
  404-not-403 split, one `VTODO`). Serve idiom: `text/calendar;
  charset=utf-8` + `Content-Disposition: attachment` + `Cache-Control:
  no-store` + `X-Content-Type-Options: nosniff` (the ADR 0034/0108 idiom).
  **No subscription token** — the feed rides the cookie, re-authorized
  every fetch (the M6 link-lane posture). Two quiet `kw-l` affordances ×
  en/de/fr/da (`projects.todos.ics.download` "Add to calendar" +
  `projects.todos.ics.feed` "Calendar feed (iCal)" — plain `<a>` links, no
  new JS). **[Veto: defer `VTODO` to its own lane — then M14 = the
  data interlock only, U05/U06 are skipped, and the parity unit keeps its
  number (U07).]**
- **D5 · No new authorization surface (C-M14·4, C-M5·11).** No new
  `AccessAction`, no new `AccessVia`, no new branch in `Decide()`. M14
  adds an **association + read lane + write lane + emitter**, all over the
  **existing** `TodoItemToAuditableResource` / `EventToAuditableResource`
  adapters and the **frozen** `IAuthorizationService.CanAsync` /
  `CanSeeAsync(Read)` path. The `set-event` standing **reuses** C-M5·6
  (creator ∪ assignee ∪ GlobalAdmin) — it does not invent a new one.
- **D6 · Scope boundary: `RRULE` / recurring is **M18's home, NOT M14's**.**
  `Milestones.cs` (source of truth, pinned by `MilestonesTests.cs`) assigns
  **M18 = "Recurring events."** ADR 0112 + the `IcsWriter.cs` doc-comment
  call `RRULE` "M14's home" — **stale forward-refs** (the roadmap settled
  M18 after they were written). U00 records this in the design doc + ADR
  0115 Consequences: M14 emits **no** `RRULE`, adds **no** recurrence field
  to `Event` (C-M14·6), and the two references are noted as to-be-
  superseded by M18's ADR. **This is the single hardest scope pin of the
  milestone** — a unit that starts adding a recurrence field is off-plan.
- **D7 · The invariants + FACES + the pinned test list (locked in the
  design doc).** See the Invariants (C-M14·1…C-M14·7) and FACES (F1–F5)
  sections below; U00 writes the **pinned test names verbatim** (U01 / U02 /
  U05 / U06 / U07 each carry theirs) + the three acceptance tests
  (closed-loop / handoff / part-vs-whole, recorded by U07) in the design
  doc's §D-pinned-tests.
- **D8 · Docs parity at the flip (U07, the parity unit — the C-M11·8
  precedent).** `Milestones.cs` M14 → `StatusDone` + M15 → `StatusNext`;
  README Roadmap (M14 → done, M15 → in progress); `docs/STATUS.md`;
  `docs/ARCHITECTURE.md` (the M14 value-chain row → done);
  `MilestonesTests.cs` re-pin (M15 becomes the single in-progress
  milestone — rename `M14_Is_The_Single_InProgress_Milestone` →
  `M15_Is_The_Single_InProgress_Milestone` + extend the shipped list with
  M14). All in **one** unit.

## Invariants (C-M14·1 … C-M14·7) — U00 locks these verbatim

- **C-M14·1 · The `EventId` is a filter/association, never a gate.** It
  narrows the candidate set in `ListTodosForEventAsync`; it **never**
  changes the audience decision (C-M3·2 / C-PL·3). A to-do is visible iff
  it passes its **own** `CanSeeAsync(Read)`; the event link neither grants
  nor denies.
- **C-M14·2 · Both display links are access-scoped + dangling-safe.**
  (The `ProjectLink` / `BlockerChip` idiom.) An unreadable / absent /
  soft-deleted target is **omitted entirely** — never a 404/403 for the
  source surface, and its title / id are **not leaked** (a `null` link
  field, the C3 split applied to a cross-reference).
- **C-M14·3 · The `set-event` write lane re-checks standing server-side**
  (creator ∪ assignee ∪ GlobalAdmin, C-M5·6); the Web `[Authorize]` is a
  convenience pre-gate only, never the source of truth; the to-do must be
  `!IsDeleted`; a non-visible `eventId` is refused (404); one
  `AccessAudit` row commits atomically with the write (C3).
- **C-M14·4 · No new authorization surface.** No new `AccessAction`,
  `AccessVia`, or `Decide()` branch (C-M5·11). All read lanes route through
  the frozen `IAuthorizationService` over the **existing**
  `TodoItemToAuditableResource` / `EventToAuditableResource` adapters.
- **C-M14·5 · (D4) The `VTODO` file never carries a decision.**
  (C-M12·2 / ADR 0028 applied to the file form.) No `ORGANIZER` /
  `CREATED-BY` / `ATTENDEE` (author / assignee identity), no `Audience` /
  grant / membership internals; the authorization was applied to the set
  *before* the emitter runs.
- **C-M14·6 · No `RRULE` / recurrence anywhere in M14.** The `Event` doc
  gains **no** recurrence field; the `VTODO` emitter emits a single `VTODO`
  per to-do. `RRULE` is **M18's** home (D6).
- **C-M14·7 · Additive-only, frozen seams untouched.** The `EventId?`
  field + the `(EventId)` index + `ListTodosForEventAsync` + `SetTodoEvent
  Async` + (D4) the `TodoIcsWriter` are **additive** (ADR 0004 §B.1
  delta-detect, idempotent at boot, zero migration for existing rows). The
  **frozen** `IEventService` lanes and the **existing** `IProjectService`
  lanes are **untouched** (the ~pinned Core/Web pins survive).

## FACES (F1–F5) + the named trade — U00 refines

- **F1 (coherent)** — the interlock rides the house's own seams: the
  `ProjectId` field shape, the `ProjectLink`/`BlockerChip` display idiom,
  the `set-project` write-lane shape, the `IcsWriter` emitter discipline.
  **Nothing new is invented** — the two surfaces link through machinery
  that already exists.
- **F2 (stable)** — additive-only schema + read/write lanes; the frozen
  `IEventService` / existing `IProjectService` surfaces are untouched; the
  pinned Core/Web pins survive the boot delta.
- **F3 (flexible)** — the `EventId` field + the `VTODO` surface compose
  with future lanes: M18 (recurring) can later add `RRULE` to the
  `VTODO`/`VEVENT` emitters and a recurrence field to `Event` **without
  touching the M14 linkage**; the many-to-many veto (D1) is a drop-in
  substitute.
- **F4 (energizing)** — a resident preparing for an event links the prep
  to-dos to it and carries **both surfaces in one calendar**; the
  "Integration over features" principle is made concrete (the linkage that
  turns two parts into a whole).
- **F5 (adaptive)** — the access-scoped dangling-safe links degrade
  gracefully (an event the actor can't see is simply not shown; an
  unlinked / dangling target omits its chip), so the interlock **never
  leaks**.
- **The trade (name at least one):** F4 (more linkage surface) spends a
  little **legibility** — a to-do card now carries up to **four**
  cross-references (project, board placements, blocker, **event**). The
  detail view must stay legible: **one labeled chip per cross-reference,
  each with its own `kw-l` label, empty-state when absent** — never a
  wall of links. (F4 consumes a slice of F2/legibility; the cost is named,
  not hidden.)

## Approach

Three tracks (the M13/M3 shape), each unit atomic per the contract:

- **Track A — Docs (the sign-off gate).** U00 authors
  `docs/design/m14-events-projects-design.md` (primary tier) + **ADR 0115**
  + the ADR index row; locks D1–D8, the invariants, the FACES, the pinned
  test names, the drift-guard frozen list, and the deferred-lane list.
- **Track B — Core (the seams).** U01 (`EventId?` field + `(EventId)` index
  + `ListTodosForEventAsync` reverse read seam + tests) → U02 (the
  `set-event` write lane + tests) → **(D4)** U05 (the `TodoIcsWriter`
  `VTODO` emitter + tests).
- **Track C — Web (the surface).** U03 (both-direction display links +
  `kw-l`) → U04 (the `set-event` lane + event picker + `kw-l`) → **(D4)**
  U06 (the two `VTODO` routes + `kw-l`).
- **Close.** U07 (the three acceptance/gate tests + the cross-surface
  read-shape pins + **docs parity at the flip** — the parity unit, always
  last; if D4 is vetoed U05/U06 are skipped and U07 keeps its number).

## Workflow (per unit, sized for ~32K)

Every unit restates its own Goal → **Entry reads (4–8 files)** →
**Deliverables (≤ 9 small files)** → **Exit** (one `dotnet build
Kumunita.slnx -c Debug` + the applicable `dotnet exec` test run green, and
a `## U##` note appended to the handoff note). A unit agent reads **its
unit-plan file + its entry reads** and does not need this register.

---

## U00 — Lock the design: `m14-events-projects-design.md` + ADR 0115

**Goal.** Author the primary tier + the decision record, locking the
register's [PROPOSED] set (D1–D8) — or recording any **veto** the user
made, with the locked text replacing it. **The sign-off gate: after U00,
the design doc is the only authority.**

**Entry reads (8).**
1. `docs/philosophy/templates/design-doc.md` — the required section set
   (Seams & contracts mandatory; FACES check mandatory).
2. `docs/design/m12-ical-design.md` — the house style (§Invariants /
   §FACES / §drift-guard / §deferred shape) + the `IcsWriter` field-map
   the D4 `TodoIcsWriter` mirrors.
3. `docs/adr/0112-ical-calendar-export.md` + `docs/adr/0114-logging-and-
   usage-analytics.md` — the ADR shapes to mirror (a milestone ADR) + the
   ADR-0112 `VTODO`/`RRULE` deferral language U00 supersedes/clarifies.
4. `src/Kumunita.Core/Projects/TodoItem.cs` +
   `src/Kumunita.Core/Projects/ProjectRequests.cs` — the `ProjectId` field
   shape (D1) + the `ProjectLink`/`BlockerChip`/`TodoDetailResult`
   display idiom (D2).
5. `src/Kumunita.Core/Projects/IProjectService.cs` (`ListTodosAsync` /
   `ListBoardsForTodoAsync` / `set-project`-adjacent lanes) — the
   reverse-seam + write-lane shapes D2/D3 copy.
6. `src/Kumunita.Core/Events/IEventService.cs` +
   `src/Kumunita.Core/Events/IcsWriter.cs` — the frozen event read seams
   D2 composes against + the emitter discipline D4 copies.
7. `src/Kumunita.Web/Controllers/EventController.cs` (the two `.ics`
   routes ≈ L822 / L898) — the serve idiom D4's routes copy.
8. `src/Kumunita.Core/M5DocTypes.cs` — the additive-index shape the
   `(EventId)` index joins.

**Deliverables (3).**
- `docs/design/m14-events-projects-design.md` — full design doc
  (Context / Goals + Non-goals / Human cost / Parts / **Seams & contracts**
  / Feedback loops / Emergent impact / Local-optimization check / FACES /
  Rollout & rollback / Risks / Integration step served), with the
  **invariants C-M14·1…7 verbatim**, the **FACES F1–F5 + the trade**,
  the **pinned test names verbatim** (for U01/U02/U05/U06/U07), the
  **drift-guard frozen list**, the **deferred-lane list** (RRULE → M18;
  `RRULE` on `VTODO`; ICS import; per-component feeds; many-to-many if
  vetoed), and the **gate** section (the three acceptance tests).
- `docs/adr/0115-integration-of-events-and-projects.md` — the decision
  record (confirm 0115 is free first); its Consequences carry the
  **D6 RRULE/M18 clarification** + the **D4 ADR-0112 `VTODO`
  supersession**.
- `docs/adr/README.md` — one index row for 0115 (append after 0114).

**Exit.** Both docs exist, internally consistent with the register (or the
locked veto text), ADR 0115's status `Accepted`, the index row present, the
pinned test list + invariants + FACES + drift-guard + deferred lists all
written verbatim. Handoff note seeded: `## U00 — design + ADR 0115` — (a)
the locked D1–D8 (or the veto + replacement), (b) the three acceptance
test names, (c) any drift from the register, (d) confirmation that ADR 0115
was verified free.

---

## U01 — Core: the `EventId?` field + `(EventId)` index + `ListTodosForEventAsync` read seam + its pinned tests

**Goal.** D1 + D2 rendered as code: the additive `EventId?` association on
`TodoItem`, the optional `(EventId)` feed index, and the **reverse read
seam** `ListTodosForEventAsync` (the `ListTodosAsync` candidate-filter +
`CanSeeAsync(Read)` + `HasMore` + aggregate `AccessAudit` shape, filtered
`EventId == eventId`, non-deleted) — **a feed filter, never a gate**
(C-M14·1).

**Entry reads (6).**
1. `docs/design/m14-events-projects-design.md` §D1/§D2 — the exact
   `EventId?` field doc-comment, the `(EventId)` index note, and the
   `ListTodosForEventAsync` signature + candidate-filter rule, verbatim.
2. `src/Kumunita.Core/Projects/TodoItem.cs` — the `ProjectId` /
   `ComponentId` field shapes + doc-comments the new `EventId?` mirrors.
3. `src/Kumunita.Core/M5DocTypes.cs` — the `(ProjectId)` / `(ComponentId,
   Created)` index blocks the `(EventId)` index joins (the naming note at
   the top of `Configure`).
4. `src/Kumunita.Core/Projects/IProjectService.cs` (`ListTodosAsync` +
   `ListBoardsForTodoAsync`) — the read-seam shape to copy (record return,
   `HasMore`, the aggregate `AccessAudit` doc-comment).
5. `src/Kumunita.Core/Projects/ProjectService.cs` (the `ListTodosAsync`
   impl + the `CanSeeAsync(Read)` pass + the `AccessAudit` store) — the
   implementation idiom `ListTodosForEventAsync` copies.
6. `tests/Kumunita.Core.Tests/` (an existing `IProjectService` read-lane
   test file) — the Testcontainers + `CanSeeAsync(Read)` fixture shape the
   4 pinned tests reuse.

**Deliverables (5).**
- `src/Kumunita.Core/Projects/TodoItem.cs` — one additive `public string?
  EventId { get; set; }` (the `ProjectId` shape, doc-comment: the M14
  interlock, C-M14·1 filter-never-gate, ADR 0115).
- `src/Kumunita.Core/M5DocTypes.cs` — one additive `(EventId)` feed index
  on `TodoItem` (the `(ProjectId)` index precedent; the unnamed-form note
  if the computed-index naming constraint applies).
- `src/Kumunita.Core/Projects/IProjectService.cs` — the
  `ListTodosForEventAsync(string eventId, string actorId, int page,
  CancellationToken ct = default)` signature + doc-comment (the
  `ListTodosAsync` shape, the `ListBoardsForTodoAsync` precedent, C-M14·1
  / C-M14·4 / C-M14·7).
- `src/Kumunita.Core/Projects/ProjectService.cs` — the implementation
  (candidate `!IsDeleted && EventId == eventId` → `CanSeeAsync(Read)` over
  `TodoItemToAuditableResource` → `Created` desc → `TodoPage`/`HasMore` +
  one aggregate `AccessAudit` row `TargetKind="todo"`).
- `tests/Kumunita.Core.Tests/` (the `ListTodosForEventAsync` pinned tests —
  **4**: empty-candidate early return; the `CanSeeAsync(Read)` filter
  drops the denied to-do; the `EventId ==` filter (a to-do linked to a
  *different* event is excluded); the `HasMore` page-boundary pin) — names
  verbatim from the design doc.

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **4 pins** discovered + executed. Handoff note: `## U01 —
EventId field + ListTodosForEventAsync` — (a) the `EventId?` field + its
doc-comment, (b) the `(EventId)` index as written (named vs unnamed), (c)
the `ListTodosForEventAsync` signature + the candidate-filter line, (d) the
4 pin names + pass/red, (e) any compile warnings.

---

## U02 — Core: the `set-event` write lane + its pinned tests

**Goal.** D3 rendered as code: `SetTodoEventAsync` — **creator ∪ assignee ∪
GlobalAdmin** (C-M5·6), to-do `!IsDeleted`, a non-visible `eventId`
refused (404), `null` clears the link, one `AccessAudit` row
(`todo.set_event`) committed atomically (C-M14·3 / C-M14·4).

**Entry reads (5).**
1. `docs/design/m14-events-projects-design.md` §D3 — the exact
   `SetTodoEventAsync` signature + standing + the `eventId` visibility
   rule + the `AccessAudit` action string, verbatim.
2. `src/Kumunita.Core/Projects/IProjectService.cs` (the
   `AssignTodoAsync` / the `set-project`-adjacent lane doc-comments) — the
   standing + 404-vs-403 + `AccessAudit` doc-comment shape.
3. `src/Kumunita.Core/Projects/ProjectService.cs` (the `AssignTodoAsync`
   impl + the standing-check helper + the `AccessAudit` store) — the
   implementation idiom to copy (the standing re-check, the atomic commit).
4. `src/Kumunita.Core/Events/IEventService.cs` (`GetAsync`) — the
   404-vs-403 split the lane's `eventId` visibility check reuses (call
   `GetAsync` and treat `UnauthorizedAccessException` /
   `KeyNotFoundException` / `IsDeleted` as a refused link).
5. `tests/Kumunita.Core.Tests/` (an existing to-do write-lane test file) —
   the standing fixture (creator / assignee / GlobalAdmin / outsider) the
   3 pinned tests reuse.

**Deliverables (3).**
- `src/Kumunita.Core/Projects/IProjectService.cs` — the
  `SetTodoEventAsync(string todoId, string actorId, IReadOnlySet<string>
  actorRoles, string? eventId, CancellationToken ct = default)` signature
  + doc-comment (C-M14·3 / C-M14·4; the `AccessAudit` action
  `todo.set_event`).
- `src/Kumunita.Core/Projects/ProjectService.cs` — the implementation
  (the standing re-check; the to-do existence + `!IsDeleted`; the `eventId`
  visibility check via `IEventService.GetAsync`; set/clear `EventId`; stamp
  `Modified`; the `AccessAudit` row).
- `tests/Kumunita.Core.Tests/` (the `SetTodoEventAsync` pinned tests —
  **3**: creator / assignee / GlobalAdmin each allowed, an outsider
  refused 403; a non-visible `eventId` refused 404, a `null` clears the
  link; the `AccessAudit` row `todo.set_event` committed with the write) —
  names verbatim from the design doc.

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **3 pins** discovered + executed. Handoff note: `## U02 —
set-event write lane` — (a) the `SetTodoEventAsync` signature, (b) the
standing + the `eventId` visibility rule as enforced, (c) the `AccessAudit`
action string, (d) the 3 pin names + pass/red, (e) any compile warnings.

---

## U03 — Web: both-direction display links (to-do → event chip + event → linked to-dos) + `kw-l`

**Goal.** D2 rendered in the UI, **both directions, access-scoped +
dangling-safe** (C-M14·2). The to-do detail shows a "linked event" chip
(resolved via the frozen `IEventService.GetAsync`); the event detail shows
a "linked to-dos" section (via U01's `ListTodosForEventAsync`). **No new
authorization surface** (C-M14·4).

**Entry reads (7).**
1. `docs/design/m14-events-projects-design.md` §D2 — the two display-link
   shapes + the `kw-l` key list (verbatim) + the dangling-safe rule
   (C-M14·2).
2. `src/Kumunita.Web/Models/ProjectTodoViewModels.cs` (the
   `TodoDetailViewModel` record + its `ProjectId?`/`ProjectTitle?`
   project-link fields) — the additive `EventId?`/`EventTitle?`/
   `EventLinkPath?` fields the chip renders from (the `ProjectLink`
   shape).
3. `src/Kumunita.Web/Models/EventEditorModel.cs` (the
   `EventDetailViewModel` record + its additive `EventTranslations` /
   `Languages` fields) — the additive `IReadOnlyList<LinkedTodoRow>?
   LinkedTodos` field the event detail renders (the
   `EventDetailViewModel`-gains-a-field precedent).
4. `src/Kumunita.Web/Controllers/ProjectsController.cs` (the
   `GET /projects/todos/{id}` action ≈ L664 — the `ProjectLink` resolve
   call) — the `EventLink` resolve call + the dangling-safe catch to add.
5. `src/Kumunita.Web/Controllers/EventController.cs` (the
   `GET /events/{id}` detail action) — the `ListTodosForEventAsync` call +
   the `LinkedTodos` population to add.
6. `src/Kumunita.Web/Views/Projects/Todo/Detail.cshtml` +
   `src/Kumunita.Web/Views/Events/Detail.cshtml` — the two detail views the
   chip + the section join (the `ProjectLink` chip + an empty-state).
7. `src/Kumunita.Web/Localization/KnownTranslationKeys.cs` +
   `src/Kumunita.Web/Localization/*.json` (en/de/fr/da) — the `kw-l`
   registry + the parity shape (the `KnownTranslationKeys_ParityTests` +
   `KwLRegistryConsistencyTests` pins).

**Deliverables (8).**
- `src/Kumunita.Web/Models/ProjectTodoViewModels.cs` — `TodoDetailViewModel`
  gains `EventId? = null`, `EventTitle? = null`, `EventLinkPath? = null`
  (additive, default null — the `ProjectLink` shape).
- `src/Kumunita.Web/Models/EventEditorModel.cs` — `EventDetailViewModel`
  gains `IReadOnlyList<LinkedTodoRow>? LinkedTodos = null` (additive) + a
  small `LinkedTodoRow` (id / title / status / due / link — or reuse the
  existing `TodoRow`; U00 locks the exact shape).
- `src/Kumunita.Web/Controllers/ProjectsController.cs` — the `EventLink`
  resolve in the to-do detail action (call `IEventService.GetAsync`, catch
  `KeyNotFoundException` / `UnauthorizedAccessException` / `IsDeleted` →
  leave the chip fields `null`).
- `src/Kumunita.Web/Controllers/EventController.cs` — the `LinkedTodos`
  population in the event detail action (call `ListTodosForEventAsync`,
  map to `LinkedTodoRow`, empty list when none).
- `src/Kumunita.Web/Views/Projects/Todo/Detail.cshtml` — the "linked
  event" chip (render only when `EventId != null`; the `todo.event_link`
  label; the empty-state when absent).
- `src/Kumunita.Web/Views/Events/Detail.cshtml` — the "linked to-dos"
  section (render only when `LinkedTodos` non-empty; one row per to-do;
  the `events.linked_todos` label + the per-row link).
- `src/Kumunita.Web/Localization/KnownTranslationKeys.cs` — the new `kw-l`
  keys (`todo.event_link`, `events.linked_todos`, + any empty-state keys —
  the exact list from the design doc).
- `src/Kumunita.Web/Localization/en.json` / `de.json` / `fr.json` /
  `da.json` — the four locale entries (the parity pins move together).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` green
(includes the `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` pins the new keys satisfy). Handoff note:
`## U03 — display links (both directions)` — (a) the `TodoDetailViewModel`
+ `EventDetailViewModel` additive fields as written, (b) the two dangling-
safe resolve calls + their catch, (c) the `kw-l` keys × en/de/fr/da, (d)
any compile warnings / parity-pin notes.

---

## U04 — Web: the `set-event` lane + the event picker + `kw-l`

**Goal.** D3 rendered in the UI: `POST /projects/todos/{id}/set-event` on
`ProjectsController` (the `set-project` lane shape ≈ L1303,
`[ValidateAntiForgeryToken]`, `eventId` form field, `null` = clear) + the
event picker on the to-do detail (the actor's visible events, a small read
via the frozen `IEventService`, capped) + the clear affordance + `kw-l`.

**Entry reads (6).**
1. `docs/design/m14-events-projects-design.md` §D3 — the lane shape + the
   picker read seam + the `kw-l` key list (verbatim).
2. `src/Kumunita.Web/Controllers/ProjectsController.cs` (the
   `POST /projects/todos/{id}/set-project` action ≈ L1303 + the
   `POST /projects/todos/{id}/assign` action ≈ L1253) — the
   `[ValidateAntiForgeryToken]` + the standing pre-gate + the
   `AccessAudit`-on-4xx shape the new lane copies.
3. `src/Kumunita.Core/Projects/IProjectService.cs` (`SetTodoEventAsync`) —
   the U02 seam this action calls.
4. `src/Kumunita.Core/Events/IEventService.cs` (`ListUpcomingAsync` /
   `ListMineAsync`) — the picker read seam (the actor's visible events,
   capped at the design-doc N).
5. `src/Kumunita.Web/Views/Projects/Todo/Detail.cshtml` — the U03 chip
   location the picker + clear form join (a nested `<form>` +
   `@Html.AntiForgeryToken()`, the account-menu picker precedent).
6. `src/Kumunita.Web/Localization/KnownTranslationKeys.cs` + the four
   `*.json` — the new `kw-l` keys + the parity shape.

**Deliverables (6).**
- `src/Kumunita.Web/Controllers/ProjectsController.cs` — the
  `POST /projects/todos/{id}/set-event` action (the `set-project` shape;
  the `eventId` field bound nullable; `null`/blank → clear; the 404/403
  → the standing pre-gate + the U02 seam's decision as truth).
- `src/Kumunita.Web/Models/ProjectTodoViewModels.cs` — a small
  `TodoEventPicker` view-model (the visible-event list + the current
  `EventId`), if U03 did not already add it (U00 locks the exact shape).
- `src/Kumunita.Web/Views/Projects/Todo/Detail.cshtml` — the event picker
  `<form>` (a `<select>` of the actor's visible events + a "clear" option)
  + the clear affordance (join the U03 chip block).
- `src/Kumunita.Web/Localization/KnownTranslationKeys.cs` — the new `kw-l`
  keys (`todo.set_event.label`, `todo.set_event.clear`, `todo.set_event.
  pick`, + any `kw-l` for the picker — the exact list from the design doc).
- `src/Kumunita.Web/Localization/en.json` / `de.json` / `fr.json` /
  `da.json` — the four locale entries (the parity pins move together).
- (optional, only if U00 says the picker needs it) one Web test file
  addition pinning the `set-event` form's CSRF + `null`-clear binding.

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` green
(includes the parity pins). Handoff note: `## U04 — set-event lane +
picker` — (a) the action signature + its route, (b) the picker read seam
+ the cap N, (c) the `kw-l` keys × en/de/fr/da, (d) any compile warnings /
parity-pin notes.

---

## U05 — (D4) Core: the `TodoIcsWriter` `VTODO` emitter + its pinned tests

> **Skip this unit if D4 was vetoed** (then M14 = the data interlock only;
> U07 keeps its number).

**Goal.** D4 rendered as code: `TodoIcsWriter` — **BCL-only, pure**
(the `IcsWriter` discipline): `BuildTodos(IReadOnlyList<TodoItem> todos,
DateTimeOffset nowUtc)` → the entire `VCALENDAR` with one `VTODO` per
**dated** to-do, on the closed `VTODO` subset (C-M14·5 / C-M14·6).

**Entry reads (5).**
1. `docs/design/m14-events-projects-design.md` §D4 — the exact `VTODO`
   field-map + the closed property set + the never-emitted list + the
   `BuildTodos` signature (verbatim).
2. `src/Kumunita.Core/Events/IcsWriter.cs` (the full class) — the
   CRLF / 75-octet fold / escape / stable-UID / empty-calendar /
   `FormatUtc` / private-helper discipline the `TodoIcsWriter` copies
   (the `VTODO` property set + the `DUE`/`DTSTART` conditional shape).
3. `src/Kumunita.Core/Projects/TodoItem.cs` — the `DueAt?` / `StartAt?` /
   `Title` / `Body` / `IsDeleted` fields the emitter reads (the
   already-authorized POCOs).
4. `tests/Kumunita.Core.Tests/` (the `IcsWriter` pinned test file) — the
   BCL-only + the fold/escape/empty-calendar test fixtures the `VTODO`
   pins reuse.
5. `docs/adr/0112-ical-calendar-export.md` (the C-M12·4 closed-subset +
   C-M12·2 no-decision pins) — the invariants the `TodoIcsWriter` carries
   to the to-do surface.

**Deliverables (2).**
- `src/Kumunita.Core/Events/TodoIcsWriter.cs` — the static class (the
  `IcsWriter` shape: one public `BuildTodos` method, the `VCALENDAR`
  envelope `VERSION`/`PRODID`/`CALSCALE`/`METHOD`, one `VTODO` per dated
  to-do: `UID` `kw-todo-{Id}@kumunita` / `DTSTAMP` / `DUE` (only when
  `DueAt` set) / `DTSTART` (only when `StartAt` set) / `SUMMARY` /
  `DESCRIPTION` (only when non-empty) / `STATUS` (only when `IsDeleted`);
  CRLF / fold / escape / stable `UID`; a valid empty calendar; **no**
  `ORGANIZER`/`ATTENDEE`/`RRULE`/`CREATED-BY`/audience/internals/`X-…`).
- `tests/Kumunita.Core.Tests/` (the `TodoIcsWriter` pinned tests — **4**:
  the `VTODO` field-map for a dated to-do; the `DUE`-only /
  `DTSTART`-only / neither (skipped) conditional shape; the fold + escape
  on a long / special-char title+body; a valid **empty** `VCALENDAR` when
  no dated to-dos — the C-M14·5 / C-M14·6 pins) — names verbatim from the
  design doc.

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
reports the **4 pins** discovered + executed. Handoff note: `## U05 —
TodoIcsWriter (VTODO)` — (a) the `BuildTodos` signature + the `VTODO`
property set as emitted, (b) the never-emitted list confirmed absent, (c)
the 4 pin names + pass/red, (d) any compile warnings.

---

## U06 — (D4) Web: the two `VTODO` routes + `kw-l` affordances

> **Skip this unit if D4 was vetoed** (then M14 = the data interlock only;
> U07 keeps its number).

**Goal.** D4 rendered in the UI: `GET /projects/todos.ics` (the caller's
visible dated to-dos, always `200`) + `GET /projects/todos/{id}.ics` (one
`VTODO`, the frozen `GetTodoAsync`'s 404-not-403 split) on
`ProjectsController`, the `text/calendar` serve idiom, no subscription
token, + the two quiet `kw-l` affordances.

**Entry reads (5).**
1. `docs/design/m14-events-projects-design.md` §D4 — the two routes'
   contracts + the serve shape + the `kw-l` key list (verbatim).
2. `src/Kumunita.Web/Controllers/EventController.cs` (the two `.ics`
   routes ≈ L822 / L898) — the serve idiom (`text/calendar` +
   `Content-Disposition` + `no-store` + `nosniff`) + the
   `IcsWriter.Build` call + the tag-resolve idiom the `VTODO` routes copy.
3. `src/Kumunita.Core/Projects/IProjectService.cs` (`ListTodosAsync` /
   `GetTodoAsync`) — the frozen read seams the two routes call (the
   "dated to-dos" filter applied in the Web layer, not a new seam).
4. `src/Kumunita.Core/Events/TodoIcsWriter.cs` (U05) — the emitter the
   routes call.
5. `src/Kumunita.Web/Views/Projects/Todo/Detail.cshtml` +
   `src/Kumunita.Web/Views/Projects/Todo/Index.cshtml` — the two surfaces
   the two quiet `kw-l` affordances join (plain `<a>` links, no new JS).
6. `src/Kumunita.Web/Localization/KnownTranslationKeys.cs` + the four
   `*.json` — the new `kw-l` keys + the parity shape.

**Deliverables (5).**
- `src/Kumunita.Web/Controllers/ProjectsController.cs` — the
  `GET /projects/todos.ics` action (the frozen `ListTodosAsync` visible
  set → filter to `DueAt != null || StartAt != null` → `TodoIcsWriter.
  BuildTodos` → the serve idiom; always `200`) + the
  `GET /projects/todos/{id}.ics` action (the frozen `GetTodoAsync`
  404-not-403 split → one `VTODO` → the serve idiom).
- `src/Kumunita.Web/Views/Projects/Todo/Detail.cshtml` — the
  `projects.todos.ics.download` "Add to calendar" `<a>` (join the U03/U04
  chip block).
- `src/Kumunita.Web/Views/Projects/Todo/Index.cshtml` — the
  `projects.todos.ics.feed` "Calendar feed (iCal)" `<a>` (join the feed
  header).
- `src/Kumunita.Web/Localization/KnownTranslationKeys.cs` — the two `kw-l`
  keys (the exact list from the design doc).
- `src/Kumunita.Web/Localization/en.json` / `de.json` / `fr.json` /
  `da.json` — the four locale entries (the parity pins move together).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` green
(includes the parity pins). Handoff note: `## U06 — VTODO routes` — (a)
the two routes + their serve shapes, (b) the "dated to-dos" filter line,
(c) the two `kw-l` keys × en/de/fr/da, (d) any compile warnings /
parity-pin notes.

---

## U07 — Close: the three acceptance tests + the cross-surface read-shape pins + docs parity at the flip

**Goal.** (1) Record the **three acceptance/gate tests** (closed-loop /
handoff / part-vs-whole) + any missing cross-surface read-shape pins
(the `EventDetailViewModel.LinkedTodos` + `TodoDetailViewModel.EventId?`
additive-field call-site pins; the dangling-safe omission pin; the
`set-event` standing pin, if not covered by U02). (2) **Docs parity at the
flip** (D8, the C-M11·8 precedent) — `Milestones.cs` M14 → `StatusDone` +
M15 → `StatusNext`, README Roadmap, `docs/STATUS.md`,
`docs/ARCHITECTURE.md`, `MilestonesTests.cs` re-pin — **all in this one
unit**.

**Entry reads (7).**
1. `docs/design/m14-events-projects-design.md` §gate — the three
   acceptance tests + the cross-surface pin list (verbatim).
2. `src/Kumunita.Web/Milestones.cs` — the M14 `StatusNext` entry to flip
   to `StatusDone` + the M15 entry to flip to `StatusNext`.
3. `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the
   `M14_Is_The_Single_InProgress_Milestone` pin to rename to `M15_…` + the
   shipped-milestone list to extend with M14 (the C-M11·8 precedent).
4. `README.md` (the Roadmap section) — the M14 → done + M15 → in-progress
   edits.
5. `docs/STATUS.md` + `docs/ARCHITECTURE.md` — the M14 status + the M14
   value-chain row → done.
6. `tests/Kumunita.Web.Tests/` (a cross-surface view-model pin file, if
   one exists) — the additive-field + dangling-safe pin fixture shape.
7. `tests/Kumunita.Core.Tests/` (the U01/U02 pin files) — the standing +
   the `EventId ==` filter pins already recorded (so U07 adds only what's
   missing, not duplicates).

**Deliverables (6).**
- `tests/Kumunita.Web.Tests/` (the three acceptance tests + the missing
  cross-surface pins — the closed-loop "a to-do linked to a visible event
  shows the chip + the event shows the to-do"; the handoff "the
  `set-event` write + the both-direction read compose without a new
  authorization surface"; the part-vs-whole "the interlock degrades
  gracefully — an unreadable event omits the chip, never a 404/403") —
  names verbatim from the design doc.
- `src/Kumunita.Web/Milestones.cs` — M14 → `StatusDone`, M15 →
  `StatusNext` (the two-line flip).
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the re-pin
  (`M14_Is_…` → `M15_Is_…`; extend the shipped list with M14).
- `README.md` — the Roadmap M14 → done + M15 → in progress.
- `docs/STATUS.md` — the M14 status → done (the M14 detail entry).
- `docs/ARCHITECTURE.md` — the M14 value-chain row → done.

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` green
(includes the re-pinned `MilestonesTests`). `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
green. Handoff note: `## U07 — acceptance + parity` — (a) the three
acceptance test names + pass/red, (b) the `Milestones.cs` two-line flip,
(c) the `MilestonesTests.cs` re-pin line, (d) the four docs edits
(README/STATUS/ARCHITECTURE/Milestones.cs) as made, (e) any
Testcontainers/Docker cleanup note (`docker container prune` if the Core
run left containers).

---

## Gate (the three acceptance tests) — recorded by U07

1. **Closed loop.** A to-do linked (via the `set-event` lane) to a
   **visible** event: the to-do detail shows the event chip **and** the
   event detail shows the to-do in its "linked to-dos" section — the
   linkage is symmetric and both directions resolve.
2. **Handoff.** The `set-event` write + the both-direction read compose
   with **no new authorization surface** (C-M14·4) — a grep of the diff
   shows no new `AccessAction` / `AccessVia` / `Decide()` branch, and the
   `EventId` is never a gate (C-M14·1).
3. **Part vs. whole.** The interlock **degrades gracefully** (F5): an
   event the actor **cannot** see (or a soft-deleted / absent target)
   **omits** the chip / the row — never a 404/403 for the source surface,
   and the target's title / id are **not leaked** (C-M14·2).

## Deferred lanes (ADR 0115 Consequences — each named, each "own ADR" / own
milestone)

- **Recurring events / `RRULE` — M18** (D6; the source of truth is
  `Milestones.cs`).
- **`RRULE` on the `VTODO` surface** (a to-do's recurrence rides M18's
  recurrence concept, if that ever extends past events).
- **ICS import** (round-tripping a calendar file *into* Kumunita — ADR
  0112's "own lane" deferral, unchanged).
- **Per-component calendar feeds** (a `?component=` scoping of the
  `VTODO` feed — a scope generalization, own lane).
- **Many-to-many event↔to-do** (if D1's 1:1 field is vetoed, the
  `EventTodoLink` doc is the alternative — own design decision U00 records).

## Drift-guard frozen list (locked by U00; a change is a drift event)

- The `EventId?` field on `TodoItem` (its name + the filter-never-gate
  rule, C-M14·1).
- The `ListTodosForEventAsync` + `SetTodoEventAsync` signatures
  (`IProjectService`).
- The `TodoDetailViewModel.EventId?`/`EventTitle?`/`EventLinkPath?` +
  `EventDetailViewModel.LinkedTodos` additive fields.
- The `TodoIcsWriter.BuildTodos` signature + the closed `VTODO` property
  set + the never-emitted list (C-M14·5 / C-M14·6).
- The two `VTODO` routes (`GET /projects/todos.ics` + `GET
  /projects/todos/{id}.ics`) + the `text/calendar` serve shape.
- The invariants C-M14·1…C-M14·7 (verbatim).
- The pinned test names (U01/U02/U05/U06/U07, verbatim).

## Verification (after any code change)

```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```

(`Kumunita.Core.Tests` takes ~20 s — it starts `postgres:18` via
Testcontainers; clean up with `docker container prune` if it leaves
containers.)
