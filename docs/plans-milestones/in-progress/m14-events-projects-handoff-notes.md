# M14 — Living handoff note (scratch tier)

> One `## U#` section per unit, **appended, never rewritten**. This is the
> scratch tier the unit agents read before they start (the register is
> `docs/plans-milestones/plan-m14-events-projects.md`, secondary tier; the
> design doc `docs/design/m14-events-projects-design.md` is the primary
> tier — the only authority after U00). A unit agent appends its own
> `## U#` section at the end when it finishes; it never rewrites an
> earlier section.

## Status

- **M14 scope (locked at U00):** the **two coordination surfaces
  interlock** — a `TodoItem` gains an `EventId?` association (the
  `ProjectId` shape, filter-never-gate) + a reverse read seam
  `ListTodosForEventAsync` + a `set-event` write lane + both-direction
  display links; **and (D4, [PROPOSED — veto before U00])** a
  `VTODO` iCal surface (`TodoIcsWriter` + two `.ics` routes + `kw-l`).
  **Out:** `RRULE` / recurring — **M18's home** (D6, the `Milestones.cs`
  source of truth).
- **Decision record:** ADR 0115 (confirm free first).
- **Unit series:** U00 (design + ADR, the gate) → U01 (field + index +
  seam) → U02 (`set-event`) → U03 (display links) → U04 (picker) → U05
  (D4: `TodoIcsWriter`) → U06 (D4: routes) → U07 (acceptance + parity).
  If D4 is vetoed: U05 + U06 are skipped, U07 keeps its number.

## U00 — design + ADR 0115

**Status:** D1–D8 **locked as written** (no veto recorded — D4 was
[PROPOSED] and stands). **ADR 0115 verified free first** — the highest
ADR before U00 was **0114** (M13 logging/analytics); 0115 was unclaimed.
Design doc `docs/design/m14-events-projects-design.md` (1017 lines) is the
primary authority; ADR 0115 is the decision record; this note is the
scratch tier unit agents read before starting.

**Locked decisions (D1–D8):**
- **D1** — one additive `EventId?` field on `TodoItem` (a 1:1
  association — the `ProjectId` shape; a feed filter / association,
  **never a gate**, C-M14·1) + one unnamed `(EventId)` index on
  `M5DocTypes` (ADR 0004 §B.1 additive, zero migration — existing rows
  read `EventId = null`).
- **D2** — both display directions as **Web-layer composition** over the
  frozen read seams (no `Core.Events` → `Core.Projects` dependency):
  to-do → event chip over the **frozen** `IEventService.GetAsync`; event →
  linked to-dos over the **new** reverse read seam
  `IProjectService.ListTodosForEventAsync(string eventId, string actorId,
  int page, CancellationToken ct = default)` (the `ListTodosAsync` shape
  filtered to one event — the `ListBoardsForTodoAsync` precedent —
  `CanSeeAsync(Read)` over the existing adapter, one aggregate
  `AccessAudit` row). Both directions **access-scoped + dangling-safe**
  (C-M14·2, the `ProjectLink`/`BlockerChip` idiom — omit, never 404/403,
  no title/id leak). `TodoDetailViewModel` gains `EventId?`/`EventTitle?`/
  `EventLinkPath?`; `EventDetailViewModel` gains
  `IReadOnlyList<LinkedTodoRow>? LinkedTodos` — the locked
  `LinkedTodoRow(TodoId, Title, Status?, DueAt?, LinkPath)` shape (drift
  entry 5).
- **D3** — the dedicated `set-event` write lane
  `IProjectService.SetTodoEventAsync(string todoItemId, string actorId,
  IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct =
  default)` — the `SetTodoProjectAsync` shape (param name `todoItemId`,
  drift entry 3); standing **creator ∪ assignee ∪ GlobalAdmin** over the
  to-do (C-M5·6, server-side); a non-visible `eventId` **refused 404**
  (the frozen `CanAsync(Read)` path over the event, drift entry 1);
  `null` **clears**; one atomic `todo.set_event` `AccessAudit` row (C3).
  Web: `POST /projects/todos/{id}/set-event` (CSRF); the picker is
  seeded by the **frozen** `IEventService.ListMineAsync` (ADR 0065).
