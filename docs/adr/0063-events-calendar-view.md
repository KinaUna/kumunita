---
id: adr-0063
title: "The events calendar (`EV-CAL`): a month-anchored, display-only overview of visible events"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/events-calendar-design.md]
supersedes: [adr-0054]
superseded_by: [adr-0064]
updated: 2026-11-02
---

# ADR [0063](0063-events-calendar-view.md) — The events calendar (`EV-CAL`): a month-anchored, display-only overview of visible events

Status: Accepted
Date: 2026-09-23
Amends: **0054** (Events) — extends the `IEventService` seam with one additive
read lane (`ListInRangeAsync`); the M4 surface is otherwise untouched.
Additive to **0019 / 0020** (the timezone / date-format machinery the anchor
and chip times use), **0015** (the `kw-l` key registry — the
`events.calendar.*` block), and **0031** (the plain-TS / no-dependency pin —
the `events-calendar.ts` module). **No** amendment to **0006** (no seam is
opened on the frozen cross-context interfaces — `IEventService` is M4's *own*
lane, so this ADD is permitted).

> **Abstract:** M4 (ADR [0054](0054-events-rsvp-reminders.md)) shipped the events **feed** — a chronological
> list that answers *"what's next"* but gives no **time overview** (a clash
> between two events is invisible; the last occurrence of a recurring kind of
> thing is behind the current page). The `EV-CAL` named lane (the `GP` / `TG`
> / `PG` precedent — a *named lane*, not a milestone renumber; **M5 stays
> Projects; M6 stays Portability**) closes that gap with a **second *view* of
> the same data**: a month-anchored calendar at `GET /events/calendar` — a
> rolling **30-day window** starting on the viewer's local month anchor, every
> event the viewer may already see rendered as chip(s) in its day column(s),
> **overlap pairs highlighted client-side**, and **prev/next/today
> navigation** to go back in time. It is the `TG` "by-tag browse" shape
> carried to *time*: a **view over already-authorized content**. **This lane is
> additive and display-only:** one read seam (`ListInRangeAsync`) on M4's own
> seam, one Web route, one view + view model (+ 2 additive defaulted
> `EventRow` fields), one `client/lib` TS module (tsc-only, zero
> dependencies), and 8 `kw-l` keys × 4 languages — and **nothing else**: no
> documents, no schema, no seeding, no write lane, no audit row kind, no §6.4
> job, no new dependency. The visibility split is **the service's, never
> re-derived in the Web** (ADR [0006](0006-module-boundary-contracts.md)-D); **overlap is a *display* concern**
> computed client-side, never an access decision. **For** a developer touching
> the `IEventService.ListInRangeAsync` seam, the `EventController.Calendar`
> action, the `Events/Calendar` view, or the `tests/*` calendar families. The
> seam signature, the route / anchor / window math, the client-side overlap
> detail, the view shape, the `kw-l` keys, the Web shape, the invariants, and
> the 13 pinned tests are in the [handoff
> note](../design/adr-handoffs.md#adr-0063--the-events-calendar--ev-cal--moved-detail).

## Context

M4 (ADR [0054](0054-events-rsvp-reminders.md)) shipped the events surface: the feed (`GET /events`, ordered by
`Start`, paged), the detail view, RSVPs, and the day-before reminder. The feed
answers *"what's next"* — but it is a **chronological list**, so it gives no
**time overview**. Two resident questions have no answer there:
*"Does the potluck clash with the cleanup day?"* (two events whose
`Start` / `End` instants intersect are just two rows, nowhere adjacent) and
*"When did we last hold the tool library day — is it time again?"* (the feed
is upcoming-only; the last occurrence is behind the current page, in no
navigable time frame).

The `EV-CAL` named lane closes that gap with a **second *view* of the same
data** (the `TG` "by-tag browse" shape carried to *time*). **The one thing this
ADR pins:** this lane is **additive and display-only.** It adds one read seam
(`ListInRangeAsync`) on M4's *own* seam, one Web route, one view + one view
model (+ 2 additive defaulted `EventRow` fields), one `client/lib` TS module
(tsc-only, zero dependencies), and 8 `kw-l` keys × 4 languages — and
**nothing else**: no documents, no schema, no seeding, no write lane, no audit
row kind, no §6.4 job, no editor work, no new dependency. The visibility split
is **the service's, never re-derived in the Web** (ADR [0006](0006-module-boundary-contracts.md)-D, the M4
`EventController` precedent): the calendar shows exactly what
`ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows. **Overlap is a
*display* concern** computed client-side over the already-authorized list — it
writes no row, calls no seam, and is never an access decision.

## Decision

- **D1 — Zero document changes.** No field is added to `Event`, `EventRsvp`,
  or `EventTranslation`; `EventDocTypes` is untouched; no
  `FeatureSchemaBase` migration; the boot paths are untouched; no EF. The
  calendar is a **view** — it reads the existing `Start` / `End` instants and
  nothing new (Invariant **C-EV·6**).
- **D2 — The one seam: `ListInRangeAsync`.** `IEventService` gains **exactly
  one** method (declared directly after `ListUpcomingAsync`):
  `Task<IReadOnlyList<Event>> ListInRangeAsync(DateTimeOffset windowStartUtc,
  DateTimeOffset windowEndUtc, string? componentId, string actorId,
  CancellationToken ct = default)`. The predicate is `Start >=
  windowStartUtc && Start < windowEndUtc` (inclusive start, exclusive end);
  the candidate filter + gate are verbatim the `ListUpcomingAsync` body
  (C-EV·1); the cap is `Take(30)` as a backstop (the 30-day span lives in the
  controller); one aggregate `AccessAudit` row per render (C-EV·2). See the
  handoff note for the full signature, parameter-order pin, predicate, and
  audit shape.
- **D3 — The route + anchor + window.** `GET /events/calendar` with
  `?from=YYYY-MM-DD` (the month anchor **in the viewer's effective timezone**,
  ADR [0019](0019-timezone-platform-default-and-resident-override.md); default = the zone's today) and `?componentId=…` (the feed's
  filter, a filter never a gate). Window = `[anchorLocalStartUtc,
  anchorLocalStartUtc + 30d)`; prev/next = the anchor shifted **±1 month**,
  pre-rendered as plain GET links (C-EV·7 — no client nav code). See the
  handoff note for the full anchor / window math.
- **D4 — Overlap is client-side display-only.** Two events overlap iff their
  `[startUtc, endUtc)` half-open intervals intersect, computed in the TS
  module **over the already-authorized row list** the view ships. The flag
  drives a CSS highlight + the registry-localized `events.calendar.overlap_
  hint`. It writes **no** row, calls **no** seam, is **never persisted**, and
  is **never an access decision** (C-EV·4). See the handoff note for the full
  detail.
- **D5 — The view shape.** `Views/Event/Calendar.cshtml` over
  `EventCalendarViewModel`: a **7-column day grid** of the 30 window-days;
  each event = **one chip per day-column it touches**; a chip = the event title
  (link to the detail page) + the `kw-dt`-rendered time. The header carries the
  month label, the prev/next/today nav, the `componentId` filter form, the
  "List" cross-link (C-EV·8), and an empty state. See the handoff note for the
  full shape.
- **D6 — The `kw-l` keys.** Eight new keys under `events.calendar.*`
  (`.title`, `.prev`, `.next`, `.today`, `.overlap_hint`, `.empty`,
  `.list_view`, `.from`) in **all four** seeded languages (en/de/fr/da). See
  the handoff note for the full list.
- **D7 — Out of scope (future lanes).** No year view; no event creation / RSVP
  from the calendar; no drag-to-reschedule; no iCal export (M6 owns it); no
  recurring-event model; no new notifications; no server-side overlap API.
  Any of these landing later is a **new named lane**, not an extension of this
  one. See the handoff note for the full catalog.
- **The Web shape + the invariants + the 13 pinned tests.** `EventRow` gains
  two additive defaulted fields (`StartUtc` / `EndUtc`);
  `EventCalendarViewModel` (the `TimeZoneId` is a display input only, never an
  authorization input — C-EV·5); `EventController.Calendar` (thin). The
  invariants **C-EV·1…C-EV·8** are pinned verbatim, and the **13 pinned tests**
  (10 seam + 3 Web) + the three-test acceptance gate are recorded by U09. See
  the handoff note for the full Web shape, invariants, and test list.

## Consequences

- **A resident gets the time overview the feed cannot give** — overlap-risk at
  a glance, and the ability to navigate back to when a recurring kind of thing
  last happened — with **zero** change to the data model, the authorization
  path, or the write surface.
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
  cross-context interfaces, no new dependency (the C-EV·6 + ADR [0031](0031-wysiwyg-editor-and-toolbar.md) pins) —
  verified by the part-vs-whole gate.
- **The roadmap stays honest.** `EV-CAL` opens as the single in-progress
  milestone (the `M5` pin moves to `Planned`, returning to `M5` at lane close)
  — the named-lane precedent, not a renumber.
