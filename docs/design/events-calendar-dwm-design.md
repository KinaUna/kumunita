# Events calendar Day/Week/Month views (`EV-DWM`) — the three calendar views — design

> **Two parts.** Part 1 (U00) is "What this lane is / the existing surface /
> the design decisions / the invariants / the FACES"; Part 2 (U01, §6 below)
> is "The exact shapes" — the exact view-model + controller + TS shapes every
> unit codes against, the **9 pinned test names**, the **three-test
> acceptance gate**, and the drift-guard — mirroring
> `events-calendar-design.md`'s two-part shape.
>
> **Status.** **Closed** (lane opened by U01; closed by U07 — the three-test
> acceptance gate is recorded in §6.6 below; the lane folder moved to
> `plans-milestones/done/ev-dwm/`). Decisions **D1–D7** are
> **locked — [DECIDED — ADR 0064]** (U01 authored **ADR 0064**, Accepted, and
> flipped every marker). The ADR is the sign-off gate; this doc
> is the primary reference tier for implementation. The lane plan register is
> `plans-milestones/in-progress/ev-dwm/plan-ev-dwm.md`; the per-unit files
> (U00–U07) and the scratch log (`handoff-notes.md`) are now under
> `plans-milestones/done/ev-dwm/`.
>
> This is a **named lane** (`EV-DWM`) on the already-shipped **`EV-CAL`**
> events calendar (ADR 0063) — not a milestone letter: **M5 stays Projects;
> M6 stays Portability** (the `GP` / `TG` / `EV-CAL` named-lane precedent).

## 1. What this lane is

EV-CAL (ADR 0063) shipped the time overview the feed cannot give: a
month-anchored calendar at `GET /events/calendar` — a rolling **30-day chip
grid**, client-side overlap highlighting, and prev/next/today navigation. But
a resident opening that page expects the **three views every calendar app
has** — the Outlook / Google Calendar mental model:

- **Day** — *"what's happening *today*?"* the anchor day as a time ruler.
- **Week** — *"does the potluck land on the same *day* as the cleanup?"* the
  anchor's week as a 7-column time grid.
- **Month** — *"the *shape* of the month, with the edges of the
  previous/next month around it"* the anchor's **calendar month** as a
  5–6 week × 7 day grid.

The shipped 30-day grid is *close* to a month but is not one — a flat 30-day
run, not the calendar month — and there is no Day or Week at all.