- **D4** — the `VTODO` calendar surface: a **BCL-only pure
  `TodoIcsWriter.BuildTodos(IReadOnlyList<TodoItem> todos, DateTimeOffset
  nowUtc)`** (the ADR 0112 `IcsWriter` discipline — no store, no clock,
  no library) emitting a **closed `VTODO` subset** (the ADR 0112
  envelope verbatim + one `VTODO` per dated to-do — `UID` / `DTSTAMP` /
  `DUE` (cond.) / `DTSTART` (cond.) / `SUMMARY` / `DESCRIPTION` (cond.,
  Markdown source) / `STATUS:CANCELLED` (cond.); **`UID` =
  `kw-todo-{Id}@kumunita`** (stable); `ORGANIZER`/`CREATED-BY`/`ATTENDEE`/
  `RRULE`/`X-…`/identity fields **never emitted** — the ADR 0028 posture,
  C-M14·5). **Two `[Authorize]` routes on `ProjectsController`**: `GET
  /projects/todos/{id}.ics` (one `VTODO`; the 404-not-403 split; an
  undated to-do still yields a valid degenerate `VTODO` — drift entry 4) +
  `GET /projects/todos.ics` (the caller's visible **dated** set, always
  200, valid-empty calendar; the skip rule is feed-only). Both served
  `text/calendar; charset=utf-8` + `Content-Disposition: attachment` +
  `Cache-Control: no-store` + `nosniff` (ADR 0034 / 0108); **no
  subscription token** (rides the cookie, ADR 0085 / 0095). **Six `kw-l`
  keys × en/de/fr/da** — `todo.event_link` / `todo.set_event.label` /
  `todo.set_event.pick` / `events.linked_todos` /
  `projects.todos.ics.download` / `projects.todos.ics.feed` (plain `<a>`,
  no new JS, ADR 0031).
- **D5** — **no new authorization surface** (C-M14·4): no new
  `AccessAction` / `AccessVia` / `Decide()` branch; all lanes reuse the
  frozen `Read` decisions + C-M5·6 for `set-event`.
- **D6** — **no `RRULE` / recurrence anywhere in M14** (C-M14·6) —
  **M18's home** (`Milestones.cs` is the source of truth). ADR 0112's
  "recurring events (`RRULE` — **M14** is the natural home)" forward-ref
  and the `IcsWriter.cs` "M14's home" doc-comment ref are **stale
  forward-refs**, written before the roadmap settled M18 — **superseded**
  (recorded in ADR 0115 Consequences).
- **D7** — the pinned test names are the feedback loop (U01:4, U02:3,
  U05:4, U06:0 new / parity pins move, U07:3 acceptance).
- **D8** — docs flip in one unit (**U07 close**): `Milestones.cs` (M14 →
  `StatusDone`, M15 → `StatusNext`), README Roadmap, `docs/STATUS.md`,
  `docs/ARCHITECTURE.md`, and `MilestonesTests` (re-pinned to **M15** as
  the single in-progress; the exact-order pin M0…M18 unchanged).

**The three acceptance tests (recorded by U07):**
1. **Closed loop** — `Interlock_ClosedLoop_TodoAndEventSeeEachOther`
2. **Handoff** — `Interlock_Handoff_WriteAndReadComposeWithoutNewAuthorizationSurface`
3. **Part vs. whole** — `Interlock_PartVsWhole_UnreadableEventOmitsTheChip`

**Drift recorded at U00 (each a refinement, not a decision change):**
1. The `set-event` lane's `eventId` visibility check runs through the
   **frozen `IAuthorizationService.CanAsync(Read)` path over the event**
   (the `SetTodoProjectAsync` target-visibility rule, C-M14·3/4), **not**
   by calling `IEventService.GetAsync` in `ProjectService` and catching
   exceptions (the register's U02 entry-read #4 suggested the latter; the
   house's association-lane precedent resolves target visibility through
   the authorization service — "a read is not a decision", ADR 0054). If
   U02 finds `SetTodoProjectAsync` in fact calls a sibling `GetAsync`, it
   may follow that implementation verbatim — the shape (refuse a
   non-visible target 404, one audit row, atomically) holds either way.
2. `ProjectService`'s constructor gains **no `IEventService` parameter**
   if drift entry 1's preferred reading holds (the design doc's §risks
   constructor-extension note is then moot; the register's U02 entry-read
   #4 remains the authoritative shape to copy *from* —
   `SetTodoProjectAsync`'s own implementation).
3. The U02 parameter name is **`todoItemId`** (matching the sibling
   `SetTodoProjectAsync(string todoItemId, …)`); the register's D3 prose
   said `todoId` — the sibling's actual signature wins.
4. The U06 **lane-1 degenerate form is locked**: a per-item request for
   an **undated** to-do emits a `VTODO` with only `UID` / `DTSTAMP` /
   `SUMMARY` (+`DESCRIPTION` when non-empty) — the RFC-legal form; the
   feed lane's skip rule (undated ⇒ omitted) applies to the *feed* only.
5. The **`LinkedTodoRow` shape is locked** (§D2 — `TodoId` / `Title` /
   `Status?` / `DueAt?` / `LinkPath`); the existing `TodoRow` is too heavy
   (it carries display fields a chip section does not render).

**Consistency check (U00):** the design doc, ADR 0115, and this note
agree on the seam signatures, the `EventId` field + index, the `VTODO`
pin (closed subset + never-emitted list), the two routes + serve shape,
the six `kw-l` keys, the invariants **C-M14·1…C-M14·7**, the locked
`LinkedTodoRow` shape, and the pinned test counts (U01:4 / U02:3 / U05:4 /
U06:0-new / U07:3). The only naming divergence (the register's `todoId`
vs the sibling's `todoItemId`) is resolved by **drift entry 3** in favor
of the live signature. The D6 RRULE→M18 supersession and the ADR-0112
`VTODO`-deferral supersession are recorded in ADR 0115 Consequences.
