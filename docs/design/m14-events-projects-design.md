# M14 — Integration of Events and Projects — design

> **Milestone M14.** The **two coordination surfaces interlock**: the Events
> (M4) and Projects (M5) surfaces — which a neighborhood uses as one thing —
> now **link**, in two faces. **The data interlock (D1–D3):** a to-do can be
> **associated with an event** — one additive `EventId?` field on
> `TodoItem` (the `ProjectId` shape — an association + feed-filter,
> **never a gate**, C-M14·1), one **reverse read seam**
> `ListTodosForEventAsync` (the "the to-dos linked to this event" read), one
> dedicated **write lane** `SetTodoEventAsync` (creator ∪ assignee ∪
> GlobalAdmin, the `set-project` shape), and **both-direction display
> links** (to-do → event chip over the frozen `IEventService.GetAsync`;
> event → linked to-dos over the new seam), each **access-scoped +
> dangling-safe** (the `ProjectLink`/`BlockerChip` idiom, C-M14·2). The
> composition is **Web-layer** (D2) — **no** `Kumunita.Core.Events` →
> `Kumunita.Core.Projects` dependency. **The calendar interlock (D4):** the
> to-dos' existing optional dates (`DueAt` / `StartAt`, ADR 0079) become a
> **standard iCal `VTODO` surface** — a **BCL-only pure `TodoIcsWriter`**
> (the `IcsWriter` discipline, ADR 0112) + two `[Authorize]` routes
> (`GET /projects/todos.ics` the caller's visible dated to-dos, always 200;
> `GET /projects/todos/{id}.ics` one `VTODO`, the 404-not-403 split) + two
> quiet `kw-l` affordances.
>
> **What M14 is NOT (the scope pins):** **no `RRULE` / recurrence of any
> kind anywhere in M14** (C-M14·6 / D6 — that is **M18's** home, per
> `Milestones.cs`, the source of truth); **no new authorization surface**
> (C-M14·4 — no new `AccessAction` / `AccessVia` / `Decide()` branch); the
> `EventId` link is **a filter/association, never a gate** (C-M14·1); the
> `VTODO` file **never carries a decision** (C-M14·5 — the ADR 0028 posture
> applied to the file form, the C-M12·2 pin carried to the to-do surface);
> and **additive-only, frozen seams untouched** (C-M14·7).
>
> **Status.** **LOCKED.** The decisions D1–D8 are locked in **ADR 0115
> (Accepted, 2026-09-29)**. The `[PROPOSED]` set in the register
> `plan-m14-events-projects.md` was **locked as written — no veto was
> recorded**; where the source contradicted the prose, the refinements are
> recorded in §drift-guard (entries 1–5), each resolved in favor of this
> doc (the source of the shape is ground truth; the prose follows it).
>
> **The one thing every unit must respect:** the **linkage degrades
> gracefully and never leaks** (C-M14·2) — an unreadable / absent /
> soft-deleted target is **omitted entirely**, never a 404/403 for the
> source surface, and its title / id are **not leaked**; and the **`VTODO`
> file is a read-only projection of already-authorized, dated to-dos**
> (C-M14·5) with a **closed property set** (C-M14·6 — no `RRULE`, no
> `ORGANIZER` / `ATTENDEE`, no `X-…`).

## Context

M4 shipped events (ADR 0054; the in-app calendar EV-CAL / EV-DWM / EV-NW /
EV-PAST, ADR 0063 / 0064 / 0081 / 0109; M12 iCal export, ADR 0112). M5
shipped projects (ADR 0067: to-dos + Kanban boards; the PL goals & projects
lane ADR 0086 added the `ProjectId` association; ADR 0079 added the
optional to-do dates `StartAt` / `DueAt`; ADR 0087 the blocker chip).
ARCHITECTURE.md's value-chain row names M14 exactly: **"M14 integration of
Events and Projects | coordination — the two coordination surfaces
interlock."** And the README principles are explicit that the interlock
**is the product**: "**Integration over features.** … Kumunita's value is
the *linkage* that turns those parts into a whole, not the count of parts."

Today the two surfaces **never point at each other** (grep-confirmed at
U00 against the live tree): there is **no `EventId` on `TodoItem`**, **no
to-dos-in-a-calendar surface**, and **no cross-surface display link**
anywhere. A resident who plans an event cannot link the prep to-dos to it,
and the to-dos — which already carry optional dates (ADR 0079) — do not
appear in **any** calendar even though the events do (M12, `IcsWriter`).

What M14 builds on, all frozen and verified against source at U00:

1. **The frozen `IEventService` read seams** (`Kumunita.Core/Events/
   IEventService.cs`): `GetAsync(eventId, actorId, ct)` (one
   `CanAsync(Read)` decision, the 404-vs-403 split) and
   `ListUpcomingAsync(componentId, actorId, page, ct)` +
   `ListMineAsync(actorId, ct)` (the actor's own events — authored ∪
   RSVPed — the ADR 0065 posture). D2 composes the to-do → event display
   link over `GetAsync`; D3 reuses `GetAsync` as the `set-event` lane's
   `eventId` visibility check; the U04 event picker reads `ListMineAsync`.
   **The frozen surface is untouched** (C-M14·7).
2. **The frozen `IProjectService` read + write lanes**
   (`Kumunita.Core/Projects/IProjectService.cs`): `ListTodosAsync`
   (candidate-filter + `CanSeeAsync(Read)` over the existing
   `TodoItemToAuditableResource` + `Created` descending + `TodoPage(Items,
   HasMore)` + one aggregate `AccessAudit` row `TargetKind = "todo"`) — the
   shape `ListTodosForEventAsync` copies; `ListBoardsForTodoAsync` (the
   "list X for a Y" reverse-read precedent); `GetTodoAsync` (the 404-vs-403
   split the `/{id}.ics` lane inherits); `SetTodoProjectAsync` (the
   association-lane shape — creator ∪ assignee ∪ GlobalAdmin, a non-visible
   target refused, `null` clears, one `todo.set_project` `AccessAudit` row)
   — the shape `SetTodoEventAsync` copies verbatim.
3. **The `ProjectId` field on `TodoItem`**
   (`Kumunita.Core/Projects/TodoItem.cs`): `public string? ProjectId
   { get; set; } // a feed filter, never a gate (C-M3·2) — the Project
   association (ADR 0086 D4)` — the *association, never a gate* shape the
   new `EventId?` copies (D1).
4. **The dangling-safe display idiom**
   (`Kumunita.Core/Projects/ProjectRequests.cs`):
   `TodoDetailResult.Blocker` + `BlockerChip` (ADR 0087) — an unreadable /
   absent / soft-deleted target degrades to a `Generic` chip with `null`
   title / link (no leak); and the Web-side `TodoDetailViewModel`
   `ProjectId?` / `ProjectTitle?` pair (ADR 0086 D9) — both `null` when the
   target is soft-deleted / unreadable, the link omitted entirely (C-M14·2
   is this idiom carried to the event link).
5. **The `M5DocTypes` registration surface**
   (`Kumunita.Core/M5DocTypes.cs`): the additive index precedent — the
   `(ComponentId, Created)` feed-ordering index, the `ProjectId` /
   `BlockedByTodoId` single-field indexes — **unnamed** (the computed-index
   naming constraint recorded in the file's own note: Marten
   `ComputedIndex` exposes no `Name`, so single-field indexes use the
   auto-derived-name form). The new `(EventId)` index joins in the same
   form (D1, C-M14·7 — additive, delta-detected, zero migration for
   existing rows, ADR 0004 §B.1).
