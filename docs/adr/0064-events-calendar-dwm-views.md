# ADR 0064 — The events calendar Day/Week/Month views (`EV-DWM`): the three named views over the same authorized events

Status: Accepted
Date: 2026-09-23
Amends: **0063** (the `EV-CAL` events calendar this extends — the `?view=` param + per-view window + Day/Week time grids; the `ListInRangeAsync` seam is *reused, unchanged*).
Additive to **0019 / 0020** (the timezone / date-format machinery the anchor + labels use — the `EffectiveTimezoneResolver` + the one `kw-dt` TagHelper), **0015** (the `kw-l` key registry — the 3 new `events.calendar.view.*` keys), and **0031** (the plain-TS / no-dependency pin — the new `events-calendar-time.ts` module). **No** amendment to **0006** (no seam is opened on the frozen cross-context interfaces — `IEventService` is *reused, unchanged*; `IAuthorizationService` / `IUserInfoService` / `IIdentityService` are untouched).

## Context

ADR 0063 (`EV-CAL`) gave residents a time overview the feed cannot: a
month-anchored calendar at `GET /events/calendar`, a rolling **30-day chip
grid**, client-side overlap highlighting, and prev/next/today navigation. But a
resident opening that page expects the **three views every calendar app has** —
the Outlook / Google Calendar mental model:

- **Day** — *"what's happening *today*?"* the anchor day as a time ruler.
- **Week** — *"does the potluck land on the same *day* as the cleanup?"* the
  anchor's week as a 7-column time grid.
- **Month** — *"the *shape* of the month, with the edges of the
  previous/next month around it"* the anchor's **calendar month** as a
  5–6 week × 7 day grid.

The shipped 30-day grid is *close* to a month but is not one (a flat 30-day
run, not the calendar month), and there is no Day or Week at all.