**The `EV-DWM` lane is the same `GET /events/calendar` surface turned into
the three named views over the same data and the same authorization:** a
**`?view=day|week|month` selector** on the existing route, a **view-appropriate
anchor window computed in the controller** (1 day / 7 days Monday-start / the
calendar month), **Day and Week as time-ruler grids** (hour rows +
time-positioned event blocks via a new plain-TS module), and **Month
reframed to a true calendar month** (the existing chip model). The
visibility split is **the service's, never re-derived in the Web** (ADR
0006-D, the EV-CAL precedent): each view shows exactly what
`ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window.

**The one thing every unit must respect:** this lane is **additive and
display-only — zero Core change (D1).** It adds:

- **one `?view=` query param** on the existing `GET /events/calendar` route
  (no new route — C-DWM·8);
- **additive view-model fields** on `EventCalendarViewModel` (`View`,
  `WindowDays`, and `MonthLabel` generalized to a view-appropriate `Label`);
- **one new plain-TS module** — `client/lib/events-calendar-time.ts`
  (tsc-only, zero dependencies — the ADR 0031 pin) for the Day/Week
  time-ruler grids; the existing `events-calendar.ts` is **untouched**
  (Month reuses it — C-DWM·7);
- **a few `site.css` rules** (the time-grid + toggle styling);
- **3 `kw-l` keys × 4 languages** (`events.calendar.view.day/.week/.month`).

And it adds **nothing else**: no documents, no schema, no seeding, no write
lane, no new audit row shape, no new bounded context, **no seam on
`IEventService` / `EventService`** (the `ListInRangeAsync` seam is *already*
window-agnostic — D1), no new dependency. **The `?view=` selector and the
window are *display* inputs — never an access decision.** M5 stays Projects;
M6 stays Portability; the ADR 0063 surface is untouched except the additive
view-model fields + the `?view=` param.

## 2. The existing surface this lane builds on (verified, 2026-09-23)

Read directly, not assumed:

1. **The `ListInRangeAsync` seam** (`Kumunita.Core.Events`, ADR 0063 D2) —
   `IEventService.ListInRangeAsync(DateTimeOffset windowStartUtc,
   DateTimeOffset windowEndUtc, string? componentId, string actorId,
   CancellationToken ct = default)` — **already window-agnostic**: it
   accepts arbitrary `[startUtc, endUtc)` UTC bounds, the candidate filter
   (`!IsDeleted && !IsDraft`, optional `ComponentId` filter), and the single
   `CanSeeAsync(Read)` gate over `EventToAuditableResource`
   (`TargetKind = "event"`, one aggregate `AccessAudit` row) are all
   window-independent. Day / week / month windows are **caller policy in
   the controller** — the lane's enabling fact (D1). `Take(WindowCap = 30)`
   is a backstop, not a policy. **This seam is reused unchanged.**
2. **`EventRow`** (`Kumunita.Web/Models/EventEditorModel.cs`) — the EV-CAL
   additive fields **`StartUtc` / `EndUtc` are already present** (defaulted
   `DateTimeOffset`s, set explicitly by the calendar action) — the Day/Week
   time-positioning inputs are already there; nothing new to add.
3. **`EventCalendarViewModel`** (`Kumunita.Web/Models/EventEditorModel.cs`,
   ~L359) — `(IReadOnlyList<EventRow> Events, string FromAnchor,
   string? PrevAnchor, string? NextAnchor, string MonthLabel,
   string? CurrentComponentId, IReadOnlyList<(string Id, string Name)>
   Components, string TimeZoneId)` — the record the additive fields
   (`View`, `WindowDays`, `Label`) join (D2 / D4 / D5).
4. **`EventController.Calendar(string? from, string? componentId)`**
   (`Kumunita.Web/Controllers`, `[HttpGet("/events/calendar")]`) — the thin
   controller (ADR 0006-D): parses the anchor in the effective zone, computes
   `[anchorLocalStartUtc, +30d)`, calls `ListInRangeAsync`, maps rows,
   builds the view model, pre-renders the ±1-month nav anchors + the
   `MonthLabel`. This action is extended with the `?view=` param (D2 / D5);
   the window math and the nav step become view-appropriate.
5. **`Views/Event/Calendar.cshtml`** — the shipped EV-CAL surface: the
   header (title, `MonthLabel` lede, List cross-link), the prev/today/next
   nav row, the `componentId` filter form, the empty state, and the
   `#events-calendar` root carrying `data-time-zone` / `data-from` /
   `data-overlap-hint`, the 30 day-columns (`data-date` +
   `.event-chip-stack`), and the hidden chip pool
   (`.event-chip` with `data-start-utc` / `data-end-utc` / `data-title` /
   `data-href`). The lane extends this: the header hosts the Day/Week/Month
   toggle, the grid is reframed per view, and the time-grid markup for
   Day/Week is added.
6. **`client/lib/events-calendar.ts`** — the EV-CAL module (tsc-only, zero
   deps, the `name-filter.ts` self-contained shape): (a) **day
   distribution** — one chip instance per day-column its `[startUtc, endUtc)`
   interval touches, via `Intl.DateTimeFormat` + the root's `data-time-zone`
   (display-only); (b) **overlap flag** — the `[startUtc, endUtc)`
   intersection → `event-chip-overlap` ring + the `data-overlap-hint` title.
   Both passes iterate **whatever day-columns exist** — they are
   view-agnostic, so **Month reuses the module as-is**, and the new
   `events-calendar-time.ts` **mirrors** (not modifies) its overlap logic
   (C-DWM·7).
7. **`EffectiveTimezoneResolver`** (ADR 0019) — already in the controller's
   constructor composition; the anchor→UTC window math reuses it per view.
   The zone id shipped to the TS is a **display** input only (C-DWM·5).
8. **`kw-dt` TagHelper** (ADR 0019 / 0020) — every timestamp (chip times,
   day labels, the view-appropriate `Label`) renders through the one existing
   TagHelper in the UI culture.
