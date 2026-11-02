---
id: adr-0115
title: "Integration of Events and Projects (M14)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m14-events-projects-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR 0115 — Integration of Events and Projects (M14)

Status: Accepted

> **Abstract:** the two coordination surfaces — M4 events (ADR 0054) and
> M5 projects (ADR 0067: to-dos + Kanban boards) — **never point at each
> other** today (no `EventId` on `TodoItem`, no to-dos-in-a-calendar
> surface, no cross-surface display link). M14 composes them: one
> **additive `EventId?` field on `TodoItem`** (a field, not a link doc —
> **a feed filter / association, never a gate**, C-M14·1), **both display
> directions** (the to-do → event chip + the event → linked to-dos section,
> Web-layer composition over the frozen read seams, dangling-safe —
> C-M14·2), a **dedicated `set-event` write lane** (the
> `SetTodoProjectAsync` shape, standing creator ∪ assignee ∪ GlobalAdmin —
> C-M14·3), and the **`VTODO` calendar surface** (a BCL-only pure
> `TodoIcsWriter` — C-M14·5 — two `[Authorize]` `.ics` routes + two quiet
> `kw-l` affordances). **No** new authorization surface (C-M14·4), **no**
> `RRULE` / recurrence anywhere in M14 (C-M14·6 — **M18's** home, the
> source of truth is `Milestones.cs`). Both surfaces stay **frozen** — M14
> only composes the *existing* `Read` decisions and surfaces *links between
> content the caller already holds*. **For** a developer touching
> `Kumunita.Core.Projects` (the `EventId` field + the additive seams), the
> `TodoIcsWriter`, the `ProjectsController` `.ics` routes, or the
> `tests/*` M14 families. The `VTODO` closed subset, the "Not chosen" list,
> the superseded stale forward-refs, and the deferred-lane catalog are in
> the [handoff
> note](../design/adr-handoffs.md#adr-0115--integration-of-events-and-projects-m14-moved-detail).

## Context

M4 shipped events (ADR 0054); EV-CAL / EV-DWM / EV-NW / EV-PAST (ADR 0063 /
0064 / 0081 / 0109) shipped the in-app calendar; M12 (ADR 0112) gave the
residents' **calendar app** the events. M5 shipped projects (ADR 0067); the
PL lane (ADR 0086) added the `ProjectId` association; ADR 0079 added the
optional to-do dates `StartAt` / `DueAt`; ADR 0087 the blocker chip. The
README principles are explicit that the interlock **is the product**:
"**Integration over features.** … Kumunita's value is the *linkage* that
turns those parts into a whole, not the count of parts."

Today the two surfaces **never point at each other** (grep-confirmed at U00
against the live tree): there is **no `EventId` on `TodoItem`**, **no
to-dos-in-a-calendar surface**, and **no cross-surface display link**
anywhere. A resident preparing for the cleanup day cannot link the prep
to-dos to the event, and the to-dos — which already carry optional dates
(ADR 0079) — do not appear in **any** calendar even though the events do
(ADR 0112). The constraint that shapes the decision is the same one that
shaped M12: **a file that travels must not carry a decision** (the ADR 0028
posture; ADR 0112 C-M12·2), and a **link must never be a gate** (C-M3·2 /
C-PL·3, the `ProjectId` precedent). The recurrence question is **out of
scope for M14** — `Milestones.cs` (the source of truth) assigns recurring
events to **M18** — and ADR 0112's "recurring events (`RRULE` — **M14** is
the natural home)" forward-ref is a **stale** ref, written before the
roadmap settled M18 (see Consequences).

## Decision