**The one thing this ADR pins:** this lane is **additive and display-only —
zero Core change.** It adds one `?view=` query param on the existing route,
two additive view-model fields (`View` + `WindowDays`, and `MonthLabel`
generalized to a view-appropriate `Label`), one new plain-TS module
(`events-calendar-time.ts`, tsc-only, zero dependencies), a few `site.css`
rules, and 3 `kw-l` keys × 4 languages — and **nothing else**: no documents, no
schema, no seeding, no write lane, no new audit row shape, no new bounded
context, **no seam on `IEventService` / `EventService`** (the
`ListInRangeAsync` seam is *already* window-agnostic — D1), no new dependency.
The visibility split is **the service's, never re-derived in the Web** (ADR
0006-D, the EV-CAL precedent): each view shows exactly what
`ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window. **The
`?view=` selector and the window are *display* inputs — never an access
decision.** M5 stays Projects; M6 stays Portability.

## Decision

### D1 — Zero Core change (the headline decision)

No field is added to `Event`, `EventRsvp`, or `EventTranslation`; `M4DocTypes`
is untouched; no `FeatureSchemaBase` migration; the boot paths are untouched;
no EF; **no** seam added on `IEventService` or
`IAuthorizationService` / `IUserInfoService` / `IIdentityService`. The
`ListInRangeAsync` seam is **reused, unchanged** — it is *already*
window-agnostic (ADR 0063 D2: it accepts arbitrary `[startUtc, endUtc)` UTC
bounds, the candidate filter `!IsDeleted && !IsDraft` + optional
`ComponentId`, and the single `CanSeeAsync(Read)` gate over
`EventToAuditableResource` are all window-independent), so day / week / month
windows are **caller policy in the controller**. The **only** additive C# in
the whole lane is: the additive `EventCalendarViewModel` fields (`View`,
`WindowDays`, and `MonthLabel` generalized to a view-appropriate `Label`),
the `?view=` param on the existing `Calendar` action, and the view / TS / CSS
/ key changes. (Invariant **C-DWM·1**.)

### D2 — The `?view=` selector + per-view window

`?view=day|week|month` on the **existing** `GET /events/calendar` route (no
new route — C-DWM·8). The window is computed **in the controller**, in the
effective zone (ADR 0019), as the anchor's local start → UTC:

- **`day`** — `[anchorLocalMidnightUtc, +1d)`;
- **`week`** — the anchor's **Monday-start** week: 7 days from the Monday on
  or before the anchor (`[mondayLocalMidnightUtc, +7d)`, C-DWM·5);
- **`month`** — the anchor's **calendar month**: from local midnight of the
  1st to local midnight of the 1st of the next month (a real, variable span —
  February shorter, etc.; the controller's `AddMonths(1)` handles it).

**`month` is the default** — a missing `?view` renders exactly the shipped
EV-CAL behavior reframed to the calendar month (C-DWM·8). An unparseable /
out-of-set `?view` value **falls back to `month`** — a display fallback, not
an error (C-DWM·3). The `?from=` anchor and `?componentId=` filter keep their
EV-CAL semantics (`componentId` a filter, never a gate — C-M3·2 / C-EV·3).

### D3 — Day + Week as time-ruler grids

Day and Week are **time-ruler grids**: a `00:00`–`24:00` hour-ruler column
plus one day-column per shown day (Day = 1 column, Week = 7 columns,
Monday-first), the hour rows rendered **server-side** as the grid skeleton.
Event blocks are **positioned by `%` top/height within their day column** —
computed **client-side** in the new `client/lib/events-calendar-time.ts`
**over the already-authorized chip list** the view ships (each chip carries
`data-start-utc` / `data-end-utc`); the module also mirrors the EV-CAL overlap
flag (the `[startUtc, endUtc)` intersection → highlight + hint). **Multi-day
events repeat per touched day** — the same `[startUtc, endUtc)` interval rule
as Month (one block per day-column the interval touches, each clipped to its
day's 24h range). All of it is a client-side display concern (C-DWM·4): it
writes no row, calls no seam, is never persisted, and is never an access
decision.

### D4 — Month reframed to a true calendar month

The anchor's **calendar month** as a **5–6 week × 7 day grid** (the month's
days plus the leading/trailing days of the adjacent months, marked as outside
the month, so the grid is always full weeks). The **existing** chip-
distribution + overlap logic in `events-calendar.ts` is **reused as-is**
because it is view-agnostic — it iterates whatever day-columns the view
renders, and the chip pool / `data-*` channel is unchanged. The shipped 30-day
flat run becomes the calendar month — the backward-compatible default view
(C-DWM·8), with the *data window* changing from a flat 30 days to the
calendar month's span (D2).

### D5 — The Day/Week/Month toggle + navigation

The header carries **three view buttons** (Day / Week / Month), each a **plain
GET link** with `?view=` + the current `?from=` + `?componentId=` (the active
one rendered pressed/disabled). **Prev/next shift the anchor by the view's
unit** — ±1 day (`day`), ±1 week (`week`, preserving the Monday-start
alignment), ±1 month (`month`) — pre-rendered as plain GET links by the
server, `?view=` + `?componentId=` riding along (C-DWM·6). **Today** resets the
anchor to the zone's today and preserves `?view=`. No POST, no client state,
no round-trip JS for nav — the server re-renders and the authorization
re-runs per request (C-EV·7 carried). The view-appropriate **`Label`** (a full
date for day, a date range for week, a month name for month) is a computed
display string rendered through the existing `kw-dt` TagHelper / UI culture —
**not** a registry key (D6).

### D6 — The `kw-l` keys

Exactly **3 new keys** under `events.calendar.view.*` in **all four** seeded
languages (en/de/fr/da) in `KnownTranslationKeys.cs`:
`events.calendar.view.day`, `events.calendar.view.week`,
`events.calendar.view.month` — each with an en floor text (the fallback in
every view; the en text is authoritative, the de/fr/da texts are the
translations). The existing `events.calendar.*` block is the host; the ADR
0015 registry shape holds. The view **labels** (a full date / a date range /
a month name) are **computed display strings** rendered through `kw-dt` / the
UI culture — dates, not UI nouns, so they are **not** registry keys (C-DWM·9).

### D7 — Out of scope (future lanes)

No event creation from the calendar; no RSVP from the calendar (the detail
page owns RSVP — ADR 0063 D7 unchanged); no drag-to-reschedule; no iCal
export (**M6** owns iCal); no recurring-event model; no **year** view; no
**multi-event column packing / overlap-lane algorithm** (overlapping blocks
simply render side-by-side or stacked — a display nicety, a future lane);
**no week-start resident override** (the platform default is **Monday**; a
per-resident week-start is a new override surface → a future lane — it does
not exist today); no new notifications; no server-side overlap or
time-positioning API (both stay D3 client-side display concerns). Any of these
landing later is a **new named lane**, not an extension of this one.

### The Web shape (D2/D5 host)

- **`EventCalendarViewModel`** gains **exactly two additive fields**
  (defaulted, so no other call site breaks) + a **generalization**:
  `(IReadOnlyList<EventRow> Events, string FromAnchor, string? PrevAnchor,
  string? NextAnchor, string Label, string? CurrentComponentId,
  IReadOnlyList<(string Id, string Name)> Components, string TimeZoneId,
  string View = "month", IReadOnlyList<DateTime> WindowDays = null!)` —
  `Label` was `MonthLabel` (now view-appropriate: a full date for Day, a date
  range for Week, a month name for Month), `View` echoes the resolved
  `?view=` back to the view (the active toggle button), and `WindowDays` is
  the ordered list of the view's anchor days (the grid's columns — day: 1;
  week: 7 Monday-first; month: the 5–6 weeks covering the calendar month).
- **`EventController.Calendar(string? from, string? componentId, string?
  view)`** — `[HttpGet("/events/calendar")]`, thin: parse `view` (default +
  invalid/out-of-set fallback = `"month"`); parse `from` (default today in the
  effective zone) → the anchor; compute the per-view window (day / week
  Monday-start / month calendar-month, D2); call `ListInRangeAsync` (the seam
  is **unchanged** — D1); map rows; build the view model with `View` /
  `WindowDays` / `Label`; pre-render the ±(view-unit) nav anchors as plain GET
  links with `?view=` + `?componentId=` riding along (D5 / C-DWM·6). **No other
  action changes; no seam on `IEventService` is touched.**
- **`client/lib/events-calendar-time.ts`** (new) — the hour-ruler + time-block
  positioning + overlap, self-contained, tsc-only, zero dependencies (ADR
  0031). The existing `client/lib/events-calendar.ts` is **not modified**
  (Month reuses it — its distribution + overlap are view-agnostic; C-DWM·7).
- **`site.css`** — the `events-calendar*` rules are extended with the
  time-grid + toggle rules (D3 / D5).

### The invariants (pinned verbatim)

- **C-DWM·1** — Zero Core change. No seam added, no `Event` / `EventRsvp` /
  `EventTranslation` field, no `M4DocTypes` / boot-path change, no EF, no new
  audit row shape. The only additive C# is the additive view-model fields
  (`View` + `WindowDays`, `MonthLabel`→`Label`) and the `?view=` param on the
  existing `Calendar` action. The `ListInRangeAsync` seam is **reused,
  unchanged** — it is already window-agnostic.
- **C-DWM·2** — Same visibility as EV-CAL. Each view shows *exactly* what
  `ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window: the
  same candidate filter (`!IsDeleted && !IsDraft`, optional `ComponentId`),
  the same single gate over the same `EventToAuditableResource`, the same one
  aggregate `AccessAudit` row (`TargetKind = "event"`). A day/week/month page
  leaks nothing a feed or a month page would not.