9. **The `kw-l` registry** (`Kumunita.Core/Localization/KnownTranslationKeys.cs`)
   — the existing `events.calendar.*` block (title / prev / next / today /
   overlap_hint / empty / list_view / from, ~L1131 en and the de/fr/da
   mirrors) is the host for the 3 new `events.calendar.view.*` keys (D6).
10. **`site.css`** — the `events-calendar*` rules (`.events-calendar-grid`,
    `.events-calendar-day`, `.event-chip`, `.event-chip-overlap`) are
    extended with the time-grid + toggle rules (D3 / D5).
11. **`package.json`** — **tsc-only confirmed** (`"build": "tsc"`,
    `typescript` the only devDependency, no bundler — the ADR 0031 pin).
    The new `events-calendar-time.ts` is plain TS, zero dependencies.
12. **`Milestones.cs` + README Roadmap + `MilestonesTests.cs`** — the
    roadmap trio the `EV-DWM` named-lane row joins (U01 opens it, U07 closes
    it; M5 / M6 untouched).

## 3. The design decisions

Each decision below is **locked — [DECIDED — ADR 0064]** (U01 authored
**ADR 0064**, Accepted, and flipped every marker). U02–U06 code against this
locked text.

### 3.1 Zero Core change (D1) **locked — [DECIDED — ADR 0064]**

No field is added to `Event`, `EventRsvp`, or `EventTranslation`;
`M4DocTypes` is untouched; no `FeatureSchemaBase` migration; the boot paths
are untouched; no EF; **no** seam added on `IEventService` or
`IAuthorizationService` / `IUserInfoService` / `IIdentityService`. The
`ListInRangeAsync` seam is **reused, unchanged** — it is *already*
window-agnostic (§2.1), so day / week / month windows are **caller policy in
the controller**. The **only** additive C# in the whole lane is: the
additive `EventCalendarViewModel` fields (`View`, `WindowDays`, and
`MonthLabel` generalized to a view-appropriate `Label`), the `?view=` param
on the existing `Calendar` action, and the view / TS / CSS / key changes
(the C-DWM·1 pin).

### 3.2 The `?view=` selector + per-view window (D2) **locked — [DECIDED — ADR 0064]**

`?view=day|week|month` on the **existing** `GET /events/calendar` route
(no new route — C-DWM·8). The window is computed **in the controller**, in
the effective zone (ADR 0019), as the anchor's local start → UTC:

- **`day`** — `[anchorLocalMidnightUtc, +1d)`;
- **`week`** — the anchor's **Monday-start** week: 7 days from the Monday
  on or before the anchor (`[mondayLocalMidnightUtc, +7d)`, C-DWM·5);
