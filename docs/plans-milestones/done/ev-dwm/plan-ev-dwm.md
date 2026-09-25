# Events calendar day/week/month views (`EV-DWM`) — sealed unit register

> **Lane plan** (the secondary register tier) for the **`EV-DWM`** named lane — an
> **additive view surface** on the already-shipped **`EV-CAL`** events calendar
> (ADR 0063). The **primary reference tier** is the design doc
> `docs/design/events-calendar-dwm-design.md` (authored U00–U01); the **scratch
> tier** is `handoff-notes.md` in this lane's folder (one appended `## U#`
> section per unit, never rewritten).
>
> **What this is:** the existing `GET /events/calendar` is a single
> month-anchored **30-day chip grid**. `EV-DWM` turns it into the **three
> views residents expect from Outlook / Google Calendar** — **Day, Week, and
> Month** — over the *same* already-authorized event set, the *same*
> `ListInRangeAsync` seam, the *same* `CanSeeAsync(Read)` gate, and the *same*
> chip + client-side-overlap model. The only new inputs are a **`?view=`
> selector** (`day|week|month`) and a **view-appropriate anchor window**
> (the anchor day / the anchor's Monday-start week / the anchor's calendar
> month), all computed **in the controller**. Day and Week are **time-ruler
> grids** (hour rows + time-positioned event blocks); Month is the existing
> chip grid reframed to a **true calendar month** (5–6 week × 7 day grid).
>
> **The one thing every unit must respect:** this lane is **additive and
> display-only — zero Core change.** It adds **one `?view=` query param** on
> the existing route, **two additive view-model fields** (`View` + `WindowDays`,
> and `MonthLabel` generalized to a view-appropriate `Label`), **one new
> plain-TS module** (`client/lib/events-calendar-time.ts`, tsc-only, zero
> dependencies — the ADR 0031 pin), **a few `site.css` rules**, and **3 `kw-l`
> keys × 4 languages** (`events.calendar.view.day/.week/.month`). It touches
> **no** document, **no** schema, **no** seeding, **no** `IEventService` /
> `EventService` method (the seam is *already* window-agnostic — `D1`), **no**
> new audit row shape, **no** new bounded context, **no** new dependency. The
> visibility split is **the service's, never re-derived in the Web** (ADR
> 0006-D, the EV-CAL precedent): each view shows exactly what
> `ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window. **The
> `?view=` selector and the window are *display* inputs — never an access
> decision.** **M5 stays Projects; M6 stays Portability; EV-CAL's ADR 0063
> surface is untouched except the additive view-model fields + the `?view=`
> param.**
>
> **Sizing:** units are sized for a **~32K-context fresh agent** one at a time,
> each with its own closed exit criteria, in the `EV-CAL` / `GP` / `TG` / `M4`
> style. **U00 is the sign-off gate** — it authors the design doc, locks the
> **[PROPOSED]** decisions into **ADR 0064**, and opens the lane on the roadmap
> trio (U01); every later unit codes against the *locked* text. **Sequencing
> invariant:** the model + controller `?view=` engine (U02) lands before any
> view (U03–U05) codes against it; **Month is the default view** (preserves the
> shipped EV-CAL behavior — backward-compatible); Week (U04) introduces the one
> new time-grid TS module; Day (U05) reuses it; the pinned tests (U06) run
> before the gate (U07); the close (U07) is last so the doc index + README +
> ARCHITECTURE.md are honest at ship time.

## Understanding (one paragraph)

EV-CAL (ADR 0063) gave residents a time overview the feed cannot: a
month-anchored calendar at `GET /events/calendar`, a rolling 30-day chip grid,
client-side overlap highlighting, and prev/next/today navigation. But a resident
opening that page expects the **three views every calendar app has** —
**Day** ("what's happening *today*?"), **Week** ("does the potluck land on the
same *day* as the cleanup?"), and **Month** ("the *shape* of the month, with the
edges of the previous/next month around it") — the Outlook / Google Calendar
mental model. The 30-day grid is *close* to a month but is not one (it is a flat
30-day run, not the calendar month), and it has no Day or Week at all. `EV-DWM`
adds the **three named views over the same data and the same authorization**: a
`?view=day|week|month` selector, a view-appropriate window computed in the
controller (1 day / 7 days Monday-start / the calendar month), Day and Week as
**time-ruler grids** (hour rows + time-positioned blocks), and Month reframed to
a **true calendar month**. It is deliberately *not* a new capability: **zero
Core change** — the `ListInRangeAsync` seam already accepts arbitrary
`[startUtc, endUtc)` bounds, so the window is caller policy; the gate, the
audit row, the chip pool, the overlap flag, the timezone machinery, and the
localization registry are all *reused, not re-invented*. This is the EV-CAL
"view over already-authorized content" shape carried from *one* view to *three*.

## Assumptions

- **Scope (locked in ADR 0064):** a **`?view=` selector** (`day|week|month`)
  on the existing `GET /events/calendar` route (no new route — C-EV·8), a
  **view-appropriate anchor window** computed in the controller
  (day = the anchor day; week = the anchor's **Monday-start** week; month = the
  anchor's **calendar month**), **Day + Week as time-ruler grids** (hour rows +
  time-positioned event blocks via a new plain-TS module), **Month reframed to
  a true calendar month** (5–6 week × 7 day grid, the existing chip model),
  **prev/next/today navigation that shifts the anchor by the view's unit**
  (±1 day / ±1 week / ±1 month) with `?view=` riding along, the **view toggle**
  (Day / Week / Month buttons in the header), **3 `kw-l` keys × 4 languages**
  (`events.calendar.view.day/.week/.month`), the `Label` view-appropriate
  display string, and the pinned tests + the acceptance gate. **Out (→ future
  lanes, ADR 0064 D7):** no event creation / RSVP / drag-to-reschedule from the
  calendar (the detail page owns RSVP; ADR 0063 D7 unchanged), no iCal export
  (**M6** owns iCal), no recurring-event model, no year view, no multi-event
  column packing / overlap-lane algorithm (overlapping blocks simply render
  side-by-side or stacked — a display nicety, a future lane), **no
  week-start resident override** (the platform default is **Monday**; a
  per-resident week-start is a new override surface → future lane, it does not
  exist today), no new notification, no server-side overlap or
  time-positioning API (both stay client-side display concerns).
- **Zero Core change (`D1`, the lane's defining pin).** `IEventService` and
  `EventService` are **untouched** — `ListInRangeAsync(windowStartUtc,
  windowEndUtc, componentId, actorId, ct)` *already* accepts arbitrary
  `[startUtc, endUtc)` UTC bounds (verified in
  `src/Kumunita.Core/Events/IEventService.cs` L48 and `EventService.cs`
  L113–145), so day/week/month windows are **caller policy in the controller**,
  not a new seam. **No** field is added to `Event` / `EventRsvp` /
  `EventTranslation`; `M4DocTypes` is untouched; the boot paths are untouched;
  no EF. The **only** additive C# in the whole lane is **2 additive view-model
  fields** (+ the `MonthLabel`→`Label` generalization) on
  `EventCalendarViewModel`, **one `?view=` param** on the existing `Calendar`
  action, and the view / TS / CSS / key changes. **No seam on
  `IAuthorizationService` / `IUserInfoService` / `IIdentityService` is touched;
  no new seam on `IEventService`.**
- **Visibility is the frozen path, unchanged (C-DWM·2).** Each view's candidate
  set is `ListInRangeAsync`'s (non-draft, non-deleted, `componentId`-filtered)
  restricted to that view's window; the `CanSeeAsync(Read)` gate is the **same
  single call**, the same `EventToAuditableResource` adapter, the **same one
  aggregate `AccessAudit` row** (`TargetKind = "event"`, C-EV·2). **A day /
  week / month page leaks nothing a feed page would not** (C-EV·1 carried).
- **`?view=` is a display selector, never an access input (C-DWM·3).** The
  selector only chooses the window the controller asks the seam for; it is not
  part of any `CanSeeAsync` call and writes no row of its own. **The
  `componentId` filter remains a filter, never a gate (C-M3·2, C-EV·3).**
- **Overlap + time-positioning are client-side display concerns (C-DWM·4,
  C-EV·4 carried).** The overlap flag and the Day/Week time-block positions are
  computed in the client **over the already-authorized chip list** the view
  ships (each chip carries `data-start-utc` / `data-end-utc`); they write no
  row, call no seam, are never persisted, and are **never an access decision**.
  The existing `client/lib/events-calendar.ts` overlap logic is **reused for
  Month** and **mirrored in the new time module for Day/Week** (a deliberate
  small duplication to keep the working EV-CAL module *untouched* —
  C-DWM·7).
- **Week starts on Monday (C-DWM·5).** Locale-independent platform default
  (matches da/de/fr first-class; the da/de/fr `Intl` week convention). The
  anchor and all window bounds use the **`EffectiveTimezoneResolver`'s zone**
  (ADR 0019); the zone id shipped to TS is a **display** input only (the
  browser formats via `Intl`), **never an authorization input** (C-EV·5
  carried).
- **Plain `client/lib` TS, tsc-only, zero dependencies (C-DWM·7, ADR 0031
  pin; C-EV·6 carried).** **No calendar library, no bundler** (`package.json`
  is `tsc` only — verified). **Month reuses the existing
  `client/lib/events-calendar.ts`** (its chip distribution + overlap are
  view-agnostic — they iterate whatever day-columns exist). **Day + Week use a
  new `client/lib/events-calendar-time.ts`** (the hour-ruler + time-block
  positioning + overlap, self-contained). The existing module is **not
  modified** (zero regression risk to the shipped EV-CAL month grid).
- **Localization:** exactly **3 new `kw-l` keys** (`events.calendar.view.day`,
  `.week`, `.month`) added to `KnownTranslationKeys.cs` in **all four** seeded
  languages (en/de/fr/da — the ADR 0042 / 0045 set), the ADR 0015 key-registry
  shape (the existing `events.calendar.*` block is the host, ~L1131/2206/3283/
  4355). English is the fallback in every view (`<kw-l key=…>English</kw-l>`).
  The day/week/month **labels** (a full date / a date range / a month name) are
  **computed display strings** rendered through the existing `kw-dt`
  TagHelper / UI culture — **not** registry keys (they are dates, not UI
  nouns).
- **Roadmap:** the lane gets a **named-lane row** (`EV-DWM`) on the roadmap trio
  (`Milestones.cs` + README + `MilestonesTests.cs`) — the `EV-CAL` / `GP` /
  `TG` / `PG` precedent (named lane, not a renumber). U01 opens it
  (`StatusNext`, `M5` → `StatusPlanned`), U07 closes it (`StatusDone`, `M5` →
  `StatusNext`). **M5 stays Projects; M6 stays Portability — untouched.**
- **Test model (unchanged).** xunit.v3, run via `dotnet exec …dll` per AGENTS.md
  (**not** `dotnet test` / VS Test Explorer on this machine);
  `Kumunita.Core.Tests` = `PostgresFixture`; `Kumunita.Web.Tests` = NSubstitute
  (no Postgres). Because the lane is **zero Core change**, the Core tests are
  **2 additive pins that re-exercise the *existing* seam with a 1-day and a
  7-day window** (proving D1's "the seam is already window-agnostic" claim),
  and the **7 Web pins** cover the new `?view=` engine. The invariant-anchored
  test list is pinned in the design doc Part 2 (U01).

## Approach

Two tracks, sequenced — like `EV-CAL`, but **Track A (Core) is empty** (zero
Core change is the point of the lane). **Track B (Web/TS/CSS/keys):** the
`EventCalendarViewModel` additive fields + the `Calendar` action's `?view=`
engine (window per view + nav by view-unit + view-appropriate label) →
**Month** (the default; the existing chip grid reframed to a true calendar
month + the Day/Week/Month toggle) → **Week** (7-column time grid + the new
time TS module) → **Day** (1-column time grid reusing the time module) →
CSS + keys. **Track C (Tests + gate):** the 2 Core additive window pins + the
7 Web pins + the three-test acceptance gate + the close (roadmap trio, ARCHITECTURE.md
sync, README, handoff summary, and moving this lane's unit files from
`in-progress/` → `done/`).

Every unit ends with **build green** (and `tsc` green from U04). The last
unit (U07) appends the final handoff section + moves the lane's folder into
`done/` so the roadmap / ADR / ARCHITECTURE.md pin is honest at ship time.

## Workflow — handoff protocol for fresh-context agents

This lane is executed as a sequence of **sealed units** (U00–U07 below), one
unit per fresh agent with a **~32K context window**.

**Shared state (three-tier contract):**

- **Primary — the design doc** (`docs/design/events-calendar-dwm-design.md`,
  authored U00–U01): pins the exact view-model + controller + TS shapes every
  unit codes against, the invariant table, the pinned test names, and the
  acceptance-gate shape. U00–U01 are the **sign-off gate** — **ADR 0064**
  (Accepted) is the decisions source; the design doc turns it into exact
  shapes.
- **Secondary — this file** (`docs/plans-milestones/plan-ev-dwm.md`) — the
  lane register: understanding, assumptions, invariants, FACES, and the unit
  index (each pointing at its unit file).
- **Unit files — `docs/plans-milestones/in-progress/ev-dwm/U0#.{md}`** — one
  file per unit with the full **Goal / Entry reads / Deliverables / Exit**.
  **On a unit's completion its file is moved
  `in-progress/ev-dwm/U0#.md` → `done/ev-dwm/U0#.md`** (the per-lane subfolder
  follows the existing `done/events-calendar/` convention and avoids
  cross-lane `U0#` collisions). When the lane closes (U07) the whole
  `in-progress/ev-dwm/` folder moves to `done/ev-dwm/`.