- **C-DWM·3** — `?view=` is a display selector, never an access input. The
  selector only chooses the window the controller asks the seam for; it is not
  part of any `CanSeeAsync` call and writes no row of its own. The window
  (1 day / 7 days / the calendar month) is **caller policy in the controller**;
  the seam stays window-agnostic. An unparseable / missing / out-of-set
  `?view` **falls back to `month`** (a display fallback, not an error).
- **C-DWM·4** — Overlap + time-positioning are client-side display concerns.
  Computed over the already-authorized chip list the view ships; they write no
  row, call no seam, are never persisted, and are never an access decision.
- **C-DWM·5** — Week starts Monday; the zone is display-only. The anchor and
  all window bounds use the `EffectiveTimezoneResolver`'s zone (ADR 0019); the
  week is the anchor's **Monday-start** week (platform default,
  locale-independent); the zone id shipped to TS is a **display** input only
  (the browser formats via `Intl`), **never an authorization input**.
- **C-DWM·6** — Navigation is plain GET links. Prev/next/today shift the anchor
  by the **view's unit** (±1 day / ±1 week / ±1 month), `?view=` rides along
  (and `?componentId=`), all pre-rendered by the server; no POST, no client
  state, no round-trip JS for nav — the server re-renders and the
  authorization re-runs per request.