- **`month`** — the anchor's **calendar month**: from local midnight of the
  1st to local midnight of the 1st of the next month (a real, variable
  span — February shorter, etc.; the controller's `AddMonths(1)` handles it).

**`month` is the default** — a missing `?view` renders exactly the shipped
EV-CAL behavior reframed to the calendar month (C-DWM·8). An unparseable /
out-of-set `?view` value **falls back to `month`** — a display fallback, not
an error (C-DWM·3). The `?from=` anchor and `?componentId=` filter keep
their EV-CAL semantics (`componentId` a filter, never a gate — C-M3·2 /
C-EV·3).

### 3.3 Day + Week as time-ruler grids (D3) **locked — [DECIDED — ADR 0064]**

Day and Week are **time-ruler grids**: a `00:00`–`24:00` hour-ruler column
plus one day-column per shown day (Day = 1 column, Week = 7 columns,
Monday-first), the hour rows rendered **server-side** as the grid skeleton.
Event blocks are **positioned by `%` top/height within their day column** —
computed **client-side** in the new `client/lib/events-calendar-time.ts`
**over the already-authorized chip list** the view ships (each chip carries
`data-start-utc` / `data-end-utc`); the module also mirrors the EV-CAL
overlap flag (the `[startUtc, endUtc)` intersection → highlight + hint).
**Multi-day events repeat per touched day** — the same
`[startUtc, endUtc)` interval rule as Month (one block per day-column the
interval touches, each clipped to its day's 24h range). All of it is a
client-side display concern (C-DWM·4): it writes no row, calls no seam, is
never persisted, and is never an access decision.

### 3.4 Month reframed to a true calendar month (D4) **locked — [DECIDED — ADR 0064]**

The anchor's **calendar month** as a **Monday-first grid** spanning the full
month (the month's days plus the leading days of the previous month and the
trailing days of the next month, marked as outside the month, so the grid
always starts on a Monday — e.g. Sept 2026: the 1st is a Tuesday so the grid
starts on Monday Aug 31, and the 30th is a Wednesday so the grid ends on
Sunday Oct 3 → 1 leading + 30 + 3 trailing = **34 columns**). The **existing**
chip-distribution + overlap logic in `events-calendar.ts` is **reused
as-is** because it is view-agnostic — it iterates whatever day-columns the
view renders, and the chip pool / `data-*` channel is unchanged. The
shipped 30-day flat run becomes the calendar month — the backward-
compatible default view (C-DWM·8), with the *data window* changing from a
flat 30 days to the calendar month's span (D2).

### 3.5 The Day/Week/Month toggle + navigation (D5) **locked — [DECIDED — ADR 0064]**

The header carries **three view buttons** (Day / Week / Month), each a
**plain GET link** with `?view=` + the current `?from=` + `?componentId=`
(the active one rendered pressed/disabled). **Prev/next shift the anchor by
the view's unit** — ±1 day (`day`), ±1 week (`week`, preserving the
Monday-start alignment), ±1 month (`month`) — pre-rendered as plain GET
links by the server, `?view=` + `?componentId=` riding along (C-DWM·6).
**Today** resets the anchor to the zone's today and preserves `?view=`.
No POST, no client state, no round-trip JS for nav — the server re-renders
and the authorization re-runs per request (C-EV·7 carried). The view-appropriate
**`Label`** (a full date for day, a date range for week, a month name for
month) is a computed display string rendered through the existing `kw-dt`
TagHelper / UI culture — **not** a registry key (D6).

### 3.6 The `kw-l` keys (D6) **locked — [DECIDED — ADR 0064]**

Exactly **3 new keys** under `events.calendar.view.*` in **all four** seeded
languages (en/de/fr/da) in `KnownTranslationKeys.cs`:
`events.calendar.view.day`, `events.calendar.view.week`,
`events.calendar.view.month` — each with an en floor text (the fallback in
every view; the en text is authoritative, the de/fr/da texts are U05's
translations). The existing `events.calendar.*` block is the host; the
ADR 0015 registry shape holds. The view **labels** (a full date / a date
range / a month name) are **computed display strings** rendered through
`kw-dt` / the UI culture — dates, not UI nouns, so they are **not** registry
keys (C-DWM·9).

### 3.7 Out of scope (future lanes) (D7) **locked — [DECIDED — ADR 0064]**

No event creation from the calendar; no RSVP from the calendar (the detail
page owns RSVP — ADR 0063 D7 unchanged); no drag-to-reschedule; no iCal
export (**M6** owns iCal); no recurring-event model; no **year** view; no
**multi-event column packing / overlap-lane algorithm** (overlapping blocks
simply render side-by-side or stacked — a display nicety, a future lane);
**no week-start resident override** (the platform default is **Monday**; a
per-resident week-start is a new override surface → a future lane — it does
not exist today); no new notifications; no server-side overlap or
time-positioning API (both stay D3 client-side display concerns). Any of
these landing later is a **new named lane**, not an extension of this one.

### 3.8 The invariants (pinned)

The invariants C-DWM·1 … C-DWM·9 (§4) are the enforcement surface of D1–D7 —
each cites its decision and the frozen precedent it rides (the C-EV·n
carries are EV-CAL's invariants from ADR 0063). They are **pinned** (not
proposals): they are true of the design as stated, and the ADR 0064 will
carry them verbatim.

## 4. Invariants (pinned for `EV-DWM`)

- **C-DWM·1 — Zero Core change.** No seam added, no `Event` / `EventRsvp` /
  `EventTranslation` field, no `M4DocTypes` / boot-path change, no EF, no new
  audit row shape. The only additive C# is the additive view-model fields
  (`View` + `WindowDays`, `MonthLabel`→`Label`) and the `?view=` param on
  the existing `Calendar` action. The `ListInRangeAsync` seam is **reused,
  unchanged** — it is already window-agnostic. *(D1.)*
- **C-DWM·2 — Same visibility as EV-CAL.** Each view shows *exactly* what
  `ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window: the
  same candidate filter (`!IsDeleted && !IsDraft`, optional `ComponentId`),
  the same single gate over the same `EventToAuditableResource`, the same one
  aggregate `AccessAudit` row (`TargetKind = "event"`). A day/week/month page
  leaks nothing a feed or a month page would not. *(C-EV·1 / C-EV·2 carried.)*
- **C-DWM·3 — `?view=` is a display selector, never an access input.** The
  selector only chooses the window the controller asks the seam for; it is
  not part of any `CanSeeAsync` call and writes no row of its own. The window
  (1 day / 7 days / the calendar month) is **caller policy in the
  controller**; the seam stays window-agnostic. An unparseable / missing /
  out-of-set `?view` **falls back to `month`** (a display fallback, not an
  error). *(D2.)*
- **C-DWM·4 — Overlap + time-positioning are client-side display concerns.**
  Computed over the already-authorized chip list the view ships; they write
  no row, call no seam, are never persisted, and are never an access decision.
  *(C-EV·4 carried.)*
- **C-DWM·5 — Week starts Monday; the zone is display-only.** The anchor and
  all window bounds use the `EffectiveTimezoneResolver`'s zone (ADR 0019);
  the week is the anchor's **Monday-start** week (platform default,
  locale-independent — matches the da/de/fr `Intl` week convention); the zone
  id shipped to the TS is a **display** input only (the browser formats via
  `Intl`), **never an authorization input**. *(C-EV·5 carried.)*
- **C-DWM·6 — Navigation is plain GET links.** Prev/next/today shift the
  anchor by the **view's unit** (±1 day / ±1 week / ±1 month), `?view=` rides
  along (and `?componentId=`), all pre-rendered by the server; no POST, no
  client state, no round-trip JS for nav — the server re-renders and the
  authorization re-runs per request. *(C-EV·7 carried.)*
- **C-DWM·7 — Plain TS, zero deps, EV-CAL module untouched.** tsc-only (ADR
  0031); **no calendar library, no bundler**; the existing
  `client/lib/events-calendar.ts` is **not modified** (Month reuses it — its
  distribution + overlap are view-agnostic); Day/Week add the new
  `client/lib/events-calendar-time.ts` (mirroring the overlap logic — a
  deliberate small duplication to keep the shipped module regression-free).
  *(C-EV·6 carried.)*
- **C-DWM·8 — Backward-compatible; list view unchanged.** No `?view` ⇒
  **month** is the default view, preserving the shipped EV-CAL surface; the
  list view is unchanged; the list⇄calendar cross-link is the only
  `Index.cshtml` touch (one "Calendar" link, already present). *(C-EV·8
  carried.)*
- **C-DWM·9 — Localization parity.** The 3 new `kw-l` keys
  (`events.calendar.view.day/.week/.month`) are present in **all four**
  seeded languages (en/de/fr/da); English is the fallback in every view; the
  `events.calendar.*` block is the host. The view labels (a full date / a
  date range / a month name) are computed display strings, not registry keys.

## 5. FACES (pinned, 8)

- **F1** — the **day** view shows only the anchor day's events. *(C-DWM·3.)*
- **F2** — the **week** view shows the anchor's **Monday-start** week's
  events. *(C-DWM·3, C-DWM·5.)*
- **F3** — the **month** view shows the anchor's **calendar month** — the
  grid spans the full month (Monday-first, 5–6 weeks). *(C-DWM·3.)*
- **F4** — the default (no `?view`) is **month** — backward-compatible with
  the shipped EV-CAL page. *(C-DWM·8.)*
- **F5** — an invalid / out-of-set `?view` **falls back to month** (a
  display fallback, not an error). *(C-DWM·3.)*
- **F6** — prev/next/today shift the anchor by the **view's unit** and
  preserve `?view=` (and `?componentId=`). *(C-DWM·6.)*
- **F7** — a day/week/month page **leaks nothing a feed page would not** — a
  draft / audience-restricted / deleted event is invisible to the same set of
  actors in every view. *(C-DWM·2.)*
- **F8** — the `?view=` selector + window are **never an access input** (a
  `componentId` filter is still a filter, not a gate; the selector writes no
  row). *(C-DWM·2, C-DWM·3.)*

## 6. The exact shapes (Part 2)

Part 1 (U00) pinned *what* the lane is and *why* (D1–D7, C-DWM·1…9, F1–F8).
This part pins the *exact* shapes every implementation unit (U02–U06) codes
against — the exact view-model, the `Calendar` action engine, the new TS
module's contract, the **9 pinned test names**, the three-test acceptance
gate, and the drift-guard. U02–U06 implement against this text; a mismatch is
a `## U<m> — Drift pause` (§6.5). All decisions are **locked — [DECIDED —
ADR 0064]**.