- **Scratch — the rolling handoff note**
  (`docs/plans-milestones/in-progress/ev-dwm/handoff-notes.md`). One `## U#`
  section per unit, appended (never rewritten). Each unit writes exactly one
  short section before it exits; the next unit reads only that section + its
  own entry-read list. Moves with the lane folder at close.

**Per-unit template** (each `U` below follows this): **Goal** (one sentence,
one or two related deliverables); **Entry reads** (the minimal file list, ≤ 5
files <~300 lines each, no full-repo scan; the design-doc section cited is
named); **Deliverables** (a closed set of new/modified files, ≤ ~4 files /
~600 LOC, no misc cleanups); **Exit** (build green for the touched projects +
`tsc` green from U04; handoff-note entry appended *before* any follow-up
action; the unit file moved `in-progress/` → `done/`).

**Unit-series rules:** (1) a unit never modifies a file not in its own
`Deliverables`; (2) never rewrites the design doc outside the §drift-guard
unit; (3) never introduces a test whose exact name is not in the design doc's
pinned list; (4) **never touches `IEventService` / `EventService` / any Core
document / `M4DocTypes` / a boot path (zero Core change — D1)**; (5) never
opens a seam on `IAuthorizationService` / `IUserInfoService` / `IIdentityService`
or adds any seam on `IEventService` (there is *no* ADD in this lane); (6) never
introduces a calendar library or a bundler (plain `client/lib` TS, tsc-only —
the ADR 0031 pin); (7) never modifies the existing
`client/lib/events-calendar.ts` (the EV-CAL module stays untouched — C-DWM·7;
Day/Week add the new `events-calendar-time.ts`); (8) if entry reads reveal the
design doc is out of date, the unit pauses and records `## U<m> — Drift pause`
in the handoff note.

