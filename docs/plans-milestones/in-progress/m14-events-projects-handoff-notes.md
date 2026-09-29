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

## U01 — `EventId` field + `(EventId)` index + `ListTodosForEventAsync`

**Status:** done, build green, **993 / 993 Core tests pass** (0 errors,
0 skipped — includes the 4 U01 pins below, discovered + executed).
Implemented **only U01**: the data seam of the M14 interlock, additive
only (C-M14·7); no `IEventService` reference anywhere in `Projects`
(U01 stays in `Kumunita.Core.Projects`); the frozen `IEventService`
surface and every existing `IProjectService` lane untouched; **no**
`RRULE` / recurrence (C-M14·6 / D6); **no** new `AccessAction` /
`AccessVia` / `Decide()` branch / adapter (C-M14·4 — reuses the frozen
`CanSeeAsync(Read)` pass over the existing
`TodoItemToAuditableResource`).

**(a) The `EventId?` field + doc-comment** —
`src/Kumunita.Core/Projects/TodoItem.cs`, one additive line directly
beside `ProjectId` (exactly the design doc §D1 locked text, comment
included):

```csharp
public string? EventId { get; set; }   // a feed filter / association, NEVER a gate (C-M14·1) — the Event association (ADR 0115, the ProjectId shape, ADR 0086 D4)
```

Not added to `CreateTodoRequest` / `UpdateTodoRequest` (D3 — the
`ProjectId` precedent: association is a dedicated lane, not the main
form).

**(b) The `(EventId)` index** — `src/Kumunita.Core/M5DocTypes.cs`, on
the `TodoItem` block, **unnamed form** (the file's own note: Marten
`ComputedIndex` exposes no `Name`, so single-field indexes use the
auto-derived name — same as the `ProjectId` / `BlockedByTodoId`
precedents beside it), written exactly as the design doc §D1 locked
comment:

```csharp
.Index(t => t.EventId)
```

**(c) `ListTodosForEventAsync` signature + candidate-filter line** —
`src/Kumunita.Core/Projects/IProjectService.cs` (declaration, after
`ListTodosAsync`, with the full §D2 doc-comment), and
`src/Kumunita.Core/Projects/ProjectService.cs` (implementation, after
`ListTodosAsync`):

```csharp
Task<TodoPage> ListTodosForEventAsync(string eventId, string actorId, int page, CancellationToken ct = default);
```

Candidate-filter line, verbatim from the implementation:
`q.Where(t => !t.IsDeleted && t.EventId == eventId)` — **a filter, never
a gate** (C-M14·1); survivors `CanSeeAsync(Read)`-filtered over the
existing `TodoItemToAuditableResource` (one shared pass, C6); `Created`
descending; `HasMore = candidates.Count == PageSize` (ADR 0090 D1/D3);
0-candidate early return before any decision (no `AccessAudit` row,
C-M7·5); 1 aggregate `AccessAudit` row `TargetKind = "todo"` on a
non-empty page (C-M3·3, the `ListTodosAsync` shape, copied verbatim);
returns an empty page, never a 404/403.

**(d) The 4 pin names + pass/red** (all in
`tests/Kumunita.Core.Tests/ProjectServiceTests.cs`, the existing
`PostgresFixture` / `BootStoreAsync` / `Services` / `Plant` shape —
nothing new introduced to the harness):

1. `ListTodosForEventAsync_ReturnsEmptyPage_WhenNoTodosLinkedToTheEvent`
   — green
2. `ListTodosForEventAsync_DropsTodosTheActorCannotRead` — green
3. `ListTodosForEventAsync_ExcludesTodosLinkedToADifferentEvent` — green
4. `ListTodosForEventAsync_HasMoreIsTrue_WhenThePageFills` — green

(4 / 4 green; whole suite: `Kumunita.Core.Tests  Total: 993, Errors: 0,
Failed: 0, Skipped: 0`.)

**(e) Compile warnings** — none; `dotnet build Kumunita.slnx -c Debug`
succeeded clean (all 4 projects). **Drift vs. the register:** none
recorded — U01's seam, field, index, and pin names all match the design
doc verbatim; drift entry 3 (the `todoItemId` parameter name, a U02
concern) is honored — untouched, and the sibling `SetTodoProjectAsync`
shape is intact. **Docker cleanup note:** none needed — the run
completed normally (no orphaned containers observed).

## U02 — `set-event` write lane (`SetTodoEventAsync`)