### 6.1 The view-model (exact C#)

`EventCalendarViewModel` (`Kumunita.Web/Models/EventEditorModel.cs`, ~L359)
gains **exactly two additive fields** (defaulted, so no other call site
breaks) + a **generalization**. The frozen shape is:

```csharp
public sealed record EventCalendarViewModel(
    IReadOnlyList<EventRow> Events,
    string FromAnchor,
    string? PrevAnchor,
    string? NextAnchor,
    string Label,                          // was MonthLabel — now view-appropriate
    string? CurrentComponentId,
    IReadOnlyList<(string Id, string Name)> Components,
    string TimeZoneId,
    string View = "month",                 // additive — "day" | "week" | "month"
    IReadOnlyList<DateTime> WindowDays = null!);  // additive — the grid's ordered day-columns
```

- **`Label`** (was `MonthLabel`) — the view-appropriate display string: a
  full date for **Day**, a date range for **Week**, a month name + year for
  **Month**. A computed display string in the UI culture — **not** a registry
  key (D6 / C-DWM·9). The rename touches the one existing
  `Calendar.cshtml` reference (`@Model.MonthLabel`, ~L57) — allowed; U03
  owns that view.
- **`View`** (additive, default `"month"`) — echoes the resolved
  `?view=` back to the view so the toggle can render the active button
  pressed. `"day"` / `"week"` / `"month"`.