---

## The invariants (pinned for `EV-DWM`)

- **C-DWM·1 — Zero Core change.** No seam added, no `Event` / `EventRsvp` /
  `EventTranslation` field, no `M4DocTypes` / boot-path change, no EF, no new
  audit row shape. The lane's only additive C# is the 2 additive view-model
  fields (+ `MonthLabel`→`Label`) and the `?view=` param on the existing
  `Calendar` action. The `ListInRangeAsync` seam is **reused, unchanged** — it
  is already window-agnostic. *(D1.)*
- **C-DWM·2 — Same visibility as EV-CAL.** Each view shows *exactly* what
  `ListInRangeAsync`'s `CanSeeAsync(Read)` gate allows for that window: the
  same candidate filter (`!IsDeleted && !IsDraft`, optional `ComponentId`),
  the same single gate over the same `EventToAuditableResource`, the same one
  aggregate `AccessAudit` row (`TargetKind = "event"`). A day/week/month page
  leaks nothing a feed or a month page would not. *(C-EV·1 / C-EV·2 carried.)*
- **C-DWM·3 — `?view=` is a display selector, never an access input.** The
  selector only chooses the window the controller asks the seam for; it is not
  part of any `CanSeeAsync` call and writes no row of its own. The window
  (1 day / 7 days / the calendar month) is **caller policy in the controller**;
  the seam stays window-agnostic. An unparseable / missing / out-of-set `?view`
  **falls back to `month`** (a display fallback, not an error). *(D2.)*