6. **The `IcsWriter` BCL-only closed-subset emitter**
   (`Kumunita.Core/Events/IcsWriter.cs`, ADR 0112): the CRLF / 75-octet
   fold / full escaping / stable-`UID` / valid-empty-calendar / caller-
   passed-`nowUtc` / **never-emitted list** discipline the
   `TodoIcsWriter` copies verbatim (D4).
7. **The two M12 iCal routes on `EventController`** (`GET /events/{id}.ics`
   + `GET /events.ics`): the serve idiom — `text/calendar; charset=utf-8` +
   `Content-Disposition: attachment; filename="…"` + `Cache-Control:
   no-store` + `X-Content-Type-Options: nosniff` (the ADR 0034/0108 idiom),
   the 404-not-403 split on the per-item lane, always-200 on the feed lane.
   The `VTODO` routes copy this shape (D4, §routes).
8. **The `kw-l` closed-key registry** (ADR 0015) + the
   `KnownTranslationKeys_ParityTests` en/de/fr/da parity pins — the new
   affordance keys join it (D3 / D4, §kw-l).

## Goals / Non-goals

**In (shipped by M14):**

- **D1** — one additive `public string? EventId { get; set; }` on
  `TodoItem` (the `ProjectId` shape, §D1) + one optional `(EventId)` feed
  index on `M5DocTypes` (the unnamed-index precedent) — **a field, not a
  link doc** (1:1).
- **D2** — both display directions as **Web-layer composition** over the
  frozen read seams: the **to-do → event** chip resolves through the
  frozen `IEventService.GetAsync`; the **event → linked to-dos** section
  resolves through the new reverse read seam
  `IProjectService.ListTodosForEventAsync` (§D2). **No**
  `Kumunita.Core.Events` → `Kumunita.Core.Projects` dependency.
- **D3** — the dedicated **`set-event` write lane**:
  `IProjectService.SetTodoEventAsync` (creator ∪ assignee ∪ GlobalAdmin,
  the `SetTodoProjectAsync` shape, §D3) + `POST /projects/todos/{id}/set-
  event` on `ProjectsController` (the `set-project` lane shape) + the
  event picker on the to-do detail (the actor's visible events via the
  frozen `ListMineAsync`, capped) + the `kw-l` keys (§kw-l).
- **D4** — the **`VTODO` calendar surface**: the **BCL-only pure
  `TodoIcsWriter`** (§vtodo — `BuildTodos(IReadOnlyList<TodoItem>,
  DateTimeOffset nowUtc)`; one `VTODO` per **dated** to-do on the closed
  pinned subset) + **two `[Authorize]` routes on `ProjectsController`**
  (§routes — `GET /projects/todos.ics` always 200 + `GET
  /projects/todos/{id}.ics` the 404-not-403 split) + **two quiet `kw-l`
  affordances** (§kw-l).
- **D7** — the invariants (C-M14·1…7, §invariants), the FACES + trade
  (FACES check), the **pinned test names verbatim** (§pinned tests), the
  drift-guard frozen list (§drift-guard), the deferred-lane list
  (§deferred lanes), and the gate (§gate).
- **D8** — **docs parity at the flip** (the U07 close unit, the C-M11·8
  precedent): `Milestones.cs` (M14 → `StatusDone`, M15 → `StatusNext`),
  README Roadmap, `docs/STATUS.md`, `docs/ARCHITECTURE.md` (the M14 value-
  chain row → done), `MilestonesTests.cs` re-pin (M15 becomes the single
  in-progress milestone) — all in **one** unit.

**Out (each a named deferred lane, §deferred lanes):** **recurring events
/ `RRULE` — M18's home** (D6 — `Milestones.cs` is the source of truth);
`RRULE` on the `VTODO` surface; ICS import (round-tripping a calendar file
*into* Kumunita); per-component calendar feeds (`?component=` scoping of
the `VTODO` feed); many-to-many event↔to-do (only relevant if D1's 1:1
field were vetoed — it was not; the field stands).

## Human cost

This gives the resident's coordination back: a resident preparing for the
cleanup day links the prep to-dos to the event **once**, and **both
surfaces now carry each other** — the to-do detail shows the event chip,
the event detail shows the linked to-dos, and (D4) the dated to-dos land
in the **same calendar app** the events already land in (M12). It costs
them nothing they did not already agree to: the `EventId` association is
set by the to-do's own standing (creator ∪ assignee ∪ GlobalAdmin —
C-M14·3); the display links only ever show content the actor may already
see (C-M14·2); the `VTODO` file carries only the to-do's title + body +
optional dates (C-M14·5); and the new strings are localized in all four
languages (§kw-l). The team pays a small, bounded cost: one additive field
+ one index (zero migration — ADR 0004 §B.1 delta-detect), one reverse
read seam + one write lane (both copy existing house shapes verbatim), and
one pure emitter that copies `IcsWriter`'s discipline. **No new bounded
context, no new authorization surface, no recurrence concept, no new
dependency** (C-M14·4 / C-M14·6 / D1 / D4 — the cost is bounded by the
pins, which is the point).

## Parts affected

- **New:** `src/Kumunita.Core/Events/TodoIcsWriter.cs` (the pure `VTODO`
  emitter — the only new Core type outside the Projects context; D4 /
  §vtodo); `tests/Kumunita.Core.Tests/` — the `ListTodosForEventAsync`
  pins (U01, 4), the `SetTodoEventAsync` pins (U02, 3), the
  `TodoIcsWriter` pins (U05, 4), and the three acceptance tests + the
  cross-surface read-shape pins (U07); the Web parity pins (U03 / U04 /
  U06 — the `kw-l` key parity moves together).