**Status:** done, build green (0 warnings), **996 / 996 Core tests pass**
(0 errors, 0 skipped — includes the 3 U02 pins below, discovered +
executed) and **585 / 585 Web tests pass**. Implemented **only U02**: the
M14 `set-event` write lane, additive only (C-M14·7); the frozen
`IEventService` surface and every existing `IProjectService` lane
untouched; **no** new `AccessAction` / `AccessVia` / `Decide()` branch /
adapter (C-M14·4 — reuses the frozen `CanAsync(Read)` path over the
**existing** `EventToAuditableResource` adapter, M4 U02); **no**
`RRULE` / recurrence (C-M14·6 / D6); **no** Events → Projects
dependency (the `Projects` context references the `Events` context's
`Event` + `EventToAuditableResource` — the same direction as
`SetTodoProjectAsync` referencing `Project`; no reference is added to the
`Events` → `Projects` direction, D2's pin holds).

**Drift entries 1 + 2 honored (verified against the live sibling).**
The house's association-lane precedent — `SetTodoProjectAsync` — resolves
its **target**'s visibility through the **frozen**
`IAuthorizationService.CanAsync(Read)` path over the target's
`IAuditableResource` (a `ProjectToAuditableResource`), **not** by calling
a sibling service's `GetAsync`. So the preferred reading (drift entry 1)
holds, and **`ProjectService`'s constructor gains no `IEventService`
parameter** (drift entry 2 confirmed — the `Services(...)` test trio and
the existing positional constructor signature are unchanged). The
`set-event` event-guard mirrors the `set-project` project-guard
**verbatim**, swapping `Project` / `ProjectToAuditableResource` for
`Event` / `EventToAuditableResource` and `"todo.set_project"` for
`"todo.set_event"`.

**(a) The seam** — `src/Kumunita.Core/Projects/IProjectService.cs`, the
locked §D3 signature verbatim (drift entry 3 — `todoItemId`, matching the
sibling `SetTodoProjectAsync(string todoItemId, …)`), placed immediately
after `SetTodoProjectAsync`:

```csharp
Task<TodoItem> SetTodoEventAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct = default);
```

+ the full §D3 doc-comment (creator ∪ assignee ∪ GlobalAdmin re-checked
server-side C-M14·3; to-do `!IsDeleted` 404; a non-null `eventId` that is
non-existent / soft-deleted / unreadable refused 404 the non-leaky split;
`null` clears; `AuthorId` / `Created` untouched, `Modified` stamped; one
`AccessAudit` row `todo.set_event` / `TargetKind = "todo"` committed
atomically, C3; C-M14·4 no new surface).

**(b) The implementation** — `src/Kumunita.Core/Projects/ProjectService.cs`,
immediately after `SetTodoProjectAsync`, the **exact** `SetTodoProjectAsync`
shape:

- Standing: `CheckTodoStanding(actorId, actorRoles, todo)` (creator ∪
  assignee ∪ GlobalAdmin, C-M5·6) against the **stored** to-do.
- Existence: `LoadAsync<TodoItem>` → null / `IsDeleted` → `KeyNotFoundException`
  (404), **before** the standing check (same order as the sibling).
- **Event guard (drift entry 1):** `if (eventId is not null)` →
  `session.LoadAsync<Event>(eventId)` → null or `IsDeleted` →
  `KeyNotFoundException` (404, the non-leaky split — the
  `SetTodoProjectAsync` project-guard shape); then
  `_authorization.CanAsync(actorId, AccessAction.Read, new
  EventToAuditableResource(@event))` → `!Allowed` → `UnauthorizedAccessException`
  (the C3 split, the "a read is not a decision" posture, ADR 0054).
  `null` `eventId` = clear the link — the guard is skipped.
- Write: `todo.EventId = eventId;` (`null` = clear), `todo.Modified =
  DateTimeOffset.UtcNow;`, `session.Store(todo);`.
- Audit: `StoreAuditRow(session, actorId, "todo.set_event", todo.Id,
  TargetKindTodo, TodoAuditViaFor(actorId, todo));` then
  `SaveChangesAsync` — **one** row committed atomically (C3), the
  `todo.set_project` shape with the `todo.set_event` action string.

`ProjectService.cs` gains one `using Kumunita.Core.Events;` (the `Event`
+ `EventToAuditableResource` types) — **no constructor change**.

**(c) The 3 pin names + pass/red** (all in
`tests/Kumunita.Core.Tests/ProjectServiceTests.cs`, the existing
`PostgresFixture` / `BootStoreAsync` / `Services` / `Plant` shape — no
new harness; `using Kumunita.Core.Events;` added to the test file):

1. `SetTodoEventAsync_AllowsCreatorAssigneeAndGlobalAdmin_RefusesOutsider`
   — green (creator/assignee/GlobalAdmin each write `EventId`; the
   outsider is refused 403 and leaves the stored row untouched —
   C-M14·3 / C-M5·6)
2. `SetTodoEventAsync_RefusesInvisibleEventId_NullClearsTheLink` — green
   (the readable event writes; an audience-restricted event is refused
   for the standing creator — nothing written; `null` clears `EventId`
   and stamps `Modified` — C-M14·3, drift entry 1)
3. `SetTodoEventAsync_CommitsOneAccessAuditRowWithTheWrite` — green (one
   `todo.set_event` row, `TargetKind = "todo"`, `Via Owner` for the
   creator — C3)

(3 / 3 green; whole suite: `Kumunita.Core.Tests  Total: 996, Errors: 0,
Failed: 0, Skipped: 0`; `Kumunita.Web.Tests  Total: 585, Errors: 0,
Failed: 0, Skipped: 0` — the frozen Web pins survive the additive seam.)

**(d) Compile warnings** — none; `dotnet build Kumunita.slnx -c Debug`
succeeded clean (all 4 projects). **Drift vs. the register:** none
recorded — the seam signature (`todoItemId`), the standing rule, the
event-visibility rule, the `todo.set_event` audit action, and the 3 pin
names all match the design doc verbatim; drift entry 1 (the `CanAsync(Read)`
over `EventToAuditableResource`, **not** `IEventService.GetAsync`) and
drift entry 2 (no `IEventService` constructor parameter) both hold —
verified against the live `SetTodoProjectAsync` sibling. **Docker cleanup
note:** none needed — both runs completed normally (no orphaned
containers observed).

## U03 — both-direction display links (to-do → event chip + event → linked to-dos)

**Status:** D2 rendered in the UI — **both directions, access-scoped +
dangling-safe** (C-M14·2), **no new authorization surface** (C-M14·4),
**additive-only** (C-M14·7). Both the to-do → event chip and the event →
linked to-dos section resolve through the frozen / U01 seams, so the
linkage degrades gracefully and never leaks (an unreadable / absent /
soft-deleted target is omitted entirely — never a 404/403 for the source
surface, and its title / id are not leaked).

**(a) Additive view-model fields (as written).**

`src/Kumunita.Web/Models/ProjectTodoViewModels.cs` —
`TodoDetailViewModel` gains three trailing `null`-defaulted fields (the
ADR 0086 D9 `ProjectId?` / `ProjectTitle?` shape, the `ProjectLink`
idiom), appended after `Comments`:

```
string? EventId = null,
string? EventTitle = null,
string? EventLinkPath = null,
```

`src/Kumunita.Web/Models/EventEditorModel.cs` — `EventDetailViewModel`
gains one trailing `null`-defaulted field, appended after
`OriginalLanguageCode`:

```
IReadOnlyList<LinkedTodoRow>? LinkedTodos = null
```

and the **locked** `LinkedTodoRow` (drift-guard entry 5 — the `TodoRow`
is too heavy for a chip; the `BlockerChip`-sized row) is added as a new
sealed record beside `EventRsvpEntry`:

```
public sealed record LinkedTodoRow(
    string TodoId,
    string Title,
    string? Status,
    DateTimeOffset? DueAt,
    string LinkPath);
```

**(b) The two dangling-safe resolve calls + their catch.**

**To-do → event chip** — `ProjectsController` `GET
/projects/todos/{id}` (the `TodoDetail` action): resolves
`result.Todo.EventId` through the **frozen** `IEventService.GetAsync`
(the seam's 404-vs-403 split, C-M14·7 — the frozen surface untouched).
Guarded on `result.Todo.EventId is { Length: > 0 } && events is not null`
(the optional-ctor idiom — absent service = chip simply omits). On
success with `!ev.IsDeleted`: sets `eventId = ev.Id`, `eventTitle =
ev.Title`, `eventLinkPath = "/events/" + ev.Id`. **Catch (dangling-safe,
C-M14·2):** `KeyNotFoundException` (soft-deleted) and
`UnauthorizedAccessException` (unreadable) both **leave all three chip
fields `null`** — the chip is omitted entirely, never a 404/403 for the
to-do, and the target's title / id are not leaked. A non-null `IsDeleted`
also leaves them null (the `BlockerChip.Generic` / ADR 0086 D9
idiom carried to the event link). The three fields flow into
`TodoDetailViewModel` (`EventId` / `EventTitle` / `EventLinkPath`).

`ProjectsController` gains an **optional** `IEventService? events`
constructor parameter (default null — the `EventController`
optional-ctor-param idiom, so the existing 5-arg test-construction sites
keep compiling; DI always supplies the live `IEventService`). One
`using Kumunita.Core.Events;` added. **No new authorization surface** —
the chip reuses the frozen `GetAsync` `Read` decision (C-M14·4).

**Event → linked to-dos** — `EventController` `GET /events/{id}` (the
`Detail` action): calls **U01's** `IProjectService
.ListTodosForEventAsync(ev.Id, actorId, 0, HttpContext.RequestAborted)`
(page 0 — the detail surface, not a paged list; `HasMore` ignored — the
section is display-only, the `BlockerChip` / `ProjectLink` "one section,
not a feed" precedent). The seam's `CanSeeAsync(Read)` pass is the gate
(C-M14·1 / C-M14·4 — **no new authorization surface**, composition stays
in the Web layer, no `Core.Events` → `Core.Projects` dependency). Maps
each `TodoItem` to a `LinkedTodoRow` (`TodoId` = `t.Id`, `Title` =
`t.Title`, `Status` = `t.Status`, `DueAt` = `t.DueAt`, `LinkPath` =
`"/projects/todos/" + t.Id`). **Dangling-safe (C-M14·2):** `Items.Count
> 0` ⇒ `linkedTodos` = mapped rows; an empty page ⇒ `linkedTodos` stays
**`null`** and the view renders **nothing** (no empty-state section for
an absent linkage — the design-doc rule, not an invented empty-state).

`EventController` gains an **optional** `IProjectService? projects`
constructor parameter (default null — the `ProjectsController` idiom,
so the existing 5/6-arg test-construction sites keep compiling; DI
always supplies the live `IProjectService`). One `using
Kumunita.Core.Projects;` added. The `LinkedTodos` field flows into
`EventDetailViewModel`.

**(c) `kw-l` keys × en/de/fr/da** (the two U03 keys — the design-doc
§kw-l locked set; added beside the existing `events.ics.*` M12 block in
`KnownTranslationKeys.cs`, which holds all four language dictionaries —
there are no separate `en.json` / `de.json` / `fr.json` / `da.json`
files in this repo, the closed-key registry **is** the parity surface the
`KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests` pins
enforce):

| Key | en | de | fr | da |
|---|---|---|---|---|
| `todo.event_link` | Linked event | Verknüpfte Veranstaltung | Événement lié | Knyttet arrangement |
| `events.linked_todos` | Linked to-dos | Verknüpfte To-dos | To-dos liés | Knyttede to-dos |

The `todo.event_link` key labels the to-do detail chip (`Views/Projects/
TodoDetail.cshtml`, rendered **only** when
`Model.EventId is not null && Model.EventTitle is not null`, the
`ProjectLink` chip directly below it — absent linkage renders nothing).
The `events.linked_todos` key labels the event detail section
(`Views/Event/Detail.cshtml`, rendered **only** when
`Model.LinkedTodos is { Count: > 0 }`, one row per to-do — title link +
optional status badge + optional due date via the existing
`projects.todo.due` `kw-l` key + the `<kw-dt>` TagHelper). The two
picker/feed keys (`todo.set_event.label` / `todo.set_event.pick` /
`projects.todos.ics.*`) are **not** in U03 — they are U04 / U06.

**(d) Drift vs. the register** — none. The two resolve shapes (frozen
`GetAsync` for the chip, U01's `ListTodosForEventAsync` for the section),
the `LinkedTodoRow` shape (drift-guard entry 5), the `null`-defaulted
additive view-model fields, the "absent ⇒ omit, never 404/403, no
leak" rule (C-M14·2), and the "empty ⇒ `null`, no empty-state" rule all
match the design doc §D2 / §seams contract 4 verbatim. The register's
U03 entry-reads named the views as `Views/Projects/Todo/Detail.cshtml` +
`Views/Events/Detail.cshtml` — the **actual** live paths are
`Views/Projects/TodoDetail.cshtml` + `Views/Event/Detail.cshtml` (a
cosmetic path mismatch only; the views were edited at their real
location). Both new controller dependencies are **optional constructor
parameters** (the house `ITagService?` / `ITranslationProvider?` idiom),
so the frozen `IEventService` / `IProjectService` surfaces are untouched
(C-M14·7) and no existing test-construction site required a change.

**(e) Build + test.** `dotnet build Kumunita.slnx -c Debug` succeeded
clean (all 4 projects). `Kumunita.Web.Tests  Total: 585, Errors: 0,
Failed: 0, Skipped: 0` (the `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` pins the two new keys satisfy, plus the
frozen `EventControllerTests` / `ProjectsController` detail pins, survive
the additive view-model fields + optional ctor params).
`Kumunita.Core.Tests  Total: 996, Errors: 0, Failed: 0, Skipped: 0` (the
kw-l registry's Core-side parity pins). **Docker cleanup note:** none
needed — both runs completed normally (no orphaned containers observed).

## U04 — `set-event` Web lane + event picker + `kw-l`

**Status:** done, build green (0 warnings, all 4 projects), **585 / 585 Web
tests pass** (0 errors, 0 skipped — includes the `KnownTranslationKeys_ParityTests`
+ `KwLRegistryConsistencyTests` pins the two new keys satisfy, plus the frozen
`ProjectsController` / `EventController` detail pins, which survive the additive
view-model field + optional ctor param). Implemented **only U04**: the M14
`set-event` Web lane (D3 rendered in the UI), additive only (C-M14·7); the
**frozen** `IEventService` surface and the **U02** `SetTodoEventAsync` seam are
consumed, not re-added/renamed; `EventId` is **not** on
`CreateTodoRequest` / `UpdateTodoRequest` (the association is a dedicated lane,
not the main form — D3); **no** new `AccessAction` / `AccessVia` / `Decide()`
branch (C-M14·4 — the lane reuses the U02 standing + the frozen `Read`
decision); **no** `RRULE` / recurrence (C-M14·6 / D6); **no** new JS (one
native `<form>` / `<select>` — the ADR 0031 tsc-only discipline holds).

**(a) The `set-event` action signature + route** —
`src/Kumunita.Web/Controllers/ProjectsController.cs`, placed immediately after
`TodoSetProject` (the `set-project` lane it copies verbatim):

```csharp
[HttpPost("/projects/todos/{id}/set-event")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> TodoSetEvent(string id, [FromForm] string? eventId)
```

The body mirrors `TodoSetProject` exactly: one `eventId` form field bound
nullable (`string.IsNullOrWhiteSpace(eventId) ? null : eventId` — blank / `null`
posts as the **clear** affordance, the `set-project` lane's `null`-clears
shape); calls the **U02** `SetTodoEventAsync` seam (whose standing
`creator ∪ assignee ∪ GlobalAdmin` + 404/403 decision is **truth** —
C-M14·3; the Web `[Authorize]` is a convenience pre-gate only — the controller
does no standing math, C-M14·4); catches `KeyNotFoundException` → 404 and
`UnauthorizedAccessException` → `ForbidResult` (the seam's decision as
truth); sets `TempData["info"]` to "Event link cleared." / "Event linked.";
redirects back to `/projects/todos/{id}` on success. **No new authorization
surface** — the action reuses the U02 standing + the frozen `Read` decision.

**(b) The picker read seam + cap** — `SeedEventPickerAsync()` (a new private
helper beside `SeedProjectPickerAsync`), seeds the picker from the **frozen**
`IEventService.ListMineAsync(actorId, ct)` (the actor's own events — authored
∪ RSVPed — the ADR 0065 posture), **capped at 25** for the `<select>` (a
**display** cap, not a gate — C-M14·4; `.Take(25)`), sorted by name
ordinal-ignoring-case (the `SeedProjectPickerAsync` shape). A `null`
`events` service (test construction without DI) or an empty / denied read
returns `[]` (the picker card hides — the ADR 0086 D9 hide rule). The picker
is a **display surface, never a gate** (C-M14·4 — the service's write-time
standing re-check + event guard is the enforcement, C-M14·3).

**(c) The `TodoEventPicker` view-model + the `EventPicker` field** — U03 did
not provide a picker model, so U04 adds one (the design doc is the authority —
D3 names `TodoEventPicker`, "the visible-event list + the current `EventId`"):

`src/Kumunita.Web/Models/ProjectTodoViewModels.cs`:

```csharp
public sealed record TodoEventPicker(
    IReadOnlyList<(string Id, string Name)> Options,
    string? CurrentEventId = null);
```

+ `TodoDetailViewModel` gains one trailing `null`-defaulted additive field,
appended after `EventLinkPath`:

```
TodoEventPicker? EventPicker = null
```

`ProjectsController` `GET /projects/todos/{id}` (the `TodoDetail` action)
populates it: `EventPicker: new TodoEventPicker(Options: await
SeedEventPickerAsync(), CurrentEventId: result.Todo.EventId)`. The field is
additive (`null`-defaulted) — the frozen view-model pins survive.

**(d) The picker `<form>` / `<select>` (the view wiring)** —
`src/Kumunita.Web/Views/Projects/TodoDetail.cshtml`, rendered **only** when
`Model.EventPicker is { Options.Count: > 0 }` (the ADR 0086 D9 hide rule — an
empty picker is a noise surface, not a control), placed directly below the
U03 event-link chip block (the same location the picker joins). A native
`<form method="post" action="/projects/todos/{id}/set-event">` +
`@Html.AntiForgeryToken()` + a `<select name="eventId">` whose **first option
is the "clear" affordance** (`<option value="">` labeled `todo.set_event.pick`)
+ one `<option>` per visible event (selected when `id ==
Model.EventPicker.CurrentEventId`) + one submit button. **No new JS** — a
single native form / select (the ADR 0031 tsc-only discipline).

**(e) The two U04 `kw-l` keys × en/de/fr/da** (the design-doc §kw-l locked set;
added beside the existing U03 M14 block in
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`, which holds all four
language dictionaries — the closed-key registry **is** the parity surface the
`KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests` pins
enforce; there are no separate `*.json` files):

| Key | en | de | fr | da |
|---|---|---|---|---|
| `todo.set_event.label` | Link to event | Mit Veranstaltung verknüpfen | Lier à un événement | Knyt til arrangement |
| `todo.set_event.pick` | Choose an event | Veranstaltung wählen | Choisir un événement | Vælg et arrangement |

The `todo.set_event.label` key labels the picker form + submit button; the
`todo.set_event.pick` key labels the `<select>`'s placeholder / clear option.
The two U06 keys (`projects.todos.ics.*`) are **not** in U04.

**(f) Drift vs. the register** — one cosmetic note (not a decision change):
the register's U04 entry-reads named the view `Views/Projects/Todo/Detail.cshtml`
+ the view-model as "if U03 did not already add it" — the **actual** live view
path is `Views/Projects/TodoDetail.cshtml` (a cosmetic path mismatch only, the
same note U03 recorded), and U03 added the chip fields but **not** a picker
model, so U04 added the `TodoEventPicker` record (the design-doc D3 name) as a
new additive view-model — exactly the design-doc's "add a small `TodoEventPicker`
view-model … only if U03 did not already provide it" branch. All other shapes
(the action signature + route, the `ListMineAsync` seam + the 25-cap, the
`null`-clears rule, the redirect-back target, the `kw-l` keys, the no-new-JS
rule, and the no-new-authorization-surface rule) match the design doc §D3 /
§seams contract 3 verbatim.

**(g) Build + test.** `dotnet build Kumunita.slnx -c Debug` succeeded clean
(all 4 projects). `Kumunita.Web.Tests  Total: 585, Errors: 0, Failed: 0,
Skipped: 0` (the `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests` pins the two new keys satisfy, plus the frozen
`ProjectsController` detail pins, survive the additive `TodoEventPicker`
view-model + `EventPicker` field + the new `TodoSetEvent` action + the new
`SeedEventPickerAsync` helper — no existing test-construction site required a
change, since `IEventService` is an optional ctor param and the new action /
helper are additive). **Docker cleanup note:** none needed — the Web run
completed normally (Testcontainers cleaned up its own containers:
"Delete Docker container …" observed for all 5).

## U05 — `TodoIcsWriter` (VTODO emitter)

**Status:** done, build green (0 warnings, all 4 projects), **1000 / 1000
Core tests pass** (0 errors, 0 skipped, 115 s — includes the 4 U05 pins
below, discovered + executed). Implemented **only U05**: the M14 `VTODO`
emitter (D4 rendered as code), additive only (C-M14·7); the **frozen**
`IcsWriter` and the `IProjectService` read seams are **consumed, not
re-added/renamed**; **no new authorization surface** (C-M14·4 — the emitter
is pure: no store, no `IDocumentSession`, no `IAuthorizationService`, no
clock, no reflection, no library — BCL-only, C-M14·5); **no `RRULE` /
`RECURRENCE-ID` / recurrence of any kind** (C-M14·6 / D6); **no**
`ORGANIZER` / `CREATED-BY` / `ATTENDEE` / `X-…` / identity fields emitted
(C-M14·5, the ADR 0028 posture); **no** `kw-l` keys and **no** routes
(both U06's surface — the two `projects.todos.ics.*` affordances and the
two `.ics` actions are untouched).

**(a) The `BuildTodos` signature + the `VTODO` property set as emitted.**
New static class `src/Kumunita.Core/Events/TodoIcsWriter.cs` (the
`IcsWriter` shape — one `using Kumunita.Core.Projects;` for the
`TodoItem` POCO; no constructor, no dependencies), the **only public
surface** (the §seams contract 5 locked text, verbatim):

```csharp
public static string BuildTodos(IReadOnlyList<TodoItem> todos, DateTimeOffset nowUtc)
```

`nowUtc` = the caller's fetch instant — the `DTSTAMP` refresh marker; the
emitter **never reads a clock** (deterministic + testable). The return is
the **entire** ICS file: the `VCALENDAR` envelope **verbatim from
`IcsWriter`** (`BEGIN:VCALENDAR` / `VERSION:2.0` /
`PRODID:-//Kumunita//community calendar//EN` / `CALSCALE:GREGORIAN` /
`METHOD:PUBLISH` / … / `END:VCALENDAR`) + **one `VTODO` per dated to-do**
in list order. **Feed skip rule** (§vtodo): the candidate set is
`t.DueAt is not null || t.StartAt is not null`; a to-do with **neither**
is skipped (no `VTODO` block — the drift entry 4 degenerate form is the
U06 lane-1 concern, not the feed). The emitted property set, in the locked
§vtodo order, per dated to-do:

```
BEGIN:VTODO
UID:kw-todo-{todo.Id}@kumunita              (stable — the kw-eve-… convention with the todo tag)
DTSTAMP:{nowUtc as yyyyMMddTHHmmss}Z        (the caller-passed refresh marker, not Modified)
DUE:{todo.DueAt as yyyyMMddTHHmmss}Z        (only when DueAt is set)
DTSTART:{todo.StartAt as yyyyMMddTHHmmss}Z  (only when StartAt is set)
SUMMARY:{todo.Title escaped}
DESCRIPTION:{todo.Body escaped, folded}     (only when Body is non-empty — the Markdown source)
STATUS:CANCELLED                            (only when todo.IsDeleted == true — the RFC update path, emitter capability)
END:VTODO
```

CRLF on every line (incl. the final `END:VCALENDAR`), 75-octet fold at a
multi-byte-safe boundary with a single leading space on continuations, and
escape (`\` → `\\` first, `;` → `\;`, `,` → `\,`, CR/LF → `\n`) — the
`IcsWriter` `FormatUtc` / `Escape` / `Fold` private-helper discipline
copied verbatim (same private methods, re-implemented in this class so the
frozen `IcsWriter` is byte-untouched, C-M14·7). A valid **empty**
`VCALENDAR` (envelope only, zero `VTODO`) when no dated to-dos — the
RFC-legal no-crash path (C-M14·5/6).

**(b) The never-emitted list — confirmed absent.** Pin 1 asserts by
**property name** (the part before the first `:` of each physical line,
after unfolding continuations) that none of `ORGANIZER` / `CREATED-BY` /
`ATTENDEE` / `RRULE` / `RECURRENCE-ID` / `AUDIENCE` / `STATUS` (the
non-deleted case) / `SEQUENCE` / `PRIORITY` / `CLASS` / `CREATED` /
`LAST-MODIFIED` / `REQUEST-ID` appears, and **no property name starts with
`X-`** — i.e. no author/assignee identity, no recurrence, no audience /
grant / membership internals, and no vendor property (C-M14·5 / C-M14·6).
The emitter reads **only** `Id` / `Title` / `Body` / `StartAt` / `DueAt`
/ `IsDeleted` off the POCO; `AuthorId` / `AssigneeId` / `Audience` /
`ComponentId` / `ProjectId` / `EventId` / `ParentId` / `BlockedByTodoId`
/ `Status` / `LanguageCode` / `TagIds` / `ImageIds` / `AttachmentIds` /
`Created` are never touched.

**(c) The 4 pin names + pass/red** (all in
`tests/Kumunita.Core.Tests/TodoIcsWriterTests.cs` — the pure-emitter
shape, **no Testcontainers**; the `IcsWriterTests` file is the model —
shared `FindLine` / `CountOccurrences` helpers, a fixed
`NowUtc = 2026-09-28T12:00Z`, a `MakeTodo(id, title, body, startAt,
dueAt, isDeleted)` factory):

1. `BuildTodos_EmitsThePinnedVTodoSubset_ForADatedTodo` — green
   (envelope verbatim + the 6-line pinned property set in locked order +
   the never-emitted property-name sweep)
2. `BuildTodos_EmitsDueOnly_DtstartOnly_SkipsUndated` — green (DUE-only
   block has no `DTSTART:`, DTSTART-only block has no `DUE:`, the undated
   to-do produces **zero** `VTODO` blocks — 2 blocks for 3 inputs)
3. `BuildTodos_FoldsAndEscapesALongTitleAndBody` — green (all four escape
   rules in SUMMARY; a 120-octet `DESCRIPTION` folds to ≥ 2 physical
   lines, every physical line ≤ 75 octets, continuation begins with
   exactly one space, unfolding reconstructs the logical line)
4. `BuildTodos_ReturnsAValidEmptyCalendar_WhenNoDatedTodos` — green
   (empty set **and** an all-undated set both yield exactly the 5-line
   envelope + `END:VCALENDAR`, zero `VTODO` — C-M14·5/6)

(4 / 4 green; whole suite: `Kumunita.Core.Tests  Total: 1000, Errors: 0,
Failed: 0, Skipped: 0, Not Run: 0, Time: 115.359s` — the prior 996 pins
survive the additive emitter; the 4 new pins are the delta.)

**(d) Compile warnings** — none; `dotnet build Kumunita.slnx -c Debug`
succeeded clean (all 4 projects). **Drift vs. the register:** none — the
`BuildTodos` signature, the closed `VTODO` property set + order, the
conditional `DUE`/`DTSTART`/`DESCRIPTION`/`STATUS` shape, the `kw-todo-{Id}@kumunita`
UID, the skip rule, the envelope, the PRODID, the never-emitted list, the
format pin, and the 4 pin names all match the design doc §vtodo / §seams
contract 5 / §pinned tests verbatim. **Docker cleanup note:** the Core run
completed normally (its own Testcontainers were reaped by the xunit.v3
runner at exit); `docker container ls` after the run shows only the
**app's** long-lived containers (`kumunita_app` / `kumunita_db` /
`kumunita_mailpit` — the dev-compose stack) and one transient
`testcontainers-ryuk-*` (the M14 test run's reaper, already orphaned-
cleanup eligible) — **no** orphaned `postgres:18` test containers, so no
`docker container prune` was needed.

## U06 — the two `VTODO` routes + the two `kw-l` affordances

**Status:** done, build green (0 warnings, all 4 projects), **585 / 585 Web
tests pass** (0 errors, 0 skipped — includes the
`KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests` pins the
two new keys satisfy) + **1000 / 1000 Core tests pass** (0 errors, 0
skipped, ~127 s — the kw-l registry's Core-side parity pins over the two new
keys). Implemented **only U06**: the M14 `VTODO` Web surface (D4 rendered in
the UI), additive only (C-M14·7); the **frozen** `IProjectService` read
seams (`ListTodosAsync` / `GetTodoAsync`) and the **U05** `TodoIcsWriter`
emitter are **consumed, not re-added/renamed**; **no new authorization
surface** (C-M14·4 — the routes ride the class-level `[Authorize]` + the
frozen seams' own `Read` decisions; the dated filter is a display filter,
never a gate); **no `RRULE` / recurrence anywhere** (C-M14·6 / D6 — the
emitter is U05's and already closed, the routes add none); **no**
`ORGANIZER` / `CREATED-BY` / `ATTENDEE` / `X-…` / identity material reaches
the file (C-M14·5 — the routes pass the frozen seams' already-authorized
output straight into `TodoIcsWriter`); **no new JS** (two plain `<a>`
links — the ADR 0031 tsc-only discipline holds); **no subscription token**
(rides the cookie, re-authorized every fetch — the M6 link-lane posture,
ADR 0085/0095).

**(a) The two routes + their serve shapes.**
`src/Kumunita.Web/Controllers/ProjectsController.cs`, placed immediately
after the U04 `TodoSetEvent` action (the `POST
/projects/todos/{id}/set-event` lane):

- **Lane 1 — `TodoIcs(string id)`** under
  `[HttpGet("/projects/todos/{id}.ics")]` — the **404-not-403 split**
  (C-M14·2, the M12 `EventController.EventIcs` idiom verbatim):
  `projects.GetTodoAsync(id, actorId, HttpContext.RequestAborted)` inside
  a try; **both** `KeyNotFoundException` (absent) and
  `UnauthorizedAccessException` (denied) map to `NotFound()` — a to-do a
  caller cannot see neither downloads nor 403s into existence. One `VTODO`
  via `TodoIcsWriter.BuildTodos([todo], DateTimeOffset.UtcNow)` — the
  to-do's own single `VTODO` on the pinned subset (§vtodo); an **undated**
  to-do still yields the degenerate `VTODO` (only `UID` / `DTSTAMP` /
  `SUMMARY` (+`DESCRIPTION`) — **drift entry 4**: the feed's skip rule does
  **not** apply on the per-item lane). Serve shape (locked):
  `Content-Type: text/calendar; charset=utf-8` (the `File(...)` second arg)
  + `Content-Disposition: attachment; filename="kumunita-todo-{id}.ics"` +
  `Cache-Control: no-store` + `X-Content-Type-Options: nosniff` — the three
  headers set on `HttpResponse.Headers` before the return, the
  `EventIcs` action's exact idiom (ADR 0034 / 0108).
- **Lane 2 — `TodosIcsFeed()`** under
  `[HttpGet("/projects/todos.ics")]` — **always `200`** (the M12
  `CalendarFeed` idiom verbatim):
  `projects.ListTodosAsync(null, null, actorId, 0, ct:
  HttpContext.RequestAborted)` (the caller's **visible** to-do set — the
  candidate filter + the `CanSeeAsync(Read)` gate all inside the frozen
  seam, C-M14·7) → **the "dated to-dos" Web-layer filter line**:

## U07 — acceptance + docs parity (the close)

**Status:** done, build green (0 warnings, all 4 projects). **No production
code changed** (a close unit — C-M14·7; the close ships tests + docs +
the plans archive move only). Delivered **all three verbatim acceptance
tests** in `tests/Kumunita.Web.Tests/M14InterlockTests.cs` (the D7
feedback loop — the names are the pins), the **Web-side cross-surface
read-shape pins** that Core's own pins do not cover (see (b)), and the
**D8 docs-parity flip** across `Milestones.cs` / `MilestonesTests.cs` /
`README.md` / `docs/STATUS.md` / `docs/ARCHITECTURE.md`.

**(a) The three acceptance tests — all green.** All three are
`[Fact]` on `M14InterlockTests` (the `PostgresFixture` class-fixture
harness — one shared `postgres:18` container; `BuildRealStoreAsync` boots
a real Marten `DocumentStore` over `M1DocTypes` / `M3DocTypes` /
`M4DocTypes` / `M5DocTypes`, since the to-do-detail chip resolves the
event over a **real store session** in `ProjectsController.TodoDetail`);
NSubstitute stands in for the frozen service seams
(`IProjectService` / `IEventService` / `IUserInfoService` /
`ILocalizationService`). Run: `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
-class Kumunita.Web.Tests.M14InterlockTests` → **3 / 3 green, 0 errors**
(~11 s).

1. **`Interlock_ClosedLoop_TodoAndEventSeeEachOther`** — **green.** The
   closed loop, three legs: **(A)** the write leg — `TodoSetEvent(todoId,
   eventId)` returns `Redirect("/projects/todos/{todoId}")` and
   `projects.Received(1).SetTodoEventAsync(todoId, actorId, roles,
   eventId, ct)` (the D3 lane, the `POST /projects/todos/{id}/set-event`
   action → the `IProjectService.SetTodoEventAsync` seam); **(B)** the
   to-do → event chip (real store) — `projects.GetTodoAsync` returns a
   `TodoDetailResult` whose `Todo.EventId` is set,
   `events.GetAsync(eventId, …)` resolves the `Event` (the frozen M4 seam),
   the `ViewResult.Model` is a `TodoDetailViewModel` with
   `EventId == eventId` + `EventTitle` (the event title) +
   `EventLinkPath == "/events/{eventId}"` + `EventPicker.CurrentEventId ==
   eventId` (the D2 chip shape, drift-guard); **(C)** the event → to-do
   leg — `EventController.Detail(eventId)` over
   `detailProjects.ListTodosForEventAsync(eventId, actorId, 0, ct)`
   (U01's reverse seam) returning a non-empty `TodoPage` yields an
   `EventDetailViewModel` whose `LinkedTodos` is a single
   `LinkedTodoRow` with `TodoId == todoId` + `Title` +
   `LinkPath == "/projects/todos/{todoId}"` +
   `detailProjects.Received(1).ListTodosForEventAsync(…)`. The two
   directions compose over the frozen seams with **no new authorization
   surface** (C-M14·4).

2. **`Interlock_Handoff_WriteAndReadComposeWithoutNewAuthorizationSurface`**
   — **green.** The write→read handoff **without a new authorization
   surface**: **(1)** the load-bearing C-M14·4 pin — `typeof(AccessAction)`
   exposes **exactly** the frozen two static `AccessAction` fields
   `Read` + `Moderate` (sorted names `["Moderate", "Read"]`;
   `AccessAction.Read.Id == "read"`, `AccessAction.Moderate.Id ==
   "moderate"`); M14 adds none. (A first draft also asserted the
   `AccessVia` type was absent — **removed**: `AccessVia` is a
   **pre-existing** M3/M5 audit `enum` in `Kumunita.Core.Authorization.Decision`,
   not an M14 surface, so asserting its absence was an over-reach.)
   **(2)** the write leg set + clear — `SetTodoEventAsync(todoId, …,
   eventId, …)` returns the `TodoItem`, then a follow-up `TodoSetEvent(todoId,
   null)` → `Redirect` (the `null`-clears branch, D3) — the write lane is
   a single seam, not a decision; **(3)** the read leg — the chip
   (`vm.EventId == eventId`) **rides the `Read` decision, not a new
   one** (the `GetAsync` resolve; C-M14·4). No new `AccessAction` /
   `AccessVia` / `Decide()` branch is required anywhere.

3. **`Interlock_PartVsWhole_UnreadableEventOmitsTheChip`** — **green.**
   The dangling-safe read (C-M14·2, the `ProjectLink` / `BlockerChip`
   idiom — omit, never 404/403, no title/id leak). Four cases, each over a
   real-store `TodoDetail` whose `Todo.EventId` is set (or null in case 4),
   asserting the page still 200s (`ViewResult`) and the chip fields
   `EventId` / `EventTitle` / `EventLinkPath` are all `null` (no leak):
   **case 1 — denied** (`events.GetAsync` throws
   `UnauthorizedAccessException` — the chip omits, no 403 into existence);
   **case 2 — absent** (`KeyNotFoundException` — no 404); **case 3 —
   soft-deleted** (`ev.IsDeleted == true` — the chip omits the
   soft-deleted event); **case 4 — no association** (`Todo.EventId == null`
   — the chip block is skipped, `events.DidNotReceiveWithAnyArgs().
   GetAsync(…)`). (A first draft also asserted
   `vm.EventPicker.CurrentEventId` was null — **removed**: the picker's
   `CurrentEventId` is the actor's **own stored association** prefill, not a
   chip leak; the three chip fields are the C-M14·2 pin.)

**(b) Pins added (U07) vs. already covered.** U07 added the **Web-side
cross-surface read-shape pins** that the Core-side pins do not reach —
the `M14InterlockTests` class is new; the three tests pin the
**controller composition** over the frozen seams: the
`TodoDetailViewModel` chip shape (`EventId` / `EventTitle` /
`EventLinkPath` — drift-guard), the `EventDetailViewModel.LinkedTodos`
row shape (`LinkedTodoRow` — drift-guard), the `SetTodoEventAsync`
write-leg + redirect mapping (the D3 lane), the dangling-safe omission
across the 4 cases (denied / absent / soft-deleted / unlinked), and the
C-M14·4 `AccessAction` closed-set pin. **Already covered and NOT
duplicated** (in `Kumunita.Core.Tests`): U01's 4 `ListTodosForEventAsync`
seam pins (the reverse read — the `(EventId)` index + the `CanSeeAsync(Read)`
gate + the `TodoPage` shape), U02's 3 `SetTodoEventAsync` write-lane pins
(`ProjectServiceTests` — standing creator ∪ assignee ∪ GlobalAdmin, the
non-visible-target 404, the `null`-clears + the one `todo.set_event` audit
row), and U05's 4 `TodoIcsWriter` emitter pins (`TodoIcsWriterTests` — the
closed `VTODO` subset, the DUE/DTSTART conditional, the fold/escape, the
valid-empty calendar). U07 does **not** re-pin any of the Core-side
shapes; it pins the Web composition over them.

**(c) The D8 docs-parity flip (as made).** Five surfaces, kept together:

1. **`src/Kumunita.Web/Milestones.cs`** — M14 → `StatusDone`
   (`new("M14", "Integration of Events and Projects", StatusDone)`); M15 →
   `StatusNext` (the single in-progress milestone flips forward; M16/M17/M18
   stay `StatusPlanned`).
2. **`tests/Kumunita.Web.Tests/MilestonesTests.cs`** — the
   single-in-progress test re-pinned to **M15** (renamed
   `M15_Is_The_Single_InProgress_Milestone`; `Assert.Equal("M15",
   next[0].Id)`; the planned-remainder loop now `new[] { "M16", "M17",
   "M18" }`; the shipped list gains `"M14"`). The exact-order pin
   `Roadmap_Covers_M0_Through_M14_Plus_Named_Lanes_In_Order` (list ends
   `"M12","M13","M14","M15","M16","M17","M18"`) is unchanged — it already
   carried M14. `No_Milestone_Has_Blank_Title` unchanged.
3. **`README.md`** — the Status line flips "M14 in progress" → "M15 in
   progress (translation bulk…); M1–M14 and all named lanes are done"; the
   Roadmap M14 row → the full scope sentence + `**Done.** (ADR 0115)`, and
   the M15 row → `**In progress.**`.
4. **`docs/STATUS.md`** — the M13/M14 close line on the live milestone
   (the long line 40) flips "…next is M14 — integration of Events and
   Projects…" → "…M14 is done — integration of Events and Projects (the
   `TodoItem.EventId?` association — a feed filter, never a gate — C-M14·1;
   the `ListTodosForEventAsync` reverse read seam + the `SetTodoEventAsync`
   set-event write lane; the both-direction display links — dangling-safe —
   C-M14·2; the `TodoIcsWriter` VTODO surface + the two `.ics` routes, no
   `RRULE` — C-M14·6; zero new authorization surface — C-M14·4; ADR 0115);
   next is M15 — translation bulk…; After that, three planned milestones:
   M16 — inventory…".
5. **`docs/ARCHITECTURE.md`** — the value-chain table's M14 row
   (`| **M14** integration of Events and Projects | **coordination** — the
   two coordination surfaces interlock |`) is **status-free** (the M13 / M14
   rows carry no done-marker — the M13-close precedent), so the reflection
   goes into the shape-of-code sections following the `PL ✓ (ADR 0086)` /
   `RC ✓ (ADR 0025)` precedent: the **`Projects/`** tree row gains
   `M14 ✓ (ADR 0115)` — the `EventId?` association (a feed filter, never a
   gate — C-M14·1) + the `ListTodosForEventAsync` reverse seam + the
   `SetTodoEventAsync` set-event lane (C-M5·6; non-visible target 404;
   `null` clears; one `todo.set_event` audit row) + the `TodoIcsWriter`
   `VTODO` emitter + the two `.ics` routes, **zero new authorization
   surface** (C-M14·4), no `RRULE` (C-M14·6); the **`Events/`** tree row
   gains `M14 ✓ (ADR 0115)` — the event-detail "linked to-dos" section
   (the frozen `GetAsync` chip resolve + U01's `ListTodosForEventAsync`,
   access-scoped + dangling-safe — C-M14·2) + the `LinkedTodoRow` shape +
   the `events.linked_todos` `kw-l` key; no `RRULE` (C-M14·6). Both rows
   cite `docs/design/m14-events-projects-design.md`. The §2 "now live"
   paragraph + the §3 feature-modules line already enumerate the
   feature-module contexts by ADR; M14 adds **no new bounded context or
   DocTypes surface** (it interlocks the existing `Events/` + `Projects/`),
   so those lines are unchanged — the interlock is reflected where the two
   owning contexts live (the tree rows).

**(d) Plans archive move.** The eight `m14-u00.md` … `m14-u07.md` unit
plans move from `docs/plans-milestones/in-progress/` →
`docs/plans-milestones/done/` (the M13 close shape — the handoff note + the
register `plan-m14-events-projects.md` stay in place).

**(e) Full-suite state at the close.** `dotnet build Kumunita.slnx -c
Debug` — clean (0 warnings, 4 projects). `dotnet exec
tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` — the
Web suite rises from the U06 baseline of 585 (the three new
`M14InterlockTests` are the delta: 585 → **588**) + the
`MilestonesTests` re-pin (M14→M15 in-progress) all green. `dotnet exec
tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll` —
**1000 / 1000** (unchanged — M14 is Web-composition + the two
Core-side pins already landed in U01/U02/U05; no Core test added or
removed at the close). **Docker cleanup note:** the Core run's
Testcontainers self-reaped at exit; if the Core run is interrupted
(`dotnet exec` killed mid-run), `docker container prune` clears any
orphaned `postgres:18` test containers before the next run.

## Summary

**Role:** U07 is the M14 **close** unit — the feedback loop (D7) and the
docs-parity flip (D8) in one unit, after U00 (design + ADR 0115) → U01
(field + index + reverse read seam) → U02 (the `set-event` write lane) →
U03 (both-direction display links) → U04 (the picker) → U05 (the
`TodoIcsWriter` `VTODO` emitter) → U06 (the two `.ics` routes + the
`kw-l` affordances). U07 ships **only** tests + docs + the plans archive
move (C-M14·7 — no production code changes in a close unit); if an
acceptance test had exposed a real defect, the unit would stop and report,
not silently fix.

**Docs flip (C-M14·7):** M14 → `StatusDone`, M15 → `StatusNext`, across
`Milestones.cs` / `MilestonesTests.cs` (re-pinned to M15 as the single
in-progress; the exact-order M0…M18 pin unchanged) / `README.md` (Status +
Roadmap) / `docs/STATUS.md` (the live-milestone line) /
`docs/ARCHITECTURE.md` (the `Events/` + `Projects/` tree rows gain
`M14 ✓ (ADR 0115)`; the value-chain table is status-free; no new bounded
context or DocTypes surface, so the §2 "now live" + §3 feature-modules
lines are unchanged).

**Shipped (the seven units U00–U06):**

| Unit | Shipped |
|---|---|
| U00 | the design doc (primary authority) + ADR 0115 (the decision record) + the three acceptance test names + the D7/D8 close shape locked |
| U01 | the additive `TodoItem.EventId?` (a feed filter, never a gate — C-M14·1) + the `(EventId)` index on `M5DocTypes` (ADR 0004 §B.1, zero migration) + the `IProjectService.ListTodosForEventAsync` reverse read seam (the `ListTodosAsync` shape, the `ListBoardsForTodoAsync` precedent, one `CanSeeAsync(Read)` over the existing adapter, one `AccessAudit` row) + 4 pins |
| U02 | the `IProjectService.SetTodoEventAsync(todoItemId, actorId, actorRoles, eventId, ct)` set-event write lane (standing creator ∪ assignee ∪ GlobalAdmin — C-M5·6; a non-visible `eventId` refused 404; `null` clears; one atomic `todo.set_event` audit row) + 3 pins |
| U03 | both-direction display links (the to-do → event chip over the frozen `GetAsync` + the event → to-do "linked to-dos" section over U01's seam) + the `TodoDetailViewModel.EventId/EventTitle/EventLinkPath` + the `EventDetailViewModel.LinkedTodos` (`LinkedTodoRow` shape) + the `todo.event_link` / `events.linked_todos` `kw-l` keys × en/de/fr/da — dangling-safe (C-M14·2) |
| U04 | the event picker (seeded by the frozen `IEventService.ListMineAsync`, ADR 0065) + the `TodoSetEvent` `POST /projects/todos/{id}/set-event` action (CSRF) + the `todo.set_event.label` / `todo.set_event.pick` `kw-l` keys |
| U05 | the BCL-only pure `TodoIcsWriter.BuildTodos` `VTODO` emitter (the ADR 0112 `IcsWriter` discipline; a closed `VTODO` subset; `UID kw-todo-{Id}@kumunita`; no `ORGANIZER` / `CREATED-BY` / `ATTENDEE` / `RRULE` / `X-…` — the ADR 0028 posture, C-M14·5; a valid-empty calendar) + 4 pins |
| U06 | the two `[Authorize]` routes — `GET /projects/todos/{id}.ics` (the 404-not-403 split; a degenerate `VTODO` for an undated to-do) + `GET /projects/todos.ics` (the caller's visible dated set, always 200) — `text/calendar; charset=utf-8` + `Content-Disposition: attachment` + `Cache-Control: no-store` + `nosniff`; no subscription token (rides the cookie) + the `projects.todos.ics.download` / `projects.todos.ics.feed` `kw-l` keys |
| U07 | this close — the three acceptance tests + the docs flip (D8) + the plans archive move |

**Full-suite state at the close (post-flip):** `Kumunita.Web.Tests` —
588 / 588 (585 U06 baseline + 3 `M14InterlockTests`); `Kumunita.Core.Tests`
— 1000 / 1000 (unchanged). `dotnet build Kumunita.slnx -c Debug` — clean.

**The pinned tests — all green, in the tree:**
- `Kumunita.Core.Tests.ListTodosForEventTests` (U01, 4 pins) — the reverse read seam (the `(EventId)` index, the `CanSeeAsync(Read)` gate, the `TodoPage` shape)
- `Kumunita.Core.Tests.ProjectServiceTests` — the `SetTodoEventAsync` set-event lane (U02, 3 pins)
- `Kumunita.Core.Tests.TodoIcsWriterTests` (U05, 4 pins) — the `VTODO` emitter
- `Kumunita.Web.Tests.M14InterlockTests` (U07, 3 acceptance tests) — the closed loop, the write/read handoff without a new authorization surface, the dangling-safe chip omission

**Invariants as landed (C-M14·1–7):**
- **C-M14·1** — `EventId?` is a feed filter / association, **never a gate**
  (the `ProjectId` shape; a to-do's visibility is unchanged by its event
  linkage).
- **C-M14·2** — both display directions are **access-scoped + dangling-safe**
  (omit, never 404/403, no title/id leak) — pinned by
  `Interlock_PartVsWhole_UnreadableEventOmitsTheChip` (4 cases).
- **C-M14·3** — the set-event lane reuses the frozen `Read` decision over
  the event (the `SetTodoProjectAsync` target-visibility rule).
- **C-M14·4** — **no new authorization surface** (the `AccessAction`
  closed-set pin — `Read` + `Moderate`; no new `AccessVia` / `Decide()`
  branch) — pinned by `Interlock_Handoff_WriteAndReadComposeWithoutNewAuthorizationSurface`.
- **C-M14·5** — the `VTODO` emitter carries no identity / audience / grant
  material (the never-emitted property-name sweep).
- **C-M14·6** — **no `RRULE` / recurrence anywhere in M14** (M18's home).
- **C-M14·7** — docs parity in one unit (this close) + no production code
  changed in a close unit.

**The milestone is complete.** M14 — integration of Events and Projects —
is **done** (ADR 0115); M15 — translation bulk (import/export, review &
extend the platform's translations as a batch) — is **next**; M16
(inventory) / M17 (bookmarks) / M18 (recurring events) are planned.
  `page.Items.Where(t => t.DueAt is not null || t.StartAt is not null)
  .ToList()` (a *display* filter, never a gate / new seam — C-M14·4 /
  C-M14·7) → `TodoIcsWriter.BuildTodos(dated, DateTimeOffset.UtcNow)` (an
  empty visible/dated set ⇒ the valid **empty** `VCALENDAR` — the
  RFC-legal no-crash path, C-M14·5/6) → the serve shape
  (`filename="kumunita-todos.ics"`, the same three other headers).
  **Anonymous ⇒ the standard sign-in challenge** (the class-level
  `[Authorize]` default — F3; no anonymous iCal surface, the ADR 0112 D2
  pin).

**(b) The route-resolution outcome (the M12 gotcha pin).** Verified
against the live tree + ASP.NET Core's route ranking: the controller has
**no** plain `GET /projects/todos/{id}.ics`-competing parameterized GET —
the nearest GET siblings are `/projects/todos/{id}` (TodoDetail),
`/projects/todos/{id}/edit`, and the two new literal/parameter `.ics`
routes. ASP.NET Core ranks a route by (1) segment count, then (2) **literal
segments cost 0, parameter tokens cost 1** — the literal `/projects/
todos.ics` scores lower cost than `/projects/todos/{id}` **and** than
`/projects/todos/{id}.ics` (one token vs. zero literals in the last
segment), so **`GET /projects/todos.ics` resolves to the feed action
(`TodosIcsFeed`), not to `TodoDetail` with `id = "todos.ics"`** — exactly
the ADR 0112 §routes precedent the `EventController`
`/events.ics` / `/events/{id}` pair already demonstrates in this tree. The
two `.ics` routes themselves are mutually unambiguous (one literal, one
token, different shapes). **Recorded as the exact resolution: literal
wins; the feed owns `/projects/todos.ics`.**

**(c) The two U06 `kw-l` keys × en/de/fr/da** (the design-doc §kw-l locked
en values; added in the M14 U03/U04 block in
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs`, which holds all
four language dictionaries — the closed-key registry **is** the parity
surface; there are no separate `*.json` files, the U03/U04 note stands):

| Key | en | de | fr | da |
|---|---|---|---|---|
| `projects.todos.ics.download` | Add to calendar | Zum Kalender hinzufügen | Ajouter à l'agenda | Tilføj til kalender |
| `projects.todos.ics.feed` | Calendar feed (iCal) | Kalender-Feed (iCal) | Flux de calendrier (iCal) | Kalenderfeed (iCal) |

The de / fr / da values are the **locked** house translations of the M12
sibling keys (`events.ics.download` / `events.ics.feed` — the §kw-l pin
says "the ADR 0112 `events.ics.*` shape carried to the to-do surface"),
reused verbatim since the English strings are identical.
**Wiring (plain `<a>`, no new JS — the ADR 0031 discipline):**
- `projects.todos.ics.download` — `Views/Projects/TodoDetail.cshtml`, a
  `btn btn-outline-secondary btn-sm` anchor to
  `/projects/todos/{Model.Todo.Id}.ics`, placed directly **below** the U03
  event-link chip + U04 picker block (the "join the chip/picker block"
  rule). Gated on nothing — the detail page only renders for a to-do the
  caller already passed the `Read` decision on, so the file lane's own
  404-not-403 split (C-M14·2) is the guard.
- `projects.todos.ics.feed` — `Views/Projects/TodosIndex.cshtml`, the
  index header's `head-row` gains the `Event/Index.cshtml` M12
  `events.ics.feed` idiom verbatim: the "New to-do" button is now wrapped
  in a `d-flex gap-2 align-items-center flex-wrap` row (the Event Index
  head-row idiom — flex-wrap lets it wrap below at 360 px, M10 re-check)
  with the feed anchor first.

**(d) Drift vs. the register** — two cosmetic notes (no decision change):
1. The register's U06 entry-reads named the views as
   `Views/Projects/Todo/Detail.cshtml` + `Views/Projects/Todo/Index.cshtml`
   — the **actual** live paths are `Views/Projects/TodoDetail.cshtml` +
   `Views/Projects/TodosIndex.cshtml` (the same cosmetic path mismatch
   U03 / U04 recorded; the views were edited at their real location).
2. The register's U06 prose for lane 2 shows the call as
   `ListTodosAsync(null, null, actorId, 0, ct)` — the **actual** frozen
   seam signature (unchanged by U01–U05) is
   `ListTodosAsync(string? componentId, string? assigneeId, string actorId,
   int page, bool unassignedOnly = false, string? projectId = null, bool
   blockedOnly = false, CancellationToken ct = default)`, so the action
   passes the two default `bool`/`string?` params through by name
   (`unassignedOnly` omitted at its `false` default, `projectId` /
   `blockedOnly` omitted at their defaults) — the in-app feed's own call
   shape, exactly "the caller's visible set" (C-M14·7). Both match the
   design doc §routes verbatim.

**(e) Build + test.** `dotnet build Kumunita.slnx -c Debug` succeeded
clean (all 4 projects, 0 warnings). `Kumunita.Web.Tests  Total: 585,
Errors: 0, Failed: 0, Skipped: 0` (the `KnownTranslationKeys_ParityTests`
+ `KwLRegistryConsistencyTests` pins the two new keys satisfy, plus the
frozen `ProjectsControllerTests` / `EventControllerTests` seam pins, all
survive the two new actions + the two view edits — **no existing
test-construction site required a change**: the two new actions use only
the existing `IProjectService` seam + the U05 pure emitter, and the
`ProjectsController` constructor is **untouched** — neither new action
needs a new dependency). `Kumunita.Core.Tests  Total: 1000, Errors: 0,
Failed: 0, Skipped: 0, Time: 127.2s` (the kw-l registry's Core-side parity
pins over the two new keys + all 996 prior pins survive the additive
registry rows). **Docker cleanup note:** the Web run's 5 Testcontainers
were reaped by the xunit.v3 runner at exit ("Delete Docker container …"
observed for all 5); the Core run's containers were likewise reaped
("Delete Docker container …" observed through to the summary line);
`docker container ls` after both runs shows only the **app's** long-lived
containers (`kumunita_app` / `kumunita_db` / `kumunita_mailpit` — the
dev-compose stack) — **no** orphaned `postgres:18` test containers, so no
`docker container prune` was needed.