- **C-DWM·4 — Overlap + time-positioning are client-side display concerns.**
  Computed over the already-authorized chip list the view ships; they write no
  row, call no seam, are never persisted, and are never an access decision.
  *(C-EV·4 carried.)*
- **C-DWM·5 — Week starts Monday; the zone is display-only.** The anchor and
  all window bounds use the `EffectiveTimezoneResolver`'s zone (ADR 0019);
  the week is the anchor's **Monday-start** week (platform default,
  locale-independent); the zone id shipped to TS is a **display** input only
  (the browser formats via `Intl`), **never an authorization input**.
  *(C-EV·5 carried.)*
- **C-DWM·6 — Navigation is plain GET links.** Prev/next/today shift the anchor
  by the **view's unit** (±1 day / ±1 week / ±1 month), `?view=` rides along,
  all pre-rendered by the server; no POST, no client state, no round-trip JS
  for nav — the server re-renders and the authorization re-runs per request.
  *(C-EV·7 carried.)*
- **C-DWM·7 — Plain TS, zero deps, EV-CAL module untouched.** tsc-only
  (ADR 0031); **no calendar library, no bundler**; the existing
  `client/lib/events-calendar.ts` is **not modified** (Month reuses it — its
  distribution + overlap are view-agnostic); Day/Week add the new
  `client/lib/events-calendar-time.ts`. *(C-EV·6 carried.)*