- **C-DWM·7** — Plain TS, zero deps, EV-CAL module untouched. tsc-only (ADR
  0031); **no calendar library, no bundler**; the existing
  `client/lib/events-calendar.ts` is **not modified** (Month reuses it);
  Day/Week add the new `client/lib/events-calendar-time.ts`.
- **C-DWM·8** — Backward-compatible; list view unchanged. `?view=month` is the
  **default** (no `?view` ⇒ month), preserving the shipped EV-CAL behavior; the
  list view is unchanged; the list⇄calendar cross-link is the only
  `Index.cshtml` touch (one "Calendar" link, already present).
- **C-DWM·9** — Localization parity. The 3 new `kw-l` keys
  (`events.calendar.view.day/.week/.month`) are present in **all four** seeded
  languages (en/de/fr/da); English is the fallback in every view; the
  `events.calendar.*` block is the host. The view labels are computed display
  strings, not registry keys.

### The 9 pinned tests

**2 Core additive pins** (`tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs`
— the lane adds **no** seam; these re-exercise the *existing*
`ListInRangeAsync` with narrower windows to prove C-DWM·1's window-agnostic
claim): `EV_Range_OneDayWindow_OnlyThatDay`,
`EV_Range_SevenDayWindow_OnlyThoseDays`.

**7 Web pins** (appended to `tests/Kumunita.Web.Tests/EventControllerTests.cs`):
`Calendar_DefaultViewIsMonth_BackwardCompat`,
`Calendar_ViewDay_WindowIsAnchorDayOnly`,
`Calendar_ViewWeek_WindowIsAnchorWeek_MondayStart`,
`Calendar_ViewMonth_WindowIsAnchorCalendarMonth`,
`Calendar_InvalidViewFallsBackToMonth`,
`Calendar_ViewRidesAlongInPrevNextNavLinks`,
`Calendar_Label_IsViewAppropriate`.

**The three-test acceptance gate** (recorded by the lane's U07): **closed
loop** (an author's published event on the anchor day appears in **Day**,
**Week**, and **Month**, each with the right chip/block + one aggregate row),
**handoff** (a group member added to the audience sees the event in all three
views on the next render — the non-member's views still exclude it), and
**part-vs-whole** (the 9 pinned tests pass together with the full M4
`EventServiceTests` + the existing EV-CAL seam + Web pin suites green — the
lane is additive, nothing regressed).

## Consequences

- **A resident gets the three named views every calendar app has** — Day,
  Week, and Month — over the same authorized events, with **zero** change to
  the data model, the authorization path, or the write surface.
- **The visibility guarantee is free and exact.** Each view's candidate set is
  *exactly* the seam's restricted to that view's window, through the *same*
  `CanSeeAsync(Read)` gate and the *same* `EventToAuditableResource` adapter,
  so a day/week/month page leaks nothing a feed or a month page would not
  (C-DWM·2) and writes the same one aggregate audit row (C-DWM·2).
- **The `?view=` selector and the window stay out of access.** They are
  display inputs only — the selector is not part of any `CanSeeAsync` call and
  writes no row (C-DWM·3); overlap + time-positioning are client-side display
  concerns over already-authorized data (C-DWM·4) — they cannot become access
  decisions, persisted rows, or service concepts.
- **The M4 + EV-CAL surface is untouched except the additive view-model fields
  + the `?view=` param.** No field on any document, no new `AccessAction` /
  `AccessVia`, no seam on the frozen cross-context interfaces, no new
  dependency (the C-DWM·1 + ADR 0031 pins) — verified by the part-vs-whole
  gate.
- **The roadmap stays honest.** `EV-DWM` opens as the single in-progress
  milestone (the `M5` pin moves to `Planned`, returning to `M5` at lane close)
  — the named-lane precedent, not a renumber.
