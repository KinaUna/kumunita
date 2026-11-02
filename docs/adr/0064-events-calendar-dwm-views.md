---
id: adr-0064
title: "The events calendar Day/Week/Month views (`EV-DWM`): the three named views over the same authorized events"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/events-calendar-dwm-design.md]
supersedes: [adr-0063]
superseded_by: []
updated: 2026-11-02
---

# ADR [0064](0064-events-calendar-dwm-views.md) — The events calendar Day/Week/Month views (`EV-DWM`): the three named views over the same authorized events

Status: Accepted
Date: 2026-09-23
Amends: **0063** (the `EV-CAL` events calendar this extends — the `?view=`
param + per-view window + Day/Week time grids; the `ListInRangeAsync` seam
is *reused, unchanged*). Additive to **0019 / 0020** (the timezone /
date-format machinery the anchor + labels use), **0015** (the `kw-l` key
registry — the 3 new `events.calendar.view.*` keys), and **0031** (the
plain-TS / no-dependency pin — the new `events-calendar-time.ts` module).
**No** amendment to **0006** (no seam is opened on the frozen cross-context
interfaces — `IEventService` is *reused, unchanged*).

> **Abstract:** this lane is **additive and display-only — zero Core
> change.** It adds one `?view=` query param on the existing
> `GET /events/calendar` route (day / week / month, `month` the default —
> backward-compatible), two additive view-model fields (`View` +
> `WindowDays`, `MonthLabel` generalized to a view-appropriate `Label`),
> one new plain-TS module (`events-calendar-time.ts`, tsc-only, zero
> dependencies), a few `site.css` rules, and 3 `kw-l` keys × 4 languages —
> and **nothing else**: no documents, no schema, no seeding, no write
> lane, no new audit row shape, no new bounded context, **no seam on
> `IEventService` / `EventService`** (the `ListInRangeAsync` seam is
> *already* window-agnostic — D1), no new dependency. The visibility split
> is **the service's, never re-derived in the Web** (ADR [0006](0006-module-boundary-contracts.md)-D, the
> EV-CAL precedent): each view shows exactly what
> `ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window.
> **The `?view=` selector and the window are *display* inputs — never an
> access decision.** M5 stays Projects; M6 stays Portability. **For** a
> developer touching `EventCalendarViewModel`, `EventController.Calendar`,
> `client/lib/events-calendar-time.ts`, or the `tests/*` DWM families. The
> per-view window, the C-DWM·1–9 invariants, and the 9 pinned tests are in
> the [handoff
> note](../design/adr-handoffs.md#adr-0064--the-events-calendar-dwm-views-moved-detail).

## Context

ADR [0063](0063-events-calendar-view.md) (`EV-CAL`) gave residents a time overview the feed cannot: a
month-anchored calendar at `GET /events/calendar`, a rolling **30-day chip
grid**, client-side overlap highlighting, and prev/next/today navigation.
But a resident opening that page expects the **three views every calendar
app has** — the Outlook / Google Calendar mental model: **Day** —
*"what's happening *today*?"* the anchor day as a time ruler; **Week** —
*"does the potluck land on the same *day* as the cleanup?"* the anchor's
week as a 7-column time grid; **Month** — *"the *shape* of the month"* the
anchor's **calendar month** as a 5–6 week × 7 day grid. The shipped 30-day
grid is *close* to a month but is not one (a flat 30-day run, not the
calendar month), and there is no Day or Week at all.

**The one thing this ADR pins:** this lane is **additive and display-only —
zero Core change** — and **nothing else**: no documents, no schema, no
seeding, no write lane, no new audit row shape, no new bounded context,
**no seam on `IEventService` / `EventService`** (the `ListInRangeAsync`
seam is *already* window-agnostic — D1), no new dependency. The visibility
split is **the service's, never re-derived in the Web** (ADR [0006](0006-module-boundary-contracts.md)-D, the
EV-CAL precedent): each view shows exactly what
`ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window.
**The `?view=` selector and the window are *display* inputs — never an
access decision.**

## Decision

- **D1 — Zero Core change (the headline decision).** No field is added to
  `Event`, `EventRsvp`, or `EventTranslation`; `EventDocTypes` is
  untouched; no `FeatureSchemaBase` migration; the boot paths are
  untouched; no EF; **no** seam added on `IEventService` or
  `IAuthorizationService` / `IUserInfoService` / `IIdentityService`. The
  `ListInRangeAsync` seam is **reused, unchanged** — it is *already*
  window-agnostic (ADR [0063](0063-events-calendar-view.md) D2: it accepts arbitrary `[startUtc, endUtc)`
  UTC bounds), so day / week / month windows are **caller policy in the
  controller**. The **only** additive C# in the whole lane is: the additive
  `EventCalendarViewModel` fields (`View`, `WindowDays`,
  `MonthLabel`→`Label`), the `?view=` param on the existing `Calendar`
  action, and the view / TS / CSS / key changes. (Invariant
  **C-DWM·1**.)
- **D2 — The `?view=` selector + per-view window.**
  `?view=day|week|month` on the **existing** `GET /events/calendar` route
  (no new route — C-DWM·8). The window is computed **in the controller**,
  in the effective zone (ADR 0019): **`day`** — `[anchorLocalMidnightUtc,
  +1d)`; **`week`** — the anchor's **Monday-start** week (7 days);
  **`month`** — the anchor's **calendar month** (a real, variable span —
  the controller's `AddMonths(1)` handles it). **`month` is the default**
  — a missing `?view` renders exactly the shipped EV-CAL behavior
  reframed to the calendar month (C-DWM·8). An unparseable / out-of-set
  `?view` value **falls back to `month`** — a display fallback, not an
  error (C-DWM·3). The `?from=` anchor and `?componentId=` filter keep
  their EV-CAL semantics (`componentId` a filter, never a gate). See the
  handoff note for the full per-view window.
- **D3 — Day + Week as time-ruler grids.** Day and Week are **time-ruler
  grids**: a `00:00`–`24:00` hour-ruler column plus one day-column per
  shown day (Day = 1 column, Week = 7 columns, Monday-first), the hour rows
  rendered **server-side** as the grid skeleton. Event blocks are
  **positioned by `%` top/height within their day column** — computed
  **client-side** in the new `client/lib/events-calendar-time.ts` **over
  the already-authorized chip list** the view ships; the module also
  mirrors the EV-CAL overlap flag. **Multi-day events repeat per touched
  day**. All of it is a client-side display concern (C-DWM·4): it writes
  no row, calls no seam, is never persisted, and is never an access
  decision.
- **D4 — Month reframed to a true calendar month.** The anchor's
  **calendar month** as a **5–6 week × 7 day grid** (the month's days plus
  the leading/trailing days of the adjacent months, marked as outside the
  month, so the grid is always full weeks). The **existing**
  chip-distribution + overlap logic in `events-calendar.ts` is **reused
  as-is** because it is view-agnostic. The shipped 30-day flat run becomes
  the calendar month — the backward-compatible default view (C-DWM·8).
- **D5 — The Day/Week/Month toggle + navigation.** The header carries
  **three view buttons** (Day / Week / Month), each a **plain GET link**
  with `?view=` + the current `?from=` + `?componentId=` (the active one
  rendered pressed/disabled). **Prev/next shift the anchor by the view's
  unit** — ±1 day (`day`), ±1 week (`week`, preserving the Monday-start
  alignment), ±1 month (`month`) — pre-rendered as plain GET links by the
  server (C-DWM·6). **Today** resets the anchor to the zone's today and
  preserves `?view=`. No POST, no client state, no round-trip JS for nav —
  the server re-renders and the authorization re-runs per request
  (C-EV·7 carried). The view-appropriate **`Label`** is a computed display
  string rendered through the existing `kw-dt` TagHelper / UI culture —
  **not** a registry key (D6).
- **D6 — The `kw-l` keys.** Exactly **3 new keys** under
  `events.calendar.view.*` in **all four** seeded languages
  (en/de/fr/da) in `KnownTranslationKeys.cs`:
  `events.calendar.view.day`, `events.calendar.view.week`,
  `events.calendar.view.month`. The existing `events.calendar.*` block is
  the host; the ADR [0015](0015-ui-view-localization-mechanics.md) registry shape holds. The view **labels** (a full
  date / a date range / a month name) are **computed display strings**
  rendered through `kw-dt` / the UI culture — dates, not UI nouns, so they
  are **not** registry keys (C-DWM·9).
- **D7 — Out of scope (future lanes).** No event creation from the
  calendar; no RSVP from the calendar (the detail page owns RSVP — ADR
  0063 D7 unchanged); no drag-to-reschedule; no iCal export (**M6** owns
  iCal); no recurring-event model; no **year** view; no **multi-event
  column packing / overlap-lane algorithm**; **no week-start resident
  override** (the platform default is **Monday**); no new notifications;
  no server-side overlap or time-positioning API. Any of these landing
  later is a **new named lane**, not an extension of this one.

## Consequences

- **A resident gets the three named views every calendar app has** — Day,
  Week, and Month — over the same authorized events, with **zero** change
  to the data model, the authorization path, or the write surface.
- **The visibility guarantee is free and exact.** Each view's candidate set
  is *exactly* the seam's restricted to that view's window, through the
  *same* `CanSeeAsync(Read)` gate and the *same*
  `EventToAuditableResource` adapter, so a day/week/month page leaks
  nothing a feed or a month page would not (C-DWM·2) and writes the same
  one aggregate audit row (C-DWM·2).
- **The `?view=` selector and the window stay out of access.** They are
  display inputs only — the selector is not part of any `CanSeeAsync` call
  and writes no row (C-DWM·3); overlap + time-positioning are
  client-side display concerns over already-authorized data (C-DWM·4) —
  they cannot become access decisions, persisted rows, or service
  concepts.
- **The M4 + EV-CAL surface is untouched except the additive view-model
  fields + the `?view=` param.** No field on any document, no new
  `AccessAction` / `AccessVia`, no seam on the frozen cross-context
  interfaces, no new dependency (the C-DWM·1 + ADR [0031](0031-wysiwyg-editor-and-toolbar.md) pins) — verified by
  the part-vs-whole gate.
- **The roadmap stays honest.** `EV-DWM` opens as the single in-progress
  milestone (the `M5` pin moves to `Planned`, returning to `M5` at lane
  close) — the named-lane precedent, not a renumber. The C-DWM·1–9
  invariants and the 9 pinned tests are pinned in the handoff note.
