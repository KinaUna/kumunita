# ADR 0115 — Integration of Events and Projects (M14)

Status: Accepted
Date: 2026-09-29
Extends the **frozen `IEventService` read seams** (`GetAsync` /
`ListUpcomingAsync` / `ListMineAsync`, the "a read is not a decision"
posture, ADR 0054), the **frozen `IProjectService` read + association
lanes** (`ListTodosAsync` / `GetTodoAsync` / `SetTodoProjectAsync` /
`ListBoardsForTodoAsync`, ADR 0067 / ADR 0086 / ADR 0087), the
**`ProjectId` association field on `TodoItem`** (a feed filter, never a
gate — C-M3·2, ADR 0086 D4), the **dangling-safe display idiom**
(`ProjectLink` / `BlockerChip`, ADR 0086 D9 / ADR 0087), the **optional
to-do dates `StartAt` / `DueAt`** (ADR 0079), the **`IcsWriter` BCL-only
closed-subset emitter + the two iCal routes + the serve idiom** (ADR 0112
/ ADR 0034 / ADR 0108), the **`kw-l` closed-key registry + en/de/fr/da
parity pins** (ADR 0015), the **WY·3 closed-subset + tsc-only discipline**
(ADR 0031), the **attachment / file-serve idiom** (ADR 0034 / ADR 0108),
the **`M5DocTypes` additive-index surface** (ADR 0004 §B.1), the
**module-boundary contract** (a bounded context per concern, ADR 0006),
the **no-standing-to-carry-a-secret posture** (ADR 0028), the
**author-only edit lanes** (ADR 0014 / ADR 0016 / ADR 0017), and the
**M0…M18 milestone-order pin** (ADR 0107 / the C-M11·8 precedent). This ADR
makes the **two coordination surfaces interlock** (the ARCHITECTURE.md M14
value-chain row, verbatim): a to-do can now be **associated with an event**
(the data interlock — D1–D3), and the to-dos' **optional dates become a
standard iCal `VTODO` surface** (the calendar interlock — D4) — a
read-only projection of already-authorized content, with **zero new
authorization surface, one additive field + index (zero migration), and no
recurrence of any kind** (C-M14·6 / D6 — that is M18's home).

## Context

M4 shipped events (ADR 0054); EV-CAL / EV-DWM / EV-NW / EV-PAST (ADR 0063 /
0064 / 0081 / 0109) shipped the in-app calendar; M12 (ADR 0112) gave the
residents' **calendar app** the events. M5 shipped projects (ADR 0067: to-
dos + Kanban boards); the PL lane (ADR 0086) added the `ProjectId`
association; ADR 0079 added the optional to-do dates `StartAt` / `DueAt`;
ADR 0087 the blocker chip. The README principles are explicit that the
interlock **is the product**: "**Integration over features.** … Kumunita's
value is the *linkage* that turns those parts into a whole, not the count
of parts."

Today the two surfaces **never point at each other** (grep-confirmed at
U00 against the live tree): there is **no `EventId` on `TodoItem`**, **no
to-dos-in-a-calendar surface**, and **no cross-surface display link**
anywhere. A resident preparing for the cleanup day cannot link the prep
to-dos to the event, and the to-dos — which already carry optional dates
(ADR 0079) — do not appear in **any** calendar even though the events do
(ADR 0112).

The constraint that shapes the decision is the same one that shaped M12:
**a file that travels must not carry a decision** (the ADR 0028 posture;
ADR 0112 C-M12·2), and a **link must never be a gate** (C-M3·2 / C-PL·3,
the `ProjectId` precedent). Both surfaces therefore stay **frozen** — M14
only composes the *existing* `Read` decisions (never inventing a new
authorization surface, C-M14·4) and surfaces *links between content the
caller already holds* (C-M14·2). The recurrence question is **out of
scope for M14** — `Milestones.cs` (the source of truth) assigns recurring
events to **M18** — and ADR 0112's "recurring events (`RRULE` — **M14** is
the natural home)" forward-ref is a **stale** ref, written before the
roadmap settled M18 (see Consequences).