- **C-DWM·8 — Backward-compatible; list view unchanged.** `?view=month` is the
  **default** (no `?view` ⇒ month), preserving the shipped EV-CAL behavior; the
  list view is unchanged; the list⇄calendar cross-link is the only
  `Index.cshtml` touch (one "Calendar" link, already present). *(C-EV·8
  carried.)*
- **C-DWM·9 — Localization parity.** The 3 new `kw-l` keys
  (`events.calendar.view.day/.week/.month`) are present in **all four** seeded
  languages (en/de/fr/da); English is the fallback in every view; the
  `events.calendar.*` block is the host. The view labels (a full date / a date
  range / a month name) are computed display strings, not registry keys.

## FACES (pinned, 8)

- **F1** day view shows only the anchor day's events — C-DWM·3
- **F2** week view shows the anchor's **Monday-start** week's events — C-DWM·3, C-DWM·5
- **F3** month view shows the anchor's **calendar month** (the grid spans the
  full month, 5–6 weeks) — C-DWM·3
- **F4** the default (no `?view`) is **month** — backward-compatible — C-DWM·8
- **F5** an invalid / out-of-set `?view` **falls back to month** (display, not
  an error) — C-DWM·3
- **F6** prev/next/today shift the anchor by the **view's unit** and preserve
  `?view=` (and `componentId`) — C-DWM·6
- **F7** a day/week/month page **leaks nothing a feed page would not** (a
  draft / audience-restricted / deleted event is invisible to the same set of
  actors in every view) — C-DWM·2
- **F8** the `?view=` selector + window are **never an access input** (a
  `componentId` filter is still a filter, not a gate; the selector writes no
  row) — C-DWM·2, C-DWM·3

## The pinned tests (9 total — pinned in detail by U01)

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

**The three-test acceptance gate** (recorded by U07): **closed loop** (an
author's published event on the anchor day appears in **Day** *and* **Week**
*and* **Month**, each with the right chip/block + one aggregate row), **handoff**
(a group member added to the audience sees the event in all three views on the
next render — the non-member's views still exclude it), **part-vs-whole** (the
9 pinned tests pass together with the full M4 `EventServiceTests` + the existing
EV-CAL seam + Web pin suites still green — the lane is additive, nothing
regressed).

---

## Units (8 total)

> Each unit's full **Goal / Entry reads / Deliverables / Exit** lives in its
> own file: `docs/plans-milestones/in-progress/ev-dwm/U0#.md`. On completion the
> file moves to `docs/plans-milestones/done/ev-dwm/U0#.md` (see the handoff
> protocol above).

| Unit | Goal (one line) | File |
|------|-----------------|------|
| **U00** | Design doc Part 1 — context, scope, decisions D1–D7, invariants C-DWM·1…9, FACES F1–F8. No code. | `U00.md` |
| **U01** | Design doc Part 2 (exact view-model + controller + TS shapes, 9 pinned tests, gate, drift-guard) + **ADR 0064** + **roadmap open** (`EV-DWM` `StatusNext`, `M5`→`StatusPlanned`). | `U01.md` |
| **U02** | `EventCalendarViewModel` additive fields (`View`, `WindowDays`, `Label`) + the `Calendar` action's `?view=` engine (per-view window + nav-by-view-unit + label). Zero Core change. | `U02.md` |
| **U03** | **Month view** (the chip grid reframed to a true calendar month) + the **Day/Week/Month toggle** in the header. Month is the default. | `U03.md` |
| **U04** | **Week view** (7-column time grid, Monday-start) + the new `client/lib/events-calendar-time.ts` (hour ruler + time-positioned blocks + overlap). | `U04.md` |
| **U05** | **Day view** (1-column time grid reusing the time module) + the `site.css` time-grid + toggle rules + the 3 `kw-l` keys × 4 languages. | `U05.md` |
| **U06** | The **9 pinned tests** — 2 Core additive window pins + 7 Web `?view=` engine pins. No gate. | `U06.md` |
| **U07** | **Acceptance gate + close** — run + record the three-test gate; roadmap trio close (`EV-DWM`→`StatusDone`, `M5`→`StatusNext`); ARCHITECTURE.md sync; handoff summary; move the lane folder `in-progress/ev-dwm/` → `done/ev-dwm/`. | `U07.md` |