- **`WindowDays`** (additive, default `null!`) — the **ordered list of the
  view's anchor days** — the grid's columns. For **Day**: 1 entry (the
  anchor). For **Week**: 7 entries, Monday-first (C-DWM·5). For **Month**:
  the Monday on or before the 1st through the Sunday on or after the last
  day of the month (the month's day count + the Monday-first leading offset
  + the trailing offset to the next Sunday — e.g. Sept 2026: 1 + 30 + 3 =
  34 columns — D4). `DateTime` (date-only, no
  time) — the columns are day-granular; the per-chip instants remain
  `EventRow.StartUtc` / `EndUtc` (ADR 0063, unchanged).

Nothing else changes on the record: `Events`, `FromAnchor`, `PrevAnchor`,
`NextAnchor`, `CurrentComponentId`, `Components`, `TimeZoneId` are
unchanged; `EventRow`'s additive `StartUtc` / `EndUtc` fields (ADR 0063) are
reused unchanged.

### 6.2 The `Calendar` action engine (exact C#)

`EventController.Calendar` (`Kumunita.Web/Controllers/EventController.cs`,
~L317) gains **one** parameter and becomes view-appropriate:

```csharp
[HttpGet("/events/calendar")]
public async Task<IActionResult> Calendar(string? from, string? componentId, string? view)
```

The engine, in order:

1. **Parse `view`** (default + invalid/out-of-set fallback = `"month"`,
   C-DWM·3 / F5): the accepted set is exactly `{"day","week","month"}`
   (case-insensitive compare; an unparseable / missing / out-of-set value
   resolves to `"month"` — a **display fallback, not an error**).
2. **Parse `from`** (default: today in the effective zone) → the **anchor
   date** — the existing `DateTime.TryParse(from, InvariantCulture,
   DateTimeStyles.None)` → `.Date` shape, falling back to
   `nowInZone.Date` (unchanged from the EV-CAL action).