## Decision

**D1 — One additive `EventId?` field on `TodoItem` (a field, not a link
doc).** `public string? EventId { get; set; }` joins `TodoItem` beside
`ProjectId`, carrying the same locked doc-comment rule — **a feed filter /
association, never a gate** (C-M14·1 — C-M3·2 / C-PL·3 carried to the event
link; the ADR 0086 D4 `ProjectId` shape). It is a **1:1 association** (a
to-do is *for* one event — the `ProjectId` "a to-do is *in* one project"
precedent), **not** on `CreateTodoRequest` / `UpdateTodoRequest` (D3 — the
`ProjectId` precedent: association is a dedicated lane, not the main form).
One additive `(EventId)` single-field index on the `TodoItem` block in
`M5DocTypes` joins the `ProjectId` / `BlockedByTodoId` indexes in the same
**unnamed** form (the computed-index naming constraint recorded in the
file's own note — the auto-derived name stays under Postgres' 64-char
`NAMEDATALEN` cap). Additive-only, delta-detected, idempotent at boot,
**zero migration for existing rows** (ADR 0004 §B.1 — existing rows read
`EventId = null`).

**D2 — Both display directions, as Web-layer composition over the frozen
read seams.** **No** `Kumunita.Core.Events` → `Kumunita.Core.Projects`
dependency (the ADR 0006 "a bounded context per concern" rule; the
composition point for event → to-dos is the `EventController`, which
already references both services). The **to-do → event** chip resolves
through the **frozen** `IEventService.GetAsync`; the **event → linked to-
dos** section resolves through the **new** additive reverse read seam
`IProjectService.ListTodosForEventAsync(string eventId, string actorId,
int page, CancellationToken ct = default)` — the `ListTodosAsync` shape
filtered to one event (the `ListBoardsForTodoAsync` "list X for a Y"
reverse-read precedent): candidate `!IsDeleted && EventId == eventId` (a
**filter, never a gate** — C-M14·1), survivors `CanSeeAsync(Read)`-filtered
over the **existing** `TodoItemToAuditableResource` (C-M14·4 — no new
adapter), `Created` descending, `HasMore` per ADR 0090, one aggregate
`AccessAudit` row `TargetKind = "todo"`. Both directions are
**access-scoped + dangling-safe** (C-M14·2 — the `ProjectLink` /
`BlockerChip` idiom, ADR 0086 D9 / ADR 0087): an unreadable / absent /
soft-deleted target is **omitted entirely** (a `null` chip field), never a
404/403 for the source surface, and its title / id are **not leaked**.
`TodoDetailViewModel` gains `EventId?` / `EventTitle?` / `EventLinkPath?`
(all `null` by default); `EventDetailViewModel` gains
`IReadOnlyList<LinkedTodoRow>? LinkedTodos = null` (the locked
`LinkedTodoRow(TodoId, Title, Status?, DueAt?, LinkPath)` row — the
`TodoRow` is too heavy for a chip; drift-guard entry 5).

**D3 — The dedicated `set-event` write lane.**
`IProjectService.SetTodoEventAsync(string todoItemId, string actorId,
IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct =
default)` — the `SetTodoProjectAsync` shape verbatim (the `todoItemId`
parameter name matching the sibling; the register's `todoId` prose loses
— the live signature wins, drift-guard entry 3). Standing: **creator ∪
assignee ∪ GlobalAdmin over the to-do** (C-M5·6 — the ADR 0014/0016/0017
matrix, re-checked **server-side**; C-M14·3; the Web `[Authorize]` is a
convenience pre-gate only). The to-do must be `!IsDeleted` (404); a
non-existent / soft-deleted / non-readable `eventId` is **refused 404**
(**you can only link to an event you can see** — the frozen
`IAuthorizationService.CanAsync(Read)` path over the event, C-M14·3/4 —
the `SetTodoProjectAsync` target-visibility rule; drift-guard entry 1);
`null` **clears** the link; `AuthorId` / `Created` untouched, `Modified`
stamped; **one `AccessAudit` row** (`todo.set_event`, `TargetKind =
"todo"`) commits **atomically** with the write (C3). The Web lane is
`POST /projects/todos/{id}/set-event` on `ProjectsController` (the
`POST /projects/todos/{id}/set-project` shape) —
`[ValidateAntiForgeryToken]`, one nullable `eventId` form field (blank /
`null` posts as the clear affordance); the picker is seeded by the **frozen**
`IEventService.ListMineAsync` (the actor's own events — authored ∪ RSVPed —
the ADR 0065 posture; capped at 25 for the `<select>` — a display cap, not
a gate); redirect back to the to-do detail on success.

