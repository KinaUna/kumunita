# ADR 0063 — The events calendar (`EV-CAL`): a month-anchored, display-only overview of visible events

Status: Accepted
Date: 2026-09-23
Amends: **0054** (Events) — extends the `IEventService` seam with one additive
read lane (`ListInRangeAsync`); the M4 surface is otherwise untouched.
Additive to **0019 / 0020** (the timezone / date-format machinery the anchor
and chip times use — the `EffectiveTimezoneResolver` + the one `kw-dt`
TagHelper), **0015** (the `kw-l` key registry — the `events.calendar.*` block),
and **0031** (the plain-TS / no-dependency pin — the `events-calendar.ts`
module). **No** amendment to **0006** (no seam is opened on the frozen
cross-context interfaces — `IEventService` is M4's *own* lane, so this ADD is
permitted; `IAuthorizationService` / `IUserInfoService` / `IIdentityService`
are untouched).

## Context

M4 (ADR 0054) shipped the events surface: the feed (`GET /events`, ordered by
`Start`, paged), the detail view, RSVPs, and the day-before reminder. The feed
answers *"what's next"* — but it is a **chronological list**, so it gives no
**time overview**. Two resident questions have no answer there:

- *"Does the potluck clash with the cleanup day?"* — two events whose
  `Start` / `End` instants intersect are just two rows, nowhere adjacent,
  nowhere highlighted.
- *"When did we last hold the tool library day — is it time again?"* — the
  feed is upcoming-only; the last occurrence is behind the current page, in no
  navigable time frame.

The `EV-CAL` named lane (the `GP` / `TG` / `PG` precedent — a *named lane*,
not a milestone renumber; **M5 stays Projects; M6 stays Portability**) closes
that gap with a **second *view* of the same data**: a month-anchored calendar
at `GET /events/calendar` — a rolling **30-day window** starting on the viewer's
local month anchor (default: today), every event the viewer may already see
rendered as chip(s) in its day column(s), **overlap pairs highlighted
client-side**, and **prev/next/today navigation** (shift the anchor ±1 month)
to go back in time. It is the `TG` "by-tag browse" shape carried to *time*: a
**view over already-authorized content**.

**The one thing this ADR pins:** this lane is **additive and display-only.**
It adds one read seam (`ListInRangeAsync`) on M4's *own* seam, one Web route,
one view + one view model (+ 2 additive defaulted `EventRow` fields), one
`client/lib` TS module (tsc-only, zero dependencies), and 8 `kw-l` keys × 4
languages — and **nothing else**: no documents, no schema, no seeding, no
write lane, no audit row kind, no §6.4 job, no editor work, no new dependency.
The visibility split is **the service's, never re-derived in the Web** (ADR
0006-D, the M4 `EventController` precedent): the calendar shows exactly what
`ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows. **Overlap is a *display*
concern** computed client-side over the already-authorized list — it writes no
row, calls no seam, and is never an access decision.

## Decision

### D1 — Zero document changes

No field is added to `Event`, `EventRsvp`, or `EventTranslation`; `M4DocTypes`
is untouched; no `FeatureSchemaBase` migration; the boot paths are untouched;
no EF. The calendar is a **view** — it reads the existing `Start` / `End`
instants and nothing new. The only additive C# in the whole lane is: one seam
(`ListInRangeAsync`) + its implementation, 2 additive defaulted `EventRow`
fields, one `EventCalendarViewModel`, one `Calendar` action, one view, one TS
module, one CSS rule, and the key block. (Invariant **C-EV·6**.)

### D2 — The one seam: `ListInRangeAsync`

`IEventService` gains **exactly one** method, declared **directly after
`ListUpcomingAsync`**:

```csharp
Task<IReadOnlyList<Event>> ListInRangeAsync(
    DateTimeOffset windowStartUtc,
    DateTimeOffset windowEndUtc,
    string? componentId,
    string actorId,
    CancellationToken ct = default);