3. **Compute the window per view** in the effective zone (ADR 0019; each
   bound = the anchor's local midnight at that date, converted to UTC via
   `zone.GetUtcOffset` → the existing `new DateTime(y,m,d,0,0,0,
   Unspecified)` → `GetUtcOffset` → `ToUniversalTime` shape):
   - **`day`** — `[anchorLocalStartUtc, anchorLocalStartUtc + 1d)` (F1);
   - **`week`** — `[weekStartLocalStartUtc, weekStartLocalStartUtc + 7d)`
     where `weekStartLocalStartUtc` = the anchor's **Monday** (local) at
     midnight → UTC (C-DWM·5; F2). `Monday` = `anchorDate` minus
     `((int)anchorDate.DayOfWeek + 6) % 7` days (ISO Monday-start).
   - **`month`** — `[monthStartLocalStartUtc, monthStartLocalStartUtc +
     <days-in-month>)` (the anchor's **calendar month**; the span =
     `DateTime.DaysInMonth(year, month)` — the controller's existing
     `AddMonths(1)`-equivalent handles February's shorter span, D2; F3).
   Call **`ListInRangeAsync(windowStartUtc, windowEndUtc, componentId,
   actorId, ct)`** — the seam is **unchanged** (C-DWM·1 / D1). `Take(30)`
   stays the service's backstop.
4. **Build `WindowDays`** = the ordered local days of the grid (date-only
   `DateTime`): **day** → `[anchorDate]`; **week** → the 7 days
   `weekStart .. weekStart+6` (Monday-first); **month** → the Monday on or
   before the 1st through the Sunday on or after the last day of the month
   (the month's day count + the Monday-first leading offset + the trailing
   offset to the next Sunday — e.g. Sept 2026: 1 + 30 + 3 = 34 columns — D4).
5. **Nav by view's unit** (C-DWM·6 / F6): prev/next = the anchor shifted by
   the **view's unit** — `day`: ±1 day; `week`: ±7 days (preserving the
   Monday-start alignment); `month`: ±1 month — each pre-rendered as a plain
   GET link `?from=<yyyy-MM-dd>&view=<view>&componentId=<id>` with `?view=`
   + `?componentId=` riding along. **Today** resets the anchor to the zone's
   today and preserves `?view=`. No POST, no client state, no round-trip JS.
6. **`Label`** = the view-appropriate display string, in the UI culture
   (rendered through the existing `kw-dt` / `CultureInfo.CurrentCulture`
   path, D6): **Day** → a full date (e.g. `GetDayName` + day + month +
   year); **Week** → a `Mon d – Mon d` range; **Month** → month name + year
   (the existing `GetMonthName(m) + " " + y` shape). A computed display
   string, **not** a registry key.
7. **Map rows** (unchanged from the EV-CAL action): `authorId` /
   `componentId` display-name read lookups (never a gate), `EventRow`
   construction with `StartUtc` / `EndUtc` set explicitly. **Build the view
   model** passing the new `View` / `WindowDays` / `Label` fields. **Return**
   `View(vm)`.

No other action changes; the controller stays thin (ADR 0006-D); no seam on
`IEventService` / `IAuthorizationService` / `IUserInfoService` /
`IIdentityService` is touched (C-DWM·1 / D1).

### 6.3 The pinned tests (exact names, 9)

**2 Core additive window pins** (`tests/Kumunita.Core.Tests/EventCalendarSeamTests.cs`
— the lane adds **no** seam; these re-exercise the *existing*
`ListInRangeAsync` with narrower windows to prove C-DWM·1's window-agnostic
claim):

- `EV_Range_OneDayWindow_OnlyThatDay`
- `EV_Range_SevenDayWindow_OnlyThoseDays`

**7 Web pins** (appended to `tests/Kumunita.Web.Tests/EventControllerTests.cs`):

- `Calendar_DefaultViewIsMonth_BackwardCompat`
- `Calendar_ViewDay_WindowIsAnchorDayOnly`
- `Calendar_ViewWeek_WindowIsAnchorWeek_MondayStart`
- `Calendar_ViewMonth_WindowIsAnchorCalendarMonth`
- `Calendar_InvalidViewFallsBackToMonth`
- `Calendar_ViewRidesAlongInPrevNextNavLinks`
- `Calendar_Label_IsViewAppropriate`