**D4 — The `VTODO` calendar surface.** The to-dos' existing optional dates
(ADR 0079) become a standard iCal `VTODO` surface — a **BCL-only pure
`TodoIcsWriter`** (the `IcsWriter` discipline, ADR 0112): one public
method, `public static string BuildTodos(IReadOnlyList<TodoItem> todos,
DateTimeOffset nowUtc)` — no store, no `IDocumentSession`, no
`IAuthorizationService`, no clock (the caller passes `nowUtc`), no
reflection, no library (C-M14·5). `todos` are the **already-authorized**
rows the caller resolved (the frozen `IProjectService` read seam's output);
the authorization was applied **before** the emitter runs — the decision is
never re-derived and never leaked (C-M14·5). The **closed, pinned
`VTODO` subset** (the §vtodo pin in the design doc): the `VCALENDAR`
envelope (the ADR 0112 envelope verbatim — `VERSION:2.0`,
`PRODID:-//Kumunita//community calendar//EN`, `CALSCALE:GREGORIAN`,
`METHOD:PUBLISH`) + one `VTODO` per **dated** to-do (`DueAt != null ||
StartAt != null`; neither ⇒ **skipped** — the "only include what's
meaningful" `IcsWriter` idiom) with the pinned order `UID` / `DTSTAMP` /
`DUE` (cond.) / `DTSTART` (cond.) / `SUMMARY` / `DESCRIPTION` (cond.) /
`STATUS:CANCELLED` (cond.). **`UID`** = `kw-todo-{todo.Id}@kumunita`
(**stable** — the calendar app's update-vs-add key; the `kw-eve-…`
convention with the `todo` tag, C-M14·6). **`DTSTAMP`** = the caller-
passed `nowUtc` (**not** `TodoItem.Modified` — the ADR 0112 D1 shape).
**`SUMMARY`** = `Title`; **`DESCRIPTION`** = `Body` as **Markdown source**
(not rendered HTML — the ADR 0112 D4 chosen-behavior pin). **Never
emitted** (the C-M14·5 / C-M14·6 "never" list — the ADR 0028 posture + the
ADR 0112 D1 "never" list carried to the to-do surface): `ORGANIZER` /
`CREATED-BY` / `ATTENDEE` (no author / assignee identity), `RRULE` /
`RECURRENCE-ID` (C-M14·6 / D6 — no recurrence in M14), `Audience` / grant /
membership internals (the decision is already *applied* — C-M14·5),
`AuthorId` / `AssigneeId` / `ComponentId` / `ProjectId` / `EventId` /
`ParentId` / `BlockedByTodoId` / `Status` / `LanguageCode` / `TagIds` /
`ImageIds` / `AttachmentIds` / `Created` (app-internal state — none of it),
and **any** `X-…` vendor property. CRLF endings; ≤ 75-octet fold (UTF-8-
safe boundary + leading-space continuation); escaping (`\` → `\\` first,
`;` → `\;`, `,` → `\,`, CR/LF → `\n`) — the ADR 0112 / `IcsWriter` format
pin verbatim.

**D4 (routes) — Two `[Authorize]` routes on the existing `ProjectsController`,
both served `text/calendar; charset=utf-8`.** `GET
/projects/todos/{id}.ics` (lane 1, one `VTODO`) + `GET
/projects/todos.ics` (lane 2, the caller's visible **dated** set). Both set
`Content-Disposition: attachment; filename="…"`
(`kumunita-todo-{id}.ics` / `kumunita-todos.ics`), `Cache-Control:
no-store`, `X-Content-Type-Options: nosniff` — the ADR 0034 / ADR 0108
serve idiom, copied from the M12 `EventController.EventIcs` /
`CalendarFeed`. Lane 1 inherits the frozen `GetTodoAsync`'s
**404-not-403** split (a denied / absent to-do neither downloads nor 403s
into existence, C-M14·2 / C3); an undated to-do still yields a valid single
`VTODO` with only `UID` / `DTSTAMP` / `SUMMARY` (+`DESCRIPTION`) — the
RFC-legal degenerate form; the feed lane's skip rule applies to the *feed*,
not an explicit per-item request (drift-guard entry 4). Lane 2 is always
`200` (an empty visible / dated set is a valid empty calendar — the RFC-
legal no-crash path, C-M14·5/6); the "filter to dated to-dos" is a **Web-
layer display filter** (`DueAt != null || StartAt != null`), not a new
seam (C-M14·7). **No subscription token** — the feed rides the cookie,
re-authorized every fetch (the M6 link-lane posture, ADR 0085 / ADR 0095,
carried to the to-do surface; F3 no anonymous iCal surface). **Route
resolution (the M12 known gotcha, carried):** ASP.NET Core ranks the
**literal** `"/projects/todos.ics"` route above the parameterized
`"/projects/todos/{id}"`, so `GET /projects/todos.ics` resolves to the
feed, not to a detail with `id = "todos.ics"` (the ADR 0112 / M12 §routes
precedent).

**D4 (affordances) — Two quiet `kw-l` affordances × en/de/fr/da.** The to-do
detail gains one "Add to calendar" link (the `projects.todos.ics.download`
key); the to-do index gains one "Calendar feed (iCal)" link (the
`projects.todos.ics.feed` key) — both plain `<a>` links (**no new JS**,
the ADR 0031 tsc-only discipline holds). The chip / picker / section labels
add `todo.event_link` / `todo.set_event.label` / `todo.set_event.pick` /
`events.linked_todos`. All six keys join the closed `kw-l` registry in
**all four** languages (the `KnownTranslationKeys_ParityTests` enforces the
4-language set + non-empty values; the ADR 0015 / ADR 0052 warm-boot
backfill seeds them idempotently).

**D5 — No new authorization surface.** (C-M14·4.) M14 adds **no**
`AccessAction`, **no** `AccessVia`, **no** `IAuthorizationService.Decide()`
branch, **no** `Audience` change, **no** `IAuditableResource` adapter. All
read lanes route through the **existing** `TodoItemToAuditableResource` /
`EventToAuditableResource` adapters and the **frozen**
`IAuthorizationService.CanAsync` / `CanSeeAsync(Read)` path. The `set-
event` standing **reuses** C-M5·6 (creator ∪ assignee ∪ GlobalAdmin — the
`SetTodoProjectAsync` matrix); it does not invent a new one. The read lanes
already commit their own `AccessAudit` rows (the frozen ADR 0006 / C3
lanes); the `set-event` lane adds exactly one (`todo.set_event`); M14 adds
no other audit verb.

**D6 — No `RRULE` / recurrence anywhere in M14.** (C-M14·6.) The `Event`
doc gains **no** recurrence field; the `VTODO` emitter emits a single
`VTODO` per to-do. **`RRULE` is M18's home** — `Milestones.cs` (the source
of truth) assigns "M18 — Recurring events — repeating events over the M4
events surface" to **M18**. ADR 0112's "recurring events (`RRULE` —
**M14** is the natural home)" forward-ref and the `IcsWriter.cs` doc-
comment "M14's home" ref are **stale forward-refs**, written before the
roadmap settled M18 — they are **superseded** by this ADR + its
Consequences (see below).

**D7 — The pinned test names are the feedback loop.** The **Core** pure
tests (no Testcontainers — the `TodoIcsWriter` is pure over POCOs) pin:
the closed `VTODO` subset, the `DUE`-only / `DTSTART`-only / neither
(skipped) conditionality, the fold + escape, the `UID` stability, the
`STATUS:CANCELLED` capability, the empty-calendar-valid pin, and the never-
emitted list (U05, 4). The **Testcontainers** pins (over the existing
`PostgresFixture` `IProjectService` shape) pin the `ListTodosForEventAsync`
candidate-filter + `CanSeeAsync(Read)` gate + `HasMore` (U01, 4) and the
`SetTodoEventAsync` standing + `eventId`-visibility refusal + null-clear +
audit row (U02, 3). The **Web** pins (NSubstitute, no Postgres) pin the
display links' dangling-safe omission (U03), the `set-event` action's CSRF
+ binding + redirect (U04), and the two `.ics` routes' serve shape +
404-not-403 split + always-200 feed + the kw-l parity (U06 — the parity
pins move; 0 new route pins). The **three acceptance tests** (U07):
**Closed loop** / **Handoff** / **Part vs. whole** (§gate).

**D8 — Docs flip in one unit (U07 close).** `Milestones.cs` (M14 →
`StatusDone`, M15 → `StatusNext`), `README.md` Roadmap, `docs/STATUS.md`,
`docs/ARCHITECTURE.md` (the M14 value-chain row → done), and
`MilestonesTests` (re-pinned to **M15** as the single in-progress) all
move in one unit (the C-M11·8 precedent verbatim) — the exact-order pin
M0…M18 unchanged, the single-in-progress now M15. This ADR is the
milestone's decision record (U00); the unit series U01–U07 executes it.

## Consequences

- **Positive:** the two coordination surfaces **interlock** — a resident
  preparing for an event links the prep to-dos **once**, and **both
  surfaces now carry each other**: the to-do detail shows the event chip,
  the event detail shows the linked to-dos, and the dated to-dos land in
  the **same calendar app** the events already land in (ADR 0112) — the
  "Integration over features" principle made concrete. The file is a
  **portable, standard** read-only projection (RFC 5545, no vendor
  extensions, no warnings); the boundary is the cookie (F3) +
  `Cache-Control: no-store` (no cross-caller leak from a shared phone); the
  change propagates **out** live (a grant added/removed or a to-do edited
  shows on the **next** fetch — strong consistency carried out of the
  platform); the cost is bounded **by the pin** (one field + one index,
  two additive seams, a pure emitter, two routes — C-M14·4/6/7).
- **Neutral / cost:** one additive nullable field + one index on `TodoItem`
  (zero migration — ADR 0004 §B.1 delta-detect); two more `IProjectService`
  seams (the frozen surface is extended, not re-scoped — the ADR 0086/0087
  frozen-surface rule honoured); the `.ics`-literal-in-`{id}` route
  resolution is the one known gotcha (the design doc §routes pin is the
  source of truth; the U06 handoff records the exact resolution); a
  `DESCRIPTION` that references `![img](/content-image/{id})` /
  `[file](/attachment/{id})` renders as **plain text** in a calendar app
  (honest + portable, but the image does not render in the calendar — the
  **chosen** behavior, D4); the to-do detail gains a fourth cross-
  reference (the FACES legibility trade — one labeled chip per cross-
  reference, empty-state when absent).
- **Not chosen:** a **many-to-many `EventTodoLink` doc** (D1's 1:1 field is
  simpler + matches the `ProjectId` "a to-do is *in* one project"
  precedent — the link doc is the alternative only if the field were
  vetoed; it was not); a **`Kumunita.Core.Events` → `Kumunita.Core.Projects`
  dependency** (the ADR 0006 "a bounded context per concern" rule — the
  composition point is the `EventController`, which already references
  both services); a **new authorization surface** (C-M14·4 — the frozen
  `Read` decisions already cover both surfaces; reusing C-M5·6 for
  `set-event` is the `SetTodoProjectAsync` precedent); a **subscription
  token** (a standing credential the resident would share — the M6
  link-lane posture, ADR 0085 / ADR 0095, wins); an **`ORGANIZER` /
  `ATTENDEE`** property (it would leak the author / assignee identity into
  a file that travels — the ADR 0028 "no standing to carry a secret"
  posture wins); a **`RRULE` / recurrence field** (C-M14·6 — **M18's**
  home, the source of truth is `Milestones.cs`).
- **Superseded (stale forward-refs, written before the roadmap settled
  M18):** ADR 0112's Consequences "Deferred … **recurring events**
  (`RRULE` — **M14** is the natural home)" and the `IcsWriter.cs` doc-
  comment "M14's home" ref. Both are superseded by this ADR: **`RRULE` /
  recurrence is M18's home** (D6), **not** M14's — M14's scope is the
  interlock (the `EventId` field + the display links + the `VTODO`
  surface), not recurrence.
- **Deferred to their own lanes (each a named follow-on, own ADR / own
  milestone):** **recurring events / `RRULE`** (**M18** — the source of
  truth is `Milestones.cs`; ADR 0112's deferral stands, only its "M14 is
  the natural home" attribution is superseded, above); **`RRULE` on the
  `VTODO` surface** (a to-do's recurrence rides M18's recurrence concept,
  if that ever extends past events — own ADR); **ICS import** (round-
  tripping calendar files **into** Kumunita — a write lane with its own
  schema / standing questions; ADR 0112's "own lane" deferral unchanged);
  **per-component calendar feeds** (a `?component=` scoping of the `VTODO`
  feed — a scope generalization; the M14 feed is the actor's visible set,
  the ADR 0112 feed shape).

## Affected files

- `src/Kumunita.Core/Projects/TodoItem.cs` — the one additive `EventId?`
  field (D1).
- `src/Kumunita.Core/M5DocTypes.cs` — the one additive `(EventId)` index
  (D1).
- `src/Kumunita.Core/Projects/IProjectService.cs` +
  `src/Kumunita.Core/Projects/ProjectService.cs` — the two additive seams
  (`ListTodosForEventAsync` D2, `SetTodoEventAsync` D3) + their
  implementations.
- `src/Kumunita.Core/Events/TodoIcsWriter.cs` — new (the pure `VTODO`
  emitter; the only new Core type; D4).
- `src/Kumunita.Web/Models/ProjectTodoViewModels.cs` —
  `TodoDetailViewModel` additive event-link fields + the
  `TodoEventPicker` picker model (D2/D3).
- `src/Kumunita.Web/Models/EventEditorModel.cs` —
  `EventDetailViewModel` additive `LinkedTodos` + the `LinkedTodoRow`
  record (D2).
- `src/Kumunita.Web/Controllers/ProjectsController.cs` — the to-do
  detail's event-link resolve (D2); the `POST /projects/todos/{id}/set-
  event` action + picker seed (D3); the two `.ics` actions (D4).
- `src/Kumunita.Web/Controllers/EventController.cs` — the event detail's
  `LinkedTodos` population (D2).
- `src/Kumunita.Web/Views/Projects/Todo/Detail.cshtml` — the "linked
  event" chip (D2) + the event picker `<form>` (D3) + the "Add to
  calendar" `<a>` (D4).
- `src/Kumunita.Web/Views/Projects/Todo/Index.cshtml` — the "Calendar
  feed (iCal)" `<a>` (D4).
- `src/Kumunita.Web/Views/Events/Detail.cshtml` — the "linked to-dos"
  section (D2).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` + the four
  locale files — the six `kw-l` keys in en/de/fr/da (D4 affordances).
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs` —
  the M14 → `StatusDone` / M15 → `StatusNext` flip (D8), in one unit.
- **New tests** (the D7 pins): `tests/Kumunita.Core.Tests/` — the
  `ListTodosForEventAsync` pins (U01, 4), the `SetTodoEventAsync` pins
  (U02, 3), the `TodoIcsWriter` pure pins (U05, 4), and the three
  acceptance tests + the cross-surface read-shape pins (U07); the Web
  parity pins (U03 / U04 / U06).