- **Touched:** `src/Kumunita.Core/Projects/TodoItem.cs` (the one additive
  `EventId?` field — D1); `src/Kumunita.Core/M5DocTypes.cs` (the one
  additive `(EventId)` index — D1); `src/Kumunita.Core/Projects/
  IProjectService.cs` (the two additive seams — D2 / D3) +
  `src/Kumunita.Core/Projects/ProjectService.cs` (their implementations —
  D2 / D3); `src/Kumunita.Web/Models/ProjectTodoViewModels.cs`
  (`TodoDetailViewModel` additive event-link fields — D2; the
  `TodoEventPicker` picker model — D3) + `src/Kumunita.Web/Models/
  EventEditorModel.cs` (`EventDetailViewModel` additive `LinkedTodos` + the
  `LinkedTodoRow` record — D2); `src/Kumunita.Web/Controllers/
  ProjectsController.cs` (the to-do detail's event-link resolve — D2; the
  `POST /projects/todos/{id}/set-event` action + picker seed — D3; the two
  `.ics` actions — D4); `src/Kumunita.Web/Controllers/EventController.cs`
  (the event detail's `LinkedTodos` population — D2); `src/Kumunita.Web/
  Views/Projects/Todo/Detail.cshtml` (the "linked event" chip — D2; the
  event picker `<form>` — D3; the "Add to calendar" `<a>` — D4) +
  `Views/Projects/Todo/Index.cshtml` (the "Calendar feed (iCal)" `<a>` —
  D4) + `Views/Events/Detail.cshtml` (the "linked to-dos" section — D2);
  `src/Kumunita.Web/Localization/KnownTranslationKeys.cs` + the four
  locale files (§kw-l).
- **Touched in the U07 close only (D8):** `src/Kumunita.Web/Milestones.cs`
  (M14 → `StatusDone`, M15 → `StatusNext`) + `README.md` Roadmap +
  `docs/STATUS.md` + `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/
  MilestonesTests.cs` (re-pin to M15).
- **Untouched (pinned):** the **frozen** `IEventService` / `EventService`
  public surface (C-M14·7 — the M14 seams compose it, never re-scope it);
  the **existing** `IProjectService` / `ProjectService` lanes
  (`ListTodosAsync` / `GetTodoAsync` / `SetTodoProjectAsync` /
  `ListBoardsForTodoAsync` / every M5/PL lane — the pinned Core pins
  survive); **no new** `AccessAction` / `AccessVia` /
  `IAuthorizationService.Decide()` branch / `IAuditableResource` adapter
  (C-M14·4); the `Event` doc (**no recurrence field** — C-M14·6 / D6);
  `CreateTodoRequest` / `UpdateTodoRequest` (**no `EventId` field** — D3:
  association is a dedicated lane, not the main form); `Program.cs` (the
  CSP header byte-identical — the new routes are server-rendered, no
  inline scripts); the `tsconfig.json` glob (no new TS — §kw-l "no new
  JS").

## Seams & contracts (mandatory)

M14 creates **two** additive `IProjectService` seams + **one** pure Core
type, and depends on the **frozen** `IEventService` read seams. Each
contract is named + closed here:

1. **The `EventId` field contract** (§D1): one additive
   `public string? EventId { get; set; }` on `TodoItem`, with the locked
   doc-comment:

   ```csharp
   // src/Kumunita.Core/Projects/TodoItem.cs — the M14 interlock (ADR 0115 D1).
   public string? EventId { get; set; }                    // a feed filter / association, NEVER a gate (C-M14·1) — the Event association (ADR 0115, the ProjectId shape, ADR 0086 D4)
   ```

   The field is an **association** (a to-do is *for* one event — 1:1, the
   `ProjectId` "a to-do is *in* one project" precedent) and a
   **feed-filter** input to `ListTodosForEventAsync` — it **never**
   changes the to-do's own audience decision (C-M14·1 — C-M3·2 / C-PL·3
   carried to the event link). It is **not** on `CreateTodoRequest` /
   `UpdateTodoRequest` (D3 — the `ProjectId` precedent: association is a
   dedicated lane, not the main form).
2. **The reverse read seam contract** (§D2): on `IProjectService`, locked
   verbatim:

   ```csharp
   // src/Kumunita.Core/Projects/IProjectService.cs — the M14 interlock (ADR 0115 D2).
   Task<TodoPage> ListTodosForEventAsync(string eventId, string actorId, int page, CancellationToken ct = default);
   ```

   The **`ListTodosAsync` shape, filtered to one event** (the
   `ListBoardsForTodoAsync` "list X for a Y" reverse-read precedent, the
   ADR 0067 §2.3 house shape): the candidate set is `!IsDeleted &&
   EventId == eventId` (**a filter, never a gate** — C-M14·1); the
   survivors are `CanSeeAsync(Read)`-filtered over the **existing**
   `TodoItemToAuditableResource` (C-M14·4 — no new adapter); ordered by
   `Created` descending; paged with `HasMore = candidates.Count ==
   PageSize` (the ADR 0090 D1/D3 record shape, C-M7·4/5); **one aggregate
   `AccessAudit` row** `TargetKind = "todo"` (the C-M3·3 shape; a 0-
   candidate page reports `HasMore: false` and emits no decision row —
   C-M7·5). The seam returns **only to-dos the actor may read** — an event
   with zero readable linked to-dos returns an empty page, never a 404/403
   (the event's own detail page decides its own visibility — this lane is
   called only from a page that already passed the event's `Read`
   decision).
3. **The write lane contract** (§D3): on `IProjectService`, locked
   verbatim (the `SetTodoProjectAsync` shape, the `todoItemId` parameter
   name matching the sibling — drift-guard entry 3):

   ```csharp
   // src/Kumunita.Core/Projects/IProjectService.cs — the M14 interlock (ADR 0115 D3).
   Task<TodoItem> SetTodoEventAsync(string todoItemId, string actorId, IReadOnlySet<string> actorRoles, string? eventId, CancellationToken ct = default);
   ```

   Sets `TodoItem.EventId` to `eventId` (`null` = **clear the link** — the
   `SetTodoProjectAsync` `null`-clears rule). **Standing: creator ∪
   assignee ∪ GlobalAdmin over the to-do** (C-M5·6 — the ADR 0014/0016/
   0017 matrix, re-checked **server-side**; C-M14·3; the Web `[Authorize]`
   is a convenience pre-gate only, never the source of truth). The to-do
   must exist and be `!IsDeleted` (`KeyNotFoundException` — 404). A
   non-null `eventId` that is **non-existent, soft-deleted, or not readable
   by the actor is refused** (`KeyNotFoundException` — 404, the non-leaky
   split, C-M14·3; the standing decision is the frozen
   `IAuthorizationService.CanAsync(Read)` path over the event — **you can
   only link to an event you can see**; C-M14·4 — no new surface).
   `AuthorId` / `Created` preserved untouched; `Modified` stamped; **one
   `AccessAudit` row** (`Action = "todo.set_event"`, `TargetKind =
   "todo"`) commits **atomically** with the write (C3).
4. **The display-link contract** (D2, Web-layer — the composition point is
   the controller, the same place the M6 notification card composes
   `ListBoardsForTodoAsync`):
   - **to-do → event** (`GET /projects/todos/{id}`): `TodoDetailViewModel`
     gains `EventId?` / `EventTitle?` / `EventLinkPath?` (the ADR 0086 D9
     `ProjectId?` / `ProjectTitle?` shape, all three `null` by default).
     The controller resolves the event through the **frozen**
     `IEventService.GetAsync(todo.EventId, actorId, ct)` and treats
     `KeyNotFoundException` / `UnauthorizedAccessException` /
     `event.IsDeleted` as **leave the chip fields `null`** — omit
     entirely, never a 404/403 for the to-do, no title / id leaked
     (C-M14·2 — the `BlockerChip.Generic` / ADR 0086 D9 dangling-safe
     idiom).
   - **event → to-dos** (`GET /events/{id}`): `EventDetailViewModel` gains
     `IReadOnlyList<LinkedTodoRow>? LinkedTodos = null`; the controller
     calls `ListTodosForEventAsync(eventId, actorId, 0, ct)` and maps to
     rows (**locked shape, §D2** — the `TodoRow` is too heavy for a chip;
     the `BlockerChip`-sized row — drift-guard entry 5):

     ```csharp
     // src/Kumunita.Web/Models/EventEditorModel.cs — the M14 interlock (ADR 0115 D2).
     public sealed record LinkedTodoRow(
         string TodoId,
         string Title,
         string? Status,
         DateTimeOffset? DueAt,
         string LinkPath);
     ```

     (`LinkPath = "/projects/todos/{TodoId}"`; `Status` / `DueAt` are the
     to-do's verbatim fields; the page's `Read` decision already ran on
     every row — the seam is the gate, C-M14·1 / C-M14·4.) An empty result
     ⇒ `LinkedTodos` stays `null` (the view renders nothing — no empty-
     state section for an absent linkage).
5. **The `VTODO` emitter contract** (§vtodo): `TodoIcsWriter` is a
   **static class** in `Kumunita.Core/Events/` (the `IcsWriter` shape)
   with **one** public method, locked verbatim:

   ```csharp
   // src/Kumunita.Core/Events/TodoIcsWriter.cs — the ONLY public surface (ADR 0115 D4).
   public static string BuildTodos(IReadOnlyList<TodoItem> todos, DateTimeOffset nowUtc);
   ```

   `todos` = the **already-authorized** rows the caller resolved (the
   frozen `IProjectService` read seam's output — C-M14·5); `nowUtc` = the
   caller's fetch instant (the `DTSTAMP` refresh marker — the emitter never
   reads a clock, so it is deterministic + testable). The return is the
   **entire** ICS file — the `VCALENDAR` envelope + one `VTODO` per
   **dated** to-do (`DueAt != null || StartAt != null`; a to-do with
   **neither** is **skipped** — a `VTODO` with neither `DUE` nor
   `DTSTART` is a valid-but-pointless component, the "only include what's
   meaningful" `IcsWriter` idiom) — CRLF-terminated, 75-octet-folded,
   fully escaped, on the **pinned subset** (§vtodo). The emitter is
   **pure**: no store, no `IDocumentSession`, no `IAuthorizationService`,
   no clock, no reflection, no library (D4 — BCL-only; C-M14·5). The
   authorization was done *before* the rows reach here; the decision is
   never re-derived and never leaked (C-M14·5).
6. **The route contracts** (§routes): two `[Authorize]` actions on
   `ProjectsController` (the class-level `[Authorize]` already applies),
   served with the ADR 0034/0108 idiom (the M12 `EventController.EventIcs`
   / `CalendarFeed` shape, verbatim):
   - **`GET /projects/todos.ics`** — `ListTodosAsync(null, null, actorId,
     0, ct)` (the caller's visible to-do set, the feed-lane shape) →
     **filter to dated to-dos in the Web layer** (`DueAt != null ||
     StartAt != null` — a display filter, not a new seam, C-M14·7) →
     `TodoIcsWriter.BuildTodos(dated, DateTimeOffset.UtcNow)` → the serve
     shape (`filename="kumunita-todos.ics"`). **Always `200`** (an empty
     visible / dated set is a valid empty `VCALENDAR` — the RFC-legal no-
     crash path, C-M14·5/6).
   - **`GET /projects/todos/{id}.ics`** — `GetTodoAsync(todoId, actorId,
     ct)` (the **404-not-403** split inherited — a denied / absent to-do
     neither downloads nor 403s into existence, C-M14·2) → one
     `VTODO` on the pinned subset (§vtodo; an undated to-do yields a
     `VTODO` with only `UID` / `DTSTAMP` / `SUMMARY` (+`DESCRIPTION`),
     the RFC-legal degenerate form — the feed lane's skip rule applies to
     the *feed*, not an explicit per-item request — drift-guard entry 4) →
     the serve shape (`filename="kumunita-todo-{id}.ics"`).

   Both routes: `text/calendar; charset=utf-8` + `Content-Disposition:
   attachment` + `Cache-Control: no-store` + `X-Content-Type-Options:
   nosniff`. **No subscription token** — the feed rides the cookie,
   re-authorized every fetch (the M6 link-lane posture, ADR 0085/0095,
   carried to the to-do surface — the ADR 0112 D2 shape).
7. **The access-model contract:** **zero change** (C-M14·4). M14 adds
   **no** `AccessAction`, **no** `AccessVia`, **no**
   `IAuthorizationService.Decide()` branch, **no** `Audience` change,
   **no** `IAuditableResource` adapter. All read lanes route through the
   **existing** `TodoItemToAuditableResource` /
   `EventToAuditableResource` adapters and the **frozen**
   `IAuthorizationService.CanAsync` / `CanSeeAsync(Read)` path. The
   `set-event` standing **reuses** C-M5·6 (creator ∪ assignee ∪ GlobalAdmin
   — the `SetTodoProjectAsync` matrix) — it does not invent a new one. The
   read lanes already commit their own `AccessAudit` rows (the frozen
   ADR 0006/C3 lanes); the `set-event` lane adds exactly one
   (`todo.set_event`); M14 adds no other audit verb.

## Feedback loops

The **closed, pinned test list** (§pinned tests) is the feedback loop —
every unit's Exit is "build green + the pinned tests discovered + passing."
The **pure** Core pins (`TodoIcsWriterTests`, no Testcontainers — the
emitter is pure over POCOs) prove the emitter's **purity + closed subset +
escaping + 75-octet fold + UID stability + DUE/DTSTART conditionality +
empty-calendar validity + the never-emitted list** (C-M14·5/6) in
milliseconds, so a regression in the emitter is caught before any route is
touched. The **Testcontainers** Core pins (U01 / U02, over the existing
`PostgresFixture` `IProjectService` test shape) prove the **reverse seam's
candidate filter + `CanSeeAsync(Read)` gate + `HasMore`** (U01, C-M14·1)
and the **write lane's standing + eventId-visibility refusal + null-clear
+ audit row** (U02, C-M14·3) against a live store. The **Web** pins
(NSubstitute, no Postgres) prove the **display links' dangling-safe
omission** (U03), the **`set-event` action's CSRF + binding + redirect**
(U04), and the **two `.ics` routes' serve shape + 404-not-403 split +
always-200 feed + kw-l parity** (U06 — the
`KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests` pins
move together with every new key). The three **acceptance tests**
(§gate) are the final gate U07 records.

## Emergent impact

**Trust** is the load-bearing virtue: the interlock **never leaks**
(C-M14·2) — a to-do linked to an event the actor cannot see simply does not
show the chip (the target's title / id are not in the to-do's HTML), and
an event's "linked to-dos" section shows only to-dos that actor may read
(the seam is the gate — C-M14·1). The **`VTODO` file carries no decision**
(C-M14·5) — no author / assignee identity (`ORGANIZER` / `ATTENDEE`
never), no audience / grant / membership material, no `X-…` — the ADR 0028
"no standing to carry a secret" posture and the ADR 0112 C-M12·2 pin,
carried to the to-do surface. **Privacy**: the association is set under
the to-do's **existing** standing (C-M14·3) and the read lanes reuse the
**existing** `Read` decisions (C-M14·4) — M14 grants no new access to any
content; it only surfaces *links between content the caller already
holds*. **Reliability**: the schema change is additive + delta-detected
(ADR 0004 §B.1 — zero migration for existing rows, idempotent at boot);
the emitter is pure + deterministic (no clock, no IO, no store); the
`Cache-Control: no-store` pin keeps the per-caller file out of any proxy
or the PWA service worker (the M10 C-M10·2 cache-boundary posture — the
service worker's GET allowlist never includes the new `.ics` routes, and
they are signed-in routes, so the fall-through rule already covers them).
**Legibility**: the to-do detail gains **one** labeled chip per cross-
reference (the FACES trade, named below) — the surface stays readable.
**Cost**: one additive field + one index + two seams + one emitter + two
routes — bounded by the pins.

## Local-optimization check

This optimizes the **whole** (the neighborhood's coordination loop —
"Integration over features," the README principle verbatim: "Kumunita's
value is the *linkage* that turns those parts into a whole"), not a part.
It is not an engagement or retention mechanic: the `EventId` field links
one to-do to one event for the actor who set it (the standing is the
to-do's own — C-M14·3); the display links surface content the caller may
already read (C-M14·1/2); the `VTODO` feed is **exactly** the caller's
visible dated to-dos (C-M14·5) — no amplification, no cross-caller bleed,
no RSVP-list / assignee-list exposure (the never-emitted list, C-M14·5).
The whole pays: one additive field + index (zero migration), two additive
seams (frozen surface extended, the ADR 0086/0087 frozen-surface rule
honoured), one pure emitter, two routes — and a little **legibility** on
the to-do detail (the named FACES trade).

## FACES check

This design **strengthens** **c**oherent (the interlock rides the house's
own seams — the `ProjectId` field shape, the `ProjectLink`/`BlockerChip`
display idiom, the `set-project` write-lane shape, the `IcsWriter`
emitter discipline — **nothing new is invented**; F1), **s**table
(additive-only schema + seams; the frozen `IEventService` / existing
`IProjectService` surfaces untouched; the pinned Core/Web pins survive —
F2), **f**lexible (the `EventId` field + the `VTODO` surface compose with
future lanes: M18 (recurring) can later add `RRULE` to the `VTODO` /
`VEVENT` emitters and a recurrence field to `Event` **without touching the
M14 linkage**; F3), and **e**nergizing (a resident preparing for an event
links the prep to-dos and carries **both surfaces in one calendar** — the
principle made concrete; F4). It **consumes** a little **a**daptive
legibility — see the trade. **The trade, named (at least one):** F4 (more
linkage surface) spends a slice of **legibility** — a to-do card now
carries up to **four** cross-references (project, board placements,
blocker, **event**). The detail view must stay legible: **one labeled chip
per cross-reference, each with its own `kw-l` label, empty-state when
absent** — never a wall of links. (F4 consumes a slice of F2/legibility;
the cost is named, not hidden.) And the interlock **degrades gracefully**
(F5 / adaptive): an event the actor can't see is simply not shown; an
unlinked / dangling target omits its chip — the interlock never leaks
(C-M14·2), which is the adaptive face doing its job.

## Rollout & rollback

**Additive-only, delta-detected** (C-M14·7 — ADR 0004 §B.1): the `EventId?`
field + the `(EventId)` index land on `M5DocTypes`'s existing `TodoItem`
block; `ApplyAllConfiguredChangesToDatabaseAsync()` delta-detects the new
column + index and applies them **idempotently at boot** — **zero
migration for existing rows** (existing `TodoItem` rows simply read
`EventId = null`; the `ProjectId` / `BlockedByTodoId` additive-index
precedent in the same file). **Rollback** is a clean code revert: the
field + index revert (the column stays in Postgres — a harmless nullable
column, the house's additive-never-removed posture), the two seams
revert, the chip / section / picker / routes / `kw-l` keys revert (an
unused `kw-l` key renders nothing — the registry rows are inert), and the
emitter + its tests revert. **No data is orphaned**: a `TodoItem.EventId`
value on a reverted build is simply never read (the ADR 0024 dangling-
association posture — the association dangles, nothing breaks). See OPS.md.

## Risks

- **The `ProjectService` constructor gaining an `IEventService?`
  parameter** (drift-guard entry 2) — the only cross-context reference M14
  could introduce is **`Projects` → `Events`**, and only if the
  `set-event` lane resolves a target's visibility through a sibling
  service's `GetAsync`. The **preferred** reading (drift-guard entry 1) is
  that the standing check runs through the **frozen**
  `IAuthorizationService.CanAsync(Read)` path (the `SetTodoProjectAsync`
  precedent), so `ProjectService` needs **no** `IEventService` reference
  at all. Either way, a `Kumunita.Core.Events` → `Kumunita.Core.Projects`
  reference is **never** introduced (D2 — the composition point for
  event → to-dos is the `EventController`, which already references both
  services; the ADR 0004 §B.1 "a bounded context per concern" rule holds).
- **The `.ics` literal in an `{id}` route template** (the M12 U02 known
  gotcha, the register names it): `GET /projects/todos.ics` and
  `GET /projects/todos/{id}` both match the literal `/projects/todos.ics`
  segment. The route strings are **pinned verbatim** in §routes
  (`[HttpGet("/projects/todos/{id}.ics")]` + `[HttpGet("/projects/todos.
  ics")]`); ASP.NET Core ranks the **literal** `"/projects/todos.ics"`
  route above the parameterized `"/projects/todos/{id}"`, so
  `GET /projects/todos.ics` resolves to the feed, not to a detail with
  `id = "todos.ics"`. The U06 handoff records the exact resolution and the
  §routes pin is the source of truth (the ADR 0112 / M12 §routes
  precedent).
- **A `DESCRIPTION` that is Markdown with `![img](/content-image/{id})` /
  `[file](/attachment/{id})` links** — the same M12 chosen-behavior risk:
  these are **app-internal paths**; a calendar app renders them as plain
  text (honest + portable). `DESCRIPTION` is the **Markdown source** (not
  rendered HTML) — the body is readable text + the image/file references
  are literal links the resident can copy. Chosen behavior (D4), not a
  gap.
- **The to-do detail gaining a fourth cross-reference** (the FACES trade)
  — the legibility risk is real if the chip / picker / calendar link stack
  into a wall. The mitigation is the pin: **one labeled chip per
  cross-reference, each with its own `kw-l` label, empty-state when
  absent** (FACES check, trade) — the U03 / U04 / U06 views join the
  existing `ProjectLink` / `BlockerChip` block, they do not restructure it.
- **The `DTSTAMP` is "now," not the to-do's `Modified`** — the same
  deliberate ADR 0112 D1 choice: `DTSTAMP` is the RFC's refresh marker
  (the calendar app's update-vs-add signal uses `UID`, not `DTSTAMP`);
  the caller passes it, the emitter never reads a clock, so the UID-
  stability pin is about `UID`, not `DTSTAMP`.

## Integration step served

The **coordination** arrow (the ARCHITECTURE.md M14 row, verbatim): "M14
integration of Events and Projects | coordination — the two coordination
surfaces interlock." M14 moves the neighborhood's planning loop **between
its own two surfaces**: the event's prep to-dos become one thing, and the
dated to-dos land in the residents' calendars **alongside** the events
(the D4 calendar interlock) — the "Integration over features" principle
made concrete (the linkage that turns two parts into a whole, README
verbatim).

## World seams

**The handoff this creates (D4):** the resident's **calendar app** (Apple
Calendar, Google Calendar, Outlook, Thunderbird, any RFC 5545 client)
gains the to-do surface — they paste `https://…/projects/todos.ics` (or
the per-to-do `GET /projects/todos/{id}.ics`) and the app imports /
subscribes to the dated to-dos **next to the events** M12 already
delivers. **Every output flows to a next action:** the per-to-do file →
one `VTODO` the app adds (the "Add to calendar" affordance, §kw-l); the
feed → the app's periodic re-fetch (the subscription — rides the cookie,
re-authorized every fetch, D4 / §routes). **No privacy boundary is
crossed:** the file carries no decision (C-M14·5), the boundary is the
cookie (F3), and `Cache-Control: no-store` keeps it out of any cache (the
M10 C-M10·2 posture). M14 adds **no** notification, **no** email, **no**
inbox row for a linked to-do — the linkage is a *link*, not a second feed
nobody reads (the display links are the surface; the calendar app is the
delivery channel).

## §D1 — the field + the index (U01 copies verbatim)

**The field** (locked, on `TodoItem` beside `ProjectId`):

```csharp
public string? EventId { get; set; }                    // a feed filter / association, NEVER a gate (C-M14·1) — the Event association (ADR 0115, the ProjectId shape, ADR 0086 D4)
```

**The index** (locked, on the `TodoItem` block in `M5DocTypes.Configure`,
joining the `ProjectId` / `BlockedByTodoId` single-field indexes):

```csharp
// the `EventId` feed-filter lookup (the M14 interlock — a feed filter,
// never a gate, ADR 0115 D1 / C-M14·1 — the ADR 0086 `ProjectId` / ADR
// 0087 `BlockedByTodoId` precedent; unnamed: the auto-derived name stays
// under Postgres' 64-char NAMEDATALEN cap, the file's own note)
.Index(t => t.EventId)
```

**The rule:** the field narrows the candidate set in
`ListTodosForEventAsync` (and is usable as any other feed filter); it
**never** changes the audience decision — a to-do is visible iff it passes
its **own** `CanSeeAsync(Read)` (C-M14·1 — C-M3·2 / C-PL·3 carried to the
event link; the `ProjectId` precedent verbatim).

## §D2 — the display links (U03 copies verbatim)

**The to-do → event chip** (on `TodoDetailViewModel`, additive, the ADR
0086 D9 `ProjectId?` / `ProjectTitle?` shape):

```csharp
// ADR 0115 D2 — the **event link** on the to-do detail (the D2
// dangling-safe rule: all three fields are `null` when the to-do has
// no `EventId`, or the target event is soft-deleted / unreadable by the
// actor — the chip is then omitted entirely, never a 404/403 for the
// to-do itself; the `todo.event_link` kw-l key labels it).
string? EventId = null,
string? EventTitle = null,
string? EventLinkPath = null,
```

**Resolve** (the `ProjectsController` to-do detail action): call the
**frozen** `IEventService.GetAsync(todo.EventId, actorId, ct)`; on
`KeyNotFoundException` / `UnauthorizedAccessException` / `event.IsDeleted`
leave all three fields `null` (C-M14·2 — the C3 404-vs-403 split applied
to a cross-reference: the target's title / id are not leaked). On success:
`EventId = ev.Id`, `EventTitle = ev.Title`, `EventLinkPath =
"/events/" + ev.Id`.

**The event → to-dos section** (on `EventDetailViewModel`, additive):

```csharp
// ADR 0115 D2 — the **linked to-dos** on the event detail (the reverse
// read seam's page, mapped to rows; `null` = no linked readable to-dos,
// the view renders nothing; the `events.linked_todos` kw-l key labels
// the section).
IReadOnlyList<LinkedTodoRow>? LinkedTodos = null
```

with the locked `LinkedTodoRow` (the §seams contract 4 shape):

```csharp
public sealed record LinkedTodoRow(
    string TodoId,
    string Title,
    string? Status,
    DateTimeOffset? DueAt,
    string LinkPath);
```

**Resolve** (the `EventController` detail action): call
`IProjectService.ListTodosForEventAsync(event.Id, actorId, 0, ct)` (page
0 — the detail surface, not a paged list; the `HasMore` signal is
ignored — the section is display-only, the `BlockerChip` /
`ProjectLink` "one section, not a feed" precedent); map each `TodoItem`
to a `LinkedTodoRow` (`LinkPath = "/projects/todos/" + todo.Id`); an empty
`Items` ⇒ leave `LinkedTodos = null`. **No new authorization surface**
(C-M14·4 — the seam's `CanSeeAsync(Read)` pass is the gate).

## §D3 — the write lane (U02 / U04 copy verbatim)

**The seam** (locked, §seams contract 3): `SetTodoEventAsync(string
todoItemId, string actorId, IReadOnlySet<string> actorRoles, string?
eventId, CancellationToken ct = default)` — standing **creator ∪ assignee
∪ GlobalAdmin** over the to-do (C-M5·6, re-checked server-side, C-M14·3);
to-do `!IsDeleted` (404); a non-visible / soft-deleted / absent `eventId`
**refused 404** (the frozen `IAuthorizationService.CanAsync(Read)` path
over the event — the `SetTodoProjectAsync` target-visibility rule, C-M14·3
/ C-M14·4); `null` **clears** the link; `AuthorId` / `Created` untouched;
`Modified` stamped; one `AccessAudit` row (`todo.set_event`,
`TargetKind = "todo"`) committed atomically (C3).

**The Web lane** (locked, the `POST /projects/todos/{id}/set-project`
shape at `ProjectsController`): `POST /projects/todos/{id}/set-event` on
`ProjectsController` — `[ValidateAntiForgeryToken]`, one `eventId` form
field (bound nullable; **blank / `null` posts as the clear affordance** —
the `set-project` lane's `null`-clears shape); the actor's visible-event
**picker** is seeded by the frozen `IEventService.ListMineAsync(actorId,
ct)` (the actor's own events — authored ∪ RSVPed — the ADR 0065 posture;
**capped at 25** for the `<select>` — a display cap, not a gate; a
`null`/absent `eventId` renders the "clear" option first) + the
`set-event` action redirects back to the to-do detail on success, 404 /
403 on the seam's decision as truth (C-M14·3). **`EventId` is not on
`CreateTodoRequest` / `UpdateTodoRequest`** (D3 — the `ProjectId`
precedent: association is a dedicated lane, not the main form).

## §vtodo — the pinned VTODO subset (D4; U05 copies verbatim)

> **The closed property set.** The emitter emits **exactly** this, in
> this order, and nothing else (C-M14·6). A property outside this set is a
> **drift event** (record it in §drift-guard, resolve in favor of this
> pin). This is the WY·3 subset pin (ADR 0031) and the C-M12·4 pin
> (ADR 0112) applied to the `VTODO` surface.

**The `VCALENDAR` envelope** (every file, one) — the `IcsWriter`
envelope verbatim:

```
BEGIN:VCALENDAR
VERSION:2.0
PRODID:-//Kumunita//community calendar//EN
CALSCALE:GREGORIAN
METHOD:PUBLISH
{VTODO blocks, one per dated to-do, in list order}
END:VCALENDAR
```

**The `PRODID` value, verbatim (locked):**
`-//Kumunita//community calendar//EN` (the ADR 0112 D4 pin — one
PRODID for the instance, `VEVENT` and `VTODO` files alike).

**One `VTODO` per dated to-do** (`DueAt != null || StartAt != null`;
neither ⇒ **skipped**), the **pinned** property order:

```
BEGIN:VTODO
UID:kw-todo-{todo.Id}@kumunita
DTSTAMP:{nowUtc as yyyyMMddTHHmmss}Z
DUE:{todo.DueAt as yyyyMMddTHHmmss}Z        (only when DueAt is set)
DTSTART:{todo.StartAt as yyyyMMddTHHmmss}Z  (only when StartAt is set)
SUMMARY:{todo.Title escaped}
DESCRIPTION:{todo.Body escaped, folded}     (only when Body is non-empty)
STATUS:CANCELLED                            (only when todo.IsDeleted == true)
END:VTODO
```

**The field-by-field pin:**

- **`UID`** = `kw-todo-{todo.Id}@kumunita` — **stable** across fetches
  (the calendar app's update-vs-add key; the same `TodoItem.Id` ⇒ the
  same `UID`; the `kw-eve-…` naming convention with the `todo` tag,
  C-M14·6).
- **`DTSTAMP`** = the caller-passed `nowUtc` (the RFC's "date-time of
  creation" = the refresh marker, **not** `TodoItem.Modified` — the
  ADR 0112 D1 shape).
- **`DUE`** = `todo.DueAt` (stored UTC, `yyyyMMddTHHmmssZ`; **only when
  set** — the ADR 0079 optional-date shape).
- **`DTSTART`** = `todo.StartAt` (stored UTC, `yyyyMMddTHHmmssZ`;
  **only when set**).
- **`SUMMARY`** = `todo.Title` (escaped).
- **`DESCRIPTION`** = `todo.Body` — the **Markdown source**, not rendered
  HTML (only when non-empty — the ADR 0112 D4 chosen-behavior pin).
- **`STATUS:CANCELLED`** = only when `todo.IsDeleted == true` (the RFC
  update path — an emitter **capability**, never reached through the
  frozen read seams, the ADR 0112 `IcsWriter` drift-guard entry-1 shape).

**Never emitted (the C-M14·5 / C-M14·6 "never" list — the ADR 0028
posture + the ADR 0112 D1 "never" list carried to the to-do surface):**
`ORGANIZER` (no author identity in a file that travels), `CREATED-BY`,
`ATTENDEE` (no assignee identity), `RRULE` / `RECURRENCE-ID` (C-M14·6 /
D6 — no recurrence in M14), `TodoItem.Audience` / grant / membership
internals (the decision is already *applied* to the set — C-M14·5),
`TodoItem.AuthorId` / `AssigneeId` beyond the never-emitted properties,
`ComponentId` / `ProjectId` / `EventId` / `ParentId` / `BlockedByTodoId`
/ `Status` / `LanguageCode` / `TagIds` / `ImageIds` / `AttachmentIds`
/ `Created` (app-internal state — none of it), and **any** vendor-
specific property (the `X-…` set — none of it).

**Formatting (C-M14·6):** CRLF endings on every line (including the final
`END:VCALENDAR`); a content line whose UTF-8 octet length exceeds 75 is
folded at a ≤ 75-octet boundary that does not break a multi-byte UTF-8
sequence, with a single leading space on each continuation line; escaping
— backslash `\` → `\\` (first), semicolon → `\;`, comma → `\,`, CR/LF →
`\n` (the ADR 0112 / `IcsWriter` format pin verbatim).

## §routes — the two VTODO routes (D4; U06 copies verbatim)

**Locked verbatim** (the ADR 0112 §routes shape, the `EventController`
serve idiom):

```csharp
// src/Kumunita.Web/Controllers/ProjectsController.cs — the M14 interlock (ADR 0115 D4).
[HttpGet("/projects/todos/{id}.ics")]   // lane 1 — one VTODO (the 404-not-403 split)
[HttpGet("/projects/todos.ics")]        // lane 2 — the feed (always 200)
```

**Lane 1 — `GET /projects/todos/{id}.ics`:** `[Authorize]` (the class-
level `[Authorize]` already applies); `IProjectService.GetTodoAsync(id,
actorId, ct)` — the **404-not-403** split inherited
(`KeyNotFoundException` / `UnauthorizedAccessException` ⇒ `NotFound()`;
C-M14·2); the to-do's single `VTODO` on the pinned subset (§vtodo — an
undated to-do yields a `VTODO` with only `UID` / `DTSTAMP` / `SUMMARY`
(+`DESCRIPTION`), the RFC-legal degenerate form); the serve shape.
**Serve (locked):** `Content-Type: text/calendar; charset=utf-8` +
`Content-Disposition: attachment; filename="kumunita-todo-{id}.ics"` +
`Cache-Control: no-store` + `X-Content-Type-Options: nosniff` (the
ADR 0034/0108 idiom, the M12 `EventIcs` action verbatim).

**Lane 2 — `GET /projects/todos.ics`:** `[Authorize]`;
`IProjectService.ListTodosAsync(null, null, actorId, 0, ct)` (the
caller's visible to-do set — the feed-lane shape, the candidate filter +
`CanSeeAsync(Read)` gate all inside it — C-M14·7); **filter to dated to-
dos in the Web layer** (`DueAt != null || StartAt != null` — a display
filter, never a new seam); `TodoIcsWriter.BuildTodos(dated,
DateTimeOffset.UtcNow)` (an empty set ⇒ a valid empty `VCALENDAR` — the
RFC-legal no-crash path, C-M14·5/6); the serve shape
(`filename="kumunita-todos.ics"`, the same three other headers).
**Always `200`** (the seam filters rather than throws for an individual
denial — no 404/403 on this lane). **Anonymous ⇒ the standard sign-in
challenge** (the class-level `[Authorize]` default — F3; no anonymous
iCal surface, the ADR 0112 D2 pin). **No subscription token** — the feed
rides the cookie, re-authorized every fetch (the M6 link-lane posture,
ADR 0085/0095; the ADR 0112 D2 shape).

**The route-resolution pin (the M12 known gotcha, carried):** ASP.NET
Core ranks the **literal** `"/projects/todos.ics"` route above the
parameterized `"/projects/todos/{id}"`, so `GET /projects/todos.ics`
resolves to the feed, not to a detail with `id = "todos.ics"` (the
`EventController` `/events.ics` / `/events/{id}` resolution, the ADR
0112 §routes precedent).

## §kw-l — the locked key set (U03 / U04 / U06 add, × en/de/fr/da)

| Key | en (locked) | Used by |
|---|---|---|
| `todo.event_link` | "Linked event" | the to-do detail chip (U03) |
| `todo.set_event.label` | "Link to event" | the picker form label (U04) |
| `todo.set_event.pick` | "Choose an event" | the `<select>` placeholder / clear option (U04) |
| `events.linked_todos` | "Linked to-dos" | the event detail section (U03) |
| `projects.todos.ics.download` | "Add to calendar" | the to-do detail `<a>` (U06 — the ADR 0112 `events.ics.download` shape) |
| `projects.todos.ics.feed` | "Calendar feed (iCal)" | the to-do index `<a>` (U06 — the ADR 0112 `events.ics.feed` shape) |

All six keys join the closed `kw-l` registry in **all four** languages
(the `KnownTranslationKeys_ParityTests` enforces the 4-language set +
non-empty values; the ADR 0015/0052 warm-boot backfill seeds them
idempotently). **No new JS** (plain `<a>` links + one native `<form>` /
`<select>` — the ADR 0031 tsc-only discipline holds).

## §invariants — C-M14·1 … C-M14·7 (locked verbatim)

- **C-M14·1 · The `EventId` is a filter/association, never a gate.** It
  narrows the candidate set in `ListTodosForEventAsync`; it **never**
  changes the audience decision (C-M3·2 / C-PL·3). A to-do is visible iff
  it passes its **own** `CanSeeAsync(Read)`; the event link neither
  grants nor denies.
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
  `AccessVia`, or `Decide()` branch (C-M5·11). All read lanes route
  through the frozen `IAuthorizationService` over the **existing**
  `TodoItemToAuditableResource` / `EventToAuditableResource` adapters.
- **C-M14·5 · (D4) The `VTODO` file never carries a decision.**
  (C-M12·2 / ADR 0028 applied to the file form.) No `ORGANIZER` /
  `CREATED-BY` / `ATTENDEE` (author / assignee identity), no `Audience` /
  grant / membership internals; the authorization was applied to the set
  *before* the emitter runs.
- **C-M14·6 · No `RRULE` / recurrence anywhere in M14.** The `Event` doc
  gains **no** recurrence field; the `VTODO` emitter emits a single
  `VTODO` per to-do. `RRULE` is **M18's** home (D6).
- **C-M14·7 · Additive-only, frozen seams untouched.** The `EventId?`
  field + the `(EventId)` index + `ListTodosForEventAsync` +
  `SetTodoEventAsync` + (D4) the `TodoIcsWriter` are **additive** (ADR
  0004 §B.1 delta-detect, idempotent at boot, zero migration for existing
  rows). The **frozen** `IEventService` lanes and the **existing**
  `IProjectService` lanes are **untouched** (the pinned Core/Web pins
  survive).

## §pinned tests — the locked test names (U01 / U02 / U05 / U06 / U07)

> **U01 (4)** — the `ListTodosForEventAsync` pins (over the existing
> `PostgresFixture` `IProjectService` test shape):
> 1. `ListTodosForEventAsync_ReturnsEmptyPage_WhenNoTodosLinkedToTheEvent`
>    (the empty-candidate early return — no decision row, C-M7·5)
> 2. `ListTodosForEventAsync_DropsTodosTheActorCannotRead` (the
>    `CanSeeAsync(Read)` filter drops the denied to-do, C-M14·1/4)
> 3. `ListTodosForEventAsync_ExcludesTodosLinkedToADifferentEvent` (the
>    `EventId ==` filter — a to-do linked to a *different* event is
>    excluded, C-M14·1)
> 4. `ListTodosForEventAsync_HasMoreIsTrue_WhenThePageFills` (the
>    `HasMore` page-boundary pin, C-M7·4)
>
> **U02 (3)** — the `SetTodoEventAsync` pins:
> 1. `SetTodoEventAsync_AllowsCreatorAssigneeAndGlobalAdmin_RefusesOutsider`
>    (creator / assignee / GlobalAdmin each allowed, an outsider refused
>    403 — C-M14·3 / C-M5·6)
> 2. `SetTodoEventAsync_RefusesInvisibleEventId_NullClearsTheLink` (a
>    non-visible `eventId` refused 404; a `null` clears the link —
>    C-M14·3)
> 3. `SetTodoEventAsync_CommitsOneAccessAuditRowWithTheWrite` (the
>    `todo.set_event` row, `TargetKind = "todo"`, committed atomically —
>    C3)
>
> **U05 (4)** — the `TodoIcsWriter` pins (pure, no Testcontainers):
> 1. `BuildTodos_EmitsThePinnedVTodoSubset_ForADatedTodo` (the `VTODO`
>    field-map for a dated to-do — C-M14·6)
> 2. `BuildTodos_EmitsDueOnly_DtstartOnly_SkipsUndated` (the `DUE`-only /
>    `DTSTART`-only / neither (skipped) conditional shape — the §vtodo
>    pin)
> 3. `BuildTodos_FoldsAndEscapesALongTitleAndBody` (the fold + escape on
>    a long / special-char title+body — the C-M14·6 format pin)
> 4. `BuildTodos_ReturnsAValidEmptyCalendar_WhenNoDatedTodos` (a valid
>    **empty** `VCALENDAR` when no dated to-dos — C-M14·5/6)
>
> **U06 (0 new pins — the parity pins move)** — the two new `kw-l` keys'
> `KnownTranslationKeys_ParityTests` + `KwLRegistryConsistencyTests`
> coverage is the feedback loop; the route / serve-shape pins ride the
> existing Web test shape (NSubstitute).
>
> **U07 (3 acceptance + the cross-surface pins)** — the §gate tests,
> recorded by U07:
> 1. **Closed loop** — `Interlock_ClosedLoop_TodoAndEventSeeEachOther` —
>    a to-do linked (via the `set-event` lane) to a **visible** event:
>    the to-do detail shows the event chip **and** the event detail shows
>    the to-do in its "linked to-dos" section — the linkage is symmetric
>    and both directions resolve.
> 2. **Handoff** — `Interlock_Handoff_WriteAndReadComposeWithoutNewAuthorizationSurface`
>    — the `set-event` write + the both-direction read compose with **no
>    new authorization surface** (C-M14·4) — a grep of the diff shows no
>    new `AccessAction` / `AccessVia` / `Decide()` branch, and the
>    `EventId` is never a gate (C-M14·1).
> 3. **Part vs. whole** — `Interlock_PartVsWhole_UnreadableEventOmitsTheChip`
>    — the interlock **degrades gracefully** (F5): an event the actor
>    **cannot** see (or a soft-deleted / absent target) **omits** the chip
>    / the row — never a 404/403 for the source surface, and the target's
>    title / id are **not leaked** (C-M14·2).

## §drift-guard — the frozen list (locked; a change is a drift event)

- The `EventId?` field on `TodoItem` (its name + the filter-never-gate
  rule, C-M14·1).
- The `ListTodosForEventAsync` + `SetTodoEventAsync` signatures
  (`IProjectService`) — §seams contracts 2/3, verbatim.
- The `TodoDetailViewModel.EventId?` / `EventTitle?` / `EventLinkPath?`
  + `EventDetailViewModel.LinkedTodos` additive fields (+ the
  `LinkedTodoRow` shape) — §D2, verbatim.
- The `TodoIcsWriter.BuildTodos` signature + the closed `VTODO` property
  set + the never-emitted list — §vtodo, verbatim (C-M14·5 / C-M14·6).
- The two `VTODO` routes (`GET /projects/todos.ics` + `GET
  /projects/todos/{id}.ics`) + the `text/calendar` serve shape — §routes,
  verbatim.
- The invariants C-M14·1…C-M14·7 (§invariants, verbatim).
- The pinned test names (U01 / U02 / U05 / U06 / U07, §pinned tests,
  verbatim).

**Drift recorded at U00 (resolved in favor of the source, each a
refinement, not a decision change):**

1. **The `set-event` lane's `eventId` visibility check runs through the
   frozen `IAuthorizationService.CanAsync(Read)` path over the event**
   (the `SetTodoProjectAsync` target-visibility rule, C-M14·3/4), **not**
   by calling `IEventService.GetAsync` in `ProjectService` and catching
   its exceptions — the register's U02 entry-read #4 suggested the latter,
   but the house's association-lane precedent (`SetTodoProjectAsync`,
   verified at U00) resolves the target's visibility through the
   **authorization service** (the "a read is not a decision" posture,
   ADR 0054) — so the lane's **only** cross-context touch is the standing
   check, and `ProjectService` needs **no** `IEventService` reference at
   all. (If U02 finds the `SetTodoProjectAsync` implementation in fact
   calls a sibling service's `GetAsync`, it may follow that implementation
   verbatim — the shape is "refuse a non-visible target 404, one audit
   row, atomically"; the mechanism follows the sibling. This drift entry
   names the *preferred* reading and the *acceptable* alternative;
   either way C-M14·3/4 hold.)
2. **The `ProjectService` constructor gains no `IEventService` parameter
   if drift entry 1's preferred reading holds** (the §risks constructor-
   extension note is then moot; the register's U02 entry-read #4 remains
   the authoritative shape to copy *from*, i.e. `SetTodoProjectAsync`'s
   own implementation). **Refinement, not a decision change** — D2/D3
   stand verbatim.
3. **The U02 parameter name is `todoItemId`** (matching the sibling
   `SetTodoProjectAsync(string todoItemId, …)`, verified at U00) — the
   register's D3 prose said `todoId`; the sibling's actual signature wins
   (drift-guard entry 2 pins `todoItemId`).
4. **The U06 lane-1 degenerate form is locked**: a per-item request for an
   **undated** to-do emits a `VTODO` with only `UID` / `DTSTAMP` /
   `SUMMARY` (+`DESCRIPTION` when non-empty) — the §routes lane-1 pin —
   the register left this to the design doc's discretion; the feed lane's
   skip rule (§vtodo) applies to the *feed* only.
5. **The `LinkedTodoRow` shape is locked** (§D2 — `TodoId` / `Title` /
   `Status?` / `DueAt?` / `LinkPath`) — the register said "or reuse the
   existing `TodoRow`; U00 locks the exact shape"; this is the lock (the
   `TodoRow` is too heavy — it carries display fields a chip section does
   not render).

## §deferred lanes (ADR 0115 Consequences — each named, each "own ADR" /
own milestone)

- **Recurring events / `RRULE` — M18** (D6; the source of truth is
  `Milestones.cs` — M18 = "Recurring events — repeating events over the
  M4 events surface"; ADR 0112's "M14 is the natural home" + the
  `IcsWriter.cs` doc-comment "M14's home" refs are **stale forward-refs**
  — written before the roadmap settled M18 — and are superseded by this
  ADR + ADR 0115's Consequences).
- **`RRULE` on the `VTODO` surface** (a to-do's recurrence rides M18's
  recurrence concept, if that ever extends past events — own ADR).
- **ICS import** (round-tripping a calendar file *into* Kumunita — ADR
  0112's "own lane" deferral, unchanged — own ADR).
- **Per-component calendar feeds** (a `?component=` scoping of the
  `VTODO` feed — a scope generalization, own ADR; the M14 feed is the
  actor's visible set, the M12 feed shape).
- **Many-to-many event↔to-do** (if D1's 1:1 field were vetoed, the
  `EventTodoLink` doc is the alternative — **not vetoed**; the field
  stands, D1 locked).

## §gate — the three acceptance tests (recorded by U07)

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