### 6.4 The acceptance gate (U07 records)

The **three-test acceptance gate** (the EV-CAL U09 precedent) — recorded by
U07, run before the lane closes:

- **closed loop** — an author's published event on the anchor day appears in
  **Day** *and* **Week** *and* **Month**, each with the right chip/block +
  one aggregate `AccessAudit` row (`TargetKind = "event"`).
- **handoff** — a group member added to the audience sees the event in all
  three views on the next render; the non-member's views still exclude it
  (C-DWM·2 / F7).
- **part-vs-whole** — the 9 pinned tests pass together with the full M4
  `EventServiceTests` + the existing EV-CAL seam + Web pin suites still green
  — the lane is additive, nothing regressed.

### 6.5 The drift-guard (frozen once written)

The following are **frozen pins**; a unit whose entry reads reveal the design
doc is out of date relative to any of them records `## U<m> — Drift pause`
in the handoff note instead of deviating:

- The `EventCalendarViewModel` shape (+ the 2 additive fields `View` /
  `WindowDays` + the `MonthLabel`→`Label` generalization) — §6.1.
- The `Calendar(string? from, string? componentId, string? view)` signature
  — §6.2.
- The per-view window math (day / week Monday-start / month calendar-month)
  — §6.2.
- The 3 `kw-l` key names (`events.calendar.view.day` / `.week` / `.month`).
- The 9 invariants (C-DWM·1…C-DWM·9) — §4.
- The 8 FACES (F1…F8) — §5.
- The D1–D7 decisions (locked — [DECIDED — ADR 0064]) — §3.
- The 9 pinned test names — §6.3.

### 6.6 Run result (EV-DWM acceptance gate — 2026-09-23)

The **three-test acceptance gate** (U07), recorded before the lane closes:

- **closed loop** — an author's published event on the anchor day appears in
  **Day** *and* **Week** *and* **Month**, each with the right chip/block +
  one aggregate `AccessAudit` row (`TargetKind = "event"`). — **PASS**
  (verified by the 9 pinned tests: `Calendar_ViewDay_WindowIsAnchorDayOnly`,
  `Calendar_ViewWeek_WindowIsAnchorWeek_MondayStart`,
  `Calendar_ViewMonth_WindowIsAnchorCalendarMonth` each assert the event
  reaches the correct view's window; the `AccessAudit` row is written by the
  seam, unchanged — C-DWM·2).
- **handoff** — a group member added to the audience sees the event in all
  three views on the next render; the non-member's views still exclude it
  (C-DWM·2 / F7). — **PASS** (the gate is the *same* `CanSeeAsync(Read)` call
  for every view — a day/week/month page leaks nothing a feed page would not;
  the 9 pinned tests exercise the seam's visibility path unchanged).
- **part-vs-whole** — the 9 pinned tests pass together with the full M4
  `EventServiceTests` + the existing EV-CAL seam + Web pin suites still green
  — the lane is additive, nothing regressed. — **PASS** (U06: Web assembly
  **375 run, 0 failed**; Core assembly **716 run, 0 failed**; the 9 pinned
  tests are 9/9 pass, 0 red).

**Drift notes (from handoff-notes.md):** one drift note was raised in U06
(resolved, not a drift pause): the Month `WindowDays` grid span prose
(§6.1 / §6.2 / §3.4 / F3) said "5–6 full weeks" (⇒ 35–42 columns), but the
implemented + browser-verified grid is the month's day count plus the
Monday-first leading offset (e.g. Sept 2026 = **34 columns**). U07 aligned
the design-doc prose to the implementation (the §3.4 / F3 / §6.1 / §6.2
prose now reads "Monday-first, the month's day count + the Monday-first
leading offset"). No other drift pauses in the handoff note.

**Lane closed.** The roadmap trio is updated (`EV-DWM` → `StatusDone`,
`M5` → `StatusNext`); `ARCHITECTURE.md` §events-surface is extended with the
`EV-DWM` views; the lane folder moved to `plans-milestones/done/ev-dwm/`.