- **D1 — One additive `EventId?` field on `TodoItem` (a field, not a link
  doc).** `public string? EventId { get; set; }` joins `TodoItem` beside
  `ProjectId`, carrying the same locked doc-comment rule — **a feed filter
  / association, never a gate** (C-M14·1 — C-M3·2 / C-PL·3 carried to the
  event link; the ADR 0086 D4 `ProjectId` shape). It is a **1:1
  association** (a to-do is *for* one event — the `ProjectId` "a to-do is
  *in* one project" precedent), **not** on `CreateTodoRequest` /
  `UpdateTodoRequest` (D3). One additive `(EventId)` single-field index
  joins the `ProjectId` / `BlockedByTodoId` indexes in the same **unnamed**
  form (the computed-index naming constraint). Additive-only,
  delta-detected, idempotent at boot, **zero migration for existing rows**
  (ADR 0004 §B.1 — existing rows read `EventId = null`).
- **D2 — Both display directions, as Web-layer composition over the frozen
  read seams.** **No** `Kumunita.Core.Events` → `Kumunita.Core.Projects`
  dependency (the ADR 0006 "a bounded context per concern" rule; the
  composition point for event → to-dos is the `EventController`, which
  already references both services). The **to-do → event** chip resolves
  through the **frozen** `IEventService.GetAsync`; the **event → linked
  to-dos** section resolves through the **new** additive reverse read seam
  `IProjectService.ListTodosForEventAsync` (the `ListTodosAsync` shape
  filtered to one event — the `ListBoardsForTodoAsync` "list X for a Y"
  reverse-read precedent). Both directions are **access-scoped +
  dangling-safe** (C-M14·2 — the `ProjectLink` / `BlockerChip` idiom): an
  unreadable / absent / soft-deleted target is **omitted entirely** (a
  `null` chip field), never a 404/403 for the source surface, and its
  title / id are **not leaked**.
- **D3 — The dedicated `set-event` write lane.**
  `IProjectService.SetTodoEventAsync` — the `SetTodoProjectAsync` shape
  verbatim. Standing: **creator ∪ assignee ∪ GlobalAdmin over the to-do**
  (C-M5·6 — the ADR 0014/0016/0017 matrix, re-checked **server-side**;
  C-M14·3). The to-do must be `!IsDeleted` (404); a non-existent /
  soft-deleted / non-readable `eventId` is **refused 404** (**you can only
  link to an event you can see** — the frozen
  `IAuthorizationService.CanAsync(Read)` path over the event); `null`
  **clears** the link; **one `AccessAudit` row** (`todo.set_event`,
  `TargetKind = "todo"`) commits **atomically** with the write (C3). The
  Web lane is `POST /projects/todos/{id}/set-event` on
  `ProjectsController`; the picker is seeded by the **frozen**
  `IEventService.ListMineAsync` (capped at 25 for the `<select>` — a
  display cap, not a gate).
- **D4 — The `VTODO` calendar surface.** The to-dos' existing optional
  dates (ADR 0079) become a standard iCal `VTODO` surface — a **BCL-only
  pure `TodoIcsWriter`** (the `IcsWriter` discipline, ADR 0112) with the
  **closed, pinned `VTODO` subset** (the `VCALENDAR` envelope verbatim +
  one `VTODO` per **dated** to-do with the pinned property order;
  **`UID`** = `kw-todo-{todo.Id}@kumunita` **stable**; **never emitted**:
  `ORGANIZER` / `ATTENDEE`, `RRULE`, `Audience` / grant / membership
  internals, app-internal state, and **any** `X-…` vendor property — the
  ADR 0028 posture + the ADR 0112 D1 "never" list). Two `[Authorize]`
  routes on the existing `ProjectsController`, both served
  `text/calendar; charset=utf-8` (`GET /projects/todos/{id}.ics` one
  `VTODO` + `GET /projects/todos.ics` the caller's visible **dated** set).
  Lane 1 inherits the frozen `GetTodoAsync`'s **404-not-403** split; lane 2
  is always `200` (an empty visible / dated set is a valid empty calendar).
  **No subscription token** — the feed rides the cookie, re-authorized every
  fetch (the M6 link-lane posture, ADR 0085 / ADR 0095). Route resolution:
  ASP.NET Core ranks the **literal** `"/projects/todos.ics"` route above
  the parameterized `"/projects/todos/{id}"`. Two quiet `kw-l` affordances
  × en/de/fr/da (the `projects.todos.ics.download` / `.feed` keys + the
  chip / picker / section labels — all six join the closed `kw-l` registry
  in **all four** languages). See the handoff note for the full `VTODO`
  closed subset.
- **D5 — No new authorization surface (C-M14·4).** M14 adds **no**
  `AccessAction`, **no** `AccessVia`, **no**
  `IAuthorizationService.Decide()` branch, **no** `Audience` change, **no**
  `IAuditableResource` adapter. All read lanes route through the
  **existing** `TodoItemToAuditableResource` / `EventToAuditableResource`
  adapters and the **frozen** `IAuthorizationService.CanAsync` /
  `CanSeeAsync(Read)` path. The `set-event` standing **reuses** C-M5·6; it
  does not invent a new one. M14 adds exactly one audit verb
  (`todo.set_event`), no other.
- **D6 — No `RRULE` / recurrence anywhere in M14 (C-M14·6).** The `Event`
  doc gains **no** recurrence field; the `VTODO` emitter emits a single
  `VTODO` per to-do. **`RRULE` is M18's home** — `Milestones.cs` (the
  source of truth) assigns "M18 — Recurring events" to **M18**. ADR 0112's
  "recurring events (`RRULE` — **M14** is the natural home)" forward-ref
  and the `IcsWriter.cs` doc-comment "M14's home" ref are **stale
  forward-refs**, written before the roadmap settled M18 — they are
  **superseded** by this ADR + its Consequences.
- **D7 — The pinned test names are the feedback loop.** The **Core** pure
  tests (no Testcontainers — the `TodoIcsWriter` is pure over POCOs) pin
  the closed `VTODO` subset, the conditionality, the fold + escape, the
  `UID` stability, the `STATUS:CANCELLED` capability, the
  empty-calendar-valid pin, and the never-emitted list. The
  **Testcontainers** pins pin `ListTodosForEventAsync` (candidate-filter +
  `CanSeeAsync(Read)` gate + `HasMore`) and `SetTodoEventAsync` (standing
  + `eventId`-visibility refusal + null-clear + audit row). The **Web**
  pins pin the display links' dangling-safe omission, the `set-event`
  action's CSRF + binding + redirect, and the two `.ics` routes' serve
  shape + 404-not-403 split + always-200 feed + the kw-l parity. The
  **three acceptance tests**: **Closed loop** / **Handoff** / **Part vs.
  whole**.
- **D8 — Docs flip in one unit (U07 close).** `Milestones.cs` (M14 →
  `StatusDone`, M15 → `StatusNext`), `README.md` Roadmap, `docs/STATUS.md`,
  `docs/ARCHITECTURE.md` (the M14 value-chain row → done), and
  `MilestonesTests` (re-pinned to **M15** as the single in-progress) all
  move in one unit (the C-M11·8 precedent verbatim) — the exact-order pin
  M0…M18 unchanged, the single-in-progress now M15.

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
  change propagates **out** live (strong consistency carried out of the
  platform); the cost is bounded **by the pin** (one field + one index, two
  additive seams, a pure emitter, two routes — C-M14·4/6/7).
- **Neutral / cost:** one additive nullable field + one index on `TodoItem`
  (zero migration — ADR 0004 §B.1 delta-detect); two more
  `IProjectService` seams (the frozen surface is extended, not re-scoped);
  the `.ics`-literal-in-`{id}` route resolution is the one known gotcha; a
  `DESCRIPTION` that references `![img](/content-image/{id})` /
  `[file](/attachment/{id})` renders as **plain text** in a calendar app
  (honest + portable, but the image does not render — the **chosen**
  behavior, D4); the to-do detail gains a fourth cross-reference (the FACES
  legibility trade — one labeled chip per cross-reference, empty-state when
  absent).
- **Not chosen:** a **many-to-many `EventTodoLink` doc** (D1's 1:1 field
  is simpler + matches the `ProjectId` precedent); a
  **`Kumunita.Core.Events` → `Kumunita.Core.Projects` dependency** (the ADR
  0006 rule); a **new authorization surface** (C-M14·4); a **subscription
  token** (the M6 link-lane posture wins); an **`ORGANIZER` / `ATTENDEE`**
  property (the ADR 0028 posture wins); a **`RRULE` / recurrence field**
  (C-M14·6 — **M18's** home). See the handoff note for the full list.
- **Superseded (stale forward-refs, written before the roadmap settled
  M18):** ADR 0112's Consequences "Deferred … **recurring events**
  (`RRULE` — **M14** is the natural home)" and the `IcsWriter.cs`
  doc-comment "M14's home" ref. Both are superseded by this ADR:
  **`RRULE` / recurrence is M18's home** (D6), **not** M14's — M14's scope
  is the interlock, not recurrence.
- **Deferred to their own lanes (each a named follow-on, own ADR / own
  milestone):** **recurring events / `RRULE`** (**M18** — the source of
  truth is `Milestones.cs`); **`RRULE` on the `VTODO` surface**; **ICS
  import**; **per-component calendar feeds**. See the handoff note for the
  full catalog.