```

- **Parameter order + types (locked):** `(windowStartUtc, windowEndUtc,
  componentId, actorId, ct)` — the window bounds lead (the query's primary
  predicate), with the feed's own two args (`componentId`, `actorId`) trailing;
  the bounds are **`DateTimeOffset`**, matching `Event.Start` / `Event.End`.
  The window replaces `ListUpcomingAsync`'s `page`.
- **Predicate:** `Start >= windowStartUtc && Start < windowEndUtc` — inclusive
  start, exclusive end; an event is in the window on the day it *starts* (a
  multi-day event's chip repetition across columns is a *display* concern — D5).
- **Candidate filter + gate (verbatim the `ListUpcomingAsync` body, C-EV·1):**
  `!IsDeleted && !IsDraft`, the optional `componentId` filter (C-M3·2 — a
  filter, never a gate), `OrderBy(Start)`, the **single**
  `CanSeeAsync(Read, …)` over `EventToAuditableResource` (standalone form),
  then the `visibleIds` filter.
- **Cap:** `Take(30)` via a private `const int WindowCap = 30;` on
  `EventService` next to `PageSize` (the `PageSize = 30` precedent re-purposed
  as a *window* bound). The **30-day span lives in the controller**
  (`windowEndUtc = windowStartUtc.AddDays(30)`) — the service's `Take(30)` is a
  **backstop**, not a policy (the service stays window-span-agnostic).
- **Audit (C-EV·2):** one aggregate `AccessAudit` row per render,
  `TargetKind = "event"`, from the single `CanSeeAsync` call — verbatim the
  feed's audit shape. No other `IEventService` / `EventService` method changes.

### D3 — The route + anchor + window

`GET /events/calendar` with `?from=YYYY-MM-DD` (the month anchor **in the
viewer's effective timezone**, the `EffectiveTimezoneResolver`'s zone, ADR
0019; default = the zone's today) and `?componentId=…` (the feed's filter
reused verbatim — a filter, never a gate, C-M3·2). Window =
`[anchorLocalStartUtc, anchorLocalStartUtc + 30d)` where
`anchorLocalStartUtc` = the anchor date at **local midnight** in the effective
zone, converted to UTC (the server-side math, in the controller — C-EV·5).
Prev/next = the anchor shifted **±1 month**, pre-rendered as plain GET links by
the server (C-EV·7 — no client nav code). Unparseable / missing `from` falls
back to the zone's today (a display fallback, not an error).

### D4 — Overlap is client-side display-only

Two events overlap iff their `[startUtc, endUtc)` half-open intervals
intersect. Computed in the TS module **over the already-authorized row list**
the view ships (each chip carries `data-start-utc` / `data-end-utc`); the flag
drives a CSS class (a ring / highlight) + the `kw-l` "overlaps" hint (`title`
attribute). It writes **no** row, calls **no** seam, is **never persisted**,
and is **never an access decision** (C-EV·4 — keeping it out of the service
keeps it from accreting).

### D5 — The view shape

`Views/Event/Calendar.cshtml` over `EventCalendarViewModel`: a **7-column day
grid** of the 30 window-days (Bootstrap shape, mirroring `Index.cshtml`); each
event = **one chip per day-column it touches** (the TS module distributes by
local day via `Intl.DateTimeFormat` + the effective zone id the view ships as a
**display value only** — C-EV·5); a chip = the event title (link to the detail
page) + the `kw-dt`-rendered time; empty days show the date label only. The
header carries the month label, the prev/next/today nav links, the
`componentId` filter form (reused from the feed), the "List" cross-link
(C-EV·8), and an empty state when the window has no visible events.

### D6 — The `kw-l` keys

Eight new keys under `events.calendar.*` in **all four** seeded languages
(en/de/fr/da) in `KnownTranslationKeys.cs`: `events.calendar.title`, `.prev`,
`.next`, `.today`, `.overlap_hint`, `.empty`, `.list_view`, `.from` — each
with an en floor text (the fallback in every view; the en text is
authoritative, the de/fr/da texts are U07's translations). The `events.*`
block is the host; the `tags.*` block is the precedent (ADR 0015).

### D7 — Out of scope (future lanes)

No year view; no event creation from the calendar; no RSVP from the calendar
(the detail page owns it); no drag-to-reschedule; no iCal export (**M6** owns
iCal); no recurring-event model (events are one-offs — "time to do it again?"
is answered by *seeing the last occurrence* in a previous month window, not by
a recurrence field); no new notifications; no server-side overlap API (overlap
stays a D4 display flag). Any of these landing later is a **new named lane**,
not an extension of this one.

### The Web shape (D2/D3/D5 host)

- **`EventRow`** gains exactly **two additive, defaulted** fields:
  `StartUtc` (`DateTimeOffset`, `= default`) + `EndUtc` (`DateTimeOffset`,
  `= default`) — the overlap + day-distribution inputs (C-EV·6). The feed
  action's existing call site compiles unchanged (the fields default); the
  calendar path sets them explicitly.
- **`EventCalendarViewModel`** (a new sealed record next to
  `EventIndexViewModel`):
  `(IReadOnlyList<EventRow> Events, string FromAnchor, string? PrevAnchor,
  string? NextAnchor, string MonthLabel, string? CurrentComponentId,
  IReadOnlyList<(string Id, string Name)> Components, string TimeZoneId)` —
  `PrevAnchor` / `NextAnchor` are `yyyy-MM-dd` strings (the ±1-month nav
  targets), `MonthLabel` is a UI-culture display string, and `TimeZoneId` is
  the effective zone id — a **display** input only, never an authorization
  input (C-EV·5).
- **`EventController.Calendar(string? from, string? componentId)`** —
  `[HttpGet("/events/calendar")]` under the controller's `[Authorize]`, thin:
  parse `from` in the effective zone (default today) → local midnight → UTC →
  `+30d`; call `ListInRangeAsync`; map rows to `EventRow`s setting
  `StartUtc` / `EndUtc` explicitly + the display names (read lookups, never a
  gate); build the view model. **No other action changes.**

### The invariants (pinned verbatim)

- **C-EV·1** — the calendar shows *exactly* what `ListUpcomingAsync` would for
  the same actor restricted to the window (same candidate filter, same gate,
  same draft/deleted exclusion).
- **C-EV·2** — one aggregate `AccessAudit` row per render (`TargetKind =
  "event"`, `visibleCount` / `hiddenCount`), from the single `CanSeeAsync`
  call.
- **C-EV·3** — the `componentId` query is a filter, never a gate (C-M3·2), and
  emits no row of its own (it rides the one aggregate row).
- **C-EV·4** — overlap is computed client-side over the authorized list; it
  writes no row, calls no seam, is never persisted, and is never an access
  decision.
- **C-EV·5** — the anchor is a date in the viewer's effective zone; the window
  math uses the `EffectiveTimezoneResolver`'s zone (ADR 0019); the zone id
  shipped to the TS module is a display input only, never an authorization
  input.
- **C-EV·6** — zero document / schema / seeding changes; the only additive C#
  is the one seam + 2 `EventRow` fields + the one view model + the one action
  + the one view + the one TS file + the CSS rule + the key block.
- **C-EV·7** — navigation is plain GET links (`from` shifted ±1 month); no
  POST, no client state, no round-trip JS for nav.
- **C-EV·8** — the list view is unchanged in behavior; the calendar is a
  second view; the list⇄calendar cross-links are the only `Index.cshtml` touch
  (one "Calendar" link).

### The 13 pinned tests

**10 seam tests** (`tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs`):
`EV_Range_IncludesEventStartingInWindow`,
`EV_Range_ExcludesEventStartingBeforeWindow`,
`EV_Range_ExcludesEventStartingOnWindowEnd_Exclusive`,
`EV_Range_DraftInvisibleToNonAuthor`, `EV_Range_DraftVisibleToAuthor`,
`EV_Range_DeletedExcludedForEveryone`, `EV_Range_AudienceMemberSeesEvent`,
`EV_Range_NonMemberDenied_NoLeak`,
`EV_Range_AggregateAuditRowShape_TargetKindEvent`,
`EV_Range_ComponentFilterIsFilterNotGate`.

**3 Web pins** (appended to `tests/Kumunita.Web.Tests/EventControllerTests.cs`):
`Calendar_DefaultFromIsTodayInEffectiveZone`,
`Calendar_FromShiftsWindow_AndPrevNextLinks`,
`Calendar_PassesComponentFilter_ToSeam`.

**The three-test acceptance gate** (recorded by the lane's U09): **closed
loop** (an author's published event in the window appears in their calendar
with the right chip + one aggregate row), **handoff** (a group member added to
the audience sees the event on the next render — the non-member's calendar
still excludes it), and **part-vs-whole** (the 13 pinned tests pass together
with the full M4 `EventServiceTests` suite still green — the lane is additive,
nothing regressed).

## Consequences

- **A resident gets the time overview the feed cannot give** — overlap-risk
  at a glance, and the ability to navigate back to when a recurring kind of
  thing last happened — with **zero** change to the data model, the
  authorization path, or the write surface.
- **The visibility guarantee is free and exact.** The calendar's candidate set
  is *exactly* the feed's restricted to the window, through the *same*
  `CanSeeAsync(Read)` gate and the *same* `EventToAuditableResource` adapter,
  so a calendar page leaks nothing a feed page would not (C-EV·1) and writes
  the same one aggregate audit row (C-EV·2).
- **Overlap stays out of the service.** It is a client-side display flag over
  already-authorized data — it cannot become an access decision, a persisted
  row, or a service concept (C-EV·4).
- **The M4 surface is untouched except the one additive seam.** No field on
  any document, no new `AccessAction` / `AccessVia`, no seam on the frozen
  cross-context interfaces, no new dependency (the C-EV·6 + ADR 0031 pins) —
  verified by the part-vs-whole gate.
- **The roadmap stays honest.** `EV-CAL` opens as the single in-progress
  milestone (the `M5` pin moves to `Planned`, returning to `M5` at lane close)
  — the named-lane precedent, not a renumber.
